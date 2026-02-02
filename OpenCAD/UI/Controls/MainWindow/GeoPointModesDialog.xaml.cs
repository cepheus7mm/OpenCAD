using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OpenCAD.Geometry.Helpers.GeoPoints;

namespace UI.Controls.MainWindow
{
    public partial class GeoPointModesDialog : Window
    {
        public HashSet<GeoPointModes> SelectedModes { get; } = new();

        public GeoPointModesDialog(GeoPointModes initialSelection)
        {
            InitializeComponent();
            foreach (GeoPointModes mode in Enum.GetValues(typeof(GeoPointModes)))
            {
                var cb = new CheckBox
                {
                    Content = mode.ToString(),
                    IsChecked = IsModeSelected(initialSelection, mode),
                    Tag = mode,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                ModesItemsControl.Items.Add(cb);
            }
        }

        private bool IsModeSelected(GeoPointModes initialSelection, GeoPointModes mode)
        {
            if (mode == GeoPointModes.None && 0 < (uint)initialSelection)
                return false;

            return initialSelection.HasFlag(mode);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedModes.Clear();
            foreach (CheckBox cb in ModesItemsControl.Items)
            {
                if (cb.IsChecked == true && cb.Tag is GeoPointModes mode)
                    SelectedModes.Add(mode);
            }
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}