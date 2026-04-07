using System;
using System.Text;
using System.Collections.Generic;

namespace ClaudeAdvisor
{
    public class JsonHelper
    {
        public static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "").Replace("\t", "\\t");
        }

        public static string ToJson(Dictionary<string, object> dict)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            bool first = true;
            foreach (var kv in dict)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append("\"" + Escape(kv.Key) + "\":");
                sb.Append(ValueToJson(kv.Value));
            }
            sb.Append("}");
            return sb.ToString();
        }

        public static string ValueToJson(object val)
        {
            if (val == null) return "null";
            if (val is bool) return (bool)val ? "true" : "false";
            if (val is int) return ((int)val).ToString();
            if (val is uint) return ((uint)val).ToString();
            if (val is long) return ((long)val).ToString();
            if (val is float) return ((float)val).ToString();
            if (val is double) return ((double)val).ToString();
            if (val is string) return "\"" + Escape((string)val) + "\"";
            if (val is Dictionary<string, object>)
                return ToJson((Dictionary<string, object>)val);
            if (val is List<Dictionary<string, object>>)
            {
                var sb = new StringBuilder();
                sb.Append("[");
                var list = (List<Dictionary<string, object>>)val;
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(ToJson(list[i]));
                }
                sb.Append("]");
                return sb.ToString();
            }
            if (val is List<object>)
            {
                var sb = new StringBuilder();
                sb.Append("[");
                var list = (List<object>)val;
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(ValueToJson(list[i]));
                }
                sb.Append("]");
                return sb.ToString();
            }
            return "\"" + Escape(val.ToString()) + "\"";
        }

        public static Dictionary<string, string> ParseSimpleJson(string json)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json)) return result;
            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1);
            if (json.EndsWith("}")) json = json.Substring(0, json.Length - 1);

            bool inString = false;
            bool escaped = false;
            string currentKey = null;
            var token = new StringBuilder();
            bool parsingKey = true;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped) { token.Append(c); escaped = false; continue; }
                if (c == '\\') { escaped = true; token.Append(c); continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) { token.Append(c); continue; }
                if (c == ':') { currentKey = token.ToString().Trim(); token.Length = 0; parsingKey = false; continue; }
                if (c == ',')
                {
                    if (currentKey != null) result[currentKey] = token.ToString().Trim();
                    token.Length = 0; currentKey = null; parsingKey = true; continue;
                }
                if (c == ' ' || c == '\n' || c == '\r' || c == '\t') continue;
                token.Append(c);
            }
            if (currentKey != null) result[currentKey] = token.ToString().Trim();
            return result;
        }
    }
}
