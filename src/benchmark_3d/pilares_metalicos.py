"""Pilar metalico P.M.I. faltante en la raiz del voladizo de piso 1 (Edificio A).

==============================================================================
GEOMETRIA VERIFICADA CONTRA LOS DXF (1 u = 1 cm)
==============================================================================
  - Planta 101 (cielo piso 1): cuadrado 30 x 30 cm en la capa RLE-PROYECCION
    con centro en (2810.3, 1943.0); eje F en x = 2061.3 y eje 3 en y = 1943.0
    => 749 cm desde F, sobre el eje 3  =>  (x, y) = (17.50, 0.00) del modelo.
    Rotulo "P.M.I." en (2857.8, 2002.0), igual que los postes de punta.
  - Planta 102 (cielo piso 2): rotulo "P.M. 300x300x20" en el mismo punto.
  - Esta en la proyeccion del poste de punta tag 341 (17.5, -4.30) y en la
    linea del pilar de hormigon tag 14 (10, 0) sobre el eje 3.

Une los nodos de raiz del borde del voladizo (rol "raiz_vol_f", ya existentes,
tags 195 y 196) entre piso 1 (z = -0.05) y piso 2 (z = 3.91), igual que los
postes de punta 340/341: seccion P.M. 300x300x20 (dat.sec_pm()), acero.

ADITIVO: no crea nodos ni toca elementos existentes; solo agrega un elemento
con tag por encima del maximo y lo registra en modelo["columns"] (peso propio,
pesos sismicos, diagramas y exportacion lo heredan).
"""

import openseespy.opensees as ops

import datos_edificio as d
import construir as c


CONFIG_PILAR_RAIZ_F = {
    "x": 17.5,              # borde del voladizo (x_borde de CONFIG_VOL_F)
    "y": 0.0,               # eje 3 (A3)
    "nivel_inf": 1,         # z = -0.05
    "nivel_sup": 2,         # z = 3.91
    "rol": "raiz_vol_f",
}


def _max_tag(getter):
    tags = getter()
    return max(tags) if tags else 0


def _nodo_raiz(modelo, conf, lvl, offset):
    ox, oy = offset
    for nd in modelo.get("voladizo", {}).get("nodos", []):
        if (nd.get("rol") == conf["rol"] and nd.get("lvl") == lvl
                and abs(nd["x"] - (conf["x"] + ox)) < 1e-6
                and abs(nd["y"] - (conf["y"] + oy)) < 1e-6):
            return nd["tag"]
    return None


def agregar_pilar_raiz_vol_f(modelo, dat=None, offset=(0.0, 0.0), cfg=None):
    """Agrega el pilar P.M. 300x300x20 en (17.5, 0) entre piso 1 y piso 2."""
    if dat is None:
        dat = d
    conf = dict(CONFIG_PILAR_RAIZ_F)
    if cfg:
        conf.update(cfg)
    li, ls = conf["nivel_inf"], conf["nivel_sup"]
    ni = _nodo_raiz(modelo, conf, li, offset)
    nj = _nodo_raiz(modelo, conf, ls, offset)
    if ni is None or nj is None:
        raise ValueError("No existen los nodos de raiz del voladizo del eje F.")

    A, Iy, Iz, J = dat.sec_pm()
    et = _max_tag(ops.getEleTags) + 1
    ops.element("elasticBeamColumn", et, ni, nj,
                A, dat.E_STEEL, dat.G_STEEL, J, Iy, Iz, c.GT_COL)
    col = {"tag": et, "ni": ni, "nj": nj, "story": li,
           "seccion": "P.M. 300x300x20", "A_pp": A,
           "rho_pp": dat.GAMMA_STEEL, "material": "acero",
           "id": "P.M.I. raiz eje 3"}
    modelo["columns"] = modelo["columns"] + [col]
    modelo["pilar_raiz_vol_f"] = {"tag": et, "ni": ni, "nj": nj,
                                  "config_usada": conf}
    return modelo
