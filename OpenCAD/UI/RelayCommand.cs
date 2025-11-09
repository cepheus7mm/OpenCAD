using System;
using System.Windows.Input;

namespace UI
{
	/// <summary>
	/// A simple ICommand implementation that relays its functionality to delegates
	/// </summary>
	public class RelayCommand : ICommand
	{
		private readonly Action _execute;
		private readonly Func<bool>? _canExecute;

		/// <summary>
		/// Creates a new RelayCommand
		/// </summary>
		/// <param name="execute">The execution logic</param>
		/// <param name="canExecute">The execution status logic (optional)</param>
		public RelayCommand(Action execute, Func<bool>? canExecute = null)
		{
			_execute = execute ?? throw new ArgumentNullException(nameof(execute));
			_canExecute = canExecute;
		}

		/// <summary>
		/// Occurs when changes occur that affect whether the command should execute
		/// </summary>
		public event EventHandler? CanExecuteChanged
		{
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}

		/// <summary>
		/// Determines whether the command can execute
		/// </summary>
		public bool CanExecute(object? parameter)
		{
			return _canExecute == null || _canExecute();
		}

		/// <summary>
		/// Executes the command
		/// </summary>
		public void Execute(object? parameter)
		{
			_execute();
		}
	}

	/// <summary>
	/// A generic ICommand implementation that accepts a parameter
	/// </summary>
	public class RelayCommand<T> : ICommand
	{
		private readonly Action<T?> _execute;
		private readonly Predicate<T?>? _canExecute;

		/// <summary>
		/// Creates a new RelayCommand with a parameter
		/// </summary>
		/// <param name="execute">The execution logic</param>
		/// <param name="canExecute">The execution status logic (optional)</param>
		public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
		{
			_execute = execute ?? throw new ArgumentNullException(nameof(execute));
			_canExecute = canExecute;
		}

		/// <summary>
		/// Occurs when changes occur that affect whether the command should execute
		/// </summary>
		public event EventHandler? CanExecuteChanged
		{
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}

		/// <summary>
		/// Determines whether the command can execute
		/// </summary>
		public bool CanExecute(object? parameter)
		{
			return _canExecute == null || _canExecute((T?)parameter);
		}

		/// <summary>
		/// Executes the command
		/// </summary>
		public void Execute(object? parameter)
		{
			_execute((T?)parameter);
		}
	}
}