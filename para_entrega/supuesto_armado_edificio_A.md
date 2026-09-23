# Supuesto adoptado: armado de secciones de muros del Edificio A

**Fecha:** Septiembre 2026 (extendido desde la semana 03 del Edificio B)
**Estado:** ADITIVO — no afecta los cálculos existentes ni los IDs/geométricos.

---

## 1. Contexto

El catálogo P-M del Edificio A se extendió al mismo motor de secciones
aditivo (`src/secciones/`) usado para el Edificio B (semana 03). Las curvas
de interacción se calculan por el método de fibras con OpenSeesPy (envolvente
de 17 puntos a malla 12×12).

Los planos del Edificio A (2017_67-*) **no especifican la armadura de los
muros estructurales** (ME-32, MI-32, MI-12, M1c-E y M2a). Solo dan la
especificación "RLE-MURO" sin detalle de acero. Por lo tanto, se adopta la
misma disposición general usada en el Edificio B con los valores indicados
a continuación, que se documentan como hipótesis de proyecto.

La **columna 70×70** sí viene documentada en los planos (capa RLE-PILAR,
plano 101): 8φ28, que se modela directamente.

---

## 2. Valores adoptados

| Parámetro | Edificio B | Edificio A (adoptado) |
|-----------|-----------|-----------------------|
| f'c (secciones) | 25 MPa | **30 MPa** (G35, ver nota 1) |
| fy | 420 MPa | 420 MPa |
| Recubrimiento muros | 5 cm | **3 cm** |
| Alma muros | φ12 @ 20 cm, 1 capa por cara | **φ10 @ 20 cm, doble malla** (ρ_v ≈ 0.003) |
| Zona de borde | 0.40 m, 4φ16 (2 a cada cara) | **max(2t, 0.15L)**, **6φ16** (3 a cada cara) |
| Columna | 8φ28, recub. 5 cm, f'c = 25 MPa | 8φ28, recub. 5 cm, f'c = 30 MPa |

### Nota 1 — f'c = 30 MPa (no 35 MPa)

El modelo elástico de A usa Ec calculada con G35 → f'c = 35 MPa (ver
`src/benchmark_3d/datos_edificio.py`, `FC_MPA = 35.0`). Sin embargo,
para el catálogo de secciones (curvas P-M de diseño y punto de falla)
se adopta f'c = 30 MPa. Esta decisión es la establecida por el usuario
como parte de las condiciones de borde del supuesto de diseño.

---

## 3. Disposición del armado (muros)

Se mantiene la misma estrategia que el Edificio B (`muro_generico`):

- **Zona de borde:** longitud `Lb = max(2·t, 0.15·L)` desde cada extremo del
  muro. En esta zona se ubican 6 barras de φ16 por extremo, es decir 3 a
  cada cara (y = ±(t/2 − recub)), a offsets de 0.10, 0.20 y 0.30 m del
  borde del paño.
- **Alma:** barras φ10 @ 0.20 m a cada cara (doble malla), fuera de las
  zonas de borde. ρ_v ≈ 0.003.
- **Recubrimiento al eje de la barra:** 3 cm (0.03 m).

### Longitud de borde por muro

| Muro | t (m) | L (m) | Lb (m) | φ16 por extremo |
|------|------:|------:|-------:|-----------------:|
| ME-32 | 0.20 | 7.25 | 1.087 | 6 (3×2) |
| MI-32 | 0.30 | 7.25 | 1.087 | 6 (3×2) |
| MI-12 | 0.30 | 8.90 | 1.335 | 6 (3×2) |
| M1c-E | 0.20 | 6.60 | 0.990 | 6 (3×2) |
| M2a | 0.20 | 3.70 | 0.555 | 6 (3×2) |

---

## 4. Secciones resultantes en el catálogo

| Etiqueta | t × L (m) | As (m²) | P0 ACI (kN) | P0 fibra (kN) |
|----------|-----------|---------|------------|--------------|
| col_A_0.70x0.70 | 0.70 × 0.70 | 0.004926 | 14 438 | 16 523 |
| muro_A_0.20x3.70 | 0.20 × 3.70 | 0.004298 | 20 565 | 23 790 |
| muro_A_0.20x6.60 | 0.20 × 6.60 | 0.006026 | 36 037 | 41 830 |
| muro_A_0.20x7.25 | 0.20 × 7.25 | 0.006340 | 39 476 | 45 846 |
| muro_A_0.30x7.25 | 0.30 × 7.25 | 0.006340 | 57 964 | 67 596 |
| muro_A_0.30x8.90 | 0.30 × 8.90 | 0.007282 | 70 958 | 82 794 |

Las envolventes P-M completas (17 puntos, malla 12×12) se encuentran en:
- `results/secciones_semana03.json` → clave `curvas_A`
- `results/edificio_solido.json` → `edificios[0].secciones.secciones`

---

## 5. Elementos del Edificio A asociados

- **79 columnas:** todas etiquetadas `col_A_0.70x0.70` (tag 1..79 del modelo).
- **32 muros:** distribuidos según el paño de `datos_edificio.WALLS`:

| Etiqueta | Paño | N° elementos en el modelo |
|----------|------|---------------------------:|
| muro_A_0.20x7.25 | ME-32 | 8 |
| muro_A_0.30x7.25 | MI-32 | 12 |
| muro_A_0.30x8.90 | MI-12 | 4 |
| muro_A_0.20x6.60 | M1c-E | 4 |
| muro_A_0.20x3.70 | M2a | 4 |

---

## 6. Impacto en la verificación

- **P0 fibra / P0 ACI ≈ 1.16** para todas las secciones de A (dentro del
  rango esperado de 1.05–1.30 para secciones de muro con alta relación
  As/Ag, ver tests).
- La superposición G+Q ≡ GQ en el Edificio A se verifica con |dP| y |dM|
  < 1×10⁻¹⁰ (linealidad del modelo), idéntica al comportamiento del B.
- Este supuesto **no modifica** el análisis lineal 3D existente (el modelo
  elástico de A usa Ec(35) y las cargas no cambian); solo afecta las
  curvas P-M de diseño del catálogo de secciones.

---

## 7. Archivos afectados (ADITIVO)

- `src/secciones/materiales.py` — constante `FC_A = 30 MPa`, funciones
  parametrizadas con defaults que preservan el comportamiento de B.
- `src/secciones/seccion.py` — constructores `columna_A_70x70()`,
  `muro_A_generico()`, `muros_A()`, p0_aci/p0_fibra usan self.fc.
- `src/secciones/analitica.py` — `_integral_grid` pasa seccion.fc/fy.
- `src/secciones/demandas_a.py` — nuevo: extracción de demandas de A.
- `scripts/semana03_edificio_A_run.py` — nuevo: runner del catálogo A.
- `src/secciones/exportar_unity.py` — ahora enriquece ambos edificios.
- `results/secciones_semana03.json` — secciones `curvas_A`, `demandas_A`, etc.
- `results/edificio_solido.json` — ambos edificios con bloque `secciones`.
