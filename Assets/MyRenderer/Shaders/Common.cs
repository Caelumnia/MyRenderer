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
        public static float3 ComputeBarycentric2D(float x, float y, float3 v0, float3 v1, float3 v2)
        {
            float3 result;
            result.x =
                (x * (v1.y - v2.y) + (v2.x - v1.x) * y + v1.x * v2.y -
                 v2.x * v1.y) / (v0.x * (v1.y - v2.y) +
                    (v2.x - v1.x) * v0.y + v1.x * v2.y - v2.x * v1.y);
            result.y =
                (x * (v2.y - v0.y) + (v0.x - v2.x) * y + v2.x * v0.y -
                 v0.x * v2.y) / (v1.x * (v2.y - v0.y) +
                    (v0.x - v2.x) * v1.y + v2.x * v0.y - v0.x * v2.y);
            result.z =
                (x * (v0.y - v1.y) + (v1.x - v0.x) * y + v0.x * v1.y -
                 v1.x * v0.y) / (v2.x * (v0.y - v1.y) +
                    (v1.x - v0.x) * v2.y + v0.x * v1.y - v1.x * v0.y);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Clipped(float4 v0, float4 v1, float4 v2)
        {
            var w0 = math.abs(v0.w);
            var w1 = math.abs(v1.w);
            var w2 = math.abs(v2.w);

            if (v0.x < -w0 && v1.x < -w1 && v2.x < -w2) return true;
            if (v0.y < -w0 && v1.y < -w1 && v2.y < -w2) return true;
            if (v0.z < -w0 && v1.z < -w1 && v2.z < -w2) return true;
            if (v0.x > w0 && v1.x > w1 && v2.x > w2) return true;
            if (v0.y > w0 && v1.y > w1 && v2.y > w2) return true;
            if (v0.z > w0 && v1.z > w1 && v2.z > w2) return true;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Backface(float4 v0, float4 v1, float4 v2)
        {
            float3 v01 = (v1 - v0).xyz;
            float3 v02 = (v2 - v1).xyz;
            float3 normal = math.cross(v01, v02);
            return normal.z < 0;
        }
    }
}