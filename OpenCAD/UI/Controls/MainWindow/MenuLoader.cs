using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace UI.Controls.MainWindow
{
    public static class MenuLoader
    {
        private const string ResourceUri = "pack://application:,,,/Resources/menus.json";

        public static AppDefinition Load()
        {
            var uri = new Uri(ResourceUri);
            var info = Application.GetResourceStream(uri)
                ?? throw new InvalidOperationException($"Menu resource not found: {ResourceUri}");

            using var reader = new StreamReader(info.Stream);
            string json = reader.ReadToEnd();

            return JsonSerializer.Deserialize<AppDefinition>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new AppDefinition();
        }
    }
}