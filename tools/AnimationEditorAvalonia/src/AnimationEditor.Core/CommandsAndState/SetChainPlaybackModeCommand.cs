using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>The per-chain playback flags beside Loop (#1538).</summary>
    internal enum ChainPlaybackMode { PingPong, Reverse }

    /// <summary>Sets <see cref="AnimationChainSave.PingPong"/> or <see cref="AnimationChainSave.Reverse"/> on one chain.</summary>
    internal sealed class SetChainPlaybackModeCommand : IUndoableCommand
    {
        private readonly AnimationChainSave _chain;
        private readonly ChainPlaybackMode _mode;
        private readonly bool _value;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public string Description { get; }

        public SetChainPlaybackModeCommand(AnimationChainSave chain, ChainPlaybackMode mode, bool value,
            IAppCommands commands, IApplicationEvents events)
        {
            _chain = chain;
            _mode = mode;
            _value = value;
            _commands = commands;
            _events = events;
            Description = $"{(value ? "Enable" : "Disable")} {Label(mode)} on '{chain.Name}'";
        }

        public static string Label(ChainPlaybackMode mode) => mode == ChainPlaybackMode.PingPong ? "ping-pong" : "reverse";

        public static bool Get(AnimationChainSave chain, ChainPlaybackMode mode) =>
            mode == ChainPlaybackMode.PingPong ? chain.PingPong : chain.Reverse;

        public bool Do()
        {
            Set(_value);
            return true;
        }

        public void Undo() => Set(!_value);

        private void Set(bool value)
        {
            if (_mode == ChainPlaybackMode.PingPong) _chain.PingPong = value;
            else _chain.Reverse = value;
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
        }
    }
}
