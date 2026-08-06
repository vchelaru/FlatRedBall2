using System.Collections.Generic;

namespace FlatRedBall2.Glue.Model;

/// <summary>A named set of value assignments applied together. Applied in Phase 7.</summary>
public class StateSave
{
    /// <summary>The state's name.</summary>
    public string? Name { get; set; }

    /// <summary>The assignments this state applies.</summary>
    public List<InstructionSave> InstructionSaves { get; set; } = new();

    /// <inheritdoc />
    public override string ToString() => Name ?? base.ToString()!;
}

/// <summary>A group of mutually exclusive <see cref="StateSave"/>s. Applied in Phase 7.</summary>
public class StateSaveCategory
{
    /// <summary>The category's name.</summary>
    public string? Name { get; set; }

    /// <summary>The states in this category.</summary>
    public List<StateSave> States { get; set; } = new();

    /// <summary>Variables this category deliberately does not set.</summary>
    public List<string> ExcludedVariables { get; set; } = new();

    /// <inheritdoc />
    public override string ToString() => Name ?? base.ToString()!;
}
