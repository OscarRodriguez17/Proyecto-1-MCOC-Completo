"""Constructor del modelo 3D en OpenSeesPy.

Convencion de ejes locales:
  - Vigas: transf vecxz=(0,0,1) -> Iy fuerte (flexion por gravedad), Iz debil.
  - Columnas y muros: transf vecxz=(1,0,0) -> Iy gobierna flexion en X,
    Iz gobierna flexion en Y.

Muros: cada piso tiene su propio elemento (base a techo segmentado por nivel).
  - Muros sobre reticula (ME-32, MI-32, MI-12): comparten nodo con la viga
    en la interseccion de ejes.
  - Muros fuera de reticula (M1c-E, M2a): nodo propio, rigid link horizontal
    al nodo de viga mas cercano via equalDOF.
"""

import datos_edificio as d

GT_COL = 1
GT_BEAM = 2


def _grid(dat):
    x_vals = list(dat.GRID_X.values())
    y_items = sorted(dat.GRID_Y.items(), key=lambda kv: kv[1])
    y_vals = [v for _, v in y_items]
    x_keys = list(dat.GRID_X.keys())
    y_keys = [k for k, _ in y_items]
    return x_vals, y_items, y_vals, x_keys, y_keys


def _wall_endpoint_grid(dat, w, ep):
    """('grid', key) si el endpoint coincide con un eje, si no ('new', None)."""
    xc, yc, _, _, _ = dat.wall_geometry(w)
    coord = xc if ep == 0 else yc
    for key, val in dat.GRID_X.items():
        if abs(coord - val) < 1e-9:
            return "grid", key
    for key, val in dat.GRID_Y.items():
        if abs(coord - val) < 1e-9:
            return "grid", key
    return "new", None


def _wall_link_xy(dat, w, ep):
    """Coordenadas (x,y) del endpoint del muro para rigid link."""
    xc, yc, L, t, resiste = dat.wall_geometry(w)
    if ep == 0:
        return (xc - L / 2 if resiste == "Y" else xc,
                yc if resiste == "Y" else yc - L / 2)
    return (xc + L / 2 if resiste == "Y" else xc,
            yc if resiste == "Y" else yc + L / 2)


def _wall_endpoints(dat, w):
    """(start_xy, end_xy) del muro en su eje principal."""
    xc, yc, L, t, resiste = dat.wall_geometry(w)
    if resiste == "Y":
        return (xc, yc - L / 2), (xc, yc + L / 2)
    return (xc - L / 2, yc), (xc + L / 2, yc)


def _nearest_grid_node(dat, x, y):
    """(xi, yi) del nodo de reticula mas cercano a (x, y) en el nivel lvl 0."""
    x_vals, _, y_vals, _, _ = _grid(dat)
    xi = min(range(len(x_vals)), key=lambda i: abs(x_vals[i] - x))
    yi = min(range(len(y_vals)), key=lambda j: abs(y_vals[j] - y))
    return xi, yi


def _x_indices_por_nivel(dat, lvl):
    """Indices xi de la reticula X que existen en el nivel lvl.

    El eje J (voladizo metalico) solo existe en los niveles del anexo."""
    x_keys = list(dat.GRID_X.keys())
    if lvl in dat.anexo_levels():
        return list(range(len(x_keys)))
    return [i for i, k in enumerate(x_keys) if k != dat.ANEXO["x1"]]


def construir(dat=None, offset=(0.0, 0.0)):
    """Construye el modelo del edificio `dat` (default: datos_edificio).

    offset=(ox, oy) traslada la planta en el plano XY.
    """
    if dat is None:
        dat = d
    ox, oy = offset
    import openseespy.opensees as ops

    x_vals, y_items, y_vals, x_keys, y_keys = _grid(dat)
    nx, ny = len(x_vals), len(y_vals)
    cx = sum(x_vals) / nx + ox
    cy = sum(y_vals) / ny + oy

    ops.wipe()
    ops.model("basic", "-ndm", 3, "-ndf", 6)
    ops.geomTransf("Linear", GT_COL, 1.0, 0.0, 0.0)
    ops.geomTransf("Linear", GT_BEAM, 0.0, 0.0, 1.0)

    A_c, Iy_c, Iz_c, J_c = dat.sec_columna()

    nid = {}
    master = {}
    wall_nodes = {}
    tag = 1

    # ── Nodos de reticula ──
    anexo_lv = dat.anexo_levels()
    xidx_por_lvl = {lvl: _x_indices_por_nivel(dat, lvl)
                    for lvl in range(dat.NLEV)}
    for lvl in range(dat.NLEV):
        z = dat.LEVEL_Z[lvl]
        for xi in xidx_por_lvl[lvl]:
            for yi in range(ny):
                ops.node(tag, x_vals[xi] + ox, y_vals[yi] + oy, z)
                nid[(xi, yi, lvl)] = tag
                tag += 1

        # ── Nodos de muros (endpoints) ──
        for wi, w in enumerate(dat.WALLS):
            eps = _wall_endpoints(dat, w)
            for ep_idx in range(2):
                kind, key = _wall_endpoint_grid(dat, w, ep_idx)
                if kind == "grid":
                    if key in dat.GRID_X:
                        xi = x_keys.index(key)
                        yi_n = min(range(ny),
                                   key=lambda j: abs(y_vals[j] - eps[ep_idx][1]))
                        wall_nodes[(wi, lvl, ep_idx)] = nid[(xi, yi_n, lvl)]
                    else:
                        yi = y_keys.index(key)
                        xi_n = min(range(nx),
                                   key=lambda i: abs(x_vals[i] - eps[ep_idx][0]))
                        wall_nodes[(wi, lvl, ep_idx)] = nid[(xi_n, yi, lvl)]
                else:
                    ops.node(tag, eps[ep_idx][0] + ox, eps[ep_idx][1] + oy, z)
                    wall_nodes[(wi, lvl, ep_idx)] = tag
                    tag += 1

        # ── Nodo maestro ──
        mt = tag
        ops.node(mt, cx, cy, z)
        if lvl == 0:
            ops.fix(mt, 1, 1, 1, 1, 1, 1)
        else:
            ops.fix(mt, 0, 0, 1, 1, 1, 0)
        master[lvl] = mt
        tag += 1

    # ── Base empotrada (sin duplicados) ──
    base_nodes = set()
    for xi in xidx_por_lvl[0]:
        for yi in range(ny):
            base_nodes.add(nid[(xi, yi, 0)])
    for wi in range(len(dat.WALLS)):
        for ep in range(2):
            base_nodes.add(wall_nodes[(wi, 0, ep)])
    for ntag in base_nodes:
        ops.fix(ntag, 1, 1, 1, 1, 1, 1)

    # ── Diafragmas rigidos ──
    for lvl in range(1, dat.NLEV):
        slaves = set(nid[(xi, yi, lvl)]
                     for xi in xidx_por_lvl[lvl] for yi in range(ny))
        for wi in range(len(dat.WALLS)):
            for ep in range(2):
                slaves.add(wall_nodes[(wi, lvl, ep)])
        ops.rigidDiaphragm(3, master[lvl], *slaves)

    # ── Columnas de hormigon P.70x70 (E..I' = 0..45, todos los pisos) ──
    eje_j = dat.GRID_X.get("J")
    anexo_stories = {st for st in range(dat.NLEV - 1)
                     if st in anexo_lv and (st + 1) in anexo_lv}
    ele = 1
    columns = []
    for xi in range(nx):
        if eje_j is not None and x_vals[xi] >= eje_j:
            break
        for yi in range(ny):
            if y_items[yi][0] not in dat.COL_EN_Y:
                continue
            for st in range(dat.NLEV - 1):
                ni = nid[(xi, yi, st)]
                nj = nid[(xi, yi, st + 1)]
                ops.element("elasticBeamColumn", ele, ni, nj,
                            A_c, dat.E_C, dat.G_C, J_c, Iy_c, Iz_c, GT_COL)
                columns.append({"tag": ele, "ni": ni, "nj": nj, "story": st,
                                "A_pp": A_c, "rho_pp": dat.GAMMA_C,
                                "seccion": "P.70x70", "material": "concreto"})
                ele += 1

    # ── Pilares metalicos P.M. 300x300x20 del anexo en fachada J ──
    xi_j = x_keys.index("J") if "J" in x_keys else None
    if xi_j is not None:
        A_s, Iy_s, Iz_s, J_s = dat.sec_pm()
        for yi in range(ny):
            if y_items[yi][0] not in dat.ANEXO["eje_y"]:
                continue
            for st in sorted(anexo_stories):
                ni = nid[(xi_j, yi, st)]
                nj = nid[(xi_j, yi, st + 1)]
                ops.element("elasticBeamColumn", ele, ni, nj,
                            A_s, dat.E_STEEL, dat.G_STEEL, J_s, Iy_s, Iz_s,
                            GT_COL)
                columns.append({"tag": ele, "ni": ni, "nj": nj, "story": st,
                                "A_pp": A_s, "rho_pp": dat.GAMMA_STEEL,
                                "seccion": "P.M. 300x300x20",
                                "material": "acero"})
                ele += 1

    # ── Muros: un elemento por piso por tramo ──
    walls = []
    for wi, w in enumerate(dat.WALLS):
        xc, yc, L, t, resiste = dat.wall_geometry(w)
        A_w, Iy_w, Iz_w, J_w = dat.sec_muro(t, L, resiste)
        Ay_w, Az_w = dat.shear_area_muro(t, L, resiste)
        for st in range(dat.NLEV - 1):
            ninf = wall_nodes[(wi, st, 0)]
            nsup = wall_nodes[(wi, st + 1, 0)]
            ops.element("elasticBeamColumn", ele, ninf, nsup,
                        A_w, dat.E_C, dat.G_C, J_w, Iy_w, Iz_w, GT_COL,
                        "-shear", Ay_w, Az_w)
            walls.append({"tag": ele, "ni": ninf, "nj": nsup, "story": st,
                          "id": w["id"], "L": L, "t": t})
            ele += 1
        if _wall_endpoint_grid(dat, w, 1)[0] == "new":
            for st in range(dat.NLEV - 1):
                ninf = wall_nodes[(wi, st, 1)]
                nsup = wall_nodes[(wi, st + 1, 1)]
                ops.element("elasticBeamColumn", ele, ninf, nsup,
                            A_w, dat.E_C, dat.G_C, J_w, Iy_w, Iz_w, GT_COL,
                            "-shear", Ay_w, Az_w)
                walls.append({"tag": ele, "ni": ninf, "nj": nsup, "story": st,
                              "id": w["id"] + "_ext", "L": L, "t": t})
                ele += 1

    # ── Rigid links: muros fuera de reticula al nodo de viga mas cercano ──
    for wi, w in enumerate(dat.WALLS):
        for ep in range(2):
            kind, _ = _wall_endpoint_grid(dat, w, ep)
            if kind == "new":
                wx, wy = _wall_link_xy(dat, w, ep)
                xi_n, yi_n = _nearest_grid_node(dat, wx, wy)
                for lvl in range(dat.NLEV):
                    beam_tag = nid[(xi_n, yi_n, lvl)]
                    wall_tag = wall_nodes[(wi, lvl, ep)]
                    ops.equalDOF(beam_tag, wall_tag, 1, 2, 6)

    # ── Vigas X ──
    vigas_x = []
    A_conc, _, _, _ = dat.sec_viga()
    anexo_lv = dat.anexo_levels()
    # X de borde del voladizo (I' = 45 m): ultimo eje del hormigon.
    x_borde = dat.GRID_X[dat.ANEXO["x0"]]
    for lvl in range(1, dat.NLEV):
        for yi in range(ny):
            es_borde = yi in (0, ny - 1)          # linea A3 / A1: losa a un lado
            for xi in range(nx - 1):
                if x_vals[xi + 1] > x_borde:
                    continue                       # tramo I'-J es metalico
                span = x_vals[xi + 1] - x_vals[xi]
                A_b, Iz_b, Iy_b, J_b = dat.sec_viga_compuesta(
                    span, "L" if es_borde else "T")
                ops.element("elasticBeamColumn", ele,
                            nid[(xi, yi, lvl)], nid[(xi + 1, yi, lvl)],
                            A_b, dat.E_C, dat.G_C, J_b, Iy_b, Iz_b, GT_BEAM)
                vigas_x.append({"tag": ele,
                                "ni": nid[(xi, yi, lvl)],
                                "nj": nid[(xi + 1, yi, lvl)],
                                "nivel": lvl,
                                "x_i": x_vals[xi], "x_j": x_vals[xi + 1],
                                "y": y_vals[yi],
                                "seccion": "L" if es_borde else "T",
                                "A_pp": A_conc, "rho_pp": dat.GAMMA_C,
                                "material": "concreto"})
                ele += 1

    # ── Vigas metalicas V.M. 300x300x5 del anexo (I'->J), niveles superiores ──
    xi_i = x_keys.index(dat.ANEXO["x0"])
    xi_j = x_keys.index(dat.ANEXO["x1"])
    A_vm, Iz_vm, Iy_vm, J_vm = dat.sec_vm()
    for lvl in sorted(anexo_lv):
        for yi in range(ny):
            ops.element("elasticBeamColumn", ele,
                        nid[(xi_i, yi, lvl)], nid[(xi_j, yi, lvl)],
                        A_vm, dat.E_STEEL, dat.G_STEEL, J_vm, Iy_vm, Iz_vm,
                        GT_BEAM)
            vigas_x.append({"tag": ele,
                            "ni": nid[(xi_i, yi, lvl)],
                            "nj": nid[(xi_j, yi, lvl)],
                            "nivel": lvl,
                            "x_i": x_vals[xi_i], "x_j": x_vals[xi_j],
                            "y": y_vals[yi],
                            "seccion": "V.M. 300x300x5",
                            "A_pp": A_vm, "rho_pp": dat.GAMMA_STEEL,
                            "material": "acero"})
            ele += 1

    # ── Vigas Y ──
    vigas_y = []
    for lvl in range(1, dat.NLEV):
        ensamblar = lvl in anexo_lv
        max_x = x_borde if not ensamblar else dat.GRID_X[dat.ANEXO["x1"]]
        for xi in range(nx):
            if x_vals[xi] > x_borde and not ensamblar:
                continue                          # sin viga Y en J fuera de niveles anexo
            es_borde = (xi == 0) or abs(x_vals[xi] - max_x) < 1e-9
            for yi in range(ny - 1):
                span = y_vals[yi + 1] - y_vals[yi]
                if x_vals[xi] > x_borde:
                    # viga Y metalica en fachada J (solo niveles anexo)
                    A_b, Iz_b, Iy_b, J_b = A_vm, Iz_vm, Iy_vm, J_vm
                    mat, rho = "acero", dat.GAMMA_STEEL
                    sec = "V.M. 300x300x5"
                else:
                    A_b, Iz_b, Iy_b, J_b = dat.sec_viga_compuesta(
                        span, "L" if es_borde else "T")
                    mat, rho, sec = "concreto", dat.GAMMA_C, \
                        ("L" if es_borde else "T")
                ops.element("elasticBeamColumn", ele,
                            nid[(xi, yi, lvl)], nid[(xi, yi + 1, lvl)],
                            A_b, dat.E_C if mat == "concreto" else dat.E_STEEL,
                            dat.G_C if mat == "concreto" else dat.G_STEEL,
                            J_b, Iy_b, Iz_b, GT_BEAM)
                vigas_y.append({"tag": ele,
                                "ni": nid[(xi, yi, lvl)],
                                "nj": nid[(xi, yi + 1, lvl)],
                                "nivel": lvl,
                                "x": x_vals[xi],
                                "y_i": y_vals[yi], "y_j": y_vals[yi + 1],
                                "seccion": sec,
                                "A_pp": A_b, "rho_pp": rho,
                                "material": mat})
                ele += 1

    return {
        "nid": nid, "master": master, "wall_nodes": wall_nodes,
        "columns": columns, "walls": walls,
        "vigas_x": vigas_x, "vigas_y": vigas_y,
    }
