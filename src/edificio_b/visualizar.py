# -*- coding: utf-8 -*-
"""
visualizar.py — Dibuja el modelo 3D (matplotlib) coloreado por tipo de elemento.

Uso:
    python -m edificio_b.visualizar          # guarda results/modelo_3d.png
"""
import os
import openseespy.opensees as ops
from .construir import build_model
from . import datos_edificio as D

AQUI    = os.path.dirname(os.path.abspath(__file__))
RESULTS = os.path.abspath(os.path.join(AQUI, "..", "..", "results"))

COLOR = {'pilar': '#c0392b', 'muro': '#2471a3', 'viga': '#7f8c8d', 'brazo': '#27ae60'}


def dibujar(guardar=True, mostrar=False):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d import Axes3D  # noqa
    from mpl_toolkits.mplot3d.art3d import Poly3DCollection

    M = build_model(verbose=True)
    fig = plt.figure(figsize=(13, 11))
    ax = fig.add_subplot(111, projection='3d')

    # pilares y vigas como líneas; muros como PANELES (huella real x altura)
    for e, tipo in M.tipo.items():
        if tipo in ('muro', 'brazo'):
            continue
        n1, n2 = ops.eleNodes(e)
        x1, y1, z1 = ops.nodeCoord(n1)
        x2, y2, z2 = ops.nodeCoord(n2)
        lw = 2.2 if tipo == 'pilar' else 0.7
        ax.plot([x1, x2], [y1, y2], [z1, z2], color=COLOR[tipo], linewidth=lw, zorder=2)

    zt = D.NIVELES[-1][0]
    zb = D.Z_BASE
    paneles = []
    for (x, y0, y1, e) in D.MUROS_V + D.MUROS_BLOQUE_SUP_V:
        paneles.append([(x, y0, zb), (x, y1, zb), (x, y1, zt), (x, y0, zt)])
    for (y, x0, x1, e) in D.MUROS_H + D.MUROS_BLOQUE_SUP_H:
        paneles.append([(x0, y, zb), (x1, y, zb), (x1, y, zt), (x0, y, zt)])
    pc = Poly3DCollection(paneles, facecolor='#2471a3', alpha=0.28,
                          edgecolor='#154360', linewidths=0.8)
    ax.add_collection3d(pc)

    bx = [M.coord[n][0] for n in M.base_nodes]
    by = [M.coord[n][1] for n in M.base_nodes]
    bz = [M.coord[n][2] for n in M.base_nodes]
    ax.scatter(bx, by, bz, c='k', marker='^', s=35, zorder=5)

    ax.set_xlabel('X [m]'); ax.set_ylabel('Y [m]'); ax.set_zlabel('Z [m]')
    ax.set_title('Edificio B — modelo 3D (rojo=pilares, gris=vigas, azul=muros)')
    from matplotlib.lines import Line2D
    from matplotlib.patches import Patch
    leg = [Line2D([0], [0], color=COLOR['pilar'], lw=3, label='pilar'),
           Line2D([0], [0], color=COLOR['viga'], lw=2, label='viga'),
           Patch(facecolor='#2471a3', alpha=0.4, label='muro (panel real)')]
    ax.legend(handles=leg, loc='upper left')
    try:
        ax.set_box_aspect((max(bx) - min(bx), max(by) - min(by), 20))
    except Exception:
        pass
    ax.view_init(elev=20, azim=-62)
    if guardar:
        os.makedirs(RESULTS, exist_ok=True)
        out = os.path.join(RESULTS, "modelo_3d.png")
        plt.savefig(out, dpi=140, bbox_inches='tight')
        print("figura ->", out)
    if mostrar:
        plt.show()
    return M


def dibujar_planta_brazos(out="/mnt/user-data/outputs/planta_brazos.png"):
    import matplotlib; matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.patches import Rectangle
    M = build_model()
    fig, ax = plt.subplots(figsize=(14, 9))
    for e, t in M.tipo.items():
        n1, n2 = ops.eleNodes(e)
        x1, y1, z1 = ops.nodeCoord(n1); x2, y2, z2 = ops.nodeCoord(n2)
        if abs(z1 + 0.05) > 0.01 or abs(z2 + 0.05) > 0.01:
            continue
        if t == 'viga': ax.plot([x1, x2], [y1, y2], color="#ccc", lw=0.8, zorder=1)
        elif t == 'brazo': ax.plot([x1, x2], [y1, y2], color="#27ae60", lw=2.0, zorder=4)
    # muros como huella real
    for (x, y0, y1, e) in D.MUROS_V + D.MUROS_BLOQUE_SUP_V:
        ax.add_patch(Rectangle((x - e/2, y0), e, y1 - y0, facecolor="#2471a3",
                     alpha=.55, edgecolor="#154360", zorder=3))
    for (y, x0, x1, e) in D.MUROS_H + D.MUROS_BLOQUE_SUP_H:
        ax.add_patch(Rectangle((x0, y - e/2), x1 - x0, e, facecolor="#2471a3",
                     alpha=.55, edgecolor="#154360", zorder=3))
    seen = set()
    for e, t in M.tipo.items():
        if t != 'pilar': continue
        n1, n2 = ops.eleNodes(e); x, y, _ = ops.nodeCoord(n1); k = (round(x,2), round(y,2))
        if k in seen: continue
        seen.add(k)
        ax.scatter([x], [y], c="#c0392b", s=80, marker="s", zorder=6, edgecolors='k', linewidths=.4)
    ax.plot([], [], color="#27ae60", lw=2.0, label="brazo rígido")
    ax.scatter([], [], c="#c0392b", marker="s", label="pilar")
    from matplotlib.patches import Patch
    ax.add_patch(Rectangle((0,0),0,0,facecolor="#2471a3",alpha=.55,label="muro (huella real)"))
    ax.set_aspect("equal"); ax.grid(True, alpha=.2); ax.legend(loc="lower right")
    ax.set_title("Piso 1 — muros (huella real), pilares y brazos rígidos (verde)")
    ax.set_xlabel("X [m]"); ax.set_ylabel("Y [m]")
    plt.tight_layout(); plt.savefig(out, dpi=140, bbox_inches="tight"); print("planta brazos ->", out)


if __name__ == '__main__':
    dibujar()