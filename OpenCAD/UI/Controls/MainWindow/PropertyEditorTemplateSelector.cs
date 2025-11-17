using System;
using System.Windows;
using System.Windows.Controls;

namespace UI.Controls.MainWindow
{
	/// <summary>
	/// Selects the appropriate editor template based on property type
	/// </summary>
	public class PropertyEditorTemplateSelector : DataTemplateSelector
	{
		public DataTemplate? EnumEditorTemplate { get; set; }
		public DataTemplate? BooleanEditorTemplate { get; set; }
		public DataTemplate? ColorEditorTemplate { get; set; } // Add this
		public DataTemplate? DefaultEditorTemplate { get; set; }
		public DataTemplate? ReadOnlyTemplate { get; set; }

		public override DataTemplate? SelectTemplate(object item, DependencyObject container)
		{
			if (item is PropertyItem prop)
			{
				// Read-only properties get a simple TextBlock
				if (prop.IsReadOnly)
					return ReadOnlyTemplate ?? DefaultEditorTemplate;

				// Color properties get a ColorPicker
				if (prop.IsColor)
					return ColorEditorTemplate;

				// Enum properties get a ComboBox
				if (prop.IsEnum)
					return EnumEditorTemplate;

				// Boolean properties get a CheckBox
				if (prop.IsBoolean)
					return BooleanEditorTemplate;
			}

			// Default to TextBox for string/numeric types
			return DefaultEditorTemplate;
		}
	}
}