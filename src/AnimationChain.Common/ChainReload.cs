using System.Collections.Generic;
using System.Linq;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>What a hot reload replaces on a chain, whichever runtime the chain belongs to.</summary>
internal interface IReloadableChain
{
    string Name { get; }
    bool Loop { get; set; }
    bool PingPong { get; set; }
    bool Reversed { get; set; }
}

/// <summary>The hot reload merge, shared by every runtime's chain list so they agree.</summary>
internal static class ChainReload
{
    /// <summary>
    /// Brings <paramref name="list"/> in line with the freshly loaded <paramref name="fresh"/>, keeping the instances a
    /// player may be holding: the nth chain of a name takes over the nth fresh chain of that name, fresh chains without a
    /// counterpart are appended, and chains the file no longer has stay as they are.
    /// </summary>
    public static void Apply<TChain, TFrame>(List<TChain> list, IEnumerable<TChain> fresh) where TChain : List<TFrame>, IReloadableChain
    {
        var unmatched = list.ToList();
        foreach (var freshChain in fresh)
        {
            int index = unmatched.FindIndex(chain => chain.Name == freshChain.Name);
            if (index < 0)
            {
                list.Add(freshChain);
                continue;
            }

            var existing = unmatched[index];
            unmatched.RemoveAt(index);
            existing.Clear();
            existing.AddRange(freshChain);
            existing.Loop = freshChain.Loop;
            existing.PingPong = freshChain.PingPong;
            existing.Reversed = freshChain.Reversed;
        }
    }
}
