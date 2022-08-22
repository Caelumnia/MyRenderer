using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MyRenderer
{
    public struct VertexShader : IJobParallelFor
    {
        [ReadOnly] public Attributes Attributes;
        [ReadOnly] public UniformBuffer Uniforms;

        public NativeArray<Varyings> VaryingsArray;

        public void Execute(int index)
        {
            Varyings varyings;

            var OSPos = new float4(Attributes.Position[index], 1.0f);
            OSPos.z *= -1;
            varyings.CSPos = math.mul(Uniforms.MatMVP, OSPos);
            varyings.WSPos = math.mul(Uniforms.MatModel, OSPos).xyz;
            var OSNormal = new float4(Attributes.Normal[index], 0.0f);
            OSNormal.z *= -1;
            varyings.WSNormal = math.mul(Uniforms.MatNormal, OSNormal).xyz;
            varyings.UV0 = Attributes.UV[index];

            VaryingsArray[index] = varyings;
        }
    }

    public struct PixelShader : IJobParallelFor
    {
        [ReadOnly] public Attributes Attributes;
        [ReadOnly] public UniformBuffer Uniforms;
        [ReadOnly] public NativeArray<Varyings> VaryingsArray;
        [ReadOnly] public int Width, Height;

        [NativeDisableParallelForRestriction] public NativeArray<Color> ColorBuffer;
        [NativeDisableParallelForRestriction] public NativeArray<float> DepthBuffer;
        
        public void Execute(int index)
        {
            var indice = Attributes.Indices[index];
            var verts = new Varyings[3];
            for (int i = 0; i < 3; ++i)
            {
                verts[i] = VaryingsArray[indice[i]];
            }

            var tempPos = new float3[3];
            tempPos[0] = verts[0].CSPos.xyz / verts[0].CSPos.w;
            tempPos[1] = verts[1].CSPos.xyz / verts[1].CSPos.w;
            tempPos[2] = verts[2].CSPos.xyz / verts[2].CSPos.w;

            if (Clipped(tempPos)) return;
            if (Backface(tempPos)) return;

            var screen = new float2(Width - 1.0f, Height - 1.0f);
            for (int i = 0; i < 3; ++i)
            {
                tempPos[i].xy = (tempPos[i].xy + new float2(1.0f, 1.0f)) * screen;
                tempPos[i].z = tempPos[i].z * 0.5f + 0.5f;
            }

            DrawTriangle(new Triangle(tempPos, verts));
        }

        private void DrawTriangle(Triangle t)
        {
            t.GetScreenBounds(new int2(Width, Height), out var minCoord, out var maxCoord);
            
            var tempPos = new float3[3];
            tempPos[0] = t.Verts[0].SSPos.xyz;
            tempPos[1] = t.Verts[1].SSPos.xyz;
            tempPos[2] = t.Verts[2].SSPos.xyz;

            for (int y = minCoord.y; y < maxCoord.y; ++y)
            {
                for (int x = minCoord.x; x < maxCoord.x; ++x)
                {
                    float3 pos = new float3(x + 0.5f, y + 0.5f, 0.0f);
                    var baryCoord = ComputeBarycentric2D(pos.x, pos.y, tempPos);
                    if (baryCoord.x < 0 || baryCoord.y < 0 || baryCoord.z < 0) continue;
                    var ws = new float3(t.Verts[0].SSPos.w, t.Verts[1].SSPos.w, t.Verts[2].SSPos.w);
                    var zs = new float3(t.Verts[0].SSPos.z, t.Verts[1].SSPos.z, t.Verts[2].SSPos.z);
                    var co = baryCoord / ws;
                    float z = 1.0f / math.csum(co);
                    pos.z = z * math.csum(co * zs);

                    int index = GetIndex(x, y);
                    if (pos.z < DepthBuffer[index]) continue;
                    
                    t.Interpolate(pos, co * z, out var Vert);
                    ColorBuffer[index] = BlinnPhong(Vert);
                }
            }
        }

        private Color BlinnPhong(TriangleVert vert)
        {
            Color color = new Color(vert.Color.x, vert.Color.y, vert.Color.z, vert.Color.w);

            return color;
        }

        private int GetIndex(int x, int y)
        {
            return x + Width * y;
        }

        private static float3 ComputeBarycentric2D(float x, float y, float3[] verts)
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

        private static bool Clipped(float3[] v)
        {
            if (v[0].x < -1 && v[1].x < -1 && v[2].x < -1) return true;
            if (v[0].y < -1 && v[1].y < -1 && v[2].y < -1) return true;
            if (v[0].z < -1 && v[1].z < -1 && v[2].z < -1) return true;
            if (v[0].x > 1 && v[1].x > 1 && v[2].x > 1) return true;
            if (v[0].y > 1 && v[1].y > 1 && v[2].y > 1) return true;
            if (v[0].z > 1 && v[1].z > 1 && v[2].z > 1) return true;
            return false;
        }

        private static bool Backface(float3[] v)
        {
            float3 v01 = v[1] - v[0];
            float3 v02 = v[2] - v[1];
            float3 normal = math.cross(v01, v02);
            return normal.z < 0;
        }
    }
}