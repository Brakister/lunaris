# Lunaris

Lunaris is an extremely fast native Windows 10/11 application launcher.
It is activated with the global hotkey `CTRL + ALT + SPACE` and lets you search and run
applications, files, commands, URLs and more with a single keystroke.

Built entirely in C# / .NET 8 (WPF, MVVM, SQLite, Serilog). No Electron, no WinForms.

## Installation

### Option 1 — Installer (recommended)

1. Download `Lunaris-Setup.exe` (see the `artifacts/` folder in this repo, or a Release).
2. Run it and follow the wizard (installer is self-contained — no .NET runtime needed).
3. A shortcut is created in the Start Menu (and optionally on the Desktop).
4. On first launch Lunaris registers the global hotkey `CTRL + ALT + SPACE`,
   shows a System Tray icon and starts indexing your apps and files.
5. Uninstall via Windows **Settings → Apps** (program data in
   `%LocalAppData%\Lunaris` is removed too).

> Tip: enable "Iniciar com o Windows" in the settings (or check the
> "Iniciar o Lunaris junto com o Windows" box during install) to have Lunaris
> available right after boot.

### Option 2 — Run from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0):

```bash
git clone https://github.com/Brakister/lunaris.git
cd lunaris
dotnet restore
dotnet run --project Lunaris/Lunaris.csproj
```

Or build a release and run the executable:

```bash
dotnet build Lunaris.slnx -c Release
.\Lunaris\bin\Release\net8.0-windows\Lunaris.exe
```

## First steps

- Press `CTRL + ALT + SPACE` to toggle the launcher from anywhere.
- Type to search; `Enter` runs the selected result, `Esc` hides the window,
  `↑`/`↓` navigate, `ALT+D` favorites a result.
- Right-click the tray icon for Open / Settings / Pause indexing / Exit.
- Change the hotkey, theme, indexed folders and commands in the settings window.

## Features

- **Global hotkey** (`CTRL + ALT + SPACE`, configurable) toggles the launcher from anywhere.
- **Fuzzy search** – accent- and case-insensitive matching with smart ranking
  (exact > prefix > word-start > acronym > subsequence).
- **Search providers** (all local and instant):
  - Installed applications (Start Menu, registry uninstall entries, UWP apps)
  - Indexed files (Desktop, Documents, Downloads, Pictures, Videos, Music)
  - Calculator – safe recursive-descent parser (`2+2`, `sqrt(144)`, `25%`, `200+10%`)
  - Custom commands and aliases (fully user-configurable)
  - URLs and bare domains (`example.com` opens in the browser)
  - Web search with bangs (`g query`, `yt query`, `wiki query`, `so query`, `gh query`, `maps query`, ...)
  - Downloads (`d url` baixa qualquer arquivo, `dv url` baixa vídeo em MP4, `d3 url` baixa áudio em MP3 — com yt-dlp/ffmpeg auto-instalados)
  - `ms-settings:` pages and system tools (`shutdown`, `restart`, `lock`, etc.)
  - History and favorites
  - Unit conversions (length, mass, volume, data, speed, temperature)
  - Tools: password generator, hash calculator, JSON formatter/validator, current time/date (`hora`, `data`)
  - Clipboard history (optional, off by default)
- **Ranking** – combines match quality with usage count, recency and favorites.
- **History & favorites** – usage is stored locally; `ALT+D` favorites a result.
- **System Tray** – icon with menu (Open, Settings, Start with Windows, Exit).
- **Single instance** – a second launch only reveals the existing window.
- **Settings window** – theme (System/Dark/Light), hotkey, index folders,
  max results, custom commands, clipboard, theme accent, start with Windows.
- **Dark & light themes** with a configurable accent color.
- Everything is stored locally in a SQLite database.

## Requirements

- Windows 10 or 11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Build

```bash
dotnet restore
dotnet build Lunaris.slnx -c Release
```

## Run

```bash
dotnet run --project Lunaris/Lunaris.csproj
```

Press `CTRL + ALT + SPACE` to toggle the launcher. `Enter` runs the selected result,
`Esc` hides the window, arrow keys navigate.

## Tests

```bash
dotnet test tests/Lunaris.Tests/Lunaris.Tests.csproj
```

## Publish

```powershell
.\scripts\publish.ps1
```

Produces a self-contained single-folder build in `artifacts/publish/` and,
if Inno Setup is installed, a Windows installer in `artifacts/`.

## Installer (optional)

If [Inno Setup](https://jrsoftware.org/isinfo.php) 6 is installed, run:

```bash
iscc installer/Lunaris.iss
```

The installer asks whether Lunaris should start with Windows (checkbox checked
by default). The same option is available in Settings → Geral.

## Data & logs

- Database: `%LocalAppData%\Lunaris\lunaris.db` (SQLite, WAL mode)
- Logs: `%LocalAppData%\Lunaris\logs\lunaris-*.log` (Serilog)

## Project layout

```
Lunaris/
  Program.cs                 Entry point, DI wiring (Host builder)
  App.xaml(.cs)              Application resources, startup/shutdown logic
  Core/                      Models, interfaces, services (search, rank, settings...)
  Infrastructure/            SQLite, migrations, Win32 interop, indexing, tray, hotkey
  Search/                    The search providers
  UI/                        WPF views, view models, themes, converters
  Assets/Lunaris.ico         Application icon
tests/Lunaris.Tests/         xUnit unit tests
scripts/publish.ps1          Publish helper
installer/Lunaris.iss        Inno Setup script
```

## Roadmap

- Screenshot capture, system monitor, developer tools provider
- Currency conversion (online provider, later)
- Plugin system

## Versionamento

> **REGRA: SEMPRE que alterar algo (bug fix, feature, build, settings), incremente a versão.**
> Nunca commite nem crie Release com a versão antiga.

- Versão atual: **1.6.0**

Onde alterar a versão (todos juntos):
- `Lunaris/Lunaris.csproj` → `<Version>`
- `installer/Lunaris.iss` → `#define MyAppVersion`
- `Lunaris/UI/Views/AboutWindow.xaml` → texto "Versão x.y.z"
- `Lunaris/UI/Views/SettingsWindow.xaml` → rodapé "Lunaris Launcher x.y.z"
- `Lunaris/app.manifest` → `assemblyIdentity version`
- `Lunaris/App.xaml.cs` e `Lunaris/Infrastructure/Update/UpdateService.cs` → fallback "x.y.z"

Depois de alterar a versão: `dotnet build` + `dotnet test`, republish com `scripts/publish.ps1`
(regenera o `artifacts/Lunaris-Setup.exe`) e `git commit`/`push`.

Para o auto-update funcionar: crie uma **Release** no GitHub com a tag `v<versão>` e anexe
o `artifacts/Lunaris-Setup.exe` como asset. O app detecta versões novas automaticamente
ou pelo menu do tray → "Verificar atualizações".

### Desempenho

- O índice de arquivos fica em SQLite e é re-scanneado no máximo uma vez a cada 12h
  (ou via tray → "Reindexar"). Em startups seguintes o app usa o índice persistido.
- A indexação roda em prioridade baixa, para não disputar CPU com seus apps.
- CPU ociosa: ~0%. RAM típica: ~70–130 MB em repouso, ~120–200 MB com o launcher aberto.
