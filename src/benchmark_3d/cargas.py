"""Cargas gravitacionales y laterales como cargas nodales equivalentes.

Convencion del curso: la carga de losa se reparte por areas tributarias
entre las vigas de borde de cada panel; aqui cada aporte se aplica como
cargas nodales en los extremos de la viga (estaticamente equivalentes para
el modelo global). Pesos propios de columnas/muros van al nodo superior.

Sismo pseudoestatico: F_i = V * W_i h_i / sum(W_j h_j), repartida en partes
iguales entre los nodos del nivel (evita aplicar carga directa al nodo
maestro del diafragma rigido).
"""

import datos_edificio as d


def _buscar_viga(lista, coord_fija, a, b):
    for v in lista:
        c = v.get("y", v.get("x"))
        ai = v.get("x_i", v.get("y_i"))
        bj = v.get("x_j", v.get("y_j"))
        if abs(c - coord_fija) < 1e-9 and abs(ai - a) < 1e-9 and abs(bj - b) < 1e-9:
            return v["tag"]
    return None


def _buscar_cadena(lista, coord_fija, a, b, tol=1e-9):
    """Tags de tramos colineales que cubren [a, b] en cadena contigua y sin
    solapes (p. ej. una viga de reticula partida por una viga secundaria).
    Retorna [] si no hay cobertura completa."""
    tramos = {}
    for v in lista:
        c = v.get("y", v.get("x"))
        ai = v.get("x_i", v.get("y_i"))
        bj = v.get("x_j", v.get("y_j"))
        if abs(c - coord_fija) > tol:
            continue
        lo, hi = min(ai, bj), max(ai, bj)
        if lo < a - tol or hi > b + tol or hi - lo < tol:
            continue
        tramos.setdefault(round(lo, 9), []).append((hi, v["tag"]))
    def _dfs(pos):
        if pos >= b - tol:
            return []
        for hi, tag in sorted(tramos.get(round(pos, 9), []), reverse=True):
            resto = _dfs(hi)
            if resto is not None:
                return [tag] + resto
        return None

    return _dfs(a) or []


def _paneles(xs, ys, vigas_y, tol=1e-9):
    """Paneles (x0, x1, y0, y1) de la reticula; un panel atravesado por una
    viga secundaria con "divide_panel" (x interior, cubre todo el vano en Y)
    se reemplaza por dos subpaneles a cada lado de la viga."""
    divisoras = [v for v in vigas_y if v.get("divide_panel")]
    out = []
    for i in range(len(xs) - 1):
        for j in range(len(ys) - 1):
            cortes = sorted({v["x"] for v in divisoras
                             if xs[i] + tol < v["x"] < xs[i + 1] - tol
                             and abs(v["y_i"] - ys[j]) < tol
                             and abs(v["y_j"] - ys[j + 1]) < tol})
            bordes_x = [xs[i]] + cortes + [xs[i + 1]]
            for k in range(len(bordes_x) - 1):
                out.append((bordes_x[k], bordes_x[k + 1], ys[j], ys[j + 1]))
    return out


def distribuir_nivel(q, vigas_x, vigas_y, dat=d, lvl=None):
    """Reparte q por areas tributarias entre las vigas de UN nivel.

    El voladizo metalico I'-J (eje J = 50 m) participa SOLO en los niveles
    del anexo; en el resto la losa llega hasta I' (45 m), segun los planos.
    Retorna ({tag: w}, {tag: L}, area_total, transferido)."""
    w_losa = {}
    longitudes = {}
    for v in vigas_x:
        w_losa[v["tag"]] = 0.0
        longitudes[v["tag"]] = v.get("L", v["x_j"] - v["x_i"])
    for v in vigas_y:
        w_losa[v["tag"]] = 0.0
        longitudes[v["tag"]] = v.get("L", v["y_j"] - v["y_i"])

    anexo = lvl in dat.anexo_levels() if lvl is not None else True
    xs = sorted(dat.GRID_X[k] for k in dat.GRID_X
                if anexo or k != dat.ANEXO["x1"])
    ys = sorted(set(dat.GRID_Y.values()))
    area_total = 0.0
    for x0, x1, y0, y1 in _paneles(xs, ys, vigas_y):
        dx = x1 - x0
        dy = y1 - y0
        area_total += dx * dy
        cuarto = q * dx * dy / 4.0
        bordes = [("x", y0, x0, x1),
                  ("x", y1, x0, x1),
                  ("y", x0, y0, y1),
                  ("y", x1, y0, y1)]
        for orient, fija, a, b in bordes:
            largo = b - a
            lista = vigas_x if orient == "x" else vigas_y
            tag = _buscar_viga(lista, fija, a, b)
            tags = [tag] if tag is not None else _buscar_cadena(lista, fija, a, b)
            if not tags:
                raise RuntimeError("panel sin viga de borde")
            # carga uniforme a lo largo del borde: mismo w en cada tramo
            for t in tags:
                w_losa[t] += cuarto / largo

    transferido = sum(w_losa[t] * longitudes[t] for t in w_losa)
    return w_losa, longitudes, area_total, transferido


def distribuir_tributaria(q, modelo, dat=d):
    """Version multi-nivel: acumula sobre todos los pisos con vigas."""
    w_tot, l_tot = {}, {}
    area = transf = 0.0
    for lvl in range(1, dat.NLEV):
        vx = [v for v in modelo["vigas_x"] if v["nivel"] == lvl]
        vy = [v for v in modelo["vigas_y"] if v["nivel"] == lvl]
        w, lng, area, tr = distribuir_nivel(q, vx, vy, dat, lvl)
        w_tot.update(w)
        l_tot.update(lng)
        transf += tr
    return w_tot, l_tot, area, transf


def cargas_gravedad(q, modelo, dat=d, ts_tag=1, pat_tag=1, incluir_pp=True):
    """Define patron con gravedad. incluir_pp=False -> solo carga superficial q."""
    import openseespy.opensees as ops
    ops.timeSeries("Constant", ts_tag)
    ops.pattern("Plain", pat_tag, ts_tag)

    A_c, _, _, _ = dat.sec_columna()

    nodal = {}
    inv_por_tag = {}
    for lvl in range(1, dat.NLEV):
        vx = [v for v in modelo["vigas_x"] if v["nivel"] == lvl]
        vy = [v for v in modelo["vigas_y"] if v["nivel"] == lvl]
        w_losa, longs, _, _ = distribuir_nivel(q, vx, vy, dat, lvl)
        inv_por_tag.update({v["tag"]: v for v in vx + vy})
        for t, wl in w_losa.items():
            v = inv_por_tag[t]
            if incluir_pp:
                a_pp = v.get("A_pp", dat.sec_viga()[0])
                rho_pp = v.get("rho_pp", dat.GAMMA_C)
            else:
                a_pp = rho_pp = 0.0
            w_total = wl + rho_pp * a_pp
            fz = w_total * longs[t] / 2.0
            nodal[v["ni"]] = nodal.get(v["ni"], 0.0) + fz
            nodal[v["nj"]] = nodal.get(v["nj"], 0.0) + fz

    if incluir_pp:
        for col in modelo["columns"]:
            h = dat.STORY_H[col["story"]]
            a_pp = col.get("A_pp", A_c)
            rho_pp = col.get("rho_pp", dat.GAMMA_C)
            nodal[col["nj"]] = nodal.get(col["nj"], 0.0) + rho_pp * a_pp * h
        for muro in modelo["walls"]:
            h = dat.STORY_H[muro["story"]]
            nodal[muro["nj"]] = (nodal.get(muro["nj"], 0.0)
                                 + dat.GAMMA_C * muro["t"] * muro["L"] * h)
        for aspa in modelo.get("aspas", []):
            nodal[aspa["nj"]] = (nodal.get(aspa["nj"], 0.0)
                                 + aspa["rho_pp"] * aspa["A_pp"] * aspa["L"])

    for ntag, fz in nodal.items():
        ops.load(ntag, 0.0, 0.0, -abs(fz), 0.0, 0.0, 0.0)
    total = sum(nodal.values())
    return nodal, total


def pesos_por_nivel(modelo, dat=d):
    """W_i por nivel: losa Q_G + peso propio vigas/columnas/muros."""
    A_c, _, _, _ = dat.sec_columna()
    w = {}
    for lvl in range(1, dat.NLEV):
        vx = [v for v in modelo["vigas_x"] if v["nivel"] == lvl]
        vy = [v for v in modelo["vigas_y"] if v["nivel"] == lvl]
        w_losa, longs, area, _ = distribuir_nivel(dat.Q_G, vx, vy, dat, lvl)
        w[lvl] = dat.Q_G * area
        for v in vx + vy:
            a_pp = v.get("A_pp", dat.sec_viga()[0])
            rho_pp = v.get("rho_pp", dat.GAMMA_C)
            w[lvl] += rho_pp * a_pp * longs[v["tag"]]
    for col in modelo["columns"]:
        h = dat.STORY_H[col["story"]]
        a_pp = col.get("A_pp", A_c)
        rho_pp = col.get("rho_pp", dat.GAMMA_C)
        w[col["story"] + 1] += rho_pp * a_pp * h
    for muro in modelo["walls"]:
        h = dat.STORY_H[muro["story"]]
        w[muro["story"] + 1] += dat.GAMMA_C * muro["t"] * muro["L"] * h
    for aspa in modelo.get("aspas", []):
        w[aspa["story"] + 1] += aspa["rho_pp"] * aspa["A_pp"] * aspa["L"]
    return w


def sismo_v_base_y_fuerzas(dat, pesos, dirn):
    """(V_base, F_por_nivel) del patron sismico pseudoestatico."""
    niveles = sorted(pesos.keys())
    w_tot = sum(pesos.values())
    denom = sum(pesos[lv] * dat.LEVEL_Z[lv] for lv in niveles)
    v_base = dat.ALPHA_EQ * w_tot
    fuerzas = {lv: v_base * pesos[lv] * dat.LEVEL_Z[lv] / denom
               for lv in niveles}
    return v_base, fuerzas


def patron_sismico(dirn, modelo, pesos, dat=d):
    """Patron sismico repartido entre nodos esclavos de cada nivel."""
    import openseespy.opensees as ops
    ops.timeSeries("Constant", 2)
    ops.pattern("Plain", 2, 2)

    v_base, fuerzas = sismo_v_base_y_fuerzas(dat, pesos, dirn)
    niveles = sorted(pesos.keys())
    for lv in niveles:
        fi = fuerzas[lv]
        esclavos = [nid_tag for nid_tag in ops.getNodeTags()
                    if abs(ops.nodeCoord(nid_tag)[2] - dat.LEVEL_Z[lv]) < 1e-9
                    and nid_tag != modelo["master"][lv]]
        frac = fi / len(esclavos)
        for nt in esclavos:
            if dirn == "X":
                ops.load(nt, frac, 0.0, 0.0, 0.0, 0.0, 0.0)
            else:
                ops.load(nt, 0.0, frac, 0.0, 0.0, 0.0, 0.0)
    return v_base, fuerzas
