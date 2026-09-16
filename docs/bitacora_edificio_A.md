# BITÁCORA DEL PROYECTO — P1 (Laboratorio Estructural Digital 3D)

> **Instrucción para agentes IA y para el equipo:** este archivo es el registro vivo del proyecto.
> Al iniciar cualquier sesión de trabajo, LEER ESTE ARCHIVO COMPLETO para recuperar el contexto
> y retomar donde quedó. Actualizarlo al final de cada sesión de trabajo.

> ⭐ **VERSIÓN OFICIAL / CANÓNICA — TRABAJAR SIEMPRE AQUÍ:**
> `Desktop\Proyecto_1_MCOC_-_Edificio_A_corregido\Proyecto 1 MCOC - Edificio A (OFICIAL)\`
> es la versión **corregida y verificada** del proyecto (ver Sesión 21). A partir de aquí, todas
> las sesiones, análisis, regeneración de JSON/HTML y ediciones se hacen en ESTA carpeta.
> La carpeta `Desktop\Proyecto 1 MCOC - Edificio A (HISTORICO)\` se deja como referencia
> histórica (voladizo de piso 1 simplificado de 2 nodos, Sesión 20) y **NO se debe seguir
> editando**.

---

## 📅 Sesión 1 — Sábado 22 de agosto 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo

1. **Análisis de enunciados** (en `Desktop\Proyecto 1 Enunciados\`):
   - `P1- Enunciado General.txt`: proyecto de 7 semanas, grupo de 3, OpenSeesPy + Unity + AR del Edificio de Ingeniería.
   - `Enunciado Entrega P1A1.txt`: primer avance = `reports/semana01.md` con 8 secciones. Rúbrica 20 pts:
     Comprensión benchmark (5) · Verificación cuantitativa (5) · Ejes/GDL/mecánica (4) · Arquitectura preliminar (3) · Gestión grupal y uso de IA (3).

2. **Exploración del estado existente:**
   - Este repo ya tenía `AGENTS.md` con reglas, desafío 2D resuelto (`src/desafio_clase.py`) con tests y README detallado, `.venv` con OpenSeesPy 3.8 + pytest + numpy + matplotlib.
   - Planos del edificio en `Desktop\planos_edificio_ing\` (~30 DWG, series `2017_67-XXX`). Son 2 edificios según Oscar.
   - NOTA: `Desktop\base\eb3m3v1-base.py` NO es de este proyecto (es tarea de biblioteca musical de otro curso).

3. **Plan de acción P1A1 diseñado** (6 fases):
   - Fase 0: infraestructura repo ✅ (hecha)
   - Fase 1 (domingo): modelo benchmark 3D paramétrico desde planos → secciones 1 y 3
   - Fase 2 (lunes): verificación cuantitativa a mano vs OpenSees + tests → sección 4
   - Fase 3 (lunes): 2 errores deliberados (sección + apoyo/signo) con detección → sección 5
   - Fase 4 (martes): arquitectura preliminar (carpetas, JSON, interfaz OpenSees→Unity) → sección 6
   - Fase 5 transversal: Issues + registro IA (`docs/ai_log.md`) + tabla responsabilidades → secciones 7-8
   - Fase 6 (martes 25): ensamblaje final `reports/semana01.md` + checklist rúbrica

### Distribución del grupo (acordada)

| Integrante | Rol | Secciones | Revisa a |
|---|---|---|---|
| Oscar Rodriguez | Modelo e interpretación | 1, 2, 3 | Pablo |
| Pablo Arancibia | Implementación y verificación | 4, 5 | Nicolás |
| Nicolás Letelier | Arquitectura y gestión + ensamblador final | 6, 7, 8 | Oscar |

- Guías personales completas creadas en el Escritorio:
  - `Oscar Rodriguez - Guia P1A1.md` (incluye plantilla de ficha de geometría)
  - `Pablo Arancibia - Guia P1A1.md`
  - `Nicolás Letelier - Guia P1A1.md`

### Infraestructura creada

- Git inicializado, `.gitignore` (`.venv/`, cachés), commit inicial `7df3f0a`.
- GitHub CLI instalado y autenticado (cuenta `OscarRodriguez17`).
- Repo remoto privado creado con push: **https://github.com/OscarRodriguez17/Proyecto1-MCOC**
- Carpetas `reports/`, `data/`, `docs/` agregadas.

### Decisiones tomadas

- **Alcance semana 1:** flexible hasta respuesta del profesor (mail enviado sin respuesta). Mientras tanto: piso tipo PARAMÉTRICO de UN edificio. Peor caso = alcance completo ("verificación cuantitativa del benchmark, interpretación, errores y arquitectura inicial") que este plan ya cubre tal cual.
- **Plazo oficial:** entre martes 25 y viernes 28 (confirmar día/hora exacto). Meta interna: TERMINADO EL MARTES 25.
- **Flujo de trabajo git paralelo** (cada uno trabaja SOLO sus archivos; pull antes, push frecuente), NO secuencial tipo cadena.
- Unidades SI obligatorias: m, kN, kN/m², kN·m, m², m⁴ (ver AGENTS.md).

---

## 📅 Sesión 2 — Sábado 22 de agosto 2026 (noche)

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo

1. **Nicolás (@nletelier1-stack) invitado al repo** con permiso de escritura (debe aceptar invitación). Grupo completo invitado.
2. **Pipeline DWG→DXF funcionando:**
   - Instalado ODA File Converter 27.1 (winget `ODA.ODAFileConverter`).
   - ⚠️ Quirk crítico: esta versión espera **`recurse` ANTES del filtro**: `<in> <out> ACAD2018 DXF 0 1 *.dwg`. Con el orden clásico falla en silencio ("There is no matched files").
   - 38 planos convertidos a DXF en `%TEMP%\opencode\dxf_out\`.
3. **Extracción automática con ezdxf** (instalado en `.venv`): textos por lámina, textos con coordenadas, líneas de ejes (capas RLE-EJE), entidades DIMENSION.
4. **Lectura estructural del conjunto:** ver `docs/ficha_geometria.md` v1. Hallazgos clave:
   - **Son 2 edificios** (A: ejes E–I', 45 m, planta irregular; B: E–J, 50 m, regular).
   - Unidad de dibujo = 1 cm (verificado contra cotas).
   - Niveles −4.01 / −0.05 / +3.91 / +7.87 / +11.83 → **altura de piso 3.96 m**, 4 pisos + subterráneo.
   - Pilares P.70x70; vigas V.60/80 (+variantes); vigas metálicas V.M.300x300x5 con pernos Nelson → **estructura mixta**.
   - Losas M.H.A. e=15 típica (12–30 por zona); hormigón G35 f'c=35 MPa.
   - Cargas (lámina 700): SC 100–800 kg/m², PM.adic 200–300 kg/m², PP=e×2500, puntuales hasta 13 t.
5. Creado `docs/ficha_geometria.md` (ficha completa + dudas abiertas + reproducibilidad).

### Próximos pasos (abren la Sesión 3)

1. **Oscar:** verificar visualmente la ficha (Autodesk Viewer/Fusion) y cerrar dudas abiertas §7 — especialmente ¿qué edificio modelamos? (sugerido B) y ubicación de muros.
2. **Agente:** extraer capa RLE-MURO (muros por nivel) y cuadro `S-COLS-IDEN` para completar la ficha a v2.
3. **Pablo (avisar por WhatsApp):** puede partir YA con el esqueleto paramétrico usando retícula del Edificio B como valores genéricos.
4. Confirmar fecha/hora exacta de entrega con el profesor (sigue sin responder).

---

## 📅 Sesión 3 — Domingo 23 de agosto 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo

1. **Análisis detallado del trabajo de Pablo** (`Desktop\Proyecto 1 - Pablo\`):
   modelo 3D completo en OpenSeesPy (retícula plano 101: 45 × 16.15 m), casos
   G/Q/GQ/EX/EY, diafragmas rígidos, muros como columnas anchas, export JSON.
   Verificación independiente: se reprodujeron a mano las reacciones
   (G, Q exactos), conteo nodos/elementos y derivas → aritmética impecable.
2. **Discrepancias detectadas y corregidas al integrar al repo:**
   | Problema | Corrección |
   |---|---|
   | f'c=25 MPa (G25 supuesto; LibreDWG no leía rotulado) | **G35** según lámina 100 (ODA sí lo lee); Ec = 4700√f'c = 27,806 MPa |
   | PM.adic = 1.0 kN/m² (planos exigen 200–300 kg/m²) | **2.55 kN/m²** (260 kg/m², aula típica ficha §4) |
   | SC = 2.0 kN/m² | **2.50 kN/m²** (aula típica, parametrizado `SC_AULA`) |
   | J torsional de muros ≈ 4·Ip (MI-12 daba J≈70 m⁴) | San Venant `(1/3)L·t³(1−0.63t/L)` → J≈0.078 m⁴ |
   | Sumas mx/my/mz del JSON sin sentido físico | eliminadas; momento volcante analítico ΣFi·hi por caso sismo |
3. **Integración al repo:** `src/benchmark_3d/` (datos_edificio · construir · cargas · analizar),
   salida JSON ahora en `results/modelo_resultados.json` (contrato Unity).
4. **Tests automatizados creados:** `tests/test_benchmark.py` — 15 tests
   (geometría, secciones, conservación tributaria, equilibrio por caso,
   superposición R(G)+R(Q)=R(GQ), V=α·W). Todos en verde (~0.2 s).
5. **Identificación del edificio modelado (datos duros):** escaneo ezdxf del
   plano 101 → el modelo replica la planta INFERIOR con coincidencia exacta:
   ejes E–I' = 45.00 m (luces 10·4+5) y ejes horizontales 3/2a/2/1''/1 =
   16.15 m. Por regularidad y ejes corresponde al "Edificio B" de la ficha v1.
   ⚠️ v1 decía B=50 m hasta J; en planta 101 termina en I'. Queda para
   verificación visual de Oscar. Ampliación: el edificio largo 50 m (eje J)
   aparece rotulado recién en láminas 102 y 103 (no en 100/101).
6. **Ficha de geometría actualizada a v2** (`docs/ficha_geometria.md`): ahora
   es espejo exacto del modelo (retícula, niveles −4.21/+11.83, regla de
   pilares, tabla de 5 muros, materiales G35, cargas adoptadas q_G=6.30/
   q_Q=2.50/α=0.10, apoyos, dudas §7 actualizadas).
7. **Extracción muros por nivel + materiales (cierra parte de dudas §7):**
   - Títulos de láminas: 100 = fundaciones · 101 = piso 1° + 1° subterráneo ·
     102 = pisos 2° y 3° · 103 = piso 4°.
   - Par interior M1c-E + M2a IDÉNTICO en pisos superiores (láms. 102/103).
   - **Muros perimetrales ME/MI no aparecen en pisos superiores** → el modelo
     los repite en todos los niveles como simplificación documentada.
   - V.M./P.M./P.M.I. son elementos METÁLICOS (láms. 800–802: pernos Nelson,
     "destajar perfiles") → piso mixto acero-hormigón.
   - Rotulado de grado de hormigón NO extraíble vía DXF (escaneadas las 38
     láminas, modelspace+atributos+bloques) → solo verificación visual.
   - Hallazgo nuevo: geometría fuera del bloque 45×16.15 (franja sur eje I,
     sector NE sobre eje 1, ejes extra 1'/1b/8 en fundaciones) → duda visual.

### Resultados corregidos (G35, q_G=6.30, q_Q=2.50, α=0.10)

| Caso | ΣFz reacción / V base | Despl. techo | Deriva máx |
|---|---|---|---|
| G   | 40,652.5 kN | – | – |
| Q   | 7,267.5 kN  | – | – |
| GQ  | 47,920.0 kN | – | – |
| EX  | V = 4,065.2 kN | ux = 9.48 mm | 0.74 mrad (piso 3) |
| EY  | V = 4,065.2 kN | uy = 4.29 mm | 0.37 mrad (piso 4) |

Equilibrio ≤ 6e-10 kN; superposición ≤ 5e-12 kN. X más flexible que Y
(muros E/I trabajan en su plano en Y) — coherente con planos.

### Decisiones tomadas

- **Estrategia para el miércoles 26:** un solo ejercicio pulido (este modelo)
  presentado como avance informal al profesor antes de la entrega formal.
- Se mantiene retícula principal del plano 101 (sin sub-ejes). Cambiar de
  edificio después es trivial (GRID_X/Y paramétricos).
- La carpeta de Pablo queda como respaldo externo; la versión canónica vive
  en el repo.

### Pendientes para miércoles

- [x] **Oscar: verificación visual de láminas REALIZADA (dom 23) — conforme.**
      Identidad del edificio confirmada (ficha §7). Quedan por confirmar a ojo:
      muros perimetrales ME/MI solo en piso 1, G35 en rotulado, geometría
      fuera del bloque 45×16.15.
- [ ] Preguntar al profesor: ¿alcance piso tipo vs completo?, fecha/hora entrega
- [ ] Nicolás: Issues + ai_log + sección arquitectura puede referenciar el JSON ya existente

---

## ⏭️ PRÓXIMOS PASOS (checklist general)

### Estado al cierre Sesión 4 (lun 25 ago)
- Repo completo y sincronizado: modelo corregido (`src/benchmark_3d/`), **20 tests en verde**,
  JSON contrato Unity, visualizador 3D + PNG, `requirements.txt`.
- **`reports/semana01.md` COMPLETO** (8/8 secciones): §1–3 Oscar, §4–5 Pablo, §6–8 Nicolás.
- Pablo añadió: `src/benchmark_3d.py` (punto de entrada único), `src/errores_deliberados.py`
  (2 experimentos), tests expandidos (15→20, +TestReferenciasAnaliticas +TestTablaVerificacion).
- Nicolás añadió: `reports/seccion6_arquitectura.md`, `docs/ai_log.md`, esquema JSON en `data/`
  (nodos, elementos, materiales, cargas).
- **Trabajo de integración:** se completaron §4–8 del reporte融合ando el trabajo de Pablo y Nicolás.

### Oscar (queda pendiente para miércoles 26)
- [ ] Estudiar secciones 1–3 del reporte con sus palabras (evaluación individual)
- [ ] Presentar avance al profesor + preguntar ¿piso tipo o edificio completo? y ¿fecha/hora exacta entrega?
- [ ] Confirmar si el profesor respondió el mail

### Pablo (instrucciones listas para clonar y partir)
- `git clone` → `python -m venv .venv` → `pip install -r requirements.txt` →
  correr tests (15 passed) y `analizar.py` (VERIF-1/2/3 OK).
- Llenar §4 (tabla Magnitud|Referencia|OpenSees|Error<1%) y §5 (2 errores
  deliberados con detección) en `reports/semana01.md`.

### Nicolás (independiente)
- [ ] Crear Issues en GitHub (uno mínimo por sección del reporte)
- [ ] Iniciar `docs/ai_log.md` (primera entrada ejemplo en bitácora Sesión 1)
- [ ] Redactar §6 arquitectura (insumos: JSON contrato, carpetas aplicadas,
      visualizador) y revisar §8 ya prellenada

---

## 📅 Sesión 4 — Lunes 25 de agosto 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo

1. **Sincronización con GitHub:** pull de los commits de Pablo y Nicolás:
   - Pablo: `src/benchmark_3d.py` (punto de entrada unificado), `src/errores_deliberados.py`
     (2 experimentos de error deliberado), tests expandidos a 20 (añadidos
     TestReferenciasAnaliticas y TestTablaVerificacion).
   - Nicolás: `reports/seccion6_arquitectura.md` (§6 completa), `docs/ai_log.md`
     (registro de uso de IA), esquema JSON en `data/` (nodos, elementos, materiales, cargas).

2. **Verificación local:** 20 tests pasan (1.2 s), errores deliberados ejecutados OK.

3. **Integración del reporte:** `reports/semana01.md` completado (8/8 secciones):
   - §4 Verificación: tabla equilibrio por caso (error 0.00%), superposición (< 5e-12),
     referencias analíticas (Euler-Bernoulli < 1e-10), derivas de piso.
   - §5 Errores deliberados: experimento 1 (inercia −50% → +23–35% desplazamiento),
     experimento 2 (carga invertida → deformada absurda).
   - §6 Arquitectura: estructura carpetas, esquema JSON, interfaz OpenSees→Unity.
   - §7 Uso de IA: registro 4 sesiones, formato Issue/Plan/Implementación/Test/Revisión.

### Próximos pasos (abren la Sesión 5 — miércoles 26)

1. **Oscar:** presentar avance al profesor, preguntar alcance y fecha exacta.
2. **Grupo:** preparar presentación/defensa del avance si el profesor lo pide.
3. **Pendiente técnica:** implementar secciones T/L de vigas (aporte de la losa a la inercia).
4. **Pendiente técnica:** mejorar rigidez al cortante de muros.

---

## 📅 Sesión 5 — Miércoles 26 de agosto 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo

1. **Análisis de mejoras al modelo** (discusión con Oscar):
   - Se identificaron 3 mejoras necesarias: (a) muros segmentados por piso con
     conexión a vigas, (b) secciones T/L de vigas, (c) rigidez cortante de muros.
   - El profesor indicó: NO usar elementos shell, mejorar de manera simple.

2. **Diagnóstico del problema de muros:**
   - Los muros estaban como 1 solo elemento de 16 m (base a techo) con nodo aislado
     del sistema de vigas.
   - El `rigidDiaphragm` copulaba ux/uy/rz pero NO uz → el muro no cargaba peso
     de la losa, solo su peso propio.
   - Muros M1c-E y M2a tenían endpoints fuera de la retícula, sin conexión a vigas.

3. **Implementación de mejora de muros** (`construir.py` reescrito):
   - Cada muro ahora tiene **2 endpoints por nivel** (uno por cada extremo).
   - **Muros sobre reticula** (ME-32, MI-32, MI-12): comparten nodo con la viga
     en la intersección de ejes (sin duplicados, usando `set`).
   - **Muros fuera de reticula** (M1c-E, M2a): nodo propio + `equalDOF` (rigid link)
     al nodo de viga más cercano → copula ux/uy/rz.
   - Diafragma incluye todos los nodos de muro como esclavos.
   - `analizar.py` actualizado para la nueva estructura de `wall_nodes` (3 claves).

4. **Verificación:**
   - 20/20 tests pasan (0.36 s).
   - Equilibrio global: errores < 1e-10 en todos los casos.
   - Superposición R(G)+R(Q)=R(GQ): error < 5e-12.
   - Errores deliberados: funcionan correctamente.

### Resultados del modelo mejorado

| Magnitud | Antes | Ahora | Cambio |
|---|---|---|---|
| Fz total caso G | 40,652 kN | **43,177 kN** | +6.2% |
| V sísmico (EX/EY) | 4,065 kN | **4,318 kN** | +6.2% |
| Techo EX (ux) | 9.48 mm | **9.73 mm** | +2.6% |
| Techo EY (uy) | 4.29 mm | **2.63 mm** | **−39%** |
| Deriva máx EX | 0.740 mrad | **0.758 mrad** | +2.4% |
| Deriva máx EY | 0.370 mrad | **0.227 mrad** | **−39%** |

**Interpretación:** el peso aumenta porque los muros ahora participan en la
carga axial (antes solo llevaban su propio peso). La rigidez en Y mejora
drásticamente porque los muros MI-32/MI-12 ahora están conectados a las vigas
del eje I y trabajan correctamente como elementos verticales de rigidez.

### Pendientes para Sesión 6

- [ ] Implementar secciones T/L de vigas (aporte de la losa a Iy).
- [ ] Mejorar rigidez al cortante de muros.
- [ ] Actualizar reporte §4–5 con nuevos valores del modelo.
- [ ] Commit + push de todos los cambios de hoy.
- [ ] Preguntar al profesor: ¿piso tipo o edificio completo? y fecha/hora entrega.

---

## 📅 Sesión 6 — Viernes 28 de agosto 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Contexto de entrega (información del profesor)

- **Entrega próxima semana:** modelo "de palitos" del proyecto COMPLETO = los
  DOS edificios (A y B), sin refinamiento de elementos finitos, con conexión a
  Unity y **visualización en Unity**.
- Estrategia acordada: pulir primero el edificio actual (benchmark) antes de
  extender a los 2 edificios. Oscar modelará el edificio en **SAP2000** estos
  días para verificar reacciones y respuestas de forma cruzada.

### Qué se hizo

1. **Secciones T/L de vigas** (`datos_edificio.sec_viga_compuesta`):
   - Ancho efectivo de losa ACI 318-19 Tabla 6.3.2.1 (e = 15 cm).
   - T (interior) vs L (borde): borde = líneas extremas (y=A3/A1 para vigas X;
     x=E/I2 para vigas Y).
   - Peso propio de viga sigue usando SOLO el alma (0.60×0.80): la losa ya vive
     en q_G → sin doble conteo. La rigidez (axial y flexión) usa la compuesta.
2. **Rigidez al cortante de muros** (`datos_edificio.shear_area_muro`):
   - `elasticBeamColumn` con opción `-shear Ay Az` (Timoshenko). Áreas A/1.2
     (k=5/6, convención SAP2000). `elasticTimoshenkoBeamColumn` 3D NO existe en
     OpenSeesPy 3.8 → se usa el `-shear` del elasticBeamColumn.
3. **Tests:** 20 → **25** (viga compuesta, ancho efectivo, A_cortante, saturación
   en viga corta). Suite completa: **50 passed** ~2.8 s.
4. **Reporte y docs actualizados:** `reports/semana01.md` (§1.3/1.5/1.7/2/3/4/5/6/7),
   `src/benchmark_3d/README.md`, `src/errores_deliberados.py` (resumen).

### Resultados del modelo perfeccionado

| Magnitud | Sesión 5 | Sesión 6 (T/L + cortante) | Cambio |
|---|---|---|---|
| Fz caso G | 43,177 kN | 43,176.8 kN | ~0 (peso no cambia) |
| V sísmico | 4,318 kN | 4,317.7 kN | ~0 |
| Techo EX (ux) | 9.73 mm | **8.05 mm** | **−17%** |
| Techo EY (uy) | 2.63 mm | **2.55 mm** | −3% |
| Deriva máx EX | 0.758 mrad | **0.622 mrad** | **−18%** |
| Deriva máx EY | 0.227 mrad | 0.220 mrad | −3% |

**Interpretación:** las vigas compuestas rigidizan pórticos en X (−17%), donde
manda el sistema de pórticos; en Y dominan los muros (ya rígidos, −3%).
Equilibrio y superposición intactos (≤ 1e-10).

### Pendientes (abren Sesión 7)

- [ ] **Oscar:** modelo SAP2000 del edificio B → comparar reacciones/derivas
      con OpenSees (verificación cruzada).
- [ ] Confirmar con el profesor: alcance reconocido de "piso tipo vs completo"
      para la entrega de "palitos" de ambos edificios + fecha/hora exacta.
- [ ] Extender a Edificio A y ensamblar los 2 edificios (paramétrico GRID_X/Y).
- [ ] Conexión Unity: visualización del modelo de palitos (JSON ya exporta
      nodos/elementos/resultados; vigas ahora con `seccion: T/L`).

---

## 📅 Sesión 7 — Viernes 28 de agosto 2026 (tarde)

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo

1. **Modelado del BLOQUE 2 (50 m E–J, anexo metálico)** — de los planos
   102/103 (eje J en x=5490 cm; patrón de muros/pilares del núcleo E–I'
   idéntico al bloque 1):
   - Nuevo `src/benchmark_3d/bloque2.py` (API espejo de `datos_edificio.py`):
     `GRID_X` = E/F/G/H/I/I'=45/J=50 m; `GRID_Y` = misma; materiales acero
     **E_s=200 GPa, G_s=77 GPa** (grado del acero no legible en DXF).
   - Anexo metálico en la bahía I'–J sobre las líneas con pilares {A3,A2,A1}:
     rigas **V.M. 300×300×5** y columnas de fachada **P.M. 300×300×20**
     (tubo cerrado, `datos_edificio.sec_box`, J = (b−t)³·t). Simplificación
     documentada: sin V.M./P.M.I. intermedios ni +V.I. 20/90 de la lámina.
   - `construir`, `cargas`, `analizar` generalizados con parámetro `dat=`
     (default bloque 1): el peso propio ahora viaja por elemento
     (`A_pp`/`rho_pp`) para distinguir hormigón y acero. Resultados del
     bloque 1 **idénticos** a la Sesión 6 (comprobado: techo EX 8.05 mm).
2. **Decisión arquitectónica (importante):** los bloques se analizan como
   **MODELOS INDEPENDIENTES** (estructuras separadas aunque colindantes en E);
   no hay un solo modelo con ambos (hubiera doble-contado las áreas
   tributarias del núcleo compartido E–I').
3. **Salidas para la entrega "palitos":**
   - `results/modelo_resultados_b2.json` (bloque 2).
   - `results/edificio_completo.json` = ambos bloques con tags únicos
     (bloque 2 renumerado +100 000) + totales G/EX/EY → contrato Unity.
   - Visualización: `results/modelo_3d_b2.png` y `results/modelo_3d_ambos.png`
     (bloque 2 desplazado +56 m SOLO para la figura; en el modelo la
     colindancia es en E, offset paramétrico default 0).
4. **Tests:** `tests/test_benchmark.py` 25 → **37** (bloque 2: retícula 50 m,
   área 807.5 m², `sec_box`, anexo acero 12 columnas + 12 rigas, núcleo
   hormigón intacto, conservación, equilibrio G/EX, V=α·W). Suite completa:
   **62 passed** ~3.2 s.

### Resultados del bloque 2 (G35 + acero E=200 GPa, α=0.10)

Área por piso 807.50 m² (vs 726.75 del bloque 1); 19 columnas/piso de
hormigón (45 m) + 3 de acero (J). Peso G ≈ 46,579 kN (+8% respecto al
bloque 1). Techo EX = **8.51 mm** (deriva máx 0.657 mrad), EY = 2.38 mm.

| Magnitud | Bloque 1 | Bloque 2 |
|---|---|---|
| Área/piso (m²) | 726.75 | 807.50 |
| ΣFz caso G (kN) | 43,176.8 | 46,579.3 |
| Techo EX (mm) | 8.05 | 8.51 |
| Techo EY (mm) | 2.55 | 2.38 |
| Deriva máx EX (mrad) | 0.622 | 0.657 |

### Decisiones tomadas / supuestos documentados

- Bloques **colindantes alineados en E** (offset paramétrico `(0,0)`).
- Acero: E=200 GPa, G=77 GPa, peso 78.5 kN/m³; ajustar si el profesor entrega
  la especificación del grado.
- Anexo simplificado (solo fachada J); muros del bloque 2 = replicados del 1.
- No commit/push aún (esperando decisión/entrega del avance).

### Pendientes (abren Sesión 8)

- [ ] **Oscar:** lo que quedó pendiente (SAP2000 edificio B).
- [ ] Verificar en láminas 102/103 la posición exacta de V.M./P.M.I. del anexo
      (por si se enriquece el modelo de palitos).
- [ ] Unidad Unity: cargar `edificio_completo.json` y generar el modelo de
      palitos de ambos edificios.
- [ ] Cerrar decisión commit/push del avance.

---

## 📅 Sesión 8 — Sábado 29 de agosto 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Contexto

- Entrega próxima semana: modelo "de palitos" COMPLETO (2 edificios) con
  visualización en Unity. Acuerdo: Oscar arma SAP2000 en paralelo (verificación
  cruzada) mientras el agente avanza el lado Unity.
- **Unity NO está instalado** en el PC de Oscar (sin Hub ni Editor). Verificado
  por el agente (rutas por defecto, registro, AppData). Se trabaja dejando los
  scripts listos para pegar cuando se instale.

### Qué se hizo

1. **Contrato JSON confirmado** (`results/edificio_completo.json`):
   - `edificios[2]` (B1: 180 nodos + 300 elementos; B2: 205 nodos tags +100000
     + 348 elementos; tipos column/wall/vigas_x/vigas_y).
   - `resultados.<caso>.desplazamientos_maestro.<nivel>` (ux, uy, rz, z) →
     suficiente para deformada porque diafragmas son rígidos.
   - `totales.G/EX/EY` (G=89,756 kN, V EX/EY=8,975.6 kN para el complejo).
2. **Carpeta `unity/` creada con scripts Unity listos (C#):**
   - `Scripts/ModeloComplejo.cs` — clases espejo del contrato JSON
     (usa Newtonsoft Json para dicts de nodos/resultados).
   - `Scripts/UnityStickModel.cs` — carga `StreamingAssets/edificio_completo.json`,
     dibuja palitos de ambos bloques con LineRenderer (B2 desplazado en +X SOLO
     visual, default 60 m), láminas por tipo, deformada por caso (Base/G/GQ/EX/EY)
     con amplificación, botón Recargar JSON.
   - `Scripts/OrbitCamera.cs` — cámara orbital (drb = rotar, mmb = pan, rueda = zoom).
   - `README.md` — pasos de instalación (Hub + Editor 2022.3 LTS + paquete
     `com.unity.nuget.newtonsoft-json`) y puesta en marcha.
3. **Test del contrato:** `tests/test_json_contrato.py` (14 tests): estructura,
   tags únicos, conectividad ni/nj, maestro por nivel, casos por bloque,
   equilibrio sísmico en totales. Suite completa: **76 passed** (~2.9 s).

### Decisiones tomadas

- Se escribe el código de Unity SIN instalar Unity (no lo bloquea); cuando se
  instale, solo copiar `unity/Scripts/` a `Assets/Scripts/`, copiar el JSON a
  `Assets/StreamingAssets/` y arrastrar el script a un GameObject.
- Mapeo plano del modelo → Unity: X=x, Y=z (altura), Z=y (SI m).
- Deformada por diafragma rígido (maestro por nivel) — no requiere resultados
  nodo a nodo.

### Pendientes (abren Sesión 9)

- [ ] **Oscar:** instalar Unity Hub + Editor 2022.3 LTS y seguir `unity/README.md`.
- [ ] **Oscar:** SAP2000 del edificio B (sigue pendiente).
- [ ] Confirmar con el profesor alcance y fecha/hora de entrega.
- [ ] Regenerar `edificio_completo.json` si el modelo cambia (los tests del
      contrato protegen a Unity).
- [ ] Commit/push del avance (incluye `unity/` y test del contrato) — decidir
      con el grupo.

---

## 📅 Sesión 9 — Sábado 29 de agosto 2026 (tarde)

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo — PRIMER MODELO COMPLETO EN UNITY ✅

1. **Unity instalado y operativo:**
   - Se instaló Unity Hub y el Editor **2022.3 LTS** en `C:\Program Files\Unity\Hub\Editor\2022.3.62f3`.
   - Se activó la licencia personal (sin `.ulf`, el editor arrancó al enfocar la
     ventana con Unity Hub abierto). `ProjectSettings\ProjectVersion.txt` = 2022.3.62f3.
   - Proyecto creado: `unity/EdificioIngUnity/` (Assets + Packages con
     `com.unity.nuget.newtonsoft-json` 3.2.1). Scripts copiados de
     `unity/Scripts/` → `Assets/Scripts/`; JSON copiado a `Assets/StreamingAssets/`.
   - `Assets/Editor/MCOCSceneSetup.cs`: prepara la escena `Main` (cámara con
     `OrbitCamera` + `UnityStickModel`), menú `Tools/MCOC/Preparar escena Main`.
   - Primer compile: 0 errores con los 3 scripts. `.gitignore` extendido para
     artefactos Unity (Library/Temp/Obj/Logs/UserSettings/Builds/csproj/sln y
     el JSON copiado en StreamingAssets).

2. **Apoyos visualizados (empotramiento):**
   - El contrato ahora exporta `edificios[i].apoyos` = base de columnas/muros en
     nivel 0 (**23 en B1, 26 en B2, todos `empotrado`** con `constraint=[1,1,1,1,1,1]`,
     6 GDL fijos, `construir.definir_base`). Fix histórico: no exportar los nodos
     de nivel 0 de la retícula vacía (daban "zapatas fantasma").
   - C# `ApoyoModelo` + dibujo: empotrado → zapata cubo gris (1.2×1.5×1.2);
     fijo → cono naranja (base en el suelo). Suelo oscuro en `zMin−1.5`.

3. **Cámara interactiva (OrbitCamera reescrito):**
   - Órbita sobre **centro real** (el pan desplaza el centro, ya no "rebota" al rotar).
   - Vuelo: `W/S/A/D` sobre plano, `Q/E` subir/bajar; suavizado (Lerp/Slerp
     framerate-independiente).
   - Presets: `V` arriba (planta), `F` frente/elevación, `C` perspectiva, `R` reset;
     `1/2` → **enfocar Bloque 1/2** (`CentroBloque`/`SpanBloque` añadidos a
     `UnityStickModel`). Panel de cámara arriba-derecha (arrastrable).
   - Fix CS0136 (variable `right` declarada dos veces en `ProcesarEntrada`).
   - De paso, el slider **Separación bloques** ahora mueve el bloque 2 en vivo.
   - Ruta de navegación típica: `1` (enfocar B1) → `W` (acercar) → `V` (arriba) → `2` (B2).

4. **Fix: "columnas desordenadas" en Unity** (`analizar.exportar_json`):
   - Causa raíz: al exportar nodos de muro se sobrescribía la posición de nodos
     **compartidos con la rejilla** con el **centro del muro** `(xc, yc)`, cuando
     la posición real es el **extremo** (o directamente el nodo de rejilla). Así
     columnas en y=0/7.25 quedaban arrastradas a y=3.625/11.7 (fachadas x=0 y x=40).
   - Fix: si el tag ya existe en `nodos`, conservar su posición (solo anotar `rol`);
     si es nodo nuevo de muro, usar la coordenada real de creación
     (`construir._wall_endpoints`, extremo del muro).
   - **Test de regresión:** `tests/test_json_contrato.py` + `TestColumnasEnMalla`
     (toda columna vertical, en GRID_X × COL_EN_Y). **Suite: 83 passed** (~4.7 s).
   - Verificación: los conjuntos de columnas python vs modelo son **idénticos**
     (B1 18, B2 21 posiciones; diff vacío).

5. **Muros como PANELES en Unity (match python):**
   - Las líneas verticales de muro ("sticks") parecían columnas sueltas cerca de
     las esquinas. En realidad eran los extremos de muro M2a (3.15–6.85 m,
     esquina SW) y M1c-E (0–6.6 m).
   - Unity ahora renderiza los muros de `geometria.secciones.muros` como paneles
     translúcidos rojos (2 caras ±t/2, por piso), igual que el visualizador python
     (`#C62828`, α=0.55). Se eliminan los sticks de muro; los paneles siguen al
     slider de separación (reconstrucción por offset).

### Decisiones / notas

- Las zapatas grises = **condición de empotramiento** (6 GDL fijos en base); hay
  23/26 y algunas quedan visualmente "dentro" del panel rojo de los muros (ok,
  son la base de columnas y extremos de muro).
- Mapeo plano→Unity conservado: X=x, Y=z (altura), Z=y.
- Validación manual: modelo de palitos COMPLETO (ambos edificios) visible y
  navegable en Unity — primer hito de la entrega de Unity.

### Qué se hizo — 2ª parte (visualización de cargas y verificación)

6. **SAP2000 (contexto):** la licencia educativa personal se venció y CSI ya no
   otorga Student Demo; ahora es **SAP2000 Ultimate (uso instruccional)** vía la
   universidad con Cloud Sign-in. Se descargó el **trial de 30 días**
   (csiamerica.com/products/sap2000/trial) y quedó en evaluación por soporte;
   mientras tanto se generó **`docs/receta_sap2000_b2.md`** con toda la receta
   numérica del edificio B (retícula, materiales, secciones, muros, diafragmas,
   cargas y valores OpenSees de referencia: ΣFz G=46,579 kN, techo EX=8.51 mm,
   EY=2.38 mm). El SAP es verificación cruzada, no bloquea la entrega.

7. **Fix del exportador y contrato ampliado (`cargas`):**
   - `analizar.exportar_json` ahora exporta por edificio:
     `cargas.vigas[]` (qG/qQ [kN/m] y G/Q [kN] por viga), `peso_columnas_muros`,
     `pesos_por_nivel` (W_i) y `sismo.{EX,EY}` (V_base y F_por_nivel).
   - `_rematar_tags` renumera tags de `cargas.vigas` en el bloque 2.
   - Nueva función pura `cargas.sismo_v_base_y_fuerzas` (el patrón sísmico la
     reutiliza sin duplicar la fórmula).

8. **Verificación de cargas (`src/benchmark_3d/verificar_cargas.py`):** cruza el
   JSON exportado contra cálculos re-hechos desde la geometría (sin re-correr el
   análisis): sumas de vigas G/Q, pesos por nivel vs ΣFz G, equilibrio sísmico
   (V=ΣF_i, reacciones vs aplicadas) y W_i por nivel. **Resultado: completo OK**:
   - B1: ΣFz G=43,176.8 kN (vigas 33,765 + col/muros 9,411) · V_EX=4,317.7 kN.
   - B2: ΣFz G=46,579.3 kN (vigas 37,083 + col/muros 9,496) · V_EX=4,657.9 kN.
   - Totales complejo: G=89,756.0 kN, V EX/EY=8,975.6 kN, equilibrio OK.

9. **Flechas de cargas en Unity (toggles nuevos en el panel):**
   - **Cargas G (vigas):** flechas oscuras hacia abajo en el centro de cada
     viga = peso propio + q_G tributaria (en kN).
   - **Cargas Q (vigas):** flechas naranjas = sobrecarga q_Q (en kN).
   - **Cargas sismo (EX/EY):** flechas rojas horizontales en el centro de
     diafragma por nivel con la fuerza lateral F_i (se muestran con el caso
     EX/EY activo; el nivel 1 se omite por el artefacto de elevación ~9 kN).
   - Etiquetas numéricas siempre de frente a la cámara.
   - `ModeloComplejo.cs` ampliado (CargasModelo/CargaViga/CargaSismo).

10. **Tests:** nueva clase `TestCargas` en `tests/test_json_contrato.py`
    (estructura, referencias, sumas internas G/Q, sismo). **Suite: 89 passed.**
    JSON regenerado y copiado a `StreamingAssets`; scripts sincronizados a
    `unity/Scripts/`. Unity espera foco para recompilar → clic en la ventana y
    Play para ver las flechas.

### Estado de cierre (para retomar mañana)

- Sesión 9 cerrada: modelo completo en Unity (2 bloques), cámara fluida,
  contratos + tests (89), cargas G/Q/sísmicas verificadas y ya visibles como
  flechas. Todo documentado arriba y en `unity/README.md` y
  `docs/receta_sap2000_b2.md`.
- SAP2000: se envió el **trial** (en evaluación por CSI) y se preparó la
  solicitud por la vía universitaria. **Si mañana no hay licencia, el plan es
  revisar el código y entender el flujo completo** (ver siguiente punto).
- Recorrido propuesto para "entender el código" (Sesión 10, si SAP no llega):
  1. `docs/ficha_geometria.md` y `docs/receta_sap2000_b2.md` → la geometría.
  2. `src/benchmark_3d/datos_edificio.py` + `bloque2.py` → datos (retículas,
     niveles, secciones, materiales, muros, anexo de acero).
  3. `construir.py` → maquetado del modelo OpenSees (nodos, elems, diafragmas,
     apoyos, rigid links, convención de ejes).
  4. `cargas.py` → tributaria, pesos por nivel, patrón sísmico.
  5. `analizar.py` → casos G/Q/GQ/EX/EY, verificaciones y exportar JSON
     (incluye el bloque `cargas`).
  6. `verificar_cargas.py` → el cross-check que se corrió hoy.
  7. `tests/test_json_contrato.py` → lo que protege el contrato.
  8. Unity: `ModeloComplejo.cs` (clases espejo) → `UnityStickModel.cs`
     (dibujo + GUI + flechas) → `OrbitCamera.cs` (cámara).
- Sugerencia: probar en Unity las flechas G/Q/sismo y anotar dudas para
  resolverlas en la siguiente sesión.

### Pendientes (abren Sesión 10)

- [ ] **Oscar:** SAP2000 del edificio B (verificación cruzada, sigue pendiente).
- [ ] Confirmar alcance y fecha/hora de entrega con el profesor.
- [ ] Commit/push del avance completo (`unity/`, tests contrato, docs) —
      decidir con el grupo.
- [ ] Opcional: paneles de muro deformándose en los casos (hoy son estáticos);
      líneas de rigid-link (muros fuera de retícula) para mayor claridad.

---

## 🎨 Sesión 10 — Miércoles 2 de septiembre 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo

1. **Rediseño 3D realista del modelo Unity** (transformación del "modelo de
   palitos" en un modelo estructural con cuerpo):
   - **Columnas:** cajas de concreto gris (0.70 × 0.70 m, sección COL_B/COL_H).
   - **Vigas:** secciones naranjas (0.60 × 0.80 m, BEAM_B×BEAM_H).
   - **Muros:** paneles de concreto más oscuros que deforman con el nivel
     (ya se deformaban desde la Sesión 9, retoque de color).
   - **Zapatas:** conos earth-tone (café) en cada apoyo.
   - **Color de cielo celeste** y terreno de base.
   - Se elimina la losa/suelo estructural (seguía la preferencia: solo
     esqueleto + terreno de cimentación).
   - Todas las secciones leídas de `datos_edificio.py` (COL 0.70, BEAM 0.60×0.80).

2. **Fix del render de vigas (problema de "vigas transparentes/huecas"):**
   - La causa era la malla manual T/L (`MeshVigaT`/`MeshVigaL`): solo tenía
     caras laterales sin tapas, por lo que se veían como prisma abierto.
   - Se reemplazó por **cubos primitivos de Unity** (`PrimitiveType.Cube`, opacos
     y sólidos) posicionados/rotados/escalados entre nodos.
   - Nuevo `PosicionarElemento3D` con matemática de orientación robusta
     (`Quaternion.LookRotation` con base ortonormal right/fwd/up).
   - Resultado: las vigas ahora se ven como prismas naranjas sólidos con su
     sección (0.6 × 0.8) estirada a lo largo del vano.

3. **Cielo (fix del fondo azul):**
   - `Main.unity` tenía la cámara en `m_ClearFlags: 1` (Skybox), que no
     mostraba el color configurado.
   - Cambiado a `m_ClearFlags: 2` (SolidColor) con `m_BackGroundColor` celeste
     (0.53, 0.81, 0.92).

4. **Iluminación de "día despejado":**
   - La escena NO tenía ninguna Light (solo RenderSettings/LightmapSettings).
   - `MCOCSceneSetup` ahora crea una **Directional Light** (Sol) con sombras
     suaves, intensidad 1.1 y ambient Flat.
   - Todos los materiales pasaron de `Unlit/Color` a shader **Standard**
     (iluminados), con `_Glossiness` por material (columna 0.4, viga 0.3,
     muro 0.1, zapata 0.2).

5. **Terreno de base (nuevo, no es losa):**
   - Se añadió un plano **terreno** color tierra (café claro 0.55/0.45/0.30) que
     se dimensiona dinámicamente al contorno de los apoyos y se sitúa debajo de
     la cimentación (zMin - 2.2 m).
   - Toggle **"Terreno"** en el panel (campo `mostrarSuelo`, default true).

### Estado de cierre (para retomar)

- **Sesión 10 cerrada con resultado visual aprobado por Oscar:** el modelo se
  ve como un edificio estructural realista (columnas de concreto, vigas
  naranjas sólidas, muros más oscuros, cielo celeste, terreno) manteniendo toda
  la deformación estructural por JSON.
- Scripts sincronizados a `unity/Scripts/`: `UnityStickModel.cs`,
  `MCOCSceneSetup.cs`, `ModeloComplejo.cs`, `OrbitCamera.cs`.
- Escena `Main.unity` actualizada (cielo SolidColor + luz sol).
- `MCOCSceneSetup` ahora también crea la luz si falta (menú
  **Tools > MCOC > Preparar escena Main**).

### Pendientes (abren Sesión 11)

- [ ] **Oscar:** SAP2000 del edificio B (verificación cruzada, sigue pendiente).
- [ ] Confirmar alcance y fecha/hora de entrega con el profesor.
- [ ] Commit/push del avance completo (Unity rediseño, escena) — decidir con el
      grupo, esto NO está commiteado.
- [ ] Opcional / próximos pasos de pulido visual:
  - etiquetas de alturas/niveles más legibles;
  - selección interactiva de un elemento para resaltarlo (raycast);
  - textos de resultados (desplazamientos por nivel) como overlay;
  - líneas de rigid-link (muros fuera de retícula) para mayor claridad.

---

## 📐 Sesión 11 — Miércoles 2 de septiembre 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo — visualización de áreas tributarias a 45° (toggle "Areas tributarias 45 (losa)")

1. **Diagnóstico del "triángulo pequeño vacío"** que Oscar notó en cada sección
   (se compensaba en la viga siguiente, en todos los pisos):
   - **NO era un artefacto visual ni de cálculo** sino un bug real de dibujo.
   - Los trapecios/triángulos se dibujaban panel por panel con los triángulos de
     viga Y (y X en el anexo) con **base MEDIA** (`(x0,yc)-(x0,y1)`) en vez de
     **base completa** (`(x0,y0)-(x0,y1)`). Con vanos `dy` desiguales
     (2.31/4.94/5.0/3.9 m) eso dejaba cuñas sin asignar en las esquinas → los
     "triángulos vacíos" que se veían, compensados por la viga vecina.
   - Verificado también que en el JSON **todas las vigas tienen carga** (0 vigas
     con G=0 en ambos bloques): el modelo/equilibrio están correctos; el problema
     era solo la representación.

2. **Rediseño a patrón 45° CONTINUO y EXACTO (mosaico clasificado por distancia):**
   - Reescrito `CrearTributaria` en `UnityStickModel.cs`. Se subdivide cada losa
     en una **malla fina (paso 0.50 m)** y cada celda se asigna a la **viga más
     cercana** (comparando distancia a los 4 bordes del panel → líneas de corte
     a 45° exactas y globales, sin huecos ni solapamientos).
   - Flood-fill para agrupar celdas contiguas de la **misma viga** en regiones.
   - Render: **1 Mesh agregado por nivel** con colores por vértice (naranja =
     viga X, azul = viga Y; `mesh.colors` + shader `Sprites/Default` vía
     `MaterialTributariaMosaico`) + **1 Mesh de contornos** (quads delgados
     oscuros alrededor de cada borde de región) + **1 etiqueta TextMesh por
     viga/región** con su carga `Q_G × área` en kN.
   - El mosaico se deforma como cuerpo rígido del diafragma por nivel
     (`ReconstruirNivelTributario`: rotación del maestro + ux/uy + amplificación)
     y sigue el offset de separación de bloques.

3. **Etiquetas numéricas (kN por trapecio/viga):**
   - Se exporta `Q_G`/`Q_Q` (kN/m²) al JSON: `cargas.q_losa = {G: 6.3, Q: 2.5}`
     en `src/benchmark_3d/analizar.py` (`_exportar_cargas`).
   - `ModeloComplejo.cs` añade `CargasModelo.q_losa` (`CargasLosaModelo.G/Q`).
   - Regenerado `results/edificio_completo.json` (verificaciones: equilibrio
     ≤1e-10, superposición ≤3.8e-12, totales G=89,756 kN intactos) y copiado a
     `StreamingAssets`.
   - Cada región (viga) muestra su kN = `Q_G × área` (coincide con la repartición
     exacta). Etiquetas siempre de frente a la cámara.

4. **Compilación y sincronización:** validado con el Roslyn de Unity (0 errores)
   y copiado `UnityStickModel.cs`/`ModeloComplejo.cs` de `unity/Scripts/` a
   `Assets/Scripts/` (hash verificados idénticos). El editor abierto recompila al
   darle foco (los errores de licencia del log son ruido normal sin ULF).

### Decisiones / notas

- El mosaico a paso 0.50 m es un compromiso nitidez/rendimiento (reconstrucción
  en vivo al arrastrar el slider de separación). Si se quiere más fino, bajar
  `paso` en `CrearTributaria` (0.50 → 0.25) a costa de más vértices.
- El patrón es el de los libros/SAP2000 (área más cercana a la viga), continuo y
  sin los huecos del dibujo anterior.
- **Oscar: por ahora está bien el efecto "Tetris".** Pendiente para mañana si
  cambia de opinión: suavizar las regiones a **polígonos con bordes rectos**
  (diagonales a 45° limpias, sin escalones). Idea: usar el mosaico clasificado
  solo para decidir a qué viga va cada celda, y luego dibujar CADA VIGA como un
  único polígono (malla única con contorno simplificado, sin dientes de 0.5 m) —
  más fiel a los libros/SAP2000. Reconstrucción en `CrearTributaria`/
  `ReconstruirNivelTributario` de `UnityStickModel.cs`.

### Pendientes (abren Sesión 12)

- [ ] **Oscar:** dar foco a Unity para recompilar y probar el toggle "Areas
      tributarias 45 (losa)"; confirmar visualmente que desaparecieron los
      triángulos vacíos y que se leen los kN.
- [ ] **Oscar:** SAP2000 del edificio B (verificación cruzada, sigue pendiente).
- [ ] Confirmar alcance y fecha/hora de entrega con el profesor.
- [ ] **Commit/push del avance** (pendiente desde Sesión 9/10; incluye rediseño de
      escena, tribunaria, JSON con `q_losa`) — decidir con el grupo.

---

## 📐 Sesión 12 — Jueves 3 de septiembre 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Contexto

- Pequeña evaluación en clase. El proyecto ya abierto en Unity (Unity 2022.3.62f3,
  escena `Main` vía **Tools > MCOC > Preparar escena Main**).
- Estructura de archivos clave repasada para la evaluación (modelo =
  `src/benchmark_3d/`, contrato = `results/edificio_completo.json`, geometría =
  `docs/ficha_geometria.md`).

### Qué se hizo

1. **Cambio de separación de bloques: "en serie" → "en paralelo"** (`UnityStickModel.cs`):
   - `separarBloquesX` renombrado a **`separarBloquesY`** (los edificios ahora se
     separan en profundidad/Y en vez de lado a lado/X, como en la realidad).
   - Aplicación del offset movida de `offset.x` a `offset.y` (construcción y
     `AplicarVisual`) y etiqueta del slider actualizada ("Separacion bloques Y").
   - ⚠️ Los scripts se editan en `unity/Scripts/` y deben **copiarse** a
     `unity/EdificioIngUnity/Assets/Scripts/` para que Unity los recompile.
2. **Painte para la evaluación — repaso conceptual:** peso muerto (G) incluye
   gravedad (peso propio ρ·g + PM_adic); `PM_ADIC=2.55 kN/m²` (acabados/tabiques)
   y `SC_AULA=2.50 kN/m²` (sobrecarga viva) definidos en `datos_edificio.py`;
   Q_G=0.15·25+2.55=6.30 kN/m² y Q_Q=2.50 kN/m². Áreas tributarias en
   `src/benchmark_3d/cargas.py` (`distribuir_nivel`/`distribuir_tributaria`);
   comando benchmark: `.venv\Scripts\python.exe src\benchmark_3d\analizar.py --ambos`.
3. **Toggle "Nodos" en Unity — INTENTO de fix (NO resuelto):**
   - Problema: al activar "Nodos" no se veía nada.
   - Diagnóstico inicial: las esferas de nodo se posicionaban SOLO al crear el
     modelo (offset 0,0, sin deformación) y nunca se reposicionaban → agregado
     `bv.nodosVis` (dict tag→GameObject) y reposicionamiento en `AplicarVisual`.
   - Segundo diagnóstico: esferas de **0.18 m** quedan **enterradas dentro de las
     cajas opacas sólidas** (columnas 0.7×0.7, vigas 0.6×0.8) → tamaño subido a
     **0.45 m**, color verde, shader `Unlit/Color`, renderQueue 4000 + sombras
     off (dibujar por encima).
   - ❗ **PERMANECE SIN RESOLVER:** con estos cambios SIGUE sin verse nada.
     Probable causa restante: los objetos de nodo no se muestran por alguna razón
     de activación/recompilación (el usuario no llegó a dar foco para recompilar
     en la última prueba), o tamaño/shaders no suficientes. **PENDIENTE para la
     próxima sesión** (ver checklist).

### Decisión

- Se abandona por hoy el fix de nodos; se retoma la próxima sesión.

### Pendientes (abren Sesión 13)

- [ ] **Retomar toggle "Nodos" en Unity** (dar foco para recompilar y verificar;
      si sigue invisible, depurar activación/posición/visibilidad real de
      `bv.nodosObj` y sus esferas).
- [ ] **Commit/push del avance** (pendiente desde Sesión 9/10/11; incluye
      rediseño de escena, tribunaria 45°, JSON `q_losa`, separación bloques en Y
      y el intento de nodos) — decidir con el grupo.
- [ ] **Oscar:** SAP2000 del edificio B (verificación cruzada, sigue pendiente).
- [ ] Confirmar alcance y fecha/hora de entrega con el profesor.

---

## 🏗️ Sesión 13 — Reestructuración a UN edificio ("Edificio A") — Jueves 3 de septiembre 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Contexto / motivación

- Los planos DWG en disco (`Desktop\planos_edificio_ing\`, serie **2017_67-***)
  corresponden a **UN solo edificio** (retícula 45 × 16.15 m), no a los dos
  bloques (A y B) que se asumieron en Sesiones 6–12.
- Se creó la carpeta **`Desktop\Proyecto 1 MCOC - Edificio A`** como punto de
  partida limpio de UN edificio, transfiriendo el código reutilizable del
  proyecto anterior y eliminando toda la lógica de Bloque 2 / doble edificio.
- El usuario señaló que hay **detalles estructurales omitidos** que deben
  redescubrirse revisando los planos con la pipeline ezdxf (ver Pendientes).

### Qué se hizo

1. **Estructura del nuevo proyecto creada** (todo el árbol de carpetas):
   `src/benchmark_3d/`, `tests/`, `results/`, `data/`, `docs/`, `unity/Scripts/`.
2. **Código transferido y simplificado a un solo edificio** (sin `bloque2`):
   - `datos_edificio.py`: se eliminaron referencias de bloque 2; quedó la data
     de un solo edificio (45 m, GRID_X de 5 ejes, 5 niveles, 5 muros, G35).
   - `construir.py`: limpio, sin lógica ANEXO/E_S ni parámetro de bloque 2.
   - `cargas.py`: se mantiene como estaba (ya era paramétrica por edificio).
   - `analizar.py`: se quitaron `bloque2`, `main_bloque2`, `main_ambos`,
     `_rematar_tags` y `_totales`; ahora `run_analisis()`/`main()` analizan UN
     edificio y exportan a `results/modelo_resultados.json`.
   - `verificar_cargas.py`: simplificado a un solo edificio.
   - `visualizar.py`: solo un edificio.
3. **Punto de entrada** `src/benchmark_3d.py` (analiza y exporta JSON).
4. **Tests adaptados:**
   - `tests/test_benchmark.py`: sin los tests de Bloque 2 (geometría,
     secciones, cargas, equilibrio, superposición, referencias analíticas).
   - `tests/test_json_contrato.py`: apunta a `results/modelo_resultados.json`;
     sin los assertions de dos edificios (`test_dos_edificios`, `_apoyos_b1_b2`).
5. **Unity scripts reescritos para UN edificio** en `unity/Scripts/`:
   - `ModeloEdificio.cs` (antes `ModeloComplejo.cs`): modelo JSON de un
     edificio (sin array `edificios`, sin `totales`).
   - `UnityStickModel.cs`: sin lista/bloque 2 ni separación de bloques;
     dibuja columnas/vigas/muros/zapatas/areas tributarias y deformada por caso.
   - `OrbitCamera.cs`: presets de cámara **sin** enfoque de Bloque 1/2.
   - `MCOCSceneSetup.cs`: adaptado a un solo edificio.
6. **Documentación copiada:** `docs/bitacora.md` (este archivo) y
   `docs/ficha_geometria.md` (se añadirá detalle si se re-examinan los planos).
7. **Data JSON copiada:** `data/{nodos,elementos,cargas,materiales}.json`
   (esquema/ejemplo).
8. **READMEs creados:** `README.md` (raíz), `src/benchmark_3d/README.md`,
   `unity/README.md`.

### Verificación

- **41 tests en verde** (25 benchmark + 16 contrato JSON) en ~0.3 s.
- El análisis del edificio único reprodujo los valores de la Sesión 6:
  - ΣFz G = 43,176.8 kN; V sísmico EX/EY = 4,317.7 kN.
  - Techo EX (ux) = 8.047 mm; techo EY (uy) = 2.546 mm.
  - Deriva máx EX = 0.622 mrad (piso 3); EY = 0.220 mrad (piso 4).
  - Equilibrio por caso ≤ ~5e-10 kN; superposición R(G)+R(Q)≈R(GQ) ≤ ~7e-12 kN.

### Decisiones tomadas

- El nuevo proyecto es la versión canónica de **un solo edificio (Edificio A)**;
  el proyecto anterior (`Proyecto 1 MCOC`) queda como respaldo histórico.
- Se mantiene el contrato Unity en `results/modelo_resultados.json`
  (mismo esquema de un edificio, sin `edificios[]`).
- Los valores numéricos coinciden con la Sesión 6 porque la geometría de este
  edificio era exactamente el "bloque 1" de la versión anterior.

### Pendientes (abren Sesión 14)

- [ ] **Revisar los detalles estructurales omitidos** con la pipeline ezdxf
      sobre los DWG 2017_67-*: muros por nivel (102/103), vigas V.40/60
      secundarias, vigas metálicas, geometría fuera del bloque 45×16.15, grado
      de hormigón/acero — antes de cerrar la geometría del Edificio A.
- [ ] Configurar `.venv` propio del nuevo proyecto (`python -m venv .venv` +
      `pip install -r requirements.txt`).
- [ ] **Oscar:** modelo SAP2000 para verificación cruzada (sigue pendiente).
- [ ] Confirmar alcance y fecha/hora de entrega con el profesor.

---

## 🏗️ Sesión 14 — Jueves 3 de septiembre 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo — voladizo/anexo metálico I'→J incorporado al modelo

1. **Análisis de los cortes y plantas** (pipeline ezdxf):
   - **Corte 1-1' (plano 300)** y **elevación EJE J (plano 310)**: confirman un
     **anexo/voladizo metálico I'→J** de 5 m (45→50 m) con:
     - **P.M. 300×300×20** (columnas de acero, tubo HSS) en la fachada J, en los
       **3 ejes Y** (A3, A2, A1);
     - **V.M. 300×300×5** (vigas de acero) en el tramo I'→J;
     - presente en los **2 niveles superiores**: losa +7.87 (indice 3) y +11.83
       (indice 4 = techo); por debajo el hormigón llega hasta I' (45 m).
   - **Plantas 102/103 y 203/204**: corroboran P.M./V.M. en piso 3 y piso 4.
   - Retícula Y (A3=0, A2=7.25, A1=16.15 m) verificada correcta; el voladizo es
     X (I'→J), no hay voladizo en la profundidad Y.

2. **Decisiones de alcance (confirmadas con Oscar):**
   - Modelar el **anexo de acero completo** (P.M. + V.M.).
   - Niveles: **como muestran los planos** → solo los 2 niveles superiores.
   - Cargas del voladizo: **solo en los niveles del anexo**.

3. **Cambios de código:**
   - `datos_edificio.py`: `GRID_X` con `J=50`; dict `ANEXO` (x0=I2, x1=J,
     levels=(3,4), secciones P.M./V.M.); `E_STEEL`/`G_STEEL`/`GAMMA_STEEL`;
     helpers `sec_pm`, `sec_vm`, `anexo_levels`.
   - `construir.py`: los ejes J (y sus nodos) se crean **solo en niveles del
     anexo**; columnas de hormigón P.70×70 en E..I' (todos los pisos) + pilares
     metálicos P.M. en J (piso superior); vigas X de hormigón hasta I' + vigas
     V.M. metálicas I'→J en niveles 3 y 4; vigas Y metálicas en fachada J.
   - `cargas.py`: `distribuir_nivel` ahora recibe `lvl` e incluye el panel
     voladizo (eje J) **solo en niveles del anexo** (área tributaria por nivel).
   - `analizar.py`: VERIF-1 refactorizado → área principal (4 pisos) + área
     voladizo (2 pisos).

4. **Resultados nuevos (con anexo, G35, α=0.10):**
   - Área losa principal = 726.75 m² × 4 pisos; voladizo = 80.75 m² × 2 pisos.
   - **G**: ΣFz = 44,729.5 kN (antes 43,176.8) · **Q**: 7,671.3 kN ·
     **GQ**: 52,400.8 kN.
   - **EX**: V = 4,473.0 kN, techo ux = 8.37 mm, deriva máx 0.647 mrad (piso 3).
   - **EY**: V = 4,473.0 kN, techo uy = 2.31 mm, deriva máx 0.200 mrad (piso 4).
   - Verif.: conservación ≤3.6e-12, equilibrio ≤2.6e-10, superposición ≤4.6e-12.

5. **Tests:** actualizados a la geometría 50×16.15 m con anexo parcial
   (`test_reticula_x_50m`, `test_voladizo_metalico_5m`, `test_anexo_solo_2...`,
   conservación con área cargada variable). **43 tests en verde** (27 benchmark
   + 16 contrato JSON) en ~0.4 s. Regenerado `results/modelo_resultados.json`.

### Decisiones / notas

- El voladizo es un **anexo metálico con columnas y vigas de acero** (no una
  simple losa en cantilever): se modelan P.M. 300×300×20 y V.M. 300×300×5.
- El hormigón del edificio principal llega hasta I' (45 m) en todos los pisos;
  el acero del anexo sólo en los 2 niveles superiores (como muestran los planos).
- El peso del acero se computa con densidad del acero (78.5 kN/m³) en
  `pesos_por_nivel` y en la reacción G.

### Pendientes (abren Sesión 15)

- [ ] Configurar `.venv` propio del nuevo proyecto (`python -m venv .venv` +
      `pip install -r requirements.txt`) — sigue pendiente (tests usan el venv
      del proyecto histórico).
- [ ] **Oscar:** modelo SAP2000 para verificación cruzada.
- [ ] Confirmar alcance y fecha/hora de entrega con el profesor.
- [ ] Actualizar `unity/README.md`/escena si se desea dibujar el anexo metálico.

---

## 🖥️ Sesión 15 — Viernes 4 de septiembre 2026

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo

1. **Visualización 3D del anexo metálico** (resultado de la Sesión 14):
   - Regenerado `results/modelo_3d.png` (anexo I'→J en naranja).
   - **Nuevo** `src/benchmark_3d/visualizar_html.py` → genera
     `results/modelo_3d.html`: modelo interactivo con **Three.js** (rotar/zoom),
     fiel a `construir.py` (anexo sólo en los 2 niveles superiores, acero en
     naranja; pilares hormigón, vigas, muros y contorno de losa por nivel).
   - Validado: 72 pilares hormigón + 3 acero P.M.; 196 vigas hormigón + 18 acero
     V.M.; 5 muros; 4 losas; grid X con J=50.

2. **Análisis de los planos — voladizos aún faltantes (dirección Y):**
   - Revisados cortes transversales (pls. 305=E, 306=F, 307=G, 308=H, 309=I,
     310=J) y plantas (101-103, 200-205) con pipeline ezdxf.
   - Triplete de columnas de hormigón P.70×70 en los cortes Y ubicado en
     x≈{-1205/A3, -480/A2, +410/A1}; **100 unidades de dibujo = 1 m**.
   - Se detectaron columnas/elementos **fuera de ese triplete** ⇒ voladizos en Y.

### Voladizos faltantes detectados (no modelados aún)

**Lado A3 (trasera, Y<0) — certeza ALTA:**
- **Eje F** (X≈10): P.M. 300×300×20 a −4.4 m de A3 (toda la altura) + V.M.
  300×300×5 a −2.2 m.
- **Eje G** (X≈20): P.M. 300×300×20 a −4.9 m + V.M. a −2.4 m (toda la altura).
- **Eje H** (X≈30): P.M. 300×300×20 a −3.8/−4.9 m (sólo pisos 3–4) + V.M. a
  −2.2 m.
- **Eje I'** (X≈48.7): P.M. 300×300×20 a −1.15 m (sólo techo).

**Lado A1 (frente, Y>16.15) — certeza MEDIA (verificar):**
- Ejes G, H, I': P.M.I. y V.M. 300×300×5 en piso P3.

**Certeza BAJA / a investigar:** eje E línea de altura completa a +10.7 m de A1
(probable muro de sótano, no columna); eje I viga V. 20/90 a +7.98 m (P1) sin
columna; eje I' viga V.I. 15/169 a +1.77 m (P1, no estructural).

**En dirección X: NO faltan voladizos** (nada más allá de E y de J).

### Decisión de cierre (acordada con Oscar)

- Se deja **sin tocar el modelo** (sigue en estado Sesión 14: anexo I'→J
  modelado, 43 tests en verde, PNG + HTML generados).
- **Oscar tiene una idea para potenciar el avance** → se retoma mañana.

---

## 📄 Sesión 16 — Viernes 4 de septiembre 2026 (documentación de planos para Claude)

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo

1. **Documento de contenido de planos generado** (para que Claude revise los
   planos y ayude a avanzar en los perfiles de voladizos):
   - `docs/info_planos_dxf.txt` (2,834 líneas) — contenido de los **38 planos
     DXF** de `Desktop\Planos DXF`: capas, textos, MTEXT, dimensiones, bloques,
     inserts por plano. Organizado por categoría (PLANTA / ELEVACION / CORTE /
     ARMADURA / METALICO / ESCALERA / CARGA / RESUMEN / DETALLE).
   - `docs/info_cortes_elevaciones.txt` (7,018 líneas) — detalle **de los
     BLOQUES** de los 11 planos de cortes/elevaciones (300–310). Importante:
     en estos planos el contenido (perfiles, textos de vigas/columnas) vive
     **dentro de bloques**, no en modelspace (por eso los TEXT no se ven en la
     extracción normal).
   - Scripts reutilizables: `scripts/extraer_info_planos.py` y
     `scripts/extraer_cortes_elevaciones.py` (usar `.venv` del proyecto,
     `pip install ezdxf`).
   - `.venv` propio del proyecto creado (pendiente de Sesión 13) +
     `pip install ezdxf`.

2. **Hallazgos de la extracción de bloques (300–310):**
   - Perfiles confirmados en cortes/elevaciones:
     * P.M. 300x300x20 (columnas de acero cajón) — ejes F, G, H, J, 1-1', 2, 3-3'
     * V.M. 300x300x5 (vigas metálicas) — mismos ejes
     * +V.I. 20/90 (2º ETAPA) — vigas invertidas
     * V. 60/80, +V. 60/80, V.F. 20/160, V.F. 20/120, V. 20/VAR
     * P. 70x70 (pilares hormigón), V. 20/90
   - Bloques de identificación de dibujo: viñeta-rl (cartela de diseño,
     oficina RENE LAGOS CONTRERAS / M. KUPFER C.), niveles 1º/1ºS/2º/3º/4º,
     escala 1:50 en cortes por eje.

### Pendientes (abren Sesión 17)

- [ ] Revisar con Claude los detalles de voladizos Y (A3 trasera / A1 frontal)
      usando `docs/info_cortes_elevaciones.txt` + `docs/info_planos_dxf.txt`.
- [ ] **Oscar:** SAP2000 para verificación cruzada.
- [ ] Confirmar alcance y fecha/hora de entrega con el profesor.
- [ ] Probar Unity del anexo metálico I'→J.

---

## 📄 Sesión 16b — Viernes 4 de septiembre 2026 (resumen del modelo actual)

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo

1. **Resumen del modelo actual generado** → `docs/resumen_edificio_actual.txt`
   (script `scripts/resumen_edificio.py`, python puro que replica
   `construir.construir()` SIN OpenSeesPy, para inspección rápida):
   - **Rangos [m]:** X 0.000–50.000 (incl. anexo I'→J) · Y 0.000–16.150 ·
     Z −4.210–11.830.
   - **Niveles:** −4.21 / −0.05 / +3.91 / +7.87 / +11.83.
   - **Nodos: 190** · **Elementos: 321** = 72 columnas hormigón P.70×70 + 3
     acero P.M. + 32 muros + 110 vigas X + 104 vigas Y (+ las losas NO son
     elementos: van como carga superficial q_G/q_Q).
   - **Último piso (z=11.83):** retícula completa hasta (50, 16.15); vigas
     superiores = 25 vigas X hormigón (E→I') + 5 V.M. metálicas (I'→J) +
     28 vigas Y. Bordes perimetrales (0,0), (0,16.15), (50,0), (50,16.15).

### Pendientes (abren Sesión 17)

- [ ] Revisar con Claude los detalles de voladizos Y (A3 trasera / A1 frontal)
      usando `docs/info_cortes_elevaciones.txt` + `docs/info_planos_dxf.txt`
      (+ `docs/resumen_edificio_actual.txt` como estado del modelo).
- [ ] **Oscar:** SAP2000 para verificación cruzada.
- [ ] Confirmar alcance y fecha/hora de entrega con el profesor.
- [ ] Probar Unity del anexo metálico I'→J.

---

## 🧩 Sesión 17 — Viernes 4 de septiembre 2026 (integración del voladizo trasero en el pipeline)

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo — `voladizos.py` integrado al análisis y al contrato Unity

1. **Inspección de `voladizos.py`** (nuevo): `agregar_voladizos_y_cubierta(dat, modelo, cfg, offset)`
   inyecta en OpenSees el voladizo trasero del eje A3 (y<0) de forma **aditiva**
   (REGLA DE ORO: no modifica el modelo base; tags por encima via
   `ops.getNodeTags()/getEleTags()`). Retorna `{voladizo_nodos, voladizo_vigas_vm,
   voladizo_postes_pm, tip_nodes, config_usada, nota}`. `proj_y=3.80 m` y
   `ejes_x=F,G,H,I2` son **tentativos** (config `CONFIG_VOLADIZO`).

2. **Puntos exactos de integración** (`src/benchmark_3d/analizar.py`):
   - Se localizó dónde se obtiene `modelo` tras `construir.construir()`
     (VERIF-1 y el loop de casos G/Q/GQ/EX/EY).
   - Nuevo helper `construir_con_voladizo(dat, offset)` = `construir.construir()`
     + `agregar_voladizos_y_cubierta(modelo=modelo, dat=dat, offset=offset)`
     + `integrar_voladizo(modelo, extra)` → se usa en TODOS los puntos.
   - `integrar_voladizo()` (nuevo en `voladizos.py`): fusiona la viguetería V.M.
     en `modelo["vigas_y"]` (con `x`/`y_i`/`y_j` añadidos a cada viga para
     longitudes/pesos) y los postes P.M. en `modelo["columns"]`; guarda la info
     en `modelo["voladizo"]`. Así **cargas, pesos por nivel, sismo y exportación
     conocen el voladizo** sin tocar `construir.py` ni `cargas.py`.

3. **Exportador Unity (`exportar_json`):** los nuevos nodos punta
   (`rol="punta_voladizo"`, tags 191–198) se agregan a `data["nodos"]` y al mapa
   `nivel_de` (para el cálculo de apoyos); los 8 V.M. salen como `tipo: vigas_y`
   y los 4 P.M. como `tipo: column` (formato existente, sin tipos nuevos).
   `results/modelo_resultados.json` regenerado.
   - ⚠️ Se detectó y corrigió un traspié: las claves del dict son
     `voladizo_nodos`/`voladizo_vigas_vm`/`voladizo_postes_pm` (singular
     "voladizo"), no "voladizos_*".

4. **`verificar_cargas.py`:** se inyecta el voladizo antes de recalcular
   `pesos_por_nivel` (idéntica lógica del análisis) → cross-check completo OK.

5. **Resultados nuevos (G35, α=0.10, con anexo + voladizo trasero):**

   | Caso | ΣFz react / V base | Techo | Deriva máx |
   |---|---|---|---|
   | G  | **44,771.5 kN** (+42 voladizo) | – | – |
   | Q  | 7,671.3 kN | – | – |
   | GQ | 52,442.7 kN | – | – |
   | EX | V = 4,477.1 kN | ux = 8.39 mm | 0.648 mrad (piso 3) |
   | EY | V = 4,477.1 kN | uy = 2.32 mm | 0.200 mrad (piso 4) |

   Conservación/equilibrio ≤ ~1e-10; superposición ≤ ~7e-12 kN.

6. **Tests:** 4 nuevos en `test_benchmark.py` (TestVoladizo: tags por encima del
   base, equilibrio G, auto-peso incluido, superposición con voladizo) + 2 en
   `test_json_contrato.py` (TestVoladizo: nodos punta en el JSON, vigas/postes
   exportados; TestColumnasEnMalla actualizado para eximir columnas de poste
   punta, que están legítimamente fuera de la retícula Y en y<0).
   **Suite completa: 49 passed** (~0.5 s). `.venv` propio con
   `requirements.txt` instalado (openseespy 3.8, pytest, matplotlib, ezdxf).

### Decisiones / notas

- Los postes P.M. se exportan como `tipo: column` (igual que las P.M. de la
  fachada J del anexo): Unity los dibuja como cajas; consistente con el modelo
  de "palitos" actual, pendiente de distinguir material/sección si se quiere.
- Unidad Unity NO requiere cambios: lee nodos/elementos nuevos con el mismo
  contrato. (Recompilar al darle foco.)
- `construir.py`, `cargas.py` y los visualizadores `visualizar.py` /
  `visualizar_html.py` (basados en la retícula) NO se tocaron: el voladizo es
  aditivo al pipeline de análisis/JSON.

### Pendientes (abren Sesión 18)

- [ ] Confirmar `proj_y` y `ejes_x` del voladizo trasero contra la elevación A3 /
      cortes acotados (hoy 3.80 m / F,G,H,I2 tentativos).
- [ ] **Oscar:** SAP2000 para verificación cruzada (sigue pendiente).
- [ ] Confirmar alcance y fecha/hora de entrega con el profesor.
- [ ] Probar en Unity el anexo + voladizo trasero (foco para recompilar).
- [ ] Decidir con el grupo el commit/push del avance (acumulado desde Sesión 9).

---

## 🧩 Sesión 17b — Viernes 4 septiembre 2026 (voladizo verificado contra DXF 102/103/303)

**Participantes:** Oscar Rodriguez + OpenCode. **Reemplaza la Sesión 17a.**

### Qué pasó

Oscar pasó a Claude los planos **2017_67-102, -103 y -303** y recibió un
`voladizos.py` nuevo, **verificado contra los DXF** (escala 1 cm/u). Se
reemplazaron las geometrías tentativas de la Sesión 17a (vuelo 3.80 m, ejes
F,G,H,I2, V.M. de acero) por la geometría real:

- **Vuelo = 4.30 m** a la punta (2027 cm de fondo − 1615 cm de retícula = 412 cm
  medidos desde el eje de la viga perimetral A3). Eje de punta y=−4.30.
- **Nervaduras SOLO en G (x=20) y H (x=30)**, A3→punta, en **HORMIGÓN V.60/80**
  (ancho 0.60). En E,F,I,I' bajo A3 solo vive la viga perimetral A3 (ya base).
- **Viga de borde de punta G↔H** (HORMIGÓN V.60/80) en los 2 niveles.
- **Postes de punta en G y H: P.M. 300×300×20** (acero), cajones RLA de la
  elevación A3 (plano 303), entre niveles 3 y 4.
- Cubierta "acartelada": NO se agrega geometría (el techo e=15 + V.60/80 ya
  existe en nivel 4; "VAR" es matiz de sección, no barras faltantes). Se
  mantiene el stub `agregar_cubierta_acartelada()`.

### Cambios de código

- `src/benchmark_3d/voladizos.py` → **reemplazado** por la versión DXF de
  Claude. El dict de retorno cambió: `nodos`, `nervaduras`, `vigas_borde`,
  `postes`, `tip_nodes`, `config_usada`, `resumen` (antes `voladizo_*`).
  Se le **añadió al final `integrar_voladizo(modelo, extra, dat, offset)`**
  (adaptador del pipeline) que fusiona nervaduras→`vigas_y` (con
  `x/y_i/y_j`), vigas_borde→`vigas_x` (con `y/x_i/x_j`) y postes→`columns`;
  los nodos quedan en `modelo["voladizo"]["nodos"]`. El resto del módulo es
  byte a byte el de Claude.
- `analizar.py`: `construir_con_voladizo` pasa `dat, offset` al adaptador;
  exportador usa la clave `nodos` del voladizo.
- `visualizar_html.py` / `visualizar.py`: el voladizo ahora se dibuja con sus
  materiales reales — **nervaduras y viga de borde en azul (hormigón)**, solo
  los **postes P.M. en naranja (acero)**; cámara ajustada a y=−4.30.
- `tests/test_benchmark.py`: `test_tags_por_encima_del_base` usa las claves
  nuevas (`nervaduras + vigas_borde + postes`).
- `verificar_cargas.py` y `test_json_contrato.py`: sin cambios de lógica
  (leyen el contrato JSON genérico).

### Resultados nuevos (G35, α=0.10)

| Caso | ΣFz react / V base | Techo | Deriva máx |
|---|---|---|---|
| G  | **45,189.9 kN** (+460 vs 17a) | – | – |
| Q  | 7,671.3 kN | – | – |
| GQ | 52,861.1 kN | – | – |
| EX | V = 4,519.0 kN | ux = 8.45 mm | 0.653 mrad (piso 3) |
| EY | V = 4,519.0 kN | uy = 2.34 mm | 0.202 mrad (piso 4) |

El diferencial del hormigón es coherente: 4 nervaduras + 2 vigas borde
V.60/80 (≈12 kN/m) + postes P.M. → ≈+460 kN. Conservación/equilibrio ≤
~7e-11; superposición ≤ 5e-12. **Suite: 49 passed.** JSON regenerado con
194 nodos / 329 elementos (voladizo: 4 punta + 4 nervaduras + 2 borde + 2
postes). HTML/PNG regenerados y abiertos.

### Pendientes (abren Sesión 18)

- [ ] **Marco metálico secundario P.M./V.M. sobre la punta** (aparece en la
      elevación 303): NO modelado — falta corte acotado por ejes E..I.
- [ ] Confirmar el corte acotado que fije ese marco (y no inventar entramado).
- [ ] **Oscar:** SAP2000 para verificación cruzada (sigue pendiente).
- [ ] Probar en Unity anexo + voladizo (recompilar para verlo).
- [ ] Decidir commit/push del avance (acumulado desde Sesión 9).

---

## 📅 Sesión 18 — Domingo 6 de septiembre 2026 (anclaje aspas + extensión de piso 1)

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo

1. **Claude generó un `voladizos (1).py` nuevo** (6/9/2026, `Desktop\Downloads\`) que
   añade dos estructuras al voladizo trasero A3: el **arriostramiento (aspa)**
   `agregar_arriostramiento_voladizo()` (diagonales V.M. 300x300x5 entre la punta del
   nivel 3 y A3 del nivel 4, en ejes G/H, verificado en cortes 307/308) y la
   **extensión de hormigón trasera en piso 1** `agregar_extension_piso1()` (grilla de
   columnas P.70x70 en X=30/34.5/40.5 e Y=−2.9/−6.5/−10.2, vigas V.60/80 + amarre a
   nodos A3, planta 101 y cortes G/H/I). Ese archivo NO trae el adaptador
   `integrar_voladizo` del pipeline.
2. **Anclaje hecho en el proyecto** (en vez de pasarlo de vuelta a Claude): se integró
   el código nuevo en `src/benchmark_3d/voladizos.py` y se extendió `integrar_voladizo`
   para fusionar aspas y extensión respetando el contrato de `cargas.py` (posible
   gracias a que el adaptador ya existía como plantilla, Sesión 17b).

### Cambios de código

- `src/benchmark_3d/voladizos.py`: incorpora las funciones de Claude
  `agregar_arriostramiento_voladizo(dat, modelo, extra)` y
  `agregar_extension_piso1(dat, modelo, cfg, offset)`, **enriquecidas** para cumplir el
  contrato del pipeline: nodos con `lvl`/`rol` (`punta_voladizo`, `base_ext`,
  `columna_ext`); columnas con `story` (0 para la base); vigas con coords
  (x/x_i/x_j o x/y_i/y_j) + `A_pp`/`rho_pp`/`material`; aspas con `story`/`A_pp`/
  `rho_pp`/`material` (L se calcula al integrar vía `_longitud_3d`). Se corrigió el
  tramo Y de la extensión para que `y_j > y_i` (longitud no negativa); el amarre a
  A3 (diagonal fino, e.g. 34.5→H-A3) lleva `L` explícita (~2.9/5.35/2.94 m).
- `integrar_voladizo` ahora fusiona: nervaduras→`vigas_y`, vigas_borde→`vigas_x`,
  postes→`columns`, **aspas→`modelo["aspas"]`** (nueva categoría), extensión→columns
  + vigas (por `dir`, incluye "Y-amarre") + nodos. Retrocompatible si `extra` no trae
  aspas/extensión.
- `cargas.py`: `cargas_gravedad` y `pesos_por_nivel` suman auto-peso de aspas
  (`ρ·A·L` en el nodo `nj` / `w[story+1]`). `distribuir_nivel` acepta `L` explícita
  en vigas (para el amarre diagonal).
- `analizar.py`: `construir_con_voladizo` construye base + voladizo + aspas +
  extensión (`completo = dict(extra); completo["aspas"]=...; completo["extension"]=...`);
  exportador JSON emite `tipo:"aspa"`; `_exportar_cargas` suma aspas al
  `peso_columnas_muros`.
- `verificar_cargas.py`: replica el pipeline completo (aspas + extensión).
- `unity/Scripts/UnityStickModel.cs`: se añade `"aspa"` al arreglo de tipos
  (sin esto, `KeyNotFoundException`), creador `CrearAspa3D` (sección 0.30, material
  acero `MaterialAspa`) reutilizando `PosicionarElemento3D` (orientación genérica).
- `visualizar_html.py`: el visualizador interactivo ahora dibuja las **aspas**
  (líneas diagonales naranjas punta-nivel3 → A3-nivel4, ejes G/H) y la **extensión
  de hormigón del piso 1** (columnas gris P.70 + vigas azules V.60/80 + amarre a
  A3, desde `CONFIG_EXT`). Regenerado `results/modelo_3d.html`.
- Tests: `test_json_contrato.py` (`TIPOS_VALIDOS` + "aspa"; `TestColumnasEnMalla`
  generalizado para roles del voladizo/extensión; nuevos `TestAspasExtension`);
  `test_benchmark.py` (nuevo `TestArriostramientoExtension`: 2 aspas, 9 columnas de
  extensión, equilibrio G, pesos > voladizo solo, invariante ΣW_i == G aplicada).

### Resultados nuevos (extensión + aspas + voladizo, α=0.10)

| Caso | ΣFz react / V base | Techo | Deriva máx |
|---|---|---|---|
| G  | **46,429.1 kN** (+1,239 vs 17b) | – | – |
| Q  | 7,671.3 kN (sin cambio: no hay losa nueva en paneles) | – | – |
| GQ | 54,100.3 kN | – | – |
| EX | V = 4,642.9 kN | ux = 8.61 mm | 0.667 mrad (piso 3) |
| EY | V = 4,642.9 kN | uy = 2.39 mm | 0.207 mrad (piso 4) |

Composición del delta G (+1,699.6 vs base 44,729.5): voladizo +460.3 (17b), aspas
+5.4 (78.5·A_vm·L, 2 diagonales), extensión +1,233.8 (columnas 458.6 + vigas
X 378.0 + Y 262.8 + amarre 134.4). Conservación tributaria 3.6e-12; equilibrio por
caso ≤ 2.2e-11; superposición ≤ 8.9e-12. **Suite: 58 passed.** JSON regenerado
(`results/modelo_resultados.json`, 233 nodos / 393 elementos: +2 aspas, +27
extension, +... voladizo) y `verificar_cargas.py` → VERIFICACION COMPLETA (OK).

### Pendientes (abren Sesión 19)

- [ ] Probar en Unity anexo + voladizo + aspas + extensión (recompilar para verlo).
- [ ] **Marco metálico secundario P.M./V.M. sobre la punta** (elevación 303): sigue
      sin modelar — falta el corte acotado por ejes E..I que lo fije.
- [ ] **Oscar:** SAP2000 para verificación cruzada (sigue pendiente).
- [ ] Decidir commit/push del avance (acumulado desde Sesión 9).

---

## 📅 Sesión 19 — Domingo 6 de septiembre 2026 (nuevo `voladizos.py` de Claude: voladizo metálico de piso 1 en eje F)

**Participantes:** Oscar Rodriguez + agente OpenCode.

### Qué se hizo

Claude generó un **nuevo `voladizos.py`** (`Desktop\Downloads\voladizos.py`) que añade
el **voladizo metálico arriostrado de piso 1 en el eje F** (`agregar_voladizo_piso1_ejeF`):
cordones V.M. 300x300x5 en piso 1 (z=−0.05) y piso 2 (z=3.91), poste P.M. 300x300x20 en
la punta (Y=−4.30 m) y diagonal (aspa) V.M. del pie a la esquina sobre A3; verificado en
corte 306 y planta 101.

**Atención:** ese archivo NO trae `integrar_voladizo`, `agregar_extension_piso1`,
`_longitud_3d` ni el campo `story` en las aspas → reemplazarlo tal cual **rompía**
`analizar.py`, `verificar_cargas.py`, `cargas.py` y los tests. Se hizo un **merge**:
se adoptó la versión nueva (con el voladizo de piso 1) y se restauraron las piezas que
el pipeline necesita, dejando 36/36 tests en verde.

### Cambios de código

- `src/benchmark_3d/voladizos.py` → reemplazado por la versión de Claude de
  `Downloads`, fusionada con el adaptador del pipeline:
  - **Nuevo:** `CONFIG_VOL_F` + `agregar_voladizo_piso1_ejeF(dat, modelo, cfg, offset)`
    (2 nodos de punta + 2 cordones + 1 poste + 1 diagonal, en eje F).
  - **Conservadas del pipeline:** `integrar_voladizo`, `agregar_extension_piso1`
    (Estructura B), `_longitud_3d` e `import math`.
  - **Corregido:** las aspas de `agregar_arriostramiento_voladizo` vuelven a llevar
    `"story": lo` (lo requiere `cargas.py::pesos_por_nivel` y
    `test_dos_aspas_con_longitud_y_geometria`); la versión nueva lo omitía.
- El voladizo de piso 1 queda disponible como generador aditivo; NO se integró al
  análisis por defecto (`construir_con_voladizo` no lo invoca) ni a los diafragmas.
- `visualizar_html.py`: el visualizador 3D ahora dibuja el **voladizo de piso 1 del
  eje F** (CONFIG_VOL_F): cordones A3→punta en piso 1 y 2, poste metálico en la
  punta y diagonal (aspa), todo en naranja (acero). Leyenda y docstring
  actualizados. Regenerado `results/modelo_3d.html`.

### Verificación

- `pytest tests/test_benchmark.py -q` → **36 passed**.
- Smoke test del nuevo generador: `agregar_voladizo_piso1_ejeF` crea 2 nodos,
  2 cordones (V.M.), 1 poste (P.M.) y 1 diagonal, sobre el modelo base sin tocar
  tags existentes.

### Pendientes (abren Sesión 20)

- [ ] Decidir si `agregar_voladizo_piso1_ejeF` entra en el análisis por defecto
      (pesos/sismo/exportación Unity) y si el pipeline lo debe fusionar.
- [ ] Probar en Unity anexo + voladizo + aspas + extensión (recompilar para verlo).
- [ ] Marco metálico secundario P.M./V.M. sobre la punta (elevación 303) — sigue sin
      modelar (falta el corte acotado por ejes E..I).
- [ ] **Oscar:** SAP2000 para verificación cruzada (sigue pendiente).
- [ ] Decidir commit/push del avance (acumulado desde Sesión 9).

---

## 🧩 Sesión 20 — Domingo 6 de septiembre 2026 (voladizo de piso 1 integrado; se elimina la extensión)

**Decisión adoptada:** el modelo analizado debe coincidir con las estructuras verificadas
contra los planos y deja reflejar los dos cambios de la Sesión 19:

1. **Cambio 1 (aceptado):** el voladizo metálico de piso 1 en el eje F ahora es **parte del
   análisis por defecto** (pesos, sismo y exportación Unity), igual que el voladizo trasero
   y sus aspas.
2. **Cambio 2 (aceptado):** se **elimina la extensión de hormigón trasera** del modelo. El
   plano 101 la rotula "RADIER SOBRE TERRENO" con "DILATACIÓN 1CM": es una losa apoyada en
   el suelo, separada estructuralmente, y no pertenece al modelo del edificio.

### Cambios de código

- `src/benchmark_3d/voladizos.py`:
  - Eliminados `CONFIG_EXT` y `agregar_extension_piso1` (bloque "Estructura B" completo).
  - `agregar_voladizo_piso1_ejeF`: enriquecido — nodos de punta ahora llevan
    `lvl` y `rol="punta_vol_f"`; los cordones llevan `nivel` (1 y 2); poste y diagonal
    llevan `story=1`; retorna `config_usada`. Docstring y comentarios actualizados.
  - `integrar_voladizo`: nueva rama para `extra["vol_f"]` —
    `cordones` → `modelo["vigas_y"]` (con `x`/`y_i`/`y_j` para `cargas.py` y el JSON),
    `poste` → `modelo["columns"]`, `diagonal` → `modelo["aspas"]` (con `L = _longitud_3d`),
    `nodos` → `modelo["voladizo"]["nodos"]`. Se eliminó la rama de la extensión.
  - El adaptador documentado en el encabezado refleja el nuevo mapeo (`vol_f.*`).
- `src/benchmark_3d/analizar.py`: `construir_con_voladizo` ahora llama a
  `agregar_voladizo_piso1_ejeF`, setea `completo["vol_f"]` e integra; sin rastro de la
  extensión.
- `src/benchmark_3d/verificar_cargas.py`: el bloque de construcción del modelo usa el mismo
  pipeline (extra + extra_er + extra_f → `completo["vol_f"]`), sin extensión.
- `src/benchmark_3d/visualizar_html.py`: eliminado el dibujo de la extensión
  (bloque `cfg_e`/`ext_z1`/amarre a A3); el dúo trasero del eje F sigue en naranja.
- **No se modificaron** `construir.py` ni `datos_edificio.py` (regla aditiva preservada).

### Tests (reconvertidos)

- `tests/test_benchmark.py`: `TestArriostramientoExtension` → `TestAspasYVoladizoF`.
  `test_extension_9_columnas_base_empotrada` → `test_voladizo_ejeF_2_nodos_y_4_elementos`
  (verifica los 2 nodos en y=−4.30 y z −0.05/3.91, los 4 elementos y tags por encima del
  base) + `test_sin_roles_ni_geometria_de_extension` + `test_modelo_base_intacto_190_321`.
  `test_pesos_por_nivel_suman_gravedad_total` ahora espera **3 aspas** (2 traseras + diagonal F).
- `tests/test_json_contrato.py`: `TestColumnasEnMalla` incluye el rol `punta_vol_f` en el
  set "fuera"; `TestAspasExtension` → `TestAspasYVoladizoF` (aspas, nodos, poste/cordones
  del eje F, y `test_no_existe_extension` con roles `base_ext`/`columna_ext` ausentes).

### Verificación

- Base intacta antes de inyectar: **190 nodos / 321 elementos** (verificado por
  `getNodeTags`/`getEleTags`).
- Modelo final (base + voladizo trasero 4 nodos/8 el. + 2 aspas + voladizo eje F
  2 nodos/4 el.): **196 nodos / 335 elementos**.
- Como el diámetro de la losa es conservado, la carga G total sigue en
  **45208.93 kN** y el sismo en **4520.89 kN** por dirección (sin aporte tributario
  adicional del eje F: sus cordones están fuera de los paneles de losa).
- `python analizar.py`: equilibrio por caso en 1e-11 a 1e-12 y superposición
  G+Q=GQ con máx. 4e-12 — OK. `verificar_cargas.py`: verificación completa OK (A1/A2/
  A3/A4/B1/C1 por X e Y).
- `pytest tests/ -q` → **60 passed**. Regenerados `results/modelo_resultados.json` y
  `results/modelo_3d.html` (el visualizador ya no dibuja la extensión).

### Pendientes (abren Sesión 21)

- [ ] Recompilar/reprobar en Unity: anexo + voladizo trasero + aspas + voladizo eje F
      (sin extensión).
- [ ] Marco metálico secundario P.M./V.M. sobre la punta (elevación 303) — sigue sin
      modelar (falta el corte acotado por ejes E..I).
- [ ] **Oscar:** SAP2000 para verificación cruzada (sigue pendiente).
- [ ] Decidir commit/push del avance (acumulado desde Sesión 9).

---

## 🧩 Sesión 21 — Domingo 6 de septiembre 2026 (CARPETA "CORREGIDO" = VERSIÓN OFICIAL: voladizo de piso 1 como RECTÁNGULO)

**CAMBIO DE VERSIÓN CANÓNICA ⭐:** este proyecto
(`Desktop\Proyecto_1_MCOC_-_Edificio_A_corregido\Proyecto 1 MCOC - Edificio A (OFICIAL)\`)
es la versión **correcta y verificada** para trabajar. Reemplaza la Sesión 20 simplificada.

### Por qué se corrige (vs. Sesión 20)

La Sesión 20 integró el voladizo metálico de piso 1 en el eje F con la geometría mínima
(2 cordones A3→punta en x=10, 1 poste y 1 diagonal). Al re-verificar **planta 101** y el
**corte por eje F (306)** se confirmó que el voladizo es un **RECTÁNGULO completo** tras A3:
**dos nervaduras** (eje F x=10 y **borde x=17.50**) unidas por vigas de punta, vigas de raíz
sobre A3, dos postes y dos diagonales. La descripción de la Sesión 20 queda **superada** por
esta Sesión 21 para toda la geometría del voladizo de piso 1.

### Cambios de código (3 archivos respecto de la Sesión 20)

- `src/benchmark_3d/voladizos.py`:
  - `CONFIG_VOL_F` ahora incluye `"x_borde": 17.50` (nervadura exterior, no es eje de retícula).
  - `agregar_voladizo_piso1_ejeF` reescrito como **rectángulo en planta**: devuelve `nodos`,
    `vigas_y` (4 nervaduras F y borde × 2 niveles), `vigas_x` (4 vigas de punta + de raíz × 2),
    `poste` (2, uno en cada punta) y `diagonal` (2, una por nervadura). **6 nodos nuevos**
    (4 `punta_vol_f` + 2 `raiz_vol_f`) y **12 elementos**. La nervadura de borde se ancla al
    edificio por la viga de raíz sobre A3 hasta el nodo del eje F (sin partir barras del base).
  - `integrar_voladizo` simplificado para esta rama: `vol_f.vigas_y`/`vigas_x` ya traen
    coordenadas y se fusionan directo (`vigas_y`/`vigas_x`); `vol_f.poste` → `columns`;
    `vol_f.diagonal` → `aspas` (con `L = _longitud_3d`); `vol_f.nodos` → `voladizo.nodos`.
    Las aspas del modelo completo pasan de 3 a **4** (2 traseras + 2 diagonales del eje F).
- `tests/test_benchmark.py`: `test_voladizo_ejeF_2_nodos_y_4_elementos` →
  `test_voladizo_ejeF_rectangulo` (6 nodos, 4 `vigas_y`, 4 `vigas_x`, 2 postes, 2 diagonales,
  12 elementos, puntas en (10,−4.30) y (17.5,−4.30) en z −0.05/3.91, roles `punta_vol_f`×4 /
  `raiz_vol_f`×2). `test_pesos_por_nivel_suman_gravedad_total`: aspas **4**.
- `tests/test_json_contrato.py`: `test_aspas_presentes` → **4** aspas; `test_punta_vol_f_en_nodos`
  → **4** puntas (incluye x=17.5); `test_poste_y_cordones_del_eje_F` → **2** postes y **4**
  nervaduras (`vigas_y`). Se agrega el rol `raiz_vol_f`.
- **No se modificaron** `construir.py`, `datos_edificio.py`, `analizar.py`, `verificar_cargas.py`
  ni `visualizar_html.py`: el pipeline sigue igual (extra + extra_er + extra_f → `completo["vol_f"]`),
  la regla aditiva se respeta intacta.

### Resultados numéricos (JSON regenerado 6/9/2026 17:28)

| Magnitud | Sesión 20 (simplificado) | Sesión 21 (rectángulo) |
|---|---|---|
| Nodos totales | 196 | **200** |
| Elementos totales | 335 | **343** (vigas_x 116 · vigas_y 112 · column 79 · wall 32 · aspa 4) |
| Aspas | 3 | **4** |
| ΣFz caso G (kN) | 45,208.93 | **45,236.48** (+27.55, peso neto del rectángulo extra) |
| ΣFz caso Q (kN) | 7,671.25 | 7,671.25 (sin área tributaria nueva) |
| ΣFz caso GQ (kN) | 52,880.18 | **52,907.73** |
| V sísmico EX/EY (kN) | 4,520.89 | **4,523.65** (α=0.10) |
| Momento volcante (kN·m) | – | **61,009.10** |
| Apoyos | – | 23 |

### Verificación

- `pytest tests/ -q` → **60 passed** (1.03 s) con el `.venv` del proyecto.
- Equilibrio por caso OK (reacciones ≈ aplicadas en 1e-10..1e-12) en los casos G/Q/GQ/EX/EY.
- Base intacta antes de inyectar: **190 nodos / 321 elementos**.
- Las diagonales del eje F y del borde no suman área tributaria de losa (fuera de los paneles),
  de ahí que Q no cambie; el aumento de G es solo peso propio del acero adicional.

### Pendientes (abren Sesión 22)

- [ ] `visualizar_html.py`: dibujar también la **nervadura de borde x=17.5** (hoy solo dibuja
      el cordón x=10) y regenerar `results/modelo_3d.html`; lo mismo para `visualizar.py` si aplica.
- [ ] Recompilar/reprobar en Unity: anexo + voladizo trasero + aspas + voladizo rectángulo eje F.
- [ ] Marco metálico secundario P.M./V.M. sobre la punta (elevación 303) — sigue sin modelar
      (falta el corte acotado por ejes E..I).
- [ ] **Oscar:** SAP2000 para verificación cruzada.
- [ ] Decidir commit/push del avance (acumulado desde Sesión 9).

---

## 🔧 Referencias rápidas

- Repo: https://github.com/OscarRodriguez17/Proyecto1-MCOC
- Proyecto activo (VERSIÓN OFICIAL): `Desktop\Proyecto_1_MCOC_-_Edificio_A_corregido\Proyecto 1 MCOC - Edificio A (OFICIAL)\`
- Proyecto anterior (referencia histórica, no editar): `Desktop\Proyecto 1 MCOC - Edificio A (HISTORICO)\`
- Proyecto histórico (dos bloques): `Desktop\Proyecto 1 MCOC\`
- Enunciados: `Desktop\Proyecto 1 Enunciados\`
- Planos: `Desktop\planos_edificio_ing\` (DWG, serie 2017_67-*)
- Doc OpenSeesPy: https://openseespydoc.readthedocs.io/
- Plantilla de código probada 2D (histórico): `src/desafio_clase.py`
- gh CLI: `"C:\Program Files\GitHub CLI\gh.exe"`
