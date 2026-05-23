using System;
using System.Windows.Input;

namespace AnimationEditor.App;

/// <summary>Wraps a parameterless <see cref="Action"/> as an <see cref="ICommand"/> that always reports CanExecute = true.</summary>
internal sealed class RelayCommand(Action execute) : ICommand
{
#pragma warning disable CS0067 // event is required by ICommand but never fired — CanExecute is always true
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
