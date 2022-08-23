using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace MyRenderer.Shaders
{
    public static class Common
    {
        public static int GetIndex(int x, int y, int width)
        {
            return x + width * y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ComputeBarycentric2D(float x, float y, NativeArray<float3> verts)
        {
            float3 result;
            result.x =
                (x * (verts[1].y - verts[2].y) + (verts[2].x - verts[1].x) * y + verts[1].x * verts[2].y -
                 verts[2].x * verts[1].y) / (verts[0].x * (verts[1].y - verts[2].y) +
                    (verts[2].x - verts[1].x) * verts[0].y + verts[1].x * verts[2].y - verts[2].x * verts[1].y);
            result.y =
                (x * (verts[2].y - verts[0].y) + (verts[0].x - verts[2].x) * y + verts[2].x * verts[0].y -
                 verts[0].x * verts[2].y) / (verts[1].x * (verts[2].y - verts[0].y) +
                    (verts[0].x - verts[2].x) * verts[1].y + verts[2].x * verts[0].y - verts[0].x * verts[2].y);
            result.z =
                (x * (verts[0].y - verts[1].y) + (verts[1].x - verts[0].x) * y + verts[0].x * verts[1].y -
                 verts[1].x * verts[0].y) / (verts[2].x * (verts[0].y - verts[1].y) +
                    (verts[1].x - verts[0].x) * verts[2].y + verts[0].x * verts[1].y - verts[1].x * verts[0].y);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Clipped(NativeArray<float3> v)
        {
            if (v[0].x < -1 && v[1].x < -1 && v[2].x < -1) return true;
            if (v[0].y < -1 && v[1].y < -1 && v[2].y < -1) return true;
            if (v[0].z < -1 && v[1].z < -1 && v[2].z < -1) return true;
            if (v[0].x > 1 && v[1].x > 1 && v[2].x > 1) return true;
            if (v[0].y > 1 && v[1].y > 1 && v[2].y > 1) return true;
            if (v[0].z > 1 && v[1].z > 1 && v[2].z > 1) return true;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Backface(NativeArray<float3> v)
        {
            float3 v01 = v[1] - v[0];
            float3 v02 = v[2] - v[1];
            float3 normal = math.cross(v01, v02);
            return normal.z < 0;
        }
    }
}