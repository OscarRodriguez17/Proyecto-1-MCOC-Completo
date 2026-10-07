# -*- coding: utf-8 -*-
"""Tests de la viga secundaria V.60/80 F-G / 3-2 de la planta 101 (Edificio A).

Ver src/benchmark_3d/vigas_secundarias.py. Se verifica:
  - Conteos: base intacto (190/321); modelo completo 203 nodos / 348 elementos.
  - Geometria: x = 15, del eje 3 al eje 2 pasando por 2a, SOLO en el nivel 1.
  - Las 3 vigas F-G partidas conservan su tag original en el tramo 10 -> 15.
  - Nodos nuevos dentro del diafragma rigido del nivel 1.
  - Area tributaria: 18.125 m2 para la viga nueva (w = 2.5 q) y conservacion
    exacta del area de losa por nivel.
  - Peso: G y GQ suben exactamente 0.48*25*7.25 = 87.00 kN; Q no cambia.
  - Equilibrio global y cierre de diagramas en las vigas nuevas y partidas.
"""
import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..",
                                "src", "benchmark_3d"))

import datos_edificio as d          # noqa: E402
import construir                    # noqa: E402
import cargas                       # noqa: E402
import esfuerzos                    # noqa: E402
import analizar                     # noqa: E402
import voladizos                    # noqa: E402
from vigas_secundarias import (CONFIG_VIGA_FG,              # noqa: E402
                               agregar_viga_secundaria_FG_piso1)

CASOS = ("G", "Q", "GQ", "EX", "EY")
PP_VIGA = 0.60 * 0.80 * d.GAMMA_C * 7.25          # 87.00 kN
PP_PILAR_RAIZ = d.sec_pm()[0] * d.GAMMA_STEEL * d.STORY_H[1]   # 6.96 kN (Ses. 27)


def _modelo_sin_viga():
    """Modelo completo ANTES de la viga (base + voladizos), como referencia."""
    import openseespy.opensees as ops
    ops.wipe()
    modelo = construir.construir()
    extra = voladizos.agregar_voladizos_y_cubierta(modelo=modelo)
    extra_er = voladizos.agregar_arriostramiento_voladizo(modelo=modelo,
                                                          extra=extra)
    extra_f = voladizos.agregar_voladizo_piso1_ejeF(modelo=modelo)
    completo = dict(extra)
    completo["aspas"] = extra_er.get("aspas", [])
    completo["vol_f"] = extra_f
    return voladizos.integrar_voladizo(modelo, completo)


@pytest.fixture(scope="module")
def modelo():
    import openseespy.opensees as ops
    ops.wipe()
    m = analizar.construir_con_voladizo(d)
    m["_n_nodos"] = len(ops.getNodeTags())
    m["_n_ele"] = len(ops.getEleTags())
    m["_coords"] = {t: tuple(ops.nodeCoord(t)) for t in ops.getNodeTags()}
    m["_ele_nodes"] = {t: tuple(ops.eleNodes(t)) for t in ops.getEleTags()}
    ops.wipe()
    return m


def _por_tag(m):
    return {v["tag"]: v for v in m["vigas_x"] + m["vigas_y"]}


# ------------------------------------------------------------------ conteos
def test_modelo_base_intacto():
    import openseespy.opensees as ops
    ops.wipe()
    construir.construir()
    assert len(ops.getNodeTags()) == 190
    assert len(ops.getEleTags()) == 321
    ops.wipe()


def test_conteos_modelo_completo(modelo):
    assert modelo["_n_nodos"] == 206     # 203 + 3 nodos de la viga p2 (S.29)
    assert modelo["_n_ele"] == 354       # 348 + pilar (S.27) + viga p2 (S.29)
    info = modelo["viga_sec_fg"]
    assert len(info["nodos"]) == 3
    assert len(info["tags_viga"]) == 2
    assert len(info["partidas"]) == 3


# --------------------------------------------------------------- geometria
def test_geometria_y_nivel_unico(modelo):
    info = modelo["viga_sec_fg"]
    vigas = _por_tag(modelo)
    z1 = d.LEVEL_Z[CONFIG_VIGA_FG["nivel"]]
    tramos = sorted((vigas[t] for t in info["tags_viga"]),
                    key=lambda v: v["y_i"])
    assert [(v["y_i"], v["y_j"]) for v in tramos] == [
        (d.GRID_Y["A3"], d.GRID_Y["A2a"]), (d.GRID_Y["A2a"], d.GRID_Y["A2"])]
    assert sum(v["y_j"] - v["y_i"] for v in tramos) == pytest.approx(7.25)
    for v in tramos:
        assert v["x"] == 15.0 and v["nivel"] == 1
        assert v["material"] == "concreto" and v["seccion"] == "T"
        assert v["A_pp"] == pytest.approx(0.48)
        for n in modelo["_ele_nodes"][v["tag"]]:
            x, _y, z = modelo["_coords"][n]
            assert x == pytest.approx(15.0) and z == pytest.approx(z1)
    # en x = 15 solo hay viga en los niveles 1 (planta 101) y 2 (cielo piso 2,
    # lamina 102; Sesion 29); NO en el cielo piso 3 ni en el techo
    niveles = {v["nivel"] for v in modelo["vigas_y"] if abs(v["x"] - 15.0) < 1e-9}
    assert niveles == {1, 2}
    i2 = modelo["vigas_sec_fg"][2]
    assert len(i2["tags_viga"]) == 2 and len(i2["partidas"]) == 3
    assert [p["tag_original"] for p in i2["partidas"]] == [134, 139, 144]


def test_vigas_partidas_conservan_tag(modelo):
    ref = _modelo_sin_viga()
    import openseespy.opensees as ops
    ops.wipe()
    orig = {(v["y"]): v for v in ref["vigas_x"]
            if v["nivel"] == 1 and v["x_i"] == 10.0 and v["x_j"] == 20.0
            and v["material"] == "concreto"}
    vigas = _por_tag(modelo)
    assert {p["y"] for p in modelo["viga_sec_fg"]["partidas"]} == {0.0, 2.31, 7.25}
    for p in modelo["viga_sec_fg"]["partidas"]:
        o = orig[p["y"]]
        assert p["tag_original"] == o["tag"]
        a, b = vigas[p["tag_original"]], vigas[p["tag_nuevo"]]
        assert (a["x_i"], a["x_j"]) == (10.0, 15.0)
        assert (b["x_i"], b["x_j"]) == (15.0, 20.0)
        assert a["ni"] == o["ni"] and b["nj"] == o["nj"]
        assert a["nj"] == b["ni"]
        assert a["seccion"] == b["seccion"] == o["seccion"]


def test_nodos_en_diafragma_nivel_1():
    """Bajo EX, todo nodo del nivel 1 sigue el movimiento de cuerpo rigido del
    nodo maestro (ux = ux_m - rz*(y - y_m), uy = uy_m + rz*(x - x_m))."""
    import openseespy.opensees as ops
    ops.wipe()
    m = analizar.construir_con_voladizo(d)
    res = analizar.correr_caso("EX", m)      # deja el modelo resuelto
    mt = m["master"][1]
    xm, ym, _ = ops.nodeCoord(mt)
    um = ops.nodeDisp(mt)
    for n in m["viga_sec_fg"]["nodos"]:
        x, y, _ = ops.nodeCoord(n)
        u = ops.nodeDisp(n)
        assert u[0] == pytest.approx(um[0] - um[5] * (y - ym), abs=1e-12)
        assert u[1] == pytest.approx(um[1] + um[5] * (x - xm), abs=1e-12)
    assert res["aplicada"]["fx"] > 0
    ops.wipe()


# ---------------------------------------------------------- area tributaria
@pytest.mark.parametrize("q", [d.Q_G, d.Q_Q])
def test_area_tributaria(modelo, q):
    info = modelo["viga_sec_fg"]
    vx = [v for v in modelo["vigas_x"] if v["nivel"] == 1]
    vy = [v for v in modelo["vigas_y"] if v["nivel"] == 1]
    w, L, area, transf = cargas.distribuir_nivel(q, vx, vy, d, 1)
    # conservacion exacta del area del nivel
    assert transf == pytest.approx(q * area, abs=1e-9)
    # viga nueva: dos subpaneles de 5 m a cada lado -> 18.125 m2, w = 2.5 q
    trib = sum(w[t] * L[t] for t in info["tags_viga"]) / q
    assert trib == pytest.approx(18.125, abs=1e-9)
    for t in info["tags_viga"]:
        assert w[t] == pytest.approx(2.5 * q, abs=1e-9)
    # tramos partidos: mismo w por metro en ambas mitades, igual al de un nivel
    # SIN viga secundaria (nivel 3, cielo piso 3; el 2 tambien la tiene, S.29)
    vx2 = [v for v in modelo["vigas_x"] if v["nivel"] == 3]
    vy2 = [v for v in modelo["vigas_y"] if v["nivel"] == 3]
    w2, _, area2, _ = cargas.distribuir_nivel(q, vx2, vy2, d, 3)
    assert area2 > area          # el nivel 3 suma el anexo I'-J
    ref2 = {v["y"]: v["tag"] for v in vx2
            if v["x_i"] == 10.0 and v["x_j"] == 20.0
            and v["material"] == "concreto"}
    for p in info["partidas"]:
        assert w[p["tag_original"]] == pytest.approx(w[p["tag_nuevo"]])
        assert w[p["tag_original"]] == pytest.approx(w2[ref2[p["y"]]])


# ------------------------------------------------------------ peso y equilibrio
def test_peso_propio_agregado_exacto():
    import openseespy.opensees as ops
    ref = _modelo_sin_viga()
    p_ref = cargas.pesos_por_nivel(ref)
    ops.wipe()
    m = analizar.construir_con_voladizo(d)
    p_new = cargas.pesos_por_nivel(m)
    ops.wipe()
    assert p_new[1] - p_ref[1] == pytest.approx(PP_VIGA, abs=1e-9)
    # el pilar de raiz (Sesion 27) carga su peso al nivel 2 (nudo superior),
    # mas la viga secundaria del cielo piso 2 (Sesion 29)
    assert p_new[2] - p_ref[2] == pytest.approx(PP_PILAR_RAIZ + PP_VIGA, abs=1e-9)
    for lvl in (3, 4):
        assert p_new[lvl] == pytest.approx(p_ref[lvl], abs=1e-9)


@pytest.fixture(scope="module")
def diagramas():
    return esfuerzos.verificar_diagramas(verbose=False)


def test_equilibrio_y_cierre_en_vigas_nuevas(diagramas, modelo):
    verif, por_caso = diagramas
    info = modelo["viga_sec_fg"]
    tags = list(info["tags_viga"]) + [t for p in info["partidas"]
                                      for t in (p["tag_original"], p["tag_nuevo"])]
    for caso in CASOS:
        assert verif[caso]["equilibrio"]
        assert verif[caso]["cierre_ok"]
        for t in tags:
            assert str(t) in por_caso[caso], f"viga {t} sin diagrama en {caso}"
            assert t not in verif[caso]["vigas_sin_cierre"]
    # carga GQ de la viga nueva: qG*2.5 + qQ*2.5 + pp
    for t in info["tags_viga"]:
        wz = por_caso["GQ"][str(t)]["Wz"]
        assert -wz == pytest.approx(2.5 * (d.Q_G + d.Q_Q) + 0.48 * d.GAMMA_C)
    assert verif["G"]["aplicada"]["fz"] == pytest.approx(
        45236.47873289924 + 2 * PP_VIGA + PP_PILAR_RAIZ, abs=1e-6)
    assert verif["Q"]["aplicada"]["fz"] == pytest.approx(7671.25, abs=1e-6)
