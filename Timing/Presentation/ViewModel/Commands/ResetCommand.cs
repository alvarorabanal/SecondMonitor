﻿namespace SecondMonitor.Timing.Presentation.ViewModel.Commands
{
    using System;
    using System.Windows.Input;

    public class NoArgumentCommand : ICommand
    {        
        private readonly Action _executeDelegate;

        private readonly Func<bool> _canExecuteDelegate;

        public event EventHandler CanExecuteChanged;

        public NoArgumentCommand(Action execute)
        {
            _executeDelegate = execute;
            _canExecuteDelegate = () => true;
        }

        public NoArgumentCommand(Action execute, Func<bool> canExecute)
        {
            _executeDelegate = execute;
            _canExecuteDelegate = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecuteDelegate();
        }

        public void Execute(object parameter)
        {
            _executeDelegate();
        }
    }
}