using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Valve.VR
{
    public enum EVRInitError { None = 0 }
    public enum EVRApplicationType { VRApplication_Overlay = 3 }
    public enum ETrackedDeviceClass { Invalid = 0, GenericTracker = 3 }
    public enum ETrackingUniverseOrigin { TrackingUniverseStanding = 1 }
    public enum ETrackedDeviceProperty { Prop_SerialNumber_String = 1000 }
    public enum ETrackedPropertyError { TrackedProp_Success = 0 }

    [StructLayout(LayoutKind.Sequential)]
    public struct HmdMatrix34_t { public float m0,m1,m2,m3,m4,m5,m6,m7,m8,m9,m10,m11; }

    [StructLayout(LayoutKind.Sequential)]
    public struct TrackedDevicePose_t
    {
        public HmdMatrix34_t mDeviceToAbsoluteTracking;
        public float vv0,vv1,vv2,va0,va1,va2;
        public int eTrackingResult;
        [MarshalAs(UnmanagedType.I1)] public bool bPoseIsValid;
        [MarshalAs(UnmanagedType.I1)] public bool bDeviceIsConnected;
    }

    public static class OpenVR
    {
        public const uint k_unMaxTrackedDeviceCount = 64;
        private static IntPtr _systemPtr = IntPtr.Zero;
        public static bool IsInitialized => _systemPtr != IntPtr.Zero;

        public static void Init(ref EVRInitError err, EVRApplicationType type)
        {
            try
            {
                _systemPtr = NativeEntrypoints.VR_Init(ref err, (uint)type);
            }
            catch (Exception e)
            {
                err = EVRInitError.None;
                VIMBodyTracking.Plugin.Log.LogWarning($"VR_Init exception: {e.Message}");
            }
        }

        public static ETrackedDeviceClass GetTrackedDeviceClass(uint i)
        {
            if (_systemPtr == IntPtr.Zero) return ETrackedDeviceClass.Invalid;
            try { return (ETrackedDeviceClass)NativeEntrypoints.VR_IVRSystem_GetTrackedDeviceClass(_systemPtr, i); }
            catch { return ETrackedDeviceClass.Invalid; }
        }

        public static void GetStringTrackedDeviceProperty(uint i, ETrackedDeviceProperty prop,
            StringBuilder sb, uint size, ref ETrackedPropertyError err)
        {
            if (_systemPtr == IntPtr.Zero) return;
            try { NativeEntrypoints.VR_IVRSystem_GetStringTrackedDeviceProperty(_systemPtr, i, (uint)prop, sb, size, ref err); }
            catch { }
        }

        public static void GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin origin,
            float seconds, TrackedDevicePose_t[] poses)
        {
            if (_systemPtr == IntPtr.Zero) return;
            try { NativeEntrypoints.VR_IVRSystem_GetDeviceToAbsoluteTrackingPose(_systemPtr, (uint)origin, seconds, poses, (uint)poses.Length); }
            catch { }
        }
    }

    internal static class NativeEntrypoints
    {
        private const string DLL = "openvr_api";
        [DllImport(DLL)] public static extern IntPtr VR_Init(ref EVRInitError err, uint type);
        [DllImport(DLL)] public static extern uint VR_IVRSystem_GetTrackedDeviceClass(IntPtr ptr, uint idx);
        [DllImport(DLL)] public static extern void VR_IVRSystem_GetStringTrackedDeviceProperty(IntPtr ptr, uint idx, uint prop, StringBuilder sb, uint size, ref ETrackedPropertyError err);
        [DllImport(DLL)] public static extern void VR_IVRSystem_GetDeviceToAbsoluteTrackingPose(IntPtr ptr, uint origin, float seconds, TrackedDevicePose_t[] poses, uint count);
    }
}

namespace VIMBodyTracking
{
    using Valve.VR;

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

        void Start()
        {
            TryInitOpenVR();
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

        private void TryInitOpenVR()
        {
            try
            {
                if (OpenVR.IsInitialized) return;
                var err = EVRInitError.None;
                OpenVR.Init(ref err, EVRApplicationType.VRApplication_Overlay);
                Plugin.Log.LogInfo($"OpenVR init result: {err}, initialized: {OpenVR.IsInitialized}");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"OpenVR init failed: {e.Message}"); }
        }

        public void RefreshTrackerList()
        {
            DetectedTrackers.Clear();

            if (!OpenVR.IsInitialized)
            {
                TryInitOpenVR();
                if (!OpenVR.IsInitialized)
                {
                    Plugin.Log.LogWarning("OpenVR not initialized.");
                    return;
                }
            }

            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                if (OpenVR.GetTrackedDeviceClass(i) != ETrackedDeviceClass.GenericTracker) continue;
                var sb = new StringBuilder(64);
                var err = ETrackedPropertyError.TrackedProp_Success;
                OpenVR.GetStringTrackedDeviceProperty(i, ETrackedDeviceProperty.Prop_SerialNumber_String, sb, 64, ref err);
                DetectedTrackers.Add(new TrackerDevice { Index = (int)i, Serial = sb.ToString() });
                Plugin.Log.LogInfo($"Tracker: [{i}] {sb}");
            }
            Plugin.Log.LogInfo($"Total trackers: {DetectedTrackers.Count}");
        }

        public void AutoAssignTracker()
        {
            if (DetectedTrackers.Count == 0) return;
            ChestTrackerIndex = 0;
        }

        private void ApplyChestPose()
        {
            if (ChestTrackerIndex < 0 || ChestTrackerIndex >= DetectedTrackers.Count) return;
            if (!OpenVR.IsInitialized) return;

            var poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            OpenVR.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, poses);

            uint idx = (uint)DetectedTrackers[ChestTrackerIndex].Index;
            if (!poses[idx].bPoseIsValid) return;

            var m = poses[idx].mDeviceToAbsoluteTracking;
            var rawPos = new Vector3(m.m3, m.m7, -m.m11);
            var mat = new Matrix4x4();
            mat[0,0]= m.m0; mat[0,1]= m.m1; mat[0,2]=-m.m2; mat[0,3]= m.m3;
            mat[1,0]= m.m4; mat[1,1]= m.m5; mat[1,2]=-m.m6; mat[1,3]= m.m7;
            mat[2,0]=-m.m8; mat[2,1]=-m.m9; mat[2,2]= m.m10; mat[2,3]=-m.m11;
            mat[3,0]=0f; mat[3,1]=0f; mat[3,2]=0f; mat[3,3]=1f;

            var offset = new Vector3(0, Plugin.CfgOffsetChestY.Value, Plugin.CfgOffsetChestZ.Value);
            float sp = Plugin.CfgSmoothingChest.Value * Time.deltaTime;
            _chestPos = Vector3.Lerp(_chestPos, rawPos + offset, sp);
            _chestRot = Quaternion.Slerp(_chestRot, mat.rotation, sp);
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
