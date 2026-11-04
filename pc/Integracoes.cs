// Marke Agenda: busca de tarefas no Google Agenda, TomTicket e Markedesk.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace MarkeAgenda
{
    public static class Http
    {
        static Http() { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; }

        public static string Pedir(string metodo, string url, string corpo, string tipoCorpo, string bearer)
        {
            for (int tentativa = 1; ; tentativa++)
            {
                HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
                r.Method = metodo; r.Timeout = 30000; r.UserAgent = "MarkeAgenda/1.0"; r.Accept = "application/json";
                if (!string.IsNullOrEmpty(bearer)) r.Headers["Authorization"] = "Bearer " + bearer;
                if (corpo != null)
                {
                    byte[] b = Encoding.UTF8.GetBytes(corpo);
                    r.ContentType = tipoCorpo; r.ContentLength = b.Length;
                    using (Stream s = r.GetRequestStream()) s.Write(b, 0, b.Length);
                }
                try
                {
                    using (HttpWebResponse resp = (HttpWebResponse)r.GetResponse())
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        return sr.ReadToEnd();
                }
                catch (WebException ex)
                {
                    HttpWebResponse resp = ex.Response as HttpWebResponse;
                    if (resp == null) throw new Exception("Sem resposta de " + new Uri(url).Host + ": " + ex.Message);
                    int cod = (int)resp.StatusCode;
                    string txt = "";
                    try { using (StreamReader sr = new StreamReader(resp.GetResponseStream())) txt = sr.ReadToEnd(); } catch { }
                    if (cod == 429 && tentativa < 4) { Thread.Sleep(3000 * tentativa); continue; }
                    if (txt.Length > 200) txt = txt.Substring(0, 200);
                    throw new Exception("HTTP " + cod + " em " + new Uri(url).Host + ": " + txt);
                }
            }
        }

        public static object Json(string texto)
        {
            JavaScriptSerializer j = new JavaScriptSerializer(); j.MaxJsonLength = int.MaxValue;
            return j.DeserializeObject(texto);
        }
        public static string ParaJson(object o) { return new JavaScriptSerializer().Serialize(o); }

        public static object D(object o, string chave)
        {
            Dictionary<string, object> d = o as Dictionary<string, object>;
            if (d == null || !d.ContainsKey(chave)) return null;
            return d[chave];
        }
        public static string S(object o, string chave) { object v = D(o, chave); return v == null ? "" : Convert.ToString(v); }
        public static object[] L(object o, string chave) { return D(o, chave) as object[] ?? new object[0]; }

        public static string Form(params string[] pares)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i + 1 < pares.Length; i += 2)
            {
                if (sb.Length > 0) sb.Append('&');
                sb.Append(Uri.EscapeDataString(pares[i])).Append('=').Append(Uri.EscapeDataString(pares[i + 1] ?? ""));
            }
            return sb.ToString();
        }

        public static string LimparHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            string t = System.Text.RegularExpressions.Regex.Replace(html, "<br\\s*/?>|</p>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            t = System.Text.RegularExpressions.Regex.Replace(t, "<[^>]+>", "");
            t = WebUtility.HtmlDecode(t).Trim();
            return t.Length > 1500 ? t.Substring(0, 1500) + "..." : t;
        }
    }

    public static class Integracoes
    {
        // ---------------- TomTicket ----------------
        public static List<Tarefa> TomTicket(Config c)
        {
            string token = Cripto.Abrir(c.TomToken);
            if (token == "") throw new Exception("TomTicket: falta o token da API em Acessos.");
            List<Tarefa> lista = new List<Tarefa>();
            // só os chamados em que a pessoa é a atendente responsável
            string filtro = (c.TomAtendente ?? "").Trim().ToLowerInvariant();
            if (filtro == "") throw new Exception("TomTicket: informe o seu nome de atendente em Acessos > TomTicket (só os seus chamados são trazidos).");
            foreach (string sit in (c.TomSituacoes ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                for (int pag = 1; pag <= 30; pag++)
                {
                    object r = Http.Json(Http.Pedir("GET", "https://api.tomticket.com/v2.0/ticket/list?situation=" + sit.Trim() + "&page=" + pag, null, null, token));
                    foreach (object t in Http.L(r, "data"))
                    {
                        string atendente = Http.S(Http.D(t, "operator"), "name");
                        if (filtro != "" && !atendente.ToLowerInvariant().Contains(filtro)) continue;
                        string prot = Http.S(t, "protocol");
                        if (lista.Any(x => x.Ref == prot)) continue;
                        Tarefa nova = new Tarefa
                        {
                            Origem = "TomTicket", Ref = prot,
                            Titulo = "#" + prot + " " + Http.S(t, "subject"),
                            Descricao = "Cliente: " + Http.S(Http.D(t, "customer"), "name")
                                + "\nSituação: " + Http.S(Http.D(t, "situation"), "description")
                                + "\nSetor: " + Http.S(Http.D(t, "department"), "name")
                                + "\nAtendente: " + (atendente == "" ? "(nenhum)" : atendente)
                                + "\n\n" + Http.LimparHtml(Http.S(t, "message")),
                            Link = string.IsNullOrEmpty(c.TomLink) ? "" : c.TomLink.Replace("{protocolo}", prot),
                            TipoId = c.TipoTomTicket
                        };
                        lista.Add(nova);
                    }
                    int paginas = 1;
                    int.TryParse(Http.S(r, "pages"), out paginas);
                    if (pag >= paginas) break;
                    Thread.Sleep(400);
                }
                Thread.Sleep(400);
            }
            return lista;
        }

        // ---------------- Markedesk ----------------
        static string mdToken; static DateTime mdQuando; static object mdUsuario;   // o login já devolve o usuário com as filas

        static string MdLogin(Config c)
        {
            if (mdToken != null && (DateTime.Now - mdQuando).TotalHours < 4) return mdToken;
            if (string.IsNullOrEmpty(c.MdUrlApi) || string.IsNullOrEmpty(c.MdEmail))
                throw new Exception("Markedesk: preencha URL da API, e-mail e senha em Acessos.");
            string corpo = Http.ParaJson(new Dictionary<string, object> { { "email", c.MdEmail }, { "password", Cripto.Abrir(c.MdSenha) } });
            object r = Http.Json(Http.Pedir("POST", c.MdUrlApi.TrimEnd('/') + "/auth/login", corpo, "application/json", null));
            mdToken = Http.S(r, "token");
            mdUsuario = Http.D(r, "user");
            if (mdToken == "") throw new Exception("Markedesk: login sem token. Confira URL (a do backend, api.) e senha.");
            mdQuando = DateTime.Now;
            return mdToken;
        }

        public static string TestarMarkedesk(Config c)
        {
            mdToken = null;
            string tk = MdLogin(c);
            string nome = Http.S(mdUsuario, "name");
            string filas = string.Join(", ", Http.L(mdUsuario, "queues").Select(q => Http.S(q, "name")));
            return "Conectado" + (nome == "" ? "" : " como " + nome) + (filas == "" ? "" : " (setor: " + filas + ")");
        }

        public static List<Tarefa> Markedesk(Config c)
        {
            string tk = MdLogin(c);
            List<Tarefa> lista = new List<Tarefa>();
            string baseUrl = c.MdUrlApi.TrimEnd('/');
            string modo = string.IsNullOrEmpty(c.MdModo) ? "setor" : c.MdModo;

            // quem sou eu e quais filas (setores) são minhas
            object usuario = mdUsuario;   // veio no login (o /auth/me do servidor está com erro)
            string meuId = Http.S(usuario, "id");
            HashSet<string> minhasFilas = new HashSet<string>(Http.L(usuario, "queues").Select(q => Http.S(q, "id")));
            if (modo == "setor" && minhasFilas.Count == 0)
                throw new Exception("Markedesk: seu usuário não está em nenhuma fila (setor). Escolha \"Só os meus\" em Acessos.");

            foreach (string status in new[] { "open", "pending" })
            {
                for (int pag = 1; pag <= 30; pag++)
                {
                    string url = baseUrl + "/tickets?status=" + status + "&pageNumber=" + pag + (modo == "meus" ? "" : "&showAll=true");
                    object r = Http.Json(Http.Pedir("GET", url, null, null, tk));
                    object[] tickets = Http.L(r, "tickets");
                    foreach (object t in tickets)
                    {
                        string id = Http.S(t, "id");
                        if (lista.Any(x => x.Ref == id)) continue;
                        string filaId = Http.S(t, "queueId");
                        if (filaId == "") filaId = Http.S(Http.D(t, "queue"), "id");
                        string donoId = Http.S(t, "userId");
                        if (modo == "setor" && !minhasFilas.Contains(filaId)) continue;
                        if (modo == "meus" && meuId != "" && donoId != meuId) continue;
                        string contato = Http.S(Http.D(t, "contact"), "name");
                        string fila = Http.S(Http.D(t, "queue"), "name");
                        string uuid = Http.S(t, "uuid");
                        lista.Add(new Tarefa
                        {
                            Origem = "Markedesk", Ref = id,
                            Titulo = "Atendimento #" + id + " " + contato,
                            Descricao = "Contato: " + contato + "\nFila: " + (fila == "" ? "(sem fila)" : fila)
                                + "\nSituação: " + (status == "open" ? "Em atendimento" : "Aguardando")
                                + "\n\nÚltima mensagem: " + Http.S(t, "lastMessage"),
                            Link = string.IsNullOrEmpty(c.MdUrlTela) ? "" : c.MdUrlTela.TrimEnd('/') + "/tickets/" + (uuid == "" ? id : uuid),
                            TipoId = c.TipoMarkedesk
                        });
                    }
                    if (tickets.Length == 0 || !(Http.D(r, "hasMore") is bool && (bool)Http.D(r, "hasMore"))) break;
                }
            }
            return lista;
        }

        // ---------------- Google Agenda ----------------
        const string Escopo = "https://www.googleapis.com/auth/calendar.readonly https://www.googleapis.com/auth/drive.appdata";

        /// Abre o navegador para o login do Google e espera a volta numa porta local.
        public static void ConectarGoogle(Config c)
        {
            string secret = Cripto.Abrir(c.GoogleSecret);
            if (string.IsNullOrEmpty(c.GoogleClientId) || secret == "")
                throw new Exception("Preencha o Client ID e o Client Secret antes de conectar.");
            TcpListener escuta = new TcpListener(IPAddress.Loopback, 0);
            escuta.Start();
            int porta = ((IPEndPoint)escuta.LocalEndpoint).Port;
            string retorno = "http://127.0.0.1:" + porta + "/";
            string url = "https://accounts.google.com/o/oauth2/v2/auth?" + Http.Form(
                "client_id", c.GoogleClientId, "redirect_uri", retorno, "response_type", "code",
                "scope", Escopo, "access_type", "offline", "prompt", "consent");
            Process.Start(url);
            string codigo = null, erro = null;
            try
            {
                IAsyncResult ar = escuta.BeginAcceptTcpClient(null, null);
                if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromMinutes(3))) throw new Exception("O login do Google não voltou em 3 minutos.");
                using (TcpClient cli = escuta.EndAcceptTcpClient(ar))
                using (NetworkStream ns = cli.GetStream())
                {
                    StreamReader sr = new StreamReader(ns);
                    string linha = sr.ReadLine() ?? "";
                    string caminho = linha.Split(' ').Length > 1 ? linha.Split(' ')[1] : "";
                    int q = caminho.IndexOf('?');
                    if (q >= 0)
                        foreach (string par in caminho.Substring(q + 1).Split('&'))
                        {
                            string[] kv = par.Split('=');
                            if (kv.Length < 2) continue;
                            if (kv[0] == "code") codigo = Uri.UnescapeDataString(kv[1]);
                            if (kv[0] == "error") erro = Uri.UnescapeDataString(kv[1]);
                        }
                    string html = "<html><meta charset='utf-8'><body style='font-family:Segoe UI;padding:40px'><h2>"
                        + (codigo != null ? "Marke Agenda conectada ao Google. Pode fechar esta aba." : "Não foi possível conectar: " + WebUtility.HtmlEncode(erro ?? "sem código"))
                        + "</h2></body></html>";
                    byte[] b = Encoding.UTF8.GetBytes(html);
                    byte[] cab = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + b.Length + "\r\nConnection: close\r\n\r\n");
                    ns.Write(cab, 0, cab.Length); ns.Write(b, 0, b.Length); ns.Flush();
                }
            }
            finally { escuta.Stop(); }
            if (codigo == null) throw new Exception("Google recusou: " + (erro ?? "sem código"));
            object r = Http.Json(Http.Pedir("POST", "https://oauth2.googleapis.com/token", Http.Form(
                "code", codigo, "client_id", c.GoogleClientId, "client_secret", secret,
                "redirect_uri", retorno, "grant_type", "authorization_code"), "application/x-www-form-urlencoded", null));
            string refresh = Http.S(r, "refresh_token");
            if (refresh == "") throw new Exception("O Google não devolveu o refresh_token.");
            c.GoogleRefresh = Cripto.Fechar(refresh);
        }

        public static string GoogleAcesso(Config c)
        {
            string refresh = Cripto.Abrir(c.GoogleRefresh);
            if (refresh == "") throw new Exception("Google: conta não conectada (menu do seu nome > Entrar com Google).");
            // login pelo app "Marke Agenda" (o padrão); o Client ID próprio em Acessos fica só para quem já usava
            if (ContaGoogle.Configurado && string.IsNullOrEmpty(c.GoogleClientId)) return ContaGoogle.Acesso(refresh);
            object r = Http.Json(Http.Pedir("POST", "https://oauth2.googleapis.com/token", Http.Form(
                "refresh_token", refresh, "client_id", c.GoogleClientId, "client_secret", Cripto.Abrir(c.GoogleSecret),
                "grant_type", "refresh_token"), "application/x-www-form-urlencoded", null));
            return Http.S(r, "access_token");
        }

        /// Eventos de 7 dias atrás até 30 dias à frente.
        public static List<Tarefa> Google(Config c, int intervalo)
        {
            string tk = GoogleAcesso(c);
            string agenda = string.IsNullOrEmpty(c.GoogleAgendaId) ? "primary" : c.GoogleAgendaId;
            string ini = DateTime.Today.AddDays(-7).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            string fim = DateTime.Today.AddDays(31).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            List<Tarefa> lista = new List<Tarefa>();
            string pagina = "";
            for (int i = 0; i < 10; i++)
            {
                string url = "https://www.googleapis.com/calendar/v3/calendars/" + Uri.EscapeDataString(agenda) + "/events?"
                    + Http.Form("timeMin", ini, "timeMax", fim, "singleEvents", "true", "orderBy", "startTime", "maxResults", "250")
                    + (pagina == "" ? "" : "&pageToken=" + Uri.EscapeDataString(pagina));
                object r = Http.Json(Http.Pedir("GET", url, null, null, tk));
                foreach (object ev in Http.L(r, "items"))
                {
                    if (Http.S(ev, "status") == "cancelled") continue;
                    Tarefa t = new Tarefa
                    {
                        Origem = "Google", Ref = Http.S(ev, "id"),
                        Titulo = Http.S(ev, "summary") == "" ? "(sem título)" : Http.S(ev, "summary"),
                        Descricao = Http.LimparHtml(Http.S(ev, "description")),
                        Link = Http.S(ev, "htmlLink"), TipoId = c.TipoGoogle
                    };
                    string dt = Http.S(Http.D(ev, "start"), "dateTime");
                    if (dt != "")
                    {
                        DateTime ini2 = DateTimeOffset.Parse(dt).LocalDateTime;
                        t.Data = Tarefa.Dia(ini2);
                        t.Hora = ini2.ToString("HH:mm");
                        string df = Http.S(Http.D(ev, "end"), "dateTime");
                        if (df != "") t.Duracao = (int)(DateTimeOffset.Parse(df).LocalDateTime - ini2).TotalMinutes;
                    }
                    else t.Data = Http.S(Http.D(ev, "start"), "date");   // dia inteiro: fica sem horário
                    lista.Add(t);
                }
                pagina = Http.S(r, "nextPageToken");
                if (pagina == "") break;
            }
            return lista;
        }

        /// Junta o que veio da ferramenta com as tarefas já existentes, sem perder o planejamento.
        public static int Mesclar(Dados d, string origem, List<Tarefa> vindas)
        {
            int novas = 0;
            vindas = vindas.Where(v => !d.NaoSaoMinhas.Contains(origem + ":" + v.Ref)).ToList();   // marcadas como "não é minha"
            HashSet<string> refs = new HashSet<string>(vindas.Select(x => x.Ref));
            foreach (Tarefa v in vindas)
            {
                Tarefa ex = d.Tarefas.FirstOrDefault(x => x.Origem == origem && x.Ref == v.Ref);
                if (ex == null)
                {
                    v.Id = Dados.NovoId(); v.Criada = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                    if (d.Tipos.All(x => x.Id != v.TipoId)) v.TipoId = d.Tipos[0].Id;
                    d.Tarefas.Add(v); novas++;
                }
                else
                {
                    ex.Titulo = v.Titulo; ex.Descricao = v.Descricao;
                    if (!string.IsNullOrEmpty(v.Link)) ex.Link = v.Link;
                    if (origem == "Google" && (ex.Data != v.Data || ex.Hora != v.Hora))
                    { ex.Data = v.Data; ex.Hora = v.Hora; ex.Duracao = v.Duracao; ex.Avisado = false; }
                }
            }
            // Sumiu da ferramenta: se não estava planejada nem concluída, sai da lista.
            d.Tarefas.RemoveAll(x => x.Origem == origem && x.Ativa() && !refs.Contains(x.Ref) && !x.Concluida
                && (origem == "Google" ? string.Compare(x.Data ?? "", Tarefa.Dia(DateTime.Today.AddDays(-7))) >= 0 : !x.EstaAgendada()));
            return novas;
        }
    }
}
