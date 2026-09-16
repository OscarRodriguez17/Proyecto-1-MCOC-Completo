# -*- coding: utf-8 -*-
"""Tests de la PARIDAD del Edificio B: casos G/Q/GQ/EX/EY, sismo pseudoestático
(V = α·G), equilibrio, superposición y contrato JSON enriquecido."""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "src"))

from edificio_b import analizar, sismo
from edificio_b.construir import build_model
from edificio_b import datos_edificio as D


def test_corte_basal_es_alpha_por_peso_muerto():
    """V_base debe ser α · peso muerto G (= marco + losa), como en el Edificio A."""
    r = analizar.correr(verbose=False)
    assert abs(r["V_EX"] - D.ALPHA_EQ * r["G"]) < 1e-6
    assert abs(r["V_EY"] - D.ALPHA_EQ * r["G"]) < 1e-6


def test_equilibrio_y_superposicion():
    r = analizar.correr(verbose=False)
    assert r["ok_equilibrio"]
    assert r["ok_superposicion"]


def test_pesos_por_nivel_suman_peso_muerto_G():
    """Σ W_i (peso sísmico por nivel) == peso muerto G (marco + losa)."""
    M = build_model()
    import openseespy.opensees as ops
    ops.timeSeries('Constant', 1); ops.pattern('Plain', 1, 1)
    from edificio_b import cargas
    Wpp = cargas.peso_propio(M)
    Wlosa = cargas.carga_muerta_losa(M)
    pesos = sismo.pesos_por_nivel(M)
    assert abs(sum(pesos.values()) - (Wpp + Wlosa)) < 1e-6


def test_json_b_tiene_casos_y_apoyos():
    data = analizar.correr(verbose=False)["data"]
    for caso in ("G", "Q", "GQ", "EX", "EY"):
        assert caso in data["resultados"]
    assert data["apoyos"] and all(a["tipo"] == "empotrado"
                                  for a in data["apoyos"])
    # el esquema B nativo se conserva (lo consumen fusión y visualizadores)
    n0 = data["nodos"][0]
    assert {"id", "x", "y", "z", "ux", "uy", "uz"} <= set(n0)
    assert "peso_propio_kN" in data and "sobrecarga_kN" in data
