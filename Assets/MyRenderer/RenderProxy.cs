using System;
using Unity.Mathematics;
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
        
        public float4x4 GetModelMatrix()
        {
            if(transform == null)
            {
                return float4x4.RotateZ(0);
            }

            var matScale = float4x4.Scale(transform.lossyScale);

            var rotation = transform.rotation.eulerAngles;
            var rotX = float4x4.RotateX(-rotation.x);
            var rotY = float4x4.RotateY(-rotation.y);
            var rotZ = float4x4.RotateZ(rotation.z);
            var matRot = math.mul(rotY, math.mul(rotX, rotZ)); // rotation apply order: z(roll), x(pitch), y(yaw) 

            var matTranslation = float4x4.identity;
            var position = transform.position;
            matTranslation.c3 = new float4(position.x, position.y, -position.z, 1.0f);

            return math.mul(matTranslation, math.mul(matRot, matScale));
        }
    }
}