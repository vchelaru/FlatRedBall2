# PremultiplyBench

Measures CPU vs GPU alpha premultiply on texture load, for [kniEngine/kni#2740](https://github.com/kniEngine/kni/pull/2740). The PR adds `Texture2D.FromStream(device, stream, Action<byte[]> colorProcessor)` so premultiply runs on the decoded bytes before upload. The counter-proposal is to upload unpremultiplied, then draw into a `RenderTarget2D` with a premultiplying `BlendState`.

Plain KNI 4.3.9001, no FlatRedBall2 engine. The web head borrows `FlatRedBall2.BlazorGL` only as the page host (canvas and tick loop).

## Method

- **Images:** generated at startup by `BenchGame.MakePng`, deterministic, so every head decodes the same bytes (sha256 prefixes are in each results file). Content is sprite-atlas-like: an 8x8 grid of shaded discs on a fully transparent background, half opaque and half translucent, with 1.5 px antialiased edges, a soft translucent glow ring, and ±3 noise inside shapes. PNG sizes: 32 → 2.9 KB, 128 → 41 KB, 512 → 505 KB, 2048 → 5.8 MB, 4096 → 20.5 MB (StbImageWriteSharp encoder).
- **Iterations:** warmup then N timed runs. 32/128: 5+40, 512: 3+15, 2048: 2+7, 4096: 1+5. Median and p90 reported.
- **Metrics:**
  1. `Texture2D.FromStream(device, stream)`: the platform's own decode + upload (WIC on DX, StbImageSharp on SDL2/Blazor, presumably the platform decoder on Android, given it is 2.5x faster than StbImageSharp there).
  2. StbImageSharp decode alone.
  3. CPU premultiply alone, the PR's `DefaultColorProcessors.PremultiplyAlpha` loop copied verbatim.
  4. Upload alone: `new Texture2D` + `SetData`.
  5. GPU premultiply of an already-uploaded texture: `SpriteBatch` draw into a `RenderTarget2D` with `BlendState { ColorSource = SourceAlpha, ColorDest = Zero, AlphaSource = One, AlphaDest = Zero }`, then a 1-pixel `GetData` to wait for the GPU. Variants: (a) new RT each time, (b) pooled RT, (c) pooled RT with no wait (CPU-thread submit cost), (s) the bare 1-pixel `GetData` on a finished RT, which is subtracted as "-sync".
- **Totals:** both paths decode and upload, so CPU path = (1) + (3) and GPU path = (1) + (5a − sync). The brief said "GPU path = upload + 5"; that leaves out the decode the GPU path also needs, so it would make the GPU path look ~10x cheaper than it is.
- **Correctness:** at 512x512, CPU-premultiplied bytes vs GPU RT readback: max diff 0 on DesktopGL, DX11 and web, 1 on Android. All within ±1 (PASS).
- **Builds:** Release everywhere. Web was run twice, `dotnet publish -c Release` with `-p:RunAOTCompilation=true` (WASM AOT) and with `=false` (interpreter), run in headless Chrome 156 with ANGLE on D3D11 (real GPU) through `tools/measure-web.ps1`.

## Results (median ms)

Machine for desktop and web: AMD Ryzen 7 260, NVIDIA GeForce RTX 5060 Laptop GPU (driver 610.47), Windows 11 26200. Full per-metric medians/p90 and machine info: `results/results-<platform>.md` and `.csv`.

### DesktopGL (SDL2, OpenGL 4.6)

| Size | FromStream | CPU premul | GPU premul, new RT −sync | GPU premul, pooled RT −sync | CPU path | GPU path |
|---|---|---|---|---|---|---|
| 32 | 0.12 | 0.02 | 0.86 | 0.11 | 0.13 | 0.97 |
| 128 | 0.57 | 0.05 | 0.76 | 0.09 | 0.63 | 1.34 |
| 512 | 6.93 | 0.81 | 0.87 | 0.25 | 7.74 | 7.80 |
| 2048 | 81.43 | 11.24 | 3.83 | 2.15 | 92.67 | 85.25 |
| 4096 | 385.24 | 64.72 | 41.03 | 12.60 | 449.96 | 426.26 |

### WinForms DX11

| Size | FromStream | CPU premul | GPU premul, new RT −sync | GPU premul, pooled RT −sync | CPU path | GPU path |
|---|---|---|---|---|---|---|
| 32 | 0.15 | 0.02 | 0.41 | 0.02 | 0.17 | 0.56 |
| 128 | 0.65 | 0.05 | 0.36 | 0.01 | 0.70 | 1.01 |
| 512 | 4.06 | 0.70 | 0.55 | 0.04 | 4.76 | 4.61 |
| 2048 | 50.88 | 17.04 | 1.37 | 0.19 | 67.92 | 52.25 |
| 4096 | 156.82 | 55.89 | 2.99 | 5.10 | 212.71 | 159.81 |

### BlazorGL (WASM AOT, headless Chrome, ANGLE D3D11)

| Size | FromStream | CPU premul | GPU premul, new RT −sync | GPU premul, pooled RT −sync | CPU path | GPU path |
|---|---|---|---|---|---|---|
| 32 | 0.20 | 0.00 | 1.80 | 0.60 | 0.20 | 2.00 |
| 128 | 1.00 | 0.10 | 1.50 | 0.30 | 1.10 | 2.50 |
| 512 | 9.20 | 0.70 | 1.00 | 0.60 | 9.90 | 10.20 |
| 2048 | 91.50 | 11.10 | 1.70 | 0.20 | 102.60 | 93.20 |
| 4096 | 369.10 | 44.90 | 4.90 | 1.00 | 414.00 | 374.00 |

### BlazorGL (WASM interpreter, no AOT, headless Chrome, ANGLE D3D11)

Same Release publish with `-p:RunAOTCompilation=false`. Decode is ~8x and the CPU premultiply loop ~4.7x slower than AOT; the GPU step is unchanged, so the GPU route wins by more here.

| Size | FromStream | CPU premul | GPU premul, new RT −sync | GPU premul, pooled RT −sync | CPU path | GPU path |
|---|---|---|---|---|---|---|
| 32 | 0.55 | 0.00 | 0.90 | 0.40 | 0.55 | 1.45 |
| 128 | 5.90 | 0.20 | 1.15 | 0.35 | 6.10 | 7.05 |
| 512 | 72.40 | 3.30 | 1.20 | 0.40 | 75.70 | 73.60 |
| 2048 | 716.70 | 52.30 | 2.70 | 0.40 | 769.00 | 719.40 |
| 4096 | 3025.50 | 210.00 | 4.50 | 1.00 | 3235.50 | 3030.00 |

### Android (slow device)

MediaTek MT8766, PowerVR Rogue GE8300, OpenGL ES 3.2, Android 12 (Phh-Treble GSI), 4 cores.

| Size | FromStream | CPU premul | GPU premul, new RT −sync | GPU premul, pooled RT −sync | CPU path | GPU path |
|---|---|---|---|---|---|---|
| 32 | 1.92 | 0.05 | 1.88 | 1.43 | 1.98 | 3.80 |
| 128 | 4.67 | 0.85 | 1.75 | 1.45 | 5.52 | 6.42 |
| 512 | 39.76 | 14.04 | 1.89 | 1.46 | 53.80 | 41.65 |
| 2048 | 532.04 | 218.97 | 30.30 | 7.40 | 751.00 | 562.34 |
| 4096 | 1873.75 | 877.02 | 119.53 | 28.18 | 2750.77 | 1993.28 |

### Android (fast device): Motorola Edge 2025, MediaTek MT6878, Mali-G615 MC2, Android 16

| Size | FromStream | CPU premul | GPU new RT −sync | GPU pooled −sync | CPU path | GPU path |
|---|---|---|---|---|---|---|
| 32 | 1.27 | 0.01 | 0.59 | 0.37 | 1.28 | 1.87 |
| 128 | 2.50 | 0.12 | 0.50 | 0.35 | 2.62 | 3.00 |
| 512 | 17.04 | 1.95 | 0.62 | 0.36 | 18.99 | 17.66 |
| 2048 | 200.42 | 30.82 | 2.86 | 0.83 | 231.25 | 203.28 |
| 4096 | 768.24 | 122.88 | 10.67 | 3.73 | 891.12 | 778.90 |

## Conclusion

- **The GPU premultiply step wins from 512x512 on DX11, both Android devices and the web interpreter, and from 2048x2048 on DesktopGL and web AOT.** The gap grows with size: at 2048, 1.4–30 ms on the GPU vs 11–219 ms on the CPU.
- **Below that the CPU wins.** At 32 and 128 the CPU loop costs 0.00–0.85 ms, less than the fixed cost of creating an RT and issuing a draw (0.4–1.9 ms). 512 is the crossover: CPU is marginally faster on DesktopGL (7.74 vs 7.80 ms path total) and web AOT (9.90 vs 10.20 ms), GPU is ahead on DX11, Android and the web interpreter. With a pooled RT the GPU step is already faster at 512 everywhere.
- **Decode dominates either way.** FromStream is 68–94% of the CPU path's total at 4096, so at 4096 the end-to-end saving from the GPU route is 5% (DesktopGL), 10% (web AOT), 6% (web interpreter), 13% (fast Android), 25% (DX11) and 28% (slow Android).
- **RT allocation is most of the GPU cost on GL.** From 2048 up, a pooled RT is 2–4x cheaper than a new one on DesktopGL and Android. On DX11 it barely matters.
- **On the CPU thread the GPU draw is nearly free** if nothing waits on it (submit-only 0.002–1.3 ms), so in a game loop the GPU path costs even less than the synced numbers show.

## Caveats

- **The sync cost is not constant on DesktopGL.** The 1-pixel `GetData` on a finished RT takes 0.25 ms at 512 and 19.6 ms at 4096 on GL but stays flat (~0.02–0.06 ms) on DX11. My guess, not verified: KNI's GL `GetData` reads back more than the requested rectangle. The "−sync" columns subtract it. If that subtraction is wrong, the raw synced numbers are in the results files, and they still favour the GPU from 2048 up.
- **The CPU loop is the PR's scalar loop.** A SIMD version, or premultiplying inside the decoder's output loop, would narrow the gap. Nobody measured that here.
- **Android `FromStream` is not StbImageSharp.** It is 2.5x faster than StbImageSharp on that device (532 vs 1314 ms at 2048).
- **Web timer resolution is 0.1 ms** (Chrome coarsens `performance.now`), so the 32/128 web rows are close to the noise floor. The run was headless; a visible tab may differ.
- **Thermal:** each Android run lasts about 3 minutes and the 4096 batch runs last. Throttling on the slow device was not controlled for.
- **Not measured: item 6, `UNPACK_PREMULTIPLY_ALPHA_WEBGL`.** KNI's BlazorGL backend exposes no `pixelStorei` hook, and reaching its WebGL context from JS would have needed a separate harness. Skipped for time.
- The GPU path temporarily holds two textures (source + RT) and returns a `RenderTarget2D`, not a plain `Texture2D`. This benchmark does not measure memory.

## Running

```
dotnet run -c Release --project diagnostics/PremultiplyBench/PremultiplyBench.DesktopGL -- <outDir>
dotnet run -c Release --project diagnostics/PremultiplyBench/PremultiplyBench.WindowsDX -- <outDir>
dotnet publish diagnostics/PremultiplyBench/PremultiplyBench.BlazorGL -c Release -p:RunAOTCompilation=true -o <pub>
pwsh tools/measure-web.ps1 -StaticDir <pub>/wwwroot -StopPattern '\[pmb-done\]' -RunSeconds 480 -TimeoutSeconds 540 -OutFile web.log
pwsh diagnostics/PremultiplyBench/run-android.ps1 -Serial <adb serial> -Label <slow|fast>
```

Desktop heads exit when done and write `results-<platform>.md/.csv` to `<outDir>` (default `bin/.../results`). The web head prints every results line to the console with a `[pmb]` prefix.
