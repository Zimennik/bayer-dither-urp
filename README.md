# Bayer Dither Post-Process for URP

A single-pass URP post-process renderer feature that combines **4×4 Bayer ordered dithering**, **color quantization**, **pixelization**, **warmth shift**, **desaturation**, **animated film grain** and **vignette** into one cheap fullscreen pass. Designed for retro / lo-fi / PSX-style looks.

Made for [Ludum Dare](https://ldjam.com/) — drop it into your jam project and tweak.

<!-- TODO: replace with a real before/after gif -->
![preview](Documentation~/images/preview.gif)

## Features

- 4×4 Bayer ordered dithering with adjustable strength
- Per-channel color depth quantization (2–32 levels)
- Resolution divisor for chunky retro pixels
- Warmth shift (sepia-style red push, blue cut)
- BT.601 desaturation
- Darkness multiplier
- Animated film grain
- Radial vignette
- Single fullscreen pass — cheap on mobile and WebGL

## Compatibility

| Unity | URP | Render path |
|---|---|---|
| Unity 6 (6000.0+) | URP 17+ | RenderGraph (default) and Compatibility Mode |
| Unity 2022.3 LTS / 2023 LTS | URP 14–16 | Legacy `Execute` |

The feature ships with two render paths in one class — RenderGraph is used automatically when available, otherwise the legacy `Execute` path runs.

XR / single-pass stereo is not supported (untested — pull requests welcome).

## Installation

Open **Window → Package Manager → + → Install package from git URL…** and paste:

```
https://github.com/zimennik/bayer-dither-urp.git
```

To pin a version, append `#v1.0.0` (any tag) to the URL.

Alternatively, add it to `Packages/manifest.json` under `dependencies`:

```json
"com.zimennik.bayer-dither-urp": "https://github.com/zimennik/bayer-dither-urp.git"
```

## Usage

1. Open your **URP Renderer asset** (the one referenced by your URP Pipeline asset, not the Pipeline asset itself).
2. Click **Add Renderer Feature → Bayer Dither Feature**.
3. Drag the `Hidden/PostFX/BayerDither` shader into the **Shader** field on the feature (auto-resolved in the editor, but explicit assignment is required for builds — otherwise Unity may strip the shader).
4. Tweak the parameters live in Play Mode.

<!-- TODO: replace with a screenshot of the feature inspector -->
![inspector](Documentation~/images/inspector.png)

## Parameter reference

| Parameter | Range | Description |
|---|---|---|
| Color Depth | 2–32 | Color levels per channel after quantization. Lower = more posterized. |
| Dither Strength | 0–2 | How strongly the Bayer pattern is applied. `0` = pure posterization, `1` = full dither. |
| Pixel Size | 1–8 | Render-resolution divisor. `1` = native, `4` = quarter-resolution chunky pixels. |
| Desaturation | 0–1 | Blend toward BT.601 grayscale. |
| Darkness | 0–0.8 | Multiplies the final color by `(1 - darkness)`. |
| Warmth Shift | 0–1 | Pushes red channel up, blue down. Sepia-like at high values. |
| Noise Amount | 0–0.15 | Animated film-grain noise amount. |
| Vignette Strength | 0–2 | Radial darkening toward the screen edges. |
| Render Pass Event | enum | Where in the URP queue this effect runs. `AfterRenderingPostProcessing` is recommended. |

## Animating settings at runtime

The feature exposes its `settings` field publicly. Read it from a `MonoBehaviour` and write to it whenever you want — changes are applied next frame:

```csharp
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zimennik.BayerDither;

public class FadeToBleak : MonoBehaviour
{
    [SerializeField] private UniversalRendererData _rendererData;

    private BayerDitherFeature _feature;

    private void Awake()
    {
        foreach (var f in _rendererData.rendererFeatures)
            if (f is BayerDitherFeature dither) _feature = dither;
    }

    public void SetIntensity(float t)
    {
        var s = _feature.settings;
        s.colorDepth        = Mathf.RoundToInt(Mathf.Lerp(16, 4,    t));
        s.ditherStrength    = Mathf.Lerp(0.5f, 1.8f, t);
        s.pixelSize         = Mathf.RoundToInt(Mathf.Lerp(1,  4,    t));
        s.desaturation      = Mathf.Lerp(0.1f, 0.6f, t);
        s.darkness          = Mathf.Lerp(0.05f, 0.4f, t);
        s.warmthShift       = Mathf.Lerp(0.1f, 0.7f, t);
        s.noiseAmount       = Mathf.Lerp(0.02f, 0.1f, t);
        s.vignetteStrength  = Mathf.Lerp(0.3f, 1.5f, t);
    }
}
```

## Tips

- **Subtle is usually better.** Color depth around 12–16, dither strength around 0.6–0.8, pixel size 1–2, desaturation under 0.3.
- **Pure posterization look:** set `Dither Strength = 0`. The Bayer pattern disappears, leaving hard color bands.
- **Pure dither without quantization:** crank `Color Depth` to 32 and dither strength to 1 — you get noise-textured originals.
- **Pixel-art mode:** `Pixel Size = 4`, `Color Depth = 8`, `Dither Strength = 1`.
- **Make sure the shader is in your build.** If the post-process disappears in a build, either keep the shader assigned in the feature's inspector, or add `Hidden/PostFX/BayerDither` to **Project Settings → Graphics → Always Included Shaders**.

## License

[MIT](LICENSE.md) — do whatever, attribution appreciated.

## Acknowledgements

Built for [Ludum Dare 59](https://ldjam.com/events/ludum-dare/59) and extracted from the game [*LD59 Signal*](https://github.com/zimennik/LD59_Signal). Bayer matrix values from the classic 1973 paper by Bryce E. Bayer.
