using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// Applies a Glue state — a named snapshot of an element's variables.
/// </summary>
/// <remarks>
/// The word "snapshot" is load-bearing. A state assigns <em>every variable it covers</em>, not just
/// the ones it names: where the state carries no instruction for a covered variable, that variable
/// is reset to its own <c>DefaultValue</c>. A state with an empty instruction list is therefore not
/// a no-op, and FRB1 has real states authored exactly that way.
/// <para>What a state covers is decided by <c>ExcludedVariables</c>, never by the instruction list.
/// An instruction naming an excluded or unknown variable is silently ignored by FRB1; here it is
/// reported, because an author who wrote one expected it to do something.</para>
/// </remarks>
internal static class GlueStateApplier
{
    /// <summary>Applies a named state, from a category when one is named.</summary>
    internal static void Apply(
        GlueElement save,
        string? categoryName,
        string stateName,
        object element,
        IReadOnlyDictionary<string, object> objects,
        Dictionary<string, JsonElement> bag,
        List<GlueLoadDiagnostic> diagnostics)
    {
        StateSave? state;
        IReadOnlyList<CustomVariable> covered;

        if (string.IsNullOrEmpty(categoryName))
        {
            state = save.States.FirstOrDefault(s => Matches(s.Name, stateName));

            // Uncategorized states have no exclusion list. FRB1 instead drops variables that are
            // themselves states, so a state cannot set another state.
            covered = save.CustomVariables.Where(v => !IsStateVariable(v, save)).ToList();
        }
        else
        {
            var category = save.StateCategoryList
                .FirstOrDefault(c => Matches(c.Name, categoryName));

            if (category is null)
            {
                Warn(diagnostics, save.Name,
                    $"'{save.Name}' has no state category named '{categoryName}'.");
                return;
            }

            state = category.States.FirstOrDefault(s => Matches(s.Name, stateName));

            covered = save.CustomVariables
                .Where(v => !category.ExcludedVariables.Any(e => Matches(e, v.Name)))
                // A variable typed as its own category is excluded, or setting the state would
                // recurse into setting the state.
                .Where(v => !Matches(v.Type, category.Name))
                .ToList();
        }

        if (state is null)
        {
            Warn(diagnostics, save.Name,
                $"'{save.Name}' has no state named '{stateName}'" +
                (string.IsNullOrEmpty(categoryName) ? "." : $" in category '{categoryName}'."));
            return;
        }

        foreach (var variable in covered)
        {
            if (string.IsNullOrEmpty(variable.Name))
                continue;

            var instruction = state.InstructionSaves
                .FirstOrDefault(i => Matches(i.Member, variable.Name));

            // Instruction if the state names one, otherwise the variable's own authored default.
            // A variable with neither is left alone rather than zeroed.
            var valued = instruction is null
                ? variable
                : WithValue(variable, instruction.Value);

            GlueVariableApplier.ApplyOne(valued, element, objects, bag, save.Name, diagnostics);
        }
    }

    /// <summary>
    /// Whether this variable's declared type names a state rather than a value — either the
    /// uncategorized marker or one of the element's own category names.
    /// </summary>
    internal static bool IsStateVariable(CustomVariable variable, GlueElement save) =>
        Matches(variable.Type, "VariableState") || FindCategory(variable, save) is not null;

    /// <summary>The category a state-typed variable selects, or null when it is not one.</summary>
    /// <remarks>A nullable category type keeps its trailing <c>?</c> on disk, which is not part of
    /// the name.</remarks>
    internal static StateSaveCategory? FindCategory(CustomVariable variable, GlueElement save)
    {
        string? type = variable.Type?.TrimEnd('?');

        return string.IsNullOrEmpty(type)
            ? null
            : save.StateCategoryList.FirstOrDefault(c => Matches(c.Name, type));
    }

    /// <summary>
    /// A copy of the variable carrying the state's value instead of its own, so a state assignment
    /// reuses the whole variable pipeline — tunneling, overriding types and converters included.
    /// </summary>
    private static CustomVariable WithValue(CustomVariable variable, JsonElement value) => new()
    {
        Name = variable.Name,
        DefaultValue = value,
        SourceObject = variable.SourceObject,
        SourceObjectProperty = variable.SourceObjectProperty,
        Properties = variable.Properties,
    };

    private static bool Matches(string? a, string? b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static void Warn(List<GlueLoadDiagnostic> diagnostics, string? elementName, string message) =>
        diagnostics.Add(new GlueLoadDiagnostic(GlueDiagnosticSeverity.Warning, message, elementName));
}
