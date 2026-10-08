# OFK GEX Pipeline

Python pipeline that produces the Greeks Exposure (GEX/VEX/CEX/DEX) levels for
NQ and ES E-mini futures, then read by the C# ATAS indicators of the OFK suite.

---

## Architecture

```
OFK_GEX_Pipeline/
├── config.py                   # centralized paths (override via env vars)
├── cme_NQ_browser_fetch.py     # scrape CME NQ options (headless Playwright)
├── cme_ES_browser_fetch.py     # scrape CME ES options (headless Playwright)
├── data_fetcher_NQ.py          # CBOE QQQ → missing metrics (NQ proxy)
├── data_fetcher_ES.py          # CBOE SPY → missing metrics (ES proxy)
├── claude_agent_{NQ,ES}.py     # AI briefing via Claude Code CLI
├── generate_pdf_{NQ,ES}.py     # PDF rendering of the briefing
├── run_morning_{NQ,ES}.py      # orchestrator (CME → CBOE → merge → AI → PDF)
├── skills/                     # markdown specs for the Claude agent
├── data/                       # runtime outputs (gitignored)
│   └── samples/                # output examples (committed)
├── requirements.txt
└── .gitignore
```

---

## Pipeline (5 stages)

```
1. CME scrape   → cme_*_browser_fetch.py    →  {NQ,ES}_gex_latest.json   (native Greeks)
2. CBOE scrape  → data_fetcher_*.py         →  levels_{NQ,ES}.json       (Max Pain, PCR, Top OI, EM)
3. Merge        → run_morning_*.py          →  full_levels_{NQ,ES}.json  (what ATAS reads)
4. AI briefing  → claude_agent_*.py         →  briefing_{NQ,ES}.json
5. PDF          → generate_pdf_*.py         →  briefing_{NQ,ES}_YYYY-MM-DD.pdf
```

---

## Installation

```bash
pip install -r requirements.txt
playwright install chromium
```

For the AI briefing, install the Claude Code CLI (`npm i -g @anthropic-ai/claude-code`).
It is found on `PATH` (`claude`, `claude.exe` or npm's `claude.cmd`); set the
environment variable `CLAUDE_CMD` only to force another binary.

---

## Running

```bash
# Full NQ pipeline (≈ 2-3 min)
python run_morning_NQ.py

# Full ES pipeline
python run_morning_ES.py

# Individual stages (for debug)
python cme_NQ_browser_fetch.py
python data_fetcher_NQ.py
python claude_agent_NQ.py
python generate_pdf_NQ.py
```

---

## Configuration

All paths are defined in `config.py` with a local default + environment
variable override. You do **not** need to modify the code to redirect outputs
elsewhere — set environment variables (no `.env` file is read): `GEX_DATA_DIR`,
`NQ_FULL_JSON`, `ES_FULL_JSON`, `CLAUDE_CMD`, `GEX_LOG_LEVEL`, `GEX_LOG_FILE`,
`GEX_HISTORY_MAX_DAYS`, `GEX_INTRADAY_HISTORY_MAX_DAYS`, `OFK_BROWSER_VISIBLE=1`
(show the CME browser window, normally kept off-screen).

Market dates follow New York time (`config.MARKET_TZ`) on any machine timezone.

---

## Historical snapshots

On every successful run, the pipeline duplicates `full_levels_*.json` into
`data/history/` with a dated name:

```
data/history/NQ_full_levels_20260501.json
data/history/ES_full_levels_20260501.json
```

Re-runs of the same day overwrite the existing snapshot (idempotent). This
history will later feed the IVR (IV Rank over 252 days, Phase 3).

---

## Output for ATAS

The C# indicators `OFK_NQ_GEX_Levels.cs` and `OFK_ES_GEX_Levels.cs` read by
default from `full_levels_{NQ,ES}.json`. Configure the `JsonPath` in the ATAS
indicator settings to point to the file produced by this pipeline, for example:

```
<repo path>/OFK_GEX_Pipeline/data/full_levels_NQ.json
```

Or redirect the pipeline to write directly to the ATAS folder via
the `NQ_FULL_JSON` environment variable.
