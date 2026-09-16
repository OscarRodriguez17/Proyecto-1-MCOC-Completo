# Ficha de geometría — Edificio B (Proyecto 1 MCOC)

> Fuente de verdad: planos DXF `2024_22-*`. Unidades del modelo: **m, kN**.
> Convención de escala verificada: **1 unidad DXF = 1 cm** (factor 0.01 a m),
> confirmada por cotas (500 u = 5.00 m) y por el espaciamiento de niveles
> (396 u = 3.96 m).

## 1. Niveles (Z)

| k | z [m] | Nivel | Rol en el modelo |
|---|-------|-------|------------------|
| 0 | −4.01 | 1º Subterráneo | **BASE empotrada** |
| 1 | −0.05 | Piso 1 | piso + diafragma |
| 2 |  3.91 | Piso 2 | piso + diafragma |
| 3 |  7.87 | Piso 3 | piso + diafragma |
| 4 | 11.83 | Piso 4 | piso + diafragma |
| 5 | 15.79 | Cubierta | piso + diafragma |

Entrepiso uniforme **3.96 m**. El nivel **z = −7.97** (vigas de fundación
"V.F. 20/120" + zapatas escalonadas) es **fundación** → se ignora
(fuente: corte EJE 1, lámina `2024_22-300`).

## 2. Grilla en planta (de lámina `2024_22-101`)

- **Ejes X** [m]: 11.30 · 14.85 · 18.60 · 22.35 · 27.35 · 32.35 · 37.35 · 42.35
- **Ejes Y** [m]: 10.93 · 15.19 · 18.18 · 22.81 · 27.08
  (15.19 y 22.81 son líneas de **vigas secundarias**, ancho 0.30)

El constructor arma la grilla de ejes **solo con pilares + vigas** (líneas
limpias del plano) y luego **ajusta muros y extremos de viga** a esa grilla.
Así los pilares quedan en su posición exacta del DXF (0.00 de desplazamiento)
y se garantiza la conectividad de la red sin desordenar columnas.

## 3. Pilares — 8, todos **70×70 cm** (P.70×70)

| x [m] | y [m] |
|------|------|
| 22.35 | 27.08 |
| 32.35 | 27.08 |
| 42.35 | 27.08 |
| 14.85 | 18.18 |
| 22.35 | 18.18 |
| 32.35 | 18.18 |
| 22.35 | 10.93 |
| 32.35 | 10.93 |

## 4. Muros (M.H.A.) — modelados como **columna ancha equivalente**

Cada paño = un elemento frame vertical en su centroide, con sección real
`espesor × largo`, que aporta rigidez lateral y se deforma con el diafragma.

**Verticales** (corren en Y) — (x, y0, y1, e):
- (11.20,  9.84, 12.75, **0.60**)   ← ala inferior-izq (L con retorno en 10.78)
- (11.20, 25.25, 28.17, **0.60**)   ← ala superior-izq (L con retorno en 27.23)
- (39.93, 20.93, 24.05, **0.25**)   ← núcleo, vertical oeste
- (42.58, 10.58, 18.53, **0.25**)   ← muro este, paño inferior
- (42.58, 20.93, 26.73, **0.25**)   ← muro este, paño superior

**Horizontales** (corren en X) — (y, x0, x1, e):
- (10.78, 11.50, 12.95, **0.30**)
- (23.90, 39.81, 42.45, **0.30**)   ← núcleo, horizontal norte
- (27.23, 11.50, 12.95, **0.30**)

Extraídos de la capa `RLE-MURO` por **unión** de caras opuestas (validado
contra el DXF crudo). Los muros se ubican en su **posición real del plano**
(no se ajustan a la grilla de vigas); trabajan por el diafragma rígido.

**Bloque escalera/ascensor** (y > 32, M.H.A. e=0.20; láminas 500/501):
`MUROS_BLOQUE_SUP_V/H` en `datos_edificio.py` — **excluido por defecto**,
disponible por si se decide incorporarlo.

## 5. Vigas (canto 0.80 salvo indicación)

Asignación de sección por ancho detectado en planta:
- ancho ≥ 0.50 → **V.60/80** (principal)
- 0.35 ≤ ancho < 0.50 → **V.40/80**
- ancho < 0.35 → **V.30/80** (secundaria)

Líneas centrales (el constructor las subdivide en la grilla): ver
`BEAMS_V` y `BEAMS_H` en `datos_edificio.py`. Se aplican en los 5 pisos
(−0.05, 3.91, 7.87, 11.83, 15.79).

## 6. Material — Hormigón armado H30 (lineal-elástico)

| Propiedad | Valor |
|-----------|-------|
| f'c | 30 MPa |
| E | 25 000 000 kN/m² (≈ 25 GPa) |
| ν | 0.20 |
| G | E / 2(1+ν) |
| γ | 25 kN/m³ (peso propio) |

## 7. Cargas

- **Peso propio:** γ · A · L por elemento, repartido a nodos (−Z).
- **Sobrecarga:** SC piso = 2.5 kN/m², SC cubierta = 1.0 kN/m²
  (lámina `2024_22-700` — **supuesto a validar**).

## 8. Modelo numérico

- Pórtico 3D, 6 GDL/nodo, `elasticBeamColumn`.
- Ejes locales explícitos (`vecxz` no colineal): transf 1 verticales,
  2 vigas-X, 3 vigas-Y.
- Diafragma rígido por piso (`rigidDiaphragm`, perpDirn=3) con
  `constraints('Transformation')`.
- **Muros con brazos rígidos**: cada muro se engancha, en cada piso, a las
  vigas de su eje y a los muros contiguos (compone las L y el núcleo en C),
  mediante elementos muy rígidos. Es la columna-ancha en su versión fiel.
- **Bloque escalera/ascensor incluido** (muros e=0.20); se amarra al marco por
  el diafragma de cada piso (supuesto: losa continua sobre el vano y 27–33).
- Base empotrada (6 GDL fijos) en z = −4.01.

**Tamaño:** 235 nodos (+5 masters), 350 elementos
(40 pilares, 60 tramos de muro, 215 vigas, 35 brazos rígidos).

**Verificaciones (`verificar.py`):**
- Equilibrio Z: residuo ≈ 3×10⁻¹¹ kN ✓
- Superposición: error ≈ 10⁻¹⁸ m ✓
- Euler-Bernoulli (voladizo): error rel ≈ 3×10⁻¹⁶ < 1×10⁻¹⁰ ✓

## 9. Supuestos y pendientes de validar

1. **Cubierta (15.79):** se asume igual a la planta tipo. Refinar con
   lámina `2024_22-102`.
2. **Bloque escalera/ascensor** (y ≈ 33–38): INCLUIDO como muros e=0.20,
   amarrado por diafragma. Validar continuidad de losa sobre el vano y≈27–33
   y si el bloque sube sobre la cubierta (penthouse).
3. **Sección por viga:** asignada por ancho; confirmar cantos reales por eje
   con lámina de elevaciones de vigas `2024_22-400`.
4. **Espesores/extensión exacta de muros** y aberturas (vanos): la columna
   ancha usa el paño lleno; los vanos no se descuentan.
5. **Sobrecargas:** valores genéricos; reemplazar por los de la lámina 700.
