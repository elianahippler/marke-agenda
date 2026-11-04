// Marke Agenda (celular). Mesma agenda do PC, sincronizada pelo Google Drive
// (pasta escondida do aplicativo). Não guarda senha nenhuma: só a conta Google.
'use strict';

// ID do cliente OAuth "Aplicativo da Web" (Google Cloud > Credenciais).
// Pode vir fixo aqui ou ser colado na tela de entrada (fica guardado no aparelho).
const CLIENT_ID_PADRAO = '554966142292-alt06npu0j304t9osgoser7f0nr53sga.apps.googleusercontent.com';
let CLIENT_ID = localStorage.getItem('ma_client') || CLIENT_ID_PADRAO;
function temCliente() { return /\.apps\.googleusercontent\.com$/.test(CLIENT_ID.trim()); }
const ESCOPOS = 'openid email profile https://www.googleapis.com/auth/drive.appdata';
const ARQUIVO = 'marke-agenda.json';

// ---------------------------------------------------------------- estado
const vazio = () => ({ Tarefas: [], Notas: [], Tipos: [], Eventos: [], NaoSaoMinhas: [], HorariosExtras: {},
  NotaRapida: '', NotaRapidaMod: '', Excluidos: {}, Agenda: {} });
let D = carregar('ma_dados') || vazio();
let assin = carregar('ma_assin') || {};
let conta = carregar('ma_conta');            // { email, nome }
let token = null, tokenVence = 0, clienteToken = null;
(function () { const t = carregar('ma_token'); if (t && t.tokenVence > Date.now() + 60000) { token = t.token; tokenVence = t.tokenVence; } })();
let dia = hoje();
let aba = 'agenda';
let filtroTarefas = 'sem';
let sincronizando = false, ultimaSinc = localStorage.getItem('ma_ultima') || '';

function carregar(k) { try { return JSON.parse(localStorage.getItem(k)); } catch (e) { return null; } }
function gravarLocal() {
  carimbar();
  localStorage.setItem('ma_dados', JSON.stringify(D));
  localStorage.setItem('ma_assin', JSON.stringify(assin));
  agendarSinc();
}

// ---------------------------------------------------------------- datas
function hoje() { const d = new Date(); d.setHours(0, 0, 0, 0); return d; }
function chave(d) { return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); }
function deChave(s) { const p = (s || '').split('-'); return new Date(+p[0], +p[1] - 1, +p[2]); }
function maisDias(d, n) { const x = new Date(d); x.setDate(x.getDate() + n); return x; }
function minutos(hhmm) { const m = /^(\d{1,2}):(\d{2})$/.exec(hhmm || ''); return m ? +m[1] * 60 + +m[2] : -1; }
function hhmm(min) { return String(Math.floor(min / 60)).padStart(2, '0') + ':' + String(min % 60).padStart(2, '0'); }
function agoraISO() { return new Date().toISOString(); }
function novoId() { return Math.random().toString(16).slice(2, 14).padEnd(12, '0'); }
const DIAS = ['domingo', 'segunda-feira', 'terça-feira', 'quarta-feira', 'quinta-feira', 'sexta-feira', 'sábado'];
const MESES = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro'];

// ---------------------------------------------------------------- regras (iguais às do PC)
function agendada(t) { return !!t.Data && !!t.Hora; }
function repete(t) { return !!t.Repetir; }
function ocorreEm(t, d) {
  if (t.Fixa || !agendada(t)) return false;
  const ini = deChave(t.Data);
  if (!repete(t)) return chave(d) === t.Data;
  if (d < ini) return false;
  if (t.RepetirAte && d > deChave(t.RepetirAte)) return false;
  const dw = d.getDay();
  switch (t.Repetir) {
    case 'diaria': return true;
    case 'uteis': return dw !== 0 && dw !== 6;
    case 'semanal': return dw === ini.getDay();
    case 'mensal': {
      const ult = new Date(d.getFullYear(), d.getMonth() + 1, 0).getDate();
      return d.getDate() === ini.getDate() || (ini.getDate() > ult && d.getDate() === ult);
    }
  }
  return false;
}
function concluidaEm(t, d) { return repete(t) ? (t.ConcluidasEm || []).includes(chave(d)) : !!t.Concluida; }
function marcarConcluida(t, d, feita) {
  if (!repete(t)) { t.Concluida = feita; return; }
  t.ConcluidasEm = (t.ConcluidasEm || []).filter(x => x !== chave(d));
  if (feita) t.ConcluidasEm.push(chave(d));
}
function tipoDe(t) { return D.Tipos.find(x => x.Id === t.TipoId) || D.Tipos[0] || { Nome: 'Tarefa', Cor: '#0EA5E9' }; }
function corDe(t) { return t.Cor || tipoDe(t).Cor || '#0EA5E9'; }
function nomeRepeticao(r) { return { diaria: 'Todo dia', uteis: 'Dias úteis', semanal: 'Toda semana', mensal: 'Todo mês' }[r] || ''; }
function atrasada(t, d) {
  if (!agendada(t) || concluidaEm(t, d)) return false;
  const fim = new Date(d); const m = minutos(t.Hora); fim.setHours(0, m + (t.Duracao > 0 ? t.Duracao : (A().Intervalo || 30)), 0, 0);
  return fim < new Date();
}
function A() { return Object.assign({ HoraInicio: 8, HoraFim: 18, Intervalo: 30, PausaAlmoco: true, AlmocoInicio: '12:00', AlmocoFim: '13:30', DestacarFimDeSemana: true, AvisoAntesMin: 5 }, D.Agenda || {}); }
function horarios(d) {
  const a = A(), passo = a.Intervalo > 0 ? a.Intervalo : 30, ai = minutos(a.AlmocoInicio), af = minutos(a.AlmocoFim);
  let r = [];
  for (let m = a.HoraInicio * 60; m <= a.HoraFim * 60; m += passo) {
    if (a.PausaAlmoco && ai >= 0 && af > ai && m > ai && m < af) continue;
    r.push(hhmm(m));
  }
  if (a.PausaAlmoco && af > 0 && af <= a.HoraFim * 60) r.push(hhmm(af));
  r = r.concat((D.HorariosExtras || {})[chave(d)] || []);
  return [...new Set(r)].sort();
}
function horarioDe(hora, hs) { let x = hs[0]; for (const h of hs) if (h <= hora) x = h; return x; }
function eventosEm(d) { const k = chave(d); return (D.Eventos || []).filter(e => e.Inicio <= k && (e.Fim || e.Inicio) >= k); }
function nomeEvento(e) { return e.Nome || { ferias: 'Férias', atestado: 'Atestado', feriado: 'Feriado', folga: 'Folga', outro: 'Outro' }[e.Tipo] || 'Evento'; }

// ---------------------------------------------------------------- sincronização (mesma lógica do PC)
function itens() {
  const r = [];
  D.Tarefas.forEach(x => r.push(['t:' + x.Id, x])); D.Notas.forEach(x => r.push(['n:' + x.Id, x]));
  D.Tipos.forEach(x => r.push(['y:' + x.Id, x])); D.Eventos.forEach(x => r.push(['e:' + x.Id, x]));
  return r;
}
function sig(x) { return JSON.stringify(Object.assign({}, x, { Mod: null })); }
function carimbar() {
  const agora = agoraISO(), vistos = new Set();
  for (const [k, x] of itens()) {
    vistos.add(k); const s = sig(x);
    if (assin[k] !== s || !x.Mod) { x.Mod = agora; assin[k] = s; }
  }
  for (const k of Object.keys(assin)) if (k !== 'nr' && !vistos.has(k)) { D.Excluidos[k] = agora; delete assin[k]; }
  if (assin.nr !== (D.NotaRapida || '')) { D.NotaRapidaMod = agora; assin.nr = D.NotaRapida || ''; }
}
function assinarTudo() { assin = {}; for (const [k, x] of itens()) assin[k] = sig(x); assin.nr = D.NotaRapida || ''; }
function juntar(local, remoto, pre) {
  const ordem = [], res = {};
  for (const x of (local || []).concat(remoto || [])) {
    if (!x || !x.Id) continue;
    if (!(x.Id in res)) { res[x.Id] = x; ordem.push(x.Id); }
    else if ((x.Mod || '') > (res[x.Id].Mod || '')) res[x.Id] = x;
  }
  return ordem.map(k => res[k]).filter(x => !(D.Excluidos[pre + x.Id] && D.Excluidos[pre + x.Id] >= (x.Mod || '')));
}
function mesclar(r) {
  if (!r) return;
  for (const [k, q] of Object.entries(r.Excluidos || {})) if (!D.Excluidos[k] || q > D.Excluidos[k]) D.Excluidos[k] = q;
  D.Tarefas = juntar(D.Tarefas, r.Tarefas, 't:');
  D.Notas = juntar(D.Notas, r.Notas, 'n:');
  const tipos = juntar(D.Tipos, r.Tipos, 'y:'); if (tipos.length) D.Tipos = tipos;
  D.Eventos = juntar(D.Eventos, r.Eventos, 'e:');
  for (const k of r.NaoSaoMinhas || []) if (!D.NaoSaoMinhas.includes(k)) D.NaoSaoMinhas.push(k);
  for (const [k, hs] of Object.entries(r.HorariosExtras || {})) { D.HorariosExtras[k] = [...new Set((D.HorariosExtras[k] || []).concat(hs))]; }
  if ((r.NotaRapidaMod || '') > (D.NotaRapidaMod || '')) { D.NotaRapida = r.NotaRapida; D.NotaRapidaMod = r.NotaRapidaMod; }
  if (r.Agenda && Object.keys(r.Agenda).length) D.Agenda = r.Agenda;   // horários: vale o que o PC definiu
  const limite = new Date(Date.now() - 90 * 864e5).toISOString().slice(0, 10);
  for (const [k, q] of Object.entries(D.Excluidos)) if (q < limite) delete D.Excluidos[k];
  assinarTudo();
}
function pacote() {
  return Object.assign({}, D, { Versao: 1, Gravado: agoraISO(), Dispositivo: 'Celular' });
}

// ---------------------------------------------------------------- Google
function iniciarGoogle() {
  if (!window.google || !google.accounts) { setTimeout(iniciarGoogle, 300); return; }
  clienteToken = google.accounts.oauth2.initTokenClient({ client_id: CLIENT_ID, scope: ESCOPOS, callback: () => { } });
}
function pedirToken(silencioso) {
  return new Promise((ok, falha) => {
    if (token && Date.now() < tokenVence - 60000) return ok(token);
    if (!clienteToken) return falha(new Error('Google ainda carregando, tente de novo.'));
    clienteToken.callback = (r) => {
      if (r.error) return falha(new Error(r.error === 'access_denied' ? 'Login cancelado.' : r.error));
      token = r.access_token; tokenVence = Date.now() + (r.expires_in || 3600) * 1000;
      // guarda no aparelho: fechar e abrir o app não pede o Google de novo enquanto valer
      localStorage.setItem('ma_token', JSON.stringify({ token, tokenVence }));
      ok(token);
    };
    clienteToken.error_callback = (e) => falha(new Error(e && e.type === 'popup_closed' ? 'Login cancelado.' : 'Não foi possível entrar.'));
    // sempre a lista de contas do aparelho: é só tocar na sua
    clienteToken.requestAccessToken({ prompt: 'select_account' });
  });
}
async function api(url, opcoes) {
  const tk = await pedirToken(true);
  opcoes = opcoes || {}; opcoes.headers = Object.assign({ Authorization: 'Bearer ' + tk }, opcoes.headers || {});
  const r = await fetch(url, opcoes);
  if (r.status === 401) { token = null; localStorage.removeItem('ma_token'); throw new Error('Toque em ☁ para entrar de novo.'); }
  if (!r.ok) throw new Error('Google respondeu ' + r.status);
  return r;
}

async function sincronizar(manual) {
  if (sincronizando || !conta) return;
  if (!tokenValido() && !manual) { mostrarSinc(); return; }   // sem toque, o iPhone não deixa abrir o Google
  sincronizando = true; mostrarSinc();
  try {
    const q = encodeURIComponent("name='" + ARQUIVO + "'");
    const lista = await (await api('https://www.googleapis.com/drive/v3/files?spaces=appDataFolder&fields=files(id)&q=' + q)).json();
    const id = lista.files && lista.files[0] ? lista.files[0].id : null;
    if (id) {
      const remoto = await (await api('https://www.googleapis.com/drive/v3/files/' + id + '?alt=media')).json();
      carimbar(); mesclar(remoto);
    } else carimbar();
    const corpo = JSON.stringify(pacote());
    if (id) await api('https://www.googleapis.com/upload/drive/v3/files/' + id + '?uploadType=media', { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: corpo });
    else {
      const b = 'marke' + novoId();
      const multi = '--' + b + '\r\nContent-Type: application/json; charset=UTF-8\r\n\r\n' + JSON.stringify({ name: ARQUIVO, parents: ['appDataFolder'] }) +
        '\r\n--' + b + '\r\nContent-Type: application/json; charset=UTF-8\r\n\r\n' + corpo + '\r\n--' + b + '--';
      await api('https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart', { method: 'POST', headers: { 'Content-Type': 'multipart/related; boundary=' + b }, body: multi });
    }
    localStorage.setItem('ma_dados', JSON.stringify(D)); localStorage.setItem('ma_assin', JSON.stringify(assin));
    ultimaSinc = new Date().toTimeString().slice(0, 5); localStorage.setItem('ma_ultima', ultimaSinc);
    desenhar();
  } catch (e) {
    if (manual) alert('Sincronização: ' + e.message);
  } finally { sincronizando = false; mostrarSinc(); }
}
let timerSinc = null;
function agendarSinc() { clearTimeout(timerSinc); timerSinc = setTimeout(() => sincronizar(false), 4000); }
function mostrarSinc() {
  const b = $('#btSinc'); b.classList.toggle('girando', sincronizando);
  $('#sincTexto').textContent = sincronizando ? 'sincronizando' : !tokenValido() ? 'tocar p/ sincronizar' : ultimaSinc ? ultimaSinc : 'ok';
}
function tokenValido() { return !!token && Date.now() < tokenVence - 60000; }

async function entrar() {
  $('#erroEntrada').textContent = '';
  if (!$('#caixaCliente').classList.contains('escondido')) {
    const v = $('#campoCliente').value.trim();
    if (!/\.apps\.googleusercontent\.com$/.test(v)) { $('#erroEntrada').textContent = 'Cole o Client ID (termina com .apps.googleusercontent.com).'; return; }
    CLIENT_ID = v; localStorage.setItem('ma_client', v);
    clienteToken = null; iniciarGoogle();
    await new Promise(r => setTimeout(r, 400));
  }
  if (!temCliente()) { $('#caixaCliente').classList.remove('escondido'); $('#erroEntrada').textContent = 'Falta o Client ID do Google.'; return; }
  try {
    await pedirToken(false);
    const eu = await (await api('https://www.googleapis.com/oauth2/v3/userinfo')).json();
    if (conta && conta.email !== eu.email) { D = vazio(); assin = {}; }   // outra conta: começa limpo
    conta = { email: eu.email, nome: eu.name || eu.email };
    localStorage.setItem('ma_conta', JSON.stringify(conta));
    abrirApp(); await sincronizar(true);
  } catch (e) { $('#erroEntrada').textContent = e.message; }
}

// ---------------------------------------------------------------- tela
const $ = s => document.querySelector(s);
function el(tag, cls, html) { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; }
function esc(s) { return String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])); }
function misturar(hex, f) {   // cor do cartão sobre o fundo
  const c = parseInt((hex || '#0EA5E9').slice(1), 16), r = c >> 16, g = (c >> 8) & 255, b = c & 255;
  const fr = 24, fg = 36, fb = 62;
  return 'rgb(' + Math.round(r * f + fr * (1 - f)) + ',' + Math.round(g * f + fg * (1 - f)) + ',' + Math.round(b * f + fb * (1 - f)) + ')';
}

function cartao(t, naAgenda) {
  const feita = concluidaEm(t, dia), cor = corDe(t);
  const c = el('div', 'cartao' + (feita ? ' feita' : ''));
  c.style.setProperty('--cor', cor); c.style.setProperty('--cor-clara', misturar(cor, .9));
  c.style.background = misturar(cor, feita ? .12 : .3);
  let sub;
  if (t.Fixa) sub = 'Fixa · ' + tipoDe(t).Nome;
  else if (t.Aguardando) sub = 'Aguardando' + (t.AguardandoDe ? ': ' + t.AguardandoDe : '');
  else if (naAgenda) sub = t.Hora + (t.Duracao > 0 ? ' (' + t.Duracao + 'min)' : '') + ' · ' + tipoDe(t).Nome;
  else sub = tipoDe(t).Nome + (t.Data ? ' · ' + t.Data.slice(8, 10) + '/' + t.Data.slice(5, 7) + (t.Hora ? ' ' + t.Hora : '') : '');
  if (repete(t)) sub += ' · ↻ ' + nomeRepeticao(t.Repetir);
  const atr = naAgenda && atrasada(t, dia);
  if (atr) sub = 'Atrasada · ' + sub;
  const selo = { Google: 'GOOGLE', TomTicket: 'TOMTICKET', Markedesk: 'MARKEDESK' }[t.Origem];
  c.innerHTML = (t.Fixa ? '' : '<button class="bola">' + (feita ? '✓' : '') + '</button>') +
    '<div class="txt"><div class="tit">' + esc(t.Titulo) + '</div><div class="sub' + (atr ? ' atrasada' : '') + '">' + esc(sub) + '</div></div>' +
    (selo ? '<span class="selo">' + selo + '</span>' : '');
  const bola = c.querySelector('.bola');
  if (bola) bola.onclick = (ev) => { ev.stopPropagation(); marcarConcluida(t, dia, !feita); gravarLocal(); desenhar(); };
  c.onclick = () => opcoesTarefa(t);
  return c;
}

function desenhar() {
  if (!conta) return;
  const nome = DIAS[dia.getDay()];
  const rel = chave(dia) === chave(hoje()) ? 'hoje' : chave(dia) === chave(maisDias(hoje(), 1)) ? 'amanhã' : chave(dia) === chave(maisDias(hoje(), -1)) ? 'ontem' : '';
  $('#diaTitulo').textContent = nome.charAt(0).toUpperCase() + nome.slice(1) + ', ' + dia.getDate() + ' de ' + MESES[dia.getMonth()];
  $('#diaSub').textContent = rel ? rel : 'toque para voltar a hoje';
  const doDia = D.Tarefas.filter(t => ocorreEm(t, dia));
  const feitas = doDia.filter(t => concluidaEm(t, dia)).length;
  $('#barra').style.width = doDia.length ? (100 * feitas / doDia.length) + '%' : '0';
  $('#navDia').style.display = $('#progresso').style.display = (aba === 'agenda' ? '' : 'none');
  document.querySelectorAll('.abas button').forEach(b => b.classList.toggle('ativa', b.dataset.aba === aba));
  const m = $('#conteudo'); const rol = m.scrollTop; m.innerHTML = '';
  if (aba === 'agenda') desenharAgenda(m, doDia);
  else if (aba === 'tarefas') desenharTarefas(m);
  else if (aba === 'aguardo') desenharAguardo(m);
  else desenharNotas(m);
  m.scrollTop = rol;
  mostrarSinc();
}

function desenharAgenda(m, doDia) {
  const evs = eventosEm(dia), fds = A().DestacarFimDeSemana && (dia.getDay() === 0 || dia.getDay() === 6);
  const corDia = evs.length ? (evs[0].Cor || '#10B981') : fds ? '#64748B' : null;
  if (corDia) {
    const f = el('div', 'faixa', esc(evs.length ? evs.map(nomeEvento).join(' · ') : (dia.getDay() === 6 ? 'Sábado' : 'Domingo')));
    f.style.borderColor = corDia; f.style.background = misturar(corDia, .35);
    m.appendChild(f);
    m.classList.add('dia-marcado'); m.style.setProperty('--listra', misturar(corDia, .25));
  } else m.classList.remove('dia-marcado');
  const hs = horarios(dia), extras = (D.HorariosExtras || {})[chave(dia)] || [];
  const agora = chave(dia) === chave(hoje()) ? horarioDe(hhmm(new Date().getHours() * 60 + new Date().getMinutes()), hs) : null;
  let alvo = null;
  for (const h of hs) {
    const linha = el('div', 'horario' + (h.endsWith(':00') ? '' : ' meia') + (h === agora ? ' agora' : ''));
    linha.appendChild(el('div', 'hora' + (h.endsWith(':00') ? ' cheia' : '') + (extras.includes(h) ? ' extra' : ''), h));
    const cs = el('div', 'cartoes');
    const aqui = doDia.filter(t => horarioDe(t.Hora, hs) === h).sort((a, b) => a.Hora.localeCompare(b.Hora));
    aqui.forEach(t => cs.appendChild(cartao(t, true)));
    if (!aqui.length) { const v = el('button', 'vazio', '+ toque para adicionar'); v.onclick = () => adicionarNoHorario(h); cs.appendChild(v); }
    else { const v = el('button', 'fraco pequeno', '+ adicionar'); v.style.textAlign = 'left'; v.onclick = () => adicionarNoHorario(h); cs.appendChild(v); }
    linha.appendChild(cs); m.appendChild(linha);
    if (h === agora) alvo = linha;
  }
  if (alvo && !m.dataset.rolou) { m.dataset.rolou = 1; setTimeout(() => alvo.scrollIntoView({ block: 'center' }), 50); }
}

function desenharTarefas(m) {
  const ad = el('div', 'adicionar');
  const inp = el('input'); inp.placeholder = 'Nova tarefa (ex.: 14:30 Ligar)';
  const b = el('button', null, '+');
  const criar = () => { if (inp.value.trim()) { adicionarRapida(inp.value); inp.value = ''; } };
  b.onclick = criar; inp.onkeydown = e => { if (e.key === 'Enter') criar(); };
  ad.append(inp, b); m.appendChild(ad);
  const fl = el('div', 'filtros');
  [['sem', 'Sem horário'], ['todas', 'Todas'], ['resolvidas', 'Resolvidas']].forEach(([k, n]) => {
    const x = el('button', filtroTarefas === k ? 'ativo' : '', n); x.onclick = () => { filtroTarefas = k; desenhar(); }; fl.appendChild(x);
  });
  m.appendChild(fl);
  let ts = D.Tarefas.filter(t => !t.Aguardando && !t.Fixa);
  if (filtroTarefas === 'sem') ts = ts.filter(t => !agendada(t) && !t.Concluida);
  else if (filtroTarefas === 'resolvidas') ts = ts.filter(t => t.Concluida || (repete(t) && concluidaEm(t, dia)));
  ts.sort((a, b) => (a.Concluida - b.Concluida) || (a.Data || '9').localeCompare(b.Data || '9') || (a.Titulo || '').localeCompare(b.Titulo || ''));
  m.appendChild(el('div', 'titulo-secao', 'Tarefas <span class="fraco pequeno">' + ts.length + '</span>'));
  const l = el('div', 'lista'); ts.forEach(t => l.appendChild(cartao(t, false))); m.appendChild(l);
  if (!ts.length) m.appendChild(el('div', 'vazio-lista', 'Nada por aqui.'));
}

function desenharAguardo(m) {
  const fixas = D.Tarefas.filter(t => t.Fixa).sort((a, b) => a.Titulo.localeCompare(b.Titulo));
  m.appendChild(el('div', 'titulo-secao', 'Tarefas fixas <span class="fraco pequeno">toque para usar</span>'));
  const lf = el('div', 'lista'); fixas.forEach(t => lf.appendChild(cartao(t, false))); m.appendChild(lf);
  if (!fixas.length) m.appendChild(el('div', 'vazio-lista', 'Nenhuma tarefa fixa. Crie no computador ou salve uma tarefa como fixa.'));
  const ag = D.Tarefas.filter(t => t.Aguardando && !t.Concluida && !t.Fixa);
  m.appendChild(el('div', 'titulo-secao', 'Em aguardo <span class="fraco pequeno">' + ag.length + '</span>'));
  const la = el('div', 'lista'); ag.forEach(t => la.appendChild(cartao(t, false))); m.appendChild(la);
  if (!ag.length) m.appendChild(el('div', 'vazio-lista', 'Nada em aguardo.'));
}

function desenharNotas(m) {
  m.appendChild(el('div', 'titulo-secao', 'Anotações rápidas'));
  const ta = el('textarea'); ta.rows = 5; ta.value = D.NotaRapida || '';
  let t0 = null; ta.oninput = () => { clearTimeout(t0); t0 = setTimeout(() => { D.NotaRapida = ta.value; gravarLocal(); }, 700); };
  m.appendChild(ta);
  const tit = el('div', 'titulo-secao', 'Notas'); const nova = el('button', 'botao sec', '+ Nova'); nova.style.padding = '6px 12px';
  nova.onclick = () => editarNota(null); tit.appendChild(nova); m.appendChild(tit);
  const l = el('div', 'lista');
  D.Notas.forEach(n => { const i = el('div', 'nota-item', '<b>' + esc(n.Titulo || '(sem título)') + '</b><span>' + esc((n.Texto || '').replace(/\s+/g, ' ')) + '</span>'); i.onclick = () => editarNota(n); l.appendChild(i); });
  m.appendChild(l);
  if (!D.Notas.length) m.appendChild(el('div', 'vazio-lista', 'Nenhuma nota.'));
}

// ---------------------------------------------------------------- conta / sair
function sairDaConta() {
  if (!confirm('Sair da conta ' + conta.email + '?\n\nA agenda continua salva no Google Drive e no computador. Para voltar, é só entrar de novo.')) return;
  try { if (token && window.google) google.accounts.oauth2.revoke(token, () => { }); } catch (e) { }
  const cliente = localStorage.getItem('ma_client');
  localStorage.clear();
  if (cliente) localStorage.setItem('ma_client', cliente);
  location.reload();
}
function abrirConta() {
  const c = el('div');
  c.appendChild(el('h3', null, esc(conta.nome || conta.email)));
  c.appendChild(el('div', 'sub', esc(conta.email) + (ultimaSinc ? ' · sincronizado às ' + ultimaSinc : '') + ' · versão 7'));
  c.appendChild(acao('☁ Sincronizar agora', () => sincronizar(true)));
  c.appendChild(acao('Sair da conta', sairDaConta, true));
  abrirFolha(c);
}

// ---------------------------------------------------------------- folha de opções
function abrirFolha(conteudo) {
  const f = $('#folha'); f.innerHTML = '<div class="alca"></div>'; f.appendChild(conteudo);
  f.classList.remove('escondido'); $('#fundoFolha').classList.remove('escondido');
}
function fecharFolha() { $('#folha').classList.add('escondido'); $('#fundoFolha').classList.add('escondido'); }
function acao(texto, fn, perigo) { const b = el('button', 'acao' + (perigo ? ' perigo' : ''), texto); b.onclick = () => { fecharFolha(); fn(); }; return b; }

function opcoesTarefa(t) {
  const c = el('div');
  c.appendChild(el('h3', null, esc(t.Titulo)));
  c.appendChild(el('div', 'sub', esc(tipoDe(t).Nome + (t.Descricao ? ' · ' + t.Descricao.slice(0, 160) : ''))));
  if (t.Fixa) {
    c.appendChild(acao('Planejar uma cópia em ' + (chave(dia) === chave(hoje()) ? 'hoje' : dia.toLocaleDateString('pt-BR')), () => escolherHorario(copiar(t), true)));
    c.appendChild(acao('Copiar para Tarefas (sem horário)', () => { D.Tarefas.push(copiar(t)); gravarLocal(); desenhar(); }));
  } else {
    const feita = concluidaEm(t, dia);
    c.appendChild(acao(feita ? 'Desmarcar concluída' : '✓ Marcar como concluída', () => { marcarConcluida(t, dia, !feita); gravarLocal(); desenhar(); }));
    c.appendChild(acao(agendada(t) ? 'Mudar horário' : 'Planejar num horário', () => escolherHorario(t, false)));
    if (agendada(t)) c.appendChild(acao('Tirar do horário', () => { t.Hora = ''; gravarLocal(); desenhar(); }));
    if (agendada(t) && !repete(t)) c.appendChild(acao('Adiar para amanhã', () => { t.Data = chave(maisDias(deChave(t.Data), 1)); gravarLocal(); desenhar(); }));
    c.appendChild(t.Aguardando ? acao('Tirar de Em aguardo', () => { t.Aguardando = false; gravarLocal(); desenhar(); })
      : acao('Mandar para Em aguardo', () => { const q = prompt('Aguardando quem ou o quê? (opcional)', t.AguardandoDe || ''); if (q === null) return; t.Hora = ''; t.Aguardando = true; t.AguardandoDe = q; gravarLocal(); desenhar(); }));
    c.appendChild(acao('Editar título', () => { const n = prompt('Título', t.Titulo); if (n && n.trim()) { t.Titulo = n.trim(); gravarLocal(); desenhar(); } }));
    if (t.Link) c.appendChild(acao('Abrir link', () => window.open(t.Link, '_blank')));
  }
  c.appendChild(acao(t.Fixa ? 'Excluir tarefa fixa' : '🗑 Não é minha', () => naoEMinha(t), true));
  abrirFolha(c);
}

function copiar(f) {
  return { Id: novoId(), Titulo: f.Titulo, Descricao: f.Descricao, TipoId: f.TipoId, Cor: f.Cor, Origem: 'Manual', Link: f.Link, Duracao: f.Duracao || 0,
    Concluida: false, Avisado: false, Aguardando: false, Fixa: false, Criada: new Date().toISOString().slice(0, 16).replace('T', ' ') };
}
function naoEMinha(t) {
  if (t.Origem && t.Origem !== 'Manual' && t.Ref) { const k = t.Origem + ':' + t.Ref; if (!D.NaoSaoMinhas.includes(k)) D.NaoSaoMinhas.push(k); }
  D.Tarefas = D.Tarefas.filter(x => x !== t); gravarLocal(); desenhar();
}

function escolherHorario(t, nova) {
  const c = el('div');
  c.appendChild(el('h3', null, 'Escolha o horário'));
  c.appendChild(el('div', 'sub', esc(t.Titulo) + ' · ' + dia.toLocaleDateString('pt-BR')));
  const g = el('div', 'horas-grade');
  horarios(dia).forEach(h => {
    const b = el('button', t.Hora === h && t.Data === chave(dia) ? 'atual' : '', h);
    b.onclick = () => {
      t.Data = repete(t) && agendada(t) ? t.Data : chave(dia); t.Hora = h; t.Aguardando = false; t.AvisadoEm = null;
      if (nova) D.Tarefas.push(t);
      fecharFolha(); gravarLocal(); aba = 'agenda'; desenhar();
    };
    g.appendChild(b);
  });
  c.appendChild(g);
  abrirFolha(c);
}

function adicionarNoHorario(h) {
  const c = el('div');
  c.appendChild(el('h3', null, h + ' · ' + dia.toLocaleDateString('pt-BR')));
  const inp = el('input'); inp.placeholder = 'Nova tarefa neste horário'; c.appendChild(inp);
  const ok = el('button', 'botao grande', 'Criar'); ok.style.marginTop = '10px';
  ok.onclick = () => {
    if (!inp.value.trim()) return;
    D.Tarefas.push({ Id: novoId(), Titulo: inp.value.trim(), TipoId: (D.Tipos[0] || {}).Id || 'tarefa', Origem: 'Manual', Data: chave(dia), Hora: h,
      Duracao: 0, Concluida: false, Avisado: false, Aguardando: false, Fixa: false, Criada: new Date().toISOString().slice(0, 16).replace('T', ' ') });
    fecharFolha(); gravarLocal(); desenhar();
  };
  c.appendChild(ok);
  const sem = D.Tarefas.filter(t => !t.Fixa && !t.Aguardando && !agendada(t) && !t.Concluida);
  const fixas = D.Tarefas.filter(t => t.Fixa);
  const usar = (t, copia) => { const x = copia ? copiar(t) : t; x.Data = chave(dia); x.Hora = h; x.Aguardando = false; if (copia) D.Tarefas.push(x); fecharFolha(); gravarLocal(); desenhar(); };
  if (fixas.length) { c.appendChild(el('label', null, 'Tarefas fixas')); fixas.forEach(t => c.appendChild(acao('📌 ' + esc(t.Titulo), () => usar(t, true)))); }
  if (sem.length) { c.appendChild(el('label', null, 'Tarefas sem horário')); sem.slice(0, 40).forEach(t => c.appendChild(acao(esc(t.Titulo), () => usar(t, false)))); }
  abrirFolha(c);
  setTimeout(() => inp.focus(), 250);
}

function adicionarRapida(texto) {
  const t = { Id: novoId(), TipoId: (D.Tipos[0] || {}).Id || 'tarefa', Origem: 'Manual', Duracao: 0, Concluida: false, Avisado: false, Aguardando: false, Fixa: false,
    Criada: new Date().toISOString().slice(0, 16).replace('T', ' ') };
  const m = /^(\d{1,2})[:h](\d{2})?\s+(.+)$/.exec(texto.trim());
  if (m && +m[1] < 24) { t.Data = chave(dia); t.Hora = String(+m[1]).padStart(2, '0') + ':' + (m[2] || '00'); texto = m[3]; }
  t.Titulo = texto.trim();
  D.Tarefas.push(t); gravarLocal(); desenhar();
}

function editarNota(n) {
  const c = el('div');
  const t = el('input'); t.placeholder = 'Título'; t.value = n ? n.Titulo || '' : '';
  const x = el('textarea'); x.rows = 10; x.value = n ? n.Texto || '' : ''; x.style.marginTop = '8px';
  const lb = el('div', 'linha-botoes');
  const ok = el('button', 'botao', 'Salvar');
  ok.onclick = () => {
    const agora = new Date().toLocaleString('pt-BR').slice(0, 17);
    if (n) { n.Titulo = t.value; n.Texto = x.value; n.Atualizada = agora; }
    else if (t.value.trim() || x.value.trim()) D.Notas.unshift({ Id: novoId(), Titulo: t.value || 'Nota', Texto: x.value, Atualizada: agora });
    fecharFolha(); gravarLocal(); desenhar();
  };
  lb.appendChild(ok);
  if (n) { const ex = el('button', 'botao perigo', 'Excluir'); ex.onclick = () => { if (confirm('Excluir a nota?')) { D.Notas = D.Notas.filter(y => y !== n); fecharFolha(); gravarLocal(); desenhar(); } }; lb.appendChild(ex); }
  c.append(t, x, lb);
  abrirFolha(c);
}

// ---------------------------------------------------------------- lembretes (com o app aberto)
function verificarAvisos() {
  const agora = new Date(), k = chave(hoje()), antes = A().AvisoAntesMin || 5;
  for (const t of D.Tarefas) {
    if (!ocorreEm(t, hoje()) || concluidaEm(t, hoje()) || t.AvisadoEm === k) continue;
    const h = new Date(hoje()); h.setMinutes(minutos(t.Hora));
    if (agora < new Date(h - antes * 60000) || agora > new Date(+h + 30 * 60000)) continue;
    t.AvisadoEm = k; gravarLocal(); avisar(t); break;
  }
}
function avisar(t) {
  const a = $('#aviso');
  a.innerHTML = '<span class="fraco pequeno">LEMBRETE · ' + esc(t.Hora) + '</span><b>' + esc(t.Titulo) + '</b><div class="linha-botoes"><button class="ok">✓ Concluir</button><button>Depois</button></div>';
  a.classList.remove('escondido');
  const bs = a.querySelectorAll('button');
  bs[0].onclick = () => { marcarConcluida(t, hoje(), true); gravarLocal(); a.classList.add('escondido'); desenhar(); };
  bs[1].onclick = () => a.classList.add('escondido');
  if (navigator.vibrate) navigator.vibrate([200, 100, 200]);
  // no iPhone a notificação só funciona pelo service worker (app adicionado à tela de início)
  try {
    if (window.Notification && Notification.permission === 'granted' && navigator.serviceWorker)
      navigator.serviceWorker.ready.then(r => r.showNotification('Marke Agenda · ' + t.Hora, { body: t.Titulo, icon: 'icone-192.png', tag: t.Id }));
  } catch (e) { }
}

// ---------------------------------------------------------------- início
function abrirApp() {
  $('#entrada').classList.add('escondido'); $('#app').classList.remove('escondido');
  $('#btConta').textContent = ((conta.nome || conta.email || '?').trim()[0] || '?').toUpperCase();
  desenhar();
}
function iniciar() {
  iniciarGoogle();
  $('#btEntrar').onclick = entrar;
  $('#btSinc').onclick = () => sincronizar(true);
  $('#btConta').onclick = abrirConta;
  $('#btAnt').onclick = () => { dia = maisDias(dia, -1); delete $('#conteudo').dataset.rolou; desenhar(); };
  $('#btProx').onclick = () => { dia = maisDias(dia, 1); delete $('#conteudo').dataset.rolou; desenhar(); };
  $('#btHoje').onclick = () => { dia = hoje(); delete $('#conteudo').dataset.rolou; desenhar(); };
  $('#fundoFolha').onclick = fecharFolha;
  document.querySelectorAll('.abas button').forEach(b => b.onclick = () => { aba = b.dataset.aba; $('#conteudo').scrollTop = 0; desenhar(); });
  if (conta) abrirApp(); else $('#entrada').classList.remove('escondido');
  setInterval(() => { verificarAvisos(); if (aba === 'agenda') desenhar(); }, 30000);
  setInterval(() => { if (tokenValido()) sincronizar(false); }, 120000);
  document.addEventListener('visibilitychange', () => { if (!document.hidden) { if (tokenValido()) sincronizar(false); desenhar(); } });
  if (window.Notification && Notification.permission === 'default') document.body.addEventListener('click', () => Notification.requestPermission(), { once: true });
  if ('serviceWorker' in navigator) navigator.serviceWorker.register('sw.js?v=7').then(r => r.update());
  verificarAvisos();
}
iniciar();
