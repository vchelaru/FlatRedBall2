using System.Linq;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Changes the file's <see cref="AnimationChainListSave.TimeMeasurementUnit"/> and converts every frame length
    /// (locked chains included, since a mixed file would play wrong) so playback time is unchanged. Undo restores
    /// the captured lengths exactly, so a round trip never drifts.
    /// </summary>
    internal sealed class SetTimeUnitCommand : IUndoableCommand
    {
        private readonly AnimationChainListSave _list;
        private readonly TimeMeasurementUnit _newUnit;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        private TimeMeasurementUnit _oldUnit;
        private (AnimationFrameSave Frame, float Before, float After)[] _lengths = [];

        public string Description { get; }

        public SetTimeUnitCommand(AnimationChainListSave list, TimeMeasurementUnit newUnit,
            IAppCommands commands, IApplicationEvents events)
        {
            _list = list;
            _newUnit = newUnit;
            _commands = commands;
            _events = events;
            Description = $"Set Time Unit: {new FrameLengthUnit(newUnit).Name}";
        }

        public bool Do()
        {
            _oldUnit = _list.TimeMeasurementUnit;
            if (_oldUnit == _newUnit) return false;

            var from = new FrameLengthUnit(_oldUnit);
            var to = new FrameLengthUnit(_newUnit);
            _lengths = _list.AnimationChains
                .SelectMany(c => c.Frames)
                .Select(f => (f, f.FrameLength, to.Convert(f.FrameLength, from)))
                .ToArray();
            Apply(_newUnit, after: true);
            return true;
        }

        public void Undo() => Apply(_oldUnit, after: false);

        public void Redo() => Apply(_newUnit, after: true);

        private void Apply(TimeMeasurementUnit unit, bool after)
        {
            _list.TimeMeasurementUnit = unit;
            foreach (var (frame, before, converted) in _lengths)
                frame.FrameLength = after ? converted : before;
            _commands.RefreshTreeView();
            _events.RaiseAnimationChainsChanged();
        }
    }
}
