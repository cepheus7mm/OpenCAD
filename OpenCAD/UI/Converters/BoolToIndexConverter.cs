using System;
using System.Globalization;
using System.Windows.Data;

namespace UI.Converters
{
	/// <summary>
	/// Converts a boolean value to a ComboBox index.
	/// True (ByLayer) -> 0, False (Custom) -> 1
	/// </summary>
	public class BoolToIndexConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is bool boolValue)
			{
				return boolValue ? 0 : 1; // ByLayer = 0, Custom = 1
			}
			return 1; // Default to Custom
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is int index)
			{
				return index == 0; // 0 = ByLayer (true), 1 = Custom (false)
			}
			return false; // Default to Custom
		}
	}
}