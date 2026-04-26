using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UI.Controls.MainWindow
{
    /// <summary>
    /// Interaction logic for ToolBarControl.xaml
    /// </summary>
    public partial class ToolBarControl : UserControl
    {
        public event EventHandler? NewFileRequested;
        public event EventHandler? OpenRequested;
        public event EventHandler? SaveRequested;
        public event EventHandler? CutRequested;
        public event EventHandler? CopyRequested;
        public event EventHandler? PasteRequested;

        /// <summary>
        /// Raised when a JSON-defined command toolbar button is clicked.
        /// The string argument is the full command string, e.g. "CIRCLE CenterRadius".
        /// </summary>
        public event EventHandler<string>? CommandRequested;

        public ToolBarControl()
        {
            InitializeComponent();
            // Defer dynamic loading until the control is in the visual tree
            // so DynamicResource lookups resolve correctly.
            Loaded += (_, _) => LoadDynamicToolBars();
        }

        /// <summary>
        /// Reads menus.json and creates one ToolBar per top-level menu on Band 2.
        /// </summary>
        private void LoadDynamicToolBars()
        {
            try
            {
                var definition = MenuLoader.Load();
                var commands = BuildCommandLookup(definition);

                int bandIndex = 1;
                foreach (var group in definition.ToolBar)
                {
                    var toolBar = new ToolBar
                    {
                        Band = 2,
                        BandIndex = bandIndex++
                    };
                    toolBar.SetResourceReference(ToolBar.BackgroundProperty, "ToolBarBandBackgroundBrush");

                    bool pendingSeparator = false;
                    foreach (var item in group.Items)
                        AddToolBarItem(toolBar, item, commands, ref pendingSeparator);

                    if (toolBar.Items.Count > 0)
                        toolBarTray.ToolBars.Add(toolBar);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ToolBarControl] Failed to load menus.json: {ex.Message}");
            }
        }

        private static Dictionary<string, CommandDefinition> BuildCommandLookup(AppDefinition definition)
        {
            var lookup = new Dictionary<string, CommandDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var cmd in definition.Commands)
                lookup[cmd.Id] = cmd;
            return lookup;
        }

        private void AddToolBarItem(ToolBar toolBar, ToolBarItemDefinition def, Dictionary<string, CommandDefinition> commands, ref bool pendingSeparator)
        {
            if (def.Separator)
            {
                pendingSeparator = toolBar.Items.Count > 0;
                return;
            }

            if (def.CommandId == null || !commands.TryGetValue(def.CommandId, out var cmd))
                return;

            if (pendingSeparator)
            {
                toolBar.Items.Add(new Separator());
                pendingSeparator = false;
            }

            string fullCommand = cmd.FullCommandString;

            var button = new Button
            {
                ToolTip = cmd.Label,
                Content = BuildToolBarIcon(cmd.Icon)
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "ToolBarButtonStyle");

            button.Click += (_, _) => CommandRequested?.Invoke(this, fullCommand);
            toolBar.Items.Add(button);
        }

        private static Image BuildToolBarIcon(string? iconPath)
        {
            if (!string.IsNullOrEmpty(iconPath))
            {
                try
                {
                    return new Image
                    {
                        Source = new BitmapImage(new Uri($"pack://application:,,,/Resources/{iconPath}")),
                        Width = 24,
                        Height = 24
                    };
                }
                catch { /* fall through to blank image */ }
            }

            return new Image { Width = 24, Height = 24 };
        }

        #region Existing click handlers

        private void NewFile_Click(object sender, RoutedEventArgs e) =>
            NewFileRequested?.Invoke(this, EventArgs.Empty);

        private void Open_Click(object sender, RoutedEventArgs e) =>
            OpenRequested?.Invoke(this, EventArgs.Empty);

        private void Save_Click(object sender, RoutedEventArgs e) =>
            SaveRequested?.Invoke(this, EventArgs.Empty);

        private void Cut_Click(object sender, RoutedEventArgs e) =>
            CutRequested?.Invoke(this, EventArgs.Empty);

        private void Copy_Click(object sender, RoutedEventArgs e) =>
            CopyRequested?.Invoke(this, EventArgs.Empty);

        private void Paste_Click(object sender, RoutedEventArgs e) =>
            PasteRequested?.Invoke(this, EventArgs.Empty);

        #endregion
    }
}
