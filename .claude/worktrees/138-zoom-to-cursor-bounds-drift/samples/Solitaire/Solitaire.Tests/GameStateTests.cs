using System;
using Shouldly;
using Solitaire.Cards;
using Xunit;

namespace Solitaire.Tests;

public class GameStateTests
{
    private static GameState NewDealtGame(int seed = 1234)
    {
        var game = new GameState();
        game.Deal(new Random(seed));
        return game;
    }

    [Fact]
    public void Deal_DistributesAllCards()
    {
        var game = NewDealtGame();

        int total = game.Stock.Count + game.Waste.Count;
        foreach (var f in game.Foundations) total += f.Count;
        foreach (var t in game.Tableaus) total += t.Count;

        total.ShouldBe(52);
    }

    [Fact]
    public void Deal_TableauColumnSizesAre1Through7()
    {
        var game = NewDealtGame();
        for (int col = 0; col < 7; col++)
        {
            game.Tableaus[col].Count.ShouldBe(col + 1);
        }
    }

    [Fact]
    public void Deal_OnlyTopOfEachTableauIsFaceUp()
    {
        var game = NewDealtGame();
        for (int col = 0; col < 7; col++)
        {
            var pile = game.Tableaus[col];
            for (int row = 0; row < pile.Count; row++)
            {
                pile.Cards[row].IsFaceUp.ShouldBe(row == pile.Count - 1);
            }
        }
    }

    [Fact]
    public void Deal_StockHasRemaining24Cards()
    {
        var game = NewDealtGame();
        game.Stock.Count.ShouldBe(24);
    }

    [Fact]
    public void Deal_FoundationsAndWasteStartEmpty()
    {
        var game = NewDealtGame();
        game.Waste.IsEmpty.ShouldBeTrue();
        foreach (var f in game.Foundations) f.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void DrawThree_MovesThreeStockCardsToWaste_FaceUp()
    {
        var game = NewDealtGame();
        int stockBefore = game.Stock.Count;

        game.DrawThree();

        game.Waste.Count.ShouldBe(3);
        game.Stock.Count.ShouldBe(stockBefore - 3);
        for (int i = 0; i < game.Waste.Count; i++)
        {
            game.Waste.Cards[i].IsFaceUp.ShouldBeTrue();
        }
    }

    [Fact]
    public void DrawThree_DrawsRemainderWhenStockHasFewerThanThree()
    {
        var game = NewDealtGame();
        // Stock starts at 24 (divisible by 3). Pop one to make it 23 → not a clean multiple.
        game.Stock.Pop();
        while (game.Stock.Count > 2) game.DrawThree();
        game.Stock.Count.ShouldBe(2);
        int wasteBefore = game.Waste.Count;

        game.DrawThree();

        game.Stock.IsEmpty.ShouldBeTrue();
        game.Waste.Count.ShouldBe(wasteBefore + 2);
    }

    [Fact]
    public void DrawThree_OnEmptyStock_RecyclesWasteFaceDown()
    {
        var game = NewDealtGame();
        // Drain entire stock into waste.
        while (!game.Stock.IsEmpty) game.DrawThree();
        int wasteBefore = game.Waste.Count;

        game.DrawThree();

        game.Stock.Count.ShouldBe(wasteBefore);
        game.Waste.IsEmpty.ShouldBeTrue();
        for (int i = 0; i < game.Stock.Count; i++)
        {
            game.Stock.Cards[i].IsFaceUp.ShouldBeFalse();
        }
    }

    [Fact]
    public void IsWon_TrueWhenAll52CardsInFoundations()
    {
        var game = new GameState();
        // Pre-condition: not won.
        game.IsWon.ShouldBeFalse();

        // Pile 13 cards into each foundation matching its suit.
        foreach (var f in game.Foundations)
        {
            for (int r = 1; r <= 13; r++) f.Push(new Card(f.Suit, (Rank)r));
        }

        game.IsWon.ShouldBeTrue();
    }

    [Fact]
    public void Deal_IsRepeatable_ForSameSeed()
    {
        var a = NewDealtGame(42);
        var b = NewDealtGame(42);

        for (int col = 0; col < 7; col++)
        {
            a.Tableaus[col].Count.ShouldBe(b.Tableaus[col].Count);
            for (int i = 0; i < a.Tableaus[col].Count; i++)
            {
                var ca = a.Tableaus[col].Cards[i];
                var cb = b.Tableaus[col].Cards[i];
                ca.Suit.ShouldBe(cb.Suit);
                ca.Rank.ShouldBe(cb.Rank);
            }
        }
    }
}
