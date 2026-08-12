using System;
using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>How much a <see cref="GlueLoadDiagnostic"/> should worry the caller.</summary>
public enum GlueDiagnosticSeverity
{
    /// <summary>Worth knowing; the project loaded as intended.</summary>
    Info = 0,

    /// <summary>Something was skipped or guessed at. The project is usable but incomplete.</summary>
    Warning = 1,

    /// <summary>The project could not be read, or something required is missing.</summary>
    Error = 2,
}

/// <summary>One thing the loader noticed. Diagnostics are collected rather than thrown.</summary>
/// <param name="Severity">How much this should worry the caller.</param>
/// <param name="Message">What happened, in terms the author of the Glue project would recognize.</param>
/// <param name="ElementName">The element involved, when the diagnostic is about one.</param>
public sealed record GlueLoadDiagnostic(
    GlueDiagnosticSeverity Severity,
    string Message,
    string? ElementName = null)
{
    /// <inheritdoc />
    public override string ToString() =>
        ElementName is null ? $"{Severity}: {Message}" : $"{Severity} [{ElementName}]: {Message}";
}

/// <summary>
/// A loaded Glue project plus everything the loader noticed on the way. Loading is deliberately
/// tolerant: a project that references things this phase cannot build yet still loads, and says so.
/// </summary>
public sealed class GlueLoadResult
{
    internal GlueLoadResult(
        GlueProjectSave project,
        IReadOnlyList<GlueLoadDiagnostic> diagnostics,
        ScreenSave? startUpScreen = null)
    {
        Project = project;
        Diagnostics = diagnostics;
        StartUpScreen = startUpScreen;
        GumProjectFile = GlueGumResolver.FindGumProjectFile(project);
    }

    /// <summary>The project. Never null — an unreadable file yields an empty project and an error.</summary>
    public GlueProjectSave Project { get; }

    /// <summary>
    /// The screen named by the project's <c>StartUpScreen</c>, already resolved. Null when the
    /// project names none, or names one that could not be found — the latter also raises an error.
    /// </summary>
    public ScreenSave? StartUpScreen { get; }

    /// <summary>
    /// The project's Gum project, as authored — a path relative to the <c>Content</c> folder, or
    /// null when the project has no UI. Gum only works when it loaded the project itself, so this is
    /// what to hand to <c>EngineInitSettings.GumProjectFile</c> before initializing.
    /// </summary>
    public string? GumProjectFile { get; }

    /// <summary>Everything the loader noticed, in the order it noticed it.</summary>
    public IReadOnlyList<GlueLoadDiagnostic> Diagnostics { get; }

    /// <summary>Whether anything failed outright. Warnings alone do not set this.</summary>
    public bool HasErrors => Diagnostics.Any(d => d.Severity == GlueDiagnosticSeverity.Error);
}

/// <summary>Thrown instead of collecting when <see cref="GlueLoadOptions.Strict"/> is set.</summary>
public sealed class GlueLoadException : Exception
{
    internal GlueLoadException(GlueLoadDiagnostic diagnostic)
        : base(diagnostic.ToString()) => Diagnostic = diagnostic;

    /// <summary>The diagnostic that stopped the load.</summary>
    public GlueLoadDiagnostic Diagnostic { get; }
}
