using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// Applies an element's <see cref="CustomVariable"/> values, and reads them back.
/// </summary>
/// <remarks>
/// A variable reaches one of three destinations, and which one is decided per variable:
/// a member of another object when it tunnels, a real property on the element when one exists by
/// that name, and otherwise a name/value bag. Reads resolve the same three, in the same order, so
/// <c>Get&lt;T&gt;("X")</c> and the element's own <c>X</c> can never disagree.
/// </remarks>
internal static class GlueVariableApplier
{
    /// <summary>
    /// Applies every variable that carries an authored value, in declaration order.
    /// </summary>
    /// <remarks>
    /// Order is load-bearing twice over. Across lists, FRB1 assigns a NamedObject's instructions
    /// first and the element's variables second, so a variable overrides an instruction on the same
    /// member. Within the list, a later variable overrides an earlier one — a real project relies on
    /// it, so the list must never be sorted.
    /// </remarks>
    internal static void Apply(
        GlueElement save,
        object element,
        IReadOnlyDictionary<string, object> objects,
        Dictionary<string, JsonElement> bag,
        List<GlueLoadDiagnostic> diagnostics)
    {
        foreach (var variable in save.CustomVariables)
        {
            if (string.IsNullOrEmpty(variable.Name) || !variable.HasAuthoredValue)
                continue;

            // A variable whose declared type names a state selects that state rather than holding a
            // value. It applies here, in list order, so a later variable can still override what the
            // state assigned — which a real project relies on.
            if (GlueStateApplier.IsStateVariable(variable, save))
            {
                string? stateName = variable.DefaultValue.ValueKind == JsonValueKind.String
                    ? variable.DefaultValue.GetString()
                    : null;

                if (!string.IsNullOrEmpty(stateName))
                {
                    GlueStateApplier.Apply(
                        save, GlueStateApplier.FindCategory(variable, save)?.Name, stateName,
                        element, objects, bag, diagnostics);
                }

                continue;
            }

            // A movement slot names a CSV row rather than carrying a value.
            if (TryApplyMovementSlot(variable, element, save, diagnostics))
                continue;

            ApplyOne(variable, element, objects, bag, save.Name, diagnostics);
        }
    }

    /// <summary>
    /// Fills a platformer movement slot from the CSV row its value names.
    /// </summary>
    /// <remarks>
    /// Unlike top-down, which takes the first row, a platformer names three rows explicitly through
    /// <c>GroundMovement</c>, <c>AirMovement</c> and <c>AfterDoubleJump</c>. A slot left unset
    /// deliberately falls back to air movement, which is what FRB1 does.
    /// </remarks>
    private static bool TryApplyMovementSlot(
        CustomVariable variable,
        object element,
        GlueElement save,
        List<GlueLoadDiagnostic> diagnostics)
    {
        if (variable.Name is not ("GroundMovement" or "AirMovement" or "AfterDoubleJump"))
            return false;

        if (element is not GlueEntity entity)
            return false;

        string? reference = variable.DefaultValue.ValueKind == JsonValueKind.String
            ? variable.DefaultValue.GetString()
            : null;

        if (!GlueMovementValues.TryParseRowReference(reference, out string row, out string file))
            return true;

        string? csv = entity.Content?.GetText(
            System.IO.Path.GetFileNameWithoutExtension(file));

        if (csv is null)
        {
            Warn(diagnostics, save.Name,
                $"'{variable.Name}' reads row '{row}' from '{file}', which was not loaded.");
            return true;
        }

        var values = GlueMovementValues.ReadPlatformer(csv);

        if (!values.TryGetValue(row, out var movement))
        {
            Warn(diagnostics, save.Name,
                $"'{variable.Name}' names row '{row}', which is not in '{file}'.");
            return true;
        }

        switch (variable.Name)
        {
            case "GroundMovement": entity.Platformer.GroundMovement = movement; break;
            case "AirMovement": entity.Platformer.AirMovement = movement; break;
            case "AfterDoubleJump": entity.Platformer.AfterDoubleJump = movement; break;
        }

        return true;
    }

    /// <summary>Applies one variable to whichever of the three destinations it resolves to.</summary>
    internal static void ApplyOne(
        CustomVariable variable,
        object element,
        IReadOnlyDictionary<string, object> objects,
        Dictionary<string, JsonElement> bag,
        string? elementName,
        List<GlueLoadDiagnostic> diagnostics)
    {
        if (string.IsNullOrEmpty(variable.Name) || !variable.HasAuthoredValue)
            return;

        if (IsUnsetNumericOrBool(variable))
            return;

        if (variable.IsShared)
        {
            Warn(diagnostics, elementName,
                $"'{variable.Name}' is shared (static in Glue), which has no data-driven " +
                "equivalent; its value was not applied.");
            return;
        }

        if (variable.IsTunneling)
            ApplyTunneled(variable, objects, elementName, diagnostics);
        else if (!TryWriteTo(element, variable, elementName, diagnostics))
            bag[variable.Name] = variable.DefaultValue;
    }

    /// <summary>
    /// Reads a variable by name, resolving through the same three destinations <see cref="Apply"/>
    /// writes to. The requested <typeparamref name="T"/> drives the read — a variable's declared
    /// type is frequently not a CLR type at all.
    /// </summary>
    internal static T? Read<T>(
        string name,
        List<CustomVariable>? variables,
        object element,
        IReadOnlyDictionary<string, object> objects,
        IReadOnlyDictionary<string, JsonElement> bag)
    {
        var variable = FindVariable(variables, name);

        if (variable?.IsTunneling == true &&
            objects.TryGetValue(variable.SourceObject!, out object? source))
        {
            var tunneled = GlueMemberWriter.FindProperty(
                source, GlueMemberWriter.ResolveMemberName(variable.SourceObjectProperty!));

            if (tunneled?.CanRead == true)
                return Coerce<T>(tunneled.GetValue(source));
        }

        if (variable is null || !variable.IsTunneling)
        {
            var property = GlueMemberWriter.FindProperty(
                element, GlueMemberWriter.ResolveMemberName(name));

            if (property?.CanRead == true)
                return Coerce<T>(property.GetValue(element));
        }

        return bag.TryGetValue(name, out var raw) ? PropertySaveExtensions.Decode<T>(raw) : default;
    }

    private static CustomVariable? FindVariable(List<CustomVariable>? variables, string name)
    {
        if (variables is null)
            return null;

        for (int i = 0; i < variables.Count; i++)
        {
            if (string.Equals(variables[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return variables[i];
        }

        return null;
    }

    private static void ApplyTunneled(
        CustomVariable variable,
        IReadOnlyDictionary<string, object> objects,
        string? elementName,
        List<GlueLoadDiagnostic> diagnostics)
    {
        if (!objects.TryGetValue(variable.SourceObject!, out object? source))
        {
            // Common and not an error: the source object's type belongs to a later phase, so it was
            // never built. The variable is simply not applied.
            Warn(diagnostics, elementName,
                $"'{variable.Name}' forwards to '{variable.SourceObject}', which this build did " +
                "not create; the value was skipped.");
            return;
        }

        WriteMember(source, variable, variable.SourceObjectProperty!,
            $"{variable.SourceObject}.{variable.SourceObjectProperty}", elementName, diagnostics);
    }

    private static bool TryWriteTo(
        object element, CustomVariable variable, string? elementName, List<GlueLoadDiagnostic> diagnostics)
    {
        string memberName = GlueMemberWriter.ResolveMemberName(variable.Name!);
        var property = GlueMemberWriter.FindProperty(element, memberName);

        if (property is null || !property.CanWrite)
            return false;

        WriteMember(element, variable, variable.Name!, memberName, elementName, diagnostics);
        return true;
    }

    private static void WriteMember(
        object target,
        CustomVariable variable,
        string glueMemberName,
        string describedAs,
        string? elementName,
        List<GlueLoadDiagnostic> diagnostics)
    {
        var property = GlueMemberWriter.FindProperty(
            target, GlueMemberWriter.ResolveMemberName(glueMemberName));

        if (property is null || !property.CanWrite)
        {
            Warn(diagnostics, elementName,
                $"'{variable.Name}' has no writable '{describedAs}'; the value was skipped.");
            return;
        }

        if (!GlueValueConverter.TryConvertVariable(
                variable.DefaultValue, variable.OverridingPropertyType, variable.TypeConverter,
                property.PropertyType, out object? converted))
        {
            Warn(diagnostics, elementName,
                $"'{variable.Name}' could not take the authored value '{variable.DefaultValue}' " +
                $"as {property.PropertyType.Name}; the default was kept.");
            return;
        }

        property.SetValue(target, converted);
    }

    /// <summary>
    /// Glue writes an empty string for a numeric or boolean variable that was never given a value,
    /// and skips the assignment rather than parsing it. Reading it as a number would silently zero
    /// a real value.
    /// </summary>
    private static bool IsUnsetNumericOrBool(CustomVariable variable) =>
        variable.DefaultValue.ValueKind == JsonValueKind.String &&
        variable.DefaultValue.GetString()?.Length == 0 &&
        variable.Type is "bool" or "int" or "float" or "long" or "byte" or "double" or "decimal";

    private static T? Coerce<T>(object? value)
    {
        if (value is T typed)
            return typed;

        if (value is null)
            return default;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
        {
            return default;
        }
    }

    private static void Warn(List<GlueLoadDiagnostic> diagnostics, string? elementName, string message) =>
        diagnostics.Add(new GlueLoadDiagnostic(GlueDiagnosticSeverity.Warning, message, elementName));
}
