using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FlatRedBall2.Animation.Content;
using FlatRedBall2.Glue.Model;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Glue;

/// <summary>
/// Loads the assets a Glue element references, and makes them addressable by the name an authored
/// instruction uses.
/// </summary>
/// <remarks>
/// A sprite does <em>not</em> reach its texture through <c>SourceType.File</c>. It is an ordinary
/// object whose instruction carries the <em>instance name</em> of a
/// <see cref="ReferencedFileSave"/> as a bare string — <c>AnimationChains = "PlatformerAnimations"</c>
/// — so resolving content means matching that name against a transformed file name.
/// <para>Assign one to <c>GlueScreen.Content</c> or <c>GlueEntity.Content</c> before building.
/// Without it an element still builds; its file-typed values are reported and skipped.</para>
/// </remarks>
public sealed class GlueContentSource
{
    private readonly ContentLoader _content;
    private readonly Dictionary<string, object> _assets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _text = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<GlueElement> _loaded = new();
    private readonly Dictionary<string, Tiled.TileMap> _maps = new(StringComparer.OrdinalIgnoreCase);
    private readonly GraphicsDevice? _graphicsDevice;

    /// <summary>
    /// Creates a source that resolves paths under <paramref name="contentRoot"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="contentRoot"/> is the directory holding the <c>.gluj</c>, and Glue's file names
    /// are relative to it directly. The editor keeps a project and everything it references in one
    /// self-contained folder, so there is no <c>Content</c> folder in between.
    /// <para><b>It must be relative, and relative to the title location — the executable's folder,
    /// not the working directory.</b> That is what <c>TitleContainer</c> resolves against on every
    /// backend, and it is the same rule the browser target needs. An absolute path throws rather
    /// than resolving; the failure is caught and reported per file, so the symptom is every asset
    /// warning "could not be loaded" rather than an exception.</para>
    /// </remarks>
    public GlueContentSource(
        ContentLoader content, string contentRoot, GraphicsDevice? graphicsDevice = null)
    {
        _content = content;
        ContentRoot = contentRoot;
        _graphicsDevice = graphicsDevice;
    }

    /// <summary>The directory holding the project file.</summary>
    public string ContentRoot { get; }

    /// <summary>A loaded asset by its Glue instance name, or null if absent or of another type.</summary>
    public T? Get<T>(string instanceName) where T : class =>
        _assets.TryGetValue(instanceName, out object? asset) ? asset as T : null;

    /// <summary>
    /// Loads a tile map, caching by path so two elements referencing one map share it.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Get{T}"/> because a map is referenced by an object's own
    /// <c>SourceFile</c> rather than through the instance-name table — it is file-sourced, not
    /// named-asset-sourced.
    /// </remarks>
    internal Tiled.TileMap? LoadTileMap(
        string relativePath, string? elementName, List<GlueLoadDiagnostic> diagnostics)
    {
        string path = Path.Combine(ContentRoot, relativePath).Replace('\\', '/');

        if (_maps.TryGetValue(path, out var cached))
            return cached;

        if (_graphicsDevice is null)
        {
            Warn(diagnostics, elementName,
                $"'{relativePath}' is a tile map, which needs a graphics device this content " +
                "source was not given.");
            return null;
        }

        try
        {
            var map = new Tiled.TileMap(path, _graphicsDevice);
            _maps[path] = map;
            return map;
        }
        catch (Exception e)
        {
            Warn(diagnostics, elementName,
                $"'{relativePath}' could not be loaded ({e.GetType().Name}: {e.Message}).");
            return null;
        }
    }

    /// <summary>A loaded text file — currently CSVs, whose rows Phases 11 and 12 parse.</summary>
    public string? GetText(string instanceName) =>
        _text.TryGetValue(instanceName, out string? text) ? text : null;

    /// <summary>
    /// The member name Glue would address a file by: extension dropped, spaces and parentheses
    /// removed, hyphens underscored, path stripped, and a leading digit prefixed.
    /// </summary>
    /// <remarks>
    /// Public because it is the contract between a file name and the instruction that references it
    /// — a caller diagnosing a failed lookup needs to be able to compute the same key.
    /// <para>Stripping the path means two same-named files in one element collide; Glue's opt-out is
    /// <c>IncludeDirectoryRelativeToContainer</c>, and the collision is reported.</para>
    /// </remarks>
    public static string InstanceNameOf(string fileName)
    {
        ReadOnlySpan<char> withoutExtension = Path.GetFileNameWithoutExtension(fileName.AsSpan());
        var builder = new StringBuilder(withoutExtension.Length);

        foreach (char c in withoutExtension)
        {
            if (c is ' ' or '(' or ')')
                continue;

            builder.Append(c == '-' ? '_' : c);
        }

        if (builder.Length > 0 && char.IsDigit(builder[0]))
            builder.Insert(0, '_');

        return builder.ToString();
    }

    /// <summary>
    /// Loads everything <paramref name="element"/> references. Repeat calls for the same element are
    /// ignored, so a rebuild does not reload.
    /// </summary>
    internal void Load(GlueElement element, List<GlueLoadDiagnostic> diagnostics)
    {
        if (!_loaded.Add(element))
            return;

        foreach (var file in element.ReferencedFiles)
        {
            if (string.IsNullOrEmpty(file.Name) || !file.LoadedAtRuntime)
                continue;

            if (file.Name.Contains('*'))
            {
                Warn(diagnostics, element.Name,
                    $"'{file.Name}' is a wildcard reference, which this loader does not expand.");
                continue;
            }

            LoadOne(file, element.Name, diagnostics);
        }
    }

    private void LoadOne(ReferencedFileSave file, string? elementName, List<GlueLoadDiagnostic> diagnostics)
    {
        string instanceName = InstanceNameOf(file.Name!);

        if (_assets.ContainsKey(instanceName) || _text.ContainsKey(instanceName))
            return;

        // Relative to the .gluj itself. The editor keeps a project and everything it references in one
        // self-contained folder, so there is no fixed "Content" segment in between.
        string path = Path.Combine(ContentRoot, file.Name!).Replace('\\', '/');
        string extension = Path.GetExtension(file.Name!).ToLowerInvariant();

        try
        {
            switch (extension)
            {
                case ".png" or ".bmp" or ".jpg" or ".jpeg" or ".gif" or ".tga":
                    _assets[instanceName] = _content.Load<Texture2D>(path);
                    break;

                case ".achx":
                    _assets[instanceName] = _content.LoadAnimationChainList(path);
                    break;

                case ".csv":
                    _text[instanceName] = ReadAllText(path);
                    break;

                default:
                    // Tiled, Gum and the formats FRB2 has no reader for are owned elsewhere. Silent
                    // rather than warned: every real project carries several, and naming each one
                    // every load would bury the diagnostics that matter.
                    break;
            }
        }
        // Deliberately broad. A single unreadable asset must cost you that asset and nothing else —
        // the loader's whole posture is that a partial project beats an exception. The failures are
        // varied and not all of them are IO: an absolute path makes TitleContainer throw
        // ArgumentException, a malformed .achx throws from the XML reader, and a corrupt PNG throws
        // from the graphics device. Catching a curated list means the next unlisted one takes down
        // the entire element load.
        catch (Exception e)
        {
            Warn(diagnostics, elementName,
                $"'{file.Name}' could not be loaded ({e.GetType().Name}: {e.Message}); anything " +
                "referencing it will keep its default.");
        }
    }

    /// <remarks>
    /// Reads through the loader's own stream seam rather than <see cref="File"/>, so a CSV loads on
    /// every backend — the browser target resolves paths through the title container, where a direct
    /// file read has nothing to open.
    /// </remarks>
    private string ReadAllText(string path)
    {
        using var stream = _content.StreamProvider(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Whether <paramref name="file"/> is one the owner should add to the engine on its own, with no
    /// object instantiating it.
    /// </summary>
    /// <remarks>
    /// Mirrors FRB1's <c>ReferencedFileSaveCodeGenerator.GetIfShouldAddToManagers</c>. Adding a tile
    /// map to a screen in Glue produces a referenced file and no <c>NamedObject</c>, so a screen that
    /// only honoured its objects drew nothing.
    /// <para>
    /// The shared-static carve-out is the subtle half: a shared-static file is a template to clone
    /// from on an entity, but on a screen it is the screen's own content and is added. Tile maps are
    /// the only referenced type FRB2 can add today — FRB1 decides this from the type's
    /// <c>AssetTypeInfo</c>, which has no FRB2 equivalent.
    /// </para>
    /// </remarks>
    internal static bool ShouldAddToManagers(ReferencedFileSave file, bool ownerIsScreen) =>
        file.LoadedAtRuntime
        && !file.LoadedOnlyWhenReferenced
        && file.AddToManagers
        && (!file.IsSharedStatic || ownerIsScreen)
        && file.Name is not null
        && Path.GetExtension(file.Name).Equals(".tmx", StringComparison.OrdinalIgnoreCase);

    private static void Warn(List<GlueLoadDiagnostic> diagnostics, string? elementName, string message) =>
        diagnostics.Add(new GlueLoadDiagnostic(GlueDiagnosticSeverity.Warning, message, elementName));
}
