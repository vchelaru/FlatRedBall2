using System;
using System.Collections.Generic;

namespace Solitaire.Cards;

public class Deck
{
    private readonly List<Card> _cards = new(52);

    public IReadOnlyList<Card> Cards => _cards;
    public int Count => _cards.Count;

    public Deck()
    {
        foreach (Suit suit in Enum.GetValues<Suit>())
        {
            for (int rank = 1; rank <= 13; rank++)
            {
                _cards.Add(new Card(suit, (Rank)rank));
            }
        }
    }

    public void Shuffle(Random random)
    {
        for (int i = _cards.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
    }

    public Card DrawTop()
    {
        var top = _cards[^1];
        _cards.RemoveAt(_cards.Count - 1);
        return top;
    }
}
