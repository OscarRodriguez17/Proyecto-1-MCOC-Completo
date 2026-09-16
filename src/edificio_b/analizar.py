# -*- coding: utf-8 -*-
"""
analizar.py — Análisis del Edificio B y exportación de
results/modelo_resultados_b.json (contrato para fusión y visualizadores).

PARIDAD CON EL EDIFICIO A: corre los casos G, Q, GQ, EX, EY (cada uno en un
modelo recién construido), verifica equilibrio por caso y superposición
R(G)+R(Q)=R(GQ), y exporta `apoyos` + `resultados` por caso.

Compatibilidad (ADITIVO): `correr()` sigue devolviendo {M, Wpp, Wsc, data, out}
y el JSON conserva sus claves previas (n_nodos, n_elementos, niveles,
nodos[list id/x/y/z/ux/uy/uz], elementos, peso_propio_kN, sobrecarga_kN). Los
desplazamientos nodales exportados son los del estado de servicio GQ (= el
antiguo PP+SC, mismos valores). Se AGREGAN: apoyos, resultados, cargas y los
totales sísmicos en el retorno.

Uso:
    python -m edificio_b.analizar        (desde src/)
"""
import os, json, sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

import openseespy.opensees as ops
from .construir import build_model
from . import cargas
from . import sismo
from . import esfuerzos
from . import datos_edificio as D

AQUI     = os.path.dirname(os.path.abspath(__file__))
RESULTS  = os.path.abspath(os.path.join(AQUI, "..", "..", "results"))
OUT_NAME = "modelo_resultados_b.json"

CASOS = ("G", "Q", "GQ", "EX", "EY")


# --------------------------------------------------------------- solver común
def _resolver():
    ops.system('BandGeneral')
    ops.numberer('RCM')
    ops.constraints('Transformation')
    ops.integrator('LoadControl', 1.0)
    ops.algorithm('Linear')
    ops.analysis('Static')
    ok = ops.analyze(1)
    if ok != 0:
        raise RuntimeError("El análisis no convergió (matriz singular).")


def _reacciones_base(M):
    ops.reactions()
    r = {"fx": 0.0, "fy": 0.0, "fz": 0.0, "por_nodo": {}}
    for nt in M.base_nodes:
        f = ops.nodeReaction(nt)
        r["por_nodo"][nt] = f
        r["fx"] += f[0]; r["fy"] += f[1]; r["fz"] += f[2]
    return r


def _desp_maestros(M):
    out = {}
    for k, mt in sorted(M.masters.items()):
        out[k] = {"ux": ops.nodeDisp(mt, 1), "uy": ops.nodeDisp(mt, 2),
                  "rz": ops.nodeDisp(mt, 6), "z": ops.nodeCoord(mt)[2]}
    return out


# ------------------------------------------------------------ correr un caso
def _correr_caso(nombre):
    """Construye un modelo nuevo, aplica el caso y lo resuelve.

    Devuelve {aplicada, reacciones, desplazamientos, M, extra}. `aplicada`
    guarda las fuerzas totales como MAGNITUD positiva (misma convención que A:
    equilibrio fz -> |R.fz - ap.fz|; fx/fy -> |R + ap|).
    """
    M = build_model()
    aplicada = {"fx": 0.0, "fy": 0.0, "fz": 0.0}
    extra = {}

    if nombre in ("G", "Q", "GQ"):
        ops.timeSeries('Constant', 1)
        ops.pattern('Plain', 1, 1)
        wpp = cargas.peso_propio(M) if nombre in ("G", "GQ") else 0.0
        wlosa = cargas.carga_muerta_losa(M) if nombre in ("G", "GQ") else 0.0
        wsc = cargas.sobrecarga(M) if nombre in ("Q", "GQ") else 0.0
        aplicada["fz"] = wpp + wlosa + wsc
        extra["Wpp"] = wpp
        extra["Wlosa"] = wlosa
        extra["Wsc"] = wsc
    elif nombre in ("EX", "EY"):
        pesos = sismo.pesos_por_nivel(M)
        dirn = "X" if nombre == "EX" else "Y"
        v_base, fuerzas = sismo.patron_sismico(dirn, M, pesos)
        if nombre == "EX":
            aplicada["fx"] = v_base
        else:
            aplicada["fy"] = v_base
        aplicada["momento_volcante"] = sismo.momento_volcante(fuerzas)
        extra["pesos_por_nivel"] = pesos
        extra["V_base"] = v_base
        extra["F_por_nivel"] = fuerzas

    _resolver()
    return {"aplicada": aplicada,
            "reacciones": _reacciones_base(M),
            "desplazamientos": _desp_maestros(M),
            "M": M, "extra": extra}


# ------------------------------------------------------------ verificaciones
def _verificar_equilibrio(res):
    """|R.fz-ap.fz|, |R.fx+ap.fx|, |R.fy+ap.fy| por caso."""
    ok = True
    print("\n[VERIF] Equilibrio por caso (reacciones vs cargas aplicadas):")
    for caso, r in res.items():
        rx, ap = r["reacciones"], r["aplicada"]
        e_fx = abs(rx["fx"] + ap["fx"])
        e_fy = abs(rx["fy"] + ap["fy"])
        e_fz = abs(rx["fz"] - ap["fz"])
        estado = "OK" if max(e_fx, e_fy, e_fz) < 1e-3 else "REVISAR"
        ok = ok and estado == "OK"
        print(f"  {caso:3s}: |eFx|={e_fx:.2e} |eFy|={e_fy:.2e} "
              f"|eFz|={e_fz:.2e}  (R.fz={rx['fz']:,.1f}) -> {estado}")
    return ok


def _verificar_superposicion(res):
    """R(G)+R(Q) == R(GQ) en cada nodo base y cada componente."""
    difs = []
    nodos = res["GQ"]["reacciones"]["por_nodo"].keys()
    for nt in nodos:
        fg = res["G"]["reacciones"]["por_nodo"][nt]
        fq = res["Q"]["reacciones"]["por_nodo"][nt]
        fgq = res["GQ"]["reacciones"]["por_nodo"][nt]
        for i in range(6):
            difs.append(abs(fg[i] + fq[i] - fgq[i]))
    err = max(difs) if difs else 0.0
    estado = "OK" if err < 1e-3 else "REVISAR"
    print(f"[VERIF] Superposición R(G)+R(Q) vs R(GQ): máx={err:.2e} kN -> {estado}")
    return err < 1e-3


# ----------------------------------------------------------------- exportar
def exportar_json(M, resultados, disp_export, out_name=OUT_NAME,
                  Wpp=None, Wsc=None, Wlosa=None, esfuerzos=None):
    """Escribe results/<out_name> con el esquema B enriquecido (apoyos +
    resultados). Los desplazamientos nodales son los de `disp_export` (GQ).
    `esfuerzos` (opcional, aditivo) es el bloque {caso: {viga: ...}} de la
    pasada dedicada de diagramas (ver edificio_b/esfuerzos.py)."""
    os.makedirs(RESULTS, exist_ok=True)

    nodos = []
    for n, (x, y, z) in sorted(M.coord.items()):
        ux, uy, uz = disp_export.get(n, (0.0, 0.0, 0.0))[:3]
        nodos.append(dict(id=n, x=round(x, 4), y=round(y, 4), z=round(z, 4),
                          ux=ux, uy=uy, uz=uz))
    elementos = []
    for e, tipo in M.tipo.items():
        n1, n2 = ops.eleNodes(e)
        elementos.append(dict(id=e, ni=n1, nj=n2, tipo=tipo,
                              A=round(M.area[e], 5)))
    niveles = [dict(z=z, nombre=nom) for (z, nom) in D.NIVELES]

    apoyos = [dict(tag=n, tipo="empotrado", constraint=[1, 1, 1, 1, 1, 1])
              for n in sorted(M.base_nodes)]

    # resultados por caso (sin por_nodo; agrega volcante si es sísmico)
    res_out = {}
    for caso, r in resultados.items():
        res_out[caso] = {
            "aplicada": r["aplicada"],
            "reacciones_totales": {k: v for k, v in r["reacciones"].items()
                                   if k != "por_nodo"},
            "desplazamientos_maestro": r["desplazamientos"],
        }
    # bloque de cargas (sismo V y F por nivel, pesos por nivel, tributario/viga)
    ex, ey = resultados["EX"]["extra"], resultados["EY"]["extra"]
    from . import tributario
    vigas = tributario.tributaria_por_viga(M)
    cargas_out = {
        "vigas": vigas,
        "q_losa": {"G": cargas.Q_G_LOSA, "Q": cargas.SC_PISO,
                   "SC_piso": cargas.SC_PISO, "SC_cubierta": cargas.SC_CUBIERTA},
        "pesos_por_nivel": {str(k): round(v, 3)
                            for k, v in ex["pesos_por_nivel"].items()},
        "sismo": {
            "alpha": D.ALPHA_EQ,
            "EX": {"V": round(ex["V_base"], 3),
                   "F_por_nivel": {str(k): round(v, 3)
                                   for k, v in ex["F_por_nivel"].items()}},
            "EY": {"V": round(ey["V_base"], 3),
                   "F_por_nivel": {str(k): round(v, 3)
                                   for k, v in ey["F_por_nivel"].items()}},
        },
    }

    extra = {}
    if Wpp is not None:
        extra["peso_propio_kN"] = round(Wpp, 3)
        extra["sobrecarga_kN"] = round(Wsc, 3)
        if Wlosa is not None:
            extra["carga_muerta_losa_kN"] = round(Wlosa, 3)
            extra["peso_muerto_G_kN"] = round(Wpp + Wlosa, 3)

    data = dict(
        proyecto="Edificio B - Proyecto 1 MCOC",
        unidades=dict(longitud="m", fuerza="kN"),
        n_nodos=len(nodos), n_elementos=len(elementos),
        niveles=niveles, nodos=nodos, elementos=elementos,
        apoyos=apoyos, cargas=cargas_out, resultados=res_out,
        **extra,
    )
    if esfuerzos:
        data["esfuerzos"] = esfuerzos
    out = os.path.join(RESULTS, out_name)
    with open(out, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
    return data


# -------------------------------------------------------------------- correr
def correr(out_name=OUT_NAME, verbose=True):
    """Corre G/Q/GQ/EX/EY, verifica y exporta `results/<out_name>`.

    Retorna dict con: M (modelo GQ), Wpp, Wsc, V_EX, V_EY, data, out.
    Compatible con src/benchmark_3d/fusionar.py (usa Wpp/Wsc).
    """
    if verbose:
        print("=" * 70)
        print("EDIFICIO B — casos G / Q / GQ / EX / EY (paridad con A)")
        print("=" * 70)

    resultados = {}
    disp_export = None
    M_ref = None
    for caso in CASOS:
        r = _correr_caso(caso)
        if caso == "GQ":
            M = r["M"]
            disp_export = {n: ops.nodeDisp(n) for n in M.coord}
            M_ref = M
        resultados[caso] = r

    Wpp = resultados["G"]["extra"]["Wpp"]
    Wlosa = resultados["G"]["extra"]["Wlosa"]
    Wsc = resultados["Q"]["extra"]["Wsc"]
    G = Wpp + Wlosa
    V_EX = resultados["EX"]["extra"]["V_base"]
    V_EY = resultados["EY"]["extra"]["V_base"]

    if verbose:
        print(f"\nG (peso muerto) = {G:,.1f} kN  [marco {Wpp:,.1f} + losa "
              f"{Wlosa:,.1f}]   Q (sobrecarga) = {Wsc:,.1f} kN")
        print(f"V sísmico  EX = {V_EX:,.1f} kN   EY = {V_EY:,.1f} kN   "
              f"(α = {D.ALPHA_EQ:.2f} · G)")
        for caso in ("EX", "EY"):
            d = resultados[caso]["desplazamientos"]
            key = "ux" if caso == "EX" else "uy"
            techo = d[max(d)][key] * 1000.0
            print(f"  {caso}: u_techo({key}) = {techo:8.3f} mm  |  "
                  f"M_volc = {resultados[caso]['aplicada']['momento_volcante']:,.0f} kN·m")

    ok_eq = _verificar_equilibrio(resultados)
    ok_sp = _verificar_superposicion(resultados)
    if verbose:
        print(f"\nRESUMEN verificación: equilibrio={'OK' if ok_eq else 'FALLA'}  "
              f"superposición={'OK' if ok_sp else 'FALLA'}")

    # Pasada dedicada de diagramas (aditivo; no toca el análisis nodal).
    if verbose:
        print("\n" + "=" * 70)
        print("DIAGRAMAS DE VIGA (pasada dedicada, por caso G/Q/GQ/EX/EY)")
        print("=" * 70)
    esf = esfuerzos.correr_esfuerzos(verbose=verbose)

    data = exportar_json(M_ref, resultados, disp_export, out_name,
                         Wpp=Wpp, Wsc=Wsc, Wlosa=Wlosa, esfuerzos=esf)
    if verbose:
        print("Exportado:", os.path.join(RESULTS, out_name))

    return {"M": M_ref, "Wpp": Wpp, "Wlosa": Wlosa, "G": G, "Wsc": Wsc,
            "V_EX": V_EX, "V_EY": V_EY, "data": data, "out": out_name,
            "ok_equilibrio": ok_eq, "ok_superposicion": ok_sp}


if __name__ == '__main__':
    correr(verbose=True)
