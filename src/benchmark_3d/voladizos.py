"""Extension aditiva del modelo del Edificio de Ingenieria: voladizo trasero A3.

REGLA DE ORO: no modifica el modelo base (190 nodos / 321 elementos de
construir.py). Solo INYECTA nodos/elementos con tags estrictamente por encima
de los existentes (consultados con ops.getNodeTags()/getEleTags()). No re-fija
nodos, no reescribe diafragmas, no toca transformaciones ni secciones previas.

==============================================================================
GEOMETRIA VERIFICADA CONTRA LOS DXF (planos 2017_67-102, -103, -303)
==============================================================================
Escala de los planos: 1 unidad = 1 cm (100 u/m). Origen del modelo: eje E (x=0),
eje 3 = A3 (y=0). Retícula reproducida exactamente (E..I' y ejes 3..1).

VOLADIZO TRASERO (proyeccion en -Y, mas alla del eje A3):
  - Vuelo: cota de plano 2027 cm de fondo total - 1615 cm de reticula A3..A1
    = 412 cm, medidos desde el eje de la viga perimetral A3. El emparrillado
    de punta se midio con caras en y=-4.00 y y=-4.60 m (ancho 0.60 m) -> eje de
    punta en y = -4.30 m respecto de la grilla A3.   => PROJ_Y = 4.30 m
  - Ejes con nervadura: SOLO G (x=20) y H (x=30). Son las unicas barras que
    salen de A3 hacia -Y; en E,F,I,I' bajo A3 solo esta la viga perimetral A3
    (ya presente en el modelo base). [medido en 102 y 103]
  - Nervaduras (A3->punta) y viga de borde de punta (G<->H): HORMIGON, ancho
    0.60 m (caras a 0.60) -> V.60/80 (dat.sec_viga()). [medido en 102 y 103]
  - Postes de punta en G y H, entre niveles: metalicos P.M.I. = P.M. 300x300x20
    (dat.sec_pm()); confirmado ademas por los cajones RLA-COLUMNAS-CAJONES
    P.M. 300x300x20 de la elevacion A3 (plano 303).
  - Niveles: 3 (cielo p3, z=7.87, plano 102) y 4 (techo, z=11.83, plano 103).

ARRIOSTRAMIENTO (aspa) del voladizo trasero [cortes G=307 y H=308]:
  - Diagonal metalica V.M. 300x300x5 entre la PUNTA del voladizo en el nivel 3
    y el nodo A3 (base del voladizo) en el nivel 4, en los ejes G y H.

VOLADIZO METALICO ARRIOSTRADO DE PISO 1 — Eje F [corte 306, planta 101]:
  - Solo en el EJE F (x=10). Vuelo en -Y hasta la punta (Y = -4.30 m).
  - Cordones horizontales en piso 1 (z=-0.05) y piso 2 (z=3.91), poste metalico
    P.M. 300x300x20 en la punta y diagonal V.M. 300x300x5 (aspa) del pie al
    apoyo sobre A3. Ver `agregar_voladizo_piso1_ejeF()`.

CUBIERTA "ACARTELADA": no se agrega geometria. En 102/103 el techo es losa
e=15 con vigas V.60/80 que YA existen en el nivel 4 del modelo base. La altura
variable "VAR" (vista en cortes) es un matiz de seccion de barras existentes,
no barras faltantes. Ver `agregar_cubierta_acartelada()` (stub documentado).

NOTA: la losa tras H..I del plano 101 ("RADIER SOBRE TERRENO", dilatacion 1 cm)
NO se modela: es una losa apoyada en el suelo, estructuralmente separada del
edificio.

NO RESUELTO con estos 3 planos (queda para los cortes por ejes E..I): un marco
metalico secundario P.M./V.M. sobre la punta (aparece en la elevacion 303). No
se modela para no inventar su entramado; los cortes acotados lo fijarian.

ADAPTADOR DEL PIPELINE: `integrar_voladizo(modelo, extra)` fusiona en el dict
de construir.construir() lo generado por el modulo (voladizo trasero + aspas +
voladizo metalico de piso 1) para que cargas.py / analizar.py / exportar_json.py
lo conozcan sin tocar `construir.py`.
==============================================================================
"""

import math

import openseespy.opensees as ops

import datos_edificio as d
import construir as c


# ── Geometria del voladizo: VALORES VERIFICADOS en DXF (ver cabecera) ─────────
CONFIG_VOLADIZO = {
    "proj_y": 4.30,             # m; eje de punta vs grilla A3 (dim plano 4.12 desde eje viga A3)
    "ejes_x": ("G", "H"),       # unicos ejes con nervadura (x=20, 30)
    "niveles": (3, 4),          # cielo p3 (z=7.87) y techo (z=11.83)
    "viga_borde_punta": True,   # viga transversal G<->H en la punta
    "postes_punta": True,       # postes metalicos P.M. en las puntas
}


def _max_tag(getter):
    tags = getter()
    return max(tags) if tags else 0


def _y_index_A3(dat):
    y_items = sorted(dat.GRID_Y.items(), key=lambda kv: kv[1])
    for yi, (k, v) in enumerate(y_items):
        if k == "A3" or abs(v) < 1e-9:
            return yi
    return 0


def _longitud_3d(nta, ntb):
    """Longitud [m] del elemento entre los nodos nta y ntb (del modelo OpenSees)."""
    ca = ops.nodeCoord(nta)
    cb = ops.nodeCoord(ntb)
    if not ca or not cb:
        return 0.0
    return math.sqrt(sum((a - b) ** 2 for a, b in zip(ca, cb)))


def agregar_voladizos_y_cubierta(dat=None, modelo=None, cfg=None, offset=(0.0, 0.0)):
    """Inyecta el voladizo trasero (Eje A3) sobre un modelo YA construido.

        modelo = construir.construir()          # 190 nodos / 321 elementos
        extra  = agregar_voladizos_y_cubierta(modelo=modelo)

    `offset` debe ser el mismo (ox,oy) que se paso a construir(). Devuelve un
    dict con SOLO los nodos/elementos nuevos. No altera ningun tag existente.
    """
    if dat is None:
        dat = d
    if modelo is None:
        raise ValueError("Se requiere `modelo` (salida de construir.construir()).")
    conf = dict(CONFIG_VOLADIZO)
    if cfg:
        conf.update(cfg)

    ox, oy = offset
    nid = modelo["nid"]
    x_keys = list(dat.GRID_X.keys())
    x_vals = list(dat.GRID_X.values())
    yi_A3 = _y_index_A3(dat)

    # Secciones (mismas del modelo base; nada inventado).
    A_v, i_weak_v, i_strong_v, J_v = dat.sec_viga()      # V.60/80 hormigon
    A_pm, Iy_pm, Iz_pm, J_pm = dat.sec_pm()              # P.M. 300x300x20 acero

    nt = _max_tag(ops.getNodeTags)
    et = _max_tag(ops.getEleTags)

    proj = conf["proj_y"]
    niveles = tuple(conf["niveles"])
    ejes_xi = [x_keys.index(k) for k in conf["ejes_x"] if k in x_keys]

    tip = {}                       # (xi, lvl) -> tag nodo punta
    r = {"nodos": [], "nervaduras": [], "vigas_borde": [], "postes": []}

    # ── 1) Nodos de punta + nervaduras A3->punta (V.60/80) por nivel ────────
    for lvl in niveles:
        z = dat.LEVEL_Z[lvl]
        for xi in ejes_xi:
            base_key = (xi, yi_A3, lvl)
            if base_key not in nid:
                continue
            x = x_vals[xi] + ox
            y_tip = 0.0 - proj + oy

            nt += 1
            ops.node(nt, x, y_tip, z)
            tip[(xi, lvl)] = nt
            r["nodos"].append({"tag": nt, "xi": xi, "lvl": lvl,
                               "x": x, "y": y_tip, "z": z, "rol": "punta_voladizo"})

            et += 1   # nervadura en -Y: nodo A3 (base) -> punta (nuevo)
            ops.element("elasticBeamColumn", et, nid[base_key], tip[(xi, lvl)],
                        A_v, dat.E_C, dat.G_C, J_v, i_strong_v, i_weak_v, c.GT_BEAM)
            r["nervaduras"].append({"tag": et, "ni": nid[base_key], "nj": tip[(xi, lvl)],
                                    "nivel": lvl, "seccion": "V.60/80",
                                    "A_pp": A_v, "rho_pp": dat.GAMMA_C,
                                    "material": "concreto"})

    # ── 2) Viga de borde de punta que une las puntas (G<->H) por nivel ──────
    if conf["viga_borde_punta"] and len(ejes_xi) >= 2:
        for lvl in niveles:
            for a, b in zip(ejes_xi[:-1], ejes_xi[1:]):
                if (a, lvl) in tip and (b, lvl) in tip:
                    et += 1
                    ops.element("elasticBeamColumn", et, tip[(a, lvl)], tip[(b, lvl)],
                                A_v, dat.E_C, dat.G_C, J_v, i_strong_v, i_weak_v, c.GT_BEAM)
                    r["vigas_borde"].append({"tag": et, "ni": tip[(a, lvl)],
                                             "nj": tip[(b, lvl)], "nivel": lvl,
                                             "seccion": "V.60/80",
                                             "A_pp": A_v, "rho_pp": dat.GAMMA_C,
                                             "material": "concreto"})

    # ── 3) Postes metalicos P.M. en las puntas, entre niveles ───────────────
    if conf["postes_punta"]:
        niv_ord = sorted(niveles)
        for lo, hi in zip(niv_ord[:-1], niv_ord[1:]):
            for xi in ejes_xi:
                if (xi, lo) in tip and (xi, hi) in tip:
                    et += 1
                    ops.element("elasticBeamColumn", et, tip[(xi, lo)], tip[(xi, hi)],
                                A_pm, dat.E_STEEL, dat.G_STEEL, J_pm, Iy_pm, Iz_pm,
                                c.GT_COL)
                    r["postes"].append({"tag": et, "ni": tip[(xi, lo)],
                                        "nj": tip[(xi, hi)], "story": lo,
                                        "seccion": "P.M. 300x300x20",
                                        "A_pp": A_pm, "rho_pp": dat.GAMMA_STEEL,
                                        "material": "acero"})

    n_nod = len(r["nodos"])
    n_ele = len(r["nervaduras"]) + len(r["vigas_borde"]) + len(r["postes"])
    r["tip_nodes"] = tip
    r["config_usada"] = conf
    r["resumen"] = {"nodos_nuevos": n_nod, "elementos_nuevos": n_ele,
                    "nota": "Nodos de punta NO se agregan al rigidDiaphragm "
                            "(cantilever libre en vertical); base intacta."}
    return r


def agregar_cubierta_acartelada(dat=None, modelo=None):
    """STUB DOCUMENTADO — intencionalmente NO agrega elementos.

    Las vigas de techo con altura variable (V.60/VAR en cortes) son las vigas
    de techo que YA existen en el nivel 4 del modelo base (V.60/80). "VAR" es un
    matiz de SECCION de barras existentes, no barras faltantes: agregarlas aqui
    las DUPLICARIA e invalidaria el conteo y las masas. Verificado en 102/103:
    techo = losa e=15 + V.60/80, sin geometria adicional.
    """
    return {"cubierta": "no-op (ver docstring): las vigas de techo ya existen"}


# =============================================================================
# ESTRUCTURA A — Arriostramiento (aspa) del voladizo trasero
# =============================================================================
# Verificado en cortes por eje G (307) y H (308), anclado en Y contra la
# planta 101 (columnas A3/A2/A1). La diagonal medida va de ~(Y=-3.9, z=9.7) a
# ~(Y=-0.35, z=12.5), en ejes G y H, entre los niveles 3 (z=7.87) y 4
# (z=11.83). Se idealiza como una diagonal nodo-a-nodo entre la PUNTA del
# voladizo en el nivel 3 y el nodo A3 (base del voladizo) en el nivel 4, en
# cada eje. Perfil: cajon metalico V.M. 300x300x5 (dat.sec_vm()).
# Requiere el dict `extra` devuelto por agregar_voladizos_y_cubierta().

def agregar_arriostramiento_voladizo(dat=None, modelo=None, extra=None):
    if dat is None:
        dat = d
    if modelo is None or extra is None:
        raise ValueError("Requiere `modelo` y el `extra` de agregar_voladizos_y_cubierta().")
    tip = extra["tip_nodes"]                 # (xi, lvl) -> tag nodo punta
    nid = modelo["nid"]
    yi_A3 = _y_index_A3(dat)
    x_keys = list(dat.GRID_X.keys())
    lo, hi = sorted(dat.ANEXO["levels"])     # 3 (z=7.87) y 4 (z=11.83)

    A_vm, Iy_vm, Iz_vm, J_vm = dat.sec_vm()
    et = _max_tag(ops.getEleTags)
    r = {"aspas": []}

    for eje in ("G", "H"):
        if eje not in x_keys:
            continue
        xi = x_keys.index(eje)
        n_tip_lo = tip.get((xi, lo))              # punta, nivel 3
        n_base_hi = nid.get((xi, yi_A3, hi))      # A3, nivel 4 (existente)
        if n_tip_lo is None or n_base_hi is None:
            continue
        et += 1
        ops.element("elasticBeamColumn", et, n_tip_lo, n_base_hi,
                    A_vm, dat.E_STEEL, dat.G_STEEL, J_vm, Iy_vm, Iz_vm, c.GT_COL)
        r["aspas"].append({"tag": et, "ni": n_tip_lo, "nj": n_base_hi,
                           "eje": eje, "seccion": "V.M. 300x300x5 (aspa)",
                           "story": lo, "A_pp": A_vm, "rho_pp": dat.GAMMA_STEEL,
                           "material": "acero"})
    r["resumen"] = {"aspas_nuevas": len(r["aspas"])}
    return r


# =============================================================================
# VOLADIZO METALICO ARRIOSTRADO DE PISO 1 — Eje F (el "voladizo del piso 1")
# =============================================================================
# Verificado en el corte por eje F (306, bloque EJEF-F') y la planta 101.
# Geometria (Y respecto de A3=0, Z reales tras corregir el offset del rotulado):
#   - Solo en el EJE F (x=10). En E y G no hay proyeccion tras A3 (planta 101).
#   - Vuelo en -Y hasta la punta: Y = -4.30 m (planta 101: material a -4.26/-4.60).
#   - Dos cordones horizontales: piso 1 (z=-0.05) y piso 2 (z=3.91).
#   - Poste metalico en la punta (P.M. 300x300x20) entre ambos cordones.
#   - Diagonal (aspa) del pie de la punta (piso1) a la esquina sobre A3 (piso2).
#   - Perfiles: cordones/diagonal V.M. 300x300x5 ; poste P.M. 300x300x20.
# Se ancla a los nodos existentes (F, A3) en los niveles 1 y 2. Nodos de punta
# NO se agregan a los diafragmas (no se mutan restricciones del modelo base).

CONFIG_VOL_F = {
    "eje_x": "F",          # nervadura interior anclada a la reticula (x=10)
    "x_borde": 17.50,      # nervadura exterior del voladizo (borde; no es eje de reticula)
    "proj_y": 4.30,        # vuelo en -Y (planta 101: material a -4.26/-4.60)
    "nivel_inf": 1,        # z = -0.05 (piso 1)
    "nivel_sup": 2,        # z =  3.91 (piso 2)
}


def agregar_voladizo_piso1_ejeF(dat=None, modelo=None, cfg=None, offset=(0.0, 0.0)):
    """Voladizo metalico arriostrado de piso 1 (aditivo) — RECTANGULO en planta.

    Verificado en planta 101 y corte por eje F (306): el voladizo es un
    rectangulo tras A3, con dos nervaduras (eje F en x=10 y borde en x=17.5)
    unidas por vigas de punta, entre piso 1 (z=-0.05) y piso 2 (z=3.91), con
    postes metalicos en ambas puntas y una diagonal en cada nervadura.

    Devuelve dict con claves `nodos`, `vigas_y` (nervaduras), `vigas_x` (vigas
    de punta y de raiz sobre A3), `poste`, `diagonal`. Los nodos de punta
    llevan rol="punta_vol_f"; los de raiz del borde, rol="raiz_vol_f".
    La nervadura de borde (x=17.5) no cae sobre un eje de la reticula, por lo
    que se ancla al edificio por la viga de raiz que corre sobre A3 hasta el
    nodo existente del eje F (no se parte ninguna barra del modelo base).
    """
    if dat is None:
        dat = d
    if modelo is None:
        raise ValueError("Requiere `modelo` (salida de construir.construir()).")
    conf = dict(CONFIG_VOL_F)
    if cfg:
        conf.update(cfg)
    ox, oy = offset
    nid = modelo["nid"]
    x_keys = list(dat.GRID_X.keys())
    yi_A3 = _y_index_A3(dat)
    xi_F = x_keys.index(conf["eje_x"])
    li, ls = conf["nivel_inf"], conf["nivel_sup"]
    zi, zs = dat.LEVEL_Z[li], dat.LEVEL_Z[ls]

    nF_inf = nid.get((xi_F, yi_A3, li))
    nF_sup = nid.get((xi_F, yi_A3, ls))
    if nF_inf is None or nF_sup is None:
        raise ValueError("No existen los nodos base (F,A3) en los niveles pedidos.")

    A_vm, Iy_vm, Iz_vm, J_vm = dat.sec_vm()
    A_pm, Iy_pm, Iz_pm, J_pm = dat.sec_pm()
    xF = dat.GRID_X[conf["eje_x"]] + ox
    xB = conf["x_borde"] + ox
    y_tip = -conf["proj_y"] + oy
    y_raiz = 0.0 + oy

    nt = _max_tag(ops.getNodeTags)
    et = _max_tag(ops.getEleTags)
    r = {"nodos": [], "vigas_y": [], "vigas_x": [], "poste": [], "diagonal": []}

    def nodo(x, y, z, rol, lvl):
        nonlocal nt
        nt += 1
        ops.node(nt, x, y, z)
        r["nodos"].append({"tag": nt, "x": x, "y": y, "z": z, "lvl": lvl, "rol": rol})
        return nt

    # nodos nuevos: raiz del borde (2) + puntas F (2) + puntas borde (2)
    rB_inf = nodo(xB, y_raiz, zi, "raiz_vol_f", li)
    rB_sup = nodo(xB, y_raiz, zs, "raiz_vol_f", ls)
    tF_inf = nodo(xF, y_tip, zi, "punta_vol_f", li)
    tF_sup = nodo(xF, y_tip, zs, "punta_vol_f", ls)
    tB_inf = nodo(xB, y_tip, zi, "punta_vol_f", li)
    tB_sup = nodo(xB, y_tip, zs, "punta_vol_f", ls)

    def _sec(sec):
        return (A_vm, Iy_vm, Iz_vm, J_vm, "V.M. 300x300x5") if sec == "vm" \
            else (A_pm, Iy_pm, Iz_pm, J_pm, "P.M. 300x300x20")

    def viga_y(ni, nj, x, nivel, sec="vm"):
        nonlocal et
        A, Iy, Iz, J, nom = _sec(sec)
        et += 1
        ops.element("elasticBeamColumn", et, ni, nj, A, dat.E_STEEL, dat.G_STEEL,
                    J, Iy, Iz, c.GT_BEAM)
        r["vigas_y"].append({"tag": et, "ni": ni, "nj": nj, "nivel": nivel,
                             "x": x, "y_i": y_tip, "y_j": y_raiz, "seccion": nom,
                             "A_pp": A, "rho_pp": dat.GAMMA_STEEL, "material": "acero"})

    def viga_x(ni, nj, y, nivel, sec="vm"):
        nonlocal et
        A, Iy, Iz, J, nom = _sec(sec)
        et += 1
        ops.element("elasticBeamColumn", et, ni, nj, A, dat.E_STEEL, dat.G_STEEL,
                    J, Iy, Iz, c.GT_BEAM)
        r["vigas_x"].append({"tag": et, "ni": ni, "nj": nj, "nivel": nivel,
                             "y": y, "x_i": xF, "x_j": xB, "seccion": nom,
                             "A_pp": A, "rho_pp": dat.GAMMA_STEEL, "material": "acero"})

    def poste(ni, nj, story):
        nonlocal et
        et += 1
        ops.element("elasticBeamColumn", et, ni, nj, A_pm, dat.E_STEEL, dat.G_STEEL,
                    J_pm, Iy_pm, Iz_pm, c.GT_COL)
        r["poste"].append({"tag": et, "ni": ni, "nj": nj, "story": story,
                           "seccion": "P.M. 300x300x20", "A_pp": A_pm,
                           "rho_pp": dat.GAMMA_STEEL, "material": "acero"})

    def diagonal(ni, nj, story):
        nonlocal et
        et += 1
        ops.element("elasticBeamColumn", et, ni, nj, A_vm, dat.E_STEEL, dat.G_STEEL,
                    J_vm, Iy_vm, Iz_vm, c.GT_COL)
        r["diagonal"].append({"tag": et, "ni": ni, "nj": nj, "story": story,
                             "seccion": "V.M. 300x300x5 (aspa)", "A_pp": A_vm,
                             "rho_pp": dat.GAMMA_STEEL, "material": "acero"})

    # nervaduras (Y): eje F y borde, en piso 1 y piso 2
    viga_y(nF_inf, tF_inf, xF, li);  viga_y(nF_sup, tF_sup, xF, ls)
    viga_y(rB_inf, tB_inf, xB, li);  viga_y(rB_sup, tB_sup, xB, ls)
    # vigas de punta (X) que cierran el rectangulo, en ambos niveles
    viga_x(tF_inf, tB_inf, y_tip, li);  viga_x(tF_sup, tB_sup, y_tip, ls)
    # vigas de raiz (X) sobre A3 -> anclan el borde al nodo del eje F
    viga_x(nF_inf, rB_inf, y_raiz, li);  viga_x(nF_sup, rB_sup, y_raiz, ls)
    # postes metalicos en ambas puntas
    poste(tF_inf, tF_sup, li);  poste(tB_inf, tB_sup, li)
    # diagonal (aspa) en cada nervadura: punta piso1 -> raiz piso2
    diagonal(tF_inf, nF_sup, li);  diagonal(tB_inf, rB_sup, li)

    r["config_usada"] = conf
    r["resumen"] = {"nodos": len(r["nodos"]),
                    "elementos": (len(r["vigas_y"]) + len(r["vigas_x"])
                                  + len(r["poste"]) + len(r["diagonal"]))}
    return r


# =============================================================================
# ADAPTADOR DEL PIPELINE (anclaje con cargas.py, pesos por nivel, sismo y
# exportador Unity). Fusiona en `modelo` (dict de construir.construir()) todo
# lo generado por el modulo (voladizo trasero + aspas + voladizo piso 1)
# para que el resto del pipeline los conozca sin tocar `construir.py`.
#      nervaduras        -> modelo['vigas_y']
#      vigas_borde       -> modelo['vigas_x']
#      postes            -> modelo['columns']
#      aspas             -> modelo['aspas']  (nueva categoria, diagonales acero)
#      vol_f.cordones    -> modelo['vigas_y']   (voladizo metalico piso 1, eje F)
#      vol_f.poste       -> modelo['columns']
#      vol_f.diagonal    -> modelo['aspas']
#      nodos (todos)     -> modelo['voladizo']['nodos']
# No modifica geometria ni secciones; no altera tags del modelo base.
# ═══════════════════════════════════════════════════════════════════════════
def integrar_voladizo(modelo, extra, dat=None, offset=(0.0, 0.0)):
    """Fusiona `extra` (salida de los generadores del modulo) en `modelo`.

    `extra` es el dict combinado: el del voladizo trasero (con claves
    nodos/nervaduras/vigas_borde/postes/tip_nodes/config_usada) mas, si
    existen, `aspas` (arriostramiento) y `vol_f` (voladizo metalico de piso
    1, eje F). La funcion es tolerante: si `aspas`/`vol_f` faltan, solo
    integra lo presente (compatibilidad con los tests del voladizo base).

    A las vigas se les agregan las coordenadas que esperan cargas.py y el
    exportador (x/y_i/y_j para vigas_y; y/x_i/x_j para vigas_x). Los nodos
    nuevos quedan en modelo['voladizo']['nodos'] con su rol (punta_voladizo,
    punta_vol_f).
    """
    if dat is None:
        dat = d
    ox, oy = offset
    extra = extra or {}
    conf = extra.get("config_usada", CONFIG_VOLADIZO)
    proj = conf.get("proj_y", 0.0)
    x_vals = list(dat.GRID_X.values())
    tip_xi = {nd["tag"]: nd["xi"] for nd in extra.get("nodos", [])
              if "xi" in nd}

    vig_y = []
    for el in extra.get("nervaduras", []):
        xi = tip_xi.get(el["nj"])
        if xi is None:
            continue
        el = dict(el)
        el["x"] = x_vals[xi] + ox
        el["y_i"] = 0.0 - proj + oy
        el["y_j"] = 0.0 + oy
        vig_y.append(el)

    vig_x = []
    for el in extra.get("vigas_borde", []):
        xa = tip_xi.get(el["ni"])
        xb = tip_xi.get(el["nj"])
        if xa is None or xb is None:
            continue
        el = dict(el)
        el["y"] = 0.0 - proj + oy
        el["x_i"] = x_vals[xa] + ox
        el["x_j"] = x_vals[xb] + ox
        vig_x.append(el)

    # ── Voladizo metalico de piso 1, eje F (RECTANGULO) ────────────────────
    vol_f = extra.get("vol_f")
    if vol_f:
        # nervaduras (Y) y vigas de punta/raiz (X): ya traen coordenadas.
        for el in vol_f.get("vigas_y", []):
            vig_y.append(dict(el))
        for el in vol_f.get("vigas_x", []):
            vig_x.append(dict(el))
        if vol_f.get("poste"):
            modelo["columns"] = modelo["columns"] + vol_f["poste"]
        extra.setdefault("nodos", []).extend(vol_f.get("nodos", []))

    if vig_y:
        modelo["vigas_y"] = modelo["vigas_y"] + vig_y
    if vig_x:
        modelo["vigas_x"] = modelo["vigas_x"] + vig_x
    if extra.get("postes"):
        modelo["columns"] = modelo["columns"] + extra["postes"]

    # ── Aspas (arriostramiento metalico V.M., diagonales) ──────────────────
    aspas = []
    for el in extra.get("aspas", []):
        el = dict(el)
        el["L"] = _longitud_3d(el["ni"], el["nj"])
        aspas.append(el)
    if vol_f:
        for el in vol_f.get("diagonal", []):
            el = dict(el)
            el["L"] = _longitud_3d(el["ni"], el["nj"])
            aspas.append(el)
    if aspas:
        modelo["aspas"] = modelo.get("aspas", []) + aspas
    else:
        modelo["aspas"] = modelo.get("aspas", [])

    extra["aspas"] = aspas
    extra["vol_f"] = vol_f
    modelo["voladizo"] = extra
    return modelo