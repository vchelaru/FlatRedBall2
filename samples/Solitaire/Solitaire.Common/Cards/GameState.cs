using System;
using System.Collections.Generic;

namespace Solitaire.Cards;

public class GameState
{
    public StockPile Stock { get; } = new();
    public WastePile Waste { get; } = new();
    public FoundationPile[] Foundations { get; } =
    [
        new(Suit.Spades),
        new(Suit.Hearts),
        new(Suit.Clubs),
        new(Suit.Diamonds),
    ];
    public TableauPile[] Tableaus { get; } =
    [
        new(), new(), new(), new(), new(), new(), new(),
    ];

    public bool IsWon
    {
        get
        {
            int total = 0;
            foreach (var f in Foundations) total += f.Count;
            return total == 52;
        }
    }

    public void Deal(Random random)
    {
        Stock.Clear();
        Waste.Clear();
        foreach (var f in Foundations) f.Clear();
        foreach (var t in Tableaus) t.Clear();

        var deck = new Deck();
        deck.Shuffle(random);

        for (int col = 0; col < 7; col++)
        {
            for (int row = 0; row <= col; row++)
            {
                var card = deck.DrawTop();
                card.IsFaceUp = (row == col);
                Tableaus[col].Push(card);
            }
        }

        while (deck.Count > 0)
        {
            var card = deck.DrawTop();
            card.IsFaceUp = false;
            Stock.Push(card);
        }
    }

    public void DrawThree()
    {
        if (Stock.IsEmpty)
        {
            while (!Waste.IsEmpty)
            {
                var card = Waste.Pop();
                card.IsFaceUp = false;
                Stock.Push(card);
            }
            return;
        }

        int toDraw = Math.Min(3, Stock.Count);
        for (int i = 0; i < toDraw; i++)
        {
            var card = Stock.Pop();
            card.IsFaceUp = true;
            Waste.Push(card);
        }
    }
}
