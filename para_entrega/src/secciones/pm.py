# -*- coding: utf-8 -*-
"""
pm.py — Envolvente P–M (interacción) de una sección.

Procedimiento:
  - la curva M(κ) se calcula a carga axial constante (curva.momento_curvatura);
  - como Steel01 endurece indefinidamente (b = 0.01), el punto de agotamiento
    de cada curva lo fija el APLASTAMIENTO del hormigón: κ donde la fibra
    extrema alcanza εcu (analitica.kappa_ultimo). La curva del motor se trunca
    ahí (motivo 'cap') y el pico M se toma dentro de ese intervalo;
  - la envolvente se obtiene barriendo P desde tracción pura (acero fluyendo,
    hormigón sin tensión: P = −fy·As) hasta compresión próxima a P0;
  - el punto BALANCEADO (ACI) se integra directamente sobre las fibras con el
    perfil lineal de deformaciones cuya fibra extrema comprimida vale εcu y el
    acero extremo traccionado vale εy.
"""
from dataclasses import dataclass

import numpy as np

from . import analitica
from .curva import momento_curvatura


@dataclass
class Envolvente:
    seccion: object
    PS: list            # carga axial de cada punto (kN)
    Ms: list            # momento máximo asociado (kN·m)
    EI0s: list          # rigidez agrietada por punto
    Kmaxs: list         # curvatura de agotamiento por punto
    motivos: list
    P_bal: float
    M_bal: float


def balanceado(seccion, gy=12, gz=12):
    """Punto balanceado (ACI) sobre las fibras, (P_b, M_b) compresión > 0."""
    return analitica.balanceado(seccion, gy=gy, gz=gz)


def curvatura_agrietada(seccion):
    """El EI0 del motor debe coincidir con la estimación agrietada de fibras
    (integrador analítico, N = 0)."""
    return analitica.EI_cracked(seccion)


def curva_PM(seccion, n_puntos=17, gy=12, gz=12, dk=1.0e-4, nincr=400, P0=None):
    """Envolvente P–M completa (pico del M–φ). El final de cada curva lo da el
    propio motor (aplastamiento del hormigón o εsu del acero).

    Convención de reporte: P POSITIVO en compresión (igual que `balanceado` y
    `demandas`). El motor, en cambio, comprime con P<0, de modo que el barrido
    se hace en la convención del motor y se niega al reportar. P0: máximo del
    barrido; por defecto P0 ACI de la sección."""
    P0 = P0 if P0 is not None else seccion.p0_aci()
    P_traccion = seccion.fy * seccion.As          # tracción pura (P>0 = trac.)
    PS_raw = list(np.linspace(1.03 * P_traccion, -1.03 * P0, n_puntos))
    Ms, EI0s, Kmaxs, motivos = [], [], [], []
    for P in PS_raw:
        try:
            c = momento_curvatura(seccion, P, gy=gy, gz=gz, dk=dk, nincr=nincr)
            Ms.append(c.Mmax)
            EI0s.append(c.EI0)
            Kmaxs.append(c.k_max)
            motivos.append(c.motivo)
        except Exception:
            Ms.append(0.0)
            EI0s.append(0.0)
            Kmaxs.append(0.0)
            motivos.append("no_aplica")
    PS = [-p for p in PS_raw]                     # compresión POSITIVA
    Pb, Mb = balanceado(seccion, gy=gy, gz=gz)
    return Envolvente(seccion, PS, Ms, EI0s, Kmaxs, motivos, Pb, Mb)