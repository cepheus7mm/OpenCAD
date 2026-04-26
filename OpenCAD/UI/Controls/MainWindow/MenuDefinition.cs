using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UI.Controls.MainWindow
{
    /// <summary>Root object for menus.json.</summary>
    public class AppDefinition
    {
        [JsonPropertyName("commands")]
        public List<CommandDefinition> Commands { get; set; } = new();

        [JsonPropertyName("menuBar")]
        public List<MenuBarGroupDefinition> MenuBar { get; set; } = new();

        [JsonPropertyName("toolBar")]
        public List<ToolBarGroupDefinition> ToolBar { get; set; } = new();
    }

    /// <summary>Defines a command — its identity, label, icon and dispatch string.</summary>
    public class CommandDefinition
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        [JsonPropertyName("entryPoint")]
        public string? EntryPoint { get; set; }

        [JsonIgnore]
        public string FullCommandString =>
            string.IsNullOrEmpty(EntryPoint) ? Command : $"{Command} {EntryPoint}";
    }

    /// <summary>A top-level menu (e.g. "Draw") in the menu bar placement.</summary>
    public class MenuBarGroupDefinition
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("items")]
        public List<MenuBarItemDefinition> Items { get; set; } = new();
    }

    /// <summary>A placement entry in a menu bar group — a command reference, sub-menu, or separator.</summary>
    public class MenuBarItemDefinition
    {
        [JsonPropertyName("separator")]
        public bool Separator { get; set; }

        [JsonPropertyName("commandId")]
        public string? CommandId { get; set; }

        /// <summary>Set when this item is a sub-menu grouping (no commandId).</summary>
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("items")]
        public List<MenuBarItemDefinition>? Items { get; set; }

        [JsonIgnore]
        public bool HasSubItems => Items != null && Items.Count > 0;
    }

    /// <summary>A toolbar band (e.g. "Draw") in the toolbar placement.</summary>
    public class ToolBarGroupDefinition
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("items")]
        public List<ToolBarItemDefinition> Items { get; set; } = new();
    }

    /// <summary>A placement entry in a toolbar group — a command reference or separator.</summary>
    public class ToolBarItemDefinition
    {
        [JsonPropertyName("separator")]
        public bool Separator { get; set; }

        [JsonPropertyName("commandId")]
        public string? CommandId { get; set; }
    }
}