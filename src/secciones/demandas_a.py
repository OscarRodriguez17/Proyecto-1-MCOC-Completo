# -*- coding: utf-8 -*-
"""
demandas_a.py — Extracción de la pareja (P_d, M_d) por elemento del Edificio A.

Espejo ADITIVO de `demandas.py` (Edificio B) para el Edificio A. Para cada caso
G/Q/GQ/EX/EY reutiliza la pasada dedicada del Edificio A
(`benchmark_3d.esfuerzos._correr_caso` — construye modelo nuevo y aplica las
cargas del caso) y se leen los esfuerzos de ELEMENTO (`localForce`) de pilares
y muros:

    P = Ni (fuerza axial local; en este modelo P>0 = COMPRESIÓN)
    M = sqrt(Myi**2 + Mzi**2)    (módulo del momento flector en el nudo i)

CONVENCIÓN DE SIGNO: en este módulo P se reporta POSITIVO en compresión
(igual que en las envolventes P–M y en el panel de Unity).

Los bloques de A se persisten en el mismo cache `results/secciones_semana03.json`
pero bajo claves propias (`per_elemento_A`, `demandas_A`, ...) para no alterar
las del Edificio B (ADITIVO).
"""
import json
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

import openseespy.opensees as ops

AQUI = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.abspath(os.path.join(AQUI, ".."))
B3 = os.path.join(SRC, "benchmark_3d")
if B3 not in sys.path:
    sys.path.insert(0, B3)
RESULTADOS = os.path.abspath(os.path.join(SRC, "..", "results"))
ARCHIVO_CACHE = os.path.join(RESULTADOS, "secciones_semana03.json")

import datos_edificio as D                      # noqa: E402  (Edificio A)
import esfuerzos as esfuerzos_A                 # noqa: E402  (pasada de A)

CASOS = ("G", "Q", "GQ", "EX", "EY")

TIPO_SOLIDO = {"pilar": "column", "muro": "wall"}


def _norma(z):
    return (z[0] ** 2 + z[1] ** 2) ** 0.5 if z else 0.0


def demandas_por_caso(caso, verbose=True):
    """(P, M) por elemento (pilares y muros) para un caso del Edificio A."""
    tipo, _aplic, _w = esfuerzos_A._correr_caso(caso)
    out = {}
    n = 0
    for e in sorted(tipo):
        if tipo[e] not in ("pilar", "muro"):
            continue
        f = ops.eleResponse(e, "localForce")
        if f is None or len(f) < 6:
            continue
        P, _Vy, _Vz, _T, Myi, Mzi = f[:6]
        out[str(e)] = {"P": float(P), "M": _norma((float(Myi), float(Mzi))),
                       "My": float(Myi), "Mz": float(Mzi)}
        n += 1
    if verbose:
        print(f"[DEMANDAS-A] {caso:3s}: {n} pilares/muros")
    return out


def calcular(verbose=True):
    """Corre los 5 casos y devuelve {caso: {tag: {P, M}}} (Edificio A)."""
    return {caso: demandas_por_caso(caso, verbose=verbose) for caso in CASOS}


def per_elemento(verbose=True):
    """Mapa tag → {tipo, seccion, cx, cy} de pilares/muros del Edificio A.

    Identifica cada muro con su paño de `datos_edificio.WALLS` (etiqueta
    `muro_A_{e:.2f}x{L:.2f}`) mediante su centroides y los pilares con
    `col_A_0.70x0.70`. Corre un único modelo (caso G) para leer la geometría.
    """
    tipo, _a, _w = esfuerzos_A._correr_caso("G")
    out = {}
    for e in sorted(tipo):
        t = tipo[e]
        if t not in ("pilar", "muro"):
            continue
        n1, n2 = ops.eleNodes(e)
        (x1, y1, _z1) = ops.nodeCoord(n1)
        (x2, y2, _z2) = ops.nodeCoord(n2)
        cx, cy = 0.5 * (x1 + x2), 0.5 * (y1 + y2)
        secc = "col_A_0.70x0.70"
        if t == "muro":
            for w in D.WALLS:
                if "axis" in w:
                    x = D.GRID_X[w["axis"]]
                    y0, y1 = D.GRID_Y[w["y0"]], D.GRID_Y[w["y1"]]
                    t_ = w["t"]
                    if abs(cx - x) < 0.05 and y0 - 0.05 <= cy <= y1 + 0.05:
                        secc = f"muro_A_{t_:.2f}x{y1 - y0:.2f}"
                        break
                else:
                    y = D.GRID_Y[w["y"]]
                    x0 = D.GRID_X[w["x0"]] if isinstance(w["x0"], str) else w["x0"]
                    x1 = D.GRID_X[w["x1"]] if isinstance(w["x1"], str) else w["x1"]
                    t_ = w["t"]
                    if abs(cy - y) < 0.05 and x0 - 0.05 <= cx <= x1 + 0.05:
                        secc = f"muro_A_{t_:.2f}x{x1 - x0:.2f}"
                        break
        out[str(e)] = {"tipo": t, "seccion": secc,
                       "cx": round(cx, 3), "cy": round(cy, 3)}
    if verbose:
        n_col = sum(1 for v in out.values() if v["tipo"] == "pilar")
        n_mur = sum(1 for v in out.values() if v["tipo"] == "muro")
        print(f"[ELEMENTOS-A] {len(out)} pilares/muros ({n_col} col, {n_mur} "
              f"muros) con etiqueta de seccion")
    return out


def guardar(bloque):
    """Fusiona `bloque` (top-level) en el caché y persiste (ADITIVO)."""
    actual = {}
    if os.path.exists(ARCHIVO_CACHE):
        with open(ARCHIVO_CACHE, encoding="utf-8") as f:
            actual = json.load(f)
    actual.update(bloque)
    os.makedirs(RESULTADOS, exist_ok=True)
    tmp = ARCHIVO_CACHE + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(actual, f, ensure_ascii=False, indent=1)
    os.replace(tmp, ARCHIVO_CACHE)