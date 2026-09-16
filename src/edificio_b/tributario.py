# -*- coding: utf-8 -*-
"""
tributario.py — Áreas tributarias por viga del Edificio B.

La planta de B es irregular: sus vigas NO tejan paneles cerrados por sus 4
bordes (como sí ocurre en el Edificio A), así que la regla estricta de A deja
casi todos los paneles sin repartir. Aquí se usa la regla de cuartos con
FALLBACK, robusta para plantas irregulares:

  - Se arma la grilla del nivel con TODAS las coordenadas de extremos de viga
    (más el bounding box de la planta).
  - Cada panel reparte su carga total q·área EN PARTES IGUALES entre las vigas
    que existan en sus bordes (1 a 4). Si un panel no tiene ninguna viga de
    borde, su carga va a la viga más cercana que lo cruce.
  - Se acumula en TOTALES [kN] por viga (no en carga lineal), y al final la
    carga lineal es total/L. Así la conservación Σ(total) = q·área es exacta
    salvo el mínimo de planta sin viga alguna (esquinas fuera de la huella).

Devuelve, por cada viga (segmento) con nivel sobre la base, su área tributaria
y las cargas G (Q_G losa) y Q (SC del nivel) totales y lineales.

Unidades: m, kN.
"""
import openseespy.opensees as ops
from . import datos_edificio as D
from . import cargas as C


def _nivel_de_z(z):
    for k, (zz, _) in enumerate(D.NIVELES):
        if abs(zz - z) < 1e-3:
            return k
    return None


def _vigas_por_nivel(M):
    """Segmentos de viga horizontales por nivel: {k: {'x':[...], 'y':[...]}}.
    Cada segmento: [coord_fija, a, b, tag, ni, nj, L]."""
    VP = {}
    for e, tipo in M.tipo.items():
        if tipo != 'viga':
            continue
        n1, n2 = ops.eleNodes(e)
        x1, y1, z1 = ops.nodeCoord(n1)
        x2, y2, z2 = ops.nodeCoord(n2)
        if abs(z1 - z2) > 1e-6:
            continue
        k = _nivel_de_z(z1)
        if k is None or k == 0:
            continue
        d = VP.setdefault(k, {'x': [], 'y': []})
        if abs(y1 - y2) < 1e-6:      # corre en X
            a, b = min(x1, x2), max(x1, x2)
            d['x'].append([round(y1, 3), round(a, 3), round(b, 3), e,
                           n1, n2, round(b - a, 4)])
        elif abs(x1 - x2) < 1e-6:    # corre en Y
            a, b = min(y1, y2), max(y1, y2)
            d['y'].append([round(x1, 3), round(a, 3), round(b, 3), e,
                           n1, n2, round(b - a, 4)])
    return VP


def _cont(s, a, b):
    return s[1] <= a + 1e-6 and s[2] >= b - 1e-6


def _bbox(M):
    xs = [c[0] for c in M.coord.values()]
    ys = [c[1] for c in M.coord.values()]
    return (min(xs), max(xs)), (min(ys), max(ys))


def _areas_nivel(vx, vy, BBX, BBY):
    """Área tributaria [m²] por tag de viga en un nivel (regla cuartos+fallback)."""
    xs = sorted(set([BBX[0], BBX[1]] + [s[1] for s in vx] + [s[2] for s in vx]
                    + [s[0] for s in vy]))
    ys = sorted(set([BBY[0], BBY[1]] + [s[0] for s in vx] + [s[1] for s in vy]
                    + [s[2] for s in vy]))
    area = {}

    def add(tag, a):
        area[tag] = area.get(tag, 0.0) + a

    for i in range(len(xs) - 1):
        for j in range(len(ys) - 1):
            x0, x1 = xs[i], xs[i + 1]
            y0, y1 = ys[j], ys[j + 1]
            dx, dy = x1 - x0, y1 - y0
            a_panel = dx * dy
            edges = []
            for grp in ([s for s in vx if abs(s[0] - y0) < 1e-6 and _cont(s, x0, x1)],
                        [s for s in vx if abs(s[0] - y1) < 1e-6 and _cont(s, x0, x1)],
                        [s for s in vy if abs(s[0] - x0) < 1e-6 and _cont(s, y0, y1)],
                        [s for s in vy if abs(s[0] - x1) < 1e-6 and _cont(s, y0, y1)]):
                if grp:
                    edges.append(grp[0][3])
            if edges:
                for tag in edges:
                    add(tag, a_panel / len(edges))
            else:
                cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
                best, bd = None, 1e9
                for s in vx:
                    if s[1] - 1e-6 <= cx <= s[2] + 1e-6 and abs(s[0] - cy) < bd:
                        bd, best = abs(s[0] - cy), s[3]
                for s in vy:
                    if s[1] - 1e-6 <= cy <= s[2] + 1e-6 and abs(s[0] - cx) < bd:
                        bd, best = abs(s[0] - cx), s[3]
                if best is not None:
                    add(best, a_panel)
    return area


def tributaria_por_viga(M):
    """Lista de cargas por viga para el contrato/visualizador:
    {tag, tipo(vigas_x|vigas_y), nivel, ni, nj, L, area, qG, qQ, G, Q}.

    G usa Q_G (losa) uniforme; Q usa SC del nivel (piso vs cubierta)."""
    BBX, BBY = _bbox(M)
    z_cubierta = D.NIVELES[-1][0]
    VP = _vigas_por_nivel(M)
    seg = {}   # tag -> (tipo, nivel, ni, nj, L, z)
    for k, d in VP.items():
        for s in d['x']:
            seg[s[3]] = ('vigas_x', k, s[4], s[5], s[6])
        for s in d['y']:
            seg[s[3]] = ('vigas_y', k, s[4], s[5], s[6])

    vigas = []
    for k in sorted(VP):
        area = _areas_nivel(VP[k]['x'], VP[k]['y'], BBX, BBY)
        z = D.NIVELES[k][0]
        sc = C.SC_CUBIERTA if abs(z - z_cubierta) < 1e-6 else C.SC_PISO
        for tag, a in area.items():
            tipo, niv, ni, nj, L = seg[tag]
            if L <= 0:
                continue
            G = round(C.Q_G_LOSA * a, 3)
            Q = round(sc * a, 3)
            vigas.append({
                "tag": tag, "tipo": tipo, "nivel": niv, "ni": ni, "nj": nj,
                "L": round(L, 3), "area": round(a, 3),
                "qG": round(G / L, 4), "qQ": round(Q / L, 4),
                "G": G, "Q": Q,
            })
    return vigas


def resumen(M):
    """(area_total, G_total, Q_total) del reparto tributario, para verificación."""
    vigas = tributaria_por_viga(M)
    at = sum(v["area"] for v in vigas)
    g = sum(v["G"] for v in vigas)
    q = sum(v["Q"] for v in vigas)
    return at, g, q
