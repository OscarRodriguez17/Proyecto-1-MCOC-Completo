"""Viga secundaria V.60/80 entre ejes F-G / 3-2 de la planta 101 (Edificio A).

==============================================================================
GEOMETRIA VERIFICADA CONTRA EL DXF 2017_67-101 (planta cielo piso 1, 1 u = 1 cm)
==============================================================================
  - Capa RLE-VIGA: dos lineas paralelas en x = 2531.3 y 2591.3 (ancho 60 cm),
    con el eje F en x = 2061.3  ->  caras a 470 y 530 cm de F, eje de la viga a
    500 cm de F  =>  x = 15.00 m en coordenadas del modelo (E = 0, F = 10).
  - Corre de la cara de la viga del eje 3 (y = 1973) a la del eje 2 (y = 2638):
    entre ejes, del eje 3 (A3, y = 0.00) al eje 2 (A2, y = 7.25). L = 7.25 m.
  - Rotulo "V. 60/80" en (2500, 2308) del mismo plano.
  - Alcance pedido: nivel de la planta 101 (indice 1, z = -0.05) y, desde la
    Sesion 29, tambien el cielo piso 2 de la lamina 102 (indice 2, z = 3.91):
    alli la viga esta igual, caras a 470/530 cm de F (x 2363.2 / 2423.2 con
    F = 1893.2), del eje 3 al eje 2. NO se agrega en el cielo piso 3 (7.87),
    aunque tambien aparece en la lamina 102, por decision del usuario.

==============================================================================
EXCEPCION JUSTIFICADA A LA REGLA ADITIVA
==============================================================================
Los extremos de la viga caen a mitad de vano de las vigas F-G de los ejes 3 y
2, y en el camino cruza la viga F-G del eje intermedio 2a (y = 2.31) que tiene
la reticula del modelo. Cada una es UN elemento de 10 m sin nodo intermedio.
OpenSees solo conecta en nodos, de modo que para que la viga trabaje (rigidez,
reacciones, diagramas) esas TRES vigas base del nivel 1 se parten en x = 15:

  - tramo 10 -> 15 : CONSERVA el tag original (ni original -> nodo nuevo)
  - tramo 15 -> 20 : tag NUEVO               (nodo nuevo -> nj original)

Ambos tramos con exactamente las mismas propiedades de seccion que la viga
original (sec_viga_compuesta(10, L/T), mismo orden de argumentos que
construir.py). construir.py y datos_edificio.py NO se tocan: el modelo base
sigue siendo 190 nodos / 321 elementos; el parche se aplica despues, igual que
los voladizos (enganche en analizar.construir_con_voladizo).

La viga nueva se modela, como todas las vigas Y del vano 3-2, en dos tramos
(3 -> 2a y 2a -> 2), cada uno con su tag.

Area tributaria: los tramos llevan "divide_panel": True y cargas.py divide los
paneles F-G / A3-A2a y F-G / A2a-A2 del nivel 1 en dos subpaneles de 5 m.

Observacion: la viga de raiz del voladizo metalico del eje F (y = 0, x 10 ->
17.5, nivel 1) pasa por x = 15 sin nodo; no se modifica.
"""

import openseespy.opensees as ops

import datos_edificio as d
import construir as c


CONFIG_VIGA_FG = {
    "x": 15.0,          # m: 500 cm desde el eje F
    "x_a": "F",         # eje X izquierdo del vano que se parte
    "x_b": "G",         # eje X derecho
    "y0": "A3",         # eje 3
    "y1": "A2",         # eje 2
    "nivel": 1,         # planta 101, z = -0.05
    "rol": "viga_sec_fg",
}


def _max_tag(getter):
    tags = getter()
    return max(tags) if tags else 0


def _buscar_viga_x_base(modelo, nivel, y, x_i, x_j):
    for v in modelo["vigas_x"]:
        if (v["nivel"] == nivel and abs(v["y"] - y) < 1e-9
                and abs(v["x_i"] - x_i) < 1e-9 and abs(v["x_j"] - x_j) < 1e-9
                and v.get("material") == "concreto"):
            return v
    return None


def agregar_viga_secundaria_FG_piso1(modelo, dat=None, offset=(0.0, 0.0),
                                    cfg=None):
    """Inyecta la viga secundaria F-G / 3-2 en el modelo OpenSees y en el dict
    `modelo` (vigas_x, vigas_y y nodos exportables). Por defecto en el nivel 1
    (planta 101); con cfg={"nivel": 2} la del cielo piso 2 (lamina 102).

    Devuelve el mismo `modelo`, con el resumen en modelo["vigas_sec_fg"][nivel]
    (y en modelo["viga_sec_fg"] para el nivel 1).
    """
    if dat is None:
        dat = d
    conf = dict(CONFIG_VIGA_FG)
    if cfg:
        conf.update(cfg)
    ox, oy = offset
    lvl = conf["nivel"]
    z = dat.LEVEL_Z[lvl]
    xm = conf["x"]
    xa, xb = dat.GRID_X[conf["x_a"]], dat.GRID_X[conf["x_b"]]
    y0, y1 = dat.GRID_Y[conf["y0"]], dat.GRID_Y[conf["y1"]]
    if not (xa < xm < xb):
        raise ValueError("La viga secundaria debe caer dentro del vano.")

    # ejes Y de la reticula dentro del vano [y0, y1] (incluye A2a = 2.31):
    # la viga cruza cada viga X de esos ejes y debe compartir nodo con ella.
    ys = sorted(v for v in dat.GRID_Y.values() if y0 - 1e-9 <= v <= y1 + 1e-9)

    nt = _max_tag(ops.getNodeTags)
    et = _max_tag(ops.getEleTags)

    # ── nodos nuevos sobre las vigas F-G de los ejes 3, 2a y 2 ──
    nodos = []
    nodo_de_y = {}
    for y in ys:
        nt += 1
        ops.node(nt, xm + ox, y + oy, z)
        nodo_de_y[y] = nt
        nodos.append({"tag": nt, "x": xm + ox, "y": y + oy, "z": z,
                      "lvl": lvl, "rol": conf["rol"]})
    # se suman al diafragma rigido del nivel (mismo nodo maestro)
    ops.rigidDiaphragm(3, modelo["master"][lvl], *nodo_de_y.values())

    # ── partir las vigas base F-G del nivel en x = xm ──
    tramos_nuevos = []
    partidas = []
    for y in ys:
        v = _buscar_viga_x_base(modelo, lvl, y, xa, xb)
        if v is None:
            raise ValueError(f"No existe la viga base F-G en y={y}, nivel {lvl}.")
        tipo = v["seccion"]                       # "L" (borde) o "T" (interior)
        A_b, Iz_b, Iy_b, J_b = dat.sec_viga_compuesta(xb - xa, tipo)
        tag_orig, ni, nj = v["tag"], v["ni"], v["nj"]
        nm = nodo_de_y[y]
        ops.remove("ele", tag_orig)
        ops.element("elasticBeamColumn", tag_orig, ni, nm,
                    A_b, dat.E_C, dat.G_C, J_b, Iy_b, Iz_b, c.GT_BEAM)
        et += 1
        ops.element("elasticBeamColumn", et, nm, nj,
                    A_b, dat.E_C, dat.G_C, J_b, Iy_b, Iz_b, c.GT_BEAM)
        v["nj"] = nm
        v["x_j"] = xm
        v["partida_en"] = xm
        nuevo = dict(v)
        nuevo.update({"tag": et, "ni": nm, "nj": nj, "x_i": xm, "x_j": xb})
        tramos_nuevos.append(nuevo)
        partidas.append({"tag_original": tag_orig, "tag_nuevo": et, "y": y})
    modelo["vigas_x"] = modelo["vigas_x"] + tramos_nuevos

    # ── viga secundaria nueva (Y), un elemento por tramo entre ejes Y ──
    # Misma convencion que las vigas Y del modelo base (seccion T por tramo).
    vigas = []
    for ya, yb in zip(ys[:-1], ys[1:]):
        A_b, Iz_b, Iy_b, J_b = dat.sec_viga_compuesta(yb - ya, "T")
        et += 1
        ops.element("elasticBeamColumn", et, nodo_de_y[ya], nodo_de_y[yb],
                    A_b, dat.E_C, dat.G_C, J_b, Iy_b, Iz_b, c.GT_BEAM)
        vigas.append({"tag": et, "ni": nodo_de_y[ya], "nj": nodo_de_y[yb],
                      "nivel": lvl, "x": xm, "y_i": ya, "y_j": yb,
                      "seccion": "T", "A_pp": dat.sec_viga()[0],
                      "rho_pp": dat.GAMMA_C, "material": "concreto",
                      "divide_panel": True, "id": "V.SEC-FG"})
    modelo["vigas_y"] = modelo["vigas_y"] + vigas

    # ── nodos exportables (analizar.exportar_json lee modelo["voladizo"]) ──
    modelo.setdefault("voladizo", {}).setdefault("nodos", []).extend(nodos)

    info = {
        "nivel": lvl,
        "tags_viga": [v["tag"] for v in vigas],
        "nodos": [n["tag"] for n in nodos],
        "partidas": partidas,
        "config_usada": conf,
        "resumen": {"nodos": len(nodos), "elementos_nuevos": len(vigas) + len(partidas)},
    }
    # registro por nivel; "viga_sec_fg" sigue siendo el del nivel 1 (planta 101)
    modelo.setdefault("vigas_sec_fg", {})[lvl] = info
    if lvl == CONFIG_VIGA_FG["nivel"]:
        modelo["viga_sec_fg"] = info
    return modelo
