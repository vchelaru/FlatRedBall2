using System.Globalization;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>Channels to change on a frame's color; a null channel is left as it is.</summary>
public readonly record struct FrameColorEdit(int? Red = null, int? Green = null, int? Blue = null, int? Alpha = null, ColorOperation? Operation = null);

/// <summary>
/// Sets chain flags, frame properties and the file's time unit (#1811). Never touches selection; each
/// change is one undo step. A value already in place is a successful no-op with no
/// <see cref="EditResult.ChangedItems"/>, so a no-change batch leaves the file byte-identical. Everything on a
/// locked chain except <see cref="SetChainLocked"/> fails with <see cref="EditErrorCode.Locked"/>.
/// <see cref="EditResult.ChangedItems"/> holds the address of the chain or frame that changed.
/// </summary>
public static class PropertyEdit
{
    // ---- chain flags ----

    public static EditResult SetChainLoop(IAppCommands commands, AnimationChainSave chain, bool value) =>
        SetChainFlag(chain, chain.Loop, value, () => commands.SetChainsLoop([chain], value));

    public static EditResult SetChainPingPong(IAppCommands commands, AnimationChainSave chain, bool value) =>
        SetChainFlag(chain, chain.PingPong, value, () => commands.SetChainsPingPong([chain], value));

    public static EditResult SetChainReverse(IAppCommands commands, AnimationChainSave chain, bool value) =>
        SetChainFlag(chain, chain.Reverse, value, () => commands.SetChainsReverse([chain], value));

    /// <summary>Locks or unlocks a chain. Works on a locked chain, since unlocking it is the way out.</summary>
    public static EditResult SetChainLocked(IAppCommands commands, AnimationChainSave chain, bool value)
    {
        if (chain.IsLocked == value) return Ok();
        commands.SetChainLocked(chain, value);
        return Ok(chain.Name);
    }

    private static EditResult SetChainFlag(AnimationChainSave chain, bool current, bool value, Action apply)
    {
        if (chain.IsLocked) return StructureEdit.LockedFail(chain);
        if (current == value) return Ok();
        apply();
        return Ok(chain.Name);
    }

    // ---- every frame of a chain ----

    /// <summary>
    /// Sets the sum of a chain's frame lengths (one forward pass, whatever its ping-pong flag) in the file's own time unit,
    /// keeping the ratios between frames or, with <paramref name="even"/>, giving every frame the same length. Each
    /// resulting frame length must be in <see cref="SetFrameLength"/>'s range.
    /// </summary>
    public static EditResult SetChainLength(
        IProjectManager projectManager, IAppCommands commands, AnimationChainSave chain, float length, bool even)
    {
        if (chain.IsLocked) return StructureEdit.LockedFail(chain);
        if (chain.Frames.Count == 0)
            return Fail(EditErrorCode.InvalidValue, $"Chain '{chain.Name}' has no frames, so it has no length to set.");

        float total = chain.Frames.Sum(f => f.FrameLength);
        if (!even && total <= 0)
            return Fail(EditErrorCode.InvalidValue,
                $"The frame lengths of chain '{chain.Name}' add up to 0, so there are no ratios to keep; use mode=even.");

        // The scaler runs on stand-ins first, so an out-of-range frame fails before anything changes.
        var scaled = chain.Frames.Select(f => new AnimationFrameSave { FrameLength = f.FrameLength }).ToList();
        if (even) FrameTimeScaler.ApplySetAllSame(scaled, length);
        else FrameTimeScaler.ApplyKeepProportional(scaled, length);

        var unit = FrameLengthUnit.Of(projectManager.AnimationChainListSave);
        for (int i = 0; i < scaled.Count; i++)
            if (CheckLength(unit, scaled[i].FrameLength, $"{chain.Name}/{i}") is { } bad) return bad;

        // Scaling leaves float noise, so a total this close is the same total, not an edit.
        bool unchanged = even
            ? chain.Frames.All(f => f.FrameLength == scaled[0].FrameLength)
            : Math.Abs(total - length) <= length * 1e-6f;
        if (unchanged) return Ok();

        if (even) commands.ScaleFrameTimesSetAllSame(chain, length);
        else commands.ScaleFrameTimesProportional(chain, length);
        return Ok(chain.Name);
    }

    /// <summary>
    /// Sets every frame's relative Y so the bottom of the sprite as displayed sits at y = 0; X is left alone. A frame whose region
    /// size is unknown fails rather than being skipped (the editor skips it).
    /// </summary>
    public static EditResult JustifyOffsetsBottom(IAppCommands commands, AnimationChainSave chain)
    {
        if (chain.IsLocked) return StructureEdit.LockedFail(chain);
        foreach (var frame in chain.Frames)
            if (CheckRegionSize(commands, chain, frame) is { } bad) return bad;

        var before = chain.Frames.Select(f => f.RelativeY).ToList();
        commands.AdjustOffsetsJustifyBottom(chain);
        return chain.Frames.Select(f => f.RelativeY).SequenceEqual(before) ? Ok() : Ok(chain.Name);
    }

    /// <summary>Why the pixel size of a frame's region cannot be used, or null when it can.</summary>
    internal static EditResult? CheckRegionSize(IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame)
    {
        if (FrameRegion.IsUnset(frame, commands.GetTextureSize))
            return Fail(EditErrorCode.UnsupportedFile,
                $"The size of frame {Address(chain, frame)} is unknown: it shows its whole texture, whose size could not be read "
                + $"({(string.IsNullOrEmpty(frame.TextureName) ? "no texture" : frame.TextureName)}). Use a PNG that exists.");
        return frame.RightCoordinate > frame.LeftCoordinate && frame.BottomCoordinate > frame.TopCoordinate
            ? null
            : Fail(EditErrorCode.InvalidValue, $"The region of frame {Address(chain, frame)} has no size; set it with set-frame-region.");
    }

    // ---- frames ----

    /// <summary>Sets one frame's length in the file's own time unit, within that unit's inspector range.</summary>
    public static EditResult SetFrameLength(
        IProjectManager projectManager, IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, float length)
    {
        var check = CheckFrame(chain, frame);
        if (check is not null) return check;

        var unit = FrameLengthUnit.Of(projectManager.AnimationChainListSave);
        if (CheckLength(unit, length, Address(chain, frame)) is { } bad) return bad;

        if (frame.FrameLength == length) return Ok();
        commands.SetFrameLength([frame], length);
        return Ok(Address(chain, frame));
    }

    /// <summary>
    /// Sets the pixel rectangle of the texture one frame shows. The size comes from the texture's PNG header,
    /// so the frame's texture must be a PNG that exists next to (or at the stored path of) the animation file.
    /// </summary>
    public static EditResult SetFrameRegion(
        IProjectManager projectManager, IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, FramePixelRegion region)
    {
        var check = CheckFrame(chain, frame);
        if (check is not null) return check;

        var folder = string.IsNullOrEmpty(projectManager.FileName) ? string.Empty : Path.GetDirectoryName(projectManager.FileName) ?? string.Empty;
        var absolute = TexturePathHelper.ResolveDisplayPath(frame.TextureName ?? string.Empty, folder);
        if (!File.Exists(absolute))
            return Fail(EditErrorCode.TextureNotFound, $"Could not load image: {absolute}");

        var size = TextureCoordinateConverter.TryReadPngSize(absolute);
        if (size is null)
            return Fail(EditErrorCode.UnsupportedFile, "A region needs the texture's size, which is only read from PNG files.");

        var (w, h) = size.Value;
        if (region.X < 0 || region.Y < 0 || region.Width <= 0 || region.Height <= 0 || region.X > w - region.Width || region.Y > h - region.Height)
            return Fail(EditErrorCode.InvalidValue,
                $"The region {region.X},{region.Y},{region.Width},{region.Height} is not inside the {w}x{h} texture.");

        var before = Coordinates(frame);
        commands.SetFramePixelRegion([frame], region.X, region.Y, region.Width, region.Height);
        return Coordinates(frame) == before ? Ok() : Ok(Address(chain, frame));
    }

    /// <summary>Sets a frame's relative offset; a null axis is left as it is, and at least one must be given.</summary>
    public static EditResult SetFrameOffset(IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, float? x, float? y)
    {
        var check = CheckFrame(chain, frame);
        if (check is not null) return check;
        if (x is null && y is null) return Fail(EditErrorCode.InvalidValue, "An offset needs x, y or both.");
        if ((x is { } nx && !NumberFields.InRange(nx)) || (y is { } ny && !NumberFields.InRange(ny)))
            return Fail(EditErrorCode.InvalidValue, $"An offset is out of range: valid values are {NumberFields.RangeText}.");

        var before = (frame.RelativeX, frame.RelativeY);
        commands.SetFrameRelative([frame], Edit(x), Edit(y));
        return (frame.RelativeX, frame.RelativeY) == before ? Ok() : Ok(Address(chain, frame));
    }

    /// <summary>Sets color channels (R/G/B -255..255, alpha 0..255) and/or the color operation; unset parts are left alone.</summary>
    public static EditResult SetFrameColor(IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, FrameColorEdit edit)
    {
        var check = CheckFrame(chain, frame);
        if (check is not null) return check;
        if (edit == default) return Fail(EditErrorCode.InvalidValue, "A color edit needs red, green, blue, alpha or operation.");

        foreach (var (name, value) in new[] { ("red", edit.Red), ("green", edit.Green), ("blue", edit.Blue) })
            if (value is < -255 or > 255)
                return Fail(EditErrorCode.InvalidValue, $"The {name} channel {value} is out of range: valid values are -255 to 255.");
        if (edit.Alpha is < 0 or > 255)
            return Fail(EditErrorCode.InvalidValue, $"The alpha channel {edit.Alpha} is out of range: valid values are 0 to 255.");

        var before = (frame.Red, frame.Green, frame.Blue, frame.Alpha, frame.ColorOperation);
        commands.SetFrameColorValues([frame], edit.Red, edit.Green, edit.Blue, edit.Alpha, edit.Operation);
        return (frame.Red, frame.Green, frame.Blue, frame.Alpha, frame.ColorOperation) == before ? Ok() : Ok(Address(chain, frame));
    }

    /// <summary>Sets absolute flip flags; a null flag is left as it is, and at least one must be given.</summary>
    public static EditResult SetFrameFlip(
        IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, bool? flipH, bool? flipV, bool? flipDiagonal)
    {
        var check = CheckFrame(chain, frame);
        if (check is not null) return check;
        if (flipH is null && flipV is null && flipDiagonal is null)
            return Fail(EditErrorCode.InvalidValue, "A flip edit needs flipH, flipV, flipDiagonal or several of them.");

        var before = (frame.FlipHorizontal, frame.FlipVertical, frame.FlipDiagonal);
        commands.SetFrameFlip([frame], flipH, flipV, flipDiagonal);
        return (frame.FlipHorizontal, frame.FlipVertical, frame.FlipDiagonal) == before ? Ok() : Ok(Address(chain, frame));
    }

    // ---- file ----

    /// <summary>
    /// Changes the file's time unit to second or millisecond and converts every frame length so playback time is
    /// unchanged. Locked chains are converted too: a file with mixed units would play wrong. A file with an undefined
    /// unit is read as seconds, so setting <c>second</c> only records the unit.
    /// </summary>
    public static EditResult SetTimeUnit(IProjectManager projectManager, IAppCommands commands, TimeMeasurementUnit unit)
    {
        if (unit == TimeMeasurementUnit.Undefined)
            return Fail(EditErrorCode.InvalidValue, "The time unit must be second or millisecond.");
        if (projectManager.AnimationChainListSave is not { } list)
            return Fail(EditErrorCode.NotFound, "No animation file is loaded.");
        if (list.TimeMeasurementUnit == unit) return Ok();

        commands.SetTimeMeasurementUnit(unit);
        return Ok("time unit");
    }

    // ---- helpers ----

    private static NumericEdit? Edit(float? value) => value is { } v ? (NumericEdit)v : null;

    /// <summary>Why a frame length, in the file's unit, is outside the Inspector's range; null when it is inside.</summary>
    internal static EditResult? CheckLength(FrameLengthUnit unit, float length, string address) =>
        // Scaling can land a hair outside a limit (9 x 0.1 scaled to 0.009), which is not an out-of-range length.
        !float.IsFinite(length) || length < (float)unit.Minimum * (1 - 1e-6f) || length > (float)unit.Maximum * (1 + 1e-6f)
            ? Fail(EditErrorCode.InvalidValue,
                $"A frame length of {length.ToString(CultureInfo.InvariantCulture)} for {address} is out of range: valid lengths are " +
                $"{unit.Minimum.ToString(CultureInfo.InvariantCulture)} to {unit.Maximum.ToString(CultureInfo.InvariantCulture)} {unit.Name}.")
            : null;

    private static EditResult? CheckFrame(AnimationChainSave chain, AnimationFrameSave frame)
    {
        if (!chain.Frames.Contains(frame)) return Fail(EditErrorCode.NotFound, $"The frame is not in chain '{chain.Name}'.");
        return chain.IsLocked ? StructureEdit.LockedFail(chain) : null;
    }

    private static (float, float, float, float) Coordinates(AnimationFrameSave frame) =>
        (frame.LeftCoordinate, frame.RightCoordinate, frame.TopCoordinate, frame.BottomCoordinate);

    private static string Address(AnimationChainSave chain, AnimationFrameSave frame) => $"{chain.Name}/{chain.Frames.IndexOf(frame)}";

    private static EditResult Ok(params string[] changed) => new(changed, [], []);

    private static EditResult Fail(EditErrorCode code, string message) => new([], [message], [], [code]);
}
