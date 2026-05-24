using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

namespace VIMBodyTracking
{
    public class BodyTrackingManager : MonoBehaviour
    {
        public static BodyTrackingManager Instance { get; private set; }
        public int ChestTrackerIndex = -1;
        public bool TrackingActive => Plugin.CfgEnabled.Value && _steamVRReady;
        private bool _steamVRReady;
        private Vector3 _chestPos;
        private Quaternion _chestRot = Quaternion.identity;
        private Transform _avatarRoot;
        private Transform _bodyBone;
        public List<TrackerDevice> DetectedTrackers { get; } = new List<TrackerDevice>();

        void Awake() { Instance = this; }

        void Start()
        {
            _steamVRReady = SteamVR.initializedState == SteamVR.InitializedStates.InitializeSuccess || TryInitSteamVR();
            if (!_steamVRReady) Plugin.Log.LogWarning("SteamVR not ready.");
            RefreshTrackerList();
        }

        void Update()
        {
            if (!TrackingActive) return;
            RefreshTrackerListPeriodic();
            ApplyChestPose();
            DriveAvatar();
        }

        private bool TryInitSteamVR()
        {
            try
            {
                var err = EVRInitError.None;
                OpenVR.Init(ref err, EVRApplicationType.VRApplication_Other);
                return err == EVRInitError.None;
            }
            catch { return false; }
        }

        private float _refreshTimer;
        private const float RefreshInterval = 3f;

        private void RefreshTrackerListPeriodic()
        {
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= RefreshInterval) { _refreshTimer = 0f; RefreshTrackerList(); }
        }

        public void RefreshTrackerList()
        {
            DetectedTrackers.Clear();
            var system = OpenVR.System;
            if (system == null) return;
            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                if (system.GetTrackedDeviceClass(i) != ETrackedDeviceClass.GenericTracker) continue;
                var serial = GetStringProperty(system, i, ETrackedDeviceProperty.Prop_SerialNumber_String);
                DetectedTrackers.Add(new TrackerDevice { Index = (int)i, Serial = serial });
            }
            Plugin.Log.LogInfo($"Found {DetectedTrackers.Count} tracker(s).");
        }

        public void AutoAssignTracker()
        {
            if (DetectedTrackers.Count == 0) return;
            ChestTrackerIndex = DetectedTrackers[0].Index;
        }

        private void ApplyChestPose()
        {
            if (ChestTrackerIndex < 0) return;
            var raw = GetDevicePose((uint)ChestTrackerIndex);
            var offset = new Vector3(0, Plugin.CfgOffsetChestY.Value, Plugin.CfgOffsetChestZ.Value);
            float sp = Plugin.CfgSmoothingChest.Value;
            float dt = Time.deltaTime;
            _chestPos = Vector3.Lerp(_chestPos, raw.pos + offset, sp * dt);
            _chestRot = Quaternion.Slerp(_chestRot, raw.rot, sp * dt);
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
            Plugin.Log.LogInfo($"Body bone: {_bodyBone?.name ?? "not found"}");
        }

        private static (Vector3 pos, Quaternion rot) GetDevicePose(uint index)
        {
            var poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            OpenVR.System?.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, poses);
            if (index >= poses.Length || !poses[index].bPoseIsValid) return (Vector3.zero, Quaternion.identity);
            var m = poses[index].mDeviceToAbsoluteTracking;
            return (m.GetPosition(), m.GetRotation());
        }

        private static string GetStringProperty(CVRSystem sys, uint index, ETrackedDeviceProperty prop)
        {
            var sb = new System.Text.StringBuilder(64);
            var err = ETrackedPropertyError.TrackedProp_Success;
            sys.GetStringTrackedDeviceProperty(index, prop, sb, 64, ref err);
            return sb.ToString();
        }

        private static Transform FindChildRecursive(Transform parent, string nameLower)
        {
            foreach (Transform child in parent)
            {
                if (child.name.ToLower().Contains(nameLower)) return child;
                var found = FindChildRecursive(child, nameLower);
                if (found != null) return found;
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
