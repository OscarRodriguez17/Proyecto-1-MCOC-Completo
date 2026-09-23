# -*- coding: utf-8 -*-
"""
semana05_refrescar_caches.py — Refresco ADITIVO de las cachés de secciones
(demanda / esfuerzos) tras editar un DATO del modelo (Semana 05).

Cuando se modifica un dato de `datos_edificio` o `cargas` del Edificio B y se
re-analiza (`python -m edificio_b.analizar` desde `src/`), las cachés de las
semanas 03/04 quedan DESACTUALIZADAS: `demandas`, `per_elemento`,
`superposicion` (§3 G+Q vs GQ y §4 G+EX / G+Q+EX con corridas directas) y las
envolventes P-M de los muros del catálogo.

Este script re-adiciona esos bloques invocando UNICAMENTE módulos verificados:

  - `secciones.demandas.calcular` / `per_elemento`   (recalcula B, no toca A);
  - la verificación §3 G+Q≡GQ sobre las demandas (misma función que el reporte
    de la semana 03);
  - `secciones.superposicion.verificar` (§4, corridas directas A y B);
  - `secciones.catalogo_muros.completar_cache` (+`--force-curvas`) para que el
    catálogo P-M siga cubriendo las secciones de muro que existan AHORA.

NUNCA toca: `curvas`/`demandas_A`/`per_elemento_A`/`curvas_A` (Edificio A), ni
los modelos lineales, ni `modelo_resultados*.json` (los lee tal cual).

Uso (después de re-analizar B):
    python scripts\\semana05_refrescar_caches.py [--force-curvas]
"""
import json
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

AQUI = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(AQUI, ".."))
SRC = os.path.join(ROOT, "src")
RESULTS = os.path.join(ROOT, "results")
sys.path.insert(0, SRC)

from secciones import demandas                     # noqa: E402
from secciones import superposicion as sup_mod     # noqa: E402

CASOS = ("G", "Q", "GQ", "EX", "EY")
CACHE = os.path.join(RESULTS, "secciones_semana03.json")


def superposicion(dem):
    """§3: máxima desviación G+Q vs GQ por componente (P, My, Mz), mismos
    criterios y tolerancias que la semana 03."""
    tags = set()
    for caso in ("G", "Q", "GQ"):
        tags |= set(dem.get(caso, {}))
    dP = dMy = dMz = n = 0.0
    for t in sorted(tags):
        g = dem.get("G", {}).get(t)
        q = dem.get("Q", {}).get(t)
        gq = dem.get("GQ", {}).get(t)
        if not (g and q and gq):
            continue
        dP = max(dP, abs(gq["P"] - (g["P"] + q["P"])))
        dMy = max(dMy, abs(gq["My"] - (g["My"] + q["My"])))
        dMz = max(dMz, abs(gq["Mz"] - (g["Mz"] + q["Mz"])))
        n += 1
    return {"n_elementos": int(n), "max_dP": dP, "max_dMy": dMy,
            "max_dMz": dMz, "max_dM": max(dMy, dMz)}


def main():
    force_curvas = "--force-curvas" in sys.argv

    with open(CACHE, encoding="utf-8") as f:
        cache = json.load(f)

    # --- demandas + etiqueta de sección por elemento (Edificio B) ----
    dem = demandas.calcular(verbose=True)
    elem = demandas.per_elemento(verbose=True)
    sup3 = superposicion(dem)
    print(f"[S05 §3] G+Q vs GQ: {sup3['n_elementos']} elementos, "
          f"max_dP={sup3['max_dP']:.3e} kN, "
          f"max_dMy={sup3['max_dMy']:.3e} max_dMz={sup3['max_dMz']:.3e}")

    # Actualizar el dict en memoria (NO via demandas.guardar, para no
    # mezclar dos escrituras que puedan pisarse entre sí).
    cache["demandas"] = dem
    cache["per_elemento"] = elem
    cache["superposicion"] = dict(sup3)

    # --- catálogo P-M de muros: cubrir TODAS las secciones actuales ----
    if force_curvas:
        from secciones import catalogo_muros
        cache, añadidos = catalogo_muros.completar_cache(cache, force=False,
                                                         verbose=True)
        if añadidos:
            print(f"[S05] curvas de muro añadidas al catálogo: {añadidos}")

    # --- §4 superposición con corridas directas (A y B) ----------------
    comb = sup_mod.verificar(cache, verbose=True, correr_directo=True)
    cache["superposicion"]["combinaciones"] = comb

    # Una SOLA escritura final con todo el cache actualizado.
    os.makedirs(RESULTS, exist_ok=True)
    tmp = CACHE + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(cache, f, ensure_ascii=False, indent=1)
    os.replace(tmp, CACHE)
    print("[S05] caché semana03 actualizada (una sola escritura):", CACHE)
    print("DONE semana05_refrescar_caches")


if __name__ == "__main__":
    main()