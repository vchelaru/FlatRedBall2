using System;
using Gum.Wireframe;
using RenderingLibrary;

using FrbCursor = FlatRedBall2.Input.ICursor;

namespace FlatRedBall2.Automation;

/// <summary>
/// The cursor FRB2 hands to Gum Forms while automation mode is active, so injected cursor commands
/// reach Gum controls — hover, push, click, focus — through Gum's ordinary input path.
/// </summary>
/// <remarks>
/// Gum's own <c>MonoGameGum.Input.Cursor</c> polls <c>Mouse.GetState()</c> inside its
/// <c>Activity</c> and has no injection seam, so with it installed an injected cursor moved
/// <c>Engine.Input.Cursor</c> for gameplay code but could never press a Gum <c>Button</c> or focus
/// a <c>TextBox</c>. This type replaces it through <c>FormsUtilities.SetCursor</c> and reads
/// everything from the FRB2 cursor instead — the same injected state the <c>input type:cursor</c>
/// command already writes. Focus routing, push/click dispatch and control state stay Gum's; nothing
/// here touches a control.
/// <para>
/// <b>Frame ordering.</b> The engine polls FRB2 input before Gum's update, so by the time Gum calls
/// <see cref="Activity"/> the FRB2 cursor already holds this frame's edges. Button state is therefore
/// read live (<see cref="PrimaryPush"/> is FRB2's <c>PrimaryPressed</c>, and so on) and only the
/// position is snapshotted, to give Gum the frame-to-frame deltas it raises RollOver and Dragging from.
/// Because injected button state is sticky, holding across stepped frames is one push followed by
/// <see cref="PrimaryDown"/> until the release — never a push per frame. Two clocks are in play:
/// the double-press/double-click edges are FRB2's, measured on its unscaled clock, while
/// <see cref="LastPrimaryPushTime"/> and <see cref="LastPrimaryClickTime"/> are stamped with the
/// game time Gum passes to <see cref="Activity"/>, as Gum's cursor does. Gum only ever compares
/// those stamps against each other.
/// </para>
/// <para>
/// <b>What is deliberately inert.</b> Cursor injection carries no wheel or middle button, so those read
/// zero/false. <see cref="LastInputDevice"/> is always the mouse: Gum's touch branch would treat a
/// stationary released cursor as over nothing. <see cref="CustomCursor"/> is stored but never
/// forwarded to the window — Gum's cursor calls <c>Mouse.SetCursor</c> there, and automation has no
/// pointer to change. While this cursor is installed <c>GumService.Cursor</c> (a cast to Gum's
/// concrete type) returns null; use <c>FormsUtilities.Cursor</c> if a Gum cursor is needed.
/// </para>
/// </remarks>
internal sealed class AutomationGumCursor : ICursor
{
    // Same "never seen" sentinel Gum's own cursor uses, so double-click math on a fresh cursor
    // behaves identically for anything reading these times.
    private const double NeverSeen = -999;

    private readonly FrbCursor _cursor;
    private int _x;
    private int _y;
    private int _lastX;
    private int _lastY;
    private double _lastPrimaryPushTime = NeverSeen;
    private double _lastPrimaryClickTime = NeverSeen;

    internal AutomationGumCursor(FrbCursor cursor) => _cursor = cursor;

    /// <inheritdoc/>
    public Cursors? CustomCursor { get; set; }

    /// <inheritdoc/>
    public InputDevice LastInputDevice => InputDevice.Mouse;

    /// <inheritdoc/>
    public int X => _x;

    /// <inheritdoc/>
    public int Y => _y;

    /// <inheritdoc/>
    /// <remarks>
    /// Same formula as Gum's cursor, off the same <c>Renderer.Camera</c> the engine syncs for Gum's
    /// hit-testing, so in-window checks agree with where controls are drawn. Falls back to the raw
    /// position when no renderer exists (headless).
    /// </remarks>
    public float XRespectingGumZoomAndBounds()
    {
        var renderer = SystemManagers.Default?.Renderer;
        if (renderer == null)
            return X;
        var left = renderer.GraphicsDevice?.Viewport.Bounds.Left ?? 0;
        return (X - left) / renderer.Camera.Zoom + renderer.Camera.X;
    }

    /// <inheritdoc/>
    public float YRespectingGumZoomAndBounds()
    {
        var renderer = SystemManagers.Default?.Renderer;
        if (renderer == null)
            return Y;
        var top = renderer.GraphicsDevice?.Viewport.Bounds.Top ?? 0;
        return (Y - top) / renderer.Camera.Zoom + renderer.Camera.Y;
    }

    /// <inheritdoc/>
    public double LastPrimaryPushTime => _lastPrimaryPushTime;

    /// <inheritdoc/>
    public double LastPrimaryClickTime => _lastPrimaryClickTime;

    /// <inheritdoc/>
    public int XChange => _x - _lastX;

    /// <inheritdoc/>
    public int YChange => _y - _lastY;

    /// <inheritdoc/>
    public int ScrollWheelChange => 0;

    /// <inheritdoc/>
    public float ZVelocity => 0;

    /// <inheritdoc/>
    public bool PrimaryPush => _cursor.PrimaryPressed;

    /// <inheritdoc/>
    public bool PrimaryDown => _cursor.PrimaryDown;

    /// <inheritdoc/>
    public bool PrimaryClick => _cursor.PrimaryClick;

    /// <inheritdoc/>
    public bool PrimaryClickNoSlide => PrimaryClick;

    /// <inheritdoc/>
    public bool PrimaryDoubleClick => _cursor.PrimaryDoubleClick;

    /// <inheritdoc/>
    public bool PrimaryDoublePush => _cursor.PrimaryDoublePressed;

    /// <inheritdoc/>
    public bool SecondaryPush => _cursor.SecondaryPressed;

    /// <inheritdoc/>
    public bool SecondaryDown => _cursor.SecondaryDown;

    /// <inheritdoc/>
    public bool SecondaryClick => _cursor.SecondaryClick;

    /// <inheritdoc/>
    public bool SecondaryDoubleClick => _cursor.SecondaryDoubleClick;

    /// <inheritdoc/>
    public bool MiddlePush => false;

    /// <inheritdoc/>
    public bool MiddleDown => false;

    /// <inheritdoc/>
    public bool MiddleClick => false;

    /// <inheritdoc/>
    public bool MiddleDoubleClick => false;

    /// <inheritdoc/>
    public InteractiveGue? WindowPushed { get; set; }

    /// <inheritdoc/>
    public InteractiveGue? VisualRightPushed { get; set; }

    /// <inheritdoc/>
    [Obsolete("Use VisualOver instead")]
    public InteractiveGue? WindowOver
    {
        get => VisualOver;
        set => VisualOver = value;
    }

    /// <inheritdoc/>
    public InteractiveGue? VisualOver { get; set; }

    /// <inheritdoc/>
    public void Activity(double currentGameTimeTotalSeconds)
    {
        _lastX = _x;
        _lastY = _y;
        var screen = _cursor.ScreenPosition;
        _x = (int)screen.X;
        _y = (int)screen.Y;

        if (PrimaryPush)
            _lastPrimaryPushTime = currentGameTimeTotalSeconds;
        if (PrimaryClick)
            _lastPrimaryClickTime = currentGameTimeTotalSeconds;
    }
}
