# -*- coding: utf-8 -*-
"""
datos_edificio.py — Geometría y propiedades del Edificio B (Proyecto 1 MCOC).

TODA la geometría proviene de la extracción verificada de los planos DXF:
  - Planta tipo:  2024_22-101  (cielo 1º subt. a cielo piso 3º)
  - Cortes:       2024_22-300..305 (niveles Z)
Convención DXF: 1 unidad = 1 cm  ->  factor 0.01 a metros. Unidades: m, kN.

Origen global: coordenadas absolutas del plano (en metros). Z = elevación real.
Base del modelo: z = -4.01 (losa 1º subterráneo). Fundaciones IGNORADAS.
"""

# ----------------------------------------------------------------------
# NIVELES  (z [m], nombre)   — espaciamiento uniforme 3.96 m
# ----------------------------------------------------------------------
NIVELES = [
    (-4.01, "1º Subterráneo (BASE empotrada)"),
    (-0.05, "Piso 1"),
    ( 3.91, "Piso 2"),
    ( 7.87, "Piso 3"),
    (11.83, "Piso 4"),
    (15.79, "Cubierta"),
]
Z_BASE = -4.01

# ----------------------------------------------------------------------
# MATERIAL — Hormigón armado H30 (lineal-elástico)
# ----------------------------------------------------------------------
FC_MPA     = 30.0
E_CONCRETO = 25_000_000.0     # kN/m²  (~4700√fc, redondeado a 25 GPa)
NU         = 0.20
G_CONCRETO = E_CONCRETO / (2.0 * (1.0 + NU))
RHO        = 2.5              # t/m³  (2500 kg/m³) — para peso propio en cargas.py

# ----------------------------------------------------------------------
# SISMO — método pseudoestático (misma convención que el Edificio A)
#   V_base = ALPHA_EQ · W    (W = peso sísmico = peso muerto G)
#   F_i    = V · W_i·z_i / Σ(W_j·z_j)   (z absoluto, como en el Edificio A)
# ----------------------------------------------------------------------
ALPHA_EQ = 0.10              # coeficiente sísmico basal (paridad con Edificio A)

# ----------------------------------------------------------------------
# SECCIONES  (b, h en metros)  — b = dimensión en X local, h = en Y local
# ----------------------------------------------------------------------
SEC_PILAR = (0.70, 0.70)          # P.70x70
# Vigas por ancho detectado en planta (canto 0.80 salvo indicación):
SEC_VIGA_PRIN = (0.60, 0.80)      # V.60/80 (principales, ancho >= 0.5)
SEC_VIGA_MED  = (0.40, 0.80)      # V.40/80 (ancho 0.35–0.5)
SEC_VIGA_SEC  = (0.30, 0.80)      # V.30/80 (secundarias, ancho < 0.35)

def seccion_viga(ancho):
    if ancho >= 0.50: return SEC_VIGA_PRIN
    if ancho >= 0.35: return SEC_VIGA_MED
    return SEC_VIGA_SEC

# ----------------------------------------------------------------------
# PILARES  (x, y) [m]  — todos 70x70
# ----------------------------------------------------------------------
PILARES = [
    (22.35, 27.08), (32.35, 27.08), (42.35, 27.08),
    (14.85, 18.18), (22.35, 18.18), (32.35, 18.18),
    (22.35, 10.93), (32.35, 10.93),
]

# ----------------------------------------------------------------------
# MUROS (modelados como COLUMNA ANCHA equivalente)
#   Verticales  (corren en Y):  (x_centro, y0, y1, espesor)
#   Horizontales(corren en X):  (y_centro, x0, x1, espesor)
# ----------------------------------------------------------------------
MUROS_V = [
    (11.20,  9.84, 12.75, 0.60),
    (11.20, 25.25, 28.17, 0.60),
    (39.93, 20.93, 24.05, 0.25),
    (42.58, 10.58, 18.53, 0.25),
    (42.58, 20.93, 26.73, 0.25),
]
MUROS_H = [
    (10.78, 11.50, 12.95, 0.30),
    (23.90, 39.81, 42.45, 0.30),
    (27.23, 11.50, 12.95, 0.30),
]

# Bloque escalera/ascensor (y > 32, M.H.A. e=0.20) — EXCLUIDO por defecto
# (láminas 500/501). Disponible por si se decide incorporarlo.
MUROS_BLOQUE_SUP_V = [(35.55, 33.60, 37.70, 0.20), (42.60, 33.40, 37.90, 0.20)]
MUROS_BLOQUE_SUP_H = [(33.50, 31.11, 40.55, 0.20), (37.80, 31.11, 42.70, 0.20)]

# ----------------------------------------------------------------------
# VIGAS — líneas centrales (el constructor las subdivide en la grilla)
#   BEAMS_V  (x, y0, y1, ancho)   corren en Y
#   BEAMS_H  (y, x0, x1, ancho)   corren en X
# ----------------------------------------------------------------------
BEAMS_V = [
    (11.30, 12.75, 25.25, 0.40),
    (14.85, 11.23, 26.78, 0.60),
    (18.60, 11.23, 26.78, 0.60),
    (22.35, 11.28, 26.73, 0.60),
    (27.35, 11.23, 26.78, 0.60),
    (32.35, 11.28, 26.73, 0.60),
    (37.35, 11.23, 26.78, 0.60),
]
BEAMS_H = [
    (10.93, 12.95, 42.45, 0.60),
    (15.19, 11.50, 18.30, 0.30),
    (18.18, 15.20, 42.45, 0.60),
    (22.81, 11.50, 18.30, 0.30),
    (27.08, 12.95, 42.00, 0.60),
]

# Niveles donde se repite la planta tipo (todos los pisos sobre la base).
# La cubierta (15.79) usa la planta 102; aquí se asume igual a la tipo
# (PENDIENTE de refinar con lámina 2024_22-102).
NIVELES_CON_VIGAS = [-0.05, 3.91, 7.87, 11.83, 15.79]

TOL      = 0.05  # tolerancia de identidad de nodo [m]
GRID_TOL = 0.30  # tolerancia para consolidar ejes cercanos a una grilla única [m]
                 # (alinea vigas/pilares/muros desfasados en planos para que la red conecte)
