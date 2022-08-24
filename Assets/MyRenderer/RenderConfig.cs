using UnityEngine;

namespace MyRenderer
{
    [CreateAssetMenu(menuName = "MyRenderer/RenderConfig")]
    public class RenderConfig : ScriptableObject
    {
        public int ShadowMapSize = 2048;
        public RenderTarget target;
    }

    public enum RenderTarget
    {
        Color, Depth, ShadowMap
    }
}