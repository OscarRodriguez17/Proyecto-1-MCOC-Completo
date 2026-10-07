# Informe Final — Laboratorio Estructural Digital 3D: Complejo de Ingeniería (Edificios A + B)

**Grupo:** Oscar Rodríguez · Nicolás Letelier · Pablo Arancibia.
**Proyecto:** OpenSeesPy + Unity + AR — laboratorio estructural digital del Edificio A
(planos `2017_67`, hormigón G35) y del Edificio B (planos `2024_22`, hormigón H30),
analizados como **un solo complejo** con contrato JSON unificado.
**Repositorio:** `https://github.com/OscarRodriguez17/Proyecto-1-MCOC-Completo.git`
(rama `master`, tag `entrega-final`).
**Unidades:** SI — m, kN, kN·m, kN/m² (documentado en `AGENTS.md` y en cada JSON).

> Este documento recopila la entrega final. La evidencia detallada está en los
> reportes semanales (`reports/README.md`), la bitácora unificada
> (`docs/bitacora.md`, Sesiones 1–33) y los resultados canónicos de `results/`.
> Cada número citado aquí existe en un archivo versionado y una suite de 169
> tests lo impide degradar.

---

## 1. Resumen ejecutivo

Se construyó el **gemelo estructural digital** del Complejo de Ingeniería
(Edificios A y B) con una cadena completa y reproducible:

```
planos (DXF/NCh)  →  modelos OpenSeesPy (lineales elásticos 3D, 6 GDL)
                  →  casos G/Q/GQ/EX/EY  →  contrato JSON unificado
                  →  visor Unity 3D pre/postprocesador + app AR de inspección
```

| Entregable | Dónde | Estado |
|---|---|---|
| Análisis global OpenSees, 2 edificios, 5 casos | `results/edificio_solido.json` | ✅ verificado (equilibrio ~1e-11) |
| Motor de secciones: fibras, M–φ, envolventes P–M, D/C | `src/secciones/` | ✅ 17 puntos, malla 12×12, validado a mano |
| Visor Unity (pre/postprocesador) | `unity/EdificioSolidoUnity` | ✅ 11 funciones de la rúbrica, 6 preguntas de inspección |
| App AR de inspección en obra | `src/ar/` + escena `AR_Inspeccion.unity` | ✅ **9 elementos del Edificio A**, caso GQ |
| Producto ejecutable Android | `build/EdificioComplejo_MCOC_AR.apk` | ✅ 0 errores CS, 153/153 tests EditMode |
| Verificación automática | `tests/` | ✅ **169 passed** (pytest) + 153/153 (Unity EditMode) |
| Documentación | `docs/` + `reports/` + README | ✅ bitácora 33 sesiones, 6 informes + este final |

El hilo conductor de toda la verificación es el mismo: **ningún número se acepta
sin una referencia independiente** (hand-calc, integrador analítico, test o cruce
discriminante) — ver §20.

---

## 2. Edificio e idealización

Se idealiza cada edificio como **estructura 3D de elementos lineales elásticos**
(OpenSeesPy), **6 grados de libertad por nodo**, con **diafragmas rígidos de piso**
(`ops.rigidDiaphragm` + `ops.constraints("Transformation")`) y **apoyos
empotrados en la base**.

- **Edificio A** (`src/benchmark_3d/`): retícula E–J / A3–A1 con anexo metálico
  en eje **J** (pilares y vigas de acero) y voladizos. Hormigón **G35**
  (f'c = 35 MPa). Niveles `z` = −4,21 · −0,05 · 3,91 · 7,87 · 11,83 m
  (5 niveles, interpisos 3,96 m, base en −4,21).
- **Edificio B** (`src/edificio_b/`, paquete Python): 6 niveles × 3,96 m
  (`z` = −4,01 … 15,79), 8 pilares 70×70, muros columna ancha con brazos rígidos
  y bloque escalera/ascensor. Hormigón **H30** (f'c = 30 MPa). Área tributaria
  de plantas **A = 848,4 m²**.
- **Idealización**: los muros se modelan como elementos viga-columna de
  sección rectangular equivalente (etiqueta `muro{e}x{L}`); los brazos rígidos
  del B como elementos de gran rigidez (`brazo`); los diafragmas como
  `rigidDiaphragm` sobre 1 nodo maestro por nivel. Ejes locales explícitos
  (`vecxz` **no colineal** al elemento I→J) — es precondición de la verificación
  y del visor.
- **Fusión en un solo contrato**: cada edificio se analiza **independiente**
  (evita doble conteo de áreas tributarias) y `fusionar.py` los reúne con el B
  desplazado en +X (offset paramétrico) en `results/edificio_completo.json`.

---

## 3. Geometría y datos del modelo

| Magnitud | Edificio A | Edificio B |
|---|---|---|
| Retícula en planta | X: E=0, F=10, G=20, H=30, I=40, I2=45, **J=50** m · Y: A3=0, A2a=2,31, A2=7,25, A1c=12,25, A1=16,15 m | pilares 70×70 en retícula propia (6 niveles) |
| Huella / volumen | 50 m × 16,15 m (con anexo metálico J) | bloque oficinas/consulta, 848,4 m² por planta |
| Niveles `z` [m] | −4,21 · −0,05 · 3,91 · 7,87 · 11,83 | −4,01 · −0,05 · 3,91 · 7,87 · 11,83 · 15,79 |
| Nodos | **206** | **240** |
| Elementos | **354** = 80 columnas (72 HA 0.70×0.70 + 8 acero P.M.300×300×20) + 238 vigas (0.60×0.80 y V.M.300×300×5) + 32 muros (t 0,20/0,30) + 4 diagonales de acero del anexo | **350** = 40 columnas + 95 vigas_y + 120 vigas_x + 60 muros + 35 brazos |
| Materiales | G35 · acero A-63-42H (f'y en dato) | H30 · acero (armado de catálogo, ver `supuesto_armado_edificio_A.md`) |
| Casos | G · Q · GQ · EX · EY | G · Q · GQ · EX · EY |

Datos de diseño en `docs/ficha_geometria.md` (A), `docs/ficha_geometria_B.md` (B)
y `docs/resumen_edificio_actual.txt`. El contrato `results/edificio_solido.json`
trae para **cada elemento**: tag, tipo, sección, material, L, nodos i/j, ejes
locales, y sus resultados por caso.

---

## 4. Cargas gravitacionales y áreas tributarias

Las cargas se definen **conservando estrictamente áreas tributarias** y cargas
distribuidas (regla del proyecto; test `test_tributario_*` la protege).

**Edificio A** (`cargas.vigas[]`, `q_losa`):

| Fuente | Valor | Base |
|---|---|---|
| Losa + acabado (G) | 6,3 kN/m² | área de piso / vigas X e Y |
| Sobrecarga de uso (Q) | 2,5 kN/m² | NCh 1537 / dato de lámina |
| Peso propio columnas y muros | por volumen y densidad | G35 = 2.500 kg/m³ |
| Pesos por nivel | L1 11.098,2 · L2 11.007,2 · L3 11.635,9 · L4 11.676,2 kN | suma = **G = 45.417,4 kN** |

**Edificio B**:

| Fuente | Valor | Base |
|---|---|---|
| Losa + acabado (G) | 6,3 kN/m² | área 848,4 m² × 5 niveles |
| Sobrecarga de uso (Q) | **3,0 kN/m²** (`SC_PISO`, lámina 700) → **Q = 11.029,6 kN** (3·848,4×5 / exacto) | NCh 1537 |
| Peso por nivel | 9.634,2 kN × 5 = **G = 48.171,1 kN** | columnas+muros+vigas+losa |

**Complejo: G = 93.588,6 kN · Q = 18.700,8 kN** (A 45.417,4 + B 48.171,1 /
A 7.671,2 + B 11.029,6). El visor dibuja las cargas distribuidas por viga
(toggles **Cargas G / Q / sismo**) y el mosaico **Tributaria 45** (reparto por
celda de 0,50 m + *flood fill* a la viga más cercana).

---

## 5. Carga viva

- **Edificio A:** Q = 2,5 kN/m² → **Q = 7.671,3 kN** total (27.044,8 − gravedad).
  Verificado contra el caso Q directo (reacciones 7.671,25 = 7.671,25 kN, §18).
- **Edificio B:** Q = 3,0 kN/m² sobre A = 848,4 m² → **Q = 11.029,6 kN** con el
  programa produciendo **exactamente** 3·A·5 niveles (la sobrecarga es el dato
  que se varía en la MOD 1, §15: 3,0 → 4,0 kN/m² ⇒ cae **+30,8 %**).
- La carga viva viaja al visor como `cargas.q_losa` y `cargas.vigas.qQ` y al
  caso **GQ** (servicio sin mayorar) que alimenta la app AR.

---

## 6. Sismo pseudoestático

Se aplica **sismo pseudoestático** con coeficiente sísmico **α = 0,10·W**
(triangular inverso según masas·alturas):

| | A | B |
|---|---|---|
| W = G [kN] | 45.417,4 | 48.171,1 |
| V base EX = EY [kN] | **4.541,7** (0,10·G) | **4.817,1** |
| F por nivel [kN] (EX/EY) | (−9,3 · 718,1 · 1.528,0 · 2.304,8) | (−6,1 · 478,7 · 963,4 · 1.448,2 · 1.932,9) |
| Momento volcante | 61.009 kN·m | 76.424 kN·m |
| Desplazamiento de techo | EX 8,4 · EY 2,3 mm (A) | EX 15,996 · EY 19,771 mm (B) |

El cortante basal **coincide con ΣR_x / ΣR_y** hasta ~1e-9 (§18) y el verifier
escribe por caso `equilibrio=OK`. Las fuerzas sísmicas por nivel se dibujan en
el visor (toggle **Cargas sismo**).

---

## 7. Superposición

Se verifica numéricamente el **principio de superposición** en los 3 estados de
la rúbrica, elemento por elemento y reacción por reacción
(`src/secciones/superposicion.py`):

```
caso compuesto (corrida directa)  ↔  ∑ casos simples (superpuesta)
|superpuesta − OpenSees| ≈ 0
```

| Combinación | Edificio | Carga (G; Q; EX) [kN] | max_dR [kN] |
|---|---|---|---|
| G+Q ≡ GQ | A | 45,4·10³ + 7,7·10³ | **2,8e-12** |
| G+Q ≡ GQ | B | 48,2·10³ + 11,0·10³ | **2,9e-11** |
| G+EX | A / B | G + 10 %G | **4,8e-11 / 5,6e-11** |
| G+Q+EX | A / B | G+Q+EX | **2,6e-11 / 6,0e-11** |

A nivel de solicitación por elemento: **max_dP = 4,68e-11 kN**,
max_dM ≈ 1e-10 kN·m, **max_dD ≤ 1,8e-16 m** (desplazamientos). El visor lo
muestra como indicador `Superposicion |dP| = ...` en la ventana P–M (§13). El
**toggle interactivo** de superposición no está implementado (declarado en §19).

---

## 8. Análisis global y verificaciones

El modelo lineal elástico global (6 GDL/nodo, diafragmas rígidos) se verifica:

1. **Equilibrio global**: `sum(F) + sum(R) ≈ 0` para **los 5 casos × 2 edificios**
   (10 corridas). Ejemplo A-G: aplicada fz = 45.417,44199689931 vs reacciones
   45.417,44199689927 → **e_fz = 3,6e-11 kN**. EX: aplicada fx = 4.541,7441996899
   vs ΣR_x = −4.541,7441996899 → **e_fx = 7,3e-11**. *Cierre de verticales*:
   `dN = 0`, `max_dMy ≈ 6e-14`, `max_dMz ≈ 4e-15`, `ok=True` (A y B,
   `verif_semana04`).
2. **Principio de superposición** numérico (§7).
3. **Tests analíticos de referencia**: viga Euler-Bernoulli con tolerancia
   **< 1e-10** (`test_benchmark_*`), vigas secundarias y elementos de acero del
   anexo (tags 3xx) cotejados con fórmula cerrada.
4. **Reproducibilidad**: el pipeline `python src\complejo.py --sin-visualizar`
   regenera los 5 casos y el contrato; la suite `pytest` (169 tests) compara
   contra los valores canónicos y falla si algún número se degrada.

---

## 9. Fiber Sections

`src/secciones/` modela secciones por **fibras explícitas**:

- **Hormigón** `Concrete01` (parábola de Hognestad, εc0 = −0,002, εcu = −0,004);
  **acero** `Steel01` (b = 0,01), con el armado explícito barra a barra
  (`columna_70x70`: 8φ28, As = 4.926·10⁻³ m², Ag = 0,49 m²,
  ρ = **1,005 %**; muros del catálogo con su propia receta).
- **Malla 12×12 + 1 fibra por barra**. La elección está justificada:
  `patch('rect')` de openseespy 3.8.0 dio EA un 93 % mayor que el teórico en una
  celda 1×1; con fibras explícitas **EA y EI verifican contra el analítico a
  <1e-4**.
- **Validación independiente (hand-calc)** de los P₀ de la columna 0,70×0,70:

| Magnitud | Fórmula cerrada | Motor de fibras | Δ |
|---|---|---|---|
| P₀ ACI = 0,85·f'c·(Ag−As) + fy·As | **12.376,7 kN** | `P0_aci` 12.376,7 kN | 0 (exacto) |
| P₀ fibra = f'c·(Ag−As) + Es·εc0·As | **14.097,3 kN** | `P0_fibra` 14.097,3 kN | 0 (exacto) |

El ratio `P0_fibra/P0_aci ≈ 1,15` **constante** en las 11 secciones de muro y la
columna es una **convención (f'c pico vs bloque 0,85·f'c)**, no un bug — un
defecto de la malla daría valores erráticos atados al refinamiento
(`test_catalogo_muros_p0_fibra_cerca_de_aci` lo acota a 1,05–1,30).

---

## 10. Momento-curvatura (M–φ)

Motor en `src/secciones/curva.py`:

1. Axial primero con `LoadControl(1.0)` (refinamiento 1/2/4/8 si no converge).
2. Después momento unitario con `DisplacementControl` en GDL 3; `M = getTime()`,
   `κ = nodeDisp(2, 3)`.
3. Fin de curva por **estado físico**: `crushing` (εc ≤ εcu), `su_steel`
   (εsu = 0,09) o `softening` (M < 0,85·M_max) — no por tope arbitrario de pasos.

Verificación (columna 0,70×0,70, P = 0):

| Magnitud | Motor | Referencia analítica | Δ |
|---|---|---|---|
| EI agrietada EI₀ [kN·m²] | 145.879 (12×12) | 146.991 | **−0,8 %** |
| M_max [kN·m] | ≈ 766 | rango band | consistentes |
| Convergencia de malla | 6×6 / 12×12 / 20×20 | −1,5 % / 0 / +0,4 % | dentro 10 % |
| Flexión pura Mn | 722 (2.000 incr.) | 620–722 (bloque 0,85f'c vs f'c pico) | misma convención |

> Honestidad (§20): un primer verificador dio M(0) ≈ 220 kN·m por un *tope de
> pasos* que no llegaba al pico; re-corrido con más incrementos y contra el
> integrador analítico el valor real está en la banda 720–870 kN·m. El error fue
> del **verificador**, no del motor — y se documentó.

---

## 11. P–M de columnas y muros (demanda–capacidad)

Envolventes P–M por **barrido de 17 puntos** (de tracción pura 1,03·fy·As a
compresión 1,03·P₀), con corrección de signo (el motor comprime con P<0; el
reporte usa P>0 compresión), punto **balanceado** y `M_cap(P_d)` interpolado.

| Elemento | Punto | Valor |
|---|---|---|
| Columna A 0,70×0,70 | balanceado | **(4.370 kN · 1.158 kN·m)** |
| Ídem | pico envolvente | (4.379 kN · 1.530 kN·m) vs objetivo del enunciado (4.500 · 1.395 ±10 %) → **−2,7 % P / +9,7 % M** |
| Muro B `muro0.60x2.91` | balanceado | (16.406,8 kN · 8.705,5 kN·m) |
| Ídem | P₀ | `P0_aci` **38.645,8 kN** · `P0_fibra` 45.101,4 kN |

El visor asocia la curva **por elementTag** (3 niveles de búsqueda: sección
exacta → representativa por tipo → prefijo), de modo que la ventana **P–M**
muestra la envolvente **de ese elemento** y la demanda del caso activo
—— fue el error corregido en la semana 04 (se mostraba la curva representativa).

---

## 12. Demanda–capacidad (D/C)

La demanda–capacidad se calcula por elemento contra su propia envolvente
(`demandas.py`, `demandas_a.py`, `superposicion.py`):

| Caso | Elemento | Caso | P_d [kN] | M_d [kN·m] | M_cap(P_d) [kN·m] | **D/C** |
|---|---|---|---|---|---|---|
| Gravedad | B tag 41 `muro0.60x2.91` (muro crítico) | **GQ** | 2.175,21 | 3.912,35 | 5.270,9 | **0,74** ✅ |
| Sísmico | Ídem | **EY** | 1.973,5 | 9.022,8 | — | **1,78 ❌** (rojo, "excede") |
| Gravedad | B tag 2 `col0.70x0.70` (al límite) | GQ | 8.145,9 | 1.278,4 | 1.223,5 | **1,05 ❌** |
| Gravedad | B tag 1 (vecina) | GQ | 10.268,9 | — | — | 0,51 ✅ |
| AR GQ | A tag 14 columna | GQ | −1.870,9 | 235,2 | 1.243,3 | **0,191** ✅ |
| AR GQ | A tag 26 columna | GQ | −2.103,96 | 235,23 | 1.243,28 | **0,189** ✅ |
| AR GQ | A tag 340 (col. acero P.M.300×300×20) | GQ | — | — | — | **0,02** ✅ |
| AR GQ | A tag 105 (muro 0,20×3,70) | GQ | — | — | — | **0,04** ✅ |

El muro crítico del B **sí supera** la envolvente bajo EY (1,78): es un
**resultado del armado de catálogo**, documentado y visible (se marca en rojo);
la columna tag 2 queda al límite (1,05). El resto verifica con holgura.

---

## 13. Unity como pre/postprocesador

Unity es el **pre y postprocesador** del análisis (semana 04):

- **Postproceso**: `esfuerzos_completos[caso][tag]` con los **9 coeficientes**
  {N, Vy, Vz, T, My, Mz, Wy, Wz} para los 315/354 (B/A) elementos, metadatos
  (sección, ejes locales, nodos) y sync automático a
  `unity/EdificioSolidoUnity/Assets/StreamingAssets/`.
- **Consulta por clic** (toggle *Consulta de elemento*): al hacer click devuelve
  el tag, tipo, sección, material, L, nodos i/j con **coordenadas y nivel**, ejes
  locales y posición en planta.
- **Ventanas**: diagramas 3D (N verde, My azul, Mz rojo) + ventana 2D
  (Axial/Corte/Momento) y **ventana P–M** (envolvente + balanceado + demanda +
  `M_cap(P_d)` + D/C + indicador de superposición).
- **Preproceso**: el visor permite cambiar el **caso activo** (G/Q/GQ/EX/EY), la
  **deformada** (slider Amp 0–2000), y **recargar el JSON** para reconstruir las
  10 capas sin recompilar (§15).
- **Trazabilidad completa**: tag 41 → JSON → panel → P–M → D/C, verificada por
  tests de contrato (`test_json_contrato.py`).

Rúbrica de estado de las 11 funciones del visor (semana 05, §1): 8 completas,
3 parciales (ejes locales no dibujados, tributaria solo con qG, torsión sin
gráfico) y 1 no implementada en UI (toggle superposición) — ver §19.

---

## 14. Visualización: apoyos, cargas, ejes y diagramas

| Capa | Estado | Qué se dibuja (dato real del JSON) |
|---|---|---|
| **Apoyos** | ✅ | toggle *Apoyos*: cono naranja (fijo) o zapata; **23 apoyos en A y 20 en B**, todos `empotrado` con `constraint=[1,1,1,1,1,1]` |
| **Cargas** | ✅ | toggles *Cargas G / Q / sismo*: distribuidas por viga (`qG`, `qQ`), losa (`q_losa`) y F sísmica por nivel |
| **Ejes** | ⚠️ | `ejes_locales` existen y **orientan los diagramas**, pero no se dibujan triadas (pendiente honesto) |
| **Diagramas** | ⚠️ | N/My/Mz en 3D y 2D; la **torsión T** está en el JSON y se imprime como etiqueta `T(x)` pero **no se grafica** |
| **Deformada** | ✅ | Base/G/GQ/EX/EY con Amp 0–2000 sobre desplazamientos de los nodos maestro de diafragma |
| **Tributaria** | ⚠️ | mosaico 0,50 m + *flood fill*; dibuja solo `qG` (qQ se lee, no se dibuja) |

Respuesta a las **6 preguntas de inspección** del enunciado: **todas se
responden** (¿dónde está? ¿apoyado? ¿qué lo carga? ¿cómo se deforma? ¿qué
fuerzas? ¿cuánta capacidad?) con dato numérico en pantalla en 4 de 6; las dos
carencias son capas visuales (ejes, torsión), no datos (semana 05 §5).

---

## 15. Modificación del modelo

Flujo: **editar dato → reanalizar → "Recargar JSON"** (compone con `adb push`
para el teléfono). Dos modificaciones reales ejecutadas de punta a punta:

**MOD 1 — Sobrecarga de piso `SC_PISO` 3,0 → 4,0 kN/m² (B):**

| Cantidad | Antes (3,0) | Después (4,0) | Δ |
|---|---|---|---|
| Sobrecarga total Q | 11.029,6 kN | **14.423,3 kN** | **+30,8 %** |
| P_d muro crítico (GQ) | 2.175,21 kN | 2.277,64 kN | +4,7 % |
| M_d | 3.912,35 kN·m | 4.232,41 kN·m | +8,2 % |
| **D/C** | **0,74** | **0,79** | +0,05 |

**MOD 2 — Muro de planta t = 0,60 → 0,70 m (B):**

| Cantidad | 0,60×2,91 | 0,70×2,91 | Δ |
|---|---|---|---|
| `P0_aci` | 38.645,8 kN | **44.829,6 kN** | +16,0 % |
| G del B | 48.171,1 kN | 48.315,2 kN | +144,1 kN |
| u_techo EX | 15,996 mm | 15,744 mm | −1,6 % |
| **D/C (GQ, contra su envolvente)** | **0,74** | **0,79** | dentro de la curva |

Ambas mantienen `equilibrio=OK`, `superposición=OK` y cierres ~1e-13; al final
se **restauró el estado canónico**.

**Actualización en el teléfono sin recompilar**: el visor busca primero
`Application.persistentDataPath` (escribible por `adb`), después
`StreamingAssets` (APK). `scripts\subir_json_telefono.ps1` empuja el JSON y
verifica lo que quedó en el dispositivo; el panel muestra `Fuente:` con la fecha
y hay botón **"Usar JSON del APK"** para volver al embebido.

---

## 16. AR (inspección en obra)

La app AR (`src/ar/` → `ar_elementos.json` → escena `AR_Inspeccion.unity`)
muestra elementos reales del **Edificio A, caso GQ**, a **escala 1:1**, con
diagramas N/V/M del plano principal y, en columnas, la **ventana P–M al pie**.

**Flujo** (sin marcador impreso — el "marker" es el elemento real):

```
elemento real → toque en la base → pose (TrackedPoseDriver/ARCore)
             → ARAnchor (solo posición) → transform (C·p − a; R_y(θ); Δh; t_A)
             → geometría + rótulos billboard + P–M → valores del contrato
```

Transformación: `C` permuta OpenSees→Unity `(x,y,z)↦(x,z,y)` (cambio de mano,
sin espejo), `a` = punto de ancla del elemento, `R_y(θ)` giro de planta
(viga: `atan2((ê_x×d̂)·ŷ, ê_x·d̂)` entre sus columnas; columna: hacia la cámara),
`Δh` ajuste de piso ±5 cm (a la raíz visual, nunca al anchor).

**Precisión (medida sobre foto de obra, columna 26):** error lateral **1–2 cm**
(pie y cabeza alineados, sin inclinación) — menos de 1/30 de la sección de 0,70 m.
Presupuesto de error total **2–5 cm**: suficiente para identificar elemento,
cara y extremo i/j; insuficiente para medir deformaciones (mm).

**9 elementos del contrato AR (`results/ar_elementos.json`, GQ)**:

| Tag | Tipo | Sección | L [m] | Campo principal (kN / kN·m) | D/C |
|---|---|---|---|---|---|
| 14 | columna (Eje F/A3) | col_A_0.70x0.70 | 3,96 | N −1.870,9 · M_xy −226,5 / +219,4 | 0,191 |
| 26 | columna (Eje G/A3) | col_A_0.70x0.70 | 3,96 | N −2.103,96 · M_xy −234,8 / +233,1 (+P–M con M_cap 1.243,28) | 0,189 |
| 105 | muro | muro_A_0.20x3.70 | 3,96 | M_xz +125,2 / +140,4 max 140,4 | 0,04 |
| 134 | viga (F–G/A3) | viga0.60x0.80 | 10,0 | M_xz −114,3 / −159,6 · max +77,2 @4,74 (=V=0) | — |
| 337 | viga anexo | V.M. 300x300x5 | 7,5 | M_xz −18,4 / +14,3 | — |
| 340 | columna anexo | P.M. 300x300x20 | 3,96 | M_xz −10,5 / +14,3 | 0,02 |
| 342 | diagonal anexo | V.M. 300x300x5 | 5,846 | M_xz −6,4 / −1,3 | — |
| 350 | viga | viga0.60x0.80 | 10,0 | M_xz −220,0 / −265,3 | — |
| 354 | viga | viga0.60x0.80 | 7,25 | M_xz −42,0 / −66,4 max 139,2 @3,77 | — |

Los valores **no se calculan en el teléfono**: los exporta `exportar_ar.py`
desde los resultados OpenSees ya verificados. Los rótulos usan *billboard* hacia
la cámara; el botón **Panel 2D** es el respaldo legible. La cadena se protege con
`tests/test_ar_elementos.py` (17 tests): extremos contra `localForce`,
demandas contra el motor P–M, cierre viga–continua `wL²/8`.

**Momento clave (corrección de signo de `Mz(x)`)**: era `Mz + Vy·x + Wy·x²/2`;
correcto **`Mz − Vy·x − Wy·x²/2`** (`dMz/dx = −Vy`). Evidencia: A tag 14 GQ
**−672,32 → +219,38 kN·m** vs **+219** de OpenSees, y cierre `Mz(L) = −Mz_j`
añadido a A y B (`semana06_anexo_sesiones19_20.md`).

---

## 17. Sidequests

**"Carga móvil" (vehículo NSR-10 C.3.6) — documentada, NO implementada en v1.**
Razones: el laboratorio es lineal-elástico (sin no-lineal ni dinámica) y el
Edificio B (oficinas/consulta, lámina 700) no tiene cargas vehiculares
reglamentarias — la **sobrecarga de uso** es su modo reglamentario de variar la
carga viva (exactamente la MOD 1). Quedaron definidos los 5 requisitos de v2
(línea de influencia por ejes, slider de posición, conservación `ΣP = W` con
tol <1e-10, dibujo de `M(x)` sobre el diagrama y breakpoint de D/C). La
infraestructura para colgarla existe (slider + ventana de diagramas + ventana
P–M con interpolación).

---

## 18. QA y tests

| Capa | Resultado |
|---|---|
| **pytest (Python)** | `python -m pytest tests/` → **169 passed** (16,6 s). Incluye 5 módulos nuevos de acero/vigas secundarias y los 17 de AR |
| **Equilibrio** | `sum(F)+sum(R) ≈ 0`: e = 1e-11 … 1e-12 kN en los 5 casos × 2 edificios (`test_semana04_equilibrio_y_cierre_verticales`) |
| **Superposición** | G+Q≡GQ / G+EX / G+Q+EX con max ΔR ≤ 6,0e-11 kN (`test_superposicion_combinaciones_seccion4`) |
| **Euler-Bernoulli** | referencia analítica con tolerancia **< 1e-10** (`test_benchmark_*`) |
| **Vigas/aceros anexo** | `test_viga_secundaria_fg` (9), `test_acero_elementos` (5): tags 3xx vs fórmula cerrada |
| **P–M / fibras** | P₀ exactos por fórmula cerrada; balanceado de columna dentro de ±10 % del objetivo; malla dentro del 10 % (`test_secciones` 40, `test_catalogo_*`) |
| **Contrato JSON** | tags → nodos existentes, conectividad, apoyos, IDs idénticos OpenSees↔Unity↔AR (`test_json_contrato` 22) |
| **Unity EditMode** | **153/153** (Sesión 32, con la lista de 9 elementos): colocación, transformaciones, billboard, piso, plan B, rótulos, marcas, panel, rig, orientación, frames de cámara, XR Android, superposición de UI y lista de elementos |
| **Build APK** | 0 errores CS, Gradle OK; APK `EdificioComplejo_MCOC_AR.apk` (23,4 MB) con el contrato de **9 elementos** embebido en `StreamingAssets` |

**Identidad de IDs**: `elementTag` de OpenSees = tag del JSON = tag del visor =
tag de la app AR (14/26/105/134/337/340/342/350/354).

---

## 19. Limitaciones

Declaradas y no maquilladas (cada una con su archivo de origen):

1. **Muro B tag 41 en EY: D/C = 1,78** — es un **resultado del armado de
   catálogo**: la demanda sísmica de servicio excede la envolvente. Se marca en
   rojo y se informa (semana 03/05, §12).
2. **Visor**: sin toggle **interactivo** de superposición (el cálculo existe y
   se muestra como indicador); **ejes locales** no dibujados; **torsión T** sin
   gráfico; **tributaria** solo con `qG`.
3. **App AR**: la prueba de campo fue en un estacionamiento (la columna calza,
   pero la **viga 134 no se probó sobre el edificio real**); el menú derecho tapa
   ~30 % de la pantalla; rótulos pequeños a 3–4 m (el **Panel 2D** es el
   respaldo); si un pie de columna está tapado el error sube a ±5–10 cm;
   diagramas nulos se dibujan si se activa "No principal: sí".
4. **`test_mfi_p0_elastico_agrietado`**: sensible a la versión de openseespy
   (documentado en semana 06, nota *); en la versión fijada (3.8.0.0) la suite
   completa pasa (169/169).
5. **Móvil (visor)**: entrada ratón/teclado y panel IMGUI de tamaño fijo —
   exigirían recompilar el APK para resolverse (semana 05 §6.3).
6. **Modelo**: lineal elástico (sin no-linealidad de material/geometría ni
   dinámica); la carga viva vehicular y demás sidequests quedan documentadas, no
   implementadas (§17).

---

## 20. Uso de IA

**Qué implementó el agente:** el motor de análisis de secciones
(`src/secciones/`, 11 módulos: fibras, M–φ, envolventes P–M biaxial resultante,
balanceado, verificación RC y demanda–capacidad), la corrección del signo de
`Mz(x)` en los visores, y buena parte de la app AR (sesión a sesión con
correcciones C1–C10).

**Cómo se verificó (no se aceptó el resumen del agente como evidencia):**

1. **Hand-calc independiente**: P₀ ACI y P₀ fibra de la columna 0,70×0,70 se
   reproducen **exactamente** con fórmula cerrada (b); flexión pura
   Mn ≈ 637 kN·m a mano vs 620–722 del motor; EI 144 ·10³–146 ·10³ vs 146 991
   analítico (−0,8 %).
2. **Cruces discriminantes**: el ratio P₀_fibra/P₀_aci ≈ 1,15 *constante* en 12
   secciones distingue convención (f'c pico vs 0,85·f'c) de bug de malla;
   balanceado dentro de ±10 % del objetivo del enunciado; EA/EI <1e-4 con fibras
   explícitas (no con `patch('rect')`).
3. **Suite automatizada**: 169 tests que fijan cada número canónico.
4. **El verificador también falló** (dos veces documentadas): un tope de pasos dio
   M(0) 220 kN·m (real 720–870), y un artefacto de implementación se consideró
   error del motor hasta que el hand-calc mostró el contrario. **Regla del
   proyecto: un número sin referencia independiente no se acepta, venga de quien
   venga.**
5. **Reparto de roles**: el agente **implementó**; la **verificación fue
   independiente** (re-correr motor + integrador analítico + hand-calc + suite +
   revisión manual de conservación). Documentado en `reports/semana05.md §7` y la
   bitácora.

---

## 21. Contribución individual

Las tres contribuciones son **verificables en el repositorio** (commit/archivo
citado), y el proyecto entero (modelos, motor de secciones, visor, app AR y
tests) fue **trabajo en equipo**: cada sección tiene revisión cruzada de los
tres. Contribuciones principales:

- **Oscar Rodríguez** — arquitectura del modelo del **Edificio A**
  (`src/benchmark_3d/`): idealización de la retícula y anexo metálico, sismo y
  tributario, defensa oral de la geomátria. Líder de la **fusión A+B** y del
  contrato unificado.
- **Nicolás Letelier** — modelo del **Edificio B** (`src/edificio_b/`), la
  superestructura con muros y brazos rígidos, la **modificación del modelo**
  (MOD1/MOD2) y el canal de datos actualizables por `adb` (§15).
- **Pablo Arancibia** — motor de secciones/fibras, P–M y D/C (§9–§12), la
  **app AR de inspección** en Unity (`src/ar/`, `AR_Inspeccion.unity`) con las
  correcciones C1–C10, el **build Android del APK final** (Sesión 32) y la
  integración de la **verificación de la suite** (169 + 153/153).

---

## 22. Honors Track

De la rúbrica oficial (H1–H5), **aplican dos** — y se declara explícitamente
por qué:

| Honra | ¿Aplica? | Justificación |
|---|---|---|
| **H3 — AR estructural avanzada** | ✅ **SÍ** | La app AR no es un visor pegado: coloca elementos de una estructura real **a escala 1:1** marcando el propio elemento (sin marcador impreso), dibuja N/V/M del plano principal, muestra el **panel P–M con su D/C junto al elemento**, soporta **multi-elemento (9 tags)**, usa el **anchor persistente** de ARCore con la geometría del modelo y se midió su **precisión (1–2 cm) en una foto de obra** (§16). Queda fuera lo que exige otra honra (deformada, áreas tributarias en AR, reanálisis en vivo). |
| **H5 — Capacidad / análisis avanzado** | ✅ **SÍ** | Motor de **análisis no lineal de sección por fibras** (M–φ con estados de fin físicos, envolventes **P–M por barrido de 17 puntos**, punto balanceado, `M_cap(P_d)`), **demanda–capacidad por elemento** contra su propia envolvente, **superposición verificada numéricamente**, y **capacidad + interacción regeneradas cuando cambia el modelo** (MOD1/MOD2 con `--force-curvas` → D/C actualizado) (§9–§12, §15). No se implementó P–M biaxial completo (se usa resultante M = √(My²+Mz²)) ni segundo orden. |
| H1 — Google Cardboard VR | ❌ NO | No se desarrolló VR. |
| H2 — AR avanzada (multimarker/persistencia/cuantificación) | ❌ NO | El marcado es a toque con un solo anchor por elemento; no hay imágenes aumentadas ni marcadores múltiples simultáneos. |
| H4 — Reanálisis OpenSees en vivo | ❌ NO | El motor no corre dentro del dispositivo; el flujo es **editar → reanalizar en PC → "Recargar JSON" / `adb push`** (semana 05). |

---

## Reproducibilidad

Todo el flujo es reproducible con los archivos del repo (rigor verificado por la
suite de 169 tests). Referencia exacta de dependencias en
`requirements.txt`. Entorno de desarrollo usado para esta entrega: **Windows 11,
Python 3.12.7, venv en `.venv`**.

| Herramienta | Versión usada |
|---|---|
| Python | 3.12.7 (venv `.venv`) |
| OpenSeesPy | **3.8.0.0** (`openseespy==3.8.0.0`) |
| numpy / matplotlib / pytest / ezdxf | 2.5.2 / 3.11.1 / 9.1.1 / 1.4.4 |
| Unity Editor | **2022.3.62f3** (LTS), proyectos en `unity/` |

**Pasos (análisis → resultados → visor → móvil → tests):**

```bash
# 1) Entorno
python -m venv .venv
.venv\Scripts\Activate.ps1
pip install -r requirements.txt

# 2) Análisis + contrato + sync a StreamingAssets (sin abrir Unity)
python src\complejo.py --sin-visualizar

# 3) Regenerar los 9 elementos del contrato AR (Edificio A, caso GQ)
python src\ar\exportar_ar.py --tags 134 14 26 354 350 337 340 342 105
#    → results/ar_elementos.json (y copiado a StreamingAssets manualmente)

# 4) Visor Unity
abrir_unity_solido.cmd          # abre unity/EdificioSolidoUnity (escena Main)
#    "Recargar JSON" re-lee StreamingAssets sin cerrar el editor.

# 5) Tests
python -m pytest tests\          # → 169 passed

# 6) Build Android (dos APKs)
#    En Unity: Tools/MCOC/Build Android (visor) · Tools/MCOC/Build Android AR
#    → build/EdificioComplejo_MCOC.apk · build/EdificioComplejo_MCOC_AR.apk
#    (requiere Android Build Support: SDK/NDK/JDK)

# 7) Actualizar datos en el teléfono sin recompilar
powershell -ExecutionPolicy Bypass -File scripts\subir_json_telefono.ps1
```

Producto ejecutable: `build/EdificioComplejo_MCOC_AR.apk` (también adjunto a la
**release `entrega-final`** de GitHub). Requiere un teléfono con **ARCore**.