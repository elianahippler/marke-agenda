// Marke Agenda: instalador e desinstalador ("Instalar Marke Agenda.exe").
// Um .exe só, com o programa embutido (recurso MarkeAgenda.exe). Instala por
// usuário, sem pedir administrador. NÃO leva dados nem senhas de ninguém.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MarkeAgenda
{
    static class ProgramaInstalador
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0 && args[0] == "--desinstalar") { Application.Run(new Desinstalador()); return 0; }
            Application.Run(new Instalador());
            return 0;
        }
    }

    static class Instalacao
    {
        public const string Versao = "1.0.0";
        public const string Chave = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MarkeAgenda";
        public const string Exe = "Marke Agenda.exe";
        public const string Desinstalar = "Desinstalar Marke Agenda.exe";

        public static string PastaPadrao { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Marke Agenda"); } }
        public static string AtalhoMesa { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Marke Agenda.lnk"); } }
        public static string AtalhoIniciar { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Marke Agenda.lnk"); } }

        public static string PastaInstalada()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Chave))
            {
                string p = k == null ? null : k.GetValue("InstallLocation") as string;
                return !string.IsNullOrEmpty(p) && File.Exists(Path.Combine(p, Exe)) ? p : null;
            }
        }

        public static void Atalho(string lnkArq, string alvo)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lnkArq));
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkArq });
            Type tl = lnk.GetType();
            tl.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { alvo });
            tl.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { Path.GetDirectoryName(alvo) });
            tl.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { alvo + ",0" });
            tl.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { "Marke Agenda: seu dia organizado" });
            tl.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
        }

        /// Marke Agenda abertas (de qualquer pasta).
        public static List<Process> Abertas()
        {
            return Process.GetProcessesByName("Marke Agenda").ToList();
        }

        /// Pasta de dados de uma Marke Agenda aberta fora da pasta de instalação (para trazer junto).
        public static string DadosDeOutraPasta(string pastaDestino)
        {
            foreach (Process p in Abertas())
            {
                try
                {
                    string dir = Path.GetDirectoryName(p.MainModule.FileName);
                    if (string.Equals(dir.TrimEnd('\\'), pastaDestino.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) continue;
                    string dados = Path.Combine(dir, "dados");
                    if (File.Exists(Path.Combine(dados, "usuarios.json"))) return dados;
                }
                catch { }
            }
            return null;
        }

        public static bool Fechar(IWin32Window dono)
        {
            if (Abertas().Count == 0) return true;
            if (MessageBox.Show(dono, "A Marke Agenda está aberta. Fechar agora para continuar?", "Marke Agenda", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return false;
            foreach (Process p in Abertas()) { try { p.Kill(); p.WaitForExit(5000); } catch { } }
            return Abertas().Count == 0;
        }
    }

    class Instalador : Form
    {
        public Instalador()
        {
            string anterior = Instalacao.PastaInstalada();
            Text = (anterior != null ? "Atualizar" : "Instalar") + " Marke Agenda " + Instalacao.Versao;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 430); Font = Tema.Fonte;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Panel cab = new Panel { Dock = DockStyle.Top, Height = 104 };
            cab.Paint += (s, e) =>
            {
                using (System.Drawing.Drawing2D.LinearGradientBrush b = new System.Drawing.Drawing2D.LinearGradientBrush(cab.ClientRectangle, Color.FromArgb(29, 78, 216), Color.FromArgb(14, 116, 144), 20f))
                    e.Graphics.FillRectangle(b, cab.ClientRectangle);
                if (Icon != null) e.Graphics.DrawIcon(new Icon(Icon, 48, 48), 28, 28);
                TextRenderer.DrawText(e.Graphics, "Marke Agenda", new Font("Segoe UI Semibold", 18f), new Point(88, 26), Color.White);
                TextRenderer.DrawText(e.Graphics, anterior != null ? "Atualizar para a versão " + Instalacao.Versao : "Versão " + Instalacao.Versao, Tema.Fonte, new Point(91, 60), Color.FromArgb(210, 230, 255));
            };
            Controls.Add(cab);

            int y = 124;
            Label l1 = new Label { Text = "Instalar em", AutoSize = true, Location = new Point(28, y), ForeColor = Tema.Fraco }; y += 22;
            TextBox pasta = new TextBox { Text = anterior ?? Instalacao.PastaPadrao, Location = new Point(28, y), Width = 370 };
            Button alterar = Tema.Botao("Alterar", false); alterar.Location = new Point(406, y - 2);
            alterar.Click += (s, e) => { using (FolderBrowserDialog f = new FolderBrowserDialog { SelectedPath = pasta.Text }) if (f.ShowDialog(this) == DialogResult.OK) pasta.Text = Path.Combine(f.SelectedPath, "Marke Agenda"); };
            y += 42;
            CheckBox mesa = new CheckBox { Text = "Atalho na área de trabalho", Checked = true, AutoSize = true, Location = new Point(28, y) }; y += 28;
            CheckBox iniciar = new CheckBox { Text = "Atalho no menu Iniciar", Checked = true, AutoSize = true, Location = new Point(28, y) }; y += 28;
            CheckBox abrir = new CheckBox { Text = "Abrir a Marke Agenda ao terminar", Checked = true, AutoSize = true, Location = new Point(28, y) }; y += 28;
            string outrosDados = Instalacao.DadosDeOutraPasta(pasta.Text);
            CheckBox trazer = new CheckBox { Text = "Trazer minha agenda da Marke Agenda que está aberta", Checked = outrosDados != null, AutoSize = true, Location = new Point(28, y), Visible = outrosDados != null };
            if (outrosDados != null) { new ToolTip().SetToolTip(trazer, "Copia de: " + outrosDados); y += 28; }
            Label info = new Label { Text = "Ela abre sozinha quando o Windows ligar e, ao fechar, fica perto do relógio para avisar dos horários.", AutoSize = false, Size = new Size(464, 40), Location = new Point(28, y + 6), ForeColor = Tema.Fraco };
            y += 52;
            Label status = new Label { AutoSize = false, Size = new Size(464, 22), Location = new Point(28, y), ForeColor = Tema.Fraco };
            Button ok = Tema.Botao(anterior != null ? "Atualizar" : "Instalar", true); ok.AutoSize = false; ok.Size = new Size(140, 36); ok.Location = new Point(352, ClientSize.Height - 54);
            Button no = Tema.Botao("Cancelar", false); no.AutoSize = false; no.Size = new Size(100, 36); no.Location = new Point(244, ClientSize.Height - 54); no.DialogResult = DialogResult.Cancel;

            ok.Click += (s, e) =>
            {
                string destino = pasta.Text.Trim();
                try
                {
                    if (!Instalacao.Fechar(this)) return;
                    ok.Enabled = false; Cursor = Cursors.WaitCursor;
                    status.Text = "Instalando..."; status.Refresh();
                    Instalar(destino, mesa.Checked, iniciar.Checked, trazer.Visible && trazer.Checked ? outrosDados : null);
                    status.ForeColor = Tema.Ok; status.Text = "Pronto! Instalada em " + destino;
                    if (abrir.Checked) Process.Start(Path.Combine(destino, Instalacao.Exe));
                    MessageBox.Show(this, "Marke Agenda instalada.\n\nPara desinstalar: Configurações > Aplicativos > Marke Agenda.", "Marke Agenda", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
                catch (Exception ex) { status.ForeColor = Tema.Atraso; status.Text = "Erro: " + ex.Message; ok.Enabled = true; }
                finally { Cursor = Cursors.Default; }
            };
            AcceptButton = ok; CancelButton = no;
            Controls.AddRange(new Control[] { l1, pasta, alterar, mesa, iniciar, abrir, trazer, info, status, ok, no });
            Tema.Aplicar(this);
        }

        static void Instalar(string pasta, bool mesa, bool iniciar, string trazerDados)
        {
            Directory.CreateDirectory(pasta);
            string exe = Path.Combine(pasta, Instalacao.Exe);
            using (Stream r = Assembly.GetExecutingAssembly().GetManifestResourceStream("MarkeAgenda.exe"))
            {
                if (r == null) throw new Exception("o instalador está sem o programa dentro (recompile pelo build.bat)");
                if (File.Exists(exe)) { try { File.Delete(exe); } catch { File.Move(exe, exe + ".antigo-" + DateTime.Now.Ticks); } }
                using (FileStream f = File.Create(exe)) r.CopyTo(f);
            }
            string desinst = Path.Combine(pasta, Instalacao.Desinstalar);
            File.Copy(Application.ExecutablePath, desinst, true);

            // traz a agenda da outra pasta (só se aqui ainda não tiver dados)
            string dadosAqui = Path.Combine(pasta, "dados");
            if (trazerDados != null && !File.Exists(Path.Combine(dadosAqui, "usuarios.json")))
            {
                Directory.CreateDirectory(dadosAqui);
                foreach (string f in Directory.GetFiles(trazerDados))
                    if (f.EndsWith(".json") || f.EndsWith(".dat")) File.Copy(f, Path.Combine(dadosAqui, Path.GetFileName(f)), true);
            }

            if (mesa) Instalacao.Atalho(Instalacao.AtalhoMesa, exe); else { try { File.Delete(Instalacao.AtalhoMesa); } catch { } }
            if (iniciar) Instalacao.Atalho(Instalacao.AtalhoIniciar, exe); else { try { File.Delete(Instalacao.AtalhoIniciar); } catch { } }
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                k.SetValue("MarkeAgenda", "\"" + exe + "\"");

            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Instalacao.Chave))
            {
                k.SetValue("DisplayName", "Marke Agenda");
                k.SetValue("DisplayVersion", Instalacao.Versao);
                k.SetValue("Publisher", "Marke Sistemas");
                k.SetValue("DisplayIcon", exe + ",0");
                k.SetValue("InstallLocation", pasta);
                k.SetValue("UninstallString", "\"" + desinst + "\" --desinstalar");
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
            }
        }
    }

    class Desinstalador : Form
    {
        public Desinstalador()
        {
            Text = "Desinstalar Marke Agenda";
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(480, 230); Font = Tema.Fonte;
            string pasta = Instalacao.PastaInstalada() ?? Path.GetDirectoryName(Application.ExecutablePath);
            Label t = new Label { Text = "Desinstalar a Marke Agenda", Font = new Font("Segoe UI Semibold", 14f), AutoSize = true, Location = new Point(22, 20) };
            Label d = new Label { Text = "de " + pasta, AutoSize = false, Size = new Size(436, 20), Location = new Point(24, 54), ForeColor = Tema.Fraco };
            CheckBox apagar = new CheckBox { Text = "Apagar também a minha agenda (tarefas, notas e acessos)", AutoSize = true, Location = new Point(24, 92) };
            Label erro = new Label { AutoSize = false, Size = new Size(436, 36), Location = new Point(24, 124), ForeColor = Tema.Atraso };
            Button ok = Tema.Botao("Desinstalar", true); ok.Location = new Point(250, 176);
            Button no = Tema.Botao("Cancelar", false); no.Location = new Point(370, 176); no.DialogResult = DialogResult.Cancel;
            ok.Click += (s, e) =>
            {
                if (apagar.Checked && MessageBox.Show(this, "Apagar a agenda não tem volta. Continuar?", "Marke Agenda", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                if (!Instalacao.Fechar(this)) return;
                try
                {
                    foreach (string a in new[] { Instalacao.AtalhoMesa, Instalacao.AtalhoIniciar }) { try { File.Delete(a); } catch { } }
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) k.DeleteValue("MarkeAgenda", false);
                    Registry.CurrentUser.DeleteSubKeyTree(Instalacao.Chave, false);
                    try { File.Delete(Path.Combine(pasta, Instalacao.Exe)); } catch { }
                    if (apagar.Checked) { try { Directory.Delete(Path.Combine(pasta, "dados"), true); } catch { } }
                    // o próprio desinstalador (e a pasta, se ficar vazia) saem depois que ele fechar
                    Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 > nul & del /f /q \"" + Path.Combine(pasta, Instalacao.Desinstalar) + "\" & rmdir \"" + pasta + "\"")
                    { CreateNoWindow = true, UseShellExecute = false });
                    MessageBox.Show(this, "Marke Agenda desinstalada." + (apagar.Checked ? "" : "\n\nSua agenda ficou guardada em " + Path.Combine(pasta, "dados") + "."), "Marke Agenda");
                    Close();
                }
                catch (Exception ex) { erro.Text = ex.Message; }
            };
            CancelButton = no;
            Controls.AddRange(new Control[] { t, d, apagar, erro, ok, no });
            Tema.Aplicar(this);
        }
    }
}
