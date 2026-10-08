"""Market dates follow the exchange clock (America/New_York), never the
machine's local timezone nor the UTC calendar day."""
from datetime import datetime, timezone
from zoneinfo import ZoneInfo

import config
import market_calendar

ET = ZoneInfo("America/New_York")


def test_market_today_uses_new_york_date_from_any_timezone():
    # Fri 2026-10-09 21:30 ET = Sat 10:30 in Tokyo = Sat 01:30 UTC
    tokyo = datetime(2026, 10, 10, 10, 30, tzinfo=ZoneInfo("Asia/Tokyo"))
    assert str(config.market_today(tokyo)) == "2026-10-09"
    sao_paulo = datetime(2026, 10, 9, 22, 30, tzinfo=ZoneInfo("America/Sao_Paulo"))
    assert str(config.market_today(sao_paulo)) == "2026-10-09"


def test_market_open_today_uses_exchange_day_not_utc_day():
    # Friday evening in New York is already Saturday in UTC.
    fri_evening = datetime(2026, 10, 9, 23, 30, tzinfo=ET).astimezone(timezone.utc)
    assert market_calendar.is_market_open_today(fri_evening) is True
    sat = datetime(2026, 10, 10, 12, 0, tzinfo=ET).astimezone(timezone.utc)
    assert market_calendar.is_market_open_today(sat) is False


def test_rth_window_handles_us_dst():
    # 10:00 ET is RTH both in EDT (October) and EST (December).
    for d in (datetime(2026, 10, 8, 10, 0, tzinfo=ET), datetime(2026, 12, 8, 10, 0, tzinfo=ET)):
        assert market_calendar.is_rth_now(d.astimezone(timezone.utc)) is True
    assert market_calendar.is_rth_now(datetime(2026, 12, 8, 16, 30, tzinfo=ET).astimezone(timezone.utc)) is False


def test_et_clock_string():
    t = datetime(2026, 12, 8, 15, 5, tzinfo=timezone.utc)  # 10:05 EST
    assert config.et_clock(t) == "10:05 ET"
