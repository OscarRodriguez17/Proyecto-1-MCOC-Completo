# BITÁCORA DEL PROYECTO — COMPLEJO DE INGENIERÍA (Edificios A y B)

> **Registro vivo del proyecto.** Al iniciar cada sesión, LEER ESTE ARCHIVO.
> Al terminar, actualizarlo.
>
> Historial previo:
> - `docs/bitacora_edificio_A.md` — bitácora completa del Edificio A (Sesiones
>   1–21) y `docs/ficha_geometria.md`. El Edificio A quedó **completo y
>   verificado** en su Sesión 21 (carpeta original `(OFICIAL)`).
> - `docs/bitacora_edificio_B.md` — bitácora del Edificio B y
>   `docs/ficha_geometria_B.md` (modelo armado y verificado desde cero).

---

## ⭐ Estado actual

**Proyecto UNIFICADO: `Proyecto 1 MCOC - Complejo (A+B)`.**

Tercera carpeta que reúne el Edificio A y el Edificio B como **un solo
proyecto** de laboratorio estructural digital 3D (OpenSeesPy + Unity + AR),
mostrándolos **lado a lado**.

- **Estrategia (decisión de arquitectura):** cada edificio corre su propio
  análisis OpenSees independiente (misma regla que la decisión histórica de
  los 2 bloques: evita el doble conteo de áreas tributarias), y un módulo de
  **fusión** los exporta en UN solo contrato JSON + visualizaciones lado a lado.
- **Separación visual:** el Edificio B se desplaza en **+X** respecto del A
  (offset paramétrico, default **60 m**, slider/argumento configurable).
- **Edificio B hoy (Sesión 6):** en **paridad con A** — casos G/Q/GQ/EX/EY,
  sismo pseudoestático (V = α·G, α=0.10), superposición verificada, carga
  muerta de losa, tributario por viga, y pasada de diagramas (bloque esfuerzos).
- **Edificio A hoy (Sesión 8):** tiene también su pasada dedicada de diagramas
  (bloque esfuerzos en `modelo_resultados.json` y `edificio_solido.json`,
  228 vigas × 5 casos). Las vigas del Edificio A se consultan en el visor
  sólido con el mismo panel que las de B.
- **Visor Unity (Sesión 8, después):** el proyecto oficial es
  `unity/EdificioSolidoUnity/` (visor sólido A+B). Se reemplazó el
  `Assets/Scenes/Main.unity` de 64 MB (geometría horneada, causa del "modelo
  deforme" que veían los compañeros) por una **escena mínima de 10 KB** que se
  reconstruye sola desde `StreamingAssets/edificio_completo.json`; se registró
  la escena en `EditorBuildSettings` y el setup la **abre automáticamente** al
  arrancar. El proyecto `unity/EdificioComplejoUnity/` queda como **visor viejo**
  (NO usar).
- **Semana 03 completada (Sesión 10, continuación):** catálogo P–M con la
  **columna y los 11 muros distintos** del Edificio B (13 curvas en caché),
  overlay por etiqueta de sección por elemento + fallback `seccion→muro/col` en
  el C#, superposición **G+EX y G+Q+EX** verificadas con corrida directa en **A y
  B** (~1e-11 kN/1e-16 m) además de la G+Q, y `reports/semana03.md` con las 9
  secciones requeridas (casos base, tributaria, sismo pseudoestático con el
  hallazgo de **rz EX del B ≈ 70× el del A**, catálogo, superposición).
  **Tests: 116 passed.**
- **Catálogo P–M del Edificio A (Sesión 11):** se extendió el MISMO motor
  aditivo al Edificio A. Con 6 secciones propias con **f'c = 30 MPa (G35)** en
  el motor de fibras (el modelo elástico sigue con Ec(35)): columna 70×70
  (8φ28 según plano 101, capa RLE-PILAR) y los **5 muros** de
  `datos_edificio.WALLS` (ME-32, MI-32, MI-12, M1c-E, M2a). La armadura de los
  muros **no está en los planos** (confirmado) → se adoptó la disposición de B
  como **supuesto documentado** (`reports/supuesto_armado_edificio_A.md`):
  recubrimiento 3 cm, alma φ10 @ 0.20 m doble malla (ρ_v ≈ 0.003), borde
  6φ16 por extremo sobre `max(2t, 0.15L)`. Se añadieron `curvas_A`,
  `demandas_A`, `per_elemento_A` y `superposicion_A` al caché y el overlay
  (`exportar_unity.py`) ahora enriquece **ambos** edificios en
  `edificio_solido.json` (111 elementos de A etiquetados: 79 columnas + 32
  muros). Runner: `scripts/semana03_edificio_A_run.py`. **Tests: 126 passed
  (116 + 10 nuevos de A).**
- **Semana 04 — PARTE A (Sesión 12):** Unity queda como **postprocesador
  estructural total**: `esfuerzos_completos` / `esfuerzos_completos_A` (los 9
  coeficientes de diagrama {L,N,Vy,Vz,T,My,Mz,Wy,Wz} por elementTag y caso
  G/Q/GQ/EX/EY de **TODOS** los elementos: columnas, muros, vigas y aspas) +
  `metadatos` (tipo de visor, sección de catálogo, material H30/G35, L,
  restricción por extremo `empotrado/master/esclavo/libre` y ejes locales). La
  convención N(x)=-N (N>0 compresión) y la repartición cuadrática del momento
  son las ya verificadas de semana 03. Cobertura **exacta** del visor sólido:
  B 315 por caso (40 col + 60 muros + 215 vigas), A 343 (79 + 32 + 228 vigas +
  4 aspas). Overlay aditivo/idempotente en `edificio_solido.json` + copia a
  StreamingAssets; runner `scripts/semana04_esfuerzos_completos_run.py`; cache
  `results/secciones_semana04.json`. **Tests: 133 passed.**
- **Semana 04 — PARTE B (Sesión 13):** el visor sólido C# (`EdificioSolidoUnity`)
  consulta estos esfuerzos completos + metadatos con un **diccionario unificado
  por bloques visuales** (columnas, muros, vigas_x, vigas_y; los muros no tienen
  objeto 3D y se eligen con el selector manual de tag) y un **panel único** con
  metadatos, selector de tag/caso/x, valores N/Vz/Vy/My/Mz/T, **diagramas 3D**
  (N verde, My azul, Mz roja sobre la pieza real, auto-escala) y la sección P–M
  del motor de semana 03. Mapeo `Posicion()` SI→Unity confirmado. Compila sin
  errores en Unity 2022.3.62f3. Pendiente validación visual en Play y **PARTE C**
  (`reports/semana04.md`).
- **Semana 04 — PARTE B2 (Sesión 14):** BUG de columnas resuelto — el overlay
  agrega la clave real del pilar `col0.70x0.70` como **alias** de la
  representativa `col` (igual que A con `col_A_0.70x0.70`) y el lookup C# quedó
  robusto (match exacto → representativa por tipo → prefijo del tipo), así toda
  columna/muro de A o B vuelve a mostrar su **P–M con demanda y caso activo**.
  Se añadió además una **ventana de diagramas arrastrable** (`GUI.Window`) que
  grafica UN esfuerzo a elección (Axial/Corte Vz·Vy/Momento My·Mz) con las
  fórmulas `N(x)=-N`, `V=V+W·x`, `M=M+V·x+W·x²/2`, muestra extremos i/j,
  máximo y el valor en el slider, y sigue al elemento/caso activo. **Tests: 134
  passed.**
- **Repositorio GitHub (Sesión 8, después):** subido a
  `https://github.com/OscarRodriguez17/Proyecto-1-MCOC-Completo` (público,
  branch `master`, commit `9413022`). Instrucciones exactas para entregar en
  Canvas en la sección de entrega abajo.

### Resultados de cierre (Sesión 1 del complejo)

| Magnitud | Edificio A | Edificio B |
|---|---|---|
| Nodos | 200 | 235 (+5 masters) |
| Elementos | 343 | 350 |
| Casos | G/Q/GQ/EX/EY | G/Q/GQ/EX/EY |
| G / PP | 45,236 kN | 21,446 kN |
| Q / SC | 7,671 kN | 9,333 kN |
| V sísmico (α=0.10) | 4,524 kN | 4,817 kN |
| Material | G35 (27,806 MPa) | H30 (25,000 MPa) |
| Base | −4.21 m | −4.01 m |

Salidas: `results/edificio_completo.json` (contrato unificado),
`results/modelo_resultados.json` (A), `results/modelo_resultados_b.json` (B),
`results/modelo_complejo_3d.png` y `results/modelo_complejo_3d.html`.
Suite de tests: **94 passed** (60 de A + 9 de B + 8 del complejo + 5 de
esfuerzos de B + 6 de tributario + **6 nuevos de esfuerzos de A**).

**Hoy (Sesión 8):** el Edificio A ya tiene su pasada **dedicada de diagramas de
viga** (G/Q/GQ/EX/EY) igual que B — bloque `esfuerzos` en `modelo_resultados.json`
y en el visor sólido (`edificio_solido.json`), 228 vigas por caso. En Unity las
vigas del Edificio A ya se pueden consultar con el mismo panel (caso + posición →
N(x)/Vz(x)/Vy(x)/My(x)/Mz(x)/T(x)).

**Ajuste de visor sólido (Sesión 8, después):** el panel de consulta de viga del
`UnityStickModel.cs` (toogles + resultados) ocupaba 360×940 px y tapaba la escena.
Se compactó a 380×700 px: toggles de visibilidad en grilla de 2 columnas
(estructura) + fila de cargas, sliders de amplificación/separación en línea con
su etiqueta, selector de caso de consulta como fila de toggles y los valores
N(x)/Vz(x)/Vy(x)/My(x)/Mz(x)/T(x) en **negrita (fontSize 13)** para que las
magnitudes sean legibles. El rectángulo de supresión de clics
(`ProcesarSeleccionViga`) se mantiene sincronizado con el área del panel.

### Cómo correr

```bash
# Entorno
python -m venv .venv && .venv\Scripts\Activate.ps1
pip install -r requirements.txt

# COMPLETO: analiza A, analiza B, fusiona y visualiza (PNG + HTML)
python src\complejo.py

# Solo fusión de JSON ya existentes (rápido)
python src\benchmark_3d\fusionar.py --sin-correr

# Verificación de cargas del A (cross-check JSON)
python src\benchmark_3d\verificar_cargas.py

# Tests (A 60 + B 9 + complejo)
python -m pytest tests\ -q
```

### Decisión de arquitectura (análisis independientes + fusión)

- No hay un único modelo OpenSees con ambos edificios: se analizan por
  separado (`src/benchmark_3d/` para A, `src/edificio_b/` para B) y se
  fusionan a nivel de contrato JSON.
- El Edificio B vive como paquete `src/edificio_b/` con imports relativos
  (`from .construir import ...`) para no colisionar con los módulos de A.
- En el JSON unificado, B queda **renumerado** (+100,000 en tags) y
  **desplazado** en X (el offset queda registrado en `config.offset_b_x_m`).
- Cada `json` de `edificios[]` conserva su esquema nativo: A usa dict de nodos
  por tag y resultados por caso; B usa listas de nodos/elementos. Los
  visualizadores (PNG/HTML/Unity) del complejo entienden ambas formas.

### Pendientes

- [x] Probar en Unity el visualizador del complejo (`UnityComplejo.cs`) —
      **DONE (Sesión 2):** proyecto autocontenido, compilado en batchmode,
      escena creada y rutina verificada. *(`JsonEdificio.nodos` pasó a
      `JToken` porque B trae array y A dict; el contrato validado contra el
      JSON y la escena generada en el editor.)*
- [ ] Abrir `EdificioComplejoUnity` en el editor gráfico de Unity y pulsar
      Play para confirmar el render con la nueva cámara orbital centrada.
- [x] Subir el Edificio B a la paridad del A (casos G/Q/GQ/EX/EY,
      superposición, cargas y apoyos en su JSON). **DONE (Sesión 3).**
      Falta solo (opcional) la carga muerta de losa como superficie.
- [ ] **Oscar:** modelo SAP2000 para verificación cruzada (pendiente original).
- [ ] Decidir commit/push del avance acumulado.

---

## 📅 Sesión 1 — Lunes 14 de septiembre 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo — unificación de los Edificios A y B en un solo proyecto

1. **Contexto:** el Edificio A está casi completo (carpeta `(OFICIAL)`,
   Sesión 21, 60 tests) y el Edificio B tiene su modelo armado y verificado
   pero mucho más básico (9 tests, solo PP+SC). Ambos deben convivir como un
   solo proyecto y verse **lado a lado**.

2. **Decisiones (confirmadas con Oscar):**
   - Estrategia de análisis: **independientes y fusionados** (no un solo
     modelo OpenSees).
   - Ubicación visual: **offset X paramétrico** del Edificio B (default 60 m).
   - Alcance de B hoy: **tal cual** (PP+SC); la paridad con A queda pendiente.

3. **Tercera carpeta creada:** `Desktop\Proyecto 1 MCOC - Complejo (A+B)\`.
   - Base = copia del proyecto `Edificio A (OFICIAL)` (reglas AGENTS.md,
     estructura, pipeline, docs, tests y scripts Unity reutilizables).
   - `src/edificio_b/` — pipeline del Edificio B convertido a **paquete** con
     imports relativos (`__init__.py`, `datos_edificio`, `construir`,
     `cargas`, `analizar`, `verificar`, `visualizar`). `analizar.correr()`
     parametrizado: escribe `results/modelo_resultados_b.json` y retorna
     `{M, Wpp, Wsc, data, out}`.
   - `tests/test_edificio_b/` — test_benchmark + test_geometria apuntando al
     paquete `edificio_b`.
   - Docs heredados renombrados: `docs/bitacora_edificio_A.md`,
     `docs/ficha_geometria_B.md`, `docs/bitacora_edificio_B.md`.

4. **Fusión** (`src/benchmark_3d/fusionar.py`): corre A (`analizar`) y B
   (`edificio_b.analizar`), aplica a B el **offset X** y la **renumeración**
   (+100,000) de tags, y emite `results/edificio_completo.json` con
   `edificios[2]`, `totales` (A: G/Q/GQ/V_EX/V_EY · B: PP/SC · complejo) y
   `config`. Admite `--sin-correr` para re-fusionar sin re-analizar.

5. **Visualización lado a lado** (ambos esquemas):
   - `src/benchmark_3d/visualizar_complejo.py` → `results/modelo_complejo_3d.png`
     (matplotlib, leyenda unificada por tipo).
   - `src/benchmark_3d/visualizar_complejo_html.py` →
     `results/modelo_complejo_3d.html` (Three.js interactivo, etiquetas
     "EDIFICIO A"/"EDIFICIO B" en el nivel base).
   - `src/complejo.py` — punto de entrada: analiza A, analiza B, fusiona,
     visualiza.

6. **Tests del complejo:** `tests/test_complejo.py` (estructura, dos
   edificios A/B, renumeración de B, conectividad renumerada, offset aplicado
   y sin solape con A).

7. **Unity:** `unity/Scripts/UnityComplejo.cs` — lee `edificio_completo.json`
   y dibuja ambos edificios lado a lado (por verificar en el editor).

### Verificación

- Análisis A: valores idénticos a la Sesión 21 del A (G=45,236 kN, V=4,524 kN).
- Análisis B: PP=21,446 kN, SC=9,333 kN, equilibrio Z ok (resid 1.8e-10).
- Fusión: JSON unificado generado; B sin colisión de tags y desplazado +60 m en X;
  totales del complejo G=66,682 kN, Q=17,004 kN.
- Tests: 77 passed (`pytest tests\ -q`): 60 de A + 9 de B + 8 del complejo.
- Salidas: `results/modelo_complejo_3d.png` y `results/modelo_complejo_3d.html`.

### Pendientes (abren Sesión 2)

- [ ] Probar en Unity del visualizador del complejo.
- [ ] (Opcional) paridad sísmica del Edificio B.
- [ ] **Oscar:** SAP2000 verificación cruzada.
- [ ] Commit/push del avance.

---

## 📅 Sesión 2 — Lunes 14 de septiembre 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo — proyecto Unity del complejo abrible y verificado

1. **Unity detectado:** editor **2022.3.62f3** (LTS) instalado en
   `C:\Program Files\Unity\Hub\Editor\2022.3.62f3\` (también 6000.5.10f1).
   No se halló el ejecutable de Unity Hub; el proyecto se abre directo con
   `Unity.exe -projectPath`.

2. **Scaffold autocontenido** en `unity/EdificioComplejoUnity/`:
   - `Assets/Scripts/` — `ModeloEdificio.cs`, `UnityStickModel.cs`,
     `OrbitCamera.cs`, `UnityComplejo.cs`. **No** se copia `MCOCSceneSetup.cs`
     (crearía `Main.unity` del Edificio A y entraría en conflicto con el
     bootstrap del complejo).
   - `Assets/Editor/ComplejoSceneSetup.cs` — bootstrap `[InitializeOnLoad]` +
     menú `Tools/MCOC/Preparar escena COMPLEJO` / `Abrir escena COMPLEJO`;
     crea la escena con cámara principal (+ `OrbitCamera` distancia 260),
     luz direccional y GameObject "Complejo" con `UnityComplejo`
     (`jsonRuta = edificio_completo.json`).
   - `Assets/StreamingAssets/` — copias de `edificio_completo.json`
     (visor del complejo) y `modelo_resultados.json` (visor del A).
   - `Packages/manifest.json` — newtonsoft-json 3.2.1 **+ módulos built-in**
     (physics, imgui, al numerar los módulos).

3. **UnityComplejo.cs centrado:** el render pasa a **dos pasadas** — cálcula
   el bbox global de A+B, expone `CentroTotal` y desplaza cada root el
   `-CentroTotal` para que el conjunto quede en el origen de la escena y
   `OrbitCamera` (centro en 0) encuadre bien. Las etiquetas "EDIFICIO A/B"
   y el título respetan el desplazamiento.

4. **Verificación en batchmode** (`Unity.exe -batchmode -nographics -quit`):
   - 1ª pasada: **errores de compilación** — el manifest solo tenía
     newtonsoft (faltaba physics → `Collider`, imgui → `GUILayout/GUI`).
     Corregido agregando los módulos built-in.
   - 2ª pasada: `Exiting batchmode successfully now!` (0 errores CS).
   - La escena NO se crea sola con `-quit` (los `delayCall` no corren en
     batch); se generó vía `-executeMethod MCOC.EditorTools.ComplejoSceneSetupPrepararEscena`
     → **`Assets/Scenes/Complejo.unity` creada** (verificado: Main Camera +
     OrbitCamera distancia 260 + AudioListener + Directional Light + Complejo
     con `jsonRuta = edificio_completo.json`).

5. **Lanzador:** `abrir_unity_complejo.cmd` (doble clic) usa
   `%-dp0%unity\EdificioComplejoUnity` con el editor 2022.3.62f3.
   README de `unity/` actualizado con el flujo del proyecto autocontenido.

### Verificación

- Compilación C# sin errores en batchmode (log:
  `Exiting batchmode successfully now!`).
- Escena `Assets/Scenes/Complejo.unity` presente y con los componentes
  esperados (grep del YAML).
- 77 tests del complejo siguen en verde (sin cambios en el pipeline).

### Pendientes (abren Sesión 3)

- [ ] Abrir el proyecto con `abrir_unity_complejo.cmd`, pulsar Play y
      confirmar el render del complejo con la cámara orbital.
- [ ] (Opcional) paridad sísmica del Edificio B.
- [ ] **Oscar:** SAP2000 verificación cruzada.
- [ ] Commit/push del avance.

---

## 📅 Sesión 3 — Lunes 15 de septiembre 2026

**Participantes:** Nicolás + asesor técnico (Claude).

### Qué se hizo — Edificio B elevado a PARIDAD con el Edificio A

Objetivo: que B deje de entrar "tal cual" (solo PP+SC) y tenga el mismo
alcance de análisis que A, porque es lo que hace que el conjunto funcione bien
como laboratorio estructural. Todo el cambio es **ADITIVO**: no se tocó la
geometría, los IDs, `construir.py` ni `verificar.py`, y las cargas calibradas
(PP=21.445,6 kN, SC=9.332,7 kN) quedaron idénticas.

1. **`src/edificio_b/datos_edificio.py`** — se agregó `ALPHA_EQ = 0.10`
   (coeficiente sísmico basal, igual que A).

2. **`src/edificio_b/sismo.py` (NUEVO)** — sismo pseudoestático con el mismo
   método que A:
   - Peso sísmico por nivel W_i = peso muerto del marco (elemento → nivel de su
     nodo superior); `Σ W_i` reproduce exactamente el peso propio total.
   - V = α · Σ W_i ;  F_i = V · W_i·z_i / Σ(W_j·z_j), con **z absoluto**
     (misma convención de A; z₁=−0.05 → aporte casi nulo en el 1er nivel).
   - F_i repartida entre los nodos esclavos del nivel (nunca el maestro del
     diafragma).

3. **`src/edificio_b/analizar.py`** — reescrito para correr los 5 casos
   G/Q/GQ/EX/EY (cada uno en modelo nuevo), verificar equilibrio por caso y
   superposición R(G)+R(Q)=R(GQ), y exportar JSON enriquecido con `apoyos`,
   `cargas` (sismo V y F por nivel, pesos por nivel) y `resultados` por caso
   (aplicada, reacciones_totales, desplazamientos_maestro). Se **conservaron**
   las claves previas (n_nodos, nodos con ux/uy/uz [estado GQ], PP/SC) y el
   retorno `{M, Wpp, Wsc, ...}` (+ `V_EX`, `V_EY`, flags de verificación).

4. **`src/benchmark_3d/fusionar.py`** — aditivo: los totales de B en el
   contrato unificado ahora incluyen `V_EX_kN`/`V_EY_kN` (sin quitar PP/SC), y
   la renumeración +100.000 se aplica también a los `apoyos` de B.

5. **`tests/test_edificio_b/test_sismo.py` (NUEVO)** — blinda la paridad:
   V=α·G, equilibrio, superposición, Σ W_i = PP, y contrato JSON (5 casos +
   apoyos + esquema B nativo).

6. **Unity al día** — `UnityComplejo.cs` ya entiende el JSON enriquecido de B
   (lee nodos/elementos de B como lista; ignora apoyos/resultados/cargas). Se
   detectó que `Assets/StreamingAssets/*.json` era una copia MANUAL y quedaba
   desfasada al regenerar el modelo. Fix: `src/complejo.py` ahora copia
   `results/edificio_completo.json` y `modelo_resultados.json` a StreamingAssets
   al final del pipeline (`_sincronizar_unity`). Copias refrescadas con B en
   paridad.

### Verificación

- Baseline reproducido: PP=21.445,6 kN, SC=9.332,7 kN (idénticos).
- **V_EX = V_EY = 2.144,6 kN = 0.10 × 21.445,6** (V = α·peso muerto, como A).
- Equilibrio OK en los 5 casos (resid ~1e−10); superposición máx 3,5e−11 kN.
- Deriva de techo: EX u_x=7,59 mm, EY u_y=9,26 mm; M_volcante=34.024 kN·m.
- Fusión regenerada: `edificio_completo.json` con B en paridad, apoyos +100.000.
- **Suite: 81 passed** (60 A + 9 B + 4 nuevos de sismo B + 8 complejo).

### Nota de modelación (a decidir)

- El peso muerto de B es **solo el del marco** (pilares/muros/vigas). A, en
  cambio, aplica una carga muerta de losa `Q_G = 0.15·γ + PM.adic = 6.3 kN/m²`
  por área tributaria. Si el curso espera esa losa también en B, hay que
  tomar su espesor + PM.adic de los planos (lámina de cargas 2024_22-700) y
  agregarla; eso subiría G y, con ello, V sísmico. Es un añadido de una función
  (no cambia lo hecho). **No se asumió un valor para no inventar dato de plano.**

### Pendientes (abren Sesión 4)

- [x] Carga muerta de losa en B — HECHO (Sesión 5, lámina 2024_22-700).
- [ ] Play en Unity del complejo con B en paridad (datos ya sincronizados en
      StreamingAssets; solo falta abrir el editor y pulsar Play).
- [ ] **Oscar:** SAP2000 verificación cruzada.
- [ ] Commit/push del avance.

---

## 📅 Sesión 4 — Lunes 15 de septiembre 2026

**Participantes:** Nicolás + asesor técnico (Claude).

### Qué se hizo — VISOR SÓLIDO 3D portado al modelo completo A+B

Se trajo el armado 3D de Unity del proyecto del grupo (`Proyecto 1 MCOC`,
carpeta `EdificioIngUnity`) — que dibuja **columnas, vigas y muros como
geometría sólida real**, con apoyos/zapatas, terreno, flechas de carga
G/Q/sismo, mosaico de áreas tributarias y **deformada por caso** (G/GQ/EX/EY)
con slider — y se conectó a NUESTRO modelo completo A+B.

Contexto: el "complejo (2 bloques)" del grupo eran en realidad **dos versiones
del mismo Edificio A** (2017_67: "45 m E-I'" y "50 m E-J anexo"); **no incluía
el Edificio B**. Correcto en estructura/armado, pero incompleto. Aquí se
reutiliza su renderer y se alimenta con el complejo real (A + B).

1. **`src/benchmark_3d/exportar_unity_solido.py` (NUEVO)** — adaptador que toma
   A (nativo) + B (reducido) y emite `results/edificio_solido.json` en el
   esquema que espera el renderer sólido (`UnityStickModel.cs`):
   - A ya viene en ese esquema (mismo edificio del curso): pasa 1:1; solo se
     envuelve con bloque/offset y se normalizan los elementos `aspa`→viga
     (el renderer solo conoce column/wall/vigas_x/vigas_y).
   - B se convierte: nodos lista→dict; tipos pilar/muro/viga→column/wall/
     vigas_x|vigas_y; se sintetiza grilla + muros como paneles + nodos
     maestros (centroide por nivel) para la deformada. Los `brazo` se omiten.

2. **`unity/EdificioSolidoUnity/` (NUEVO)** — copia limpia del proyecto Unity
   del grupo (Assets/Scripts, Editor, Scenes/Main.unity, Packages con
   newtonsoft, ProjectSettings). Su escena ya trae `UnityStickModel` adjunto.
   Su `Assets/StreamingAssets/edificio_completo.json` = modelo completo A+B.

3. **`src/complejo.py`** — el pipeline ahora también genera el JSON sólido y lo
   copia al StreamingAssets del visor sólido (`_exportar_y_sincronizar_solido`).

4. **`abrir_unity_solido.cmd` (NUEVO)** — lanzador del visor sólido (Unity
   2022.3.62f3).

### Verificación

- Adaptador validado contra las clases C# del renderer: TODO elemento con tipo
  ∈ {column, wall, vigas_x, vigas_y} (crítico: un `aspa` reventaba el
  contenedor); conectividad ni/nj íntegra; muros con claves de grilla válidas;
  nodos maestros presentes en A (5) y B (5, sintéticos).
- A: 200 nodos / 343 elem. B: 240 nodos (235 + 5 maestros) / 315 elem (sin los
  35 brazos). Ambos con resultados G/Q/GQ/EX/EY → deformada disponible.
- Pipeline completo `python src/complejo.py` corre y sincroniza ambos visores.
- Suite: 81 passed (sin cambios; el contrato anidado del complejo intacto).

### Notas / limitaciones (honestas)

- **A renderiza completo** (columnas/vigas/muros sólidos + apoyos + terreno +
  flechas G/Q + tributarias + deformada).
- **B renderiza** columnas/vigas/muros sólidos + apoyos + terreno + flechas de
  sismo + deformada, PERO **sin flechas G/Q por viga ni mosaico tributario**
  (el modelo de cargas de B es más simple: no tiene tributario por viga ni
  carga muerta de losa; ligado al pendiente de la lámina 2024_22-700).
- La separación entre bloques: A en x=0, B en x=+60. El HUD tiene el slider
  "Separacion bloques Y" (default 60): dejarlo en 60 separa también en Y, o
  llevarlo a 0 para verlos lado a lado solo en X.
- El compilado/Play final se confirma en el editor de Unity (no ejecutable aquí).

### Pendientes (abren Sesión 5)

- [ ] Abrir `abrir_unity_solido.cmd`, pulsar Play y confirmar el render sólido
      del complejo A+B.
- [ ] (Opcional) Dar a B tributario por viga + carga muerta de losa (lámina
      2024_22-700) para que sus flechas G/Q y mosaico también aparezcan.
- [ ] **Oscar:** SAP2000 verificación cruzada.
- [ ] Commit/push del avance.

---

## 📅 Sesión 5 — Lunes 15 de septiembre 2026

**Participantes:** Nicolás + asesor técnico (Claude).

### Qué se hizo — CARGA MUERTA DE LOSA + SC validada en el Edificio B (Paso 1)

Se leyó la lámina de cargas **2024_22-700** (con ezdxf) y se completó el modelo
de cargas de B, que antes solo tenía el peso propio del marco.

**Datos extraídos de la lámina (capa HATCH CARGAS + planos 101/102 referenciados):**
- Espesor de losa: **LOSA e=15 cm** (gobierna; e=20 solo en zonas puntuales).
- PP losa = 0,15·2500 = 375 kgf/m² = **3,75 kN/m²**.
- PM. adicional (terminaciones) = 260 kgf/m² ≈ **2,55 kN/m²** (200 en algunas
  zonas; 1500 kgf/m lineal en tabiques de perímetro).
- **Carga muerta de losa Q_G = 3,75 + 2,55 ≈ 6,3 kN/m²** → idéntica al
  Edificio A (misma losa, mismo ingeniero M. Kupfer C., misma receta). Cruce
  de validación fuerte.
- Sobrecarga SC (zonificada): 500/300/200/100 kgf/m². Se adoptó, por decisión,
  el criterio **simple**: SC representativa = **3,0 kN/m²** en piso, 1,0 en
  cubierta (antes 2,5 supuesto).

**Cambios (aditivos):**
1. `src/edificio_b/cargas.py` — `Q_G_LOSA = 6.3`, `SC_PISO = 3.0`; nuevo
   `carga_muerta_losa(M)` (Q_G por área de planta, todos los niveles) y helper
   `area_planta(M)`.
2. `src/edificio_b/sismo.py` — `pesos_por_nivel` ahora suma la losa por nivel →
   masa sísmica W = G (marco + losa).
3. `src/edificio_b/analizar.py` — la losa entra en G y GQ; se exporta
   `carga_muerta_losa_kN` y `peso_muerto_G_kN`.
4. `src/benchmark_3d/fusionar.py` — el peso muerto del complejo usa el **G** de
   B (marco + losa), no solo el marco.
5. `tests/test_edificio_b/test_sismo.py` — V = α·G (antes α·marco); Σpesos = G.
6. `exportar_unity_solido.py` — q_losa de B con valores reales (G=6.3, Q=3.0).

### Números nuevos (verificados)

- **B: G = 48.171 kN** (marco 21.446 + losa 26.725) — antes 21.446. Q = 11.030 kN.
- **V sísmico B = 4.817 kN = 0,10·G** (antes 2.145; ahora incluye masa de losa).
- Equilibrio OK en los 5 casos; superposición máx 2,4e−11 kN.
- **COMPLEJO: G = 93.408 kN, Q = 18.701 kN** (A + B).
- Suite: **81 passed**.

### Nota

- Reparto de superficie **simple** (bounding box de planta / nodos), igual que
  la SC previa. La huella real de B es algo irregular, así que esto es
  ligeramente conservador. El reparto tributario por viga (Paso 2) afinaría
  esto y además habilitaría las flechas G/Q y el mosaico por viga en el visor.

### Pendientes (abren Sesión 6)

- [x] Paso 2 (visual): tributario por viga de B → flechas G/Q + mosaico. HECHO
      (Sesión 6). (Opcional: SC zonificada real.)
- [ ] Play en Unity del visor sólido con B ya completo.
- [ ] Oscar: SAP2000 verificación cruzada.  |  Commit/push.

---

## 📅 Sesión 6 — Lunes 15 de septiembre 2026

**Participantes:** Nicolás + asesor técnico (Claude).

### Qué se hizo — ÁREA TRIBUTARIA POR VIGA de B (Paso 2 visual)

Objetivo: obtener el área tributaria de B y su visualización (flechas G/Q por
viga + mosaico) como en A.

**Hallazgo:** la planta de B es irregular — con la regla estricta de A (panel
cerrado por 4 vigas) solo 2 de 48 paneles por nivel quedan cerrados (28,6 m²
vs 848 del bounding box). Las vigas de B NO tejan paneles limpios. Por eso se
usó la regla de **cuartos con FALLBACK**, robusta para plantas irregulares:
cada panel reparte q·área en partes iguales entre las vigas que existan en sus
bordes; si no hay ninguna, va a la viga más cercana que lo cruce; se acumula en
TOTALES [kN] por viga y la carga lineal es total/L (conservación exacta).

1. **`src/edificio_b/tributario.py` (NUEVO)** — `tributaria_por_viga(M)` →
   lista {tag, tipo, nivel, ni, nj, L, area, qG, qQ, G, Q} por viga.
2. **`src/edificio_b/analizar.py`** — exporta `cargas.vigas` en el JSON de B.
3. **`src/benchmark_3d/exportar_unity_solido.py`** — pasa `cargas.vigas` al
   visor sólido y enriquece la grilla del mosaico con los ejes de viga.
4. **`tests/test_edificio_b/test_tributario.py` (NUEVO)** — conservación y
   contrato de cargas.vigas.

### Verificación

- **215 vigas** con carga tributaria; área tributaria = 4.223 m² = **99,6%** de
  la huella (bbox×5). El 0,4% restante son esquinas fuera de la planta.
- G tributario = 26.606 kN (coherente con la losa aplicada 26.725 kN, 99,6%).
  Q tributario = 10.980 kN.
- Suite: 81 → **83 passed** (2 tests nuevos).

### En el visor sólido, para B ya aparecen:

- **Áreas tributarias** (toggle "Areas tributarias 45"): mosaico por viga con
  kN de losa por región.
- **Flechas G / Q por viga** (toggles "Cargas G/Q (vigas)").
- (más lo ya existente: columnas/vigas/muros sólidos, apoyos, sismo, deformada).

### Nota

- El tributario usa la huella (bounding box) como extensión de planta, igual
  que la carga de losa del Paso 1 → ambos coherentes. Si se quisiera la huella
  EXACTA (planta en L / patios), habría que extraer el polígono de losa
  (capa RLE-LOSA de los planos 101/102) — refinamiento opcional.

### Pendientes (abren Sesión 7)

- [ ] Play en Unity del visor sólido: confirmar mosaico + flechas de B.
- [ ] (Opcional) Huella exacta de B desde el polígono de losa / SC zonificada.
- [ ] Oscar: SAP2000.  |  Commit/push.

---

## 📅 Sesión 7 — Miércoles 16 de septiembre 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo — DIAGRAMAS DE VIGA (pasada dedicada) + consulta interactiva en visor sólido

Objetivo: exportar coeficientes de esfuerzos por viga y caso (G/Q/GQ/EX/EY) y
permitir leer N(x), V(x), M(x) en cualquier punto de una viga del Edificio B
en el visor sólido de Unity, **todo aditivo** (sin tocar IDs, geometría,
construir.py, verificar.py ni el análisis nodal existente).

1. **`src/edificio_b/esfuerzos.py` (NUEVO)** — pasada dedicada de diagramas con
   el modelo/esquema/solver de `analizar.py` (ModelElastic3D, rigidDiaphragm,
   BandGeneral/RCM/Transformation/LoadControl). Para cada caso G/Q/GQ/EX/EY:
   `w` por viga (G → qG+γ·A, Q → qQ, GQ → qG+qQ+γ·A, EX/EY → 0; γ=25 kN/m³) y
   exporta por viga `{L, N, Vy, Vz, T, My, Mz, Wy=0, Wz=-w}` desde
   `ops.eleResponse(e,'localForce')` (extremo i). Permite evaluar con las
   fórmulas del enunciado: `N(x)=-N`, `Vz(x)=Vz+Wz·x`, `My(x)=My+Vz·x+Wz·x²/2`.
   Incluye `verificar_diagramas()`: equilibrio global por caso (ΣR+Σaplicada≈0)
   y cierre del diagrama por viga (`|Vz(L)+Vzj|` y `|My(L)+Myj|` < 1e-6·max).
2. **`src/edificio_b/analizar.py`** — bloque `esfuerzos` en `exportar_json`
   (paso de `correr()`), **sin** alterar `resultados`/`cargas`/`apoyos`.
3. **`src/benchmark_3d/exportar_unity_solido.py`** — propaga `esfuerzos` a `B`
   y a `A` (clave vacía en A → vigas de A dirán "sin datos").
4. **`tests/test_edificio_b/test_esfuerzos.py` (NUEVO, 5 tests)** — cierre por
   viga/caso, equilibrio global por caso, fórmulas de evaluación y contrato
   (JSON de B y `edificio_solido.json` incluyen el bloque con las 5 casos).
5. **Unity (visión sólida), `ModeloComplejo.cs` + `UnityStickModel.cs`:**
   - `EsfuerzosVigaModelo` (L,N,Vy,Vz,T,My,Mz,Wy,Wz) y campo `esfuerzos`
     en `ModeloEdificio`.
   - Vigas seleccionables: se conserva su `BoxCollider` (antes destruido) y un
     **raycast** en clic izquierdo (excluye Alt+clic de la órbita y los rects
     GUI) seleccionan la viga y la **resaltan** (material estándar con emisión).
   - Panel de consulta: toggle de activación, etiqueta Bloque/Tag/Nivel,
     selector de caso (G/Q/GQ/EX/EY), slider de posición 0..1 y lecturas en
     vivo de `L`, `x`, `N(x)`, `Vz(x)`, `My(x)`, `Vy(x)`, `Mz(x)`, `T(x)`.
     Vigas sin bloque (Edificio A) muestran "Sin datos de esfuerzos".

### Verificación

- Pasada de diagramas: **215 vigas** en los 5 casos, equilibrio fx/fy/fz en
  1e-10..1e-9 (OK) y cierre OK en todos los casos.
- `python src\complejo.py` completo OK (A+B+unión+PNG+HTML+Unity solido).
- Suite: **88 passed** (83 previos + 5 nuevos de esfuerzos).
- Visual: no se alteran materiales/luces/cámara existentes; los cubos de viga
  ahora conservan colisionador, nada más.

### Pendientes (abren Sesión 8)

- [x] Abrir `EdificioSolidoUnity` en el editor Unity y pulsar Play: clic sobre
      una viga de B → resaltado + panel con lecturas N/Vz/My según caso y
      posición. **DONE (Sesión 7b, ver abajo):** el visor se abre con el modelo
      visible ya en modo edición.
- [ ] Probar en Play la selección de viga (clic izquierdo) + lecturas vivas.
- [ ] (Opcional) Marcar el punto consultado sobre la viga (esfera/gizmo).
- [ ] Oscar: SAP2000.  |  Commit/push.

---

## 📅 Sesión 7b — Miércoles 16 de septiembre 2026 (misma tarde)

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo — DESBLOQUEO DEL VISOR EN UNITY (compilación + escena + visibilidad en editor)

Problema reportado por Oscar: al abrir el proyecto en Unity "no se veía nada"
(solo fondo celeste/suelo plomo). Tres causas encontradas y resueltas:

1. **Error de compilación C#** (dialogo "Enter Safe Mode?"): variable duplicada
   `nuevoCaso` en `UnityStickModel.cs` dentro de `OnGUI` (chocaba con la de
   "Deformada por caso") → renombrada a `nuevoCasoConsulta`. Confirmado vía
   `Editor.log` (error CS0136) y recompilación limpia.
2. **La escena abierta era "Untitled" (vacía), no `Main.unity`.** El editor
   restauraba una sesión previa sin escena nombrada; además `-openfile` vía
   PowerShell no aplicaba. Se resolvió ejecutando en **batchmode** el menú ya
   existente `Tools/MCOC/Preparar escena Main` (`MCOCSceneSetup.PrepararEscena`),
   que abre y guarda `Assets/Scenes/Main.unity`; al relanzar el editor queda esa
   escena y ahora sí se cargan `Main Camera` (OrbitCamera) + `StickModel`.
3. **El modelo solo se construía en Play** (`Start`), y `Start` no se relanza en
   recargas de dominio con backup → en modo edición la escena estaba vacía.
   Cambios en `UnityStickModel.cs`:
   - `[ExecuteInEditMode]` en la clase → el modelo y el panel se ven ya en modo
     edición, sin necesidad de pulsar Play.
   - Trigger de carga movido/aditivo a `OnEnable()` (además de `Start()`),
     guiado por `bloques.Count == 0` → reconstruye en cada activación.
   - Helper `Destruir()` (mode-aware) reemplaza los 7 `Object.Destroy` de la
     construcción (valen en edición y en Play).
   - `ProcesarSeleccionViga()` y `ReconstruirPanelesDeformados()` solo corren en
     Play (`Application.isPlaying`).
   - **Diagnóstico**: `LogArchivo()` escribe `BuildLog_unity.txt` en la raíz del
     proyecto (OnEnable/Start/Cargar: path, existencia de archivo, parse,
     ConstruirEscena, excepciones).

### Verificación

- Batchmode exit (0), sin `error CS`; editor relanzado con escena Main.
- `BuildLog_unity.txt`: `[OnEnable] bloques=0` → `parse OK` → `ConstruirEscena OK
  bloques=2` (2 edificios construidos en edición) y sin excepciones en el log.
- `python -m pytest tests\ -q` sigue en **88 passed** (no se tocó Python aquí).
- El flujo Python/JSON no cambió esta sesión (ningún resultado regenerado).

### Pendientes (abren Sesión 8)

- [ ] En Play: clic izquierdo sobre una viga de B → resaltado + panel con
      N/Vz/My/T según caso (G/Q/GQ/EX/EY) y posición (slider 0..1).
- [ ] (Opcional) Marcar sobre la viga el punto consultado (esfera/gizmo).
- [ ] (Opcional) Limpiar `BuildLog_unity.txt` del proyecto (solo diagnóstico).
- [ ] Oscar: SAP2000.  |  Commit/push.

---

## 📅 Sesión 8 — Miércoles 16 de septiembre 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo — DIAGRAMAS DE VIGA del EDIFICIO A (esfuerzos, paridad con B)

Objetivo: replicar para el Edificio A la pasada dedicada de diagramas que ya
existía para B (Sesión 7), usando los módulos nativos de A en `src/benchmark_3d/`.
**Todo aditivo**: no se tocó geometría, IDs, `construir.py`, `voladizos.py`, el
análisis nodal de A ni nada del Edificio B (sus totales y su bloque esfuerzos
quedaron idénticos).

1. **`src/benchmark_3d/esfuerzos.py` (NUEVO)** — espejo de `edificio_b/esfuerzos.py`
   con la API de A:
   - Cada caso G/Q/GQ/EX/EY construye un modelo nuevo con
     `analizar.construir_con_voladizo(dat)` (mismo solver BandGen/RCM/
     Transformation/LoadControl/Linear) y extrae `ops.eleResponse(e,'localForce')`
     (extremo i) para todas las `vigas_x`/`vigas_y`
     (116+112 = **228 vigas**, incluye nervaduras/vigas_borde del voladizo
     trasero y las del voladizo metálico de piso 1).
   - Cargas por viga: `w(G)=qG_trib + pp_viga`, `w(Q)=qQ_trib`,
     `w(GQ)=qG+qQ+pp_viga`, `EX/EY=0`, con `qG/qQ` de
     `cargas.distribuir_tributaria(Q_G|Q_Q, modelo)` (misma fuente que
     `_exportar_cargas`) y `pp_viga = A_pp·rho_pp` (ρ del material de cada viga:
     hormigón ó acero) → elevado con `-beamUniform 0.0 -w`.
   - Peso propio de columnas/muros/aspas al nodo superior (`ρ·A_pp·L`), solo en
     G y GQ (igual que `cargas_gravedad`).
   - EX/EY aplican solo `cargas.patron_sismico("X"/"Y")`.
   - Contrato exportado por viga y caso: `{L,N,Vy,Vz,T,My,Mz,Wy:0,Wz:-w}`
     (evaluable con `N(x)=-N`, `Vz(x)=Vz+Wz·x`, `My(x)=My+Vz·x+Wz·x²/2`, …).
   - `verificar_diagramas()` reporta equilibrio global por caso (ΣR+Σaplicada≈0)
     y cierre del diagrama por viga (`Vz(L)=-Vzj`, `My(L)=-Myj` con tol
     `1e-6·max(1,|valor|)`).

2. **`src/benchmark_3d/analizar.py`** — `exportar_json(...)` acepta (aditivo)
   `esfuerzos=None`; `run_analisis` corre la pasada y la escribe en el JSON de A
   (bloque `esfuerzos`; `resultados`/`cargas`/`apoyos` intactos).

3. **`src/benchmark_3d/exportar_unity_solido.py` — SIN CAMBIOS** (ya propagaba
   `data_a.get("esfuerzos", {})`); la clave deja de salir vacía para A.

4. **`tests/test_esfuerzos_a.py` (NUEVO, 6 tests)** — cierre por viga/caso,
   equilibrio global por caso, **discriminador del peso propio**
   (`Wz(G)+Wz(Q)=Wz(GQ)` con 0 fallas ⇒ la sobrecarga Q no arrastra PP), fórmulas
   de evaluación, contrato del JSON de A y del `edificio_solido.json`. (Nombre
   `_a` para no colisionar con `test_edificio_b/test_esfuerzos.py`.)

### Verificación

- Pasada de diagramas de A: **228 vigas** en los 5 casos; equilibrio fx/fy/fz en
  ~1e-11..1e-9 (OK) y cierre OK en todos los casos. Discriminador Wz: 0 fallas.
  (Sin vigas inclinadas reales — 0 con dz>0.05 — así que `Wz=-w` exacto.)
- `python src\complejo.py` completo OK: A (G=45.236, Q=7.671) y B (G=48.171,
  SC=11.030) **idénticos**; COMPLEJO G=93.407,6 kN / Q=18.700,8 kN intactos.
- `modelo_resultados.json` y `edificio_solido.json` traen el bloque `esfuerzos`
  de A (5 casos × 228 vigas); en el visor sólido las vigas de A ya consultables.
- Suite: **94 passed** (88 previos + 6 nuevos de esfuerzos de A).

### Pendientes (abren Sesión 9)

- [ ] Commit + push del fix de visor (escena mínima, EditorBuildSettings, README).
- [ ] (Opcional) Tag `` entrega-v1 `` para Canvas.
- [ ] En Play del visor sólido: clic sobre una viga del Edificio A → panel con
      N/Vz/My/T (G/Q/GQ/EX/EY) igual que B.
- [ ] (Opcional) Marcar sobre la viga el punto consultado (esfera/gizmo).
- [ ] Oscar: SAP2000.

---

## Ajuste de visor sólido (Sesión 8, después)

**Síntoma:** los compañeros clonando el repo veían "un modelo muy feo, como
deforme" al abrir Unity, distinto al que Oscar ve localmente.

### Diagnóstico (causa raíz)

- En el repo había **dos proyectos Unity** con visores distintos:
  - `unity/EdificioSolidoUnity/` — **visor oficial** (sólido 3D, consulta de
    vigas A+B). Lee `StreamingAssets/edificio_completo.json`.
  - `unity/EdificioComplejoUnity/` — **visor viejo** (líneas `UnityComplejo`/
    `UnityStickModel` anclado a `modelo_resultados.json`). No es el de entrega.
- `EdificioSolidoUnity/Assets/Scenes/Main.unity` pesaba **64 MB** porque traía
  **toda la geometría horneada** en la escena (9 595 GameObjects / 4 312
  MeshFilters, nombres tipo `etiqueta`, `punta`, `flecha_carga`, `tributaria`,
  `apoyo_empotrado`, `viga_L`, `Cylinder`), con valores serializados
  `amplificacion: 400` y `separarBloquesY: 60` — esa es la vista "deforme" que
  se podía mostrar sin reconstruir desde el JSON.
- `EditorBuildSettings.asset` estaba vacío (`m_Scenes: []`) en ambos proyectos
  → Unity **no abría `Main.unity` automáticamente** y, con la caché `Library/`
  en `.gitignore`, cada máquina arrancaba en una escena distinta/al azar.
- `MCOCSceneSetup.AutoPreparar` solo creaba la escena si NO existía; si existía,
  nunca la abría por sí solo.

### Cambios aplicados

1. **`EdificioSolidoUnity/Assets/Scenes/Main.unity` (REEMPLAZADA):** de 64 MB a
   **10 KB**. Escena mínima (Occlusion/Render/Lightmap/NavMesh + Main Camera con
   `OrbitCamera` + luz `Sol` + GameObject `StickModel` con `UnityStickModel` y
   `jsonFileName: edificio_completo.json`, `separarBloquesY: 0`). El modelo se
   reconstruye solo desde el JSON en `OnEnable`/`Start` → **todos ven lo mismo**.
   (La `.meta` con su guid se conserva intacta.)
2. **`EditorBuildSettings.asset`:** `m_Scenes` ahora incluye
   `Assets/Scenes/Main.unity` (guid `c3e4403ad283f6547896806ec87539d8`).
3. **`MCOCSceneSetup.cs`:** `AutoPreparar` ahora, si la escena ya existe, **abre
   `Main.unity` la primera vez que el editor arranca en la sesión**
   (`SessionState.GetBool/SetBool`), evitando secuestrar otra escena en recargas
   de dominio.
4. **`unity/README.md`:** cabecera con instrucción clara de qué proyecto abrir
   (`abrir_unity_solido.cmd` → escena `Main.unity` → Play) y qué NO abrir
   (`EdificioComplejoUnity`, visor viejo).

### Verificación / impacto

- El repo deja de arrastrar el `Main.unity` de 64 MB (repo más liviano y sin
  warning de GitHub por archivo >50 MB).
- Cualquier compañero que clone, abra `EdificioSolidoUnity` y pulse Play ve **el
  mismo visor sólido** de Oscar construido desde el JSON (no geometría horneada).
- Pendiente de probar en máquina limpia: abrir `abrir_unity_solido.cmd`, ver que
  `Main.unity` se abre sola y Play muestra A+B con el panel de vigas.

---

## 📅 Sesión 9 — Miércoles 16 de septiembre 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### ORDEN 1 — Patrón sísmico de B repartido por ÁREA TRIBUTARIA (no igual por nodo)

`src/edificio_b/sismo.py` (ADITIVO: no toca geometría, IDs ni pesos):

- El reparto de `F_k` entre los nodos esclavos de cada nivel pasó de **partes
  iguales** a **proporcional al área tributaria** del nodo (Σ `area`/2 de las
  vigas conectadas, tomado de `tributario.tributaria_por_viga(M)`). La
  resultante de cada planta cae ahora en el **centro de masa** de la losa
  (26,74; 21,98) m y no se induce torsión espuria por el reparto.
- Nodos sin área tributaria no reciben carga; respaldo igualitario si un nivel
  careciera de área.
- V = α·G **intacto** (4.817,1 kN = 0,10·48.171); equilibrio por caso y
  superposición OK; cierre de diagramas OK en los 5 casos (pasada re-corrida).

**Resultados regenerados:** `results/modelo_resultados_b.json` (EX/EY cambian
levemente), `results/edificio_solido.json`, `results/edificio_completo.json`
(vía `fusionar --sin-correr`) y copias de StreamingAssets.

| Magnitud | antes | después |
|---|---|---|
| rz techo EX | 0,778 mrad | **0,694 mrad** |
| ux techo EX | 17,06 mm | **16,00 mm** |
| rz techo EY | −0,710 mrad | **−0,621 mrad** |
| uy techo EY | 20,80 mm | **19,77 mm** |
| V EX / EY | 4.817,1 kN | **4.817,1 kN** (sin cambios) |
| Centro de masa (tributario) | — | **(26,74; 21,98) m** |

### ORDEN 2 — Brazos rígidos muro↔marco visibles en el visor sólido

Hoy los 35 brazos rígidos (tipo `brazo`) del Edificio B se descartaban en
`exportar_unity_solido.py` y los muros parecían flotando. Cambios (solo visual):

- **`src/benchmark_3d/exportar_unity_solido.py`** — los `brazo` ya no se omiten:
  se exportan como tipo dibujable `brazo` con su `ni`/`nj` (35 elementos de B).
- **`unity/EdificioSolidoUnity/Assets/Scripts/UnityStickModel.cs`** — contenedor,
  rama y material propios para `brazo`: cilindro fino (mesh propia de altura 1
  para que la longitud coincida con el tramo en `PosicionarElemento3D`) con
  material tenue translúcido (gris azulado, alpha 0,45). Toggle **"Brazos
  rígidos"** en el panel (default ON).
- El análisis no cambia por este orden (puramente visual).

### Verificación

- Suite: **94 passed** (`python -m pytest tests\ -q`).
- `edificio_solido.json`: B → 350 elem (40 column + 60 wall + 95 vigas_y +
  120 vigas_x + **35 brazo**), ni/nj íntegros.

### Pendientes (abren Sesión 10)

- [ ] Confirmar en Unity el toggle "Brazos rígidos" y que la conexión
      muro↔marco se vea en Play.
- [ ] Commit + push del avance.
- [ ] Oscar: SAP2000.  |  (Opcional) marcar punto consultado sobre la viga.

---

## Sesión 10 — Miércoles 16 de septiembre 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

**Tema: Semana 03 — motor de secciones ADITIVO (`src/secciones/`).**

Capa nueva 100% aditiva: no toca los modelos lineales A/B, sus IDs, geometría,
casos G/Q/GQ/EX/EY, sismo, tributario ni esfuerzos. Materiales del enunciado:
hormigón 25 MPa (`Concrete01`, εc0=−0,002, εcu=−0,004) y acero 420 MPa
(`Steel01`, b=0,01). Unidades SI (m, kN, kN/m²).

### Motor

- `materiales.py`, `seccion.py` (`columna_70x70`, `muro_b1`), `curva.py`
  (M–φ por fibras con `zeroLengthSection`), `analitica.py` (integrador de fibras
  puro en Python), `pm.py` (envolvente P–M + balanceado), `demandas.py`
  (demandas por caso reutilizando `esfuerzos._correr_caso`) y `exportar_unity.py`
  (overlay al contrato fused).
- **Receta M–φ validada:** `fix(2,0,1,0)`, axial con sub-incrementos (1/2/4/8),
  momento unitario con `DisplacementControl`; `M = getTime()`, `φ = nodeDisp(2,3)`.
  Fibras **explícitas** (`patch('rect')` no es fiable en este build).
- **Criterios de fin de curva:** `crushing`, `su_steel` (εsu=0,09), `softening`
  (M < 0,85·M_max), `no_convergencia`/`nincr`.
- **Convención reportada:** P > 0 = compresión; M = √(My²+Mz²). El signo de
  `localForce[0]` en este modelo es compresión positiva (verificado contra la
  reacción de base). La envolvente se barre en la convención del motor y se
  niega al reportar.

### Validación

| Verificación | Valor |
|---|---|
| P0 columna ACI / fibras | 12 377 / 14 097 kN |
| P0 muro ACI (As=0,003870 m²) | 38 646 kN |
| EI₀ columna 6/12/20 | 144 124 / 145 879 / 146 399 kN·m² |
| EI₀ analítico agrietado | 146 991 (coincide −0,8 %) |
| Balanceado columna | (4 370 kN, 1 158 kN·m) |
| Pico envolvente columna | **(4 379 kN, 1 530 kN·m)** (objetivo 4500,1395 ±10 %) |

### Resultados y hallazgos

- Muro 2,91×0,60: pico (19 065 kN, 15 167 kN·m), EI₀(0,5·P0) ≈ 2,59·10⁷, M_max
  15 197 (`softening`).
- Demandas de los 100 pilares/muros en 5 casos; **superposición G+Q ≡ GQ**
  verificada por componente (máx ΔP = 4,7·10⁻¹¹ kN, ΔM = 7,8·10⁻¹⁰ kN·m).
- D/C de las 40 columnas contra la envolvente: **solo 2 > 1,0** bajo GQ
  (`tag 5` = 1,13 y `tag 2` = 1,04). El modelo lineal con diafragma rígido
  induce flexión de pórtico gravitatoria apreciable: punto de diseño a revisar.
- Se **corrigió** un falso hallazgo previo: `M_max(P=0) ≈ 220 kN·m` era un
  artefacto del tope de pasos; el valor real es ~750–870 kN·m.

### Entregables

- `src/secciones/*` (+ `exportar_unity.py`), `scripts/semana03_run.py`,
  `tests/test_secciones.py` (15 tests).
- `results/secciones_semana03.json`; `results/edificio_solido.json` →
  `unity/EdificioSolidoUnity/Assets/StreamingAssets/edificio_completo.json`
  (enriquecido con bloque `secciones` en el Edificio B y campo `seccion` por
  elemento; **conserva** config/totales/edificios). Ojo: el visor sólido lee
  `edificio_solido.json` (esquema plano), NO el contrato fusionado
  `results/edificio_completo.json`.
- `src/complejo.py`: hook aditivo `_enriquecer_secciones_solido()` (se ejecuta
  tras generar el sólido; omite el overlay si falta el caché, sin romper nada).
- **Corregido** en `demandas.per_elemento`: los 20 muros del bloque superior
  (espesor 0,20, en `MUROS_BLOQUE_SUP_V/H`) caían por defecto en `col0.70x0.70`;
  ahora los 60 muros tienen su etiqueta `muro{e}x{L}` propia.
- Parte D Unity: `ModeloComplejo.cs` parsea el bloque `secciones`; en
  `UnityStickModel.cs` se conserva el collider de las columnas, se registran para
  raycast y hay panel P–M (envolvente + balanceado + demanda del caso activo con
  D/C). **Sin compilar aquí** (no hay Unity en el entorno).
- `reports/semana03.md` + `reports/fig/semana03_*.png`.
- **Suite: 109 passed** (`python -m pytest -q`).

### Verificación

```powershell
python scripts\semana03_run.py
python src\secciones\exportar_unity.py
python -m pytest -q
```

### Pendientes (abren Sesión 11)

- [x] **Catálogo P–M de las demás longitudes de muro** — HECHO (Sesión 10 cont.):
      `seccion.muro()` + `catalogo_muros.py` → 11 envolventes + overlay por
      elemento (`per_elemento` mantiene las 60 etiquetas de muro).
- [x] **Probar la Parte D en Unity** — el C# de selección/P-M quedó afinado
      (`InteraccionDe` usa `el.seccion` con fallback); falta solo compilar/Play
      en el editor (sin Unity en este entorno).
- [x] **Superposición §4 en A y B** — HECHO (Sesión 10 cont.): `superposicion.py`
      verifica G+EX y G+Q+EX (suma vs directa) con desvíos ≤ 6e-11 kN y
      ≤ 1,8e-16 m; `max_dR/max_dD` en caché + tests.
- [ ] Chequeo biaxial My–Mz (hoy conservador con el resultante).
- [ ] Commit + push del avance (Semana 03 completa).

---

## Sesión 10 (continuación) — Miércoles 16 de septiembre 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

**Tema: Semana 03 completada — catálogo P–M completo del Edificio B +
superposición §4 en A y B + reporte con las 9 secciones.**

### Catálogo P–M completo (11 muros + columna)

1. `seccion.py` — `muro_generico(tb, L, nombre, d_borde=0.016, d_alma=0.012,
   recub=0.05, sb=0.20)` y fábrica `muro()` con las 11 secciones distintas
   **etiquetadas idéntico a `per_elemento`**. `muro_b1` se refactorizó a
   `muro_generico(0.60, 2.91)` sin cambiar ni As (0,003870) ni P0_ACI (38 645,8).
2. `src/secciones/catalogo_muros.py` (nuevo) — envolvente P–M por sección
   (`curva_PM`, 17 pts, 12×12, dk=1e-4, nincr=2000); `completar_cache()` es
   **idempotente**; `muro0.60x2.91` reutiliza `curvas["muro"]`. Caché: 13 claves.
3. Warnings "failed to converge" del solver en post-pico/tracción pura: normales
   (capturados por `curva_PM` → M = 0).
4. Overlay asociativo: cada muro del contrato sólido lleva su sección; el C# de
   `UnityStickModel.InteraccionDe` ahora busca **`el.seccion`** en el catálogo
   con fallback `"col"/"muro"` (antes siempre la curva representativa).

### Superposición §4 (A y B) — suma vs corrida directa

`src/secciones/superposicion.py` (nuevo): corre el modelo combinado (G+EX y
G+Q+EX) directo en OpenSees y compara contra la suma de los casos individuales
en reacciones totales y `desplazamientos_maestro`, para A y B. Resultados:

| Edif. | G + EX | G + Q + EX |
|---|---|---|
| A | máx ΔR 4,8e-11 kN / ΔD 8,7e-18 m | 2,6e-11 kN / 1,4e-17 m |
| B | máx ΔR 5,5e-11 kN / ΔD 1,8e-16 m | 6,0e-11 kN / 1,7e-16 m |

(G + Q ya verificada por componente: ΔP 4,7e-11 kN, ΔM 7,8e-10 kN·m.) Se
persiste `superposicion.combinaciones` en el caché.

### Reporte `reports/semana03.md` — 9 secciones

Casos base G/Q/EX/EY (reacciones + techo), carga viva tributaria (A exacta,
B 99,6 % = sello bbox vs polígono), sismo pseudoestático W_i/F_i/V/ux/uy/rz,
catálogo P–M (tabla de 12 secciones con As/P0_ACI/P0_fibra/balanceado/pico),
superposición (3 combinaciones) y entregables. Figuras nuevas:
`semana03_muros_pm.png` y `semana03_mosaico.png`.

**Hallazgo torsión:** `rz` del Edificio B ≈ **70,1× el de A en el techo bajo EX**
(n1→n4: 38,8/43,7/48,2/52,3×) y 14,3× bajo EY: masa/muros excéntricos del B
(bloque superior con pilares, núcleo sur). Punto de diseño a revisar (no es
defecto del motor).

### Verificación

- Suite completa: **116 passed** (109 previos + 7 nuevos: muro_generico≡muro_b1,
  11 secciones, P0f/P0a ∈ (1,05;1,30), curvas por muro, muro representativo
  idem, superposición combinaciones, overlay curva propia por muro).
- Pipeline `scripts/semana03_run.py` corre de punta a punta (idempotente,
  reusa caché): curvas → demandas → §4 → figuras → overlay ("100 elementos
  etiquetados de 350"), sin `--recalcular`.

### Cómo correr

```powershell
python scripts\semana03_run.py            # (--recalcular fuerza recálculo)
python -m pytest -q                       # 116 passed
```

---

## 📅 Sesión 12 — Viernes 18 de septiembre 2026

**Tema: Semana 04 PARTE A — Unity como postprocesador estructural: fuerzas
completas por elementTag de TODOS los elementos + metadatos (restricciones,
material, ejes locales) en el visor sólido.**

### Qué se hizo

- `src/secciones/diagramas.py` (nuevo): por elemento estructural y caso
  G/Q/GQ/EX/EY exporta los 9 coeficientes {L,N,Vy,Vz,T,My,Mz,Wy,Wz} con la
  misma convención verificada en semana 03: `N(x) = -N` (N>0 = compresión),
  momentos con repartición cuadrática, PP nodal (`Wy=0`) y gravedad `Wz = -w`
  solo en vigas. Reutiliza las pasadas `_correr_caso` (no toca el análisis).
- `esfuerzos_completos` (B) y `esfuerzos_completos_A` (A) indexados por
  elementTag, con `metadatos` por tag: tipo de visor (`column`/`wall`/
  `vigas_x`/`vigas_y`), sección de catálogo (fallback `viga0.60x0.80`),
  material (H30/G35), longitud, restricción de cada extremo
  (`empotrado`/`maestro_diafragma`/`esclavo_diafragma`/`libre`) y ejes locales
  {x̂=(nj−ni)/L, ẑ=proyección vertical ⊥ x̂, ŷ=ẑ×x̂} — idéntico a los vecxz de
  OpenSees reproduciendo transformaciones (1,0,0)-(0,0,1).
- Cobertura igual al contrato del visor sólido: **B 315** por caso (40
  columnas + 60 muros + 215 vigas), **A 343** (79 + 32 + 228 vigas + 4 aspas).
- Verificación `verif_semana04`: equilibrio global ΣR + Σaplicada ≈ 0 y cierre
  de elementos verticales dN = dVz = dMy ≈ 0 (máx 1,1e-13 kN) por caso y
  edificio.
- Overlay aditivo en `exportar_unity.py`: escribe `esfuerzos_completos` y
  `metadatos` (idempotente, con/sin sufijo `_A`) en `results/edificio_solido.json`
  y copia a `StreamingAssets`.
- Runner `scripts/semana04_esfuerzos_completos_run.py`: reutiliza el caché
  (`--recalcular` repite las 10 corridas), cache en
  `results/secciones_semana04.json`.
- Tests nuevos en `tests/test_secciones.py` (sector "semana 04"): esquema de
  coeficientes por caso, metadatos/cobertura/ejes unitarios/restricciones,
  evaluador (paridad N(x) = -N y M cuadrático con la semana 03),
  compresión de columnas bajo G/GQ, |M| de muros ex sísmicos, equilibrio +
  cierre vertical, y overlay end-to-end sobre `edificio_solido.json`.

### Valores verificados

| Magnitud | Edificio B | Edificio A |
|---|---|---|
| Columnas G — N (compresión) | 462,5 – 7.837 kN | 3,7 – 3.586 kN |
| Muro de mayor |M| bajo EX | tag 91 = 24.195,6 kN·m | tag 100 = 20.951,6 kN·m |
| Cubrimiento por caso | 315/315 | 343/343 |

Los valores de columna y muro coinciden **exactamente** con las demandas de
semana 03 (mismo motor, misma pasada) — solo se añade la segmentación
completa y los metadatos.

### Verificación

- Suite completa: **133 passed** (126 + 7 nuevos de semana 04).
- Sobre la marcha se corrigió un desliz en `diagramas.evaluar`: `My(x)` usaba
  `Wy` en lugar de `Wz` (no afecta el caché, que se genera desde `localForce`).

### Cómo correr

```powershell
python scripts\semana04_esfuerzos_completos_run.py   # (--recalcular repite)
python -m pytest -q                                  # 133 passed
```

### Pendientes (abren Sesión 13)

- PARTE C: `reports/semana04.md` con la cadena de trazabilidad.



## 📅 Sesión 13 — Viernes 18 de septiembre 2026

**Tema: Semana 04 PARTE B — panel unificado y diagramas 3D en el visor sólido
C# (`EdificioSolidoUnity`).**

### Qué se hizo

- **Mapeo `Posicion()` confirmado**: coordenadas SI `(x, y, z)` → Unity
  `(x + offset.x, z, y + offset.y)` (SI z → Unity Y; plano SI xy → Unity XZ).
  Los ejes locales de los metadatos se pasan a Unity con `(vx, vy, vz) → (vx,
  vz, vy)`, base para los diagramas.
- `ModeloComplejo.cs`: campos JSON nuevos en `ModeloEdificio`:
  `esfuerzos_completos` y `metadatos` (Json.NET, sin tocar el resto) + clases
  `MetadatoElemento` (tipo, seccion, material, L, nodos, ejes_locales),
  `NodosMetadato` (ni, nj, i, j) y `EjesLocalesModal`.
- `UnityStickModel.cs`: clase `ConsultaS4` (bloque + ev + tag) y diccionarios
  `consultasPorObjeto` / `consultaPorTag` por bloque — TIPOS visor column,
  wall, vigas_x, vigas_y (los muros no tienen objeto 3D: solo seleccionables
  por el selector manual de tag con flechas ◀▶). La selección reemplaza los
  paneles "Consulta de viga" + "Consulta P–M" por UN panel unificado
  `DibujarPanelConsulta` (metadatos, selector de tag/caso/x, valores
  N/Vz/Vy/My/Mz/T, toggle "Diagramas 3D", y la sección P–M del motor de
  semana 03 reutilizando `SeccionDesdeTag` sobre el catálogo `secciones` — sin
  duplicar curvas). El caso actúa sobre las claves de los esfuerzos.
- **Diagramas 3D**: `ReconstruirDiagramas3D` + `CrearLinea3D` dibujan en el
  mundo (LineRenderer hijos de `diagramas3d`) las curvas **N (verde), My
  (azul), Mz (roja)** con la convención verificada (`N(x)=-N`, M cuadráticos),
  superpuestas a la pieza real como en días hábiles de semana 03. Auto-escala
  por curva a ~15 % de la luz (cada uno a su máximo para que el modo sea
  visible), casi planos (offsets solo fuera de ex/ey/ez); regeneración
  dirigida por una clave `Objeto|tag|caso|x` y colores originales recuperados
  al deseleccionar.
- Corrección de tipos: `seccion` de metadatos pasa a emitir el **nombre de
  sección (string)** (p. ej. `col_A_0.70x0.70`), no el dict de `per_elemento`
  (Json.NET espera string). Se corrigió `diagramas._metadatos` y se saneó el
  caché existente (211 columnas/muros) regenerando el overlay.
- Compilación verificada en **Unity 2022.3.62f3** (batchmode):
  `Assembly-CSharp.dll` compiló sin errores (5,97 s).

### Verificación

- Suite completa: **133 passed**.
- Overlay regenerado: `semana04 esfuerzos_completos B=True A=True`, cobertura
  B 315 / A 343, spot values intactos (col G N 462,5–7.837 kN; muro EX tag 91
  = 24.195,6 kN·m; A tag 100 = 20.951,6).

### Cómo correr

```powershell
python scripts\semana04_esfuerzos_completos_run.py   # overlay desde caché
python -m pytest -q                                  # 133 passed
```

- Validación visual pendiente en el editor Unity (panel + diagramas), sobre
  todo el selector manual de tag para muros.

### Pendientes

- Probar en Unity el panel unificado y diagramas 3D (Play) y revisar la curva
  de muro bajo EX en planta vs la pieza.
- PARTE C: `reports/semana04.md` con la cadena de trazabilidad (casos →
  fuerzas completas → metadatos → overlay → verificación → valores).



## 📅 Sesión 14 — Viernes 18 de septiembre 2026

**Tema: Semana 04 PARTE B2 — BUG del P‑M de columnas + ventana de diagramas
con selector de esfuerzo.**

### Diagnóstico del BUG (data intacta)

- En el Edificio B, `per_elemento` referencia la columna por su nombre real
  (`col0.70x0.70`) pero el catálogo P‑M de semana 03 solo trae la clave
  representativa `col` → sin match exacto. En el Edificio A sí calza
  (`col_A_0.70x0.70`). El panel unificado ya llamaba el parseo, pero en B
  resolvía a la curva genérica "col" o no usaba la sección real.

### Qué se hizo

- **Overlay (`exportar_unity.py`)**: al construir el bloque `secciones`, cada
  sección real referenciada en `per_elemento` que no esté en el catálogo se
  agrega como **alias de la representativa de su tipo** (`col0.70x0.70` →
  `col`; aditivo, no borra la original). El B ahora hace **match exacto** igual
  que A (`col_A_0.70x0.70`).
- **Lookup C# robusto (`SeccionDesdeTag`)**: match exacto → clave
  representativa por tipo (`pilar→col`, `muro→muro`) → primera clave del
  catálogo con ese prefijo. Toda columna/muro de A o B resuelve su curva P‑M,
  y el panel redibuja la texPM con el caso/combinación activo al cambiar tag.
- **Ventana de diagramas (`DibujarVentanaDiagramas`)**: GUI.Window arrastrable
  dibujada tras el panel de metadatos cuando hay consulta activa (toggle
  "Ventana de diagramas (arrastrable)"). Deja elegir **Axial (N) / Corte
  (Vz·Vy) / Momento (My·Mz)** con toggles; grafica el esfuerzo a lo largo del
  elemento (`N(x)=-N`, `V(x)=V+W·x`, `M(x)=M+V·x+W·x²/2` — lineal en
  columnas/muros, parabólico en vigas) en una textura generada por software
  (cian) con extremos i/j (blancos) y marcador del slider (amarillo); muestra
  **i/j, máximo con su posición y valor en x**; comparte `posConsulta` y
  `casoConsulta` con el panel, así que sigue al elemento y caso seleccionado.

### Verificación

- Suite completa: **134 passed** (133 + `test_semana04_pm_columnas_todas_resuelven_catalogo`,
  que replica el resolver exacto/representativa/prefijo para TODAS las
  columnas y muros de A y B sobre el JSON del visor y valida el envelope).
- Overlay regenerado: el catálogo de B ahora lista `... 'col0.70x0.70'` al
  final (alias añadida); `semana04 esfuerzos_completos B=True A=True`,
  cobertura B 315 / A 343, spot values intactos.

### Cómo correr

```powershell
python scripts\semana04_esfuerzos_completos_run.py   # overlay + alias
python -m pytest -q                                  # 134 passed
```

### Segunda pasada — BUG de layout: P‑M fuera de pantalla

- El panel de consulta usa `GUILayout.BeginArea(new Rect(16,16,380,700))` sin
  scroll, y el bloque "Consulta P‑M" + la textura `texPM` (240×170) se dibujaban
  al final, más allá de los 700 px → quedaba recortado (el dato y
  `GenerarTexPM` eran correctos: 17 puntos por envolvente). Se eligió la
  **opción B** (mejor UX): el bloque P‑M vive ahora en su **propia
  GUI.Window arrastrable** (`DibujarVentanaPM`, id 51002, rect que sigue al
  elemento/caso activo) con toggle en el panel de metadatos; el panel solo
  muestra un aviso "P-M de <seccion> en la ventana (arrastrable)". Así se ve
  completo y no compite por espacio con la metadata o la ventana de diagramas.
- Batchmode re-verificado con el editor cerrado: **compila sin errores**
  (2,2 s, sin `error CS`). Suite: **134 passed** (sin cambios en Python esta
  pasada).

### Tercera pasada — Muros seleccionables por clic

- Los muros NO eran seleccionables con el mouse: sus paneles
  (`CrearPanelMuro`/`ReconstruirPanel`) no tenían collider ni registro en el
  raycast; solo el cicleador ◀/▶ alcanzaba sus tags (impráctico con cientos).
- Ahora `CrearPanelMuro` agrega un **BoxCollider fijo al muro sin deformar**
  (el transform del GO/contenedor es identidad, así las coords locales del
  collider coinciden con el mesh que ya embebe `bv.offset`; la deformada solo
  reconstruye el mesh y el collider basta para seleccionar). Tamaño: `t
  x(z_top-z_bot) x L` orientado según `resisteY`.
- Registro `panelesPorObjeto[go] = p` + rama en `ProcesarSeleccionViga`:
  `SeleccionarElementoDeMuro(p, hit.point.y)`. Como un panel abarca todos los
  niveles y hay un elemento por nivel, `hit.point.y` (cota `niveles_z`) →
  nivel `st` → match por posición de planta (nodo x/y contra `xc,yc,L,t`) y el
  nodo base cuyo `z == niveles_z[st]` → **tag del elemento de muro** → abre la
  misma `ConsultaS4` (ID, nodos, sección, material, ejes, restricciones,
  N/Vy/Vz/T/My/Mz, diagramas 3D y ventana P‑M con su demanda), igual que
  columnas/vigas. Respaldo: si no hay match de nivel, cae al elemento base del
  muro y el ◀/▶ recorre los niveles.
- Aceptación verificada en datos: nodos de muro (ej. tag 76) tienen
  `ni.z == niveles_z[st]` y `ni.nivel == st` (A: [-4.21,…,11.83]); `resisteY`
  y bandas cx/cy separan las líneas de muro. Batchmode **compila sin errores**
  (5,9 s). Suite: **134 passed** (sin cambios Python).

### Pendientes / nota

- Validar en Play del editor: seleccionar columna/muro de A y B → la ventana
  P‑M muestra la envolvente + balanceado + demanda del caso activo completo
  (sin cortes), y arrastrar las dos ventanas (diagramas y P‑M).
- PARTE C: `reports/semana04.md`.


## 📅 Sesión 15 — Martes 22 de septiembre 2026

**Tema: Semana 04 PARTE C (solo documento, sin tocar código).**

### Qué se hizo

- `reports/semana04.md` (nuevo): Unity como postprocesador conectado a
  resultados OpenSees verificados. Contiene: objetivo; selección de elementos
  con panel unificado (tabla de ejemplo por tipo — viga B tag 106, columna B
  tag 1, muro B tag 41 — con valores reales del JSON, caso GQ); visualización
  por capas (deformada por caso, diagramas 3D N/My/Mz, ventana 2D, tributaria
  45, cargas G/Q/sismo, apoyos) con marcadores `[CAPTURA: …]` para Nicolás;
  demanda–capacidad P–M de columna y muro (tag 1 GQ D/C 0,51; tag 2 GQ 1,05;
  tag 41 GQ 0,74 y EY 1,78); **cadena de trazabilidad** de punta a punta con el
  ejemplo real del muro tag 41 (`esfuerzos_completos.GQ."41"`: N = 2 175,21 kN,
  Mz = 3 909,15 kN·m → `metadatos."41"`/`secciones.elementos."41"` →
  `muro0.60x2.91` → P‑M (2 175,2; 3 912,3) → M_cap 5 270,9 → D/C 0,74); mapeo
  a la rúbrica (5 items) y reproducción.
- Nota de convención documentada: el panel evalúa `N(x) = −N` (tracción
  positiva) mientras la ventana P‑M usa compresión positiva (mismo dato).

### Verificación

- Suite completa: **134 passed** (sin cambios de código esta sesión).


## Sesión 16 — Martes 22 de septiembre 2026

**Tema: Semana 05 PROMPT 2 — modificaciones reales del modelo con
actualización automática en Unity, superposición interactiva y build Android.**

### Qué se hizo

- **CICLO 0 (baseline)**: `python -m edificio_b.analizar` → B G = 48 171,1 kN
  (marco 21 445,6 + losa 26 725,5), Q = 11 029,6 kN, EX u_techo = 15,996 mm,
  EY = 19,771 mm, M_volc = 76 424 kN·m, `equilibrio=OK superposición=OK`.
- **CICLO A — MOD1 (SC_PISO 3,0 → 4,0 kN/m²)**: Q pasa a **14 423,3 kN**
  (exacto al esperado 17·848,4); tag 41 GQ: P_d 2 175,21 → 2 277,64 kN,
  M_d 3 912,35 → 4 232,41 kN·m; esfuerzo GQ N/Mz → 2 277,64 / 4 228,96;
  equilibrios OK. Restaurado SC = 3,0.
- **CICLO B — MOD2 (muro MUROS_V[0] t 0,60 → 0,70 m)**: G 48 171,1 → 48 315,2;
  EX u_techo 15,996 → 15,744 mm; P0_aci 38 645,8 → **44 829,6 kN**;
  P0_fibra 45 101,4 → 52 376,4; D/C GQ tag 41 0,74 → 0,79 (M_cap 5 270,9 →
  5 573,4); `--force-curvas` añadió `muro0.70x2.91`. Restaurado t = 0,60.
- **CICLO C (canónico)**: restaurados los dos archivos, regenerada la cadena
  completa y suite `python -m pytest tests` → **134 passed**. La ruta completa
  `pytest` sin argumentos choca con `para_entrega/test_secciones.py` (basename
  duplicado, preexistente); la suite entregable es `tests/`.
- **Script nuevo** `scripts/semana05_refrescar_caches.py`: refresca demandas /
  per_elemento / §3 G+Q vs GQ / §4 superposición directa / curvas de muro
  nuevas (`--force-curvas`), con **una sola escritura** final (se corrigió un
  bug de doble escritura que pisaba demandas nuevas con el cache viejo).
- **`reports/semana05.md`** (6 secciones + capturas): funciones; MOD1/MOD2 con
  tablas antes→después; superposición interactiva (G+Q≡GQ, G+EX, G+Q+EX con
  max_dP 4,68e-11 y max_dR ≤ 6e-11); sidequest carga móvil documentada como
  **no implementada en v1**; UX estructural (6 preguntas con números reales);
  preparación móvil.
- **`Assets/Editor/MCOCBuildAndroid.cs`** (aditivo): menú
  `Tools/MCOC/Build Android` → escena `Assets/Scenes/Main.unity`, package
  `com.mcoc.edificiocomplejo`, min SDK 22, landscape (LandscapeLeft), IL2CPP,
  `bundleVersion 1.0`, salida `build/EdificioComplejo_MCOC.apk`. Verificado que
  `ProjectSettings.asset` ya traía `AndroidMinSdkVersion: 22` y orientación 4;
  sin compilar en batchmode en esta sesión.

### Verificación

- Canónico idéntico al baseline (tag 41 GQ P = 2 175,21 / M = 3 912,35;
  max_dR GQ B = 2,9e-11). Suite **134 passed**.
- Números de MOD1/MOD2 reproducibles con la cadena de §2 del reporte.

### Pendientes / nota

- Probar el build Android manualmente (menú Tools/MCOC/Build Android) con el
  SDK instalado y validar en teléfono (paisaje, JSON embebido en StreamingAssets).
- Sidequest "carga móvil": solo documentada (NO implementada en v1), alcance
  v2 en §4 de reports/semana05.md.


## Sesión 17 — Martes 22 de septiembre 2026 (FINAL — preparación de la entrega)

**Tema: Semanas 4–5 completas + entrega lista para Canvas.**

### Qué se hizo

- **Verificación final limpia**: `python -m pytest tests/ -q` → **134 passed**
  (7,9 s) y `python src\complejo.py --sin-visualizar` regenera análisis, fusión
  y sincroniza `StreamingAssets` (edificio_completo.json, modelo_resultados.json,
  edificio_solido.json + overlay P–M). Resultados = canónico (G 93 407,6 kN /
  Q 18 700,8 kN; B G 48 171,1 / SC 11 029,6).
- **Índice de entrega**: `reports/README.md` con enlaces a `semana03.md`,
  `semana04.md`, `semana05.md` y `supuesto_armado_edificio_A.md` (una línea por
  semana), cómo correr el pipeline (`python src\complejo.py --sin-visualizar`),
  cómo abrir el visor (`abrir_unity_solido.cmd` / escena Main) y cómo generar el
  APK (`Tools/MCOC/Build Android`).
- **Higiene del repo**: `.gitignore` ampliado con `build/` (APK Android) y los
  scratch de investigación; `.meta` quedan commiteados (no ignorados, los
  requiere Unity); sin Library/Temp/__pycache__/UserSettings/Build commiteados;
  binario mayor = 2,1 MB (`results/edificio_solido.json`, legítimo).
- **Commit + tag**: `git commit` + `git tag entrega-semana05` + `git push origin
  master --tags`.

### Verificación

- Suite **134 passed**; canónico idéntico al baseline; working tree limpio.

### Pendientes (hechos A MANO en Unity, fuera de opencode)

- **APK**: menú `Tools/MCOC/Build Android` con el módulo Android instalado
  (salida `build/EdificioComplejo_MCOC.apk`, ya git-ignorada).
- **Capturas**: llenar los `[CAPTURA: …]` de `reports/semana04.md` y
  `reports/semana05.md` tomando Play en el editor (selección de tag 41, ventana
  P–M con envolvente 0,60x2,91 y 0,70x2,91, superposición G+Q+EX).

### CIERRE — Estado de Semanas 4–5

- [x] Dashboard de esfuerzos completos + panel de consulta + ventana P–M (S4).
- [x] Trazabilidad tag Web 41 → JSON → panel → P–M con números (S4).
- [x] MOD1/MOD2 reales con "antes → después" y actualización por "Recargar JSON" (S5).
- [x] Superposición interactiva G+Q≡GQ / G+EX / G+Q+EX con |Δ| ≈ e-11 (S5).
- [x] `reports/semana04.md` y `reports/semana05.md` (6 secciones) + índice.
- [x] Suite ≥ 134, repo limpio, commit + tag `entrega-semana05`.
- [ ] APK Android compilado a mano (pendiente).
- [ ] Capturas en los `[CAPTURA: …]` (pendiente).
