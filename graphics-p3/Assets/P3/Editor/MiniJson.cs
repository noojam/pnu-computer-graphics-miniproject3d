// A tiny JSON reader for the Editor tools (scene.json and sounds.json use plain objects as
// dictionaries, which JsonUtility cannot read). Objects become Dictionary<string, object>,
// arrays List<object>, numbers double, plus string, bool and null.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace P3.EditorTools
{
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            var v = Value(text, ref i);
            Skip(text, ref i);
            if (i != text.Length) throw new FormatException($"extra text at {i}");
            return v;
        }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end");
            char ch = s[i];
            if (ch == '{') return Obj(s, ref i);
            if (ch == '[') return Arr(s, ref i);
            if (ch == '"') return Str(s, ref i);
            if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
            if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
            if (s.Substring(i).StartsWith("null")) { i += 4; return null; }
            return Num(s, ref i);
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;   // {
            Skip(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Skip(s, ref i);
                string key = Str(s, ref i);
                Skip(s, ref i);
                if (s[i] != ':') throw new FormatException($"':' expected at {i}");
                i++;
                d[key] = Value(s, ref i);
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException($"',' or '}}' expected at {i}");
            }
        }

        static List<object> Arr(string s, ref int i)
        {
            var a = new List<object>();
            i++;   // [
            Skip(s, ref i);
            if (s[i] == ']') { i++; return a; }
            while (true)
            {
                a.Add(Value(s, ref i));
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return a; }
                throw new FormatException($"',' or ']' expected at {i}");
            }
        }

        static string Str(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException($"string expected at {i}");
            i++;
            var sb = new StringBuilder();
            while (s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            i++;
            return sb.ToString();
        }

        static double Num(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }
    }
}
