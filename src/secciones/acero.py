# -*- coding: utf-8 -*-
"""
acero.py — Diagrama de interacción P–M de los pilares METÁLICOS del Edificio A.

Sección: tubo/cajón cuadrado P.M. 300×300×20 (anexo eje J, voladizos y raíz).
Material: acero estructural A270ES (NCh203), fy = 270 MPa, E = 200 GPa.
  Los planos no especifican la calidad del acero; A270ES es el acero
  estructural chileno usual en cajones soldados. (Los 420 MPa del proyecto son
  de las BARRAS de refuerzo A630-420H, no de perfiles.) Cambiar `FY_MPA` aquí
  y re-correr `scripts/semana03_edificio_A_run.py --recalcular` si se adopta otro.

Envolvente NOMINAL de sección (misma convención que las curvas de hormigón del
catálogo, P > 0 = compresión, M = momento resistente ≥ 0): plastificación total
(bloque rectangular ±fy) con el eje neutro barriendo la altura, flexión en torno
a un eje principal (sección doblemente simétrica, igual en ambos ejes).
  - P0 = fy·A (aplastamiento), Pt = −fy·A (tracción), Mp = fy·Z (P = 0).
  - Sección compacta: (b − 3t)/t = 12 < 1.12·√(E/fy) = 30.5 (AISC 360 B4.1b),
    así que alcanza Mp sin pandeo local.
Información adicional (no reduce la envolvente de sección): resistencia a
compresión con pandeo por flexión AISC 360 E3 para L = 3.96 m, K = 1.
"""
import math

FY_MPA = 270.0                 # A270ES
E_MPA = 200000.0
MPA = 1000.0                   # kN/m2
NOMBRE = "PM_A_300x300x20"
N_PUNTOS = 17


def _fibras_box(b, t, fibras_por_t=40):
    """Fibras (y, dA) de un tubo cuadrado b×b×t, y = distancia al eje.
    El paso es t/fibras_por_t para que el borde ala–alma caiga justo en el
    borde de una fibra (sin error de discretización en P ni en Mp)."""
    import numpy as np
    dy = t / fibras_por_t
    n = int(round(b / dy))
    y = -b / 2.0 + (np.arange(n) + 0.5) * dy
    ancho = np.where(np.abs(y) > b / 2.0 - t, b, 2.0 * t)
    return y, ancho * dy


def propiedades(b=0.30, t=0.020):
    A = b * b - (b - 2 * t) ** 2
    I = (b ** 4 - (b - 2 * t) ** 4) / 12.0
    Z = b * b * b / 4.0 - (b - 2 * t) ** 3 / 4.0
    return {"A": A, "I": I, "Z": Z, "r": math.sqrt(I / A)}


def envolvente(b=0.30, t=0.020, fy_mpa=FY_MPA, n_puntos=N_PUNTOS):
    """(P[], M[]) plástica, P ascendente de −fy·A a +fy·A, M ≥ 0 [kN, kN·m]."""
    import numpy as np
    fy = fy_mpa * MPA
    y, dA = _fibras_box(b, t)
    # eje neutro en cada borde de fibra: P y M exactos para el bloque ±fy
    bordes = np.concatenate(([-b / 2.0], (y[:-1] + y[1:]) / 2.0, [b / 2.0]))
    Ps, Ms = [], []
    for yn in bordes:
        sig = np.where(y > yn, fy, -fy)           # compresión sobre yn
        Ps.append(float(np.sum(sig * dA)))
        Ms.append(abs(float(np.sum(sig * dA * y))))
    orden = np.argsort(Ps)
    Ps = [Ps[k] for k in orden]
    Ms = [Ms[k] for k in orden]
    Py = fy * propiedades(b, t)["A"]
    objetivo = [-Py + 2.0 * Py * k / (n_puntos - 1) for k in range(n_puntos)]

    def interp(p):
        if p <= Ps[0]:
            return Ms[0]
        if p >= Ps[-1]:
            return Ms[-1]
        lo, hi = 0, len(Ps) - 1
        while hi - lo > 1:
            mid = (lo + hi) // 2
            if Ps[mid] <= p:
                lo = mid
            else:
                hi = mid
        p0, p1 = Ps[lo], Ps[hi]
        w = 0.0 if p1 == p0 else (p - p0) / (p1 - p0)
        return Ms[lo] + w * (Ms[hi] - Ms[lo])

    M = [interp(p) for p in objetivo]
    M[0] = M[-1] = 0.0                            # aplastamiento / tracción pura
    return objetivo, M


def pandeo_aisc(b=0.30, t=0.020, L=3.96, K=1.0, fy_mpa=FY_MPA, e_mpa=E_MPA):
    """Pn [kN] a compresión con pandeo por flexión (AISC 360-16 E3)."""
    pr = propiedades(b, t)
    esb = K * L / pr["r"]
    Fe = math.pi ** 2 * e_mpa / esb ** 2
    if fy_mpa / Fe <= 2.25:
        Fcr = 0.658 ** (fy_mpa / Fe) * fy_mpa
    else:
        Fcr = 0.877 * Fe
    return Fcr * MPA * pr["A"], esb


def entrada_catalogo(b=0.30, t=0.020, fy_mpa=FY_MPA, L=3.96):
    """Entrada del catálogo `curvas_A` con el mismo esquema que las secciones
    de hormigón (A, As, P0_aci, P0_fibra, balanceado, envelope{P,M,EI0,MOTIVO})."""
    pr = propiedades(b, t)
    P, M = envolvente(b, t, fy_mpa)
    Py = fy_mpa * MPA * pr["A"]
    Mp = fy_mpa * MPA * pr["Z"]
    Pn, esb = pandeo_aisc(b, t, L, 1.0, fy_mpa)
    EI = E_MPA * MPA * pr["I"]
    return {
        "A": pr["A"],
        "As": pr["A"],                 # toda la sección es acero
        "P0_aci": Py,                  # aplastamiento fy·A (análogo a P0)
        "P0_fibra": Py,
        "balanceado": {"P": 0.0, "M": Mp},   # máximo momento: Mp en P = 0
        "envelope": {
            "P": [round(float(x), 3) for x in P],
            "M": [round(float(x), 3) for x in M],
            "EI0": [round(EI, 3)] * len(P),
            "MOTIVO": ["plastificacion_total"] * len(P),
        },
        "material": "acero",
        "acero": f"A270ES fy={fy_mpa:.0f} MPa" if abs(fy_mpa - 270.0) < 1e-9
                 else f"fy={fy_mpa:.0f} MPa",
        "fy_MPa": fy_mpa,
        "Mp": Mp,
        "Z": pr["Z"],
        "Pn_pandeo_AISC": Pn,
        "esbeltez_KL_r": esb,
        "L_pandeo": L,
        "nota": ("Envolvente nominal de seccion (plastificacion total, sin "
                 "phi). Pn_pandeo_AISC informa la compresion con pandeo por "
                 "flexion (AISC 360 E3, K = 1)."),
    }
