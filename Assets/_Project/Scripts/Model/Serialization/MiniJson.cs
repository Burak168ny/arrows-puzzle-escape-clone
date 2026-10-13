using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Arrows.Model.Serialization
{
    /// <summary>
    /// Minimal JSON reader producing Dictionary&lt;string, object&gt;, List&lt;object&gt;, double, string, bool and null.
    /// Unity's JsonUtility cannot read nested arrays like [[x,y]], which the level format uses.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var parser = new Parser(json);
            object value = parser.ParseValue();
            parser.SkipWhitespace();
            if (!parser.AtEnd) throw parser.Error("Unexpected trailing characters");
            return value;
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s) => _s = s;

            public bool AtEnd => _i >= _s.Length;

            public FormatException Error(string message) =>
                new FormatException($"{message} at position {_i}.");

            public void SkipWhitespace()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (AtEnd) throw Error("Unexpected end of input");

                char c = _s[_i];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || char.IsDigit(c)) return ParseNumber();
                        throw Error($"Unexpected character '{c}'");
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var obj = new Dictionary<string, object>();
                _i++; // {
                SkipWhitespace();
                if (Peek() == '}') { _i++; return obj; }

                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"') throw Error("Expected property name");
                    string key = ParseString();
                    SkipWhitespace();
                    if (Next() != ':') throw Error("Expected ':'");
                    obj[key] = ParseValue();
                    SkipWhitespace();
                    char c = Next();
                    if (c == '}') return obj;
                    if (c != ',') throw Error("Expected ',' or '}'");
                }
            }

            private List<object> ParseArray()
            {
                var list = new List<object>();
                _i++; // [
                SkipWhitespace();
                if (Peek() == ']') { _i++; return list; }

                while (true)
                {
                    list.Add(ParseValue());
                    SkipWhitespace();
                    char c = Next();
                    if (c == ']') return list;
                    if (c != ',') throw Error("Expected ',' or ']'");
                }
            }

            private string ParseString()
            {
                var sb = new StringBuilder();
                _i++; // opening quote
                while (true)
                {
                    if (AtEnd) throw Error("Unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }

                    if (AtEnd) throw Error("Unterminated escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Error("Bad unicode escape");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber));
                            _i += 4;
                            break;
                        default: throw Error($"Bad escape '\\{e}'");
                    }
                }
            }

            private double ParseNumber()
            {
                int start = _i;
                while (_i < _s.Length && "+-0123456789.eE".IndexOf(_s[_i]) >= 0) _i++;
                string token = _s.Substring(start, _i - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    throw Error($"Bad number '{token}'");
                return d;
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Error($"Expected '{word}'");
                _i += word.Length;
            }

            private char Peek() => AtEnd ? '\0' : _s[_i];
            private char Next() => AtEnd ? '\0' : _s[_i++];
        }
    }
}
