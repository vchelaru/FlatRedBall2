using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.HotReload;
using AnimationEditor.Core.Models;
using AnimationEditor.DocumentMatrix;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Random edits, undo, Save As, failed saves, outside changes, background saves and tab switches over two .tsx, an .achx and an
/// .achj. A saved document is what its file holds, every frame keeps its texture, and a failed save never costs an outside change.
/// </summary>
[Collection("SequentialSingletons")]
public class MultiTabRandomWalkTests
{
    private sealed class Watcher : IHotReloadWatcher
    {
        public event Action<string>? AchxChangedOnDisk;
        public event Action<string>? PngChangedOnDisk { add { } remove { } }
        public event Action<string>? SourceChangedOnDisk { add { } remove { } }
        public event Action<string>? AchxDeletedOnDisk { add { } remove { } }
        public bool IsEnabled { get; set; }
        public string? Watched;
        public List<string> Pending = new();
        public void StartWatching(string achxPath, IEnumerable<string> pngPaths) => Watched = achxPath;
        public void UpdatePngList(IEnumerable<string> newPngPaths) { }
        public void UpdateSourceList(IEnumerable<string> sourcePaths) { }
        public void StopWatching() { }
        public void RunOwnSave(string filePath, Action write) => write();
        public void ReportChange(string filePath) => Pending.Add(filePath);
        public void Flush()
        {
            while (Pending.Count > 0)
            {
                var p = Pending[0];
                Pending.RemoveAt(0);
                AchxChangedOnDisk?.Invoke(p);
            }
        }
        public void Changed(string p)
        {
            if (Watched != null && new FilePath(Watched) == new FilePath(p)) AchxChangedOnDisk?.Invoke(p);
        }
        public void Dispose() { }
    }

    private sealed class Doc
    {
        public required TabEntry Tab;
        public required DocType Type;
        public string Sheet = "";
    }

    [Fact]
    public async Task RandomActions_DiskTabsAndOutsideChangesAgree()
    {
        var fails = new List<string>();
        for (int seed = 0; seed < 20; seed++)
        {
            var log = new StringBuilder($"seed {seed}");
            try { await One(seed, log); }
            catch (Exception ex) { fails.Add(log + "\n" + ex.Message.Replace("Shouldly.ShouldAssertException : ", "") + ex.StackTrace!.Split('\n').FirstOrDefault(l => l.Contains("MultiTabRandomWalk"))); }
        }
        fails.Count.ShouldBe(0, string.Join("\n=====\n", fails));
    }

    private static string Norm(AnimationChainListSave list, string path) => string.Join("\n", Documents.Lines(list, Path.GetDirectoryName(path)!));

    private async Task One(int seed, StringBuilder log)
    {
        using var dir = new TestHelpers.TempDir();
        var ctx = new TestServices();
        var watcher = new Watcher();
        ctx.AppCommands.HotReloadWatcher = watcher;
        ctx.AppCommands.WireHotReloadWatcher();
        ctx.AppCommands.IsDocumentTabInFront = () => true;
        var messages = new List<string>();
        ctx.AppCommands.SaveFailed += messages.Add;
        ctx.AppCommands.SaveAsRefused += messages.Add;
        ctx.AppCommands.SaveCompletedWithWarnings += (_, w) => messages.AddRange(w);
        ctx.AppCommands.LoadFailed += (p, ex) => messages.Add(p + ex.Message);
        var random = new Random(seed);
        var docs = new List<Doc>();
        Doc? front = null;
        async Task Switch(Doc doc)
        {
            if (doc == front) return;
            if (front != null)
            {
                front.Tab.UndoSnapshot = ctx.UndoManager.TakeSnapshot();
                ctx.AppCommands.CaptureTabEditorState(front.Tab);
            }
            front = doc;
            ctx.UndoManager.Clear();
            (await ctx.AppCommands.ActivateTabContentAsync(doc.Tab)).ShouldBeTrue();
            if (doc.Tab.UndoSnapshot != null) ctx.UndoManager.RestoreSnapshot(doc.Tab.UndoSnapshot);
            watcher.Flush();
        }
        int n = 0;
        foreach (var type in new[] { DocType.Achx, DocType.Achj, DocType.Tsx, DocType.Tsx })
        {
            string name = "d" + n++;
            string folder = Path.Combine(dir.Path, name);
            var doc = new Doc
            {
                Type = type,
                Tab = new TabEntry(new FilePath(Documents.Write(folder, type, name))),
                Sheet = new FilePath(Path.Combine(folder, "sheet.png")).FullPath,
            };
            docs.Add(doc);
            await Switch(doc);
        }
        var pm = ctx.ProjectManager;
        string P(Doc d) => d.Tab.Path.FullPath;
        bool Failed(Doc d) => d == front ? ctx.UndoManager.SaveState == SaveState.Failed : d.Tab.SaveFailed;
        AnimationChainListSave Mem(Doc d) => (d == front ? pm.AnimationChainListSave : d.Tab.CachedEditorModel)!;
        var readOnly = new HashSet<string>();
        var owed = new Dictionary<Doc, string>();
        try
        {
            for (int step = 0; step < 14; step++)
            {
                var d = docs[random.Next(docs.Count)];
                int action = random.Next(13);
                log.Append($"\n{step}: {action} front {docs.IndexOf(front!)} other {docs.IndexOf(d)}");
                var chains = pm.AnimationChainListSave!.AnimationChains;
                switch (action)
                {
                    case 0 or 1:
                        if (ctx.AppCommands.AddAnimationChainWithName($"N{step}") is { } a)
                        {
                            int c = random.Next(4) * 16, r = random.Next(4) * 16;
                            ctx.AppCommands.AddFrameFromPixelBounds(a, Path.GetRelativePath(Path.GetDirectoryName(P(front!))!, front!.Sheet).Replace('\\', '/'), c, r, c + 16, r + 16);
                        }
                        break;
                    case 2 when chains.Count > 0: ctx.AppCommands.DeleteAnimationChains([chains[0]]); break;
                    case 3 when chains.Count > 0: ctx.AppCommands.RenameChain(chains[^1], $"R{step}"); break;
                    case 4 or 5: ctx.UndoManager.Undo(); break;
                    case 6: ctx.UndoManager.Redo(); break;
                    case 7 or 8: await Switch(d); break;
                    case 9:
                    {
                        string to = Path.Combine(dir.Path, random.Next(2) == 0 ? Path.GetFileName(Path.GetDirectoryName(P(front!))!) : $"moved{step}");
                        Directory.CreateDirectory(to);
                        string target = Path.Combine(to, $"as{step}" + front!.Type.Extension());
                        ctx.AppCommands.FileDialogService = new StubFileDialogService(target);
                        if (await ctx.AppCommands.SaveCurrentAnimationChainListAsync())
                        {
                            front.Tab = new TabEntry(new FilePath(target));
                            owed.Remove(front);
                        }
                        break;
                    }
                    case 10:
                        if (readOnly.Remove(P(d))) Documents.SetWritable(P(d), true);
                        else
                        {
                            readOnly.Add(P(d));
                            Documents.SetWritable(P(d), false);
                        }
                        break;
                    case 11:
                    {
                        string path = P(d);
                        if (readOnly.Contains(path)) break;
                        string mark = $"Out{step}";
                        bool tsx = d.Type == DocType.Tsx;
                        if (tsx)
                        {
                            string text = File.ReadAllText(path);
                            File.WriteAllText(path, text.Replace("</tileset>", $" <!--{mark}-->\n</tileset>"));
                        }
                        else
                        {
                            var list = AnimationChainListSave.FromFile(path);
                            list.AnimationChains.Add(new AnimationChainSave { Name = mark });
                            if (d.Type == DocType.Achj) list.SaveJson(path); else list.Save(path);
                        }
                        bool wasFailed = Failed(d);
                        if (wasFailed) owed[d] = mark;
                        watcher.Changed(path);
                        if (!wasFailed && d == front && !tsx) Mem(d).AnimationChains.ShouldContain(x => x.Name == mark, "live reload");
                        break;
                    }
                    case 12:
                        if (d != front && d.Tab.CachedEditorModel is { } model && model.AnimationChains.Count > 1 && !(d.Tab.CachedDiskState?.HasChanged(d.Tab.Path) ?? false))
                        {
                            model.AnimationChains.RemoveAt(model.AnimationChains.Count - 1);
                            d.Tab.UndoSnapshot = null;
                            ctx.AppCommands.SaveDocument(d.Tab);
                        }
                        break;
                }
                watcher.Flush();
                foreach (var x in docs)
                {
                    if (owed.TryGetValue(x, out var mark)) File.ReadAllText(P(x)).ShouldContain(mark, Case.Sensitive, $"doc {docs.IndexOf(x)}: outside change overwritten");
                    if (x != front && (x.Tab.CachedDiskState?.HasChanged(x.Tab.Path) ?? false)) continue;
                    foreach (var f in Mem(x).AnimationChains.SelectMany(c => c.Frames))
                        new FilePath(Path.Combine(Path.GetDirectoryName(P(x))!, f.TextureName)).FullPath.ShouldBe(x.Sheet, $"doc {docs.IndexOf(x)} texture");
                    if (Failed(x)) continue;
                    string folder = Path.GetDirectoryName(P(x))!;
                    if (x.Type == DocType.Tsx)
                    {
                        var re = new ProjectManager();
                        re.LoadTsxProject(new FilePath(P(x)));
                        var notFull = x == front ? pm.TsxChainsNotFullySaved : ((ProjectManager.TsxState)x.Tab.CachedTsxState!).ChainsNotFullySaved;
                        var full = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
                        full.AnimationChains.AddRange(Mem(x).AnimationChains.Where(c => !notFull.Contains(c)));
                        Documents.Lines(full, folder).Except(Documents.Lines(re.AnimationChainListSave!, folder)).ShouldBeEmpty($"doc {docs.IndexOf(x)} disk");
                    }
                    else
                        Norm(AnimationChainListSave.FromFile(P(x)), P(x)).ShouldBe(Norm(Mem(x), P(x)), $"doc {docs.IndexOf(x)} disk");
                }
            }
        }
        finally
        {
            foreach (var p in readOnly.Where(File.Exists)) Documents.SetWritable(p, true);
        }
    }
}
