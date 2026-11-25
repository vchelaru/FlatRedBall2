using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// Every public member of the save model survives XML, JSON and XML to JSON to XML. The document is filled by reflection,
/// so a field added to the model without a writer and a reader in both dialects fails here.
/// </summary>
public class EveryFieldRoundTripTests
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.Instance;
    private int _next;

    // Lists get two items, numbers are all different, and every value differs from the member's default.
    private object Filled(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        _next++;
        if (type == typeof(string)) return "text" + _next;
        if (type == typeof(float)) return -_next - 0.1f;
        if (type == typeof(int)) return -_next;
        if (type.IsEnum) return Enum.GetValues(type).Cast<object>().Last();
        if (type == typeof(ShapesSave))
        {
            var shapes = new ShapesSave();
            foreach (var kind in new[] { typeof(AARectSave), typeof(PolygonSave), typeof(CircleSave) }) shapes.Add(Filled(kind));
            return shapes;
        }

        var value = Activator.CreateInstance(type)!;
        if (value is IList list)
        {
            for (int i = 0; i < 2; i++) list.Add(Filled(type.GetGenericArguments()[0]));
            return value;
        }
        foreach (var field in type.GetFields(Members))
            field.SetValue(value, field.FieldType == typeof(bool) ? !(bool)field.GetValue(value)! : Filled(field.FieldType));
        foreach (var property in type.GetProperties(Members).Where(p => p.Name != nameof(AnimationChainListSave.FileName)))
        {
            if (property.CanWrite) property.SetValue(value, Filled(property.PropertyType));
            else if (property.GetValue(value) is List<AnimationFrameEvent> events)
            {
                events.Add((AnimationFrameEvent)Filled(typeof(AnimationFrameEvent)));
                events.Add(new AnimationFrameEvent { Name = "no data" });
            }
        }
        return value;
    }

    private static string Dump(object? value) => value switch
    {
        null => "null",
        string text => $"'{text}'",
        bool flag => flag.ToString(),
        float number => number.ToString("R", CultureInfo.InvariantCulture),
        Enum or int => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        IEnumerable items => "[" + string.Join(",\n", items.Cast<object>().Select(Dump)) + "]",
        _ => value.GetType().Name + "{" + string.Join(", ", value.GetType().GetFields(Members).Select(f => $"{f.Name}={Dump(f.GetValue(value))}")
            .Concat(value.GetType().GetProperties(Members).Where(p => p.Name != nameof(AnimationChainListSave.FileName))
                .Select(p => $"{p.Name}={Dump(p.GetValue(value))}"))) + "}",
    };

    [Theory]
    [InlineData("")]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    [InlineData("sv-SE")]
    public void EveryField_BothDialects_RoundTripsInAnyCulture(string culture)
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var document = (AnimationChainListSave)Filled(typeof(AnimationChainListSave));
            string expected = Dump(document), xml = document.ToXmlString();

            Dump(AnimationChainListSave.FromString(xml)).ShouldBe(expected);
            Dump(AnimationChainListSave.FromString(document.ToJsonString())).ShouldBe(expected);
            AnimationChainListSave.FromString(AnimationChainListSave.FromString(xml).ToJsonString()).ToXmlString().ShouldBe(xml);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}
