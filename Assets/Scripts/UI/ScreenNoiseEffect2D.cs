// ScreenNoiseEffect2D.cs — 2D 屏幕噪点后处理（简化版）
// 挂在主摄像机上，产生 CRT 风格噪点效果
using UnityEngine;

namespace SWO1.UI
{
    [RequireComponent(typeof(Camera))]
    public class ScreenNoiseEffect2D : MonoBehaviour
    {
        [Range(0f, 1f)] public float noiseIntensity = 0.08f;
        [Range(0f, 0.1f)] public float scanlineIntensity = 0.05f;
        public bool enableVignette = true;

        private Material _mat;
        private static readonly int IntensityID = Shader.PropertyToID("_NoiseIntensity");
        private static readonly int ScanlineID = Shader.PropertyToID("_ScanlineIntensity");
        private static readonly int VignetteID = Shader.PropertyToID("_VignetteEnabled");

        void Start()
        {
            var shader = Shader.Find("Hidden/ScreenNoise2D");
            if (shader != null && shader.isSupported)
                _mat = new Material(shader);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (_mat == null)
            {
                Graphics.Blit(src, dest);
                return;
            }

            _mat.SetFloat(IntensityID, noiseIntensity);
            _mat.SetFloat(ScanlineID, scanlineIntensity);
            _mat.SetInt(VignetteID, enableVignette ? 1 : 0);
            Graphics.Blit(src, dest, _mat);
        }
    }
}
