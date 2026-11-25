using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>Random cuts and drags of frames in a <c>.tsx</c>, with saves as the editor does them: no move takes a frame out of the file.</summary>
[Collection("SequentialSingletons")]
public class TsxMoveFuzzTests
{
    [Fact]
    public void RandomMoves_KeepEveryFrameInTheFile()
    {
        var failures = new List<string>();
        for (int seed = 0; seed < 20; seed++)
            if (Run(seed) is { } failure) failures.Add(failure);
        failures.Take(5).ShouldBeEmpty();
    }

    private static string? Run(int seed)
    {
        using var dir = new TestHelpers.TempDir();
        var ctx = new TestServices();
        var random = new Random(seed);
        string path = Path.Combine(dir.Path, "Heroes.tsx");
        File.WriteAllText(path, """
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="32" columns="8">
             <image source="Heroes.png" width="128" height="64"/>
             <tile id="5"><animation><frame tileid="5" duration="100"/><frame tileid="6" duration="110"/><frame tileid="9" duration="120"/></animation></tile>
             <tile id="9"><animation><frame tileid="9" duration="130"/></animation></tile>
             <tile id="12"><animation><frame tileid="20" duration="140"/><frame tileid="5" duration="150"/></animation></tile>
             <tile id="16"><properties><property name="Name" value="Wide"/></properties><animation><frame tileid="16" duration="160"/><frame tileid="18" duration="170"/></animation></tile>
             <tile id="17"><properties><property name="ParentId" type="int" value="16"/></properties><animation><frame tileid="17" duration="160"/><frame tileid="19" duration="170"/></animation></tile>
            </tileset>
            """);
        ctx.ProjectManager.LoadTsxProject(new FilePath(path));
        var acls = ctx.ProjectManager.AnimationChainListSave!;
        string Save()
        {
            ctx.ProjectManager.SaveTsxProject();
            return string.Join(" ", Regex.Matches(File.ReadAllText(path), "<frame [^>]*>").Select(m => m.Value).Order());
        }
        var log = new List<string>();
        string before = Save();
        try
        {
            for (int step = 0; step < 15; step++)
            {
                var sources = acls.AnimationChains.Where(c => c.Frames.Count > 0).ToList();
                var source = sources[random.Next(sources.Count)];
                var target = acls.AnimationChains[random.Next(acls.AnimationChains.Count)];
                var frames = source.Frames.Where(_ => random.Next(2) == 0).DefaultIfEmpty(source.Frames[0]).ToArray();
                bool skippedBefore = ctx.ProjectManager.TsxChainsNotFullySaved.Any(c => c.Frames.Count > 0);
                bool isMove = true;
                switch (random.Next(8))
                {
                    case 0: log.Add("add"); ctx.AppCommands.AddAnimationChainWithName("N" + step); isMove = false; break;
                    case 1: log.Add("undo"); ctx.UndoManager.Undo(); isMove = false; break;
                    case 2 or 3 or 4:
                        log.Add($"cut {source.Name}[{string.Join(",", frames.Select(f => source.Frames.IndexOf(f)))}] to {target.Name}");
                        ctx.AppCommands.PasteFramesCut(target, frames, random.Next(target.Frames.Count + 1)); break;
                    default:
                        log.Add($"drag {source.Name}[{string.Join(",", frames.Select(f => source.Frames.IndexOf(f)))}] to {target.Name}");
                        ctx.AppCommands.MoveFrames(frames, source, target, random.Next(target.Frames.Count + 1)); break;
                }
                string after = Save();
                if (isMove && !skippedBefore && after != before)
                    return $"seed {seed}: {string.Join("; ", log)}\n  before {before}\n  after  {after}";
                before = after;
            }
        }
        catch (Exception ex)
        {
            return $"seed {seed}: {string.Join("; ", log)} threw {ex.Message}";
        }
        return null;
    }
}
