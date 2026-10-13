using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Arrows.Model.Serialization
{
    /// <summary>
    /// Reads and writes the level JSON format:
    /// { "width": 6, "height": 6, "arrows": [ { "cells": [[0,0],[1,0]], "color": "#FF8800" } ] }
    /// The head is the last cell; the direction is the difference of the last two cells.
    /// </summary>
    public static class LevelSerializer
    {
        public static LevelData FromJson(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root))
                throw new FormatException("Level JSON root must be an object.");

            var data = new LevelData
            {
                Width = ReadInt(root, "width"),
                Height = ReadInt(root, "height"),
            };

            if (!root.TryGetValue("arrows", out object arrowsObj) || !(arrowsObj is List<object> arrows))
                throw new FormatException("Level JSON needs an 'arrows' array.");

            for (int a = 0; a < arrows.Count; a++)
            {
                if (!(arrows[a] is Dictionary<string, object> arrowObj))
                    throw new FormatException($"Arrow {a} must be an object.");
                if (!arrowObj.TryGetValue("cells", out object cellsObj) || !(cellsObj is List<object> cells))
                    throw new FormatException($"Arrow {a} needs a 'cells' array.");

                var arrow = new ArrowData();
                foreach (object cellObj in cells)
                {
                    if (!(cellObj is List<object> pair) || pair.Count != 2)
                        throw new FormatException($"Arrow {a}: each cell must be [x,y].");
                    arrow.Cells.Add(new GridPos(ToInt(pair[0]), ToInt(pair[1])));
                }

                if (arrowObj.TryGetValue("color", out object color) && color is string hex)
                    arrow.Color = hex;

                data.Arrows.Add(arrow);
            }

            return data;
        }

        /// <summary>Writes compact-but-readable JSON: one line per arrow.</summary>
        public static string ToJson(LevelData data)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"width\": ").Append(data.Width.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"height\": ").Append(data.Height.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"arrows\": [");

            for (int a = 0; a < data.Arrows.Count; a++)
            {
                var arrow = data.Arrows[a];
                sb.Append(a == 0 ? "\n" : ",\n");
                sb.Append("    { \"cells\": [");
                for (int i = 0; i < arrow.Cells.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append('[').Append(arrow.Cells[i].X).Append(',').Append(arrow.Cells[i].Y).Append(']');
                }
                sb.Append(']');
                if (!string.IsNullOrEmpty(arrow.Color)) sb.Append(", \"color\": \"").Append(arrow.Color).Append('"');
                sb.Append(" }");
            }

            sb.Append(data.Arrows.Count > 0 ? "\n  ]\n}\n" : "]\n}\n");
            return sb.ToString();
        }

        private static int ReadInt(Dictionary<string, object> obj, string key)
        {
            if (!obj.TryGetValue(key, out object value)) throw new FormatException($"Missing '{key}'.");
            return ToInt(value);
        }

        private static int ToInt(object value)
        {
            if (value is double d && Math.Abs(d - Math.Round(d)) < 1e-9) return (int)Math.Round(d);
            throw new FormatException($"Expected an integer, got '{value}'.");
        }
    }
}
