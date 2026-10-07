# -*- coding: utf-8 -*-
"""Tests de los elementos METALICOS del Edificio A (Sesion 27).

  - Pilar P.M.I. faltante en la raiz del voladizo de piso 1, (17.5, 0),
    entre z = -0.05 y 3.91 (planta 101 RLE-PROYECCION + rotulo P.M.I.;
    planta 102 rotulo P.M. 300x300x20). Ver src/benchmark_3d/pilares_metalicos.py.
  - Los pilares (73, 74, 75, 328, 329, 340, 341 + el nuevo) y las diagonales
    (330, 331, 342, 343) se calculan en ACERO (E = 200 GPa) y se exportan al
    visor con material "acero"/"Acero" y su perfil real, sin curva P-M de
    hormigon armado.
"""
import json
import os
import sys

import pytest

RAIZ = os.path.join(os.path.dirname(__file__), "..")
sys.path.insert(0, os.path.join(RAIZ, "src", "benchmark_3d"))
sys.path.insert(0, os.path.join(RAIZ, "src"))

import datos_edificio as d          # noqa: E402
import analizar                     # noqa: E402

PILARES = (73, 74, 75, 328, 329, 340, 341)
DIAGONALES = (330, 331, 342, 343)
SOLIDO = os.path.join(RAIZ, "results", "edificio_solido.json")
CACHE3 = os.path.join(RAIZ, "results", "secciones_semana03.json")


@pytest.fixture(scope="module")
def modelo():
    import openseespy.opensees as ops
    ops.wipe()
    m = analizar.construir_con_voladizo(d)
    m["_coords"] = {t: tuple(ops.nodeCoord(t)) for t in ops.getNodeTags()}
    m["_ele_nodes"] = {t: tuple(ops.eleNodes(t)) for t in ops.getEleTags()}
    ops.wipe()
    return m


def _pilar_raiz(m):
    return m["pilar_raiz_vol_f"]["tag"]


def test_pilar_raiz_geometria_y_seccion(modelo):
    t = _pilar_raiz(modelo)
    assert t == 349                                  # tag estable (S.27)
    ni, nj = modelo["_ele_nodes"][t]
    xi, yi, zi = modelo["_coords"][ni]
    xj, yj, zj = modelo["_coords"][nj]
    assert (xi, yi) == pytest.approx((17.5, 0.0))
    assert (xj, yj) == pytest.approx((17.5, 0.0))
    assert zi == pytest.approx(d.LEVEL_Z[1]) and zj == pytest.approx(d.LEVEL_Z[2])
    col = [c for c in modelo["columns"] if c["tag"] == t][0]
    assert col["material"] == "acero"
    assert col["seccion"] == "P.M. 300x300x20"
    assert col["A_pp"] == pytest.approx(d.sec_pm()[0])
    assert col["rho_pp"] == d.GAMMA_STEEL
    # reutiliza los nodos de raiz existentes (no crea nodos)
    roles = {n["tag"]: n["rol"] for n in modelo["voladizo"]["nodos"]}
    assert roles[ni] == roles[nj] == "raiz_vol_f"


def test_tags_dados_son_acero_en_el_calculo(modelo):
    tabla = analizar.tabla_materiales(modelo)
    for t in PILARES + DIAGONALES + (_pilar_raiz(modelo),):
        assert tabla[t]["material"] == "acero", t
    for t in PILARES + (_pilar_raiz(modelo),):
        assert tabla[t]["perfil"] == "P.M. 300x300x20"
    for t in DIAGONALES:
        assert tabla[t]["perfil"] == "V.M. 300x300x5"
        assert tabla[t]["rol"] == "diagonal"
    assert tabla[14]["material"] == "concreto"


def test_equilibrio_con_pilar_raiz():
    import openseespy.opensees as ops
    for caso in ("G", "EX"):
        ops.wipe()
        m = analizar.construir_con_voladizo(d)
        res = analizar.correr_caso(caso, m)
        rx, ap = res["reacciones"], res["aplicada"]
        assert abs(rx["fx"] + ap["fx"]) < 1e-6
        assert abs(rx["fy"] + ap["fy"]) < 1e-6
        assert abs(rx["fz"] - ap["fz"]) < 1e-6
        if caso == "G":
            pp = d.sec_pm()[0] * d.GAMMA_STEEL * d.STORY_H[1]
            assert ap["fz"] == pytest.approx(
                45236.47873289924 + 2 * 87.0 + pp, abs=1e-6)
    ops.wipe()


@pytest.fixture(scope="module")
def solido():
    if not os.path.exists(SOLIDO):
        pytest.skip("Falta results/edificio_solido.json")
    with open(SOLIDO, encoding="utf-8") as f:
        doc = json.load(f)
    return [e for e in doc["edificios"] if "Edificio A" in e["bloque"]][0]


def test_visor_rotula_acero(solido, modelo):
    els = {e["tag"]: e for e in solido["elementos"]}
    md = solido["metadatos"]
    for t in PILARES + (_pilar_raiz(modelo),):
        assert els[t]["tipo"] == "column"
        assert els[t]["material"] == "acero"
        assert els[t]["perfil"] == "P.M. 300x300x20"
        assert md[str(t)]["material"] == "Acero"
        assert md[str(t)]["seccion"] == "P.M. 300x300x20"
    for t in DIAGONALES:
        assert els[t]["material"] == "acero" and els[t]["rol"] == "diagonal"
        assert els[t]["perfil"] == "V.M. 300x300x5"
        assert md[str(t)]["material"] == "Acero"
        assert md[str(t)]["rol"] == "diagonal"
    assert els[14]["material"] == "concreto"
    assert md["14"]["material"] == "G35"


def test_pilares_acero_con_diagrama_de_interaccion_propio(solido, modelo):
    """Sesion 29: los pilares metalicos tienen SU diagrama P-M (cajon de acero
    A270ES, plastificacion total), no el de la columna de hormigon 0.70x0.70."""
    with open(CACHE3, encoding="utf-8") as f:
        cache = json.load(f)
    per = cache["per_elemento_A"]
    for t in PILARES + (_pilar_raiz(modelo),):
        assert per[str(t)]["seccion"] == "PM_A_300x300x20"
        el = solido["secciones"]["elementos"][str(t)]
        assert el["seccion"] == "PM_A_300x300x20"
        assert set(el["demanda"]) == {"G", "Q", "GQ", "EX", "EY"}
    assert per["14"]["seccion"] == "col_A_0.70x0.70"
    cur = cache["curvas_A"]["PM_A_300x300x20"]
    A = 0.30 ** 2 - 0.26 ** 2
    Z = 0.30 ** 3 / 4 - 0.26 ** 3 / 4
    assert cur["P0_aci"] == pytest.approx(270e3 * A, rel=1e-9)       # 6048 kN
    assert cur["Mp"] == pytest.approx(270e3 * Z, rel=1e-9)           # 636.1 kN·m
    env = cur["envelope"]
    P, M = env["P"], env["M"]
    assert P == sorted(P) and P[0] == pytest.approx(-P[-1])
    assert M[len(M) // 2] == pytest.approx(cur["Mp"], abs=1e-2)       # P = 0
    assert M[0] == 0.0 and M[-1] == 0.0
    # simetria y punto analitico con eje neutro en las almas:
    # P = fy*(2t)*2e  ->  M = Mp - fy*(2t)*e^2
    e = 756.0 / (270e3 * 0.04 * 2)
    k = P.index(756.0)
    assert M[k] == pytest.approx(cur["Mp"] - 270e3 * 0.04 * e * e, abs=1e-3)
    assert M[k] == pytest.approx(M[len(M) - 1 - k], abs=1e-6)
    # el visor resuelve la curva por clave exacta
    cat = solido["secciones"]["secciones"]
    assert cat["PM_A_300x300x20"]["envelope"]["P"] == P
