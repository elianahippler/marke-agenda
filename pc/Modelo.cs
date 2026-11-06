// Marke Agenda: dados, gravação em disco e cifragem das senhas.
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
    public class Tipo
    {
        public string Id { get; set; }
        public string Nome { get; set; }
        public string Cor { get; set; }      // "#RRGGBB"
        public bool Avisar { get; set; }     // mostra aviso quando chega o horário
        public string Mod { get; set; }      // quando mudou (sincronização)
    }

    public class Tarefa
    {
        public string Id { get; set; }
        public string Titulo { get; set; }
        public string Descricao { get; set; }
        public string TipoId { get; set; }
        public string Cor { get; set; }      // vazio = cor do tipo
        public string Origem { get; set; }   // Manual, Google, TomTicket, Markedesk
        public string Ref { get; set; }      // id na ferramenta de origem
        public string Link { get; set; }
        public string Data { get; set; }     // yyyy-MM-dd
        public string Hora { get; set; }     // HH:mm
        public int Duracao { get; set; }     // minutos (a hora final sai daqui)
        public bool Concluida { get; set; }
        public bool Avisado { get; set; }
        public string Criada { get; set; }
        public string Mod { get; set; }      // quando mudou (sincronização)

        // Repetição: "" (não repete), "diaria", "uteis", "semanal", "mensal"
        public string Repetir { get; set; }
        public int RepetirCada { get; set; }            // de quantos em quantos (só nas repetições "cada-*")
        public string RepetirAte { get; set; }          // yyyy-MM-dd ou vazio
        public List<string> ConcluidasEm { get; set; }  // dias concluídos de uma tarefa que repete
        public List<string> ExcluidasEm { get; set; }   // dias em que esta ocorrência foi removida
        public string AvisadoEm { get; set; }           // último dia em que o aviso saiu
        public string SonecaAte { get; set; }           // adiado: avisar de novo neste momento
        public bool Aguardando { get; set; }            // em aguardo: depende de terceiros
        public bool Fixa { get; set; }                  // modelo: arrastar cria uma cópia, ela nunca sai da lista
        public string NaLixeira { get; set; }           // quando foi para a Lixeira (vazio = ativa)
        public bool Ativa() { return string.IsNullOrEmpty(NaLixeira); }
        public string AguardandoDe { get; set; }        // de quem / do quê

        public bool EstaAgendada() { return !string.IsNullOrEmpty(Data) && !string.IsNullOrEmpty(Hora); }
        public bool Repete() { return !string.IsNullOrEmpty(Repetir); }

        public static string Dia(DateTime d) { return d.ToString("yyyy-MM-dd"); }

        /// A tarefa aparece neste dia? (considera a repetição)
        public bool OcorreEm(DateTime dia)
        {
            if (Fixa || !Ativa() || !EstaAgendada()) return false;
            DateTime ini;
            if (!DateTime.TryParse(Data, out ini)) return false;
            dia = dia.Date; ini = ini.Date;
            if (!Repete()) return dia == ini;
            if (dia < ini) return false;
            DateTime ate;
            if (!string.IsNullOrEmpty(RepetirAte) && DateTime.TryParse(RepetirAte, out ate) && dia > ate.Date) return false;
            switch (Repetir)
            {
                case "diaria": return true;
                case "uteis": return dia.DayOfWeek != DayOfWeek.Saturday && dia.DayOfWeek != DayOfWeek.Sunday;
                case "semanal": return dia.DayOfWeek == ini.DayOfWeek;
                case "mensal": return MesmoDiaDoMes(ini, dia);
                case "cada-dia": return (dia - ini).Days % Cada() == 0;
                case "cada-semana": return dia.DayOfWeek == ini.DayOfWeek && ((dia - ini).Days / 7) % Cada() == 0;
                case "cada-mes": return MesmoDiaDoMes(ini, dia) && (((dia.Year - ini.Year) * 12 + dia.Month - ini.Month) % Cada() == 0);
            }
            return false;
        }

        /// De quantos em quantos a tarefa repete (mínimo 1).
        public int Cada() { return RepetirCada > 1 ? RepetirCada : 1; }

        /// Mesmo dia do mês, segurando o fim de mês curto (dia 31 cai no dia 30, 28...).
        static bool MesmoDiaDoMes(DateTime ini, DateTime dia)
        {
            int ultimo = DateTime.DaysInMonth(dia.Year, dia.Month);
            return dia.Day == ini.Day || (ini.Day > ultimo && dia.Day == ultimo);
        }

        /// Tira só a ocorrência deste dia, deixando o resto da repetição de pé.
        public void ExcluirSo(DateTime dia)
        {
            if (ExcluidasEm == null) ExcluidasEm = new List<string>();
            string k = Dia(dia);
            if (!ExcluidasEm.Contains(k)) ExcluidasEm.Add(k);
        }

        public bool ConcluidaEm(DateTime dia)
        {
            if (!Repete()) return Concluida;
            return ConcluidasEm != null && ConcluidasEm.Contains(Dia(dia));
        }

        public void MarcarConcluida(DateTime dia, bool feita)
        {
            if (!Repete()) { Concluida = feita; return; }
            if (ConcluidasEm == null) ConcluidasEm = new List<string>();
            string k = Dia(dia);
            ConcluidasEm.Remove(k);
            if (feita) ConcluidasEm.Add(k);
        }

        public static string NomeRepeticao(string r) { return NomeRepeticao(r, 1); }

        public static string NomeRepeticao(string r, int cada)
        {
            if (cada < 2) cada = 1;
            switch (r)
            {
                case "diaria": return "Todo dia";
                case "uteis": return "Dias úteis";
                case "semanal": return "Toda semana";
                case "mensal": return "Todo mês";
                case "cada-dia": return cada == 1 ? "Todo dia" : "A cada " + cada + " dias";
                case "cada-semana": return cada == 1 ? "Toda semana" : "A cada " + cada + " semanas";
                case "cada-mes": return cada == 1 ? "Todo mês" : "A cada " + cada + " meses";
            }
            return "";
        }

        /// O texto da repetição desta tarefa, já com o "de quantos em quantos".
        public string NomeDaRepeticao() { return NomeRepeticao(Repetir, Cada()); }

        /// A repetição por extenso, dizendo em que dia ela cai ("Todo mês, no dia 5").
        public string DetalheRepeticao()
        {
            DateTime ini;
            if (!DateTime.TryParse(Data ?? "", out ini)) return NomeDaRepeticao();
            if (Repetir == "mensal" || Repetir == "cada-mes") return NomeDaRepeticao() + ", no dia " + ini.Day;
            if (Repetir == "semanal" || Repetir == "cada-semana") return NomeDaRepeticao() + ", " + DiaDaSemana(ini.DayOfWeek);
            return NomeDaRepeticao();
        }

        static string DiaDaSemana(DayOfWeek d)
        {
            switch (d)
            {
                case DayOfWeek.Sunday: return "domingo";
                case DayOfWeek.Monday: return "segunda-feira";
                case DayOfWeek.Tuesday: return "terça-feira";
                case DayOfWeek.Wednesday: return "quarta-feira";
                case DayOfWeek.Thursday: return "quinta-feira";
                case DayOfWeek.Friday: return "sexta-feira";
            }
            return "sábado";
        }
    }

    /// Versão do programa (o botão "Procurar atualização" compara com pc/versao.txt no GitHub).
    public static class VersaoApp
    {
        public const string Atual = "1.1.1";
    }

    public class Nota
    {
        public string Id { get; set; }
        public string Titulo { get; set; }
        public string Texto { get; set; }
        public string Atualizada { get; set; }
        public string Mod { get; set; }
    }

    /// Evento que marca o dia inteiro (ou um período): férias, atestado, feriado...
    public class EventoDia
    {
        public string Id { get; set; }
        public string Tipo { get; set; }     // ferias, atestado, feriado, folga, outro
        public string Nome { get; set; }
        public string Cor { get; set; }
        public string Inicio { get; set; }   // yyyy-MM-dd
        public string Fim { get; set; }      // yyyy-MM-dd (igual ao início se for um dia só)
        public string HoraInicio { get; set; }   // HH:mm, vazio = o dia inteiro
        public string HoraFim { get; set; }      // HH:mm
        public string Mod { get; set; }

        public bool DiaInteiro() { return string.IsNullOrEmpty(HoraInicio) || string.IsNullOrEmpty(HoraFim); }

        /// "09:00 às 11:00" quando tem horário; vazio quando é o dia inteiro.
        public string Faixa() { return DiaInteiro() ? "" : HoraInicio + " às " + HoraFim; }

        public static readonly string[] Tipos = { "ferias", "atestado", "feriado", "folga", "outro" };
        public static readonly string[] Nomes = { "Férias", "Atestado", "Feriado", "Folga", "Outro" };
        public static readonly string[] CoresPadrao = { "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#64748B" };

        public string Titulo() { return string.IsNullOrEmpty(Nome) ? NomeTipo(Tipo) : Nome; }
        public static string NomeTipo(string t) { int i = Array.IndexOf(Tipos, t); return i < 0 ? "Evento" : Nomes[i]; }
    }

    /// Pedaço do dia que não aparece na agenda: almoço, café, reunião fixa...
    public class Intervalo
    {
        public string Id { get; set; }
        public string Nome { get; set; }
        public string Inicio { get; set; }   // HH:mm
        public string Fim { get; set; }      // HH:mm
        public bool Ligado { get; set; }
        public string Mod { get; set; }

        public int MinInicio() { return Minutos(Inicio); }
        public int MinFim() { return Minutos(Fim); }
        public bool Vale() { return Ligado && MinInicio() >= 0 && MinFim() > MinInicio(); }

        /// Este horário (em minutos desde a meia-noite) cai dentro do intervalo?
        public bool Pega(int min) { return Vale() && min > MinInicio() && min < MinFim(); }

        public static int Minutos(string hhmm)
        {
            if (string.IsNullOrEmpty(hhmm)) return -1;
            string[] p = hhmm.Split(':');
            int h, m;
            if (p.Length != 2 || !int.TryParse(p[0], out h) || !int.TryParse(p[1], out m)) return -1;
            return h * 60 + m;
        }
    }

    /// Tipo de evento que a pessoa cadastra: aniversário, férias, feriado...
    public class TipoEvento
    {
        public string Id { get; set; }      // o mesmo "Tipo" gravado no EventoDia
        public string Nome { get; set; }
        public string Cor { get; set; }
        public string Mod { get; set; }
    }

    public class Acesso
    {
        public string Ferramenta { get; set; }
        public string Url { get; set; }
        public string Login { get; set; }
        public string Senha { get; set; }    // cifrada
        public string Obs { get; set; }
    }

    public class Config
    {
        public int HoraInicio { get; set; }
        public int HoraFim { get; set; }
        public int Intervalo { get; set; }
        public bool AvisarLembretes { get; set; }
        public int AvisoAntesMin { get; set; }
        public int AtualizarMin { get; set; }
        public bool AtualizarAoAbrir { get; set; }
        public bool FecharParaBandeja { get; set; }
        public bool AvisoJanela { get; set; }   // janela por cima de tudo (senão, balão do Windows)
        public bool AvisoSom { get; set; }
        public bool AvisarTodas { get; set; }   // toda tarefa com horário avisa (senão, só os tipos marcados)
        public bool PausaAlmoco { get; set; }          // antigo: virou o primeiro item de Intervalos
        public bool MostrarAguardando { get; set; }    // exibir a seção "Aguardando" na tela
        public bool DestacarFimDeSemana { get; set; }
        public bool InicioConfigurado { get; set; }   // já ligou "abrir com o Windows" e "fechar para segundo plano" uma vez
        public string AlmocoInicio { get; set; }
        public string AlmocoFim { get; set; }

        public string TipoGoogle { get; set; }
        public string TipoTomTicket { get; set; }
        public string TipoMarkedesk { get; set; }

        // Google (Client ID / Secret do OAuth, tipo "App para computador")
        public string GoogleClientId { get; set; }
        public string GoogleSecret { get; set; }    // cifrado
        public string GoogleRefresh { get; set; }   // cifrado
        public string GoogleAgendaId { get; set; }
        public string GoogleLogin { get; set; }
        public string GoogleSenha { get; set; }     // cifrada, só referência

        // TomTicket
        public string TomToken { get; set; }        // cifrado
        public string TomSituacoes { get; set; }
        public string TomAtendente { get; set; }
        public string TomLink { get; set; }
        public string TomLogin { get; set; }
        public string TomSenha { get; set; }        // cifrada, só referência

        // Markedesk
        public string MdUrlApi { get; set; }
        public string MdUrlTela { get; set; }
        public string MdEmail { get; set; }
        public string MdSenha { get; set; }         // cifrada
        public bool MdTodos { get; set; }     // antigo, não usado
        public string MdModo { get; set; }    // setor (filas do usuário), meus, todos

        public Config()
        {
            HoraInicio = 8; HoraFim = 18; Intervalo = 30;
            AvisarLembretes = true; AvisoAntesMin = 5; AvisoJanela = true; AvisoSom = true; AvisarTodas = true;
            PausaAlmoco = true; MostrarAguardando = true; DestacarFimDeSemana = true; AlmocoInicio = "12:00"; AlmocoFim = "13:30"; AtualizarMin = 15; AtualizarAoAbrir = true;
            TipoGoogle = "evento"; TipoTomTicket = "chamado"; TipoMarkedesk = "atendimento";
            GoogleAgendaId = "primary"; TomSituacoes = "0,1,2,3,6,7";
        }
    }

    public class Dados
    {
        public Config Config { get; set; }
        public List<Tipo> Tipos { get; set; }
        public List<Tarefa> Tarefas { get; set; }
        public List<Nota> Notas { get; set; }
        public List<Acesso> Acessos { get; set; }
        public string NotaRapida { get; set; }
        public string NotaRapidaMod { get; set; }
        public Dictionary<string, string> Excluidos { get; set; }     // id -> quando foi excluído (o outro aparelho exclui também)
        public Dictionary<string, string> Assinaturas { get; set; }   // id -> como estava no último salvamento (para saber o que mudou)
        public string UltimaSinc { get; set; }
        public event Action Salvou;   // avisa a tela para sincronizar com o celular
        public Dictionary<string, List<string>> HorariosExtras { get; set; }   // dia (yyyy-MM-dd) -> horários a mais só naquele dia
        public List<EventoDia> Eventos { get; set; }                            // férias, atestado, feriado...
        public List<Intervalo> Intervalos { get; set; }                         // pedaços do dia fora da agenda
        public List<TipoEvento> TiposEvento { get; set; }                       // tipos de evento cadastrados
        public List<string> NaoSaoMinhas { get; set; }                          // "Origem:Ref" que não voltam ao atualizar

        public List<EventoDia> EventosEm(DateTime dia)
        {
            string k = Tarefa.Dia(dia);
            return Eventos.Where(e => string.Compare(e.Inicio ?? "", k) <= 0 && string.Compare(string.IsNullOrEmpty(e.Fim) ? e.Inicio : e.Fim, k) >= 0).ToList();
        }

        public Dados()
        {
            Config = new Config(); Tipos = new List<Tipo>(); Tarefas = new List<Tarefa>();
            Notas = new List<Nota>(); Acessos = new List<Acesso>();
        }

        static string pasta;
        public static string Pasta
        {
            get
            {
                if (pasta != null) return pasta;
                // Ao lado do programa; se não der para gravar ali, na pasta do usuário do Windows.
                string p = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "dados");
                try
                {
                    Directory.CreateDirectory(p);
                    string teste = Path.Combine(p, ".teste");
                    File.WriteAllText(teste, "ok"); File.Delete(teste);
                }
                catch
                {
                    p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarkeAgenda");
                    Directory.CreateDirectory(p);
                }
                pasta = p;
                return p;
            }
        }
        [ScriptIgnore] public Sessao Sessao { get; set; }

        static JavaScriptSerializer Json() { JavaScriptSerializer j = new JavaScriptSerializer(); j.MaxJsonLength = int.MaxValue; return j; }

        /// Arquivo da versão antiga, sem login (texto puro).
        public static Dados LerTextoPuro(string arq)
        {
            try { return Json().Deserialize<Dados>(File.ReadAllText(arq, Encoding.UTF8)); }
            catch (Exception ex) { Log("Falha ao ler " + arq + ": " + ex.Message); return null; }
        }

        public static Dados Carregar(Sessao s)
        {
            Dados d = null;
            try { if (File.Exists(s.Caminho)) d = Json().Deserialize<Dados>(s.Decifrar(File.ReadAllBytes(s.Caminho))); }
            catch (Exception ex)
            {
                Log("Falha ao ler os dados: " + ex.Message);
                try { File.Copy(s.Caminho, s.Caminho + ".erro-" + DateTime.Now.ToString("yyyyMMddHHmmss")); } catch { }
                // tenta a cópia anterior
                try { if (File.Exists(s.Caminho + ".bak")) d = Json().Deserialize<Dados>(s.Decifrar(File.ReadAllBytes(s.Caminho + ".bak"))); } catch { }
            }
            if (d == null) d = new Dados();
            d.Sessao = s;
            if (d.Config == null) d.Config = new Config();
            if (d.Tipos == null) d.Tipos = new List<Tipo>();
            if (d.Tarefas == null) d.Tarefas = new List<Tarefa>();
            if (d.Notas == null) d.Notas = new List<Nota>();
            if (d.Acessos == null) d.Acessos = new List<Acesso>();
            if (d.HorariosExtras == null) d.HorariosExtras = new Dictionary<string, List<string>>();
            if (d.Eventos == null) d.Eventos = new List<EventoDia>();
            if (d.NaoSaoMinhas == null) d.NaoSaoMinhas = new List<string>();
            string limiteLixeira = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
            d.Tarefas.RemoveAll(t => !string.IsNullOrEmpty(t.NaLixeira) && string.CompareOrdinal(t.NaLixeira, limiteLixeira) < 0);
            if (d.Excluidos == null) d.Excluidos = new Dictionary<string, string>();
            if (d.Assinaturas == null) d.Assinaturas = new Dictionary<string, string>();
            if (d.Tipos.Count == 0)
            {
                d.Tipos.Add(new Tipo { Id = "tarefa", Nome = "Tarefa", Cor = "#0EA5E9", Avisar = false });
                d.Tipos.Add(new Tipo { Id = "lembrete", Nome = "Lembrete", Cor = "#F59E0B", Avisar = true });
                d.Tipos.Add(new Tipo { Id = "evento", Nome = "Evento da agenda", Cor = "#3B82F6", Avisar = true });
                d.Tipos.Add(new Tipo { Id = "chamado", Nome = "Chamado TomTicket", Cor = "#10B981", Avisar = false });
                d.Tipos.Add(new Tipo { Id = "atendimento", Nome = "Atendimento Markedesk", Cor = "#EC4899", Avisar = false });
            }
            d.AjeitarIntervalos();
            d.AjeitarTiposEvento();
            return d;
        }

        /// Primeira vez com intervalos: a pausa de almoço que já existia vira o primeiro da lista.
        public void AjeitarIntervalos()
        {
            if (Intervalos == null) Intervalos = new List<Intervalo>();
            if (Intervalos.Count > 0) return;
            Intervalos.Add(new Intervalo
            {
                Id = "almoco",
                Nome = "Almoço",
                Inicio = string.IsNullOrEmpty(Config.AlmocoInicio) ? "12:00" : Config.AlmocoInicio,
                Fim = string.IsNullOrEmpty(Config.AlmocoFim) ? "13:30" : Config.AlmocoFim,
                Ligado = Config.PausaAlmoco,
            });
        }

        /// Os tipos de evento que já eram fixos no código viram cadastro, e a pessoa pode criar outros.
        public void AjeitarTiposEvento()
        {
            if (TiposEvento == null) TiposEvento = new List<TipoEvento>();
            if (TiposEvento.Count > 0) return;
            for (int i = 0; i < EventoDia.Tipos.Length; i++)
                TiposEvento.Add(new TipoEvento { Id = EventoDia.Tipos[i], Nome = EventoDia.Nomes[i], Cor = EventoDia.CoresPadrao[i] });
            TiposEvento.Insert(0, new TipoEvento { Id = "aniversario", Nome = "Aniversário", Cor = "#F472B6" });
        }

        /// O tipo de evento cadastrado, ou um genérico quando o tipo sumiu.
        public TipoEvento TipoEventoDe(string id)
        {
            TipoEvento t = (TiposEvento ?? new List<TipoEvento>()).FirstOrDefault(x => x.Id == id);
            return t ?? new TipoEvento { Id = id, Nome = EventoDia.NomeTipo(id), Cor = "#64748B" };
        }

        /// Os intervalos que valem (ligados e com horário certo).
        public List<Intervalo> IntervalosAtivos()
        {
            return (Intervalos ?? new List<Intervalo>()).Where(i => i.Vale()).ToList();
        }

        /// Este horário cai em algum intervalo que não aparece na agenda?
        public bool EmIntervalo(int minutos)
        {
            return IntervalosAtivos().Any(i => i.Pega(minutos));
        }

        public void Salvar()
        {
            if (Sessao == null) return;
            try { Sincronia.Carimbar(this); } catch (Exception ex) { Log("Carimbar: " + ex.Message); }
            try
            {
                string arq = Sessao.Caminho, tmp = arq + ".tmp";
                File.WriteAllBytes(tmp, Sessao.Cifrar(Json().Serialize(this)));
                if (File.Exists(arq)) File.Copy(arq, arq + ".bak", true);
                File.Copy(tmp, arq, true);
                File.Delete(tmp);
            }
            catch (Exception ex) { Log("Falha ao salvar: " + ex.Message); }
            if (Salvou != null) try { Salvou(); } catch { }
        }

        public Tipo TipoDe(Tarefa t)
        {
            Tipo tp = Tipos.FirstOrDefault(x => x.Id == t.TipoId);
            return tp ?? Tipos.First();
        }

        public Color CorDe(Tarefa t)
        {
            string c = string.IsNullOrEmpty(t.Cor) ? TipoDe(t).Cor : t.Cor;
            return Cores.De(c);
        }

        public static string NovoId() { return Guid.NewGuid().ToString("N").Substring(0, 12); }

        public static void Log(string msg)
        {
            try { File.AppendAllText(Path.Combine(Pasta, "log.txt"), DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8); }
            catch { }
        }
    }

    public static class Cores
    {
        public static Color De(string hex)
        {
            try { return ColorTranslator.FromHtml(string.IsNullOrEmpty(hex) ? "#8B5CF6" : hex); }
            catch { return Color.MediumPurple; }
        }
        public static string Hex(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }
    }

    /// Cifra com a conta do Windows (DPAPI): só este usuário, neste PC, consegue abrir.
    public static class Cripto
    {
        public static string Fechar(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return "";
            byte[] b = ProtectedData.Protect(Encoding.UTF8.GetBytes(texto), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(b);
        }
        public static string Abrir(string cifrado)
        {
            if (string.IsNullOrEmpty(cifrado)) return "";
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(cifrado), null, DataProtectionScope.CurrentUser)); }
            catch { return ""; }
        }
    }
}
