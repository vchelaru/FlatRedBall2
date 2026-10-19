using FlatRedBall2;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Solitaire.Screens;

namespace Solitaire;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        // Apos.Shapes needs SM 4.0+; KNI's FL10_0 profile provides it. The Reach default crashes at startup.
        _graphics.GraphicsProfile = GraphicsProfile.FL10_0;
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        var ds = FlatRedBallService.Default.DisplaySettings;
        const int height = 640;
        ds.ResolutionWidth = 800;
        ds.ResolutionHeight = height;
        ds.PreferredWindowWidth = 800;
        ds.PreferredWindowHeight = height;
        ds.AllowUserResizing = true;

        FlatRedBallService.Default.PrepareWindow<GameScreen>(_graphics);
    }

    protected override void Initialize()
    {
        base.Initialize();
        if (FlatRedBall2.ProbeFlags.Has("bundle")) BundleBench();
        // Path is relative to the Content root; do NOT prepend "Content/".
        // Gum 2026.6 selects loose vs. bundle purely from the extension (no sibling probing),
        // so this must match the deployed artifact. The build defines GUM_BUNDLE when
        // UseGumPackage=true (the default) — see Solitaire.GumPackage.targets.
        FlatRedBallService.Default.Initialize(this, new EngineInitSettings
        {
#if GUM_BUNDLE
            GumProjectFile = FlatRedBall2.ProbeFlags.Get("pkg") is { } pk ? $"GumProject/v_{pk}/GumProject.gumpkg" : "GumProject/GumProject.gumpkg",
#else
            GumProjectFile = "GumProject/GumProject.gumj",
#endif
            // Solitaire's cards zoom with Camera.Zoom, so crisp re-rasterized text matters here —
            // most FRB2 projects default this off (see EngineInitSettings.UseFontOversampling).
            UseFontOversampling = true,
            ProfileStartup = true
        });
        FlatRedBallService.Default.Start<GameScreen>();
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();
        if (FlatRedBall2.ProbeFlags.AfterScreen is { } a) { FlatRedBall2.ProbeFlags.AfterScreen = null; a(); }
        FlatRedBallService.Default.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        FlatRedBallService.Default.Draw();
        base.Draw(gameTime);
    }

    private static void BundleBench()
    {
        var L = (Action<string>)FlatRedBall2.ProbeFlags.Log;
        long t = FlatRedBall2.ProbeFlags.Now();
        byte[] bytes;
        using (var st = Microsoft.Xna.Framework.TitleContainer.OpenStream(FlatRedBall2.ProbeFlags.Get("pkg") is { } pk ? $"Content/GumProject/v_{pk}/GumProject.gumpkg" : "Content/GumProject/GumProject.gumpkg"))
        using (var ms = new System.IO.MemoryStream()) { st.CopyTo(ms); bytes = ms.ToArray(); }
        L($"bundle fetch+read {bytes.Length} bytes: {FlatRedBall2.ProbeFlags.Ms(t):F1}ms");
        const int header = 5; // GUMP + version, see GumBundleFormat.HeaderLength (verified at runtime below)
        int hl = Gum.Bundle.GumBundleFormat.HeaderLength;

        System.IO.MemoryStream Brotli(out double ms)
        {
            long a = FlatRedBall2.ProbeFlags.Now();
            var input = new System.IO.MemoryStream(bytes, hl, bytes.Length - hl);
            var dec = new System.IO.MemoryStream();
            using (var b = new BrotliSharpLib.BrotliStream(input, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true)) b.CopyTo(dec);
            ms = FlatRedBall2.ProbeFlags.Ms(a); return dec;
        }
        int Tar(System.IO.MemoryStream tar, out double ms)
        {
            long a = FlatRedBall2.ProbeFlags.Now(); int n = 0; long total = 0;
            tar.Position = 0;
            using var r = SharpCompress.Readers.Tar.TarReader.OpenReader(tar, new SharpCompress.Readers.ReaderOptions());
            while (r.MoveToNextEntry())
            {
                if (r.Entry.IsDirectory) continue;
                using var es = r.OpenEntryStream(); using var buf = new System.IO.MemoryStream(); es.CopyTo(buf); total += buf.Length; n++;
            }
            ms = FlatRedBall2.ProbeFlags.Ms(a); return n;
        }
        double m;
        var tarStream = Brotli(out m); L($"brotli decode (BrotliSharpLib) COLD: {m:F1}ms -> {tarStream.Length} tar bytes");
        int entries = Tar(tarStream, out m); L($"tar parse (SharpCompress) COLD: {m:F1}ms, {entries} entries");

        // gzip of the same tar
        var gz = new System.IO.MemoryStream();
        using (var g = new System.IO.Compression.GZipStream(gz, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true)) { tarStream.Position = 0; tarStream.CopyTo(g); }
        L($"gzip size {gz.Length} bytes (brotli {bytes.Length - hl})");
        long ga = FlatRedBall2.ProbeFlags.Now();
        var un = new System.IO.MemoryStream(); gz.Position = 0;
        using (var g = new System.IO.Compression.GZipStream(gz, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true)) g.CopyTo(un);
        L($"gzip decode COLD: {FlatRedBall2.ProbeFlags.Ms(ga):F1}ms -> {un.Length} bytes");
        for (int i = 0; i < 2; i++)
        {
            var ts = Brotli(out m); L($"brotli decode warm#{i}: {m:F1}ms");
            Tar(ts, out m); L($"tar parse warm#{i}: {m:F1}ms");
            long gb = FlatRedBall2.ProbeFlags.Now(); var u2 = new System.IO.MemoryStream(); gz.Position = 0;
            using (var g = new System.IO.Compression.GZipStream(gz, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true)) g.CopyTo(u2);
            L($"gzip decode warm#{i}: {FlatRedBall2.ProbeFlags.Ms(gb):F1}ms");
        }
    }
}
