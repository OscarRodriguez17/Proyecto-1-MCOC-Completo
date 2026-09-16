# -*- coding: utf-8 -*-
"""
cargas.py — Cargas del Edificio B.

  - Peso propio (PP): densidad del hormigón x volumen de cada elemento,
    repartido por mitades a sus nodos como fuerza -Z [kN].
  - Sobrecarga (SC): carga distribuida de piso [kN/m²] tomada de la lámina
    de cargas 2024_22-700, aplicada por área tributaria a los nodos de piso.

Unidades: m, kN.  Convención: gravedad en -Z.
"""
import openseespy.opensees as ops
from . import datos_edificio as D

GAMMA_HORMIGON = 25.0     # kN/m³  (hormigón armado)
SC_PISO        = 3.0      # kN/m²  (sobrecarga de uso representativa; lámina 700)
SC_CUBIERTA    = 1.0      # kN/m²
# Carga muerta de losa (lámina 2024_22-700): PP losa (e=0.15·25=3.75) + PM.adic
# (260 kgf/m² ≈ 2.55). Q_G ≈ 6.3 kN/m², idéntica al Edificio A (misma losa/receta).
Q_G_LOSA       = 6.3      # kN/m²  (carga muerta de losa, todos los niveles)


def _long(n1, n2):
    x1, y1, z1 = ops.nodeCoord(n1)
    x2, y2, z2 = ops.nodeCoord(n2)
    return ((x2 - x1) ** 2 + (y2 - y1) ** 2 + (z2 - z1) ** 2) ** 0.5


def area_planta(M):
    """Huella (bounding box) de la planta [m²] — reparto de superficie simple."""
    xs = [c[0] for c in M.coord.values()]
    ys = [c[1] for c in M.coord.values()]
    return (max(xs) - min(xs)) * (max(ys) - min(ys))


def peso_propio(M):
    """Aplica peso propio como cargas nodales -Z. Devuelve el peso total [kN]."""
    from .construir import _props
    W = 0.0
    carga = {}
    for e, tipo in M.tipo.items():
        if tipo == 'brazo':
            continue
        n1, n2 = ops.eleNodes(e)
        # sección: re-deducir A desde el tipo no es directo; usamos volumen por A*L
        # A se obtiene del área ya asignada al elemento mediante sus nodos/orientación.
        # Para PP basta A*L*gamma; recuperamos A del registro de secciones:
        A = M.area.get(e)
        if A is None:
            continue
        w = GAMMA_HORMIGON * A * _long(n1, n2)
        W += w
        carga[n1] = carga.get(n1, 0.0) + w / 2.0
        carga[n2] = carga.get(n2, 0.0) + w / 2.0
    for n, p in carga.items():
        ops.load(n, 0.0, 0.0, -p, 0.0, 0.0, 0.0)
    return W


def sobrecarga(M):
    """Aplica SC de piso repartida a los nodos de cada nivel (área tributaria
    simplificada: área de planta / nº de nodos del nivel). Devuelve SC total [kN]."""
    area = area_planta(M)
    Wsc = 0.0
    for k, nodos in M.by_level.items():
        if k == 0:
            continue
        z = M.coord[next(iter(nodos))][2]
        q = SC_CUBIERTA if abs(z - D.NIVELES[-1][0]) < 1e-6 else SC_PISO
        p = q * area / len(nodos)
        Wsc += q * area
        for n in nodos:
            ops.load(n, 0.0, 0.0, -p, 0.0, 0.0, 0.0)
    return Wsc


def carga_muerta_losa(M):
    """Aplica la carga muerta de losa Q_G (losa + terminaciones) a los nodos de
    cada nivel sobre la base, por área tributaria simplificada. Es carga muerta
    (va en G), en todos los niveles incluida la cubierta. Devuelve total [kN]."""
    area = area_planta(M)
    Wlosa = 0.0
    for k, nodos in M.by_level.items():
        if k == 0:
            continue
        p = Q_G_LOSA * area / len(nodos)
        Wlosa += Q_G_LOSA * area
        for n in nodos:
            ops.load(n, 0.0, 0.0, -p, 0.0, 0.0, 0.0)
    return Wlosa
