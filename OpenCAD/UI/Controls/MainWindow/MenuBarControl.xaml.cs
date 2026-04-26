using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace UI.Controls.MainWindow
{
    /// <summary>
    /// Interaction logic for MenuBarControl.xaml
    /// </summary>
    public partial class MenuBarControl : UserControl
    {
        // Events to notify the main window of menu actions
        public event EventHandler? NewFileRequested;
        public event EventHandler? ExitRequested;
        public event EventHandler? LightThemeRequested;
        public event EventHandler? DarkThemeRequested;
        public event EventHandler? NewViewportRequested;
        public event EventHandler<bool>? LayersVisibilityChanged;
        public event EventHandler<bool>? SettingsVisibilityChanged;
        public event EventHandler<bool>? TextStylesVisibilityChanged;
        public event EventHandler? SaveAsRequested;

        /// <summary>
        /// Raised when a JSON-defined command menu item is clicked.
        /// The string argument is the full command string, e.g. "ARC StartCenterEnd".
        /// </summary>
        public event EventHandler<string>? CommandRequested;

        public MenuBarControl()
        {
            InitializeComponent();
            LoadDynamicMenus();
        }

        /// <summary>
        /// Reads menus.json and injects top-level menus (Draw, Modify, etc.)
        /// before the Help menu.
        /// </summary>
        private void LoadDynamicMenus()
        {
            try
            {
                var definition = MenuLoader.Load();
                var commands = BuildCommandLookup(definition);
                var helpItem = FindHelpMenuItem();

                foreach (var group in definition.MenuBar)
                {
                    var menuItem = BuildTopLevelMenuItem(group, commands);

                    if (helpItem != null)
                        mainMenu.Items.Insert(mainMenu.Items.IndexOf(helpItem), menuItem);
                    else
                        mainMenu.Items.Add(menuItem);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MenuBarControl] Failed to load menus.json: {ex.Message}");
            }
        }

        private static Dictionary<string, CommandDefinition> BuildCommandLookup(AppDefinition definition)
        {
            var lookup = new Dictionary<string, CommandDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var cmd in definition.Commands)
                lookup[cmd.Id] = cmd;
            return lookup;
        }

        private MenuItem? FindHelpMenuItem()
        {
            foreach (var item in mainMenu.Items)
            {
                if (item is MenuItem mi && mi.Header is string h && h == "_Help")
                    return mi;
            }
            return null;
        }

        private MenuItem BuildTopLevelMenuItem(MenuBarGroupDefinition group, Dictionary<string, CommandDefinition> commands)
        {
            var root = new MenuItem { Header = $"_{group.Label}" };

            foreach (var item in group.Items)
                root.Items.Add(BuildMenuItem(item, commands));

            return root;
        }

        private Control BuildMenuItem(MenuBarItemDefinition def, Dictionary<string, CommandDefinition> commands)
        {
            if (def.Separator)
                return new Separator();

            if (def.HasSubItems)
            {
                var subMenu = new MenuItem { Header = def.Label };
                foreach (var child in def.Items!)
                    subMenu.Items.Add(BuildMenuItem(child, commands));
                return subMenu;
            }

            if (def.CommandId != null && commands.TryGetValue(def.CommandId, out var cmd))
            {
                var item = new MenuItem { Header = cmd.Label };

                if (!string.IsNullOrEmpty(cmd.Icon))
                    item.Icon = BuildIcon(cmd.Icon);

                string fullCommand = cmd.FullCommandString;
                item.Click += (_, _) => CommandRequested?.Invoke(this, fullCommand);
                return item;
            }

            return new MenuItem { Header = def.Label ?? def.CommandId, IsEnabled = false };
        }

        private static Image BuildIcon(string iconPath)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/Resources/{iconPath}");
                return new Image
                {
                    Source = new BitmapImage(uri),
                    Width = 16,
                    Height = 16
                };
            }
            catch
            {
                return new Image();
            }
        }

        #region Existing click handlers

        private void NewFile_Click(object sender, RoutedEventArgs e) =>
            NewFileRequested?.Invoke(this, EventArgs.Empty);

        private void Exit_Click(object sender, RoutedEventArgs e) =>
            ExitRequested?.Invoke(this, EventArgs.Empty);

        private void LightTheme_Click(object sender, RoutedEventArgs e)
        {
            LightThemeMenuItem.IsChecked = true;
            DarkThemeMenuItem.IsChecked = false;
            LightThemeRequested?.Invoke(this, EventArgs.Empty);
        }

        private void DarkTheme_Click(object sender, RoutedEventArgs e)
        {
            LightThemeMenuItem.IsChecked = false;
            DarkThemeMenuItem.IsChecked = true;
            DarkThemeRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Layers_Click(object sender, RoutedEventArgs e) =>
            LayersVisibilityChanged?.Invoke(this, LayersMenuItem.IsChecked);

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            SettingsVisibilityChanged?.Invoke(this, !SettingsMenuItem.IsChecked);
            if (sender is MenuItem menuItem)
                SettingsMenuItem.IsChecked = menuItem.IsChecked;
        }

        private void TextStyles_Click(object sender, RoutedEventArgs e) =>
            TextStylesVisibilityChanged?.Invoke(this, TextStylesMenuItem.IsChecked);

        #endregion

        /// <summary>
        /// Keeps the Theme menu checkmarks in sync when theme is changed programmatically.
        /// </summary>
        public void UpdateThemeSelection(bool isLightTheme)
        {
            LightThemeMenuItem.IsChecked = isLightTheme;
            DarkThemeMenuItem.IsChecked = !isLightTheme;
        }

        /// <summary>
        /// Keeps the Layers menu checkmark in sync when visibility is changed programmatically.
        /// </summary>
        public void UpdateLayersVisibility(bool isVisible)
        {
            LayersMenuItem.IsChecked = isVisible;
        }
    }
}
