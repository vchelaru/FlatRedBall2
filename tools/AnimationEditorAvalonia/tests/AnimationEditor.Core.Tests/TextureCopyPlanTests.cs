using AnimationEditor.Core.IO;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Copying a texture next to the .achx must never silently replace a different file that already
/// has that name there (#1249).
/// </summary>
public class TextureCopyPlanTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AnimationEditorCoreTests", Guid.NewGuid().ToString("N"));
    private readonly string _achxFolder;
    private readonly string _source;
    private readonly string _destination;

    public TextureCopyPlanTests()
    {
        _achxFolder = Path.Combine(_root, "Animations");
        string outsideFolder = Path.Combine(_root, "Downloads");
        Directory.CreateDirectory(_achxFolder);
        Directory.CreateDirectory(outsideFolder);
        _source = Path.Combine(outsideFolder, "hero.png");
        _destination = Path.Combine(_achxFolder, "hero.png");
        File.WriteAllBytes(_source, new byte[] { 1, 2, 3 });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void PlanCopy_NoFileAtDestination_OffersCopyOrKeep()
    {
        var plan = TextureCopyDecider.PlanCopy(_source, _achxFolder);

        plan.DestinationPath.ShouldBe(_destination);
        plan.Conflict.ShouldBe(TextureCopyConflict.None);
        plan.Choices.ShouldBe(new[] { TextureCopyChoice.Copy, TextureCopyChoice.KeepInPlace });
    }

    [Fact]
    public void PlanCopy_DifferentFileAtDestination_OffersKeepOverwriteOrUseExisting()
    {
        File.WriteAllBytes(_destination, new byte[] { 9, 9 });

        var plan = TextureCopyDecider.PlanCopy(_source, _achxFolder);

        plan.Conflict.ShouldBe(TextureCopyConflict.DifferentFileExists);
        plan.Choices.ShouldBe(new[]
        {
            TextureCopyChoice.KeepInPlace, TextureCopyChoice.Overwrite, TextureCopyChoice.UseExisting
        });
    }

    [Fact]
    public void PlanCopy_IdenticalFileAtDestination_OffersCopyOrKeepWithoutConflict()
    {
        // Same bytes: "copy" loses nothing, so there is nothing to ask about overwriting.
        File.WriteAllBytes(_destination, new byte[] { 1, 2, 3 });

        var plan = TextureCopyDecider.PlanCopy(_source, _achxFolder);

        plan.Conflict.ShouldBe(TextureCopyConflict.IdenticalFileExists);
        plan.Choices.ShouldBe(new[] { TextureCopyChoice.Copy, TextureCopyChoice.KeepInPlace });
    }

    [Fact]
    public void Apply_UseExisting_ReturnsDestinationAndLeavesItUntouched()
    {
        File.WriteAllBytes(_destination, new byte[] { 9, 9 });
        var plan = TextureCopyDecider.PlanCopy(_source, _achxFolder);

        string? resolved = TextureCopyDecider.Apply(plan, TextureCopyChoice.UseExisting);

        resolved.ShouldBe(_destination);
        File.ReadAllBytes(_destination).ShouldBe(new byte[] { 9, 9 });
    }

    [Fact]
    public void Apply_Overwrite_ReplacesDestinationWithSource()
    {
        File.WriteAllBytes(_destination, new byte[] { 9, 9 });
        var plan = TextureCopyDecider.PlanCopy(_source, _achxFolder);

        string? resolved = TextureCopyDecider.Apply(plan, TextureCopyChoice.Overwrite);

        resolved.ShouldBe(_destination);
        File.ReadAllBytes(_destination).ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void Apply_Copy_WhenFileAppearedAfterPlanning_ThrowsInsteadOfOverwriting()
    {
        var plan = TextureCopyDecider.PlanCopy(_source, _achxFolder);
        File.WriteAllBytes(_destination, new byte[] { 9, 9 });

        Should.Throw<IOException>(() => TextureCopyDecider.Apply(plan, TextureCopyChoice.Copy));
        File.ReadAllBytes(_destination).ShouldBe(new byte[] { 9, 9 });
    }

    [Fact]
    public void Apply_KeepInPlaceOrCancel_ReturnsSourceOrNull()
    {
        var plan = TextureCopyDecider.PlanCopy(_source, _achxFolder);

        TextureCopyDecider.Apply(plan, TextureCopyChoice.KeepInPlace).ShouldBe(_source);
        TextureCopyDecider.Apply(plan, TextureCopyChoice.Cancel).ShouldBeNull();
        File.Exists(_destination).ShouldBeFalse();
    }
}
