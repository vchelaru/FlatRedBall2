using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Direction words in a duplicated chain's name follow the composed flip. Screen space is Y-down;
/// a diagonal flip is the main-diagonal transpose, so on its own it swaps Right/Down and Left/Up.
/// </summary>
[Collection("SequentialSingletons")]
public class ChainFlipNamingTests
{
    private const ChainFlip H = ChainFlip.Horizontal;
    private const ChainFlip V = ChainFlip.Vertical;
    private const ChainFlip D = ChainFlip.Diagonal;

    private static string DuplicatedName(string sourceName, ChainFlip flip, Action<AnimationChainSave>? prepare = null)
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var source = TestHelpers.MakeChain(ctx.Acls, sourceName, 2);
        prepare?.Invoke(source);
        return ctx.AppCommands.DuplicateChain(source, flip)!.Name;
    }

    // Expected results per flip, for Right / Left / Up / Down words. Derived from the matrix the
    // flags render with (FlipScaleCalculator.ComputeMatrix), applied to screen directions.
    [Theory]
    [InlineData(H,       "WalkRight", "WalkLeft")]
    [InlineData(H,       "Run_Left",  "Run_Right")]
    [InlineData(H,       "IdleUp",    "IdleUp2")]
    [InlineData(V,       "IdleUp",    "IdleDown")]
    [InlineData(V,       "Run_Down",  "Run_Up")]
    [InlineData(V,       "WalkRight", "WalkRight2")]
    [InlineData(H | V,   "WalkRight", "WalkLeft")]
    [InlineData(H | V,   "Run_Left",  "Run_Right")]
    [InlineData(H | V,   "IdleUp",    "IdleDown")]
    [InlineData(D,       "WalkRight", "WalkDown")]
    [InlineData(D,       "Run_Left",  "Run_Up")]
    [InlineData(D,       "IdleUp",    "IdleLeft")]
    [InlineData(D,       "IdleDown",  "IdleRight")]
    [InlineData(H | D,   "WalkRight", "WalkDown")]
    [InlineData(H | D,   "Run_Left",  "Run_Up")]
    [InlineData(H | D,   "IdleUp",    "IdleRight")]
    [InlineData(H | D,   "IdleDown",  "IdleLeft")]
    [InlineData(V | D,   "WalkRight", "WalkUp")]
    [InlineData(V | D,   "Run_Left",  "Run_Down")]
    [InlineData(V | D,   "IdleUp",    "IdleLeft")]
    [InlineData(V | D,   "IdleDown",  "IdleRight")]
    [InlineData(H | V | D, "WalkRight", "WalkUp")]
    [InlineData(H | V | D, "Run_Left",  "Run_Down")]
    [InlineData(H | V | D, "IdleUp",    "IdleRight")]
    [InlineData(H | V | D, "IdleDown",  "IdleLeft")]
    public void Duplicate_RenamesDirectionWords_ByTheComposedFlip(ChainFlip flip, string source, string expected)
    {
        Assert.Equal(expected, DuplicatedName(source, flip));
    }

    [Theory]
    [InlineData(H)]
    [InlineData(V)]
    [InlineData(D)]
    [InlineData(H | V)]
    [InlineData(H | D)]
    [InlineData(V | D)]
    [InlineData(H | V | D)]
    public void Duplicate_NameWithoutDirectionWords_GetsTheUniqueNumberSuffix(ChainFlip flip)
    {
        Assert.Equal("Idle2", DuplicatedName("Idle", flip));
    }

    [Fact]
    public void Duplicate_Diagonal_SwapsEveryWordInOnePass()
    {
        // A two-pass swap would turn Right->Down->Right.
        Assert.Equal("AttackDownRight", DuplicatedName("AttackRightDown", D));
    }

    [Fact]
    public void Duplicate_Diagonal_DoesNotMatchWordsInsideLongerWords()
    {
        Assert.Equal("Downtown2", DuplicatedName("Downtown", D));
        Assert.Equal("Upgrade2", DuplicatedName("Upgrade", D));
    }

    [Fact]
    public void Duplicate_Diagonal_FromAHorizontallyFlippedSource_IsTheAntiDiagonal()
    {
        // A diagonal toggle on top of H^V flags acts as the other diagonal on what is displayed.
        Assert.Equal("WalkUp", DuplicatedName("WalkRight", D,
            chain => { foreach (var frame in chain.Frames) frame.FlipHorizontal = true; }));
    }

    [Fact]
    public void Duplicate_Diagonal_WhenFramesDisagreeOnTheirFlags_KeepsTheNameUnrenamed()
    {
        // The diagonal's effect depends on each frame's current flags; mixed frames have no single answer.
        Assert.Equal("WalkRight2", DuplicatedName("WalkRight", D,
            chain => chain.Frames[0].FlipHorizontal = true));
    }

    [Fact]
    public void Duplicate_HorizontalAndDiagonal_WhenFramesDisagree_FallsBackToTheHorizontalRename()
    {
        Assert.Equal("WalkLeft", DuplicatedName("WalkRight", H | D,
            chain => chain.Frames[0].FlipHorizontal = true));
    }

    [Fact]
    public void Duplicate_Diagonal_OnAChainWithNoFrames_UsesTheUnflippedMapping()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var source = TestHelpers.MakeChain(ctx.Acls, "WalkRight");
        Assert.Equal("WalkDown", ctx.AppCommands.DuplicateChain(source, D)!.Name);
    }
}
