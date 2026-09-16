"""visualizar_complejo.py — PNG 3D de los Edificios A y B lado a lado.

Lee `results/edificio_completo.json` (ver src/benchmark_3d/fusionar.py) y
dibuja ambos edificios con el offset ya aplicado (B desplazado en +X).

Uso:
    python src\\benchmark_3d\\visualizar_complejo.py

Salida: results/modelo_complejo_3d.png

Colores (semantica compartida entre esquemas):
    columnas/pilares  gris oscuro · vigas azul · muros rojo · acero/brazos naranja
"""

import json
import os

OUT_DIR = os.path.abspath(os.path.join(
    os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
    "results"))

JSON_COMPLEJO = os.path.join(OUT_DIR, "edificio_completo.json")

C_COL = "#5b6470"     # columnas / pilares
C_VIG = "#3b82f6"     # vigas de hormigon
C_MUR = "#ef4444"     # muros
C_ACE = "#e65100"     # acero / aspas / brazos rigidos / postes


def _segmentos(edificio):
    """Extrae (x0,y0,z0, x1,y1,z1, categoria) de un edificio del complejo."""
    js = edificio["json"]
    segs = []
    if edificio["esquema"] == "A":
        nodos = {int(k): v for k, v in js["nodos"].items()}
        mapa = {"column": C_COL, "wall": C_MUR, "vigas_x": C_VIG,
                "vigas_y": C_VIG, "aspa": C_ACE}
        for el in js["elementos"]:
            ni, nj = nodos[el["ni"]], nodos[el["nj"]]
            segs.append((ni["x"], ni["y"], ni["z"],
                         nj["x"], nj["y"], nj["z"],
                         mapa.get(el["tipo"], C_VIG)))
    else:  # esquema B
        nodos = {n["id"]: n for n in js["nodos"]}
        mapa = {"pilar": C_COL, "muro": C_MUR, "viga": C_VIG, "brazo": C_ACE}
        for el in js["elementos"]:
            ni, nj = nodos[el["ni"]], nodos[el["nj"]]
            segs.append((ni["x"], ni["y"], ni["z"],
                         nj["x"], nj["y"], nj["z"],
                         mapa.get(el["tipo"], C_VIG)))
    return segs


def _bbox(segs):
    xs, ys, zs = [], [], []
    for s in segs:
        xs += [s[0], s[3]]
        ys += [s[1], s[4]]
        zs += [s[2], s[5]]
    return min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)


def dibujar(out_png=None, mostrar=False, json_complejo=JSON_COMPLEJO):
    import matplotlib
    if not mostrar:
        matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d.art3d import Line3DCollection
    from matplotlib.lines import Line2D

    with open(json_complejo, encoding="utf-8") as f:
        data = json.load(f)

    fig = plt.figure(figsize=(16, 9))
    ax = fig.add_subplot(111, projection="3d")

    for ed in data["edificios"]:
        segs = _segmentos(ed)
        by_color = {}
        for s in segs:
            by_color.setdefault(s[6], []).append(
                [(s[0], s[1], s[2]), (s[3], s[4], s[5])])
        for color, seg_list in by_color.items():
            ax.add_collection3d(Line3DCollection(seg_list, colors=color,
                                                 linewidths=1.2))
        xs, ys, zs = [s[0] for s in segs], [s[1] for s in segs], [s[2] for s in segs]
        cx = (min(xs) + max(xs)) / 2.0
        ax.text(cx, min(ys) - 2.0, min(zs) + 0.5, f"EDIFICIO {ed['id']}",
                fontsize=11, fontweight="bold", ha="center", color="#1f2937",
                bbox=dict(facecolor="white", alpha=0.7, edgecolor="#9ca3af"))

    x0, x1, y0, y1, z0, z1 = _bbox([s for ed in data["edificios"]
                                    for s in _segmentos(ed)])
    m = max((x1 - x0), (y1 - y0), (z1 - z0))
    ax.set_xlim(x0 - 3, x1 + 3)
    ax.set_ylim(y0 - 3, y1 + 3)
    ax.set_zlim(z0 - 2, z1 + 2)
    ax.set_box_aspect(((x1 - x0) + 6, (y1 - y0) + 6, (z1 - z0) + 4))
    ax.set_xlabel("X [m]"); ax.set_ylabel("Y [m]"); ax.set_zlabel("Z [m]")
    ax.view_init(elev=16, azim=-60)
    ax.set_title(f"Complejo de Ingenieria — Edificios A y B "
                 f"(offset B en X = {data['config']['offset_b_x_m']:.1f} m)",
                 fontsize=11)

    leyenda = [
        Line2D([0], [0], color=C_COL, lw=3, label="Columnas / pilares"),
        Line2D([0], [0], color=C_VIG, lw=2, label="Vigas (hormigón)"),
        Line2D([0], [0], color=C_MUR, lw=6, alpha=0.6, label="Muros"),
        Line2D([0], [0], color=C_ACE, lw=2, label="Acero / aspas / brazos rígidos"),
    ]
    ax.legend(handles=leyenda, loc="upper left", fontsize=8)
    fig.tight_layout()

    if out_png is None:
        out_png = os.path.join(OUT_DIR, "modelo_complejo_3d.png")
    os.makedirs(os.path.dirname(out_png), exist_ok=True)
    fig.savefig(out_png, dpi=160)
    print(f"PNG complejo -> {out_png}")
    if mostrar:
        plt.show()
    return fig, ax


def main():
    import argparse
    ap = argparse.ArgumentParser(description="PNG 3D del complejo A+B")
    ap.add_argument("--json", default=JSON_COMPLEJO)
    ap.add_argument("--mostrar", action="store_true")
    args = ap.parse_args()
    dibujar(json_complejo=args.json, mostrar=args.mostrar)


if __name__ == "__main__":
    main()