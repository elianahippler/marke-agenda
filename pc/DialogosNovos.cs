// Marke Agenda: diálogos novos — excluir repetição, intervalos do dia e tipos de evento.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MarkeAgenda
{
    /// Ao excluir uma tarefa que repete: só o dia que está na tela, ou a repetição inteira.
    public class DialogoExcluirRepeticao : DialogoBase
    {
        public bool SoEsta { get; private set; }

        public DialogoExcluirRepeticao(Tarefa t, DateTime dia) : base("Excluir tarefa que repete", 440)
        {
            Label titulo = new Label
            {
                Text = "\"" + t.Titulo + "\"",
                Font = Tema.Titulo,
                Location = new Point(X, y),
                Size = new Size(Largura - 2 * X, 24),
                AutoEllipsis = true,
            };
            Controls.Add(titulo); y += 28;

            Rotulo(this, "Esta tarefa repete: " + t.DetalheRepeticao() + ".");
            y += 6;

            RadioButton so = new RadioButton
            {
                Text = "Remover só a de " + dia.ToString("dd/MM/yyyy"),
                AutoSize = true,
                Checked = true,
                Location = new Point(X, y),
            };
            Controls.Add(so); y += 22;
            Label soAjuda = new Label
            {
                Text = "As outras continuam na agenda.",
                AutoSize = true,
                ForeColor = Tema.Fraco,
                Font = Tema.Pequena,
                Location = new Point(X + 20, y),
            };
            Controls.Add(soAjuda); y += 26;

            RadioButton todas = new RadioButton { Text = "Remover todas as repetições", AutoSize = true, Location = new Point(X, y) };
            Controls.Add(todas); y += 22;
            Label todasAjuda = new Label
            {
                Text = "A tarefa inteira vai para a Lixeira (dá para restaurar por 30 dias).",
                AutoSize = true,
                ForeColor = Tema.Fraco,
                Font = Tema.Pequena,
                Location = new Point(X + 20, y),
            };
            Controls.Add(todasAjuda); y += 30;

            Button ok = Tema.Botao("Excluir", true);
            ok.Click += (s, e) => { SoEsta = so.Checked; DialogResult = DialogResult.OK; };
            Rodape(ok);
        }
    }

    /// Intervalos do dia que não aparecem na agenda: almoço, café, reunião fixa...
    public class DialogoIntervalos : DialogoBase
    {
        readonly Dados d;
        readonly ListBox lista;

        public DialogoIntervalos(Dados dados) : base("Intervalos fora da agenda", 460)
        {
            d = dados;
            d.AjeitarIntervalos();

            Rotulo(this, "Nesses horários a agenda não mostra linha nenhuma.");
            lista = new ListBox
            {
                Location = new Point(X, y),
                Size = new Size(Largura - 2 * X, 200),
                BackColor = Tema.Painel,
                ForeColor = Tema.Texto,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false,
            };
            Controls.Add(lista); y += 208;

            Button novo = Tema.Botao("+ Novo", false);
            Button editar = Tema.Botao("Editar", false);
            Button ligar = Tema.Botao("Ligar/desligar", false);
            Button remover = Tema.Botao("Remover", false);
            int bx = X;
            foreach (Button b in new[] { novo, editar, ligar, remover })
            {
                b.AutoSize = false; b.Width = b == ligar ? 110 : 80; b.Height = 28;
                b.Location = new Point(bx, y); bx += b.Width + 6;
                Controls.Add(b);
            }
            y += 36;

            novo.Click += (s, e) => Editar(null);
            editar.Click += (s, e) => { Intervalo i = Atual(); if (i != null) Editar(i); };
            lista.DoubleClick += (s, e) => { Intervalo i = Atual(); if (i != null) Editar(i); };
            ligar.Click += (s, e) =>
            {
                Intervalo i = Atual();
                if (i == null) return;
                i.Ligado = !i.Ligado; i.Mod = Sincronia.Agora();
                Recarregar();
            };
            remover.Click += (s, e) =>
            {
                Intervalo i = Atual();
                if (i == null) return;
                if (MessageBox.Show(this, "Remover o intervalo \"" + i.Nome + "\"?", "Intervalos", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                d.Intervalos.Remove(i);
                d.Excluidos[i.Id] = Sincronia.Agora();
                Recarregar();
            };

            Button ok = Tema.Botao("Fechar", true);
            ok.Click += (s, e) => DialogResult = DialogResult.OK;
            Rodape(ok);
            Recarregar();
        }

        Intervalo Atual() { return lista.SelectedIndex < 0 ? null : d.Intervalos[lista.SelectedIndex]; }

        void Recarregar()
        {
            int sel = lista.SelectedIndex;
            lista.Items.Clear();
            foreach (Intervalo i in d.Intervalos)
                lista.Items.Add((i.Ligado ? "ON   " : "off  ") + i.Nome + "   " + i.Inicio + " - " + i.Fim);
            if (sel >= 0 && sel < lista.Items.Count) lista.SelectedIndex = sel;
            else if (lista.Items.Count > 0) lista.SelectedIndex = 0;
        }

        void Editar(Intervalo i)
        {
            bool novo = i == null;
            if (novo) i = new Intervalo { Id = Dados.NovoId(), Nome = "", Inicio = "15:00", Fim = "15:15", Ligado = true };
            using (DialogoUmIntervalo f = new DialogoUmIntervalo(i))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                i.Mod = Sincronia.Agora();
                if (novo) d.Intervalos.Add(i);
            }
            Recarregar();
        }
    }

    /// Um intervalo: nome e horário.
    public class DialogoUmIntervalo : DialogoBase
    {
        public DialogoUmIntervalo(Intervalo i) : base(string.IsNullOrEmpty(i.Nome) ? "Novo intervalo" : "Intervalo", 380)
        {
            TextBox nome = Campo(this, "Nome (almoço, café, reunião...)", new TextBox { Text = i.Nome ?? "" }, 0);

            Rotulo(this, "Das ... às ...");
            MaskedTextBox ini = new MaskedTextBox { Mask = "00:00", Text = i.Inicio ?? "12:00", Width = 70, Location = new Point(X, y) };
            Label ate = new Label { Text = "às", AutoSize = true, Location = new Point(X + 80, y + 4), ForeColor = Tema.Fraco };
            MaskedTextBox fim = new MaskedTextBox { Mask = "00:00", Text = i.Fim ?? "13:30", Width = 70, Location = new Point(X + 105, y) };
            Controls.AddRange(new Control[] { ini, ate, fim });
            y += 36;

            CheckBox ligado = new CheckBox { Text = "Ligado (esconde esses horários da agenda)", AutoSize = true, Location = new Point(X, y), Checked = i.Ligado };
            Controls.Add(ligado); y += 32;

            Button ok = Tema.Botao("Salvar", true);
            ok.Click += (s, e) =>
            {
                if (nome.Text.Trim() == "") { MessageBox.Show(this, "Dê um nome para o intervalo."); nome.Focus(); return; }
                int mi = Intervalo.Minutos(ini.Text), mf = Intervalo.Minutos(fim.Text);
                if (mi < 0 || mf < 0) { MessageBox.Show(this, "Horário inválido. Use o formato 12:00."); ini.Focus(); return; }
                if (mf <= mi) { MessageBox.Show(this, "O fim do intervalo precisa ser depois do início."); fim.Focus(); return; }
                i.Nome = nome.Text.Trim(); i.Inicio = ini.Text; i.Fim = fim.Text; i.Ligado = ligado.Checked;
                DialogResult = DialogResult.OK;
            };
            Rodape(ok);
        }
    }

    /// Tipos de evento: aniversário, férias, feriado... a pessoa cadastra os que quiser.
    public class DialogoTiposEvento : DialogoBase
    {
        readonly Dados d;
        readonly ListBox lista;

        public DialogoTiposEvento(Dados dados) : base("Tipos de evento", 440)
        {
            d = dados;
            d.AjeitarTiposEvento();

            Rotulo(this, "Usados ao criar um evento (férias, aniversário, feriado...).");
            lista = new ListBox
            {
                Location = new Point(X, y),
                Size = new Size(Largura - 2 * X, 200),
                BackColor = Tema.Painel,
                ForeColor = Tema.Texto,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 24,
            };
            lista.DrawItem += Desenhar;
            Controls.Add(lista); y += 208;

            Button novo = Tema.Botao("+ Novo", false);
            Button editar = Tema.Botao("Editar", false);
            Button remover = Tema.Botao("Remover", false);
            int bx = X;
            foreach (Button b in new[] { novo, editar, remover })
            {
                b.AutoSize = false; b.Width = 80; b.Height = 28;
                b.Location = new Point(bx, y); bx += 86;
                Controls.Add(b);
            }
            y += 36;

            novo.Click += (s, e) => Editar(null);
            editar.Click += (s, e) => { TipoEvento t = Atual(); if (t != null) Editar(t); };
            lista.DoubleClick += (s, e) => { TipoEvento t = Atual(); if (t != null) Editar(t); };
            remover.Click += (s, e) =>
            {
                TipoEvento t = Atual();
                if (t == null) return;
                int usos = d.Eventos.Count(x => x.Tipo == t.Id);
                string aviso = usos > 0
                    ? "Remover o tipo \"" + t.Nome + "\"?\n\n" + usos + " evento(s) usam ele e vão ficar sem tipo."
                    : "Remover o tipo \"" + t.Nome + "\"?";
                if (MessageBox.Show(this, aviso, "Tipos de evento", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                d.TiposEvento.Remove(t);
                d.Excluidos[t.Id] = Sincronia.Agora();
                Recarregar();
            };

            Button ok = Tema.Botao("Fechar", true);
            ok.Click += (s, e) => DialogResult = DialogResult.OK;
            Rodape(ok);
            Recarregar();
        }

        TipoEvento Atual() { return lista.SelectedIndex < 0 ? null : d.TiposEvento[lista.SelectedIndex]; }

        void Recarregar()
        {
            int sel = lista.SelectedIndex;
            lista.Items.Clear();
            foreach (TipoEvento t in d.TiposEvento) lista.Items.Add(t.Nome);
            if (sel >= 0 && sel < lista.Items.Count) lista.SelectedIndex = sel;
            else if (lista.Items.Count > 0) lista.SelectedIndex = 0;
        }

        /// Desenha o quadradinho da cor ao lado do nome.
        void Desenhar(object remetente, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= d.TiposEvento.Count) return;
            e.DrawBackground();
            TipoEvento t = d.TiposEvento[e.Index];
            using (SolidBrush b = new SolidBrush(Cores.De(t.Cor)))
                e.Graphics.FillRectangle(b, new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 6, 12, 12));
            TextRenderer.DrawText(e.Graphics, t.Nome, Tema.Fonte,
                new Rectangle(e.Bounds.X + 26, e.Bounds.Y, e.Bounds.Width - 26, e.Bounds.Height),
                Tema.Texto, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        void Editar(TipoEvento t)
        {
            bool novo = t == null;
            if (novo) t = new TipoEvento { Id = Dados.NovoId(), Nome = "", Cor = "#38BDF8" };
            using (DialogoUmTipoEvento f = new DialogoUmTipoEvento(t))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                t.Mod = Sincronia.Agora();
                if (novo) d.TiposEvento.Add(t);
            }
            Recarregar();
        }
    }

    /// Um tipo de evento: nome e cor.
    public class DialogoUmTipoEvento : DialogoBase
    {
        public DialogoUmTipoEvento(TipoEvento t) : base(string.IsNullOrEmpty(t.Nome) ? "Novo tipo de evento" : "Tipo de evento", 360)
        {
            TextBox nome = Campo(this, "Nome (Aniversário, Férias, Feriado...)", new TextBox { Text = t.Nome ?? "" }, 0);

            Rotulo(this, "Cor");
            Button cor = BotaoCor(Cores.De(t.Cor));
            cor.Location = new Point(X, y);
            Controls.Add(cor); y += 38;

            Button ok = Tema.Botao("Salvar", true);
            ok.Click += (s, e) =>
            {
                if (nome.Text.Trim() == "") { MessageBox.Show(this, "Dê um nome para o tipo."); nome.Focus(); return; }
                t.Nome = nome.Text.Trim(); t.Cor = Cores.Hex(cor.BackColor);
                DialogResult = DialogResult.OK;
            };
            Rodape(ok);
        }
    }
}
