using System;
using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// Resolves <c>BaseScreen</c> / <c>BaseEntity</c> by flattening each derived element into the union
/// of its inheritance chain.
/// </summary>
/// <remarks>
/// FRB1 expresses inheritance as C# class inheritance, so the compiler does this work. With one CLR
/// type per element kind there is no compiler to do it, and a derived file on its own is badly
/// incomplete — DoorsDemo's start-up screen declares four objects and inherits nine.
/// <para>Flattening happens at load, where the whole project is in hand. A loaded element has no
/// way to reach its base otherwise.</para>
/// </remarks>
internal static class GlueInheritanceResolver
{
    /// <summary>Flattens every derived element in the project, in place.</summary>
    internal static void Flatten(GlueProjectSave project, List<GlueLoadDiagnostic> diagnostics)
    {
        var byName = new Dictionary<string, GlueElement>(StringComparer.OrdinalIgnoreCase);

        foreach (GlueElement element in project.Screens.Concat<GlueElement>(project.Entities))
        {
            if (!string.IsNullOrEmpty(element.Name))
                byName[element.Name] = element;
        }

        var flattened = new HashSet<GlueElement>();
        var visiting = new HashSet<GlueElement>();

        foreach (var element in byName.Values.ToList())
            FlattenElement(element, byName, flattened, visiting, diagnostics);
    }

    private static void FlattenElement(
        GlueElement element,
        Dictionary<string, GlueElement> byName,
        HashSet<GlueElement> flattened,
        HashSet<GlueElement> visiting,
        List<GlueLoadDiagnostic> diagnostics)
    {
        if (flattened.Contains(element))
            return;

        string? baseName = element.BaseElement;

        if (string.IsNullOrEmpty(baseName))
        {
            flattened.Add(element);
            return;
        }

        if (!visiting.Add(element))
        {
            diagnostics.Add(new GlueLoadDiagnostic(
                GlueDiagnosticSeverity.Error,
                $"'{element.Name}' is part of an inheritance cycle through '{baseName}'; " +
                "the chain was not resolved.",
                element.Name));
            flattened.Add(element);
            return;
        }

        if (!byName.TryGetValue(baseName, out GlueElement? baseElement))
        {
            // A base that names no element is either a genuinely missing file or — for 12 entities
            // across FRB1 — an engine type, which FRB1 expresses as `class Foo : Sprite`. One shared
            // CLR type per element kind cannot express that, so it is reported rather than resolved.
            diagnostics.Add(new GlueLoadDiagnostic(
                GlueDiagnosticSeverity.Warning,
                baseName.Contains('\\')
                    ? $"'{element.Name}' derives from '{baseName}', which is not in this project; " +
                      "only its own objects were kept."
                    : $"'{element.Name}' derives from the engine type '{baseName}' rather than from " +
                      "another element, which this loader cannot express; only its own objects were kept.",
                element.Name));

            visiting.Remove(element);
            flattened.Add(element);
            return;
        }

        FlattenElement(baseElement, byName, flattened, visiting, diagnostics);
        visiting.Remove(element);

        MergeInto(element, baseElement);
        flattened.Add(element);
    }

    /// <summary>
    /// Layers <paramref name="derived"/> over <paramref name="baseElement"/>.
    /// </summary>
    /// <remarks>
    /// A derived entry replaces its base counterpart <em>wholesale</em> rather than being overlaid
    /// on it. That is not a simplification: Glue strips a redeclared entry from the derived file
    /// unless it differs from the base, so an entry that survives to disk carries its complete
    /// state. DoorsDemo's <c>CloudCollision</c> appears in both files byte-identical apart from the
    /// derived flags.
    /// <para>Inherited entries are shared by reference, not copied. Nothing downstream mutates a
    /// save — the builder reads it to construct new objects — so two screens sharing a base share
    /// the data safely.</para>
    /// </remarks>
    private static void MergeInto(GlueElement derived, GlueElement baseElement)
    {
        derived.NamedObjects = Merge(
            baseElement.NamedObjects, derived.NamedObjects, o => o.InstanceName, keepBase: null);

        // A derived variable with no authored value is Glue's way of writing "inherit": it nulls
        // DefaultValue when it copies one down. Letting the stub win would blank the base's value.
        derived.CustomVariables = Merge(
            baseElement.CustomVariables, derived.CustomVariables, v => v.Name,
            keepBase: v => v.DefinedByBase && !v.HasAuthoredValue);

        derived.ReferencedFiles = Merge(
            baseElement.ReferencedFiles, derived.ReferencedFiles, f => f.Name, keepBase: null);

        derived.Properties = Merge(
            baseElement.Properties, derived.Properties, p => p.Name, keepBase: null);

        // States are prepended base-first, matching the order FRB1 exposes them in.
        derived.States = Merge(baseElement.States, derived.States, s => s.Name, keepBase: null);
        derived.StateCategoryList = Merge(
            baseElement.StateCategoryList, derived.StateCategoryList, c => c.Name, keepBase: null);
    }

    /// <summary>
    /// Base entries in their original order, each replaced by the derived entry of the same name,
    /// followed by whatever the derived element adds.
    /// </summary>
    /// <remarks>
    /// <c>keepBase</c> is given the derived entry and answers whether it is a stub that should leave
    /// the base's entry in place — the "inherit rather than override" case.
    /// </remarks>
    private static List<T> Merge<T>(
        List<T> baseItems, List<T> derivedItems, Func<T, string?> keySelector, Func<T, bool>? keepBase)
    {
        var byKey = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in derivedItems)
        {
            string? key = keySelector(item);
            if (!string.IsNullOrEmpty(key))
                byKey[key] = item;
        }

        var merged = new List<T>(baseItems.Count + derivedItems.Count);
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var baseItem in baseItems)
        {
            string? key = keySelector(baseItem);

            if (!string.IsNullOrEmpty(key) && byKey.TryGetValue(key, out T? derivedItem))
            {
                consumed.Add(key);
                merged.Add(keepBase?.Invoke(derivedItem) == true ? baseItem : derivedItem);
            }
            else
            {
                merged.Add(baseItem);
            }
        }

        foreach (var item in derivedItems)
        {
            string? key = keySelector(item);
            if (string.IsNullOrEmpty(key) || !consumed.Contains(key))
                merged.Add(item);
        }

        return merged;
    }
}
