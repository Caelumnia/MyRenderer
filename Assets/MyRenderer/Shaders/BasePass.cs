using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace MyRenderer.Shaders
{
    public static class BasePass
    {
        [BurstCompile]
        public struct VertexShader : IJobParallelFor
        {
            [ReadOnly] public Attributes Attributes;
            [ReadOnly] public float4x4 MatMVP;
            [ReadOnly] public float4x4 MatModel;
            [ReadOnly] public float4x4 MatNormal;

            public NativeArray<Varyings> VaryingsArray;

            public void Execute(int index)
            {
                Varyings varyings;

                var OSPos = new float4(Attributes.Position[index], 1.0f);
                OSPos.z *= -1;
                varyings.CSPos = math.mul(MatMVP, OSPos);
                varyings.WSPos = math.mul(MatModel, OSPos).xyz;
                var OSNormal = new float4(Attributes.Normal[index], 0.0f);
                OSNormal.z *= -1;
                varyings.WSNormal = math.mul(MatNormal, OSNormal).xyz;
                varyings.UV0 = Attributes.UV[index];

                VaryingsArray[index] = varyings;
            }
        }

        [BurstCompile]
        public struct PixelShader : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int3> Indices;
            [ReadOnly] public UniformBuffer Uniforms;
            [ReadOnly] public NativeArray<Varyings> VaryingsArray;
            [ReadOnly] public int Width, Height;
            [ReadOnly] public NativeArray<float> ShadowMap;

            [NativeDisableParallelForRestriction] public NativeArray<Color> ColorBuffer;
            [NativeDisableParallelForRestriction] public NativeArray<float> DepthBuffer;

            [WriteOnly] public NativeArray<bool> Renderred;

            private static readonly float2[] PoissonFilter = new[]
            {
                new float2(-0.94201624f, -0.39906216f),
                new float2(0.94558609f, -0.76890725f),
                new float2(-0.094184101f, -0.92938870f),
                new float2(0.34495938f, 0.29387760f),
                new float2(-0.91588581f, 0.45771432f),
                new float2(-0.81544232f, -0.87912464f),
                new float2(-0.38277543f, 0.27676845f),
                new float2(0.97484398f, 0.75648379f),
                new float2(0.44323325f, -0.97511554f),
                new float2(0.53742981f, -0.47373420f),
                new float2(-0.26496911f, -0.41893023f),
                new float2(0.79197514f, 0.19090188f),
                new float2(-0.24188840f, 0.99706507f),
                new float2(-0.81409955f, 0.91437590f),
                new float2(0.19984126f, 0.78641367f),
                new float2(0.14383161f, -0.14100790f)
            };

            public void Execute(int index)
            {
                var indice = Indices[index];
                var verts = new NativeArray<Varyings>(3, Allocator.Temp);
                for (int i = 0; i < 3; ++i)
                {
                    verts[i] = VaryingsArray[indice[i]];
                }

                var Verts0 = VaryingsArray[indice[0]];
                var Verts1 = VaryingsArray[indice[1]];
                var Verts2 = VaryingsArray[indice[2]];

                var v0 = Verts0.CSPos;
                var v1 = Verts1.CSPos;
                var v2 = Verts2.CSPos;

                if (Common.Clipped(v0, v1, v2)) return;

                var pos = new NativeArray<float3>(3, Allocator.Temp);
                for (int i = 0; i < 3; ++i)
                {
                    pos[i] = verts[i].CSPos.xyz / verts[i].CSPos.w;
                }

                if (Common.Backface(pos[0], pos[1], pos[2])) return;

                var screen = new float2(Width - 1, Height - 1) * 0.5f;
                for (int i = 0; i < 3; ++i)
                {
                    pos[i] = new float3((pos[i].xy + new float2(1.0f, 1.0f)) * screen, pos[i].z * 0.5f + 0.5f);
                }

                var t = new Triangle(pos, verts);
                t.GetScreenBounds(new int2(Width, Height), out var minCoord, out var maxCoord);

                for (int y = minCoord.y; y < maxCoord.y; ++y)
                {
                    for (int x = minCoord.x; x < maxCoord.x; ++x)
                    {
                        float3 pixelPos = new float3(x + 0.5f, y + 0.5f, 0.0f);
                        var baryCoord = Common.ComputeBarycentric2D(pixelPos.x, pixelPos.y, pos[0], pos[1], pos[2]);
                        if (baryCoord.x < 0 || baryCoord.y < 0 || baryCoord.z < 0) continue;

                        var ws = new float3(t.Verts[0].SSPos.w, t.Verts[1].SSPos.w, t.Verts[2].SSPos.w);
                        var zs = new float3(t.Verts[0].SSPos.z, t.Verts[1].SSPos.z, t.Verts[2].SSPos.z);
                        var co = baryCoord / ws;
                        float z = 1.0f / math.csum(co);
                        pixelPos.z = z * math.csum(co * zs);

                        int bufIndex = Common.GetIndex(x, y, Width);
                        if (pixelPos.z < DepthBuffer[bufIndex]) continue;
                        DepthBuffer[bufIndex] = pixelPos.z;

                        TriangleVert inpVert;
                        inpVert.SSPos = new float4(pixelPos, 1.0f);
                        inpVert.WSPos =
                            z * (co.x * t.Verts[0].WSPos + co.y * t.Verts[1].WSPos + co.z * t.Verts[2].WSPos);
                        inpVert.WSNormal = z * (co.x * t.Verts[0].WSNormal + co.y * t.Verts[1].WSNormal +
                                                co.z * t.Verts[2].WSNormal);
                        inpVert.Color =
                            z * (co.x * t.Verts[0].Color + co.y * t.Verts[1].Color + co.z * t.Verts[2].Color);
                        inpVert.TexCoord = z * (co.x * t.Verts[0].TexCoord + co.y * t.Verts[1].TexCoord +
                                                co.z * t.Verts[2].TexCoord);

                        var shadow = SampleShadowDepth(inpVert.WSPos, screen);
                        var color = BlinnPhong(ref inpVert);

                        ColorBuffer[bufIndex] = color * shadow;
                    }
                }

                Renderred[index] = true;

                t.Release();
                pos.Dispose();
                verts.Dispose();
            }

            private Color BlinnPhong(ref TriangleVert vert)
            {
                float3 normal = math.normalize(vert.WSNormal);
                float3 viewDir = math.normalize(Uniforms.WSCameraPos - vert.WSPos);
                float3 halfVec = math.normalize(Uniforms.WSLightDir + viewDir);
                float dotNL = math.dot(normal, Uniforms.WSLightDir);
                float dotNH = math.dot(normal, halfVec);

                float4 ambient = 0.5f * Uniforms.Albedo;
                float4 diffuse = 0.8f * Uniforms.Albedo * math.max(0, dotNL);
                float4 specular = 0.7f * Uniforms.Albedo * math.pow(math.max(0, dotNH), 256f);

                float4 color = (ambient + diffuse + specular) * Uniforms.LightColor;
                return new Color(color.x, color.y, color.z, color.w);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float3 GetLightPos(float3 WSPos, float2 viewport)
            {
                var pos = math.mul(Uniforms.MatLightViewProj, new float4(WSPos, 1.0f));
                pos.xy = (pos.xy + new float2(1.0f)) * viewport;
                pos.z = pos.z * 0.5f + 0.5f;
                return pos.xyz;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float SampleShadowMap(float2 pos, float depth)
            {
                var p0 = math.floor(pos);
                var x0 = math.clamp((int) p0.x, 0, Width);
                var y0 = math.clamp((int) p0.y, 0, Height);
                var bias = 0.0003f;
                return depth > ShadowMap[Common.GetIndex(x0, y0, Width)] - bias ? 1.0f : 0.0f;
            }

            private int GetRandomSampleIndex(float3 WSPos, int index)
            {
                var seed = new float4(WSPos, index);
                var dot = math.dot(seed, new float4(12.9898f, 78.233f, 45.164f, 94.673f));
                var rand = math.frac(math.sin(dot) * 43758.5453f);
                return Mathf.RoundToInt(rand * 16.0f) % 16;
            }

            private float SampleShadowDepth(float3 WSPos, float2 viewport)
            {
                var lightPos = GetLightPos(WSPos, viewport);
                var pos = WSPos * 100f;
                var size = 0.5f;
                var samples0 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 0)] * size;
                var samples1 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 1)] * size;
                var samples2 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 2)] * size;
                var samples3 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 3)] * size;
                var samples4 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 4)] * size;
                var samples5 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 5)] * size;
                var samples6 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 6)] * size;
                var samples7 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 7)] * size;
                var samples8 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 8)] * size;
                var samples9 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 9)] * size;
                var samples10 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 10)] * size;
                var samples11 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 11)] * size;
                var samples12 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 12)] * size;
                var samples13 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 13)] * size;
                var samples14 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 14)] * size;
                var samples15 = lightPos.xy + PoissonFilter[GetRandomSampleIndex(pos, 15)] * size;

                float depth = 0.0f;
                depth += SampleShadowMap(samples0, lightPos.z);
                depth += SampleShadowMap(samples1, lightPos.z);
                depth += SampleShadowMap(samples2, lightPos.z);
                depth += SampleShadowMap(samples3, lightPos.z);
                depth += SampleShadowMap(samples4, lightPos.z);
                depth += SampleShadowMap(samples5, lightPos.z);
                depth += SampleShadowMap(samples6, lightPos.z);
                depth += SampleShadowMap(samples7, lightPos.z);
                depth += SampleShadowMap(samples8, lightPos.z);
                depth += SampleShadowMap(samples9, lightPos.z);
                depth += SampleShadowMap(samples10, lightPos.z);
                depth += SampleShadowMap(samples11, lightPos.z);
                depth += SampleShadowMap(samples12, lightPos.z);
                depth += SampleShadowMap(samples13, lightPos.z);
                depth += SampleShadowMap(samples14, lightPos.z);
                depth += SampleShadowMap(samples15, lightPos.z);

                return depth / 16.0f;
            }
        }
    }
}