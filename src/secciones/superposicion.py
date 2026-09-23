# -*- coding: utf-8 -*-
"""
superposicion.py — Verificación §4 del reporte semana03.

Para los Edificios A y B comprueba que la respuesta SUPERPUESTA de los casos
individuales (G, Q, EX) coincide con una corrida DIRECTA en OpenSees de la
combinación (gravedad + sismo coexistindo en un mismo análisis):

  - G+Q    : superpuesta = R(G)+R(Q)   vs   directa = caso GQ (ya verificado);
  - G+EX   : superpuesta = R(G)+R(EX)   vs   directa (corrida nueva);
  - G+Q+EX : superpuesta = R(G)+R(Q)+R(EX) vs directa (corrida nueva).

Se comparan las reacciones totales de la base (fx, fy, fz) y los
desplazamientos de los nodos maestro (ux, uy, rz) de cada piso. El desvío
debe ser ~0 por la linealidad del modelo lineal-elástico (diafragma rígido,
mismo solver con constraints Transformation).

ADITIVO: los modelos de las corridas directas son efímeros (ops.wipe() al
inicio de cada una) y reutilizan las funciones verifi cadas de cada edificio:
  - B: edificio_b.construir.build_model + edificio_b.cargas/sismo + resolver
    de edificio_b.analizar;
  - A: benchmark_3d.analizar.construir_con_voladizo + benchmark_3d.cargas +
    patrones/correr de benchmark_3d.
Nunca toca results/modelo_resultados*.json; solo agrega al cache el bloque
`superposicion.combinaciones`.

Unidades: kN, m.
"""
import json
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.abspath(os.path.join(AQUI, ".."))
ROOT = os.path.abspath(os.path.join(SRC, ".."))
RESULTS = os.path.join(ROOT, "results")
B3 = os.path.join(SRC, "benchmark_3d")

sys.path.insert(0, SRC)
sys.path.insert(0, B3)

import openseespy.opensees as ops  # noqa: E402

import analizar as A_AN          # noqa: E402   (benchmark_3d)
import cargas as C_A             # noqa: E402   (benchmark_3d)
import datos_edificio as D_A     # noqa: E402

from edificio_b.construir import build_model      # noqa: E402
from edificio_b import cargas as C_B              # noqa: E402
from edificio_b import sismo as S_B               # noqa: E402
from edificio_b import datos_edificio as D_B      # noqa: E402
from edificio_b.analizar import _resolver, _reacciones_base, _desp_maestros  # noqa: E402

CASOS_SISMO = {"EX": "X", "EY": "Y"}
CASOS_GRANDEZA = ("G", "Q")

# ------------------------------------------------------------------ utilidades
def _leer(edificio):
    """Resultados por caso del contrato JSON del edificio (A o B)."""
    nombre = "modelo_resultados.json" if edificio == "A" else "modelo_resultados_b.json"
    with open(os.path.join(RESULTS, nombre), encoding="utf-8") as f:
        doc = json.load(f)
    return doc["resultados"]


def _norm_nivel(d):
    """Desplazamientos_maestro keyed por nivel; normaliza str->int."""
    return {int(k): v for k, v in d.items()}


def _superpuesta(resultados, casos):
    """Suma componente a componente de reacciones y maestros de `casos`."""
    R = {"fx": 0.0, "fy": 0.0, "fz": 0.0}
    D = {}
    for nombre in casos:
        r = resultados[nombre]
        rt = r["reacciones_totales"]
        R["fx"] += rt["fx"]
        R["fy"] += rt["fy"]
        R["fz"] += rt["fz"]
        for k, v in _norm_nivel(r["desplazamientos_maestro"]).items():
            d = D.setdefault(k, {"ux": 0.0, "uy": 0.0, "rz": 0.0})
            d["ux"] += v["ux"]
            d["uy"] += v["uy"]
            d["rz"] += v["rz"]
    return R, D


def _max_desvio_desp(Da, Db):
    """Máx. |Da - Db| entre componentes (ux, uy, rz) en pisos comunes."""
    mx = 0.0
    detalle = {}
    for k in sorted(set(Da) & set(Db)):
        da, db = Da[k], Db[k]
        ds = {c: abs(da[c] - db[c]) for c in ("ux", "uy", "rz")}
        detalle[k] = ds
        mx = max(mx, max(ds.values()))
    return mx, detalle


# ------------------------------------------------------------------ corridas B
def _directo_b(casos):
    """Corrida directa de la combinación en el Edificio B (modelo efímero)."""
    M = build_model()
    aplicada = {"fx": 0.0, "fy": 0.0, "fz": 0.0}
    ops.timeSeries('Constant', 1)
    ops.pattern('Plain', 1, 1)
    if "G" in casos:
        aplicada["fz"] += C_B.peso_propio(M) + C_B.carga_muerta_losa(M)
    if "Q" in casos:
        aplicada["fz"] += C_B.sobrecarga(M)
    for s in casos:
        if s not in CASOS_SISMO:
            continue
        pesos = S_B.pesos_por_nivel(M)
        v_base, fuerzas = S_B.patron_sismico(CASOS_SISMO[s], M, pesos)
        if s == "EX":
            aplicada["fx"] += v_base
        else:
            aplicada["fy"] += v_base
        aplicada["momento_volcante"] = S_B.momento_volcante(fuerzas)
    _resolver()
    return aplicada, _reacciones_base(M), _desp_maestros(M)


# ------------------------------------------------------------------ corridas A
def _directo_a(casos):
    """Corrida directa de la combinación en el Edificio A (modelo efímero)."""
    ops.wipe()
    modelo = A_AN.construir_con_voladizo(D_A)
    aplicada = {"fx": 0.0, "fy": 0.0, "fz": 0.0}
    tiene_g = "G" in casos
    tiene_q = "Q" in casos
    # misma convención de patrones que los casos individuales ya verificados:
    # pat 1 para carga sola (G o Q), pat 11+12 para G+Q combinado, pat 2 sismo.
    if tiene_g and tiene_q:
        _, t1 = C_A.cargas_gravedad(D_A.Q_G, modelo, dat=D_A, ts_tag=11, pat_tag=11)
        _, t2 = C_A.cargas_gravedad(D_A.Q_Q, modelo, dat=D_A, ts_tag=12, pat_tag=12,
                                    incluir_pp=False)
        aplicada["fz"] = t1 + t2
    elif tiene_g:
        _, t1 = C_A.cargas_gravedad(D_A.Q_G, modelo, dat=D_A)
        aplicada["fz"] = t1
    elif tiene_q:
        _, t2 = C_A.cargas_gravedad(D_A.Q_Q, modelo, dat=D_A, incluir_pp=False)
        aplicada["fz"] = t2
    for s in casos:
        if s not in CASOS_SISMO:
            continue
        pesos = C_A.pesos_por_nivel(modelo, dat=D_A)
        v_base, fuerzas = C_A.patron_sismico(CASOS_SISMO[s], modelo, pesos, dat=D_A)
        if s == "EX":
            aplicada["fx"] += v_base
        else:
            aplicada["fy"] += v_base

    ops.system("BandGen")
    ops.numberer("RCM")
    ops.constraints("Transformation")
    ops.integrator("LoadControl", 1.0)
    ops.algorithm("Linear")
    ops.analysis("Static")
    ok = ops.analyze(1)
    if ok != 0:
        raise RuntimeError(f"analisis fallo en combinacion {casos} (A)")
    return aplicada, A_AN.reacciones_base(), A_AN.desplazamientos_maestros(modelo, dat=D_A)


# -------------------------------------------------------------------- verificar
COMBINACIONES = (("GQ", ("G", "Q")), ("G+EX", ("G", "EX")),
                 ("G+Q+EX", ("G", "Q", "EX")))


def _verificar_edificio(edificio, resultados, incluir_directo):
    """Verifica las combinaciones de UN edificio; devuelve el dict bloque."""
    bloque = {}
    for nombre, casos in COMBINACIONES:
        Rsup, Dsup = _superpuesta(resultados, casos)
        if nombre == "GQ":
            gq = resultados["GQ"]
            Rdir = gq["reacciones_totales"]
            Ddir = _norm_nivel(gq["desplazamientos_maestro"])
            aplicada = gq.get("aplicada", {})
        elif incluir_directo:
            if edificio == "B":
                aplicada, reacc, desp = _directo_b(casos)
            else:
                aplicada, reacc, desp = _directo_a(casos)
            Rdir = {k: reacc[k] for k in ("fx", "fy", "fz")}
            Ddir = desp
        else:
            continue

        dR = max(abs(Rsup[c] - Rdir[c]) for c in ("fx", "fy", "fz"))
        dD, detalle_dD = _max_desvio_desp(Dsup, Ddir)
        bloque[nombre] = {
            "casos": list(casos),
            "aplicada": aplicada,
            "reacciones_superpuesta": Rsup,
            "reacciones_directa": Rdir,
            "max_dR": dR,
            "desplazamientos_superpuesta": {k: v for k, v in sorted(Dsup.items())},
            "desplazamientos_directa": {k: v for k, v in sorted(Ddir.items())},
            "max_dD": dD,
            "detalle_desvio_desplaz": detalle_dD,
        }
    return bloque


def verificar(cache=None, verbose=True, correr_directo=True):
    """Llena `cache["superposicion"]["combinaciones"]` {A, B} y lo devuelve.

    `correr_directo=False` omite las corridas directas de G+EX / G+Q+EX (p.ej.
    si ya están en el cache y solo se reconstruye el bloque de lectura).
    """
    resultados_a = _leer("A")
    resultados_b = _leer("B")
    comb = {
        "A": _verificar_edificio("A", resultados_a, correr_directo),
        "B": _verificar_edificio("B", resultados_b, correr_directo),
    }
    if verbose:
        for ed in ("A", "B"):
            print(f"[SUPERPOSICION] {ed}:")
            for nombre, b in comb[ed].items():
                print(f"  {nombre:6s} max_dR = {b['max_dR']:9.3e} kN"
                      f"   max_dD = {b['max_dD']:9.3e} m"
                      f"   ({'+'.join(b['casos'])})")
    if cache is not None:
        cache.setdefault("superposicion", {})["combinaciones"] = comb
    return comb


def main():
    import json
    cache_path = os.path.join(RESULTS, "secciones_semana03.json")
    with open(cache_path, encoding="utf-8") as f:
        cache = json.load(f)
    verificar(cache)
    with open(cache_path, "w", encoding="utf-8") as f:
        json.dump(cache, f, ensure_ascii=False, indent=1)
    print(f"[SUPERPOSICION] guardado en {cache_path}")


if __name__ == "__main__":
    main()