# -*- coding: utf-8 -*-
"""
construir.py — Ensambla el modelo 3D lineal-elástico del Edificio B en OpenSeesPy.

Modelo:
  - Pórtico 3D, 6 GDL/nodo, material lineal-elástico (H30).
  - Pilares 70x70 y MUROS como columna-ancha equivalente (sección real e x L).
  - Vigas por nivel según planta tipo (subdivididas en la grilla).
  - Diafragma rígido por piso (ops.rigidDiaphragm, constraints Transformation).
  - Empotramiento perfecto en la base (z = -4.01). Fundaciones ignoradas.

Ejes locales explícitos (vecxz no colineal al elemento):
  transf 1 (verticales)  vecxz=(1,0,0)
  transf 2 (vigas X)     vecxz=(0,0,1)
  transf 3 (vigas Y)     vecxz=(0,0,1)
"""
import math
import openseespy.opensees as ops
from . import datos_edificio as D


# ---------------------------------------------------------------- utilidades
def _clusters(vals, tol, cap=None):
    """Agrupa valores cercanos (<tol) y devuelve (lista_canonica, funcion_snap).
    snap() ajusta un valor al eje canónico más cercano si está dentro de `cap`."""
    if cap is None:
        cap = tol * 4
    us = []
    for v in sorted(vals):
        if not us or v - us[-1] > tol:
            us.append(v)
    def snap(v):
        b = min(us, key=lambda u: abs(u - v))
        return b if abs(b - v) <= cap else v
    return us, snap


def _props(b, h):
    """A, Iy, Iz, J de sección rectangular b(x) x h(y)."""
    A = b * h
    Iz = b * h ** 3 / 12.0        # flexión en plano local x-y (canto h)
    Iy = h * b ** 3 / 12.0        # flexión en plano local x-z (ancho b)
    a, c = max(b, h), min(b, h)
    J = a * c ** 3 * (1.0 / 3.0 - 0.21 * (c / a) * (1.0 - (c / a) ** 4 / 12.0))
    return A, Iy, Iz, J


class Modelo:
    def __init__(self):
        self.nodes = {}      # (x,y,k) -> nid
        self.coord = {}      # nid -> (x,y,z)
        self.nid = 0
        self.eid = 0
        self.by_level = {}   # k -> set(nid)
        self.base_nodes = []
        self.tipo = {}       # eid -> 'pilar'|'muro'|'viga'
        self.area = {}       # eid -> A [m²]
        self.masters = {}    # k -> master nid

    def node(self, x, y, k, z):
        key = (round(x, 3), round(y, 3), k)
        if key in self.nodes:
            return self.nodes[key]
        self.nid += 1
        ops.node(self.nid, float(x), float(y), float(z))
        self.nodes[key] = self.nid
        self.coord[self.nid] = (x, y, z)
        self.by_level.setdefault(k, set()).add(self.nid)
        return self.nid

    def elem(self, n1, n2, b, h, transf, tipo):
        A, Iy, Iz, J = _props(b, h)
        self.eid += 1
        ops.element('elasticBeamColumn', self.eid, n1, n2,
                    A, D.E_CONCRETO, D.G_CONCRETO, J, Iy, Iz, transf)
        self.tipo[self.eid] = tipo
        self.area[self.eid] = A
        return self.eid

    def rigid_arm(self, n1, n2):
        """Brazo rígido (elemento muy rígido) para enganchar muro <-> marco."""
        if n1 == n2:
            return
        self.eid += 1
        Ab, Ib, Jb = 5.0, 5.0, 10.0          # sección ficticia muy rígida
        ops.element('elasticBeamColumn', self.eid, n1, n2,
                    Ab, D.E_CONCRETO, D.G_CONCRETO, Jb, Ib, Ib, 4)
        self.tipo[self.eid] = 'brazo'
        self.area[self.eid] = 0.0            # sin peso propio
        return self.eid


def build_model(verbose=False, diafragma=True,
                incluir_bloque_sup=True, brazos_rigidos=True):
    ops.wipe()
    ops.model('basic', '-ndm', 3, '-ndf', 6)
    ops.geomTransf('Linear', 1, 1.0, 0.0, 0.0)   # verticales
    ops.geomTransf('Linear', 2, 0.0, 0.0, 1.0)   # vigas X
    ops.geomTransf('Linear', 3, 0.0, 0.0, 1.0)   # vigas Y
    ops.geomTransf('Linear', 4, 0.0, 0.0, 1.0)   # brazos rígidos (horizontales)

    M = Modelo()
    niv = D.NIVELES
    zk = {k: niv[k][0] for k in range(len(niv))}     # k -> z
    kfloor = {round(z, 3): k for k, (z, _) in enumerate(niv)}

    # --- grilla canónica de EJES: solo pilares + vigas (líneas limpias).
    #     Los muros y los extremos de viga se AJUSTAN luego a esta grilla,
    #     de modo que los pilares nunca se desplazan. ---
    xs = [b[0] for b in D.BEAMS_V] + [p[0] for p in D.PILARES]
    ys = [b[0] for b in D.BEAMS_H] + [p[1] for p in D.PILARES]
    _, sx = _clusters(xs, 0.15, cap=D.GRID_TOL + 0.10)
    _, sy = _clusters(ys, 0.15, cap=D.GRID_TOL + 0.10)

    muros_v = list(D.MUROS_V)
    muros_h = list(D.MUROS_H)
    if incluir_bloque_sup:
        muros_v += D.MUROS_BLOQUE_SUP_V     # bloque escalera/ascensor (e=0.20)
        muros_h += D.MUROS_BLOQUE_SUP_H

    verticales = []  # (x, y, b, h, tipo)  columnas y muros como columna-ancha
    muros_info = []  # (xc, yc, 'V'/'H', c0, c1)  para brazos rígidos
    for (x, y) in D.PILARES:                    # pilares: sí a la grilla canónica
        verticales.append((sx(x), sy(y), D.SEC_PILAR[0], D.SEC_PILAR[1], 'pilar'))
    for (x, y0, y1, e) in muros_v:              # muros: posición REAL del DXF (sin snap)
        xc, yc = x, (y0 + y1) / 2
        verticales.append((xc, yc, e, (y1 - y0), 'muro'))
        muros_info.append((xc, yc, 'V', y0, y1))
    for (y, x0, x1, e) in muros_h:
        xc, yc = (x0 + x1) / 2, y
        verticales.append((xc, yc, (x1 - x0), e, 'muro'))
        muros_info.append((xc, yc, 'H', x0, x1))

    # --- nodos + columnas/muros verticales (base -> cubierta) ---
    for (x, y, b, h, tipo) in verticales:
        prev = None
        for k in range(len(niv)):
            n = M.node(x, y, k, zk[k])
            if k == 0:
                M.base_nodes.append(n)
            if prev is not None:
                M.elem(prev, n, b, h, 1, tipo)
            prev = n

    # --- vigas por nivel (subdividiendo en la grilla) ---
    g = D.GRID_TOL
    def cut_coords_V(xf, y0, y1):
        ya, yb_ = sy(y0), sy(y1)
        cuts = {ya, yb_}
        for (yb, x0, x1, w) in D.BEAMS_H:
            if ya - g <= sy(yb) <= yb_ + g and sx(x0) - g <= xf <= sx(x1) + g:
                cuts.add(sy(yb))
        for (x, y, b, h, t) in verticales:      # nodos de pilares/muros en la línea
            if abs(x - xf) <= g and ya - g <= y <= yb_ + g:
                cuts.add(y)
        return sorted(cuts)

    def cut_coords_H(yf, x0, x1):
        xa, xb_ = sx(x0), sx(x1)
        cuts = {xa, xb_}
        for (xb, yy0, yy1, w) in D.BEAMS_V:
            if xa - g <= sx(xb) <= xb_ + g and sy(yy0) - g <= yf <= sy(yy1) + g:
                cuts.add(sx(xb))
        for (x, y, b, h, t) in verticales:
            if abs(y - yf) <= g and xa - g <= x <= xb_ + g:
                cuts.add(x)
        return sorted(cuts)

    for zf in D.NIVELES_CON_VIGAS:
        k = kfloor[round(zf, 3)]
        z = zk[k]
        for (x, y0, y1, w) in D.BEAMS_V:
            xf = sx(x)
            b, h = D.seccion_viga(w)
            ys_cut = cut_coords_V(xf, y0, y1)
            for ya, yb in zip(ys_cut[:-1], ys_cut[1:]):
                if yb - ya < D.TOL:
                    continue
                n1 = M.node(xf, ya, k, z)
                n2 = M.node(xf, yb, k, z)
                M.elem(n1, n2, b, h, 3, 'viga')
        for (y, x0, x1, w) in D.BEAMS_H:
            yf = sy(y)
            b, h = D.seccion_viga(w)
            xs_cut = cut_coords_H(yf, x0, x1)
            for xa, xb in zip(xs_cut[:-1], xs_cut[1:]):
                if xb - xa < D.TOL:
                    continue
                n1 = M.node(xa, yf, k, z)
                n2 = M.node(xb, yf, k, z)
                M.elem(n1, n2, b, h, 2, 'viga')

    # --- brazos rígidos: enganchan cada muro al marco de su eje y componen
    #     los núcleos conectados (L/C). Método columna-ancha + rigid arm. ---
    if brazos_rigidos:
        import math as _m
        for k in range(1, len(niv)):
            fn = {(round(M.coord[n][0], 2), round(M.coord[n][1], 2)): n
                  for n in M.by_level.get(k, [])}
            def _dist(a, b):
                return _m.hypot(M.coord[a][0] - M.coord[b][0],
                                M.coord[a][1] - M.coord[b][1])
            # muro -> nodos de viga sobre su eje más cercano
            for (xc, yc, axis, c0, c1) in muros_info:
                w = M.nodes.get((round(xc, 3), round(yc, 3), k))
                if w is None:
                    continue
                if axis == 'V':
                    xg = sx(xc)
                    tgt = [(round(xg, 2), round(sy(yb), 2))
                           for (yb, x0, x1, ww) in D.BEAMS_H if c0 - 0.3 <= sy(yb) <= c1 + 0.3]
                else:
                    yg = sy(yc)
                    tgt = [(round(sx(xb), 2), round(yg, 2))
                           for (xb, y0, y1, ww) in D.BEAMS_V if c0 - 0.3 <= sx(xb) <= c1 + 0.3]
                for key in tgt:
                    tn = fn.get(key)
                    if tn is not None and 0.05 < _dist(w, tn) < 4.0:
                        M.rigid_arm(w, tn)
            # muro -> muro cercano (compone L/C)
            wall_nodes = [M.nodes.get((round(xc, 3), round(yc, 3), k))
                          for (xc, yc, *_) in muros_info]
            wall_nodes = [n for n in wall_nodes if n is not None]
            for i in range(len(wall_nodes)):
                for j in range(i + 1, len(wall_nodes)):
                    if 0.05 < _dist(wall_nodes[i], wall_nodes[j]) < 2.2:
                        M.rigid_arm(wall_nodes[i], wall_nodes[j])

    # --- apoyos: empotramiento en la base ---
    for n in M.base_nodes:
        ops.fix(n, 1, 1, 1, 1, 1, 1)

    # --- diafragma rígido por piso (perpDirn=3) ---
    for k in (range(1, len(niv)) if diafragma else []):
        slaves = sorted(M.by_level.get(k, []))
        if len(slaves) < 2:
            continue
        cx = sum(M.coord[n][0] for n in slaves) / len(slaves)
        cy = sum(M.coord[n][1] for n in slaves) / len(slaves)
        M.nid += 1
        master = M.nid
        ops.node(master, cx, cy, zk[k])
        ops.fix(master, 0, 0, 1, 1, 1, 0)     # libera UX,UY,RZ (GDL de diafragma)
        M.masters[k] = master
        ops.rigidDiaphragm(3, master, *slaves)

    if verbose:
        npil = sum(1 for t in M.tipo.values() if t == 'pilar')
        nmur = sum(1 for t in M.tipo.values() if t == 'muro')
        nvig = sum(1 for t in M.tipo.values() if t == 'viga')
        nbra = sum(1 for t in M.tipo.values() if t == 'brazo')
        print(f"Nodos: {len(M.coord)} (+{len(M.masters)} masters)  "
              f"Elementos: {M.eid}  [pilares {npil}, muros {nmur}, "
              f"vigas {nvig}, brazos {nbra}]")
    return M


if __name__ == '__main__':
    M = build_model(verbose=True)
