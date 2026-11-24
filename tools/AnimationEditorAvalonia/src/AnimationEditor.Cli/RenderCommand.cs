using System.Globalization;
using AnimationEditor.Editing;
using AnimationEditor.Views.Export;
using AnimationEditor.Views.Services;

namespace AnimationEditor.Cli;

/// <summary>What <c>aecli render</c> produced: the files written, or why not. <see cref="UsageError"/> means the command line was malformed.</summary>
internal sealed record RenderOutcome(IReadOnlyList<string> Files, IReadOnlyList<(string Code, string Message)> Errors, string? UsageError = null)
{
    public bool Success => Errors.Count == 0 && UsageError is null;
}

/// <summary>Writes PNGs of a sequence at times, or of a chain at frames or times, using the editor's scene logic and frame drawing.</summary>
internal static class RenderCommand
{
    public const int MaxScale = 16;

    public static RenderOutcome Run(string file, IReadOnlyDictionary<string, string> values)
    {
        if (!values.TryGetValue("--out", out var outDir)) return Usage("render needs --out <dir>.");

        bool grid = values.ContainsKey("--grid"), shapes = values.ContainsKey("--shapes"), sheet = values.ContainsKey("--sheet");
        int scale = 4;
        if (values.TryGetValue("--scale", out var scaleText) && !(int.TryParse(scaleText, NumberStyles.None, CultureInfo.InvariantCulture, out scale) && scale is >= 1 and <= MaxScale))
            return Usage($"--scale must be a whole number from 1 to {MaxScale}.");

        bool transparent = true;
        uint background = 0;
        if (values.TryGetValue("--background", out var bg) && !TryParseBackground(bg, out transparent, out background))
            return Usage("--background must be 'transparent', a name (black, white, gray) or #RRGGBB.");

        StillBounds? bounds = null;
        if (values.TryGetValue("--bounds", out var boundsText) && !boundsText.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseBounds(boundsText, out var parsed)) return Usage("--bounds must be 'auto' or x,y,w,h with w and h above 0.");
            bounds = parsed;
        }

        IReadOnlyList<double>? times = null;
        if (values.TryGetValue("--at", out var atText))
        {
            if (!TryParseList(atText, s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d >= 0 && double.IsFinite(d) ? d : null, out times))
                return Usage("--at must be a comma list of times in seconds, 0 or more (0,2.15,4.2).");
        }
        IReadOnlyList<int>? frames = null;
        if (values.TryGetValue("--frame", out var frameText))
        {
            if (!TryParseList(frameText, s => int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var i) ? i : null, out frames))
                return Usage("--frame must be a comma list of zero-based frame indexes (0,1,2).");
        }

        values.TryGetValue("--sequence", out var name);
        if (name is null) values.TryGetValue("--chain", out name);
        if (!CliCommands.TryPass(values, out int pass, out var passError)) return Usage(passError!);

        var plan = RenderPlanner.Plan(new RenderRequest(file, name, times, frames, pass), out var error, out var usage);
        if (plan is null) return usage ? Usage(error!) : Failed(RenderPlanner.CodeOf(error!), error!);
        if (TextureErrors(plan) is { Count: > 0 } textureErrors) return new RenderOutcome([], textureErrors);

        StillImages images;
        try
        {
            var options = new StillRenderOptions(scale, transparent, background, grid, bounds, shapes);
            images = sheet
                ? StillImageRenderer.RenderSheet(plan.Frames, plan.Labels, plan.ResolveTexture, options)
                : StillImageRenderer.Render(plan.Frames, plan.ResolveTexture, options);
        }
        catch (InvalidOperationException ex)
        {
            return Failed(EditErrorCode.Unspecified, ex.Message);
        }

        try
        {
            Directory.CreateDirectory(outDir);
            var files = new List<string>();
            for (int i = 0; i < images.Pngs.Count; i++)
            {
                var path = Path.GetFullPath(Path.Combine(outDir, (sheet ? plan.Name + "_sheet" : plan.Stems[i]) + ".png"));
                File.WriteAllBytes(path, images.Pngs[i]);
                files.Add(path);
            }
            return new RenderOutcome(files, []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failed(EditErrorCode.Unspecified, $"Could not write to {outDir}: {ex.Message}");
        }
    }

    // The renderer leaves out a frame whose texture it cannot load, so what it would leave out is refused here.
    private static IReadOnlyList<(string Code, string Message)> TextureErrors(RenderPlan plan)
    {
        var errors = new List<(string, string)>();
        var layers = plan.Frames.SelectMany(f => f.Layers).ToList();
        foreach (var layer in layers.DistinctBy(l => plan.ResolveTexture(l.Frame), StringComparer.OrdinalIgnoreCase))
        {
            if (plan.ResolveTexture(layer.Frame) is not { } path) continue;
            string where = $"(frame {layer.Chain.Name}/{layer.Chain.Frames.IndexOf(layer.Frame)})", name = layer.Frame.TextureName;
            if (!File.Exists(path))
                errors.Add((nameof(EditErrorCode.TextureNotFound), $"Texture '{name}' not found {where}."));
            else if (SkiaFileDecoder.DecodeFile(path, out var reason) is { } bitmap)
                bitmap.Dispose();
            else
                errors.Add((nameof(EditErrorCode.TextureUndecodable), reason ?? $"Texture '{name}' could not be read as an image {where}."));
        }
        if (errors.Count == 0 && layers.All(l => plan.ResolveTexture(l.Frame) is null))
            errors.Add((nameof(EditErrorCode.Unspecified), "Nothing to draw: the frames shown have no texture."));
        return errors;
    }

    private static RenderOutcome Usage(string message) => new([], [], message);

    private static RenderOutcome Failed(EditErrorCode code, string message) => new([], [(code.ToString(), message)]);

    private static bool TryParseList<T>(string text, Func<string, T?> parse, out IReadOnlyList<T>? result) where T : struct
    {
        var list = new List<T>();
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries))
        {
            if (parse(part) is not { } value) { result = null; return false; }
            list.Add(value);
        }
        result = list;
        return list.Count > 0;
    }

    private static bool TryParseBackground(string text, out bool transparent, out uint argb)
    {
        transparent = false;
        argb = 0;
        switch (text.ToLowerInvariant())
        {
            case "transparent": transparent = true; return true;
            case "black": return true;
            case "white": argb = 0xFFFFFF; return true;
            case "gray": case "grey": argb = 0x808080; return true;
        }
        return text.Length == 7 && text[0] == '#' && uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out argb);
    }

    private static bool TryParseBounds(string text, out StillBounds bounds)
    {
        bounds = default;
        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4) return false;
        var n = new float[4];
        for (int i = 0; i < 4; i++)
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out n[i]) || !float.IsFinite(n[i])) return false;
        if (n[2] <= 0 || n[3] <= 0) return false;
        bounds = new StillBounds(n[0], n[1], n[2], n[3]);
        return true;
    }
}
