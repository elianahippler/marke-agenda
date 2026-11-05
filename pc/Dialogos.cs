// Marke Agenda: janelas de edição (tarefa, tipos, acessos, configurações).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MarkeAgenda
{
    /// Base com layout simples: rótulo em cima, campo embaixo.
    public class DialogoBase : Form
    {
        protected int y = 14;
        protected const int X = 16;
        protected int Largura = 460;

        public DialogoBase(string titulo, int largura)
        {
            Text = titulo; Largura = largura; Font = Tema.Fonte;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent; AutoScroll = true;
            BackColor = Tema.Fundo; ForeColor = Tema.Texto;
        }

        protected Label Rotulo(Control pai, string texto)
        {
            Label l = new Label { Text = texto, Location = new Point(X, y), AutoSize = true, ForeColor = Tema.Fraco };
            pai.Controls.Add(l); y += 20; return l;
        }

        protected T Campo<T>(Control pai, string rotulo, T c, int altura) where T : Control
        {
            if (rotulo != null) Rotulo(pai, rotulo);
            c.Location = new Point(X, y); c.Width = Largura - 2 * X; if (altura > 0) c.Height = altura;
            pai.Controls.Add(c); y += c.Height + 10; return c;
        }

        protected void Rodape(Button ok)
        {
            Button cancelar = Tema.Botao("Cancelar", false);
            ok.Location = new Point(X, y + 6); cancelar.Location = new Point(X + 110, y + 6);
            cancelar.DialogResult = DialogResult.Cancel;
            Controls.Add(ok); Controls.Add(cancelar);
            CancelButton = cancelar;
            ClientSize = new Size(Largura, Math.Min(y + 52, Screen.PrimaryScreen.WorkingArea.Height - 80));
            Tema.Aplicar(this);
        }

        protected static Button BotaoCor(Color inicial)
        {
            Button b = new Button { Width = 90, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = inicial, Text = "", Cursor = Cursors.Hand };
            b.Click += (s, e) =>
            {
                using (ColorDialog cd = new ColorDialog { Color = b.BackColor, FullOpen = true })
                    if (cd.ShowDialog() == DialogResult.OK) b.BackColor = cd.Color;
            };
            return b;
        }
    }

    // ------------------------------------------------------------------ tarefa
    public class DialogoTarefa : DialogoBase
    {
        public DialogoTarefa(Dados d, Tarefa t, DateTime dia) : base(string.IsNullOrEmpty(t.Titulo) ? "Nova tarefa" : "Editar tarefa", 500)
        {
            TextBox titulo = Campo(this, "Título", new TextBox { Text = t.Titulo }, 0);

            ComboBox tipo = Campo(this, "Tipo", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 0);
            foreach (Tipo tp in d.Tipos) tipo.Items.Add(tp.Nome);
            tipo.SelectedIndex = Math.Max(0, d.Tipos.FindIndex(x => x.Id == t.TipoId));

            Rotulo(this, "Cor");
            RadioButton corTipo = new RadioButton { Text = "Cor do tipo", AutoSize = true, Location = new Point(X, y), Checked = string.IsNullOrEmpty(t.Cor) };
            RadioButton corPropria = new RadioButton { Text = "Cor própria", AutoSize = true, Location = new Point(X + 120, y), Checked = !string.IsNullOrEmpty(t.Cor) };
            Button cor = BotaoCor(d.CorDe(t)); cor.Location = new Point(X + 240, y - 3);
            cor.Visible = corPropria.Checked;
            corPropria.CheckedChanged += (s, e) => cor.Visible = corPropria.Checked;
            Controls.AddRange(new Control[] { corTipo, corPropria, cor });
            y += 34;

            // quando
            Rotulo(this, "Dia, das ... às ...");
            DataEscura data = new DataEscura { Width = 130, Location = new Point(X, y - 2), TextoVazio = "Sem dia" };
            DateTime dt;
            if (!string.IsNullOrEmpty(t.Data) && DateTime.TryParse(t.Data, out dt)) { data.Value = dt; data.Checked = true; }
            else { data.Value = dia; data.Checked = false; }
            ComboBox hora = new ComboBox { Width = 100, Location = new Point(X + 145, y), DropDownStyle = ComboBoxStyle.DropDown };
            hora.Items.Add("");
            for (int m = d.Config.HoraInicio * 60; m <= d.Config.HoraFim * 60; m += Math.Max(5, d.Config.Intervalo)) hora.Items.Add((m / 60).ToString("00") + ":" + (m % 60).ToString("00"));
            hora.Text = t.Hora ?? "";
            Label lAteH = new Label { Text = "às", AutoSize = true, Location = new Point(X + 253, y + 4), ForeColor = Tema.Fraco };
            ComboBox horaFim = new ComboBox { Width = 100, Location = new Point(X + 278, y), DropDownStyle = ComboBoxStyle.DropDown };
            horaFim.Items.Add("");
            for (int m = d.Config.HoraInicio * 60; m <= d.Config.HoraFim * 60; m += Math.Max(5, d.Config.Intervalo)) horaFim.Items.Add((m / 60).ToString("00") + ":" + (m % 60).ToString("00"));
            horaFim.Text = HoraFinal(t);
            Controls.AddRange(new Control[] { data, hora, lAteH, horaFim });
            y += 36;

            // repetição
            Rotulo(this, "Repetir");
            string[] codigos = { "", "diaria", "uteis", "semanal", "mensal", "cada" };
            string[] nomes = { "Não repete", "Todo dia", "Dias úteis", "Toda semana", "Todo mês", "A cada" };
            List<RadioButton> reps = new List<RadioButton>();
            // grupo próprio: sem isso, marcar a cor desmarcava a repetição (mesmo grupo de opções)
            Panel grupoRep = new Panel { Location = new Point(X, y), Size = new Size(Largura - 2 * X, 26) };
            int rx = X;
            for (int i = 0; i < codigos.Length; i++)
            {
                RadioButton r = new RadioButton { Text = nomes[i], AutoSize = true, Location = new Point(rx - X, 0), Tag = codigos[i], Checked = (t.Repetir ?? "") == codigos[i] };
                reps.Add(r); grupoRep.Controls.Add(r); rx += i == 0 ? 100 : 92;
            }
            Controls.Add(grupoRep);
            y += 28;

            // "A cada N dias/semanas/meses"
            NumericUpDown cada = new NumericUpDown { Width = 60, Location = new Point(X, y), Minimum = 1, Maximum = 365, Value = Math.Max(1, Math.Min(365, t.Cada())) };
            ComboBox cadaUn = new ComboBox { Width = 110, Location = new Point(X + 70, y), DropDownStyle = ComboBoxStyle.DropDownList };
            cadaUn.Items.AddRange(new object[] { "dias", "semanas", "meses" });
            cadaUn.SelectedIndex = (t.Repetir ?? "") == "cada-semana" ? 1 : (t.Repetir ?? "") == "cada-mes" ? 2 : 0;
            Controls.Add(cada); Controls.Add(cadaUn);
            RadioButton repCada = reps[reps.Count - 1];
            repCada.Checked = (t.Repetir ?? "").StartsWith("cada-");
            Action mostrarCada = () => { cada.Visible = cadaUn.Visible = repCada.Checked; };
            foreach (RadioButton r in reps) r.CheckedChanged += (s, e) => mostrarCada();
            mostrarCada();
            y += 32;
            Label lAte = new Label { Text = "Repetir até", AutoSize = true, Location = new Point(X, y), ForeColor = Tema.Fraco };
            DataEscura ate = new DataEscura { Width = 130, Location = new Point(X + 100, y - 5), TextoVazio = "Sem fim" };
            if (!string.IsNullOrEmpty(t.RepetirAte) && DateTime.TryParse(t.RepetirAte, out dt)) { ate.Value = dt; ate.Checked = true; } else { ate.Value = dia.AddMonths(1); ate.Checked = false; }
            Controls.Add(lAte); Controls.Add(ate);
            Action mostrarAte = () => { bool rep = !reps[0].Checked; lAte.Visible = ate.Visible = rep; };
            foreach (RadioButton r in reps) r.CheckedChanged += (s, e) => mostrarAte();
            mostrarAte();
            y += 34;

            // situação
            CheckBox feita = new CheckBox { AutoSize = true, Location = new Point(X, y), Checked = t.ConcluidaEm(dia),
                Text = t.Repete() ? "Concluída em " + dia.ToString("dd/MM") + " (as outras repetições continuam)" : "Concluída" };
            Controls.Add(feita); y += 28;
            CheckBox aguardo = new CheckBox { Text = "Em aguardo (depende de terceiros)", AutoSize = true, Location = new Point(X, y), Checked = t.Aguardando };
            Controls.Add(aguardo); y += 28;
            Label lDe = Rotulo(this, "Aguardando quem ou o quê");
            TextBox de = Campo(this, null, new TextBox { Text = t.AguardandoDe ?? "" }, 0);
            lDe.Visible = de.Visible = aguardo.Checked;
            aguardo.CheckedChanged += (s, e) => { lDe.Visible = de.Visible = aguardo.Checked; };

            TextBox link = Campo(this, "Link", new TextBox { Text = t.Link ?? "" }, 0);
            TextBox desc = Campo(this, "Descrição", new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Text = (t.Descricao ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n") }, 130);
            if (!string.IsNullOrEmpty(t.Origem) && t.Origem != "Manual")
            {
                Label o = new Label { Text = "Origem: " + t.Origem + (string.IsNullOrEmpty(t.Ref) ? "" : " (" + t.Ref + ")"), AutoSize = true, Location = new Point(X, y), ForeColor = Tema.Fraco };
                Controls.Add(o); y += 24;
            }

            Button ok = Tema.Botao("Salvar", true);
            ok.Click += (s, e) =>
            {
                if (titulo.Text.Trim() == "") { MessageBox.Show(this, "Dê um título para a tarefa."); titulo.Focus(); return; }
                string h = hora.Text.Trim();
                DateTime th = DateTime.MinValue;
                if (h != "" && !DateTime.TryParse("2000-01-01 " + h, out th)) { MessageBox.Show(this, "Horário inválido. Use o formato 09:30."); hora.Focus(); return; }
                if (h != "") h = th.ToString("HH:mm");
                RadioButton rep = reps.FirstOrDefault(r => r.Checked);
                string repetir = rep == null ? "" : (string)rep.Tag;   // nenhuma marcada = não repete
                if (repetir == "cada") repetir = cadaUn.SelectedIndex == 1 ? "cada-semana" : cadaUn.SelectedIndex == 2 ? "cada-mes" : "cada-dia";

                int minutos = 0;
                string hf = horaFim.Text.Trim();
                if (hf != "")
                {
                    DateTime tf;
                    if (!DateTime.TryParse("2000-01-01 " + hf, out tf)) { MessageBox.Show(this, "Hora final inválida. Use o formato 10:30."); horaFim.Focus(); return; }
                    if (h == "") { MessageBox.Show(this, "Para ter hora final, a tarefa precisa de hora inicial."); hora.Focus(); return; }
                    minutos = (int)(tf - th).TotalMinutes;
                    if (minutos <= 0) { MessageBox.Show(this, "A hora final precisa ser depois da inicial."); horaFim.Focus(); return; }
                }
                if (repetir != "" && h == "") { MessageBox.Show(this, "Para repetir, a tarefa precisa de um horário."); hora.Focus(); return; }

                t.Titulo = titulo.Text.Trim();
                t.TipoId = d.Tipos[tipo.SelectedIndex].Id;
                t.Cor = corPropria.Checked ? Cores.Hex(cor.BackColor) : "";
                if (h != "" && !data.Checked) data.Checked = true;
                string novaData = data.Checked ? Tarefa.Dia(data.Value) : "";
                if (novaData != t.Data || h != t.Hora) { t.Avisado = false; t.AvisadoEm = null; }
                t.Data = novaData; t.Hora = h; t.Duracao = minutos;
                t.Repetir = repetir; t.RepetirCada = repetir.StartsWith("cada-") ? (int)cada.Value : 0;
                t.RepetirAte = repetir != "" && ate.Checked ? Tarefa.Dia(ate.Value) : "";
                t.MarcarConcluida(dia, feita.Checked);
                t.Aguardando = aguardo.Checked; t.AguardandoDe = aguardo.Checked ? de.Text.Trim() : "";
                if (t.Aguardando && h != "") { t.Aguardando = false; }  // planejou um horário: sai do aguardo
                t.Link = link.Text.Trim(); t.Descricao = desc.Text;
                DialogResult = DialogResult.OK;
            };
            AcceptButton = null;
            Rodape(ok);
            Shown += (s, e) => titulo.Focus();
        }

        /// A hora em que a tarefa termina (início + duração), ou vazio se não tem.
        static string HoraFinal(Tarefa t)
        {
            DateTime ini;
            if (t.Duracao <= 0 || string.IsNullOrEmpty(t.Hora) || !DateTime.TryParse("2000-01-01 " + t.Hora, out ini)) return "";
            return ini.AddMinutes(t.Duracao).ToString("HH:mm");
        }
    }

    // ------------------------------------------------------------------ tipos
    public class DialogoTipos : DialogoBase
    {
        public DialogoTipos(Dados d) : base("Tipos de tarefa", 480)
        {
            Rotulo(this, "Tipos cadastrados (dois cliques para editar)");
            ListBox lb = Campo(this, null, new ListBox { DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 30, IntegralHeight = false }, 200);
            lb.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                Tipo t = d.Tipos[e.Index];
                bool sel = (e.State & DrawItemState.Selected) != 0;
                e.Graphics.FillRectangle(new SolidBrush(sel ? Tema.Linha : Tema.Painel), e.Bounds);
                using (SolidBrush b = new SolidBrush(Cores.De(t.Cor))) e.Graphics.FillRectangle(b, e.Bounds.X + 6, e.Bounds.Y + 7, 16, 16);
                TextRenderer.DrawText(e.Graphics, t.Nome + (t.Avisar ? "   (avisa no horário)" : ""), Tema.Fonte, new Point(e.Bounds.X + 30, e.Bounds.Y + 6), Tema.Texto);
            };
            Action recarregar = () => { int i = lb.SelectedIndex; lb.Items.Clear(); foreach (Tipo t in d.Tipos) lb.Items.Add(t.Nome); if (i >= 0 && i < lb.Items.Count) lb.SelectedIndex = i; };
            recarregar();

            Button novo = Tema.Botao("+ Novo tipo", true), editar = Tema.Botao("Editar", false), excluir = Tema.Botao("Excluir", false);
            novo.Location = new Point(X, y); editar.Location = new Point(X + 110, y); excluir.Location = new Point(X + 180, y);
            Controls.AddRange(new Control[] { novo, editar, excluir });
            y += 46;
            novo.Click += (s, e) =>
            {
                Tipo t = new Tipo { Id = Dados.NovoId(), Nome = "", Cor = "#64748B" };
                if (EditarTipo(t)) { d.Tipos.Add(t); recarregar(); lb.SelectedIndex = lb.Items.Count - 1; }
            };
            Action edt = () => { if (lb.SelectedIndex >= 0 && EditarTipo(d.Tipos[lb.SelectedIndex])) recarregar(); };
            editar.Click += (s, e) => edt();
            lb.DoubleClick += (s, e) => edt();
            excluir.Click += (s, e) =>
            {
                if (lb.SelectedIndex < 0) return;
                if (d.Tipos.Count == 1) { MessageBox.Show(this, "Precisa existir pelo menos um tipo."); return; }
                Tipo t = d.Tipos[lb.SelectedIndex];
                int usadas = d.Tarefas.Count(x => x.TipoId == t.Id);
                Tipo destino = d.Tipos.First(x => x != t);
                if (MessageBox.Show(this, "Excluir o tipo \"" + t.Nome + "\"?" + (usadas > 0 ? "\n\n" + usadas + " tarefa(s) passam para \"" + destino.Nome + "\"." : ""), "Excluir tipo", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                foreach (Tarefa x in d.Tarefas.Where(x => x.TipoId == t.Id)) x.TipoId = destino.Id;
                d.Tipos.Remove(t); recarregar();
            };

            // tipo usado ao importar
            Label sub = new Label { Text = "Tipo usado ao importar", Font = Tema.Negrito, Location = new Point(X, y), AutoSize = true };
            Controls.Add(sub); y += 28;
            ComboBox cg = ComboTipos(d, "Google Agenda", d.Config.TipoGoogle);
            ComboBox ct = ComboTipos(d, "TomTicket", d.Config.TipoTomTicket);
            ComboBox cm = ComboTipos(d, "Markedesk", d.Config.TipoMarkedesk);

            Button ok = Tema.Botao("Fechar", true);
            ok.Click += (s, e) =>
            {
                if (cg.SelectedIndex >= 0) d.Config.TipoGoogle = d.Tipos[cg.SelectedIndex].Id;
                if (ct.SelectedIndex >= 0) d.Config.TipoTomTicket = d.Tipos[ct.SelectedIndex].Id;
                if (cm.SelectedIndex >= 0) d.Config.TipoMarkedesk = d.Tipos[cm.SelectedIndex].Id;
                DialogResult = DialogResult.OK;
            };
            lb.Click += (s, e) => { };
            // atualiza as combos se mudar a lista
            lb.SelectedIndexChanged += (s, e) => { };
            Rodape(ok);
            Controls.Remove((Control)CancelButton); CancelButton = ok;
        }

        ComboBox ComboTipos(Dados d, string rotulo, string atual)
        {
            Label l = new Label { Text = rotulo, Location = new Point(X, y + 4), Width = 130, ForeColor = Tema.Fraco };
            ComboBox c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(X + 140, y), Width = Largura - 2 * X - 140 };
            c.DropDown += (s, e) => { int i = c.SelectedIndex; c.Items.Clear(); foreach (Tipo t in d.Tipos) c.Items.Add(t.Nome); c.SelectedIndex = Math.Min(i, c.Items.Count - 1); };
            foreach (Tipo t in d.Tipos) c.Items.Add(t.Nome);
            c.SelectedIndex = Math.Max(0, d.Tipos.FindIndex(x => x.Id == atual));
            Controls.Add(l); Controls.Add(c); y += 34;
            return c;
        }

        bool EditarTipo(Tipo t)
        {
            using (DialogoBase f = new DialogoBase(string.IsNullOrEmpty(t.Nome) ? "Novo tipo" : "Editar tipo", 380))
            {
                int yy = 14;
                Label l1 = new Label { Text = "Nome", Location = new Point(16, yy), AutoSize = true, ForeColor = Tema.Fraco }; yy += 20;
                TextBox nome = new TextBox { Text = t.Nome, Location = new Point(16, yy), Width = 340 }; yy += 36;
                Label l2 = new Label { Text = "Cor", Location = new Point(16, yy), AutoSize = true, ForeColor = Tema.Fraco }; yy += 20;
                Button cor = BotaoCor(Cores.De(t.Cor)); cor.Location = new Point(16, yy); yy += 40;
                CheckBox avisar = new CheckBox { Text = "Avisar quando chegar o horário", Checked = t.Avisar, AutoSize = true, Location = new Point(16, yy) }; yy += 40;
                Button ok = Tema.Botao("Salvar", true); ok.Location = new Point(16, yy);
                Button can = Tema.Botao("Cancelar", false); can.Location = new Point(110, yy); can.DialogResult = DialogResult.Cancel;
                ok.Click += (s, e) =>
                {
                    if (nome.Text.Trim() == "") { MessageBox.Show(f, "Dê um nome para o tipo."); return; }
                    t.Nome = nome.Text.Trim(); t.Cor = Cores.Hex(cor.BackColor); t.Avisar = avisar.Checked;
                    f.DialogResult = DialogResult.OK;
                };
                f.Controls.AddRange(new Control[] { l1, nome, l2, cor, avisar, ok, can });
                f.CancelButton = can; f.AcceptButton = ok;
                f.ClientSize = new Size(380, yy + 50);
                Tema.Aplicar(f); cor.BackColor = Cores.De(t.Cor);
                return f.ShowDialog(this) == DialogResult.OK;
            }
        }
    }

    // ------------------------------------------------------------------ lixeira
    public class DialogoLixeira : DialogoBase
    {
        public DialogoLixeira(Dados d) : base("Lixeira", 520)
        {
            Rotulo(this, "Tarefas removidas (ficam aqui por 30 dias)");
            ListBox lb = Campo(this, null, new ListBox { DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 44, IntegralHeight = false, SelectionMode = SelectionMode.MultiExtended }, 320);
            Func<List<Tarefa>> itens = () => d.Tarefas.Where(t => !t.Ativa()).OrderByDescending(t => t.NaLixeira).ToList();
            lb.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                List<Tarefa> l = itens();
                if (e.Index >= l.Count) return;
                Tarefa t = l[e.Index];
                bool sel = (e.State & DrawItemState.Selected) != 0;
                using (SolidBrush b = new SolidBrush(sel ? Tema.Linha : Tema.Painel)) e.Graphics.FillRectangle(b, e.Bounds);
                using (SolidBrush b = new SolidBrush(d.CorDe(t))) e.Graphics.FillRectangle(b, e.Bounds.X + 4, e.Bounds.Y + 8, 4, e.Bounds.Height - 16);
                TextRenderer.DrawText(e.Graphics, t.Titulo, Tema.Negrito, new Rectangle(e.Bounds.X + 16, e.Bounds.Y + 4, e.Bounds.Width - 20, 20), Tema.Texto, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                DateTime q;
                string quando = DateTime.TryParse(t.NaLixeira, null, System.Globalization.DateTimeStyles.RoundtripKind, out q) ? q.ToLocalTime().ToString("dd/MM HH:mm") : "";
                TextRenderer.DrawText(e.Graphics, (t.Origem == "Manual" || string.IsNullOrEmpty(t.Origem) ? "Criada por você" : t.Origem) + "  ·  removida em " + quando,
                    Tema.Pequena, new Rectangle(e.Bounds.X + 16, e.Bounds.Y + 23, e.Bounds.Width - 20, 16), Tema.Fraco, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
            Label vazio = new Label { Text = "A Lixeira está vazia.", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Bounds = lb.Bounds, ForeColor = Tema.Fraco, BackColor = Tema.Painel };
            Controls.Add(vazio); vazio.BringToFront();
            Action recarregar = () =>
            {
                lb.BeginUpdate(); lb.Items.Clear();
                foreach (Tarefa t in itens()) lb.Items.Add(t.Id);
                lb.EndUpdate();
                vazio.Visible = lb.Items.Count == 0;
            };

            Button restaurar = Tema.Botao("Restaurar", true), apagar = Tema.Botao("Excluir de vez", false), esvaziar = Tema.Botao("Esvaziar Lixeira", false);
            restaurar.Location = new Point(X, y); apagar.Location = new Point(X + 104, y); esvaziar.Location = new Point(X + 232, y);
            Controls.AddRange(new Control[] { restaurar, apagar, esvaziar });
            y += 46;
            Func<List<Tarefa>> selecionadas = () => { List<Tarefa> l = itens(); return lb.SelectedIndices.Cast<int>().Where(i => i < l.Count).Select(i => l[i]).ToList(); };
            restaurar.Click += (s, e) =>
            {
                foreach (Tarefa t in selecionadas())
                {
                    t.NaLixeira = null;
                    if (!string.IsNullOrEmpty(t.Ref)) d.NaoSaoMinhas.Remove(t.Origem + ":" + t.Ref);   // volta a ser "minha"
                }
                recarregar();
            };
            apagar.Click += (s, e) =>
            {
                List<Tarefa> sel = selecionadas();
                if (sel.Count == 0) return;
                if (MessageBox.Show(this, "Excluir de vez " + sel.Count + " tarefa(s)? Não tem volta.", "Lixeira", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                foreach (Tarefa t in sel) d.Tarefas.Remove(t);
                recarregar();
            };
            esvaziar.Click += (s, e) =>
            {
                int n = d.Tarefas.Count(t => !t.Ativa());
                if (n == 0) return;
                if (MessageBox.Show(this, "Excluir de vez as " + n + " tarefa(s) da Lixeira? Não tem volta.", "Lixeira", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                d.Tarefas.RemoveAll(t => !t.Ativa());
                recarregar();
            };
            lb.DoubleClick += (s, e) => restaurar.PerformClick();
            recarregar();

            Button ok = Tema.Botao("Fechar", true);
            ok.Click += (s, e) => DialogResult = DialogResult.OK;
            Rodape(ok);
            Controls.Remove((Control)CancelButton); CancelButton = ok;
            vazio.BackColor = Tema.Painel;
        }
    }

    // ------------------------------------------------------------------ eventos do dia
    public class DialogoEventos : DialogoBase
    {
        Dados dados;

        public DialogoEventos(Dados d, DateTime dia) : base("Eventos (férias, atestado, feriado...)", 460)
        {
            dados = d; d.AjeitarTiposEvento();
            Rotulo(this, "Eventos cadastrados (dois cliques para editar)");
            ListBox lb = Campo(this, null, new ListBox { DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 40, IntegralHeight = false }, 260);
            Func<List<EventoDia>> ordenados = () => d.Eventos.OrderByDescending(x => x.Inicio).ToList();
            lb.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                EventoDia ev = ordenados()[e.Index];
                bool sel = (e.State & DrawItemState.Selected) != 0;
                using (SolidBrush b = new SolidBrush(sel ? Tema.Linha : Tema.Painel)) e.Graphics.FillRectangle(b, e.Bounds);
                using (SolidBrush b = new SolidBrush(Cores.De(ev.Cor))) e.Graphics.FillRectangle(b, e.Bounds.X + 6, e.Bounds.Y + 8, 6, e.Bounds.Height - 16);
                TextRenderer.DrawText(e.Graphics, ev.Titulo(), Tema.Negrito, new Point(e.Bounds.X + 20, e.Bounds.Y + 3), Tema.Texto);
                string det = Periodo(ev) + "  ·  " + dados.TipoEventoDe(ev.Tipo).Nome + (ev.DiaInteiro() ? "" : "  ·  " + ev.Faixa());
                TextRenderer.DrawText(e.Graphics, det, Tema.Pequena, new Point(e.Bounds.X + 20, e.Bounds.Y + 21), Tema.Fraco);
            };
            Action recarregar = () => { lb.Items.Clear(); foreach (EventoDia ev in ordenados()) lb.Items.Add(ev.Id); };
            recarregar();

            Button novo = Tema.Botao("+ Novo evento", true), editar = Tema.Botao("Editar", false), excluir = Tema.Botao("Excluir", false);
            novo.Location = new Point(X, y); editar.Location = new Point(X + 124, y); excluir.Location = new Point(X + 196, y);
            Controls.AddRange(new Control[] { novo, editar, excluir });
            y += 46;
            novo.Click += (s, e) =>
            {
                TipoEvento t0 = d.TiposEvento.FirstOrDefault() ?? new TipoEvento { Id = "outro", Nome = "Evento", Cor = "#64748B" };
                EventoDia ev = new EventoDia { Id = Dados.NovoId(), Tipo = t0.Id, Cor = t0.Cor, Inicio = Tarefa.Dia(dia), Fim = Tarefa.Dia(dia) };
                if (EditarEvento(ev, true)) { d.Eventos.Add(ev); recarregar(); }
            };
            Action edt = () => { if (lb.SelectedIndex >= 0 && EditarEvento(ordenados()[lb.SelectedIndex], false)) recarregar(); };
            editar.Click += (s, e) => edt();
            lb.DoubleClick += (s, e) => edt();
            excluir.Click += (s, e) =>
            {
                if (lb.SelectedIndex < 0) return;
                EventoDia ev = ordenados()[lb.SelectedIndex];
                if (MessageBox.Show(this, "Excluir \"" + ev.Titulo() + "\" (" + Periodo(ev) + ")?", "Excluir evento", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                d.Eventos.Remove(ev); recarregar();
            };

            Button ok = Tema.Botao("Fechar", true);
            ok.Click += (s, e) => DialogResult = DialogResult.OK;
            Rodape(ok);
            Controls.Remove((Control)CancelButton); CancelButton = ok;
        }

        public static string Periodo(EventoDia ev)
        {
            DateTime a, b;
            if (!DateTime.TryParse(ev.Inicio, out a)) return "";
            if (string.IsNullOrEmpty(ev.Fim) || ev.Fim == ev.Inicio || !DateTime.TryParse(ev.Fim, out b)) return a.ToString("dd/MM/yyyy");
            return a.ToString("dd/MM") + " a " + b.ToString("dd/MM/yyyy");
        }

        bool EditarEvento(EventoDia ev, bool novo)
        {
            using (DialogoBase f = new DialogoBase(novo ? "Novo evento" : "Editar evento", 420))
            {
                int yy = 14;
                Label l1 = new Label { Text = "Tipo", Location = new Point(16, yy), AutoSize = true, ForeColor = Tema.Fraco }; yy += 22;
                ComboBox tipo = new ComboBox { Location = new Point(16, yy), Width = 380, DropDownStyle = ComboBoxStyle.DropDownList };
                foreach (TipoEvento t in dados.TiposEvento) tipo.Items.Add(t.Nome);
                int iSel = dados.TiposEvento.FindIndex(t => t.Id == ev.Tipo);
                tipo.SelectedIndex = iSel >= 0 ? iSel : 0;
                yy += 38;
                Label l2 = new Label { Text = "Nome (opcional)", Location = new Point(16, yy), AutoSize = true, ForeColor = Tema.Fraco }; yy += 20;
                TextBox nome = new TextBox { Text = ev.Nome ?? "", Location = new Point(16, yy), Width = 380 }; yy += 38;
                Label l3 = new Label { Text = "De", Location = new Point(16, yy + 6), AutoSize = true, ForeColor = Tema.Fraco };
                DataEscura de = new DataEscura { Location = new Point(46, yy), Width = 140, TextoVazio = "Escolha" };
                Label l4 = new Label { Text = "até", Location = new Point(196, yy + 6), AutoSize = true, ForeColor = Tema.Fraco };
                DataEscura ate = new DataEscura { Location = new Point(228, yy), Width = 140, TextoVazio = "Só um dia" };
                DateTime dt;
                if (DateTime.TryParse(ev.Inicio, out dt)) de.Value = dt;
                if (!string.IsNullOrEmpty(ev.Fim) && ev.Fim != ev.Inicio && DateTime.TryParse(ev.Fim, out dt)) ate.Value = dt; else { ate.Value = de.Value; ate.Checked = false; }
                yy += 44;

                // o evento pode valer o dia inteiro ou só um pedaço dele
                CheckBox comHora = new CheckBox { Text = "Só em parte do dia", AutoSize = true, Location = new Point(16, yy), Checked = !ev.DiaInteiro() };
                yy += 26;
                Label l6 = new Label { Text = "Das", Location = new Point(36, yy + 4), AutoSize = true, ForeColor = Tema.Fraco };
                MaskedTextBox hIni = new MaskedTextBox { Mask = "00:00", Text = string.IsNullOrEmpty(ev.HoraInicio) ? "09:00" : ev.HoraInicio, Width = 70, Location = new Point(76, yy) };
                Label l7 = new Label { Text = "às", Location = new Point(156, yy + 4), AutoSize = true, ForeColor = Tema.Fraco };
                MaskedTextBox hFim = new MaskedTextBox { Mask = "00:00", Text = string.IsNullOrEmpty(ev.HoraFim) ? "12:00" : ev.HoraFim, Width = 70, Location = new Point(182, yy) };
                Action verHoras = () => { l6.Visible = hIni.Visible = l7.Visible = hFim.Visible = comHora.Checked; };
                comHora.CheckedChanged += (s, e) => verHoras();
                yy += 40;

                Label l5 = new Label { Text = "Cor", Location = new Point(16, yy + 6), AutoSize = true, ForeColor = Tema.Fraco };
                Button cor = BotaoCor(Cores.De(ev.Cor)); cor.Location = new Point(46, yy);
                tipo.SelectedIndexChanged += (s, e) =>
                {
                    if (tipo.SelectedIndex >= 0) cor.BackColor = Cores.De(dados.TiposEvento[tipo.SelectedIndex].Cor);
                };
                yy += 46;
                Button ok = Tema.Botao("Salvar", true); ok.Location = new Point(16, yy);
                Button can = Tema.Botao("Cancelar", false); can.Location = new Point(110, yy); can.DialogResult = DialogResult.Cancel;
                ok.Click += (s, e) =>
                {
                    if (tipo.SelectedIndex < 0) { MessageBox.Show(f, "Escolha o tipo."); return; }
                    if (!de.Checked) { MessageBox.Show(f, "Escolha o dia de início."); return; }
                    DateTime fimDia = ate.Checked ? ate.Value : de.Value;
                    if (fimDia < de.Value) { MessageBox.Show(f, "O fim precisa ser depois do início."); return; }
                    if (comHora.Checked)
                    {
                        int mi = Intervalo.Minutos(hIni.Text), mf = Intervalo.Minutos(hFim.Text);
                        if (mi < 0 || mf < 0) { MessageBox.Show(f, "Horário inválido. Use o formato 09:00."); hIni.Focus(); return; }
                        if (mf <= mi) { MessageBox.Show(f, "A hora final precisa ser depois da inicial."); hFim.Focus(); return; }
                        ev.HoraInicio = hIni.Text; ev.HoraFim = hFim.Text;
                    }
                    else { ev.HoraInicio = ""; ev.HoraFim = ""; }
                    ev.Tipo = dados.TiposEvento[tipo.SelectedIndex].Id;
                    ev.Nome = nome.Text.Trim(); ev.Cor = Cores.Hex(cor.BackColor);
                    ev.Inicio = Tarefa.Dia(de.Value); ev.Fim = Tarefa.Dia(fimDia);
                    ev.Mod = Sincronia.Agora();
                    f.DialogResult = DialogResult.OK;
                };
                f.Controls.AddRange(new Control[] { l1, tipo, l2, nome, l3, de, l4, ate, comHora, l6, hIni, l7, hFim, l5, cor, ok, can });
                f.CancelButton = can;
                f.ClientSize = new Size(420, yy + 50);
                Tema.Aplicar(f); cor.BackColor = Cores.De(ev.Cor); verHoras();
                return f.ShowDialog(this) == DialogResult.OK;
            }
        }
    }

    // ------------------------------------------------------------------ configurações
    public class DialogoConfig : DialogoBase
    {
        public DialogoConfig(Dados d) : base("Configurações", 440)
        {
            Config c = d.Config;
            Rotulo(this, "Horário da agenda (início e fim)");
            NumericUpDown ini = new NumericUpDown { Minimum = 0, Maximum = 23, Value = c.HoraInicio, Width = 70, Location = new Point(X, y) };
            Label ate = new Label { Text = "até", AutoSize = true, Location = new Point(X + 80, y + 3) };
            NumericUpDown fim = new NumericUpDown { Minimum = 1, Maximum = 23, Value = c.HoraFim, Width = 70, Location = new Point(X + 110, y) };
            Controls.AddRange(new Control[] { ini, ate, fim }); y += 38;

            Rotulo(this, "Tamanho de cada horário");
            RadioButton r15 = new RadioButton { Text = "15 min", AutoSize = true, Location = new Point(X, y), Checked = c.Intervalo == 15 };
            RadioButton r30 = new RadioButton { Text = "30 min", AutoSize = true, Location = new Point(X + 90, y), Checked = c.Intervalo == 30 };
            RadioButton r60 = new RadioButton { Text = "1 hora", AutoSize = true, Location = new Point(X + 180, y), Checked = c.Intervalo == 60 };
            if (!r15.Checked && !r60.Checked) r30.Checked = true;
            Controls.AddRange(new Control[] { r15, r30, r60 }); y += 38;

            CheckBox avisar = new CheckBox { Text = "Avisar no horário das tarefas (tipos marcados para avisar)", AutoSize = true, Location = new Point(X, y), Checked = c.AvisarLembretes };
            Controls.Add(avisar); y += 28;
            Label lAntes = new Label { Text = "Minutos de antecedência", AutoSize = true, Location = new Point(X + 20, y + 3), ForeColor = Tema.Fraco };
            NumericUpDown antes = new NumericUpDown { Minimum = 0, Maximum = 120, Value = c.AvisoAntesMin, Width = 70, Location = new Point(X + 200, y) };
            Controls.Add(lAntes); Controls.Add(antes); y += 34;
            RadioButton avJanela = new RadioButton { Text = "Janela por cima de tudo", AutoSize = true, Location = new Point(X + 20, y), Checked = c.AvisoJanela };
            RadioButton avBalao = new RadioButton { Text = "Balão do Windows", AutoSize = true, Location = new Point(X + 210, y), Checked = !c.AvisoJanela };
            Controls.Add(avJanela); Controls.Add(avBalao); y += 28;
            CheckBox som = new CheckBox { Text = "Tocar som", AutoSize = true, Location = new Point(X + 20, y), Checked = c.AvisoSom };
            Controls.Add(som); y += 28;
            CheckBox todas = new CheckBox { Text = "Avisar em todas as tarefas com horário", AutoSize = true, Location = new Point(X + 20, y), Checked = c.AvisarTodas };
            new ToolTip().SetToolTip(todas, "Desmarcado: avisa só nos tipos marcados para avisar (em Tipos)");
            Controls.Add(todas); y += 36;
            Action mostrarAviso = () => { lAntes.Visible = antes.Visible = avJanela.Visible = avBalao.Visible = som.Visible = todas.Visible = avisar.Checked; };
            mostrarAviso();
            avisar.CheckedChanged += (s, e) => mostrarAviso();

            CheckBox fimSemana = new CheckBox { Text = "Destacar sábado e domingo na agenda", AutoSize = true, Location = new Point(X, y), Checked = c.DestacarFimDeSemana };
            Controls.Add(fimSemana); y += 30;
            // os intervalos fora da agenda ficam numa tela propria (o almoco e um deles)
            Button intervalos = Tema.Botao("Intervalos fora da agenda...", false);
            intervalos.AutoSize = false; intervalos.Width = 240; intervalos.Height = 30; intervalos.Location = new Point(X, y);
            intervalos.Click += (s, e) => { using (DialogoIntervalos g = new DialogoIntervalos(d)) g.ShowDialog(this); };
            new ToolTip().SetToolTip(intervalos, "Almoco, cafe, reuniao fixa: horarios que a agenda nao mostra");
            Controls.Add(intervalos); y += 38;

            Button tiposEv = Tema.Botao("Tipos de evento...", false);
            tiposEv.AutoSize = false; tiposEv.Width = 240; tiposEv.Height = 30; tiposEv.Location = new Point(X, y);
            tiposEv.Click += (s, e) => { using (DialogoTiposEvento g = new DialogoTiposEvento(d)) g.ShowDialog(this); };
            new ToolTip().SetToolTip(tiposEv, "Aniversario, ferias, feriado: os tipos usados ao criar um evento");
            Controls.Add(tiposEv); y += 38;

            CheckBox verAguardo = new CheckBox { Text = "Mostrar a secao \"Em aguardo\"", AutoSize = true, Location = new Point(X, y), Checked = c.MostrarAguardando };
            new ToolTip().SetToolTip(verAguardo, "Desmarcado: a coluna fica so com as tarefas fixas");
            Controls.Add(verAguardo); y += 32;

            CheckBox aoAbrir = new CheckBox { Text = "Atualizar integrações ao abrir", AutoSize = true, Location = new Point(X, y), Checked = c.AtualizarAoAbrir };
            Controls.Add(aoAbrir); y += 28;
            CheckBox auto = new CheckBox { Text = "Atualizar integrações sozinha", AutoSize = true, Location = new Point(X, y), Checked = c.AtualizarMin > 0 };
            Controls.Add(auto); y += 28;
            Label lCada = new Label { Text = "A cada (minutos)", AutoSize = true, Location = new Point(X + 20, y + 3), ForeColor = Tema.Fraco };
            NumericUpDown cada = new NumericUpDown { Minimum = 5, Maximum = 240, Value = Math.Max(5, c.AtualizarMin == 0 ? 15 : c.AtualizarMin), Width = 70, Location = new Point(X + 200, y) };
            Controls.Add(lCada); Controls.Add(cada); y += 40;
            lCada.Visible = cada.Visible = auto.Checked;
            auto.CheckedChanged += (s, e) => lCada.Visible = cada.Visible = auto.Checked;

            Label pasta = new Label { Text = "Dados salvos em: " + Dados.Pasta, AutoSize = false, Width = Largura - 2 * X, Height = 34, Location = new Point(X, y), ForeColor = Tema.Fraco, Font = Tema.Pequena };
            Controls.Add(pasta); y += 40;

            Button ok = Tema.Botao("Salvar", true);
            ok.Click += (s, e) =>
            {
                if (fim.Value <= ini.Value) { MessageBox.Show(this, "O fim precisa ser depois do início."); return; }
                c.HoraInicio = (int)ini.Value; c.HoraFim = (int)fim.Value;
                c.Intervalo = r15.Checked ? 15 : r60.Checked ? 60 : 30;
                c.AvisarLembretes = avisar.Checked; c.AvisoAntesMin = (int)antes.Value; c.AvisoJanela = avJanela.Checked; c.AvisoSom = som.Checked; c.AvisarTodas = todas.Checked;
                c.DestacarFimDeSemana = fimSemana.Checked;
            c.MostrarAguardando = verAguardo.Checked;
            // mantem PausaAlmoco/AlmocoInicio/AlmocoFim em dia com o intervalo chamado almoco,
            // para o celular e as versoes antigas continuarem entendendo
            Intervalo alm = (d.Intervalos ?? new List<Intervalo>()).FirstOrDefault(x => x.Id == "almoco");
            if (alm != null) { c.PausaAlmoco = alm.Ligado; c.AlmocoInicio = alm.Inicio; c.AlmocoFim = alm.Fim; }
                c.AtualizarAoAbrir = aoAbrir.Checked; c.AtualizarMin = auto.Checked ? (int)cada.Value : 0;
                DialogResult = DialogResult.OK;
            };
            Rodape(ok);
        }
    }

    // ------------------------------------------------------------------ acessos
    public class DialogoAcessos : DialogoBase
    {
        Dados d;
        List<TextBox> senhas = new List<TextBox>();

        public DialogoAcessos(Dados dados) : base("Acessos", 620)
        {
            d = dados;
            Config c = d.Config;
            TabControl abas = new AbasEscuras { Location = new Point(X, 14), Size = new Size(Largura - 2 * X, 470) };
            Controls.Add(abas);

            // Google
            TabPage g = new TabPage("Google Agenda");
            y = 12;
            Label ajuda = new Label { Text = "No Google Cloud: ative a Google Calendar API e crie um ID do cliente OAuth do tipo \"App para computador\". Cole aqui o Client ID e o Client Secret e clique em Conectar.", Location = new Point(X, y), Size = new Size(abas.Width - 50, 52), ForeColor = Tema.Fraco };
            g.Controls.Add(ajuda); y += 58;
            TextBox gId = CampoAba(g, "Client ID", c.GoogleClientId, false);
            TextBox gSecret = CampoAba(g, "Client Secret", Cripto.Abrir(c.GoogleSecret), true);
            TextBox gAgenda = CampoAba(g, "ID da agenda (primary = agenda principal)", c.GoogleAgendaId, false);
            TextBox gLogin = CampoAba(g, "Login (e-mail)", c.GoogleLogin, false);
            TextBox gSenha = CampoAba(g, "Senha (só para consulta, não é usada na conexão)", Cripto.Abrir(c.GoogleSenha), true);
            Button gCon = Tema.Botao("Conectar conta Google", true); gCon.Location = new Point(X, y);
            Label gStatus = new Label { AutoSize = true, Location = new Point(X + 190, y + 6), ForeColor = Tema.Fraco, Text = string.IsNullOrEmpty(c.GoogleRefresh) ? "Não conectado" : "Conectado" };
            g.Controls.Add(gCon); g.Controls.Add(gStatus);
            gCon.Click += (s, e) =>
            {
                c.GoogleClientId = gId.Text.Trim(); c.GoogleSecret = Cripto.Fechar(gSecret.Text.Trim());
                gStatus.Text = "Aguardando o login no navegador..."; gStatus.Refresh();
                try { Integracoes.ConectarGoogle(c); gStatus.Text = "Conectado"; d.Salvar(); }
                catch (Exception ex) { gStatus.Text = "Falhou"; MessageBox.Show(this, ex.Message, "Google"); }
            };
            abas.TabPages.Add(g);

            // TomTicket
            TabPage tt = new TabPage("TomTicket");
            y = 12;
            TextBox tToken = CampoAba(tt, "Token da API (TomTicket > Configurações > API)", Cripto.Abrir(c.TomToken), true);
            TextBox tAtend = CampoAba(tt, "Só chamados do atendente (parte do nome, vazio = todos)", c.TomAtendente, false);
            Label ls = new Label { Text = "Situações importadas", Location = new Point(X, y), AutoSize = true, ForeColor = Tema.Fraco };
            tt.Controls.Add(ls); y += 22;
            string[] sits = { "0 Aguardando", "1 Em atendimento", "2 Aguardando cliente", "3 Aguardando terceiros", "6 Reaberto", "7 Pausado" };
            List<string> marcadas = (c.TomSituacoes ?? "").Split(',').Select(x => x.Trim()).ToList();
            List<CheckBox> cks = new List<CheckBox>();
            for (int i = 0; i < sits.Length; i++)
            {
                CheckBox ck = new CheckBox { Text = sits[i], AutoSize = true, Location = new Point(X + (i % 2) * 230, y + (i / 2) * 24), Checked = marcadas.Contains(sits[i].Split(' ')[0]), Tag = sits[i].Split(' ')[0] };
                cks.Add(ck); tt.Controls.Add(ck);
            }
            y += 80;
            TextBox tLink = CampoAba(tt, "Link para abrir o chamado (use {protocolo})", c.TomLink, false);
            TextBox tLogin = CampoAba(tt, "Login", c.TomLogin, false);
            TextBox tSenha = CampoAba(tt, "Senha (só para consulta)", Cripto.Abrir(c.TomSenha), true);
            abas.TabPages.Add(tt);

            // Markedesk
            TabPage md = new TabPage("Markedesk");
            y = 12;
            TextBox mApi = CampoAba(md, "URL da API (a do backend, começa com api.)", c.MdUrlApi, false);
            TextBox mTela = CampoAba(md, "URL da tela (para abrir o atendimento)", c.MdUrlTela, false);
            TextBox mEmail = CampoAba(md, "E-mail", c.MdEmail, false);
            TextBox mSenha = CampoAba(md, "Senha", Cripto.Abrir(c.MdSenha), true);
            string modo = string.IsNullOrEmpty(c.MdModo) ? "setor" : c.MdModo;
            RadioButton mSetor = new RadioButton { Text = "Do meu setor", AutoSize = true, Location = new Point(X, y), Checked = modo == "setor" };
            RadioButton mMeus = new RadioButton { Text = "Só os meus", AutoSize = true, Location = new Point(X + 150, y), Checked = modo == "meus" };
            RadioButton mTodos = new RadioButton { Text = "Todos", AutoSize = true, Location = new Point(X + 280, y), Checked = modo == "todos" };
            md.Controls.Add(mSetor); md.Controls.Add(mMeus); md.Controls.Add(mTodos); y += 34;
            Button mTestar = Tema.Botao("Testar conexão", false); mTestar.Location = new Point(X, y);
            Label mStatus = new Label { AutoSize = true, Location = new Point(X + 140, y + 6), ForeColor = Tema.Fraco };
            md.Controls.Add(mTestar); md.Controls.Add(mStatus);
            mTestar.Click += (s, e) =>
            {
                Config teste = new Config { MdUrlApi = mApi.Text.Trim(), MdEmail = mEmail.Text.Trim(), MdSenha = Cripto.Fechar(mSenha.Text) };
                mStatus.Text = "Testando..."; mStatus.Refresh();
                try { mStatus.Text = Integracoes.TestarMarkedesk(teste); } catch (Exception ex) { mStatus.Text = "Falhou"; MessageBox.Show(this, ex.Message, "Markedesk"); }
            };
            abas.TabPages.Add(md);

            // Outros acessos
            TabPage ou = new TabPage("Outros acessos");
            DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToDeleteRows = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            grid.Columns.Add("Ferramenta", "Ferramenta");
            grid.Columns.Add("Url", "Endereço");
            grid.Columns.Add("Login", "Login");
            grid.Columns.Add("Senha", "Senha");
            grid.Columns.Add("Obs", "Observação");
            foreach (Acesso a in d.Acessos) grid.Rows.Add(a.Ferramenta, a.Url, a.Login, Cripto.Abrir(a.Senha), a.Obs);
            bool mostrarGrid = false;
            grid.CellFormatting += (s, e) =>
            {
                if (!mostrarGrid && e.ColumnIndex == 3 && e.Value != null && !grid.IsCurrentCellInEditMode)
                { e.Value = new string('•', Math.Min(12, e.Value.ToString().Length)); e.FormattingApplied = true; }
            };
            ou.Controls.Add(grid);
            abas.TabPages.Add(ou);

            y = abas.Bottom + 10;
            CheckBox mostrar = new CheckBox { Text = "Mostrar senhas", AutoSize = true, Location = new Point(X, y) };
            mostrar.CheckedChanged += (s, e) =>
            {
                foreach (TextBox t in senhas) t.UseSystemPasswordChar = !mostrar.Checked;
                mostrarGrid = mostrar.Checked; grid.Invalidate();
            };
            Button importar = Tema.Botao("Puxar dos arquivos do Claude", false); importar.Location = new Point(X + 150, y - 4);
            importar.Visible = Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "secrets"));
            new ToolTip().SetToolTip(importar, "Lê o token do TomTicket e o login do Markedesk que o Claude Code já usa neste PC");
            importar.Click += (s, e) =>
            {
                string pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "secrets");
                Dictionary<string, string> v = LerEnv(Path.Combine(pasta, "tomticket.env"));
                foreach (KeyValuePair<string, string> kv in LerEnv(Path.Combine(pasta, "markedesk.env"))) v[kv.Key] = kv.Value;
                int n = 0;
                if (v.ContainsKey("TOMTICKET_TOKEN")) { tToken.Text = v["TOMTICKET_TOKEN"]; n++; }
                if (v.ContainsKey("MARKEDESK_URL")) { mApi.Text = v["MARKEDESK_URL"]; n++; }
                if (v.ContainsKey("MARKEDESK_EMAIL")) { mEmail.Text = v["MARKEDESK_EMAIL"]; n++; }
                if (v.ContainsKey("MARKEDESK_PASS")) { mSenha.Text = v["MARKEDESK_PASS"]; n++; }
                MessageBox.Show(this, n == 0 ? "Nada encontrado em " + pasta : n + " campo(s) preenchido(s). Confira e clique em Salvar.", "Acessos");
            };
            Controls.Add(mostrar); Controls.Add(importar);
            y += 34;

            Button ok = Tema.Botao("Salvar", true);
            ok.Click += (s, e) =>
            {
                c.GoogleClientId = gId.Text.Trim(); c.GoogleSecret = Cripto.Fechar(gSecret.Text.Trim());
                c.GoogleAgendaId = gAgenda.Text.Trim() == "" ? "primary" : gAgenda.Text.Trim();
                c.GoogleLogin = gLogin.Text.Trim(); c.GoogleSenha = Cripto.Fechar(gSenha.Text);
                c.TomToken = Cripto.Fechar(tToken.Text.Trim()); c.TomAtendente = tAtend.Text.Trim();
                c.TomSituacoes = string.Join(",", cks.Where(x => x.Checked).Select(x => (string)x.Tag));
                c.TomLink = tLink.Text.Trim(); c.TomLogin = tLogin.Text.Trim(); c.TomSenha = Cripto.Fechar(tSenha.Text);
                c.MdUrlApi = mApi.Text.Trim(); c.MdUrlTela = mTela.Text.Trim(); c.MdEmail = mEmail.Text.Trim();
                c.MdSenha = Cripto.Fechar(mSenha.Text); c.MdModo = mTodos.Checked ? "todos" : mMeus.Checked ? "meus" : "setor";
                grid.EndEdit();
                d.Acessos.Clear();
                foreach (DataGridViewRow r in grid.Rows)
                {
                    if (r.IsNewRow) continue;
                    Func<int, string> v = i => r.Cells[i].Value == null ? "" : r.Cells[i].Value.ToString();
                    if (v(0) == "" && v(2) == "" && v(3) == "") continue;
                    d.Acessos.Add(new Acesso { Ferramenta = v(0), Url = v(1), Login = v(2), Senha = Cripto.Fechar(v(3)), Obs = v(4) });
                }
                DialogResult = DialogResult.OK;
            };
            Rodape(ok);
            foreach (TabPage p in abas.TabPages) p.BackColor = Tema.Fundo;
        }

        TextBox CampoAba(TabPage p, string rotulo, string valor, bool senha)
        {
            Label l = new Label { Text = rotulo, Location = new Point(X, y), AutoSize = true, ForeColor = Tema.Fraco };
            TextBox t = new TextBox { Text = valor ?? "", Location = new Point(X, y + 20), Width = 540, UseSystemPasswordChar = senha };
            if (senha) senhas.Add(t);
            p.Controls.Add(l); p.Controls.Add(t);
            y += 54;
            return t;
        }

        static Dictionary<string, string> LerEnv(string arq)
        {
            Dictionary<string, string> r = new Dictionary<string, string>();
            if (!File.Exists(arq)) return r;
            foreach (string linha in File.ReadAllLines(arq))
            {
                string l = linha.Trim().TrimStart('﻿');
                int i = l.IndexOf('=');
                if (l.StartsWith("#") || i <= 0) continue;
                r[l.Substring(0, i).Trim()] = l.Substring(i + 1).Trim().Trim('"');
            }
            return r;
        }
    }
}
