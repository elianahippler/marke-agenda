// Guarda os arquivos do app para abrir mesmo sem internet. Dados não passam por aqui.
const VERSAO = 'marke-agenda-2';
const ARQUIVOS = ['./', 'index.html', 'estilo.css', 'app.js', 'manifest.webmanifest', 'icone-180.png', 'icone-192.png', 'icone-512.png'];

self.addEventListener('install', e => { e.waitUntil(caches.open(VERSAO).then(c => c.addAll(ARQUIVOS))); self.skipWaiting(); });
self.addEventListener('activate', e => {
  e.waitUntil(caches.keys().then(ks => Promise.all(ks.filter(k => k !== VERSAO).map(k => caches.delete(k)))));
  self.clients.claim();
});
self.addEventListener('fetch', e => {
  const url = new URL(e.request.url);
  if (url.origin !== location.origin || e.request.method !== 'GET') return;   // Google e Drive vão direto
  // primeiro a rede (para pegar versão nova), sem internet usa a cópia
  e.respondWith(fetch(e.request).then(r => { const c = r.clone(); caches.open(VERSAO).then(x => x.put(e.request, c)); return r; })
    .catch(() => caches.match(e.request)));
});
self.addEventListener('notificationclick', e => { e.notification.close(); e.waitUntil(self.clients.openWindow('./')); });
