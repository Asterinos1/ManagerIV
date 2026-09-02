using System.Windows.Input;

namespace ManagerIV.Core;

/// <summary>
/// A command that delegates its execution logic.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelayCommand"/> class.
    /// </summary>
    /// <param name="execute">The action to execute.</param>
    /// <param name="canExecute">The status query function.</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// Evaluates if the command can execute in its current state.
    /// </summary>
    public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();

    /// <summary>
    /// Executes the action.
    /// </summary>
    public void Execute(object? parameter) => _execute();

    /// <summary>
    /// Occurs when changes occur that affect whether or not the command should execute.
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

/// <summary>
/// A generic command that delegates its execution logic.
/// </summary>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool>? _canExecute;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelayCommand{T}"/> class.
    /// </summary>
    /// <param name="execute">The action to execute.</param>
    /// <param name="canExecute">The status query function.</param>
    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// Evaluates if the command can execute with the given parameter.
    /// </summary>
    public bool CanExecute(object? parameter)
    {
        if (_canExecute == null) return true;
        if (parameter is T val) return _canExecute(val);
        return false;
    }

    /// <summary>
    /// Executes the action with the given parameter.
    /// </summary>
    public void Execute(object? parameter)
    {
        if (parameter is T val) _execute(val);
    }

    /// <summary>
    /// Occurs when changes occur that affect whether or not the command should execute.
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
