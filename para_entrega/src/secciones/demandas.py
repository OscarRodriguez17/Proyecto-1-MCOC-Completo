# -*- coding: utf-8 -*-
"""
demandas.py — Extracción de la pareja (P_d, M_d) por elemento estructural.

Para cada caso G/Q/GQ/EX/EY se reutiliza la pasada dedicada del Edificio B
(esfuerzos._correr_caso) —que construye modelo nuevo y aplica las cargas del
caso— y se leen los esfuerzos de ELEMENTO (localForce) de pilares y muros:

    P = Ni (fuerza axial local; en este modelo P>0 = COMPRESIÓN)
    M = sqrt(Myi**2 + Mzi**2)    (módulo del momento flector en el nudo i)

CONVENCIÓN DE SIGNO: en este módulo P se reporta POSITIVO en compresión
(igual que en las envolventes P–M y en el panel de Unity).

Es ADITIVO: no modifica construir.py, analizar.py ni esfuerzos.py; solo los
invoca. El barrido de los 5 casos es costoso, por eso `resultado()` persiste en
`results/secciones_semana03.json` (se regenera con scripts/semana03_run.py).
"""
import json
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

import openseespy.opensees as ops

AQUI = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.abspath(os.path.join(AQUI, ".."))
RESULTADOS = os.path.abspath(os.path.join(SRC, "..", "results"))
ARCHIVO_CACHE = os.path.join(RESULTADOS, "secciones_semana03.json")

CASOS = ("G", "Q", "GQ", "EX", "EY")


def _norma(z):
    return (z[0] ** 2 + z[1] ** 2) ** 0.5 if z else 0.0


def demandas_por_caso(caso, verbose=True):
    """(P, M) por elemento (pilares y muros) para un caso del Edificio B."""
    from edificio_b import esfuerzos
    M_mod, _aplic, _w = esfuerzos._correr_caso(caso)
    out = {}
    n = 0
    for e in sorted(M_mod.tipo):
        if M_mod.tipo[e] not in ("pilar", "muro"):
            continue
        f = ops.eleResponse(e, "localForce")
        if f is None or len(f) < 6:
            continue
        P, _Vy, _Vz, _T, Myi, Mzi = f[:6]
        out[str(e)] = {"P": float(P), "M": _norma((float(Myi), float(Mzi))),
                       "My": float(Myi), "Mz": float(Mzi)}
        n += 1
    if verbose:
        print(f"[DEMANDAS] {caso:3s}: {n} pilares/muros")
    return out


def calcular(verbose=True):
    """Corre los 5 casos y devuelve {caso: {tag: {P, M}}}."""
    return {caso: demandas_por_caso(caso, verbose=verbose) for caso in CASOS}


def per_elemento(verbose=True):
    """Mapa tag → {tipo, seccion, cx, cy} de pilares/muros del Edificio B.

    Identifica cada muro con su paño de `datos_edificio` (etiqueta del tipo
    `muro{e:.2f}x{L:.2f}`, p.ej. `muro0.60x2.91`) y los pilares con
    `col0.70x0.70`. Corre un único modelo (caso G) para leer la geometría."""
    from edificio_b import esfuerzos
    from edificio_b import datos_edificio as D
    M, _a, _w = esfuerzos._correr_caso("G")
    out = {}
    for e in sorted(M.tipo):
        t = M.tipo[e]
        if t not in ("pilar", "muro"):
            continue
        n1, n2 = ops.eleNodes(e)
        (x1, y1, _z1) = ops.nodeCoord(n1)
        (x2, y2, _z2) = ops.nodeCoord(n2)
        cx, cy = 0.5 * (x1 + x2), 0.5 * (y1 + y2)
        secc = "col0.70x0.70"
        if t == "muro":
            muros_v = list(D.MUROS_V) + list(getattr(D, "MUROS_BLOQUE_SUP_V", []))
            muros_h = list(D.MUROS_H) + list(getattr(D, "MUROS_BLOQUE_SUP_H", []))
            for (x, y0, y1, e_) in muros_v:
                if abs(cx - x) < 0.05 and y0 - 0.05 <= cy <= y1 + 0.05:
                    secc = f"muro{e_:.2f}x{y1 - y0:.2f}"
                    break
            else:
                for (y, x0, x1, e_) in muros_h:
                    if abs(cy - y) < 0.05 and x0 - 0.05 <= cx <= x1 + 0.05:
                        secc = f"muro{e_:.2f}x{x1 - x0:.2f}"
                        break
        out[str(e)] = {"tipo": t, "seccion": secc,
                       "cx": round(cx, 3), "cy": round(cy, 3)}
    if verbose:
        n_col = sum(1 for v in out.values() if v["tipo"] == "pilar")
        n_mur = sum(1 for v in out.values() if v["tipo"] == "muro")
        print(f"[ELEMENTOS] {len(out)} pilares/muros ({n_col} col, {n_mur} "
              f"muros) con etiqueta de seccion")
    return out


def resultado(verbose=True, recalcular=False):
    """Lectura cacheada del bloque de demandas (json de resultados)."""
    if not recalcular and os.path.exists(ARCHIVO_CACHE):
        with open(ARCHIVO_CACHE, encoding="utf-8") as f:
            return json.load(f).get("demandas", {})
    demanda = calcular(verbose=verbose)
    guardar({"demandas": demanda})
    return demanda


def critico(demanda, tipos=None):
    """Elemento de mayor par (P, M) combinado (pilar o muro).

    Criterio: máximo de M sobre todos los casos (el que toma la envolvente),
    desempate por |P|. Devuelve (tag, caso, P, M)."""
    mejor = None
    for tag, por_caso in demanda.items():
        if tipos is not None and tipos.get(tag) not in tipos:
            continue
        for caso, dm in por_caso.items():
            M_, P_ = dm["M"], dm["P"]
            if mejor is None or M_ > mejor[1]:
                mejor = (tag, caso, P_, M_)
    return mejor


def guardar(bloque):
    """Fusiona `bloque` (top-level) en el caché y persiste."""
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


def guardar_curvas(curvas):
    """Persiste las curvas M–φ (columna y muro) en el caché."""
    guardar({"curvas": curvas})