using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using DrawingColor = System.Drawing.Color;

namespace UI.Converters
{
	/// <summary>
	/// Shows "ByLayer" text when color is #00000000
	/// </summary>
	public class ColorToByLayerVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is DrawingColor color)
			{
				return (color.A == 0 && color.R == 0 && color.G == 0 && color.B == 0) 
					? Visibility.Visible 
					: Visibility.Collapsed;
			}
			return Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// Shows color picker when color is NOT #00000000
	/// </summary>
	public class ColorToPickerVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is DrawingColor color)
			{
				return (color.A == 0 && color.R == 0 && color.G == 0 && color.B == 0) 
					? Visibility.Collapsed 
					: Visibility.Visible;
			}
			return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}