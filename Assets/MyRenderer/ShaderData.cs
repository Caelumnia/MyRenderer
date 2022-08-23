using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace MyRenderer
{
    public struct UniformBuffer // uniform variables for shading compute
    {
        public float3 WSCameraPos;
        public float3 WSLightDir;
        public float4 LightColor;

        public float4x4 MatModel;
        public float4x4 MatView;
        public float4x4 MatProjection;

        public float4x4 MatMVP => math.mul(MatProjection, math.mul(MatView, MatModel));
        public float4x4 MatNormal => math.transpose(math.inverse(MatModel));
    }

    public struct Attributes // Data of VS input
    {
        public NativeArray<float3> Position;
        public NativeArray<float3> Normal;
        public NativeArray<float2> UV;
        public NativeArray<int3> Indices;

        public Attributes(Mesh mesh)
        {
            Position = new NativeArray<float3>(mesh.vertexCount, Allocator.Persistent);
            for (int i = 0; i < mesh.vertexCount; ++i)
            {
                Position[i] = mesh.vertices[i];
            }

            Normal = new NativeArray<float3>(mesh.vertexCount, Allocator.Persistent);
            for (int i = 0; i < mesh.vertexCount; ++i)
            {
                Normal[i] = mesh.normals[i];
            }

            UV = new NativeArray<float2>(mesh.vertexCount, Allocator.Persistent);
            for (int i = 0; i < mesh.vertexCount; ++i)
            {
                UV[i] = mesh.uv[i];
            }

            int triangleCount = mesh.triangles.Length / 3;
            Indices = new NativeArray<int3>(triangleCount, Allocator.Persistent);
            for (int i = 0; i < triangleCount; ++i)
            {
                int j = i * 3;
                Indices[i] = new int3(mesh.triangles[j + 2], mesh.triangles[j + 1], mesh.triangles[j]);
            }
        }

        public void Release()
        {
            Position.Dispose();
            Normal.Dispose();
            UV.Dispose();
            Indices.Dispose();
        }
    }

    public struct Varyings // Data transfered between VS and PS
    {
        public float4 CSPos;
        public float3 WSPos;
        public float3 WSNormal;
        public float2 UV0;
    }

    public struct TriangleVert
    {
        public float4 SSPos;

        public float3 WSPos;

        // public float3 OSNormal;
        public float3 WSNormal;
        public float4 Color;
        public float2 TexCoord;
    }

    public struct Triangle
    {
        public NativeArray<TriangleVert> Verts;

        public Triangle(NativeArray<float3> pos, NativeArray<Varyings> verts)
        {
            Verts = new NativeArray<TriangleVert>(3, Allocator.Temp);
            for (int i = 0; i < 3; ++i)
            {
                Verts[i] = new TriangleVert()
                {
                    SSPos = new float4(pos[i], verts[i].CSPos.w),
                    WSPos = verts[i].WSPos,
                    WSNormal = verts[i].WSNormal,
                    Color = new float4(1.0f),
                    TexCoord = verts[i].UV0,
                };
            }
        }

        public void Release()
        {
            Verts.Dispose();
        }

        public void GetScreenBounds(int2 screenSize, out int2 minCoord, out int2 maxCoord)
        {
            minCoord = new int2(Int32.MaxValue);
            maxCoord = new int2(Int32.MinValue);
            for (int i = 0; i < 3; ++i)
            {
                minCoord.x = math.min(minCoord.x, Mathf.FloorToInt(Verts[i].SSPos.x));
                minCoord.y = math.min(minCoord.y, Mathf.FloorToInt(Verts[i].SSPos.y));
                maxCoord.x = math.max(maxCoord.x, Mathf.CeilToInt(Verts[i].SSPos.x));
                maxCoord.y = math.max(maxCoord.y, Mathf.CeilToInt(Verts[i].SSPos.y));
            }

            minCoord = math.max(minCoord, 0);
            maxCoord = math.min(maxCoord, screenSize);
        }

        public void Interpolate(float3 pos, float3 co, out TriangleVert vert)
        {
            vert = new TriangleVert()
            {
                SSPos = new float4(pos, 1.0f),
                WSPos = co.x * Verts[0].WSPos + co.y * Verts[1].WSPos + co.z * Verts[2].WSPos,
                WSNormal = co.x * Verts[0].WSNormal + co.y * Verts[1].WSNormal + co.z * Verts[2].WSNormal,
                Color = co.x * Verts[0].Color + co.y * Verts[1].Color + co.z * Verts[2].Color,
                TexCoord = co.x * Verts[0].TexCoord + co.y * Verts[1].TexCoord + co.z * Verts[2].TexCoord,
            };
        }
    }
}