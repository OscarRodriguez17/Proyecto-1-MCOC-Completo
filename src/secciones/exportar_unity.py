# -*- coding: utf-8 -*-
"""
exportar_unity.py — Overlay ADITIVO de los resultados de secciones al contrato
JSON que consume el VISOR SOLIDO de Unity.

Aclaracion de contratos (importante):
  - `results/edificio_completo.json`  = contrato FUSIONADO (fusionar.py) que
    consumen el visor de lineas viejo y los tests (config/totales/edificios[].json).
  - `results/edificio_solido.json`    = contrato del VISOR SOLIDO
    (exportar_unity_solido.py); esquema PLANO: edificios[i].elementos[].tag.
  - `unity/EdificioSolidoUnity/Assets/StreamingAssets/edificio_completo.json` es
    una COPIA de `edificio_solido.json` (asi lo hace `src/complejo.py`,
    `_exportar_y_sincronizar_solido`). ModeloComplejo.cs parsea ese esquema plano.

Este modulo NO toca `exportar_unity_solido.py` ni `fusionar.py`. En su lugar
ENRIQUECE (no reemplaza) `results/edificio_solido.json`:

  1. carga `results/edificio_solido.json`;
  2. localiza los Edificios B y A y añade a cada pilar/muro (tipo `column`/`wall`)
     un campo `"seccion"` (p.ej. "col0.70x0.70", "muro0.60x2.91",
     "col_A_0.70x0.70", "muro_A_0.20x7.25") mapeando su `tag` al mapa
     `per_elemento`/`per_elemento_A` del cache;
  3. inyecta en cada edificio un bloque `"secciones"` con:
        - catalogo P–M / M–φ por seccion (envolvente, balanceado, P0);
        - demandas (P, M) por caso de cada elemento;
        - verificacion de superposicion G+Q ≡ GQ;
  4. reescribe `results/edificio_solido.json` y lo copia a
     `unity/EdificioSolidoUnity/Assets/StreamingAssets/edificio_completo.json`.

Es idempotente y conserva todas las claves del contrato. Requiere haber corrido
`scripts/semana03_run.py` y `src/benchmark_3d/exportar_unity_solido.py`.

Uso: python src\\secciones\\exportar_unity.py
"""
import json
import os
import shutil
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

AQUI = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.abspath(os.path.join(AQUI, ".."))
ROOT = os.path.abspath(os.path.join(SRC, ".."))
RESULTS = os.path.join(ROOT, "results")
ENTRADA = os.path.join(RESULTS, "edificio_solido.json")
CACHE = os.path.join(RESULTS, "secciones_semana03.json")
CACHE4 = os.path.join(RESULTS, "secciones_semana04.json")
STREAMING = os.path.join(ROOT, "unity", "EdificioSolidoUnity", "Assets",
                         "StreamingAssets", "edificio_completo.json")

CASOS = ("G", "Q", "GQ", "EX", "EY")

# Tipo del visor solido por cada tipo del motor de secciones.
TIPO_SOLIDO = {"pilar": "column", "muro": "wall"}


def _catalogo(cache, prefijo=""):
    """Bloque `secciones` (catalogo + demandas + superposicion) para el visor.

    `prefijo` selecciona el subconjunto del cache: "" para el Edificio B
    (claves `curvas`/`per_elemento`/`demandas`/`superposicion`) y "_A" para el
    Edificio A (claves `curvas_A`/`per_elemento_A`/`demandas_A`/`superposicion_A`).
    """
    curvas = cache.get("curvas" + prefijo, {})
    per = cache.get("per_elemento" + prefijo, {})
    dem = cache.get("demandas" + prefijo, {})

    catalogo = {}
    for clave, d in curvas.items():
        catalogo[clave] = {
            "P0": d.get("P0_aci"),
            "P0_fibra": d.get("P0_fibra"),
            "As": d.get("As"),
            "balanceado": d.get("balanceado"),
            "envelope": d.get("envelope"),
        }

    # Alias de representativa: curvas reales referenciadas por `per_elemento`
    # con su NOMBRE real (p.ej. "col0.70x0.70" en el Edificio B) pueden no
    # estar en el catalogo, que solo trae la clave representativa ("col").
    # Para que el visor resuelva MATCH EXACTO como en el Edificio A
    # ("col_A_0.70x0.70"), se agrega la clave real como alias de la
    # representativa del tipo (aditivo; no borra la original).
    representante = {"pilar": "col", "muro": "muro"}
    for tag, info in per.items():
        secc = info.get("seccion")
        if not secc or secc in catalogo:
            continue
        rep = representante.get(info.get("tipo"))
        if rep in catalogo:
            catalogo.setdefault(secc, catalogo[rep])

    elementos = {}
    for tag, info in per.items():
        reg = {"seccion": info["seccion"], "tipo": info["tipo"],
               "cx": info["cx"], "cy": info["cy"], "demanda": {}}
        for caso in CASOS:
            dm = dem.get(caso, {}).get(tag)
            if dm:
                reg["demanda"][caso] = {"P": dm["P"], "M": dm["M"]}
        elementos[tag] = reg

    return {
        "convencion": "P>0 = compresion (kN); M = sqrt(My^2+Mz^2) (kN*m)",
        "unidades": "kN, kN*m, m",
        "secciones": catalogo,
        "elementos": elementos,
        "superposicion": cache.get("superposicion" + prefijo, {}),
    }


def _edificio_b(doc):
    for e in doc.get("edificios", []):
        bloque = str(e.get("bloque", ""))
        offset = e.get("offset") or {}
        if "Edificio B" in bloque or abs(float(offset.get("x", 0.0))) > 1e-9:
            return e
    return None


def _edificio_a(doc):
    """Edificio A del contrato solido (sin el offset X del B)."""
    for e in doc.get("edificios", []):
        bloque = str(e.get("bloque", ""))
        offset = e.get("offset") or {}
        if "Edificio A" in bloque or (
                abs(float(offset.get("x", 1e9))) < 1e-9
                and not _edificio_b_marca(bloque, offset)):
            return e
    return None


def _edificio_b_marca(bloque, offset):
    """True si el bloque pertenece al Edificio B (misma logica que _edificio_b)."""
    return "Edificio B" in bloque or abs(float(offset.get("x", 0.0))) > 1e-9


def _esfuerzos_completos(ed, cache4, prefijo=""):
    """(SEMANA 04) Inyecta los bloques `esfuerzos_completos` y `metadatos`
    (por elementTag) leidos del cache de la semana 04.

    ADITIVO e idempotente: si el cache no existe o no trae el edificio, deja el
    bloque sin tocar. No modifica el bloque `esfuerzos` (vigas) de semana 03.
    """
    ec = cache4.get("esfuerzos_completos" + prefijo)
    md = cache4.get("metadatos" + prefijo)
    if ec:
        ed["esfuerzos_completos"] = ec
    if md:
        ed["metadatos"] = md
    return bool(ec) or bool(md)


def _etiquetar(elementos, por_tag):
    """Añade el campo `seccion` a los pilar/muro del visor solido. Devuelve el
    número de elementos etiquetados."""
    n_etq = 0
    for el in elementos:
        tag = el.get("tag")
        if tag is None:
            continue
        info = por_tag.get(str(tag))
        if info is None:
            continue
        if el.get("tipo") not in (TIPO_SOLIDO[info["tipo"]], None):
            # el elemento del visor no es pilar/muro del motor: no etiquetar
            continue
        el["seccion"] = info["seccion"]
        n_etq += 1
    return n_etq


def exportar(verbose=True):
    if not os.path.exists(ENTRADA):
        raise SystemExit(f"[OVERLAY] Falta {ENTRADA} "
                         f"(corre benchmark_3d.exportar_unity_solido)")
    if not os.path.exists(CACHE):
        raise SystemExit(f"[OVERLAY] Falta {CACHE} "
                         f"(corre scripts/semana03_run.py)")

    with open(ENTRADA, encoding="utf-8") as f:
        doc = json.load(f)
    with open(CACHE, encoding="utf-8") as f:
        cache = json.load(f)
    cache4 = {}
    if os.path.exists(CACHE4):
        with open(CACHE4, encoding="utf-8") as f:
            cache4 = json.load(f)

    bloque = _catalogo(cache)
    por_tag = bloque["elementos"]

    edb = _edificio_b(doc)
    if edb is None:
        raise SystemExit("[OVERLAY] No se encontro el Edificio B en el contrato")

    n_etq = _etiquetar(edb.get("elementos", []), por_tag)
    edb["secciones"] = bloque
    s04_b = _esfuerzos_completos(edb, cache4)

    # ---- Edificio A (ADITIVO): mismo overlay si el cache trae sus curvas ----
    n_etq_a = 0
    s04_a = False
    eda = _edificio_a(doc)
    if eda is not None and cache.get("curvas_A"):
        bloque_a = _catalogo(cache, "_A")
        n_etq_a = _etiquetar(eda.get("elementos", []), bloque_a["elementos"])
        eda["secciones"] = bloque_a
        s04_a = _esfuerzos_completos(eda, cache4, "_A")

    with open(ENTRADA, "w", encoding="utf-8") as f:
        json.dump(doc, f, ensure_ascii=False, indent=1)

    copiado = False
    if os.path.isdir(os.path.dirname(STREAMING)):
        shutil.copy2(ENTRADA, STREAMING)
        copiado = True

    if verbose:
        print(f"[OVERLAY] catalogo: {list(bloque['secciones'])}")
        print(f"[OVERLAY] {n_etq} elementos etiquetados de "
              f"{len(edb.get('elementos', []))} | {len(por_tag)} con demandas")
        print(f"[OVERLAY] superposicion: {bloque['superposicion']}")
        if eda is not None and "secciones" in eda:
            print(f"[OVERLAY] Edificio A: {n_etq_a} elementos etiquetados | "
                  f"catalogo: {list(bloque_a['secciones'])} | "
                  f"superposicion: {bloque_a['superposicion']}")
        print(f"[OVERLAY] semana04 esfuerzos_completos: B={s04_b} A={s04_a}")
        print(f"[OVERLAY] contrato enriquecido: {ENTRADA}")
        print(f"[OVERLAY] copiado a StreamingAssets: {copiado}")
    return ENTRADA


if __name__ == "__main__":
    exportar()
