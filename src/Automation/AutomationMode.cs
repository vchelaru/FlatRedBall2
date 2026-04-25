#if DEBUG
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using FlatRedBall2.Input;
using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Automation;

internal class AutomationMode
{
    private readonly FlatRedBallService _engine;
    private readonly System.IO.TextWriter _output;
    // Single FIFO of every parsed command — including step. The reader thread enqueues; the
    // game thread drains. Keeping all command kinds in one queue is what makes a recorded
    // NDJSON file reproducible: a query sent after a step is guaranteed to observe the
    // post-step frame, regardless of how fast the reader pumps lines from the source.
    private readonly ConcurrentQueue<JsonElement> _commandQueue = new();
    private readonly Dictionary<string, Func<object>> _stateProviders = new();
    private readonly Dictionary<string, Action<double>> _valueSetters = new();
    // Frames remaining for an in-progress step command (count > 1 spreads across frames).
    // Game-thread only — no synchronization needed.
    private int _pendingStepCount;
    private bool _stepConsumedThisFrame;

    internal AutomationMode(FlatRedBallService engine, System.IO.TextWriter? output = null)
    {
        _engine = engine;
        _output = output ?? Console.Out;
    }

    internal void Start(System.IO.TextReader? input = null)
    {
        var reader = input ?? Console.In;
        var thread = new Thread(() => ReaderLoop(reader)) { IsBackground = true, Name = "AutomationMode.Reader" };
        thread.Start();
    }

    internal void ProcessLine(string line)
    {
        try
        {
            var doc = JsonDocument.Parse(line);
            var root = doc.RootElement.Clone();
            _commandQueue.Enqueue(root);
        }
        catch (JsonException ex)
        {
            WriteResponse(new { ok = false, error = $"JSON parse error: {ex.Message}" });
        }
    }

    /// <summary>
    /// Drains queued commands at the given frame, processing non-step commands inline and
    /// stopping when a step is encountered. Decrements one step from the in-progress count.
    /// Returns true if a frame should run this tick (a step was available); false if the
    /// queue is empty and no step is pending — in which case the engine should suppress draw.
    /// </summary>
    internal bool TryAdvanceFrame(long frame)
    {
        if (_pendingStepCount == 0)
        {
            while (_commandQueue.TryDequeue(out var cmd))
            {
                if (TryReadStep(cmd, out var count))
                {
                    _pendingStepCount = count;
                    break;
                }
                ProcessCommand(cmd, frame);
            }
        }

        if (_pendingStepCount > 0)
        {
            _pendingStepCount--;
            _stepConsumedThisFrame = true;
            return true;
        }
        return false;
    }

    private static bool TryReadStep(JsonElement cmd, out int count)
    {
        count = 0;
        if (!cmd.TryGetProperty("cmd", out var cmdProp) || cmdProp.GetString() != "step")
            return false;
        count = cmd.TryGetProperty("count", out var countProp) ? countProp.GetInt32() : 1;
        if (count < 1) count = 1;
        return true;
    }

    internal void FlushStepResponse(long frame)
    {
        if (_stepConsumedThisFrame)
        {
            _stepConsumedThisFrame = false;
            WriteResponse(new { ok = true, frame });
        }
    }

    internal void RegisterStateProvider(string name, Func<object> provider)
        => _stateProviders[name] = provider;

    internal void RegisterValueSetter(string entityName, string propName, Action<double> setter)
        => _valueSetters[$"{entityName}.{propName}"] = setter;

    private void ReaderLoop(System.IO.TextReader reader)
    {
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
                ProcessLine(line);
        }
    }

    private void ProcessCommand(JsonElement cmd, long frame)
    {
        if (!cmd.TryGetProperty("cmd", out var cmdProp))
        {
            WriteResponse(new { ok = false, frame, error = "missing 'cmd' field" });
            return;
        }

        var command = cmdProp.GetString();
        switch (command)
        {
            case "input":  ProcessInputCommand(cmd, frame);  break;
            case "query":  ProcessQueryCommand(cmd, frame);  break;
            case "set":    ProcessSetCommand(cmd, frame);    break;
            case "quit":
                try { _engine.Game.Exit(); }
                catch (InvalidOperationException) { }
                break;
            default:
                WriteResponse(new { ok = false, frame, error = $"unknown command: {command}" });
                break;
        }
    }

    private void ProcessInputCommand(JsonElement cmd, long frame)
    {
        if (!cmd.TryGetProperty("type", out var typeProp))
        {
            WriteResponse(new { ok = false, frame, error = "input command missing 'type'" });
            return;
        }

        var type = typeProp.GetString();
        switch (type)
        {
            case "key":
            {
                var keyStr  = cmd.TryGetProperty("key",  out var k) ? k.GetString() : null;
                var down    = cmd.TryGetProperty("down", out var d) && d.GetBoolean();
                if (keyStr != null && Enum.TryParse<Keys>(keyStr, out var key))
                    _engine.Input.InjectKey(key, down);
                else
                    WriteResponse(new { ok = false, frame, error = $"unknown key: {keyStr}" });
                break;
            }
            case "gamepad":
            {
                var player    = cmd.TryGetProperty("player", out var p) ? p.GetInt32() : 0;
                var buttonStr = cmd.TryGetProperty("button", out var b) ? b.GetString() : null;
                var down      = cmd.TryGetProperty("down",   out var d) && d.GetBoolean();
                if (buttonStr != null && Enum.TryParse<Buttons>(buttonStr, out var button))
                    _engine.Input.InjectGamepadButton(player, button, down);
                else
                    WriteResponse(new { ok = false, frame, error = $"unknown button: {buttonStr}" });
                break;
            }
            case "axis":
            {
                var player  = cmd.TryGetProperty("player", out var p) ? p.GetInt32() : 0;
                var axisStr = cmd.TryGetProperty("axis",   out var a) ? a.GetString() : null;
                var value   = cmd.TryGetProperty("value",  out var v) ? (float)v.GetDouble() : 0f;
                if (axisStr != null && Enum.TryParse<GamepadAxis>(axisStr, out var axis))
                    _engine.Input.InjectGamepadAxis(player, axis, value);
                else
                    WriteResponse(new { ok = false, frame, error = $"unknown axis: {axisStr}" });
                break;
            }
            default:
                WriteResponse(new { ok = false, frame, error = $"unknown input type: {type}" });
                break;
        }
    }

    private void ProcessQueryCommand(JsonElement cmd, long frame)
    {
        var target = cmd.TryGetProperty("target", out var t) ? t.GetString() : null;

        switch (target)
        {
            case "screen":
            {
                string screenName;
                try { screenName = _engine.CurrentScreen.GetType().Name; }
                catch { screenName = "unknown"; }
                WriteResponse(new { ok = true, frame, result = new { screen = screenName } });
                break;
            }
            case "entities":
            {
                var allResults = new Dictionary<string, object>();
                // Reflection-based: every factory's instances are enumerated automatically.
                foreach (var f in _engine.EnumerateFactories())
                    allResults[f.EntityType.Name] = SnapshotInstances(f.EntityInstances);
                // Registered providers layered on top — they win on name collisions and add
                // derived state (Score, Lives, etc.) that isn't a plain entity property.
                foreach (var kvp in _stateProviders)
                    allResults[kvp.Key] = kvp.Value();
                WriteResponse(new { ok = true, frame, result = allResults });
                break;
            }
            default:
            {
                if (target != null && _stateProviders.TryGetValue(target, out var provider))
                {
                    WriteResponse(new { ok = true, frame, result = provider() });
                }
                else if (target != null && TryFindFactoryByTypeName(target, out var factory))
                {
                    WriteResponse(new { ok = true, frame, result = SnapshotInstances(factory!.EntityInstances) });
                }
                else
                {
                    WriteResponse(new { ok = false, frame, error = $"unknown query target: {target}" });
                }
                break;
            }
        }
    }

    private bool TryFindFactoryByTypeName(string name, out IFactory? factory)
    {
        foreach (var f in _engine.EnumerateFactories())
        {
            if (f.EntityType.Name == name) { factory = f; return true; }
        }
        factory = null;
        return false;
    }

    private void ProcessSetCommand(JsonElement cmd, long frame)
    {
        var entity = cmd.TryGetProperty("entity", out var e) ? e.GetString() : null;
        var prop   = cmd.TryGetProperty("prop",   out var p) ? p.GetString() : null;
        var value  = cmd.TryGetProperty("value",  out var v) ? v.GetDouble() : 0.0;

        var key = $"{entity}.{prop}";
        if (_valueSetters.TryGetValue(key, out var setter))
        {
            setter(value);
            WriteResponse(new { ok = true, frame });
            return;
        }

        // Reflection fallback: locate factory by entity type name, set property on first instance.
        string? error = $"no setter registered for {key}";
        if (entity != null && prop != null && TryReflectSet(entity, prop, value, out error))
        {
            WriteResponse(new { ok = true, frame });
        }
        else
        {
            WriteResponse(new { ok = false, frame, error });
        }
    }

    private bool TryReflectSet(string entityName, string propName, double value, out string? error)
    {
        if (!TryFindFactoryByTypeName(entityName, out var factory))
        {
            error = $"no factory or setter for {entityName}";
            return false;
        }
        if (factory!.EntityInstances.Count == 0)
        {
            error = $"no live instances of {entityName}";
            return false;
        }
        var inst = factory.EntityInstances[0];
        var p = inst.GetType().GetProperty(propName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (p == null || !p.CanWrite)
        {
            error = $"{entityName}.{propName} is not a writable public property";
            return false;
        }
        var target = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        try
        {
            object? converted = target.IsEnum
                ? Enum.ToObject(target, (long)value)
                : Convert.ChangeType(value, target);
            p.SetValue(inst, converted);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"failed to set {entityName}.{propName}: {ex.Message}";
            return false;
        }
    }

    // --- Reflection-based property snapshotting ---

    private static readonly HashSet<Type> AllowedPrimitives = new()
    {
        typeof(bool),
        typeof(byte), typeof(sbyte),
        typeof(short), typeof(ushort),
        typeof(int), typeof(uint),
        typeof(long), typeof(ulong),
        typeof(float), typeof(double), typeof(decimal),
        typeof(string),
        typeof(Microsoft.Xna.Framework.Vector2),
        typeof(Microsoft.Xna.Framework.Vector3),
    };

    private static List<Dictionary<string, object?>> SnapshotInstances(IReadOnlyList<Entity> instances)
    {
        var list = new List<Dictionary<string, object?>>(instances.Count);
        foreach (var e in instances) list.Add(SnapshotEntity(e));
        return list;
    }

    private static Dictionary<string, object?> SnapshotEntity(Entity entity)
    {
        var dict = new Dictionary<string, object?>();
        var t = entity.GetType();
        foreach (var prop in t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (!prop.CanRead) continue;
            if (prop.GetIndexParameters().Length > 0) continue;
            var pt = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            if (!IsAllowed(pt)) continue;
            try
            {
                var v = prop.GetValue(entity);
                dict[prop.Name] = ConvertForOutput(v);
            }
            catch
            {
                // Ignore properties that throw during read — defensive against accidental
                // null-deref or "accessed before initialized" guards.
            }
        }
        return dict;
    }

    private static bool IsAllowed(Type t)
    {
        if (t.IsEnum) return true;
        if (AllowedPrimitives.Contains(t)) return true;
        if (t == typeof(Microsoft.Xna.Framework.Color)) return true;
        return false;
    }

    private static object? ConvertForOutput(object? v)
    {
        if (v == null) return null;
        var t = v.GetType();
        if (t.IsEnum) return v.ToString();
        if (t == typeof(Microsoft.Xna.Framework.Color))
        {
            var c = (Microsoft.Xna.Framework.Color)v;
            return new { c.R, c.G, c.B, c.A };
        }
        return v;
    }

    private void WriteResponse(object response)
    {
        var json = JsonSerializer.Serialize(response);
        lock (_output)
        {
            _output.WriteLine(json);
            _output.Flush();
        }
    }
}
#endif
