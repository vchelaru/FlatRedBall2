using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.DocumentMatrix;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

/// <summary>The file after an undo, where the save the edit made did not store it all.</summary>
[Collection("SequentialSingletons")]
public class UndoFileTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();
    private string _path = "";

    public void Dispose() => _dir.Dispose();

    private List<AnimationChainSave> Chains => _ctx.ProjectManager.AnimationChainListSave!.AnimationChains;

    private string OnDisk => File.ReadAllText(_path);

    private void Open(DocType type)
    {
        _path = Documents.Write(_dir.Path, type, "front");
        if (type == DocType.Tsx) _ctx.ProjectManager.LoadTsxProject(new FilePath(_path));
        else _ctx.ProjectManager.LoadAnimationChain(new FilePath(_path));
        _ctx.AppCommands.SaveCurrentAnimationChainList();
    }

    [Fact]
    public void Undo_AfterAFailedSave_IsSavedOnceTheFileIsWritable()
    {
        Open(DocType.Achx);
        string before = OnDisk;
        File.SetAttributes(_path, FileAttributes.ReadOnly);
        _ctx.AppCommands.RenameChain(Chains[0], "Renamed");
        _ctx.AppCommands.AddFrame(Chains[0]);
        bool failed = _ctx.UndoManager.SaveState == SaveState.Failed;
        File.SetAttributes(_path, FileAttributes.Normal);

        _ctx.UndoManager.Undo();
        _ctx.UndoManager.Undo();

        failed.ShouldBeTrue();
        OnDisk.ShouldBe(before);
        _ctx.UndoManager.SaveState.ShouldBe(SaveState.AutoSaveOn);
    }

    [Fact]
    public void Tsx_Undo_AfterAFailedSave_IsSavedOnceTheFileIsWritable()
    {
        Open(DocType.Tsx);
        string before = OnDisk;
        File.SetAttributes(_path, FileAttributes.ReadOnly);
        _ctx.AppCommands.RenameChain(Chains[0], "Renamed");
        _ctx.AppCommands.SetChainTsxOwnerTileId(Chains[0], 9);
        _ctx.AppCommands.DeleteAnimationChains([Chains[1]]);
        bool failed = _ctx.UndoManager.SaveState == SaveState.Failed;
        File.SetAttributes(_path, FileAttributes.Normal);

        _ctx.UndoManager.Undo();
        _ctx.UndoManager.Undo();
        _ctx.UndoManager.Undo();

        failed.ShouldBeTrue();
        OnDisk.ShouldBe(before);
    }
}
