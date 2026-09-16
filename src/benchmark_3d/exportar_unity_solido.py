# -*- coding: utf-8 -*-
"""
exportar_unity_solido.py — Adaptador para el VISUALIZADOR SÓLIDO de Unity.

Toma el modelo COMPLETO del Complejo (Edificio A nativo + Edificio B) y emite
`results/edificio_solido.json` en el ESQUEMA que espera el renderer sólido
(`UnityStickModel.cs` / `ModeloComplejo.cs`, reutilizado del proyecto del grupo):
columnas, vigas y muros como geometría 3D real, apoyos, cargas y deformada.

Esquema de salida (por edificio, dentro de `edificios[]`):
  proyecto, bloque, offset{x,y}, unidades,
  geometria{grid_x, grid_y, niveles_z, secciones{pilar, viga, muros[]}},
  nodos{ "tag": {x,y,z,nivel,rol} },           # dict por tag
  elementos[ {tag, tipo∈column|wall|vigas_x|vigas_y, ni, nj} ],
  cargas{vigas[], q_losa{G,Q}, pesos_por_nivel{}, sismo{EX,EY}},
  resultados{ G,Q,GQ,EX,EY : {aplicada, reacciones_totales, desplazamientos_maestro} },
  apoyos[ {tag, tipo, constraint} ]

- Edificio A ya viene en este esquema (mismo edificio del curso): pasa casi 1:1;
  solo se envuelve con bloque/offset y se normalizan los elementos `aspa`→viga.
- Edificio B está en esquema reducido: se convierte (nodos lista→dict, tipos
  pilar/muro/viga→column/wall/vigas_x/vigas_y, se sintetiza grilla + muros como
  paneles + nodos maestros para la deformada). Los `brazo` (brazos rígidos) se
  omiten del dibujo.

NO modifica el contrato `results/edificio_completo.json` (esquema anidado) que
usan los tests del complejo ni el visualizador de líneas.

Uso:
    python src\\benchmark_3d\\exportar_unity_solido.py [--offset-b 60]
"""
import argparse
import json
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

AQUI    = os.path.dirname(os.path.abspath(__file__))
SRC     = os.path.abspath(os.path.join(AQUI, ".."))
RESULTS = os.path.abspath(os.path.join(AQUI, "..", "..", "results"))

DAT_A = "modelo_resultados.json"       # Edificio A (esquema rico nativo)
DAT_B = "modelo_resultados_b.json"     # Edificio B (esquema reducido)
FUERA = "edificio_solido.json"


def _cargar(name):
    with open(os.path.join(RESULTS, name), encoding="utf-8") as f:
        return json.load(f)


def _orient(ni, nj):
    """'vigas_x' si el elemento corre más en X que en Y, si no 'vigas_y'."""
    dx = abs(nj["x"] - ni["x"])
    dy = abs(nj["y"] - ni["y"])
    return "vigas_x" if dx >= dy else "vigas_y"


# --------------------------------------------------------------- Edificio A
def adaptar_a(data_a, offset=(0.0, 0.0)):
    """A ya está en el esquema del renderer; solo se envuelve y se normalizan
    los elementos `aspa` (arriostramiento) a viga para que el renderer no falle
    (solo conoce column/wall/vigas_x/vigas_y)."""
    nodos = data_a["nodos"]
    elems = []
    for el in data_a["elementos"]:
        tipo = el["tipo"]
        if tipo == "aspa":
            ni = nodos[str(el["ni"])]
            nj = nodos[str(el["nj"])]
            tipo = _orient(ni, nj)      # aspa diagonal -> viga (se ve inclinada)
        elems.append({"tag": el["tag"], "tipo": tipo,
                      "ni": el["ni"], "nj": el["nj"]})
    return {
        "proyecto": data_a.get("proyecto", "Edificio A"),
        "bloque": "Edificio A - Ingenieria (2017_67)",
        "offset": {"x": offset[0], "y": offset[1]},
        "unidades": data_a.get("unidades", "m, kN"),
        "geometria": data_a["geometria"],
        "nodos": nodos,
        "elementos": elems,
        "cargas": data_a.get("cargas", {}),
        "esfuerzos": data_a.get("esfuerzos", {}),
        "resultados": data_a.get("resultados", {}),
        "apoyos": data_a.get("apoyos", []),
    }


# --------------------------------------------------------------- Edificio B
_TIPO_B = {"pilar": "column", "muro": "wall", "viga": None}  # viga -> por orientación


def adaptar_b(data_b, offset=(60.0, 0.0)):
    """Convierte el esquema reducido de B al esquema rico del renderer."""
    import importlib
    sys.path.insert(0, SRC)
    D = importlib.import_module("edificio_b.datos_edificio")

    niveles_z = [z for (z, _) in D.NIVELES]
    z_a_nivel = {round(z, 3): k for k, z in enumerate(niveles_z)}

    # --- nodos: lista -> dict por tag, con nivel y rol ---
    nodos = {}
    por_nivel = {}
    for n in data_b["nodos"]:
        k = z_a_nivel.get(round(n["z"], 3), 0)
        nodos[str(n["id"])] = {"x": n["x"], "y": n["y"], "z": n["z"],
                               "nivel": k, "rol": ""}
        por_nivel.setdefault(k, []).append(n)

    # --- nodos maestros sintéticos (centroide por nivel) para la deformada ---
    for k, ns in por_nivel.items():
        if k == 0:
            continue
        cx = sum(m["x"] for m in ns) / len(ns)
        cy = sum(m["y"] for m in ns) / len(ns)
        tag_m = 990000 + k
        nodos[str(tag_m)] = {"x": round(cx, 4), "y": round(cy, 4),
                             "z": niveles_z[k], "nivel": k,
                             "rol": "maestro_diafragma"}

    # --- elementos: normaliza tipos; omite brazos rígidos ---
    nd = {n["id"]: n for n in data_b["nodos"]}
    elems = []
    for el in data_b["elementos"]:
        t = el.get("tipo")
        if t == "brazo":
            continue
        if t == "viga":
            tipo = _orient(nd[el["ni"]], nd[el["nj"]])
        else:
            tipo = _TIPO_B.get(t, "column")
        elems.append({"tag": el["id"], "tipo": tipo,
                      "ni": el["ni"], "nj": el["nj"]})

    # --- geometría: grilla + muros como paneles (desde datos_edificio de B) ---
    # Consolidar ejes cercanos (< 0.35 m) para evitar grilla ruidosa
    def _consolidar_ejes(valores, tol=0.35):
        unicos = sorted(set(valores))
        if not unicos:
            return {}
        grupos = [[unicos[0]]]
        prom = unicos[0]
        for v in unicos[1:]:
            if v - prom <= tol + 1e-6:
                grupos[-1].append(v)
                prom = sum(grupos[-1]) / len(grupos[-1])
            else:
                grupos.append([v])
                prom = v
        mapa = {}
        for g in grupos:
            prom_g = sum(g) / len(g)
            for v in g:
                mapa[v] = round(prom_g, 4)
        return mapa

    _xs_raw = [p[0] for p in D.PILARES]
    _ys_raw = [p[1] for p in D.PILARES]
    for b in getattr(D, "BEAMS_V", []):
        _xs_raw.append(b[0])
    for b in getattr(D, "BEAMS_H", []):
        _ys_raw.append(b[0])
    for (x, y0, y1, _) in D.MUROS_V:
        _xs_raw.append(x); _ys_raw.append(y0); _ys_raw.append(y1)
    for (y, x0, x1, _) in D.MUROS_H:
        _ys_raw.append(y); _xs_raw.append(x0); _xs_raw.append(x1)
    if getattr(D, "MUROS_BLOQUE_SUP_V", None):
        for (x, y0, y1, _) in D.MUROS_BLOQUE_SUP_V:
            _xs_raw.append(x); _ys_raw.append(y0); _ys_raw.append(y1)
        for (y, x0, x1, _) in D.MUROS_BLOQUE_SUP_H:
            _ys_raw.append(y); _xs_raw.append(x0); _xs_raw.append(x1)

    _mapa_x = _consolidar_ejes(_xs_raw)
    _mapa_y = _consolidar_ejes(_ys_raw)

    grid_x, grid_y = {}, {}

    def gx(v):
        vs = _mapa_x.get(v, v)
        grid_x[f"{vs:.2f}"] = vs
        return f"{vs:.2f}"

    def gy(v):
        vs = _mapa_y.get(v, v)
        grid_y[f"{vs:.2f}"] = vs
        return f"{vs:.2f}"

    for (x, y) in D.PILARES:      # ejes de pilares en la grilla
        gx(x); gy(y)
    for b in getattr(D, "BEAMS_V", []):   # ejes de vigas (para el mosaico)
        gx(b[0])
    for b in getattr(D, "BEAMS_H", []):
        gy(b[0])

    muros = []
    for (x, y0, y1, e) in D.MUROS_V:
        muros.append({"id": f"MV_{x:.1f}", "axis": gx(x),
                      "y0": gy(y0), "y1": gy(y1), "t": e})
    for (y, x0, x1, e) in D.MUROS_H:
        muros.append({"id": f"MH_{y:.1f}", "y": gy(y),
                      "x0": gx(x0), "x1": gx(x1), "t": e})
    if getattr(D, "MUROS_BLOQUE_SUP_V", None):
        for (x, y0, y1, e) in D.MUROS_BLOQUE_SUP_V:
            muros.append({"id": f"MVsup_{x:.1f}", "axis": gx(x),
                          "y0": gy(y0), "y1": gy(y1), "t": e})
        for (y, x0, x1, e) in D.MUROS_BLOQUE_SUP_H:
            muros.append({"id": f"MHsup_{y:.1f}", "y": gy(y),
                          "x0": gx(x0), "x1": gx(x1), "t": e})

    geometria = {
        "grid_x": grid_x, "grid_y": grid_y, "niveles_z": niveles_z,
        "secciones": {"pilar": "0.70x0.70", "viga": "0.60/0.80",
                      "muros": muros},
    }

    # --- cargas: al esquema del renderer (sin vigas -> sin flechas G/Q de viga) ---
    car_b = data_b.get("cargas", {})
    sismo = car_b.get("sismo", {})
    from edificio_b import cargas as _cg
    cargas = {
        "vigas": car_b.get("vigas", []),
        "q_losa": {"G": _cg.Q_G_LOSA, "Q": _cg.SC_PISO},
        "peso_columnas_muros": data_b.get("peso_muerto_G_kN",
                                          data_b.get("peso_propio_kN", 0.0)),
        "pesos_por_nivel": car_b.get("pesos_por_nivel", {}),
        "sismo": {c: {"V": sismo.get(c, {}).get("V", 0.0),
                      "F_por_nivel": sismo.get(c, {}).get("F_por_nivel", {})}
                  for c in ("EX", "EY") if c in sismo},
    }

    return {
        "proyecto": data_b.get("proyecto", "Edificio B"),
        "bloque": "Edificio B - (planos 2024_22)",
        "offset": {"x": offset[0], "y": offset[1]},
        "unidades": "m, kN",
        "geometria": geometria,
        "nodos": nodos,
        "elementos": elems,
        "cargas": cargas,
        "esfuerzos": data_b.get("esfuerzos", {}),
        "resultados": data_b.get("resultados", {}),
        "apoyos": data_b.get("apoyos", []),
    }


def _tot_caso(ed_a, ed_b, caso, comp):
    """Totales para el HUD (aplicada/reacción por caso, A+B)."""
    def r(ed):
        return ed.get("resultados", {}).get(caso, {})
    ra = r(ed_a).get("reacciones_totales", {})
    rb = r(ed_b).get("reacciones_totales", {})
    aa = r(ed_a).get("aplicada", {})
    ab = r(ed_b).get("aplicada", {})
    return {
        "fz_aplicada": round(aa.get("fz", 0.0) + ab.get("fz", 0.0), 3),
        "fz_reaccion": round(ra.get("fz", 0.0) + rb.get("fz", 0.0), 3),
        "fx_aplicada": round(aa.get("fx", 0.0) + ab.get("fx", 0.0), 3),
        "fx_reaccion": round(ra.get("fx", 0.0) + rb.get("fx", 0.0), 3),
        "fy_aplicada": round(aa.get("fy", 0.0) + ab.get("fy", 0.0), 3),
        "fy_reaccion": round(ra.get("fy", 0.0) + rb.get("fy", 0.0), 3),
    }


def exportar(offset_b_x=60.0):
    data_a = _cargar(DAT_A)
    data_b = _cargar(DAT_B)
    ed_a = adaptar_a(data_a, offset=(0.0, 0.0))
    ed_b = adaptar_b(data_b, offset=(offset_b_x, 0.0))

    complejo = {
        "proyecto": "Complejo de Ingenieria - Edificios A y B (solido)",
        "unidades": "m, kN",
        "nota": "Visualizador solido: columnas/vigas/muros 3D, cargas y deformada.",
        "edificios": [ed_a, ed_b],
        "totales": {
            "G": _tot_caso(ed_a, ed_b, "G", None),
            "EX": _tot_caso(ed_a, ed_b, "EX", None),
            "EY": _tot_caso(ed_a, ed_b, "EY", None),
        },
    }
    os.makedirs(RESULTS, exist_ok=True)
    out = os.path.join(RESULTS, FUERA)
    with open(out, "w", encoding="utf-8") as f:
        json.dump(complejo, f, ensure_ascii=False, indent=1)

    na = len(ed_a["nodos"]); ea = len(ed_a["elementos"])
    nb = len(ed_b["nodos"]); eb = len(ed_b["elementos"])
    print(f"[SOLIDO] {FUERA} generado")
    print(f"  A: {na} nodos, {ea} elem | B: {nb} nodos, {eb} elem "
          f"(offset B x={offset_b_x} m)")
    return complejo


def main():
    ap = argparse.ArgumentParser(description="Exporta JSON para el visor solido")
    ap.add_argument("--offset-b", type=float, default=60.0)
    args = ap.parse_args()
    exportar(offset_b_x=args.offset_b)


if __name__ == "__main__":
    main()
