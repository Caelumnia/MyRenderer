using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace MyRenderer
{
    public struct UniformBuffer // uniform variables for shading compute
    {
        public float3 WSCameraPos;
        public float3 WSCameraLookAt;
        public float3 WSCameraUp;

        public float3 WSLightPos;
        public float3 WSLightDir;
        public float4 LightColor;

        public float4x4 MatModel;
        public float4x4 MatView;
        public float4x4 MatProj;
        public float4x4 MatLightViewProj;

        public float4x4 MatMVP => math.mul(MatProj, math.mul(MatView, MatModel));
        public float4x4 MatNormal => math.transpose(math.inverse(MatModel));

        public float4 Albedo;
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
                Indices[i] = new int3(mesh.triangles[j + 1], mesh.triangles[j], mesh.triangles[j + 2]);
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

    public struct Varyings // Data transferred between VS and PS
    {
        public float4 CSPos;
        public float3 WSPos;
        public float3 WSNormal;
        public float4 Color;
        public float2 UV0;
    }

    public struct TriangleVert
    {
        public float4 SSPos;
        public float3 WSPos;
        public float3 WSNormal;
        public float4 Color;
        public float2 TexCoord;
    }
}