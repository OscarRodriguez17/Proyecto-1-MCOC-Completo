# -*- coding: utf-8 -*-
"""
materiales.py — Leyes constitutivas de los materiales de las secciones RC.

Unidades SI (ver AGENTS.md): longitud [m], fuerza [kN], tensión [kN/m²].

Modelos idénticos a los utilizados en el motor OpenSeesPy:
  - Concrete01 : parábola de Hognestad en compresión (pico en εc0, f'c),
                 sin tensión, descarga lineal hasta εcu (fpcu = 0).
  - Steel01    : bilineal elástico-perfectamente plástico (b = 0.01).

Los argumentos de OpenSeesPy se pasan con VALUES NOMINALES POSITIVOS
(`Concrete01(tag, fpc, epsc0, fpcu, epscu)`), que es la convención verificada
de la documentación: OpenSees interpreta compresión proveniente de EPSILONES
NEGATIVOS y devuelve tensiones negativas (verificado empíricamente).

Estas funciones puras de Python permiten: (1) contrastes analíticos de mano
(P0 nominal, capacidad balanceada) y (2) tests de regresión del motor de
fibras, sin depender de openseespy.
"""
import math

MPA = 1e3  # 1 MPa = 1000 kN/m²

# ---------------------------------------------------------------------------
# Material base del enunciado semana03  (f'c = 25 MPa, fy = 420 MPa)
# ---------------------------------------------------------------------------
FC  = 25.0 * MPA        # 25000 kN/m²
EPSC0 = -0.002          # deformación en el pico de la parábola
EPSCU = -0.004          # aplastamiento (σ = 0)
FPCU  = 0.0

# f'c ADOPTADO para las curvas de sección del Edificio A (G35). Supuesto
# documentado (ver reports/): los planos no dan armadura de muros de A y el
# catálogo P-M del Edificio A se calcula con f'c = 30 MPa (no el 25 MPa de B;
# el modelo elástico de A usa Ec(35) — esta constante solo alimenta el motor
# de secciones). fy = 420 MPa igual que B.
FC_A = 30.0 * MPA       # 30000 kN/m²

FY = 420.0 * MPA        # 420000 kN/m²
ES = 200.0 * 1e6        # 200 GPa en kN/m²
BHARD = 0.01            # pendiente post-fluencia de Steel01

EY = FY / ES            # 0.0021


def sig_concreto(eps, fc=FC, epc0=EPSC0, epcu=EPSCU):
    """Tensión (compresión negativa) de Concrete01 en la ruta monotónica."""
    if eps >= 0.0:
        return 0.0                # sin tensión
    if eps <= epcu:
        return 0.0                # aplastado
    if eps >= epc0:
        u = eps / epc0            # 0..1 en la rama ascendente
        return -fc * (2.0 * u - u * u)
    t = (eps - epc0) / (epcu - epc0)      # rama descendente lineal → FPCU=0
    return -fc * (1.0 - t)


def sig_acer(eps, fy=FY, Es=ES, bhard=BHARD):
    """Tensión de Steel01 en la ruta monotónica (b = bhard tras fluir)."""
    ey = fy / Es
    if eps < -ey:
        return -(fy + Es * bhard * (-eps - ey))
    if eps > ey:
        return fy + Es * bhard * (eps - ey)
    return Es * eps


# ---------------------------------------------------------------------------
# Nota: recálculos tipo "mano" usados por el reporte y los tests
# ---------------------------------------------------------------------------
def p0_nominal_aci(b, h, As, fc=FC, fy=FY):
    """P0 nominal ACI: 0.85·f'c·(Ag − As) + fy·As  (compresión POSITIVA)."""
    Ag = b * h
    return 0.85 * fc * (Ag - As) + fy * As


def p0_fibra_max(b, h, As, fc=FC, fy=FY, epc0=EPSC0, epcu=EPSCU, Es=ES,
                 bhard=BHARD):
    """Máximo N del material compuesto (concreto+fierro) bajo deformación
    uniforme ε ∈ [epcu, 0]. Mayor que P0 ACI porque aquí el hormigón llega a
    su pico f'c (la ACI usa el bloque 0.85·f'c en el estado último)."""
    mejor = 0.0
    n = 20001
    for i in range(n):
        eps = epcu + (0.0 - epcu) * i / (n - 1)
        N = sig_concreto(eps, fc, epc0, epcu) * (b * h - As) \
            + sig_acer(eps, fy, Es, bhard) * As
        if N < mejor:
            mejor = N
    return -mejor


def eps_0_85fc():
    """Deformación uniforme donde la parábola de Concrete01 vale exactamente
    0.85·f'c (u = 1 + √0.15). En ese punto el acero ya fluyó y el N del
    material compuesto coincide EXACTAMENTE con P0 ACI."""
    u = 1.0 + math.sqrt(0.15)
    return EPSC0 * u