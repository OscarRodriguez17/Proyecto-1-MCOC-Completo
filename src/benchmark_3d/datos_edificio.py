"""Datos del Edificio de Ingenieria extraidos de los planos (2017_67-*).

Unidades SI: metros, kN.
Fuente:
  - Plano 101 (planta estructural piso 1): reticulas X e Y, pilares 70x70,
    vigas V.60/80, muros RLE-MURO, radier N.R.=-0.05.
  - Plano 100 (fundaciones): subsuelo N.O.G.=-4.21 / losa cielo -4.01;
    lamina 100 tambien rotula hormigon G35 (ver docs/ficha_geometria.md).
  - Plano 501 (escaleras): niveles piso +3.91 y +7.87 (1.98 y 5.94 son
    descansos intermedios).
  - Elevacion EJE1-1' (plano 300): pisos 1S,1,2,3,4 -> techo +11.83.
  - Lamina 700 (cargas de diseno): SC 100-800 kg/m2; PM.adic 200-300 kg/m2;
    PP losa = e x 2500 kg/m3.

Materiales: hormigon G35, f'c = 35 MPa (lamina 100). Ec = 4700 sqrt(f'c)
[ACI 318] = 27,806 MPa.

Cargas representativas piso tipo (aula tipica, ficha_geometria.md §4):
  PM.adic = 260 kg/m2 -> 2.55 kN/m2 ; SC = 250 kg/m2 -> 2.50 kN/m2.
Rangos completos en lamina 700; ajustar aqui si cambia la zona elegida.

Alcance: reticula principal del plano 101 (45 x 16.15 m) mas el anexo/voladizo
metalico I'-J (45->50) en los 2 niveles superiores (planos 300, 310 y plantas
102/103). Sin sub-ejes intermedios.
"""

import math

GRID_X = {"E": 0.0, "F": 10.0, "G": 20.0, "H": 30.0, "I": 40.0,
          "I2": 45.0, "J": 50.0}
GRID_Y = {"A3": 0.0, "A2a": 2.31, "A2": 7.25, "A1c": 12.25, "A1": 16.15}

# ── Anexo / voladizo metalico I'->J (45 -> 50 m) ─────────────────────────
# Presente SOLO en los 2 niveles superiores (losa +7.87 = indice 3 y
# +11.83 = indice 4 = techo), segun corte 1-1' (plano 300), elevacion EJE J
# (plano 310) y plantas 102/103. Por debajo el hormigon llega hasta I' (45 m).
ANEXO = {
    "x0": "I2",                 # 45 m
    "x1": "J",                  # 50 m
    "levels": (3, 4),           # indices normalizados de nivel con losa
    "col": ("P.M.", 0.30, 0.020),   # tubo cerrado 300x300x20 mm
    "beam": ("V.M.", 0.30, 0.005),  # tubo cerrado 300x300x5 mm
    "eje_y": ("A3", "A2", "A1"),    # fachada J con pilares metalicos
}
E_STEEL = 200000000.0          # kN/m2 (200 GPa)
G_STEEL = E_STEEL / (2.0 * (1.0 + 0.3))
GAMMA_STEEL = 78.5             # kN/m3 acero

LEVEL_Z = [-4.21, -0.05, 3.91, 7.87, 11.83]
NLEV = len(LEVEL_Z)
STORY_H = [LEVEL_Z[i + 1] - LEVEL_Z[i] for i in range(NLEV - 1)]

COL_B = COL_H = 0.70
BEAM_B = 0.60
BEAM_H = 0.80
SLAB_H = 0.15                      # losas M.H.A. e=15 cm (lamina 700; rango 12-30)
K_SHEAR = 5.0 / 6.0                # correccion por cortante seccion rectangular (SAP2000 A/1.2)

# Ejes Y con pilares 70x70 segun plano 101 (capa RLE-PILAR);
# en ejes 2a y 1'' no hay pilares, solo muros cortos y vigas transversales.
COL_EN_Y = ("A3", "A2", "A1")

WALLS = [
    {"id": "ME-32", "axis": "E", "y0": "A3", "y1": "A2", "t": 0.20},
    {"id": "MI-32", "axis": "I", "y0": "A3", "y1": "A2", "t": 0.30},
    {"id": "MI-12", "axis": "I", "y0": "A2", "y1": "A1", "t": 0.30},
    {"id": "M1c-E", "y": "A1c", "x0": "E", "x1": 6.60, "t": 0.20},
    {"id": "M2a", "y": "A2a", "x0": 3.15, "x1": 6.85, "t": 0.20},
]

FC_MPA = 35.0                      # G35 segun lamina 100 (docs/ficha_geometria.md)
FC = FC_MPA * 1000.0               # kN/m2
E_C = 4700.0 * math.sqrt(FC_MPA) * 1000.0   # ACI 318: kN/m2 (=27,806 MPa)
NU = 0.2
G_C = E_C / (2.0 * (1.0 + NU))
GAMMA_C = 25.0                     # kN/m3 hormigon armado

PM_ADIC = 2.55                     # kN/m2 (260 kg/m2, lamina 700: rango 200-300)
SC_AULA = 2.50                     # kN/m2 (250 kg/m2, lamina 700: rango 100-800)

Q_G = 0.15 * GAMMA_C + PM_ADIC     # PP losa e=15 cm + PM.adic
Q_Q = SC_AULA

ALPHA_EQ = 0.10


def sec_columna():
    a = COL_B * COL_H
    i = COL_B ** 4 / 12.0
    j = 0.1406 * COL_B ** 4
    return a, i, i, j


def sec_viga():
    a = BEAM_B * BEAM_H
    i_strong = BEAM_B * BEAM_H ** 3 / 12.0
    i_weak = BEAM_H * BEAM_B ** 3 / 12.0
    j = 0.196 * BEAM_B * BEAM_H ** 3
    return a, i_weak, i_strong, j


def sec_viga_compuesta(span, tipo):
    """Seccion T ('T', viga interior) o L ('L', viga de borde) con losa
    colaborante. Retorna (A, i_weak, i_strong, J) = mismo orden que sec_viga,
    donde i_strong es la flexion por gravedad (flange en compresion).

    Ancho efectivo segun ACI 318-19 Table 6.3.2.1:
      - T : min(L_n/4, b_w + 16 h_f, separacion c-c de almas)
      - L : min(b_w + L_n/12, b_w + 6 h_f)
    La inercia debil y J se toman del alma rectangular (aporte de la losa
    despreciable fuera del plano). El peso propio de la viga se computa SOLO
    con el alma (la losa ya va en la carga superficial q_G): ver cargas.py.
    """
    h_f = SLAB_H
    ln = max(span - COL_B, 1e-9)
    if tipo == "T":
        b_e = min(ln / 4.0, BEAM_B + 16.0 * h_f, span)
    else:
        b_e = BEAM_B + min(ln / 12.0, 6.0 * h_f)
    b_e = max(b_e, BEAM_B)          # viga muy corta: sin alas efectivas

    A_web = BEAM_B * BEAM_H
    A_f = (b_e - BEAM_B) * h_f
    A = A_web + A_f
    y_web = h_f + BEAM_H / 2.0
    y_f = h_f / 2.0
    ybar = (A_web * y_web + A_f * y_f) / A
    I_f = (b_e - BEAM_B) * h_f ** 3 / 12.0 + A_f * (ybar - y_f) ** 2
    I_web = BEAM_B * BEAM_H ** 3 / 12.0 + A_web * (y_web - ybar) ** 2
    i_strong = I_f + I_web
    i_weak = BEAM_H * BEAM_B ** 3 / 12.0
    return A, i_weak, i_strong, sec_viga()[3]


def shear_area_muro(t, largo, resiste):
    """(Ay, Az): areas de cortante en ejes locales para '-shear' de
    elasticBeamColumn (rigidez de cortante finita tipo Timoshenko).

    Seccion rectangular: A_s = k*A con k=5/6 (equivalente a A/1.2 de SAP2000,
    convencion por defecto en frame members).
    """
    a = t * largo
    return K_SHEAR * a, K_SHEAR * a


def sec_box(b, t):
    """Seccion HSS cuadrada (tubo cerrado) de acero: (A, Iy, Iz, J).

    J de tubo cerrado de pared delgada: J = (b-t)^3 t.
    """
    a = b ** 2 - (b - 2.0 * t) ** 2
    i = (b ** 4 - (b - 2.0 * t) ** 4) / 12.0
    return a, i, i, (b - t) ** 3 * t


def sec_pm():
    """Seccion P.M. 300x300x20 (pilar metalico del anexo)."""
    return sec_box(*ANEXO["col"][1:])


def sec_vm():
    """Seccion V.M. 300x300x5 (viga metalica del anexo)."""
    return sec_box(*ANEXO["beam"][1:])


def anexo_levels():
    """Indices de nivel (1..NLEV-1) con losa del anexo metalico."""
    return set(ANEXO["levels"])


def sec_muro(t, largo, resiste):
    """Columna ancha equivalente. resiste='X': plano del muro contiene eje X."""
    a = t * largo
    i_big = t * largo ** 3 / 12.0
    i_small = largo * t ** 3 / 12.0
    # Constante torsional de San Venant para seccion rectangular alargada.
    j = (1.0 / 3.0) * largo * t ** 3 * (1.0 - 0.63 * t / largo)
    if resiste == "X":
        return a, i_big, i_small, j
    return a, i_small, i_big, j


def wall_geometry(w):
    """(xc, yc, largo, espesor, resiste) de un muro en planta."""
    t = w["t"]
    if "axis" in w:
        x = GRID_X[w["axis"]]
        y0, y1 = GRID_Y[w["y0"]], GRID_Y[w["y1"]]
        return x, (y0 + y1) / 2.0, abs(y1 - y0), t, "Y"
    y = GRID_Y[w["y"]]
    x0 = GRID_X[w["x0"]] if isinstance(w["x0"], str) else w["x0"]
    x1 = GRID_X[w["x1"]] if isinstance(w["x1"], str) else w["x1"]
    return (x0 + x1) / 2.0, y, abs(x1 - x0), t, "X"
