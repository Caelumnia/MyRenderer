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
            [ReadOnly] public int Width, Height;
            
            [NativeDisableParallelForRestriction] public NativeArray<float> ShadowMap;
            
            public void Execute(int index)
            {
                var indice = Indices[index];
                var v0 = CSPosArray[indice[0]];
                var v1 = CSPosArray[indice[1]];
                var v2 = CSPosArray[indice[2]];

                if (Common.Clipped(v0, v1, v2)) return;

                var pos0 = v0.xyz / v0.w;
                var pos1 = v1.xyz / v1.w;
                var pos2 = v2.xyz / v2.w;

                if (Common.Backface(pos0, pos1, pos2)) return;

                var screen = new float2(Width - 1, Height - 1) * 0.5f;
                pos0.xy = (pos0.xy + new float2(1.0f)) * screen;
                pos0.z = pos0.z * 0.5f + 0.5f;
                pos1.xy = (pos1.xy + new float2(1.0f)) * screen;
                pos1.z = pos1.z * 0.5f + 0.5f;
                pos2.xy = (pos2.xy + new float2(1.0f)) * screen;
                pos2.z = pos2.z * 0.5f + 0.5f;

                var minCoord = new int2(Int32.MaxValue);
                var maxCoord = new int2(Int32.MinValue);

                minCoord.x = Mathf.FloorToInt(math.min(pos0.x, math.min(pos1.x, pos2.x)));
                minCoord.y = Mathf.FloorToInt(math.min(pos0.y, math.min(pos1.y, pos2.y)));
                maxCoord.x = Mathf.CeilToInt(math.max(pos0.x, math.max(pos1.x, pos2.x)));
                maxCoord.y = Mathf.CeilToInt(math.max(pos0.y, math.max(pos1.y, pos2.y)));
                
                minCoord = math.max(minCoord, 0);
                maxCoord = math.min(maxCoord, new int2(Width, Height));
                
                for (int y = minCoord.y; y < maxCoord.y; ++y)
                {
                    for (int x = minCoord.x; x < maxCoord.x; ++x)
                    {
                        float3 pixelPos = new float3(x + 0.5f, y + 0.5f, 0.0f);
                        var baryCoord = Common.ComputeBarycentric2D(pixelPos.x, pixelPos.y, pos0, pos1, pos2);
                        if (baryCoord.x < 0 || baryCoord.y < 0 || baryCoord.z < 0) continue;

                        var ws = new float3(v0.w, v1.w, v2.w);
                        var zs = new float3(pos0.z, pos1.z, pos2.z);
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