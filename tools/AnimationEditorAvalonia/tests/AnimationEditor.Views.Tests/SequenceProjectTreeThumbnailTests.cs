using AnimationEditor.App.Services;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Views.Tests;

// The Open Project Folder row of a .seqj gets the composite of its first sequence at the first instant where something is
// visible, drawn by the same code as the sequence list rows, and cached on disk under a key covering every input.
public class SequenceProjectTreeThumbnailTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), "ae-seqthumb-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, true);
    }

    private static byte[] Png(int w, int h, SKColor color)
    {
        using var bm = new SKBitmap(w, h);
        bm.Erase(color);
        using var img = SKImage.FromBitmap(bm);
        return img.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    private static string Achj(string chain, string texture)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        var save = new AnimationChainSave { Name = chain };
        save.Frames.Add(new AnimationFrameSave { TextureName = texture, FrameLength = 0.2f, RightCoordinate = 1, BottomCoordinate = 1 });
        list.AnimationChains.Add(save);
        return list.ToJsonString();
    }

    private static string Seqj(string sourcesAndTracks) => $$"""{ "version": 1, "sources": ["body/body.achj", "fx/fx.achj"], "sequences": [ { "name": "Attack", "tracks": [ {{sourcesAndTracks}} ] } ] }""";

    private static string Track(string name, string source, string chain, double start) =>
        $$"""{ "name": "{{name}}", "chains": [ { "source": "{{source}}", "chain": "{{chain}}", "start": {{start.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "loops": 1 } ] }""";

    private static Project Make(string seqj)
    {
        var root = new Folder("Content");
        var project = new Project(root)
        {
            Seqj = new File("boss.seqj", Encoding.UTF8.GetBytes(seqj)),
            Body = new File("body/body.achj", Encoding.UTF8.GetBytes(Achj("Walk", "body.png"))),
            Fx = new File("fx/fx.achj", Encoding.UTF8.GetBytes(Achj("Spark", "spark.png"))),
            BodyPng = new File("body/body.png", Png(16, 16, SKColors.Red)),
            FxPng = new File("fx/spark.png", Png(16, 16, SKColors.Blue)),
        };
        foreach (var file in new[] { project.Seqj, project.Body, project.Fx, project.BodyPng, project.FxPng })
            root.Files[file.RootPath] = file;
        project.Entry = new AchxFileEntry(project.Seqj, root, "boss.seqj");
        return project;
    }

    private string[] CacheFiles() => Directory.Exists(_cacheDir) ? Directory.GetFiles(_cacheDir, "*.png") : [];

    [AvaloniaFact]
    public async Task OneTrack_ThumbnailIsTheSpriteFittedLikeAnAchxRow()
    {
        var project = Make(Seqj(Track("Body", "body/body.achj", "Walk", 0)));
        var service = new ProjectTreeThumbnailService(_cacheDir);

        var thumbnail = await service.GetThumbnailAsync(project.Entry!, 28, 28);

        Assert.NotNull(thumbnail);
        Assert.Equal(28, thumbnail!.PixelSize.Width);
        Assert.Equal(28, thumbnail.PixelSize.Height);
    }

    [AvaloniaFact]
    public async Task EveryTrackStartsLater_ShowsTheEarliestInstant()
    {
        var project = Make(Seqj(Track("Body", "body/body.achj", "Walk", 0.3)));
        var service = new ProjectTreeThumbnailService(_cacheDir);

        Assert.NotNull(await service.GetThumbnailAsync(project.Entry!, 28, 28));
    }

    [AvaloniaFact]
    public async Task TwoTracks_CompositeTheTopLaneInFront()
    {
        // Fx is the top lane (first), so its blue square covers the red body where they overlap.
        var project = Make(Seqj(Track("Fx", "fx/fx.achj", "Spark", 0) + "," + Track("Body", "body/body.achj", "Walk", 0)));
        var service = new ProjectTreeThumbnailService(_cacheDir);

        Assert.NotNull(await service.GetThumbnailAsync(project.Entry!, 28, 28));

        using var png = SKBitmap.Decode(CacheFiles().Single());
        Assert.Equal(SKColors.Blue, png.GetPixel(png.Width / 2, png.Height / 2).WithAlpha(255));
    }

    [AvaloniaTheory]
    [InlineData("{ not json")]
    [InlineData("""{ "version": 1, "sources": [], "sequences": [] }""")]
    public async Task MalformedOrEmptyFile_HasNoThumbnail_AndNeverThrows(string text)
    {
        var project = Make(text);

        Assert.Null(await new ProjectTreeThumbnailService(_cacheDir).GetThumbnailAsync(project.Entry!, 28, 28));
    }

    [AvaloniaFact]
    public async Task MissingSource_RendersWhatResolves()
    {
        var project = Make(Seqj(Track("Gone", "missing/none.achj", "Walk", 0) + "," + Track("Fx", "fx/fx.achj", "Spark", 0)));
        var service = new ProjectTreeThumbnailService(_cacheDir);

        Assert.NotNull(await service.GetThumbnailAsync(project.Entry!, 28, 28));
    }

    [AvaloniaFact]
    public async Task NothingResolves_HasNoThumbnail()
    {
        var project = Make(Seqj(Track("Gone", "missing/none.achj", "Walk", 0)));

        Assert.Null(await new ProjectTreeThumbnailService(_cacheDir).GetThumbnailAsync(project.Entry!, 28, 28));
    }

    [AvaloniaFact]
    public async Task SecondGeneration_IsServedFromDisk_WithoutDecodingTheTextures()
    {
        var project = Make(Seqj(Track("Body", "body/body.achj", "Walk", 0)));
        var service = new ProjectTreeThumbnailService(_cacheDir);
        await service.GetThumbnailAsync(project.Entry!, 28, 28);
        project.BodyPng.ReadCount = 0;

        service.InvalidateEntry(project.Entry!);
        Assert.NotNull(await service.GetThumbnailAsync(project.Entry!, 28, 28));

        Assert.Equal(0, project.BodyPng.ReadCount);
    }

    public static IEnumerable<object[]> Inputs() =>
        new[] { "seqj", "source", "png" }.Select(name => new object[] { name });

    [AvaloniaTheory]
    [MemberData(nameof(Inputs))]
    public async Task ChangingAnyInput_ChangesTheCacheKey_AndReplacesTheStaleFile(string input)
    {
        var project = Make(Seqj(Track("Body", "body/body.achj", "Walk", 0)));
        var service = new ProjectTreeThumbnailService(_cacheDir);
        await service.GetThumbnailAsync(project.Entry!, 28, 28);
        var before = Path.GetFileName(CacheFiles().Single());

        var file = input switch { "seqj" => project.Seqj, "source" => project.Body, _ => project.BodyPng };
        file.Modified = file.Modified.AddMinutes(1);
        service.InvalidateEntry(project.Entry!);
        await service.GetThumbnailAsync(project.Entry!, 28, 28);

        var after = Path.GetFileName(CacheFiles().Single());
        Assert.NotEqual(before, after);
        Assert.Equal(before.Split('_')[0], after.Split('_')[0]);
    }

    [AvaloniaFact]
    public async Task ChangingASeqjsTextureContent_ChangesTheThumbnail()
    {
        var project = Make(Seqj(Track("Body", "body/body.achj", "Walk", 0)));
        var service = new ProjectTreeThumbnailService(_cacheDir);
        await service.GetThumbnailAsync(project.Entry!, 28, 28);
        using (var red = SKBitmap.Decode(CacheFiles().Single()))
            Assert.Equal(SKColors.Red, red.GetPixel(14, 14).WithAlpha(255));

        project.BodyPng.Content = Png(16, 16, SKColors.Lime);
        project.BodyPng.Modified = project.BodyPng.Modified.AddMinutes(1);
        service.InvalidateEntry(project.Entry!);
        await service.GetThumbnailAsync(project.Entry!, 28, 28);

        using var green = SKBitmap.Decode(CacheFiles().Single());
        Assert.Equal(SKColors.Lime, green.GetPixel(14, 14).WithAlpha(255));
    }

    [AvaloniaFact]
    public async Task Generation_RunsOffTheCallingThread()
    {
        var project = Make(Seqj(Track("Body", "body/body.achj", "Walk", 0)));
        int caller = Environment.CurrentManagedThreadId;

        await new ProjectTreeThumbnailService(_cacheDir).GetThumbnailAsync(project.Entry!, 28, 28);

        Assert.NotNull(project.BodyPng.ReadThreadId);
        Assert.NotEqual(caller, project.BodyPng.ReadThreadId);
        Assert.NotEqual(caller, project.Seqj.ReadThreadId);
    }

    private sealed class Project(Folder root)
    {
        public Folder Root { get; } = root;
        public File Seqj { get; init; } = null!;
        public File Body { get; init; } = null!;
        public File Fx { get; init; } = null!;
        public File BodyPng { get; init; } = null!;
        public File FxPng { get; init; } = null!;
        public AchxFileEntry? Entry { get; set; }
    }

    // RootPath is the path from the root folder ("body/body.png"), as ResolveRelativeFileAsync is asked for it.
    private sealed class File(string path, byte[] content) : IEditorFile
    {
        public string RootPath { get; } = path;
        public string Name => System.IO.Path.GetFileName(RootPath);
        public byte[] Content { get; set; } = content;
        public DateTimeOffset Modified { get; set; } = DateTimeOffset.UnixEpoch;
        public int ReadCount { get; set; }
        public int? ReadThreadId { get; private set; }

        public Task<Stream> OpenReadAsync()
        {
            ReadCount++;
            ReadThreadId = Environment.CurrentManagedThreadId;
            return Task.FromResult<Stream>(new MemoryStream(Content));
        }

        public Task<Stream> OpenWriteAsync() => throw new NotSupportedException();
        public Task<FolderEntrySnapshot> GetBasicPropertiesAsync() =>
            Task.FromResult(new FolderEntrySnapshot((ulong)Content.Length, Modified));
    }

    private sealed class Folder(string name) : IEditorFolder
    {
        public string Name { get; } = name;
        public Dictionary<string, File> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

#pragma warning disable CS1998
        public async IAsyncEnumerable<IEditorFile> GetItemsAsync() { foreach (var f in Files.Values) yield return f; }
        public async IAsyncEnumerable<IEditorFolder> GetSubfoldersAsync() { yield break; }
#pragma warning restore CS1998

        public Task<IEditorFile?> GetFileAsync(string name) => ResolveRelativeFileAsync(name);

        public Task<IEditorFile?> ResolveRelativeFileAsync(string relativePath) =>
            Task.FromResult(Files.TryGetValue(relativePath.Replace('\\', '/'), out var file) ? (IEditorFile?)file : null);
    }
}
