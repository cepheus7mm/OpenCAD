using OpenCAD;
using OpenCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UI.Commands.InputHelpers
{
    public static class PromptBuilder
    {
        public static string Build(InputParams inputParams, Point3D? lastPoint)
        {
            string display = inputParams.Prompt;

            // Keywords with AutoCAD-style capitalization
            if (inputParams.Keywords is { Length: > 0 } kws)
                display += FormatKeywords(kws);

            // Default value <formatted>
            display += FormatDefault(inputParams);

            // Last point <x,y,z>
            if (inputParams.AllowLastPoint && lastPoint != null)
            {
                return string.Format(
                    OpenCADStrings.PromptWithLastPointFormat,
                    display,
                    lastPoint.Value.X,
                    lastPoint.Value.Y,
                    lastPoint.Value.Z);
            }

            return string.Format(OpenCADStrings.PromptWithViewportFormat, display);
        }

        // --------------------------
        // Keyword formatting helpers
        // --------------------------

        private static string FormatKeywords(string[] keywords)
        {
            var formatted = new List<string>();

            foreach (var kw in keywords)
            {
                string prefix = GetMinimumUniquePrefix(kw, keywords);
                string rest = kw.Substring(prefix.Length);
                formatted.Add(prefix.ToUpperInvariant() + rest.ToLowerInvariant());
            }

            return $" [{string.Join("/", formatted)}]";
        }

        private static string GetMinimumUniquePrefix(string keyword, string[] all)
        {
            for (int len = 1; len <= keyword.Length; len++)
            {
                string prefix = keyword.Substring(0, len);
                bool unique = all.Count(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) == 1;

                if (unique)
                    return prefix;
            }

            return keyword;
        }

        // --------------------------
        // Default value formatting
        // --------------------------

        private static string FormatDefault(InputParams inputParams)
        {
            if (inputParams.DefaultValue == null)
                return string.Empty;

            if (inputParams.Context != null && inputParams.UnitFormatType != null && inputParams.DefaultValue is double def)
            {
                string formatted = inputParams.Context.GetDocument()?.ValueToString(def, inputParams.UnitFormatType.Value) ?? string.Empty;
                if (!string.IsNullOrEmpty(formatted))
                    return $" <{formatted}>";
            }

            // Fallback for non-numeric defaults (e.g. enum values, strings)
            string text = inputParams.DefaultValue.ToString() ?? string.Empty;
            if (!string.IsNullOrEmpty(text))
                return $" <{text}>";

            return string.Empty;
        }
    }
}
