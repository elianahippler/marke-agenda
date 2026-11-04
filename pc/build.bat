@echo off
rem Compila a Marke Agenda com o C# que ja vem no Windows (.NET 4.x). Nao precisa instalar nada.
rem Gera nesta pasta:
rem   Marke Agenda.exe             - o programa (os dados ficam em "dados\" ao lado dele)
rem   Instalar Marke Agenda.exe    - instalador unico (leva o programa dentro)
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REFS=/reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Security.dll
set FONTES=Modelo.cs Usuarios.cs Integracoes.cs Tela.cs Dialogos.cs Lembrete.cs Escuros.cs Sincronia.cs Google.cs

"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /win32icon:marke-agenda.ico /main:MarkeAgenda.Programa /out:"Marke Agenda.exe" %REFS% %FONTES%
if errorlevel 1 (echo. & echo FALHOU A COMPILACAO & exit /b 1)
echo Gerado: "%~dp0Marke Agenda.exe"

"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /win32icon:marke-agenda.ico /main:MarkeAgenda.ProgramaInstalador /out:"Instalar Marke Agenda.exe" ^
  /resource:"Marke Agenda.exe",MarkeAgenda.exe %REFS% %FONTES% Instalador.cs
if errorlevel 1 (echo. & echo FALHOU A COMPILACAO DO INSTALADOR & exit /b 1)
echo Gerado: "%~dp0Instalar Marke Agenda.exe"
