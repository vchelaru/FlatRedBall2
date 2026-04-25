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
    private readonly ConcurrentQueue<JsonElement> _commandQueue = new();
    private readonly Dictionary<string, Func<object>> _stateProviders = new();
    private readonly Dictionary<string, Action<double>> _valueSetters = new();
    private int _stepsRemaining;
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

            if (root.TryGetProperty("cmd", out var cmdProp) && cmdProp.GetString() == "step")
            {
                int count = 1;
                if (root.TryGetProperty("count", out var countProp))
                    count = countProp.GetInt32();
                Interlocked.Add(ref _stepsRemaining, count);
            }
            else
            {
                _commandQueue.Enqueue(root);
            }
        }
        catch (JsonException ex)
        {
            WriteResponse(new { ok = false, error = $"JSON parse error: {ex.Message}" });
        }
    }

    // Returns true and marks step consumed if a step was available.
    internal bool ConsumeStep()
    {
        while (true)
        {
            int current = _stepsRemaining;
            if (current <= 0) return false;
            if (Interlocked.CompareExchange(ref _stepsRemaining, current - 1, current) == current)
            {
                _stepConsumedThisFrame = true;
                return true;
            }
        }
    }

    internal void ProcessQueuedCommands(long frame)
    {
        while (_commandQueue.TryDequeue(out var cmd))
            ProcessCommand(cmd, frame);
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
                else
                {
                    WriteResponse(new { ok = false, frame, error = $"unknown query target: {target}" });
                }
                break;
            }
        }
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
        }
        else
        {
            WriteResponse(new { ok = false, frame, error = $"no setter registered for {key}" });
        }
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
