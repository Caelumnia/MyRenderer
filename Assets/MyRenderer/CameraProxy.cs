using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MyRenderer
{
    public class CameraProxy : MonoBehaviour
    {
        public RawImage rawImage;
        public RenderConfig config;

        [SerializeField] private Light mainLight;
        private Camera _camera;
        private Stats _stats;
        private Rasterizer _rasterizer;
        private List<RenderProxy> _renderProxies;
        private int _width, _height;

        private bool _isPrepared;

        private void Start()
        {
            _camera = GetComponent<Camera>();

            _renderProxies = new List<RenderProxy>();
            var rootObjects = gameObject.scene.GetRootGameObjects();
            foreach (var obj in rootObjects)
            {
                _renderProxies.AddRange(obj.GetComponentsInChildren<RenderProxy>());
            }

            Debug.Log($"Collect {_renderProxies.Count} render objects.");

            _width = Screen.width;
            _height = Screen.height;
            RectTransform rect = rawImage.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(_width, _height);
            Debug.Log($"Screen size {_width}x{_height}");

            _rasterizer = new Rasterizer(_width, _height, config);
            if (config.target == RenderTarget.ShadowMap)
            {
                rawImage.texture = _rasterizer.ShadowRT;
            }
            else
            {
                rawImage.texture = _rasterizer.ScreenRT;
            }

            _stats = GetComponent<Stats>();
            if (_stats != null)
            {
                _rasterizer.StatsUpdate += _stats.UpdateStats;
            }

            _isPrepared = true;
        }

        private void OnPostRender()
        {
            if (!_isPrepared) return;
            Render();
        }

        private void OnDestroy()
        {
            _rasterizer.Release();
        }

        private void Render()
        {
            Profiler.BeginSample("CameraProxy.Render()");

            _rasterizer.Clear();
            _rasterizer.SetupUniform(_camera, mainLight);

            _rasterizer.SetupLight(mainLight);
            foreach (var obj in _renderProxies)
            {
                _rasterizer.ShadowPass(obj);
            }

            _rasterizer.SetupCamera(_camera);
            foreach (var obj in _renderProxies)
            {
                _rasterizer.BasePass(obj);
            }

            _rasterizer.Flush();

            Profiler.EndSample();
        }
    }
}