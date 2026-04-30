namespace Solitaire.Cards;

public static class Rules
{
    public static bool CanPlaceOnTableau(Card moving, Card? top)
    {
        if (top is null)
        {
            return moving.Rank == Rank.King;
        }

        return moving.Suit.IsRed() != top.Suit.IsRed()
            && (int)moving.Rank == (int)top.Rank - 1;
    }

    public static bool CanPlaceOnFoundation(Card moving, FoundationPile foundation)
    {
        if (moving.Suit != foundation.Suit) return false;

        if (foundation.IsEmpty)
        {
            return moving.Rank == Rank.Ace;
        }

        return (int)moving.Rank == (int)foundation.Top!.Rank + 1;
    }
}
