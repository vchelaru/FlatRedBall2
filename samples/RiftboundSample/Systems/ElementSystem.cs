using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public static class ElementSystem
{
    private const float Advantage = 1.5f;
    private const float Disadvantage = 0.5f;
    private const float ResistSelf = 0.5f;
    private const float Neutral = 1.0f;

    /// <summary>
    /// Returns the damage multiplier for an attack element against a defender's affinities.
    /// Checks explicit affinities first; falls back to triangle rules if no explicit match.
    /// </summary>
    public static float GetMultiplier(Element attackElement, List<ElementAffinity> defenderAffinities)
    {
        if (attackElement == Element.None)
            return Neutral;

        // Check explicit affinities first
        var explicit_ = defenderAffinities.FirstOrDefault(a => a.Element == attackElement);
        if (explicit_ != null)
            return explicit_.Multiplier;

        // No explicit affinity — fall back to triangle rules
        return GetTriangleMultiplier(attackElement, defenderAffinities);
    }

    /// <summary>
    /// Returns the triangle-based multiplier. If the defender has an element that the
    /// attack element is strong against (or weak against), returns the appropriate value.
    /// </summary>
    private static float GetTriangleMultiplier(Element attack, List<ElementAffinity> defenderAffinities)
    {
        // Determine which elements the defender "is" from their affinities
        // If no affinities at all, neutral
        if (defenderAffinities.Count == 0)
            return Neutral;

        // Check each defender element for triangle relationship
        float bestMultiplier = Neutral;
        foreach (var affinity in defenderAffinities)
        {
            var defElement = affinity.Element;
            if (attack == defElement)
            {
                bestMultiplier = Math.Min(bestMultiplier, ResistSelf);
            }
            else if (IsStrongAgainst(attack, defElement))
            {
                bestMultiplier = Math.Max(bestMultiplier, Advantage);
            }
            else if (IsStrongAgainst(defElement, attack))
            {
                bestMultiplier = Math.Min(bestMultiplier, Disadvantage);
            }
        }

        return bestMultiplier;
    }

    /// <summary>
    /// Returns true if attacker element beats defender element in triangle rules.
    /// World: Steam > Glitch > Aether > Steam.
    /// Classical: Fire > Ice > Lightning > Fire.
    /// </summary>
    public static bool IsStrongAgainst(Element attacker, Element defender)
    {
        return (attacker, defender) switch
        {
            // World triangle
            (Element.Steam, Element.Glitch) => true,
            (Element.Glitch, Element.Aether) => true,
            (Element.Aether, Element.Steam) => true,
            // Classical triangle
            (Element.Fire, Element.Ice) => true,
            (Element.Ice, Element.Lightning) => true,
            (Element.Lightning, Element.Fire) => true,
            _ => false,
        };
    }
}
