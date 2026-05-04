using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
#endif

namespace Zimennik.BayerDither
{
    public class BayerDitherFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Header("Dithering")]
            [Tooltip("Color levels per channel after quantization. Lower = more posterized.")]
            [Range(2, 32)] public int colorDepth = 8;

            [Tooltip("Strength of the 4x4 Bayer pattern. 0 = pure posterization, 1 = full dither.")]
            [Range(0f, 2f)] public float ditherStrength = 1.0f;

            [Header("Pixelization")]
            [Tooltip("Render-resolution divisor. 1 = native, 4 = quarter-resolution chunky pixels.")]
            [Range(1, 8)] public int pixelSize = 1;

            [Header("Color Grading")]
            [Tooltip("Blend toward BT.601 grayscale.")]
            [Range(0f, 1f)] public float desaturation = 0.3f;

            [Tooltip("Multiplies the final color by (1 - darkness).")]
            [Range(0f, 0.8f)] public float darkness = 0.15f;

            [Tooltip("Pushes red, dampens blue. Sepia-like at high values.")]
            [Range(0f, 1f)] public float warmthShift = 0.0f;

            [Header("Film")]
            [Tooltip("Animated film-grain noise amount.")]
            [Range(0f, 0.15f)] public float noiseAmount = 0.0f;

            [Tooltip("Radial darkening toward the edges of the screen.")]
            [Range(0f, 2f)] public float vignetteStrength = 0.0f;

            [Header("Rendering")]
            [Tooltip("Where in the URP queue this effect runs. AfterRenderingPostProcessing is recommended.")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        private const string ShaderPath = "Hidden/PostFX/BayerDither";

        [Tooltip("Reference to the Bayer Dither shader. Auto-resolved if left empty, but assigning it explicitly is recommended for builds.")]
        [SerializeField] private Shader _shader;
        public Settings settings = new Settings();

        private Material _material;
        private BayerDitherPass _pass;

        public override void Create()
        {
            if (_shader == null)
                _shader = Shader.Find(ShaderPath);

            if (_shader == null)
            {
                Debug.LogError($"BayerDitherFeature: shader '{ShaderPath}' not found. Assign it explicitly in the Renderer Feature inspector.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(_shader);
            _pass = new BayerDitherPass(_material, settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _material == null) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            _pass.renderPassEvent = settings.renderPassEvent;
            _pass.UpdateSettings(settings);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CoreUtils.Destroy(_material);
                _pass?.Dispose();
            }
            _material = null;
            _pass = null;
        }

        // -----------------------------------------------------------------------
        // Render pass
        // -----------------------------------------------------------------------

        private class BayerDitherPass : ScriptableRenderPass
        {
            private static readonly int IdColorDepth       = Shader.PropertyToID("_ColorDepth");
            private static readonly int IdDitherStrength   = Shader.PropertyToID("_DitherStrength");
            private static readonly int IdPixelSize        = Shader.PropertyToID("_PixelSize");
            private static readonly int IdDesaturation     = Shader.PropertyToID("_Desaturation");
            private static readonly int IdDarkness         = Shader.PropertyToID("_Darkness");
            private static readonly int IdWarmthShift      = Shader.PropertyToID("_WarmthShift");
            private static readonly int IdNoiseAmount      = Shader.PropertyToID("_NoiseAmount");
            private static readonly int IdVignetteStrength = Shader.PropertyToID("_VignetteStrength");

            private const string PassName = "Bayer Dither";

            private readonly Material _material;
            private Settings _settings;
            private RTHandle _tempHandle;

            public BayerDitherPass(Material material, Settings settings)
            {
                _material = material;
                _settings = settings;
            }

            public void UpdateSettings(Settings settings) => _settings = settings;

            private void ApplySettings()
            {
                if (_material == null) return;
                _material.SetFloat(IdColorDepth,       _settings.colorDepth);
                _material.SetFloat(IdDitherStrength,   _settings.ditherStrength);
                _material.SetFloat(IdPixelSize,        _settings.pixelSize);
                _material.SetFloat(IdDesaturation,     _settings.desaturation);
                _material.SetFloat(IdDarkness,         _settings.darkness);
                _material.SetFloat(IdWarmthShift,      _settings.warmthShift);
                _material.SetFloat(IdNoiseAmount,      _settings.noiseAmount);
                _material.SetFloat(IdVignetteStrength, _settings.vignetteStrength);
            }

            public void Dispose()
            {
                _tempHandle?.Release();
                _tempHandle = null;
            }

            // ---- Compatibility-mode path (URP 14-16, or URP 17 with RenderGraph disabled) ----

#pragma warning disable CS0618 // Execute is obsolete in URP 17 but still required for compatibility mode
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null) return;

                ApplySettings();

                var cmd = CommandBufferPool.Get(PassName);
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;

                RenderingUtils.ReAllocateIfNeeded(
                    ref _tempHandle, desc,
                    FilterMode.Bilinear, TextureWrapMode.Clamp,
                    name: "_BayerDitherTemp");

                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

                Blitter.BlitCameraTexture(cmd, source, _tempHandle, _material, 0);
                Blitter.BlitCameraTexture(cmd, _tempHandle, source);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#pragma warning restore CS0618

            // ---- RenderGraph path (URP 17+) ----

#if UNITY_6000_0_OR_NEWER
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null) return;

                ApplySettings();

                var resourceData = frameData.Get<UniversalResourceData>();
                var source = resourceData.activeColorTexture;
                if (!source.IsValid()) return;

                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "_BayerDitherTemp";
                desc.clearBuffer = false;
                desc.depthBufferBits = DepthBits.None;
                var temp = renderGraph.CreateTexture(desc);

                var blitParams = new RenderGraphUtils.BlitMaterialParameters(source, temp, _material, 0);
                renderGraph.AddBlitPass(blitParams, PassName);

                resourceData.cameraColor = temp;
            }
#endif
        }
    }
}
