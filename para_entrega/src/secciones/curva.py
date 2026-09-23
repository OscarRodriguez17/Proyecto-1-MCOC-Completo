# -*- coding: utf-8 -*-
"""
curva.py — Momento-curvatura de una sección con el motor OpenSeesPy.

Procedimiento (igual al de referencia de la documentación de OpenSeesPy):

  1. nodos coincidentes (0,0); fijación (1,1,1) en el nodo 1 y (0,1,0) en el 2
     → solo quedan libres la axial (GDL 1) y la rotación Mz (GDL 3);
  2. elemento zeroLengthSection  con la sección de fibras;
  3. carga axial constante P (pattern Plain + Constant; LoadControl 0.0),
  4. momento de referencia unitario (Linear; DisplacementControl en el GDL 3)
     → cada paso impone una curvatura Δκ y el "time" es el momento M(κ) que la
     sección es capaz de sostener; κ = rotación del nodo 2 (elemento longitud 0);

El análisis termina por: (a) aplastamiento del hormigón extremo (εc ≤ εcu),
(b) no convergencia de Newton, o (c) agotamiento del número de incrementos.

Se validó contra contrastes analíticos:
  - EA y EI de una sección elástica 12×12 → exactos (<1e-4);
  - σ(ε) de Concrete01 medida por equilibrio → parábola teórica;
  - rigidez elástica del 70×70 armado → EI ≈ 1.47e5 kN·m² (estado AGRIETADO:
    hormigón sin tensión; correcto, frente a EI ≈ 5.9e5 bruto).
"""
from dataclasses import dataclass
import math

import openseespy.opensees as ops

from . import materiales as M

TAG_MAT_CONCRETO = 1
TAG_MAT_ACERO = 2
TAG_SECCION = 3


def _fibras_de(seccion, gy, gz):
    """Lista de (y, z, A, matTag) con la grilla de hormigón + barras."""
    fibras = []
    Acell = (seccion.h / gy) * (seccion.b / gz)
    dy, dz = seccion.h / gy, seccion.b / gz
    for i in range(gy):
        for j in range(gz):
            y = -seccion.h / 2.0 + (i + 0.5) * dy
            z = -seccion.b / 2.0 + (j + 0.5) * dz
            fibras.append((y, z, Acell, TAG_MAT_CONCRETO))
    for br in seccion.barras:
        fibras.append((br.y, br.z, math.pi * br.d ** 2 / 4.0, TAG_MAT_ACERO))
    return fibras


@dataclass
class Curva:
    seccion: object
    P: float
    kaps: list
    moms: list
    motivo: str            # 'crushing'|'su_steel'|'softening'|'no_convergencia'|'nincr'
    EI0: float
    Mmax: float
    k_max: float

    def pares(self, cada=1):
        return [(k, m) for k, m in zip(self.kaps[::cada], self.moms[::cada])]


def momento_curvatura(seccion, P, gy=12, gz=12, dk=1.0e-4, nincr=500,
                      tol_k=1.0e-8, eps_su=0.09, f_soft=0.85):
    """Calcula la curva M–φ a carga axial constante P (kN, compresión < 0).

    La deformación de la sección se lee del propio elemento
    (`deformation = [ε_axial, κ]`, verificado consistente con nodeDisp). El
    final de la curva lo marca el PROPIO estado del fibro-solver:

      - 'crushing'     : fibra de hormigón extrema ≤ εcu (aplastamiento);
      - 'su_steel'     : acero extremo ≥ εsu (límite de ductilidad);
      - 'softening'    : tras el pico, el momento cae por debajo de
                         f_soft·M_máximo (rama descendente consolidada).
    """
    ops.wipe()
    ops.model('basic', '-ndm', 2, '-ndf', 3)

    ops.uniaxialMaterial('Concrete01', TAG_MAT_CONCRETO,
                         seccion.fc, seccion.epc0, 0.0, seccion.epcu)
    ops.uniaxialMaterial('Steel01', TAG_MAT_ACERO,
                         seccion.fy, seccion.Es, seccion.bhard)

    ops.section('Fiber', TAG_SECCION)
    for (y, z, A, mat) in _fibras_de(seccion, gy, gz):
        ops.fiber(y, z, A, mat)

    ops.node(1, 0.0, 0.0)
    ops.node(2, 0.0, 0.0)
    ops.fix(1, 1, 1, 1)
    ops.fix(2, 0, 1, 0)
    ops.element('zeroLengthSection', 1, 1, 2, TAG_SECCION)

    # ---- carga axial constante -----------------------------------------
    ops.timeSeries('Constant', 1)
    ops.pattern('Plain', 1, 1)
    ops.load(2, P, 0.0, 0.0)
    ops.system('SparseGeneral', '-piv')
    ops.test('NormUnbalance', tol_k, 40)
    ops.numberer('Plain')
    ops.constraints('Plain')
    ops.algorithm('Newton')
    ops.integrator('LoadControl', 1.0)
    ops.analysis('Static')
    ok = _aplicar_axial()
    if ok != 0:
        raise RuntimeError(f"Paso axial no convergió (P={P} kN).")

    # ---- posición de las fibras extremas (para el criterio de falla) ----
    y_conc_ext = -seccion.h / 2.0 + 0.5 * (seccion.h / gy)   # fila extrema
    y_steel_ext = max(br.y for br in seccion.barras)

    # ---- momento unitario + control por curvatura -----------------------
    ops.timeSeries('Linear', 2)
    ops.pattern('Plain', 2, 2)
    ops.load(2, 0.0, 0.0, 1.0)
    ops.integrator('DisplacementControl', 2, 3, dk, 1, dk, dk)

    kaps, moms = [], []
    motivo = 'nincr'
    rmax, rmax_k = None, 0.0
    for _ in range(nincr):
        ok = ops.analyze(1)
        if ok != 0:
            motivo = 'no_convergencia'
            break
        kap = ops.nodeDisp(2, 3)
        mom = ops.getTime()
        kaps.append(kap)
        moms.append(mom)
        if rmax is None or mom > rmax:
            rmax, rmax_k = mom, kap
        d = ops.eleResponse(1, 'section', 1, 'deformation')
        e0 = d[0] if (d and len(d) > 0) else 0.0
        if e0 + kap * y_conc_ext <= seccion.epcu + 1e-6:
            motivo = 'crushing'
            break
        if e0 + kap * y_steel_ext >= eps_su:
            motivo = 'su_steel'
            break
        if mom < f_soft * rmax and kap > rmax_k:
            motivo = 'softening'
            break

    if not kaps:
        raise RuntimeError(f"Sin puntos de M-φ (P={P} kN).")

    kaps, moms = list(kaps), list(moms)
    Mmax = max(moms)
    k_max = kaps[moms.index(Mmax)]
    EI0 = _ei0(kaps, moms, Mmax)
    return Curva(seccion, P, kaps, moms, motivo, EI0, Mmax, k_max)


def _aplicar_axial():
    """Aplica la carga axial en sub-incrementos robustos (Newton). El análisis
    ya se creó con `LoadControl(1.0)`; aquí solo se rifina el paso si falla."""
    for nsub in (1, 2, 4, 8):
        ops.integrator('LoadControl', 1.0 / nsub)
        ok = 0
        for _ in range(nsub):
            ok = ops.analyze(1)
            if ok != 0:
                break
        if ok == 0:
            ops.integrator('LoadControl', 0.0)
            return 0
    return -1


def _ei0(kaps, moms, Mmax):
    """Pendiente M/κ del tramo ELÁSTICO inicial de la curva: regresión por el
    origen sobre los puntos con κ ≤ 0.20·κ_max y M ≤ 0.5·Mmax."""
    k_end = 0.2 * max(kaps)
    pts = [(k, m) for k, m in zip(kaps, moms) if k <= k_end and m <= 0.5 * Mmax]
    if len(pts) < 3:
        pts = [(k, m) for k, m in zip(kaps, moms)][:min(8, len(kaps))]
    a = sum(k * m for k, m in pts) / sum(k * k for k, m in pts)
    return a