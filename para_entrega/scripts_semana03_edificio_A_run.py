# -*- coding: utf-8 -*-
"""
semana03_edificio_A_run.py — Catalogo P-M, demandas y mapa elemento→sección
del Edificio A (ADITIVO a la semana 03, motor de secciones).

Calcula y persiste en `results/secciones_semana03.json` (claves propias de A,
sin tocar las de B) usando el MISMO motor validado `secciones.pm`:
  - `curvas_A`        : envolventes P-M (17 puntos, 12×12) de la columna
                        `col_A_0.70x0.70` (8φ28) y de los 5 muros
                        `muro_A_{t}x{L}` de `datos_edificio.WALLS`;
  - `demandas_A`      : (P, M) POR CASO (G/Q/GQ/EX/EY) de pilares/muros de A
                        (reutiliza `benchmark_3d/esfuerzos._correr_caso`);
  - `per_elemento_A`  : mapa elemento → sección (etiquetas `col_A_*`/`muro_A_*`);
  - `superposicion_A` : verificación G+Q ≡ GQ en los elementos de A;
  - `fc_secciones`    : documentación del supuesto (f'c A = 30 MPa, armado).

ARMADO DE MUROS DE A (supuesto confirmado por el usuario: no hay armadura en
los planos; se adopta la disposición de B y estos valores — ver reports/):
  f'c = 30 MPa (G35) ; fy = 420 MPa ; recubrimiento 3 cm ;
  alma φ10 @ 0.20 m doble malla (ρ_v ≈ 0.003) ;
  borde 6φ16 por extremo sobre longitud max(2·t, 0.15·L).

Es 100% ADITIVO y idempotente. Uso: python scripts\\semana03_edificio_A_run.py
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

from secciones import pm
from secciones.seccion import columna_A_70x70, muros_A
from secciones import demandas_a

CACHE = os.path.join(ROOT, "results", "secciones_semana03.json")
FIG = os.path.join(ROOT, "reports", "fig")
os.makedirs(FIG, exist_ok=True)

N_PUNTOS = 17
GY = GZ = 12
DK = 1.0e-4
NINCR = 2000

CASOS = ("G", "Q", "GQ", "EX", "EY")

SUPUESTO = {
    "fc_mpa": 30.0,
    "fy_mpa": 420.0,
    "hormigon": "G35 (supuesto de seccion: f'c = 30 MPa, ver reports/)",
    "recubrimiento_muro_cm": 3,
    "alma_muro": "phi10 @ 0.20 m doble malla (rho_v ~ 0.003)",
    "borde_muro": "6 phi16 por extremo, longitud max(2*t, 0.15*L)",
    "columna": "70x70 con 8 phi28 (plano 101, capa RLE-PILAR)",
    "nota": "Supuesto documentado: los planos de A no dan armadura de muros; "
            "se reutiliza la disposicion de B (muro_generico: alma distribuida "
            "+ zonas de borde).",
}


def entrada(sec, env):
    return {
        "A": sec.Ag,
        "As": sec.As,
        "P0_aci": sec.p0_aci(),
        "P0_fibra": sec.p0_fibra(),
        "balanceado": {"P": env.P_bal, "M": env.M_bal},
        "envelope": {
            "P": [round(float(x), 3) for x in env.PS],
            "M": [round(float(x), 3) for x in env.Ms],
            "EI0": [round(float(x), 3) for x in env.EI0s],
            "MOTIVO": [x for x in env.motivos],
        },
    }


def curvas_edificio_A(verbose=True):
    """{etiqueta: entrada} con la envolvente P-M de las 6 secciones de A."""
    import time
    secs = [columna_A_70x70()] + muros_A()
    out = {}
    for sec in secs:
        t0 = time.time()
        env = pm.curva_PM(sec, n_puntos=N_PUNTOS, gy=GY, gz=GZ, dk=DK,
                          nincr=NINCR)
        out[sec.nombre] = entrada(sec, env)
        if verbose:
            print(f"  {sec.nombre:18s} As={sec.As:.6f} "
                  f"P0_aci={sec.p0_aci():8.1f} P0_fibra={sec.p0_fibra():8.1f} "
                  f"({time.time() - t0:.1f} s)")
    return out


def superposicion(dem):
    """Máxima desviación G+Q vs GQ por COMPONENTE (P, My, Mz) de A."""
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


def figura_pm_A(curvas):
    """Envolventes P-M de las 6 secciones del Edificio A."""
    fig, ax = plt.subplots(figsize=(9.0, 6.2))
    for k in sorted(curvas):
        env = curvas[k]["envelope"]
        ax.plot(env["P"], env["M"], lw=1.5,
                label=f"{k} (P0={curvas[k]['P0_aci']:.0f})")
    ax.axhline(0, color="k", lw=0.6)
    ax.set_xlabel(r"Carga axial $P$ [kN] (compresión positiva)")
    ax.set_ylabel(r"Momento máximo $M_{max}$ [kN·m]")
    ax.set_title("Catalogo P-M del Edificio A (fibras 12x12, "
                 r"f'c=30 MPa G35, fy=420 MPa)")
    ax.grid(alpha=0.3)
    ax.legend(fontsize=8, ncol=2)
    fig.tight_layout()
    fig.savefig(os.path.join(FIG, "semana03_edificio_A_pm.png"), dpi=160)
    plt.close(fig)


def main():
    recalcular = "--recalcular" in sys.argv
    cache = {}
    if os.path.exists(CACHE):
        with open(CACHE, encoding="utf-8") as f:
            cache = json.load(f)

    if recalcular or "curvas_A" not in cache:
        print("[CATALOGO-A] envolventes P-M (motor real, 17 puntos 12x12):")
        cache["curvas_A"] = curvas_edificio_A(verbose=True)
    else:
        print("[CACHE] curvas_A reutilizadas")

    if recalcular or "demandas_A" not in cache:
        cache["demandas_A"] = demandas_a.calcular(verbose=True)
    else:
        print("[CACHE] demandas_A reutilizadas")

    if recalcular or "per_elemento_A" not in cache:
        cache["per_elemento_A"] = demandas_a.per_elemento(verbose=True)
    else:
        print("[CACHE] per_elemento_A reutilizadas")

    cache["superposicion_A"] = superposicion(cache["demandas_A"])
    cache["fc_secciones"] = SUPUESTO
    s = cache["superposicion_A"]
    print(f"[SUPERPOSICION-A] G+Q vs GQ: {s['n_elementos']} elementos, "
          f"máx dP={s['max_dP']:.3e} kN, máx dM={s['max_dM']:.3e} kN·m")

    with open(CACHE, "w", encoding="utf-8") as f:
        json.dump(cache, f, ensure_ascii=False, indent=1)

    figura_pm_A(cache["curvas_A"])

    try:
        from secciones import exportar_unity as overlay
        overlay.exportar(verbose=True)
    except Exception as exc:
        print("[OVERLAY] omitido:", exc)

    print(f"\n[CACHE]  {CACHE}")
    print(f"[FIGURAS] {FIG}")
    print("DONE-A")


if __name__ == "__main__":
    main()