# Marke Agenda

Agenda do dia com tarefas, chamados do TomTicket, atendimentos do Markedesk, eventos do Google Agenda e notas. Funciona no computador e no celular, sincronizada pelo Google Drive de cada pessoa.

## Celular (iPhone e Android)

Abra **https://elianahippler.github.io/marke-agenda/** no Safari (iPhone) ou no Chrome (Android), entre com a conta Google e use **Compartilhar > Adicionar à Tela de Início**.

## Computador (Windows)

Baixe e rode o instalador: [pc/Instalar Marke Agenda.exe](https://github.com/elianahippler/marke-agenda/raw/main/pc/Instalar%20Marke%20Agenda.exe)

Na primeira vez, entre com a conta Google (o mesmo login do celular). O Client ID e o Client Secret do Google são colados na tela de entrada e ficam só no computador.

## Onde ficam os dados

- No computador: na pasta `dados` ao lado do programa, cifrados pela conta do Windows.
- Na nuvem: num arquivo escondido do Google Drive de cada pessoa (pasta do aplicativo), que só a Marke Agenda enxerga.
- Nada de dados, senhas ou tokens fica neste repositório.

## Estrutura

- Raiz: app do celular (página instalável).
- `pc/`: app do computador em C# (.NET Framework 4, compila com o `build.bat`, sem instalar nada).
