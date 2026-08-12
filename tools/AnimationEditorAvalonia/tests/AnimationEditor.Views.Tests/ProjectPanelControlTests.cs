using AnimationEditor.App.Services;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Views.Tests;

// Issue #770: recursive .achx tree for Open Project Folder. Platform-agnostic (no Window
// dependency, unlike desktop's FilesPanelControl) -- see ProjectPanelControl's doc comment.
public class ProjectPanelControlTests
{
    [AvaloniaFact]
    public void SetEntries_DefaultExcludesBinObj()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var entries = new[]
        {
            new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx"),
            new AchxFileEntry(new FakeFile("stale.achx"), root, "bin/stale.achx"),
        };

        control.SetEntries(entries);

        Assert.Single(control.TreeRoots);
        Assert.Equal("hero.achx", control.TreeRoots[0].Name);
    }

    [AvaloniaFact]
    public void SetEntries_NoEntries_ShowsEmptyMessage()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();

        control.SetEntries(System.Array.Empty<AchxFileEntry>());

        Assert.True(control.EmptyMessage.IsVisible);
    }

    [AvaloniaFact]
    public void SelectingFileNode_RaisesFileSelected()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var entry = new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx");
        control.SetEntries(new[] { entry });
        AchxFileEntry? selected = null;
        control.FileSelected += e => selected = e;

        control.ProjectTree.SelectedItem = control.TreeRoots[0];

        Assert.Same(entry, selected);
    }

    [AvaloniaFact]
    public void UncheckExcludeBinObj_ReincludesBinObjEntries()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var entries = new[]
        {
            new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx"),
            new AchxFileEntry(new FakeFile("stale.achx"), root, "bin/stale.achx"),
        };
        control.SetEntries(entries);

        control.ExcludeBinObjCheck.IsChecked = false;

        Assert.Equal(2, control.TreeRoots.Count);
    }

    [AvaloniaFact]
    public void TypingInSearch_FiltersTreeToMatches()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var entries = new[]
        {
            new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx"),
            new AchxFileEntry(new FakeFile("enemy.achx"), root, "enemy.achx"),
        };
        control.SetEntries(entries);

        control.ProjectSearchBox.SearchBox.Text = "hero";

        Assert.Single(control.TreeRoots);
        Assert.Equal("hero.achx", control.TreeRoots[0].Name);
    }

    [AvaloniaFact]
    public void SelectingFilteredResult_ClearsSearchAndRevealsFullTreeWithSelection()
    {
        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        var root = new FakeFolder("Content");
        var hero = new AchxFileEntry(new FakeFile("hero.achx"), root, "hero.achx");
        var enemy = new AchxFileEntry(new FakeFile("enemy.achx"), root, "enemy.achx");
        control.SetEntries(new[] { hero, enemy });
        control.ProjectSearchBox.SearchBox.Text = "hero";

        var selections = new List<AchxFileEntry>();
        control.FileSelected += e => selections.Add(e);
        control.ProjectTree.SelectedItem = control.TreeRoots[0];

        Assert.Equal([hero], selections); // fired exactly once, not re-fired by the reveal step
        Assert.False(control.ProjectSearchBox.SearchBox.IsVisible); // search collapsed
        Assert.Equal(2, control.TreeRoots.Count); // full tree restored
        Assert.Same(hero, ((AnimationEditor.Views.Controls.AchxTreeNodeVm)control.ProjectTree.SelectedItem!).Entry);
    }

    // Issue #839: SetEntries kicks off async thumbnail generation via ProjectTreeThumbnailService.
    // ThumbnailLoadTask is the test seam for awaiting it deterministically.
    [AvaloniaFact]
    public async Task SetEntries_WithThumbnailServiceInitialized_PopulatesThumbnailOnFileNode()
    {
        const string achxWithFrame =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <AnimationChainArraySave>
              <FileRelativeTextures>true</FileRelativeTextures>
              <TimeMeasurementUnit>Second</TimeMeasurementUnit>
              <CoordinateType>UV</CoordinateType>
              <AnimationChain>
                <Name>Walk</Name>
                <Frame>
                  <TextureName>hero.png</TextureName>
                  <FrameLength>0.1</FrameLength>
                  <LeftCoordinate>0</LeftCoordinate>
                  <RightCoordinate>1</RightCoordinate>
                  <TopCoordinate>0</TopCoordinate>
                  <BottomCoordinate>1</BottomCoordinate>
                </Frame>
              </AnimationChain>
            </AnimationChainArraySave>
            """;
        using var textureBitmap = new SKBitmap(8, 8);
        textureBitmap.Erase(SKColors.Red);
        using var textureImage = SKImage.FromBitmap(textureBitmap);
        using var textureData = textureImage.Encode(SKEncodedImageFormat.Png, 100);

        var root = new FakeFolder("Content");
        root.Files["hero.png"] = new FakeFile("hero.png", textureData.ToArray());
        var achxFile = new FakeFile("hero.achx", Encoding.UTF8.GetBytes(achxWithFrame));
        var entry = new AchxFileEntry(achxFile, root, "hero.achx");

        var control = new AnimationEditor.Views.Controls.ProjectPanelControl();
        control.Initialize(new ProjectTreeThumbnailService(diskCacheDirectory: null));

        control.SetEntries(new[] { entry });
        await control.ThumbnailLoadTask;

        Assert.True(control.TreeRoots[0].HasThumbnail);
    }

    private sealed class FakeFile : IEditorFile
    {
        public FakeFile(string name, byte[]? content = null) { Name = name; _content = content; }
        private readonly byte[]? _content;
        public string Name { get; }
        public Task<Stream> OpenReadAsync() => _content is null
            ? throw new NotSupportedException()
            : Task.FromResult<Stream>(new MemoryStream(_content));
        public Task<Stream> OpenWriteAsync() => throw new NotSupportedException();
        public Task<FolderEntrySnapshot> GetBasicPropertiesAsync() =>
            Task.FromResult(new FolderEntrySnapshot(null, null));
    }

    private sealed class FakeFolder : IEditorFolder
    {
        public FakeFolder(string name) => Name = name;
        public string Name { get; }
        public Dictionary<string, FakeFile> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
#pragma warning disable CS1998 // no subfolders/items to enumerate -- these entries are hand-built for the tree tests above
        public async IAsyncEnumerable<IEditorFile> GetItemsAsync() { yield break; }
        public async IAsyncEnumerable<IEditorFolder> GetSubfoldersAsync() { yield break; }
#pragma warning restore CS1998
        public Task<IEditorFile?> GetFileAsync(string name) =>
            Task.FromResult(Files.TryGetValue(name, out var f) ? (IEditorFile?)f : null);
    }
}
