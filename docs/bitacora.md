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

- [ ] En Play del visor sólido: clic sobre una viga del Edificio A → panel con
      N/Vz/My/T (G/Q/GQ/EX/EY) igual que B.
- [ ] (Opcional) Marcar sobre la viga el punto consultado (esfera/gizmo).
- [ ] Oscar: SAP2000.  |  Commit/push.
