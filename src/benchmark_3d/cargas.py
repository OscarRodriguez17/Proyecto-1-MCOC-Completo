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
    for i in range(len(xs) - 1):
        for j in range(len(ys) - 1):
            dx = xs[i + 1] - xs[i]
            dy = ys[j + 1] - ys[j]
            area_total += dx * dy
            cuarto = q * dx * dy / 4.0
            bordes = [("x", ys[j], xs[i], xs[i + 1]),
                      ("x", ys[j + 1], xs[i], xs[i + 1]),
                      ("y", xs[i], ys[j], ys[j + 1]),
                      ("y", xs[i + 1], ys[j], ys[j + 1])]
            for orient, fija, a, b in bordes:
                largo = b - a
                if orient == "x":
                    tag = _buscar_viga(vigas_x, fija, a, b)
                else:
                    tag = _buscar_viga(vigas_y, fija, a, b)
                if tag is None:
                    raise RuntimeError("panel sin viga de borde")
                w_losa[tag] += cuarto / largo

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
