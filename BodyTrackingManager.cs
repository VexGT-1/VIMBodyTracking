using System;
using System.Collections.Generic;
using System.Text;
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

        void Awake() { Instance = this; }
        void Start() { RefreshTrackerList(); }

        void Update()
        {
            if (!TrackingActive) return;
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= 3f) { _refreshTimer = 0f; RefreshTrackerList(); }
            ApplyChestPose();
            DriveAvatar();
        }

        public void RefreshTrackerList()
        {
            DetectedTrackers.Clear();

            // Get ALL tracked devices and log them so we can see what's available
            var all = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.TrackedDevice, all);

            Plugin.Log.LogInfo($"All tracked devices ({all.Count}):");
            foreach (var d in all)
                Plugin.Log.LogInfo($"  name={d.name} characteristics={d.characteristics}");

            // Add anything that isn't HMD or controller
            int index = 0;
            foreach (var d in all)
            {
                bool isHMD = d.characteristics.HasFlag(InputDeviceCharacteristics.HeadMounted);
                bool isController = d.characteristics.HasFlag(InputDeviceCharacteristics.Controller);
                if (!isHMD && !isController)
                {
                    DetectedTrackers.Add(new TrackerDevice
                    {
                        Index = index,
                        Serial = string.IsNullOrEmpty(d.name) ? $"Tracker {index}" : d.name,
                        Device = d
                    });
                    index++;
                }
            }
            Plugin.Log.LogInfo($"Trackers found: {DetectedTrackers.Count}");
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

            Vector3 pos; Quaternion rot;
            device.TryGetFeatureValue(CommonUsages.devicePosition, out pos);
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out rot);

            var offset = new Vector3(0, Plugin.CfgOffsetChestY.Value, Plugin.CfgOffsetChestZ.Value);
            float sp = Plugin.CfgSmoothingChest.Value * Time.deltaTime;
            _chestPos = Vector3.Lerp(_chestPos, pos + offset, sp);
            _chestRot = Quaternion.Slerp(_chestRot, rot, sp);
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
        public InputDevice Device;
    }
}
