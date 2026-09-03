using FlatRedBall2;
using Gum.GueDeriving;
using RenderingLibrary;
using Solitaire.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solitaire.Screens;

internal class TestScreenFrb : Screen
{
    private Factory<CardEntity> _cardFactory = null!;
    private List<CardEntity> _cards = new();
    private bool _loggedLayoutDiagnostic;

    private Gum.GueDeriving.TextRuntime? _perfText;
    private double _perfTextTimer;

    public override void CustomInitialize()
    {
        //this.Add(new TestScreen());

        _cardFactory = new(this);

        for(int i = 0; i < 2; i++)
        {
            var card = _cardFactory.Create();
            card.X = i * 100;
            _cards.Add(card);
        }

        _perfText = new Gum.GueDeriving.TextRuntime
        {
            FontSize = 13,
            Color = Microsoft.Xna.Framework.Color.Yellow,
        };
        _perfText.X = 6;
        _perfText.Y = 6;
        AddOverlay(_perfText);


        Engine.Performance.IsEnabled = true;
        Engine.RenderDiagnostics.IsEnabled = true;

        //var rectEntity = new GumRectEntity { X = -330, Y = 240 };
        //Register(rectEntity);
        //rectEntity.CustomInitialize();

        //base.CustomInitialize();
    }

    public override void CustomActivity(FrameTime time)
    {
        base.CustomActivity(time);

        UpdatePerfOverlay(time);
    }

    private void UpdatePerfOverlay(FrameTime time)
    {
        var diag = Engine.RenderDiagnostics;

        _perfTextTimer += time.DeltaSeconds;
        if (_perfTextTimer < 0.25) return;
        _perfTextTimer = 0;

        // TEMP diagnostic (batching investigation) - remove once done. Placed AFTER the timer gate
        // above so this only runs once real frames have elapsed (previous attempt ran on frame 1,
        // before FRB's entity->visual position sync had happened even once - gave a false "both
        // cards at the same position" reading that wasn't a real bug, just read-too-early).
        // BatchKey/BatchSortKey were already proven identical across cards; this checks the actual
        // layout instead: absolute bounds (do the cards really not overlap - and does anything
        // inside one card spill into the other's bounds?), and which Layer/how many Layers exist
        // (a second Layer means a second, independent reorder window).
        if (!_loggedLayoutDiagnostic && _cards.Count >= 2)
        {
            _loggedLayoutDiagnostic = true;
            try
            {
                var c0 = _cards[0].DiagnosticGum;
                var c1 = _cards[1].DiagnosticGum;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"card0 Entity.X={_cards[0].X:F1} Entity.Y={_cards[0].Y:F1}");
                sb.AppendLine($"card1 Entity.X={_cards[1].X:F1} Entity.Y={_cards[1].Y:F1}");
                sb.AppendLine($"ReferenceEquals(card0, card1): {ReferenceEquals(_cards[0], _cards[1])}");
                sb.AppendLine($"ReferenceEquals(card0.DiagnosticGum, card1.DiagnosticGum): {ReferenceEquals(c0, c1)}");
                sb.AppendLine($"ReferenceEquals(card0.Visual, card1.Visual): {ReferenceEquals(c0.Visual, c1.Visual)}");

                void Describe(string name, Gum.Wireframe.GraphicalUiElement e)
                {
                    var b = ((RenderingLibrary.Graphics.IRenderableIpso)e).GetAbsoluteBounds();
                    sb.AppendLine($"{name}: X={e.GetAbsoluteX():F1} Y={e.GetAbsoluteY():F1} W={e.Width:F1} H={e.Height:F1} Bounds={b}");
                }
                Describe("card0.Visual", c0.Visual);
                Describe("card0.Background", c0.Background);
                Describe("card0.RankText1", c0.RankText1);
                Describe("card0.SuitIcon", c0.SuitIcon);
                Describe("card1.Visual", c1.Visual);
                Describe("card1.Background", c1.Background);
                Describe("card1.RankText1", c1.RankText1);
                Describe("card1.SuitIcon", c1.SuitIcon);

                var b0 = ((RenderingLibrary.Graphics.IRenderableIpso)c0.Visual).GetAbsoluteBounds();
                var b1 = ((RenderingLibrary.Graphics.IRenderableIpso)c1.Visual).GetAbsoluteBounds();
                sb.AppendLine($"card0/card1 Visual bounds intersect: {b0.IntersectsWith(b1)}");

                var renderer = RenderingLibrary.SystemManagers.Default.Renderer;
                sb.AppendLine($"Renderer.Layers.Count: {renderer.Layers.Count}");
                for (int li = 0; li < renderer.Layers.Count; li++)
                {
                    var layer = renderer.Layers[li];
                    sb.AppendLine($"  Layer[{li}] Name={layer.Name} SecondarySortOnY={layer.SecondarySortOnY} Renderables.Count={layer.Renderables.Count}");
                }

                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gum-layout-identity-debug.log"), sb.ToString());
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gum-layout-identity-debug.log"), ex.ToString());
            }
        }

        //Engine.RenderDiagnostics.

        //var drawCount = GumService.Default.Renderer.RenderStateChangeStatistics.DrawCallCount;

        var perf = Engine.Performance;
        string text =
            $"FPS: {perf.Fps.Current:F0}\n" +
            $"GPU draw calls: {perf.DrawCallCount.Current:F0}\n" +
            $"FRB batch breaks: {diag.BatchBreakCount}\n" +
            $"Gum draw calls: {diag.InternalDrawCallCount}";

        // TEMPORARY diagnostic (Deferred-mode batching investigation) - remove once done.
        // GetBreakGroups() (not GetBreakGroupsByType()) so this names the actual textures
        // involved, not just "Sprite->Sprite" with no way to tell which ones.
        if (RenderingLibrary.Graphics.Renderer.SiblingOrdering is RenderingLibrary.Graphics.BatchKeyGroupedOrderer orderer)
        {
            var breaks = orderer.GetBreakGroups();
            int totalDraws = breaks.Sum(g => g.Count);
            text += $"\n{breaks.Count} batch breaks ({totalDraws} draws):";
            foreach (var group in breaks)
            {
                string toType = group.ToRenderableType.Name;
                string to = FlatRedBall2.UI.GumRenderBatch.DescribeSortKey(group.ToSortKey);
                string from = group.Reason == RenderingLibrary.Graphics.BatchKeyGroupedOrderer.BreakReason.NoPredecessor
                    ? "(none)"
                    : $"{group.FromRenderableType.Name}({FlatRedBall2.UI.GumRenderBatch.DescribeSortKey(group.FromSortKey)})";
                text += $"\n  {group.Count}x [{group.Reason}]: {from} -> {toType}({to})";
            }
        }

        try
        {
            string perfLogPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gum-perf-overlay-debug.log");
            System.IO.File.WriteAllText(perfLogPath, text);
        }
        catch { }

        _perfText!.Text = text;
    }

}


