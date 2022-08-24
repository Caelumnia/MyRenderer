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
    public class Rasterizer
    {
        public Texture2D ScreenRT;
        public Texture2D ShadowRT;
        public OnStatsUpdate StatsUpdate;

        private int _width, _height;
        private RenderConfig _config;
        private UniformBuffer _uniforms;
        private NativeBuffer<Color> _colorBuffer;
        private NativeBuffer<float> _depthBuffer;
        private NativeBuffer<float> _shadowMap;

        private int _triangleCount, _verticeCount, _triangleAll;

        public Rasterizer(int width, int height, RenderConfig config)
        {
            _width = width;
            _height = height;
            _config = config;

            int bufferSize = width * height;
            _colorBuffer = new NativeBuffer<Color>(bufferSize);
            _depthBuffer = new NativeBuffer<float>(bufferSize);
            _shadowMap = new NativeBuffer<float>(_config.ShadowMapSize * _config.ShadowMapSize);
            ScreenRT = new Texture2D(width, height)
            {
                filterMode = FilterMode.Point
            };
            if (_config.target == RenderTarget.ShadowMap)
            {
                ShadowRT = new Texture2D(_config.ShadowMapSize, config.ShadowMapSize)
                {
                    filterMode = FilterMode.Point
                };
            }

            _uniforms = new UniformBuffer();
        }

        public void Release()
        {
            _colorBuffer.Release();
            _depthBuffer.Release();
            _shadowMap.Release();
            ScreenRT = null;
            ShadowRT = null;
        }

        public void Clear()
        {
            Profiler.BeginSample("Rasterizer.Clear()");

            _colorBuffer.Fill(Color.black);
            _depthBuffer.Clear();
            _shadowMap.Clear();
            _triangleCount = _triangleAll = _verticeCount = 0;

            Profiler.EndSample();
        }

        public void SetupUniform(Camera camera, Light mainLight)
        {
            var transform = camera.transform;
            _uniforms.WSCameraPos = transform.position;
            _uniforms.WSCameraPos.z *= -1;
            _uniforms.WSCameraLookAt = transform.forward.normalized;
            _uniforms.WSCameraLookAt.z *= -1;
            _uniforms.WSCameraUp = transform.up.normalized;
            _uniforms.WSCameraUp.z *= -1;

            var lightTransform = mainLight.transform;
            _uniforms.WSLightDir = -lightTransform.forward.normalized;
            _uniforms.WSLightDir.z *= -1;
            _uniforms.WSLightPos = lightTransform.position;
            _uniforms.WSLightPos.z *= -1;
            Color lightColor = mainLight.color * mainLight.intensity;
            _uniforms.LightColor = new float4(lightColor.r, lightColor.g, lightColor.b, lightColor.a);
        }

        public void SetupUniform(Material material)
        {
            _uniforms.Albedo = new float4(material.color.r, material.color.g, material.color.b, material.color.a);
        }

        public void SetupCamera(Camera camera)
        {
            _uniforms.MatView = GetViewMatrix(_uniforms.WSCameraPos, _uniforms.WSCameraLookAt, _uniforms.WSCameraUp);
            float aspect = (float)_width / _height;
            float near = camera.nearClipPlane;
            float far = camera.farClipPlane;
            if (camera.orthographic)
            {
                float halfHeight = camera.orthographicSize;
                float halfWidth = aspect * halfHeight;
                _uniforms.MatProj = GetOrthoMatrix(-halfWidth, halfWidth, -halfHeight, halfHeight, far, near);
            }
            else
            {
                _uniforms.MatProj = GetPerspectiveMatrix(camera.fieldOfView, aspect, near, far);
            }
        }

        public void SetupLight(Light light)
        {
            _uniforms.MatView = GetViewMatrix(0.0f, -_uniforms.WSLightDir, new float3(0.0f, 1.0f, 0.0f));
            float halfWidth = 10.0f;
            _uniforms.MatProj = GetOrthoMatrix(-halfWidth, halfWidth, -halfWidth, halfWidth, -100, 100);
            _uniforms.MatLightViewProj = math.mul(_uniforms.MatProj, _uniforms.MatView);
        }

        public void ShadowPass(RenderProxy proxy)
        {
            Profiler.BeginSample($"Rasterizer.ShadowPass({proxy.mesh.name})");

            var transform = proxy.transform;
            var position = transform.position;
            position.z *= -1;
            _uniforms.MatModel = proxy.GetModelMatrix();

            var CSPosArray = new NativeArray<float4>(proxy.mesh.vertexCount, Allocator.TempJob);
            var VS = new Shaders.ShadowPass.VertexShader()
            {
                PositionArray = proxy.Attributes.Position,
                MatMVP = _uniforms.MatMVP,
                CSPosArray = CSPosArray,
            };
            var VSHandle = VS.Schedule(CSPosArray.Length, 1);

            var PS = new Shaders.ShadowPass.PixelShader()
            {
                Indices = proxy.Attributes.Indices,
                CSPosArray = CSPosArray,
                ShadowMap = _shadowMap.Buffer,
                Width = _config.ShadowMapSize,
            };
            var PSHandle = PS.Schedule(proxy.Attributes.Indices.Length, 1, VSHandle);
            PSHandle.Complete();

            CSPosArray.Dispose();

            Profiler.EndSample();
        }

        public void BasePass(RenderProxy proxy)
        {
            Profiler.BeginSample($"Rasterizer.BasePass({proxy.mesh.name})");

            _verticeCount += proxy.mesh.vertexCount;
            _triangleAll += proxy.Attributes.Indices.Length;

            var transform = proxy.transform;
            var position = transform.position;
            position.z *= -1;
            _uniforms.MatModel = proxy.GetModelMatrix();
            SetupUniform(proxy.material);

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
                ShadowMap = _shadowMap.Buffer,
                Width = _width,
                Height = _height,
                ShadowMapSize = _config.ShadowMapSize,
                ColorBuffer = _colorBuffer.Buffer,
                DepthBuffer = _depthBuffer.Buffer,
                Renderred = Renderred,
            };
            var PSHandle = PS.Schedule(proxy.Attributes.Indices.Length, 1, VSHandle);
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
            Profiler.BeginSample("Rasterizer.Flush()");

            if (_config.target == RenderTarget.ShadowMap)
            {
                var shadow = _shadowMap.Raw;
                var color = new Color[shadow.Length];
                for (int y = 0; y < _config.ShadowMapSize; ++y)
                {
                    for (int x = 0; x < _config.ShadowMapSize; ++x)
                    {
                        int index = x + y * _config.ShadowMapSize;
                        color[index] = new Color(shadow[index], shadow[index], shadow[index], 1.0f);
                    }
                }

                ShadowRT.SetPixels(color);
                ShadowRT.Apply(false);
            }
            else
            {
                ScreenRT.SetPixels(_colorBuffer.Raw);
                ScreenRT.Apply(false);
            }

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