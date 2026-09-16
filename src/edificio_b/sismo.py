# -*- coding: utf-8 -*-
"""
sismo.py — Sismo pseudoestático del Edificio B (paridad con el Edificio A).

Método (idéntico al del Edificio A):
  - Peso sísmico W_i por nivel = PESO MUERTO (peso propio del marco) del
    nivel. El peso de cada elemento se asigna al nivel de su nodo SUPERIOR
    (columnas/muros → piso que cargan; vigas → su propio nivel). La suma de
    los W_i reproduce exactamente el peso propio total (`cargas.peso_propio`).
  - Corte basal:   V = ALPHA_EQ · Σ W_i
  - Distribución:  F_i = V · W_i·z_i / Σ(W_j·z_j)   con z ABSOLUTO (como A).
  - Las fuerzas F_i se reparten en partes iguales entre los nodos ESCLAVOS de
    cada nivel (nunca sobre el nodo maestro del diafragma rígido).

Este módulo es ADITIVO: no modifica geometría, IDs ni el peso propio/sobrecarga
ya calibrados. Solo agrega el patrón lateral y su distribución.

Unidades: m, kN.
"""
import openseespy.opensees as ops

from . import datos_edificio as D
from . import cargas


# --------------------------------------------------------- nivel <-> z absoluto
_Z_NIVEL = {k: z for k, (z, _) in enumerate(D.NIVELES)}   # k -> z [m]


def _nivel_de_z(z, tol=0.05):
    """Índice de nivel k cuyo z coincide con `z` (nodo superior de un elemento)."""
    mejor = min(_Z_NIVEL, key=lambda k: abs(_Z_NIVEL[k] - z))
    return mejor if abs(_Z_NIVEL[mejor] - z) <= tol else None


def _long(n1, n2):
    x1, y1, z1 = ops.nodeCoord(n1)
    x2, y2, z2 = ops.nodeCoord(n2)
    return ((x2 - x1) ** 2 + (y2 - y1) ** 2 + (z2 - z1) ** 2) ** 0.5


# ------------------------------------------------------------ pesos por nivel
def pesos_por_nivel(M):
    """W_i [kN] por nivel (peso muerto = marco + losa). Σ W_i == G total.

    Marco: cada elemento (salvo brazos) aporta γ·A·L al nivel de su nodo
    superior. Losa: Q_G·área_planta se suma a cada nivel sobre la base (misma
    carga muerta de losa que entra en el caso G). Requiere el modelo construido.
    """
    w = {}
    for e, tipo in M.tipo.items():
        if tipo == 'brazo':
            continue
        A = M.area.get(e)
        if not A:
            continue
        n1, n2 = ops.eleNodes(e)
        z1 = ops.nodeCoord(n1)[2]
        z2 = ops.nodeCoord(n2)[2]
        k = _nivel_de_z(max(z1, z2))
        if k is None or k == 0:
            # nodo superior en la base: asigna al primer nivel sobre la base
            k = 1
        w[k] = w.get(k, 0.0) + cargas.GAMMA_HORMIGON * A * _long(n1, n2)
    # carga muerta de losa por nivel (mismos niveles que M.by_level, sin base)
    area = cargas.area_planta(M)
    for k in M.by_level:
        if k == 0:
            continue
        w[k] = w.get(k, 0.0) + cargas.Q_G_LOSA * area
    return dict(sorted(w.items()))


# ------------------------------------------------------- V basal y fuerzas F_i
def v_base_y_fuerzas(pesos, alpha=None):
    """(V_base, {k: F_k}) del patrón pseudoestático. z ABSOLUTO (como A)."""
    if alpha is None:
        alpha = D.ALPHA_EQ
    ks = sorted(pesos)
    w_tot = sum(pesos.values())
    denom = sum(pesos[k] * _Z_NIVEL[k] for k in ks)
    v_base = alpha * w_tot
    fuerzas = {k: v_base * pesos[k] * _Z_NIVEL[k] / denom for k in ks}
    return v_base, fuerzas


# ------------------------------------------------------------ patrón sísmico
def patron_sismico(dirn, M, pesos, ts_tag=2, pat_tag=2, alpha=None):
    """Aplica el patrón sísmico en X o Y. Devuelve (V_base, {k: F_k}).

    Reparte F_k entre los nodos esclavos del nivel k (M.by_level[k], que NO
    incluye al maestro del diafragma).
    """
    ops.timeSeries('Constant', ts_tag)
    ops.pattern('Plain', pat_tag, ts_tag)
    v_base, fuerzas = v_base_y_fuerzas(pesos, alpha=alpha)
    for k in sorted(pesos):
        esclavos = sorted(M.by_level.get(k, []))
        if not esclavos:
            continue
        frac = fuerzas[k] / len(esclavos)
        for nt in esclavos:
            if dirn == "X":
                ops.load(nt, frac, 0.0, 0.0, 0.0, 0.0, 0.0)
            else:
                ops.load(nt, 0.0, frac, 0.0, 0.0, 0.0, 0.0)
    return v_base, fuerzas


def momento_volcante(fuerzas):
    """Σ F_k · (z_k − z_base) [kN·m]."""
    z0 = _Z_NIVEL[0]
    return sum(f * (_Z_NIVEL[k] - z0) for k, f in fuerzas.items())
