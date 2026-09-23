# Reporte — Semana 03: casos base, cargas vivas tributarias, sismo pseudoestático
# y superposición (Edificios A y B) + catálogo P–M completo del Edificio B

**Proyecto:** Laboratorio estructural digital 3D — Complejo de Ingeniería
(Edificios A y B).
**Edificios:** A (3D, G35) y B (3D, H30, planos 2024_22).
**Fecha:** Miércoles 16 de septiembre de 2026 — Sesión 10.
**Alcance:** capa **ADITIVA**. No se modifican los modelos lineales A/B, sus IDs,
geometría, casos G/Q/GQ/EX/EY, sismo, tributario ni esfuerzos; todo lo nuevo es
cálculo **sobre** resultados verificados.

---

## 1. Objetivo

Completar la Semana 03 del laboratorio digital:

1. **Casos base G/Q/EX/EY** — reacciones totales y desplazamientos de techo de A y B.
2. **Carga viva tributaria (Q)** — reparto por áreas tributarias y conservación.
3. **Sismo pseudoestático** (α = 0,10) — pesos por nivel, fuerzas F_i, cortante V,
   y desplazamientos/rotaciones (ux, uy, rz) para **A y B**.
4. **Superposición** — verificar G+EX y G+Q+EX como suma de casos (además de la
   G+Q ya validada), en **A y B**, con ≥3 combinaciones.
5. **Catálogo P–M completo del Edificio B** — envolventes de interacción de la
   columna 70×70 y de los **11 muros distintos** (motor de fibras OpenSees),
   asociadas por etiqueta a cada elemento del contrato del visor sólido.
6. **Catálogo P–M del Edificio A (extensión)** — 6 secciones propias (1 columna
   + 5 muros) con **f'c = 30 MPa (G35)** y el supuesto de armado documentado
   (§2.4.1), asociadas a los 111 elementos de A del contrato del visor.
7. Proteger todo con tests sin romper la suite existente (116 passed → 126 hoy).

## 2. Metodología y supuestos

### 2.1 Convenciones generales

- Unidades SI (m, kN, kN/m²). `P > 0` compresión, `M = √(My²+Mz²)` [kN·m].
- Reacciones totales = suma de reacciones de la base (equilibrio ΣF = ΣW verificado).
- Niveles normalizados a enteros: A (base z = −4,21) → 0..4; B (base z = −4,01) → 1..5.
  El techo de B (nivel 5) se compara con el techo de A (nivel 4).

### 2.2 Cargas vivas tributarias (Q)

- **A:** losa `q_losa = {G: 6,3, Q: 2,5} kN/m²`, aplicada sobre área tributaria
  calculada por **triangulación de losa** (grilla E…J / A3…A1 + anexo),
  incluyendo los 2 niveles en voladizo; por tanto ΣQ es **exacta** por
  construcción.
- **B:** `q_losa = {G: 6,3, Q: 3,0, SC_piso: 3,0, SC_cubierta: 1,0}`. La `Q` de
  **diseño** se reparte por **áreas tributarias por viga** (`tributaria_por_viga`,
  vigas + umbral interno), mientras la **solicitación aplicada** al modelo usa el
  área de planta (bbox) por nivel. Se reporta la diferencia.
- Conservación: Σ(transferido por viga) debe ≈ q·(área realmente cargada).

### 2.3 Sismo pseudoestático

- Método: cortante basal `V = α·W` con **α = 0,10** e `W` = peso total
  (G = peso propio + losa + viva media para el sismo, según `pesos_por_nivel`).
- Distribución en altura: `F_i = V · (W_i · z_i) / Σ(W_j · z_j)` con **z absoluta**
  (desde el pavimento −4,2/−4,0 m) para garantizar **paridad A/B**; esto produce
  un `F_1` pequeño y **negativo**, normal y esperado (nivel 1 por debajo del
  centro de masa en altura), que se integra en `ΣF_i = V`.

### 2.4 Catálogo P–M (motor de fibras)

- Metodología idéntica a la sección representativa validada: malla 12×12 + fibra
  por barra, Concrete01 (Hognestad, sin tracción, εc0 = −0,002, εcu = −0,004),
  Steel01 (fy = 420 MPa, Es = 200 GPa, b = 0,01), `curva_PM` con 17 puntos
  (`dk=1e-4`, `nincr=2000`).
- **Cada edificio usa su propio f'c de sección:** las curvas P–M del **Edificio A**
  se calculan con **f'c = 30 MPa (G35)** y las del **Edificio B** con
  **f'c = 25 MPa** (representativa del enunciado); no existe un único f'c
  global. Detalle en §2.4.1 y §2.4.2.
- `P0_ACI = 0,85·f'c·(Ag−As) + fy·As` (referencia); `P0_fibra` de la integración.
- P de cada sección barre desde tracción pura (−fy·As) hasta ≈ +1,03·P0_ACI.
- Overlay: cada pilar/muro del contrato del visor se etiqueta con su sección
  (`col_*` / `murowxh`) y el visor busca **su propia** curva en el catálogo de
  **su edificio** (con fallback `muro`/`col`).

#### 2.4.1 Catálogo del Edificio A (6 secciones) — supuesto documentado

- **f'c = 30 MPa (G35)** y fy = 420 MPa; **recubrimiento de muros 3 cm**.
- Columna 70×70 con **8φ28** (plano 101, capa RLE-PILAR) → `col_A_0.70x0.70`.
- **Armadura de muros adoptada por supuesto (confirmado):** los planos del
  Edificio A **no incluyen la armadura** de los muros estructurales (verificado
  y coincidido con otros grupos). Se adopta la disposición equivalente a la de B:
  - **Alma:** φ10 @ 0,20 m **doble malla** (ρ_v ≈ 0,003).
  - **Borde:** **6φ16 por extremo** sobre `Lb = max(2t, 0,15·L)`.
- Muros (5 secciones `muro_A_*`): ME-32 (0,20×7,25), MI-32 (0,30×7,25),
  MI-12 (0,30×8,90), M1c-E (0,20×6,60) y M2a (0,20×3,70).
- Convención detallada: `reports/supuesto_armado_edificio_A.md`.

#### 2.4.2 Catálogo del Edificio B (12 secciones)

- **f'c = 25 MPa** (representativa del enunciado) y fy = 420 MPa.
- **Supuesto de armado (documentado):** receta uniforme de `muro_b1` — bordes de
  0,40 m con 4φ16 por cara + alma φ12@0,20 por cara, recubrimiento 0,05 m.
  Si un plano contradictorio apareciera, se debería recalcular ese muro.

### 2.5 Superposición

- `G + Q = GQ`: verificada **por componente** (P, My, Mz) sobre los 100
  elementos del Edificio B (el módulo √(My²+Mz²) no es lineal y no superpone).
- `G + EX`, `G + Q + EX`: se compara la **suma de casos individuales** con una
  **corrida directa** del mismo modelo combinado (OpenSees, patrón único
  gravitatorio + patrón 2 sísmico), en reacciones totales y en los
  desplazamientos maestros por nivel, para A y B.

## 3. Materiales (motor de secciones)

| Material | Modelo | Parámetros |
|---|---|---|
| Hormigón | `Concrete01` (Hognestad, sin tracción) | **A: f'c = 30 MPa (G35)** · **B: f'c = 25 MPa (enunciado)**; εc0 = −0,002, εcu = −0,004 |
| Acero | `Steel01` (bilineal con endurecimiento) | fy = 420 MPa, Es = 200 GPa, b = 0,01 |

Cada edificio se verifica contra **su propio f'c de sección**: A con f'c = 30 MPa
(etiqueta de plano G35) y B con f'c = 25 MPa (representativa del enunciado). Los
**modelos lineales** A/B siguen con sus materiales elásticos G35 / H30
(únicamente el catálogo de secciones usa estos valores; sin efecto en §4–§6).

## 4. Casos base G/Q/EX/EY — reacciones y desplazamiento de techo

Equilibrio verificado en todos los casos: **ΣR = ΣF aplicada** (< 0,01 %).

| Edif. | Caso | Aplicada fz [kN] | ΣR fz [kN] | ux techo [mm] | uy techo [mm] | rz techo [rad] |
|---|---|---|---|---|---|---|
| A | G | +45 236,5 | +45 236,5 | +0,507 | +0,023 | +1,57e-06 |
| A | Q | +7 671,2 | +7 671,3 | +0,171 | +0,027 | −1,18e-07 |
| A | GQ | +52 907,7 | +52 907,7 | +0,678 | +0,050 | +1,45e-06 |
| A | EX | fx = +4 523,6 | fx = −4 523,6 | +8,449 | −0,043 | +9,90e-06 |
| A | EY | fy = +4 523,6 | fy = −4 523,6 | −0,044 | +2,343 | −4,33e-05 |
| B | G | +48 171,1 | +48 171,1 | −7,372 | +4,563 | −3,52e-04 |
| B | Q | +11 029,6 | +11 029,6 | −2,268 | +1,366 | −1,08e-04 |
| B | GQ | +59 200,7 | +59 200,7 | −9,640 | +5,929 | −4,60e-04 |
| B | EX | fx = +4 817,1 | fx = −4 817,1 | +15,996 | −8,275 | +6,94e-04 |
| B | EY | fy = +4 817,1 | fy = −4 817,1 | −8,115 | +19,771 | −6,21e-04 |

## 5. Carga viva tributaria (Q)

| Edificio | q_losa Q [kN/m²] | ΣQ repartida por vigas [kN] | ΣQ aplicada [kN] | Conservación |
|---|---|---|---|---|
| A | 2,5 | 7 671,2 (228 vigas) | 7 671,25 | **exacta** (triangulación de losa) |
| B | 3,0 piso / 1,0 cubierta | 10 980,1 (215 vigas; ΣA = 4 223 m²) | 11 029,6 | 99,6 % (bbox vs polígono real) |

- **A:** Q = 2,5 kN/m² sobre 3 068,5 m² → 7 671,25 kN; el reparto por vigas
  transfiere exactamente ese total (grilla + anexo + voladizos sin pérdida).
- **B:** la solicitación aplicada usa el área de planta rectangular
  (≈ 848 m²/nivel → 13·A = 11 029,6 kN); el área **real** cargada (polígono de
  las losas medidas por áreas tributarias) es ≈ 844,6 m²/nivel ⇒ el sello entre
  la huella rectangular y la planta real es solo del **0,4–0,5 %**.
- Figura: `reports/fig/semana03_mosaico.png` (planta B — muros, pilares y vigas
  coloreadas por su carga viva tributaria qG).

## 6. Sismo pseudoestático (α = 0,10) — A y B

Pesos por nivel, fuerzas laterales y cortante basal. `V = 0,10·W` (escala
factorizada para el laboratorio):

| Edif. | Nivel | W_i [kN] | F_i [kN] |
|---|---|---|---|
| A | 1 | 11 011,2 | −9,16 |
| A | 2 | 10 913,2 | +710,12 |
| A | 3 | 11 635,9 | +1 523,97 |
| A | 4 | 11 676,2 | +2 298,72 |
| A | **Σ** | | **45 236,5** | **V = 4 523,6** |
| B | 1 | 9 634,2 | −6,12 |
| B | 2 | 9 634,2 | +478,65 |
| B | 3 | 9 634,2 | +963,42 |
| B | 4 | 9 634,2 | +1 448,19 |
| B | 5 | 9 634,2 | +1 932,96 |
| B | **Σ** | | **48 171,1** | **V = 4 817,1** |

(fz de referencia: plano de las losas; `F_1 < 0` es el efecto del z absoluto;
ΣF_i = V en ambos).

**Desplazamientos y rotaciones por nivel (EX y EY):**

| Edif. | Nivel | ux EX [mm] | rz EX [rad] | uy EY [mm] | rz EY [rad] |
|---|---|---|---|---|---|
| A | 1 | | 1,34e-06 | | −4,71e-06 |
| A | 2 | | 4,04e-06 | | −1,54e-05 |
| A | 3 | | 7,07e-06 | | −2,90e-05 |
| A | 4 | 8,449 | 9,90e-06 | 2,343 | −4,33e-05 |
| B | 1 | | 5,18e-05 | | −4,57e-05 |
| B | 2 | | 1,77e-04 | | −1,57e-04 |
| B | 3 | | 3,40e-04 | | −3,03e-04 |
| B | 4 | | 5,18e-04 | | −4,62e-04 |
| B | 5 | 15,996 | 6,94e-04 | 19,771 | −6,21e-04 |

**Hallazgo (torsión):** la rotación de piso `rz` del Edificio B es **≈ 70× la del
A en el techo bajo EX** y crece con la altura (n1→n4: 38,8 ×, 43,7 ×, 48,2 ×,
52,3 ×; techo B5/A4: **70,07 ×**) y **≈ 14,3 ×** bajo EY. La causa es la
distribución muy excéntrica de la masa/muros (núcleo sur + bloque superior con
pilares sin núcleo) frente al Edificio A, más simétrico y con núcleo central; el
B requiere chequeos de torsión en planta y verificación de derivas amplificadas
(grado de acoplamiento bajo `rz/h`).

## 7. Catálogo P–M del Edificio B (12 secciones)

Envolventes de interacción del Edificio B (compresión positiva; 17 puntos, malla
12×12) con **f'c = 25 MPa** (§2.4.2). `col = col0.70x0.70` (8 φ28); los 11 muros
con el armado uniforme de §2.4.2.

> El catálogo del **Edificio A** (6 secciones: 1 columna + 5 muros, con
> **f'c = 30 MPa (G35)** y el supuesto de armado §2.4.1) se documenta en
> `reports/supuesto_armado_edificio_A.md`; sus envolventes quedan en
> `results/secciones_semana03.json` (`curvas_A`) y en `edificio_solido.json`
> (bloque `secciones` del Edificio A).

| Sección | As [m²] | P0_ACI [kN] | P0_fibra [kN] | P0f/P0a | Balanceado (P,M) [kN, kN·m] | Pico envolvente (P,M) |
|---|---|---|---|---|---|---|
| col0.70x0.70 | 0,004926 | 12 377 | 14 097 | 1,139 | (4 370, 1 158) | (4 379, 1 530) |
| muro0.20x4.10 | 0,005228 | 19 510 | 22 460 | 1,151 | (8 157, 6 961) | (8 917, 11 006) |
| muro0.20x4.50 | 0,005680 | 21 390 | 24 630 | 1,151 | (8 985, 8 317) | (9 787, 13 221) |
| muro0.20x9.44 | 0,011335 | 44 640 | 51 451 | 1,153 | (19 180, 35 027) | (10 997, 50 480) |
| muro0.20x11.59 | 0,013597 | 54 679 | 63 049 | 1,153 | (23 694, 51 972) | (9 668, 66 678) |
| muro0.25x3.12 | 0,004097 | 18 209 | 21 036 | 1,155 | (7 574, 4 863) | (8 491, 7 782) |
| muro0.25x5.80 | 0,007037 | 33 619 | 38 889 | 1,157 | (14 444, 15 909) | (15 791, 26 404) |
| muro0.25x7.95 | 0,009525 | 46 033 | 53 260 | 1,157 | (19 906, 29 480) | (11 984, 43 377) |
| muro0.30x1.45 | 0,002287 | 10 156 | 11 733 | 1,155 | (4 006, 1 319) | (4 736, 2 022) |
| muro0.30x2.64 | 0,003644 | 18 283 | 21 167 | 1,158 | (7 532, 4 095) | (8 628, 6 607) |
| muro0.60x2.91 | 0,003870 | 38 646 | 45 101 | 1,167 | (16 407, 8 705) | (19 065, 15 167) |
| muro0.60x2.92 | 0,003870 | 38 773 | 45 251 | 1,167 | (16 469, 8 760) | (19 131, 15 265) |

- **P0** por fibras ≈ **1,15–1,17× P0_ACI** en todos los muros (hormigón al pico
  f'c en deformación uniforme vs bloque 0,85·f'c): coherente con la columna.
- En muros muy largos el pico de M se alcanza en **baja compresión** (ej.
  0,20×11,59: M_max = 66 678 kN·m con P ≈ 9 700 kN) y el punto balanceado
  simplificado (fibras a εcu/εy) da un M menor: el fibro-solver gobierna.
- Demandas (D/C): 40 columnas + 60 muros etiquetados; solo 2 columnas superan
  D/C = 1,0 (tag 5: 1,13; tag 2: 1,04); muro crítico `muro0.60x2.91` bajo EY,
  (1 974 kN, 9 023 kN·m).
- **Visor:** cada muro muestra **su propia** curva (ver §2.4); la etiqueta por
  elemento está en `per_elemento` y el contrato `edificio_solido.json`.
- Figuras: `reports/fig/semana03_muros_pm.png` (11 envolventes de muro),
  `semana03_muro_pm.png`, `semana03_columna_pm.png`, `semana03_columna_mfi.png`.

## 8. Superposición — 3 combinaciones (A y B)

Verificación numérica **suma de casos vs. corrida directa** (OpenSees) con
solución lineal idéntica; los desvíos reflejan solo ruido numérico del solver.

| Edif. | Combinación | máx ΔR [kN] | máx ΔD [m] |
|---|---|---|---|
| A | G + Q | — (por componente: ΔP = 4,7e-11, ΔM = 7,8e-10) | — |
| A | G + EX | 4,82e-11 | 8,7e-18 |
| A | G + Q + EX | 2,64e-11 | 1,4e-17 |
| B | G + Q | — (por componente sobre 100 elementos) | — |
| B | G + EX | 5,55e-11 | 1,8e-16 |
| B | G + Q + EX | 6,00e-11 | 1,7e-16 |

- Las reacciones totales de cada corrida directa igualan a las de la suma
  (p. ej. B GQ aplicada fz = 59 200,7 = G 48 171,1 + Q 11 029,6).
- Niveles superpuestos vs directos idénticos a `desplazamientos_maestro`
  (desvío ≤ 1,8e-16 m). La linealidad se cumple dentro del ruido del solver.
- ΔD de B (> A) está aún 11 órdenes bajo cualquier tolerancia de diseño.
- Figura: `reports/fig/semana03_superposicion.png`.

## 9. Entregables y reproducción

| Artefacto | Ruta |
|---|---|
| Motor | `src/secciones/` (`materiales`, `seccion`, `curva`, `pm`, `analitica`, `demandas`, `demandas_a`, `catalogo_muros`, `superposicion`, `exportar_unity`) |
| Pipeline + figuras | `scripts/semana03_run.py` |
| Catálogo/superposición | `scripts/semana03_muros_catalog.py`, `scripts/semana03_superposicion.py` |
| Catálogo Edificio A | `scripts/semana03_edificio_A_run.py`, `reports/supuesto_armado_edificio_A.md` |
| Caché de resultados | `results/secciones_semana03.json` |
| Contrato enriquecido | `results/edificio_solido.json` → `StreamingAssets/edificio_completo.json` |
| Visor (Unity) | `UnityStickModel.InteraccionDe` con fallback `el.seccion → muro/col` |
| Tests | `tests/test_secciones.py` (**126 passed** en toda la suite; 116 de B + 10 nuevos de A) |
| Figuras | `reports/fig/semana03_{columna_mfi,columna_pm,muro_pm,muros_pm,superposicion,mosaico}.png`, `semana03_edificio_A_pm.png` |

```powershell
# Pipeline completo: curvas + demandas + §4 + figuras + caché (idempotente)
python scripts\semana03_run.py          # --recalcular fuerza el recálculo
# Catálogo del Edificio A (curvas_A + demandas_A + overlay en ambos edificios)
python scripts\semana03_edificio_A_run.py
# Suite completa
python -m pytest -q                     # 126 passed
# Enriquecer el contrato del visor sólido (invocado automáticamente por complejo.py)
python src\secciones\exportar_unity.py
```

## 10. Limitaciones y trabajo futuro

- `P > 0` compresión y `M = √(My²+Mz²)` conservador; el chequeo biaxial My–Mz
  queda pendiente.
- Armado de muros asumido por supuesto (A: receta §2.4.1; B: receta uniforme
  §2.4.2); validar con planos definitivos. En A está confirmado que los planos
  **no traen la armadura de los muros**.
- f'c de sección diferenciado por edificio: el catálogo del **A usa f'c = 30 MPa
  (G35)** y el del **B usa f'c = 25 MPa** (representativa del enunciado); **ya no
  es "todo a 25 MPa"** (línea corregida de versiones anteriores de este reporte).
  Cada curva de A y B se calcula con **su propio f'c**; el catálogo del B es
  conservador frente a una calidad real superior (H30) y el del A respeta el
  material de plano (G35).
- Torsión del Edificio B: derivas amplificadas y `rz` máx (>70× A en techo bajo
  EX) ameritan chequeo/refuerzo — resultado del modelo, no del motor de secciones.