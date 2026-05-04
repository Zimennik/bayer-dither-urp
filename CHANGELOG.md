# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-05-04

### Added

- Initial release.
- `BayerDitherFeature` — URP `ScriptableRendererFeature` with both RenderGraph (URP 17+) and legacy `Execute` paths.
- `Hidden/PostFX/BayerDither` shader with 4x4 Bayer ordered dithering, color depth quantization, pixelization, warmth shift, desaturation, darkness, animated film grain and radial vignette.
