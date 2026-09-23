namespace AnimationEditor.Views.Tests;

/// <summary>A unique temp path for an <c>IoManager</c>'s recovery file, so no test writes the editor's real one.</summary>
internal static class TestRecoveryPath
{
    public static string New() =>
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AnimationEditorViewsTests", $"recovery_{System.Guid.NewGuid():N}.achx");
}
