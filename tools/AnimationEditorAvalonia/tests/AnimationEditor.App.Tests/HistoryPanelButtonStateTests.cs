using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.App.Models;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies that HistoryUndoButton and HistoryRedoButton enable/disable
/// to reflect the current undo/redo availability.
/// </summary>
public class HistoryPanelButtonStateTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        var window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void HistoryUndoButton_DisabledInitially()
    {
        var (window, _) = CreateWindow();
        try
        {
            var btn = window.FindControl<Button>("HistoryUndoButton")!;
            Assert.False(btn.IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void HistoryRedoButton_DisabledInitially()
    {
        var (window, _) = CreateWindow();
        try
        {
            var btn = window.FindControl<Button>("HistoryRedoButton")!;
            Assert.False(btn.IsEnabled);
        }
        finally { window.Close(); }
    }

    // ── After recording a command ─────────────────────────────────────────────

    [AvaloniaFact]
    public void HistoryUndoButton_EnabledAfterCommandRecorded()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.UndoManager.Record(new StubCmd());
            Dispatcher.UIThread.RunJobs();

            var btn = window.FindControl<Button>("HistoryUndoButton")!;
            Assert.True(btn.IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void HistoryRedoButton_StillDisabledAfterCommandRecorded()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.UndoManager.Record(new StubCmd());
            Dispatcher.UIThread.RunJobs();

            var btn = window.FindControl<Button>("HistoryRedoButton")!;
            Assert.False(btn.IsEnabled);
        }
        finally { window.Close(); }
    }

    // ── After undoing ─────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void HistoryRedoButton_EnabledAfterUndo()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.UndoManager.Record(new StubCmd());
            ctx.UndoManager.Undo();
            Dispatcher.UIThread.RunJobs();

            var btn = window.FindControl<Button>("HistoryRedoButton")!;
            Assert.True(btn.IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void HistoryUndoButton_DisabledAfterAllCommandsUndone()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.UndoManager.Record(new StubCmd());
            ctx.UndoManager.Undo();
            Dispatcher.UIThread.RunJobs();

            var btn = window.FindControl<Button>("HistoryUndoButton")!;
            Assert.False(btn.IsEnabled);
        }
        finally { window.Close(); }
    }

    // ── After redoing ─────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void HistoryRedoButton_DisabledAfterRedo()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.UndoManager.Record(new StubCmd());
            ctx.UndoManager.Undo();
            ctx.UndoManager.Redo();
            Dispatcher.UIThread.RunJobs();

            var btn = window.FindControl<Button>("HistoryRedoButton")!;
            Assert.False(btn.IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void HistoryUndoButton_ReEnabledAfterRedo()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.UndoManager.Record(new StubCmd());
            ctx.UndoManager.Undo();
            ctx.UndoManager.Redo();
            Dispatcher.UIThread.RunJobs();

            var btn = window.FindControl<Button>("HistoryUndoButton")!;
            Assert.True(btn.IsEnabled);
        }
        finally { window.Close(); }
    }

    // ── History list ordering ─────────────────────────────────────────────────

    /// <summary>
    /// Photoshop order: applied (undo) items sit above redo items.
    /// Oldest applied entry is at the top; newest applied is the "you are here" row;
    /// redo items appear below, with the next-to-redo item immediately under the cursor.
    /// </summary>
    [AvaloniaFact]
    public void HistoryList_AppliedItemsAboveRedoItems_PhotoshopOrder()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.UndoManager.Record(new StubCmd("A"));
            ctx.UndoManager.Record(new StubCmd("B"));
            ctx.UndoManager.Undo(); // B → redo stack
            Dispatcher.UIThread.RunJobs();

            var list = window.FindControl<ItemsControl>("HistoryList")!;
            var items = ((IEnumerable<HistoryEntryVm>)list.ItemsSource!).ToList();

            // Applied history at top, redo items below
            Assert.Equal(2, items.Count);
            Assert.Equal("A", items[0].Description); // oldest applied at top
            Assert.Equal("B", items[1].Description); // redo item at bottom

            // A is bright (applied), B is dimmed (redo)
            Assert.Equal("#e6e8ec", items[0].Foreground);
            Assert.Equal("#6a6e76", items[1].Foreground);

            // A is the current "you are here" entry
            Assert.True(items[0].IsCurrent);
            Assert.False(items[1].IsCurrent);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void HistoryList_MultipleAppliedCommands_OldestAtTopNewestAtBottom()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.UndoManager.Record(new StubCmd("A"));
            ctx.UndoManager.Record(new StubCmd("B"));
            ctx.UndoManager.Record(new StubCmd("C"));
            Dispatcher.UIThread.RunJobs();

            var list = window.FindControl<ItemsControl>("HistoryList")!;
            var items = ((IEnumerable<HistoryEntryVm>)list.ItemsSource!).ToList();

            Assert.Equal(3, items.Count);
            Assert.Equal("A", items[0].Description);
            Assert.Equal("B", items[1].Description);
            Assert.Equal("C", items[2].Description);

            // C is the most recently applied — "you are here"
            Assert.False(items[0].IsCurrent);
            Assert.False(items[1].IsCurrent);
            Assert.True(items[2].IsCurrent);
        }
        finally { window.Close(); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed class StubCmd(string description = "Stub") : IUndoableCommand
    {
        public string Description => description;
        public bool Do() => true;
        public void Undo() { }
    }
}
