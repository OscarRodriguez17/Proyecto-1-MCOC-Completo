# Reporte — Semana 04: Unity como postprocesador estructural conectado a resultados OpenSees verificados (fuerzas completas por elementTag + metadatos + diagramas 3D + ventanas de diagramas y P–M)

**Proyecto:** Laboratorio estructural digital 3D — Complejo de Ingeniería
(Edificios A y B).
**Edificios:** A (3D, G35) y B (3D, H30, planos 2024_22).
**Fecha:** Viernes 18 de septiembre de 2026 — Sesiones 12–14.
**Alcance:** capa **ADITIVA**. No se tocan los modelos lineales A/B, sus IDs,
geometría, casos G/Q/GQ/EX/EY, sismo, tributario ni esfuerzos; todo lo nuevo es
**postproceso** sobre resultados verificados **+ presentación en Unity**.
**Sin cambios de código en esta entrega:** solo `reports/semana04.md` (documento).
Suite protegida: **134 passed**.

---

## 1. Objetivo

Convertir al visor Unity (`EdificioSolidoUnity`) en un **postprocesador
estructural** del modelo OpenSees, de modo que cualquier elemento estructural
(viga / columna / muro / aspa) pueda consultarse con datos verificados, no
aproximados:

1. **Fuerzas completas por elementTag** para **los 9 coeficientes** por caso
   (`esfuerzos_completos` / `esfuerzos_completos_A`): `{L, N, Vy, Vz, T, My,
   Mz, Wy, Wz}`, con la misma convención de la semana 03 (`N > 0` = compresión,
   `M(x) = M + V·x + W·x²/2`, `Wz = −w` solo en vigas; `Wy = Wz = 0` en
   columnas / muros). Cobertura: **B 315** y **A 343** elementos por caso.
2. **Metadatos por tag** (`metadatos` / `metadatos_A`): tipo de visor
   (`column`/`wall`/`vigas_x`/`vigas_y`), sección de catálogo, material
   (H30/G35), longitud, nodos `ni/nj`, restricción de cada extremo y **ejes
   locales** {x̂=(nj−ni)/L, ẑ = vertical ⊥ x̂, ŷ = ẑ×x̂} idénticos a los `vecxz`
   de OpenSees.
3. **Selección por clic** de columna, viga **y muro** → panel unificado con ID,
   nodos, sección, material, restricciones, ejes locales y los esfuerzos del
   caso activo (G/Q/GQ/EX/EY), evaluados en cualquier posición `x` del slider.
4. **Visualización por capas** (deformada por caso, diagramas 3D N/My/Mz,
   ventana de diagramas 2D, áreas tributarias, cargas G/Q/sismo, apoyos).
5. **Demanda–capacidad P–M** por elemento: envolvente del catálogo de semana 03
   + punto balanceado + demanda del caso activo, con `M_cap(P_d)` y `D/C`.
6. **Trazabilidad completa** elementTag → JSON → GameObject/collider →
   panel/ventana → esfuerzos → sección/capacidad P–M (sección 5 de este
   reporte, el eslabón clave de la rúbrica).

No se reanaliza nada: el runner reutiliza el caché de las 10 corridas y el
overlay es aditivo e idempotente.

## 2. Selección de elementos — el panel unificado (Semana 04)

Al activar **"Consulta de elemento (click izquierdo)"** y hacer clic en
cualquier columna o viga — o en el **panel de un muro** (ahora con
`BoxCollider` fijo, sin deformar), el visor resuelve el `elementTag` del tramo
y abre **un solo panel** (`DibujarPanelConsulta`) que muestra:

- Bloque (Edificio A/B), **tag** (elementTag OpenSees), tipo de visor y material.
- **Restricción i→j** (`empotrado` / `maestro_diafragma` / `esclavo_diafragma` /
  `libre`) y nivel del nodo i.
- Selector ◀/▶ de tag (necesario para los muros, cuyo panel abarca todos los
  niveles y el clic elige el tramo por cota `y`).
- **Caso activo** G/Q/GQ/EX/EY y slider `x` (0–100 % de la luz).
- **N(x) / Vz(x) / Vy(x) / My(x) / Mz(x) / T** evaluados con los coeficientes
  del JSON en esa `x`.
- Toggle **"Diagramas 3D"** (N verde, My azul, Mz rojo) y aviso de la ventana
  **P–M** (amarilla, arrastrable).

> Convención de signos (importante para explicar): el JSON y la demanda P–M
> usan **compresión positiva** (`esfuerzos_completos.GQ."41".N = +2175,21 kN`).
> El campo `N(x)` del panel evalúa `N(x) = −esf.N` (tracción positiva, como la
> semana 03), así que un muro comprimido muestra **−2175,2 kN** en el panel y
> **+2175 kN** como `P_d` en la ventana P–M. Se trata del mismo dato.

### 2.1 Ejemplos reales del JSON (un elemento por tipo, caso activo GQ)

Valores **crudos de los coeficientes** extraídos de `results/edificio_solido.json`
→ `esfuerzos_completos` y `metadatos` (coincidentes con
`results/secciones_semana04.json`). Los coeficientes corresponden al extremo
`i` (x = 0); el panel **evalúa las fórmulas en la posición `x` del slider**
(`N(x) = −N`, `V(x) = V + W·x`, `M(x) = M + V·x + W·x²/2`), y arranca en
**x = 50 %** por defecto (`casoConsulta = 2` → GQ, `posConsulta = 0,5`):

| Campo (metadatos) | Viga — **B tag 106** | Columna — **B tag 1** | Muro — **B tag 41** |
|---|---|---|---|
| Tipo de visor | `vigas_y` | `column` | `wall` |
| Nodos `ni → nj` | 20 → 127 | 1 → 2 | 49 → 50 |
| Nivel (nodo i) | 1 | 0 (base) | 0 (base) |
| Sección de catálogo | `viga0.60x0.80` | `col0.70x0.70` | `muro0.60x2.91` |
| Material | H30 | H30 | H30 |
| Longitud `L` [m] | 4,63 | 3,96 | 3,96 |
| Restricción i / j | esclavo_diafragma / esclavo_diafragma | **empotrado** / esclavo_diafragma | **empotrado** / esclavo_diafragma |
| Ejes locales {x̂, ŷ, ẑ} SI | (0,1,0)·(−1,0,0)·(0,0,1) | (0,0,1)·(0,−1,0)·(1,0,0) | (0,0,1)·(0,−1,0)·(1,0,0) |

| Caso activo **GQ** — coeficientes (x = 0) | Viga tag 106 | Columna tag 1 | Muro tag 41 |
|---|---|---|---|
| `N` [kN] | 0,0 | **10 268,90** (compresión) | **2 175,21** (compresión) |
| `Vy` [kN] | ≈ 0 | −24,62 | 189,39 |
| `Vz` [kN] | 877,36 | −352,58 | 20,80 |
| `T` [kN·m] | 1 110,93 | 2,47 | 13,30 |
| `My` [kN·m] | −3 376,06 | 443,42 | −158,14 |
| `Mz` [kN·m] | ≈ 0 | 7,20 | **3 909,15** |
| `Wz` [kN/m] | −29,5535 (carga grav.) | 0 (elemento vertical) | 0 (elemento vertical) |

Notas de lectura:

- En el slider por defecto (x = 50 %), el muro tag 41 muestra `N = −2175,2 kN`
  (constante) y `Mz = 3909,15 + 189,39·1,98 ≈ 4284,2 kN·m`; en x = 0 y x = L el
  mismo `Mz` es 3909,2 y 4659,2 kN·m respectivamente. Los valores de la tabla
  son los coeficientes crudos (x = 0), que son los que se verifican y trazan en
  §5.
- El muro tag 41 es el tramo base del muro en planta `(x, y) = (11,20; 11,295)`
  entre nodos 49 (nivel 0, `z = −4,01`) y 50 (nivel 1, `z = −0,05`): de ahí
  `L = 3,96` m y `cx = 11,20`, `cy = 11,295` en `secciones.elementos."41"`.
- La viga tag 106 va del nodo 20 (14,85; 18,18) al 127 (14,85; 22,81): es una
  viga en Y cargada; su `My` es **parabólico** entre −3 376,1 kN·m (extremo i) y
  +369,3 kN·m (extremo j) por el término `Wz·x²/2`, y su `Vz` constante 877,36.
- La columna tag 1 (base, empotrada) lleva gran compresión
  (10 269 kN bajo GQ) con flexión pequeña → domina el modo N, aún lejos del
  balanceado (ver §4).

[CAPTURA: Play — visor con muro B tag 41 seleccionado por clic: panel unificado
mostrando tag, nodos 49→50, sección muro0.60x2.91, restricción
empotrado/esclavo, caso GQ (por defecto 50 % del tramo): N = −2175,2 kN,
Mz ≈ 4284,2 kN·m.]
[CAPTURA: Play — misma vista para la columna B tag 1 (N = −10268,9 kN bajo GQ)
y para la viga B tag 106 (panel con slider x y valores N/Vz/My/Mz/T).]

## 3. Visualización (capas)

Todas las capas conviven en la misma escena y se alternan con los toggles del
panel superior (OnGUI). Una línea por capa:

| Capa | Toggle / control | Qué muestra | Datos fuente |
|---|---|---|---|
| **Deformada por caso** | `Base / G / GQ / EX / EY` + slider `Amp` (0–2000) | Reconstruye el mesh de columnas/vigas/muros con el desplazamiento del caso (`ReconstruirPanelesDeformados`) | `resultados` (desplazamientos por nodo) |
| **Diagramas 3D** | "Diagramas 3D" del panel de consulta | Curvas **N (verde), My (azul), Mz (roja)** superpuestas a la pieza, auto-escala a ~15 % de la luz (`ReconstruirDiagramas3D` + `CrearLinea3D`) | `esfuerzos_completos` + `metadatos.ejes_locales` |
| **Ventana de diagramas 2D** | "Ventana de diagramas (arrastrable)" | GUI.Window con **Axial (N) / Corte (Vz·Vy) / Momento (My·Mz)**, extremos i/j, máximo con su posición y marcador del slider (`DibujarVentanaDiagramas` + `GenerarTexDiagrama`) | 9 coeficientes del caso activo |
| **Áreas tributarias** | `Tributaria 45` | Polígonos de influencia (45°) por viga: X en naranja, Y en azul (`CrearTributaria…`) | `cargas.q_losa` + geometría; figura estática en `reports/fig/semana03_mosaico.png` |
| **Cargas G** | `Cargas G` | Flechas/regiones de carga permanente (color gris `ColorCargaG`) | `cargas` |
| **Cargas Q** | `Cargas Q` | Carga viva distribuida (naranja `ColorCargaQ`) | `cargas` |
| **Cargas sismo** | `Cargas sismo` | Flechas `F_i` por nivel bajo EX/EY (rojo `ColorCargaSismo`) | `cargas.sismo.F_por_nivel` |
| **Apoyos** | `Apoyos` | Zapatas (cubos) sobre los apoyos empotrados de la base (`CrearZapata` + `bv.apoyos`) | `apoyos` / nodos con `rol` base |
| **Estructura** | Columnas / Vigas X / Vigas Y / Muros / Brazos rígidos / Terreno / Nodos | Visibilidad por familia de elemento | `elementos[]` del contrato |

La deformada por caso comparte el selector con el caso activo de la consulta,
así se puede superponer la deformada EX con los diagramas del mismo caso.

[CAPTURA: Play — deformada GQ amplificada con toggle "Deformada por caso = GQ".]
[CAPTURA: Play — diagramas 3D N/My/Mz sobre el muro tag 41 (verde/azul/rojo).]
[CAPTURA: Play — ventana de diagramas (arrastrable) con Momento → Mz del muro
tag 41 bajo GQ: i = 3909,2 → j = 4659,2 kN·m.]
[CAPTURA: Play — capa "Tributaria 45" (polígonos naranja/azul por viga); la
figura estática semana03_mosaico.png sirve de referencia.]
[CAPTURA: Play — capas "Cargas G" (gris), "Cargas Q" (naranja) y "Cargas
sismo" (rojo) con flechas EX/EY por nivel.]
[CAPTURA: Play — capa "Apoyos" (zapatas empotradas en base).]

## 4. Demanda–capacidad: ventana P–M de columna y muro

El visor **no duplica** el motor de secciones: reutiliza el catálogo P–M de la
semana 03 (bloque `secciones` del contrato) y resuelve la curva de cada elemento
con `SeccionDesdeTag` (match exacto → claves representativas `col`/`muro` →
primera clave por prefijo; el overlay agrega las claves reales como aliases,
p. ej. `col0.70x0.70` → `col` en el Edificio B). La ventana **"Diagrama P–M —
Semana 04"** (`DibujarVentanaPM` + `GenerarTexPM`) dibuja:

- **Envolvente** del catálogo (celeste, correspondiente a 17 puntos por malla
  12×12 de fibras, f'c H30/G35 según edificio y fy 420 MPa, semana 03);
- **punto balanceado** (verde);
- **las demandas de los 5 casos** (gris el inactivo, **rojo la del caso
  activo**), con `P_d`, `M_d`, **`M_cap(P_d)`** interpolado sobre la envolvente
  (`CapacidadM`) y **`D/C = M_d / M_cap(P_d)`**.

Valores reales para columna y muro (del bloque `secciones.elementos`):

| Elemento (B) | Caso | Sección | P_d [kN] | M_d [kN·m] | M_cap(P_d) [kN·m] | **D/C** |
|---|---|---|---|---|---|---|
| tag 1 (columna) | GQ | `col0.70x0.70` | 10 268,9 | 443,5 | 865,8 | **0,51** |
| tag 2 (columna) | GQ | `col0.70x0.70` | 8 145,9 | 1 278,4 | 1 223,5 | **1,05 (excede)** |
| tag 41 (muro) | GQ | `muro0.60x2.91` | 2 175,2 | 3 912,3 | 5 270,9 | **0,74** |
| tag 41 (muro) | EY | `muro0.60x2.91` | 1 973,5 | 9 022,8 | 5 066,6 | **1,78 (excede)** |

Referencias de la curva mostrada en la ventana:

- `col`: P0 fibras = 14 097 kN, balanceado (4 369,5 kN; 1 157,9 kN·m).
- `muro0.60x2.91`: P0 fibras = 45 101 kN, balanceado (16 406,8 kN;
  8 705,5 kN·m); es el **muro crítico del Edificio B bajo EY**
  (D/C = 1,78, coherente con la semana 03: "muro crítico muro0.60x2.91 bajo EY,
  (1 974 kN; 9 023 kN·m)").

[CAPTURA: Play — ventana P–M del muro tag 41 con caso GQ: envolvente celeste,
balanceado verde, demanda roja (2 175; 3 912) dentro de la envolvente (D/C
0,74).]
[CAPTURA: Play — ventana P–M del mismo muro con caso EY: demanda
(1 974; 9 023) por fuera de la envolvente (D/C 1,78, "excede").]
[CAPTURA: Play — ventana P–M de la columna tag 1 (o tag 2) con GQ; en tag 2 la
demanda roja queda apenas sobre la envolvente (D/C 1,05).]

## 5. Trazabilidad — la cadena completa (clave para la rúbrica)

Este es el eslabón de 2 pts de la rúbrica: se puede recorrer **de punta a punta**
y explicarlo en voz alta. La cadena es la siguiente:

```
elementTag OpenSees (modelo B/A)
  → clave del JSON  (esfuerzos_completos[caso][tag] · metadatos[tag]
                     · secciones.elementos[tag])
  → GameObject/collider en Unity  (consultaPorTag[tag] / consultasPorObjeto[GO])
  → panel / ventanas  (DibujarPanelConsulta · Diagramas 3D · Ventana P–M)
  → esfuerzos del caso activo  (N, Vy, Vz, T, My, Mz evaluados)
  → sección/capacidad P–M  (metadatos.seccion → catálogo secciones.secciones
                     · secciones.elementos.demanda[caso] → M_cap → D/C)
```

| # | Eslabón | Dónde vive | Sobre el ejemplo (muro tag 41) |
|---|---|---|---|
| 1 | **elementTag OpenSees** | modelo del Edificio B (`src/edificio_b/`), tag entero por elemento | tag **41** = muro en planta `(11,20; 11,295)`, tramo base nodos **49 → 50**, `L = 3,96`, base empotrada |
| 2 | **Clave JSON** | `results/secciones_semana04.json` → `esfuerzos_completos."GQ"."41"` + `metadatos."41"`; overlay los vuelca en `results/edificio_solido.json` y en `StreamingAssets/edificio_completo.json` | `esfuerzos_completos.GQ."41"` = {L 3,96 · N **2175,21** (compresión) · Vy 189,39 · Vz 20,80 · T 13,30 · My −158,14 · **Mz 3909,15** · Wy 0 · Wz 0}; `metadatos."41"` = {tipo `wall`, sección `muro0.60x2.91`, H30, ni 49 / nj 50, i empotrado, j esclavo_diafragma, ejes {x̂=(0,0,1), ŷ=(0,−1,0), ẑ=(1,0,0)}} |
| 3 | **GameObject / collider** | `CrearPanelMuro` crea el panel con **BoxCollider** fijo (el mesh deformado solo reconstruye la geometría); `panelesPorObjeto[go] = p` registra el par | Clic sobre el muro = clic sobre su BoxCollider (tamaño t × altura × L, orientado según `resisteY`) |
| 4 | **Selección → panel** | `ProcesarSeleccionViga` → `SeleccionarElementoDeMuro(p, hit.point.y)`: cota `y` → nivel `st` → match por posición de planta (nodos x/y vs `xc,yc,L,t`) → **tag 41** → `consultaPorTag["41"]` → `DibujarPanelConsulta` | El panel muestra tag 41, tipo wall, H30, restricción empotrado → esclavo_diafragma, caso GQ y los esfuerzos de `esfuerzos_completos.GQ."41"` (§2.1) |
| 5 | **Diagramas / ventanas** | `ReconstruirDiagramas3D` (N verde, My azul, Mz rojo) y `DibujarVentanaDiagramas` (2D) evalúan con los 9 coeficientes | Mz(x) lineal 3909,15 → 4659,15 kN·m (x = L); My lineal −158,14 → −75,79; N constante −2175,2 (tracción positiva en pantalla) |
| 6 | **Sección y capacidad P–M** | `SeccionDesdeTag("41")`: `secciones.elementos."41".seccion = "muro0.60x2.91"` → catálogo `secciones.secciones.muro0.60x2.91` (envolvente 17 pts + balanceado + P0) y demanda `elementos."41".demanda` | GQ: P_d = **2 175,21 kN**, M_d = 3 912,35 kN·m (= √(My²+Mz²)), M_cap(P_d) = 5 270,9 kN·m → **D/C = 0,74** (la demanda queda **dentro** de la envolvente) |

> **Contar el ejemplo en 30 segundos:** "El muro tag 41 es el tramo del muro en
> (11,20; 11,295) entre los nodos 49 y 50. Su fila en `esfuerzos_completos` bajo
> GQ da N = +2 175,21 kN (compresión) y Mz = 3 909,15 kN·m; esos mismos datos,
> vía `metadatos."41"` (sección `muro0.60x2.91`, H30, empotrado/esclavo), llegan
> al panel y a la ventana P–M por el tag. La demanda P–M es
> (2 175,21 kN; 3 912,35 kN·m) y su capacidad interpolada en la envolvente de la
> sección es 5 270,9 kN·m → D/C = 0,74."

> **Verificación de que la cadena está pegada a datos reales** (no dibujada):
> la coordenada `cx = 11,20`, `cy = 11,295` de `secciones.elementos."41"`
> coincide exactamente con la posición de los nodos 49/50 del contrato, y la
> demanda `P = 2175,211` de la columna `demanda` repite `N = 2175,211` de
> `esfuerzos_completos` (mismo motor, misma pasada — ver `verif_semana04`).

## 6. Mapeo a la rúbrica

| Item de rúbrica | Cómo se satisface | Dónde |
|---|---|---|
| **Resultados conectados** | Los esfuerzos que se ven/consultan en Unity son los **mismos** números verificados de OpenSees (JSON con convención de semana 03, equilibrio global y cierre vertical en `verif_semana04`) | §1, §2, §5 |
| **Diagramas–deformada** | Deformada por caso (mesh reconstruido) + diagramas 3D N/My/Mz + ventana 2D con extremos, máximo y slider | §3 (filas 1–3), `[CAPTURA]` |
| **Capas** | Toggles independientes: deformada, diagramas, tributaria 45, cargas G/Q/sismo, apoyos, familias estructurales | §3 |
| **Demanda–capacidad** | Ventana P–M con envolvente + balanceado + demanda del caso activo + `M_cap(P_d)` + `D/C`, para columna y muro, sin duplicar el motor | §4, `[CAPTURA]` |
| **Defensa–trazabilidad** | Cadena completa documentada con un ejemplo real de punta a punta (tag 41 → JSON → collider → panel → esfuerzos → P–M) | §5 |

## 7. Entregables, reproducción y aceptación

| Artefacto | Ruta |
|---|---|
| Fuerzas completas + metadatos (caché) | `results/secciones_semana04.json` (`esfuerzos_completos`, `esfuerzos_completos_A`, `metadatos`, `metadatos_A`, `verif_semana04`, `convencion`, `unidades`) |
| Runner aditivo | `scripts/semana04_esfuerzos_completos_run.py` (`--recalcular` repite las 10 corridas; por defecto reutiliza el caché) |
| Motor de postproceso | `src/secciones/diagramas.py` (evaluador `N(x) = −N`, `M(x) = M + V·x + W·x²/2`) y overlay `src/secciones/exportar_unity.py` (`esfuerzos_completos` + `metadatos` + alias de catálogo) |
| Contrato del visor | `results/edificio_solido.json` → copia `unity/EdificioSolidoUnity/Assets/StreamingAssets/edificio_completo.json` |
| Visor (Unity) | `UnityStickModel.cs`: `ConsultaS4`, `consultasPorObjeto`/`consultaPorTag`, `DibujarPanelConsulta`, `ReconstruirDiagramas3D`, `DibujarVentanaDiagramas`, `DibujarVentanaPM`, `SeccionDesdeTag`, `CapacidadM`, `SeleccionarElementoDeMuro`; parseo en `ModeloComplejo.cs` |
| Tests | `tests/test_secciones.py` (sector "semana 04": esquema por caso, metadatos/cobertura/ejes/restricciones, evaluador, compresión, |M| sísmico, equilibrio/cierre, overlay end-to-end y `pm_columnas_todas_resuelven_catalogo`) |

```powershell
# Regenerar overlay desde caché (aditivo, idempotente) y correr la suite
python scripts\semana04_esfuerzos_completos_run.py      # B=True A=True, 315/343
python -m pytest -q                                     # 134 passed (>= 134)
```

**Aceptación de esta entrega:** `reports/semana04.md` con secciones 1–6,
números reales del JSON (§2.1, §4, §5) y la cadena de trazabilidad con ejemplo.
**Sin cambios de código** en esta sesión; la suite queda en **134 passed**.

## 8. Limitaciones y notas

- **EX a tracción fuera de la envolvente:** el muro tag 41 bajo EX tiene
  `P = −2 213,85 kN` (tracción) por debajo del `P` mínimo de la envolvente
  (−1 674,35 kN): `CapacidadM` recorta al primer punto y el `D/C` resultante es
  un artefacto de clamp, no una capacidad real. El chequeo a tracción queda
  pendiente (ya anotado en semana 03).
- **Signos en pantalla:** el panel de esfuerzos evalúa `N(x) = −N` (tracción
  positiva) mientras la ventana P–M usa compresión positiva; se aclaró en §2
  para que la defensa no desoriente.
- Los diagramas 3D se auto-escalan (cada curva a su máximo, ~15 % de la luz):
  son **cualitativos en amplitud**; los valores exactos están en el panel y en
  la ventana 2D.
- La validación visual en Play del editor (panel, ventanas arrastrables,
  selección de muro por clic, P–M completo para columna/muro de A y B) quedó
  compilada sin errores (batchmode, Unity 2022.3.62f3) y pendiente de capturas —
  marcadas `[CAPTURA: …]` para Nicolás.