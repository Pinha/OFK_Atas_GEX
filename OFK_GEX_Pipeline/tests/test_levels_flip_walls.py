"""Gamma/Vanna Flip from the exposure profile + side-aware intraday walls."""
import pytest

import data_fetcher_ES
import data_fetcher_NQ
from gamma_profile import zero_gamma, zero_vanna

T = 30 / 365


def _c(strike, typ, gamma, oi, dte=1):
    return {"strike": strike, "type": typ, "gamma": gamma, "oi": oi, "dte": dte}


@pytest.mark.parametrize("mod", [data_fetcher_NQ, data_fetcher_ES])
def test_walls_intraday_bracket_spot_when_atm_dominates(mod):
    # 2026-10-08 ES case: SPY 775 (ATM, spot 773.34) had the largest call AND
    # put gamma → CW == PW == 775, which disables the Context Score walls/GF.
    spot = 773.34
    contracts = [
        _c(775, "call", 0.09, 9000), _c(775, "put", 0.09, 9000),
        _c(780, "call", 0.05, 6000), _c(770, "put", 0.05, 7000),
        _c(765, "call", 0.01, 100),  _c(790, "put", 0.01, 100),
        _c(800, "call", 0.50, 9999, dte=30),  # beyond max_dte: ignored
    ]
    w = mod.calc_walls_intraday(contracts, spot)
    assert w["call_wall_intraday"] == 775   # resistance: at/above spot
    assert w["put_wall_intraday"] == 770    # support: at/below spot
    assert w["call_wall_intraday"] > w["put_wall_intraday"]


@pytest.mark.parametrize("mod", [data_fetcher_NQ, data_fetcher_ES])
def test_walls_intraday_one_sided_book_falls_back(mod):
    contracts = [_c(100, "call", 0.05, 500), _c(105, "call", 0.04, 400)]
    w = mod.calc_walls_intraday(contracts, 110.0)  # no call strike ≥ spot
    assert w["call_wall_intraday"] == 100


def test_gamma_flip_between_put_and_call_mass():
    book = [(95.0, T, 0, 0.2, 1000, 0.2), (105.0, T, 1000, 0.2, 0, 0.2)]
    f = zero_gamma(book, spot=103.0)
    assert f is not None and 99.0 < f < 101.0
    assert zero_gamma(book, spot=97.0) == pytest.approx(f)


def test_flip_none_instead_of_spot_when_no_sign_change():
    # The old algorithms returned the spot itself here (a fake level).
    assert zero_gamma([(100.0, T, 500, 0.2, 0, 0.2)], spot=100.0) is None
    assert zero_vanna([(100.0, T, 0, 0.2, 1000, 0.2)], spot=100.0) is None
