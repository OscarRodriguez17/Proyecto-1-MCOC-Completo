# -*- coding: utf-8 -*-
"""
Tests de los datos de la app AR (Semana 6): src/ar/exportar_ar.py.

1) CONVENCIONES contra casos de solución conocida (OpenSees, elementos elásticos):
   viga simplemente apoyada, viga empotrada y columna en voladizo cargada en
   +X y en +Y. Fijan el signo de My(x) y la fórmula CORREGIDA de Mz(x).
2) DATOS EXPORTADOS contra una corrida OpenSees nueva del Edificio A (caso GQ):
   los diagramas deben reproducir localForce en AMBOS extremos del elemento.
3) Valores de referencia de la viga tag 134 y coherencia dM/dx = V.

No escribe en results/ (la exportación de prueba va a un directorio temporal).
"""
import json
import math
import os
import sys

import pytest

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, ".."))
sys.path.insert(0, os.path.join(RAIZ, "src"))
sys.path.insert(0, os.path.join(RAIZ, "src", "benchmark_3d"))

import openseespy.opensees as ops  # noqa: E402
from ar import exportar_ar as AR   # noqa: E402

TAGS = AR.TAGS_POR_DEFECTO


# ------------------------------------------------------------------ helpers
def _resolver():
    ops.system("BandGeneral"); ops.numberer("Plain"); ops.constraints("Plain")
    ops.integrator("LoadControl", 1.0); ops.algorithm("Linear"); ops.analysis("Static")
    assert ops.analyze(1) == 0


def _esf(f, L, Wy=0.0, Wz=0.0):
    N, Vy, Vz, T, My, Mz = f[:6]
    return {"N": N, "Vy": Vy, "Vz": Vz, "T": T, "My": My, "Mz": Mz,
            "Wy": Wy, "Wz": Wz, "L": L}


_SEC = dict(A=0.48, E=3e7, G=1.2e7, J=0.01, Iy=0.0256, Iz=0.0144)


def _elem(tag, i, j, transf):
    s = _SEC
    ops.element("elasticBeamColumn", tag, i, j, s["A"], s["E"], s["G"],
                s["J"], s["Iy"], s["Iz"], transf)


@pytest.fixture(scope="module")
def doc(tmp_path_factory):
    salida = tmp_path_factory.mktemp("ar") / "ar_elementos.json"
    return AR.exportar(TAGS, salida=str(salida), verbose=False)


@pytest.fixture(scope="module")
def fuerzas_opensees():
    """localForce de los elementos exportados, corrida GQ nueva del Edificio A."""
    import esfuerzos as E_A
    E_A._correr_caso("GQ")
    out = {}
    for t in TAGS:
        n1, n2 = ops.eleNodes(t)
        out[t] = (list(ops.eleResponse(t, "localForce")),
                  math.dist(ops.nodeCoord(n1), ops.nodeCoord(n2)))
    return out


# ---------------------------------------------------- 1) convenciones de signo
def test_viga_simple_momento_positivo_es_traccion_abajo():
    ops.wipe(); ops.model("basic", "-ndm", 3, "-ndf", 6)
    ops.node(1, 0, 0, 0); ops.node(2, 10, 0, 0)
    ops.fix(1, 1, 1, 1, 1, 0, 0); ops.fix(2, 0, 1, 1, 0, 0, 0)
    ops.geomTransf("Linear", 1, 0, 0, 1); _elem(1, 1, 2, 1)
    ops.timeSeries("Constant", 1); ops.pattern("Plain", 1, 1)
    ops.eleLoad("-ele", 1, "-type", "-beamUniform", 0.0, -10.0)
    _resolver()
    e = _esf(ops.eleResponse(1, "localForce"), 10.0, Wz=-10.0)
    assert AR.evaluar(e, 5.0)["M_xz"] == pytest.approx(125.0, abs=1e-6)   # +wL²/8
    assert AR.evaluar(e, 10.0)["M_xz"] == pytest.approx(0.0, abs=1e-6)


def test_viga_empotrada_apoyos_negativos():
    ops.wipe(); ops.model("basic", "-ndm", 3, "-ndf", 6)
    for n, x in ((1, 0), (2, 5), (3, 10)):
        ops.node(n, x, 0, 0)
    ops.fix(1, 1, 1, 1, 1, 1, 1); ops.fix(3, 1, 1, 1, 1, 1, 1)
    ops.geomTransf("Linear", 1, 0, 0, 1); _elem(1, 1, 2, 1); _elem(2, 2, 3, 1)
    ops.timeSeries("Constant", 1); ops.pattern("Plain", 1, 1)
    for k in (1, 2):
        ops.eleLoad("-ele", k, "-type", "-beamUniform", 0.0, -10.0)
    _resolver()
    e = _esf(ops.eleResponse(1, "localForce"), 5.0, Wz=-10.0)
    assert AR.evaluar(e, 0.0)["M_xz"] == pytest.approx(-100.0 / 12 * 10, abs=1e-6)
    assert AR.evaluar(e, 5.0)["M_xz"] == pytest.approx(100.0 / 24 * 10, abs=1e-6)


@pytest.mark.parametrize("dirn", ["X", "Y"])
def test_columna_voladizo_cierra_en_ambos_planos(dirn):
    """Carga lateral 10 kN en la cabeza (H=4): |M_base| = 40, M_cabeza = 0."""
    ops.wipe(); ops.model("basic", "-ndm", 3, "-ndf", 6)
    ops.node(1, 0, 0, 0); ops.node(2, 0, 0, 4); ops.fix(1, 1, 1, 1, 1, 1, 1)
    ops.geomTransf("Linear", 1, 1, 0, 0); _elem(1, 1, 2, 1)
    ops.timeSeries("Constant", 1); ops.pattern("Plain", 1, 1)
    ops.load(2, 10 if dirn == "X" else 0, 10 if dirn == "Y" else 0, -100, 0, 0, 0)
    _resolver()
    e = _esf(ops.eleResponse(1, "localForce"), 4.0)
    base, cabeza = AR.evaluar(e, 0.0), AR.evaluar(e, 4.0)
    clave = "M_xz" if dirn == "X" else "M_xy"
    assert abs(base[clave]) == pytest.approx(40.0, abs=1e-6)
    assert cabeza[clave] == pytest.approx(0.0, abs=1e-6)
    assert base["N"] == pytest.approx(-100.0, abs=1e-6)          # compresión < 0
    if dirn == "Y":   # la fórmula antigua Mz + Vy·x NO cierra (daba 80 en la cabeza)
        assert abs(e["Mz"] + e["Vy"] * 4.0) == pytest.approx(80.0, abs=1e-6)


# ------------------------------------- 2) datos exportados vs OpenSees (GQ)
@pytest.mark.parametrize("tag", TAGS)
def test_diagramas_reproducen_localforce_en_ambos_extremos(doc, fuerzas_opensees, tag):
    f, L = fuerzas_opensees[tag]
    Ni, Vyi, Vzi, Ti, Myi, Mzi, Nj, Vyj, Vzj, Tj, Myj, Mzj = f
    d = doc["elementos"][str(tag)]["diagramas"]
    assert doc["elementos"][str(tag)]["L"] == pytest.approx(L, abs=1e-3)
    tol = lambda v: 1e-3 * max(1.0, abs(v))   # redondeo a 3 decimales en el JSON
    # extremo i
    assert d["N"]["i"] == pytest.approx(-Ni, abs=tol(Ni))
    assert d["M_xz"]["i"] == pytest.approx(Myi, abs=tol(Myi))
    assert d["M_xy"]["i"] == pytest.approx(Mzi, abs=tol(Mzi))
    assert d["V_xz"]["i"] == pytest.approx(Vzi, abs=tol(Vzi))
    assert d["V_xy"]["i"] == pytest.approx(-Vyi, abs=tol(Vyi))
    # extremo j (cierre del diagrama)
    assert d["M_xz"]["j"] == pytest.approx(-Myj, abs=tol(Myj))
    assert d["M_xy"]["j"] == pytest.approx(-Mzj, abs=tol(Mzj))
    assert d["V_xz"]["j"] == pytest.approx(-Vzj, abs=tol(Vzj))
    assert d["V_xy"]["j"] == pytest.approx(Vyj, abs=tol(Vyj))
    assert d["N"]["j"] == pytest.approx(Nj, abs=tol(Nj))


@pytest.mark.parametrize("tag", TAGS)
def test_dM_dx_igual_a_V(doc, tag):
    e = doc["elementos"][str(tag)]
    xs = e["x"]
    for p in ("xz", "xy"):
        M, V = e["diagramas"]["M_" + p]["valores"], e["diagramas"]["V_" + p]["valores"]
        for k in range(1, len(xs) - 1):
            dM = (M[k + 1] - M[k - 1]) / (xs[k + 1] - xs[k - 1])
            assert dM == pytest.approx(V[k], abs=0.05 + 1e-3 * abs(V[k]))


# ------------------------------------------------ 3) referencia viga tag 134
def test_viga_134_valores_de_referencia(doc):
    e = doc["elementos"]["134"]
    assert e["tipo"] == "viga" and e["L"] == pytest.approx(10.0)
    assert "Eje F" in e["extremos"]["i"]["etiqueta"]
    assert "Eje G" in e["extremos"]["j"]["etiqueta"]
    assert e["principal"]["M"] == "M_xz"
    M, V = e["diagramas"]["M_xz"], e["diagramas"]["V_xz"]
    assert V["i"] == pytest.approx(80.88, abs=0.05)
    assert V["j"] == pytest.approx(-89.94, abs=0.05)
    assert M["i"] == pytest.approx(-114.30, abs=0.05)
    assert M["j"] == pytest.approx(-159.58, abs=0.05)
    assert M["max_pos"]["valor"] == pytest.approx(77.19, abs=0.05)
    assert M["max_pos"]["x"] == pytest.approx(4.735, abs=0.01)   # donde V = 0
    w = -e["coeficientes_extremo_i"]["Wz"]
    # |M_apoyos| promedio + M_tramo = wL²/8 (equilibrio de la viga continua)
    assert (abs(M["i"]) + abs(M["j"])) / 2 + M["max_pos"]["valor"] == \
        pytest.approx(w * 10.0 ** 2 / 8, rel=5e-3)
    assert "inferior" in M["lado_positivo"]["texto"]
    assert abs(e["diagramas"]["N"]["i"]) < 1e-3


def test_columnas_demanda_coincide_con_motor_de_secciones(doc):
    with open(os.path.join(RAIZ, "results", "edificio_solido.json"), encoding="utf-8") as f:
        ed = next(e for e in json.load(f)["edificios"]
                  if e["bloque"].startswith("Edificio A"))
    for t in (14, 26):
        e = doc["elementos"][str(t)]
        assert e["tipo"] == "columna" and e["pm"] is not None
        ref = ed["secciones"]["elementos"][str(t)]["demanda"]["GQ"]
        assert e["pm"]["demanda"]["i"]["P"] == pytest.approx(ref["P"], abs=0.01)
        assert e["pm"]["demanda"]["i"]["M"] == pytest.approx(ref["M"], abs=0.01)
        assert 0.0 < e["pm"]["DC"] < 1.0
        # doble curvatura: momento del plano principal cambia de signo en altura
        Mp = e["diagramas"][e["principal"]["M"]]
        assert Mp["i"] * Mp["j"] < 0


# ------------------------------------------- 4) anclaje (colocación en AR)
def test_anclaje_centra_el_elemento_sobre_su_piso(doc):
    """El punto de anclaje es el piso bajo el centro del elemento."""
    v = doc["elementos"]["134"]["anclaje"]
    assert v["punto_opensees"] == pytest.approx([15.0, 0.0, -0.05])   # centro F–G, piso 1
    assert v["punto_unity"] == pytest.approx([15.0, -0.05, 0.0])      # swap Y↔Z
    assert v["altura_sobre_piso"]["i"] == pytest.approx(3.96)         # viga en el cielo
    for t, x in (("14", 10.0), ("26", 20.0)):
        c = doc["elementos"][t]["anclaje"]
        assert c["punto_opensees"] == pytest.approx([x, 0.0, -0.05])  # pie de la columna
        assert c["altura_sobre_piso"]["i"] == pytest.approx(0.0)
        assert c["altura_sobre_piso"]["j"] == pytest.approx(3.96)


# ----------------------------- 5) paridad con el motor de secciones (semana 06)
# El teléfono y el visor de secciones son dos consumidores independientes de los
# mismos coeficientes. Si divergen, cada pantalla miente por su lado.

# Clave de `secciones.diagramas.evaluar` -> clave de `ar.exportar_ar.evaluar`.
# El plano x–y es el caso con signo: la app exporta el corte como −Vy para que
# dM/dx = +V, así que el signo se recupera al comparar.
PARIDAD = (
    ("N", "N", 1.0),
    ("Vz", "V_xz", 1.0),
    ("Vy", "V_xy", -1.0),
    ("My", "M_xz", 1.0),
    ("Mz", "M_xy", 1.0),
    ("T", "T", 1.0),
)


def test_columna14_mz_en_cabeza_es_219_4_kNm(doc):
    """Regresión: Mz(L) de la columna 14 en GQ, con el motor de secciones.

    Es el valor de referencia de la orden (+219,4 kN·m). Se evalúa con
    `secciones.diagramas`, NO con el exportador, para que el número no dependa
    del código que dibuja el teléfono: si `Mz(x) = Mz − Vy·x − Wy·x²/2` volviera
    a llevar el signo + (el error que se corrigió en la semana 06), el cierre
    contra `localForce` del extremo j se rompería y este test caería.
    """
    from secciones import diagramas

    e = doc["elementos"]["14"]
    coef = e["coeficientes_extremo_i"]
    L = e["L"]

    assert e["tipo"] == "columna"
    assert L == pytest.approx(3.96, abs=1e-3)

    Mz_L = diagramas.evaluar(coef, L)["Mz"]
    assert Mz_L == pytest.approx(219.4, abs=0.05)      # +219,4 kN·m

    # Y cierra contra el extremo j del propio diagrama exportado.
    assert e["diagramas"]["M_xy"]["j"] == pytest.approx(Mz_L, abs=1e-3)


@pytest.mark.parametrize("tag", [134, 14, 26])
def test_paridad_evaluadores_secciones_vs_app_ar(doc, tag):
    """`secciones.diagramas.evaluar` y `ar.exportar_ar.evaluar` dan lo mismo.

    My ↔ M_xz y Mz ↔ M_xy en todas las abscisas muestreadas del elemento, y con
    ellas los cortantes y el axial. La única diferencia nominal es el signo del
    corte del plano x–y: la app exporta V_xy = −Vy para que dM_xy/dx = +V_xy.
    """
    from secciones import diagramas

    e = doc["elementos"][str(tag)]
    coef = e["coeficientes_extremo_i"]

    for x in e["x"]:
        sd = diagramas.evaluar(coef, x)
        ar = AR.evaluar(coef, x)
        for clave_sd, clave_ar, signo in PARIDAD:
            assert sd[clave_sd] == pytest.approx(signo * ar[clave_ar], abs=1e-6), (
                f"tag {tag} en x={x}: {clave_sd} != {clave_ar}")

