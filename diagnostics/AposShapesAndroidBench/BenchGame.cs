using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Android.Opengl;
using Apos.Shapes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AposShapesAndroidBench;

// Modes, one per cold launch so one mode's caches can't help another:
//   batch - new ShapeBatch(GraphicsDevice, Content) end to end, then the first circle draws.
//   split - the same work split into: read+decompress the embedded .knifx (managed),
//           new Effect (KNI parse + GL shader compiles), the rest of the ShapeBatch ctor.
//   gl    - glCompileShader + COMPILE_STATUS on every GLSL shader in the .knifx, directly
//           through GLES20, in file order. Driver compile only, no KNI.
//   glrev - gl in reverse order, to tell a variant's own cost from a slowdown over the run.
// The work runs on the first Update after one plain frame has been drawn.
public class BenchGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly string _mode;
    private ShapeBatch? _batch;
    private int _plainFrames;
    private int _circleDraws;
    private bool _done;

    public BenchGame(string mode)
    {
        _mode = mode;
        _graphics = new GraphicsDeviceManager(this)
        {
            // KNI's Android backend rejects FL10_0, so HiDef (same as diagnostics/AndroidSpike).
            GraphicsProfile = GraphicsProfile.HiDef,
            IsFullScreen = true,
        };
    }

    protected override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (_plainFrames < 1 || _done)
            return;
        _done = true;
        Bench.Log($"GL_RENDERER={GLES20.GlGetString(GLES20.GlRenderer)} GL_VERSION={GLES20.GlGetString(GLES20.GlVersion)}");
        var total = Stopwatch.StartNew();
        switch (_mode)
        {
            case "split": RunSplit(); break;
            case "gl": RunGl(reverse: false); break;
            case "glrev": RunGl(reverse: true); break;
            default: RunBatch(); break;
        }
        Bench.Log($"[{_mode}] work total: {total.ElapsedMilliseconds} ms");
    }

    private void RunBatch()
    {
        var sw = Stopwatch.StartNew();
        _batch = new ShapeBatch(GraphicsDevice, Content);
        Bench.Log($"[batch] new ShapeBatch: {sw.ElapsedMilliseconds} ms");
    }

    private void RunSplit()
    {
        var sw = Stopwatch.StartNew();
        var bytes = Bench.ReadKnifx(out var name);
        Bench.Log($"[split] read+decompress {name} ({bytes.Length} bytes): {sw.ElapsedMilliseconds} ms");
        sw.Restart();
        var effect = new Effect(GraphicsDevice, bytes);
        Bench.Log($"[split] new Effect: {sw.ElapsedMilliseconds} ms");
        sw.Restart();
        _batch = new ShapeBatch(GraphicsDevice, Content, effect);
        Bench.Log($"[split] new ShapeBatch(effect) remainder: {sw.ElapsedMilliseconds} ms");
    }

    private void RunGl(bool reverse)
    {
        var bytes = Bench.ReadKnifx(out _);
        var shaders = Bench.ExtractGlsl(bytes);
        if (reverse)
            shaders.Reverse();
        var seen = new HashSet<string>();
        long totalMs = 0, uniqueMs = 0, uniqueChars = 0;
        int i = 0;
        foreach (var (isFragment, source) in shaders)
        {
            bool dup = !seen.Add(source);
            // The .knifx stores each shader twice. Recompiling identical text costs the full
            // time again on PowerVR, which would double the run, and KNI's own Effect does not
            // pay it (new Effect ~= the unique-only total), so skip duplicates.
            if (dup)
            {
                Bench.Log($"[gl] #{i++} {(isFragment ? "FS" : "VS")} {source.Length} chars (duplicate text): skipped");
                continue;
            }
            var sw = Stopwatch.StartNew();
            int s = GLES20.GlCreateShader(isFragment ? GLES20.GlFragmentShader : GLES20.GlVertexShader);
            GLES20.GlShaderSource(s, source);
            GLES20.GlCompileShader(s);
            var status = new int[1];
            GLES20.GlGetShaderiv(s, GLES20.GlCompileStatus, status, 0);
            long ms = sw.ElapsedMilliseconds;
            GLES20.GlDeleteShader(s);
            totalMs += ms;
            if (!dup) { uniqueMs += ms; uniqueChars += source.Length; }
            Bench.Log($"[gl] #{i++} {(isFragment ? "FS" : "VS")} {source.Length} chars{(dup ? " (duplicate text)" : "")} status={status[0]}: {ms} ms");
        }
        Bench.Log($"[gl] {shaders.Count} shaders ({seen.Count} unique, {uniqueChars} unique chars): all {totalMs} ms, unique only {uniqueMs} ms");
    }

    protected override void Draw(GameTime gameTime)
    {
        var sw = Stopwatch.StartNew();
        GraphicsDevice.Clear(Color.CornflowerBlue);
        if (_batch != null)
        {
            var vp = GraphicsDevice.Viewport;
            _batch.Begin();
            _batch.FillCircle(new Vector2(vp.Width / 2f, vp.Height / 2f), vp.Width / 4f, Color.OrangeRed);
            _batch.End();
        }
        base.Draw(gameTime);
        if (_batch == null)
        {
            _plainFrames++;
            return;
        }
        _circleDraws++;
        if (_circleDraws <= 2)
            Bench.Log($"[{_mode}] circle draw #{_circleDraws}: {sw.ElapsedMilliseconds} ms");
    }
}

static class Bench
{
    public static string AposVersion => typeof(ShapeBatch).Assembly.GetName().Version?.ToString() ?? "?";

    public static void Log(string message) => Android.Util.Log.Info("APOSBENCH", message);

    /// <summary>Reads Apos.Shapes' embedded KNI effect: raw in 0.8.6, gzipped in 0.9.1.</summary>
    public static byte[] ReadKnifx(out string name)
    {
        var asm = typeof(ShapeBatch).Assembly;
        name = asm.GetManifestResourceNames().Single(n => n.Contains(".knifx"));
        using var raw = asm.GetManifestResourceStream(name)!;
        using Stream s = name.EndsWith(".gz") ? new GZipStream(raw, CompressionMode.Decompress) : raw;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Pulls each GLSL shader out of the .knifx as a printable-text run containing "void main".</summary>
    public static List<(bool isFragment, string source)> ExtractGlsl(byte[] knifx)
    {
        var text = Encoding.Latin1.GetString(knifx);
        var list = new List<(bool, string)>();
        foreach (Match m in Regex.Matches(text, @"[\t\n\r\x20-\x7e]{200,}"))
        {
            if (!m.Value.Contains("void main"))
                continue;
            // The run can start with printable header bytes; the GLSL starts at the first
            // directive and is preceded by its int32 little-endian length.
            int p = m.Index + m.Value.IndexOf('#');
            int len = BitConverter.ToInt32(knifx, p - 4);
            var src = text.Substring(p, len);
            list.Add((!src.Contains("gl_Position"), src));
        }
        return list;
    }
}
