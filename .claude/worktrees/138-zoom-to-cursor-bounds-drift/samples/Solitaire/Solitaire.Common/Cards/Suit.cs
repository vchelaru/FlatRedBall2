namespace Solitaire.Cards;

public enum Suit
{
    Spades,
    Hearts,
    Clubs,
    Diamonds,
}

public static class SuitExtensions
{
    public static bool IsRed(this Suit suit) => suit is Suit.Hearts or Suit.Diamonds;
}
