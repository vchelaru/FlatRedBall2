using System;
using System.Collections.Generic;
using System.Numerics;
using Gum.Forms.Controls;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using XnaColor = Microsoft.Xna.Framework.Color;

// Aliases to avoid method-vs-type name ambiguity within this file.
using CircleShape = FlatRedBall2.Collision.Circle;
using RectShape = FlatRedBall2.Collision.AxisAlignedRectangle;
using LineShape = FlatRedBall2.Collision.Line;
using PolyShape = FlatRedBall2.Collision.Polygon;
using SpriteShape = FlatRedBall2.Rendering.Sprite;

namespace FlatRedBall2.Diagnostics;

/// <summary>
/// Immediate-mode visual overlay for debug drawing and in-game editors.
/// Call any draw method each frame — objects appear this frame and are hidden automatically
/// the next. No setup or cleanup needed.
/// </summary>
/// <remarks>
/// <para>
/// Access via <see cref="FlatRedBallService.Overlay"/> or <see cref="Screen.Overlay"/>.
/// Each screen owns its own <see cref="Overlay"/> instance; the pool is discarded when
/// the screen transitions.
/// </para>
/// <para>
/// All objects are pooled and reused across frames to avoid per-frame allocation.
/// </para>
/// <para>
/// Overlay draws on top of typical world content by default (<see cref="DefaultZ"/> = 1000).
/// Override Z on the returned shape to change draw order.
/// </para>
/// <para>
/// Shape methods return the underlying object for optional same-frame configuration (e.g.,
/// <c>IsFilled = true</c> or a custom <c>Z</c>).
/// </para>
/// </remarks>
public class Overlay
{
    private readonly Screen _screen;

    internal Overlay(Screen screen) => _screen = screen;

    // ── Pools ────────────────────────────────────────────────────────────

    private readonly List<CircleShape> _circles = new();
    private readonly List<RectShape> _rectangles = new();
    private readonly List<LineShape> _lines = new();
    private readonly List<PolyShape> _polygons = new();
    private readonly List<ArrowEntry> _arrows = new();
    private readonly List<SpriteShape> _sprites = new();
    private readonly List<Label> _labels = new();
    private readonly List<RectShape> _textBackgrounds = new();

    private readonly Dictionary<Label, (float screenX, float screenY)> _labelScreenPositions = new();

    private int _nextCircle;
    private int _nextRectangle;
    private int _nextLine;
    private int _nextPolygon;
    private int _nextArrow;
    private int _nextSprite;
    private int _nextLabel;
    private int _nextTextBackground;

    // ── Configuration ────────────────────────────────────────────────────

    /// <summary>
    /// Z draw-order assigned to newly created pool objects. Higher values draw in front.
    /// Defaults to 1000, placing Overlay in front of typical world content (Z = 0).
    /// </summary>
    public float DefaultZ { get; set; } = 1000f;

    // ── Internal arrow entry ─────────────────────────────────────────────

    private sealed class ArrowEntry
    {
        internal readonly LineShape Body;
        internal readonly PolyShape Head;
        internal ArrowEntry(LineShape body, PolyShape head) { Body = body; Head = head; }
    }

    // ── Draw calls ──────────────────────────────────────────────────────

    /// <summary>
    /// Draws a circle outline at world position (<paramref name="x"/>, <paramref name="y"/>)
    /// with the given <paramref name="radius"/> for this frame.
    /// </summary>
    public CircleShape Circle(float x, float y, float radius, XnaColor? color = null)
    {
        while (_nextCircle >= _circles.Count)
            _circles.Add(CreateCircle());

        var c = _circles[_nextCircle++];
        c.IsVisible = true;
        c.X = x;
        c.Y = y;
        c.Radius = radius;
        c.Color = color ?? XnaColor.White;
        return c;
    }

    /// <summary>
    /// Draws a rectangle outline centered at world position (<paramref name="x"/>, <paramref name="y"/>)
    /// with the given dimensions for this frame.
    /// </summary>
    public RectShape Rectangle(float x, float y, float width, float height, XnaColor? color = null)
    {
        while (_nextRectangle >= _rectangles.Count)
            _rectangles.Add(CreateRectangle());

        var r = _rectangles[_nextRectangle++];
        r.IsVisible = true;
        r.X = x;
        r.Y = y;
        r.Width = width;
        r.Height = height;
        r.Color = color ?? XnaColor.White;
        return r;
    }

    /// <summary>
    /// Draws a line segment between two world-space points for this frame.
    /// </summary>
    public LineShape Line(float x1, float y1, float x2, float y2, XnaColor? color = null)
    {
        while (_nextLine >= _lines.Count)
            _lines.Add(CreateLine());

        var l = _lines[_nextLine++];
        l.IsVisible = true;
        l.X = x1;
        l.Y = y1;
        l.EndPoint = new Vector2(x2 - x1, y2 - y1);
        l.Color = color ?? XnaColor.White;
        return l;
    }

    /// <summary>
    /// Draws a polygon outline centered at world position (<paramref name="x"/>, <paramref name="y"/>).
    /// <paramref name="relativePoints"/> are offsets from the center, in world units.
    /// </summary>
    public PolyShape Polygon(float x, float y, IEnumerable<Vector2> relativePoints, XnaColor? color = null)
    {
        while (_nextPolygon >= _polygons.Count)
            _polygons.Add(CreatePolygon());

        var p = _polygons[_nextPolygon++];
        p.IsVisible = true;
        p.X = x;
        p.Y = y;
        p.SetPoints(relativePoints);
        p.Color = color ?? XnaColor.White;
        return p;
    }

    /// <summary>
    /// Draws a line with a filled triangular arrowhead pointing toward
    /// (<paramref name="x2"/>, <paramref name="y2"/>) for this frame.
    /// </summary>
    public void Arrow(float x1, float y1, float x2, float y2, XnaColor? color = null)
    {
        while (_nextArrow >= _arrows.Count)
            _arrows.Add(CreateArrow());

        var entry = _arrows[_nextArrow++];
        var c = color ?? XnaColor.White;

        const float BodyThickness  = 2.5f;
        const float HeadLength     = 14f;
        const float HeadHalfWidth  = 7f;

        float dx = x2 - x1, dy = y2 - y1;
        float len = MathF.Sqrt(dx * dx + dy * dy);

        entry.Body.IsVisible = true;
        entry.Body.Color = c;
        entry.Body.LineThickness = BodyThickness;

        if (len > 0.001f)
        {
            float dirX = dx / len, dirY = dy / len;
            float perpX = -dirY, perpY = dirX;

            float bodyEndX = x2 - dirX * HeadLength;
            float bodyEndY = y2 - dirY * HeadLength;
            entry.Body.X = x1;
            entry.Body.Y = y1;
            entry.Body.EndPoint = new Vector2(bodyEndX - x1, bodyEndY - y1);

            entry.Head.IsVisible = true;
            entry.Head.X = x2;
            entry.Head.Y = y2;
            entry.Head.Color = c;
            entry.Head.SetPoints(new[]
            {
                new Vector2(0f, 0f),
                new Vector2(-dirX * HeadLength + perpX * HeadHalfWidth,
                            -dirY * HeadLength + perpY * HeadHalfWidth),
                new Vector2(-dirX * HeadLength - perpX * HeadHalfWidth,
                            -dirY * HeadLength - perpY * HeadHalfWidth),
            });
        }
        else
        {
            entry.Body.X = x1;
            entry.Body.Y = y1;
            entry.Body.EndPoint = Vector2.Zero;
            entry.Head.IsVisible = false;
        }
    }

    /// <summary>
    /// Draws a sprite with <paramref name="texture"/> at world position (<paramref name="x"/>, <paramref name="y"/>)
    /// for this frame. Sizes to the texture dimensions at a 1:1 scale by default.
    /// </summary>
    /// <returns>The underlying <see cref="Sprite"/> for optional further configuration (size, rotation, alpha).</returns>
    public SpriteShape Sprite(Texture2D texture, float x, float y)
    {
        while (_nextSprite >= _sprites.Count)
            _sprites.Add(CreateSprite());

        var s = _sprites[_nextSprite++];
        s.IsVisible = true;
        s.X = x;
        s.Y = y;
        s.Texture = texture;
        return s;
    }

    /// <summary>
    /// Draws a text label at world position (<paramref name="worldX"/>, <paramref name="worldY"/>)
    /// for this frame. The label is positioned in screen space; it does not scale or move
    /// with the camera.
    /// </summary>
    /// <returns>
    /// The underlying Gum <see cref="Label"/> for optional same-frame configuration.
    /// Pass this to <see cref="TextBackground"/> to draw a colored backing rectangle.
    /// </returns>
    public Label Text(string text, float worldX, float worldY)
    {
        while (_nextLabel >= _labels.Count)
            _labels.Add(CreateLabel());

        var label = _labels[_nextLabel++];
        var screenPos = _screen.Camera.WorldToScreen(new Vector2(worldX, worldY));
        label.Visual.Visible = true;
        label.X = screenPos.X;
        label.Y = screenPos.Y;
        label.Text = text;
        _labelScreenPositions[label] = (screenPos.X, screenPos.Y);
        return label;
    }

    /// <summary>
    /// Draws a filled background rectangle behind <paramref name="label"/> for this frame.
    /// Always call this immediately after <see cref="Text"/> in the same frame.
    /// </summary>
    /// <param name="label">A label returned by <see cref="Text"/> this frame.</param>
    /// <param name="bgColor">Background fill color. Defaults to semi-transparent black.</param>
    public RectShape TextBackground(Label label, XnaColor? bgColor = null)
    {
        if (!_labelScreenPositions.TryGetValue(label, out var sp))
            return new RectShape();

        while (_nextTextBackground >= _textBackgrounds.Count)
            _textBackgrounds.Add(CreateTextBackground());

        var bg = _textBackgrounds[_nextTextBackground++];

        const float Padding = 4f;
        float totalScreenW = label.ActualWidth  + Padding * 2f;
        float totalScreenH = label.ActualHeight + Padding * 2f;

        var camera = _screen.Camera;
        float centerScreenX = sp.screenX + label.ActualWidth  / 2f;
        float centerScreenY = sp.screenY + label.ActualHeight / 2f;
        var worldCenter = camera.ScreenToWorld(new Vector2(centerScreenX, centerScreenY));

        var worldOrigin = camera.ScreenToWorld(Vector2.Zero);
        float worldW = MathF.Abs(camera.ScreenToWorld(new Vector2(totalScreenW, 0f)).X - worldOrigin.X);
        float worldH = MathF.Abs(camera.ScreenToWorld(new Vector2(0f, totalScreenH)).Y - worldOrigin.Y);

        bg.IsVisible = true;
        bg.X = worldCenter.X;
        bg.Y = worldCenter.Y;
        bg.Width = worldW;
        bg.Height = worldH;
        bg.Color = bgColor ?? new XnaColor(0, 0, 0, 200);
        return bg;
    }

    // ── Frame lifecycle ──────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="FlatRedBallService"/> at the start of each frame.
    /// Hides all pool objects so they disappear unless re-requested this frame.
    /// </summary>
    internal void BeginFrame()
    {
        foreach (var c in _circles) c.IsVisible = false;
        foreach (var r in _rectangles) r.IsVisible = false;
        foreach (var l in _lines) l.IsVisible = false;
        foreach (var p in _polygons) p.IsVisible = false;
        foreach (var a in _arrows) { a.Body.IsVisible = false; a.Head.IsVisible = false; }
        foreach (var s in _sprites) s.IsVisible = false;
        foreach (var l in _labels) l.Visual.Visible = false;
        foreach (var bg in _textBackgrounds) bg.IsVisible = false;

        _labelScreenPositions.Clear();

        _nextCircle = 0;
        _nextRectangle = 0;
        _nextLine = 0;
        _nextPolygon = 0;
        _nextArrow = 0;
        _nextSprite = 0;
        _nextLabel = 0;
        _nextTextBackground = 0;
    }

    // ── Pool factory helpers ─────────────────────────────────────────────

    private CircleShape CreateCircle()
    {
        var c = new CircleShape { IsFilled = false, IsVisible = false, Z = DefaultZ };
        _screen.Add(c);
        return c;
    }

    private RectShape CreateRectangle()
    {
        var r = new RectShape { IsFilled = false, IsVisible = false, Z = DefaultZ };
        _screen.Add(r);
        return r;
    }

    private LineShape CreateLine()
    {
        var l = new LineShape { IsVisible = false, Z = DefaultZ };
        _screen.Add(l);
        return l;
    }

    private PolyShape CreatePolygon()
    {
        var p = new PolyShape { IsFilled = false, IsVisible = false, Z = DefaultZ };
        _screen.Add(p);
        return p;
    }

    private ArrowEntry CreateArrow()
    {
        var body = new LineShape { IsVisible = false, Z = DefaultZ };
        var head = new PolyShape { IsFilled = true, IsVisible = false, Z = DefaultZ };
        _screen.Add(body);
        _screen.Add(head);
        return new ArrowEntry(body, head);
    }

    private SpriteShape CreateSprite()
    {
        var s = new SpriteShape { IsVisible = false, Z = DefaultZ };
        _screen.Add(s);
        return s;
    }

    private Label CreateLabel()
    {
        var label = new Label { Text = "" };
        _screen.Add(label);
        return label;
    }

    private RectShape CreateTextBackground()
    {
        var r = new RectShape { IsFilled = true, IsVisible = false, Z = DefaultZ - 1f };
        _screen.Add(r);
        return r;
    }
}
