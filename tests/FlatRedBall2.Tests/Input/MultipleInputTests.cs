using FlatRedBall2.Input;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Input;

// --- fakes ---

file sealed class Fake1DInput(float value) : I1DInput
{
    public float Value => value;
}

file sealed class Fake2DInput(float x, float y) : I2DInput
{
    public float X => x;
    public float Y => y;
}

file sealed class FakePressableInput : IPressableInput
{
    public bool IsDown { get; set; }
    public bool WasJustPressed { get; set; }
    public bool WasJustReleased { get; set; }
}

// --- Multiple1DInputs / I1DInput.Or ---

public class Multiple1DInputsTests
{
    [Fact]
    public void Or_ReturnsDominantValue_WhenFirstIsLarger()
    {
        I1DInput a = new Fake1DInput(0.8f);
        I1DInput b = new Fake1DInput(0.3f);

        a.Or(b).Value.ShouldBe(0.8f);
    }

    [Fact]
    public void Or_ReturnsDominantValue_WhenSecondIsLarger()
    {
        I1DInput a = new Fake1DInput(0.3f);
        I1DInput b = new Fake1DInput(0.8f);

        a.Or(b).Value.ShouldBe(0.8f);
    }

    [Fact]
    public void Or_UsesMagnitude_ForNegativeValues()
    {
        I1DInput a = new Fake1DInput(-0.9f);
        I1DInput b = new Fake1DInput(0.5f);

        a.Or(b).Value.ShouldBe(-0.9f);
    }

    [Fact]
    public void Or_Chaining_ReturnsTheSameMultipleInstance()
    {
        I1DInput a = new Fake1DInput(0.1f);
        I1DInput b = new Fake1DInput(0.2f);
        I1DInput c = new Fake1DInput(0.9f);

        var first = a.Or(b);
        var chained = first.Or(c);

        chained.ShouldBeSameAs(first);
        chained.Value.ShouldBe(0.9f);
    }

    [Fact]
    public void Or_AllZero_ReturnsZero()
    {
        I1DInput a = new Fake1DInput(0f);
        I1DInput b = new Fake1DInput(0f);

        a.Or(b).Value.ShouldBe(0f);
    }
}

// --- Multiple2DInputs / I2DInput.Or ---

public class Multiple2DInputsTests
{
    [Fact]
    public void Or_ReturnsDominantX_WhenFirstIsLarger()
    {
        I2DInput a = new Fake2DInput(0.9f, 0f);
        I2DInput b = new Fake2DInput(0.2f, 0f);

        a.Or(b).X.ShouldBe(0.9f);
    }

    [Fact]
    public void Or_ReturnsDominantX_WhenSecondIsLarger()
    {
        I2DInput a = new Fake2DInput(0.1f, 0f);
        I2DInput b = new Fake2DInput(0.7f, 0f);

        a.Or(b).X.ShouldBe(0.7f);
    }

    [Fact]
    public void Or_EachAxisIsIndependent()
    {
        // a wins on X, b wins on Y
        I2DInput a = new Fake2DInput(0.8f, 0.1f);
        I2DInput b = new Fake2DInput(0.2f, 0.9f);

        var result = a.Or(b);

        result.X.ShouldBe(0.8f);
        result.Y.ShouldBe(0.9f);
    }

    [Fact]
    public void Or_UsesMagnitude_ForNegativeValues()
    {
        I2DInput a = new Fake2DInput(-0.8f, 0f);
        I2DInput b = new Fake2DInput(0.5f, 0f);

        a.Or(b).X.ShouldBe(-0.8f);
    }

    [Fact]
    public void Or_Chaining_ReturnsTheSameMultipleInstance()
    {
        I2DInput a = new Fake2DInput(0.1f, 0f);
        I2DInput b = new Fake2DInput(0.2f, 0f);
        I2DInput c = new Fake2DInput(0.9f, 0f);

        var first = a.Or(b);
        var chained = first.Or(c);

        chained.ShouldBeSameAs(first);
        chained.X.ShouldBe(0.9f);
    }
}

// --- OrPressableInput / IPressableInput.Or ---

public class OrPressableInputTests
{
    [Fact]
    public void IsDown_True_WhenAnyIsDown()
    {
        var a = new FakePressableInput { IsDown = false };
        var b = new FakePressableInput { IsDown = true };

        ((IPressableInput)a).Or(b).IsDown.ShouldBeTrue();
    }

    [Fact]
    public void IsDown_False_WhenNoneIsDown()
    {
        var a = new FakePressableInput { IsDown = false };
        var b = new FakePressableInput { IsDown = false };

        ((IPressableInput)a).Or(b).IsDown.ShouldBeFalse();
    }

    [Fact]
    public void WasJustPressed_True_WhenAnyWasJustPressed()
    {
        var a = new FakePressableInput { WasJustPressed = false };
        var b = new FakePressableInput { WasJustPressed = true };

        ((IPressableInput)a).Or(b).WasJustPressed.ShouldBeTrue();
    }

    [Fact]
    public void WasJustPressed_False_WhenNoneWasJustPressed()
    {
        var a = new FakePressableInput { WasJustPressed = false };
        var b = new FakePressableInput { WasJustPressed = false };

        ((IPressableInput)a).Or(b).WasJustPressed.ShouldBeFalse();
    }

    [Fact]
    public void WasJustReleased_True_WhenAnyWasJustReleased()
    {
        var a = new FakePressableInput { WasJustReleased = false };
        var b = new FakePressableInput { WasJustReleased = true };

        ((IPressableInput)a).Or(b).WasJustReleased.ShouldBeTrue();
    }

    [Fact]
    public void WasJustReleased_False_WhenNoneWasJustReleased()
    {
        var a = new FakePressableInput { WasJustReleased = false };
        var b = new FakePressableInput { WasJustReleased = false };

        ((IPressableInput)a).Or(b).WasJustReleased.ShouldBeFalse();
    }

    [Fact]
    public void Or_Chaining_ReturnsTheSameOrInstance()
    {
        IPressableInput a = new FakePressableInput();
        IPressableInput b = new FakePressableInput();
        IPressableInput c = new FakePressableInput { WasJustPressed = true };

        var first = a.Or(b);
        var chained = first.Or(c);

        chained.ShouldBeSameAs(first);
        chained.WasJustPressed.ShouldBeTrue();
    }

    [Fact]
    public void Or_AllFalse_WhenNoInputsActive()
    {
        IPressableInput a = new FakePressableInput();
        IPressableInput b = new FakePressableInput();

        var result = a.Or(b);

        result.IsDown.ShouldBeFalse();
        result.WasJustPressed.ShouldBeFalse();
        result.WasJustReleased.ShouldBeFalse();
    }
}
