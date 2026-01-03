using System;
using System.Windows.Input;

/// <summary>
/// ICommandの実装クラス
/// アクションと実行可能判定をラムダ式で指定できる
/// </summary>
internal sealed class RelayCommand : ICommand
{
    /// <summary>コマンド実行時のアクション</summary>
    private readonly Action<object?> _execute;
    /// <summary>コマンド実行可能判定の関数（nullの場合は常にtrue）</summary>
    private readonly Func<object?, bool>? _canExecute;

    /// <summary>
    /// RelayCommandのインスタンスを初期化する
    /// </summary>
    /// <param name="execute">コマンド実行時のアクション</param>
    /// <param name="canExecute">コマンド実行可能判定の関数（省略可）</param>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// コマンドが実行可能かどうかを判定する
    /// </summary>
    /// <param name="parameter">コマンドのパラメータ</param>
    /// <returns>実行可能な場合はtrue</returns>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <summary>
    /// コマンドを実行する
    /// </summary>
    /// <param name="parameter">コマンドのパラメータ</param>
    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }
}