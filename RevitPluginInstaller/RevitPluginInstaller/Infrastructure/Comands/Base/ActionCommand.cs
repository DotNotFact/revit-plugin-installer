namespace RevitPluginInstaller.Infrastructure.Comands.Base;

public class ActionCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : Command
{
    private readonly Action<object?> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    private readonly Func<object?, bool>? _canExecute = canExecute;

    public override bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public override void Execute(object? parameter) => _execute(parameter);
}
