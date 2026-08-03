using System.Text.Json;
using RiftboundSample.Models;

namespace RiftboundSample.Systems;

/// <summary>
/// Loads dialogue data from JSON and manages progression through dialogue nodes.
/// </summary>
public class DialogueSystem
{
    private readonly Dictionary<string, DialogueNode> _nodes = [];
    private readonly List<string> _log = [];
    private DialogueNode? _current;

    public DialogueNode? Current => _current;
    public bool IsActive => _current != null;

    /// <summary>All lines displayed so far (speaker + text).</summary>
    public IReadOnlyList<string> Log => _log;

    public void LoadFromFile(string path)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        string json = File.ReadAllText(DataPath.Resolve(path));
        var nodes = JsonSerializer.Deserialize<List<DialogueNode>>(json, options) ?? [];
        foreach (var node in nodes)
            _nodes[node.Id] = node;
    }

    public void StartDialogue(string nodeId)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            _current = node;
            LogCurrent();
        }
    }

    /// <summary>
    /// Advances to the next node. For nodes with choices, use <see cref="SelectChoice"/> instead.
    /// Returns false if dialogue has ended.
    /// </summary>
    public bool Advance()
    {
        if (_current == null) return false;

        if (_current.Choices is { Count: > 0 })
            return true; // Must use SelectChoice for branching nodes

        if (_current.NextId == null)
        {
            _current = null;
            return false;
        }

        if (_nodes.TryGetValue(_current.NextId, out var next))
        {
            _current = next;
            LogCurrent();
            return true;
        }

        _current = null;
        return false;
    }

    public bool SelectChoice(int choiceIndex)
    {
        if (_current?.Choices == null || choiceIndex < 0 || choiceIndex >= _current.Choices.Count)
            return false;

        var choice = _current.Choices[choiceIndex];
        _log.Add($"  > {choice.Text}");

        if (_nodes.TryGetValue(choice.NextId, out var next))
        {
            _current = next;
            LogCurrent();
            return true;
        }

        _current = null;
        return false;
    }

    private void LogCurrent()
    {
        if (_current != null)
            _log.Add($"{_current.Speaker}: {_current.Text}");
    }
}
