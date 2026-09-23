# -*- coding: utf-8 -*-
"""
semana03_run.py — Recorre TODA la semana 03 (motor de secciones aditivo).

Calcula y persiste en `results/secciones_semana03.json`:
- curvas M-φ de la columna 70x70 (8φ28) y el muro B (2.91×0.60) a varias
      cargas axiales (12×12) + detalle a P=0 con las mallas 6/12/20;
    - envolventes P–M (17 puntos) con balanceado analítico;
    - demandas (P, M) POR CASO (G/Q/GQ/EX/EY) de todos los pilares/muros del
      Edificio B (reutilizando `edificio_b/esfuerzos._correr_caso`), el mapa
      elemento → sección y la verificación de superposición G+Q ≡ GQ;
    - catalogado P–M de los 11 muros del Edificio B (aditivo, §3) y su
      inyección al contrato del visor sólido;
    - verificación §4 de superposición con corridas DIRECTAS (G+EX, G+Q+EX)
      de los Edificios A y B frente a la suma de casos (secciones.superposicion);
    - figuras PNG en `reports/fig/`.

Es 100% ADITIVO: no modifica los modelos lineales (A o B) ni sus JSON.

Uso: python scripts\\semana03_run.py [--recalcular]
"""
import json
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

AQUI = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.abspath(os.path.join(AQUI, "..", "src"))
ROOT = os.path.abspath(os.path.join(AQUI, ".."))
sys.path.insert(0, SRC)

import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

from secciones.seccion import columna_70x70, muro_b1
from secciones.curva import momento_curvatura
from secciones import analitica, pm, demandas

RESULTS = os.path.join(ROOT, "results")
FIG = os.path.join(ROOT, "reports", "fig")
os.makedirs(RESULTS, exist_ok=True)
os.makedirs(FIG, exist_ok=True)


def adelgazar(kaps, moms, n=500):
    if len(kaps) <= n:
        return list(kaps), list(moms)
    idx = np.linspace(0, len(kaps) - 1, n).astype(int)
    return [kaps[i] for i in idx], [moms[i] for i in idx]


def curvas_de(col, wall):
    """Curvas M-φ y envolventes P-M de columna y muro."""
    out = {}
    for nombre, sec in (("col", col), ("muro", wall)):
        env = pm.curva_PM(sec, n_puntos=17, gy=12, gz=12, dk=1.0e-4,
                          nincr=2000)
        P, M = env.PS, env.Ms
        out[nombre] = {
            "A": sec.b * sec.h,
            "As": sec.As,
            "P0_aci": sec.p0_aci(),
            "P0_fibra": sec.p0_fibra(),
            "balanceado": {"P": env.P_bal, "M": env.M_bal},
            "envelope": {
                "P": [round(float(x), 3) for x in P],
                "M": [round(float(x), 3) for x in M],
                "EI0": [round(float(x), 3) for x in env.EI0s],
                "MOTIVO": [x for x in env.motivos],
            },
        }
    # M-φ detalle: columna P=0 con mallas 6/12/20 y columna a P≈demanda
    mfi_col = {}
    for gy in (6, 12, 20):
        c = momento_curvatura(col, 0.0, gy=gy, gz=gy, dk=5.0e-5, nincr=3000)
        k, m = adelgazar(c.kaps, c.moms)
        mfi_col[str(gy)] = {"k": k, "M": m, "EI0": c.EI0, "Mmax": c.Mmax,
                            "kmax": c.k_max, "MOTIVO": c.motivo}
    c = momento_curvatura(col, -3000.0, gy=12, gz=12, dk=5.0e-5, nincr=3000)
    k, m = adelgazar(c.kaps, c.moms)
    mfi_col["P3000"] = {"k": k, "M": m, "EI0": c.EI0, "Mmax": c.Mmax,
                        "kmax": c.k_max, "MOTIVO": c.motivo}
    out["col"]["mfi"] = mfi_col

    cw = momento_curvatura(wall, -0.5 * wall.p0_aci(), gy=12, gz=12,
                           dk=2.0e-5, nincr=6000)
    k, m = adelgazar(cw.kaps, cw.moms)
    out["muro"]["mfi"] = {"P05": {"k": k, "M": m, "EI0": cw.EI0,
                                  "Mmax": cw.Mmax, "kmax": cw.k_max,
                                  "MOTIVO": cw.motivo}}
    return out


def superposicion(dem):
    """Máxima desviación G+Q vs GQ por COMPONENTE (P, My, Mz)."""
    tags = set()
    for caso in ("G", "Q", "GQ"):
        tags |= set(dem.get(caso, {}))
    dP, dMy, dMz, n = 0.0, 0.0, 0.0, 0
    for t in sorted(tags):
        g, q, gq = dem.get("G", {}).get(t), dem.get("Q", {}).get(t), \
            dem.get("GQ", {}).get(t)
        if not (g and q and gq):
            continue
        dP = max(dP, abs(gq["P"] - (g["P"] + q["P"])))
        dMy = max(dMy, abs(gq["My"] - (g["My"] + q["My"])))
        dMz = max(dMz, abs(gq["Mz"] - (g["Mz"] + q["Mz"])))
        n += 1
    return {"n_elementos": n, "max_dP": dP, "max_dMy": dMy, "max_dMz": dMz,
            "max_dM": max(dMy, dMz)}


def elegir_critica(dem, elem, tipo="pilar"):
    """Elemento (tag) de mayor M entre sus casos, de un tipo dado."""
    mejor = None
    for tag, info in elem.items():
        if info["tipo"] != tipo:
            continue
        if tipo == "muro" and info["seccion"] != "muro0.60x2.91":
            continue
        pares = [(dem[c][tag]["M"], c) for c in dem if tag in dem[c]]
        if not pares:
            continue
        mx = max(pares)
        if mejor is None or mx[0] > mejor[0]:
            mejor = (mx[0], mx[1], tag)
    return mejor


def figura_col_mfi(mfi):
    fig, ax = plt.subplots(figsize=(8.5, 5.2))
    for gy in ("6", "12", "20"):
        d = mfi[gy]
        ax.plot(np.asarray(d["k"]), np.asarray(d["M"]), lw=1.4,
                label=f"malla {gy}×{gy}  (EI₀={d['EI0']:.0f})")
    ax.set_xlabel(r"Curvatura $\kappa$ [1/m]")
    ax.set_ylabel(r"Momento $M$ [kN·m]")
    ax.set_title("Columna 70×70 — 8φ28 — M-φ a $P=0$ (hormigón 25 MPa, "
                 r"acero 420 MPa)")
    ax.grid(alpha=0.3)
    ax.legend()
    fig.tight_layout()
    fig.savefig(os.path.join(FIG, "semana03_columna_mfi.png"), dpi=160)
    plt.close(fig)


def figura_pm(nombre, sec, env, crit, dem_por_caso):
    fig, ax = plt.subplots(figsize=(8.5, 5.6))
    ev = env["envelope"]
    ax.plot(ev["P"], ev["M"], "o-", lw=1.5, color="#1f77b4",
            label="Envolvente P–M (fibros)")
    b = env.get("balanceado")
    if b:
        ax.plot([b["P"]], [b["M"]], "s", ms=9, color="#2ca02c",
                label=f"Balanceado ACI ({b['P']:.0f}, {b['M']:.0f})")
    ax.axhline(0, color="k", lw=0.6)
    casos = ("G", "Q", "GQ", "EX", "EY")
    cols = {"G": "#d62728", "Q": "#ff7f0e", "GQ": "#9467bd",
            "EX": "#17becf", "EY": "#8c564b"}
    for caso in casos:
        dm = dem_por_caso.get(caso, {})
        if dm:
            ax.plot([dm["P"]], [dm["M"]], "o", ms=8, color=cols[caso],
                    label=f"Demanda {caso} ({dm['P']:.0f}, {dm['M']:.0f})",
                    zorder=5)
    ax.set_xlabel(r"Carga axial $P$ [kN] (compresión positiva)")
    ax.set_ylabel(r"Momento máximo $M_{max}$ [kN·m]")
    ax.set_title(f"{nombre} — ({sec.Ag:.2f} m²) — envolvente P–M y demandas")
    ax.grid(alpha=0.3)
    ax.legend(fontsize=8)
    fig.tight_layout()
    fig.savefig(os.path.join(FIG, f"semana03_{nombre.split()[0].lower()}_pm.png"),
                dpi=160)
    plt.close(fig)


def figura_superposicion(sup):
    fig, ax = plt.subplots(figsize=(6.5, 3.6))
    ax.bar(["máx |ΔP|", "máx |ΔM|"], [sup["max_dP"], sup["max_dM"]],
           color=["#1f77b4", "#ff7f0e"])
    ax.set_title(f"Superposición G+Q ≡ GQ en pilares/muros "
                 f"({sup['n_elementos']} elementos)")
    ax.set_ylabel("desviación [kN / kN·m]")
    for i, v in enumerate([sup["max_dP"], sup["max_dM"]]):
        ax.text(i, v, f"{v:.3e}", ha="center", va="bottom", fontsize=8)
    ax.grid(alpha=0.3, axis="y")
    fig.tight_layout()
    fig.savefig(os.path.join(FIG, "semana03_superposicion.png"), dpi=160)
    plt.close(fig)


def figura_muros_pm(curvas):
    """Envolventes P–M de TODOS los muros del catálogo (11 secciones)."""
    fig, ax = plt.subplots(figsize=(9.0, 6.2))
    etiquetas = sorted(k for k in curvas
                       if k.startswith("muro") and k.count("x") == 1)
    for k in etiquetas:
        env = curvas[k]["envelope"]
        ax.plot(env["P"], env["M"], lw=1.5, label=f"{k} (P0={curvas[k]['P0_aci']:.0f})")
    ax.axhline(0, color="k", lw=0.6)
    ax.set_xlabel(r"Carga axial $P$ [kN] (compresión positiva)")
    ax.set_ylabel(r"Momento máximo $M_{max}$ [kN·m]")
    ax.set_title("Catálogo P–M de los 11 muros del Edificio B "
                 "(fibras 12×12, f'c=25 MPa, fy=420 MPa)")
    ax.grid(alpha=0.3)
    ax.legend(fontsize=8, ncol=2)
    fig.tight_layout()
    fig.savefig(os.path.join(FIG, "semana03_muros_pm.png"), dpi=160)
    plt.close(fig)


def figura_mosaico():
    """Mosaico tributario del Edificio B: vigas coloreadas por carga de losa G."""
    import json as _json
    with open(os.path.join(RESULTS, "modelo_resultados_b.json"),
              encoding="utf-8") as f:
        doc = _json.load(f)
    nodos = {str(n["id"]): (n["x"], n["y"], n["z"]) for n in doc["nodos"]}
    fig, ax = plt.subplots(figsize=(9.0, 6.0))
    from edificio_b import datos_edificio as D_B
    for (x, y0, y1, e_) in D_B.MUROS_V:
        ax.plot([x, x], [y0, y1], color="0.35", lw=4, alpha=0.55)
    for (y, x0, x1, e_) in D_B.MUROS_H:
        ax.plot([x0, x1], [y, y], color="0.35", lw=4, alpha=0.55)
    for (xp, yp) in D_B.PILARES:
        ax.add_patch(plt.Rectangle((xp - 0.35, yp - 0.35), 0.7, 0.7,
                                   facecolor="0.15", edgecolor="none"))
    qmax = max(v["qG"] for v in doc["cargas"]["vigas"]) or 1.0
    cmap = plt.cm.viridis
    for v in doc["cargas"]["vigas"]:
        ni, nj = str(v["ni"]), str(v["nj"])
        if ni not in nodos or nj not in nodos:
            continue
        (x1, y1, _), (x2, y2, _) = nodos[ni], nodos[nj]
        ax.plot([x1, x2], [y1, y2], color=cmap(v["qG"] / qmax), lw=1.8)
    sm = plt.cm.ScalarMappable(cmap=cmap,
                               norm=plt.Normalize(0.0, qmax))
    cbar = fig.colorbar(sm, ax=ax)
    cbar.set_label("Carga de losa G por viga [kN/m]")
    ax.set_aspect("equal")
    ax.set_xlabel("x [m]")
    ax.set_ylabel("y [m]")
    ax.set_title("Edificio B — mosaico tributario (G por viga, áreas de losa)")
    fig.tight_layout()
    fig.savefig(os.path.join(FIG, "semana03_mosaico.png"), dpi=160)
    plt.close(fig)


def main():
    recalcular = "--recalcular" in sys.argv
    col = columna_70x70()
    wall = muro_b1()
    print(f"[COLUMNA] {col}  P0 ACI={col.p0_aci():.0f}  "
          f"P0 fibra={col.p0_fibra():.0f}")
    print(f"[MURO]    {wall}  P0 ACI={wall.p0_aci():.0f}")

    cache = {}
    if not recalcular and os.path.exists(demandas.ARCHIVO_CACHE):
        with open(demandas.ARCHIVO_CACHE, encoding="utf-8") as f:
            cache = json.load(f)

    if recalcular or "curvas" not in cache:
        curvas = curvas_de(col, wall)
        demandas.guardar({"curvas": curvas})
    else:
        curvas = cache["curvas"]
        print("[CACHE] curvas M-φ reutilizadas")

    if recalcular or "demandas" not in cache:
        dem = demandas.calcular(verbose=True)
        demandas.guardar({"demandas": dem})
    else:
        dem = cache["demandas"]
        print("[CACHE] demandas reutilizadas")

    if recalcular or "per_elemento" not in cache:
        elem = demandas.per_elemento(verbose=True)
    else:
        elem = cache["per_elemento"]

    sup = superposicion(dem)
    demandas.guardar({"per_elemento": elem, "superposicion": sup})
    print(f"[SUPERPOSICION] G+Q vs GQ: {sup['n_elementos']} elementos, "
          f"máx dP={sup['max_dP']:.3e} kN, máx dM={sup['max_dM']:.3e} kN·m")

    crit_col = elegir_critica(dem, elem, "pilar")
    crit_mur = elegir_critica(dem, elem, "muro")
    print(f"[CRITICA] columna: tag={crit_col[2]} caso={crit_col[1]} "
          f"M={crit_col[0]:.1f} | muro: tag={crit_mur[2]} caso={crit_mur[1]} "
          f"M={crit_mur[0]:.1f}")

    # demandas por caso de los elementos críticos
    dem_por_caso_col = {c: dem[c].get(crit_col[2]) for c in ("G", "Q", "GQ", "EX", "EY")}
    dem_por_caso_mur = {c: dem[c].get(crit_mur[2]) for c in ("G", "Q", "GQ", "EX", "EY")}

    figura_col_mfi(curvas["col"]["mfi"])
    figura_pm("Columna 70×70 (8φ28)", col, curvas["col"], crit_col,
              dem_por_caso_col)
    figura_pm("Muro 2.91×0.60", wall, curvas["muro"], crit_mur,
              dem_por_caso_mur)
    figura_superposicion(sup)

    # ------------------------------------------------------------------
    # AMPLIACION semana03 §3/§4 (aditivo):
    #   §3 catálogo P-M de los 11 muros + inyección al visor sólido
    #   §4 superposición con corridas directas de A y B (G+EX, G+Q+EX)
    # ------------------------------------------------------------------
    from secciones import catalogo_muros
    cache, añadidos = catalogo_muros.completar_cache(cache, force=recalcular)
    if añadidos:
        print(f"[CATALOGO] {len(añadidos)} envolventes de muro añadidas")
    else:
        print("[CATALOGO] envolventes de muro ya en el cache")

    from secciones import superposicion as sp_mod
    if recalcular or "combinaciones" not in cache.get("superposicion", {}):
        sp_mod.verificar(cache, correr_directo=True)
    else:
        print("[SUPERPOSICION] §4 combinaciones ya en cache "
              "(--recalcular para repetir corridas directas)")

    demandas.guardar(cache)

    figura_muros_pm(cache["curvas"])
    figura_mosaico()

    try:
        from secciones import exportar_unity as overlay
        overlay.exportar(verbose=True)
    except Exception as exc:
        print("[OVERLAY] omitido:", exc)

    print(f"\n[CACHE]  {demandas.ARCHIVO_CACHE}")
    print(f"[FIGURAS] {FIG}")
    print("DONE")


if __name__ == "__main__":
    main()