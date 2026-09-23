# -*- coding: utf-8 -*-
"""
seccion.py — Definición de secciones de hormigón armado (modelo de fibras 2D).

Una sección se describe por su hormigón (base b × peralte h) y la lista de
barras (y, z, diámetro). El plano de la sección es (y, z): la curvatura Mz
flexa variando la deformación a lo largo de y (peralte), y z es el ancho
(out-of-plane).

El motor monta las fibras en OpenSeesPy como fibras EXPLÍCITAS:
  - grilla gy×gz de fibras de hormigón (Concrete01) al centro de cada celda;
  - una fibra por cada barra (Steel01) con área de la barra.
(En esta build de openseespy 3.8.0 el comando patch() 'rect' presentó un
comportamiento anómalo (EA medido 93% mayor que el teórico en una celda 1×1);
se documenta en la bitácora y la razón por la que se usan fibras explícitas,
que verifican ANALÍTICAMENTE: EA y EI exactos a <1e-4.)

Discretizaciones prescritas por el enunciado: 6×6, 12×12 y 20×20.
"""
from dataclasses import dataclass, field
import math

from . import materiales as M


@dataclass(frozen=True)
class Barra:
    y: float       # posición a lo largo del peralte (m)
    z: float       # posición a lo largo del ancho (m)
    d: float       # diámetro (m)


@dataclass
class Seccion:
    nombre: str
    b: float
    h: float
    barras: list = field(default_factory=list)
    fc: float = M.FC
    epc0: float = M.EPSC0
    epcu: float = M.EPSCU
    fy: float = M.FY
    Es: float = M.ES
    bhard: float = M.BHARD

    # ------------------------------------------------------------------
    @property
    def Ag(self):
        return self.b * self.h

    @property
    def As(self):
        return sum(math.pi * br.d ** 2 / 4.0 for br in self.barras)

    @property
    def As_por_cara(self):
        """As total agrupado por signo de y (caras del peralte)."""
        pos = sum(a for br in self.barras for a in (math.pi * br.d ** 2 / 4.0,) if br.y > 0)
        neg = sum(a for br in self.barras for a in (math.pi * br.d ** 2 / 4.0,) if br.y < 0)
        return {"cara_pos": pos, "cara_neg": neg}

    def p0_aci(self):
        return M.p0_nominal_aci(self.b, self.h, self.As, self.fc, self.fy)

    def p0_fibra(self):
        return M.p0_fibra_max(self.b, self.h, self.As, self.fc, self.fy,
                              self.epc0, self.epcu, self.Es, self.bhard)

    def ey(self):
        return self.fy / self.Es

    def __repr__(self):
        return (f"Seccion({self.nombre}, {self.b:.3f}x{self.h:.3f}, "
                f"As={self.As:.6f} m2)")


# ---------------------------------------------------------------------------
# Constructores de secciones del proyecto (semana03)
# ---------------------------------------------------------------------------
def columna_70x70(d=0.028, recub=0.05, n_por_cara=4, fc=M.FC,
                 nombre="col0.70x0.70"):
    """Columna 70×70 del Edificio B con 8φ28 (4 por cara). d' = 0.05 m.

    Recubrimiento del eje de la barra → y = ±(h/2 − recub) = ±0.30 m.
    La geometría adoptada reproduce los VALORES DE ACEPTACIÓN del enunciado:
      P0 ≈ 12 377 kN  (0.85·f'c·(Ag−As) + fy·As, se obtiene EXACTO por
                       construirse Así_total = 0.00492726 m²);
      balanceado ≈ (4 500 kN, 1 395 kN·m).
    """
    yb = 0.5 * 0.70 - recub
    zb = 0.5 * 0.70 - recub
    barras = []
    for sgn in (-1.0, 1.0):
        for i in range(n_por_cara):
            z = -zb + i * (2.0 * zb) / (n_por_cara - 1) if n_por_cara > 1 else 0.0
            barras.append(Barra(sgn * yb, z, d))
    return Seccion(nombre, 0.70, 0.70, barras, fc=fc)


def columna_A_70x70():
    """Columna 70×70 del Edificio A (8φ28 según capa RLE-PILAR, plano 101).

    Misma geometría que la del Edificio B, pero con el f'c ADOPTADO del
    Edificio A (G35): fc = 30 MPa (materiales.FC_A), fy = 420 MPa.
    """
    return columna_70x70(fc=M.FC_A, nombre="col_A_0.70x0.70")


def muro_b1(d_borde=0.016, d_alma=0.012, recub=0.05, sb=0.20):
    """Muro B MUROS_V[0] (x=11.20, y0=9.84, y1=12.75, e=0.60): 2.91×0.60.

    ARMADO ADOPTADO (documentado en el reporte, hipótesis de proyecto):
      - zonas de borde: 0.40 m en cada extremo con 4φ16 c/u (2 a cada cara);
      - alma: φ12 @ 0.20 m a CADA cara (una capa por cara);
      - recubrimiento al eje: 0.05 m.
    As total ≈ 0.00455 m² → P0 ACI ≈ 38 770 kN (coherente con muro H30).
    """
    L = 12.75 - 9.84                     # 2.91 m  (peralte en y → flexión en su plano)
    tb = 0.60                            # espesor (z)
    zb = 0.5 * tb - recub
    barras = []
    # zonas de borde (y = ±(L/2 − 0.20) .. ±L/2)
    y_e = 0.5 * L
    for sgn in (-1.0, 1.0):
        for zz in (zb, -zb):
            barras.append(Barra(sgn * (y_e - 0.10), zz, d_borde))
            barras.append(Barra(sgn * (y_e - 0.30), zz, d_borde))
    # alma: φ12 @ sb a cada cara
    return muro_generico(tb, L, nombre="muro0.60x2.91")


def muro_generico(tb, L, nombre=None, d_borde=0.016, d_alma=0.012, recub=0.05,
                  sb=0.20):
    """Constructor GENERICO de muro para el catalogo P-M (Semana 03).

    Aplica a cualquier muro del Edificio B (MUROS_V/H + bloque superior e=0.20)
    la MISMA hipotesis de armado que el muro representativo `muro_b1`:

      - zonas de borde: 0.40 m en cada extremo con 4φ16 c/u (2 a cada cara);
      - alma: φ12 @ 0.20 m a CADA cara (una capa por cara);
      - recubrimiento al eje: 0.05 m.

    Para el muro 0.60x2.91 reproduce EXACTO el armado de `muro_b1`
    (mismo n.o de barras y posiciones), por lo que As y P0 coinciden.

    Convencion: tb = espesor (z, fuera del plano); L = largo del muro en su
    plano (y). Curvatura Mz flexiona a lo largo de y.
    """
    if nombre is None:
        nombre = f"muro{tb:.2f}x{L:.2f}"
    zb = 0.5 * tb - recub
    barras = []
    # zonas de borde (y = ±(L/2 − 0.10) y ±(L/2 − 0.30))
    y_e = 0.5 * L
    for sgn in (-1.0, 1.0):
        for zz in (zb, -zb):
            barras.append(Barra(sgn * (y_e - 0.10), zz, d_borde))
            barras.append(Barra(sgn * (y_e - 0.30), zz, d_borde))
    # alma: φ12 @ sb a cada cara (mismo barrido que muro_b1)
    y4 = y_e - 0.40
    n = int(math.floor((2.0 * y4) / sb)) + 1
    for i in range(n):
        y = -y4 + i * sb
        if abs(y) <= y4 - 1e-9:
            for zz in (zb, -zb):
                barras.append(Barra(y, zz, d_alma))
    return Seccion(nombre, tb, L, barras)


def muro(distintas=True):
    """Catalogo de las secciones de muro DISTINTAS del Edificio B.

    Deduplica los muros de `datos_edificio` (MUROS_V + MUROS_H + bloque
    superior) por (espesor, largo) y los etiqueta con el mismo nombre
    `muro{e:.2f}x{L:.2f}` que usa `per_elemento` del cache, de modo que el
    visor puede asociar cada elemento a su propia curva P-M.
    """
    from edificio_b import datos_edificio as D
    seen = {}
    for (x, y0, y1, e) in D.MUROS_V:
        seen[(e, round(y1 - y0, 4))] = f"muro{e:.2f}x{y1 - y0:.2f}"
    for (y, x0, x1, e) in D.MUROS_H:
        seen[(e, round(x1 - x0, 4))] = f"muro{e:.2f}x{x1 - x0:.2f}"
    for (x, y0, y1, e) in D.MUROS_BLOQUE_SUP_V:
        seen[(e, round(y1 - y0, 4))] = f"muro{e:.2f}x{y1 - y0:.2f}"
    for (y, x0, x1, e) in D.MUROS_BLOQUE_SUP_H:
        seen[(e, round(x1 - x0, 4))] = f"muro{e:.2f}x{x1 - x0:.2f}"
    out = []
    for (e, L), nombre in seen.items():
        out.append(muro_generico(e, L, nombre=nombre))
    out.sort(key=lambda s: (s.b, s.h))
    return out


# ---------------------------------------------------------------------------
# Secciones del Edificio A (ADITIVO Semana 03 + A). f'c = 30 MPa (G35).
# ARMADO DE MUROS ADOPTADO POR SUPUESTO (confirmado: sin armadura en planos).
# Se reutiliza la disposición de `muro_generico` (B): alma distribuida a cada
# cara + zonas de borde, con estos valores (ver reports/):
#   - f'c = 30 MPa (G35)  ·  fy = 420 MPa  ·  recubrimiento 3 cm;
#   - alma: φ10 @ 0.20 m a CADA cara (doble malla, ρ_v ≈ 0.003);
#   - borde: 6φ16 por extremo (3 a cada cara) sobre una longitud de borde
#            Lb = max(2·t, 0.15·L), barras a 0.10/0.20/0.30 m del borde.
# ---------------------------------------------------------------------------
def muro_A_generico(tb, L, nombre=None, d_borde=0.016, d_alma=0.010,
                    recub=0.03, fc=M.FC_A, sb=0.20):
    """Constructor GENERICO de muro del Edificio A para el catalogo P-M.

    Convencion igual que `muro_generico`: tb = espesor (z, fuera del plano);
    L = largo del muro en su plano (y). Curvatura Mz flexiona a lo largo de y.
    """
    if nombre is None:
        nombre = f"muro_A_{tb:.2f}x{L:.2f}"
    zb = 0.5 * tb - recub
    y_e = 0.5 * L
    Lb = max(2.0 * tb, 0.15 * L)          # longitud de la zona de borde
    barras = []
    # zonas de borde: 6φ16 por extremo (3 a cada cara)
    for sgn in (-1.0, 1.0):
        for zz in (zb, -zb):
            for off in (0.10, 0.20, 0.30):
                barras.append(Barra(sgn * (y_e - off), zz, d_borde))
    # alma: φ10 @ sb a CADA cara (doble malla), fuera de los bordes
    yb = y_e - Lb
    n = int(math.floor((2.0 * yb) / sb)) + 1
    for i in range(n):
        y = -yb + i * sb
        if abs(y) <= yb - 1e-9:
            for zz in (zb, -zb):
                barras.append(Barra(y, zz, d_alma))
    return Seccion(nombre, tb, L, barras, fc=fc)


def muros_A():
    """Catalogo de las secciones de muro DISTINTAS del Edificio A.

    Deduplica los muros de `benchmark_3d.datos_edificio.WALLS` por
    (espesor, largo) y los etiqueta `muro_A_{e:.2f}x{L:.2f}`, el mismo nombre
    que usa `per_elemento` del cache para asociar cada elemento a su curva.
    """
    import os
    import sys
    AQUI = os.path.dirname(os.path.abspath(__file__))
    B3 = os.path.abspath(os.path.join(AQUI, "..", "benchmark_3d"))
    if B3 not in sys.path:
        sys.path.insert(0, B3)
    import datos_edificio as D
    seen = {}
    for w in D.WALLS:
        _xc, _yc, L, e_, _r = D.wall_geometry(w)
        seen[(e_, round(L, 4))] = f"muro_A_{e_:.2f}x{L:.2f}"
    out = []
    for (e_, L), nombre in seen.items():
        out.append(muro_A_generico(e_, L, nombre=nombre))
    out.sort(key=lambda s: (s.b, s.h))
    return out