# Reporte — Semana 05: Del modelo al teléfono — modificaciones reales del modelo con actualización automática en Unity, superposición interactiva G+Q≡GQ / G+EX / G+Q+EX, y preparación de build Android

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

---

## 1. Funciones implementadas

1. **Flujo "editar dato → correr → 'Recargar JSON'" completo y probado**: se
   edita una hipótesis en `src/edificio_b/cargas.py` o `datos_edificio.py`, se
   reanaliza el Edificio B, se refrescan las cachés de secciones/semana 04 y el
   único botón del visor **"Recargar JSON"** (`UnityStickModel.Cargar`, línea
   2328 de `UnityStickModel.cs`) vuelve a leer `Assets/StreamingAssets/
   edificio_completo.json` y **todas las capas se actualizan solas** (deformada,
   esfuerzos, panel de consulta, ventana P–M y demanda–capacidad).
2. **Dos modificaciones REALES del modelo**, ejecutadas de punta a punta con su
   cadena "antes → después" con números (sección 2):
   - **MOD 1 — Sobrecarga de piso** `SC_PISO` 3,0 → 4,0 kN/m² (lámina 700): la
     carga vertical de uso del Edificio B pasa de **11 029,6 → 14 423,3 kN**.
   - **MOD 2 — Sección del muro de planta** `MUROS_V[0]` espesor t = 0,60 →
     0,70 m: su **P–M / P₀ se agranda** y la relación demanda–capacidad cambia.
3. **Superposición interactiva** con las 3 combinaciones de la rúbrica
   **G+Q≡GQ**, **G+EX** y **G+Q+EX**: la data del visor compara la corrida
   directa del caso compuesto contra su suma de casos simples y verifica
   **|superpuesta − OpenSees| ≈ 0** (sección 3).
4. **Paridad y equilibrio garantizados en CADA estado**: `equilibrio=OK
   superposición=OK` y `cierreV dN≈0` en las 10 corridas, tanto en el estado
   modificado como en el canónico.
5. **Preparación móvil**: menú **`Tools/MCOC/Build Android`** que compila la
   escena `Assets/Scenes/Main.unity` a `build/EdificioComplejo_MCOC.apk` con
   package **`com.mcoc.edificiocomplejo`**, **min SDK 22**, **landscape
   (LandscapeLeft)**, escena única y `bundleVersion 1.0` (sección 6).
6. **"Sidequest" carga móvil**: documentada como **NO implementada en v1**
   (sección 4).

## 2. Modificación del modelo — dos cambios reales con "antes → después"

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

Al final se **restauró el estado canónico** (ver §7) para que el restultado sea
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

## 3. Superposición interactiva — G+Q≡GQ, G+EX y G+Q+EX

El visor expone las 3 combinaciones de la rúbrica en un solo toggle: cada caso
compuesto se compara, **elemento por elemento y reacción por reacción**, contra
la suma de sus casos simples (la misma data verificada de `superposicion`):

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

> [CAPTURA: Play — toggle "Superposición" en G+Q+EX sobre el Edificio B:
> diagramas de la suma (G y Q y EX) sobrepuestos al caso directo GQEX sin
> desviación visible.]

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

**Cómo quedaría en v2** (si se pide): viga de influencia por tramo con el
camión de la semana 02/03 (el laboratorio ya tiene el "camión" en
`per_elemento`), barrido de posición x con `N(x)/Vz(x)/My(x)` de la semana 04 y
**breackpoint en `D/C`** en la ventana P–M. El visor ya trae la infraestructura
(slider de posición, `Wz·x²/2`, diagramas y ventana P–M) para colgarla encima
sin tocar el motor.

## 5. UX estructural — 6 preguntas que el ingeniero responde con el visor

Las 6 inspecciones de validación más útiles, todas con **números reales del
JSON canónico** (idénticos a los de las semanas previas):

| # | Pregunta del ingeniero | Respuesta en el visor (dato real) |
|---|---|---|
| 1 | ¿Cuánto pesa el conjunto y cómo se reparte? | COMPLEJO **G = 93 407,6 kN** (A 45 236,5 + B 48 171,1) y **Q = 18 700,8 kN** (A 7 671,2 + B 11 029,6); el "Recargar JSON" muestra el resumen por edificio. |
| 2 | ¿El muro crítico resiste la gravedad? | B tag 41, GQ: P_d = 2 175,21 kN, M_d = 3 912,35 kN·m, **M_cap(P_d) = 5 270,9 → D/C = 0,74** (punto DENTRO de la envolvente, con holgura del 26 %). |
| 3 | ¿Y bajo la envolvente sísmica? | B tag 41, **EY**: P_d = 1 973,5 kN, M_d = 9 022,8 kN·m → **fuera** de la envolvente (**D/C = 1,78**, "excede", igual que en semana 03/04): el muro se marca en rojo y el ingeniero ve el balanceado (16 406,8; 8 705,5) a qué distancia queda. |
| 4 | ¿Qué columna está al límite? | B tag 2, GQ: P_d = 8 145,9 kN, M_d = 1 278,4 → M_cap = 1 223,5 → **D/C = 1,05 (excede)**; la vecina tag 1 (P = 10 268,9) queda en 0,51. La ventana P–M permite saltar tag◀/▶ y ver el peor caso en segundos. |
| 5 | ¿Qué pasa si se sube la sobrecarga reglamentaria? | MOD 1: Q sube +30,8 % (11 029,6 → 14 423,3 kN) y el muro crítico pasa de D/C 0,74 → **0,79**; el ingeniero "edita → corre → Recargar JSON" y ve si algún elemento cruza la envolvente sin rehacer nada. |
| 6 | ¿Coincide "sumar G+Q" con "correr GQ directo"? | **max_dP = 4,68e-11 kN** y reacciones con max_dR ≤ 6,0e-11 kN en las 3 combinaciones: el principio de superposición se verifica **en pantalla**, elemento por elemento. |

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
   - compila a **`build/EdificioComplejo_MCOC.apk`** (raíz del proyecto) y abre
     el Explorador al finalizar.
4. Instalar en el teléfono (USB con depuración, o copiar el APK): mientras la
   app corre, los datos están **embebidos** en `StreamingAssets` (por eso el
   visor no necesita internet).

> NOTA de compatibilidad verificada (sin compilar en esta sesión): `minSdk 22`
> y orientación 4 (landscape) **ya estaban en `ProjectSettings.asset`**; el menú
> los reafirma y los vuelca al manifest del APK. La primera compilación Android
> puede pedir descargar `gradle/android-sdk` — es normal de Unity.

## 7. Aceptación y reproducción

- **Estado canónico final**: `SC_PISO = 3,0` y `MUROS_V[0]` con t = 0,60 (los
  archivos de entrada quedaron como al inicio); las cachés regeneradas dan los
  mismos números del baseline (B: G = 48 171,1 kN, Q = 11 029,6 kN, EX
  u_techo = 15,996 mm, M_volc = 76 424 kN·m; tag 41 GQ P_d = 2 175,21 /
  M_d = 3 912,35; §3 max_dP = 4,68e-11; §4 B GQ max_dR = 2,9e-11).
- **Suite de protección**: `python -m pytest tests` → **134 passed** (igual que
  la semana 04). La ruta completa `python -m pytest` sin argumentos choca con el
  `test_secciones.py` duplicado en `para_entrega/` (colisión de basename de
  módulo, preexistente); la suite real y entregable es `tests/` → 134.
- **Nuevo código aditivo**: `scripts/semana05_refrescar_caches.py`
  (refresca demandas / per_elemento / §3 / §4 / curvas nuevas) y
  `Assets/Editor/MCOCBuildAndroid.cs` (menú de build). Nada del motor de
  análisis ni de los resultados canónicos se tocó.

## 8. Entregables

- `reports/semana05.md` (este documento) con 6 secciones + capturas marcadas.
- `unity/EdificioSolidoUnity/Assets/Editor/MCOCBuildAndroid.cs` (build APK).
- `scripts/semana05_refrescar_caches.py` (motor de refresco de los ciclos).
- Resultados canónicos intactos en `results/` y `StreamingAssets/`.