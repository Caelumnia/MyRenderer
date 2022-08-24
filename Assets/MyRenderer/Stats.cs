using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MyRenderer
{
    public class Stats : MonoBehaviour
    {
        public int fontSize = 20;
        public Color fontColor = Color.white;
        public float SampleTime = 2f;
        
        public Text TrianglesText;
        public Text VerticesText;
        public Text FPSText;
        
        private GUIStyle _style;
        private int frameCount;
        private float timeTotal;
        
        private void Awake()
        {
            _style = new GUIStyle();
            _style.fontSize = fontSize;
            _style.normal.textColor = fontColor;
        }

        private void Start()
        {
            frameCount = 0;
            timeTotal = 0;
        }

        private void Update()
        {
            frameCount++;
            timeTotal += Time.unscaledDeltaTime;

            if (timeTotal >= SampleTime)
            {
                float fps = frameCount / timeTotal;
                frameCount = 0;
                timeTotal = 0;
                this.FPSText.text = $"FPS: {fps:F}";
            }
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 200, 20), FPSText.text, _style);
            GUI.Label(new Rect(10, 35, 200, 20), TrianglesText.text, _style);
            GUI.Label(new Rect(10, 60, 200, 20), VerticesText.text, _style);
        }

        public void UpdateStats(int vertices, int triangles, int totalTriangles)
        {
            TrianglesText.text = $"Triangles: {triangles} / {totalTriangles}";
            VerticesText.text = $"Vertices: {vertices}";
        }
    }

    public delegate void OnStatsUpdate(int vertices, int triangles, int totalTriangles);
}