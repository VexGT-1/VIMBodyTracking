using System.Collections.Generic;
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

        public List<InputDevice> DetectedTrackers { get; } = new List<InputDevice>();

        void Awake() { Instance = this; }
        void Start() { RefreshTrackerList(); }

        void Update()
        {
            if (!TrackingActive) return;
            RefreshTrackerListPeriodic();
            ApplyChestPose();
            DriveAvatar();
        }

        private float _refreshTimer;

        private void RefreshTrackerListPeriodic()
        {
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= 3f) { _refreshTimer = 0f; RefreshTrackerList(); }
        }

        public void RefreshTrackerList()
        {
            DetectedTrackers.Clear();
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.TrackedDevice, devices);
            foreach (var d in devices)
                if (!d.characteristics.HasFlag(InputDeviceCharacteristics.HeadMounted) &&
                    !d.characteristics.HasFlag(InputDeviceCharacteristics.Controller))
                    DetectedTrackers.Add(d);
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
            var device = DetectedTrackers[ChestTrackerIndex];

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

        private static Transform FindChildRecursive(Transform parent, string nameLower)
        {
            foreach (Transform child in parent)
            {
                if (child.name.ToLower().Contains(nameLower)) return child;
                var f = FindChildRecursive(child, nameLower);
                if (f != null) return f;
            }
            return null;
        }
    }
}
