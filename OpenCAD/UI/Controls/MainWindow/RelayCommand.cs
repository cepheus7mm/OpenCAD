using System;
using System.Windows.Input;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// A command implementation that relays execution to delegates
	/// </summary>
	public class RelayCommand : ICommand
	{
		private readonly Action _execute;
		private readonly Func<bool>? _canExecute;

		public event EventHandler? CanExecuteChanged
		{
			add => CommandManager.RequerySuggested += value;
			remove => CommandManager.RequerySuggested -= value;
		}

		/// <summary>
		/// Creates a new RelayCommand that can always execute
		/// </summary>
		public RelayCommand(Action execute)
			: this(execute, null)
		{
		}

		/// <summary>
		/// Creates a new RelayCommand with a conditional execution
		/// </summary>
		public RelayCommand(Action execute, Func<bool>? canExecute)
		{
			_execute = execute ?? throw new ArgumentNullException(nameof(execute));
			_canExecute = canExecute;
		}

		public bool CanExecute(object? parameter)
		{
			return _canExecute == null || _canExecute();
		}

		public void Execute(object? parameter)
		{
			_execute();
		}

		/// <summary>
		/// Raises the CanExecuteChanged event
		/// </summary>
		public void RaiseCanExecuteChanged()
		{
			CommandManager.InvalidateRequerySuggested();
		}
	}
}