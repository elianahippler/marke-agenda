// Marke Agenda: login com a conta Google (o mesmo login libera o Google Agenda e a
// sincronização pelo Google Drive). O app de celular usa a mesma conta.
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MarkeAgenda
{
    public static class ContaGoogle
    {
        // Credenciais do app "Marke Agenda" (Google Cloud > Credenciais > ID do cliente OAuth, tipo
        // "App para computador"). Ficam só neste PC, cifradas pela conta do Windows: nunca no código/git.
        // Coladas na tela "Entrar com Google" na primeira vez.
        public const string Link = "https://console.cloud.google.com/apis/credentials";
        static string Arquivo { get { return Path.Combine(Dados.Pasta, "google-cliente.dat"); } }
        static string id, secret;

        static void Ler()
        {
            if (id != null) return;
            id = ""; secret = "";
            try
            {
                if (!File.Exists(Arquivo)) return;
                string[] l = Cripto.Abrir(File.ReadAllText(Arquivo)).Split('\n');
                if (l.Length >= 2) { id = l[0].Trim(); secret = l[1].Trim(); }
            }
            catch { }
        }

        public static string ClientId { get { Ler(); return id; } }
        public static string Secret { get { Ler(); return secret; } }

        public static void Guardar(string novoId, string novoSecret)
        {
            File.WriteAllText(Arquivo, Cripto.Fechar(novoId.Trim() + "\n" + novoSecret.Trim()));
            id = novoId.Trim(); secret = novoSecret.Trim();
        }

        public const string Escopos = "openid email profile https://www.googleapis.com/auth/calendar.readonly https://www.googleapis.com/auth/drive.appdata";

        /// Sempre há login Google (se faltar o Client ID, a tela pede).
        public static bool Configurado { get { return true; } }
        public static bool TemCliente { get { return ClientId.EndsWith(".apps.googleusercontent.com") && Secret.Length > 0; } }

        public class Resultado { public string Email; public string Nome; public string Refresh; }

        /// Abre o navegador para o login do Google e espera a volta numa porta local.
        public static Resultado Entrar()
        {
            if (!TemCliente) throw new Exception("Falta o Client ID e o Client Secret do Google.");
            TcpListener escuta = new TcpListener(IPAddress.Loopback, 0);
            escuta.Start();
            int porta = ((IPEndPoint)escuta.LocalEndpoint).Port;
            string retorno = "http://127.0.0.1:" + porta + "/";
            Process.Start("https://accounts.google.com/o/oauth2/v2/auth?" + Http.Form(
                "client_id", ClientId, "redirect_uri", retorno, "response_type", "code",
                "scope", Escopos, "access_type", "offline", "prompt", "consent select_account"));
            string codigo = null, erro = null;
            try
            {
                IAsyncResult ar = escuta.BeginAcceptTcpClient(null, null);
                if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromMinutes(5))) throw new Exception("O login do Google não voltou em 5 minutos.");
                using (TcpClient cli = escuta.EndAcceptTcpClient(ar))
                using (NetworkStream ns = cli.GetStream())
                {
                    string linha = new StreamReader(ns).ReadLine() ?? "";
                    string[] partes = linha.Split(' ');
                    string caminho = partes.Length > 1 ? partes[1] : "";
                    int q = caminho.IndexOf('?');
                    if (q >= 0)
                        foreach (string par in caminho.Substring(q + 1).Split('&'))
                        {
                            string[] kv = par.Split('=');
                            if (kv.Length < 2) continue;
                            if (kv[0] == "code") codigo = Uri.UnescapeDataString(kv[1]);
                            if (kv[0] == "error") erro = Uri.UnescapeDataString(kv[1]);
                        }
                    string html = "<html><meta charset='utf-8'><body style='font-family:Segoe UI;background:#0f172a;color:#e6eefa;padding:60px;text-align:center'><h2>"
                        + (codigo != null ? "Pronto! Pode voltar para a Marke Agenda." : "Não foi possível entrar: " + WebUtility.HtmlEncode(erro ?? "sem código"))
                        + "</h2><p>Esta aba já pode ser fechada.</p></body></html>";
                    byte[] b = Encoding.UTF8.GetBytes(html);
                    byte[] cab = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + b.Length + "\r\nConnection: close\r\n\r\n");
                    ns.Write(cab, 0, cab.Length); ns.Write(b, 0, b.Length); ns.Flush();
                }
            }
            finally { escuta.Stop(); }
            if (codigo == null) throw new Exception(erro == "access_denied" ? "Login cancelado." : "O Google recusou: " + (erro ?? "sem código"));

            object r = Http.Json(Http.Pedir("POST", "https://oauth2.googleapis.com/token", Http.Form(
                "code", codigo, "client_id", ClientId, "client_secret", Secret,
                "redirect_uri", retorno, "grant_type", "authorization_code"), "application/x-www-form-urlencoded", null));
            string refresh = Http.S(r, "refresh_token"), acesso = Http.S(r, "access_token");
            if (refresh == "") throw new Exception("O Google não devolveu a autorização permanente. Tente de novo.");
            object eu = Http.Json(Http.Pedir("GET", "https://www.googleapis.com/oauth2/v3/userinfo", null, null, acesso));
            return new Resultado { Email = Http.S(eu, "email"), Nome = Http.S(eu, "name"), Refresh = refresh };
        }

        /// Token de acesso (vale 1 hora) a partir da autorização guardada.
        public static string Acesso(string refresh)
        {
            object r = Http.Json(Http.Pedir("POST", "https://oauth2.googleapis.com/token", Http.Form(
                "refresh_token", refresh, "client_id", ClientId, "client_secret", Secret,
                "grant_type", "refresh_token"), "application/x-www-form-urlencoded", null));
            string tk = Http.S(r, "access_token");
            if (tk == "") throw new Exception("O Google pediu para entrar de novo (menu do seu nome > Entrar com Google).");
            return tk;
        }
    }
}
