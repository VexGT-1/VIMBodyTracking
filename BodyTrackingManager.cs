using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

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

        private object _vrSystem;
        private MethodInfo _getClassMethod;
        private MethodInfo _getPoseMethod;
        private Type _poseElementType;
        private FieldInfo _poseValidField;
        private FieldInfo _poseMatField;
        private Type _matType;
        private bool _ready;

        void Awake() { Instance = this; }

        void Start()
        {
            // Wait 5 seconds for SteamVR to fully initialize before trying
            StartCoroutine(DelayedInit());
        }

        private IEnumerator DelayedInit()
        {
            Plugin.Log.LogInfo("Waiting 5s for SteamVR to initialize...");
            yield return new WaitForSeconds(5f);
            TryInit();
            RefreshTrackerList();
        }

        void Update()
        {
            if (!TrackingActive) return;
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= 3f)
            {
                _refreshTimer = 0f;
                if (!_ready) TryInit();
                RefreshTrackerList();
            }
            ApplyChestPose();
            DriveAvatar();
        }

        private void TryInit()
        {
            try
            {
                Type openVRType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    openVRType = asm.GetType("Valve.VR.OpenVR");
                    if (openVRType != null) break;
                }

                if (openVRType == null) { Plugin.Log.LogWarning("Valve.VR.OpenVR not found."); return; }

                var sysProp = openVRType.GetProperty("System", BindingFlags.Public | BindingFlags.Static);
                if (sysProp == null) { Plugin.Log.LogWarning("OpenVR.System property not found."); return; }

                _vrSystem = sysProp.GetValue(null);
                if (_vrSystem == null) { Plugin.Log.LogWarning("OpenVR.System still null."); return; }

                Plugin.Log.LogInfo($"Got OpenVR.System: {_vrSystem.GetType().FullName}");

                var sysType = _vrSystem.GetType();
                _getClassMethod = sysType.GetMethod("GetTrackedDeviceClass");
                _getPoseMethod = sysType.GetMethod("GetDeviceToAbsoluteTrackingPose");

                if (_getPoseMethod != null)
                {
                    _poseElementType = _getPoseMethod.GetParameters()[2].ParameterType.GetElementType();
                    _poseValidField = _poseElementType.GetField("bPoseIsValid");
                    _poseMatField = _poseElementType.GetField("mDeviceToAbsoluteTracking");
                    if (_poseMatField != null) _matType = _poseMatField.FieldType;
                }

                _ready = _getClassMethod != null && _getPoseMethod != null;
                Plugin.Log.LogInfo($"SteamVR ready: {_ready}");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"TryInit error: {e.Message}"); }
        }

        public void RefreshTrackerList()
        {
            DetectedTrackers.Clear();
            if (!_ready) return;

            try
            {
                for (uint i = 0; i < 64; i++)
                {
                    var cls = Convert.ToInt32(_getClassMethod.Invoke(_vrSystem, new object[] { i }));
                    if (cls != 3) continue;
                    DetectedTrackers.Add(new TrackerDevice { Index = (int)i, Serial = $"Tracker_{i}" });
                    Plugin.Log.LogInfo($"Tracker found: [{i}]");
                }
                Plugin.Log.LogInfo($"Total trackers: {DetectedTrackers.Count}");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"RefreshTrackerList error: {e.Message}"); }
        }

        public void AutoAssignTracker()
        {
            if (DetectedTrackers.Count == 0) return;
            ChestTrackerIndex = 0;
        }

        private void ApplyChestPose()
        {
            if (ChestTrackerIndex < 0 || ChestTrackerIndex >= DetectedTrackers.Count) return;
            if (!_ready) return;

            try
            {
                var poses = Array.CreateInstance(_poseElementType, 64);
                _getPoseMethod.Invoke(_vrSystem, new object[] { 1, 0f, poses });

                uint idx = (uint)DetectedTrackers[ChestTrackerIndex].Index;
                var pose = poses.GetValue((int)idx);
                if (pose == null) return;

                bool valid = (bool)_poseValidField.GetValue(pose);
                if (!valid) return;

                var mat34 = _poseMatField.GetValue(pose);
                float G(string n) => (float)_matType.GetField(n).GetValue(mat34);

                var rawPos = new Vector3(G("m3"), G("m7"), -G("m11"));
                var mat = new Matrix4x4();
                mat[0,0]= G("m0"); mat[0,1]= G("m1"); mat[0,2]=-G("m2"); mat[0,3]= G("m3");
                mat[1,0]= G("m4"); mat[1,1]= G("m5"); mat[1,2]=-G("m6"); mat[1,3]= G("m7");
                mat[2,0]=-G("m8"); mat[2,1]=-G("m9"); mat[2,2]= G("m10"); mat[2,3]=-G("m11");
                mat[3,0]=0f; mat[3,1]=0f; mat[3,2]=0f; mat[3,3]=1f;

                var offset = new Vector3(0, Plugin.CfgOffsetChestY.Value, Plugin.CfgOffsetChestZ.Value);
                float sp = Plugin.CfgSmoothingChest.Value * Time.deltaTime;
                _chestPos = Vector3.Lerp(_chestPos, rawPos + offset, sp);
                _chestRot = Quaternion.Slerp(_chestRot, mat.rotation, sp);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"ApplyChestPose error: {e.Message}"); }
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
