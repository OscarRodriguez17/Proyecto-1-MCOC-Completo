# -*- coding: utf-8 -*-
"""Tests del reparto tributario por viga del Edificio B (Paso 2): conservación
y contrato de cargas.vigas para el visor."""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "src"))

from edificio_b.construir import build_model
from edificio_b import tributario
from edificio_b import cargas as C


def _bbox_area(M):
    xs = [c[0] for c in M.coord.values()]
    ys = [c[1] for c in M.coord.values()]
    return (max(xs) - min(xs)) * (max(ys) - min(ys))


def test_tributaria_conserva():
    """El área tributaria cubre ≥99% de la huella y G = Q_G·área (coherencia)."""
    M = build_model()
    at, g, _ = tributario.resumen(M)
    assert at >= 0.99 * _bbox_area(M) * 5
    assert abs(g - C.Q_G_LOSA * at) < 1.0


def test_vigas_bien_formadas():
    M = build_model()
    vigas = tributario.tributaria_por_viga(M)
    assert len(vigas) > 100
    for v in vigas:
        assert v["area"] > 0 and v["G"] > 0 and v["L"] > 0
        assert v["tipo"] in ("vigas_x", "vigas_y")
        assert abs(v["qG"] - v["G"] / v["L"]) < 1e-2   # carga lineal = total/L
