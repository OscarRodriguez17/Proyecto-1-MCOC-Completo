# -*- coding: utf-8 -*-
"""analitica.py — Integrador de fibras 100% Python (contraste y criterios).

El motor OpenSees resuelve el equilibrio real de la sección (curva M-φ). Este
integrador resuelve el MISMO problema con las leyes de materiales de
materiales.py sobre la misma discretización de fibras, y sirve para:

  1. VALIDAR al motor: las curvas M(κ) deben coincidir antes de la fluencia
     (mismo EI0 agrietado, mismos momentos);
  2. fijar el CRITERIO DE FALLA de la curva: el ultimo punto de carga es el
     κ donde la fibra de hormigón extrema alcanza εcu (aplastamiento); con
     Steel01 (b = 0.01) el hormigón es el material que agota la ductilidad;
  3. calcular el punto BALANCEADO (ACI): fibra extrema comprimida en εcu y
     acero extremo traccionado en εy.

El perfil de deformaciones es lineal: ε(y) = ε0 + κ·y, con ε0 el "desplazamiento
axial" del plano de la sección (resuelto por bisection con N = P) y κ la
curvatura impuesta.
"""
import math

from . import materiales as M


def _integral_grid(seccion, eps0, k, gy=12, gz=12):
    dy, dz = seccion.h / gy, seccion.b / gz
    Acell = dy * dz
    N = Mz = 0.0
    for i in range(gy):
        y = -seccion.h / 2.0 + (i + 0.5) * dy
        eps = eps0 + k * y
        s = M.sig_concreto(eps, seccion.fc, seccion.epc0, seccion.epcu)
        N += s * Acell * gz
        Mz += s * Acell * y * gz
    for br in seccion.barras:
        eps = eps0 + k * br.y
        s = M.sig_acer(eps, seccion.fy, seccion.Es, seccion.bhard)
        Ab = math.pi * br.d ** 2 / 4.0
        N += s * Ab
        Mz += s * Ab * br.y
    return N, Mz


def resolver_eps0(seccion, P, k, gy=12, gz=12):
    """ε0 tal que N(ε0,κ) = P (compresión < 0, igual que en el motor)."""
    a, b = -0.02, 0.05
    fa = _integral_grid(seccion, a, k, gy, gz)[0] - P
    for _ in range(2):
        fb = _integral_grid(seccion, b, k, gy, gz)[0] - P
        if fa * fb < 0.0:
            break
        a *= 2.0; b *= 2.0
    for _ in range(120):
        m = 0.5 * (a + b)
        fm = _integral_grid(seccion, m, k, gy, gz)[0] - P
        if fm == 0.0 or (b - a) < 1e-10:
            return m
        if fa * fm < 0.0:
            b = m
        else:
            a = m
            fa = fm
    return 0.5 * (a + b)


def momento_teorico(seccion, P, k, gy=12, gz=12):
    """M(κ) analítico a P constante (validación del motor OpenSees)."""
    eps0 = resolver_eps0(seccion, P, k, gy, gz)
    return _integral_grid(seccion, eps0, k, gy, gz)[1]


def EI_cracked(seccion, gy=12, gz=12):
    """EI agrietado inicial: M/κ → 0 con N = 0 (curva P = 0)."""
    k = 1e-6
    eps0 = resolver_eps0(seccion, 0.0, k, gy, gz)
    M = _integral_grid(seccion, eps0, k, gy, gz)[1]
    return M / k


def kappa_ultimo(seccion, P, gy=12, gz=12, tol=1e-6):
    """κ donde la fibra de hormigón extrema alcanza εcu (aplastamiento).

    Recorre κ crecientes; por cada κ resuelve ε0 y comprueba la deformación
    mínima de la malla de hormigón.
    """
    dy = seccion.h / gy
    y_ext = -seccion.h / 2.0 + 0.5 * dy      # centro de la fila extrema
    k = 0.0
    dk = 2.0e-5
    k_final = 0.5
    while k < k_final:
        k += dk
        eps0 = resolver_eps0(seccion, P, k, gy, gz)
        eps_ext = eps0 + k * y_ext
        if eps_ext <= seccion.epcu + tol:
            return k
    return k_final


def balanceado(seccion, gy=12, gz=12):
    """(P_b, M_b): fibra extrema comprimida en εcu y acero extremo en εy."""
    y_ext_acero = max(br.y for br in seccion.barras)
    kb = (seccion.ey() - seccion.epcu) / (y_ext_acero + seccion.h / 2.0)
    eps0 = seccion.epcu + kb * (seccion.h / 2.0)
    N, M = _integral_grid(seccion, eps0, kb, gy, gz)
    return -N, M