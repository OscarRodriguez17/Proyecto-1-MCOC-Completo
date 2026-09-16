# FICHA DE GEOMETRÍA — Edificio de Ingeniería (Proyecto P1 MCOC)

> Fuente: extracción automática DXF desde `Desktop\planos_edificio_ing\` (serie 2017_67).
> Método: ODA File Converter (DWG→DXF 2018 ASCII) + ezdxf. Unidad de dibujo = **1 cm**
> (verificado: cotas 360/280/500 coinciden exactamente con distancias entre ejes en unidades).
> Estado: **v2 — espejo del modelo `src/benchmark_3d/` (Sesión 3). Ítems ⚠️ siguen abiertos.**
> Sesión 7: añadida sección §1b (bloque 2, 50 m E–J + anexo metálico).

---

## Índice de láminas (escaneo Sesión 3, rotulados verificados)

| Láminas | Contenido |
|---|---|
| 100 | Planta fundaciones + corte típico |
| 101 | Planta cielo piso 1° + cielo 1° subterráneo (**modelo**) |
| 102 | Plantas cielo pisos 2° y 3° |
| 103 | Planta cielo piso 4° |
| 200-I/S, 201–205 | Armaduras (malla inf/sup) fundaciones y losas por piso |
| 300–310 | Elevaciones por eje (300 = "EJE 1-1'", techo +11.83). ⚠️ Rotulado dentro de BLOQUES: ezdxf no lo ve en modelspace — recorrer `doc.blocks` |
| 400–402 | Ilegibles vía DXF (revisar visualmente) |
| 500–503 | Escaleras + cuadro de niveles (descansos +1.98/+5.94) |
| 600 | Plantas resumen + ELEVACIONES EJE EA / EB (posible clave A/B) |
| 700 | Cuadro de cargas de diseño (fuente q_G / q_Q) |
| 800–802 | Elevaciones de ejes con vigas metálicas, pernos Nelson |
| 000–002 | Detalles típicos (trabas, mallas, contención) |

---

## 0. Qué edificio estamos modelando

La lámina 101 contiene las dos plantas (superior e inferior). El modelo replica la
**planta INFERIOR**, identificada por coordenadas DXF (verificación Sesión 3):

| Evidencia | Modelo (`datos_edificio.py`) | Plano 101 inferior | Coincide |
|---|---|---|---|
| Ejes verticales E–I' | 0 / 10 / 20 / 30 / 40 / 45 m | x = 1061 → 5561 (luces 1000·4 + 500) | ✅ |
| Ejes horizontales 3/2a/2/1''/1 | 0 / 2.31 / 7.25 / 12.25 / 16.15 m | y = 1961 / 2192 / 2686 / 3186 / 3576 | ✅ |

- Por su retícula regular corresponde al edificio que v1 llamaba "Edificio B".
  ⚠️ Precisión Sesión 3: existen DOS anchos — **edificio corto 45 m (E–I')**,
  que es el modelado, y un **edificio largo 50 m (E–J)** cuyos rótulos de eje
  J aparecen recién en las láminas **102 y 103** (en 100/101 no se detectó J
  por texto; posible rótulo en bloques no escaneados). Verificar visualmente.
- Sub-ejes presentes en la lámina y **no incluidos** en el modelo: E' (−1.04 m
  antes de E), H' (+4.48 tras H), IA (+2.60 tras I), IB (+0.88 tras I'),
  1' (+0.88 sobre eje 1, lado oriente).

## 1. Retícula del modelo (valores exactos)

```python
GRID_X = {"E": 0.0, "F": 10.0, "G": 20.0, "H": 30.0, "I": 40.0, "I2": 45.0}
GRID_Y = {"A3": 0.0, "A2a": 2.31, "A2": 7.25, "A1c": 12.25, "A1": 16.15}
```

Grado de losa: Área de planta: 45 × 16.15 = **726.75 m²**. Panelaje: X [10,10,10,10,5] ·
Y [2.31, 4.94, 5.00, 3.90].

## 1b. Bloque 2 — 50 m E–J (Sesión 7, láminas 102 y 103)

El edificio largo (eje J en x DXF = 5490 cm) vive en las láminas 102/103 y se
modela en `src/benchmark_3d/bloque2.py`.

```python
GRID_X = {"E": 0.0, "F": 10.0, "G": 20.0, "H": 30.0, "I": 40.0,
          "I2": 45.0, "J": 50.0}
GRID_Y = {"A3": 0.0, "A2a": 2.31, "A2": 7.25, "A1c": 12.25, "A1": 16.15}
```

- Área por piso: 50 × 16.15 = **807.50 m²**. Núcleo E–I' idéntico al bloque 1
  (pilares P.70x70 en {A3,A2,A1}, 5 muros iguales).
- **Anexo metálico** bahía I'–J sobre las líneas con pilares:
  - Rigas **V.M. 300×300×5** (tubo cerrado de acero, vano de 5 m).
  - Columnas de fachada **P.M. 300×300×20** en el eje J (3 × 4 pisos).
  - Las losas de esa bahía siguen siendo M.H.A. con los mismo q_G/q_Q.
- Supuestos aceptados (decisión Oscar 28-08):
  - Bloques **colindantes alineados en E** (offset paramétrico, default 0).
  - Acero estructural: **E = 200 GPa, G = 77 GPa, γ = 78.5 kN/m³** (grado no
    legible en DXF; ajustar si el profesor da la especificación).
  - Simplificación preliminar: solo columnas de fachada en J (se omiten las
    V.M./P.M.I. intermedias y +V.I. 20/90 detectadas en la lámina 102).
  - Muros del bloque 2 = replicados del bloque 1 (pendiente verificación).

## 2. Niveles

| Nivel | Cota (m) | Altura de piso (m) |
|---|---|---|
| Base (sello fundación, empotrado) | −4.21 | – |
| Radier piso 1° | −0.05 | **4.16** |
| Cielo piso 2° | +3.91 | 3.96 |
| Cielo piso 3° | +7.87 | 3.96 |
| Techo (estimado desde elevación plano 300) | +11.83 | 3.96 |

- Nota: v1 listaba −4.01 ("cielo 1° subterráneo"); el modelo empotra en el
  sello de fundación −4.21 (N.O.G. plano 100), de ahí el primer piso de 4.16 m.
- +1.98 y +5.94 son descansos de escalera, NO niveles (plano 501).

## 3. Elementos estructurales del modelo

### Pilares
- `P.70x70` SOLO en intersecciones E…I' × {3, 2, 1} → **18 pilares por piso**
  (capa RLE-PILAR plano 101). En ejes 2a y 1'' NO hay pilares: solo vigas
  transversales y muros cortos.

### Vigas
- Todas idealizadas como `V.60/80` (b=0.60, h=0.80) en toda la retícula,
  incluidas transversales.
- ⚠️ Simplificación: los planos también tienen `V.40/60` secundarias y vigas
  metálicas `V.M. 300x300x5` mixtas — aún no diferenciadas en el modelo.

### Muros (los 5 del modelo, plano 101 — capa RLE-MURO)

| id | Ubicación | De – hasta (m) | Espesor | Resiste |
|---|---|---|---|---|
| ME-32 | Eje E | entre ejes 3–2 (largo 7.25) | 20 cm | Y (plano YZ) |
| MI-32 | Eje I | entre ejes 3–2 (largo 7.25) | 30 cm | Y |
| MI-12 | Eje I | entre ejes 2–1 (largo 8.90) | 30 cm | Y |
| M1c-E | Eje 1'' | x = 0 → 6.60 | 20 cm | X |
| M2a | Eje 2a | x = 3.15 → 6.85 | 20 cm | X |

**Verificación por nivel (Sesión 3, escaneo RLE-MURO completo láminas 101/102/103):**
- El par interior **M1c-E + M2a aparece IDÉNTICO** (largos 370/695 cm, mismos
  retornos de 215 cm) en las plantas de pisos superiores (láms. 102 y 103).
- Los muros perimetrales **ME-32 / MI-32 / MI-12 NO aparecen** en las plantas
  de pisos superiores (escaneo de lámina completa): según planos existirían
  solo en piso 1 / subterráneo. El modelo los repite en TODOS los niveles =
  **simplificación conservadora documentada**, pendiente de confirmación visual.

## 4. Materiales

- Hormigón **G35**: f'c = 35 MPa (lectura de Pablo, lámina 100). ⚠️ Escaneo
  exhaustivo Sesión 3 (TEXT/MTEXT + ATTRIB + bloques; LibreDWG y ODA; las 38
  láminas): **el rotulado de grado NO es extraíble vía DXF** — confirmar a ojo,
  incluido el piso sobre cielo 3°. Ec = 4700√f'c ≈ **27,806 MPa** (ACI 318),
  ν = 0.2, γc = 25 kN/m³.
- `V.M. 300x300x5` / `P.M.I.` / `P.M. 300x300x20` son **elementos METÁLICOS**:
  las láminas 800–802 (detalles) muestran pernos Nelson y "DESTAJAR PERFILES".
  Coherente con sistema de piso mixto acero-hormigón.
- ⚠️ Grado del acero estructural: por confirmar (tampoco legible vía DXF).

## 5. Cargas adoptadas (decisión final del modelo)

| Parámetro | Valor | Origen |
|---|---|---|
| q_G | **6.30 kN/m²** = PP losa 15 cm (3.75) + PM.adic 2.55 (260 kg/m²) | lámina 700, aula típica |
| q_Q | **2.50 kN/m²** (SC aula 250 kg/m²) | lámina 700 (rango 100–800) |
| α sísmico | **0.10** (paramétrico, `ALPHA_EQ`) | convención curso |

- Rangos completos lámina 700: SC 100–800 kg/m²; PM.adic 200–300 kg/m²;
  PP = e×2500 kg/m³.
- ⚠️ Excluidos semana 1: cargas puntuales de equipos (hasta 13 t) y cargas
  lineales de tabiquería (200–500 kg/m).

## 6. Apoyos

- Empotramiento total (6 GDL) en base −4.21: nodos de retícula, nodos de
  muros y nodo maestro del diafragma base.
- Fundaciones escalonadas más profundas (−8.42 / −9.32) y ala sur (y<0):
  fuera de alcance semana 1 (documentado en README del modelo).

## 7. Dudas abiertas

- [x] Identidad del edificio modelado **CONFIRMADA visualmente por Oscar**
      (23-08): el modelo corresponde al edificio correcto según planos.
- [x] ~~Muros por nivel~~ → par interior M1c-E/M2a constante en pisos 2–4;
      **NUEVA duda:** ¿muros perimetrales ME/MI existen solo en piso 1?
- [ ] Hormigón sobre cielo piso 3° y grado del acero (rotulado no legible vía
      DXF; solo lectura visual en AutoCAD).
- [x] ~~Naturaleza V.M./P.M.~~ → elementos metálicos con pernos Nelson
      (láms. 800–802); sigla exacta es detalle menor.
- [ ] **Geometría fuera del bloque 45×16.15** (hallazgo Sesión 3): franja sur
      en eje I (~10 m al sur de eje 3) y sector NE sobre eje 1 (H→I', hasta
      ~11 m más al norte); fundaciones usan ejes extra 1'/1b/8. ¿Parte del
      mismo edificio o segunda planta de la lámina? Verificación visual.
- [ ] Confirmar fecha/hora exacta de entrega con el profesor.

## 8. Reproducibilidad

- Conversión ODA (orden crítico: recurse ANTES de filtro):
  `"C:\Program Files\ODA\ODAFileConverter 27.1.0\ODAFileConverter.exe" <in> <out> ACAD2018 DXF 0 1 *.dwg`
- DXF disponibles en `%TEMP%\opencode\dxf_out\` (ODA, 38 archivos) y en
  `Desktop\Proyecto 1 - Pablo\planos_dxf\` (LibreDWG; ⚠️ no lee algunos
  rotulados, p.ej. G35).
- Scripts de extracción: en carpeta de Pablo (`scripts/`) y
  `%TEMP%\opencode\` (`titulos_laminas.py`, `muros_por_nivel.py`,
  `materiales_y_muros_full.py`, `scan_hormigones.py`,
  `scan_edificios_101.py`, `scan_ejes_y_101.py`). Usar `.venv` del repo
  (`pip install ezdxf`).
- Modelo canónico: `src/benchmark_3d/` (verificar con
  `pytest tests/test_benchmark.py -v`).
