using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FileSyncClientUICommon
{
    public class SimpleCommand : ICommand
    {
        private Func<object?, bool> _canExecute;
        private Action<object?> _execute;
        public SimpleCommand(Func<object?, bool> canExecute, Action<object?> execute)
        {
            _canExecute = canExecute;
            _execute = execute;
        }
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return _canExecute.Invoke(parameter);
        }

        public void Execute(object? parameter)
        {
            _execute.Invoke(parameter);
        }
    }
}
