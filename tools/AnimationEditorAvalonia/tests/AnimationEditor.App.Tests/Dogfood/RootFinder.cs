using System.Reflection;

namespace AnimationEditor.App.Tests.Dogfood;

internal static class RootFinder
{
    public static string? FindPath(object target, int maxNodes = 8_000_000)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var parent = new Dictionary<object, (object? From, string Edge)>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<object>();
        void Enqueue(object o, object? from, string edge)
        {
            if (o is string || o is Pointer || o.GetType().IsPrimitive || o is Type || o is Assembly || o is Module || o is MemberInfo) return;
            if (!visited.Add(o)) return;
            parent[o] = (from, edge);
            queue.Enqueue(o);
        }
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); } catch { continue; }
            foreach (var t in types)
            {
                if (t.ContainsGenericParameters) continue;
                FieldInfo[] fields;
                try { fields = t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); } catch { continue; }
                foreach (var f in fields)
                {
                    if (f.FieldType.IsPrimitive || f.IsLiteral || f.FieldType.IsPointer) continue;
                    object? v;
                    try { v = f.GetValue(null); } catch { continue; }
                    if (v == null) continue;
                    string edge = $"[static {t.FullName}.{f.Name}]";
                    if (v.GetType().IsValueType) { Walk(v, null, edge, Enqueue); continue; }
                    Enqueue(v, null, edge);
                }
            }
        }
        int n = 0;
        while (queue.Count > 0 && n++ < maxNodes)
        {
            var o = queue.Dequeue();
            if (ReferenceEquals(o, target))
            {
                var lines = new List<string>();
                object? cur = o;
                while (cur != null)
                {
                    var (from, edge) = parent[cur];
                    lines.Add($"{edge} -> {cur.GetType().FullName}");
                    cur = from;
                }
                lines.Reverse();
                return string.Join("\n", lines);
            }
            Walk(o, o, null, Enqueue);
        }
        return null;
    }

    private static void Walk(object o, object? from, string? rootEdge, Action<object, object?, string> enqueue)
    {
        var t = o.GetType();
        if (o is Array arr)
        {
            if (t.GetElementType()!.IsPrimitive) return;
            int i = 0;
            foreach (var e in arr)
            {
                if (e != null)
                {
                    if (e.GetType().IsValueType) Walk(e, from, rootEdge, enqueue);
                    else enqueue(e, from, rootEdge ?? $"[{i}]");
                }
                i++;
            }
            return;
        }
        for (var ty = t; ty != null; ty = ty.BaseType)
        {
            foreach (var f in ty.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (f.FieldType.IsPrimitive || f.FieldType.IsPointer) continue;
                object? v;
                try { v = f.GetValue(o); } catch { continue; }
                if (v == null) continue;
                if (v.GetType().IsValueType) { Walk(v, from, rootEdge ?? $".{ty.Name}.{f.Name}", enqueue); continue; }
                enqueue(v, from, rootEdge ?? $".{ty.Name}.{f.Name}");
            }
        }
    }
}
