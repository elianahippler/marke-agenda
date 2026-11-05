// Marke Agenda: tela principal (calendário do dia, colunas laterais, notas).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MarkeAgenda
{
    static class Programa
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool primeiro;
            using (Mutex m = new Mutex(true, "MarkeAgenda-" + Environment.UserName, out primeiro))
            {
                if (!primeiro) { MessageBox.Show("A Marke Agenda já está aberta (veja perto do relógio do Windows).", "Marke Agenda"); return; }
                Application.ThreadException += (s, e) => { Dados.Log("Erro: " + e.Exception); MessageBox.Show(e.Exception.Message, "Marke Agenda"); };
                Sessao sessao = Usuarios.Atual();
                if (ContaGoogle.Configurado)
                {
                    // login é a conta Google (o mesmo do celular)
                    if (sessao == null || string.IsNullOrEmpty(sessao.Usuario.GoogleRefresh))
                        using (TelaLoginGoogle l = new TelaLoginGoogle(sessao != null))
                        {
                            if (l.ShowDialog() == DialogResult.OK) sessao = l.Sessao;
                            else if (sessao == null) return;
                        }
                }
                else if (sessao == null)
                    using (TelaCadastro l = new TelaCadastro(null))
                    {
                        if (l.ShowDialog() != DialogResult.OK) return;
                        sessao = l.Sessao;
                    }
                Application.Run(new Tela(sessao));
            }
        }
    }

    public static class Tema
    {
        public static Color Fundo = Color.FromArgb(15, 23, 42);
        public static Color Painel = Color.FromArgb(24, 36, 62);
        public static Color Barra = Color.FromArgb(18, 30, 56);
        public static Color Linha = Color.FromArgb(40, 58, 92);
        public static Color Texto = Color.FromArgb(230, 238, 250);
        public static Color Fraco = Color.FromArgb(140, 160, 190);
        public static Color Roxo = Color.FromArgb(37, 99, 235);      // destaque (azul)
        public static Color Claro = Color.FromArgb(56, 189, 248);    // azul claro: linha da hora atual
        public static Color Agora = Color.FromArgb(25, 45, 85);
        public static Color Alvo = Color.FromArgb(30, 64, 140);      // horário sob o arraste
        public static Color Atraso = Color.FromArgb(248, 113, 113);
        public static Color Ok = Color.FromArgb(52, 211, 153);
        public static Font Fonte = new Font("Segoe UI", 9.5f);
        public static Font Negrito = new Font("Segoe UI Semibold", 9.5f);
        public static Font Pequena = new Font("Segoe UI", 8f);
        public static Font PequenaN = new Font("Segoe UI Semibold", 7.5f);
        public static Font Titulo = new Font("Segoe UI Semibold", 13f);
        public static Font Secao = new Font("Segoe UI Semibold", 11.5f);
        public static Font Glifos = new Font("Segoe MDL2 Assets", 9f);

        public static Button Botao(string texto, bool destaque)
        {
            Button b = new Button { Text = texto, AutoSize = true, FlatStyle = FlatStyle.Flat, Height = 30, Padding = new Padding(6, 0, 6, 0), Cursor = Cursors.Hand };
            b.BackColor = destaque ? Roxo : Painel; b.ForeColor = Texto;
            b.FlatAppearance.BorderColor = destaque ? Roxo : Linha;
            b.FlatAppearance.MouseOverBackColor = destaque ? Misturar(Roxo, Color.White, 0.85) : Linha;
            return b;
        }

        public static void Aplicar(Control c)
        {
            c.Font = c.Font.Bold ? c.Font : (c.Font.Size == Fonte.Size ? Fonte : c.Font);
            if (c is Form || c is Panel || c is TabPage || c is GroupBox || c is Label || c is CheckBox || c is RadioButton)
            {
                c.BackColor = c is Form || c is TabPage ? Fundo : (c.BackColor == SystemColors.Control ? Fundo : c.BackColor);
                if (c.ForeColor == SystemColors.ControlText) c.ForeColor = Texto;
            }
            if (c is TextBoxBase || c is ComboBox || c is ListBox || c is NumericUpDown)
            { c.BackColor = Painel; c.ForeColor = Texto; }
            if (c is TextBoxBase) ((TextBoxBase)c).BorderStyle = BorderStyle.FixedSingle;
            if (c is NumericUpDown) ((NumericUpDown)c).BorderStyle = BorderStyle.FixedSingle;
            if (c is DataGridView)
            {
                DataGridView g = (DataGridView)c;
                g.BackgroundColor = Painel; g.GridColor = Linha; g.BorderStyle = BorderStyle.None;
                g.EnableHeadersVisualStyles = false;
                g.ColumnHeadersDefaultCellStyle.BackColor = Linha; g.ColumnHeadersDefaultCellStyle.ForeColor = Texto;
                g.DefaultCellStyle.BackColor = Painel; g.DefaultCellStyle.ForeColor = Texto;
                g.DefaultCellStyle.SelectionBackColor = Roxo; g.RowHeadersVisible = false;
            }
            foreach (Control f in c.Controls) Aplicar(f);
            if (c is Form) Escuro(c);
        }

        public static Color Escurecer(Color c, double f) { return Color.FromArgb((int)(c.R * f), (int)(c.G * f), (int)(c.B * f)); }

        /// Mistura "a" sobre "b": f = 1 é só "a".
        public static Color Misturar(Color a, Color b, double f)
        {
            return Color.FromArgb((int)(a.R * f + b.R * (1 - f)), (int)(a.G * f + b.G * (1 - f)), (int)(a.B * f + b.B * (1 - f)));
        }

        public static GraphicsPath Caminho(Rectangle r, int raio)
        {
            GraphicsPath p = new GraphicsPath();
            int d = raio * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Arredondado(Graphics g, Rectangle r, int raio, Brush b)
        {
            using (GraphicsPath p = Caminho(r, raio)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.FillPath(b, p); }
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string app, string id);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

        /// Tira o tema do Windows (o calendário passa a usar as cores definidas no código).
        public static void SemTema(Control c) { SetWindowTheme(c.Handle, "", ""); }

        static void QuandoTiverHandle(Control c, Action a)
        {
            if (c.IsHandleCreated) a(); else c.HandleCreated += (s, e) => a();
        }

        /// Barras de rolagem escuras (tema escuro do Windows 10/11) e barra de título escura nas janelas.
        public static void Escuro(Control c)
        {
            if (c is ListBox || c is TextBoxBase || c is DataGridView || c is NumericUpDown || (c is ScrollableControl && ((ScrollableControl)c).AutoScroll))
                QuandoTiverHandle(c, () => SetWindowTheme(c.Handle, "DarkMode_Explorer", null));
            else if (c is ComboBox)
            {
                Escuros.Combo((ComboBox)c);
                QuandoTiverHandle(c, () => SetWindowTheme(c.Handle, "DarkMode_CFD", null));
            }
            if (c is Form)
                QuandoTiverHandle(c, () => { int v = 1; DwmSetWindowAttribute(c.Handle, 20, ref v, 4); });
            foreach (Control f in c.Controls) Escuro(f);
            c.ControlAdded += (s, e) => Escuro(e.Control);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
        public static void Dica(TextBox t, string texto) { SendMessage(t.Handle, 0x1501, (IntPtr)1, texto); }

        public static void Menu(ToolStrip m)
        {
            m.Renderer = new ToolStripProfessionalRenderer(new CoresMenu());
            m.BackColor = Painel; m.ForeColor = Texto; m.Font = Fonte;
        }

        class CoresMenu : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Painel; } }
            public override Color MenuBorder { get { return Linha; } }
            public override Color MenuItemBorder { get { return Roxo; } }
            public override Color MenuItemSelected { get { return Linha; } }
            public override Color MenuItemSelectedGradientBegin { get { return Linha; } }
            public override Color MenuItemSelectedGradientEnd { get { return Linha; } }
            public override Color ImageMarginGradientBegin { get { return Painel; } }
            public override Color ImageMarginGradientMiddle { get { return Painel; } }
            public override Color ImageMarginGradientEnd { get { return Painel; } }
            public override Color SeparatorDark { get { return Linha; } }
            public override Color SeparatorLight { get { return Painel; } }
            public override Color CheckBackground { get { return Roxo; } }
            public override Color CheckSelectedBackground { get { return Roxo; } }
        }
    }

    /// Desenho de uma tarefa: o mesmo na lista, na agenda e no arraste.
    public static class Cartao
    {
        public static bool Atrasada(Tarefa t, DateTime dia, int intervalo)
        {
            if (!t.EstaAgendada() || t.ConcluidaEm(dia)) return false;
            DateTime h;
            if (!DateTime.TryParse(Tarefa.Dia(dia) + " " + t.Hora, out h)) return false;
            return h.AddMinutes(t.Duracao > 0 ? t.Duracao : intervalo) < DateTime.Now;
        }

        static string Sigla(Tarefa t)
        {
            switch (t.Origem)
            {
                case "Google": return "GOOGLE";
                case "TomTicket": return "TOMTICKET";
                case "Markedesk": return "MARKEDESK";
            }
            return null;
        }

        /// Onde fica a bolinha de "resolvida" dentro do cartão.
        public static Rectangle Bolinha(Rectangle r) { return new Rectangle(r.X + 13, r.Y + (r.Height - 18) / 2, 18, 18); }

        /// Lixeirinha "não é minha" (canto de baixo, à direita): aparece com o mouse em cima.
        public static Rectangle Lixeira(Rectangle r) { return new Rectangle(r.Right - 26, r.Bottom - 22, 20, 18); }
        static readonly Font fonteLixeira = new Font("Segoe MDL2 Assets", 9f);

        public static string Duracao(int min)
        {
            if (min <= 0) return "";
            if (min < 60) return min + "min";
            return (min / 60) + "h" + (min % 60 > 0 ? (min % 60).ToString("00") : "");
        }

        /// naAgenda: mostra horário e duração; senão, mostra a data planejada.
        public static void Desenhar(Graphics g, Rectangle r, Tarefa t, Dados d, DateTime dia, bool naAgenda, bool realce, Color fundo)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool feita = t.ConcluidaEm(dia);
            bool atrasada = naAgenda && Atrasada(t, dia, d.Config.Intervalo);
            Color cor = d.CorDe(t);
            Color corpo = Tema.Misturar(cor, fundo, feita ? 0.12 : realce ? 0.42 : 0.28);
            using (SolidBrush b = new SolidBrush(corpo)) Tema.Arredondado(g, r, 7, b);
            if (realce)
                using (GraphicsPath p = Tema.Caminho(r, 7)) using (Pen pen = new Pen(Tema.Misturar(cor, Color.White, 0.7))) g.DrawPath(pen, p);
            using (SolidBrush b = new SolidBrush(feita ? Tema.Misturar(cor, fundo, 0.4) : cor))
                Tema.Arredondado(g, new Rectangle(r.X + 3, r.Y + 5, 4, r.Height - 10), 2, b);

            int tx = r.X + 13;
            if (!t.Fixa)
            {
                Rectangle bol = Bolinha(r);
                tx = bol.Right + 8;
                if (feita)
                {
                    using (SolidBrush b = new SolidBrush(Tema.Ok)) g.FillEllipse(b, bol);
                    using (Pen p = new Pen(Tema.Fundo, 2f))
                        g.DrawLines(p, new[] { new Point(bol.X + 4, bol.Y + 9), new Point(bol.X + 7, bol.Y + 12), new Point(bol.X + 13, bol.Y + 5) });
                }
                else
                    using (Pen p = new Pen(Tema.Misturar(cor, Color.White, 0.75), 1.6f)) g.DrawEllipse(p, bol);
            }

            int direita = r.Right - 8;
            string sigla = Sigla(t);
            if (sigla != null)
            {
                Size ts = TextRenderer.MeasureText(sigla, Tema.PequenaN);
                Rectangle pill = new Rectangle(r.Right - ts.Width - 8, r.Y + 5, ts.Width + 2, 15);
                using (SolidBrush b = new SolidBrush(Tema.Misturar(cor, fundo, 0.45))) Tema.Arredondado(g, pill, 4, b);
                TextRenderer.DrawText(g, sigla, Tema.PequenaN, pill, Tema.Texto, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                direita = pill.X - 4;
            }

            Font f = feita ? new Font(Tema.Negrito, FontStyle.Strikeout) : Tema.Negrito;
            string titulo = (feita ? "✓ " : "") + t.Titulo;
            TextRenderer.DrawText(g, titulo, f, new Rectangle(tx, r.Y + 4, direita - tx, 20), feita ? Tema.Fraco : Tema.Texto, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            string sub;
            if (t.Fixa) sub = "Fixa  ·  " + d.TipoDe(t).Nome + (t.Duracao > 0 ? "  ·  " + Duracao(t.Duracao) : "");
            else if (t.Aguardando) sub = "Aguardando" + (string.IsNullOrEmpty(t.AguardandoDe) ? "" : ": " + t.AguardandoDe);
            else if (naAgenda) sub = t.Hora + (t.Duracao > 0 ? " (" + Duracao(t.Duracao) + ")" : "") + "  ·  " + d.TipoDe(t).Nome;
            else
            {
                sub = d.TipoDe(t).Nome;
                DateTime dt;
                if (!string.IsNullOrEmpty(t.Data) && DateTime.TryParse(t.Data, out dt))
                    sub += "  ·  " + dt.ToString("dd/MM") + (t.EstaAgendada() ? " " + t.Hora : "");
            }
            if (t.Repete()) sub += "  ·  ↻ " + t.DetalheRepeticao();
            Color corSub = Tema.Fraco;
            if (atrasada) { sub = "Atrasada  ·  " + sub; corSub = Tema.Atraso; }
            bool lixo = realce && !t.Fixa;
            TextRenderer.DrawText(g, sub, Tema.Pequena, new Rectangle(tx, r.Y + 23, r.Right - tx - (lixo ? 30 : 8), 17), corSub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (lixo)
                TextRenderer.DrawText(g, "", fonteLixeira, Lixeira(r), Tema.Atraso, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// Janela transparente que segue o mouse durante o arraste.
    class Fantasma : Form
    {
        public Fantasma(Bitmap b)
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            TopMost = true; Size = b.Size; BackgroundImage = b; BackColor = Color.Magenta; TransparencyKey = Color.Magenta; Opacity = 0.78;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x20 | 0x80 | 0x08000000;   // transparente ao mouse, sem barra de tarefas, sem foco
                return cp;
            }
        }
    }

    static class Arraste
    {
        public static void Iniciar(Control fonte, Tarefa t, Dados d, DateTime dia, Size tamanho, Point pegada)
        {
            Iniciar(fonte, t, d, dia, tamanho, pegada, t.Fixa ? "fixa:" + t.Id : t.Id);
        }

        public static void Iniciar(Control fonte, Tarefa t, Dados d, DateTime dia, Size tamanho, Point pegada, string dado)
        {
            Bitmap bmp = new Bitmap(Math.Max(140, tamanho.Width), Math.Max(40, tamanho.Height));
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Magenta);
                Cartao.Desenhar(g, new Rectangle(0, 0, bmp.Width - 1, bmp.Height - 1), t, d, dia, false, true, Tema.Fundo);
            }
            Fantasma f = new Fantasma(bmp);
            Action mover = () => { Point p = Cursor.Position; f.Location = new Point(p.X - pegada.X, p.Y - pegada.Y); };
            GiveFeedbackEventHandler gf = (s, e) => mover();
            fonte.GiveFeedback += gf;
            mover(); f.Show();
            try { fonte.DoDragDrop(dado, t.Fixa ? DragDropEffects.Copy : DragDropEffects.Move); }
            finally { fonte.GiveFeedback -= gf; f.Close(); f.Dispose(); bmp.Dispose(); }
        }
    }

    public class Tela : Form
    {
        Dados dados;
        Sessao sessao;
        DateTime dia = DateTime.Today;

        Panel topo, painelAgenda, painelNotas, grade, linhaAgora;
        Label lbDia, lbStatus, lbProgresso, lbTarefas, lbAguardo, vazioAguardo, lbFixas, vazioFixas;
        ProgressoDia barraProgresso;
        ListBox lista, listaAguardo, listaFixas;
        TextBox busca, notaRapida, rapida;
        ComboBox filtroTipo, filtroTipoFixas;
        RadioButton rbSemHorario, rbTodas, rbResolvidas;
        Button btAgenda, btNotas, btMax, btMais;
        Button escolher;
        NotifyIcon icone;
        System.Windows.Forms.Timer relogio, autoAtualizar;
        bool atualizando, sair, telaCheia;
        Rectangle boundsAntes; FormWindowState estadoAntes;
        string ultimoSlotAgora = "";
        Dictionary<string, Panel> linhas = new Dictionary<string, Panel>();

        ListBox listaNotas; TextBox notaTitulo, notaTexto, buscaNota;
        System.Windows.Forms.Timer salvarNota; bool carregandoNota; Nota notaAtual;

        public Tela(Sessao s)
        {
            sessao = s;
            dados = Dados.Carregar(s);
            // o login Google também é o acesso do Google Agenda e do Drive
            if (!string.IsNullOrEmpty(s.Usuario.GoogleRefresh)) dados.Config.GoogleRefresh = s.Usuario.GoogleRefresh;
            Text = "Marke Agenda"; FormBorderStyle = FormBorderStyle.None;
            Width = 1280; Height = 820; StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1100, 600); BackColor = Tema.Linha; ForeColor = Tema.Texto; Font = Tema.Fonte;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            MontarTopo();
            MontarAgenda();
            MontarNotas();
            lbStatus = new Label { Dock = DockStyle.Bottom, Height = 26, ForeColor = Tema.Fraco, BackColor = Tema.Barra, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0), Text = "Olá, " + s.Usuario.Nome + "!" };
            Controls.Add(lbStatus);
            // O que preenche o meio precisa ficar na frente, senão o topo cobre parte dele.
            painelAgenda.BringToFront(); painelNotas.BringToFront();
            MostrarAba(true);

            MontarBandeja();
            Tema.Escuro(this);

            // Padrão: abre com o Windows e, ao fechar, fica em segundo plano (perto do relógio).
            // Feito uma vez só; depois vale o que estiver marcado no menu.
            if (!dados.Config.InicioConfigurado)
            {
                try { DefinirInicioWindows(true); } catch (Exception ex) { Dados.Log("Início com o Windows: " + ex.Message); }
                dados.Config.FecharParaBandeja = true;
                dados.Config.InicioConfigurado = true;
                dados.Salvar();
            }
            else if (IniciaComWindows()) DefinirInicioWindows(true);   // mantém o caminho do exe atualizado

            relogio = new System.Windows.Forms.Timer { Interval = 30000 };
            relogio.Tick += (o, e) =>
            {
                VerificarAvisos();
                if (SlotAgora() != ultimoSlotAgora) MontarGrade(); else { PosicionarLinhaAgora(); grade.Invalidate(true); }
            };
            relogio.Start();
            autoAtualizar = new System.Windows.Forms.Timer();
            autoAtualizar.Tick += (o, e) => AtualizarIntegracoes(false);
            AjustarAutoAtualizar();

            // sincronização com o celular (Google Drive): a cada 2 minutos e logo depois de mudar algo
            timerSinc = new System.Windows.Forms.Timer { Interval = 120000 };
            timerSinc.Tick += (o, e) => Sincronizar(false);
            timerSinc.Start();
            sincLogo = new System.Windows.Forms.Timer { Interval = 8000 };
            sincLogo.Tick += (o, e) => { sincLogo.Stop(); Sincronizar(false); };
            dados.Salvou += () => { if (!sincronizando) { sincLogo.Stop(); sincLogo.Start(); } };

            Load += (o, e) => { AjustarLimiteMaximizar(); WindowState = FormWindowState.Maximized; };
            Move += (o, e) => AjustarLimiteMaximizar();
            Shown += (o, e) =>
            {
                AtualizarTudo();
                VerificarAvisos();
                Sincronizar(false);
                ProcurarAtualizacao(false);
                if (dados.Config.AtualizarAoAbrir) AtualizarIntegracoes(false);
                if (dados.Tarefas.Count == 0 && string.IsNullOrEmpty(dados.Config.TomToken) && string.IsNullOrEmpty(dados.Config.MdUrlApi) && string.IsNullOrEmpty(dados.Config.GoogleRefresh))
                    lbStatus.Text = "Bem-vindo(a), " + s.Usuario.Nome + "! Comece por Acessos para ligar o seu Google Agenda, TomTicket e Markedesk.";
            };
            FormClosing += (o, e) =>
            {
                if (!sair && e.CloseReason == CloseReason.UserClosing && dados.Config.FecharParaBandeja)
                {
                    e.Cancel = true; Hide();
                    icone.ShowBalloonTip(4000, "Marke Agenda", "Continua aberta aqui para avisar dos horários. Botão direito para sair.", ToolTipIcon.Info);
                    return;
                }
                SalvarNotaAgora(); dados.NotaRapida = notaRapida.Text; dados.Salvar(); icone.Visible = false;
            };
            KeyPreview = true;
            KeyDown += (o, e) =>
            {
                if (e.KeyCode == Keys.F5) { AtualizarIntegracoes(true); e.Handled = true; }
                else if (e.KeyCode == Keys.F11) { AlternarTelaCheia(); e.Handled = true; }
                else if (e.KeyCode == Keys.Escape && telaCheia) { AlternarTelaCheia(); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.N) { NovaTarefa(null); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.F) { MostrarAba(true); busca.Focus(); e.Handled = true; }
                else if (e.Alt && e.KeyCode == Keys.Left) { IrPara(dia.AddDays(-1)); e.Handled = true; }
                else if (e.Alt && e.KeyCode == Keys.Right) { IrPara(dia.AddDays(1)); e.Handled = true; }
            };
        }

        // ------------------------------------------------------------------ janela sem a barra do Windows
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x20000 | 0x10000;   // minimizar/maximizar pela barra de tarefas
                cp.ClassStyle |= 0x20000;        // sombra
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x84 && WindowState == FormWindowState.Normal && !telaCheia)   // WM_NCHITTEST: bordas para redimensionar
            {
                long lp = m.LParam.ToInt64();
                Point p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                const int b = 6;
                bool l = p.X < b, r = p.X >= ClientSize.Width - b, t = p.Y < b, bo = p.Y >= ClientSize.Height - b;
                if (t && l) m.Result = (IntPtr)13; else if (t && r) m.Result = (IntPtr)14;
                else if (bo && l) m.Result = (IntPtr)16; else if (bo && r) m.Result = (IntPtr)17;
                else if (l) m.Result = (IntPtr)10; else if (r) m.Result = (IntPtr)11;
                else if (t) m.Result = (IntPtr)12; else if (bo) m.Result = (IntPtr)15;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Padding = WindowState == FormWindowState.Normal && !telaCheia ? new Padding(1) : Padding.Empty;
            if (btMax != null) btMax.Text = WindowState == FormWindowState.Maximized ? "" : "";
        }

        void AjustarLimiteMaximizar()
        {
            if (telaCheia) return;
            Screen s = Screen.FromControl(this);
            Rectangle wa = s.WorkingArea, sb = s.Bounds;
            MaximizedBounds = new Rectangle(wa.X - sb.X, wa.Y - sb.Y, wa.Width, wa.Height);
        }

        void AlternarMaximizar()
        {
            if (telaCheia) { AlternarTelaCheia(); return; }
            AjustarLimiteMaximizar();
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        }

        void AlternarTelaCheia()
        {
            if (!telaCheia)
            {
                estadoAntes = WindowState; boundsAntes = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                Rectangle tela = Screen.FromControl(this).Bounds;
                telaCheia = true;
                WindowState = FormWindowState.Normal;
                Bounds = tela;
                lbStatus.Text = "Tela cheia. F11 ou Esc para sair.";
            }
            else
            {
                telaCheia = false;
                Bounds = boundsAntes;
                AjustarLimiteMaximizar();
                WindowState = estadoAntes;
            }
            OnResize(EventArgs.Empty);
        }

        void Arrastavel(Control c)
        {
            c.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left || telaCheia) return;
                if (e.Clicks == 2) { AlternarMaximizar(); return; }
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);   // WM_NCLBUTTONDOWN na "barra de título"
            };
        }

        Button BotaoJanela(string glifo, bool fechar)
        {
            Button b = new Button { Text = glifo, Font = Tema.Glifos, Width = 46, Dock = DockStyle.Right, FlatStyle = FlatStyle.Flat, ForeColor = Tema.Texto, BackColor = Tema.Barra, TabStop = false };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = fechar ? Color.FromArgb(196, 43, 28) : Tema.Linha;
            return b;
        }

        // ------------------------------------------------------------------ topo
        void MontarTopo()
        {
            topo = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Tema.Barra };
            topo.Paint += (s, e) =>
            {
                using (LinearGradientBrush b = new LinearGradientBrush(topo.ClientRectangle, Color.FromArgb(23, 48, 105), Tema.Barra, 0f))
                    e.Graphics.FillRectangle(b, topo.ClientRectangle);
                using (Pen p = new Pen(Tema.Linha)) e.Graphics.DrawLine(p, 0, topo.Height - 1, topo.Width, topo.Height - 1);
            };
            PictureBox logo = new PictureBox { Size = new Size(26, 26), Location = new Point(14, 13), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            try { logo.Image = new Icon(Icon ?? SystemIcons.Application, 32, 32).ToBitmap(); } catch { }
            Label titulo = new Label { Text = "Marke Agenda", Font = Tema.Titulo, ForeColor = Tema.Texto, AutoSize = true, Location = new Point(46, 13), BackColor = Color.Transparent };
            btAgenda = Tema.Botao("Agenda", true); btAgenda.Location = new Point(190, 11);
            btNotas = Tema.Botao("Notas", false); btNotas.Location = new Point(264, 11);
            btAgenda.Click += (s, e) => MostrarAba(true);
            btNotas.Click += (s, e) => MostrarAba(false);

            Button fechar = BotaoJanela("", true);
            btMax = BotaoJanela("", false);
            Button min = BotaoJanela("", false);
            Button cheia = BotaoJanela("", false);
            fechar.Click += (s, e) => Close();
            btMax.Click += (s, e) => AlternarMaximizar();
            min.Click += (s, e) => WindowState = FormWindowState.Minimized;
            cheia.Click += (s, e) => AlternarTelaCheia();
            ToolTip tt = new ToolTip();
            tt.SetToolTip(cheia, "Tela cheia (F11)"); tt.SetToolTip(min, "Minimizar"); tt.SetToolTip(btMax, "Maximizar"); tt.SetToolTip(fechar, "Fechar");

            FlowLayoutPanel dir = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Padding = new Padding(0, 11, 14, 0), BackColor = Color.Transparent };
            Button btAtualizar = Tema.Botao("⟳  Atualizar", false);
            Button btTipos = Tema.Botao("Tipos", false);
            Button btAcessos = Tema.Botao("Acessos", false);
            Button btConfig = Tema.Botao("Configurações", false);
            btMais = Tema.Botao(sessao.Usuario.Nome + "  ▾", false);
            btAtualizar.Click += (s, e) => AtualizarIntegracoes(true);
            btTipos.Click += (s, e) => { using (DialogoTipos f = new DialogoTipos(dados)) f.ShowDialog(this); dados.Salvar(); AtualizarTudo(); };
            btAcessos.Click += (s, e) => { using (DialogoAcessos f = new DialogoAcessos(dados)) f.ShowDialog(this); dados.Salvar(); };
            btConfig.Click += (s, e) => { using (DialogoConfig f = new DialogoConfig(dados)) f.ShowDialog(this); dados.Salvar(); AjustarAutoAtualizar(); AplicarAguardo(); AtualizarTudo(); };
            btMais.Click += (s, e) => MenuMais().Show(btMais, new Point(0, btMais.Height));
            tt.SetToolTip(btAtualizar, "Busca Google Agenda, TomTicket e Markedesk (F5)");
            tt.SetToolTip(btMais, "Mais opções e meu cadastro");
            dir.Controls.AddRange(new Control[] { btAtualizar, btTipos, btAcessos, btConfig, btMais });
            foreach (Control b in dir.Controls) b.Margin = new Padding(4, 0, 0, 0);

            topo.Controls.AddRange(new Control[] { logo, titulo, btAgenda, btNotas, dir, cheia, min, btMax, fechar });
            dir.BringToFront();
            Arrastavel(topo); Arrastavel(titulo); Arrastavel(logo);
            Controls.Add(topo);
        }

        ContextMenuStrip MenuMais()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            Tema.Menu(m);
            m.Items.Add("Levar pendentes deste dia para o próximo dia útil", null, (s, e) => LevarPendentes());
            m.Items.Add("Copiar o plano do dia", null, (s, e) => CopiarPlano());
            m.Items.Add("Limpar concluídas sem horário", null, (s, e) => LimparConcluidas());
            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem iniciar = new ToolStripMenuItem("Abrir junto com o Windows") { Checked = IniciaComWindows(), CheckOnClick = true };
            iniciar.CheckedChanged += (s, e) => DefinirInicioWindows(iniciar.Checked);
            ToolStripMenuItem bandeja = new ToolStripMenuItem("Fechar manda para perto do relógio") { Checked = dados.Config.FecharParaBandeja, CheckOnClick = true };
            bandeja.CheckedChanged += (s, e) => { dados.Config.FecharParaBandeja = bandeja.Checked; dados.Salvar(); };
            m.Items.Add(iniciar); m.Items.Add(bandeja);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Testar lembrete", null, (s, e) =>
                Lembrete.Mostrar(this, new Tarefa { Id = "teste", Titulo = "Assim aparece o seu lembrete", TipoId = "lembrete", Hora = DateTime.Now.ToString("HH:mm"), Origem = "Manual" }, dados.Config.AvisoSom));
            m.Items.Add("Atalhos do teclado", null, (s, e) => MessageBox.Show(this,
                "F5: atualizar integrações\nF11: tela cheia (Esc sai)\nCtrl+N: nova tarefa\nCtrl+F: buscar tarefa\nAlt+Seta: dia anterior / próximo\n" +
                "Na lista: Enter edita, Espaço conclui, Delete exclui\n\n" +
                "Na tarefa rápida, comece com o horário para já planejar: \"14:30 Ligar para o cliente\".", "Atalhos"));
            if (dados.NaoSaoMinhas.Count > 0)
                m.Items.Add("Trazer de volta as " + dados.NaoSaoMinhas.Count + " marcadas como \"não é minha\"", null, (s, e) =>
                {
                    dados.NaoSaoMinhas.Clear(); dados.Salvar();
                    lbStatus.Text = "Elas voltam na próxima atualização.";
                    AtualizarIntegracoes(false);
                });
            int naLixeira = dados.Tarefas.Count(t => !t.Ativa());
            m.Items.Add("Lixeira" + (naLixeira > 0 ? " (" + naLixeira + ")" : ""), null, (s, e) =>
            {
                using (DialogoLixeira f = new DialogoLixeira(dados)) f.ShowDialog(this);
                dados.Salvar(); AtualizarTudo();
            });
            m.Items.Add("Abrir a pasta de dados", null, (s, e) => Process.Start(Dados.Pasta));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Procurar atualização (versão " + VersaoApp.Atual + ")", null, (s, e) => ProcurarAtualizacao(true));
            m.Items.Add("Sincronizar com o celular agora", null, (s, e) => Sincronizar(true));
            if (ContaGoogle.Configurado)
            {
                if (string.IsNullOrEmpty(sessao.Usuario.GoogleRefresh))
                    m.Items.Add("Entrar com Google", null, (s, e) =>
                    {
                        using (TelaLoginGoogle f = new TelaLoginGoogle(true))
                            if (f.ShowDialog(this) == DialogResult.OK)
                            {
                                sessao.Usuario.GoogleRefresh = f.Sessao.Usuario.GoogleRefresh;
                                dados.Config.GoogleRefresh = f.Sessao.Usuario.GoogleRefresh; dados.Config.GoogleClientId = "";
                                dados.Salvar(); btMais.Text = f.Sessao.Usuario.Nome + "  ▾";
                                Sincronizar(true); AtualizarIntegracoes(false);
                            }
                    });
                else
                    m.Items.Add("Sair da conta Google (" + sessao.Usuario.GoogleEmail + ")", null, (s, e) =>
                    {
                        if (MessageBox.Show(this, "Sair da conta " + sessao.Usuario.GoogleEmail + "?\n\nA agenda continua salva no Google Drive e neste PC. A Marke Agenda volta para a tela de entrar.", "Sair", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                        Usuarios.SairGoogle(sessao); dados.Config.GoogleRefresh = ""; dados.Salvar();
                        sair = true;
                        icone.Visible = false;
                        Application.Restart();   // abre de novo já na tela "Entrar com Google"
                    });
            }
            else
                m.Items.Add("Meu cadastro", null, (s, e) =>
                {
                    using (TelaCadastro f = new TelaCadastro(sessao))
                        if (f.ShowDialog(this) == DialogResult.OK) btMais.Text = sessao.Usuario.Nome + "  ▾";
                });
            return m;
        }

        void MostrarAba(bool agenda)
        {
            painelAgenda.Visible = agenda; painelNotas.Visible = !agenda;
            btAgenda.BackColor = agenda ? Tema.Roxo : Tema.Painel; btAgenda.FlatAppearance.BorderColor = agenda ? Tema.Roxo : Tema.Linha;
            btNotas.BackColor = !agenda ? Tema.Roxo : Tema.Painel; btNotas.FlatAppearance.BorderColor = !agenda ? Tema.Roxo : Tema.Linha;
            if (!agenda) CarregarNotas(null);
        }

        // ------------------------------------------------------------------ bandeja
        void MontarBandeja()
        {
            icone = new NotifyIcon { Icon = Icon ?? SystemIcons.Information, Text = "Marke Agenda", Visible = true };
            ContextMenuStrip m = new ContextMenuStrip();
            Tema.Menu(m);
            m.Items.Add("Abrir", null, (s, e) => Reabrir());
            m.Items.Add("Atualizar integrações", null, (s, e) => AtualizarIntegracoes(false));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Sair", null, (s, e) => { sair = true; Close(); });
            icone.ContextMenuStrip = m;
            icone.DoubleClick += (s, e) => Reabrir();
            icone.BalloonTipClicked += (s, e) => Reabrir();
        }

        public void Reabrir()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Maximized;
            Activate();
        }

        const string ChaveRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
        bool IniciaComWindows()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(ChaveRun)) return k != null && k.GetValue("MarkeAgenda") != null;
        }
        void DefinirInicioWindows(bool sim)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(ChaveRun, true))
            {
                if (sim) k.SetValue("MarkeAgenda", "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue("MarkeAgenda", false);
            }
        }

        void AjustarAutoAtualizar()
        {
            autoAtualizar.Stop();
            if (dados.Config.AtualizarMin > 0) { autoAtualizar.Interval = dados.Config.AtualizarMin * 60000; autoAtualizar.Start(); }
        }

        // ------------------------------------------------------------------ agenda
        Label Titulo(string texto) { return new Label { Text = texto, Font = Tema.Secao, Dock = DockStyle.Top, Height = 30, ForeColor = Tema.Texto, TextAlign = ContentAlignment.BottomLeft }; }
        Label Espaco(int h) { return new Label { Dock = DockStyle.Top, Height = h }; }

        void MontarAgenda()
        {
            painelAgenda = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Fundo };

            // coluna 1: anotações rápidas + tarefas
            Panel lateral = new Panel { Dock = DockStyle.Right, Width = 310, BackColor = Tema.Painel, Padding = new Padding(12, 6, 12, 12) };
            Label lr = Titulo("Anotações rápidas");
            notaRapida = new TextBox { Dock = DockStyle.Top, Height = 140, Multiline = true, ScrollBars = ScrollBars.Vertical, BackColor = Tema.Fundo, ForeColor = Tema.Texto, BorderStyle = BorderStyle.FixedSingle, Text = (dados.NotaRapida ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n") };
            System.Windows.Forms.Timer salvarRapida = new System.Windows.Forms.Timer { Interval = 800 };
            salvarRapida.Tick += (s, e) => { salvarRapida.Stop(); dados.NotaRapida = notaRapida.Text; dados.Salvar(); };
            notaRapida.TextChanged += (s, e) => { salvarRapida.Stop(); salvarRapida.Start(); };

            lbTarefas = Titulo("Tarefas");
            lbTarefas.Height = 38;
            rapida = new TextBox { Dock = DockStyle.Top, BackColor = Tema.Fundo, ForeColor = Tema.Texto, BorderStyle = BorderStyle.FixedSingle };
            rapida.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                AdicionarRapida(rapida.Text);
                rapida.Text = "";
            };
            busca = new TextBox { Dock = DockStyle.Top, BackColor = Tema.Fundo, ForeColor = Tema.Texto, BorderStyle = BorderStyle.FixedSingle };
            busca.TextChanged += (s, e) => PreencherLista();
            filtroTipo = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Tema.Fundo, ForeColor = Tema.Texto, FlatStyle = FlatStyle.Flat };
            filtroTipo.SelectedIndexChanged += (s, e) => PreencherLista();
            Panel radios = new Panel { Dock = DockStyle.Top, Height = 30 };
            rbSemHorario = new RadioButton { Text = "Sem horário", Checked = true, AutoSize = true, Location = new Point(0, 6), ForeColor = Tema.Texto };
            rbTodas = new RadioButton { Text = "Todas", AutoSize = true, Location = new Point(106, 6), ForeColor = Tema.Texto };
            rbResolvidas = new RadioButton { Text = "Resolvidas", AutoSize = true, Location = new Point(172, 6), ForeColor = Tema.Texto };
            rbSemHorario.CheckedChanged += (s, e) => PreencherLista();
            rbResolvidas.CheckedChanged += (s, e) => PreencherLista();
            radios.Controls.AddRange(new Control[] { rbSemHorario, rbTodas, rbResolvidas });
            Button btNova = Tema.Botao("+ Nova tarefa", true); btNova.Dock = DockStyle.Bottom; btNova.AutoSize = false; btNova.Height = 36;
            btNova.Click += (s, e) => NovaTarefa(null);
            lista = CriarLista(0);

            foreach (Control c in new Control[] { lista, Espaco(4), radios, filtroTipo, Espaco(4), busca, Espaco(6), rapida, lbTarefas, notaRapida, lr, btNova })
                lateral.Controls.Add(c);

            // coluna 2: em aguardo
            Panel aguardo = new Panel { Dock = DockStyle.Right, Width = 290, BackColor = Tema.Painel, Padding = new Padding(12, 6, 12, 12) };
            Panel separador = new Panel { Dock = DockStyle.Right, Width = 1, BackColor = Tema.Linha };
            Panel separador2 = new Panel { Dock = DockStyle.Right, Width = 1, BackColor = Tema.Linha };
            lbAguardo = Titulo("Em aguardo");
            Label lad = new Label { Text = "Depende de terceiros. Arraste para cá.", Dock = DockStyle.Top, Height = 22, ForeColor = Tema.Fraco, Font = Tema.Pequena };
            listaAguardo = CriarLista(1);
            vazioAguardo = new Label { Text = "Nada em aguardo.\r\nArraste uma tarefa para cá\r\nquando depender de alguém.", ForeColor = Tema.Fraco, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill, BackColor = Tema.Painel, AllowDrop = true };
            vazioAguardo.DragOver += (s, e) => e.Effect = DragDropEffects.Move;
            vazioAguardo.DragDrop += (s, e) => SoltarNaLista((string)e.Data.GetData(typeof(string)), 1);
            Button btNovaAg = Tema.Botao("+ Nova em aguardo", false); btNovaAg.Dock = DockStyle.Bottom; btNovaAg.AutoSize = false; btNovaAg.Height = 36;
            btNovaAg.Click += (s, e) => NovaTarefa(null, true);
            Panel fixas = new Panel { Dock = DockStyle.Top, Height = 300, BackColor = Tema.Painel, Padding = new Padding(0, 0, 0, 10) };
            lbFixas = Titulo("Tarefas fixas");
            Label ladF = new Label { Text = "Arraste quantas vezes quiser: elas nunca saem daqui.", Dock = DockStyle.Top, Height = 22, ForeColor = Tema.Fraco, Font = Tema.Pequena };
            filtroTipoFixas = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Tema.Fundo, ForeColor = Tema.Texto, FlatStyle = FlatStyle.Flat };
            filtroTipoFixas.SelectedIndexChanged += (s, e) => PreencherLista();
            listaFixas = CriarLista(2);
            vazioFixas = new Label { Text = "Nenhuma tarefa fixa.\r\nArraste uma tarefa para cá\r\nou crie uma nova.", ForeColor = Tema.Fraco, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill, BackColor = Tema.Painel, AllowDrop = true };
            vazioFixas.DragOver += (s, e) => e.Effect = DragDropEffects.Copy;
            vazioFixas.DragDrop += (s, e) => SoltarNaLista((string)e.Data.GetData(typeof(string)), 2);
            Button btNovaFixa = Tema.Botao("+ Nova tarefa fixa", false); btNovaFixa.Dock = DockStyle.Bottom; btNovaFixa.AutoSize = false; btNovaFixa.Height = 32;
            btNovaFixa.Click += (s, e) => NovaFixa(null);
            Panel linhaSep = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Tema.Linha };
            foreach (Control c in new Control[] { vazioFixas, listaFixas, Espaco(4), filtroTipoFixas, ladF, lbFixas, btNovaFixa, Espaco(10), linhaSep }) fixas.Controls.Add(c);
            foreach (Control c in new Control[] { vazioAguardo, listaAguardo, Espaco(4), lad, lbAguardo, fixas, btNovaAg }) aguardo.Controls.Add(c);
            // meio a meio: metade de cima para as fixas, metade de baixo para Em aguardo
            aguardo.Resize += (s, e) => { if (dados.Config.MostrarAguardando) fixas.Height = Math.Max(160, (aguardo.ClientSize.Height - aguardo.Padding.Vertical - btNovaAg.Height) / 2); };
            // desligada nas configuracoes, a parte de baixo some e as fixas tomam a coluna inteira
            painelAguardo = aguardo; blocoFixas = fixas; linhaSepFixas = linhaSep;
            itensAguardo = new Control[] { vazioAguardo, listaAguardo, lad, lbAguardo, btNovaAg };
            AplicarAguardo();

            // navegação de dia + progresso
            Panel nav = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Tema.Fundo };
            Button ant = Tema.Botao("‹", false), prox = Tema.Botao("›", false), hoje = Tema.Botao("Hoje", false);
            escolher = Tema.Botao("", false); escolher.AutoSize = false; escolher.Size = new Size(130, 30);
            escolher.Text = "📅  " + dia.ToString("dd/MM/yyyy");
            lbDia = new Label { Font = Tema.Titulo, AutoSize = true, ForeColor = Tema.Texto };
            ant.Click += (s, e) => IrPara(dia.AddDays(-1));
            prox.Click += (s, e) => IrPara(dia.AddDays(1));
            hoje.Click += (s, e) => IrPara(DateTime.Today);
            escolher.Click += (s, e) => AbrirCalendario();
            FlowLayoutPanel fl = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(12, 13, 0, 0), BackColor = Tema.Fundo };
            foreach (Button b in new[] { ant, prox, hoje }) { b.AutoSize = false; b.Width = b == hoje ? 60 : 36; b.Margin = new Padding(0, 0, 6, 0); }
            ant.Font = prox.Font = new Font("Segoe UI Semibold", 12f);
            escolher.Margin = new Padding(6, 0, 16, 0); lbDia.Margin = new Padding(0, 1, 0, 0);
            Button eventos = Tema.Botao("🏖  Eventos", false);
            eventos.Margin = new Padding(0, 0, 16, 0);
            new ToolTip().SetToolTip(eventos, "Férias, atestado, feriado, folga: deixam o dia marcado na agenda");
            eventos.Click += (s, e) => { using (DialogoEventos f = new DialogoEventos(dados, dia)) f.ShowDialog(this); dados.Salvar(); AtualizarTudo(); };
            fl.Controls.AddRange(new Control[] { ant, prox, hoje, escolher, eventos, lbDia });
            Panel prog = new Panel { Dock = DockStyle.Right, Width = 480, BackColor = Tema.Fundo };
            lbProgresso = new Label { Location = new Point(0, 8), Size = new Size(460, 18), ForeColor = Tema.Fraco, TextAlign = ContentAlignment.MiddleRight };
            barraProgresso = new ProgressoDia { Location = new Point(260, 31), Size = new Size(200, 8) };
            prog.Controls.Add(lbProgresso); prog.Controls.Add(barraProgresso);
            nav.Controls.Add(fl); nav.Controls.Add(prog);

            grade = new DoubleBufferedPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Tema.Fundo };
            grade.Resize += (s, e) => MontarGrade();
            grade.Scroll += (s, e) => PosicionarLinhaAgora();
            grade.MouseWheel += (s, e) => PosicionarLinhaAgora();

            painelAgenda.Controls.Add(grade);
            painelAgenda.Controls.Add(nav);
            painelAgenda.Controls.Add(lateral);
            painelAgenda.Controls.Add(separador);
            painelAgenda.Controls.Add(aguardo);
            painelAgenda.Controls.Add(separador2);
            Controls.Add(painelAgenda);

            Tema.Dica(rapida, "+ Tarefa rápida (Enter). Ex.: 14:30 Ligar");
            Tema.Dica(busca, "Buscar tarefa (Ctrl+F)");
        }

        ListBox CriarLista(int qual)
        {
            ListBox lb = new ListaSuave { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 50, BackColor = Tema.Painel, ForeColor = Tema.Texto, BorderStyle = BorderStyle.None, AllowDrop = true, IntegralHeight = false };
            int sobre = -1;
            lb.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                using (SolidBrush fb = new SolidBrush(Tema.Painel)) e.Graphics.FillRectangle(fb, e.Bounds);
                bool realce = (e.State & DrawItemState.Selected) != 0 || e.Index == sobre;
                Rectangle r = new Rectangle(e.Bounds.X + 1, e.Bounds.Y + 3, e.Bounds.Width - 4, e.Bounds.Height - 6);
                Cartao.Desenhar(e.Graphics, r, (Tarefa)lb.Items[e.Index], dados, dia, false, realce, Tema.Painel);
            };
            Point inicio = Point.Empty;
            lb.MouseDown += (s, e) =>
            {
                inicio = e.Location;
                int i = lb.IndexFromPoint(e.Location);
                if (i >= 0 && i < lb.Items.Count && e.Button == MouseButtons.Left)
                {
                    Rectangle ir = lb.GetItemRectangle(i);
                    Rectangle r = new Rectangle(ir.X + 1, ir.Y + 3, ir.Width - 4, ir.Height - 6);
                    Tarefa tb = (Tarefa)lb.Items[i];
                    if (!tb.Fixa && Rectangle.Inflate(Cartao.Lixeira(r), 2, 2).Contains(e.Location))
                    {
                        inicio = new Point(-1000, -1000);
                        BeginInvoke(new Action(() => NaoEMinha(tb)));
                        return;
                    }
                    if (!tb.Fixa && Rectangle.Inflate(Cartao.Bolinha(r), 4, 4).Contains(e.Location))
                    {
                        inicio = new Point(-1000, -1000);   // não começa arraste
                        BeginInvoke(new Action(() => AlternarConcluida(tb)));
                        return;
                    }
                }
                if (i >= 0) lb.SelectedIndex = i;
                if (e.Button == MouseButtons.Right && i >= 0) MenuTarefa((Tarefa)lb.Items[i]).Show(lb, e.Location);
            };
            lb.MouseMove += (s, e) =>
            {
                int i = lb.IndexFromPoint(e.Location);
                if (i >= lb.Items.Count || (i >= 0 && !lb.GetItemRectangle(i).Contains(e.Location))) i = -1;
                if (i != sobre)
                {
                    // redesenha só os dois itens que mudaram, sem apagar a lista toda
                    int antes = sobre; sobre = i;
                    if (antes >= 0 && antes < lb.Items.Count) lb.Invalidate(lb.GetItemRectangle(antes));
                    if (i >= 0) lb.Invalidate(lb.GetItemRectangle(i));
                }
                if (e.Button != MouseButtons.Left || lb.SelectedItem == null) return;
                if (inicio.X < -500 || Math.Abs(e.X - inicio.X) + Math.Abs(e.Y - inicio.Y) < 6) return;
                Rectangle ir = lb.GetItemRectangle(lb.SelectedIndex);
                Arraste.Iniciar(lb, (Tarefa)lb.SelectedItem, dados, dia, new Size(ir.Width - 4, ir.Height - 6), new Point(e.X - ir.X, e.Y - ir.Y - 3));
            };
            lb.MouseLeave += (s, e) =>
            {
                if (sobre == -1) return;
                int antes = sobre; sobre = -1;
                if (antes < lb.Items.Count) lb.Invalidate(lb.GetItemRectangle(antes));
            };
            lb.DoubleClick += (s, e) => { if (lb.SelectedItem != null) Editar((Tarefa)lb.SelectedItem); };
            lb.KeyDown += (s, e) =>
            {
                Tarefa t = lb.SelectedItem as Tarefa;
                if (t == null) return;
                if (e.KeyCode == Keys.Enter) Editar(t);
                if (e.KeyCode == Keys.Delete) Excluir(t);
                if (e.KeyCode == Keys.Space) { AlternarConcluida(t); e.Handled = true; }
            };
            lb.DragOver += (s, e) => e.Effect = !e.Data.GetDataPresent(typeof(string)) ? DragDropEffects.None : (e.AllowedEffect & DragDropEffects.Move) != 0 ? DragDropEffects.Move : DragDropEffects.Copy;
            lb.DragDrop += (s, e) => SoltarNaLista((string)e.Data.GetData(typeof(string)), qual);
            return lb;
        }

        /// Quem é a tarefa arrastada. Se for fixa, cria a cópia que vai ser planejada.
        Tarefa Resolver(string dado)
        {
            if (dado == null) return null;
            if (!dado.StartsWith("fixa:")) return Achar(dado);
            Tarefa f = Achar(dado.Substring(5));
            if (f == null) return null;
            Tarefa c = Copiar(f);
            dados.Tarefas.Add(c);
            return c;
        }

        Tarefa Copiar(Tarefa f)
        {
            return new Tarefa { Id = Dados.NovoId(), Titulo = f.Titulo, Descricao = f.Descricao, TipoId = f.TipoId, Cor = f.Cor, Origem = "Manual", Link = f.Link, Duracao = f.Duracao, Criada = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
        }

        void SoltarNaLista(string id, int qual)
        {
            if (qual == 2)
            {
                // soltar nas fixas: vira um modelo (a tarefa original continua onde estava)
                if (id == null || id.StartsWith("fixa:")) return;
                Tarefa o = Achar(id);
                if (o == null) return;
                Tarefa f = Copiar(o); f.Fixa = true;
                dados.Tarefas.Add(f); dados.Salvar();
                BeginInvoke(new Action(AtualizarTudo));
                lbStatus.Text = "\"" + f.Titulo + "\" agora é uma tarefa fixa";
                return;
            }
            bool ehAguardo = qual == 1;
            Tarefa t = Resolver(id);
            if (t == null) return;
            if (t.EstaAgendada())
            {
                if (t.Origem == "Google" && MessageBox.Show(this, "Este é um evento do Google Agenda. Tirar do horário só aqui na Marke Agenda?", "Tirar do horário", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                t.Hora = "";
            }
            t.Aguardando = ehAguardo;
            dados.Salvar();
            BeginInvoke(new Action(AtualizarTudo));
            lbStatus.Text = "\"" + t.Titulo + "\" " + (ehAguardo ? "foi para Em aguardo" : "voltou para Tarefas");
        }

        void AdicionarRapida(string texto)
        {
            texto = (texto ?? "").Trim();
            if (texto == "") return;
            Tarefa t = new Tarefa { Id = Dados.NovoId(), Origem = "Manual", TipoId = dados.Tipos[0].Id, Criada = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(texto, @"^(\d{1,2})[:h](\d{2})?\s+(.+)$");
            if (m.Success && int.Parse(m.Groups[1].Value) < 24)
            {
                t.Data = Tarefa.Dia(dia);
                t.Hora = int.Parse(m.Groups[1].Value).ToString("00") + ":" + (m.Groups[2].Success ? m.Groups[2].Value : "00");
                texto = m.Groups[3].Value;
            }
            t.Titulo = texto;
            dados.Tarefas.Add(t); dados.Salvar(); AtualizarTudo();
            lbStatus.Text = "Tarefa criada: " + t.Titulo + (t.EstaAgendada() ? " às " + t.Hora : "");
        }

        void IrPara(DateTime d)
        {
            dia = d.Date;
            escolher.Text = "📅  " + dia.ToString("dd/MM/yyyy");
            grade.AutoScrollPosition = Point.Empty;
            AtualizarTudo();
        }

        /// Calendário escuro que abre embaixo do botão da data.
        void AbrirCalendario()
        {
            Escuros.Calendario(escolher, dia, d => IrPara(d), null, null);
        }

        void AtualizarTudo()
        {
            string nome = dia.ToString("dddd, dd 'de' MMMM", new CultureInfo("pt-BR"));
            lbDia.Text = char.ToUpper(nome[0]) + nome.Substring(1) + (dia == DateTime.Today ? "  ·  hoje" : dia == DateTime.Today.AddDays(1) ? "  ·  amanhã" : dia == DateTime.Today.AddDays(-1) ? "  ·  ontem" : "");
            string sel = filtroTipo.SelectedItem as string;
            filtroTipo.Items.Clear();
            filtroTipo.Items.Add("Todos os tipos");
            foreach (Tipo t in dados.Tipos) filtroTipo.Items.Add(t.Nome);
            filtroTipo.SelectedItem = sel != null && filtroTipo.Items.Contains(sel) ? sel : "Todos os tipos";

            string selF = filtroTipoFixas.SelectedItem as string;
            filtroTipoFixas.Items.Clear();
            filtroTipoFixas.Items.Add("Todos os tipos");
            foreach (Tipo t in dados.Tipos) filtroTipoFixas.Items.Add(t.Nome);
            filtroTipoFixas.SelectedItem = selF != null && filtroTipoFixas.Items.Contains(selF) ? selF : "Todos os tipos";
            PreencherLista();
            MontarGrade();
            AtualizarProgresso();
        }

        void AtualizarProgresso()
        {
            List<Tarefa> doDia = Ativas.Where(t => t.OcorreEm(dia)).ToList();
            int feitas = doDia.Count(t => t.ConcluidaEm(dia));
            int atrasadas = doDia.Count(t => Cartao.Atrasada(t, dia, dados.Config.Intervalo));
            int minutos = doDia.Sum(t => t.Duracao > 0 ? t.Duracao : dados.Config.Intervalo);
            lbProgresso.Text = doDia.Count == 0 ? "Nada planejado neste dia"
                : feitas + " de " + doDia.Count + " concluídas  ·  " + Cartao.Duracao(minutos) + " planejadas" + (atrasadas > 0 ? "  ·  " + atrasadas + " atrasada(s)" : "");
            lbProgresso.ForeColor = atrasadas > 0 ? Tema.Atraso : Tema.Fraco;
            barraProgresso.Valor = doDia.Count == 0 ? 0 : (double)feitas / doDia.Count;
        }

        Tarefa Achar(string id) { return dados.Tarefas.FirstOrDefault(x => x.Id == id); }

        Panel painelAguardo, blocoFixas, linhaSepFixas;
        Control[] itensAguardo;

        /// Mostra ou esconde a secao "Em aguardo", conforme as configuracoes.
        public void AplicarAguardo()
        {
            if (painelAguardo == null) return;
            bool ver = dados.Config.MostrarAguardando;
            foreach (Control c in itensAguardo) c.Visible = ver;
            blocoFixas.Dock = ver ? DockStyle.Top : DockStyle.Fill;
            // o separador de baixo e o espaco so fazem sentido com as duas secoes
            linhaSepFixas.Visible = ver;
            if (!ver) { blocoFixas.Padding = new Padding(0); blocoFixas.BringToFront(); }
            else
            {
                blocoFixas.Padding = new Padding(0, 0, 0, 10);
                // volta ao lugar de origem (logo antes do botão "+ Nova em aguardo"): se ficasse na frente,
                // ao religar as fixas iam parar embaixo de "Em aguardo"
                Control bt = itensAguardo[itensAguardo.Length - 1];
                painelAguardo.Controls.SetChildIndex(blocoFixas, Math.Max(0, painelAguardo.Controls.GetChildIndex(bt) - 1));
                painelAguardo.PerformLayout();
            }
        }

        /// Tarefas fora da Lixeira.
        IEnumerable<Tarefa> Ativas { get { return dados.Tarefas.Where(t => t.Ativa()); } }

        void PreencherLista()
        {
            if (filtroTipo.SelectedItem == null) return;
            string q = busca.Text.Trim().ToLowerInvariant();
            string tipo = filtroTipo.SelectedIndex > 0 ? dados.Tipos[filtroTipo.SelectedIndex - 1].Id : null;
            IEnumerable<Tarefa> ts = Ativas.Where(t => !t.Aguardando && !t.Fixa);
            if (rbSemHorario.Checked) ts = ts.Where(t => !t.EstaAgendada() && !t.Concluida);
            else if (rbResolvidas.Checked) ts = ts.Where(t => t.Concluida || (t.Repete() && t.ConcluidaEm(dia)));
            else ts = ts.Where(t => true);
            if (tipo != null) ts = ts.Where(t => t.TipoId == tipo);
            if (q != "") ts = ts.Where(t => (t.Titulo + " " + t.Descricao).ToLowerInvariant().Contains(q));
            ts = ts.OrderBy(t => t.Concluida).ThenBy(t => string.IsNullOrEmpty(t.Data) ? "9" : t.Data).ThenBy(t => t.Hora).ThenBy(t => t.Titulo);
            Recarregar(lista, ts);
            lbTarefas.Text = "Tarefas  (" + lista.Items.Count + ")";

            if (dados.Config.MostrarAguardando)
            {
                Recarregar(listaAguardo, Ativas.Where(x => x.Aguardando && !x.Concluida && !x.Fixa).OrderBy(x => x.Criada));
                lbAguardo.Text = "Em aguardo  (" + listaAguardo.Items.Count + ")";
                vazioAguardo.Visible = listaAguardo.Items.Count == 0;
                listaAguardo.Visible = !vazioAguardo.Visible;   // só mostra/esconde: mudar a ordem bagunçava a coluna
            }
            else vazioAguardo.Visible = false;

            string tipoFixas = filtroTipoFixas.SelectedIndex > 0 ? dados.Tipos[filtroTipoFixas.SelectedIndex - 1].Id : null;
            IEnumerable<Tarefa> fixasVis = Ativas.Where(x => x.Fixa);
            if (tipoFixas != null) fixasVis = fixasVis.Where(x => x.TipoId == tipoFixas);
            Recarregar(listaFixas, fixasVis.OrderBy(x => x.Titulo));
            lbFixas.Text = "Tarefas fixas  (" + listaFixas.Items.Count + ")" + (tipoFixas != null ? "  -  filtrando" : "");
            vazioFixas.Visible = listaFixas.Items.Count == 0;
            listaFixas.Visible = !vazioFixas.Visible;
        }

        /// Recarrega a lista sem perder a rolagem nem a seleção.
        static void Recarregar(ListBox lb, IEnumerable<Tarefa> itens)
        {
            int topo = lb.TopIndex;
            Tarefa sel = lb.SelectedItem as Tarefa;
            lb.BeginUpdate();
            lb.Items.Clear();
            foreach (Tarefa t in itens) lb.Items.Add(t);
            if (sel != null && lb.Items.Contains(sel)) lb.SelectedItem = sel;
            if (lb.Items.Count > 0) lb.TopIndex = Math.Min(topo, lb.Items.Count - 1);
            lb.EndUpdate();
        }

        // --- grade de horários
        List<string> Slots()
        {
            Config c = dados.Config;
            int passo = c.Intervalo <= 0 ? 30 : c.Intervalo;
            List<string> r = new List<string>();
            // intervalos (almoco, cafe, reuniao fixa...): dentro deles nao ha horarios na agenda
            List<Intervalo> fora = dados.IntervalosAtivos();
            for (int m = c.HoraInicio * 60; m <= c.HoraFim * 60; m += passo)
            {
                if (dados.EmIntervalo(m)) continue;
                r.Add((m / 60).ToString("00") + ":" + (m % 60).ToString("00"));
            }
            // a volta de cada intervalo vira um horario, para o dia nao ficar com buraco
            foreach (Intervalo iv in fora)
            {
                int fim = iv.MinFim();
                if (fim > 0 && fim <= c.HoraFim * 60 && !dados.EmIntervalo(fim)) r.Add((fim / 60).ToString("00") + ":" + (fim % 60).ToString("00"));
            }
            List<string> extras;
            if (dados.HorariosExtras.TryGetValue(Tarefa.Dia(dia), out extras)) r.AddRange(extras);
            return r.Distinct().OrderBy(x => x).ToList();
        }

        /// A hora em que a tarefa termina (inicio + duracao).
        static string FimDe(Tarefa t)
        {
            DateTime x;
            if (!DateTime.TryParse("2000-01-01 " + (t.Hora ?? ""), out x)) return "";
            return x.AddMinutes(Math.Max(0, t.Duracao)).ToString("HH:mm");
        }

        /// A tarefa comecou num horario anterior e ainda esta correndo neste?
        bool Atravessa(Tarefa t, string slot, List<string> slots)
        {
            if (t.Duracao <= 0 || string.IsNullOrEmpty(t.Hora)) return false;
            string inicio = SlotDe(t.Hora, slots);
            if (inicio == slot || string.IsNullOrEmpty(inicio)) return false;
            int mSlot = Minutos(slot, -1), mIni = Minutos(t.Hora, -1);
            if (mSlot < 0 || mIni < 0) return false;
            return mSlot > mIni && mSlot < mIni + t.Duracao;
        }

        static int Minutos(string hhmm, int padrao)
        {
            DateTime x;
            return DateTime.TryParse("2000-01-01 " + hhmm, out x) ? x.Hour * 60 + x.Minute : padrao;
        }

        bool EhExtra(string slot)
        {
            List<string> extras;
            return dados.HorariosExtras.TryGetValue(Tarefa.Dia(dia), out extras) && extras.Contains(slot);
        }

        /// Cria um horário só para o dia que está na tela.
        void NovoHorario(string apos)
        {
            DateTime b;
            DateTime.TryParse("2000-01-01 " + apos, out b);
            string sugestao = b.AddMinutes(Math.Max(5, dados.Config.Intervalo / 2)).ToString("HH:mm");
            using (DialogoBase f = new DialogoBase("Novo horário", 300))
            {
                Label l = new Label { Text = "Horário em " + dia.ToString("dd/MM") + " (só neste dia)", Location = new Point(16, 14), AutoSize = true, ForeColor = Tema.Fraco };
                MaskedTextBox h = new MaskedTextBox { Mask = "00:00", Text = sugestao, Location = new Point(16, 38), Width = 80, Font = new Font("Segoe UI", 12f), BackColor = Tema.Painel, ForeColor = Tema.Texto, BorderStyle = BorderStyle.FixedSingle };
                Button ok = Tema.Botao("Criar", true); ok.Location = new Point(16, 84);
                Button can = Tema.Botao("Cancelar", false); can.Location = new Point(100, 84); can.DialogResult = DialogResult.Cancel;
                ok.Click += (s, e) =>
                {
                    DateTime x;
                    if (!DateTime.TryParse("2000-01-01 " + h.Text, out x)) { MessageBox.Show(f, "Use o formato 09:15."); return; }
                    string k = Tarefa.Dia(dia);
                    if (!dados.HorariosExtras.ContainsKey(k)) dados.HorariosExtras[k] = new List<string>();
                    string hh = x.ToString("HH:mm");
                    if (!Slots().Contains(hh)) dados.HorariosExtras[k].Add(hh);
                    dados.Salvar();
                    f.DialogResult = DialogResult.OK;
                };
                f.Controls.AddRange(new Control[] { l, h, ok, can });
                f.AcceptButton = ok; f.CancelButton = can;
                f.ClientSize = new Size(300, 130);
                Tema.Aplicar(f);
                if (f.ShowDialog(this) == DialogResult.OK) MontarGrade();
            }
        }

        void RemoverHorario(string slot)
        {
            List<string> extras;
            if (!dados.HorariosExtras.TryGetValue(Tarefa.Dia(dia), out extras)) return;
            extras.Remove(slot);
            if (extras.Count == 0) dados.HorariosExtras.Remove(Tarefa.Dia(dia));
            dados.Salvar();
            BeginInvoke(new Action(MontarGrade));
            lbStatus.Text = "Horário " + slot + " removido. As tarefas dele ficam no horário anterior.";
        }

        string SlotDe(string hora, List<string> slots)
        {
            if (string.IsNullOrEmpty(hora)) return null;
            string achado = slots[0];
            foreach (string s in slots) if (string.Compare(s, hora) <= 0) achado = s;
            return achado;
        }

        string SlotAgora() { return dia == DateTime.Today ? SlotDe(DateTime.Now.ToString("HH:mm"), Slots()) ?? "" : ""; }

        /// Cor do dia: a do evento (férias, feriado...) ou cinza no fim de semana. Empty = dia normal.
        Color corDoDia = Color.Empty;

        Color CorLinha(string slot)
        {
            Color b = corDoDia.IsEmpty ? Tema.Fundo : Tema.Misturar(corDoDia, Tema.Fundo, 0.13);
            return slot == ultimoSlotAgora ? Tema.Misturar(Tema.Agora, b, 0.7) : b;
        }

        /// Só repinta o que mudou (sem refazer a agenda): usado ao concluir, para não piscar.
        void AtualizarLeve()
        {
            PreencherLista();
            AtualizarProgresso();
            foreach (Panel l in linhas.Values) foreach (Control c in l.Controls) if (c is Chip) c.Invalidate();
        }

        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
        void Desenho(Control c, bool ligado) { SendMessage(c.Handle, 0x0B, ligado ? 1 : 0, 0); }   // WM_SETREDRAW

        void MontarGrade()
        {
            if (grade == null || dados == null || lbProgresso == null) return;
            int rolagem = -grade.AutoScrollPosition.Y;
            Desenho(grade, false);   // monta tudo sem desenhar e mostra de uma vez (não pisca)
            grade.SuspendLayout();

            // dia com evento (férias, feriado...) ou fim de semana: agenda com outra cor e listras
            List<EventoDia> evs = dados.EventosEm(dia);
            bool fds = dados.Config.DestacarFimDeSemana && (dia.DayOfWeek == DayOfWeek.Saturday || dia.DayOfWeek == DayOfWeek.Sunday);
            corDoDia = evs.Count > 0 ? Cores.De(evs[0].Cor) : fds ? Color.FromArgb(100, 116, 139) : Color.Empty;
            foreach (Control c in grade.Controls.Cast<Control>().ToList()) { grade.Controls.Remove(c); c.Dispose(); }
            // Volta ao topo antes de posicionar os horários: com a tela rolada, o 08:00
            // ficaria deslocado e sobraria um espaço vazio em cima dele.
            grade.AutoScrollPosition = Point.Empty;
            linhas.Clear();
            List<string> slots = Slots();
            ultimoSlotAgora = SlotAgora();
            List<Tarefa> doDia = Ativas.Where(t => t.OcorreEm(dia)).OrderBy(t => t.Hora).ToList();
            int larguraUtil = grade.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2;
            const int colHora = 74, chipL = 260, chipA = 44, gap = 6;
            int porLinha = Math.Max(1, (larguraUtil - colHora - 10) / (chipL + gap));
            int y = 0;
            Control rolarPara = null;
            if (!corDoDia.IsEmpty)
            {
                string texto = evs.Count > 0
                    ? string.Join("  ·  ", evs.Select(ev => ev.Titulo() + " (" + (ev.DiaInteiro() ? DialogoEventos.Periodo(ev) : ev.Faixa()) + ")"))
                    : (dia.DayOfWeek == DayOfWeek.Saturday ? "Sábado" : "Domingo");
                Color cb = corDoDia;
                Panel faixa = new DoubleBufferedPanel { Location = new Point(0, 0), Size = new Size(larguraUtil, 40), BackColor = Tema.Misturar(cb, Tema.Fundo, 0.35) };
                faixa.Paint += (s, e) =>
                {
                    using (SolidBrush b = new SolidBrush(cb)) e.Graphics.FillRectangle(b, 0, 0, 6, faixa.Height);
                    TextRenderer.DrawText(e.Graphics, texto, Tema.Secao, new Point(18, 9), Color.White);
                };
                grade.Controls.Add(faixa);
                y = 40;
            }
            foreach (string slot in slots)
            {
                List<Tarefa> aqui = doDia.Where(t => SlotDe(t.Hora, slots) == slot).ToList();
                // tarefas que comecaram antes e ainda estao correndo neste horario
                List<Tarefa> seguindo = doDia.Where(t => Atravessa(t, slot, slots)).ToList();
                int nLinhas = Math.Max(1, (aqui.Count + seguindo.Count + porLinha - 1) / porLinha);
                int altura = Math.Max(54, nLinhas * (chipA + gap) + gap);
                bool cheia = slot.EndsWith(":00");
                bool extra = EhExtra(slot);
                Panel linha = new DoubleBufferedPanel { Location = new Point(0, y), Size = new Size(larguraUtil, altura), BackColor = CorLinha(slot), Tag = slot };
                string s0 = slot;
                Color corDia = corDoDia;
                linha.Paint += (s, e) =>
                {
                    if (!corDia.IsEmpty)
                        using (HatchBrush hb = new HatchBrush(HatchStyle.WideUpwardDiagonal, Color.FromArgb(28, corDia), Color.Transparent))
                            e.Graphics.FillRectangle(hb, colHora - 8, 0, linha.Width, linha.Height);
                    using (Pen p = new Pen(cheia ? Tema.Linha : Tema.Misturar(Tema.Linha, Tema.Fundo, 0.5)))
                    {
                        if (!cheia) p.DashStyle = DashStyle.Dot;
                        e.Graphics.DrawLine(p, colHora - 8, linha.Height - 1, linha.Width, linha.Height - 1);
                    }
                    using (Pen p = new Pen(Tema.Linha)) e.Graphics.DrawLine(p, colHora - 8, 0, colHora - 8, linha.Height);
                    TextRenderer.DrawText(e.Graphics, s0, cheia ? Tema.Negrito : Tema.Fonte, new Point(10, 16), extra ? Tema.Claro : cheia ? Tema.Texto : Tema.Fraco);
                };
                LigarSoltar(linha, slot, linha);

                // coluna da hora: "+" ao passar o mouse (e "×" nos horários extras)
                Panel colunaHora = new Panel { Location = new Point(0, 0), Size = new Size(colHora - 9, altura), BackColor = Color.Transparent };
                Label mais = new Label { Text = "+", Font = new Font("Segoe UI Semibold", 13f), ForeColor = Tema.Claro, AutoSize = false, Size = new Size(20, 24), Location = new Point(colHora - 31, 11), Cursor = Cursors.Hand, Visible = false, BackColor = Color.Transparent };
                Label tirar = new Label { Text = "×", Font = new Font("Segoe UI Semibold", 12f), ForeColor = Tema.Atraso, AutoSize = false, Size = new Size(18, 24), Location = new Point(colHora - 31, 30), Cursor = Cursors.Hand, Visible = false, BackColor = Color.Transparent };
                ToolTip dicaHora = new ToolTip();
                dicaHora.SetToolTip(mais, "Criar um horário só neste dia");
                dicaHora.SetToolTip(tirar, "Remover este horário extra");
                mais.Click += (s, e) => NovoHorario(s0);
                tirar.Click += (s, e) => RemoverHorario(s0);
                Action atualizarMais = () =>
                {
                    bool dentro = colunaHora.ClientRectangle.Contains(colunaHora.PointToClient(Cursor.Position));
                    mais.Visible = dentro; tirar.Visible = dentro && extra;
                };
                colunaHora.MouseEnter += (s, e) => atualizarMais();
                colunaHora.MouseLeave += (s, e) => atualizarMais();
                mais.MouseLeave += (s, e) => atualizarMais();
                tirar.MouseLeave += (s, e) => atualizarMais();
                colunaHora.Controls.Add(mais); colunaHora.Controls.Add(tirar);
                LigarSoltar(colunaHora, slot, linha);
                linha.Controls.Add(colunaHora);
                for (int i = 0; i < aqui.Count; i++)
                {
                    Chip c = new Chip(this, aqui[i]);
                    c.Location = new Point(colHora + (i % porLinha) * (chipL + gap), gap + (i / porLinha) * (chipA + gap));
                    c.Size = new Size(chipL, chipA);
                    LigarSoltar(c, slot, linha);
                    linha.Controls.Add(c);
                }
                // rabicho das que vieram do horario de cima: so o nome, sem poder arrastar
                for (int i = 0; i < seguindo.Count; i++)
                {
                    Tarefa ts = seguindo[i];
                    int pos = aqui.Count + i;
                    Label cont = new Label
                    {
                        Text = "↳  " + ts.Titulo + "   (até " + FimDe(ts) + ")",
                        AutoEllipsis = true,
                        ForeColor = Tema.Fraco,
                        Font = Tema.Pequena,
                        TextAlign = ContentAlignment.MiddleLeft,
                        BackColor = Tema.Misturar(dados.CorDe(ts), Tema.Fundo, 0.12),
                        Location = new Point(colHora + (pos % porLinha) * (chipL + gap), gap + (pos / porLinha) * (chipA + gap)),
                        Size = new Size(chipL, chipA),
                        Padding = new Padding(10, 0, 0, 0),
                    };
                    new ToolTip().SetToolTip(cont, ts.Titulo + " — " + ts.Hora + " às " + FimDe(ts));
                    LigarSoltar(cont, slot, linha);
                    linha.Controls.Add(cont);
                }
                grade.Controls.Add(linha);
                linhas[slot] = linha;
                if (slot == ultimoSlotAgora) rolarPara = linha;
                y += altura;
            }
            linhaAgora = new Panel { Height = 2, BackColor = Tema.Claro, Visible = false };
            grade.Controls.Add(linhaAgora);
            linhaAgora.BringToFront();
            grade.ResumeLayout();
            if (rolagem > 0) grade.AutoScrollPosition = new Point(0, rolagem);
            else if (rolarPara != null) grade.AutoScrollPosition = new Point(0, Math.Max(0, rolarPara.Top - 120));
            PosicionarLinhaAgora();
            Desenho(grade, true);
            grade.Invalidate(true);
        }

        void PosicionarLinhaAgora()
        {
            if (linhaAgora == null || linhaAgora.IsDisposed) return;
            Panel l;
            if (dia != DateTime.Today || ultimoSlotAgora == "" || !linhas.TryGetValue(ultimoSlotAgora, out l)) { linhaAgora.Visible = false; return; }
            // até o próximo horário (que pode ser um horário extra)
            List<string> sl = Slots();
            int idx = sl.IndexOf(ultimoSlotAgora);
            DateTime ini = DateTime.Parse(Tarefa.Dia(DateTime.Today) + " " + ultimoSlotAgora);
            double passo = idx >= 0 && idx + 1 < sl.Count
                ? (DateTime.Parse(Tarefa.Dia(DateTime.Today) + " " + sl[idx + 1]) - ini).TotalMinutes
                : Math.Max(5, dados.Config.Intervalo);
            double f = (DateTime.Now - ini).TotalMinutes / passo;
            if (f < 0 || f > 1) { linhaAgora.Visible = false; return; }
            linhaAgora.Location = new Point(66, l.Top + (int)(f * (l.Height - 2)));
            linhaAgora.Width = l.Width - 66;
            linhaAgora.Visible = true;
            linhaAgora.BringToFront();
        }

        void LigarSoltar(Control c, string slot, Panel linha)
        {
            c.AllowDrop = true;
            c.DragEnter += (s, e) => { if (e.Data.GetDataPresent(typeof(string))) linha.BackColor = Tema.Alvo; };
            c.DragLeave += (s, e) => { linha.BackColor = CorLinha(slot); };
            c.DragOver += (s, e) => { e.Effect = !e.Data.GetDataPresent(typeof(string)) ? DragDropEffects.None : (e.AllowedEffect & DragDropEffects.Move) != 0 ? DragDropEffects.Move : DragDropEffects.Copy; linha.BackColor = Tema.Alvo; };
            c.DragDrop += (s, e) =>
            {
                linha.BackColor = CorLinha(slot);
                Tarefa t = Resolver((string)e.Data.GetData(typeof(string)));
                if (t == null) return;
                if (t.Repete() && t.EstaAgendada() && Tarefa.Dia(dia) != t.Data)
                    t.Hora = slot;   // repetição: muda o horário de todas, mantém o dia de início
                else { t.Data = Tarefa.Dia(dia); t.Hora = slot; }
                t.Avisado = false; t.AvisadoEm = null; t.Aguardando = false;
                dados.Salvar();
                BeginInvoke(new Action(AtualizarTudo));   // não destruir o cartão no meio do arraste
                lbStatus.Text = "\"" + t.Titulo + "\" planejada para " + dia.ToString("dd/MM") + " às " + slot;
            };
        }

        public Dados Dados { get { return dados; } }
        public DateTime Dia { get { return dia; } }

        // --- ações em tarefa
        public ContextMenuStrip MenuTarefa(Tarefa t)
        {
            ContextMenuStrip m = new ContextMenuStrip();
            Tema.Menu(m);
            if (t.Fixa)
            {
                m.Items.Add("Editar tarefa fixa", null, (s, e) => Editar(t));
                m.Items.Add("Planejar uma cópia sem horário", null, (s, e) => { dados.Tarefas.Add(Copiar(t)); dados.Salvar(); AtualizarTudo(); });
                m.Items.Add(new ToolStripSeparator());
                m.Items.Add("Excluir tarefa fixa", null, (s, e) => Excluir(t));
                return m;
            }
            bool feita = t.ConcluidaEm(dia);
            m.Items.Add("Editar", null, (s, e) => Editar(t));
            m.Items.Add(feita ? "Desmarcar concluída" : "Marcar como concluída" + (t.Repete() ? " (só neste dia)" : ""), null, (s, e) => AlternarConcluida(t));
            if (t.EstaAgendada()) m.Items.Add("Tirar do horário", null, (s, e) => { t.Hora = ""; dados.Salvar(); AtualizarTudo(); });
            if (!t.Aguardando) m.Items.Add("Mandar para Em aguardo", null, (s, e) => SoltarNaLista(t.Id, 1));
            else m.Items.Add("Tirar de Em aguardo", null, (s, e) => SoltarNaLista(t.Id, 0));
            if (t.EstaAgendada() && !t.Repete())
                m.Items.Add("Adiar para amanhã", null, (s, e) => { t.Data = Tarefa.Dia(dia.AddDays(1)); t.AvisadoEm = null; dados.Salvar(); AtualizarTudo(); });
            if (!string.IsNullOrEmpty(t.Link)) m.Items.Add("Abrir no navegador", null, (s, e) => { try { Process.Start(t.Link); } catch { } });
            if (t.Origem == "TomTicket") m.Items.Add("Copiar protocolo", null, (s, e) => Clipboard.SetText(t.Ref));
            ToolStripMenuItem tipos = new ToolStripMenuItem("Trocar tipo");
            foreach (Tipo tp in dados.Tipos)
            {
                Tipo tp0 = tp;
                ToolStripMenuItem it = new ToolStripMenuItem(tp.Nome) { Checked = t.TipoId == tp.Id, ForeColor = Tema.Texto };
                it.Click += (s, e) => { t.TipoId = tp0.Id; dados.Salvar(); AtualizarTudo(); };
                tipos.DropDownItems.Add(it);
            }
            m.Items.Add(tipos);
            m.Items.Add("Salvar como tarefa fixa", null, (s, e) => SoltarNaLista(t.Id, 2));
            m.Items.Add("Duplicar", null, (s, e) =>
            {
                Tarefa c = new Tarefa { Id = Dados.NovoId(), Titulo = t.Titulo, Descricao = t.Descricao, TipoId = t.TipoId, Cor = t.Cor, Origem = "Manual", Link = t.Link, Duracao = t.Duracao, Criada = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
                dados.Tarefas.Add(c); dados.Salvar(); AtualizarTudo();
            });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Excluir", null, (s, e) => Excluir(t));
            return m;
        }

        void Excluir(Tarefa t)
        {
            if (t.Repete())
            {
                // tarefa que repete: ou tira so o dia que esta na tela, ou a repeticao inteira
                using (DialogoExcluirRepeticao f = new DialogoExcluirRepeticao(t, dia))
                {
                    if (f.ShowDialog(this) != DialogResult.OK) return;
                    if (f.SoEsta)
                    {
                        t.ExcluirSo(dia);
                        t.Mod = Sincronia.Agora();
                        dados.Salvar(); AtualizarTudo();
                        lbStatus.Text = "\"" + t.Titulo + "\" saiu do dia " + dia.ToString("dd/MM") + ". As outras repeticoes continuam.";
                        return;
                    }
                }
            }
            else if (MessageBox.Show(this, "Mandar \"" + t.Titulo + "\" para a Lixeira?\n\nDá para restaurar por 30 dias (menu do seu nome > Lixeira).", "Excluir tarefa", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            t.NaLixeira = Sincronia.Agora(); dados.Salvar(); AtualizarTudo();
            lbStatus.Text = "\"" + t.Titulo + "\" foi para a Lixeira.";
        }

        /// Lixeirinha: tira a tarefa e, se veio do TomTicket/Markedesk/Google, não deixa voltar.
        public void NaoEMinha(Tarefa t)
        {
            if (t.Origem != "Manual" && !string.IsNullOrEmpty(t.Ref))
            {
                string k = t.Origem + ":" + t.Ref;
                if (!dados.NaoSaoMinhas.Contains(k)) dados.NaoSaoMinhas.Add(k);
            }
            t.NaLixeira = Sincronia.Agora();
            dados.Salvar();
            AtualizarTudo();
            lbStatus.Text = "\"" + t.Titulo + "\" foi para a Lixeira (menu do seu nome > Lixeira para restaurar).";
        }

        public void AlternarConcluida(Tarefa t)
        {
            bool agora = !t.ConcluidaEm(dia);
            t.MarcarConcluida(dia, agora);
            dados.Salvar(); AtualizarLeve();
            lbStatus.Text = "\"" + t.Titulo + "\" " + (agora ? "concluída" : "reaberta");
        }

        public void Editar(Tarefa t)
        {
            using (DialogoTarefa f = new DialogoTarefa(dados, t, dia))
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    if (t.Fixa) { t.Data = ""; t.Hora = ""; t.Repetir = ""; t.Aguardando = false; t.Concluida = false; }
                    dados.Salvar(); AtualizarTudo();
                }
        }

        void NovaTarefa(Nota deNota) { NovaTarefa(deNota, false); }

        void NovaFixa(Nota deNota)
        {
            Tarefa t = new Tarefa { Id = Dados.NovoId(), Origem = "Manual", TipoId = dados.Tipos[0].Id, Criada = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Titulo = "", Fixa = true };
            using (DialogoTarefa f = new DialogoTarefa(dados, t, dia))
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    t.Fixa = true; t.Data = ""; t.Hora = ""; t.Repetir = ""; t.Aguardando = false; t.Concluida = false;
                    dados.Tarefas.Add(t); dados.Salvar(); AtualizarTudo();
                }
        }

        void NovaTarefa(Nota deNota, bool emAguardo)
        {
            Tarefa t = new Tarefa { Id = Dados.NovoId(), Origem = "Manual", TipoId = dados.Tipos[0].Id, Criada = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Titulo = "", Aguardando = emAguardo };
            if (deNota != null) { t.Titulo = deNota.Titulo; t.Descricao = deNota.Texto; }
            using (DialogoTarefa f = new DialogoTarefa(dados, t, dia))
                if (f.ShowDialog(this) == DialogResult.OK) { dados.Tarefas.Add(t); dados.Salvar(); AtualizarTudo(); MostrarAba(true); }
        }

        // --- recursos do menu
        void LevarPendentes()
        {
            DateTime alvo = dia.AddDays(1);
            while (alvo.DayOfWeek == DayOfWeek.Saturday || alvo.DayOfWeek == DayOfWeek.Sunday) alvo = alvo.AddDays(1);
            List<Tarefa> pend = Ativas.Where(t => t.OcorreEm(dia) && !t.Repete() && !t.ConcluidaEm(dia) && t.Origem != "Google").ToList();
            if (pend.Count == 0) { MessageBox.Show(this, "Nenhuma pendência neste dia.", "Levar pendentes"); return; }
            if (MessageBox.Show(this, pend.Count + " tarefa(s) pendente(s) vão para " + alvo.ToString("dddd, dd/MM", new CultureInfo("pt-BR")) + ", no mesmo horário. Continuar?", "Levar pendentes", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            foreach (Tarefa t in pend) { t.Data = Tarefa.Dia(alvo); t.AvisadoEm = null; }
            dados.Salvar(); AtualizarTudo();
            lbStatus.Text = pend.Count + " tarefa(s) levada(s) para " + alvo.ToString("dd/MM");
        }

        void CopiarPlano()
        {
            List<Tarefa> doDia = Ativas.Where(t => t.OcorreEm(dia)).OrderBy(t => t.Hora).ToList();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Plano de " + dia.ToString("dd/MM/yyyy"));
            sb.AppendLine();
            foreach (Tarefa t in doDia)
                sb.AppendLine(t.Hora + "  " + (t.ConcluidaEm(dia) ? "[feito] " : "") + t.Titulo + (t.Duracao > 0 ? " (" + Cartao.Duracao(t.Duracao) + ")" : ""));
            List<Tarefa> ag = Ativas.Where(t => t.Aguardando && !t.Concluida).ToList();
            if (ag.Count > 0)
            {
                sb.AppendLine(); sb.AppendLine("Em aguardo:");
                foreach (Tarefa t in ag) sb.AppendLine("- " + t.Titulo + (string.IsNullOrEmpty(t.AguardandoDe) ? "" : " (aguardando " + t.AguardandoDe + ")"));
            }
            Clipboard.SetText(sb.ToString());
            lbStatus.Text = "Plano do dia copiado. É só colar.";
        }

        void LimparConcluidas()
        {
            int n = Ativas.Count(t => t.Concluida && !t.EstaAgendada() && !t.Repete());
            if (n == 0) { MessageBox.Show(this, "Nenhuma concluída sem horário.", "Limpar"); return; }
            if (MessageBox.Show(this, "Excluir " + n + " tarefa(s) concluída(s) sem horário?", "Limpar", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            foreach (Tarefa t in Ativas.Where(t => t.Concluida && !t.EstaAgendada() && !t.Repete()).ToList()) t.NaLixeira = Sincronia.Agora();
            dados.Salvar(); AtualizarTudo();
        }

        // --- atualização do programa (baixa o instalador mais novo do GitHub)
        const string UrlVersao = "https://raw.githubusercontent.com/elianahippler/marke-agenda/main/pc/versao.txt";
        const string UrlInstalador = "https://raw.githubusercontent.com/elianahippler/marke-agenda/main/pc/Instalar%20Marke%20Agenda.exe";

        void ProcurarAtualizacao(bool avisar)
        {
            if (avisar) lbStatus.Text = "Procurando atualização...";
            Thread th = new Thread(() =>
            {
                try
                {
                    string nova = Http.Pedir("GET", UrlVersao + "?t=" + DateTime.Now.Ticks, null, null, null).Trim();
                    Version vNova, vAtual = new Version(VersaoApp.Atual);
                    if (!Version.TryParse(nova, out vNova)) throw new Exception("versão inválida no GitHub");
                    BeginInvoke(new Action(() =>
                    {
                        if (vNova <= vAtual)
                        {
                            if (avisar) { lbStatus.Text = "Você já está na versão mais nova (" + VersaoApp.Atual + ")."; MessageBox.Show(this, "A Marke Agenda já está na versão mais nova (" + VersaoApp.Atual + ").", "Atualização"); }
                            return;
                        }
                        if (!avisar) { lbStatus.Text = "Nova versão " + nova + " disponível: menu do seu nome > Procurar atualização."; return; }
                        if (MessageBox.Show(this, "Tem uma versão nova: " + nova + " (você está na " + VersaoApp.Atual + ").\n\nAtualizar agora? A Marke Agenda fecha, atualiza e abre de novo. Sua agenda fica do jeito que está.",
                            "Atualização", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                        BaixarEAtualizar();
                    }));
                }
                catch (Exception ex)
                {
                    Dados.Log("Atualização: " + ex.Message);
                    if (avisar) BeginInvoke(new Action(() => { lbStatus.Text = "Não consegui procurar atualização."; MessageBox.Show(this, "Não consegui procurar atualização: " + ex.Message, "Atualização"); }));
                }
            });
            th.IsBackground = true; th.Start();
        }

        void BaixarEAtualizar()
        {
            lbStatus.Text = "Baixando a versão nova...";
            Thread th = new Thread(() =>
            {
                try
                {
                    System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
                    string pasta = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MarkeAgenda-atualizacao");
                    System.IO.Directory.CreateDirectory(pasta);
                    string inst = System.IO.Path.Combine(pasta, "Instalar Marke Agenda.exe");
                    using (System.Net.WebClient wc = new System.Net.WebClient()) wc.DownloadFile(UrlInstalador + "?t=" + DateTime.Now.Ticks, inst);
                    string aqui = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
                    BeginInvoke(new Action(() =>
                    {
                        // o instalador espera este programa fechar, instala por cima e abre de novo
                        Process.Start(new ProcessStartInfo(inst, "--atualizar \"" + aqui + "\"") { UseShellExecute = false });
                        sair = true;
                        Close();
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(() => { lbStatus.Text = "A atualização falhou."; MessageBox.Show(this, "Não consegui baixar a versão nova: " + ex.Message, "Atualização"); }));
                }
            });
            th.IsBackground = true; th.Start();
        }

        // --- sincronização com o celular (Google Drive)
        System.Windows.Forms.Timer timerSinc, sincLogo;
        volatile bool sincronizando;

        void Sincronizar(bool avisar)
        {
            if (sincronizando) return;
            if (string.IsNullOrEmpty(dados.Config.GoogleRefresh))
            {
                if (avisar) MessageBox.Show(this, "Para sincronizar com o celular, entre com a sua conta Google (menu do seu nome).", "Sincronizar");
                return;
            }
            sincronizando = true;
            Config c = dados.Config;
            Thread th = new Thread(() =>
            {
                try
                {
                    string tk = Integracoes.GoogleAcesso(c);
                    string id = Sincronia.AcharArquivo(tk);
                    string remoto = id == null ? null : Sincronia.Baixar(tk, id);
                    string enviar = null;
                    Invoke(new Action(() =>
                    {
                        if (!string.IsNullOrEmpty(remoto)) Sincronia.Mesclar(dados, Sincronia.Ler(remoto));
                        dados.UltimaSinc = DateTime.Now.ToString("dd/MM HH:mm");
                        dados.Salvar();
                        enviar = Sincronia.Montar(dados);
                        AtualizarLeveOuTudo();
                    }));
                    Sincronia.Enviar(tk, id, enviar);
                    BeginInvoke(new Action(() => { if (avisar || lbStatus.Text.StartsWith("Sincroniz")) lbStatus.Text = "Sincronizado com o celular às " + DateTime.Now.ToString("HH:mm"); }));
                }
                catch (Exception ex)
                {
                    Dados.Log("Sincronizar: " + ex);
                    BeginInvoke(new Action(() =>
                    {
                        lbStatus.Text = "Sincronização com o celular falhou: " + ex.Message;
                        if (avisar) MessageBox.Show(this, ex.Message, "Sincronizar");
                    }));
                }
                finally { sincronizando = false; }
            });
            th.IsBackground = true; th.Start();
            if (avisar) lbStatus.Text = "Sincronizando com o celular...";
        }

        /// Depois de sincronizar: só remonta a agenda se não estiver no meio de um arraste.
        void AtualizarLeveOuTudo()
        {
            if (Control.MouseButtons != MouseButtons.None) { AtualizarLeve(); return; }
            AtualizarTudo();
            if (notaRapida != null && !notaRapida.Focused && notaRapida.Text != (dados.NotaRapida ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n"))
                notaRapida.Text = (dados.NotaRapida ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
        }

        // --- integrações
        void AtualizarIntegracoes(bool avisarSemConfig)
        {
            if (atualizando) return;
            Config c = dados.Config;
            bool g = !string.IsNullOrEmpty(c.GoogleRefresh), tt = !string.IsNullOrEmpty(c.TomToken), md = !string.IsNullOrEmpty(c.MdUrlApi) && !string.IsNullOrEmpty(c.MdEmail);
            if (!g && !tt && !md)
            {
                if (avisarSemConfig) MessageBox.Show(this, "Nenhuma integração configurada ainda. Abra Acessos para cadastrar Google, TomTicket e Markedesk.", "Atualizar integrações");
                return;
            }
            atualizando = true;
            lbStatus.Text = "Atualizando integrações...";
            Thread th = new Thread(() =>
            {
                List<string> msgs = new List<string>();
                Dictionary<string, List<Tarefa>> vindas = new Dictionary<string, List<Tarefa>>();
                if (g) try { vindas["Google"] = Integracoes.Google(c, c.Intervalo); } catch (Exception ex) { msgs.Add("Google: " + ex.Message); Dados.Log("Google: " + ex); }
                if (tt) try { vindas["TomTicket"] = Integracoes.TomTicket(c); } catch (Exception ex) { msgs.Add(ex.Message.StartsWith("TomTicket") ? ex.Message : "TomTicket: " + ex.Message); Dados.Log("TomTicket: " + ex); }
                if (md) try { vindas["Markedesk"] = Integracoes.Markedesk(c); } catch (Exception ex) { msgs.Add(ex.Message.StartsWith("Markedesk") ? ex.Message : "Markedesk: " + ex.Message); Dados.Log("Markedesk: " + ex); }
                BeginInvoke(new Action(() =>
                {
                    List<string> resumo = new List<string>();
                    foreach (KeyValuePair<string, List<Tarefa>> kv in vindas)
                    {
                        int novas = Integracoes.Mesclar(dados, kv.Key, kv.Value);
                        resumo.Add(kv.Key + ": " + kv.Value.Count + (novas > 0 ? " (" + novas + " novas)" : ""));
                    }
                    dados.Salvar(); AtualizarTudo(); atualizando = false;
                    lbStatus.Text = "Atualizado às " + DateTime.Now.ToString("HH:mm") + "   " + string.Join("  ·  ", resumo.Concat(msgs.Select(x => "ERRO " + x)));
                    Sincronizar(false);   // o celular recebe os chamados e atendimentos que o PC buscou
                    if (msgs.Count > 0 && avisarSemConfig) MessageBox.Show(this, string.Join("\n\n", msgs), "Algumas integrações falharam");
                }));
            });
            th.IsBackground = true; th.Start();
        }

        // --- avisos
        void VerificarAvisos()
        {
            AtualizarProgresso();
            if (!dados.Config.AvisarLembretes) return;
            DateTime agora = DateTime.Now;
            string hoje = Tarefa.Dia(DateTime.Today);
            foreach (Tarefa t in Ativas.Where(x => x.OcorreEm(DateTime.Today) && !x.ConcluidaEm(DateTime.Today)))
            {
                if (!dados.Config.AvisarTodas && !dados.TipoDe(t).Avisar) continue;
                DateTime sa;
                if (!string.IsNullOrEmpty(t.SonecaAte) && DateTime.TryParse(t.SonecaAte, out sa))
                {
                    if (agora >= sa) { t.SonecaAte = null; dados.Salvar(); Avisar(t); }
                    continue;
                }
                if (t.AvisadoEm == hoje) continue;
                DateTime h;
                if (!DateTime.TryParse(hoje + " " + t.Hora, out h)) continue;
                if (agora < h.AddMinutes(-dados.Config.AvisoAntesMin) || agora > h.AddMinutes(30)) continue;
                t.AvisadoEm = hoje; dados.Salvar();
                Avisar(t);
            }
        }

        void Avisar(Tarefa t)
        {
            if (dados.Config.AvisoJanela) Lembrete.Mostrar(this, t, dados.Config.AvisoSom);
            else icone.ShowBalloonTip(15000, t.Hora + "  " + dados.TipoDe(t).Nome, t.Titulo, ToolTipIcon.Info);
            if (WindowState == FormWindowState.Minimized || !Visible) FlashWindow.Piscar(this);
        }

        public void ConcluirHoje(Tarefa t)
        {
            t.MarcarConcluida(DateTime.Today, true); t.SonecaAte = null;
            dados.Salvar(); AtualizarLeve();
            lbStatus.Text = "\"" + t.Titulo + "\" concluída";
        }

        public void Adiar(Tarefa t, int minutos)
        {
            t.SonecaAte = DateTime.Now.AddMinutes(minutos).ToString("yyyy-MM-dd HH:mm:ss");
            dados.Salvar();
            lbStatus.Text = "Lembrete de \"" + t.Titulo + "\" adiado para " + DateTime.Now.AddMinutes(minutos).ToString("HH:mm");
        }

        // ------------------------------------------------------------------ notas
        void MontarNotas()
        {
            painelNotas = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Fundo, Visible = false };
            Panel esq = new Panel { Dock = DockStyle.Left, Width = 310, BackColor = Tema.Painel, Padding = new Padding(12, 6, 12, 12) };
            Label t = Titulo("Notas");
            buscaNota = new TextBox { Dock = DockStyle.Top, BackColor = Tema.Fundo, ForeColor = Tema.Texto, BorderStyle = BorderStyle.FixedSingle };
            buscaNota.TextChanged += (s, e) => CarregarNotas(null);
            listaNotas = new ListaSuave { Dock = DockStyle.Fill, BackColor = Tema.Painel, ForeColor = Tema.Texto, BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 58, IntegralHeight = false };
            listaNotas.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                Nota n = (Nota)listaNotas.Items[e.Index];
                bool sel = (e.State & DrawItemState.Selected) != 0;
                using (SolidBrush fb = new SolidBrush(Tema.Painel)) e.Graphics.FillRectangle(fb, e.Bounds);
                Rectangle r = new Rectangle(e.Bounds.X + 1, e.Bounds.Y + 3, e.Bounds.Width - 4, e.Bounds.Height - 6);
                using (SolidBrush b = new SolidBrush(sel ? Tema.Linha : Tema.Misturar(Tema.Linha, Tema.Painel, 0.4))) Tema.Arredondado(e.Graphics, r, 7, b);
                if (sel) using (SolidBrush b = new SolidBrush(Tema.Roxo)) Tema.Arredondado(e.Graphics, new Rectangle(r.X + 3, r.Y + 6, 4, r.Height - 12), 2, b);
                TextRenderer.DrawText(e.Graphics, string.IsNullOrEmpty(n.Titulo) ? "(sem título)" : n.Titulo, Tema.Negrito, new Rectangle(r.X + 13, r.Y + 4, r.Width - 18, 20), Tema.Texto, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                string previa = (n.Texto ?? "").Replace("\r", " ").Replace("\n", " ");
                TextRenderer.DrawText(e.Graphics, previa, Tema.Pequena, new Rectangle(r.X + 13, r.Y + 23, r.Width - 18, 15), Tema.Fraco, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(e.Graphics, n.Atualizada ?? "", Tema.Pequena, new Rectangle(r.X + 13, r.Y + 37, r.Width - 18, 14), Tema.Misturar(Tema.Fraco, Tema.Painel, 0.7), TextFormatFlags.NoPrefix);
            };
            listaNotas.SelectedIndexChanged += (s, e) => MostrarNota();
            FlowLayoutPanel bts = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, BackColor = Tema.Painel };
            Button nova = Tema.Botao("+ Nova nota", true), excluir = Tema.Botao("Excluir", false);
            nova.Click += (s, e) =>
            {
                SalvarNotaAgora();
                Nota n = new Nota { Id = Dados.NovoId(), Titulo = "Nova nota", Texto = "", Atualizada = DateTime.Now.ToString("dd/MM/yyyy HH:mm") };
                dados.Notas.Insert(0, n); dados.Salvar(); buscaNota.Text = ""; CarregarNotas(n);
                notaTitulo.Focus(); notaTitulo.SelectAll();
            };
            excluir.Click += (s, e) =>
            {
                Nota n = listaNotas.SelectedItem as Nota;
                if (n == null || MessageBox.Show(this, "Excluir a nota \"" + n.Titulo + "\"?", "Excluir nota", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                notaAtual = null; dados.Notas.Remove(n); dados.Salvar(); CarregarNotas(null);
            };
            bts.Controls.AddRange(new Control[] { nova, excluir });
            esq.Controls.Add(listaNotas); esq.Controls.Add(Espaco(6)); esq.Controls.Add(buscaNota); esq.Controls.Add(t); esq.Controls.Add(bts);

            Panel dir = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 18, 28, 18), BackColor = Tema.Fundo };
            notaTitulo = new TextBox { Dock = DockStyle.Top, Font = new Font("Segoe UI Semibold", 16f), BackColor = Tema.Fundo, ForeColor = Tema.Texto, BorderStyle = BorderStyle.None };
            Panel barra = new Panel { Dock = DockStyle.Top, Height = 48 };
            Button virarTarefa = Tema.Botao("Criar tarefa com esta nota", false); virarTarefa.Location = new Point(0, 10);
            virarTarefa.Click += (s, e) => { Nota n = listaNotas.SelectedItem as Nota; if (n != null) { SalvarNotaAgora(); NovaTarefa(n); } };
            barra.Controls.Add(virarTarefa);
            Panel moldura = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Painel, Padding = new Padding(14) };
            notaTexto = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsTab = true, BackColor = Tema.Painel, ForeColor = Tema.Texto, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 11f) };
            moldura.Controls.Add(notaTexto);
            dir.Controls.Add(moldura); dir.Controls.Add(barra); dir.Controls.Add(notaTitulo);

            salvarNota = new System.Windows.Forms.Timer { Interval = 800 };
            salvarNota.Tick += (s, e) => SalvarNotaAgora();
            notaTitulo.TextChanged += (s, e) => { if (!carregandoNota) { salvarNota.Stop(); salvarNota.Start(); } };
            notaTexto.TextChanged += (s, e) => { if (!carregandoNota) { salvarNota.Stop(); salvarNota.Start(); } };

            painelNotas.Controls.Add(dir); painelNotas.Controls.Add(esq);
            Controls.Add(painelNotas);
            Tema.Dica(buscaNota, "Buscar nota");
        }

        void CarregarNotas(Nota selecionar)
        {
            if (selecionar == null) selecionar = notaAtual;
            string q = buscaNota.Text.Trim().ToLowerInvariant();
            listaNotas.BeginUpdate(); listaNotas.Items.Clear();
            foreach (Nota n in dados.Notas.Where(n => q == "" || ((n.Titulo ?? "") + " " + (n.Texto ?? "")).ToLowerInvariant().Contains(q)))
                listaNotas.Items.Add(n);
            listaNotas.EndUpdate();
            if (selecionar != null && listaNotas.Items.Contains(selecionar)) listaNotas.SelectedItem = selecionar;
            else if (listaNotas.Items.Count > 0) listaNotas.SelectedIndex = 0;
            else MostrarNota();
        }

        void MostrarNota()
        {
            SalvarNotaAgora();
            notaAtual = listaNotas.SelectedItem as Nota;
            carregandoNota = true;
            notaTitulo.Text = notaAtual == null ? "" : notaAtual.Titulo;
            notaTexto.Text = notaAtual == null ? "" : (notaAtual.Texto ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
            notaTitulo.Enabled = notaTexto.Enabled = notaAtual != null;
            carregandoNota = false;
        }

        void SalvarNotaAgora()
        {
            if (salvarNota != null) salvarNota.Stop();
            if (notaAtual == null) return;
            if (notaAtual.Titulo == notaTitulo.Text && notaAtual.Texto == notaTexto.Text) return;
            notaAtual.Titulo = notaTitulo.Text; notaAtual.Texto = notaTexto.Text;
            notaAtual.Atualizada = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            dados.Salvar();
            listaNotas.Invalidate();
        }
    }

    /// Bloco de uma tarefa dentro de um horário.
    public class Chip : Control
    {
        Tela tela; Tarefa t; Point inicio; bool sobre;

        public Chip(Tela tela, Tarefa t)
        {
            this.tela = tela; this.t = t;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent; Cursor = Cursors.Hand;
            string dica = t.Titulo + "\n" + tela.Dados.TipoDe(t).Nome + (t.Repete() ? " · " + Tarefa.NomeRepeticao(t.Repetir) : "")
                + (string.IsNullOrEmpty(t.Descricao) ? "" : "\n\n" + (t.Descricao.Length > 400 ? t.Descricao.Substring(0, 400) + "..." : t.Descricao))
                + "\n\nDois cliques: editar  ·  Botão direito: opções";
            new ToolTip { AutoPopDelay = 20000 }.SetToolTip(this, dica);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color fundo = Parent != null ? Parent.BackColor : Tema.Fundo;
            Cartao.Desenhar(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), t, tela.Dados, tela.Dia, true, sobre, fundo);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); sobre = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); sobre = false; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); inicio = e.Location;
            if (e.Button == MouseButtons.Left && !t.Fixa && Rectangle.Inflate(Cartao.Lixeira(new Rectangle(0, 0, Width - 1, Height - 1)), 2, 2).Contains(e.Location))
            {
                inicio = new Point(-1000, -1000);
                Tarefa t1 = t; Tela tl1 = tela;
                tl1.BeginInvoke(new Action(() => tl1.NaoEMinha(t1)));
                return;
            }
            if (e.Button == MouseButtons.Left && Rectangle.Inflate(Cartao.Bolinha(new Rectangle(0, 0, Width - 1, Height - 1)), 4, 4).Contains(e.Location))
            {
                inicio = new Point(-1000, -1000);
                Tarefa t0 = t; Tela tl = tela;
                tl.BeginInvoke(new Action(() => tl.AlternarConcluida(t0)));
                return;
            }
            if (e.Button == MouseButtons.Right) tela.MenuTarefa(t).Show(this, e.Location);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Button == MouseButtons.Left && inicio.X > -500 && Math.Abs(e.X - inicio.X) + Math.Abs(e.Y - inicio.Y) > 6)
                Arraste.Iniciar(this, t, tela.Dados, tela.Dia, Size, e.Location);
        }
        protected override void OnDoubleClick(EventArgs e) { base.OnDoubleClick(e); tela.Editar(t); }
    }

    /// Barrinha de progresso do dia.
    public class ProgressoDia : Control
    {
        double valor;
        public double Valor { get { return valor; } set { valor = Math.Max(0, Math.Min(1, value)); Invalidate(); } }
        public ProgressoDia() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Tema.Fundo);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (SolidBrush b = new SolidBrush(Tema.Linha)) Tema.Arredondado(e.Graphics, r, 3, b);
            int w = (int)(r.Width * valor);
            if (w > 6)
                using (LinearGradientBrush b = new LinearGradientBrush(r, Tema.Roxo, Tema.Ok, 0f))
                    Tema.Arredondado(e.Graphics, new Rectangle(0, 0, w, r.Height), 3, b);
        }
    }

    /// Lista que não apaga o fundo antes de desenhar (é isso que fazia piscar).
    public class ListaSuave : ListBox
    {
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x14)   // WM_ERASEBKGND: pinta só a parte vazia, abaixo do último item
            {
                using (Graphics g = Graphics.FromHdc(m.WParam))
                {
                    int fim = Items.Count > 0 ? GetItemRectangle(Items.Count - 1).Bottom : 0;
                    if (fim < ClientSize.Height)
                        using (SolidBrush b = new SolidBrush(BackColor)) g.FillRectangle(b, 0, fim, ClientSize.Width, ClientSize.Height - fim);
                }
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }
    }

    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel() { DoubleBuffered = true; }
    }

    static class FlashWindow
    {
        [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
        struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }
        public static void Piscar(Form f)
        {
            FLASHWINFO fi = new FLASHWINFO { hwnd = f.Handle, dwFlags = 3 | 12, uCount = 5 };
            fi.cbSize = (uint)Marshal.SizeOf(fi);
            FlashWindowEx(ref fi);
        }
    }
}
