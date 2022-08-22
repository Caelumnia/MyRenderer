using System;
using UnityEngine;

namespace MyRenderer
{
    public class RenderProxy : MonoBehaviour
    {
        public Mesh mesh;
        public Material material;
        
        public Attributes Attributes;

        private void Start()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                mesh = meshFilter.mesh;
            }

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                material = meshRenderer.sharedMaterial;
            }

            Attributes = new Attributes(mesh);
        }

        private void OnDestroy()
        {
            Attributes.Release();
        }
    }
}