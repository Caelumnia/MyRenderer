using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace MyRenderer
{
    [Flags]
    public enum BufferMask
    {
        None = 0,
        Color = 1,
        Depth = 2,
    }

    public struct FrameBuffer
    {
        public NativeBuffer<Color> ColorBuffer;
        public NativeBuffer<float> DepthBuffer;

        public Texture2D ScreenRT;

        public FrameBuffer(int width, int height)
        {
            int bufferSize = width * height;
            ColorBuffer = new NativeBuffer<Color>(bufferSize);
            DepthBuffer = new NativeBuffer<float>(bufferSize);
            ScreenRT = new Texture2D(width, height)
            {
                filterMode = FilterMode.Point
            };
        }

        public void Clear(BufferMask mask)
        {
            if (mask.HasFlag(BufferMask.Color))
            {
                ColorBuffer.Fill(Color.black);
            }

            if (mask.HasFlag(BufferMask.Depth))
            {
                DepthBuffer.Fill(0.0f);
            }
        }

        public void Flush(BufferMask mask = BufferMask.Color)
        {
            ScreenRT.SetPixels(ColorBuffer.Raw);
            ScreenRT.Apply();
        }

        public void Release()
        {
            ColorBuffer.Release();
            DepthBuffer.Release();
            ScreenRT = null;
        }
    }

    public class Rasterizer
    {
        public FrameBuffer FrameBuffer;
        public OnStatsUpdate StatsUpdate;

        private int _width, _height;
        private UniformBuffer _uniforms;

        private int _triangleCount, _verticeCount, _triangleAll;

        public Rasterizer(int width, int height)
        {
            _width = width;
            _height = height;

            FrameBuffer = new FrameBuffer(_width, _height);
            _uniforms = new UniformBuffer();
        }

        public void Release()
        {
            FrameBuffer.Release();
        }

        public void Clear()
        {
            Profiler.BeginSample("MyRenderer::Rasterizer::Clear");

            FrameBuffer.Clear(BufferMask.Color | BufferMask.Depth);
            _triangleCount = _triangleAll = _verticeCount = 0;

            Profiler.EndSample();
        }

        public void SetupView(Transform transform)
        {
            float3 eye = transform.position;
            float3 lookAt = transform.forward.normalized;
            float3 up = transform.up.normalized;
            eye.z *= -1;
            lookAt.z *= -1;
            up.z *= -1;

            _uniforms.MatView = GetViewMatrix(eye, lookAt, up);
        }

        public void SetupUniform(Camera camera, Light mainLight)
        {
            // 着色时在右手坐标系计算，因此 uniforms 的 z 值需要翻转
            var transform = camera.transform;
            _uniforms.WSCameraPos = transform.position;
            _uniforms.WSCameraPos.z *= -1;
            var lightTransform = mainLight.transform;
            _uniforms.WSLightDir = -lightTransform.forward;
            _uniforms.WSLightDir.z *= -1;
            _uniforms.WSLightPos = lightTransform.position;
            _uniforms.WSLightPos.z *= -1;

            Color lightColor = mainLight.color * mainLight.intensity;
            _uniforms.LightColor = new float4(lightColor.r, lightColor.g, lightColor.b, lightColor.a);

            float3 lookAt = transform.forward.normalized;
            float3 up = transform.up.normalized;
            lookAt.z *= -1;
            up.z *= -1;
            _uniforms.MatView = GetViewMatrix(_uniforms.WSCameraPos, lookAt, up);

            float aspect = (float) _width / _height;
            float near = camera.nearClipPlane;
            float far = camera.farClipPlane;
            if (camera.orthographic)
            {
                float halfHeight = camera.orthographicSize;
                float halfWidth = aspect * halfHeight;
                _uniforms.MatProjection = GetOrthoMatrix(-halfWidth, halfWidth, -halfHeight, halfHeight, far, near);
            }
            else
            {
                _uniforms.MatProjection = GetPerspectiveMatrix(camera.fieldOfView, aspect, near, far);
            }
        }

        public void Draw(RenderProxy proxy)
        {
            Profiler.BeginSample("MyRenderer::Rasterizer::Draw");

            _verticeCount += proxy.mesh.vertexCount;
            _triangleAll += proxy.Attributes.Indices.Length;

            var transform = proxy.transform;
            var position = transform.position;
            position.z *= -1;
            _uniforms.MatModel = proxy.GetModelMatrix();

            var VaryingsArray = new NativeArray<Varyings>(proxy.mesh.vertexCount, Allocator.TempJob);
            var VS = new Shaders.BasePass.VertexShader()
            {
                Attributes = proxy.Attributes,
                MatMVP = _uniforms.MatMVP,
                MatModel = _uniforms.MatModel,
                MatNormal = _uniforms.MatNormal,
                VaryingsArray = VaryingsArray,
            };
            var VSHandle = VS.Schedule(VaryingsArray.Length, 1);

            var Renderred = new NativeArray<bool>(proxy.Attributes.Indices.Length, Allocator.TempJob);
            var PS = new Shaders.BasePass.PixelShader()
            {
                Indices = proxy.Attributes.Indices,
                Uniforms = _uniforms,
                VaryingsArray = VaryingsArray,
                Width = _width,
                Height = _height,
                ColorBuffer = FrameBuffer.ColorBuffer.Buffer,
                DepthBuffer = FrameBuffer.DepthBuffer.Buffer,
                Renderred = Renderred,
            };
            var PSHandle = PS.Schedule(proxy.Attributes.Indices.Length, 2, VSHandle);
            PSHandle.Complete();

            foreach (bool b in Renderred)
            {
                if (b) _triangleCount++;
            }

            VaryingsArray.Dispose();
            Renderred.Dispose();

            Profiler.EndSample();
        }

        public void Flush()
        {
            Profiler.BeginSample("MyRenderer::Rasterizer::Flush");

            FrameBuffer.Flush();
            StatsUpdate(_verticeCount, _triangleCount, _triangleAll);

            Profiler.EndSample();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4x4 GetViewMatrix(float3 eye, float3 lookAt, float3 up)
        {
            float3 camZ = -math.normalize(lookAt);
            float3 camY = math.normalize(up);
            float3 camX = math.cross(camY, camZ);
            camY = math.cross(camZ, camX);
            float4x4 rotate = float4x4.identity;
            rotate.c0 = new float4(camX, 0.0f);
            rotate.c1 = new float4(camY, 0.0f);
            rotate.c2 = new float4(camZ, 0.0f);

            float4x4 translate = float4x4.identity;
            translate.c3 = new float4(-eye, 1.0f);

            return math.mul(math.transpose(rotate), translate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4x4 GetOrthoMatrix(float l, float r, float b, float t, float f, float n)
        {
            float4x4 translate = float4x4.identity;
            translate.c3 = new float4(r + l, t + b, n + f, -2.0f) * -0.5f;
            float4x4 scale = float4x4.identity;
            scale.c0.x = 2f / (r - l);
            scale.c1.y = 2f / (t - b);
            scale.c2.z = 2f / (n - f);

            return math.mul(scale, translate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4x4 GetPerspectiveMatrix(float fov, float aspect, float near, float far)
        {
            float n = -near;
            float f = -far;
            float t = near * math.tan(math.radians(fov * 0.5f));
            float b = -t;
            float r = t * aspect;
            float l = -r;

            float4x4 perspectiveToOrtho = float4x4.identity;
            perspectiveToOrtho.c0.x = n;
            perspectiveToOrtho.c1.y = n;
            perspectiveToOrtho.c2.z = n + f;
            perspectiveToOrtho.c3.z = -n * f;
            perspectiveToOrtho.c2.w = 1;
            perspectiveToOrtho.c3.w = 0;

            return math.mul(GetOrthoMatrix(l, r, b, t, f, n), perspectiveToOrtho);
        }
    }
}