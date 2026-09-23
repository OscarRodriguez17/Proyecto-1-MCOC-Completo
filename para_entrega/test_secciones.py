# -*- coding: utf-8 -*-
"""Tests de semana 03 — motor de secciones ADITIVO (`src/secciones`).

Valida:
  - materiales Constitucionales (σ–ε) contra la teoría;
  - P0 de la columna 70×70 (8φ28) ACI y por fibras;
  - M–φ a P=0 y la convergencia de malla del EI agrietado (6/12/20);
  - punto balanceado y pico de la envolvente P–M en la región de balanceo;
  - convención y coherencia de demandas (P>0 compresión) y del mapa
    elemento→sección (40 pilares + 60 muros del Edificio B);
  - el overlay aditivo a Unity (`_catalogo`).

No modifica ningún modelo lineal: reutiliza los módulos existentes.
"""
import json
import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from secciones.seccion import columna_70x70, muro_b1
from secciones.curva import momento_curvatura
from secciones import analitica, pm, materiales

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
CACHE = os.path.join(ROOT, "results", "secciones_semana03.json")


# --------------------------------------------------------------------------
# materiales y geometría de las secciones
# --------------------------------------------------------------------------
def test_p0_aci_columna():
    col = columna_70x70()
    assert abs(col.p0_aci() - 12377.0) < 0.01 * 12377.0
    assert abs(col.As - 0.004926) < 1e-4


def test_p0_fibra_mayor_que_aci():
    col = columna_70x70()
    p0f = col.p0_fibra()
    assert 0.95 * 14097 < p0f < 1.05 * 14097
    assert p0f > col.p0_aci()


def test_muro_geometria_y_as():
    m = muro_b1()
    assert abs(m.As - 0.003870) < 0.01 * 0.003870
    assert abs(m.p0_aci() - 38646.0) < 0.02 * 38646.0


def test_material_constitucional():
    assert materiales.sig_concreto(0.0) == pytest.approx(0.0)
    assert materiales.sig_concreto(materiales.EPSC0) < 0.0          # comprime
    assert materiales.sig_concreto(0.01) == pytest.approx(0.0)      # agrietado
    assert materiales.sig_acer(materiales.EY) > 0.0                 # fluye


# --------------------------------------------------------------------------
# M–φ: rigidez agrietada y convergencia de malla
# --------------------------------------------------------------------------
def _ei0_col(gy):
    col = columna_70x70()
    c = momento_curvatura(col, 0.0, gy=gy, gz=gy, dk=2.0e-4, nincr=400)
    return c


def test_mfi_p0_elastico_agrietado():
    c = _ei0_col(12)
    assert 1.3e5 < c.EI0 < 1.6e5           # EI agrietado, NO bruto (~5.9e5)
    assert c.Mmax > 700.0
    assert c.k_max > 0.0
    assert c.motivo in ("crushing", "su_steel", "softening", "no_convergencia",
                        "nincr")


def test_ei0_converge_con_la_malla():
    eis = [_ei0_col(g).EI0 for g in (6, 12, 20)]
    assert max(eis) / min(eis) < 1.10      # 6/12/20 dentro del 10%
    assert all(1.3e5 < e < 1.6e5 for e in eis)


def test_ei_cracked_analitico_coincide():
    col = columna_70x70()
    assert abs(analitica.EI_cracked(col) - 1.47e5) < 0.05 * 1.47e5


# --------------------------------------------------------------------------
# balanceado y envolvente P–M
# --------------------------------------------------------------------------
def test_balanceado_columna():
    pb, mb = pm.balanceado(columna_70x70())
    assert 4000 < pb < 4800
    assert 900 < mb < 1400


def test_pico_envolvente_cerca_de_balance():
    """El momento máximo en la región de balanceo supera ~1250 kN·m y queda
    dentro del ±10% del objetivo (4500 kN, 1395 kN·m)."""
    c = momento_curvatura(columna_70x70(), -4500.0, gy=12, gz=12,
                          dk=1.0e-4, nincr=800)
    assert 1250 < c.Mmax < 1650


def test_curva_pm_compresion_positiva():
    col = columna_70x70()
    env = pm.curva_PM(col, n_puntos=5, gy=6, gz=6, dk=5.0e-4, nincr=150)
    assert env.PS[0] < 0 < env.PS[-1]            # -tracción .. +compresión
    assert env.PS == sorted(env.PS)
    assert all(m >= 0 for m in env.Ms)
    assert env.P_bal > 0 and env.M_bal > 0


# --------------------------------------------------------------------------
# demandas y overlay (requieren el caché de la semana)
# --------------------------------------------------------------------------
def _cache():
    if not os.path.exists(CACHE):
        pytest.skip("Falta results/secciones_semana03.json (corre semana03_run)")
    with open(CACHE, encoding="utf-8") as f:
        return json.load(f)


def test_demandas_convencion_y_cobertura():
    c = _cache()
    per = c.get("per_elemento", {})
    n_col = sum(1 for v in per.values() if v["tipo"] == "pilar")
    n_mur = sum(1 for v in per.values() if v["tipo"] == "muro")
    assert n_col == 40 and n_mur == 60
    g = c["demandas"]["G"]
    Ps = [d["P"] for d in g.values()]
    assert max(Ps) > 0.0                          # hay compresión (P>0)
    assert all(abs(d["M"] - (d["My"] ** 2 + d["Mz"] ** 2) ** 0.5) < 1e-6
               for d in g.values())


def test_superposicion_componentes():
    c = _cache()
    s = c.get("superposicion", {})
    assert s.get("n_elementos", 0) >= 90
    assert s["max_dP"] < 1e-6
    assert s["max_dMy"] < 1e-6
    assert s["max_dMz"] < 1e-6


def test_overlay_catalogo():
    import secciones.exportar_unity as ex
    c = _cache()
    bloque = ex._catalogo(c)
    assert set(bloque["secciones"]) >= {"col", "muro"}
    assert len(bloque["elementos"]) == 100
    col = bloque["secciones"]["col"]
    assert col["P0"] and col["balanceado"] and col["envelope"]
    assert "P" in col["envelope"] and "M" in col["envelope"]


def test_per_elemento_muros_con_seccion_propia():
    """Ningun muro debe caer en la seccion del pilar: los 60 muros (incluidos
    los del bloque superior, espesor 0.20, que no estan en MUROS_V/H) tienen su
    propia etiqueta `muro{e}x{L}`."""
    c = _cache()
    per = c["per_elemento"]
    muros = [v for v in per.values() if v["tipo"] == "muro"]
    assert len(muros) == 60
    assert all(v["seccion"].startswith("muro") for v in muros)
    assert {"muro0.60x2.91", "muro0.20x4.10", "muro0.20x11.59"} <= {
        v["seccion"] for v in muros}


def test_overlay_solido_edificio_b():
    """El overlay enriquece el contrato del VISOR SOLIDO (esquema plano) y es
    idempotente: se ejecuta y se comprueba el resultado sobre el fichero."""
    import secciones.exportar_unity as ex
    if not os.path.exists(ex.ENTRADA) or not os.path.exists(ex.CACHE):
        pytest.skip("Falta results/edificio_solido.json o el cache de secciones")
    ex.exportar(verbose=False)
    with open(ex.ENTRADA, encoding="utf-8") as f:
        doc = json.load(f)
    edb = ex._edificio_b(doc)
    assert edb is not None
    assert "secciones" in edb
    assert edb["secciones"]["elementos"]
    etiquetados = [el for el in edb["elementos"] if "seccion" in el]
    assert len(etiquetados) >= 100
    assert any(el["tipo"] == "wall" and el["seccion"].startswith("muro")
               for el in etiquetados)


# --------------------------------------------------------------------------
# catálogo P-M COMPLETO de muros (Semana 03 §3) y superposición §4
# --------------------------------------------------------------------------
def _muros_del_catalogo():
    from secciones.seccion import muro
    return muro()


def test_muro_generico_reproduce_muro_b1():
    from secciones.seccion import muro_generico
    mb = muro_b1()
    mg = muro_generico(0.60, 2.91)
    assert mg.nombre == "muro0.60x2.91"
    assert abs(mg.As - mb.As) < 1e-12
    assert len(mg.barras) == len(mb.barras)


def test_catalogo_muros_11_secciones_distintas():
    secs = _muros_del_catalogo()
    assert len(secs) == 11
    assert {s.nombre for s in secs} == {
        "muro0.30x1.45", "muro0.60x2.91", "muro0.60x2.92",
        "muro0.25x3.12", "muro0.25x7.95", "muro0.25x5.80",
        "muro0.20x4.10", "muro0.20x4.50", "muro0.30x2.64",
        "muro0.20x9.44", "muro0.20x11.59"}


def test_catalogo_muros_p0_fibra_cerca_de_aci():
    """P0 de fibra ≈ P0 analítico ACI en cada muro del catálogo (1.05–1.30×)."""
    for s in _muros_del_catalogo():
        assert s.p0_aci() > 0.0
        r = s.p0_fibra() / s.p0_aci()
        assert 1.05 < r < 1.30
        assert s.p0_fibra() > s.p0_aci()


def test_cache_curvas_por_cada_muro():
    """Cada sección del catálogo tiene su envolvente P-M en el cache."""
    c = _cache()
    curvas = c.get("curvas", {})
    for s in _muros_del_catalogo():
        label = s.nombre
        assert label in curvas, label
        env = curvas[label]["envelope"]
        assert len(env["P"]) == len(env["M"]) >= 10
        assert curvas[label]["P0_aci"] == pytest.approx(s.p0_aci(), rel=0.01)
        assert curvas[label]["P0_fibra"] == pytest.approx(s.p0_fibra(), rel=0.01)


def test_muro_representativo_idem_catalogo():
    c = _cache()
    assert (c["curvas"]["muro0.60x2.91"]["As"]
            == pytest.approx(c["curvas"]["muro"]["As"], rel=0.01))


def test_superposicion_combinaciones_seccion4():
    """§4: G+EX y G+Q+EX directas (OpenSees) ≈ suma de casos, en A y B."""
    c = _cache()
    comb = c.get("superposicion", {}).get("combinaciones", {})
    assert set(comb) >= {"A", "B"}
    for ed in ("A", "B"):
        for nombre in ("GQ", "G+EX", "G+Q+EX"):
            b = comb[ed].get(nombre, {})
            assert b.get("max_dR", 1.0) < 1e-6, (ed, nombre)
            assert b.get("max_dD", 1.0) < 1e-6, (ed, nombre)


def test_overlay_curva_propia_por_muro():
    """El visor solido asocia a CADA muro su propia curva P-M (no la del
    muro representativo)."""
    import secciones.exportar_unity as ex
    if not os.path.exists(ex.ENTRADA) or not os.path.exists(ex.CACHE):
        pytest.skip("Falta results/edificio_solido.json o el cache de secciones")
    ex.exportar(verbose=False)
    with open(ex.ENTRADA, encoding="utf-8") as f:
        doc = json.load(f)
    edb = ex._edificio_b(doc)
    cat = edb["secciones"]["secciones"]
    walls = [el for el in edb["elementos"]
             if el.get("tipo") == "wall" and "seccion" in el]
    assert len(walls) >= 60
    etiquetas = {el["seccion"] for el in walls}
    assert len(etiquetas) == 11
    assert {"muro0.20x11.59", "muro0.30x1.45", "muro0.60x2.92"} <= etiquetas
    for el in walls:
        env = cat[el["seccion"]]["envelope"]
        assert len(env["P"]) == len(env["M"]) >= 10


# --------------------------------------------------------------------------
# Catálogo P-M del EDIFICIO A (ADITIVO Semana 03 + A). f'c = 30 MPa (G35).
# --------------------------------------------------------------------------
def _secciones_A():
    from secciones.seccion import columna_A_70x70, muros_A
    return [columna_A_70x70()] + muros_A()


def test_curvas_A_secciones_distintas():
    secs = _secciones_A()
    assert len(secs) == 6
    assert {s.nombre for s in secs} == {
        "col_A_0.70x0.70",
        "muro_A_0.20x3.70", "muro_A_0.20x6.60", "muro_A_0.20x7.25",
        "muro_A_0.30x7.25", "muro_A_0.30x8.90"}


def test_p0_aci_columna_A_usa_fc_30():
    from secciones.seccion import columna_A_70x70
    from secciones import materiales as M
    col = columna_A_70x70()
    # f'c = 30 MPa (FC_A), fy = 420 MPa → P0 ACI = 14 438 kN aprox.
    assert col.fc == M.FC_A == 30.0 * M.MPA
    assert abs(col.p0_aci() - 14438.0) < 0.01 * 14438.0


def test_p0_fibra_ratio_edificio_A():
    """P0 de fibra ≈ P0 ACI (1.05–1.30) en las 6 secciones del Edificio A."""
    for s in _secciones_A():
        r = s.p0_fibra() / s.p0_aci()
        assert 1.05 < r < 1.30
        assert s.p0_fibra() > s.p0_aci()


def test_muro_A_geometria_y_as():
    from secciones.seccion import muro_A_generico
    m = muro_A_generico(0.20, 7.25)
    # 2 extremos × 6φ16 + alma φ10@0.20 doble malla
    assert m.nombre == "muro_A_0.20x7.25"
    bordes = 0.016
    assert sum(1 for br in m.barras if abs(br.d - bordes) < 1e-12) == 12
    assert m.fc == pytest.approx(30.0 * 1000)      # f'c = 30 MPa (G35)


def test_analitica_usa_fc_de_la_seccion():
    """El integrador analítico respeta el f'c de la sección (A: 30 MPa)."""
    from secciones.seccion import columna_70x70, columna_A_70x70
    from secciones import analitica, materiales as M
    a = columna_A_70x70()
    b = columna_70x70()
    # P0 del material compuesto con fc=30 > con fc=25 (misma geometría/acero)
    assert a.p0_fibra() > b.p0_fibra()
    # y el balanceado de la columna A (mayor fc) está más arriba en P
    pb_a, _ = analitica.balanceado(a)
    pb_b, _ = analitica.balanceado(b)
    assert pb_a > pb_b


def test_cache_curvas_A_por_cada_seccion():
    c = _cache()
    curvas = c.get("curvas_A", {})
    assert set(curvas) >= {s.nombre for s in _secciones_A()}
    for s in _secciones_A():
        env = curvas[s.nombre]["envelope"]
        assert len(env["P"]) == len(env["M"]) >= 10
        assert curvas[s.nombre]["P0_aci"] == pytest.approx(s.p0_aci(), rel=0.01)
        assert curvas[s.nombre]["P0_fibra"] == pytest.approx(s.p0_fibra(), rel=0.01)


def test_per_elemento_A_etiquetas():
    c = _cache()
    per = c.get("per_elemento_A", {})
    assert len(per) >= 100
    muros = [v for v in per.values() if v["tipo"] == "muro"]
    cols = [v for v in per.values() if v["tipo"] == "pilar"]
    assert all(v["seccion"] == "col_A_0.70x0.70" for v in cols)
    assert all(v["seccion"].startswith("muro_A_") for v in muros)
    assert {v["seccion"] for v in muros} == {
        "muro_A_0.20x3.70", "muro_A_0.20x6.60", "muro_A_0.20x7.25",
        "muro_A_0.30x7.25", "muro_A_0.30x8.90"}


def test_superposicion_A_componentes():
    c = _cache()
    s = c.get("superposicion_A", {})
    assert s.get("n_elementos", 0) >= 100
    assert s["max_dP"] < 1e-6
    assert s["max_dMy"] < 1e-6
    assert s["max_dMz"] < 1e-6


def test_demandas_A_convencion():
    c = _cache()
    g = c.get("demandas_A", {}).get("G", {})
    assert g
    Ps = [d["P"] for d in g.values()]
    assert max(Ps) > 0.0                          # hay compresión (P>0)
    assert all(abs(d["M"] - (d["My"] ** 2 + d["Mz"] ** 2) ** 0.5) < 1e-6
               for d in g.values())


def test_overlay_solido_edificio_a():
    """El overlay enriquece también el bloque del Edificio A en el visor
    sólido (su propio catálogo P-M y demandas por elemento)."""
    import secciones.exportar_unity as ex
    if not os.path.exists(ex.ENTRADA) or not os.path.exists(ex.CACHE):
        pytest.skip("Falta results/edificio_solido.json o el cache de secciones")
    if "curvas_A" not in _cache():
        pytest.skip("Falta el catálogo de A (corre semana03_edificio_A_run)")
    ex.exportar(verbose=False)
    with open(ex.ENTRADA, encoding="utf-8") as f:
        doc = json.load(f)
    eda = ex._edificio_a(doc)
    assert eda is not None
    assert "secciones" in eda
    s = eda["secciones"]
    assert set(s["secciones"]) >= {"col_A_0.70x0.70", "muro_A_0.20x7.25"}
    etiquetados = [el for el in eda["elementos"] if "seccion" in el]
    assert len(etiquetados) >= 100
    for el in etiquetados:
        env = s["secciones"][el["seccion"]]["envelope"]
        assert len(env["P"]) == len(env["M"]) >= 10
    walls = [el for el in eda["elementos"]
             if el.get("tipo") == "wall" and "seccion" in el]
    assert {el["seccion"] for el in walls} == {
        "muro_A_0.20x3.70", "muro_A_0.20x6.60", "muro_A_0.20x7.25",
        "muro_A_0.30x7.25", "muro_A_0.30x8.90"}