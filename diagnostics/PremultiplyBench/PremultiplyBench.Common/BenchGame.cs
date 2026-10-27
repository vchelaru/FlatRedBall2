using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StbImageSharp;
using StbImageWriteSharp;

namespace PremultiplyBench;

/// <summary>
/// CPU premultiply (kniEngine/kni#2740's colorProcessor) vs GPU premultiply (render into a
/// RenderTarget2D with a premultiplying BlendState). Runs one (size, metric) batch per Draw so the
/// window/browser stays alive, writes results-&lt;platform&gt;.md/.csv, then reports done.
/// </summary>
public class BenchGame : Game
{
    // Heads set these.
    public static string PlatformName = "unknown";
    public static Func<string> ExtraInfo = () => "";
    public static Action<string, string> WriteResult = (name, text) =>
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "results");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name), text);
    };
    public static bool ExitWhenDone = true;

    static readonly int[] Sizes = { 32, 128, 512, 2048, 4096 };

    static readonly BlendState PremultiplyBlend = new()
    {
        ColorSourceBlend = Blend.SourceAlpha,
        ColorDestinationBlend = Blend.Zero,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.Zero,
    };

    readonly GraphicsDeviceManager _graphics;
    SpriteBatch _spriteBatch = null!;
    IEnumerator<bool>? _steps;
    bool _done;
    readonly uint[] _onePixel = new uint[1];

    public BenchGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            GraphicsProfile = GraphicsProfile.HiDef, // 4096 textures need more than Reach's 2048 cap
            PreferredBackBufferWidth = 320,
            PreferredBackBufferHeight = 180,
            SynchronizeWithVerticalRetrace = false,
        };
        IsFixedTimeStep = false;
    }

    protected override void LoadContent() => _spriteBatch = new SpriteBatch(GraphicsDevice);

    protected override void Draw(GameTime gameTime)
    {
        if (!_done)
        {
            _steps ??= Steps().GetEnumerator();
            _done = !_steps.MoveNext();
            if (_done && ExitWhenDone) Exit();
        }
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(_done ? Color.DarkGreen : Color.Black);
        base.Draw(gameTime);
    }

    IEnumerable<bool> Steps()
    {
        var gd = GraphicsDevice;
        var csv = new StringBuilder("platform,size,metric,n,median_ms,p90_ms\n");
        var summary = new StringBuilder();
        var detail = new StringBuilder();
        string correctness = "not run";
        string hashes = "";
        yield return true;

        foreach (int size in Sizes)
        {
            byte[] png = MakePng(size);
            hashes += $"{size}: {png.Length / 1024.0:F1} KB, sha256 {Convert.ToHexString(SHA256.HashData(png))[..12]}; ";
            byte[] decoded = ImageResult.FromMemory(png, StbImageSharp.ColorComponents.RedGreenBlueAlpha).Data;
            byte[] scratch = new byte[decoded.Length];
            (int warm, int n) = size switch { <= 128 => (5, 40), <= 512 => (3, 15), <= 2048 => (2, 7), _ => (1, 5) };
            var m = new Dictionary<string, (double med, double p90)>();

            void Record(string name, double[] s)
            {
                var r = Stats(s);
                m[name] = r;
                csv.Append(CultureInfo.InvariantCulture, $"{PlatformName},{size},{name},{s.Length},{r.med:F3},{r.p90:F3}\n");
            }

            Record("1_FromStream", Time(warm, n, () => Texture2D.FromStream(gd, new MemoryStream(png))));
            yield return true;
            Record("2_StbDecode", Time(warm, n, () => { ImageResult.FromMemory(png, StbImageSharp.ColorComponents.RedGreenBlueAlpha); return null; }));
            yield return true;
            Record("3_CpuPremultiply", Time(warm, n, () => { Premultiply(scratch); return null; },
                pre: () => Buffer.BlockCopy(decoded, 0, scratch, 0, decoded.Length)));
            yield return true;
            Record("4_Upload", Time(warm, n, () => Upload(gd, size, decoded)));
            yield return true;

            var src = Upload(gd, size, decoded);
            var pooled = new RenderTarget2D(gd, size, size, false, SurfaceFormat.Color, DepthFormat.None);
            DrawPremultiplied(src, pooled);
            Sync(pooled); // src upload and first draw are finished before any timing below

            Record("5a_GpuPremul_NewRT_Synced", Time(warm, n, () =>
            {
                var rt = new RenderTarget2D(gd, size, size, false, SurfaceFormat.Color, DepthFormat.None);
                DrawPremultiplied(src, rt);
                Sync(rt);
                return rt;
            }));
            yield return true;
            Record("5b_GpuPremul_PooledRT_Synced", Time(warm, n, () => { DrawPremultiplied(src, pooled); Sync(pooled); return null; }));
            yield return true;
            Record("5c_GpuPremul_PooledRT_SubmitOnly", Time(warm, n, () => { DrawPremultiplied(src, pooled); return null; }));
            Sync(pooled);
            Record("5s_SyncOnly_FinishedRT", Time(warm, n, () => { Sync(pooled); return null; }));
            yield return true;

            if (size == 512)
            {
                Buffer.BlockCopy(decoded, 0, scratch, 0, decoded.Length);
                Premultiply(scratch);
                DrawPremultiplied(src, pooled);
                byte[] gpu = new byte[decoded.Length];
                pooled.GetData(gpu);
                int maxDiff = 0, over1 = 0;
                for (int i = 0; i < gpu.Length; i++)
                {
                    int d = Math.Abs(gpu[i] - scratch[i]);
                    if (d > maxDiff) maxDiff = d;
                    if (d > 1) over1++;
                }
                correctness = $"512x512: max per-channel diff CPU vs GPU = {maxDiff}, channels off by more than 1 = {over1} of {gpu.Length} -> {(maxDiff <= 1 ? "PASS" : "FAIL")}";
            }
            src.Dispose();
            pooled.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            double sync = m["5s_SyncOnly_FinishedRT"].med, fromStream = m["1_FromStream"].med, cpuStep = m["3_CpuPremultiply"].med;
            double gpuNew = m["5a_GpuPremul_NewRT_Synced"].med, gpuPooled = m["5b_GpuPremul_PooledRT_Synced"].med;
            double gpuNet = Math.Max(0, gpuNew - sync);
            summary.Append(CultureInfo.InvariantCulture,
                $"| {size} | {fromStream:F2} | {cpuStep:F2} | {gpuNew:F2} | {gpuNet:F2} | {gpuPooled:F2} | {Math.Max(0, gpuPooled - sync):F2} | {m["5c_GpuPremul_PooledRT_SubmitOnly"].med:F3} | {sync:F3} | {fromStream + cpuStep:F2} | {fromStream + gpuNet:F2} | {(cpuStep <= gpuNet ? "CPU" : "GPU")} |\n");
            foreach (var kv in m)
                detail.Append(CultureInfo.InvariantCulture, $"| {size} | {kv.Key} | {kv.Value.med:F3} | {kv.Value.p90:F3} |\n");
            yield return true;
        }

#if DEBUG
        const string config = "Debug";
#else
        const string config = "Release";
#endif
        var md = new StringBuilder();
        md.Append($"## {PlatformName}\n\n");
        md.Append($"- Adapter: {SafeAdapter(gd)}\n");
        md.Append($"- OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture}), runtime {RuntimeInformation.FrameworkDescription}, {Environment.ProcessorCount} logical CPUs\n");
        md.Append($"- Build: {config}\n");
        foreach (var line in ExtraInfo().Split('\n', StringSplitOptions.RemoveEmptyEntries)) md.Append($"- {line}\n");
        md.Append($"- Images: {hashes}\n");
        md.Append($"- Correctness: {correctness}\n\n");
        md.Append("Summary (median ms). Both paths decode and upload (FromStream); they differ only in the premultiply step. CPU path = FromStream + CPU premultiply. GPU path = FromStream + GPU premultiply into a new RT, synced, minus the bare readback cost (sync). Submit-only = CPU-thread cost of the pooled draw when nothing waits on the GPU.\n\n");
        md.Append("| Size | FromStream | CPU premul | GPU premul new RT | GPU new RT -sync | GPU pooled RT | GPU pooled -sync | GPU pooled submit-only | sync | CPU path | GPU path | Faster premul |\n|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        md.Append(summary);
        md.Append("\n<details><summary>Per-metric median / p90 (ms)</summary>\n\n| Size | Metric | Median | p90 |\n|---|---|---|---|\n");
        md.Append(detail);
        md.Append("\n</details>\n");

        WriteResult($"results-{PlatformName}.md", md.ToString());
        WriteResult($"results-{PlatformName}.csv", csv.ToString());
        WriteResult($"results-{PlatformName}.done", "done");
    }

    static string SafeAdapter(GraphicsDevice gd)
    {
        try { return gd.Adapter.Description; } catch (Exception e) { return "n/a (" + e.GetType().Name + ")"; }
    }

    static Texture2D Upload(GraphicsDevice gd, int size, byte[] data)
    {
        var t = new Texture2D(gd, size, size, false, SurfaceFormat.Color);
        t.SetData(data);
        return t;
    }

    void DrawPremultiplied(Texture2D src, RenderTarget2D dst)
    {
        GraphicsDevice.SetRenderTarget(dst);
        GraphicsDevice.Clear(Color.Transparent);
        _spriteBatch.Begin(SpriteSortMode.Immediate, PremultiplyBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        _spriteBatch.Draw(src, Vector2.Zero, Color.White);
        _spriteBatch.End();
        GraphicsDevice.SetRenderTarget(null);
    }

    // GPU work is async; a 1-pixel readback blocks until everything that writes the RT is done.
    void Sync(RenderTarget2D rt) => rt.GetData(0, new Rectangle(0, 0, 1, 1), _onePixel, 0, 1);

    // Copied from kniEngine/kni#2740 DefaultColorProcessors.PremultiplyAlpha.
    static void Premultiply(byte[] data)
    {
        for (int i = 0; i + 3 < data.Length; i += 4)
        {
            int alpha = data[i + 3];
            if (alpha == 255)
                continue;

            data[i + 0] = (byte)((data[i + 0] * alpha + 127) / 255);
            data[i + 1] = (byte)((data[i + 1] * alpha + 127) / 255);
            data[i + 2] = (byte)((data[i + 2] * alpha + 127) / 255);
        }
    }

    static double[] Time(int warm, int n, Func<IDisposable?> act, Action? pre = null)
    {
        var samples = new double[n];
        for (int i = -warm; i < n; i++)
        {
            pre?.Invoke();
            long t0 = Stopwatch.GetTimestamp();
            var result = act();
            long t1 = Stopwatch.GetTimestamp();
            result?.Dispose();
            if (i >= 0) samples[i] = (t1 - t0) * 1000.0 / Stopwatch.Frequency;
        }
        return samples;
    }

    static (double med, double p90) Stats(double[] s)
    {
        var sorted = s.OrderBy(x => x).ToArray();
        double med = sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
        return (med, sorted[(int)Math.Ceiling(0.9 * sorted.Length) - 1]);
    }

    /// <summary>
    /// Deterministic sprite-atlas-like image: an 8x8 grid of shaded discs (opaque and translucent
    /// interiors, 1.5 px antialiased edges, a soft translucent glow ring) on a fully transparent
    /// background, with +/-3 hash noise inside shapes so it isn't trivially compressible.
    /// Integer math plus Math.Sqrt only, so every platform encodes the same bytes (see sha256 in results).
    /// </summary>
    public static byte[] MakePng(int size)
    {
        int cell = Math.Max(16, size / 8);
        var px = new byte[size * size * 4];
        byte[][] palette = { new byte[] { 230, 60, 50 }, new byte[] { 60, 180, 90 }, new byte[] { 50, 110, 230 }, new byte[] { 240, 200, 40 }, new byte[] { 180, 70, 200 }, new byte[] { 40, 200, 210 } };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int cxI = x / cell, cyI = y / cell, idx = cyI * 97 + cxI;
                double r = cell * 0.36, glow = cell * 0.48;
                double dx = x - (cxI * cell + cell / 2.0), dy = y - (cyI * cell + cell / 2.0);
                double d = Math.Sqrt(dx * dx + dy * dy);
                int interior = idx % 2 == 0 ? 255 : 64 + (idx * 37) % 160;
                int a;
                if (d <= r) a = interior;
                else if (d <= r + 1.5) a = (int)(interior * (r + 1.5 - d) / 1.5);
                else if (d <= glow) a = (int)(96 * (glow - d) / (glow - r));
                else a = 0;
                int o = (y * size + x) * 4;
                if (a == 0) continue;
                var c = palette[idx % palette.Length];
                int shade = 255 - (int)(120 * d / glow); // radial gradient
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                int noise = (int)(h % 7) - 3;
                px[o + 0] = (byte)Math.Clamp(c[0] * shade / 255 + noise, 0, 255);
                px[o + 1] = (byte)Math.Clamp(c[1] * shade / 255 + noise, 0, 255);
                px[o + 2] = (byte)Math.Clamp(c[2] * shade / 255 + noise, 0, 255);
                px[o + 3] = (byte)a;
            }
        }
        using var ms = new MemoryStream();
        new ImageWriter().WritePng(px, size, size, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, ms);
        return ms.ToArray();
    }
}
