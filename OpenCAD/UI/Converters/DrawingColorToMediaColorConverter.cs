using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UI.Converters
{
	/// <summary>
	/// Converts System.Drawing.Color to System.Windows.Media.Color for WPF binding
	/// </summary>
	public class DrawingColorToMediaColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is System.Drawing.Color drawingColor)
			{
				return Color.FromArgb(
					drawingColor.A,
					drawingColor.R,
					drawingColor.G,
					drawingColor.B);
			}
			
			// Default to white if conversion fails
			return Colors.White;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is Color mediaColor)
			{
				return System.Drawing.Color.FromArgb(
					mediaColor.A,
					mediaColor.R,
					mediaColor.G,
					mediaColor.B);
			}
			
			return System.Drawing.Color.White;
		}
	}
}