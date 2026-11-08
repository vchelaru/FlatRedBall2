using System.Diagnostics;
using AnimationEditor.App.Services;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>SCRATCH probe (not committed): times the Animations tab click on a real folder.</summary>
public class ProjectTabStallProbe
{
    // Settings: the real folder, plus the user's real collapsed-folder list (read only) when AE_PERF_REALCOLLAPSE=1.
    private static string BuildSettings(string folder)
    {
        var node = new System.Text.Json.Nodes.JsonObject { ["LastProjectFolderPath"] = folder };
        if (Environment.GetEnvironmentVariable("AE_PERF_REALCOLLAPSE") == "1")
        {
            string real = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AnimationEditor", "AESettings.json");
            var parsed = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(real))!;
            node["CollapsedAnimationFolders"] = parsed["CollapsedAnimationFolders"]!.DeepClone();
        }
        return node.ToJsonString();
    }

    [AvaloniaFact]
    public async Task Probe()
    {
        string? folder = Environment.GetEnvironmentVariable("AE_PERF_FOLDER");
        string? outFile = Environment.GetEnvironmentVariable("AE_PERF_OUT");
        string? cache = Environment.GetEnvironmentVariable("AE_PERF_CACHE");
        if (folder is null || outFile is null) return;

        void Log(string line) => File.AppendAllText(outFile, line + Environment.NewLine);
        Log($"=== run folder={folder} cache={cache} exists={(cache != null && Directory.Exists(cache))}");

        for (int round = 0; round < 2; round++)
        {
            Log("-- round " + round);
            string settingsRoot = Path.Combine(Path.GetTempPath(), "AEPerf", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(settingsRoot, "AnimationEditor"));
            File.WriteAllText(Path.Combine(settingsRoot, "AnimationEditor", "AESettings.json"),
                BuildSettings(folder));

            using var meter = new UiStallMeter();
            var sw = Stopwatch.StartNew();
            meter.Mark("startup");
            using var editor = new AnimationEditorHarness(settingsRoot,
                projectTreeThumbnails: new ProjectTreeThumbnailService(cache));
            meter.Flush();
            Log($"ctor+Show: {sw.ElapsedMilliseconds} ms (gap {meter.MaxGapByPhase["startup"]:F0})");

            var panel = editor.Control<ProjectPanelControl>("ProjectPanel");
            sw.Restart();
            meter.Mark("startup-scan");
            while (panel.TreeRoots.Count == 0 && sw.ElapsedMilliseconds < 60000)
                await Task.Delay(5);
            meter.Flush();
            static int Collapsed(IEnumerable<AchxTreeNodeVm> nodes) => nodes.Sum(n => (n.IsFolder && !n.IsExpanded ? 1 : 0) + Collapsed(n.Children));
            Log($"scan done: {sw.ElapsedMilliseconds} ms, roots={panel.TreeRoots.Count}, collapsedFolders={Collapsed(panel.TreeRoots)}");

            sw.Restart();
            meter.Mark("thumbs-hidden");
            await panel.ThumbnailLoadTask;
            meter.Flush();
            Log($"thumbnails loaded (tab hidden): {sw.ElapsedMilliseconds} ms, gap {meter.MaxGapByPhase["thumbs-hidden"]:F0}");

            meter.Mark("click-tab");
            sw.Restart();
            editor.Click(editor.Control<TabItem>("ProjectTab"));
            meter.Flush();
            Log($"click Animations tab: {sw.ElapsedMilliseconds} ms; now on {(editor.Window.SidebarTabs.SelectedItem as TabItem)?.Header}");
            meter.Mark("settle");
            await Task.Delay(300);
            meter.Flush();
            foreach (var kv in meter.MaxGapByPhase) Log($"  phase {kv.Key}: max gap {kv.Value:F0} ms");
            Log($"  TreeViewItems realized: {panel.GetVisualDescendants().OfType<TreeViewItem>().Count()}");
        }
    }
}
