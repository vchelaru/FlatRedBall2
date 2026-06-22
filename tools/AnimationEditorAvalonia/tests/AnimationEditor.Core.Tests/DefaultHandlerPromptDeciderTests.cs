using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests the three-state decision for the "make me the default .achx handler" prompt:
/// already-default and user-dismissed both stay silent; only a supported, non-default,
/// not-yet-dismissed state prompts.
/// </summary>
public class DefaultHandlerPromptDeciderTests
{
    [Fact]
    public void ShouldPrompt_AlreadyDefault_ReturnsFalse()
    {
        bool result = DefaultHandlerPromptDecider.ShouldPrompt(
            isSupported: true, isDefault: true, isPromptSuppressed: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldPrompt_NotDefaultAndNotSuppressed_ReturnsTrue()
    {
        bool result = DefaultHandlerPromptDecider.ShouldPrompt(
            isSupported: true, isDefault: false, isPromptSuppressed: false);

        Assert.True(result);
    }

    [Fact]
    public void ShouldPrompt_NotSupportedPlatform_ReturnsFalse()
    {
        bool result = DefaultHandlerPromptDecider.ShouldPrompt(
            isSupported: false, isDefault: false, isPromptSuppressed: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldPrompt_SuppressedByUser_ReturnsFalse()
    {
        bool result = DefaultHandlerPromptDecider.ShouldPrompt(
            isSupported: true, isDefault: false, isPromptSuppressed: true);

        Assert.False(result);
    }
}
