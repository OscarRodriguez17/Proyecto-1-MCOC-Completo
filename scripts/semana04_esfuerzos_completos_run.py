# -*- coding: utf-8 -*-
"""
semana04_esfuerzos_completos_run.py — Semana 04 (PARTE A): fuerzas completas
por elementTag de TODOS los elementos (columnas, muros, vigas, aspas) + metadata
(material, ejes locales, restricciones) del Complejo A+B.

Calcula y persiste en `results/secciones_semana04.json`:
  - `esfuerzos_completos`   (B)  y `esfuerzos_completos_A`   (A):
        {caso: {tag: {L,N,Vy,Vz,T,My,Mz,Wy,Wz}}}   ← mismo esquema semana03
  - `metadatos` (B) y `metadatos_A` (A):
        {tag: {tipo de visor, seccion, material, L, nodos i/j con restriccion,
               ejes_locales}}
  - `verif_semana04`: equilibrio global y cierre de elementos verticales
        (Vz(L)=-Vzj, My(L)=-Myj, N constante) por caso y edificio.

Luego reenvía a `secciones.exportar_unity.exportar()` para que el JSON del
VISOR SOLIDO (y la copia en StreamingAssets) quede enriquecido de forma
ADITIVA (no se toca el bloque `esfuerzos` de vigas de la semana 03).

Requisitos: haber corrido `scripts/semana03_run.py` y
`scripts/semana03_edificio_A_run.py` (labels de sección por tag).

Uso: python scripts\\semana04_esfuerzos_completos_run.py [--recalcular]
"""
import json
import math
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

AQUI = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.abspath(os.path.join(AQUI, "..", "src"))
ROOT = os.path.abspath(os.path.join(AQUI, ".."))
sys.path.insert(0, SRC)

from secciones import diagramas          # noqa: E402

RESULTS = os.path.join(ROOT, "results")
CACHE03 = os.path.join(RESULTS, "secciones_semana03.json")
CACHE04 = os.path.join(RESULTS, "secciones_semana04.json")


def spot_check(cache4):
    """Plausibilidad de los valores exportados (para el log)."""
    import secciones.exportar_unity as overlay
    import json as _json
    with open(overlay.ENTRADA, encoding="utf-8") as f:
        doc = _json.load(f)
    print("\n[SPOT] plausibilidad de valores:")
    for pre, edf in (("", "B"), ("_A", "A")):
        ec = cache4["esfuerzos_completos" + pre]
        md = cache4["metadatos" + pre]
        cols = [t for t, v in md.items() if v["tipo"] == "column"]
        muros = [t for t, v in md.items() if v["tipo"] == "wall"]
        gc = [ec["G"][t]["N"] for t in cols]
        ex = [(math.hypot(ec["EX"][t]["My"], ec["EX"][t]["Mz"]), t)
              for t in muros]
        mx = max(ex) if ex else (0.0, None)
        print(f"  {edf}: col(g) N min/max = {min(gc):8.1f}/{max(gc):8.1f} kN  "
              f"| muro EX |M|max = {mx[0]:9.1f} kN·m (tag {mx[1]})")
        b = overlay._edificio_b(doc) if edf == "B" else overlay._edificio_a(doc)
        extra_add = len(md)
        no_md = [el for el in b.get("elementos", [])
                 if el.get("tipo") in ("column", "wall", "vigas_x", "vigas_y")
                 and str(el.get("tag")) not in md]
        print(f"  {edf}: metadatos={extra_add} solidos estructurales="
              f"{extra_add + len(no_md)} sin-metadata={len(no_md)} "
              f"({no_md[:6]})")


def main():
    recalcular = "--recalcular" in sys.argv
    if not os.path.exists(CACHE03):
        raise SystemExit(f"Falta {CACHE03} (corre semana03_run + "
                         f"semana03_edificio_A_run)")

    with open(CACHE03, encoding="utf-8") as f:
        cache03 = json.load(f)

    cache4 = {}
    if not recalcular and os.path.exists(CACHE04):
        with open(CACHE04, encoding="utf-8") as f:
            cache4 = json.load(f)
        if "esfuerzos_completos" in cache4 and "metadatos_A" in cache4:
            print("[CACHE] semana04 reutilizada (--recalcular para repetir "
                  "las 10 corridas)")
        else:
            cache4 = {}

    if not cache4:
        cache4 = diagramas.resultado(cache03, verbose=True)
        with open(CACHE04, "w", encoding="utf-8") as f:
            json.dump(cache4, f, ensure_ascii=False)
        print(f"[CACHE4] {CACHE04}")
    else:
        print("[SPOT] cache reutilizada; verificando solo overlay + valores")

    # Overlay aditivo al contrato del visor sólido + copia a StreamingAssets.
    try:
        from secciones import exportar_unity as overlay
        overlay.exportar(verbose=True)
    except Exception as exc:
        print("[OVERLAY] omitido:", exc)

    spot_check(cache4)
    print("\nDONE semana04")


if __name__ == "__main__":
    main()