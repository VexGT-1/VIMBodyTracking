using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

namespace VIMBodyTracking
{
    /// <summary>
    /// Reads SteamVR tracker poses every frame and applies them to the local
    /// GorillaTag avatar, mimicking the VIM subscription body-tracking feature.
    /// Supports: chest, hips, left foot, right foot.
    /// </summary>
    public class BodyTrackingManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────
        public static BodyTrackingManager Instance { get; private set; }

        // ── Tracker role assignment (set via MenuController) ─────────────────
        public int ChestTrackerIndex    = -1;
        public int HipTrackerIndex      = -1;
        public int LeftFootTrackerIndex = -1;
        public int RightFootTrackerIndex= -1;

        // ── Runtime state ────────────────────────────────────────────────────
        public bool TrackingActive => Plugin.CfgEnabled.Value && _steamVRReady;

        private bool _steamVRReady;

        // Smoothed world-space poses
        private Vector3    _chestPos,    _hipPos,    _leftFootPos,    _rightFootPos;
        private Quaternion _chestRot,    _hipRot,    _leftFootRot,    _rightFootRot;

        // References to gorilla avatar transforms (resolved at runtime)
        private Transform _avatarRoot;
        private Transform _spineBone;
        private Transform _hipBone;
        private Transform _leftFootBone;
        private Transform _rightFootBone;

        // Cache tracker serial → device index
        public List<TrackerDevice> DetectedTrackers { get; } = new List<TrackerDevice>();

        // ────────────────────────────────────────────────────────────────────

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            _steamVRReady = SteamVR.initializedState == SteamVR.InitializedStates.InitializeSuccess
                         || TryInitSteamVR();

            if (!_steamVRReady)
                Plugin.Log.LogWarning("SteamVR not initialised — body tracking inactive.");

            RefreshTrackerList();
        }

        void Update()
        {
            if (!TrackingActive) return;

            RefreshTrackerListPeriodic();
            ApplyTrackerPoses();
            DriveAvatar();
        }

        // ── SteamVR init helper ──────────────────────────────────────────────
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

        // ── Tracker discovery ─────────────────────────────────────────────────
        private float _trackerRefreshTimer;
        private const float TrackerRefreshInterval = 3f;

        private void RefreshTrackerListPeriodic()
        {
            _trackerRefreshTimer += Time.deltaTime;
            if (_trackerRefreshTimer >= TrackerRefreshInterval)
            {
                _trackerRefreshTimer = 0f;
                RefreshTrackerList();
            }
        }

        public void RefreshTrackerList()
        {
            DetectedTrackers.Clear();

            var system = OpenVR.System;
            if (system == null) return;

            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                var deviceClass = system.GetTrackedDeviceClass(i);
                if (deviceClass != ETrackedDeviceClass.GenericTracker) continue;

                var serial = GetStringProperty(system, i,
                    ETrackedDeviceProperty.Prop_SerialNumber_String);

                DetectedTrackers.Add(new TrackerDevice { Index = (int)i, Serial = serial });
            }

            Plugin.Log.LogInfo($"[BodyTracking] Found {DetectedTrackers.Count} tracker(s).");
        }

        public void AutoAssignTrackers()
        {
            if (DetectedTrackers.Count == 0) return;

            // Sort by Y position relative to HMD: highest = chest, middle = hip, lowest = feet
            var hmdPose = GetDevicePose(0); // HMD is always device 0
            float hmdY  = hmdPose.pos.y;

            var sorted = new List<TrackerDevice>(DetectedTrackers);
            sorted.Sort((a, b) =>
            {
                float ay = GetDevicePose((uint)a.Index).pos.y;
                float by_ = GetDevicePose((uint)b.Index).pos.y;
                return by_.CompareTo(ay); // descending
            });

            ChestTrackerIndex     = sorted.Count > 0 ? sorted[0].Index : -1;
            HipTrackerIndex       = sorted.Count > 1 ? sorted[1].Index : -1;

            // Feet: distinguish left/right by X position
            if (sorted.Count >= 4)
            {
                var foot1 = sorted[2];
                var foot2 = sorted[3];
                float x1  = GetDevicePose((uint)foot1.Index).pos.x;
                float x2  = GetDevicePose((uint)foot2.Index).pos.x;

                if (x1 < x2)   // negative X = left in VR space
                {
                    LeftFootTrackerIndex  = foot1.Index;
                    RightFootTrackerIndex = foot2.Index;
                }
                else
                {
                    LeftFootTrackerIndex  = foot2.Index;
                    RightFootTrackerIndex = foot1.Index;
                }
            }
            else if (sorted.Count == 3)
            {
                // Treat third tracker as hip fallback / single foot
                HipTrackerIndex = sorted.Count > 1 ? sorted[1].Index : -1;
            }

            Plugin.Log.LogInfo($"[AutoAssign] Chest={ChestTrackerIndex} Hip={HipTrackerIndex} " +
                               $"LFoot={LeftFootTrackerIndex} RFoot={RightFootTrackerIndex}");
        }

        // ── Pose reading ──────────────────────────────────────────────────────
        private void ApplyTrackerPoses()
        {
            float dt = Time.deltaTime;

            if (ChestTrackerIndex >= 0)
            {
                var raw = GetDevicePose((uint)ChestTrackerIndex);
                var offset = new Vector3(0, Plugin.CfgOffsetChestY.Value,
                                            Plugin.CfgOffsetChestZ.Value);
                float sp = Plugin.CfgSmoothingChest.Value;
                _chestPos = Vector3.Lerp(_chestPos, raw.pos + offset, sp * dt);
                _chestRot = Quaternion.Slerp(_chestRot, raw.rot, sp * dt);
            }

            if (HipTrackerIndex >= 0)
            {
                var raw = GetDevicePose((uint)HipTrackerIndex);
                var offset = new Vector3(0, Plugin.CfgOffsetHipY.Value,
                                            Plugin.CfgOffsetHipZ.Value);
                float sp = Plugin.CfgSmoothingHips.Value;
                _hipPos = Vector3.Lerp(_hipPos, raw.pos + offset, sp * dt);
                _hipRot = Quaternion.Slerp(_hipRot, raw.rot, sp * dt);
            }

            if (LeftFootTrackerIndex >= 0)
            {
                var raw = GetDevicePose((uint)LeftFootTrackerIndex);
                var offset = new Vector3(0, Plugin.CfgOffsetLeftFootY.Value, 0);
                float sp = Plugin.CfgSmoothingLegs.Value;
                _leftFootPos = Vector3.Lerp(_leftFootPos, raw.pos + offset, sp * dt);
                _leftFootRot = Quaternion.Slerp(_leftFootRot, raw.rot, sp * dt);
            }

            if (RightFootTrackerIndex >= 0)
            {
                var raw = GetDevicePose((uint)RightFootTrackerIndex);
                var offset = new Vector3(0, Plugin.CfgOffsetRightFootY.Value, 0);
                float sp = Plugin.CfgSmoothingLegs.Value;
                _rightFootPos = Vector3.Lerp(_rightFootPos, raw.pos + offset, sp * dt);
                _rightFootRot = Quaternion.Slerp(_rightFootRot, raw.rot, sp * dt);
            }
        }

        // ── Avatar driving ────────────────────────────────────────────────────
        private void DriveAvatar()
        {
            if (_avatarRoot == null) FindAvatarBones();
            if (_avatarRoot == null) return;

            if (_spineBone != null && ChestTrackerIndex >= 0)
            {
                _spineBone.position = _chestPos;
                _spineBone.rotation = _chestRot;
            }

            if (_hipBone != null && HipTrackerIndex >= 0)
            {
                _hipBone.position = _hipPos;
                _hipBone.rotation = _hipRot;
            }

            if (_leftFootBone != null && LeftFootTrackerIndex >= 0)
            {
                _leftFootBone.position = _leftFootPos;
                _leftFootBone.rotation = _leftFootRot;
            }

            if (_rightFootBone != null && RightFootTrackerIndex >= 0)
            {
                _rightFootBone.position = _rightFootPos;
                _rightFootBone.rotation = _rightFootRot;
            }
        }

        // ── Avatar bone lookup ────────────────────────────────────────────────
        private void FindAvatarBones()
        {
            // Gorilla Tag uses the local player object "Player Objects/Local VRRig"
            var rig = GameObject.Find("Local VRRig");
            if (rig == null) rig = GameObject.Find("Player Objects/Local VRRig");
            if (rig == null) return;

            _avatarRoot    = rig.transform;
            _spineBone     = FindChildRecursive(_avatarRoot, "spine");
            _hipBone       = FindChildRecursive(_avatarRoot, "hips");
            _leftFootBone  = FindChildRecursive(_avatarRoot, "leftFoot");
            _rightFootBone = FindChildRecursive(_avatarRoot, "rightFoot");

            Plugin.Log.LogInfo($"[BodyTracking] Avatar bones found: " +
                $"spine={_spineBone != null}, hip={_hipBone != null}, " +
                $"lFoot={_leftFootBone != null}, rFoot={_rightFootBone != null}");
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private static (Vector3 pos, Quaternion rot) GetDevicePose(uint deviceIndex)
        {
            var poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            OpenVR.System?.GetDeviceToAbsoluteTrackingPose(
                ETrackingUniverseOrigin.TrackingUniverseStanding, 0, poses);

            if (deviceIndex >= poses.Length || !poses[deviceIndex].bPoseIsValid)
                return (Vector3.zero, Quaternion.identity);

            var m = poses[deviceIndex].mDeviceToAbsoluteTracking;
            return (m.GetPosition(), m.GetRotation());
        }

        private static string GetStringProperty(CVRSystem sys, uint deviceIndex,
            ETrackedDeviceProperty prop)
        {
            var sb = new System.Text.StringBuilder(64);
            var err = ETrackedPropertyError.TrackedProp_Success;
            sys.GetStringTrackedDeviceProperty(deviceIndex, prop, sb, 64, ref err);
            return sb.ToString();
        }

        private static Transform FindChildRecursive(Transform parent, string nameLower)
        {
            foreach (Transform child in parent)
            {
                if (child.name.ToLower().Contains(nameLower))
                    return child;
                var found = FindChildRecursive(child, nameLower);
                if (found != null) return found;
            }
            return null;
        }
    }

    public class TrackerDevice
    {
        public int    Index;
        public string Serial;
        public string AssignedRole; // "Chest", "Hip", "LeftFoot", "RightFoot", "None"
    }
}
