"""Verificacion de cargas del edificio.

Cruza el JSON exportado (results/modelo_resultados.json) contra los calculos
re-hechos desde la geometria (construir + cargas), sin volver a correr el
analisis:

  [A] Consistencia interna del JSON (vigas G/Q, pesos por nivel, sismo).
  [B] Pesos por nivel re-calculados vs cargas G aplicadas y reacciones.
  [C] Equilibrio sismico: V_base, sum F_i, y reacciones vs fuerzas aplicadas.

Uso:  .\\.venv\\Scripts\\python.exe src\\benchmark_3d\\verificar_cargas.py
Sale con codigo != 0 si hay alguna FALLA.
"""

import json
import os
import sys
from math import isclose

import openseespy.opensees as ops

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import construir          # noqa: E402
import cargas             # noqa: E402
import datos_edificio as d  # noqa: E402
from voladizos import (agregar_voladizos_y_cubierta,
                       agregar_arriostramiento_voladizo,
                       agregar_voladizo_piso1_ejeF, integrar_voladizo)  # noqa: E402

RUTA_JSON = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..",
                         "..", "results", "modelo_resultados.json")

REL = 1e-6  # tolerancia relativa de las comprobaciones


def _check(tag, ok, detalle=""):
    estado = "OK" if ok else "FALLA"
    print(f"  [{tag}] {estado}  {detalle}")
    return ok


def main():
    with open(RUTA_JSON, encoding="utf-8") as fh:
        data = json.load(fh)
    ed = data
    resultados = ed["resultados"]
    cargas_json = ed["cargas"]
    ok_all = True

    # --- [A] consistencia interna del JSON ---
    print("=== Verificacion de cargas ===")
    G_apl = abs(resultados["G"]["aplicada"]["fz"])
    Q_apl = abs(resultados["Q"]["aplicada"]["fz"])
    sumG_v = sum(vg["G"] for vg in cargas_json["vigas"])
    sumQ_v = sum(vg["Q"] for vg in cargas_json["vigas"])
    cm = cargas_json["peso_columnas_muros"]
    ok_all &= _check("A1", isclose(sumG_v + cm, G_apl, rel_tol=REL),
                     f"sum(vigas.G)={sumG_v:.2f} + peso_cm={cm:.2f} "
                     f"== G aplicada {G_apl:.2f}")
    ok_all &= _check("A2", isclose(sumQ_v, Q_apl, rel_tol=REL),
                     f"sum(vigas.Q)={sumQ_v:.2f} == Q aplicada {Q_apl:.2f}")

    pesos_json = {int(k): v for k, v in cargas_json["pesos_por_nivel"].items()}
    for caso in ("EX", "EY"):
        s = cargas_json["sismo"][caso]
        comp = "fx" if caso == "EX" else "fy"
        aplicada = abs(resultados[caso]["aplicada"][comp])
        suma_f = sum(s["F_por_nivel"].values())
        ok_all &= _check("A3-" + caso, isclose(s["V"], suma_f, rel_tol=REL),
                         f"V={s['V']:.2f} == sum(F_i)={suma_f:.2f}")
        reac_json = resultados[caso]["reacciones_totales"][comp]
        ok_all &= _check("A4-" + caso,
                         isclose(abs(reac_json), aplicada, rel_tol=REL),
                         f"|reaccion {comp}|={abs(reac_json):.2f} "
                         f"== V aplicada {aplicada:.2f}")

    # --- [B] pesos por nivel recalculados ---
    modelo = construir.construir(dat=d)
    extra = agregar_voladizos_y_cubierta(modelo=modelo, dat=d)
    extra_er = agregar_arriostramiento_voladizo(dat=d, modelo=modelo,
                                                extra=extra)
    extra_f = agregar_voladizo_piso1_ejeF(dat=d, modelo=modelo)
    completo = dict(extra)
    completo["aspas"] = extra_er.get("aspas", [])
    completo["vol_f"] = extra_f
    modelo = integrar_voladizo(modelo, completo, dat=d)
    ops.wipe()
    pesos_rec = cargas.pesos_por_nivel(modelo, dat=d)
    suma_rec = sum(pesos_rec.values())
    ok_all &= _check("B1", isclose(suma_rec, G_apl, rel_tol=REL),
                     f"sum(W_i recalc)={suma_rec:.2f} == G aplicada {G_apl:.2f}")
    for lv, wjson in pesos_json.items():
        wrec = pesos_rec.get(lv, 0.0)
        if not isclose(wjson, wrec, rel_tol=REL):
            ok_all &= _check("B2", False,
                             f"nivel {lv}: JSON={wjson:.2f} vs recalc={wrec:.2f}")

    # --- [C] equilibrio sismico (recalculado) ---
    for dirn, caso in (("X", "EX"), ("Y", "EY")):
        v_base, fuerzas = cargas.sismo_v_base_y_fuerzas(d, pesos_rec, dirn)
        s = cargas_json["sismo"][caso]
        ok_all &= _check("C1-" + caso,
                         isclose(v_base, s["V"], rel_tol=REL)
                         and all(isclose(f, s["F_por_nivel"][str(lv)])
                                 for lv, f in fuerzas.items()),
                         f"V recalc={v_base:.2f} == JSON {s['V']:.2f}")

    # --- tabla resumen por nivel ---
    print("  Nivel |   W_i (kN)  |  F_EX (kN)  |  F_EY (kN)")
    for lv in sorted(pesos_json):
        s_ex = cargas_json["sismo"]["EX"]["F_por_nivel"][str(lv)]
        s_ey = cargas_json["sismo"]["EY"]["F_por_nivel"][str(lv)]
        print(f"    {lv}  | {pesos_json[lv]:10.1f} | {s_ex:10.2f} | {s_ey:10.2f}")

    print("\n=> VERIFICACION",
          "COMPLETA (OK)" if ok_all else "CON FALLAS")
    return 0 if ok_all else 1


if __name__ == "__main__":
    sys.exit(main())
