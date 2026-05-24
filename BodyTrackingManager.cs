using System.Collections.Generic;
using UnityEngine;
using OpenVR.NET;
using OpenVR.NET.Manifests;

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

        private VR _vr;
        private float _refreshTimer;

        void Awake() { Instance = this; }

        void Start()
        {
            try
            {
                _vr = new VR();
                _vr.Initialize();
                Plugin.Log.LogInfo("OpenVR.NET initialized.");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"OpenVR init failed: {e.Message}");
            }
            RefreshTrackerList();
        }

        void Update()
        {
            if (!TrackingActive) return;
            _vr?.Update();
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= 3f) { _refreshTimer = 0f; RefreshTrackerList(); }
            ApplyChestPose();
            DriveAvatar();
        }

        public void RefreshTrackerList()
        {
            DetectedTrackers.Clear();
            if (_vr == null) return;

            int index = 0;
            foreach (var device in _vr.TrackedDevices)
            {
                if (device.DeviceType == DeviceType.GenericTracker)
                {
                    DetectedTrackers.Add(new TrackerDevice
                    {
                        Index = index,
                        Serial = device.Serial ?? $"Tracker {index}",
                        Device = device
                    });
                    index++;
                }
            }
            Plugin.Log.LogInfo($"Found {DetectedTrackers.Count} tracker(s).");
        }

        public void AutoAssignTracker()
        {
            if (DetectedTrackers.Count == 0) return;
            ChestTrackerIndex = 0;
        }

        private void ApplyChestPose()
        {
            if (ChestTrackerIndex < 0 || ChestTrackerIndex >= DetectedTrackers.Count) return;
            var device = DetectedTrackers[ChestTrackerIndex].Device;
            if (device == null) return;

            var pose = device.RenderPose;
            var rawPos = new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z);
            var rawRot = new Quaternion(pose.Orientation.X, pose.Orientation.Y, pose.Orientation.Z, pose.Orientation.W);

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
        public object Device;
    }
}
