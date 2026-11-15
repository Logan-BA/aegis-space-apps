using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SpaceApps.Bus;

namespace SpaceApps.Assistant
{
    /// <summary>
    /// Cliente del servidor local de Ollama (http://127.0.0.1:11434). Todo corre en la PC:
    /// sin internet y sin costo por uso. Endpoints: GET /api/tags (modelos instalados)
    /// y POST /api/chat (conversación).
    /// </summary>
    public class OllamaClient
    {
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        public string BaseUrl = "http://127.0.0.1:11434";
        public bool Available { get; private set; }
        volatile List<string> _models = new List<string>();
        /// <summary>Copia de la lista de modelos instalados (se reemplaza entera: segura entre hilos).</summary>
        public List<string> Models => _models;
        public string LastError = "";

        public async Task<bool> Refresh()
        {
            try
            {
                var json = await Http.GetStringAsync(BaseUrl + "/api/tags").ConfigureAwait(false);
                var root = MiniJson.Parse(json) as Dictionary<string, object>;
                var found = new List<string>();
                if (root != null && root.TryGetValue("models", out var m) && m is List<object> list)
                    foreach (var item in list)
                        if (item is Dictionary<string, object> d && d.TryGetValue("name", out var n) && n != null) found.Add(n.ToString());
                _models = found;
                Available = true;
                LastError = "";
            }
            catch (Exception e) { Available = false; LastError = e.Message; }
            return Available;
        }

        /// <summary>Elige el primer modelo disponible de la lista de preferencia.</summary>
        public string Pick(params string[] preferred)
        {
            var models = _models;
            foreach (var p in preferred ?? new string[0])
            {
                if (string.IsNullOrEmpty(p)) continue;
                foreach (var m in models)
                    if (m != null && (m == p || m.StartsWith(p + ":"))) return m;
            }
            return models.Count > 0 ? models[0] : null;
        }

        /// <summary>Envía la conversación y devuelve el texto de la respuesta.</summary>
        public async Task<string> Chat(string model, List<(string role, string content)> messages, float temperature, int maxTokens)
        {
            var sb = new StringBuilder();
            sb.Append("{\"model\":").Append(Q(model)).Append(",\"stream\":false,\"keep_alive\":\"30m\",");
            sb.Append("\"options\":{\"temperature\":").Append(temperature.ToString("0.##", CultureInfo.InvariantCulture))
              .Append(",\"num_ctx\":4096,\"num_predict\":").Append(maxTokens).Append("},\"messages\":[");
            for (int i = 0; i < messages.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"role\":").Append(Q(messages[i].role)).Append(",\"content\":").Append(Q(messages[i].content)).Append('}');
            }
            sb.Append("]}");
            var resp = await Http.PostAsync(BaseUrl + "/api/chat", new StringContent(sb.ToString(), Encoding.UTF8, "application/json")).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) throw new Exception($"Ollama {(int)resp.StatusCode}: {body}");
            var root = MiniJson.Parse(body);
            return MiniJson.Text(MiniJson.Path(root, "message.content")) ?? "";
        }

        public static string Q(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
