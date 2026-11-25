using System.Collections;
using System.Reflection;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.SaveModel;

/// <summary>
/// Gives every stored member of the save model a distinct non-default value by reflection, and compares two models member
/// by member, so a member added later is covered by every copy, round-trip and export test without being listed.
/// </summary>
public static class SaveModelFill
{
    private static bool IsScalar(Type type) => type == typeof(string) || type.IsValueType;

    public static IEnumerable<MemberInfo> StoredMembers(Type type) =>
        type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m is FieldInfo || m is PropertyInfo { CanRead: true });

    private static Type TypeOf(MemberInfo member) =>
        member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;

    private static object? Get(MemberInfo member, object target) =>
        member is FieldInfo field ? field.GetValue(target) : ((PropertyInfo)member).GetValue(target);

    private static void Set(MemberInfo member, object target, object value)
    {
        if (member is FieldInfo field) field.SetValue(target, value);
        else if (member is PropertyInfo { CanWrite: true } property) property.SetValue(target, value);
    }

    public static object CreateFilled(Type type, ref int counter)
    {
        var target = Activator.CreateInstance(type)!;
        if (target is ShapesSave shapes)
        {
            foreach (var shapeType in ShapeTypes) shapes.Add(CreateFilled(shapeType, ref counter));
            return target;
        }

        foreach (var member in StoredMembers(type)) SetNonDefault(target, member, ref counter);
        return target;
    }

    /// <summary>Every concrete shape type the save model declares.</summary>
    public static IEnumerable<Type> ShapeTypes =>
        typeof(ShapeSave).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(ShapeSave)) && !t.IsAbstract);

    /// <summary>Gives one member a non-default value, filled all the way down; a list gets one more element.</summary>
    public static void SetNonDefault(object target, MemberInfo member, ref int counter)
    {
        var memberType = TypeOf(member);
        if (Get(member, target) is IList list)
            list.Add(CreateFilled(memberType.GenericTypeArguments[0], ref counter));
        else if (IsScalar(memberType))
            Set(member, target, NonDefaultScalar(memberType, Get(member, target), ref counter));
        else
            Set(member, target, CreateFilled(memberType, ref counter));
    }

    private static object NonDefaultScalar(Type memberType, object? defaultValue, ref int counter)
    {
        var type = Nullable.GetUnderlyingType(memberType) ?? memberType;
        counter++;
        return type == typeof(string) ? $"set{counter}" :
            type == typeof(bool) ? !(bool)(defaultValue ?? false) :
            type.IsEnum ? Enum.GetValues(type).Cast<object>().Last() :
            Convert.ChangeType(counter, type);
    }

    public static void Compare(object? expected, object? actual, string path, List<string> differences)
    {
        if (expected is null || actual is null || IsScalar(expected.GetType()))
        {
            if (!Equals(expected, actual)) differences.Add($"{path}: expected {expected ?? "null"}, copy has {actual ?? "null"}");
            return;
        }

        if (ReferenceEquals(expected, actual)) differences.Add($"{path}: the copy shares the source's instance");
        if (expected.GetType() != actual.GetType() && expected is not IEnumerable) differences.Add($"{path}: copied as {actual.GetType().Name}");
        else if (expected is IEnumerable expectedItems)
        {
            var expectedList = expectedItems.Cast<object>().ToList();
            var actualList = ((IEnumerable)actual).Cast<object>().ToList();
            if (expectedList.Count != actualList.Count) differences.Add($"{path}: expected {expectedList.Count} items, copy has {actualList.Count}");
            for (int i = 0; i < Math.Min(expectedList.Count, actualList.Count); i++)
                Compare(expectedList[i], actualList[i], $"{path}[{expectedList[i].GetType().Name} {i}]", differences);
        }
        else
        {
            foreach (var member in StoredMembers(expected.GetType()))
                Compare(Get(member, expected), Get(member, actual), $"{path}.{member.Name}", differences);
        }
    }
}
