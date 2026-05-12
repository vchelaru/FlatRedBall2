using Shouldly;
using Solitaire.Cards;
using Xunit;

namespace Solitaire.Tests;

public class RulesTests
{
    [Fact]
    public void EmptyTableau_AcceptsKing()
    {
        var king = new Card(Suit.Spades, Rank.King);
        Rules.CanPlaceOnTableau(king, top: null).ShouldBeTrue();
    }

    [Fact]
    public void EmptyTableau_RejectsNonKing()
    {
        var queen = new Card(Suit.Hearts, Rank.Queen);
        Rules.CanPlaceOnTableau(queen, top: null).ShouldBeFalse();
    }

    [Theory]
    [InlineData(Suit.Spades, Rank.Queen, Suit.Hearts, Rank.King, true)]   // black on red, descending
    [InlineData(Suit.Clubs,  Rank.Queen, Suit.Diamonds, Rank.King, true)] // black on red
    [InlineData(Suit.Hearts, Rank.Queen, Suit.Spades, Rank.King, true)]   // red on black
    public void Tableau_AlternatingColorDescending_Accepted(Suit ms, Rank mr, Suit ts, Rank tr, bool expected)
    {
        Rules.CanPlaceOnTableau(new Card(ms, mr), new Card(ts, tr)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(Suit.Spades, Rank.Queen, Suit.Clubs, Rank.King)]    // same color
    [InlineData(Suit.Hearts, Rank.Queen, Suit.Diamonds, Rank.King)] // same color
    [InlineData(Suit.Spades, Rank.Jack, Suit.Hearts, Rank.King)]    // off by 2
    [InlineData(Suit.Spades, Rank.King, Suit.Hearts, Rank.Queen)]   // ascending
    public void Tableau_InvalidPlacements_Rejected(Suit ms, Rank mr, Suit ts, Rank tr)
    {
        Rules.CanPlaceOnTableau(new Card(ms, mr), new Card(ts, tr)).ShouldBeFalse();
    }

    [Fact]
    public void EmptyFoundation_AcceptsAceOfMatchingSuit()
    {
        var foundation = new FoundationPile(Suit.Hearts);
        Rules.CanPlaceOnFoundation(new Card(Suit.Hearts, Rank.Ace), foundation).ShouldBeTrue();
    }

    [Fact]
    public void EmptyFoundation_RejectsAceOfWrongSuit()
    {
        var foundation = new FoundationPile(Suit.Hearts);
        Rules.CanPlaceOnFoundation(new Card(Suit.Spades, Rank.Ace), foundation).ShouldBeFalse();
    }

    [Fact]
    public void EmptyFoundation_RejectsNonAce()
    {
        var foundation = new FoundationPile(Suit.Hearts);
        Rules.CanPlaceOnFoundation(new Card(Suit.Hearts, Rank.Two), foundation).ShouldBeFalse();
    }

    [Fact]
    public void Foundation_AcceptsNextRankOfSameSuit()
    {
        var foundation = new FoundationPile(Suit.Hearts);
        foundation.Push(new Card(Suit.Hearts, Rank.Ace));
        Rules.CanPlaceOnFoundation(new Card(Suit.Hearts, Rank.Two), foundation).ShouldBeTrue();
    }

    [Fact]
    public void Foundation_RejectsSkippedRank()
    {
        var foundation = new FoundationPile(Suit.Hearts);
        foundation.Push(new Card(Suit.Hearts, Rank.Ace));
        Rules.CanPlaceOnFoundation(new Card(Suit.Hearts, Rank.Three), foundation).ShouldBeFalse();
    }

    [Fact]
    public void Foundation_RejectsWrongSuit()
    {
        var foundation = new FoundationPile(Suit.Hearts);
        foundation.Push(new Card(Suit.Hearts, Rank.Ace));
        Rules.CanPlaceOnFoundation(new Card(Suit.Diamonds, Rank.Two), foundation).ShouldBeFalse();
    }
}
