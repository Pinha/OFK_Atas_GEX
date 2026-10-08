"""
gamma_profile.py — Gamma Flip and Vanna Flip from the dealer exposure profile.

The Gamma Flip is the PRICE where total dealer gamma changes sign
(GUIDE_GEX_LEVELS.md: above it = positive regime, below it = negative).
Summing per-strike GEX measured at the current spot cannot find it: that
sum is one number per strike, not a function of price. Here the total GEX
is recomputed for hypothetical spots S (each option's Black-Scholes gamma
re-evaluated at S) and the zero crossing nearest the real spot is returned.

The Vanna Flip ("level where vanna changes sign") is found the same way on
the vanna profile, with the collectors' VEX convention (calls·vanna + puts·|vanna|).

legs: iterable of (K, T_years, c_oi, c_iv, p_oi, p_iv) — one per strike and
expiration, same dealer convention as compute_greek_exposures.
"""
from __future__ import annotations

import math
from typing import Iterable, List, Optional, Tuple

Leg = Tuple[float, float, float, float, float, float]


def _gamma(S: float, K: float, T: float, sigma: float, r: float) -> float:
    if S <= 0 or K <= 0 or T <= 0 or sigma <= 0:
        return 0.0
    sq = sigma * math.sqrt(T)
    d1 = (math.log(S / K) + (r + 0.5 * sigma * sigma) * T) / sq
    return math.exp(-0.5 * d1 * d1) / (math.sqrt(2 * math.pi) * S * sq)


def _vanna(S: float, K: float, T: float, sigma: float, r: float) -> float:
    if S <= 0 or K <= 0 or T <= 0 or sigma <= 0:
        return 0.0
    sq = sigma * math.sqrt(T)
    d1 = (math.log(S / K) + (r + 0.5 * sigma * sigma) * T) / sq
    return -math.exp(-0.5 * d1 * d1) / math.sqrt(2 * math.pi) * (d1 - sq) / sigma


def net_vanna(legs: List[Leg], S: float, r: float = 0.045) -> float:
    """Dealer vanna (calls·vanna + puts·|vanna|, as in VEX) at spot S."""
    return sum(c_oi * _vanna(S, K, T, c_iv, r) + p_oi * abs(_vanna(S, K, T, p_iv, r))
               for K, T, c_oi, c_iv, p_oi, p_iv in legs)


def net_gamma(legs: List[Leg], S: float, r: float = 0.045) -> float:
    """Dealer net gamma (calls − puts, OI-weighted) at hypothetical spot S."""
    return sum(c_oi * _gamma(S, K, T, c_iv, r) - p_oi * _gamma(S, K, T, p_iv, r)
               for K, T, c_oi, c_iv, p_oi, p_iv in legs)


def zero_gamma(legs: Iterable[Leg], spot: float, r: float = 0.045,
               span: float = 0.15, steps: int = 240) -> Optional[float]:
    """Gamma Flip: zero crossing of the gamma profile nearest to spot."""
    return _zero_crossing(net_gamma, legs, spot, r, span, steps)


def zero_vanna(legs: Iterable[Leg], spot: float, r: float = 0.045,
               span: float = 0.15, steps: int = 240) -> Optional[float]:
    """Vanna Flip: zero crossing of the vanna profile nearest to spot."""
    return _zero_crossing(net_vanna, legs, spot, r, span, steps)


def _zero_crossing(profile, legs, spot, r, span, steps) -> Optional[float]:
    """Zero crossing of profile(legs, S) nearest to spot, linearly
    interpolated on a ±span grid. None if the profile keeps one sign."""
    legs = [l for l in legs if (l[2] > 0 or l[4] > 0) and l[0] > 0]
    if spot <= 0 or not legs:
        return None
    lo, hi = spot * (1 - span), spot * (1 + span)
    grid = [lo + (hi - lo) * i / steps for i in range(steps + 1)]
    vals = [profile(legs, s, r) for s in grid]
    crossings = []
    for (s0, v0), (s1, v1) in zip(zip(grid, vals), zip(grid[1:], vals[1:])):
        if v0 == 0:
            crossings.append(s0)
        elif (v0 < 0) != (v1 < 0):
            crossings.append(s0 + (s1 - s0) * v0 / (v0 - v1))
    if not crossings:
        return None
    return round(min(crossings, key=lambda s: abs(s - spot)), 2)


if __name__ == "__main__":
    # Self-check: puts below, calls above → flip between them, regardless of
    # where the spot sits; one-sided book → no flip (None, never the spot).
    T = 30 / 365
    book = [(95.0, T, 0, 0.2, 1000, 0.2), (105.0, T, 1000, 0.2, 0, 0.2)]
    f = zero_gamma(book, spot=103.0)
    assert f is not None and 99.0 < f < 101.0, f
    assert abs(zero_gamma(book, spot=97.0) - f) < 1e-6
    assert net_gamma(book, 110.0) > 0 > net_gamma(book, 90.0)
    assert zero_gamma([(100.0, T, 500, 0.2, 0, 0.2)], spot=100.0) is None
    assert zero_gamma([], spot=100.0) is None
    # Vanna of a long call is + below the strike and − above → flip near K.
    v = zero_vanna([(100.0, T, 1000, 0.2, 0, 0.2)], spot=95.0)
    assert v is not None and 98.0 < v < 102.0, v
    assert zero_vanna([(100.0, T, 0, 0.2, 1000, 0.2)], spot=100.0) is None  # |put vanna| ≥ 0
    print("gamma_profile self-check OK — gamma flip", f, "vanna flip", v)
