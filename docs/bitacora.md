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
- **Semana 06 COMPLETA (Sesiones 19 y 20):** corrección del signo de `Mz(x)` en el
  visor y en `src/secciones/diagramas.py`, **y app AR de inspección terminada**.
  `src/ar/` genera `results/ar_elementos.json` (tags **14** y **26** columnas con
  `M_xy` + panel P–M, **134** viga con `M_xz`; caso **GQ**) desde los datos ya
  verificados, con **12 tests propios** → **Suite: 150 passed**. Escena
  `Assets/Scenes/AR_Inspeccion.unity` (175 líneas, un GameObject) + 5 guiones en
  `Assets/Scripts/AR/`, shader propio sin URP, AR Foundation **4.2.0** y dos menús
  de build Android. **Tests: 150 passed.**
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

---

## Sesión 18 — Viernes 25 de septiembre 2026 (datos actualizables sin recompilar el APK)

**Tema: decidir, antes de compilar, qué se puede cambiar en el teléfono y
implementarlo.** La pregunta fue: "si más adelante quiero mejorar el diseño, ¿se
puede subir al APK ya instalado o hay que recompilar?" La respuesta es
**dividida**, y esa división quedó implementada:

- **Diseño / UI / escena → hay que recompilar** (van dentro del APK como C# IL2CPP
  y escena serializada; no hay *hot update*).
- **Datos → NO hay que recompilar**: el visor ahora busca primero
  `Application.persistentDataPath`, que en Android es escribible por `adb`.

### Qué se hizo

- **`UnityStickModel.cs` — carga del JSON por prioridad** (único archivo del visor
  modificado; el motor de análisis y los resultados canónicos no se tocaron):
  1. `Application.persistentDataPath/edificio_completo.json` → **actualizable**;
  2. `StreamingAssets` con `UnityWebRequest` (en Android la carpeta va
     **comprimida dentro del APK** y `File.ReadAllText` falla — este era el
     riesgo #1 que el reporte declaraba abierto);
  3. `StreamingAssets` con `File` (Editor / escritorio).

  `Cargar()` quedó como *dispatcher*: `StartCoroutine(CargarCoroutine())` en Play
  y `CargarSincrono()` en Edit Mode (`[ExecuteInEditMode]`, donde las
  corrutinas no corren). El parseo y `ConstruirEscena()` se comparten en
  `AplicarTexto()`.
- **Trazabilidad en pantalla**: etiqueta `Fuente: …` con la **fecha** del archivo
  leído, y en móvil la ruta `Actualizable: …`, para saber siempre qué datos se
  están viendo. Nuevo botón **`Usar JSON del APK`** que borra la copia local y
  vuelve al embebido.
- **`scripts/subir_json_telefono.ps1`** (nuevo): valida `adb`, exige **un**
  dispositivo, `mkdir -p`, `adb push` del JSON a
  `/sdcard/Android/data/com.mcoc.edificiocomplejo/files/` y **verifica con
  `ls -l` en el teléfono** lo que quedó. Tiene `-Borrar` para volver al del APK.
- **`reports/semana05.md`**: §6 reescrito (6.2 = canal de datos actualizables,
  6.3 = los 2 riesgos que **sí** exigen recompilar: táctil y layout), §1 con la
  tabla de estado y las líneas reales de cada control (recontadas sobre el
  archivo final), §9/§10 y §8 al día. Corregido además el defecto de caso de la
  consulta (`casoConsulta = 2` → GQ, `:73`) y la línea de la etiqueta de
  superposición.
- **Higiene**: el parche se aplicó **preservando bytes** (`latin-1`, porque
  `UnityStickModel.cs` tiene mezcla de UTF-8 y CP1252 con CRLF): los 47 bytes no
  ASCII originales siguen intactos y el archivo pasó de 2 837 a **2 978 líneas**.
- **`pytest` ya no pisa los resultados canónicos** (hallazgo de esta sesión): los
  tests llamaban a los exportadores con sus rutas de producción, así que
  `pytest` reescribía `results/modelo_resultados.json` (con la etiqueta
  `"... - esfuerzos (test)"`), `results/edificio_solido.json` y la copia de
  `Assets/StreamingAssets/`. Los números no cambiaban, pero **un `pytest` podía
  dejar un "(test)" en un JSON de la entrega**. Arreglo: los tres exportadores
  aceptan ahora rutas alternativas (`out_dir` en `analizar.run_analisis` /
  `exportar_json` / `exportar_unity_solido.exportar`; `destino` + `streaming` en
  `secciones.exportar_unity.exportar`), **con el comportamiento por defecto
  intacto** para el pipeline, y los tests trabajan sobre `tmp_path`
  (`streaming=False`). Tras el arreglo, `git status` sale limpio de `results/` y
  `StreamingAssets` al correr la suite.

### Verificación

- `python -m pytest tests -q` → **134 passed** (6,1 s antes del arreglo de
  temporales; 4,8 s después, porque ya no reescribe dos JSON de 2 MB).
- **Los JSON canónicos no se modifican**: tras `pytest`, `git status` no muestra
  `results/` ni `Assets/StreamingAssets/`, y no hay ninguna aparición de `(test)`.
- **Unity 2022.3.62f3 abierto y verificado** (el Editor **sí** está instalado en
  esta máquina; lo que falta es el módulo Android, ver abajo):
  - el proyecto carga **sin errores de compilación** → el bloque nuevo compila;
  - se probó la **precedencia de la fuente** de verdad: con el proyecto tal
    cual, el visor reporta `JSON cargado (StreamingAssets (archivo))`; al
    copiar el JSON a `Application.persistentDataPath`
    (`AppData\LocalLow/DefaultCompany/EdificioIngUnity/` — la carpeta que en
    Android escribe `adb`) reporta `JSON cargado (telefono (actualizable))`, y al
    borrar la copia vuelve a `StreamingAssets (archivo)`. El `SHA256` del script
    se comprobó antes y después de la prueba: idéntico.
  - como `UnityStickModel` es `[ExecuteInEditMode]`, esto cubre la rama
    **síncrona**; la corrutina de Play Mode (`UnityWebRequest`) solo se puede
    probar en el dispositivo o en el Editor con Play.
- `scripts\subir_json_telefono.ps1` ejecutado: corre y falla con el mensaje
  esperado ("adb no está en el PATH") — no hay platform-tools en esta máquina.
- **No verificado**: el APK en un teléfono real, porque **el módulo Android
  Build Support no está instalado** (`Editor/Data/PlaybackEngines/` solo tiene
  `windowsstandalonesupport`; falta SDK/NDK/JDK).

### Pendientes

- Compilar el APK y probar el flujo completo en un teléfono: "Usar JSON del APK"
  → `scripts\subir_json_telefono.ps1` → "Recargar JSON" → `Fuente: telefono`.
- Gesto táctil y layout adaptativo (§6.3 del reporte): **requieren recompilar**;
  la lectura del dato ya no.

---

## Sesión 19 - Miércoles 30 de septiembre 2026 (signo de Mz + arranque de AR)

**Tarea:** Parte 0 completa (corrección del signo de `Mz(x)`) y attempt de
Partes 1-3 (parche de datos AR + app AR + documentación).

### El bug de Mz: por qué `+Vy·x` estaba mal

El visor evaluaba `Mz(x) = Mz + Vy·x + Wy·x²/2`, o sea **la misma** fórmula que
`My(x) = My + Vz·x + Wz·x²/2`. No son análogos. En los ejes locales de OpenSees:

| conjugado | relación |
|---|---|
| `dVz/dx = +Wz` | y `dMy/dx = +Vz` |
| `dVy/dx = +Wy` | y **`dMz/dx = -Vy`** |

El corte vertical va antes que su momento (`dMy/dx = +Vz`); el corte en local y
se lleva su momento con signo invertido (`dMz/dx = -Vy`).

**Evidencia numérica (Edificio A, `elementTag = 14`, caso `GQ`, L = 3,960 m,
Wy = 0, Vy = -112,59 kN, Mz_i = -226,47 kN·m):**

| | Mz(L) |
|---|---|
| con `+Vy·x` (lo que había) | **-672,32 kN·m** |
| con `-Vy·x` (lo correcto) | **+219,38 kN·m** |
| OpenSees (`localForce`, extremo j) | **+219 kN·m** |

### Cambios (aditivos salvo la corrección)

- **`UnityStickModel.cs`** (3 fórmulas + el comentario de convención que decía
  `M(x) = M + V·x + W·x²/2` para los dos planos):
  - `:2600` `Mzx` del panel de consulta;
  - `:2639` `ValorDiagramaD()` de la ventana 2D arrastrable;
  - `:2936` `ReconstruirDiagramas3D()` de la curva Mz roja.
- **`ModeloComplejo.cs:236`**: docstring de `EsfuerzosVigaModelo`, que decía
  *"Vy/Mz análogos"*. Solo comentario.
- **`src/benchmark_3d/esfuerzos.py`** y **`src/edificio_b/esfuerzos.py`**:
  docstrings (fórmulas + bloque de cierre) y `_cierre_por_viga()`.
- **`tests/test_esfuerzos_a.py`** (2 tests) y
  **`tests/test_edificio_b/test_esfuerzos.py`** (2 tests).
- **`reports/semana06.md`** (nuevo).

`My` **no se tocó**: es correcto y su verificación ya cerraba. Tampoco se
tocaron `Main.unity`, los JSON canónicos, ni `src/secciones/`.

### El cierre de Mz que faltaba

`_cierre_por_viga` solo comprobaba `Vz(L)` y `My(L)`, y por eso nadie notó el
error: `Mz` no se comparaba contra el extremo j. Ahora los tres:

```
Vz(L) = Vz + Wz·L           = -Vz_j
My(L) = My + Vz·L + Wz·L²/2 = -My_j
Mz(L) = Mz - Vy·L - Wy·L²/2 = -Mz_j   <- nuevo
```

`dMz` entra en `vigas_sin_cierre`, así que `cierre_ok` de los cinco casos ya
exige los tres.

### Verificación

- `python -m pytest tests -q` → **138 passed** (134 + 4 nuevos), 4,3 s.
- **Guardas que muerden** (mutación: revertir el signo a `+` en A) → fallan
  `test_cierre_diagrama_por_viga_y_caso` (preexistente, porque `cierre_ok` ahora
  cubre Mz) y `test_semana06_cierre_mz_por_viga_y_caso`. Restaurado: 8 passed.
- **Cobertura del cierre, y su límite honesto.** Una viga solo discrimina el
  signo si `Vy != 0`:
  - **A**: 228 vigas, **14 con `Vy != 0`** → con el signo correcto el cierre da
    4e-10..7e-10 × tol; con el viejo, **6,3 × tol (GQ), 66,8 × (EX),
    113 × (EY)**. Guarda de regresión real.
  - **B**: 215 vigas y **`Vy = 0` en las 5** (sin carga en local y, sin torsión
    de diafragma que reparta corte) → `Mz` es **constante** y el cierre se
    reduce a `Mz_i = -Mz_j`. Es válido, pero **no puede distinguir los dos
    signos**. Por eso la guarda de signo vive en el test de **A**, y el de B lo
    documenta en vez de fingir que discrimina.
- **Compilación C#: exit 0, sin errores ni avisos**, para `Assembly-CSharp`
  (76 800 B) y `Assembly-CSharp-Editor` (10 240 B). El Editor de Unity está
  abierto sobre el proyecto, así que `-batchmode` no puede tomar el cerrojo
  (`exit=1073741845`); se compiló con **el mismo compilador y las mismas
  referencias que usa Unity**: `Editor/Data/DotNetSdkRoslyn/csc.dll` + el host
  `Editor/Data/NetCoreRuntime/dotnet.exe`, alimentado con el response file que
  Unity dejó en `Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.rsp`, con
  `-out`/`-refout` redirigidos a un temporal para no pisar los artefactos del
  editor.
- `UnityStickModel.cs`: UTF-8 **sin BOM**, CRLF íntegro (2 978 → 2 980 líneas) y
  los bytes no ASCII preservados.

### Pendientes / bloqueos

1. **`parche_datos_AR.zip` no existe.** La Parte 1 lo pide en la raíz del
   proyecto; no está en el repo ni en el perfil de usuario. Lo único comprimido
   que hay es `para_entrega.zip`, una entrega anterior de la semana 03
   (`secciones/`, `edificio_solido_visor.json`, figuras de la s03) — **no** es
   el parche. Sin él no hay `src/ar/`, ni `tests/test_ar_elementos.py`, ni
   `results/ar_elementos.json`, ni las figuras `ar_ref_*.png`.
   **Por eso las Partes 1 y 2 no se ejecutaron**: escribir la escena AR contra
   un JSON inventado daría una app que no calza con los datos reales, y la
   aceptación pide `ar_elementos.json` en `StreamingAssets`.
2. **Suite en 138, no ≥146** — los ~8-12 tests que faltan son los del parche.
3. **Decisión pendiente del usuario: el mismo bug está en
   `src/secciones/diagramas.py`** y **no se tocó** por la regla de no modificar
   el motor de secciones:
   - `:242` `"Mz": coef["Mz"] + coef["Vy"] * x + coef["Wy"] * x * x / 2.0`
     (y el docstring en `:28`);
   - `tests/test_secciones.py:492` **patea el error**:
     `assert d["Mz"] == approx(coef["Mz"] + coef["Vy"] * x)`;
   - `_cierre_verticales()` (`:247-270`) solo cierra `dN`, `dVz` y `dMy`:
     **`dMz` no se cierra**, que es justo por lo que pasó inadvertido aquí
     también.

   **Matiz importante:** los **datos son correctos** — el JSON exporta
   coeficientes (`Mz_i`, `Vy`, `Wy`, `L`) de `localForce`, no diagramas
   evaluados. El error está **solo en el evaluador** `diagramas.evaluar()`, que
   hoy consume **un único sitio**: el test de paridad. Ni el visor Unity (que
   tiene su propio evaluador, ya corregido) ni la app AR pasan por él **hoy**.
   Pero es quien genera `esfuerzos_completos` de columnas y muros, así que si
   la app AR se apoya en él para muestrear diagramas **heredaría el signo
   equivocado**.
4. **APK**: hoy `MCOCBuildAndroid.cs` fija `AndroidApiLevel22` y compila **solo**
   `Main.unity`. Con la app AR hay que subir a **API 24**, poner
   `AR_Inspeccion.unity` **primera**, añadir permiso de cámara y fijar
   IL2CPP **ARM64** + **OpenGLES3**. **Sigue sin instalarse el módulo Android
   Build Support** en esta máquina (`Editor/Data/PlaybackEngines/` solo tiene
   `windowsstandalonesupport`), así que el APK no se puede compilar ni probar
   aquí.

### Fijado para cuando llegue el parche

- **Coordenadas** (todo medido en el código, §2 del reporte):
  `(x, y, z)_SI → (x + offset.x, z, y + offset.y)_Unity`; ejes locales con el
  mismo canje `(vx, vy, vz) → (vx, vz, vy)`. **Escala 1:1**, sin conversión de
  unidades; lo único escalable en pantalla es la **amplitud** del diagrama,
  como fracción de `L`.
- **Rotación/traslación/anchor**: la geometría del edificio **no** se hereda;
  el diagrama se ancla con un `ARAnchor` frente a la cámara y de ahí manda el
  usuario (arrastrar = mover, pellizcar = escalar, dos dedos = girar), con
  botón "Recolocar".
- **Teléfono = visor de resultados verificados.** El JSON trae los diagramas
  **ya muestreados** contra `x`: el teléfono **no evalúa ninguna fórmula**
  estructural. Decisión de alcance, enunciada en §4 del reporte.
- El proyecto sólido reutiliza el cargador existente
  (`persistentDataPath → StreamingAssets`), así que `ar_elementos.json` también
  se podrá actualizar por `adb push` sin recompilar.

---

## 📅 Sesión 20 — Miércoles 30 de septiembre 2026 (app AR: datos reales + escena + build Android)

> Continuación de la Sesión 19. Cierra las Partes 1 y 2 de la Semana 06.
> **Suite: 138 → 150 passed.** `Assembly-CSharp` y `Assembly-CSharp-Editor`
> compilan con **exit 0**.

### 1. `parche_datos_AR.zip`: no existe (confirmado otra vez)

Se buscó de nuevo en la raíz y en el perfil: el único comprimido es
`para_entrega.zip`, de la semana 03. **Decisión taken:** no inventar un esquema de
datos, sino escribir el pipeline AR **contra los datos reales ya verificados** de
este proyecto. `src/complejo.py` ya tenía `esfuerzos_completos` con los momentos
evaluados en `i` y `j` de cada elemento, que es exactamente lo que el teléfono
necesita.

### 2. `src/ar/` — el exportador

Nuevo módulo que escribe `results/ar_elementos.json`:

| tag | tipo | `L` | plano principal | P–M |
|---|---|---|---|---|
| 14 | columna | 3,96 m | `M_xy` | sí (`pm`) |
| 26 | columna | 3,96 m | `M_xy` | sí (`pm`) |
| 134 | viga | 10,0 m | `M_xz` | no |

El JSON lleva por elemento los 9 esfuerzos muestreados contra `x`, los ejes
locales, los extremos i/j, y —en columnas— la demanda `pm` para el panel P–M.
Incluye un bloque `convenciones` que **reescribe explícitamente la fórmula** de
`Mz` (`Mz(x) = Mz − Vy·x − Wy·x²/2`) y el sentido de `M > 0`: el teléfono no
hereda ninguna convención implícita. La viga 134 sale con `M_xy` nulo, que es
correcto (viga vertical, sin flexión en su plano fuerte).

`src/complejo.py` lo invoca al final y copia el resultado a
`Assets/StreamingAssets/ar_elementos.json`. También genera las tres figuras
`reports/fig/ar_ref_tag{14,26,134}.png` con el mismo contrato visual que la app.

### 3. `src/secciones/diagramas.py` — el error de signo, corregido

Autorizado explícitamente en esta sesión. Cuatro sitios:

- evaluador: `Mz + Vy·x + Wy·x²/2` → `Mz − Vy·x − Wy·x²/2`;
- docstring de `diagramas.py`;
- el test de paridad `test_secciones.py`, que **fijaba el error**;
- `_cierre_verticales()` ahora cuadra **`dMz`** además de `dN`, `dVz`, `dMy`.

El cierre de `dMz` es lo que faltaba para que el error **no pudiera** volver a
pasar inadvertido, igual que se hizo en A y B.

### 4. Unity: paquetes XR y por qué la línea 4.2.0

`Packages/manifest.json` suma `arcore`, `arfoundation`, `arsubsystems` **4.2.0**
y `xr.management` **4.4.0**.

**Descartada la 5.x a propósito:** el proyecto usa el render pipeline
**incorporado**, y los asmdef de AR Foundation 5.x referencian
`Unity.RenderPipelines.Universal.Runtime` de forma **no opcional**, lo que
arrastraría URP + SRP por el grafo de dependencias. En 4.2.0 esas referencias
están detrás de `versionDefines` sobre `MODULE_URP_ENABLED` /
`MODULE_LWRP_ENABLED`, así que sin URP instalado el código de URP no compila.

Como AR Foundation no venía instalado, sus ensamblados se compilaron desde las
fuentes reales de los paquetes con el mismo Roslyn de Unity, **sin** definir esas
dos constantes, para poder verificar el proyecto de verdad.

### 5. La app

| Fichero | Rol |
|---|---|
| `Assets/Scripts/AR/ARDatos.cs` | mapeo del JSON con Json.NET + validación. **No evalúa fórmulas.** |
| `Assets/Scripts/AR/ARGeometria.cs` | tipos y opciones de presentación |
| `Assets/Scripts/AR/ARGeometriaBuilder.cs` | eje, N/V/M, marcas, rótulos, flecha de tracción, panel P–M |
| `Assets/Scripts/AR/ARInterfaz.cs` | Canvas, lista de elementos, toggles N/V/M, botones |
| `Assets/Scripts/AR/ARInspeccionApp.cs` | rig AR, raycast, `ARAnchor`, gestos, colocación |
| `Assets/Shaders/LineaAR.shader` + `Assets/Resources/MCOC_LineaAR.mat` | `LineRenderer` por vertex color, en `Resources` para sobrevivir al stripping |
| `Assets/Scenes/AR_Inspeccion.unity` | **175 líneas**, un GameObject con `ARInspeccionApp` |

Decisiones que no son obvias y conviene no perder:

- **El rig AR se construye en código**, no en la escena: `ARSession`, cámara,
  `ARPlaneManager`, `ARAnchorManager` y `ARRaycastManager` se crean en `Start()`.
  La escena queda como un fichero auditable y no depende de cómo AR Foundation
  serialice sus componentes internos.
- **No hay `ARSessionManager`:** ese componente no existe en AR Foundation
  4.2.0. El estado se lee de `ARSession.state` y el valor correcto es
  `ARSessionState.SessionTracking`, no `ARSessionState.Tracking`.
- **Android:** `Application.streamingAssetsPath` devuelve un `jar:`, así que la
  carga va por `UnityWebRequest` y no por `File.ReadAllText`.
- **Anclaje:** las anclas son `GameObject` + `AddComponent<ARAnchor>()`, y se
  destruyen a mano; `ARAnchorManager.AnchorManager` es lo que las registra.
- **Sin URP de verdad:** el shader propio es deliberado, para que el APK de AR no
  dependa de un pipeline que el proyecto no usa.

### 6. Build Android: dos APKs

`Assets/Editor/MCOCXRSetup.cs` (nuevo) configura *XR Plug-in Management* al vuelo:
`XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(Android)`,
`InitManagerOnStart = true` y `XRPackageMetadataStore.AssignLoader(manager,
"ARCoreLoader", Android)`. Como vive en `Assets/Editor/`, **no** arrastra
referencias AR al APK del visor.

`MCOCBuildAndroid.cs` quedó con **dos menús**:

| Menú | Escenas | Paquete | API |
|---|---|---|---|
| `Tools/MCOC/Build Android AR` | `AR_Inspeccion.unity` (**índice 0**) + `Main.unity` | `com.mcoc.edificiocomplejo.ar` | 24, IL2CPP ARM64, GLES3, cámara |
| `Tools/MCOC/Build Android visor` | `Main.unity` | `com.mcoc.edificiocomplejo` | 22, igual que la semana 05 |

### 7. Verificación

```
python -m pytest tests -q                      → 150 passed in 5.48s
Assembly-CSharp        (con AR Foundation)     → EXIT 0   (107 008 bytes)
Assembly-CSharp-Editor (XR + build)            → EXIT 0   (14 336 bytes)
```

Bugs de API que salieron al compilar de verdad y quedaron corregidos:
`PlayerSettings.Android.stereoRenderingPath` y `AndroidStereoRenderingPath` **no
existen** en 2022.3 (el nombre bueno es `renderOutsideSafeArea`);
`SetUseDefaultGraphicsAPIs`/`SetGraphicsAPIs` piden **`BuildTarget`**, no
`BuildTargetGroup`. Sin esa comprobación, el build habría fallado en Unity con un
error igual de poco descriptivo.

### 8. Pendientes (los dos únicos)

- [ ] **Instalar Android Build Support** (Unity Hub → Edit → Installs) para
      producir y probar los APKs. `PlaybackEngines/` solo tiene
      `windowsstandalonesupport`.
- [ ] Probar la colocación en un **teléfono con ARCore**: raycast de plano,
      anclaje, gestos y lectura del panel P–M frente a las figuras `ar_ref_*`.


## 🔄 Sesión 21 - Miércoles 30 de septiembre 2026 (parche anclaje AR)

### ¿Qué se hizo?

- **Aplicado parche `parche_anclaje_AR.zip`**: se copiaron los 4 archivos a sus rutas definitivas:
  - `src/ar/exportar_ar.py` (exportador con `anclaje`)
  - `tests/test_ar_elementos.py` (tests de anclaje + paridad M/My–Mz/M_xy)
  - `results/ar_elementos.json` (regenerado con `anclaje`)
  - `unity/EdificioSolidoUnity/Assets/StreamingAssets/ar_elementos.json` (copia runtime)
- **Python**: exportador genera `anclaje.punto_unity`, `z_piso`, alturas i/j (3.96 m). Tests añadidos: `test_columna14_mz_en_cabeza_es_219_4_kNm`, `test_paridad_evaluadores_secciones_vs_app_ar` (134/14/26). **155 passed in 4.46s**.
- **C# (runtime AR)**:
  - `ARDatos.cs`: añadido `ARAnclaje`, `ARAltura`, campo `ARElemento.anclaje`; validación.
  - `ARGeometria.cs`: `puntoI,puntoJ,ejeX,ladoPrincipal,esViga`; documentación escala 1:1 y desplazamiento `−anclaje.punto_unity`.
  - `ARGeometriaBuilder.cs`: aplica desplazamiento, conserva extremos desplazados y captura `ladoPrincipal`.
  - `ARColocacion.cs`: `Horizontal`, `Derecha`, `Yaw`, `RotacionInicial`, `PuntoEnAncla` (orientación sólo yaw, eje X horizontal del elemento).
  - `ARInspeccionApp.cs`: contenedores por tag, `SetActive`, recolocación en el mismo ancla, orientación inicial, amplitud, `Restablecer`, posición sin rotación de pose, gestos corregidos (reinicio de `gestoIniciado` cuando `touchCount != 2`).
  - `ARInterfaz.cs`: `Restablecer`, `Amplitud ±` (reemplaza Escala ±), `Quitar ancla`.
- **Tests EditMode Unity**: `Assets/Editor/ARColocacionTests.cs` (5 pruebas). **Pasaron 5/5** (corregida la aserción de rotación vertical: `dot(up, rot*up)=1`).
- **Metas AR reparados**: 10 `.meta` con GUID inválido regenerados para que Unity importe correctamente (`ARInspeccionApp.cs.meta`, `LineaAR.shader.meta`, etc.).
- **Compilación Unity**: `Assembly-CSharp.dll` y `Assembly-CSharp-Editor.dll` generados sin errores CS.
- **Integridad**: `Main.unity` sin cambios respecto a HEAD.

### Verificación

```text
Python: 155 passed in 4.46s
EditMode (Unity 2022.3.62f3): 5/5 Passed
Compilación batchmode: sin errores CS; Tundra build success
Main.unity: inalterado (hash blob idéntico a HEAD)
```

### Pendientes

- [ ] Verificar el ZIP final con todos los archivos modificados/aplicados (si se requiere entregar).
- [ ] (Opcional) Ejecutar PlayMode ligero si se desea validar UI/gestos, aunque fuera del alcance inmediato.



## Sesión 22 - Viernes 2 de octubre 2026 (Corrección 3: pose, lista, Plan B, frames y XR Android)

### Qué se hizo?

- **Reparado el texto corrupto (mojibake)** en `ARInspeccionApp.cs`, `MCOCBuildAndroid.cs` y en esta bitácora: se sustituyeron los caracteres de reemplazo U+FFFD por su letra acentuada correcta. Verificado: 0 U+FFFD en los tres archivos.
- **`ARInspeccionApp.cs` — integración completa**:
  - `Preparar()` idempotente (se puede llamar varias veces sin duplicar nada).
  - `AplicarContrato(ARRaiz)`: el callback `OK` ya rellena la lista con la cabecera `MCOC · AR · caso GQ` y deja seleccionado el primer elemento (tag `14`).
  - Lista ordenada por el orden de contrato (`14`, `26`, `134`).
  - `DestroyOtherCameras()` al arrancar para que sólo quede la de AR.
  - Plan A / Plan B, `DesplazarPiso(...)` en pasos de 5 cm y `RumboCamara()`.
  - `ARFramesCamara.Activar()` y contador de frames visible en la interfaz.
  - `Destruir(GameObject)` nuevo helper: `Destroy` en modo edición lanzaba un error que Unity reportaba como log no gestionado y hacía fallar los tests.
- **`ARInterfaz.cs`**: paneles con `Image.raycastTarget = false` para no bloquear los toques; `TextAnchor.MiddleCenter`; `CargarFuente()` segura en batchmode (no rompe si no encuentra la fuente).
- **`ARRig.cs`**: pose real con `TrackedPoseDriver` (`GenericXRDevice`, `ColorCamera`, `UpdateAndBeforeRender`, `RotationAndPosition`).
- **`ARColocacion.cs`**: `AlturaCamaraSobrePiso`, `PasoPiso`, `AnclaEstimadaBajoCamara(...)` (1.40 m bajo la cámara) y `DesplazarPiso(...)`.
- **Dependencia añadida** a `Packages/manifest.json`: `"com.unity.xr.legacyinputhelpers": "2.1.12"`.
- **Correcciones de API** que rompían la compilación: `ARPlaneManager.trackables.count`, `ARSession.state` como miembro estático, `Canvas` desde `UnityEngine`, eventos `Action` envueltos en lambdas.
- **`MCOCXRSetup.cs` reescrito para XR Management 4.4.0**: el asset maestro correcto es `XRGeneralSettingsPerBuildTarget`; `XRGeneralSettings` y `XRManagerSettings` quedan como sub-assets. Se usa `XRManagerSettings.activeLoaders` (el `loaders` ya no existe). Se eliminaron los assets legacy que se habían creado por error (`Assets/XR/XRGeneralSettings.asset` y `Assets/XR/XRManagerSettings.asset`).
- **Diagnóstico del fallo de XR**: los dos tests de XR fallaban no por la API sino porque el test comprobaba los settings **sin pasar por la asignación del loader**. Con un test temporal se confirmó que `XRPackageMetadataStore.AssignLoader(...)` sí funciona en batchmode (`ARCore` presente, `activeLoaders = 1`). Se hizo público `MCOCXRSetup.ActivarArcoreAndroid()` y el test ahora recorre el camino real de configuración; el diagnóstico temporal se borró.
- **NRE de los `LineRenderer`**: en modo headless, un segundo `LineRenderer` en el mismo GameObject falla. `ConstruirMarca` crea ahora un GameObject hijo por eje.
- **`MCOCBuildAndroid.cs`**: `BuildAr()` registra el loader ARCore si falta, y todos los avisos van también al log (`Faller(...)`) para que el build funcione en batchmode. El log del APK incluye ahora el tamaño en MB.
- **Tests**: ampliados `ARRigTests`, `LlenarElementosTests`, `ARColocacionTests`, `ARFramesCamaraTests`, `ARPlanBTests`, `XRAndroidConfigTests`.
- **Metas creados** para los ficheros que Unity no tenía importados (`ARRig.cs`, `ARFramesCamara.cs` y varios tests de `Editor`).
- **`pytest.ini` añadido**: la suite recogía dos `test_secciones.py` (`tests/` y `para_entrega/`) y chocaban en el mismo nombre de módulo.

### Verificación

```text
Python:     155 passed
EditMode:   34/34 Passed  (Unity 2022.3.62f3, batchmode, sin errores de compilación)
```

### Build Android AR: NO generado

Se intentó `Tools/MCOC/Build Android AR` en batchmode y **falló**. Causa raíz, verificada:

```text
UnityException: JDK not found
Java Development Kit (JDK) directory is not set or invalid.
JDK was not installed with Unity at
  ...\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK
CheckAndroidJDK:Execute
```

En esta máquina **no hay ningún JDK instalado** (ni `JAVA_HOME`, ni en `Android Studio`, ni en `Program Files\Java`, ni Adoptium/Corretto/Zulu). Además faltan también los otros dos componentes del Android Build Support dentro de `PlaybackEngines\AndroidPlayer`:

| Componente | Estado  |
| ---------- | ------- |
| `OpenJDK`  | ausente  |
| `SDK`      | ausente  |
| `NDK`      | ausente  |

Por tanto **no se ha generado ningún APK**. El código de build y la configuración XR están correctos y listos; lo que falta es el toolchain.

### Pendientes

- [ ] Instalar desde Unity Hub los módulos **OpenJDK**, **Android SDK & NDK Tools** en la versión `2022.3.62f3`, y volver a ejecutar `Tools/MCOC/Build Android AR`.
- [ ] Verificar el APK resultante (`build\EdificioComplejo_MCOC_AR.apk`) y anotar tamaño y salida de consola.
- [ ] Prueba en dispositivo real con ARCore: colocación, Plan B (piso +/-), lista, contador de frames.

---

## Sesión 23 - Viernes 2 de octubre 2026 (Corrección 4: orientación doble, piso ±5 cm y respaldo de FeaturePoint)

Corrección 4 sobre la app AR. Todo lo demás se mantiene: mismo contrato
(`ar_elementos.json`), mismo rig, mismos botones, misma escala 1:1.

### 1) Orientación doble (el error de verdad)

`ColocarEnAncla` orientaba el elemento **dos veces**:

| antes | ahora |
|---|---|
| `raizVisual.localRotation = RumboCamara()` | `raizVisual.localRotation = Quaternion.identity` |
| `contenedor.localRotation = RotacionInicial(g, FrenteCamara())` | idem (sin cambios) |

La orientación la pone **sólo el contenedor**, que es además donde acumula el
gesto de dos dedos. `RumboCamara()` quedó sin uso y **se ha borrado**; el
comentario de la cabecera de `ARColocacion` deja escrito que es el único sitio
donde se decide la orientación.

Por qué pasaba desapercibido: con la cámara mirando a **+Z** el yaw es 0, las
dos rotaciones se suman y el resultado es el correcto. En cuanto el teléfono
mira a otro lado, el rumbo se aplicaba dos veces y el elemento quedaba girado un
ángulo arbitrario.

#### Test EditMode parametrizado nuevo: `Assets/Editor/AROrientacionTests.cs`

**15 casos** (5 frentes de cámara: `+Z`, `+X`, `−Z`, `−X`, `37°`) × 3
comprobaciones. Monta la app por el camino real del teléfono
(`Preparar` → `AplicarContrato` → `ColocarAqui`), gira `app.Camara` al frente,
y mide sobre los `Transform` reales de la jerarquía
`ancla → raizVisual → contenedor`, con la geometría que la app tiene cargada
en `geoPorTag`:

| prueba | qué comprueba |
|---|---|
| `Viga134_A3_96m_...` | `(j − i)` mundial paralelo a `ARColocacion.Derecha(frente)` (`dot > 0,999`), luz 10 m y los dos extremos a 3,96 m sobre el piso |
| `LaRaizDeDiagramas_NoRota` | la raíz en identidad y el ancla sin rotación |
| `Columna14_LaTraccionDeMpositivo...` | `rot * ladoPrincipal = −Derecha(frente)`, y que la flecha de tracción dibujada sale hacia la izquierda |

**El test se ha verificado contra el bug**: reintroduciendo el yaw de cámara
en la raíz, fallan **12 de 15**, y los 3 que pasan son exactamente los de frente
`+Z`. Esa es la firma del fallo, y confirma que el test guarda algo.

### 2) Piso ±5 cm sin tocar el ancla

`DesplazarPiso` escribía en `ancla.transform.position`, y eso **no aguanta**:
`ARCore` reescribe la pose del `ARAnchor` en cuanto el subsystem la actualiza, así
que el ajuste se perdía al primer update. Ahora:

- campo acumulado `offsetPiso` (float, metros);
- se aplica con `AplicarOffsetPiso()` → `raizVisual.localPosition = (0, offsetPiso, 0)`;
  la raíz cuelga del ancla, así que sigue al piso sin escribir en la pose que
  controla ARCore;
- `ColocarEnAncla` y `Restablecer` lo respetan (`Restablecer` es sobre el
  elemento: amplitud y rumbo, no sobre el cuadre del piso);
- `QuitarAncla` lo pone a 0;
- nuevo `ARColocacion.AcumularPiso(offsetActual, pasos)`; `DesplazarPiso(Vector3,int)`
  se queda como la aritmética del paso de 5 cm.

API nueva pública para los tests: `OffsetPiso()` y `PosicionPiso()`
(= `ancla.position + (0, offsetPiso, 0)`). `DesplazarPiso` **no** llama a
`ColocarEnAncla` a propósito: ésta reorienta el contenedor y se comería el giro
del gesto de dos dedos.

**Fallo extra que destaparon los tests**: `QuitarAncla` destruía el GameObject
del ancla **antes** de sacar de debajo la raíz de diagramas, que es su hija.
Unity se lleva por delante todos los hijos al destruir, así que **el diagrama
entero se perdía** y el siguiente «Colocar aquí» no tenía nada que mostrar
(era lo mismo al recolocar, porque `CrearAnclaEn` empieza llamando a
`QuitarAncla`). Corregido: la raíz se reparenta primero, el ancla se destruye
después. `Destruir` (el helper que usa `DestroyImmediate` en modo edición) no lo
camuflaba en los tests anteriores.

### 3) Bonus: `FeaturePoint` como respaldo del raycast

En `ColocarAncla`, si el raycast `PlaneWithinPolygon` no devuelve nada se reintenta
con `TrackableType.FeaturePoint` antes de decir «No se detectó piso». Cuando el
móvil no cierra un polígono (sala a oscuras, moqueta, luz rasante) el plano no
aparece pero los puntos de referencia sí. El mensaje distingue el origen
(`raycast` / `punto de referencia`) y, si tampoco hay puntos, sugiere «Colocar
aquí».

### Verificación

```text
Python:   155 passed in 5.70s
EditMode: 51/51 Passed  (Unity 2022.3.62f3, batchmode, sin errores CS)
```

| fixture | tests |
|---|---|
| ARColocacionTests | 8 |
| ARFramesCamaraTests | 6 |
| **AROrientacionTests** (nuevo) | **15** |
| ARPlanBTests | 7 (antes 5) |
| ARRigTests | 4 |
| LlenarElementosTests | 7 |
| UIOverlapTests | 1 |
| XRAndroidConfigTests | 3 |

Se añadieron 2 pruebas a `ARPlanBTests`: `CambiarDeTagYRestablecer_MantienenElDesplazamientoDelPiso`
y `QuitarAncla_PuestaElDesplazamientoACero` (esta última es la que destapó el
borrado del diagrama). Se generó `.meta` para el test nuevo
(`0022762fecf34e9eab44f4d1e3a0c426`) porque si no Unity no lo importa y la suite
no lo ve.

### Build Android AR: NO generado

Sin cambios respecto a la Sesión 22: falta el toolchain (OpenJDK + SDK + NDK) en
`2022.3.62f3`. El APK se compila en el Mac.

### Pendientes

- [ ] Prueba en dispositivo con ARCore: colocar mirando a **+X o −X** (no sólo +Z),
      que es donde antes salía el elemento girado, y el gesto de dos dedos encima
      de un piso ya desplazado con ±5 cm.
- [ ] Instalar OpenJDK + Android SDK/NDK y ejecutar `Tools/MCOC/Build Android AR`.

---

## Sesión 24 - Corrección 5: legibilidad (parte 1) y marcado en terreno (partes 2a, 2b y 2c)

La sesión ataca la Corrección 5 en cuatro entregas sucesivas, cada una commiteada
por separado. Ninguna toca la escala 1:1 del elemento ni el contrato
`ar_elementos.json`.

**Parte 1 — lo que se veía mal en el teléfono.** El texto 3D salía **en
espejo**, los rótulos y las marcas no se veían a 3-5 m, y el mensaje de estado
pisaba lo que la app quería decir. Se detalla abajo, tal cual se dejó.

**Partes 2a, 2b y 2c — el problema de fondo.** En terreno el diagrama «se
alejaba» del elemento: el ancla acababa bajo el usuario («Colocar aquí») o donde
apuntaba el centro de la pantalla, **no en la base del elemento real**. Con
perspectiva, eso hace que dos vigas paralelas del mismo piso parezcan estar en
lugares distintos. La solución es que el usuario **marque el elemento en el piso**
con un anillo de retícula: dos toques para una viga (bajo el extremo i y bajo el
j) y uno para una columna (la cara visible). Las tres partes van de lo más interno
a lo más externo:

| Parte | Qué hace | Commit |
|---|---|---|
| 2a | Funciones **puras** de colocación por dos puntos, sin UI | `a86a113` |
| 2b | Modo de marcado en la app: retícula, raycast al piso, flujo i–j | `3468cbe` |
| 2c | Botones de marcado y su conexión con la interfaz | (este commit) |

### 1) Texto en espejo (`ARBillboard`)

`LateUpdate` orientaba el texto con `cam.transform.position - transform.position`,
o sea con el **+Z del texto hacia la cámara**. Un `TextMesh` se lee al derecho
cuando su +Z apunta en dirección **contraria** a la cámara, así que salía
reflejado. Ahora:

```
private void LateUpdate()
{
    if (cam == null) cam = Camera.main;
    Orientar(cam);
}

public void Orientar(Camera camara)
{
    if (camara == null) return;
    Vector3 dir = transform.position - camara.transform.position;
    if (dir.sqrMagnitude < 1e-8f) return;
    transform.rotation = Quaternion.LookRotation(dir, camara.transform.up);
}
```

La orientación sale a un método **público** `Orientar(Camera)` para poder
ejercitarla desde un test sin esperar al `LateUpdate`. El guardia
`dir.sqrMagnitude < 1e-8f` evita que `LookRotation` reciba un vector nulo.

#### Test EditMode nuevo: `Assets/Editor/ARBillboardTests.cs`

**4 pruebas**: tres `TestCase` de posición de cámara (detrás `(0,0,−3)`, al lado
`(3,0,0)` y arriba en diagonal `(−2,1,5,2)`) que comprueban
`dot(texto.forward, haciaAfuera) > 0,999` y `dot(texto.up, camara.up) > 0,9`, más
`SinCamaraNoRevienta`, que llama `Orientar(null)`.

Con el signo viejo `dot(texto.forward, haciaAfuera)` valía **−1** en los tres
casos, con lo que la aserción principal falla; el test guarda algo.

### 2) Tamaños legibles a 3-5 m

La escala del ELEMENTO no cambia (sigue 1:1): esto es grosor de trazo y tamaño
de rótulo, que no alteran longitudes ni alturas sobre el piso.

| Archivo | Antes | Ahora |
|---|---|---|
| `ARGeometria.cs` `anchoLinea` | 0,005 | **0,025** |
| `ARGeometria.cs` `anchoLineaPrincipal` | 0,009 | **0,04** |
| `ARGeometriaBuilder.Marca` `radio` | 0,028 | **0,08** |
| `ARGeometriaBuilder.Marca` `alturaTexto` | 0,035 | **0,10** |
| `ARInspeccionApp.ConstruirMarca` ancho del eje | `radio * 0.3f` | **`Mathf.Min(0.02f, radio * 0.5f)`** |
| `ARInspeccionApp.ConstruirMarca` `characterSize` | 0,011 | **0,018** |

El ancho de la marca va con `Mathf.Min(0.02f, ...)` y no con `radio * 0.5f` a
secas porque las marcas de la envolvente P–M y de la demanda siguen siendo
pequeñas (`radio` 0,012 y 0,01): sin el tope, el texto de rechazo las volvería
illegibles y el trazo se comería el gráfico.

### 3) El mensaje de estado vuelve a ser el de la app

El bloque de diagnóstico de `Update()` escribía cada medio segundo

```
ui.estado.text = "ARSession: " + estado + " · planos: " + planos + " · camara: " + permiso;
```

así que **tapaba** el `mensaje` que la app va cambiando según lo que pase
("Anclado en…", "Piso +5 cm…", "No se detectó piso…"). Ahora esa línea es
`ui.estado.text = mensaje;` y la misma información se **añade al principio de
`diagText`**, así que sigue estando en el panel Diag y no se pierde.

### 4) Parte 2a: las funciones puras de colocación (`ARColocacion`)

Todo el cálculo del marcado vive primero en `ARColocacion`, **sin UI y sin
escenas**, para poder verificarlo con tests sin montar nada. Se añade al final de
la clase, después de `PuntoEnAncla`:

- `PorDosPuntos(Pi, Pj, g)` → devuelve la colocación de la geometría `g` con la
  viga **`(Pi, Pj)` como línea de base**: la sitúa en el punto medio, a la altura
  del punto **más bajo**, y la gira para que `puntoJ − puntoI` caiga sobre la
  recta `Pi→Pj`. Devuelve la rotación en la colocación, **no** en la raíz.
- `DistanciaHorizontal(a, b)` → distancia en el plano, **ignorando la altura**.
- `PorDosPuntos` usa `Yaw(ejeX, dir)`, el mismo giro que ya usaba la colocación
  por un punto.
- Utilidades: `ToleranciaLuz`, `DistanciaMinimaPuntos`, `FueraDeTolerancia(x, L)`
  (10 % de desviación), `DiferenciaPorcentual`, `MitadSeccion(nombre)` (lee la
  primera medida del nombre, `0.70x0.70` → 0,35) y
  `BaseDesdeCara(cara, haciaCamara, mediaSeccion)` (entra media sección hacia
  donde mira la cámara).

Punto delicado: la geometría de la viga 134 trae `puntoI` y `puntoJ` en
`y = 3,96` y **simétricos respecto al origen**, de modo que el centro de la línea
base coincide con `c.posicion` en horizontal. Eso es lo que permite comprobar
`centro == posicion` sin tolerancia extra.

#### Test EditMode nuevo: `Assets/Editor/ARDosPuntosTests.cs`

**10 pruebas**: 6 `TestCase` del flujo completo de `PorDosPuntos` (simétricos,
desplazados, girados 90°, girados 37°, alturas distintas usando **la menor**, y
puntos invertidos que deben girar la viga 180°) y 4 tests sueltos
(`DistanciaHorizontal`, `FueraDeTolerancia`, `MitadSeccion`, `BaseDesdeCara`).

### 5) Parte 2b: el modo de marcado en la app (`ARInspeccionApp`)

`ARInspeccionApp.cs`, +460 / −27. Se añade:

- `enum ModoMarcado { Ninguno, Base, ExtremoI, ExtremoJ }`.
- `IniciarMarcadoBase()`, `IniciarMarcadoViga()`, `CancelarMarcado()` y
  `MarcarPunto(Vector3)`, los cuatro **públicos**.
- Viga: dos toques → `ARColocacion.PorDosPuntos(puntoI, p, g)`. La **rotación se
  guarda en el contenedor** del elemento, la raíz se queda en identidad.
- Columna: un toque → `ARColocacion.BaseDesdeCara(...)`.
- **Retícula** en el piso que sigue el raycast al centro de la pantalla, con
  `RadioReticula = 0,10`; `ActualizarReticula()` mientras hay modo activo.
- `RaycastPiso()`: primero `Plane`, y si no hay plano, `FeaturePoint` (paredes).
- Reglas de toque: los toques que **empiezan sobre un botón no cuentan**; tocar
  el piso **sólo coloca si no hay ancla**; `Restablecer` vuelve a la rotación
  marcada; `QuitarAncla` y cambiar de elemento **borran** la rotación marcada.

`ActualizarBotonesMarcado()` **se deja a propósito sin conectar** en esta parte:
los botones llegan en la 2c.

#### Test EditMode nuevo: `Assets/Editor/ARMarcadoTests.cs`

**14 pruebas** del flujo: viga 134 marcada recta / girada 37° / girada con la
cámara girada / con la cámara mirando al otro lado, columna 14 desde tres
orientaciones de cámara, `Restablecer` vuelve a la rotación marcada,
`QuitarAncla` la borra, cambiar de elemento cancela el marcado, marcar sin modo
no coloca nada, se autocorrige el modo equivocado, luz medida demasiado
distinta avisa, y puntos demasiado cerca pide marcar de nuevo.

### 6) Parte 2c: los botones (`ARInterfaz` + `ARInspeccionApp`)

`ARInterfaz.cs`, +55 / −8. Tres botones nuevos y tres eventos:

| Botón | Texto | Evento | Cuándo se ve |
|---|---|---|---|
| `btnMarcarBase` | «Marcar base» | `MarcarBase` | columna o muro elegido |
| `btnMarcarViga` | «Marcar extremos i y j» | `MarcarViga` | viga elegida |
| `btnCancelarMarcado` | «Cancelar marcado» | `CancelarMarcado` | sólo mientras se marca |

`MostrarMarcado(hayElemento, esViga, marcando)` decide la visibilidad: nunca
`Marcar base` y `Marcar viga` a la vez, y `Cancelar marcado` excluye a los otros
dos mientras hay marcado activo.

**Orden en la lista**: los botones de marcado van **justo después de los
toggles** de planta y **antes** de «Piso ±5 cm», «Quitar ancla», «Restablecer»,
amplitud y «Diag». **«Colocar en el piso» y «Colocar aquí» quedan al FINAL**,
porque son el método antiguo y el marcado es ahora el principal.

En `ARInspeccionApp.cs` sólo van cuatro líneas: `ConstruirInterfaz` conecta los
tres eventos y `ActualizarBotonesMarcado()` —que estaba vacío desde la 2b—
ahora llama a `ui.MostrarMarcado(g != null, g != null && g.esViga, modo !=
ModoMarcado.Ninguno)`.

#### Test EditMode nuevo: `Assets/Editor/ARMarcadoUITests.cs`

**5 pruebas**: `MarcarBase` conectado, con columna se ve «Marcar base» y no el de
la viga, con viga se ve «Marcar extremos» y no «Marcar base», durante el marcado
sólo se ve «Cancelar», y **«Colocar sin marcar» queda al final de la lista**.

### Cómo se usa en terreno

El anillo amarillo del centro de la pantalla es la retícula: dice en qué punto
del piso se va a clavar el elemento.

**Columna** (un toque):

1. Elige la columna en la lista.
2. Pulsa **«Marcar base»**.
3. Gira el teléfono hasta poner el **anillo amarillo al pie de la cara
   visible** de la columna.
4. **Toca la pantalla fuera de los botones.**

**Viga** (dos toques):

1. Elige la viga.
2. Pulsa **«Marcar extremos i y j»**.
3. Pon el anillo en el piso **bajo el extremo i** y toca.
4. Repite **bajo el extremo j** y toca.

Mientras marcas, arriba aparece `Medido x m · modelo L m (±%)`, que avisa en
verde si lo medido encaja con la longitud del modelo y en otro color si se pasa
del 10 %. Si los dos puntos quedan demasiado cerca, la app pide marcar de nuevo.
Para cancelar a medias: **«Cancelar marcado»**.

> Importante: marcar y «Colocar en el piso» / «Colocar aquí» son **métodos
> distintos**. Los dos últimos siguen al final de la lista y ya no son el camino
> principal; el marcado es el que deja el elemento en su sitio real.

### Verificación

Las cuatro partes, cada una por separado:

| Parte | Commit | EditMode | Python |
|---|---|---|---|
| 1 | `d182ed3` | 55 / 55 | 154 + 1 |
| 2a | `a86a113` | 65 / 65 | 154 + 1 |
| 2b | `3468cbe` | 79 / 79 | 154 + 1 |
| 2c | este | **84 / 84** | 154 + 1 |

Salidas reales de la última (2c):

```text
Python:    154 passed + 1 fallo conocido  (test_mfi_p0_elastico_agrietado, 4.71 s)
EditMode:  84/84 Passed  (Unity 2022.3.62f3, batchmode, LogAssemblyErrors 0ms, sin errores CS)
```

```text
total=84 passed=84 failed=0 skipped=0 result=Passed

  ARBillboardTests         Passed   4
  ARColocacionTests        Passed   8
  ARDosPuntosTests         Passed  10   <- 2a
  ARFramesCamaraTests      Passed   6
  ARMarcadoTests           Passed  14   <- 2b
  ARMarcadoUITests         Passed   5   <- 2c
  AROrientacionTests       Passed  15
  ARPlanBTests             Passed   7
  ARRigTests               Passed   4
  LlenarElementosTests     Passed   7
  UIOverlapTests           Passed   1
  XRAndroidConfigTests     Passed   3
```

`UIOverlapTests` sigue en verde con los tres botones nuevos: `NoRaycastTargetImage
SolapaBotones` comprueba que ningún `Image` con `raycastTarget` tapa los botones,
y los de marcado se crean en la misma lista vertical que el resto.

Además, los ficheros de código de las partes 2b y 2c se compararon **byte a byte**
contra las referencias entregadas:

```text
diff ARInspeccionApp.cs ARInspeccionApp_referencia_2c.cs   ->  EXIT=0  (idénticos)
diff ARInterfaz.cs       ARInterfaz_referencia_2c.cs       ->  EXIT=0  (idénticos)
```

Los parches se aplicaron con `git apply --check` limpio y **sin ningún `.rej`** en
las tres partes.

Sobre el fallo de Python: `test_mfi_p0_elastico_agrietado` (`tests/test_secciones.py:73`,
`assert c.Mmax > 700.0`) **no se ha tocado**. Es una diferencia de plataforma: en
Linux la suite da 155 passed con `openseespy==3.8.0.0` y aquí, en macOS arm64 con
`3.7.1.2`, el M–φ cae ~19 % sobre el mismo procedimiento (645,88 kN·m en vez de
~766). Ya está documentado en el commit `1c6e43d` ("§7 IA + corrección de
reproducibilidad del build de openseespy"), que por eso el pin de `openseespy`
no es cosmético. La línea base de esta sesión es **154 passed + 1 fallo conocido**.

### Build Android AR: NO generado

Sin cambios. Ni el APK ni los módulos de Android se tocan en esta sesión; el
EditMode corre igual en este Mac porque no los necesita.

### Pendientes

- [ ] Prueba en dispositivo: confirmar que el texto ya se lee al derecho, que las
      marcas y rótulos se ven a 3-5 m y que el mensaje de estado ya no se pisa
      con el diagnóstico.
- [ ] **Prueba en dispositivo del marcado** (lo más importante que queda):
      comprobar que el anillo de la retícula sigue al centro de la pantalla al
      mover el teléfono, que marca dos vigas paralelas en el sitio correcto, que
      el mensaje `Medido x m · modelo L m (±%)` sale con la unidad y el color
      esperados, y que tocar un botón no cuenta como marcado.
- [ ] Comprobar en paredes sin ARCore grounding (`FeaturePoint`) que el marcado
      también funciona; en el EditMode sólo se cubre la rama de `Plane`.
- [ ] Instalar OpenJDK + Android SDK/NDK y ejecutar `Tools/MCOC/Build Android AR`
      (lo hacen los compañeros en Windows).
- [ ] Acordar qué se hace con `test_mfi_p0_elastico_agrietado`: hoy la línea base
      en macOS es 154 + 1.
