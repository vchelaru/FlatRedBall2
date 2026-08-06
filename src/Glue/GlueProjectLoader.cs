using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// Reads a Glue <c>.gluj</c> project and the <c>.glsj</c>/<c>.glej</c> element files it references.
/// </summary>
/// <remarks>
/// Parsing only — the loaded graph is data. Building real objects from it, applying variables, and
/// resolving inheritance are later phases, so a project whose contents this build cannot construct
/// still loads cleanly and reports what it could not handle.
/// </remarks>
public static class GlueProjectLoader
{
    private const string ScreenExtension = ".glsj";
    private const string EntityExtension = ".glej";

    /// <summary>
    /// Loads the project at <paramref name="glujPath"/> and resolves its element references.
    /// </summary>
    /// <param name="glujPath">Path to the <c>.gluj</c> file.</param>
    /// <param name="options">File access and strictness. Defaults to reading from disk, tolerantly.</param>
    /// <returns>The project plus every diagnostic raised. Check <see cref="GlueLoadResult.HasErrors"/>.</returns>
    /// <exception cref="GlueLoadException">Only when <see cref="GlueLoadOptions.Strict"/> is set.</exception>
    public static GlueLoadResult Load(string glujPath, GlueLoadOptions? options = null)
    {
        options ??= new GlueLoadOptions();
        var diagnostics = new List<GlueLoadDiagnostic>();

        GlueProjectSave? project = ReadFile(glujPath, GlueJsonContext.Default.GlueProjectSave, options, diagnostics, elementName: null);

        if (project is null)
        {
            // Hand back an empty project rather than null so callers have one shape to handle.
            return new GlueLoadResult(new GlueProjectSave(), diagnostics);
        }

        ReportVersion(project, diagnostics, options);

        string projectDirectory = GetDirectory(glujPath, out char pathSeparator);

        foreach (var reference in project.ScreenReferences)
        {
            var screen = ReadElement(reference.Name, ScreenExtension, projectDirectory, pathSeparator,
                GlueJsonContext.Default.ScreenSave, options, diagnostics);
            if (screen is not null)
                project.Screens.Add(screen);
        }

        foreach (var reference in project.EntityReferences)
        {
            var entity = ReadElement(reference.Name, EntityExtension, projectDirectory, pathSeparator,
                GlueJsonContext.Default.EntitySave, options, diagnostics);
            if (entity is not null)
                project.Entities.Add(entity);
        }

        // Matches FRB1: once resolved, the references are spent and the elements are the truth.
        project.ScreenReferences.Clear();
        project.EntityReferences.Clear();

        // Before anything inspects an element, make it whole. A derived file on its own is badly
        // incomplete, and nothing downstream can reach its base to find out what is missing.
        GlueInheritanceResolver.Flatten(project, diagnostics);

        ReportUnbuildableTypes(project, diagnostics, options);

        return new GlueLoadResult(project, diagnostics, ResolveStartUpScreen(project, diagnostics, options));
    }

    /// <summary>
    /// Turns a Glue element name into a path. Element names are backslash-separated regardless of
    /// platform, so they need converting before use — but only here. Everywhere else the name is an
    /// identity that <c>StartUpScreen</c> and <c>BaseScreen</c> compare against verbatim.
    /// </summary>
    internal static string ElementNameToRelativePath(string elementName) =>
        elementName.Replace('\\', '/');

    private static readonly char[] PathSeparatorChars = { '\\', '/' };

    /// <summary>
    /// Splits off the directory portion of <paramref name="path"/> and reports which separator it
    /// used, without going through <see cref="Path.GetDirectoryName(string)"/>. That API infers the
    /// separator from the running OS, not from the string itself, so the same project path resolves
    /// its element files differently on Windows than on Linux CI — this instead mirrors whatever
    /// separator the caller's path already contains, so resolution is identical on every platform.
    /// </summary>
    private static string GetDirectory(string path, out char separator)
    {
        int lastSeparator = path.LastIndexOfAny(PathSeparatorChars);
        if (lastSeparator < 0)
        {
            separator = '/';
            return string.Empty;
        }

        separator = path[lastSeparator];
        return path[..lastSeparator];
    }

    private static TElement? ReadElement<TElement>(
        string? elementName,
        string extension,
        string projectDirectory,
        char pathSeparator,
        JsonTypeInfo<TElement> typeInfo,
        GlueLoadOptions options,
        List<GlueLoadDiagnostic> diagnostics)
        where TElement : GlueElement
    {
        if (string.IsNullOrEmpty(elementName))
        {
            Report(diagnostics, options, new GlueLoadDiagnostic(
                GlueDiagnosticSeverity.Warning, "An element reference has no name and was skipped."));
            return null;
        }

        string relativePath = ElementNameToRelativePath(elementName) + extension;
        string path = projectDirectory.Length == 0 ? relativePath : projectDirectory + pathSeparator + relativePath;
        var element = ReadFile(path, typeInfo, options, diagnostics, elementName);

        if (element is not null && element.Name != elementName)
        {
            // The file that was found wins; the mismatch usually means a rename went half-applied.
            Report(diagnostics, options, new GlueLoadDiagnostic(
                GlueDiagnosticSeverity.Warning,
                $"File declares its name as '{element.Name}' but was referenced as '{elementName}'. " +
                "Using the file that was found.",
                elementName));
        }

        return element;
    }

    private static T? ReadFile<T>(
        string path,
        JsonTypeInfo<T> typeInfo,
        GlueLoadOptions options,
        List<GlueLoadDiagnostic> diagnostics,
        string? elementName)
    {
        string? resolvedPath = options.ResolveFilePath(path);

        if (resolvedPath is null)
        {
            // A missing element is survivable; a missing project is not.
            var severity = elementName is null ? GlueDiagnosticSeverity.Error : GlueDiagnosticSeverity.Warning;
            Report(diagnostics, options, new GlueLoadDiagnostic(
                severity, $"Could not find '{path}'.", elementName));
            return default;
        }

        if (!string.Equals(resolvedPath, path, StringComparison.Ordinal))
        {
            Report(diagnostics, options, new GlueLoadDiagnostic(
                GlueDiagnosticSeverity.Warning,
                $"Found '{resolvedPath}' for '{path}' by ignoring case. This loads here but may not " +
                "on a case-sensitive filesystem.",
                elementName));
        }

        try
        {
            return JsonSerializer.Deserialize(options.ReadAllText(resolvedPath), typeInfo);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            var severity = elementName is null ? GlueDiagnosticSeverity.Error : GlueDiagnosticSeverity.Warning;
            Report(diagnostics, options, new GlueLoadDiagnostic(
                severity, $"Could not read '{resolvedPath}': {exception.Message}", elementName));
            return default;
        }
    }

    /// <summary>
    /// Finds the screen the project starts on. Compares against the element name verbatim — the
    /// backslash form is an identity here, not a path.
    /// </summary>
    private static ScreenSave? ResolveStartUpScreen(
        GlueProjectSave project, List<GlueLoadDiagnostic> diagnostics, GlueLoadOptions options)
    {
        if (string.IsNullOrEmpty(project.StartUpScreen))
            return null;

        foreach (var screen in project.Screens)
        {
            if (string.Equals(screen.Name, project.StartUpScreen, StringComparison.OrdinalIgnoreCase))
                return screen;
        }

        // An error rather than a warning: the project named a starting point and it is not there, so
        // there is nothing sensible to boot into.
        Report(diagnostics, options, new GlueLoadDiagnostic(
            GlueDiagnosticSeverity.Error,
            $"StartUpScreen is '{project.StartUpScreen}', but no screen with that name was loaded.",
            project.StartUpScreen));

        return null;
    }

    /// <summary>
    /// Walks every object in the project and notes the ones whose type this build cannot construct.
    /// </summary>
    /// <remarks>
    /// These are warnings by design, not errors: most of a real project's objects belong to phases
    /// that have not landed yet, so a project that reports many of these has still loaded correctly.
    /// The count is a progress metric — each phase that lands should shrink it.
    /// </remarks>
    private static void ReportUnbuildableTypes(
        GlueProjectSave project, List<GlueLoadDiagnostic> diagnostics, GlueLoadOptions options)
    {
        var elementNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var screen in project.Screens)
        {
            if (screen.Name is not null)
                elementNames.Add(screen.Name);
        }

        foreach (var entity in project.Entities)
        {
            if (entity.Name is not null)
                elementNames.Add(entity.Name);
        }

        foreach (var screen in project.Screens)
            ReportUnbuildableTypes(screen.NamedObjects, screen.Name, elementNames, diagnostics, options);

        foreach (var entity in project.Entities)
            ReportUnbuildableTypes(entity.NamedObjects, entity.Name, elementNames, diagnostics, options);
    }

    private static void ReportUnbuildableTypes(
        List<NamedObjectSave> namedObjects,
        string? elementName,
        HashSet<string> elementNames,
        List<GlueLoadDiagnostic> diagnostics,
        GlueLoadOptions options)
    {
        foreach (var namedObject in namedObjects)
        {
            var typeName = GlueTypeName.Parse(namedObject.SourceClassType);

            // An object whose type names another element resolves within the project, so it is not
            // unmapped — it is built from that element's own data.
            bool isKnownElement = elementNames.Contains(typeName.ToElementNameCandidate());

            // Tile objects and collision relationships are not in GlueTypeMap because they are not
            // constructed from a CLR type — a map comes from a file, a collection is derived from a
            // map, and a relationship is registered rather than instantiated. Counting them as
            // unmapped would report as missing the very things that now work.
            bool isBuiltElsewhere =
                GlueTileBuilder.IsTileObject(namedObject) ||
                GlueCollisionBuilder.IsRelationship(namedObject);

            if (!isKnownElement && !isBuiltElsewhere && !GlueTypeMap.TryGetType(typeName, out _))
            {
                Report(diagnostics, options, new GlueLoadDiagnostic(
                    GlueDiagnosticSeverity.Warning,
                    $"'{namedObject.InstanceName}' is a '{namedObject.SourceClassType}', which " +
                    "cannot be built by this build. A later phase owns this type.",
                    elementName));
            }

            ReportUnbuildableTypes(namedObject.ContainedObjects, elementName, elementNames, diagnostics, options);
        }
    }

    private static void ReportVersion(
        GlueProjectSave project, List<GlueLoadDiagnostic> diagnostics, GlueLoadOptions options)
    {
        if (project.FileVersion >= GlueVersions.Latest)
            return;

        string detail = project.FileVersion < GlueVersions.LastFileShapeChange
            ? $"This is below version {GlueVersions.LastFileShapeChange}, the last version that changed " +
              "what Glue writes to disk, so some data may be shaped differently than expected."
            : "This is above every version that changed what Glue writes to disk, so it reads the same " +
              "as a current project.";

        Report(diagnostics, options, new GlueLoadDiagnostic(
            GlueDiagnosticSeverity.Info,
            $"Project is FileVersion {project.FileVersion}; the newest known is {GlueVersions.Latest}. {detail}"));
    }

    private static void Report(
        List<GlueLoadDiagnostic> diagnostics, GlueLoadOptions options, GlueLoadDiagnostic diagnostic)
    {
        diagnostics.Add(diagnostic);

        if (options.Strict && diagnostic.Severity == GlueDiagnosticSeverity.Error)
            throw new GlueLoadException(diagnostic);
    }
}
