using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MyRenderer.Shaders
{
    public static class ShadowPass
    {
        [BurstCompile]
        public struct VertexShader : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> PositionArray;
            [ReadOnly] public float4x4 MatMVP;

            public NativeArray<float4> CSPosArray;

            public void Execute(int index)
            {
                var OSPos = new float4(PositionArray[index], 1.0f);
                OSPos.z *= -1;
                CSPosArray[index] = math.mul(MatMVP, OSPos);
            }
        }

        [BurstCompile]
        public struct PixelShader : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int3> Indices;
            [ReadOnly] public NativeArray<float4> CSPosArray;
            [ReadOnly] public int Width;
            
            [NativeDisableParallelForRestriction] public NativeArray<float> ShadowMap;
            
            public void Execute(int index)
            {
                var indice = Indices[index];
                var v0 = CSPosArray[indice[0]];
                var v1 = CSPosArray[indice[1]];
                var v2 = CSPosArray[indice[2]];

                if (Common.Clipped(v0, v1, v2)) return;

                v0.xyz /= v0.w;
                v1.xyz /= v1.w;
                v2.xyz /= v2.w;

                if (Common.Backface(v0.xyz, v1.xyz, v2.xyz)) return;

                var screen = new float2(Width * 0.5f);
                v0.xy = (v0.xy + new float2(1.0f)) * screen;
                v0.z = v0.z * 0.5f + 0.5f;
                v1.xy = (v1.xy + new float2(1.0f)) * screen;
                v1.z = v1.z * 0.5f + 0.5f;
                v2.xy = (v2.xy + new float2(1.0f)) * screen;
                v2.z = v2.z * 0.5f + 0.5f;

                var minCoord = new int2(Int32.MaxValue);
                var maxCoord = new int2(Int32.MinValue);

                minCoord.x = Mathf.FloorToInt(math.min(v0.x, math.min(v1.x, v2.x)));
                minCoord.y = Mathf.FloorToInt(math.min(v0.y, math.min(v1.y, v2.y)));
                maxCoord.x = Mathf.CeilToInt(math.max(v0.x, math.max(v1.x, v2.x)));
                maxCoord.y = Mathf.CeilToInt(math.max(v0.y, math.max(v1.y, v2.y)));
                
                minCoord = math.max(minCoord, 0);
                maxCoord = math.min(maxCoord, new int2(Width, Width));
                
                for (int y = minCoord.y; y < maxCoord.y; ++y)
                {
                    for (int x = minCoord.x; x < maxCoord.x; ++x)
                    {
                        float3 pixelPos = new float3(x + 0.5f, y + 0.5f, 0.0f);
                        var baryCoord = Common.ComputeBarycentric2D(pixelPos.x, pixelPos.y, v0.xyz, v1.xyz, v2.xyz);
                        if (baryCoord.x < -0.0005f || baryCoord.y < -0.0005f || baryCoord.z < -0.0005f) continue;

                        var ws = new float3(v0.w, v1.w, v2.w);
                        var zs = new float3(v0.z, v1.z, v2.z);
                        var co = baryCoord / ws;
                        float z = 1.0f / math.csum(co);
                        pixelPos.z = z * math.csum(co * zs);

                        int bufIndex = Common.GetIndex(x, y, Width);
                        if (pixelPos.z >= ShadowMap[bufIndex]) ShadowMap[bufIndex] = pixelPos.z;
                    }
                }
            }
        }
    }
}