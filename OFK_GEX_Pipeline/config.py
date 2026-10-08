"""
config.py — Centralized paths and settings for the OFK GEX Pipeline.

All hardcoded paths previously scattered across the pipeline live here.
Override any path via environment variables (see .env.example).

Resolution order for each path:
  1. Environment variable (if set)
  2. Default = relative to PIPELINE_ROOT (this file's parent directory)
"""
# Timezone convention:
# All internal logic and JSON timestamps use UTC (datetime.now(timezone.utc)).
# Console logs use local time (datetime.now()) for readability.
# ATAS indicator displays use the chart timezone (set in ATAS preferences).
from __future__ import annotations
import json
import os
import shutil
from datetime import date, datetime, timezone
from zoneinfo import ZoneInfo
from pathlib import Path
from typing import Any, Dict

# ── Schema version (Block 7 — versioning + health check) ────────────────────
# Increment on every change to the full_levels_* JSON structure.
# The ATAS indicator reads this version and shows a warning on mismatch.
JSON_SCHEMA_VERSION: str = "1.0"


def compute_data_quality(full_dict: Dict[str, Any]) -> str:
    """Determine the overall data quality in full_levels_*.json.

    Returns:
      "ok"      : all main sources (CME + CBOE + VIX) present
      "partial" : at least one source missing (but not critical)
      "error"   : broken structure / empty dict
    """
    if not full_dict:
        return "error"
    has_cme  = bool(full_dict.get("total_gex"))
    has_cboe = bool(full_dict.get("atm_iv_intraday"))
    has_vix  = bool(full_dict.get("vix"))
    if has_cme and has_cboe and has_vix:
        return "ok"
    if has_cme or has_cboe:
        return "partial"
    return "error"


# ── Exchange clock ───────────────────────────────────────────────────────────
# Market dates (trade date, DTE, holidays, "today") follow New York — the
# exchange clock — whatever the machine's timezone: in Tokyo or São Paulo the
# local calendar day and the UTC day both differ from the US session day for
# part of the day. Needs the IANA database (`tzdata` package on Windows).
MARKET_TZ = ZoneInfo("America/New_York")


def market_now(ref: datetime | None = None) -> datetime:
    """Aware datetime in New York time (ref: aware datetime, default now)."""
    return (ref or datetime.now(timezone.utc)).astimezone(MARKET_TZ)


def market_today(ref: datetime | None = None) -> date:
    """The US session calendar date (New York), independent of local tz."""
    return market_now(ref).date()


def et_clock(ref: datetime | None = None) -> str:
    """'HH:MM ET' wall-clock string of New York time."""
    return market_now(ref).strftime("%H:%M ET")


# ── Roots ────────────────────────────────────────────────────────────────────
PIPELINE_ROOT: Path = Path(__file__).parent.resolve()

# Main data directory (where all JSON / PDF / raw outputs land).
# Override with env var GEX_DATA_DIR if you want to write elsewhere
# (e.g. C:\gex_agent\data, ~/AppData/Roaming/ATAS/data, …).
DATA_DIR: Path = Path(os.environ.get("GEX_DATA_DIR", PIPELINE_ROOT / "data"))

# Skills (markdown specs) consumed by the Claude agents.
SKILLS_DIR: Path = PIPELINE_ROOT / "skills"

# Historical snapshots (Phase 1.3 — daily archives of full_levels_*.json).
HISTORY_DIR: Path = DATA_DIR / "history"

# Intraday snapshots (Replay feature — 5min granularity, short retention).
INTRADAY_HISTORY_DIR: Path = DATA_DIR / "history" / "intraday"

# ── Per-instrument output paths ──────────────────────────────────────────────
# CME-only output (written by cme_*_browser_fetch.py).
NQ_GEX_JSON: Path = Path(os.environ.get("NQ_GEX_JSON", DATA_DIR / "NQ_gex_latest.json"))
ES_GEX_JSON: Path = Path(os.environ.get("ES_GEX_JSON", DATA_DIR / "ES_gex_latest.json"))

# CBOE-only intermediate output (written by data_fetcher_*.py).
NQ_LEVELS_JSON: Path = Path(os.environ.get("NQ_LEVELS_JSON", DATA_DIR / "levels_NQ.json"))
ES_LEVELS_JSON: Path = Path(os.environ.get("ES_LEVELS_JSON", DATA_DIR / "levels_ES.json"))

# Merged final output (consumed by ATAS C# indicators).
NQ_FULL_JSON: Path = Path(os.environ.get("NQ_FULL_JSON", DATA_DIR / "full_levels_NQ.json"))
ES_FULL_JSON: Path = Path(os.environ.get("ES_FULL_JSON", DATA_DIR / "full_levels_ES.json"))

# Claude agent intermediate files.
NQ_BRIEFING_JSON: Path = DATA_DIR / "briefing_NQ.json"
ES_BRIEFING_JSON: Path = DATA_DIR / "briefing_ES.json"
NQ_BRIEFING_RAW:  Path = DATA_DIR / "_briefing_NQ_raw.txt"
ES_BRIEFING_RAW:  Path = DATA_DIR / "_briefing_ES_raw.txt"
NQ_PROMPT_FILE:   Path = DATA_DIR / "_prompt_NQ.txt"
ES_PROMPT_FILE:   Path = DATA_DIR / "_prompt_ES.txt"

# ── CME browser window ───────────────────────────────────────────────────────
# CME's WAF (Akamai) blocks headless Chromium, so the scrape runs a real
# (headed) browser placed far off every monitor: Windows' virtual desktop
# stays within ±32767, no multi-monitor layout reaches -32000.
# Set OFK_BROWSER_VISIBLE=1 to bring the window on screen for debugging.
_BROWSER_VISIBLE = os.environ.get("OFK_BROWSER_VISIBLE") == "1"
# (No --disable-features here: Chrome honors only one such flag and
# Playwright already passes its own list.)
BROWSER_OFFSCREEN_ARGS = [] if _BROWSER_VISIBLE else ["--window-position=-32000,-32000"]


def foreground_window() -> int:
    """Handle of the window that has the keyboard focus (0 off Windows)."""
    if os.name != "nt":
        return 0
    import ctypes
    return ctypes.windll.user32.GetForegroundWindow()


def give_back_focus(previous: int) -> None:
    """A newly launched (off-screen) Chromium window takes the keyboard focus:
    keystrokes — trading hotkeys included — would go to an invisible window.
    Hand the focus back to `previous`, but only if the off-screen browser
    still holds it (never yank focus from a window the user picked meanwhile).
    """
    if os.name != "nt" or _BROWSER_VISIBLE or not previous:
        return
    import ctypes
    from ctypes import wintypes
    u, k = ctypes.windll.user32, ctypes.windll.kernel32
    cur = u.GetForegroundWindow()
    rect = wintypes.RECT()
    if not cur or cur == previous or not u.GetWindowRect(cur, ctypes.byref(rect)) or rect.left > -30000:
        return
    # SetForegroundWindow is refused to background processes unless their
    # input is attached to the current foreground thread.
    t_cur, t_me = u.GetWindowThreadProcessId(cur, None), k.GetCurrentThreadId()
    u.AttachThreadInput(t_me, t_cur, True)
    try:
        u.SetForegroundWindow(previous)
    finally:
        u.AttachThreadInput(t_me, t_cur, False)

# ── External tools ───────────────────────────────────────────────────────────
# Claude Code CLI (npm-installed). Override via env CLAUDE_CMD.
# Default: whatever `claude` resolves to on PATH (claude.exe from the native
# installer, claude.cmd from npm, or the Unix binary).
CLAUDE_CMD: str = os.environ.get("CLAUDE_CMD") or shutil.which("claude") or "claude"


def ensure_dirs() -> None:
    """Create DATA_DIR and HISTORY_DIR if missing. Idempotent."""
    DATA_DIR.mkdir(parents=True, exist_ok=True)
    HISTORY_DIR.mkdir(parents=True, exist_ok=True)
    INTRADAY_HISTORY_DIR.mkdir(parents=True, exist_ok=True)


# Daily snapshot retention. Beyond this → deletion.
# 380 days = 1 year + 15 days margin (enough for 252-day IV Rank).
HISTORY_MAX_DAYS: int = int(os.environ.get("GEX_HISTORY_MAX_DAYS", "380"))

# Intraday snapshot retention (replay feature). 7 days by default.
# 7 days × ~80 snapshots/day × ~50KB ≈ 28 MB per symbol — acceptable.
INTRADAY_HISTORY_MAX_DAYS: int = int(os.environ.get("GEX_INTRADAY_HISTORY_MAX_DAYS", "7"))


def cleanup_history(symbol: str, max_days: int = HISTORY_MAX_DAYS) -> int:
    """Remove {symbol}_full_levels_*.json snapshots older than max_days.
    Returns the number of files removed."""
    if not HISTORY_DIR.exists():
        return 0
    from datetime import datetime, timedelta, timezone
    cutoff = datetime.now(timezone.utc).date() - timedelta(days=max_days)
    pattern = f"{symbol}_full_levels_*.json"
    deleted = 0
    for snap in HISTORY_DIR.glob(pattern):
        try:
            # Extract YYYYMMDD from filename: {SYM}_full_levels_YYYYMMDD.json
            stem = snap.stem  # ES_full_levels_20260430
            date_str = stem.split("_")[-1]
            if len(date_str) != 8:
                continue
            snap_date = datetime.strptime(date_str, "%Y%m%d").date()
            if snap_date < cutoff:
                snap.unlink()
                deleted += 1
        except Exception:
            continue
    return deleted


def save_snapshot(symbol: str, full_dict: Dict[str, Any]) -> Path:
    """
    Persist a dated copy of the full levels dict into HISTORY_DIR.

    Filename: {symbol}_full_levels_{YYYYMMDD}.json
    Date source: full_dict['trade_date'] if present, else today.
    Same-day re-runs overwrite the existing snapshot (idempotent).

    Also calls cleanup_history() to enforce HISTORY_MAX_DAYS.

    Returns the path written.
    """
    trade_date = (full_dict.get("trade_date") or "").strip() or \
                 market_today().strftime("%Y%m%d")
    # CME format is already YYYYMMDD; if user injects ISO (YYYY-MM-DD), normalize.
    if "-" in trade_date:
        trade_date = trade_date.replace("-", "")
    HISTORY_DIR.mkdir(parents=True, exist_ok=True)
    path = HISTORY_DIR / f"{symbol}_full_levels_{trade_date}.json"
    path.write_text(json.dumps(full_dict, indent=2))
    # Automatic cleanup (rolling retention)
    cleanup_history(symbol)
    return path


def cleanup_intraday_history(symbol: str,
                             max_days: int = INTRADAY_HISTORY_MAX_DAYS) -> int:
    """Remove intraday {symbol}_full_levels_*_*.json snapshots older
    than max_days. Returns the number of files removed."""
    if not INTRADAY_HISTORY_DIR.exists():
        return 0
    from datetime import datetime, timedelta, timezone
    cutoff = datetime.now(timezone.utc).date() - timedelta(days=max_days)
    pattern = f"{symbol}_full_levels_*_*.json"
    deleted = 0
    for snap in INTRADAY_HISTORY_DIR.glob(pattern):
        try:
            # Stem: {SYM}_full_levels_YYYYMMDD_HHMM
            parts = snap.stem.split("_")
            if len(parts) < 5:
                continue
            date_str = parts[-2]
            if len(date_str) != 8:
                continue
            snap_date = datetime.strptime(date_str, "%Y%m%d").date()
            if snap_date < cutoff:
                snap.unlink()
                deleted += 1
        except Exception:
            continue
    return deleted


def save_intraday_snapshot(symbol: str, full_dict: Dict[str, Any]) -> Path:
    """
    Persist a timestamped intraday snapshot into INTRADAY_HISTORY_DIR.

    Filename: {symbol}_full_levels_{YYYYMMDD}_{HHMM}.json
    Timestamp = now (local time, minute granularity).

    Also calls cleanup_intraday_history() to enforce
    INTRADAY_HISTORY_MAX_DAYS.

    Returns the path written.
    """
    from datetime import datetime, timezone
    now = datetime.now(timezone.utc)
    date_str = now.strftime("%Y%m%d")
    time_str = now.strftime("%H%M")
    INTRADAY_HISTORY_DIR.mkdir(parents=True, exist_ok=True)
    path = INTRADAY_HISTORY_DIR / f"{symbol}_full_levels_{date_str}_{time_str}.json"
    path.write_text(json.dumps(full_dict, indent=2))
    cleanup_intraday_history(symbol)
    return path


def compute_iv_rank(symbol: str, current_iv: float, lookback_days: int = 252,
                    iv_field: str = "atm_iv_intraday") -> Dict[str, Any]:
    """IV Rank: position of current_iv within the past lookback_days window.

    IVR = (IV - IVmin) / (IVmax - IVmin) × 100  → 0-100%
    IVR < 30 = low vol, complacent market
    IVR > 70 = high vol, stress, prefer vol mean-reversion

    Reads {symbol}_full_levels_*.json snapshots in HISTORY_DIR.
    Returns dict with ivr, iv_min, iv_max, n_samples, status.
    Status: 'ok' if >= 20 days, 'partial' if 5-19, 'insufficient' if < 5.
    """
    if not HISTORY_DIR.exists() or current_iv <= 0:
        return {"ivr": None, "status": "insufficient", "n_samples": 0}

    from datetime import datetime, timedelta, timezone
    cutoff = datetime.now(timezone.utc).date() - timedelta(days=lookback_days)
    pattern = f"{symbol}_full_levels_*.json"

    ivs = []
    for snap in HISTORY_DIR.glob(pattern):
        try:
            stem = snap.stem
            date_str = stem.split("_")[-1]
            if len(date_str) != 8:
                continue
            snap_date = datetime.strptime(date_str, "%Y%m%d").date()
            if snap_date < cutoff:
                continue
            data = json.loads(snap.read_text())
            iv = data.get(iv_field)
            # Fallback to the legacy name if present
            if iv is None or iv <= 0:
                iv = data.get("atm_iv_front")
            if iv and iv > 0:
                ivs.append(iv)
        except Exception:
            continue

    n = len(ivs)
    if n < 5:
        return {"ivr": None, "status": "insufficient", "n_samples": n,
                "iv_min": None, "iv_max": None}

    iv_min = min(ivs)
    iv_max = max(ivs)
    if iv_max - iv_min < 1e-6:
        ivr = 50.0
    else:
        ivr = (current_iv - iv_min) / (iv_max - iv_min) * 100.0
        ivr = max(0.0, min(100.0, ivr))

    status = "ok" if n >= 20 else "partial"
    return {
        "ivr"       : round(ivr, 1),
        "iv_min"    : round(iv_min, 4),
        "iv_max"    : round(iv_max, 4),
        "n_samples" : n,
        "status"    : status,
        "lookback"  : lookback_days,
        "field"     : iv_field,
    }


def update_session_log(symbol: str, trade_date: str, spot: float) -> None:
    """Update today's snapshot with open_rth_spot/close_rth_spot.

    Logic:
    - If open_rth_spot does not exist (first call of the day during RTH):
      the current spot becomes the open.
    - Always: close_rth_spot = current spot (the last refresh wins,
      so close = last refresh before session end).
    - intraday_refresh_count incremented.

    Called by run_intraday_refresh.py on each CBOE refresh.
    """
    if not trade_date or spot <= 0:
        return
    if "-" in trade_date:
        trade_date = trade_date.replace("-", "")
    snap_path = HISTORY_DIR / f"{symbol}_full_levels_{trade_date}.json"
    if not snap_path.exists():
        return
    try:
        data = json.loads(snap_path.read_text())
        from datetime import datetime, timezone
        now_iso = datetime.now(timezone.utc).isoformat()
        if not data.get("open_rth_spot"):
            data["open_rth_spot"] = spot
            data["open_rth_time"] = now_iso
        data["close_rth_spot"] = spot
        data["close_rth_time"] = now_iso
        data["intraday_refresh_count"] = (data.get("intraday_refresh_count") or 0) + 1
        snap_path.write_text(json.dumps(data, indent=2))
    except Exception:
        pass


# Auto-create on import so downstream code can write without prelude.
ensure_dirs()
