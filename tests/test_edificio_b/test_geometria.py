# -*- coding: utf-8 -*-
"""Tests de la geometría del Edificio B (coherencia del dataset)."""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "src"))

from edificio_b import datos_edificio as D


def test_niveles():
    zs = [z for z, _ in D.NIVELES]
    assert zs == sorted(zs)
    # espaciamiento uniforme 3.96 m sobre la base
    dif = [round(zs[i + 1] - zs[i], 2) for i in range(len(zs) - 1)]
    assert all(d == 3.96 for d in dif)
    assert D.Z_BASE == zs[0]


def test_pilares():
    assert len(D.PILARES) == 8
    assert D.SEC_PILAR == (0.70, 0.70)


def test_muros_definidos():
    assert len(D.MUROS_V) + len(D.MUROS_H) == 8
    for (x, y0, y1, e) in D.MUROS_V:
        assert y1 > y0 and 0.1 <= e <= 0.7
    for (y, x0, x1, e) in D.MUROS_H:
        assert x1 > x0 and 0.1 <= e <= 0.7


def test_seccion_viga():
    assert D.seccion_viga(0.60) == D.SEC_VIGA_PRIN
    assert D.seccion_viga(0.40) == D.SEC_VIGA_MED
    assert D.seccion_viga(0.30) == D.SEC_VIGA_SEC


def test_material():
    assert D.E_CONCRETO > 2e7          # kN/m²
    assert 0.15 <= D.NU <= 0.25