using System;
using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// Finds a Glue project's Gum project, and turns its <c>.gusx</c>/<c>.gucx</c> references into the
/// element names Gum looks elements up by.
/// </summary>
/// <remarks>
/// Resolution only — nothing here touches Gum. Instantiating the visual needs a Gum runtime that
/// <see cref="FlatRedBallService"/> has initialized, so it lives at the point of use.
/// </remarks>
public static class GlueGumResolver
{
    private const string GumProjectExtension = ".gumx";
    private const string GumProjectJsonExtension = ".gumj";

    private static readonly string[] GumElementExtensions =
    {
        ".gusx", ".gucx", ".gusj", ".gucj",
    };

    /// <summary>
    /// The Gum project in <paramref name="project"/>'s global files, as authored — a path relative
    /// to the project's <c>Content</c> folder. Null when the project has no Gum project.
    /// </summary>
    public static string? FindGumProjectFile(GlueProjectSave project) =>
        project.GlobalFiles.FirstOrDefault(IsGumProject)?.Name;

    /// <summary>Whether this global file is the project's Gum project.</summary>
    public static bool IsGumProject(ReferencedFileSave file) =>
        HasExtension(file.Name, GumProjectExtension) || HasExtension(file.Name, GumProjectJsonExtension);

    /// <summary>Whether this referenced file is a Gum screen or component.</summary>
    public static bool IsGumElement(ReferencedFileSave file) =>
        GumElementExtensions.Any(extension => HasExtension(file.Name, extension));

    /// <summary>
    /// Whether this file loads through FRB1's legacy <c>GumIdb</c> model, which reads the
    /// <c>.gusx</c> directly rather than looking the element up in the project.
    /// </summary>
    /// <remarks>
    /// A genuinely different loading model, not just a different type name — worth a diagnostic
    /// rather than silent best-effort handling. See G54.
    /// </remarks>
    public static bool IsLegacyGumIdb(ReferencedFileSave file) =>
        file.RuntimeType is "FlatRedBall.Gum.GumIdb";

    /// <summary>
    /// The Gum element name for a referenced <c>.gusx</c>/<c>.gucx</c>, which is its path minus the
    /// Gum project's own folder, the <c>Screens</c>/<c>Components</c> category folder, and the
    /// extension. <c>GumProject/Screens/GameScreenGum.gusx</c> becomes <c>GameScreenGum</c>.
    /// </summary>
    /// <remarks>
    /// Folders <em>below</em> the category survive: Gum knows a nested component as
    /// <c>Controls/ButtonStandard</c>, so stripping them would break the lookup.
    /// </remarks>
    /// <param name="referencedFileName">The referenced file's <see cref="ReferencedFileSave.Name"/>.</param>
    /// <param name="gumProjectFile">The project's Gum project path, whose folder comes off the front.</param>
    public static string? ElementNameFor(string? referencedFileName, string? gumProjectFile)
    {
        if (string.IsNullOrWhiteSpace(referencedFileName))
            return null;

        // Documented as forward-slashed, written both ways in practice.
        string path = referencedFileName!.Replace('\\', '/');

        string? gumProjectFolder = DirectoryOf(gumProjectFile?.Replace('\\', '/'));
        if (!string.IsNullOrEmpty(gumProjectFolder))
            path = StripLeadingSegment(path, gumProjectFolder!);

        path = StripLeadingSegment(path, "Screens");
        path = StripLeadingSegment(path, "Components");

        int lastDot = path.LastIndexOf('.');
        int lastSlash = path.LastIndexOf('/');
        if (lastDot > lastSlash)
            path = path.Substring(0, lastDot);

        return path.Length == 0 ? null : path;
    }

    /// <summary>
    /// Whether this object is a Gum visual rather than something the type map builds.
    /// </summary>
    /// <remarks>
    /// G51 — a Gum object's <see cref="NamedObjectSave.SourceClassType"/> names a class Glue's
    /// codegen generates, which exists in no FRB2 build. It is classified here rather than added to
    /// <see cref="GlueTypeMap"/>, because there is no fixed set of names to enumerate: every project
    /// generates its own.
    /// </remarks>
    public static bool IsGumObject(NamedObjectSave save) =>
        save.SourceType == SourceType.Gum ||
        (save.SourceType == SourceType.File &&
            GumElementExtensions.Any(extension => HasExtension(save.SourceFile, extension)));

    /// <summary>
    /// The Gum component a <see cref="NamedObjectSave"/> builds, or null when it is not a Gum object.
    /// </summary>
    /// <remarks>
    /// Two shapes reach the same place. <see cref="SourceType.Gum"/> identifies the element by its
    /// generated runtime type name and puts that same bare name in
    /// <see cref="NamedObjectSave.SourceFile"/> where every other source type puts a path — so
    /// resolving it as a path looks for a file that does not exist. <see cref="SourceType.File"/>
    /// with a <c>.gucx</c> is the ordinary path case.
    /// </remarks>
    public static string? ComponentElementNameFor(NamedObjectSave save, string? gumProjectFile)
    {
        if (save.SourceType == SourceType.Gum)
            return ElementNameFromRuntimeType(save.SourceClassType);

        bool isGumFile = GumElementExtensions.Any(
            extension => HasExtension(save.SourceFile, extension));

        return save.SourceType == SourceType.File && isGumFile
            ? ElementNameFor(save.SourceFile, gumProjectFile)
            : null;
    }

    /// <summary>
    /// The Gum element name behind a generated runtime class name — its namespace and the
    /// <c>Runtime</c> suffix Glue's codegen appends both come off.
    /// </summary>
    /// <remarks>
    /// A name that does not end in <c>Runtime</c> is returned as-is rather than truncated.
    /// </remarks>
    public static string? ElementNameFromRuntimeType(string? runtimeType)
    {
        if (string.IsNullOrWhiteSpace(runtimeType))
            return null;

        string name = runtimeType!;

        int lastDot = name.LastIndexOf('.');
        if (lastDot >= 0)
            name = name.Substring(lastDot + 1);

        const string suffix = "Runtime";
        if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
            name = name.Substring(0, name.Length - suffix.Length);

        return name.Length == 0 ? null : name;
    }

    /// <summary>
    /// The Gum element with this name in the loaded Gum project, or null when there is none.
    /// </summary>
    /// <remarks>
    /// Matches a trailing segment as well as the whole name, because a generated runtime type name
    /// loses the folders Gum keeps: <c>ButtonStandardRuntime</c> has to find
    /// <c>Controls/ButtonStandard</c>.
    /// </remarks>
    public static Gum.DataTypes.ElementSave? FindGumElement(string? elementName)
    {
        var project = Gum.Managers.ObjectFinder.Self.GumProjectSave;

        if (project is null || string.IsNullOrEmpty(elementName))
            return null;

        foreach (var component in project.Components)
        {
            if (NameMatches(component.Name, elementName!))
                return component;
        }

        foreach (var screen in project.Screens)
        {
            if (NameMatches(screen.Name, elementName!))
                return screen;
        }

        return null;
    }

    private static bool NameMatches(string? candidate, string elementName)
    {
        if (candidate is null)
            return false;

        if (string.Equals(candidate, elementName, StringComparison.OrdinalIgnoreCase))
            return true;

        int lastSlash = candidate.LastIndexOf('/');
        return lastSlash >= 0 &&
            string.Equals(candidate.Substring(lastSlash + 1), elementName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The Gum screen <paramref name="element"/> shows, or null when neither it nor anything it
    /// derives from references one.
    /// </summary>
    /// <remarks>
    /// A loaded project has already merged each element with its base (<see cref="GlueProjectLoader"/>
    /// flattens on load), so the inherited case normally resolves from <paramref name="element"/>'s
    /// own referenced files. The walk up <paramref name="project"/> is the fallback for an element
    /// that has not been flattened.
    /// </remarks>
    public static string? GumElementNameFor(
        GlueElement element, GlueProjectSave project, string? gumProjectFile)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        GlueElement? current = element;

        while (current is not null)
        {
            var gumFile = current.ReferencedFiles.FirstOrDefault(IsGumElement);
            if (gumFile is not null)
                return ElementNameFor(gumFile.Name, gumProjectFile);

            string? baseName = current.BaseElement;
            if (string.IsNullOrEmpty(baseName) || !seen.Add(baseName!))
                return null;

            current = FindElement(project, baseName!);
        }

        return null;
    }

    private static GlueElement? FindElement(GlueProjectSave project, string name)
    {
        foreach (var screen in project.Screens)
        {
            if (string.Equals(screen.Name, name, StringComparison.OrdinalIgnoreCase))
                return screen;
        }

        foreach (var entity in project.Entities)
        {
            if (string.Equals(entity.Name, name, StringComparison.OrdinalIgnoreCase))
                return entity;
        }

        return null;
    }

    private static bool HasExtension(string? name, string extension) =>
        name is not null && name.EndsWith(extension, StringComparison.OrdinalIgnoreCase);

    private static string? DirectoryOf(string? path)
    {
        int lastSlash = path?.LastIndexOf('/') ?? -1;
        return lastSlash <= 0 ? null : path!.Substring(0, lastSlash);
    }

    private static string StripLeadingSegment(string path, string segment)
    {
        if (path.Length > segment.Length &&
            path[segment.Length] == '/' &&
            path.AsSpan(0, segment.Length).Equals(segment.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return path.Substring(segment.Length + 1);
        }

        return path;
    }
}
