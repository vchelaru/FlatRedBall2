using FlatRedBall2.AnimationEditorCommon;
using System.Linq;

namespace AnimationEditor.Core.Utilities;

internal static class ChainNames
{
    public static bool HasOtherChainNamed(this AnimationChainListSave list, AnimationChainSave chain, string name) =>
        list.AnimationChains.Any(c => !ReferenceEquals(c, chain) && c.Name == name);
}
