"""Visualizacion 3D del modelo (sin OpenSees, solo geometria).

Uso:
    python src\\benchmark_3d\\visualizar.py                 # edificio A
    python src\\benchmark_3d\\visualizar.py --solo-guardar  # solo exporta PNG

Salida: results/modelo_3d.png (+ ventana rotatable con matplotlib).
Colores: pilares gris oscuro · vigas azul · muros rojo · losas borde gris.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import matplotlib.pyplot as plt
import numpy as np
from mpl_toolkits.mplot3d.art3d import Line3DCollection, Poly3DCollection

import datos_edificio as d
from voladizos import CONFIG_VOLADIZO

OUT_DIR = os.path.join(
    os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
    "results",
)

C_COL = "#37474F"
C_VIG = "#1565C0"
C_MUR = "#C62828"
C_LOSA = "#9E9E9E"
C_ACERO = "#E65100"


def lineos(ax, pts, color, lw, alpha=1.0):
    segs = [[(x1, y1, z1), (x2, y2, z2)] for (x1, y1, z1), (x2, y2, z2) in pts]
    ax.add_collection3d(Line3DCollection(segs, colors=color, linewidths=lw,
                                         alpha=alpha))


def dibujar(dat=d, offset=(0.0, 0.0), fig=None, ax=None, titulo=None):
    ox, oy = offset
    xv = [v + ox for v in dat.GRID_X.values()]
    yv = sorted(v + oy for v in dat.GRID_Y.values())
    xn = list(dat.GRID_X.keys())
    yn = [k for k, _ in sorted(dat.GRID_Y.items(), key=lambda kv: kv[1])]

    if fig is None or ax is None:
        fig = plt.figure(figsize=(15, 9))
        ax = fig.add_subplot(111, projection="3d")

    # --- Losas: contorno de la planta en cada nivel sobre el terreno ---
    anexo_lv = dat.anexo_levels()
    x_borde = dat.GRID_X[dat.ANEXO["x0"]]          # I' = 45 m
    x_max = dat.GRID_X[dat.ANEXO["x1"]]            # J = 50 m
    for lvl in range(1, dat.NLEV):
        z = dat.LEVEL_Z[lvl]
        xm = x_max if lvl in anexo_lv else x_borde  # voladizo solo en anexo
        esq = [(xv[0], yv[0], z), (xm, yv[0], z), (xm, yv[-1], z),
               (xv[0], yv[-1], z), (xv[0], yv[0], z)]
        ax.plot([p[0] for p in esq], [p[1] for p in esq], [p[2] for p in esq],
                color=C_LOSA, lw=1.0, alpha=0.55)

    # --- Vigas de hormigon V.60/80 (E..I' = 0..45, todos los niveles) ---
    seg_vig = []
    for lvl in range(1, dat.NLEV):
        z = dat.LEVEL_Z[lvl]
        for y in yv:
            for i in range(len(xv) - 1):
                if xv[i + 1] > x_borde:
                    continue
                seg_vig.append(((xv[i], y, z), (xv[i + 1], y, z)))
        for x in xv:
            if x > x_borde:
                continue
            for j in range(len(yv) - 1):
                seg_vig.append(((x, yv[j], z), (x, yv[j + 1], z)))

    # --- Voladizo trasero (eje A3, y<0): nervaduras + viga de borde en HORMIGON ---
    cfg_v = CONFIG_VOLADIZO
    proj = cfg_v["proj_y"]
    v_niveles = tuple(cfg_v["niveles"])
    y_vol = 0.0 + oy
    ejes_v = [k for k in cfg_v["ejes_x"] if k in xn]
    for lvl in v_niveles:
        z = dat.LEVEL_Z[lvl]
        for k in ejes_v:
            x = xv[xn.index(k)]
            seg_vig.append(((x, y_vol, z), (x, y_vol - proj, z)))
        if cfg_v.get("viga_borde_punta", True) and len(ejes_v) >= 2:
            for ak, bk in zip(ejes_v[:-1], ejes_v[1:]):
                x_a, x_b = xv[xn.index(ak)], xv[xn.index(bk)]
                seg_vig.append(((x_a, y_vol - proj, z), (x_b, y_vol - proj, z)))
    lineos(ax, seg_vig, C_VIG, 1.6)

    # --- Vigas metalicas V.M. del anexo (I'->J y fachada J, niveles superiores) ---
    seg_acer = []
    for lvl in sorted(anexo_lv):
        z = dat.LEVEL_Z[lvl]
        for y in yv:
            seg_acer.append(((x_borde, y, z), (x_max, y, z)))
        for j in range(len(yv) - 1):
            seg_acer.append(((x_max, yv[j], z), (x_max, yv[j + 1], z)))
    lineos(ax, seg_acer, C_ACERO, 1.8)

    # --- Pilares de hormigon P.70x70 (E..I', ejes Y con columna) ---
    seg_col = []
    fil_cols = [yv[j] for j in range(len(yn)) if yn[j] in dat.COL_EN_Y]
    for st in range(dat.NLEV - 1):
        zb, zt = dat.LEVEL_Z[st], dat.LEVEL_Z[st + 1]
        for x in xv:
            if x > x_borde:
                continue
            for y in fil_cols:
                seg_col.append(((x, y, zb), (x, y, zt)))
    lineos(ax, seg_col, C_COL, 3.2)

    # --- Pilares metalicos P.M. del anexo en fachada J (piso superior) ---
    seg_pm = []
    st_pm = {st for st in range(dat.NLEV - 1)
             if st in anexo_lv and (st + 1) in anexo_lv}
    for st in sorted(st_pm):
        zb, zt = dat.LEVEL_Z[st], dat.LEVEL_Z[st + 1]
        for y in fil_cols:
            seg_pm.append(((xv[-1], y, zb), (xv[-1], y, zt)))

    # --- Postes metalicos P.M. del voladizo en las puntas (y=-proj) ---
    if cfg_v.get("postes_punta", True):
        zb = dat.LEVEL_Z[v_niveles[0]]
        zt = dat.LEVEL_Z[v_niveles[-1]]
        for k in ejes_v:
            x = xv[xn.index(k)]
            seg_pm.append(((x, y_vol - proj, zb), (x, y_vol - proj, zt)))
    lineos(ax, seg_pm, C_ACERO, 3.4)

    # --- Muros como paneles verticales (dos caras separadas t) ---
    caras = []
    for w in dat.WALLS:
        xc, yc, L, t, resiste = dat.wall_geometry(w)
        xc += ox
        yc += oy
        for st in range(dat.NLEV - 1):
            zb, zt = dat.LEVEL_Z[st], dat.LEVEL_Z[st + 1]
            if resiste == "Y":
                y0, y1 = yc - L / 2, yc + L / 2
                for off in (-t / 2, t / 2):
                    caras.append([(xc + off, y0, zb), (xc + off, y1, zb),
                                  (xc + off, y1, zt), (xc + off, y0, zt)])
            else:
                x0, x1 = xc - L / 2, xc + L / 2
                for off in (-t / 2, t / 2):
                    caras.append([(x0, yc + off, zb), (x1, yc + off, zb),
                                  (x1, yc + off, zt), (x0, yc + off, zt)])
    mp = Poly3DCollection(caras, facecolor=C_MUR, edgecolor="#7F1D1D",
                          alpha=0.55, linewidths=0.5)
    ax.add_collection3d(mp)

    # --- Rotulos de niveles (cotas al costado oeste) ---
    for lvl, z in enumerate(dat.LEVEL_Z):
        nombre = ["Base", "Piso 1", "Piso 2", "Piso 3", "Techo"][lvl]
        ax.text(xv[0] - 3.5, yv[0] - 1.0, z, f"{nombre}  {z:+.2f}", fontsize=8,
                color="#455A64", ha="right")

    # --- Ejes en planta (base) ---
    for i, x in enumerate(xv):
        ax.text(x, yv[0] - 1.2, dat.LEVEL_Z[0] - 0.3, xn[i], fontsize=8,
                ha="center", color="#37474F")
    for j, y in enumerate(yv):
        ax.text(xv[0] - 1.5, y, dat.LEVEL_Z[0] - 0.3, yn[j], fontsize=8,
                ha="right", color="#37474F")

    dx = xv[-1] - xv[0]
    dy = (yv[-1] - yv[0]) + proj
    dz = dat.LEVEL_Z[-1] - dat.LEVEL_Z[0] + 2
    ax.set_xlim(xv[0] - 4, xv[-1] + 4)
    ax.set_ylim(yv[0] - 3 - proj, yv[-1] + 3)
    ax.set_zlim(dat.LEVEL_Z[0] - 1, dat.LEVEL_Z[-1] + 1)
    ax.set_box_aspect((dx + 8, dy + 6, dz))
    ax.set_xlabel("X [m]")
    ax.set_ylabel("Y [m]")
    ax.set_zlabel("Z [m]")
    ax.view_init(elev=14, azim=-58)
    ax.set_title(
        titulo or (f"Edificio de Ingenieria — Edificio A "
                   f"(G35, {len(dat.WALLS)} muros, {dat.NLEV - 1} pisos + base)"),
        fontsize=11,
    )

    from matplotlib.lines import Line2D

    leyenda = [
        Line2D([0], [0], color=C_COL, lw=3, label="Pilares P.70x70 (hormigón)"),
        Line2D([0], [0], color=C_VIG, lw=2, label="Vigas V.60/80 (hormigón)"),
        Line2D([0], [0], color=C_MUR, lw=6, alpha=0.6, label="Muros"),
        Line2D([0], [0], color=C_LOSA, lw=1, label="Contorno losa"),
        Line2D([0], [0], color=C_ACERO, lw=3, label="Acero: anexo I'–J + postes voladizo"),
    ]
    ax.legend(handles=leyenda, loc="upper left", fontsize=8)
    fig.tight_layout()
    return fig, ax


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    fig, _ = dibujar()
    png = os.path.join(OUT_DIR, "modelo_3d.png")
    fig.savefig(png, dpi=160)
    print(f"PNG guardado en: {png}")
    if "--solo-guardar" not in sys.argv:
        plt.show()


if __name__ == "__main__":
    main()
