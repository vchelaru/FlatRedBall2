using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FlatRedBall2.AnimationEditorCommon;
using FlatRedBall2.Glue.Model;
using FlatRedBall2.IO;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

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
    /// Reads a text file at <paramref name="relativePath"/> (relative to <see cref="ContentRoot"/>)
    /// that is not a <see cref="ReferencedFileSave"/> — e.g. a sidecar file discovered by naming
    /// convention rather than named in an instruction. Returns null when nothing exists there; that
    /// is the common case (most elements have no sidecar), so it is silent rather than a diagnostic.
    /// A file that exists but fails to <em>parse</em> is the caller's diagnostic to raise, not this
    /// method's — it only reports read failures.
    /// </summary>
    internal string? TryReadRelativeText(string relativePath)
    {
        string path = Path.Combine(ContentRoot, relativePath).Replace('\\', '/');

        try
        {
            using var stream = _content.StreamProvider(path);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }

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

        LoadFiles(element.ReferencedFiles, element.Name, diagnostics);
    }

    /// <summary>
    /// Loads a project's <c>GlobalFiles</c> — the same <see cref="ReferencedFileSave"/> shape as an
    /// element's own <see cref="GlueElement.ReferencedFiles"/>, but declared at the project root
    /// rather than under a screen or entity. <see cref="GlueGumResolver"/> already pulls the Gum
    /// project out of this same list; this loads everything else in it the same way an element's
    /// own referenced files are loaded.
    /// </summary>
    internal void LoadGlobalFiles(List<ReferencedFileSave> files, List<GlueLoadDiagnostic> diagnostics) =>
        LoadFiles(files, elementName: null, diagnostics);

    private void LoadFiles(
        List<ReferencedFileSave> files, string? elementName, List<GlueLoadDiagnostic> diagnostics)
    {
        foreach (var file in files)
        {
            if (string.IsNullOrEmpty(file.Name) || !file.LoadedAtRuntime)
                continue;

            if (file.Name.Contains('*'))
            {
                LoadWildcard(file.Name, elementName, diagnostics);
                continue;
            }

            LoadOne(file, elementName, diagnostics);
        }
    }

    /// <summary>
    /// Expands a <c>Name</c> like <c>GlobalContent/Audio/Sfx/**/*.wav</c> against the files actually
    /// on disk under <see cref="ContentRoot"/>, then loads each match through the normal per-extension
    /// path. <c>**</c> matches any depth of subdirectories (including none); a bare <c>*</c> matches
    /// anything within one path segment. Matching is case-insensitive, matching how the rest of this
    /// loader keys its lookups (<see cref="_assets"/>, <see cref="_text"/>).
    /// </summary>
    /// <remarks>
    /// Unlike every other read in this type, this walks the real filesystem rather than going through
    /// <c>ContentLoader.StreamProvider</c> — expanding a glob means listing a directory, and
    /// <c>TitleContainer</c> has no such operation on any backend (the browser target has no
    /// directory listing at all). Wildcard <c>GlobalFiles</c> entries are a desktop-authoring feature;
    /// each expanded match is loaded normally afterward, so the result is browser-safe even though the
    /// expansion step itself is not.
    /// </remarks>
    private void LoadWildcard(string pattern, string? elementName, List<GlueLoadDiagnostic> diagnostics)
    {
        string absoluteRoot = Path.Combine(AppContext.BaseDirectory, ContentRoot);

        if (!Directory.Exists(absoluteRoot))
        {
            Warn(diagnostics, elementName,
                $"'{pattern}' is a wildcard reference, but its content root could not be found to " +
                "expand it.");
            return;
        }

        var regex = BuildWildcardRegex(pattern);
        var matches = Directory.EnumerateFiles(absoluteRoot, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(absoluteRoot, f).Replace('\\', '/'))
            .Where(relative => regex.IsMatch(relative))
            .OrderBy(relative => relative, StringComparer.Ordinal)
            .ToList();

        if (matches.Count == 0)
        {
            Warn(diagnostics, elementName, $"'{pattern}' is a wildcard reference that matched no files.");
            return;
        }

        foreach (var relative in matches)
        {
            LoadOne(
                new ReferencedFileSave { Name = relative, IsCreatedByWildcard = true },
                elementName, diagnostics);
        }
    }

    /// <summary>
    /// Converts a Glue wildcard pattern into a regex matched against forward-slashed relative paths.
    /// </summary>
    /// <remarks>
    /// Walks the pattern one character at a time rather than round-tripping through
    /// <see cref="Regex.Escape(string)"/> on the whole string, so a literal <c>*</c> never has to be
    /// disguised behind a placeholder — every character is either a wildcard token handled here or an
    /// ordinary character escaped on its own.
    /// </remarks>
    private static Regex BuildWildcardRegex(string pattern)
    {
        string normalized = pattern.Replace('\\', '/');
        var regexPattern = new StringBuilder("^");
        int i = 0;

        while (i < normalized.Length)
        {
            bool isRecursiveSegment =
                normalized[i] == '*' && i + 2 < normalized.Length &&
                normalized[i + 1] == '*' && normalized[i + 2] == '/';

            if (isRecursiveSegment)
            {
                // "**/" - zero or more whole directory segments, so "a/**/*.wav" also matches "a/x.wav".
                regexPattern.Append("(?:.*/)?");
                i += 3;
            }
            else if (normalized[i] == '*')
            {
                // A bare "*" stays within one path segment.
                regexPattern.Append("[^/]*");
                i++;
            }
            else
            {
                regexPattern.Append(Regex.Escape(normalized[i].ToString()));
                i++;
            }
        }

        regexPattern.Append('$');
        return new Regex(regexPattern.ToString(), RegexOptions.IgnoreCase);
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
                    string text = ReadAllText(path);
                    _text[instanceName] = text;

                    if (file.CreatesCsvDictionary)
                    {
                        _assets[instanceName] = CsvTable.Parse(text).ToDictionary(header =>
                            Warn(diagnostics, elementName,
                                $"'{file.Name}' column '{header.Name}' declares type " +
                                $"'{header.Type}', which could not be resolved; its values are " +
                                "kept as raw text."));
                    }

                    break;

                case ".wav":
                    // SoundEffect.FromStream is WAV-only (PCM), but it does go through the loader's
                    // own StreamProvider seam, so this works on every backend including the browser.
                    using (var stream = _content.StreamProvider(path))
                    {
                        var soundEffect = SoundEffect.FromStream(stream);
                        _content.Track(soundEffect);
                        _assets[instanceName] = soundEffect;
                    }
                    break;

                case ".ogg":
                    // Song.FromUri is OGG-only on DesktopGL and needs a real file:// URI rather than
                    // a stream, so — unlike every other case here — it bypasses StreamProvider and
                    // resolves straight against the title location. Desktop-only for the same reason
                    // wildcard expansion is: no browser equivalent exists.
                    _assets[instanceName] = Song.FromUri(
                        instanceName, new Uri(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path))));
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
