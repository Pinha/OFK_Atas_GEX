# OFK_Atas_GEX — ATAS Indicators

ATAS indicators that read the GEX/options levels produced by the Python pipeline `OFK_GEX_Pipeline/`.

This branch targets **ATAS X**, whose loader rejects WPF indicators: the panel is drawn on the
chart (`OnRender`) and its buttons are clicked on the chart (`ProcessMouseClick`).

---

## Installation

The repository can live in **any folder**. The indicators' default paths come from the
environment variable `OFK_GEX_HOME` (the repository folder); without it they default to
`C:\OFK_Atas_GEX\` (`~/OFK_Atas_GEX` outside Windows). Every path stays editable in each
indicator's settings.

1. Clone or extract the repository, e.g. to `D:\Trading\OFK_Atas_GEX\`
2. If that is not `C:\OFK_Atas_GEX\`, set the variable once (then restart ATAS):
   `[Environment]::SetEnvironmentVariable('OFK_GEX_HOME', 'D:\Trading\OFK_Atas_GEX', 'User')`
3. Copy `dist\OFK_Atas_GEX.dll` (ATAS X build) to `%APPDATA%\ATAS X\Indicators\` — ATAS X
   hot-reloads it, or build it yourself (see below)
4. Install Python dependencies: `cd OFK_GEX_Pipeline && pip install -r requirements.txt && playwright install chromium`
5. Indicators appear under **OFK Suite**

---

## Included indicators

| File | Display name | Description |
|---|---|---|
| `OFK_NQ_GEX_Levels.cs` | OFK NQ GEX Levels | NQ GEX/options levels (walls, gamma flip, DEX, 0DTE, IV, VIX), on-chart panel with buttons and intraday replay |
| `OFK_ES_GEX_Levels.cs` | OFK ES GEX Levels | Same for ES E-mini S&P500 |
| `OFK_NQ_ContextScore.cs` | OFK NQ Context Score | Directional score -100/+100 based on GEX + VIX + macro |
| `OFK_ES_ContextScore.cs` | OFK ES Context Score | Same for ES |
| `OFK_GexShared.cs` | (lib) | JSON loader (`GexLoader`), environment/clock (`OfkEnv`), utilities |

**Namespace**: `OFK_GEX` · **Assembly**: `OFK_Atas_GEX.dll` · **ATAS category**: `OFK Suite`

Session rules (last RTH hour, 0DTE pin window, replay day, "today" alert stats) follow the
exchange clock — America/New_York, DST-aware — whatever the machine's timezone.

---

## Build

Prerequisites: .NET 10 SDK and ATAS X installed.

```bash
cd OFK_ATAS
dotnet build -c Release
# ATAS installed elsewhere?  dotnet build -c Release -p:ATASPath="D:\Apps\ATAS X"
```

Produces `bin\Release\net10.0-windows\OFK_Atas_GEX.dll`; copy it to `%APPDATA%\ATAS X\Indicators\`.
If ATAS does not list the indicators, check `%APPDATA%\ATAS X\Logs\app_*.log` for
`AssemblyPatcher` / `Skipped loading` lines.

---

## Python pipeline

The pipeline that produces the JSON files lives in `OFK_GEX_Pipeline/`. See
`OFK_GEX_Pipeline/CLAUDE.md` for its architecture and `docs/integration_handoff/` for the
output contract. The indicators read `<OFK_GEX_HOME>\OFK_GEX_Pipeline\data\full_levels_{NQ,ES}.json`
by default (group `01.Source`, parameter `JSON Path`).

---

## Documentation

- `OFK_GEX_Pipeline/GUIDE_GEX_LEVELS.md` — plain-English guide to reading the levels
- `OFK_GEX_Pipeline/CLAUDE.md` — Python pipeline architecture
- `docs/integration_handoff/` — integration contract for external consumers
