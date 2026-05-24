using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR;

namespace VIMBodyTracking
{
    public class BodyTrackingManager : MonoBehaviour
    {
        public static BodyTrackingManager Instance { get; private set; }
        public int ChestTrackerIndex = -1;
        public bool TrackingActive => Plugin.CfgEnabled.Value;

        private Quaternion _chestRot = Quaternion.identity;
        private Vector3 _chestPos = Vector3.zero;
        private Transform _avatarRoot;
        private Transform _bodyBone;
        public List<TrackerDevice> DetectedTrackers { get; } = new List<TrackerDevice>();
        private float _refreshTimer;

        // SteamVR reflection
        private object _steamVRInstance;
        private Type _steamVRType;
        private bool _steamVRReady;

        void Awake() { Instance = this; }

        void Start()
        {
            TryGetSteamVR();
            RefreshTrackerList();
        }

        void Update()
        {
            if (!TrackingActive) return;
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= 3f) { _refreshTimer = 0f; RefreshTrackerList(); }
            ApplyChestPose();
            DriveAvatar();
        }

        private void TryGetSteamVR()
        {
            try
            {
                // Find SteamVR type in loaded assemblies
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    _steamVRType = asm.GetType("Valve.VR.OpenVR");
                    if (_steamVRType != null) break;
                }

                if (_steamVRType == null)
                {
                    Plugin.Log.LogWarning("Could not find Valve.VR.OpenVR type.");
                    return;
                }

                // Get the System property
                var sysProp = _steamVRType.GetProperty("System",
                    BindingFlags.Public | BindingFlags.Static);
                if (sysProp == null)
                {
                    Plugin.Log.LogWarning("Could not find OpenVR.System property.");
                    return;
                }

                _steamVRInstance = sysProp.GetValue(null);
                _steamVRReady = _steamVRInstance != null;
                Plugin.Log.LogInfo($"SteamVR via reflection: {(_steamVRReady ? "ready" : "null")}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"SteamVR reflection failed: {e.Message}");
            }
        }

        public void RefreshTrackerList()
        {
            DetectedTrackers.Clear();

            if (!_steamVRReady) TryGetSteamVR();
            if (!_steamVRReady) { Plugin.Log.LogWarning("SteamVR not ready."); return; }

            try
            {
                var sysType = _steamVRInstance.GetType();

                // GetTrackedDeviceClass method
                var getClassMethod = sysType.GetMethod("GetTrackedDeviceClass");
                if (getClassMethod == null) { Plugin.Log.LogWarning("GetTrackedDeviceClass not found."); return; }

                for (uint i = 0; i < 64; i++)
                {
                    var deviceClass = getClassMethod.Invoke(_steamVRInstance, new object[] { i });
                    int classInt = Convert.ToInt32(deviceClass);
                    // GenericTracker = 3
                    if (classInt != 3) continue;

                    DetectedTrackers.Add(new TrackerDevice { Index = (int)i, Serial = $"Tracker_{i}" });
                    Plugin.Log.LogInfo($"Tracker found at index {i}");
                }

                Plugin.Log.LogInfo($"Total trackers: {DetectedTrackers.Count}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"RefreshTrackerList error: {e.Message}");
            }
        }

        public void AutoAssignTracker()
        {
            if (DetectedTrackers.Count == 0) return;
            ChestTrackerIndex = 0;
        }

        private void ApplyChestPose()
        {
            if (ChestTrackerIndex < 0 || ChestTrackerIndex >= DetectedTrackers.Count) return;
            if (!_steamVRReady) return;

            try
            {
                var sysType = _steamVRInstance.GetType();
                var getPoseMethod = sysType.GetMethod("GetDeviceToAbsoluteTrackingPose");
                if (getPoseMethod == null) return;

                // Create pose array via reflection
                var poseArrayType = getPoseMethod.GetParameters()[2].ParameterType;
                var poses = Array.CreateInstance(poseArrayType.GetElementType(), 64);
                getPoseMethod.Invoke(_steamVRInstance, new object[] { 1, 0f, poses });

                uint idx = (uint)DetectedTrackers[ChestTrackerIndex].Index;
                var pose = poses.GetValue((int)idx);
                if (pose == null) return;

                var poseType = pose.GetType();
                var validField = poseType.GetField("bPoseIsValid");
                if (validField == null) return;
                bool valid = (bool)validField.GetValue(pose);
                if (!valid) return;

                var matField = poseType.GetField("mDeviceToAbsoluteTracking");
                if (matField == null) return;
                var mat34 = matField.GetValue(pose);
                var matType = mat34.GetType();

                float Get(string name) => (float)matType.GetField(name).GetValue(mat34);

                var rawPos = new Vector3(Get("m3"), Get("m7"), -Get("m11"));
                var mat = new Matrix4x4();
                mat[0,0]= Get("m0"); mat[0,1]= Get("m1"); mat[0,2]=-Get("m2"); mat[0,3]= Get("m3");
                mat[1,0]= Get("m4"); mat[1,1]= Get("m5"); mat[1,2]=-Get("m6"); mat[1,3]= Get("m7");
                mat[2,0]=-Get("m8"); mat[2,1]=-Get("m9"); mat[2,2]= Get("m10"); mat[2,3]=-Get("m11");
                mat[3,0]=0f; mat[3,1]=0f; mat[3,2]=0f; mat[3,3]=1f;

                var offset = new Vector3(0, Plugin.CfgOffsetChestY.Value, Plugin.CfgOffsetChestZ.Value);
                float sp = Plugin.CfgSmoothingChest.Value * Time.deltaTime;
                _chestPos = Vector3.Lerp(_chestPos, rawPos + offset, sp);
                _chestRot = Quaternion.Slerp(_chestRot, mat.rotation, sp);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"ApplyChestPose error: {e.Message}");
            }
        }

        private void DriveAvatar()
        {
            if (_avatarRoot == null) FindAvatarBones();
            if (_bodyBone == null || ChestTrackerIndex < 0) return;
            _bodyBone.rotation = _chestRot;
        }

        private void FindAvatarBones()
        {
            var rig = GameObject.Find("Local VRRig") ?? GameObject.Find("Player Objects/Local VRRig");
            if (rig == null) return;
            _avatarRoot = rig.transform;
            _bodyBone = FindChildRecursive(_avatarRoot, "body")
                     ?? FindChildRecursive(_avatarRoot, "chest")
                     ?? FindChildRecursive(_avatarRoot, "spine")
                     ?? _avatarRoot;
            Plugin.Log.LogInfo($"Body bone: {_bodyBone?.name ?? "none"}");
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.ToLower().Contains(name)) return child;
                var f = FindChildRecursive(child, name);
                if (f != null) return f;
            }
            return null;
        }
    }

    public class TrackerDevice
    {
        public int Index;
        public string Serial;
    }
}
