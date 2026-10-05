# -*- coding: utf-8 -*-
"""
figuras_referencia.py — Diagramas de referencia (PNG) de los elementos AR.

Lee results/ar_elementos.json y dibuja, por elemento, N, V y M del plano
principal, con los mismos datos y convenciones que usará la app. Sirven para
comparar lo que muestra el teléfono contra el cálculo verificado.

  - Vigas: horizontales, de i (izquierda) a j (derecha); M dibujado hacia su
    lado de tracción (M > 0 = fibra inferior → hacia abajo).
  - Columnas/muros: verticales, de i (abajo) a j (arriba); M dibujado hacia la
    cara traccionada que indica el JSON.

Uso: python src/ar/figuras_referencia.py
"""
import json
import os
import sys

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", ".."))
ENTRADA = os.path.join(RAIZ, "results", "ar_elementos.json")
SALIDA = os.path.join(RAIZ, "reports", "fig")

AZUL = "#2a78d6"
TXT = "#0b0b0b"
TXT2 = "#52514e"
GRID = "#e4e3df"


def _fmt(v):
    if abs(v) < 0.05:
        v = 0.0
    return f"{v:,.1f}".replace(",", " ").replace(".", ",")


def _panel(ax, xs, vals, d, titulo, unidad, vertical, invertir):
    """Una serie (sin leyenda): relleno suave + línea 2 px + rótulos i/j/máx."""
    if vertical:
        ax.fill_betweenx(xs, 0, vals, color=AZUL, alpha=0.15, lw=0)
        ax.plot(vals, xs, color=AZUL, lw=2)
        ax.axvline(0, color=TXT2, lw=1)
        pts = [(vals[0], xs[0], "i"), (vals[-1], xs[-1], "j")]
    else:
        ax.fill_between(xs, 0, vals, color=AZUL, alpha=0.15, lw=0)
        ax.plot(xs, vals, color=AZUL, lw=2)
        ax.axhline(0, color=TXT2, lw=1)
        pts = [(xs[0], vals[0], "i"), (xs[-1], vals[-1], "j")]
    for key in ("max_pos", "max_neg"):
        m = d.get(key)
        if m and all(abs(m["x"] - xs[k]) > 1e-6 for k in (0, len(xs) - 1)) \
                and abs(m["valor"]) > 1e-6:
            pts.append((m["valor"], m["x"], "max") if vertical
                       else (m["x"], m["valor"], "max"))
    for px, py, _ in pts:
        ax.plot(px, py, "o", color=AZUL, ms=6, mec="white", mew=1.5, zorder=3)
        val = px if vertical else py
        ax.annotate(_fmt(val), (px, py), textcoords="offset points",
                    xytext=(8, 0) if vertical else (0, 8),
                    ha="left" if vertical else "center", va="center" if vertical else "bottom",
                    fontsize=9, color=TXT, fontweight="bold")
    # margen para que los rótulos no choquen con el título ni el borde
    lo, hi = min(vals + [0.0]), max(vals + [0.0])
    pad = 0.25 * (hi - lo) if hi > lo else 1.0
    (ax.set_xlim if vertical else ax.set_ylim)(lo - pad, hi + pad)
    if invertir:
        (ax.invert_xaxis if vertical else ax.invert_yaxis)()
    ax.set_title(f"{titulo} [{unidad}]", fontsize=10, color=TXT, loc="left", pad=10)
    ax.grid(True, color=GRID, lw=0.8)
    ax.tick_params(colors=TXT2, labelsize=8)
    for s in ax.spines.values():
        s.set_color(GRID)


def figura(e, caso, ruta):
    vertical = e["tipo"] in ("columna", "muro")
    xs = e["x"]
    dN = e["diagramas"]["N"]
    dV = e["diagramas"][e["principal"]["V"]]
    dM = e["diagramas"][e["principal"]["M"]]
    # M se dibuja hacia su lado de tracción. Viga: M>0 abajo (eje y invertido).
    # Columna: el lado de tracción de M>0 va a la IZQUIERDA (eje x invertido).
    if vertical:
        fig, axs = plt.subplots(1, 3, figsize=(10, 5.2), sharey=True)
    else:
        fig, axs = plt.subplots(3, 1, figsize=(9, 7.5), sharex=True)
    fig.patch.set_facecolor("#fcfcfb")
    _panel(axs[0], xs, dN["valores"], dN, "N (axial, < 0 compresión)", "kN", vertical, False)
    _panel(axs[1], xs, dV["valores"], dV, f"V ({e['principal']['plano']})", "kN", vertical, False)
    _panel(axs[2], xs, dM["valores"], dM, f"M ({e['principal']['plano']})", "kN·m", vertical, True)
    ei, ej = e["extremos"]["i"]["etiqueta"], e["extremos"]["j"]["etiqueta"]
    if vertical:
        axs[0].set_ylabel(f"altura desde i [m]\n(i: {ei.split(' · ')[1]} → j: {ej.split(' · ')[1]})",
                          fontsize=9, color=TXT2)
        axs[2].set_xlabel(f"← {dM['lado_positivo']['texto']} (M > 0)", fontsize=8, color=TXT2)
    else:
        axs[2].set_xlabel(f"x desde i [m]    i = {ei.split(' · ')[0]}   →   j = {ej.split(' · ')[0]}",
                          fontsize=9, color=TXT2)
        axs[2].set_ylabel(f"M > 0 abajo\n({dM['lado_positivo']['texto']})", fontsize=8, color=TXT2)
    fig.suptitle(f"tag {e['tag']} · {e['tipo']} {e['seccion']} ({e['material']}) · L = {_fmt(e['L'])} m · caso {caso}\n"
                 f"{e['ubicacion']}", fontsize=11, color=TXT, x=0.02, ha="left")
    fig.tight_layout(rect=(0, 0, 1, 0.9))
    fig.savefig(ruta, dpi=150, facecolor=fig.get_facecolor())
    plt.close(fig)


def main():
    with open(ENTRADA, encoding="utf-8") as f:
        doc = json.load(f)
    os.makedirs(SALIDA, exist_ok=True)
    for t, e in doc["elementos"].items():
        ruta = os.path.join(SALIDA, f"ar_ref_tag{t}.png")
        figura(e, doc["caso"], ruta)
        print("[AR] figura:", os.path.relpath(ruta, RAIZ))


if __name__ == "__main__":
    sys.exit(main())
