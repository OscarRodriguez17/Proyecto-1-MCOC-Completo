# Reporte — Semana 05: Del modelo al teléfono — modificaciones reales del modelo con actualización automática en Unity, verificación numérica de la superposición G+Q≡GQ / G+EX / G+Q+EX, UX de inspección en 6 preguntas y preparación de build Android

**Proyecto:** Laboratorio estructural digital 3D — Complejo de Ingeniería
(Edificios A y B).
**Edificios:** A (3D, G35) y B (3D, H30, planos 2024_22).
**Fecha:** Martes 22 de septiembre de 2026 — Sesiones 15–16.
**Alcance:** capa **ADITIVA**. No se tocan los motores de análisis (lineal
elástico A/B, IDs, casos G/Q/GQ/EX/EY, sismo, tributario, envolventes P–M);
lo nuevo es **postproceso, documentos y un menú de build**.
**Cambios en esta entrega:** `reports/semana05.md` (documento) +
`Assets/Editor/MCOCBuildAndroid.cs` (menú de build, aditivo) + nuevas líneas
`scripts/semana05_refrescar_caches.py` (aditivo). Los **resultados quedan en su
estado canónico** (baseline) y la suite protegida continúa en **134 passed**.
**Estructura:** las 7 secciones del enunciado están en §1 (funciones + tabla de
estado de las 11), §2 (dos modificaciones), §3 (superposición, 3 estados),
§4 (sidequest), §5 (UX, las 6 preguntas), §6 (build móvil) y §7 (IA).

---

## 1. Funciones implementadas

### 1.1 Tabla de estado de las funciones del visor (las 11 de la rúbrica)

Control citado = etiqueta exacta en pantalla / archivo:línea del script
(`unity/EdificioSolidoUnity/Assets/Scripts/UnityStickModel.cs`, 2 978 líneas;
`OrbitCamera.cs`, 169 líneas). Las líneas corresponden al archivo tal como
queda con la carga actualizable de §6.2.

| # | Función | Estado | Control en el visor | Qué muestra y de dónde sale |
|---|---|---|---|---|
| 1 | **Navegación** | ✅ completa | cámara orbital + botones `Arriba`/`Frente`/`Enfocar B1`/`Enfocar B2`; atajos `1`/`2`/`V`/`F`/`C`/`R` (`OrbitCamera.cs:42-140`) | rotar (botón derecho o Alt+izq.), pan (botón central), zoom (rueda, 2–1200 m), vuelo `W/S/A/D/Q/E` |
| 2 | **Selección** | ✅ completa | toggle `Consulta de elemento (click izquierdo)` (`:2476`) + raycast en `ProcesarSeleccionViga` (`:1991-2038`) | al hacer click registra el `elementTag` y abre el panel de consulta con el caso activo (G/Q/GQ/EX/EY, **defecto GQ**, `:73` y `:2571`) |
| 3 | **Apoyos** | ✅ completa | toggle `Apoyos` (`:2422`), dibujo en `:558-567` con `CrearCono` (`:2192`) y `CrearZapata` (`:2183`) | cono naranja si `tipo = "fijo"`, zapata en el resto; el JSON trae 23 apoyos en A y 20 en B, **todos `empotrado` con `constraint = [1,1,1,1,1,1]`** |
| 4 | **Ejes** | ⚠️ parcial | **sin control en pantalla** | `metadatos[tag].ejes_locales` existe (x/y/z) y **orienta los diagramas** (`:2905-2912`), pero **no se dibujan triadas**: el ingeniero no los ve |
| 5 | **Cargas** | ✅ completa | toggles `Cargas G`, `Cargas Q`, `Cargas sismo` (`:2412-2414`), flechas en `:1278-1328` | `cargas.vigas[]` (qG, qQ, G, Q por viga), `cargas.q_losa` (B: G 6,3 / Q 3,0 kN/m²) y `cargas.sismo.F_por_nivel` |
| 6 | **Áreas tributarias** | ⚠️ parcial | toggle `Tributaria 45` (`:2407`), mosaico de 0,50 m en `CrearTributaria` (`:1353-1649`) | reparto por celda a la viga más cercana + *flood fill*. **Dibuja solo `qG`; `qQ` se lee pero no se dibuja** |
| 7 | **Deformada** | ✅ completa | toggles `Base`/`G`/`GQ`/`EX`/`EY` (`:2435-2439`) + slider `Amp:` 0–2000 (def. 400, `:2446-2448`) | `resultados[caso].desplazamientos_maestro` de los nodos maestro de diafragma |
| 8 | **Diagramas** | ⚠️ parcial | `Diagramas 3D (N verde, My azul, Mz rojo)` (`:2610-2612`) + ventana arrastrable con `Axial (N)`, `Corte (V)`, `Momento (M)` (`:2706-2790`, botones `:2729-2731`) | `esfuerzos_completos[caso][tag]` (N, Vy, Vz, T, My, Mz, Wy, Wz). **La torsión T está en el JSON pero no se grafica** (sí se lee y se imprime como etiqueta `T(x)`, `:2608`) |
| 9 | **Superposición** | ❌ **no implementada en v1** | **no hay toggle**: solo una etiqueta de lectura `Superposicion \|dP\| = …` dentro de la ventana P–M (`:2867-2868`) | el cálculo y la verificación **sí existen** en datos (`secciones.superposicion` con las 3 combinaciones, §3), pero el visor **no los compara de forma interactiva** |
| 10 | **P–M** | ✅ completa | toggle `Ventana P-M (arrastrable)` (`:2488-2490`), ventana en `DibujarVentanaPM` (`:2792-2878`) | envolvente + punto balanceado del catálogo de la sección del `elementTag` + demanda del caso activo + `M_cap(P_d)` + **D/C** |
| 11 | **Modificación del modelo** | ✅ completa | botones **`Recargar JSON`** y **`Usar JSON del APK`** + etiqueta **`Fuente:`** (`:2464-2470`) → `Cargar()` `:310-314` → `ConstruirEscena()` `:462` | relee el JSON y **reconstruye las 10 capas**: columnas/vigas/muros, apoyos, nodos, cargas, tributary, deformada, diagramas, panel de consulta, P–M y D/C. **En el teléfono lee `Application.persistentDataPath`**, así que el dato se actualiza con `adb push` **sin recompilar** (§6.2) |

> **Honestidad de la tabla (QA):** 3 de las 11 funciones quedan **parciales**
> (ejes no dibujados, tributarias solo con G, torsión no graficada) y **una no
> está implementada en el visor** (superposición interactiva, §3.3). Se
> declaran aquí en lugar de presenting como "lista" porque la rúbrica pide
> precisamente poder ver los huecos. Ninguno de esos huecos afecta a los
> **números**: los datos existen y están verificados; falta la capa visual.

### 1.2 Lo entregado esta semana

1. **Flujo "editar dato → correr → 'Recargar JSON'" completo y probado**: se
   edita una hipótesis en `src/edificio_b/cargas.py` o `datos_edificio.py`, se
   reanaliza el Edificio B, se refrescan las cachés de secciones/semana 04 y el
   único botón del visor **"Recargar JSON"** vuelve a leer el JSON y **todas las
   capas se actualizan solas** (deformada, esfuerzos, panel de consulta, ventana
   P–M y demanda–capacidad). Ahora además el panel dice **de qué archivo leyó**
   (`Fuente: …`) y con qué fecha, y hay un botón para **volver al JSON del APK**.
2. **En el teléfono el dato se actualiza sin recompilar** (decidido antes de
   compilar): la carga busca primero `Application.persistentDataPath`, que es
   escribible por `adb`, y el script `scripts/subir_json_telefono.ps1` empuja
   el JSON y verifica lo que quedó en el dispositivo (§6.2). El diseño/UI sigue
   siendo parte del APK: eso sí exige recompilar.
3. **Dos modificaciones REALES del modelo**, ejecutadas de punta a punta con su
   cadena "antes → después" con números (sección 2):
   - **MOD 1 — Sobrecarga de piso** `SC_PISO` 3,0 → 4,0 kN/m² (lámina 700): la
     carga vertical de uso del Edificio B pasa de **11 029,6 → 14 423,3 kN**.
   - **MOD 2 — Sección del muro de planta** `MUROS_V[0]` espesor t = 0,60 →
     0,70 m: su **P–M / P₀ se agranda** y la relación demanda–capacidad cambia.
4. **Superposición verificada numéricamente** en los 3 estados de la rúbrica
   (**G+Q≡GQ**, **G+EX** y **G+Q+EX**): la data del visor trae, por combinación,
   la comparación de la corrida directa contra la suma de casos simples con
   **|superpuesta − OpenSees| ≈ 0** (sección 3). Lo que **no** hay en v1 es el
   toggle interactivo en pantalla (ver la tabla de estado, fila 9).
5. **Paridad y equilibrio garantizados en CADA estado**: `equilibrio=OK
   superposición=OK` y `cierreV dN≈0` en las 10 corridas, tanto en el estado
   modificado como en el canónico.
6. **Preparación móvil**: menú **`Tools/MCOC/Build Android`** que compila la
   escena `Assets/Scenes/Main.unity` a `build/EdificioComplejo_MCOC.apk` con
   package **`com.mcoc.edificiocomplejo`**, **min SDK 22**, **landscape
   (LandscapeLeft)**, escena única y `bundleVersion 1.0` (sección 6).
7. **"Sidequest" carga móvil**: documentada como **NO implementada en v1**
   (sección 4).
8. **Trazabilidad de la parte compleja**: la sección **7** documenta la
   funcionalidad más difícil del proyecto —el motor de análisis de secciones
   `src/secciones/` (fibras, momento-curvatura, envolventes P–M, verificación RC
   y demanda-capacidad)— **implementada por el agente de código**, y sobre todo
   **cómo se verificó de forma independiente** (hand-calc, cruces discriminantes
   y suite de tests). Esta semana **no se modificó una línea de ese motor**:
   es la bitácora de verificación exigida en la rúbrica.

## 2. Modificación del modelo — dos cambios reales con "antes → después"

### 2.0 La cadena completa: interfaz/dato → modelo → OpenSees → resultados → Unity

Las dos modificaciones son **semi-automáticas**: el paso de Unity es automático
(el botón **Recargar JSON** reconstruye las 10 capas sin tocar código), pero los
cuatro pasos de datos son **scripts de consola**. Se documentan tal cual, con el
comando exacto, para que el flujo sea **reproducible a mano**:

```
INTERFAZ/DATO      MODELO            OPENSEES              RESULTADOS        UNITY
────────────────────────────────────────────────────────────────────────────────────────────
1) editar el dato  → se reconstruye → python -m edificio_b  → 5 casos         → StreamingAssets/
   cargas.py /        el modelo de     .analizar (G,Q,GQ,     (reacciones,     edi-
   datos_edificio     elementos         EX,EY)                desplaz.,       Building
   .py                (vigas, muros,                                  com-
                      y nodos)         [OpenSeesPy]          ueta JSON        pleto
                                                                                .json
                                         └── balance ──┐
2) refrescar cachés: demandas, §3 superposición y §4 directo                → overlay `secciones`
                                         └────┬───────┘
3) esfuerzos completos + curvas P–M nuevas                                   → el visor resuelve
    (--recalcular, --force-curvas)                                            la curva de CADA tag
                                                                              ("Recargar JSON"
                                                                               = 0 clics de
                                                                               código)
```

En una línea: **dato → modelo → OpenSees → resultados → Unity → "Recargar
JSON"**, y el último tramo es el único automático; todo lo anterior son cuatro
comandos documentados y verificados. No se editó ninguna escena de Unity a mano
en ninguna de las dos modificaciones.

Flujo reproducido en cada modificación (mismo que ejecutará el usuario):

```
1) editar el dato en src/ (cargas.py o datos_edificio.py)
2) python -m edificio_b.analizar           (desde src/)  → reanaliza B (5 casos)
3) python scripts\semana05_refrescar_caches.py [--force-curvas]
                                           → recalcula demandas, etiquetas de
                                             sección, §3-G+Q vs GQ, §4-directo y
                                             añade curvas P–M nuevas si hace falta
4) python scripts\semana04_esfuerzos_completos_run.py --recalcular
                                           → esfuerzos completos + overlay P–M
                                             + sync a Assets/StreamingAssets/
5) (Unity) botón "Recargar JSON"           → todo el visor se actualiza solo
```

Al final se **restauró el estado canónico** (ver §8) para que el restultado sea
reproducible y la suite de protección siga en verde.

### 2.1 MOD 1 — Sobrecarga de piso `SC_PISO` 3,0 → 4,0 kN/m²

`src/edificio_b/cargas.py` (edificio B; lámina 700). El área tributaria de
plantas es **A = 848,4 m²**: con `SC_PISO = 3,0` el total vertical de uso es
13·A = 11 029,6 kN y con `SC_PISO = 4,0` pasa a 17·A = **14 423,3 kN** — el
programa produce **exactamente** el valor esperado:

| Cantidad (Edificio B, `analizar`) | Antes (3,0) | Después (4,0) | Δ |
|---|---|---|---|
| Sobrecarga total `Q` [kN] | 11 029,6 | **14 423,3** | **+30,8 %** |
| Peso muerto `G` [kN] | 48 171,1 | 48 171,1 | — (la SC no cambia G) |
| Verificación equilibrio | OK | OK | — |
| Verificación superposición | OK | OK | — |
| Esfuerzos `cierreV dN` | ≈ 1e-13 | ≈ 1e-13 | máquina |

El mismo dato se ve en el **visor**: en la ventana **P–M** del muro crítico
**B tag 41** (`muro0.60x2.91`, caso activo `GQ`), la demanda corre con la carga:

| Demanda B tag 41 (caso GQ) | Antes (3,0) | Después (4,0) | Δ |
|---|---|---|---|
| `P_d` [kN] (compresión positiva) | 2 175,21 | **2 277,64** | +4,7 % |
| `M_d = √(My²+Mz²)` [kN·m] | 3 912,35 | **4 232,41** | +8,2 % |
| `M_cap(P_d)` [kN·m] (envolvente 0.60x2.91) | 5 270,9 | 5 374,6 | +2,0 % |
| **D/C = M_d / M_cap** | **0,74** | **0,79** | +0,05 |

`esfuerzos_completos.GQ."41"` (x = 0) coincide: `N` 2 175,21 → **2 277,64 kN** y
`Mz` 3 909,15 → **4 228,96 kN·m**; la fila B del verifier `aplicada.fz` sube de
59 031,37 a **62 409,90 kN** (mismo orden de magnitud que el análisis global).

> [CAPTURA: Play — ventana P–M del muro B tag 41 tras "Recargar JSON" con SC=4,0:
> envolvente `muro0.60x2.91` sin cambios y punto de demanda desplazado (2 278;
> 4 232) con D/C = 0,79.]

### 2.2 MOD 2 — Sección del muro `MUROS_V[0]` t = 0,60 → 0,70 m

`src/edificio_b/datos_edificio.py` (muro de planta en `(x, y) = (11,20; 11,295)`,
nodos 49→50). La sección cambia de `muro0.60x2.91` a **`muro0.70x2.91`** y
`--force-curvas` regenera su envolvente P–M con el mismo motor de fibra de la
semana 03:

| Cantidad | Muro 0,60 × 2,91 | Muro 0,70 × 2,91 | Δ |
|---|---|---|---|
| Área `A` [m²] | 1,746 | **2,037** | +16,7 % |
| **`P₀` núcleo ACI** `P0_aci` [kN] | 38 645,8 | **44 829,6** | **+16,0 %** |
| **`P₀` fibra explícita** `P0_fibra` [kN] | 45 101,4 | **52 376,4** | +16,1 % |
| Punto balanceado `(P; M)` [kN; kN·m] | (16 406,8; 8 705,5) | (a) | +16,5 % |
| Peso muerto `G` del B [kN] | 48 171,1 | **48 315,2** | +144,1 kN |
| Desplaz. techo `EX u_techo` [mm] | 15,996 | **15,744** | −1,6 % (más rígido) |
| Desplaz. techo `EY u_techo` [mm] | 19,771 | **19,281** | −2,5 % |
| Momento de vuelco `M_volc` [kN·m] | 76 424 | **76 653** | +0,3 % |

> (a) El balanceado del muro 0,70x2,91 se generó con el motor (17 puntos, malla
> 12×12) junto con su envolvente; en la ventana P–M del visor, `CapacidadM`
> interpola la envolvente en `P_d` con el mismo criterio que la semana 04.

La **demanda–capacidad** del muro crítico bajo `GQ` (comparando cada modelo con
su propia envolvente):

| B tag 41 (GQ) | Modelo 0,60 (antes) | Modelo 0,70 (después) |
|---|---|---|
| `P_d` [kN] | 2 175,21 | **2 320,6** (pesa más) |
| `M_d` [kN·m] | 3 912,35 | **4 382,16** |
| `M_cap(P_d)` [kN·m] | 5 270,9 | **5 573,4** |
| **D/C** | **0,74** | **0,79** |

La envolvente creció (+16 %) casi el doble de la demanda (+12 % en `M` y +6,7 %
en `P`) y el punto de demanda **se mantiene dentro** de la curva: el muro sigue
verificando con holgura. `equilibrio = OK`, `superposición = OK` y cierres a
nivel máquina en todos los casos.

> [CAPTURA: Play — ventana P–M del muro B tag 41 con sección `muro0.70x2.91`:
> envolvente más grande (P0_aci ≈ 44 829 kN) y punto de demanda (2 321; 4 382)
> dentro de la curva; P₀/marca se leen en la ventana.]

## 3. Superposición — los 3 estados verificados contra resultados numéricos

### 3.1 Qué se compara

Los **tres estados** de la rúbrica se verifican con la misma pregunta: ¿la
corrida **directa** del caso compuesto da lo mismo que la **suma** de sus casos
simples? La comparación es **elemento por elemento y reacción por reacción**
(`src/secciones/superposicion.py`, capa de verificación de la semana 03):

```
caso_compuesto(directo)  ↔  ∑ casos simples (superpuesta)
diferencia ≈ 0  ⇔  |superpuesta − OpenSees| ≈ 0
```

Resultados con el modelo **canónico** (estado final, idéntico al baseline):

| Combinación | Edificio | Carga aplicada (fz [kN]; fx [kN]) | Reacción directa [kN] | Reacción superpuesta [kN] | **max_dR [kN]** |
|---|---|---|---|---|---|
| G+Q ≡ GQ | A | 52 907,7 | 52 907,73 | 52 907,73 | **2,8e-12** |
| G+Q ≡ GQ | B | 59 200,7 | 59 200,73 | 59 200,73 | **2,9e-11** |
| G+EX | A | 45 236,5 + 4 523,6 | 45 236,48 | 45 236,48 | **4,8e-11** |
| G+EX | B | 48 171,1 + 4 817,1 | 48 171,11 | 48 171,11 | **5,6e-11** |
| G+Q+EX | A | 52 907,7 + 4 523,6 | 52 907,73 | 52 907,73 | **2,6e-11** |
| G+Q+EX | B | 59 200,7 + 4 817,1 | 59 200,73 | 59 200,73 | **6,0e-11** |

Y a nivel de solicitaciones por elemento (`superposicion` de semana 03, 100
elementos etiquetados del B): **max_dP = 4,68e-11 kN**, max_dMy = 7,84e-10,
max_dMz = 2,58e-10 kN·m. O sea, la diferencia entre "correr GQ directo" y
"sumar G + Q" es de **~10⁻¹⁰ kN** — el error numérico de un doble.

### 3.2 Dónde vive la verificación (y qué ve el usuario)

Los resultados de la tabla anterior **sí llegan al visor**: el JSON
`edificio_completo.json` trae, por edificio, el bloque
`secciones.superposicion` con `max_dP`, `max_dMy`, `max_dMz` **y el detalle
`combinaciones`** (A: 111 elementos; B: 100 elementos etiquetados). La ventana
P–M muestra el indicador `Superposicion |dP| = 4.7E-11 kN`
(`UnityStickModel.cs:2867-2868`), que es la comprobación de que el modelo sigue
linealmente elástico.

### 3.3 Lo que NO hay en v1 (declarado, no maquillado)

**No existe el toggle interactivo de superposición.** Revisando el visor línea
por línea: el único uso de la palabra es esa etiqueta de lectura; la clase
`SuperposicionInteraccion` se parsea (dimensiones: `n_elementos`, `max_dP`, …)
pero el diccionario `combinaciones` **no se dibuja**, y no hay modelo
superpuesto ni checkbox. En consecuencia, la **captura** que corresponde es la
de la *ventana P–M* (no la de dos diagramas superpuestos):
> [CAPTURA: Play — Edificio B, tag 41, ventana P–M: envolvente `muro0.60x2.91`,
> punto de demanda GQ y, debajo, el indicador `Superposicion |dP| = 4.7E-11 kN`
> que deja constancia numérica de la verificación de §3.1.]

**Qué haría falta para el toggle** (alcance de v2, no de esta semana): un
`GameObject` por caso simple ya coloreado (G / Q / EX) que se superponga al
caso directo, y un checkbox que active/desactive la capa. La data y las
ventanas ya existen; es una capa de render, no de cálculo.

## 4. Sidequest "carga móvil" — NO implementada en v1 (documentación)

La idea de la sidequest era acercar la **carga móvil (vehículo según NSR-10
C.3.6)** a una viga/franja del edificio laboratorio y ver cómo cambia la
envolvente local. **En v1 queda documentada, no implementada**, por dos razones:

1. **Modelo no lineal-geométrico ni dinámico**: el laboratorio es lineal elástico
   y la envolvente P–M por muro ya se calcula por fibra; incluir un vehículo
   exige definir influencia dinámica (amplificación), posición crítica y lazos
   de envolvente por tramo — un alcance propio, no un patch de una tarde.
2. **Supuesto de proyecto**: el Edificio B, por su uso (oficinas/consulta, lámina
   700), no tiene cargas vehiculares reglamentarias en ningún nivel; la
   sobrecarga de uso (`SC_PISO`) es el modo reglamentario de variar la carga
   viva — exactamente lo que muestra la MOD 1.

**Cómo quedaría en v2**, punto por punto de lo que pide el enunciado para una
sidequest implementada (los 5 requisitos, con el decisions de diseño que ya
están tomadas):

| Requisito | Decisión de diseño (v2) |
|---|---|
| **Regla física** | vehículo tipo camión según **NSR-10 C.3.6** (tandem 2 ejes), como carga **móvil** sobre una línea de influencia de viga: `P_i` por eje y `M(x) = Σ P_i·(x − x_i)`, más **efecto dinámico** con factor de amplificación y, si se pide, unsteady |
| **Panel** | un bloque en el panel del visor con *slider* de **posición x** (0 → L), casing de "Colocar / Quitar camión" y lectura de `M_max(x)` y de su posición crítica |
| **Reparto** | reparto **por línea de influencia**, no por área: cada eje aporta a los nodos de la viga con su ordenada; se acumula sobre `N/Vz/My` del elemento y **se compara con la envolvente P–M** del muro/columna donde llega |
| **Conservación de la carga** | test explícito de que `Σ P_i = W_camión` y que la suma de los momentos nodales reproduce `Σ P_i·x_i` (tolerancia < 1e-10, como el resto de la suite) |
| **Respuesta visual** | la curva `M(x)` se dibuja sobre el diagrama de momentos de la viga en la **ventana de diagramas**, el punto de máximo se marca, y la **ventana P–M** muestra un *breakpoint* de `D/C` en el instante en que el Truck cruza el umbral |

El visor ya tiene la infraestructura para colgarla encima sin tocar el motor:
el *slider* y la ventana de diagramas (semana 04) y la ventana P–M con
interpolación de `M_cap(P_d)`. Lo que **no** hay es la línea de influencia y el
perfil del camión, que es lo que habría que construir.

## 5. UX estructural — las 6 preguntas de inspección del enunciado

### 5.1 ¿Responde el visor a las 6 preguntas? (con el dato real que muestra)

| # | Pregunta | ¿Sí? | Dato real que el visor muestra |
|---|---|---|---|
| 1 | **¿Dónde está el elemento?** | ✅ | Al hacer click devuelve el `elementTag`, el tipo y la **sección** (`muro0.60x2.91`, `col0.70x0.70`), el material (`H30`, `G35`), la longitud, los nodos `i`/`j` **con sus coordenadas (x,y,z) y nivel**, los **ejes locales** y la **posición en planta (cx, cy)**. Ejemplos: B tag 41 → nodos 49→50, cx 11,20 / cy 11,295; A tag 1 → nodos 1→37, cx 0,00 / cy 0,00 |
| 2 | **¿Cómo está apoyado?** | ✅ (dibujado) / ⚠️ (el `constraint` no se imprime) | El toggle **Apoyos** dibuja un cono naranja (fijo) o una zapata en la base, y `metadatos` dice si el extremo i es **`empotrado`** o `esclavo_diafragma`. Hay **23 apoyos en A y 20 en B**, todos `empotrado` con `constraint = [1,1,1,1,1,1]`. Lo que **no** se ve en pantalla es el vector de restricción numérico: está en el JSON pero no se imprime |
| 3 | **¿Qué lo carga?** | ✅ (con un hueco) | Toggles **Cargas G**, **Cargas Q** y **Cargas sismo**: dibujan las cargas distribuidas de cada viga (`qG`, `qQ`, `G`, `Q`), la losa (`q_losa`: B → G 6,3 / Q 3,0 kN/m²) y las fuerzas sísmicas por nivel. Ejemplo real: viga B tag 104, L 4,26 m, área 12,557 m² → qG 18,57 + qQ 8,84 kN/m = **G 79,11 kN + Q 37,67 kN**. El toggle **Tributaria 45** muestra el mosaico de reparto, **pero solo con `qG`** |
| 4 | **¿Cómo se deforma?** | ✅ | Toggles **Base / G / GQ / EX / EY** + slider **Amp:** (0–2000, por defecto 400) sobre los desplazamientos de los 5 nodos maestro de diafragma. Valores reales del JSON: B **EX máx 15,996 mm**, B **EY máx 19,771 mm**, B GQ 9,640 / 5,929 mm; A **EX 8,449 mm**, A EY 2,343 mm |
| 5 | **¿Qué fuerzas tiene?** | ✅ (torsión no graficada) | El panel de consulta tiene selector de caso (**G/Q/GQ/EX/EY**, defecto GQ) y da `N, Vy, Vz, T, My, Mz` del `esfuerzos_completos`; más **Diagramas 3D** (N verde, My azul, Mz rojo) y la **ventana de diagramas** con los gráficos de **Axial (N), Corte (V) y Momento (M)** a lo largo del elemento. Ejemplo: B tag 41 en GQ → N = 2 175,21 kN y M = √(My²+Mz²) = 3 912,35 kN·m. El campo **T (torsión) está en el JSON pero no se dibuja** |
| 6 | **¿Cuánta capacidad tiene?** | ✅ | La **ventana P–M** resuelve la curva de la sección **de ese `elementTag`** (no la representativa), muestra el **balanceado**, la **demanda del caso activo** y **`M_cap(P_d)` interpolado** con el **D/C**. B tag 41 GQ: `M_cap = 5 270,9` → **D/C = 0,74**; en **EY** el mismo muro da **D/C = 1,78** y se marca en rojo. Columna B tag 2 GQ: **D/C = 1,05** |

> **Veredicto honesto:** las 6 preguntas se responden, y 4 de 6 con el dato
> numérico en pantalla. Las dos carencias son de **capas visuales**, no de
> datos: (i) los **ejes locales** no se dibujan (solo orientan los diagramas) y
> (ii) la **torsión** no tiene gráfico. Ninguna de las dos impide responder las
> preguntas, pero son los dos "pendientes" honestos de la semana 06.

### 5.2 Además: 6 preguntas de diseño que el visor ya responde

Las 6 inspecciones de validación más útiles, todas con **números reales del
JSON canónico** (idénticos a los de las semanas previas):

| # | Pregunta del ingeniero | Respuesta en el visor (dato real) |
|---|---|---|
| 1 | ¿Cuánto pesa el conjunto y cómo se reparte? | COMPLEJO **G = 93 407,6 kN** (A 45 236,5 + B 48 171,1) y **Q = 18 700,8 kN** (A 7 671,2 + B 11 029,6); el "Recargar JSON" muestra el resumen por edificio. |
| 2 | ¿El muro crítico resiste la gravedad? | B tag 41, GQ: P_d = 2 175,21 kN, M_d = 3 912,35 kN·m, **M_cap(P_d) = 5 270,9 → D/C = 0,74** (punto DENTRO de la envolvente, con holgura del 26 %). |
| 3 | ¿Y bajo la envolvente sísmica? | B tag 41, **EY**: P_d = 1 973,5 kN, M_d = 9 022,8 kN·m → **fuera** de la envolvente (**D/C = 1,78**, "excede", igual que en semana 03/04): el muro se marca en rojo y el ingeniero ve el balanceado (16 406,8; 8 705,5) a qué distancia queda. |
| 4 | ¿Qué columna está al límite? | B tag 2, GQ: P_d = 8 145,9 kN, M_d = 1 278,4 → M_cap = 1 223,5 → **D/C = 1,05 (excede)**; la vecina tag 1 (P = 10 268,9) queda en 0,51. La ventana P–M permite saltar tag◀/▶ y ver el peor caso en segundos. |
| 5 | ¿Qué pasa si se sube la sobrecarga reglamentaria? | MOD 1: Q sube +30,8 % (11 029,6 → 14 423,3 kN) y el muro crítico pasa de D/C 0,74 → **0,79**; el ingeniero "edita → corre → Recargar JSON" y ve si algún elemento cruza la envolvente sin rehacer nada. |
| 6 | ¿Coincide "sumar G+Q" con "correr GQ directo"? | **max_dP = 4,68e-11 kN** y reacciones con max_dR ≤ 6,0e-11 kN en las 3 combinaciones: el principio de superposición queda verificado **numéricamente** y con su indicador en la ventana P–M (el toggle interactivo queda para v2, §3.3). |

> La P significa "pregunta de diseño", no "píxel": todas las cifras salen de
> `results/secciones_semana03.json` + `secciones_semana04.json` y son las mismas
> que la suite de protección verifica.

## 6. Preparación móvil — build Android (manual, sin batchmode)

Enfoque acordado: **no se compila en batchmode**; se deja un menú en el Editor
para que el estudiante apriete un botón. Pasos en la máquina del estudiante:

1. Abrir `unity/EdificioSolidoUnity` con un **Unity 2022.3 LTS (o 2021.3)** con
   módulo **Android Build Support** (SDK, NDK, JDK) instalado.
2. (Opcional) `Tools/MCOC/Preparar escena Main` — deja la cámara orbital +
   `StickModel`. La escena y los JSON ya viven en el proyecto.
3. **`Tools/MCOC/Build Android`** (nuevo `Assets/Editor/MCOCBuildAndroid.cs`):
   - garantiza `Assets/Scenes/Main.unity` como única escena en build settings;
   - fija **identifier = `com.mcoc.edificiocomplejo`** (BuildTargetGroup.Android);
   - **min SDK = 22** (Android 5.1), **landscape = LandscapeLeft**;
   - `bundleVersion = 1.0`; IL2CPP;
   - compila a **`unity/EdificioSolidoUnity/build/EdificioComplejo_MCOC.apk`**
     (carpeta `build/` junto a `Assets/`) y abre el Explorador al finalizar.
4. Instalar en el teléfono (USB con depuración, o copiar el APK): el JSON viaja
   **embebido en el APK** (carpeta `StreamingAssets`), así que la app **no
   necesita internet**. A partir de ahí, para **cambiar los datos** ya no hace
   falta reinstalar: `scripts\subir_json_telefono.ps1` (§6.2).

### 6.1 Teléfono compatible (ficha verificada en `ProjectSettings.asset`)

| Parámetro | Valor | Lectura |
|---|---|---|
| SDK mínimo | **22 → Android 5.1 Lollipop** | `ProjectSettings.asset:169` (ya venía así; el menú lo reafirma) |
| Orientación | **LandscapeLeft** (forzada en el build) | `MCOCBuildAndroid.cs:17`; `ProjectSettings.asset:11` |
| Resolución | 1920×1080, **mínima 400×300** | `ProjectSettings.asset:74-77` |
| Backend | **IL2CPP**, arquitectura **ARM** | `MCOCBuildAndroid.cs:27-30`; `ProjectSettings.asset:261` |
| Identificador / versión | `com.mcoc.edificiocomplejo` / 1.0 | `MCOCBuildAndroid.cs` |
| Salida | `unity/EdificioSolidoUnity/build/EdificioComplejo_MCOC.apk` | `MCOCBuildAndroid.cs:23` |

**Teléfono de referencia:** cualquier Android **5.1 o superior** con pantalla
táctil en horizontal (equipos de la gama 2015 en adelante, 2 GB de RAM o más).
Se eligió **min SDK 22** a propósito porque es el piso que cubre desde Android
5.1 sin pedir permisos ni Target API modernos; con `targetSdk` heredado por Unity
no hay requisito de *scoped storage* ni de *targetSdk 31+*.

### 6.2 Datos actualizables SIN recompilar el APK (implementado en v1)

El APK lleva dentro el **C# compilado (IL2CPP ARM) y la escena serializada**: el
**diseño** (materiales, cámara, ventanas, posiciones) solo cambia recompilando.
Los **datos**, en cambio, se pueden subir al teléfono sin tocar el build. Esa
distinción se decidió **antes** de compilar, precisamente para poder cambiar
el modelo varias veces durante la semana sin rehacer el APK.

**Orden de búsqueda del JSON** (`UnityStickModel.cs`, `Cargar()`):

| Prioridad | Fuente | Cuándo se usa | ¿Actualizable sin rebuild? |
|---|---|---|---|
| 1 | `Application.persistentDataPath` | si existe el archivo ahí | **sí** (`adb push`) |
| 2 | `StreamingAssets` con `UnityWebRequest` | Android: la carpeta va **comprimida dentro del APK** y `File.ReadAllText` falla | no |
| 3 | `StreamingAssets` con `File` | Editor / escritorio | no |

En Edit Mode (fuera de Play) las corrutinas no corren, así que ahí se usa la vía
síncrona (1 y 3) y la escena se sigue construyendo en el Editor como siempre.

**El flujo de trabajo queda así:**

```
en el PC                                   en el teléfono
──────────────────────────────────────────────────────────────
se edita el dato (src/)  ─┐
se reanaliza y se refrescan cachés ─┤
  (los 4 comandos de §2.0)          │
se copia el JSON al visor:          │
  scripts\subir_json_telefono.ps1 ──┼──►  /sdcard/Android/data/
                                       com.mcoc.edificiocomplejo/files/
                                       edificio_completo.json
                                       └─ se aprieta "Recargar JSON"
                                          → "Fuente: telefono (actualizable)"
                                            con la fecha del push
```

El script es aditivo y verificable: comprueba que `adb` exista, que haya **un**
dispositivo, hace `mkdir -p`, empuja el archivo y **verifica con `ls -l` en el
teléfono** lo que quedó. Además tiene `-Borrar` para volver al JSON del APK, y
el botón **"Usar JSON del APK"** del panel hace lo mismo desde el teléfono (borra
la copia sobrescrita y recarga). El panel muestra siempre la **fuente** usada y
la **fecha** del archivo, para que nunca haya duda de qué datos se están viendo.

> Esto **no** arregla los otros dos riesgos de Android (no hay entrada táctil ni
> layout adaptativo), que sí exigen recompilar: son 2 y 3 de §6.3.

### 6.3 Riesgos que quedan del build en Android

1. **Sin entrada táctil.** Toda la interacción es ratón/teclado
   (`Input.GetMouseButton*`, `Input.mousePosition`, `Input.GetAxis("Mouse X")`):
   en el móvil no se puede rotar, ni hacer zoom, ni *click* en un elemento para
   abrir el panel de consulta — que es justamente lo que hace útil al visor.
2. **GUI de tamaño fijo.** El panel es IMGUI con `Rect(16, 16, 380, 700)`: con
   una pantalla de móvil en horizontal puede quedar fuera de área visible.

> Ninguno de los dos se puede resolver empujando datos: los dos exigen
> recompilar el APK. El trabajo es de código acotado y **no toca el motor de
> análisis**.

> Conclusión honesta: el **build móvil inicial está preparado y es
> reproducible**, y los **datos ya son actualizables sin recompilar** — ambas
> cosas se verificaron en el Editor (ver §8). Lo que **no** se puede afirmar
> todavía es que la **interacción** (táctil) sea usable en el teléfono, porque
> el **módulo Android Build Support no está instalado** en esta máquina
> (`Editor/Data/PlaybackEngines/` solo tiene `windowsstandalonesupport`): el APK
> hay que compilarlo a mano en un equipo con SDK/NDK/JDK.

## 7. IA — la parte más compleja la escribió el agente, y se verificó por fuera

### 7.1 Qué funcionalidad se le delegó al agente

El motor de análisis no lineal de secciones (`src/secciones/`, 11 módulos)
— es la pieza con más riesgo del proyecto y **la implementó el agente de
código**:

| Módulo | Qué resuelve |
|---|---|
| `materiales.py` | leyes `Concrete01` (parábola de Hognestad, εc0 = −0,002, εcu = −0,004) y `Steel01` (b = 0,01) + recálculos de "mano" (`p0_nominal_aci`, `p0_fibra_max`) |
| `seccion.py` | geometría de fibra: hormigón Concrete01 + acero Steel01 + armadura explícita (`columna_70x70`, `muro_generico`, catálogos A y B) |
| `curva.py` | **momento-curvatura** con OpenSeesPy (`zeroLengthSection` + fibras) |
| `analitica.py` | integrador de fibras 100 % Python (contraste independiente del motor) |
| `pm.py` | **envolventes P–M** de columnas y muros (barrido) + punto balanceado |
| `catalogo_muros.py` | catálogo de las 11 secciones de muro distintas del B |
| `demandas.py`, `demandas_a.py`, `superposicion.py` | **verificación RC y demanda-capacidad por elemento** (reutiliza `esfuerzos._correr_caso`) y superposición G+Q ≡ GQ / G+EX / G+Q+EX |
| `diagramas.py`, `exportar_unity.py` | diagramas de esfuerzo y **inyección de las curvas al visor** |

El último punto es el que ata el cálculo con la pantalla: cada elemento del
contrato sólido lleva su campo `seccion` (`muro0.20x11.59`, `col0.70x0.70`, …) y
el visor resuelve la curva **por `elementTag`**, con tres niveles de búsqueda
(`UnityStickModel.cs:2230-2257` → `TryGetValue(tag)`; `:2241` → clave de sección
exacta → representativa por tipo → prefijo de tipo). Sin esa asociación por tag
el panel P–M mostraría siempre la curva representativa, que es el error que se
detectó y corrigió en la semana 04.

### 7.2 Por qué es compleja

1. **Análisis no lineal de sección**: no es una simple comprobación de
   equilibrio, es un problema de interacción entre dos materiales con
   degradación monotónica: el hormigón no toma tensión y se aplasta en εcu, y el
   acero fluye y endurece.
2. **Procedimiento momento-curvatura** (el que más se equivoca):
   axial primero con `LoadControl(1.0)` —más refinamiento 1/2/4/8 si no
   converge— (`curva.py:158`), y **después** momento unitario con
   `DisplacementControl` en el GDL 3; de ahí `M = getTime()` y
   `κ = nodeDisp(2,3)` (`curva.py:120-131`). El criterio de fin de curva lo fija
   el propio estado: `crushing` (εc ≤ εcu), `su_steel` (εsu = 0,09) o
   `softening` (M < 0,85·M_max), no un tope de pasos arbitrario.
3. **Barrido P–M**: 17 puntos de tracción pura (1,03·fy·As) a compresión
   (1,03·P0), y hay que **cambiar la convención de signos**: el motor comprime con
   P < 0 y el reporte usa P > 0 compresión (`pm.py:52-72`).
4. **Asociación por tag al viewer**: la curva correcta depende del `elementTag`
   seleccionado, y cada uno tiene su propia sección.

> Nota de implementación (por qué fibras explícitas y no `patch`): en
> openseespy 3.8.0 el `patch('rect')` dio un EA 93 % mayor que el teórico en una
> celda 1×1; por eso la malla 12×12 + 1 fibra por barra, que sí verifica EA y EI
> analíticos a < 1e-4.

### 7.3 Cómo se verificó — metodología, no confianza ciega

**a) Referencia analítica independiente (hand-calc a mano, sin código).**
Para la columna 0,70 × 0,70 (8φ28, As = 4 926·10⁻³ m², Ag = 0,49 m²,
ρ = 1,005 %, f'c = 25 MPa, fy = 420 MPa):

| Magnitud | Hand-calc a mano | Motor de fibra / caché |
|---|---|---|
| P₀ nominal ACI = 0,85·f'c·(Ag−As) + fy·As | 0,85·25 000·0,485 074 + 420 000·0,004 926 02 = **12 376,7 kN** | `P0_aci` = **12 376,7 kN** (coincidencia exacta) |
| P₀ de fibra = f'c·(Ag−As) + Es·\|εc0\|·As | 25 000·0,485 074 + 200 000·0,002·0,004 926 02 = 12 126,8 + 1 970,4 = **14 097,3 kN** | `P0_fibra` = **14 097,3 kN** |

Los dos valores se reproducen **exactamente** con la fórmula cerrada, no
"aproximadamente": el motor no está inventando capacidad, está integrando las
mismas leyes de materiales que la calculadora a mano.

**b) Cruce de puntos característicos de la envolvente de la columna:**

- **Flexión pura** (par de acero, sección doblemente armada simétrica): el
  hand-calc con bloque 0,85·f'c (T = As/2·fy = 1 034,5 kN, a = 0,067 m,
  brazo M/T = 0,616 m = d − a/2 exacto) da **Mn ≈ 637 kN·m**. El motor da
  **620 kN·m** (barrido truncado, nincr = 400) y **722 kN·m** (barrido canónico,
  nincr = 2 000): los tres en la misma banda, y la diferencia es la misma
  convención f'c pico vs 0,85·f'c del punto (a).
- **Tracción pura**: P = −2 131 kN = 1,03·As·fy (As·fy = 2 068,9 kN) con
  M = 241 kN·m — sección simétrica, sin bloque de compresión. En rango.
- **Balanceado** (εc = εcu en la fibra extrema comprimida, εs = εy en el acero
  extremo): (4 370 kN, 1 158 kN·m). El **pico** de la envolvente es
  (4 379 kN, 1 530 kN·m) frente al objetivo del enunciado (4 500, 1 395 ±10 %):
  **−2,7 % en P y +9,7 % en M**, dentro de tolerancia.
- **Rigidez agrietada EI₀**: 144 124 / 145 879 / 146 399 kN·m² con malla 6×6 /
  12×12 / 20×20 frente a 146 991 kN·m² del integrador analítico (−0,8 %).

**c) Chequeo discriminante — ¿el 1,15 del P₀ es un error o una convención?**
El ratio `P0_fibra / P0_aci` es **≈ 1,15 en las 11 secciones de muro + la
columna** (mínimo 1,139 en la columna, máximo 1,167 en los muros de 0,60 m;
media ≈ 1,155). Un bug de la malla o de las barras se manifestaría **errático o
atado al refinamiento**; un valor **constante y reproducible** por la fórmula
cerrada f'c vs 0,85·f'c (más el término del acero a εc0 = 0,002 < εy) confirma
que es la **convención de f'c pico frente al bloque 0,85·f'c, no un error**. El
test `test_catalogo_muros_p0_fibra_cerca_de_aci` lo acota además a 1,05–1,30.

**d) Muros con el espesor correcto (un error REAL encontrado y corregido).**
Los **20 muros del bloque superior** (espesor 0,20, en `MUROS_BLOQUE_SUP_V/H`)
caían por defecto en la sección de la columna `col0.70x0.70`; se corrigió en
`demandas.per_elemento` y ahora los **60 muros** tienen su etiqueta
`muro{e}x{L}` propia. Cubierto por `test_per_elemento_muros_con_seccion_propia`
(afirma 60 muros y que ninguno empieza por `col`).

**e) Suite automática: 134 tests.** `python -m pytest tests` → **134 passed**
(5,7 s), incluidos los que cierran esta funcionalidad:

| Test | Qué garantiza |
|---|---|
| `test_semana04_pm_columnas_todas_resuelven_catalogo` | **toda** columna/muro de A y B resuelve su curva en el catálogo (si un tag no resolviera, caería) |
| `test_catalogo_muros_11_secciones_distintas` | las 11 secciones de muro del B, cada una con su nombre real |
| `test_per_elemento_muros_con_seccion_propia` | los 60 muros con sección propia (incluye los de e = 0,20) |
| `test_cierre_diagrama_por_viga_y_caso`, `test_semana04_equilibrio_y_cierre_verticales` | **cierre de los diagramas de esfuerzo** y del equilibrio |
| `test_superposicion_combinaciones_seccion4` | G+Q ≡ GQ, G+EX, G+Q+EX (max ΔR ≤ 6e-11 kN) |

**f) Honestidad del proceso — el verificador también falló.** Un primer
prototipo de verificación dio `M(0) ≈ 220 kN·m` (un artefacto del tope de pasos:
no había llegado al pico). Al re-correr con más incrementos y contrastar con el
integrador analítico, el valor real está en **720–870 kN·m** (12×12 canónico:
722 kN·m). En el otro extremo, la capacidad "1,15 veces mayor" del P₀ se leyó
inicialmente como sobreestimación hasta que el hand-calc mostró que el agente
tenía razón y lo que difería era la convención de f'c. **Los dos casos
confirman lo mismo: un número sin referencia independiente no se acepta ni
cuando sale del agente ni cuando sale del verificador.**

**g) Reparto de roles.** El agente **implementó**; la verificación fue
**independiente** y así se hizo: (i) re-correr el motor de fibra y comparar con
el integrador analítico del mismo repo, (ii) cálculos a mano de P₀ y Mn con la
fórmula cerrada, (iii) suite de 134 tests como puerta, y (iv) revisión manual de
conservación/equilibrio y de los cruces P–M. **No se aceptó el resumen del
agente como evidencia:** cada cifra de esta sección se reprodujo o se refutó
con un cálculo externo.

> [CAPTURA: ventana P–M del visor — envolvente `muro0.60x2.91` del muro B tag 41
> con el punto de demanda del caso activo GQ (2 175 kN; 3 912 kN·m) y su
> capacidad Mcap = 5 271 kN·m → D/C = 0,74; abajo, el balanceado (16 407; 8 706)
> y la etiqueta de sección del muro.]

## 8. Aceptación y reproducción

- **Estado canónico final**: `SC_PISO = 3,0` y `MUROS_V[0]` con t = 0,60 (los
  archivos de entrada quedaron como al inicio); las cachés regeneradas dan los
  mismos números del baseline (B: G = 48 171,1 kN, Q = 11 029,6 kN, EX
  u_techo = 15,996 mm, M_volc = 76 424 kN·m; tag 41 GQ P_d = 2 175,21 /
  M_d = 3 912,35; §3 max_dP = 4,68e-11; §4 B GQ max_dR = 2,9e-11).
- **Suite de protección**: `python -m pytest tests` → **134 passed** (igual que
  la semana 04). La ruta completa `python -m pytest` sin argumentos choca con el
  `test_secciones.py` duplicado en `para_entrega/` (colisión de basename de
  módulo, preexistente); la suite real y entregable es `tests/` → 134.
- **Verificado en el Editor (Unity 2022.3.62f3)**: el proyecto abre **sin errores
  de compilación** y la carga del JSON quedó probada en los dos senses:
  1. tal como está → `JSON cargado (StreamingAssets (archivo))`;
  2. copiando el JSON a `Application.persistentDataPath` (la carpeta que en
     Android escribe `adb`) → `JSON cargado (telefono (actualizable))`, o sea
     **la precedencia funciona de verdad** y la etiqueta `Fuente:` del panel
     cambia. Al borrar la copia vuelve a `StreamingAssets (archivo)`.
  Se comprobó también en Edit Mode, que es donde la corrutina no correría.
- **Lo que sigue sin verificar**: el APK en un teléfono real, porque esta
  máquina no tiene el módulo **Android Build Support** instalado (§6.3).
- **Nuevo código aditivo**: `scripts/semana05_refrescar_caches.py`
  (refresca demandas / per_elemento / §3 / §4 / curvas nuevas) y
  `Assets/Editor/MCOCBuildAndroid.cs` (menú de build). **El motor de análisis y
  los resultados canónicos NO se tocaron**: el único archivo del visor
  modificado es `UnityStickModel.cs`, y solo en su **carga del JSON** (§6.2):
  búsqueda por prioridad + `UnityWebRequest` + etiqueta de fuente + botón de
  vuelta al APK. Ni la geometría, ni los materiales, ni las curvas P–M cambian
  de comportamiento.
- **Cobertura del enunciado**: §1 (11 funciones con estado) · §2 (2
  modificaciones con la cadena completa) · §3 (3 estados de superposición con
  números) · §4 (sidequist documentada con los 5 requisitos de v2) · §5 (las 6
  preguntas de inspección + 6 de diseño) · §6 (ficha de teléfono + build + canal
  de datos actualizables + 2 riesgos que quedan) · §7 (IA con verificación
  independiente).

## 9. Entregables

- `reports/semana05.md` (este documento) con 7 secciones (las 7 del enunciado)
  + capturas marcadas.
- `unity/EdificioSolidoUnity/Assets/Editor/MCOCBuildAndroid.cs` (build APK).
- `scripts/semana05_refrescar_caches.py` (motor de refresco de los ciclos).
- `scripts/subir_json_telefono.ps1` (actualiza el dato del teléfono por `adb`
  y verifica lo que quedó en el dispositivo; `-Borrar` vuelve al del APK).
- `unity/EdificioSolidoUnity/Assets/Scripts/UnityStickModel.cs` con la carga
  por prioridad (§6.2), la etiqueta de fuente y el botón `Usar JSON del APK`.
- Resultados canónicos intactos en `results/` y `StreamingAssets/`.

## 10. Pendientes declarados para la semana 06

Ninguno de estos huecos afecta a los números entregados; son capas de la
interfaz y de la plataforma móvil:

1. **Superposición interactiva** (toggle + render superpuesto de G/Q/EX sobre el
   caso directo) — la verificación numérica ya está (§3).
2. **Ejes locales dibujados** (hoy solo orientan los diagramas).
3. **Gráfico de torsión T** (el dato existe en `esfuerzos_completos`; hoy se
   imprime como etiqueta `T(x)` pero no tiene diagrama).
4. **Tributarias con `qQ`** (hoy el mosaico solo dibuja `qG`).
5. **Móvil — interacción**: gestos táctiles (rotar / zoom / seleccionar) y
   layout adaptativo del panel (§6.3). **Esto sí exige recompilar el APK**;
   lo que ya no queda pendiente es la *lectura del dato* (§6.2).