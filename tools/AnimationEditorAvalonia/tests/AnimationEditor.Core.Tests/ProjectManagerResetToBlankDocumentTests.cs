using AnimationEditor.Core;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Issue #1147: several UI-layer "start a fresh document" call sites (MainWindow.axaml.cs's
// ActivateUntitledTabContent, OpenAsNewUnsavedDocument, etc.) assigned AnimationChainListSave/
// FileName directly without clearing ProjectManager's private native-tsx/texture-size/
// ReferencedPngs tracking state -- the same leak class NewFile/CloseProject were already fixed
// for. ResetToBlankDocument centralizes that reset so every call site shares one implementation.
public class ProjectManagerResetToBlankDocumentTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    // 4 columns, 16x16 tiles, one animated tile with no Name property.
    private const string TsxFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    [Fact]
    public void ResetToBlankDocument_AfterLoadTsxProject_ClearsNativeTsxState()
    {
        var pm = new ProjectManager();
        var tsxPath = Path.Combine(_dir.Path, "Heroes.tsx");
        File.WriteAllText(tsxPath, TsxFixtureXml);
        pm.LoadTsxProject(new FilePath(tsxPath));
        Assert.True(pm.IsNativeTsxProject);

        pm.ResetToBlankDocument();

        Assert.False(pm.IsNativeTsxProject);
        Assert.Null(pm.TsxTileSize);
        Assert.NotNull(pm.AnimationChainListSave);
        Assert.Empty(pm.AnimationChainListSave!.AnimationChains);
        Assert.Null(pm.FileName);
    }

    [Fact]
    public void ResetToBlankDocument_AfterLoadAchxWithProjectFileAndKnownTextureSizes_ClearsReferencedPngsAndTextureSizeState()
    {
        var contentDir = Path.Combine(_dir.Path, "Content");
        Directory.CreateDirectory(contentDir);
        File.WriteAllText(Path.Combine(contentDir, "Hero.png"), "");

        var projectFile = Path.Combine(_dir.Path, "Game.gluj");
        File.WriteAllText(projectFile,
            """
            <Project>
              <GlobalFiles>
                <ReferencedFileSave><Name>Hero.png</Name></ReferencedFileSave>
              </GlobalFiles>
            </Project>
            """);

        var achxPath = Path.Combine(_dir.Path, "WithProject.achx");
        var acls = new AnimationChainListSave
        {
            CoordinateType = TextureCoordinateType.Pixel,
            ProjectFile = "Game.gluj",
        };
        acls.Save(achxPath);

        var pm = new ProjectManager();
        var knownTextureSizes = new Dictionary<string, (int Width, int Height)> { ["Hero.png"] = (32, 32) };
        pm.LoadAnimationChain(new FilePath(achxPath), knownTextureSizes: knownTextureSizes);
        Assert.NotEmpty(pm.ReferencedPngs);
        Assert.NotNull(pm.CaptureTextureSizeState());

        pm.ResetToBlankDocument();

        Assert.Empty(pm.ReferencedPngs);
        Assert.Null(pm.CaptureTextureSizeState());
        Assert.Null(pm.FileName);
        Assert.Equal(TextureCoordinateType.Pixel, pm.OnDiskCoordinateType);
    }
}
