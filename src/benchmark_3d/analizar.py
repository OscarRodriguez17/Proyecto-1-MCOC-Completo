"""Analisis principal: casos G, Q, EX, EY y combinado G+Q.

Verifica:
  1. Conservacion de areas tributarias (carga transferida = q * area).
  2. Equilibrio por caso (sum de reacciones vs sum de cargas aplicadas).
  3. Superposicion: R(G)+R(Q) == R(GQ) en reacciones.

Salida: results/modelo_resultados.json (contrato Unity).
"""

import json
import os
import sys

import openseespy.opensees as ops

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import datos_edificio as d
import construir
import cargas
from voladizos import (agregar_voladizos_y_cubierta,
                       agregar_arriostramiento_voladizo,
                       agregar_voladizo_piso1_ejeF, integrar_voladizo)

OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..",
                       "results")


def construir_con_voladizo(dat=d, offset=(0.0, 0.0)):
    """Construye el modelo base y le inyecta lo trasero del eje A3 (aditivo).

    Equivale a:
        modelo = construir.construir()
        extra  = agregar_voladizos_y_cubierta(modelo=modelo)
        extra += agregar_arriostramiento_voladizo(modelo=modelo, extra=extra)
        extra += agregar_voladizo_piso1_ejeF(modelo=modelo)
        modelo = integrar_voladizo(modelo, extra)

    Los nuevos nodos/elementos (voladizo trasero, aspas de arriostramiento y
    voladizo metalico de piso 1 en el eje F) se agregan a OpenSees y se
    fusionan en `modelo` para que cargas, pesos por nivel y la exportacion
    Unity los incluyan sin tocar el modulo base.
    """
    modelo = construir.construir(dat, offset)
    extra = agregar_voladizos_y_cubierta(modelo=modelo, dat=dat,
                                         offset=offset)
    extra_er = agregar_arriostramiento_voladizo(dat=dat, modelo=modelo,
                                                extra=extra)
    extra_f = agregar_voladizo_piso1_ejeF(dat=dat, modelo=modelo,
                                          offset=offset)
    completo = dict(extra)
    completo["aspas"] = extra_er.get("aspas", [])
    completo["vol_f"] = extra_f
    return integrar_voladizo(modelo, completo, dat=dat, offset=offset)


def reacciones_base():
    """Reacciones de fuerza en la base.

    Nota: NO se suman los momentos locales de cada nodo (mx/my/mz): sumar
    momentos referidos a nodos distintos carece de sentido fisico.
    """
    ops.reactions()
    r = {"fx": 0.0, "fy": 0.0, "fz": 0.0, "por_nodo": {}}
    fixed = ops.getFixedNodes()
    for nt in fixed:
        f = ops.nodeReaction(nt)
        r["por_nodo"][nt] = f
        r["fx"] += f[0]
        r["fy"] += f[1]
        r["fz"] += f[2]
    return r


def desplazamientos_maestros(modelo, dat=d):
    out = {}
    for lv in range(dat.NLEV):
        mt = modelo["master"][lv]
        out[lv] = {
            "ux": ops.nodeDisp(mt, 1),
            "uy": ops.nodeDisp(mt, 2),
            "rz": ops.nodeDisp(mt, 6),
            "z": dat.LEVEL_Z[lv],
        }
    return out


def correr_caso(nombre, modelo, dat=d):
    aplicada = {"fx": 0.0, "fy": 0.0, "fz": 0.0}
    if nombre == "G":
        _, tot = cargas.cargas_gravedad(dat.Q_G, modelo, dat=dat)
        aplicada["fz"] = tot
    elif nombre == "Q":
        _, tot = cargas.cargas_gravedad(dat.Q_Q, modelo, dat=dat,
                                        incluir_pp=False)
        aplicada["fz"] = tot
    elif nombre == "GQ":
        _, t1 = cargas.cargas_gravedad(dat.Q_G, modelo, dat=dat,
                                       ts_tag=11, pat_tag=11)
        _, t2 = cargas.cargas_gravedad(dat.Q_Q, modelo, dat=dat, ts_tag=12,
                                       pat_tag=12, incluir_pp=False)
        aplicada["fz"] = t1 + t2
    if nombre == "EX":
        pesos = cargas.pesos_por_nivel(modelo, dat=dat)
        v_base, fuerzas = cargas.patron_sismico("X", modelo, pesos, dat=dat)
        aplicada["fx"] = v_base
        z0 = dat.LEVEL_Z[0]
        aplicada["momento_volcante"] = sum(
            f * (dat.LEVEL_Z[lv] - z0) for lv, f in fuerzas.items())
    elif nombre == "EY":
        pesos = cargas.pesos_por_nivel(modelo, dat=dat)
        v_base, fuerzas = cargas.patron_sismico("Y", modelo, pesos, dat=dat)
        aplicada["fy"] = v_base
        z0 = dat.LEVEL_Z[0]
        aplicada["momento_volcante"] = sum(
            f * (dat.LEVEL_Z[lv] - z0) for lv, f in fuerzas.items())

    ops.system("BandGen")
    ops.numberer("RCM")
    ops.constraints("Transformation")
    ops.integrator("LoadControl", 1.0)
    ops.algorithm("Linear")
    ops.analysis("Static")
    ok = ops.analyze(1)
    if ok != 0:
        raise RuntimeError(f"analisis fallo en caso {nombre}")
    return {
        "aplicada": aplicada,
        "reacciones": reacciones_base(),
        "desplazamientos": desplazamientos_maestros(modelo, dat=dat),
    }


def _exportar_cargas(dat, modelo, resultados):
    """Cargas aplicadas en el modelo para visualizacion y verificacion."""
    A_c, _, _, _ = dat.sec_columna()
    wG, lng, _, _ = cargas.distribuir_tributaria(dat.Q_G, modelo, dat=dat)
    wQ, _, _, _ = cargas.distribuir_tributaria(dat.Q_Q, modelo, dat=dat)
    vigas = []
    for cat in ("vigas_x", "vigas_y"):
        for v in modelo[cat]:
            tag = v["tag"]
            L = lng[tag]
            a_pp = v.get("A_pp", dat.sec_viga()[0])
            rho_pp = v.get("rho_pp", dat.GAMMA_C)
            qg = wG[tag] + rho_pp * a_pp
            qq = wQ[tag]
            vigas.append({
                "tag": tag, "tipo": cat, "nivel": v["nivel"],
                "ni": v["ni"], "nj": v["nj"], "L": L,
                "qG": qg, "qQ": qq, "G": qg * L, "Q": qq * L,
            })
    extra = 0.0
    for col in modelo["columns"]:
        h = dat.STORY_H[col["story"]]
        extra += col.get("A_pp", A_c) * col.get("rho_pp", dat.GAMMA_C) * h
    for muro in modelo["walls"]:
        h = dat.STORY_H[muro["story"]]
        extra += dat.GAMMA_C * muro["t"] * muro["L"] * h
    for aspa in modelo.get("aspas", []):
        extra += aspa["rho_pp"] * aspa["A_pp"] * aspa["L"]
    pesos = cargas.pesos_por_nivel(modelo, dat=dat)
    sismo = {}
    for dirn, caso in (("X", "EX"), ("Y", "EY")):
        v_base, fuerzas = cargas.sismo_v_base_y_fuerzas(dat, pesos, dirn)
        sismo[caso] = {"V": v_base,
                       "F_por_nivel": {str(lv): f for lv, f in fuerzas.items()}}
    return {
        "vigas": vigas,
        "q_losa": {"G": dat.Q_G, "Q": dat.Q_Q},
        "peso_columnas_muros": extra,
        "pesos_por_nivel": {str(lv): v for lv, v in pesos.items()},
        "sismo": sismo,
    }


def exportar_json(dat, modelo, resultados, out_name, nombre_proyecto,
                  offset=(0.0, 0.0), esfuerzos=None, out_dir=None):
    ox, oy = offset
    data = {
        "proyecto": f"Edificio de Ingenieria 2017_67 - {nombre_proyecto}",
        "unidades": "m, kN",
        "geometria": {
            "grid_x": dat.GRID_X, "grid_y": dat.GRID_Y, "niveles_z": dat.LEVEL_Z,
            "secciones": {
                "pilar": "0.70x0.70",
                "viga": "V.60/80",
                "muros": [{"id": w["id"], **w} for w in dat.WALLS],
            },
        },
        "nodos": {},
        "elementos": [],
        "cargas": {},
        "resultados": {},
    }
    inv_nid = {v: k for k, v in modelo["nid"].items()}
    xs_ord = sorted(set(dat.GRID_X.values()))
    ys_ord = sorted(set(dat.GRID_Y.values()))
    for tag, (xi, yi, lvl) in inv_nid.items():
        data["nodos"][str(tag)] = {
            "x": xs_ord[xi] + ox, "y": ys_ord[yi] + oy, "z": dat.LEVEL_Z[lvl],
            "nivel": lvl}
    for (wi, lvl, ep), tag in modelo["wall_nodes"].items():
        w = dat.WALLS[wi]
        clave = str(tag)
        if clave in data["nodos"]:
            data["nodos"][clave]["rol"] = f"muro_{w['id']}"
            continue
        ex, ey = construir._wall_endpoints(dat, w)[ep]
        data["nodos"][clave] = {
            "x": ex + ox, "y": ey + oy, "z": dat.LEVEL_Z[lvl], "nivel": lvl,
            "rol": f"muro_{w['id']}"}
    cx_m = sum(xs_ord) / len(xs_ord) + ox
    cy_m = sum(ys_ord) / len(ys_ord) + oy
    for lvl, mt in modelo["master"].items():
        data["nodos"][str(mt)] = {
            "x": cx_m, "y": cy_m, "z": dat.LEVEL_Z[lvl], "nivel": lvl,
            "rol": "maestro_diafragma"}
    for nd in modelo.get("voladizo", {}).get("nodos", []):
        data["nodos"][str(nd["tag"])] = {
            "x": nd["x"], "y": nd["y"], "z": nd["z"], "nivel": nd["lvl"],
            "rol": nd.get("rol", "voladizo")}
    nivel_de = {tag: lvl for (xi, yi, lvl), tag in modelo["nid"].items()}
    for (wi, lvl, ep), tag in modelo["wall_nodes"].items():
        nivel_de[tag] = lvl
    for lvl, mt in modelo["master"].items():
        nivel_de[mt] = lvl
    for nd in modelo.get("voladizo", {}).get("nodos", []):
        nivel_de[nd["tag"]] = nd["lvl"]
    apoyos = []
    for category in ("columns", "walls"):
        for el in modelo[category]:
            lv_i = nivel_de[el["ni"]]
            lv_j = nivel_de[el["nj"]]
            base = el["ni"] if lv_i < lv_j else el["nj"]
            if nivel_de[base] != 0:
                continue
            apoyos.append({"tag": base, "tipo": "empotrado",
                           "constraint": [1, 1, 1, 1, 1, 1]})
    apoyos = sorted({a["tag"]: a for a in apoyos}.values(),
                    key=lambda a: a["tag"])
    data["apoyos"] = apoyos
    for cat in ("columns", "walls", "vigas_x", "vigas_y"):
        for el in modelo[cat]:
            data["elementos"].append({
                "tag": el["tag"], "tipo": cat.rstrip("s"),
                "ni": el["ni"], "nj": el["nj"]})
    for el in modelo.get("aspas", []):
        data["elementos"].append({
            "tag": el["tag"], "tipo": "aspa", "ni": el["ni"], "nj": el["nj"]})
    data["cargas"] = _exportar_cargas(dat, modelo, resultados)
    for caso, res in resultados.items():
        data["resultados"][caso] = {
            "aplicada": res["aplicada"],
            "reacciones_totales": {k: v for k, v in res["reacciones"].items()
                                   if k != "por_nodo"},
            "desplazamientos_maestro": res["desplazamientos"],
        }
    if esfuerzos:
        data["esfuerzos"] = esfuerzos
    destino = os.path.join(out_dir or OUT_DIR, out_name)
    os.makedirs(os.path.dirname(destino), exist_ok=True)
    with open(destino, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=1)
    return data


def run_analisis(dat, out_name, nombre_proyecto, offset=(0.0, 0.0),
                 out_dir=None):
    """Analiza el Edificio A y escribe `out_name` en `out_dir` (por defecto
    `results/`). Los tests pasan un directorio temporal para no pisar los
    resultados canonicos de la entrega."""
    os.makedirs(OUT_DIR, exist_ok=True)
    print("=" * 70)
    print(f"MODELO 3D - {nombre_proyecto} (planos 2017_67, M. Kupfer C.)")
    print("=" * 70)

    # --- verificacion tributaria ---
    xs = sorted(set(dat.GRID_X.values()))
    ys = sorted(set(dat.GRID_Y.values()))
    x_borde = dat.GRID_X[dat.ANEXO["x0"]]
    anexo_lv = dat.anexo_levels()
    area_principal = x_borde * (ys[-1] - ys[0])
    area_voladizo = (xs[-1] - x_borde) * (ys[-1] - ys[0])
    n_voladizo = len([lv for lv in range(1, dat.NLEV) if lv in anexo_lv])
    area_real = area_principal + area_voladizo
    area_cargada_total = area_principal * (dat.NLEV - 1) \
        + area_voladizo * n_voladizo

    modelo = construir_con_voladizo(dat, offset)
    w_losa, longs, _, transferido = cargas.distribuir_tributaria(
        dat.Q_G, modelo, dat=dat)
    n_pisos = dat.NLEV - 1
    print(f"[VERIF-1] Area losa principal   : {area_principal:10.4f} m2 "
          f"x {n_pisos} pisos")
    print(f"[VERIF-1] Area losa voladizo    : {area_voladizo:10.4f} m2 "
          f"x {n_voladizo} pisos (anexo)")
    print(f"[VERIF-1] Carga G transferida   : {transferido:10.2f} kN")
    print(f"[VERIF-1] G esperado            : {dat.Q_G * area_cargada_total:10.2f} kN")
    err_trib = abs(transferido - dat.Q_G * area_cargada_total)
    print(f"[VERIF-1] Error conservacion    : {err_trib:.2e} kN "
          f"({'OK' if err_trib < 1e-6 else 'FALLA'})")

    resultados = {}
    for caso in ("G", "Q", "GQ", "EX", "EY"):
        ops.wipe()
        modelo = construir_con_voladizo(dat, offset)
        res = correr_caso(caso, modelo, dat=dat)
        resultados[caso] = res
        rx = res["reacciones"]
        print(f"\nCaso {caso}: analizado OK")
        print(f"  Sum Fx react = {rx['fx']:12.4f} kN | Fy = {rx['fy']:12.4f} kN | "
              f"Fz = {rx['fz']:14.4f} kN")
        roof = res["desplazamientos"][dat.NLEV - 1]
        print(f"  Techo: ux={roof['ux'] * 1000:8.3f} mm  uy={roof['uy'] * 1000:8.3f} mm")

    # --- verificacion equilibrio ---
    print("\n[VERIF-2] Equilibrio por caso (reacciones vs cargas aplicadas):")
    ok_all = True
    for caso, res in resultados.items():
        rx = res["reacciones"]
        ap = res["aplicada"]
        e_fx = abs(rx["fx"] + ap["fx"])
        e_fy = abs(rx["fy"] + ap["fy"])
        e_fz = abs(rx["fz"] - ap["fz"])
        estado = "OK" if max(e_fx, e_fy, e_fz) < 1e-3 else "REVISAR"
        if estado != "OK":
            ok_all = False
        print(f"  {caso}: |errFx|={e_fx:.2e} |errFy|={e_fy:.2e} "
              f"|errFz|={e_fz:.2e} (Fz react={rx['fz']:.1f}) -> {estado}")

    # --- verificacion superposicion ---
    print("\n[VERIF-3] Superposicion R(G)+R(Q) vs R(GQ):")
    difs = []
    n_gq = set(resultados["GQ"]["reacciones"]["por_nodo"].keys())
    for nt in n_gq:
        fg = resultados["G"]["reacciones"]["por_nodo"][nt]
        fq = resultados["Q"]["reacciones"]["por_nodo"][nt]
        fgq = resultados["GQ"]["reacciones"]["por_nodo"][nt]
        for i in range(6):
            difs.append(abs(fg[i] + fq[i] - fgq[i]))
    err_sup = max(difs) if difs else 0.0
    print(f"  Max diferencia = {err_sup:.2e} kN -> "
          f"{'OK' if err_sup < 1e-3 else 'REVISAR'}")

    # --- derivas ---
    print("\nDerivas de piso (casos sismicos):")
    for caso in ("EX", "EY"):
        disp = resultados[caso]["desplazamientos"]
        key = "ux" if caso == "EX" else "uy"
        print(f"  Caso {caso}:")
        for st in range(1, dat.NLEV):
            dr = (disp[st][key] - disp[st - 1][key]) / dat.STORY_H[st - 1]
            print(f"    Piso {st}: dr = {dr * 1000:7.3f} mrad")

    # --- pasada dedicada de diagramas de viga (aditivo; G/Q/GQ/EX/EY) ---
    print("\n" + "=" * 70)
    print("DIAGRAMAS DE VIGA (pasada dedicada, por caso G/Q/GQ/EX/EY)")
    print("=" * 70)
    import esfuerzos as esf_a
    esfuerzos_bloque = esf_a.correr_esfuerzos(verbose=True, dat=dat, offset=offset)

    data = exportar_json(dat, modelo, resultados, out_name, nombre_proyecto,
                         offset, esfuerzos=esfuerzos_bloque, out_dir=out_dir)
    ops.wipe()
    print("\nListo. Resultados en:", os.path.abspath(OUT_DIR))
    return data


def main():
    return run_analisis(d, "modelo_resultados.json",
                        "Edificio A - 50 m E-J (anexo metalico I'-J)")


if __name__ == "__main__":
    main()
