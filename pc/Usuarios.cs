// Marke Agenda: usuários locais. Cada usuário tem um arquivo de dados próprio,
// cifrado com uma chave tirada da senha dele: sem a senha, ninguém lê.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace MarkeAgenda
{
    public class Usuario
    {
        public string Nome { get; set; }
        public string Email { get; set; }
        public string Arquivo { get; set; }
        public string Chave { get; set; }   // chave dos dados, cifrada pela conta do Windows
        public string GoogleEmail { get; set; }
        public string GoogleRefresh { get; set; }   // autorização do Google, cifrada pela conta do Windows
    }

    public class Sessao
    {
        public Usuario Usuario;
        public byte[] Chave;
        public string Caminho { get { return Path.Combine(Dados.Pasta, Usuario.Arquivo); } }

        public byte[] Cifrar(string texto)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Chave; aes.GenerateIV();
                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(aes.IV, 0, 16);
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    { byte[] b = Encoding.UTF8.GetBytes(texto); cs.Write(b, 0, b.Length); }
                    return ms.ToArray();
                }
            }
        }

        public string Decifrar(byte[] dados)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Chave;
                byte[] iv = new byte[16]; Array.Copy(dados, iv, 16); aes.IV = iv;
                using (MemoryStream ms = new MemoryStream(dados, 16, dados.Length - 16))
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (StreamReader sr = new StreamReader(cs, Encoding.UTF8)) return sr.ReadToEnd();
            }
        }
    }

    public static class Usuarios
    {
        static string Arq { get { return Path.Combine(Dados.Pasta, "usuarios.json"); } }

        public static List<Usuario> Lista()
        {
            try { if (File.Exists(Arq)) return new JavaScriptSerializer().Deserialize<List<Usuario>>(File.ReadAllText(Arq, Encoding.UTF8)) ?? new List<Usuario>(); }
            catch (Exception ex) { Dados.Log("usuarios.json: " + ex.Message); }
            return new List<Usuario>();
        }

        static void Gravar(List<Usuario> l) { File.WriteAllText(Arq, new JavaScriptSerializer().Serialize(l), Encoding.UTF8); }

        /// O cadastro desta conta do Windows (a chave só abre para ela).
        public static Sessao Atual()
        {
            foreach (Usuario u in Lista())
            {
                try
                {
                    byte[] chave = ProtectedData.Unprotect(Convert.FromBase64String(u.Chave), null, DataProtectionScope.CurrentUser);
                    return new Sessao { Usuario = u, Chave = chave };
                }
                catch { }
            }
            return null;
        }

        public static Sessao Criar(string nome, string email)
        {
            nome = (nome ?? "").Trim();
            if (nome == "") throw new Exception("Informe o seu nome.");
            byte[] chave = new byte[32];
            using (RandomNumberGenerator r = RandomNumberGenerator.Create()) r.GetBytes(chave);
            Usuario u = new Usuario
            {
                Nome = nome, Email = (email ?? "").Trim(), Arquivo = "agenda-" + Dados.NovoId() + ".dat",
                Chave = Convert.ToBase64String(ProtectedData.Protect(chave, null, DataProtectionScope.CurrentUser))
            };
            List<Usuario> l = Lista();
            bool primeiro = l.Count == 0;
            l.Add(u); Gravar(l);
            Sessao s = new Sessao { Usuario = u, Chave = chave };
            // A versão anterior guardava tudo em agenda.json: o primeiro cadastro herda.
            string antigo = Path.Combine(Dados.Pasta, "agenda.json");
            if (primeiro && File.Exists(antigo))
            {
                Dados velho = Dados.LerTextoPuro(antigo);
                if (velho != null)
                {
                    velho.Sessao = s; velho.Salvar();
                    foreach (string f in new[] { antigo, antigo + ".bak" }) try { File.Delete(f); } catch { }
                }
            }
            return s;
        }

        public static void AtualizarCadastro(Sessao s, string nome, string email)
        {
            List<Usuario> l = Lista();
            Usuario u = l.FirstOrDefault(x => x.Arquivo == s.Usuario.Arquivo);
            if (u == null) return;
            u.Nome = nome.Trim(); u.Email = (email ?? "").Trim();
            s.Usuario.Nome = u.Nome; s.Usuario.Email = u.Email;
            Gravar(l);
        }

        /// Login com o Google: acha (ou cria) o cadastro dessa conta e guarda a autorização.
        public static Sessao EntrarGoogle(ContaGoogle.Resultado g)
        {
            List<Usuario> l = Lista();
            Usuario u = l.FirstOrDefault(x => string.Equals(x.GoogleEmail, g.Email, StringComparison.OrdinalIgnoreCase));
            // quem já usava a Marke Agenda sem Google: o primeiro login liga ao cadastro que já existe
            if (u == null && l.Count(x => string.IsNullOrEmpty(x.GoogleEmail)) == 1 && l.All(x => string.IsNullOrEmpty(x.GoogleEmail) || x.GoogleEmail == g.Email))
                u = l.First(x => string.IsNullOrEmpty(x.GoogleEmail));
            Sessao s;
            if (u == null)
            {
                s = Criar(string.IsNullOrEmpty(g.Nome) ? g.Email : g.Nome, g.Email);
                l = Lista();
                u = l.First(x => x.Arquivo == s.Usuario.Arquivo);
            }
            u.GoogleEmail = g.Email;
            if (!string.IsNullOrEmpty(g.Nome)) u.Nome = g.Nome;
            if (string.IsNullOrEmpty(u.Email)) u.Email = g.Email;
            u.GoogleRefresh = Cripto.Fechar(g.Refresh);
            Gravar(l);
            byte[] chave = ProtectedData.Unprotect(Convert.FromBase64String(u.Chave), null, DataProtectionScope.CurrentUser);
            return new Sessao { Usuario = u, Chave = chave };
        }

        public static void SairGoogle(Sessao s)
        {
            List<Usuario> l = Lista();
            Usuario u = l.FirstOrDefault(x => x.Arquivo == s.Usuario.Arquivo);
            if (u != null) { u.GoogleRefresh = ""; Gravar(l); }
            s.Usuario.GoogleRefresh = "";
        }
    }

    /// Entrada: "Entrar com Google" (o mesmo login do app de celular).
    public class TelaLoginGoogle : Form
    {
        public Sessao Sessao;

        public TelaLoginGoogle(bool podePular)
        {
            Text = "Marke Agenda"; FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(420, 330);
            BackColor = Tema.Fundo; ForeColor = Tema.Texto; Font = Tema.Fonte;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Panel cab = new Panel { Dock = DockStyle.Top, Height = 110 };
            cab.Paint += (s, e) =>
            {
                using (System.Drawing.Drawing2D.LinearGradientBrush b = new System.Drawing.Drawing2D.LinearGradientBrush(cab.ClientRectangle, Color.FromArgb(29, 78, 216), Color.FromArgb(14, 116, 144), 20f))
                    e.Graphics.FillRectangle(b, cab.ClientRectangle);
                if (Icon != null) e.Graphics.DrawIcon(new Icon(Icon, 48, 48), 28, 31);
                TextRenderer.DrawText(e.Graphics, "Marke Agenda", new Font("Segoe UI Semibold", 18f), new Point(88, 30), Color.White);
                TextRenderer.DrawText(e.Graphics, "Entre com a sua conta Google", Tema.Fonte, new Point(91, 64), Color.FromArgb(210, 230, 255));
            };
            Controls.Add(cab);
            Label info = new Label { Text = "O mesmo login do celular. Ele também liga o seu Google Agenda e guarda a sincronização no seu Google Drive.", AutoSize = false, Size = new Size(360, 44), Location = new Point(30, 128), ForeColor = Tema.Fraco };

            // Client ID / Secret: aparecem só quando ainda não foram colados neste PC
            Panel cliente = new Panel { Location = new Point(30, 176), Size = new Size(360, 168) };
            Label lId = new Label { Text = "Client ID (App para computador)", AutoSize = true, Location = new Point(0, 0), ForeColor = Tema.Fraco };
            TextBox tId = new TextBox { Location = new Point(0, 20), Width = 360, Text = ContaGoogle.ClientId };
            Label lSec = new Label { Text = "Client Secret", AutoSize = true, Location = new Point(0, 54), ForeColor = Tema.Fraco };
            TextBox tSec = new TextBox { Location = new Point(0, 74), Width = 360, Text = ContaGoogle.Secret, UseSystemPasswordChar = true };
            LinkLabel link = new LinkLabel { Text = "Criar o Client ID no Google Cloud ›", AutoSize = true, Location = new Point(0, 108), LinkColor = Tema.Claro, ActiveLinkColor = Tema.Claro };
            link.LinkClicked += (s, e) => { try { System.Diagnostics.Process.Start(ContaGoogle.Link); } catch { } };
            Label lObs = new Label { Text = "Fica só neste computador, cifrado.", AutoSize = true, Location = new Point(0, 132), ForeColor = Tema.Fraco, Font = Tema.Pequena };
            cliente.Controls.AddRange(new Control[] { lId, tId, lSec, tSec, link, lObs });
            cliente.Visible = !ContaGoogle.TemCliente;
            int y0 = cliente.Visible ? 352 : 182;

            Button entrar = Tema.Botao("G   Entrar com Google", true); entrar.AutoSize = false; entrar.Size = new Size(360, 42); entrar.Location = new Point(30, y0);
            entrar.Font = new Font("Segoe UI Semibold", 10.5f);
            Label status = new Label { AutoSize = false, Size = new Size(360, 40), Location = new Point(30, y0 + 50), ForeColor = Tema.Fraco };
            Button pular = Tema.Botao("Agora não", false); pular.Location = new Point(30, y0 + 98); pular.Visible = podePular; pular.DialogResult = DialogResult.Cancel;
            LinkLabel trocar = new LinkLabel { Text = "Trocar o Client ID", AutoSize = true, Location = new Point(300, y0 + 104), LinkColor = Tema.Fraco, Visible = !cliente.Visible };
            trocar.LinkClicked += (s, e) =>
            {
                cliente.Visible = true; trocar.Visible = false;
                entrar.Top = 352; status.Top = 402; pular.Top = 450;
                ClientSize = new Size(420, 500);
            };
            ClientSize = new Size(420, y0 + 148);
            entrar.Click += (s, e) =>
            {
                if (cliente.Visible)
                {
                    if (!tId.Text.Trim().EndsWith(".apps.googleusercontent.com") || tSec.Text.Trim() == "")
                    { status.ForeColor = Tema.Atraso; status.Text = "Cole o Client ID (termina com .apps.googleusercontent.com) e o Client Secret."; return; }
                    ContaGoogle.Guardar(tId.Text, tSec.Text);
                }
                entrar.Enabled = false; status.ForeColor = Tema.Fraco; status.Text = "Continue no navegador que abriu..."; status.Refresh();
                try
                {
                    Sessao = Usuarios.EntrarGoogle(ContaGoogle.Entrar());
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex) { status.ForeColor = Tema.Atraso; status.Text = ex.Message; entrar.Enabled = true; }
                Activate();
            };
            Controls.AddRange(new Control[] { info, cliente, entrar, status, pular, trocar });
            Tema.Aplicar(this);
            status.ForeColor = Tema.Fraco; link.LinkColor = Tema.Claro; trocar.LinkColor = Tema.Fraco;
        }
    }

    /// Cadastro simples, feito uma vez: nome e e-mail.
    public class TelaCadastro : Form
    {
        public Sessao Sessao;

        public TelaCadastro(Sessao editar)
        {
            Text = "Marke Agenda"; FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; MinimizeBox = editar == null;
            StartPosition = editar == null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
            ShowInTaskbar = editar == null;
            BackColor = Tema.Fundo; ForeColor = Tema.Texto; Font = Tema.Fonte;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Panel cab = new Panel { Dock = DockStyle.Top, Height = 110 };
            cab.Paint += (s, e) =>
            {
                using (System.Drawing.Drawing2D.LinearGradientBrush b = new System.Drawing.Drawing2D.LinearGradientBrush(cab.ClientRectangle, Color.FromArgb(29, 78, 216), Color.FromArgb(14, 116, 144), 20f))
                    e.Graphics.FillRectangle(b, cab.ClientRectangle);
                if (Icon != null) e.Graphics.DrawIcon(new Icon(Icon, 48, 48), 28, 31);
                TextRenderer.DrawText(e.Graphics, editar == null ? "Bem-vindo(a)!" : "Meu cadastro", new Font("Segoe UI Semibold", 18f), new Point(88, 30), Color.White);
                TextRenderer.DrawText(e.Graphics, editar == null ? "Conte quem você é para começar" : "Seus dados na Marke Agenda", Tema.Fonte, new Point(91, 64), Color.FromArgb(210, 230, 255));
            };
            Controls.Add(cab);

            int y = 132;
            Label l1 = new Label { Text = "Nome", AutoSize = true, Location = new Point(30, y), ForeColor = Tema.Fraco }; y += 20;
            TextBox nome = new TextBox { Location = new Point(30, y), Width = 360, Text = editar == null ? "" : editar.Usuario.Nome }; y += 44;
            Label l2 = new Label { Text = "E-mail (opcional)", AutoSize = true, Location = new Point(30, y), ForeColor = Tema.Fraco }; y += 20;
            TextBox email = new TextBox { Location = new Point(30, y), Width = 360, Text = editar == null ? "" : (editar.Usuario.Email ?? "") }; y += 44;
            Label info = new Label { Text = "Seus dados ficam só neste computador.", AutoSize = true, Location = new Point(30, y), ForeColor = Tema.Fraco, Font = Tema.Pequena }; y += 30;
            Button ok = Tema.Botao(editar == null ? "Começar" : "Salvar", true); ok.AutoSize = false; ok.Size = new Size(360, 38); ok.Location = new Point(30, y); y += 46;
            Label erro = new Label { AutoSize = false, Size = new Size(360, 24), Location = new Point(30, y), ForeColor = Tema.Atraso };
            ClientSize = new Size(420, y + 34);

            ok.Click += (s, e) =>
            {
                try
                {
                    if (nome.Text.Trim() == "") throw new Exception("Informe o seu nome.");
                    if (editar == null) Sessao = Usuarios.Criar(nome.Text, email.Text);
                    else { Usuarios.AtualizarCadastro(editar, nome.Text, email.Text); Sessao = editar; }
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex) { erro.Text = ex.Message; }
            };
            AcceptButton = ok;
            Controls.AddRange(new Control[] { l1, nome, l2, email, info, ok, erro });
            Tema.Aplicar(this);
            erro.ForeColor = Tema.Atraso;
            Shown += (s, e) => nome.Focus();
        }
    }
}
