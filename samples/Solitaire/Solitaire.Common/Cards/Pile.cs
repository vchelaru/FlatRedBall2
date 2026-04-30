using System.Collections.Generic;

namespace Solitaire.Cards;

public abstract class Pile
{
    protected readonly List<Card> _cards = new();

    public IReadOnlyList<Card> Cards => _cards;
    public int Count => _cards.Count;
    public bool IsEmpty => _cards.Count == 0;
    public Card? Top => IsEmpty ? null : _cards[^1];

    public void Push(Card card) => _cards.Add(card);

    public Card Pop()
    {
        var top = _cards[^1];
        _cards.RemoveAt(_cards.Count - 1);
        return top;
    }

    public void Clear() => _cards.Clear();
}

public class StockPile : Pile { }

public class WastePile : Pile { }

public class FoundationPile : Pile
{
    public Suit Suit { get; }
    public FoundationPile(Suit suit) { Suit = suit; }
}

public class TableauPile : Pile { }
