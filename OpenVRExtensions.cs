using UnityEngine;
using Valve.VR;

namespace VIMBodyTracking
{
    /// <summary>
    /// Extension helpers to convert OpenVR's HmdMatrix34_t into Unity Vector3/Quaternion.
    /// </summary>
    public static class OpenVRExtensions
    {
        public static Vector3 GetPosition(this HmdMatrix34_t m)
        {
            return new Vector3(m.m3, m.m7, -m.m11); // right-handed → left-handed
        }

        public static Quaternion GetRotation(this HmdMatrix34_t m)
        {
            // Build a 4x4 then extract quaternion
            var mat = new Matrix4x4();
            mat[0, 0] =  m.m0;  mat[0, 1] =  m.m1;  mat[0, 2] = -m.m2;  mat[0, 3] =  m.m3;
            mat[1, 0] =  m.m4;  mat[1, 1] =  m.m5;  mat[1, 2] = -m.m6;  mat[1, 3] =  m.m7;
            mat[2, 0] = -m.m8;  mat[2, 1] = -m.m9;  mat[2, 2] =  m.m10; mat[2, 3] = -m.m11;
            mat[3, 0] =  0f;    mat[3, 1] =  0f;    mat[3, 2] =  0f;    mat[3, 3] =  1f;

            return mat.rotation;
        }
    }
}
