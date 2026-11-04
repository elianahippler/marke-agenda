// Marke Agenda: lembrete em janela que fica por cima de tudo até ser respondido.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Media;
using System.Windows.Forms;

namespace MarkeAgenda
{
    public class Lembrete : Form
    {
        static List<Lembrete> abertos = new List<Lembrete>();
        Tarefa t; Tela tela; Color cor; int pulso;
        Timer anima;

        public static void Mostrar(Tela tela, Tarefa t, bool som)
        {
            if (abertos.Any(x => x.t == t)) return;
            Lembrete l = new Lembrete(tela, t);
            abertos.Add(l);
            l.Posicionar();
            l.Show();
            if (som) SystemSounds.Exclamation.Play();
        }

        Lembrete(Tela tela, Tarefa t)
        {
            this.tela = tela; this.t = t;
            cor = tela.Dados.CorDe(t);
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            TopMost = true; Size = new Size(380, 150); BackColor = Tema.Painel; Font = Tema.Fonte; DoubleBuffered = true;

            Label quando = new Label { Text = "LEMBRETE  ·  " + (string.IsNullOrEmpty(t.Hora) ? "agora" : t.Hora) + "  ·  " + tela.Dados.TipoDe(t).Nome, Location = new Point(20, 14), Size = new Size(320, 18), ForeColor = Tema.Misturar(cor, Color.White, 0.6), Font = Tema.PequenaN, BackColor = Color.Transparent };
            Label titulo = new Label { Text = t.Titulo, Location = new Point(20, 34), Size = new Size(345, 48), ForeColor = Tema.Texto, Font = new Font("Segoe UI Semibold", 12f), AutoEllipsis = true, BackColor = Color.Transparent };
            Button fechar = new Button { Text = "", Font = new Font("Segoe MDL2 Assets", 8f), Location = new Point(346, 6), Size = new Size(28, 26), FlatStyle = FlatStyle.Flat, ForeColor = Tema.Fraco, BackColor = Tema.Painel, TabStop = false };
            fechar.FlatAppearance.BorderSize = 0;
            fechar.Click += (s, e) => Close();

            Button concluir = Tema.Botao("✓ Concluir", true);
            Button adiar = Tema.Botao("Adiar  ▾", false);
            Button abrir = Tema.Botao("Abrir", false);
            concluir.Location = new Point(20, 100); adiar.Location = new Point(124, 100); abrir.Location = new Point(214, 100);
            concluir.Click += (s, e) => { tela.ConcluirHoje(t); Close(); };
            abrir.Click += (s, e) => { Close(); tela.Reabrir(); tela.Editar(t); };
            adiar.Click += (s, e) =>
            {
                ContextMenuStrip m = new ContextMenuStrip();
                Tema.Menu(m);
                foreach (int min in new[] { 5, 10, 15, 30, 60 })
                {
                    int m0 = min;
                    m.Items.Add(min < 60 ? min + " minutos" : "1 hora", null, (o, a) => { tela.Adiar(t, m0); Close(); });
                }
                m.Show(adiar, new Point(0, adiar.Height));
            };
            Controls.AddRange(new Control[] { quando, titulo, fechar, concluir, adiar, abrir });

            // clicar em qualquer parte do lembrete (fora dos botões) fecha
            Cursor = Cursors.Hand; quando.Cursor = titulo.Cursor = Cursors.Hand;
            Click += (s, e) => Close();
            quando.Click += (s, e) => Close();
            titulo.Click += (s, e) => Close();

            // borda pulsando por alguns segundos, para chamar a atenção
            anima = new Timer { Interval = 120 };
            anima.Tick += (s, e) => { pulso++; Invalidate(); if (pulso > 60) anima.Stop(); };
            anima.Start();
            FormClosed += (s, e) => { anima.Stop(); abertos.Remove(this); Reorganizar(); };
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x8 | 0x80;   // sempre por cima, fora da barra de tarefas
                cp.ClassStyle |= 0x20000;   // sombra
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush b = new SolidBrush(cor)) g.FillRectangle(b, 0, 0, 6, Height);
            double f = anima.Enabled ? 0.5 + 0.5 * Math.Abs(Math.Sin(pulso / 3.0)) : 0.6;
            using (Pen p = new Pen(Tema.Misturar(cor, Tema.Painel, f), 2)) g.DrawRectangle(p, 1, 1, Width - 2, Height - 2);
        }

        void Posicionar()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int i = abertos.IndexOf(this);
            Location = new Point(wa.Right - Width - 16, wa.Bottom - (Height + 12) * (i + 1) - 4);
        }

        static void Reorganizar() { foreach (Lembrete l in abertos) l.Posicionar(); }
    }
}
