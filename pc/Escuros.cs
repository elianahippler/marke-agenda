// Marke Agenda: versões escuras dos componentes que o Windows desenha em branco
// (lista de escolha, campo de data, abas).
using System;
using System.Drawing;
using System.Windows.Forms;

namespace MarkeAgenda
{
    public static class Escuros
    {
        /// Lista de escolha (ComboBox) escura: itens, seleção, borda e seta.
        public static void Combo(ComboBox c)
        {
            if ("escuro".Equals(c.AccessibleDescription)) return;
            c.AccessibleDescription = "escuro";
            c.FlatStyle = FlatStyle.Flat; c.BackColor = Tema.Painel; c.ForeColor = Tema.Texto;
            c.DrawMode = DrawMode.OwnerDrawFixed;
            c.ItemHeight = Math.Max(c.ItemHeight, 20);
            c.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
                bool sel = (e.State & DrawItemState.Selected) != 0 && !edit;
                using (SolidBrush b = new SolidBrush(sel ? Tema.Roxo : Tema.Painel)) e.Graphics.FillRectangle(b, e.Bounds);
                TextRenderer.DrawText(e.Graphics, c.GetItemText(c.Items[e.Index]), c.Font,
                    new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height), Tema.Texto,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
            new PintorCombo(c);
        }

        /// Calendário escuro que abre embaixo de um controle.
        public static void Calendario(Control ancora, DateTime atual, Action<DateTime> escolher, string textoLimpar, Action limpar)
        {
            MonthCalendar mc = new MonthCalendar { MaxSelectionCount = 1, ShowTodayCircle = true, Font = Tema.Fonte };
            IntPtr h = mc.Handle;           // cria agora para tirar o tema antes de medir
            Tema.SemTema(mc);
            mc.BackColor = Tema.Painel; mc.ForeColor = Tema.Texto;
            mc.TitleBackColor = Tema.Roxo; mc.TitleForeColor = Color.White;
            mc.TrailingForeColor = Tema.Misturar(Tema.Fraco, Tema.Painel, 0.5);
            mc.Size = mc.SingleMonthSize;
            mc.SetDate(atual);

            ToolStripDropDown dd = new ToolStripDropDown { Padding = new Padding(1), DropShadowEnabled = true };
            Tema.Menu(dd);
            dd.Items.Add(new ToolStripControlHost(mc) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = mc.Size });
            if (limpar != null)
            {
                ToolStripMenuItem b = new ToolStripMenuItem(textoLimpar) { ForeColor = Tema.Texto };
                b.Click += (s, e) => { dd.Close(); limpar(); };
                dd.Items.Add(b);
            }
            mc.DateSelected += (s, e) => { dd.Close(); escolher(e.Start.Date); };
            dd.Closed += (s, e) => ancora.BeginInvoke(new Action(() => dd.Dispose()));
            dd.Show(ancora, new Point(0, ancora.Height + 2));
        }
    }

    /// Desenha borda e seta escuras por cima da lista de escolha.
    class PintorCombo : NativeWindow
    {
        ComboBox c;
        public PintorCombo(ComboBox c)
        {
            this.c = c;
            if (c.IsHandleCreated) AssignHandle(c.Handle);
            c.HandleCreated += (s, e) => AssignHandle(c.Handle);
            c.HandleDestroyed += (s, e) => ReleaseHandle();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0xF) return;   // WM_PAINT
            using (Graphics g = Graphics.FromHwnd(c.Handle))
            {
                int bw = SystemInformation.VerticalScrollBarWidth;
                Rectangle seta = new Rectangle(c.Width - bw - 1, 1, bw, c.Height - 2);
                using (SolidBrush b = new SolidBrush(Tema.Painel)) g.FillRectangle(b, seta);
                int cx = seta.X + seta.Width / 2, cy = seta.Y + seta.Height / 2;
                using (SolidBrush b = new SolidBrush(Tema.Fraco))
                    g.FillPolygon(b, new[] { new Point(cx - 4, cy - 2), new Point(cx + 4, cy - 2), new Point(cx, cy + 3) });
                using (Pen p = new Pen(c.Focused ? Tema.Roxo : Tema.Linha)) g.DrawRectangle(p, 0, 0, c.Width - 1, c.Height - 1);
            }
        }
    }

    /// Campo de data escuro: mostra a data e abre o calendário escuro.
    public class DataEscura : Button
    {
        DateTime valor = DateTime.Today;
        bool marcado = true;
        string vazio = "Sem data";

        public string TextoVazio { get { return vazio; } set { vazio = value; Atualizar(); } }
        public DateTime Value { get { return valor; } set { valor = value.Date; Atualizar(); } }
        public bool Checked { get { return marcado; } set { marcado = value; Atualizar(); } }

        public DataEscura()
        {
            FlatStyle = FlatStyle.Flat; BackColor = Tema.Painel; Height = 30; Cursor = Cursors.Hand;
            TextAlign = ContentAlignment.MiddleLeft; FlatAppearance.BorderColor = Tema.Linha;
            FlatAppearance.MouseOverBackColor = Tema.Linha;
            Atualizar();
            Click += (s, e) => Escuros.Calendario(this, valor, d => { valor = d; marcado = true; Atualizar(); }, vazio, () => { marcado = false; Atualizar(); });
        }

        void Atualizar()
        {
            Text = "📅  " + (marcado ? valor.ToString("dd/MM/yyyy") : vazio);
            ForeColor = marcado ? Tema.Texto : Tema.Fraco;
        }
    }

    /// Abas escuras (o TabControl do Windows só sabe ser branco).
    public class AbasEscuras : TabControl
    {
        public AbasEscuras()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SizeMode = TabSizeMode.Fixed; ItemSize = new Size(140, 32);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Tema.Fundo);
            Rectangle area = DisplayRectangle; area.Inflate(1, 1);
            using (Pen p = new Pen(Tema.Linha)) g.DrawRectangle(p, area.X, area.Y, area.Width - 1, area.Height - 1);
            for (int i = 0; i < TabCount; i++)
            {
                Rectangle r = GetTabRect(i);
                bool sel = i == SelectedIndex;
                using (SolidBrush b = new SolidBrush(sel ? Tema.Painel : Tema.Fundo)) g.FillRectangle(b, r);
                if (sel) using (SolidBrush b = new SolidBrush(Tema.Roxo)) g.FillRectangle(b, r.X, r.Bottom - 3, r.Width, 3);
                TextRenderer.DrawText(g, TabPages[i].Text, sel ? Tema.Negrito : Tema.Fonte, r, sel ? Tema.Texto : Tema.Fraco,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
