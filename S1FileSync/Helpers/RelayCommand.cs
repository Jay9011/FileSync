using System.Windows.Input;

namespace S1FileSync.Helpers;

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="execute">명령 실행 메서드</param>
    /// <param name="canExecute">명령 실행 여부 메서드</param>
    /// <exception cref="ArgumentNullException">명령 실행 메서드가 null일 경우 발생</exception>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// 명령 실행 여부
    /// </summary>
    /// <param name="parameter">여부 판단에 사용되는 메서드 매개변수</param>
    /// <returns>bool</returns>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <summary>
    /// 명령 실행
    /// </summary>
    /// <param name="parameter">명령 실행시 전달되는 매개변수</param>
    public void Execute(object? parameter) => _execute();

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}