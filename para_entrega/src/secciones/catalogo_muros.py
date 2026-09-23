# -*- coding: utf-8 -*-
"""
catalogo_muros.py — Envolventes P-M del CATALOGO COMPLETO de muros (Semana 03).

Amplia (ADITIVO) el cache `results/secciones_semana03.json` para que TODOS los
muros del Edificio B tengan su propia curva P-M (antes solo la seccion
representativa 0.60x2.91 bajo la clave `muro`).

Para cada seccion DISTINTA del Edificio B (`seccion.muro()`: MUROS_V + MUROS_H
+ bloque superior e=0.20, deduplicados por espesor/largo) se calcula con el
mismo motor validado (fibra explicita, `pm.curva_PM`) la envolvente P-M de 17
puntos a malla 12x12. Cada envolvente se guarda como:

    curvas["muro0.20x9.44"] = {
        "A", "As", "P0_aci", "P0_fibra",
        "balanceado": {P, M},
        "envelope": {P, M, EI0, MOTIVO}}

La seccion representativa `muro0.60x2.91` REUTILIZA la entrada `curvas["muro"]`
ya existente (misma geometria y armado), sin recalcular nada.

Es idempotente: no recalcula una seccion que ya este en el cache. Uso:
    python scripts/semana03_muros_catalog.py [--force]
"""
import json
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

AQUI = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.abspath(os.path.join(AQUI, ".."))
ROOT = os.path.abspath(os.path.join(SRC, ".."))
CACHE = os.path.join(ROOT, "results", "secciones_semana03.json")

sys.path.insert(0, SRC)

from secciones import pm                       # noqa: E402
from secciones.seccion import muro             # noqa: E402

N_PUNTOS = 17
GY = GZ = 12
DK = 1.0e-4
NINCR = 2000


def _entrada(sec, env):
    return {
        "A": sec.Ag,
        "As": sec.As,
        "P0_aci": sec.p0_aci(),
        "P0_fibra": sec.p0_fibra(),
        "balanceado": {"P": env.P_bal, "M": env.M_bal},
        "envelope": {"P": env.PS, "M": env.Ms,
                     "EI0": env.EI0s, "MOTIVO": env.motivos},
    }


def labels_distintas():
    """Etiquetas `muro{e:.2f}x{L:.2f}` de todas las secciones de muro del B."""
    return [s.nombre for s in muro()]


def curvas_por_muro(verbose=True):
    """{etiqueta: entrada} con la envolvente P-M de cada muro (motor real)."""
    import time
    out = {}
    for sec in muro():
        t0 = time.time()
        env = pm.curva_PM(sec, n_puntos=N_PUNTOS, gy=GY, gz=GZ,
                          dk=DK, nincr=NINCR)
        out[sec.nombre] = _entrada(sec, env)
        if verbose:
            print(f"  {sec.nombre:15s} As={sec.As:.6f} "
                  f"P0_aci={sec.p0_aci():8.1f} P0_fibra={sec.p0_fibra():8.1f} "
                  f"({time.time() - t0:.1f} s)")
    return out


def completar_cache(cache, force=False, verbose=True):
    """Agrega a `cache["curvas"]` las envolventes faltantes (idempotente).

    La seccion representativa `muro0.60x2.91` reutiliza `curvas["muro"]`
    (misma geometria); las demas se calculan con el motor. Devuelve el cache
    modificado y la lista de labels añadidos.
    """
    curvas = cache.setdefault("curvas", {})
    añadidos = []
    for label in labels_distintas():
        if label in curvas and not force:
            continue
        if label == "muro0.60x2.91" and "muro" in curvas and not force:
            curvas[label] = curvas["muro"]
        else:
            from secciones.seccion import muro_generico
            tb, L = [float(x) for x in label[len("muro"):].split("x")]
            sec = muro_generico(tb, L, nombre=label)
            curvas[label] = _entrada(sec, pm.curva_PM(
                sec, n_puntos=N_PUNTOS, gy=GY, gz=GZ, dk=DK, nincr=NINCR))
        añadidos.append(label)
    if añadidos and verbose:
        print(f"[CATALOGO] curvas añadidas: {añadidos}")
    return cache, añadidos


def main():
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument("--force", action="store_true")
    args = ap.parse_args()
    if not os.path.exists(CACHE):
        raise SystemExit(f"Falta {CACHE} (corre primero scripts/semana03_run.py)")

    with open(CACHE, encoding="utf-8") as f:
        cache = json.load(f)
    cache, añadidos = completar_cache(cache, force=args.force)
    if not añadidos:
        print("[CATALOGO] nada que recalcular (cache completo)")
    with open(CACHE, "w", encoding="utf-8") as f:
        json.dump(cache, f, ensure_ascii=False, indent=1)
    print(f"[CATALOGO] {len(cache['curvas'])} secciones en el cache:")
    for k in cache["curvas"]:
        print(f"   - {k}")


if __name__ == "__main__":
    main()