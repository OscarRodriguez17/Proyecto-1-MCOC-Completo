"""Resumen del modelo del edificio actual — inspección de nodos y elementos.

Replica la construcción geométrica de `construir.construir()` en python puro
(sin OpenSeesPy) para resumir: rangos X/Y/Z, niveles, total de nodos,
elementos por tipo y bordes perimetrales del último piso.
"""
import sys
import os

# Permitir importar desde src/benchmark_3d
SYS_PATH = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(SYS_PATH, "src", "benchmark_3d")
sys.path.insert(0, SRC)

import datos_edificio as d


def replicar_construccion(dat):
    """Replica construir.construir() y devuelve est. python (nodos, elementos)."""
    x_vals = list(dat.GRID_X.values())
    y_items = sorted(dat.GRID_Y.items(), key=lambda kv: kv[1])
    y_vals = [v for _, v in y_items]
    x_keys = list(dat.GRID_X.keys())
    y_keys = [k for k, _ in y_items]
    nx, ny = len(x_vals), len(y_vals)

    def wall_endpoints(w):
        xc, yc, L, t, res = dat.wall_geometry(w)
        if res == "Y":
            return (xc, yc - L / 2), (xc, yc + L / 2)
        return (xc - L / 2, yc), (xc + L / 2, yc)

    def endpoint_kind(w, ep):
        xc, yc, *_ = dat.wall_geometry(w)
        coord = xc if ep == 0 else yc
        for key, val in dat.GRID_X.items():
            if abs(coord - val) < 1e-9:
                return "grid"
        for key, val in dat.GRID_Y.items():
            if abs(coord - val) < 1e-9:
                return "grid"
        return "new"

    def xidx_por_nivel(lvl):
        if lvl in dat.anexo_levels():
            return list(range(len(x_keys)))
        return [i for i, k in enumerate(x_keys) if k != dat.ANEXO["x1"]]

    anexo_lv = dat.anexo_levels()
    xidx_lvl = {lvl: xidx_por_nivel(lvl) for lvl in range(dat.NLEV)}

    nodos = []          # (x, y, z)
    nid = {}            # (xi, yi, lvl) -> idx  ... solo retícula
    wall_nodes = {}
    tag = 0

    # Nodos de retícula + muros
    for lvl in range(dat.NLEV):
        z = dat.LEVEL_Z[lvl]
        for xi in xidx_lvl[lvl]:
            for yi in range(ny):
                nodos.append((x_vals[xi], y_vals[yi], z))
                nid[(xi, yi, lvl)] = tag
                tag += 1
        for wi, w in enumerate(dat.WALLS):
            eps = wall_endpoints(w)
            for ei in range(2):
                if endpoint_kind(w, ei) == "grid":
                    xc, yc, *_ = dat.wall_geometry(w)
                    if any(abs(xc - v) < 1e-9 for v in dat.GRID_X.values()):
                        xi = x_keys.index(next(k for k, v in dat.GRID_X.items()
                                              if abs(v - xc) < 1e-9))
                        yi = min(range(ny), key=lambda j: abs(y_vals[j] - eps[ei][1]))
                        wall_nodes[(wi, lvl, ei)] = nid[(xi, yi, lvl)]
                    else:
                        yi = y_keys.index(next(k for k, v in dat.GRID_Y.items()
                                               if abs(v - yc) < 1e-9))
                        xi = min(range(nx), key=lambda i: abs(x_vals[i] - eps[ei][0]))
                        wall_nodes[(wi, lvl, ei)] = nid[(xi, yi, lvl)]
                else:
                    nodos.append((eps[ei][0], eps[ei][1], z))
                    wall_nodes[(wi, lvl, ei)] = tag
                    tag += 1

    # Nodo maestro por nivel
    cx = sum(x_vals) / nx
    cy = sum(y_vals) / ny
    master = {}
    for lvl in range(dat.NLEV):
        nodos.append((cx, cy, dat.LEVEL_Z[lvl]))
        master[lvl] = tag
        tag += 1

    # Elementos: columnas / vigas / muros
    eje_j = dat.GRID_X.get("J")
    anexo_stories = {st for st in range(dat.NLEV - 1)
                     if st in anexo_lv and (st + 1) in anexo_lv}
    cols = 0
    # Columnas hormigón P.70x70
    for xi in range(nx):
        if eje_j is not None and x_vals[xi] >= eje_j:
            break
        for yi in range(ny):
            if y_items[yi][0] not in dat.COL_EN_Y:
                continue
            for st in range(dat.NLEV - 1):
                cols += 1
    # Columnas acero P.M. anexo
    pm_count = 0
    xi_j = x_keys.index("J") if "J" in x_keys else None
    if xi_j is not None:
        for yi in range(ny):
            if y_items[yi][0] not in dat.ANEXO["eje_y"]:
                continue
            for st in sorted(anexo_stories):
                pm_count += 1
    cols += pm_count

    # Muros
    walls_total = 0
    for wi, w in enumerate(dat.WALLS):
        for st in range(dat.NLEV - 1):
            walls_total += 1            # segmento principal
        if endpoint_kind(w, 1) == "new":
            for st in range(dat.NLEV - 1):
                walls_total += 1        # segmento extremo

    # Vigas X
    vx = 0
    x_borde = dat.GRID_X[dat.ANEXO["x0"]]
    for lvl in range(1, dat.NLEV):
        for yi in range(ny):
            for xi in range(nx - 1):
                if x_vals[xi + 1] > x_borde:
                    continue
                vx += 1
    # Vigas metálicas X (I'->J) niveles anexo
    for lvl in sorted(anexo_lv):
        for yi in range(ny):
            vx += 1

    # Vigas Y
    vy = 0
    for lvl in range(1, dat.NLEV):
        ensamblar = lvl in anexo_lv
        max_x = x_borde if not ensamblar else dat.GRID_X[dat.ANEXO["x1"]]
        for xi in range(nx):
            if x_vals[xi] > x_borde and not ensamblar:
                continue
            for yi in range(ny - 1):
                vy += 1

    return {
        "x_vals": x_vals, "y_vals": y_vals, "nodos": nodos,
        "xidx_lvl": xidx_lvl, "master": master,
        "n_columns": cols, "n_col_pm": pm_count, "n_walls": walls_total,
        "n_vigas_x": vx, "n_vigas_y": vy,
        "xidx_last": xidx_lvl[dat.NLEV - 1],
    }


def main():
    out_path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        SYS_PATH, "docs", "resumen_edificio_actual.txt")

    r = replicar_construccion(d)
    nodos = r["nodos"]
    xs = [n[0] for n in nodos]
    ys = [n[1] for n in nodos]
    zs = [n[2] for n in nodos]

    L = []
    a = L.append
    a("=" * 72)
    a("RESUMEN EDIFICIO ACTUAL (Edificio de Ingeniería - Edificio A)")
    a("Modelo de palitos: OpenSeesPy / datos_edificio.py + construir.py")
    a("=" * 72)
    a("")
    a("1) RANGOS COORDENADOS TOTALES [m]")
    a(f"   X: min = {min(xs):9.3f}   max = {max(xs):9.3f}   ancho = {max(xs)-min(xs):7.2f}")
    a(f"   Y: min = {min(ys):9.3f}   max = {max(ys):9.3f}   profund. = {max(ys)-min(ys):7.2f}")
    a(f"   Z: min = {min(zs):9.3f}   max = {max(zs):9.3f}   altura = {max(zs)-min(zs):7.2f}")
    a("")
    a("2) COTAS DE NIVELES Z [m]")
    for i, z in enumerate(d.LEVEL_Z):
        a(f"   nivel {i}: z = {z:8.3f}")
    a("")
    a("3) TOTALES")
    a(f"   Nodos totales:                  {len(nodos)}")
    a(f"   Elementos totales:              {r['n_columns'] + r['n_walls'] + r['n_vigas_x'] + r['n_vigas_y']}")
    a(f"     - Columnas hormigón P.70x70:  {r['n_columns'] - r['n_col_pm']}")
    a(f"     - Columnas acero   P.M.:      {r['n_col_pm']}")
    a(f"     - Muros (por piso):           {r['n_walls']}")
    a(f"     - Vigas X (incl. V.M.):       {r['n_vigas_x']}")
    a(f"     - Vigas Y (incl. V.M.):       {r['n_vigas_y']}")
    a("   Nota: las losas NO se modelan como elementos (modelo de palitos);")
    a("   su aporte va como carga superficial q_G/q_Q en cargas.py (áreas tributarias).")
    a("")
    a("4) BORDES PERIMETRALES DEL ÚLTIMO PISO (z_max = %.2f m)" % max(zs))
    lv_last = d.NLEV - 1
    # nodos 4 de esquina del último piso (existe eje J en nivel 4)
    xs_last = [d.GRID_X[k] for k in ("E", "J")]
    ys_last = [d.GRID_Y[k] for k in ("A3", "A1")]
    a("   Extremos de la retícula del nivel superior:")
    for x in xs_last:
        for y in ys_last:
            a(f"     ({x:6.2f}, {y:6.2f}, {max(zs):6.2f} m)")
    a("   --> Las vigas superiores llegan hasta:")
    a(f"     X: E = {d.GRID_X['E']:.2f} m  ->  J = {d.GRID_X['J']:.2f} m  (incluye anexo metálico I'->J)")
    a(f"     Y: A3 = {d.GRID_Y['A3']:.2f} m  ->  A1 = {d.GRID_Y['A1']:.2f} m")
    a("   Vigas superiores (nivel 4):")
    a(f"     - Vigas X de hormigón (E->I', 0..45 m): {sum(1 for lvl in range(1, d.NLEV) for xi in range(len(r['x_vals'])-1) if lvl == lv_last and r['x_vals'][xi+1] <= d.GRID_X[d.ANEXO['x0']]) * len(r['y_vals'])}")
    a(f"     - Vigas metálicas V.M. I'->J (45..50 m, fachadas A3/A2a/A2/A1c/A1): {len(r['y_vals'])}")
    a(f"     - Vigas Y (A3->A1) en cada eje X presente (E..J): "
      f"{sum(1 for xi in r['xidx_last'] for _ in range(len(r['y_vals'])-1))}")
    a("")
    a("=" * 72)

    texto = "\n".join(L)
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(texto)
    print(texto)
    print(f"\nReporte guardado en: {out_path}")


if __name__ == "__main__":
    main()


