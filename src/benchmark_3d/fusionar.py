"""fusionar.py — Une los resultados de los Edificios A y B en UN solo contrato.

Estrategia: cada edificio corre su propio analisis OpenSees (independiente;
evita el doble conteo de areas tributarias de la decision arquitectonica del
proyecto) y aqui se fusionan en `results/edificio_completo.json`:

  {
    "proyecto", "unidades", "version",
    "config": {"offset_b_x_m": 60.0, "tag_offset_b": 100000},
    "totales": {"A": {G,Q,GQ,V_EX,V_EY}, "B": {PP,SC}, "complejo_G_kN", ...},
    "edificios": [
        {"id": "A", "esquema": "A", "json": {...contrato Edificio A...}},
        {"id": "B", "esquema": "B", "offset": [60,0,0], "tag_offset": 100000,
         "json": {...contrato Edificio B, desplazado +X y renumerado...}},
    ]
  }

- Al edificio B se le aplica un offset en X (lado a lado, configurable) y se
  renumeran sus tags de nodo/elemento (+tag_offset) para no colisionar con A.
- Cada `json` conserva su esquema nativo ("A": dict de nodos por tag; "B":
  listas). Los visualizadores del complejo entienden ambas formas.

Uso:
    python src\\benchmark_3d\\fusionar.py [--offset-b 60] [--sin-correr]
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

DAT_A = "modelo_resultados.json"
DAT_B = "modelo_resultados_b.json"
FUERA = "edificio_completo.json"


def _path(name):
    return os.path.join(RESULTS, name)


def _cargar(name):
    with open(_path(name), encoding="utf-8") as f:
        return json.load(f)


def _totales_a(data):
    r = data["resultados"]
    return {
        "G_kN": round(abs(r["G"]["aplicada"]["fz"]), 3),
        "Q_kN": round(abs(r["Q"]["aplicada"]["fz"]), 3),
        "GQ_kN": round(abs(r["GQ"]["aplicada"]["fz"]), 3),
        "V_EX_kN": round(r["EX"]["aplicada"]["fx"], 3),
        "V_EY_kN": round(r["EY"]["aplicada"]["fy"], 3),
    }


def _desplazar_y_renumerar_b(data, offset_x, tag_offset):
    """Aplica offset X a los nodos de B y suma `tag_offset` a todos los tags."""
    nodos, new_id = [], {}
    for nd in data["nodos"]:
        remap = nd["id"] + tag_offset
        new_id[nd["id"]] = remap
        n = dict(nd)
        n["id"] = remap
        n["x"] = round(nd["x"] + offset_x, 4)
        nodos.append(n)
    elems = []
    for el in data["elementos"]:
        e = dict(el)
        e["id"] = el["id"] + tag_offset
        e["ni"] = new_id[el["ni"]]
        e["nj"] = new_id[el["nj"]]
        elems.append(e)
    out = dict(data)
    out["nodos"] = nodos
    out["elementos"] = elems
    out["n_nodos"] = len(nodos)
    out["n_elementos"] = len(elems)
    # renumera los apoyos (si el JSON de B ya trae el bloque de paridad)
    if isinstance(data.get("apoyos"), list):
        out["apoyos"] = [{**a, "tag": a["tag"] + tag_offset}
                         if isinstance(a.get("tag"), int) else a
                         for a in data["apoyos"]]
    return out


def fusion(offset_b_x=60.0, tag_offset=100000, correr=True):
    sys.path.insert(0, AQUI)      # permite `import analizar` (Edificio A)
    sys.path.insert(0, SRC)       # permite `from edificio_b import ...`
    os.makedirs(RESULTS, exist_ok=True)

    if correr:
        print("=" * 70)
        print("COMPLEJO — Edificios A y B (analisis independientes)")
        print("=" * 70)
        from analizar import run_analisis
        import datos_edificio as d_a
        run_analisis(d_a, DAT_A,
                     "Edificio A - 50 m E-J (anexo metalico I'-J)", (0.0, 0.0))

        from edificio_b import analizar as analizar_b
        res_b = analizar_b.correr(out_name=DAT_B)
        tot_b = {"PP_kN": round(res_b["Wpp"], 3),
                 "SC_kN": round(res_b["Wsc"], 3)}
        # carga muerta de losa + peso muerto total G de B (aditivo)
        if res_b.get("Wlosa") is not None:
            tot_b["losa_kN"] = round(res_b["Wlosa"], 3)
            tot_b["G_kN"] = round(res_b["G"], 3)
        # paridad: si B ya trae sismo, se suma a sus totales (aditivo)
        if res_b.get("V_EX") is not None:
            tot_b["V_EX_kN"] = round(res_b["V_EX"], 3)
            tot_b["V_EY_kN"] = round(res_b["V_EY"], 3)

    data_a = _cargar(DAT_A)
    data_b_raw = _cargar(DAT_B)
    data_b = _desplazar_y_renumerar_b(data_b_raw, offset_b_x, tag_offset)
    tot_a = _totales_a(data_a)
    if correr:
        pass  # tot_b ya quedó calculado del análisis
    else:
        tot_b = {"PP_kN": data_b_raw.get("peso_propio_kN"),
                 "SC_kN": data_b_raw.get("sobrecarga_kN")}
        if data_b_raw.get("carga_muerta_losa_kN") is not None:
            tot_b["losa_kN"] = data_b_raw.get("carga_muerta_losa_kN")
            tot_b["G_kN"] = data_b_raw.get("peso_muerto_G_kN")
        res_json = data_b_raw.get("resultados", {})
        if "EX" in res_json:
            tot_b["V_EX_kN"] = round(
                res_json["EX"]["aplicada"].get("fx", 0.0), 3)
            tot_b["V_EY_kN"] = round(
                res_json["EY"]["aplicada"].get("fy", 0.0), 3)

    complejo = {
        "proyecto": "Complejo de Ingenieria - Edificios A y B",
        "unidades": "m, kN",
        "version": 1,
        "config": {"offset_b_x_m": offset_b_x, "tag_offset_b": tag_offset},
        "totales": {
            "A": tot_a,
            "B": tot_b,
        },
        "edificios": [
            {"id": "A", "nombre": "Edificio A (planos 2017_67)",
             "esquema": "A", "offset": [0.0, 0.0, 0.0],
             "n_nodos": len(data_a["nodos"]),
             "n_elementos": len(data_a["elementos"]),
             "json": data_a},
            {"id": "B", "nombre": "Edificio B (planos 2024_22)",
             "esquema": "B", "offset": [offset_b_x, 0.0, 0.0],
             "tag_offset": tag_offset,
             "n_nodos": len(data_b["nodos"]),
             "n_elementos": len(data_b["elementos"]),
             "json": data_b},
        ],
    }
    b_dead = tot_b.get("G_kN", tot_b.get("PP_kN"))
    if tot_a.get("G_kN") is not None and b_dead is not None:
        complejo["totales"]["complejo_G_kN"] = round(tot_a["G_kN"]
                                                     + b_dead, 3)
        complejo["totales"]["complejo_Q_kN"] = round(tot_a["Q_kN"]
                                                     + tot_b["SC_kN"], 3)

    out = _path(FUERA)
    with open(out, "w", encoding="utf-8") as f:
        json.dump(complejo, f, indent=1)

    ta = complejo["totales"]["A"]
    tb = complejo["totales"]["B"]
    def _fmt_carga(v):
        return f"{v:,.1f} kN" if isinstance(v, (int, float)) else "-"
    print("\n[FUSION] edificio_completo.json generado")
    print(f"  Edificio A: {len(data_a['nodos']):5d} nodos, "
          f"{len(data_a['elementos']):5d} elem | G={ta['G_kN']:,.1f} kN  "
          f"Q={ta['Q_kN']:,.1f} kN  V_EX={ta['V_EX_kN']:,.1f} kN")
    print(f"  Edificio B (+ X {offset_b_x:.1f} m, tags +{tag_offset}): "
          f"{len(data_b['nodos']):5d} nodos, "
          f"{len(data_b['elementos']):5d} elem | G={_fmt_carga(tb.get('G_kN', tb['PP_kN']))}  "
          f"SC={_fmt_carga(tb['SC_kN'])}")
    if "complejo_G_kN" in complejo["totales"]:
        print(f"  COMPLEJO: G={complejo['totales']['complejo_G_kN']:,.1f} kN  "
              f"Q={complejo['totales']['complejo_Q_kN']:,.1f} kN")
    return complejo


def main():
    ap = argparse.ArgumentParser(description="Fusiona Edificios A y B")
    ap.add_argument("--offset-b", type=float, default=60.0,
                    help="Offset X [m] del Edificio B (default 60)")
    ap.add_argument("--sin-correr", action="store_true",
                    help="Solo fusiona los JSON existentes (no re-analiza)")
    args = ap.parse_args()
    fusion(offset_b_x=args.offset_b, correr=not args.sin_correr)


if __name__ == "__main__":
    main()