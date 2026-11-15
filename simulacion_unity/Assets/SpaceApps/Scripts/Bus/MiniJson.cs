using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SpaceApps.Bus
{
    /// <summary>
    /// Lector JSON mínimo sin dependencias: objetos → Dictionary&lt;string, object&gt;, arreglos → List&lt;object&gt;,
    /// números → double, más string, bool y null. Sirve para mensajes del bus cuyo esquema puede cambiar.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string s)
        {
            int i = 0;
            Skip(s, ref i);
            var v = Value(s, ref i);
            return v;
        }

        static object Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) throw new System.FormatException("JSON incompleto");
            char c = s[i];
            switch (c)
            {
                case '{': return Obj(s, ref i);
                case '[': return Arr(s, ref i);
                case '"': return Str(s, ref i);
                case 't': i += 4; return true;
                case 'f': i += 5; return false;
                case 'n': i += 4; return null;
                default: return Num(s, ref i);
            }
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            Skip(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Skip(s, ref i);
                string k = Str(s, ref i);
                Skip(s, ref i);
                i++; // ':'
                d[k] = Value(s, ref i);
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; // '}'
                return d;
            }
        }

        static List<object> Arr(string s, ref int i)
        {
            var l = new List<object>();
            i++;
            Skip(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; // ']'
                return l;
            }
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // '"'
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            return sb.ToString();
        }

        static object Num(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        // ------------------------------------------------------------ acceso por ruta

        /// <summary>Resuelve "a.b.0.c" (los números indexan arreglos).</summary>
        public static object Path(object root, string path)
        {
            object cur = root;
            foreach (var part in path.Split('.'))
            {
                if (cur is Dictionary<string, object> d)
                {
                    if (!d.TryGetValue(part, out cur)) return null;
                }
                else if (cur is List<object> l && int.TryParse(part, out int idx))
                {
                    if (idx < 0 || idx >= l.Count) return null;
                    cur = l[idx];
                }
                else return null;
            }
            return cur;
        }

        /// <summary>Primer valor encontrado entre varias rutas; si ninguna sirve, busca la clave en todo el árbol.</summary>
        public static object First(object root, IList<string> paths, bool deepSearch = true)
        {
            if (paths == null) return null;
            foreach (var p in paths)
            {
                var v = Path(root, p);
                if (v != null) return v;
            }
            if (!deepSearch) return null;
            foreach (var p in paths)
            {
                if (p.Contains(".")) continue;
                var v = FindKey(root, p, 0);
                if (v != null) return v;
            }
            return null;
        }

        static object FindKey(object node, string key, int depth)
        {
            if (depth > 6 || node == null) return null;
            if (node is Dictionary<string, object> d)
            {
                if (d.TryGetValue(key, out var v) && v != null) return v;
                foreach (var kv in d)
                {
                    var r = FindKey(kv.Value, key, depth + 1);
                    if (r != null) return r;
                }
            }
            else if (node is List<object> l && l.Count > 0 && !(l[0] is Dictionary<string, object> first && LooksLikeCrew(first)))
            {
                foreach (var item in l)
                {
                    var r = FindKey(item, key, depth + 1);
                    if (r != null) return r;
                }
            }
            return null;
        }

        // Evita tomar el valor de otro astronauta al buscar claves globales dentro de una lista de tripulantes
        static bool LooksLikeCrew(Dictionary<string, object> d) => d.ContainsKey("id") && (d.ContainsKey("name") || d.ContainsKey("nombre"));

        public static double? Number(object v)
        {
            switch (v)
            {
                case double d: return d;
                case bool b: return b ? 1 : 0;
                case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var r): return r;
                case Dictionary<string, object> o:
                    foreach (var k in new[] { "value", "valor", "v" })
                        if (o.TryGetValue(k, out var inner)) return Number(inner);
                    return null;
                default: return null;
            }
        }

        public static string Text(object v) => v == null ? null : v is double d ? d.ToString(CultureInfo.InvariantCulture) : v.ToString();
    }
}
