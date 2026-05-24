using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

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

        void Awake() { Instance = this; }

        void Start()
        {
            TryInitSteamVR();
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

        private float _refreshTimer;

        private void TryInitSteamVR()
        {
            try
            {
                if (OpenVR.System == null)
                {
                    var err = EVRInitError.None;
                    OpenVR.Init(ref err, EVRApplicationType.VRApplication_Overlay);
                    if (err != EVRInitError.None)
                        Plugin.Log.LogWarning($"SteamVR init error: {err}");
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"SteamVR init failed: {e.Message}");
            }
        }

        public void RefreshTrackerList()
        {
            DetectedTrackers.Clear();
            var system = OpenVR.System;
            if (system == null) { Plugin.Log.LogWarning("OpenVR.System is null."); return; }

            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                if (system.GetTrackedDeviceClass(i) != ETrackedDeviceClass.GenericTracker) continue;
                var sb = new System.Text.StringBuilder(64);
                var err = ETrackedPropertyError.TrackedProp_Success;
                system.GetStringTrackedDeviceProperty(i, ETrackedDeviceProperty.Prop_SerialNumber_String, sb, 64, ref err);
                DetectedTrackers.Add(new TrackerDevice { Index = (int)i, Serial = sb.ToString() });
            }
            Plugin.Log.LogInfo($"Found {DetectedTrackers.Count} tracker(s).");
        }

        public void AutoAssignTracker()
        {
            if (DetectedTrackers.Count == 0) return;
            ChestTrackerIndex = 0;
            Plugin.Log.LogInfo("Auto-assigned tracker 0 as chest.");
        }

        private void ApplyChestPose()
        {
            if (ChestTrackerIndex < 0 || ChestTrackerIndex >= DetectedTrackers.Count) return;

            var poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            OpenVR.System?.GetDeviceToAbsoluteTrackingPose(
                ETrackingUniverseOrigin.TrackingUniverseStanding, 0, poses);

            uint idx = (uint)DetectedTrackers[ChestTrackerIndex].Index;
            if (idx >= poses.Length || !poses[idx].bPoseIsValid) return;

            var m = poses[idx].mDeviceToAbsoluteTracking;
            var rawPos = new Vector3(m.m3, m.m7, -m.m11);
            var mat = new Matrix4x4();
            mat[0,0]= m.m0; mat[0,1]= m.m1; mat[0,2]=-m.m2; mat[0,3]= m.m3;
            mat[1,0]= m.m4; mat[1,1]= m.m5; mat[1,2]=-m.m6; mat[1,3]= m.m7;
            mat[2,0]=-m.m8; mat[2,1]=-m.m9; mat[2,2]= m.m10; mat[2,3]=-m.m11;
            mat[3,0]=0f;    mat[3,1]=0f;    mat[3,2]=0f;     mat[3,3]=1f;
            var rawRot = mat.rotation;

            var offset = new Vector3(0, Plugin.CfgOffsetChestY.Value, Plugin.CfgOffsetChestZ.Value);
            float sp = Plugin.CfgSmoothingChest.Value * Time.deltaTime;
            _chestPos = Vector3.Lerp(_chestPos, rawPos + offset, sp);
            _chestRot = Quaternion.Slerp(_chestRot, rawRot, sp);
        }

        private void DriveAvatar()
        {
            if (_avatarRoot == null) FindAvatarBones();
            if (_bodyBone == null || ChestTrackerIndex < 0) return;
            _bodyBone.rotation = _chestRot;
        }

        private void FindAvatarBones()
        {
            var rig = GameObject.Find("Local VRRig")
                   ?? GameObject.Find("Player Objects/Local VRRig");
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
