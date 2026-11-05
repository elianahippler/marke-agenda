// Marke Agenda: sincronização PC <-> celular pelo Google Drive.
// O arquivo fica na "pasta do aplicativo" (appDataFolder): escondida, só este app enxerga.
// Senhas e acessos NUNCA vão para o Drive: ficam só no PC.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace MarkeAgenda
{
    public class PacoteSinc
    {
        public int Versao { get; set; }
        public string Gravado { get; set; }
        public string Dispositivo { get; set; }
        public List<Tarefa> Tarefas { get; set; }
        public List<Nota> Notas { get; set; }
        public List<Tipo> Tipos { get; set; }
        public List<EventoDia> Eventos { get; set; }
        public List<Intervalo> Intervalos { get; set; }       // pedacos do dia fora da agenda
        public List<TipoEvento> TiposEvento { get; set; }     // tipos usados pelos eventos
        public List<string> NaoSaoMinhas { get; set; }
        public Dictionary<string, List<string>> HorariosExtras { get; set; }
        public string NotaRapida { get; set; }
        public string NotaRapidaMod { get; set; }
        public Dictionary<string, string> Excluidos { get; set; }
        public Dictionary<string, object> Agenda { get; set; }   // horários do dia (o celular usa o que o PC definiu)
    }

    public static class Sincronia
    {
        const string Arquivo = "marke-agenda.json";

        public static string Agora() { return DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"); }

        static JavaScriptSerializer Json() { JavaScriptSerializer j = new JavaScriptSerializer(); j.MaxJsonLength = int.MaxValue; return j; }

        static string Assinatura(object item)
        {
            // o próprio carimbo não entra na comparação
            System.Reflection.PropertyInfo p = item.GetType().GetProperty("Mod");
            string mod = (string)p.GetValue(item, null);
            p.SetValue(item, null, null);
            string s = Json().Serialize(item);
            p.SetValue(item, mod, null);
            return s;
        }

        static IEnumerable<KeyValuePair<string, object>> Itens(Dados d)
        {
            foreach (Tarefa t in d.Tarefas) yield return new KeyValuePair<string, object>("t:" + t.Id, t);
            foreach (Nota n in d.Notas) yield return new KeyValuePair<string, object>("n:" + n.Id, n);
            foreach (Tipo y in d.Tipos) yield return new KeyValuePair<string, object>("y:" + y.Id, y);
            foreach (EventoDia e in d.Eventos) yield return new KeyValuePair<string, object>("e:" + e.Id, e);
            foreach (Intervalo i in d.Intervalos ?? new List<Intervalo>()) yield return new KeyValuePair<string, object>("i:" + i.Id, i);
            foreach (TipoEvento t in d.TiposEvento ?? new List<TipoEvento>()) yield return new KeyValuePair<string, object>("v:" + t.Id, t);
        }

        /// Antes de gravar: carimba o que mudou e registra o que foi excluído.
        public static void Carimbar(Dados d)
        {
            string agora = Agora();
            HashSet<string> vistos = new HashSet<string>();
            foreach (KeyValuePair<string, object> kv in Itens(d))
            {
                vistos.Add(kv.Key);
                string sig = Assinatura(kv.Value);
                string antes;
                System.Reflection.PropertyInfo p = kv.Value.GetType().GetProperty("Mod");
                if (!d.Assinaturas.TryGetValue(kv.Key, out antes) || antes != sig || p.GetValue(kv.Value, null) == null)
                {
                    p.SetValue(kv.Value, agora, null);
                    d.Assinaturas[kv.Key] = sig;
                }
            }
            foreach (string k in d.Assinaturas.Keys.Where(k => k != "nr" && !vistos.Contains(k)).ToList())
            {
                d.Excluidos[k] = agora;
                d.Assinaturas.Remove(k);
            }
            string nr = d.NotaRapida ?? "", nrAntes;
            if (!d.Assinaturas.TryGetValue("nr", out nrAntes) || nrAntes != nr) { d.NotaRapidaMod = agora; d.Assinaturas["nr"] = nr; }
        }

        /// Depois de mesclar: guarda como está agora, sem carimbar de novo.
        static void Assinar(Dados d)
        {
            d.Assinaturas.Clear();
            foreach (KeyValuePair<string, object> kv in Itens(d)) d.Assinaturas[kv.Key] = Assinatura(kv.Value);
            d.Assinaturas["nr"] = d.NotaRapida ?? "";
        }

        static List<T> Juntar<T>(List<T> local, List<T> remoto, string pre, Func<T, string> id, Func<T, string> mod, Dictionary<string, string> excl)
        {
            List<string> ordem = new List<string>();
            Dictionary<string, T> res = new Dictionary<string, T>();
            foreach (T x in local.Concat(remoto ?? new List<T>()))
            {
                string k = id(x);
                if (string.IsNullOrEmpty(k)) continue;
                T atual;
                if (!res.TryGetValue(k, out atual)) { res[k] = x; ordem.Add(k); }
                else if (string.CompareOrdinal(mod(x) ?? "", mod(atual) ?? "") > 0) res[k] = x;
            }
            return ordem.Select(k => res[k]).Where(x =>
            {
                string quando;
                return !(excl.TryGetValue(pre + id(x), out quando) && string.CompareOrdinal(quando, mod(x) ?? "") >= 0);
            }).ToList();
        }

        public static PacoteSinc Ler(string json) { return Json().Deserialize<PacoteSinc>(json); }

        /// Junta o que veio do Drive com o que está no PC: vale o mais recente de cada item.
        public static void Mesclar(Dados d, PacoteSinc r)
        {
            if (r == null) return;
            foreach (KeyValuePair<string, string> kv in r.Excluidos ?? new Dictionary<string, string>())
            {
                string q;
                if (!d.Excluidos.TryGetValue(kv.Key, out q) || string.CompareOrdinal(kv.Value, q) > 0) d.Excluidos[kv.Key] = kv.Value;
            }
            d.Tarefas = Juntar(d.Tarefas, r.Tarefas, "t:", x => x.Id, x => x.Mod, d.Excluidos);
            d.Notas = Juntar(d.Notas, r.Notas, "n:", x => x.Id, x => x.Mod, d.Excluidos);
            List<Tipo> tipos = Juntar(d.Tipos, r.Tipos, "y:", x => x.Id, x => x.Mod, d.Excluidos);
            if (tipos.Count > 0) d.Tipos = tipos;
            d.Eventos = Juntar(d.Eventos, r.Eventos, "e:", x => x.Id, x => x.Mod, d.Excluidos);
            d.Intervalos = Juntar(d.Intervalos ?? new List<Intervalo>(), r.Intervalos, "i:", x => x.Id, x => x.Mod, d.Excluidos);
            List<TipoEvento> tiposEv = Juntar(d.TiposEvento ?? new List<TipoEvento>(), r.TiposEvento, "v:", x => x.Id, x => x.Mod, d.Excluidos);
            if (tiposEv.Count > 0) d.TiposEvento = tiposEv;
            foreach (string k in r.NaoSaoMinhas ?? new List<string>()) if (!d.NaoSaoMinhas.Contains(k)) d.NaoSaoMinhas.Add(k);
            foreach (KeyValuePair<string, List<string>> kv in r.HorariosExtras ?? new Dictionary<string, List<string>>())
            {
                if (!d.HorariosExtras.ContainsKey(kv.Key)) d.HorariosExtras[kv.Key] = new List<string>();
                foreach (string h in kv.Value) if (!d.HorariosExtras[kv.Key].Contains(h)) d.HorariosExtras[kv.Key].Add(h);
            }
            if (string.CompareOrdinal(r.NotaRapidaMod ?? "", d.NotaRapidaMod ?? "") > 0) { d.NotaRapida = r.NotaRapida; d.NotaRapidaMod = r.NotaRapidaMod; }
            // exclusões com mais de 90 dias já foram vistas pelos dois lados
            string limite = DateTime.UtcNow.AddDays(-90).ToString("yyyy-MM-dd");
            foreach (string k in d.Excluidos.Where(kv => string.CompareOrdinal(kv.Value, limite) < 0).Select(kv => kv.Key).ToList()) d.Excluidos.Remove(k);
            Assinar(d);
        }

        public static string Montar(Dados d)
        {
            Config c = d.Config;
            PacoteSinc p = new PacoteSinc
            {
                Versao = 1, Gravado = Agora(), Dispositivo = "PC " + Environment.MachineName,
                Tarefas = d.Tarefas, Notas = d.Notas, Tipos = d.Tipos, Eventos = d.Eventos,
                Intervalos = d.Intervalos, TiposEvento = d.TiposEvento,
                NaoSaoMinhas = d.NaoSaoMinhas, HorariosExtras = d.HorariosExtras,
                NotaRapida = d.NotaRapida, NotaRapidaMod = d.NotaRapidaMod, Excluidos = d.Excluidos,
                Agenda = new Dictionary<string, object>
                {
                    { "HoraInicio", c.HoraInicio }, { "HoraFim", c.HoraFim }, { "Intervalo", c.Intervalo },
                    { "PausaAlmoco", c.PausaAlmoco }, { "AlmocoInicio", c.AlmocoInicio }, { "AlmocoFim", c.AlmocoFim },
                    { "DestacarFimDeSemana", c.DestacarFimDeSemana }, { "AvisoAntesMin", c.AvisoAntesMin },
                    { "MostrarAguardando", c.MostrarAguardando }
                }
            };
            return Json().Serialize(p);
        }

        // ---------------- Google Drive (pasta do aplicativo) ----------------
        public static string AcharArquivo(string tk)
        {
            object r = Http.Json(Http.Pedir("GET", "https://www.googleapis.com/drive/v3/files?spaces=appDataFolder&fields=files(id,name)&q="
                + Uri.EscapeDataString("name='" + Arquivo + "'"), null, null, tk));
            object[] fs = Http.L(r, "files");
            return fs.Length == 0 ? null : Http.S(fs[0], "id");
        }

        public static string Baixar(string tk, string id)
        {
            return Http.Pedir("GET", "https://www.googleapis.com/drive/v3/files/" + id + "?alt=media", null, null, tk);
        }

        public static void Enviar(string tk, string id, string json)
        {
            if (id != null)
            {
                Http.Pedir("PATCH", "https://www.googleapis.com/upload/drive/v3/files/" + id + "?uploadType=media", json, "application/json; charset=UTF-8", tk);
                return;
            }
            string b = "marke" + Guid.NewGuid().ToString("N");
            string corpo = "--" + b + "\r\nContent-Type: application/json; charset=UTF-8\r\n\r\n"
                + "{\"name\":\"" + Arquivo + "\",\"parents\":[\"appDataFolder\"]}\r\n"
                + "--" + b + "\r\nContent-Type: application/json; charset=UTF-8\r\n\r\n" + json + "\r\n--" + b + "--";
            Http.Pedir("POST", "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart", corpo, "multipart/related; boundary=" + b, tk);
        }
    }
}
