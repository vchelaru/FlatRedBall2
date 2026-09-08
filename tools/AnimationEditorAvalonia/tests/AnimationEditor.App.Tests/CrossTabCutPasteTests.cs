using AnimationEditor.Core;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// End-to-end coverage for issue #1026: copying/cutting an animation from one open tab into
/// another tab in the same project, where the two <c>.achx</c> files live in different folders.
/// </summary>
public class CrossTabCutPasteTests
{
    private static string WriteAchx(string dir, string fileName, string chainName, string textureName)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = chainName };
        chain.Frames.Add(new AnimationFrameSave { TextureName = textureName, FrameLength = 0.1f });
        acls.AnimationChains.Add(chain);
        acls.Save(path);
        return path;
    }

    private static async Task InvokePrivateAsync(MainWindow window, string methodName)
    {
        var method = typeof(MainWindow).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(window, null)!;
    }

    private static void FlushUi()
    {
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    private static void RebuildTree(MainWindow window)
    {
        typeof(MainWindow).GetMethod("RebuildTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, new object[] { Array.Empty<string>() });
        FlushUi();
    }

    [AvaloniaFact]
    public async Task CutChain_FromOtherOpenTab_MovesItAndResolvesTextureToOriginalFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var folderA = Path.Combine(root, "FolderA");
        var folderB = Path.Combine(root, "FolderB");
        try
        {
            var pathA = WriteAchx(folderA, "a.achx", "Walk", "hero.png");
            var pathB = WriteAchx(folderB, "b.achx", "Run", "enemy.png");

            var ctx = TestHelpers.BuildServices();
            ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
            ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
            var window = ctx.CreateMainWindow();
            window.Show();
            try
            {
                // Open tab A and cut its "Walk" chain.
                await window.OpenFileAsTab(pathA);
                FlushUi();
                RebuildTree(window);
                var tree = window.FindControl<TreeView>("AnimTree")!;
                var walkNode = tree.ItemsSource!.Cast<TreeNodeVm>().First();
                tree.SelectedItems!.Clear();
                tree.SelectedItems.Add(walkNode);
                FlushUi();

                await InvokePrivateAsync(window, "HandleCutCoreAsync");

                // The clipboard payload is self-contained: the texture reference was resolved to
                // an absolute path at cut time, before the folder it's relative to (FolderA) goes
                // out of scope by switching tabs.
                var clipTextAfterCut = await window.Clipboard!.TryGetTextAsync();
                var absoluteHeroPath = TexturePathHelper.ResolveDisplayPath("hero.png", folderA);
                Assert.Contains(absoluteHeroPath.Replace("\\", "/"), clipTextAfterCut!.Replace("\\", "/"));

                // The document the cut's sources live in, captured at cut time -- this is the
                // exact object a cross-tab paste must later remove "Walk" from directly (#1026),
                // since it stops being the active document (and the undo stack moves on to
                // whichever tab is pasted into) the moment a different tab is opened.
                var sourceDoc = ctx.PendingCutState.SourceDocument!;
                Assert.Contains(sourceDoc.AnimationChains, c => c.Name == "Walk");

                // Switch to tab B -- this is what makes the cut "cross-tab".
                await window.OpenFileAsTab(pathB);
                FlushUi();
                RebuildTree(window);

                Assert.Equal(CutCompletion.CrossDocument,
                    ctx.PendingCutState.ResolveCompletion(ctx.ProjectManager.AnimationChainListSave));

                await InvokePrivateAsync(window, "HandlePasteCoreAsync");
                FlushUi();

                // The chain landed in B's document with its texture reference re-relativized
                // (by the paste command's own auto-save) against FolderB, pointing back at the
                // same physical file in FolderA -- not "hero.png" relative to FolderB, which
                // would silently point at a texture that doesn't exist there.
                var bAcls = ctx.ProjectManager.AnimationChainListSave!;
                var pastedChain = Assert.Single(bAcls.AnimationChains, c => c.Name == "Walk");
                var expectedTexturePath = TexturePathHelper.ComputeStorePath(absoluteHeroPath, folderB);
                Assert.Equal(expectedTexturePath, pastedChain.Frames[0].TextureName);

                // The source chain was actually moved, not merely copied.
                Assert.DoesNotContain(sourceDoc.AnimationChains, c => c.Name == "Walk");
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
