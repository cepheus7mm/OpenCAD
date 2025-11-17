using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DrawingColor = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;

namespace UI.Converters
{
	/// <summary>
	/// Converts System.Drawing.Color to System.Windows.Media.Color for WPF binding
	/// </summary>
	public class DrawingColorToMediaColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is DrawingColor drawingColor)
			{
				// Special case: #00000000 means "ByLayer" - show as gray to indicate special state
				if (drawingColor.A == 0 && drawingColor.R == 0 && drawingColor.G == 0 && drawingColor.B == 0)
				{
					return Colors.LightGray; // Visual indicator that it's "ByLayer"
				}
				
				return MediaColor.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);
			}
			return Colors.White;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is MediaColor mediaColor)
			{
				return DrawingColor.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);
			}
			return DrawingColor.White;
		}
	}
}