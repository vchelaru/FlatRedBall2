using System;
using System.Collections.Generic;
using System.Numerics;
using Gum.Forms.Controls;
using MonoGameGum.GueDeriving;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using XnaColor = Microsoft.Xna.Framework.Color;

// Aliases to avoid method-vs-type name ambiguity within this file.
using CircleShape = FlatRedBall2.Collision.Circle;
using RectShape = FlatRedBall2.Collision.AARect;
using LineShape = FlatRedBall2.Collision.Line;
using PolyShape = FlatRedBall2.Collision.Polygon;
using SpriteShape = FlatRedBall2.Rendering.Sprite;
using SolidSides = FlatRedBall2.Collision.SolidSides;

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
/// <para>
/// <b>Single-camera assumption.</b> World-anchored text (<see cref="Text(string, float, float)"/>,
/// <see cref="Text(string)"/>) and its <see cref="TextBackground"/> are placed in
/// <c>Cameras[0]</c>'s HUD and positioned against <c>Cameras[0]</c>'s view. In split-screen they
/// only appear inside <c>Cameras[0]</c>'s viewport — there is no built-in way to target a
/// secondary camera's view. Shape methods (Circle/Rectangle/Line/Polygon/Arrow/Sprite) are
/// world-space and render in every camera's draw pass, so those work fine in split-screen.
/// Debug overlay × split-screen is intentionally not optimized — overlay is a debug tool and
/// split-screen is uncommon, so the intersection is left simple.
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
    /// for this frame. The label follows the world position — if the camera pans, the label
    /// moves with the world. Use this for annotations attached to game objects (entity names,
    /// health values above enemies, debug coordinates).
    /// For fixed screen-position text (HUD, diagnostics), use
    /// <see cref="TextScreen(string, float, float)"/> instead.
    /// </summary>
    /// <remarks>
    /// In split-screen, the label is placed in <c>Cameras[0]</c>'s HUD and only renders inside
    /// that camera's viewport. See the <see cref="Overlay"/> class remarks.
    /// </remarks>
    /// <returns>
    /// The underlying Gum <see cref="Label"/> for optional same-frame configuration.
    /// Pass this to <see cref="TextBackground"/> to draw a colored backing rectangle.
    /// </returns>
    public Label Text(string text, float worldX, float worldY)
    {
        var (canvasX, canvasY) = WorldToCanvas(_screen.Camera, worldX, worldY);
        return PlaceLabel(text, canvasX, canvasY);
    }

    // Converts a world-space point to Gum-canvas units (origin top-left, Y+ down) for the
    // current camera. The canvas spans (0, 0) at Camera.Left/Top to
    // (OrthogonalWidth/Zoom, OrthogonalHeight/Zoom) at Camera.Right/Bottom — i.e. canvas
    // units == world units within the visible area, just origin-shifted and Y-flipped.
    // Internal so tests can verify the math without constructing a Gum Label.
    internal static (float canvasX, float canvasY) WorldToCanvas(Camera camera, float worldX, float worldY)
    {
        return (worldX - camera.Left, camera.Top - worldY);
    }

    /// <summary>
    /// Draws a text label at the camera's world-space center for this frame. Convenience overload
    /// for quick "where am I, is this code running" debug labels — the label appears in the middle
    /// of the visible area regardless of where the camera currently is.
    /// </summary>
    public Label Text(string text)
        => Text(text, _screen.Camera.X, _screen.Camera.Y);

    /// <summary>
    /// Draws a text label at a fixed screen position for this frame. The label stays
    /// put regardless of camera movement. Use this for HUD text, diagnostics, or any overlay
    /// that should not move with the world.
    /// Coordinates are in Gum canvas space (origin top-left, Y increases downward).
    /// Canvas size = <c>Camera.OrthogonalWidth</c> x <c>Camera.OrthogonalHeight</c>.
    /// For text anchored to a world position, use <see cref="Text(string, float, float)"/> instead.
    /// </summary>
    /// <returns>
    /// The underlying Gum <see cref="Label"/> for optional same-frame configuration.
    /// Pass this to <see cref="TextBackground"/> to draw a colored backing rectangle.
    /// </returns>
    public Label TextScreen(string text, float screenX, float screenY)
    {
        return PlaceLabel(text, screenX, screenY);
    }

    private Label PlaceLabel(string text, float screenX, float screenY)
    {
        while (_nextLabel >= _labels.Count)
            _labels.Add(CreateLabel());

        var label = _labels[_nextLabel++];
        label.Visual.Visible = true;
        label.X = screenX;
        label.Y = screenY;
        label.Text = text;
        _labelScreenPositions[label] = (screenX, screenY);
        return label;
    }

    /// <summary>
    /// Draws a filled background rectangle behind <paramref name="label"/> for this frame.
    /// Always call this immediately after <see cref="Text(string, float, float)"/> in the same frame.
    /// </summary>
    /// <param name="label">A label returned by <see cref="Text(string, float, float)"/> this frame.</param>
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
        float zoom = camera.Zoom;
        // Stored positions are in Gum canvas space; ScreenToWorld expects viewport pixels.
        float centerVpX = (sp.screenX + label.ActualWidth  / 2f) * zoom;
        float centerVpY = (sp.screenY + label.ActualHeight / 2f) * zoom;
        var worldCenter = camera.ScreenToWorld(new Vector2(centerVpX, centerVpY));

        var worldOrigin = camera.ScreenToWorld(Vector2.Zero);
        float worldW = MathF.Abs(camera.ScreenToWorld(new Vector2(totalScreenW * zoom, 0f)).X - worldOrigin.X);
        float worldH = MathF.Abs(camera.ScreenToWorld(new Vector2(0f, totalScreenH * zoom)).Y - worldOrigin.Y);

        bg.IsVisible = true;
        bg.X = worldCenter.X;
        bg.Y = worldCenter.Y;
        bg.Width = worldW;
        bg.Height = worldH;
        bg.Color = bgColor ?? new XnaColor(0, 0, 0, 200);
        return bg;
    }

    /// <summary>
    /// Draws the four edges of each entity's first <see cref="AARect"/> child
    /// for every instance tracked by <paramref name="factory"/>. Each edge is green when its
    /// <see cref="SolidSides"/> bit is set (solid collision surface) and dim red when
    /// cleared (suppressed / pass-through). Call from <c>CustomActivity</c> each frame while
    /// diagnosing one-way platforms or <c>IsSolidGrid</c> seam issues. Entities without an
    /// <see cref="AARect"/> child are silently skipped.
    /// </summary>
    public void DrawSolidSides<T>(Factory<T> factory) where T : Entity, new()
    {
        foreach (var entity in factory.Instances)
        {
            var body = FindBody(entity);
            if (body != null) DrawRectSolidSides(body);
        }
    }

    /// <summary>
    /// Draws the four edges of every <see cref="AARect"/> tile in
    /// <paramref name="tiles"/>. Each edge is green when its <see cref="SolidSides"/>
    /// bit is set (solid collision surface) and dim red when cleared (suppressed, which is how
    /// <see cref="TileShapes"/> prevents seam snagging between adjacent tiles). Call
    /// from <c>CustomActivity</c> each frame while diagnosing tile-grid collision issues.
    /// </summary>
    public void DrawSolidSides(TileShapes tiles)
    {
        foreach (var renderable in tiles.AllTiles)
        {
            if (renderable is RectShape rect)
                DrawRectSolidSides(rect);
        }
    }

    private static AARect? FindBody(Entity entity)
    {
        foreach (var child in entity.Children)
            if (child is AARect rect) return rect;
        return null;
    }

    private void DrawRectSolidSides(RectShape rect)
    {
        var color = new XnaColor(60, 220, 60);
        float cx = rect.AbsoluteX;
        float cy = rect.AbsoluteY;
        float halfW = rect.Width / 2f;
        float halfH = rect.Height / 2f;
        // Triangle lives entirely INSIDE the rect. Tip touches the face (pointing outward
        // toward that face); base sits a fraction of the way toward the rect center. Fully
        // contained, so adjacent rects never overlap visually.
        const float EdgeInset = 1f;
        float faceL = cx - halfW + EdgeInset;
        float faceR = cx + halfW - EdgeInset;
        float faceB = cy - halfH + EdgeInset;
        float faceT = cy + halfH - EdgeInset;
        float depthX = halfW * 0.42f;
        float depthY = halfH * 0.42f;
        float baseHalfX = halfW * 0.28f;
        float baseHalfY = halfH * 0.28f;

        var rd = rect.SolidSides;
        if ((rd & SolidSides.Up) != 0)
            Triangle(cx, faceT,
                     cx - baseHalfX, faceT - depthY,
                     cx + baseHalfX, faceT - depthY, color);
        if ((rd & SolidSides.Down) != 0)
            Triangle(cx, faceB,
                     cx - baseHalfX, faceB + depthY,
                     cx + baseHalfX, faceB + depthY, color);
        if ((rd & SolidSides.Left) != 0)
            Triangle(faceL, cy,
                     faceL + depthX, cy - baseHalfY,
                     faceL + depthX, cy + baseHalfY, color);
        if ((rd & SolidSides.Right) != 0)
            Triangle(faceR, cy,
                     faceR - depthX, cy - baseHalfY,
                     faceR - depthX, cy + baseHalfY, color);
    }

    private void Triangle(float ax, float ay, float bx, float by, float cx, float cy, XnaColor color)
    {
        var p = Polygon(ax, ay, new[]
        {
            new Vector2(0f, 0f),
            new Vector2(bx - ax, by - ay),
            new Vector2(cx - ax, cy - ay),
        }, color);
        p.IsFilled = true;
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
        // Label's visual is a TextRuntime; default it to white so it reads against the
        // default dark TextBackground. Callers can recolor the returned Label as needed.
        if (label.Visual is TextRuntime text)
            text.Color = XnaColor.White;
        _screen.Add(label);
        // Raise the Gum renderable Z above the text background (DefaultZ - 1) so labels
        // draw on top of their backgrounds, not underneath.
        foreach (var r in _screen.GumRenderables)
        {
            if (r.Visual == label.Visual)
            {
                r.Z = DefaultZ;
                break;
            }
        }
        return label;
    }

    private RectShape CreateTextBackground()
    {
        var r = new RectShape { IsFilled = true, IsVisible = false, Z = DefaultZ - 1f };
        _screen.Add(r);
        return r;
    }
}
