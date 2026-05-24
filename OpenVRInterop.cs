//
// Minimal OpenVR interop stubs — enough to call tracker poses at runtime.
// The actual implementation lives in openvr_api.dll which ships with SteamVR.
//
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Valve.VR
{
    public enum EVRInitError { None = 0 }
    public enum EVRApplicationType { VRApplication_Overlay = 3 }
    public enum ETrackedDeviceClass { Invalid = 0, GenericTracker = 3 }
    public enum ETrackingUniverseOrigin { TrackingUniverseStanding = 1 }
    public enum ETrackedDeviceProperty { Prop_SerialNumber_String = 1000 }
    public enum ETrackedPropertyError { TrackedProp_Success = 0 }

    [StructLayout(LayoutKind.Sequential)]
    public struct HmdMatrix34_t
    {
        public float m0, m1, m2, m3;
        public float m4, m5, m6, m7;
        public float m8, m9, m10, m11;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TrackedDevicePose_t
    {
        public HmdMatrix34_t mDeviceToAbsoluteTracking;
        public HmdVector3_t vVelocity;
        public HmdVector3_t vAngularVelocity;
        public int eTrackingResult;
        [MarshalAs(UnmanagedType.I1)] public bool bPoseIsValid;
        [MarshalAs(UnmanagedType.I1)] public bool bDeviceIsConnected;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HmdVector3_t
    {
        public float v0, v1, v2;
    }

    public class CVRSystem
    {
        private IntPtr _ptr;
        public CVRSystem(IntPtr ptr) { _ptr = ptr; }

        public ETrackedDeviceClass GetTrackedDeviceClass(uint index)
        {
            return (ETrackedDeviceClass)NativeEntrypoints.VR_IVRSystem_GetTrackedDeviceClass(_ptr, index);
        }

        public uint GetStringTrackedDeviceProperty(uint index, ETrackedDeviceProperty prop,
            StringBuilder sb, uint bufSize, ref ETrackedPropertyError err)
        {
            return NativeEntrypoints.VR_IVRSystem_GetStringTrackedDeviceProperty(
                _ptr, index, (uint)prop, sb, bufSize, ref err);
        }

        public void GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin origin,
            float predictedSecondsToPhotonsFromNow, TrackedDevicePose_t[] poseArray)
        {
            NativeEntrypoints.VR_IVRSystem_GetDeviceToAbsoluteTrackingPose(
                _ptr, (uint)origin, predictedSecondsToPhotonsFromNow,
                poseArray, (uint)poseArray.Length);
        }
    }

    public static class OpenVR
    {
        public const uint k_unMaxTrackedDeviceCount = 64;
        private static CVRSystem _system;

        public static CVRSystem System => _system;

        public static CVRSystem Init(ref EVRInitError err, EVRApplicationType type)
        {
            IntPtr ptr = NativeEntrypoints.VR_Init(ref err, (uint)type);
            if (err == EVRInitError.None)
                _system = new CVRSystem(ptr);
            return _system;
        }

        public static void Shutdown()
        {
            NativeEntrypoints.VR_Shutdown();
            _system = null;
        }
    }

    internal static class NativeEntrypoints
    {
        private const string DLL = "openvr_api";

        [DllImport(DLL)] public static extern IntPtr VR_Init(ref EVRInitError err, uint type);
        [DllImport(DLL)] public static extern void VR_Shutdown();
        [DllImport(DLL)] public static extern uint VR_IVRSystem_GetTrackedDeviceClass(IntPtr ptr, uint idx);
        [DllImport(DLL)] public static extern uint VR_IVRSystem_GetStringTrackedDeviceProperty(
            IntPtr ptr, uint idx, uint prop, StringBuilder sb, uint bufSize, ref ETrackedPropertyError err);
        [DllImport(DLL)] public static extern void VR_IVRSystem_GetDeviceToAbsoluteTrackingPose(
            IntPtr ptr, uint origin, float seconds, TrackedDevicePose_t[] poses, uint count);
    }
}
