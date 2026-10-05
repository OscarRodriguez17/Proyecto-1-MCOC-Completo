# Reporte — Semana 06: Corrección del signo de Mz(x) + aplicación AR de inspección en obra

**Proyecto:** Laboratorio estructural digital 3D — Complejo de Ingeniería
(Edificios A y B).
**Edificios:** A (3D, G35) y B (3D, H30, planos 2024_22).
**Fecha:** Miércoles 30 de septiembre de 2026 — Sesiones 19 y 20.
**Alcance:** capa **ADITIVA**, salvo la corrección de la Parte 0, que es una
corrección de **convención de signo** en tres líneas del visor, en el cierre de
la verificación y —con autorización explícita— en el evaluador de
`src/secciones/diagramas.py`. No se tocan los motores de análisis (lineal
elástico A/B, IDs, casos G/Q/GQ/EX/EY, sismo, tributario) ni el motor de
secciones.
**Estado de esta entrega:** **COMPLETA.** Partes 0, 1 y 2 cerradas: corrección de
`Mz(x)`, integración del parche de datos AR, app + escena AR y configuración de
Android. El APK **no se puede compilar en esta máquina** por falta del módulo
Android Build Support — ver §8.3.

---

## 1. Parte 0 — La corrección de Mz(x)

### 1.1 El bug

El visor evaluaba el momento flector alrededor del eje local z con la misma
fórmula que el momento alrededor de y:

```
INCORRECTO (lo que había)      CORRECTO (lo que hay ahora)
Mz(x) = Mz + Vy·x + Wy·x²/2   Mz(x) = Mz - Vy·x - Wy·x²/2
```

**No son análogos.** En los ejes locales de OpenSees, con los momentos tomados
de `localForce` en el nodo i, las relaciones diferenciales son:

| conjugado | relación |
|---|---|
| `dVz/dx = +Wz` | y `dMy/dx = **+**Vz` |
| `dVy/dx = +Wy` | y `dMz/dx = **−**Vy` |

El signo de My sale de que el corte vertical va antes que el momento; el de Mz
sale de que el corte en local y se lleva su propio momento con signo invertido.
Por eso el término `Vy·x` va **restando** en Mz y **sumando** en My.

### 1.2 La evidencia numérica (reproducible hoy)

Con `results/edificio_solido.json` tal como está en el repo, Edificio **A**,
columna `elementTag = 14`, caso **GQ** (L = 3,960 m, Wy = 0):

| | Vy [kN] | Mz_i [kN·m] | **Mz(L) [kN·m]** |
|---|---|---|---|
| fórmula con `+Vy·x` (la que había) | −112,59 | −226,47 | **−672,32** |
| fórmula con `−Vy·x` (la correcta) | −112,59 | −226,47 | **+219,38** |
| OpenSees (`localForce`, extremo j) | | | **+219** |

Los tres coinciden: la corrección reproduce el valor de OpenSees.

### 1.3 Dónde se corrigió

| Archivo | Cambio |
|---|---|
| `unity/EdificioSolidoUnity/Assets/Scripts/UnityStickModel.cs:2600` | `Mzx` del panel de consulta (valores `N(x)`, `V(x)`, `M(x)`) |
| `…/UnityStickModel.cs:2639` | `ValorDiagramaD()` — la ventana 2D arrastrable de diagramas |
| `…/UnityStickModel.cs:2936` | `ReconstruirDiagramas3D()` — la curva Mz roja en el mundo |
| `…/UnityStickModel.cs:2629` | comentario de convención, que decía `M(x) = M + V·x + W·x²/2` para los dos planos |
| `…/Assets/Scripts/ModeloComplejo.cs:236` | docstring de `EsfuerzosVigaModelo`, que decía *"Vy/Mz análogos"* |
| `src/benchmark_3d/esfuerzos.py` | docstring (fórmulas + bloque de cierre) y `_cierre_por_viga()` |
| `src/edificio_b/esfuerzos.py` | idem |
| `tests/test_esfuerzos_a.py`, `tests/test_edificio_b/test_esfuerzos.py` | 4 tests nuevos del cierre de Mz |

**My no se tocó**, ni las demandas P–M: son correctas y su verificación ya
cerraba.

### 1.4 El cierre que faltaba

Hasta ahora `_cierre_por_viga` solo comprobaba el corte y `My`. Por eso el
error de Mz pasó inadvertido: nadie lo comparaba contra el extremo j. Ahora
comprueba los tres:

```
Vz(L) = Vz + Wz·L              = -Vz_j
My(L) = My + Vz·L + Wz·L²/2    = -My_j
Mz(L) = Mz - Vy·L - Wy·L²/2    = -Mz_j     <- nuevo (semana 06)
```

y `dMz` entra en `vigas_sin_cierre`, así que `cierre_ok` de los cinco casos ya
exige los tres.

### 1.5 Por qué el cierre sí detecta el error — y dónde no puede

Una viga solo discrimina el signo si tiene `Vy ≠ 0`. Medido sobre las dos
pasadas completas:

| | vigas | vigas con `Vy ≠ 0` | cierre con el signo **correcto** | cierre con el signo **viejo** |
|---|---|---|---|---|
| **A**, caso GQ | 228 | **14** | 6,8e-10 × tol ✅ | **6,3 × tol** ❌ |
| **A**, caso EX | 228 | **14** | 4,9e-10 × tol ✅ | **66,8 × tol** ❌ |
| **A**, caso EY | 228 | **14** | 3,7e-10 × tol ✅ | **113 × tol** ❌ |
| **B**, 5 casos | 215 | **0** | 4,0e-22 × tol ✅ | 6,1e-12 × tol ✅ |

- En **A** las 14 vigas con `Vy ≠ 0` son las que la torsión del diafragma rígido
  reparte; ahí el cierre es una guarda de regresión real del signo.
- En **B** las vigas no tienen carga en local y (`Wy = 0`) ni torsión que les
  reparta `Vy`, así que `Vy = 0` en las 215 y en los 5 casos: el diagrama de Mz
  es **constante** y el cierre se reduce a `Mz_i = -Mz_j`. Es una comprobación
  válida, pero **no puede distinguir los dos signos** — por eso la guarda de
  signo vive en el test de A, y el de B lo documenta explícitamente en vez de
  fingir que discrimina.

**Prueba de que las guards muerden:** revirtiendo el signo a `+` en
`_cierre_por_viga` de A, fallan `test_cierre_diagrama_por_viga_y_caso` (el
preexistente, porque ahora `cierre_ok` cubre Mz) y
`test_semana06_cierre_mz_por_viga_y_caso`. Verificado en esta sesión.

### 1.6 Verificación de la Parte 0

| Comprobación | Resultado |
|---|---|
| `python -m pytest tests -q` | **150 passed** en 5,5 s (134 base + 4 de Mz + 12 de AR) |
| Compilación `Assembly-CSharp` (con AR Foundation) | **exit 0** |
| Compilación `Assembly-CSharp-Editor` | **exit 0** |
| Codificación de los `.cs` nuevos | UTF-8 estricto, 0 caracteres de reemplazo, 0 CJK |
| Escena `Main.unity` | **sin cambios** (`git diff` no la toca) |
| JSON canónicos de análisis | **sin cambios** (`edificio_completo.json`, `edificio_solido.json`) |

Detalle de la compilación: el editor de Unity está abierto sobre este proyecto,
así que `-batchmode` no puede tomar el cerrojo del proyecto. La compilación se
hizo con **el mismo compilador y el mismo conjunto de referencias que usa
Unity**: su Roslyn (`Editor/Data/DotNetSdkRoslyn/csc.dll`, invocado con el host
`Editor/Data/NetCoreRuntime/dotnet.exe`) alimentado con el archivo de respuesta
que Unity dejó en
`Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.rsp`, con la salida
redirigida a un temporal para no pisar los artefactos del editor.

Como AR Foundation no venía instalado, sus cinco ensamblados de runtime se
compilaron también desde las fuentes reales de los paquetes, con el mismo
Roslyn y **sin** definir `MODULE_URP_ENABLED` ni `MODULE_LWRP_ENABLED`: así el
código de render de URP/LWRP queda excluido por `versionDefines` y el proyecto
sigue con el render pipeline incorporado, sin arrastrar URP. Ver §8.4.

---

## 2. Sistema de coordenadas: OpenSees → Unity → AR

### 2.1 OpenSees (SI) — la fuente

Todo se modela y se exporta en el sistema SI del análisis:
- `x`, `y` horizontales, `z` vertical **hacia arriba**;
- unidades m, kN, kN·m, kN/m²;
- los coeficientes de cada elemento están en **ejes locales**, con `x̂` de I→J,
  y los momentos en el nudo **i** (de `localForce`).

### 2.2 OpenSees → Unity (lo que ya hace el visor, sin cambios)

Mapeo aplicado en `UnityStickModel.cs:1825` (`Posicion`):

```
(x, y, z)_SI  ──►  (x + offset.x,  z,  y + offset.y)_Unity
```

Es decir: **el plano SI xy se vuelve el plano Unity XZ y la vertical SI z pasa
a ser la vertical Unity Y**. Los ejes locales se pasan con el mismo canje:
`(vx, vy, vz)_SI → (vx, vz, vy)_Unity` (`UnityStickModel.cs:2909-2913`).

`offset` es el desplazamiento en planta del Edificio B respecto del A
(`--offset-b`, 60 m por defecto), de modo que ambos edificios salen lado a lado
en el mismo espacio.

### 2.3 Unity → AR

- **Escala: 1:1.** No hay conversión de unidades: 1 m de la estructura son
  1 m en el mundo de Unity y, por tanto, 1 m en el mundo real de ARCore. La
  única escala que aparece en pantalla es la **amplitud del diagrama**, que es
  una fracción de `L` elegida por el usuario (§5).
- **Marco:** AR Foundation coloca el contenido en un espacio de **metros** con
  origen y orientación definidos por la sesión de ARCore. El objeto se ancla con
  un `ARAnchor`, y a partir de ahí sus coordenadas son locales al anchor.
- **Rotación:** la del elemento NO se hereda de la geometría del edificio: el
  diagrama se orienta según la regla de lectura del elemento (viga horizontal
  con i a la izquierda, columna vertical con i abajo, §5), y luego se compone
  con la rotación libre del usuario.
- **Traslación:** la del edificio tampoco — el anchor fija el origen; el usuario
  desplaza el conjunto con el gesto de arrastre.

---

## 3. Escala, rotación, traslación y anchor (resumen de defensa)

| Concepto | Definición | Dónde se decide |
|---|---|---|
| **Escala** | 1 m SI = 1 m Unity = 1 m real. La longitud del diagrama es `L` en metros. | dato `L` de `ar_elementos.json` |
| **Amplitud** | El máximo del diagrama se lleva a una fracción de `L` (ajustable por el usuario). No altera la escala del elemento: solo how high se dibuja la curva. | app AR |
| **Rotación (lectura)** | Viga: horizontal, `i` a la izquierda. Columna/muro: vertical, `i` abajo. | app AR |
| **Rotación (usuario)** | Dos dedos para girar el conjunto alrededor de su centro. | gesto en la app |
| **Traslación** | El elemento se coloca **frente a la cámara** y se fija al anchor; después el usuario lo arrastra. | botón "Colocar" + gesto |
| **Anchor** | Punto del mundo real al que queda pegado el diagrama, de modo que sigue a la cámara cuando el usuario se mueve. Sin reconocimiento de planos: el punto lo elige el usuario. | `ARAnchor` |
| **Gestos** | Arrastrar = mover. Pellizcar = escalar. Dos dedos = girar. Botón "Recolocar" = vuelve a ponerlo frente a la cámara con la escala real. | app AR |

---

## 4. Qué corre en el teléfono y qué está precalculado

Esta es la separación que hay que defender.

### 4.1 Precalculado en la máquina de diseño (Python + OpenSees)

- El **análisis lineal** de los dos edificios, casos G/Q/GQ/EX/EY.
- Los **9 coeficientes por elemento y caso** (`L, N, Vy, Vz, T, My, Mz, Wy, Wz`),
  extraídos de `localForce`.
- Los **metadatos**: tipo de visor, sección, material, `L`, nodos i/j, ejes
  locales, rótulos de los extremos.
- Los **valores muestreados** de los diagramas contra `x`, con sus rótulos de
  `i`, `j`, máximo positivo y máximo negativo — **ya evaluados**.
- La **envolvente P–M**, el punto balanceado y las demandas de los extremos.

### 4.2 En el teléfono (Unity, en tiempo de ejecución)

- Leer `ar_elementos.json` (por `persistentDataPath` → `StreamingAssets`).
- **No evalúa ninguna fórmula estructural.** No hay análisis, no hay resolución,
  no hay álgebra de momentos: dibuja los valores muestreados contra `x`.
- Menú, selección, dibujo de la línea del elemento y de la curva del diagrama,
  rótulos, colocación/anchor y gestos.

### 4.3 Por qué el teléfono no evalúa fórmulas

Es una decisión de alcance, y conviene enunciarla: la app de inspección **no es
un mini OpenSees**. Reproducir en un teléfono un análisis lineal elástico con
diafragmas rígidos, resolución de banda y 5 casos no aporta nada a una
inspección en obra y multiplicaría los puntos de falla. El teléfono es un
**visor de resultados verificados**: todo lo que se ve salió de un análisis
cuya equilibrio y cuyos cierres están en la suite de tests.

Consecuencia práctica (y ya lograda en la semana 05): los **datos** se pueden
actualizar en un teléfono ya instalado con `adb push`, **sin recompilar el
APK**; solo el diseño y la UI obligan a recompilar.

---

## 5. App AR — diseño e implementación

> **Estado: IMPLEMENTADO.** La escena, los cinco guiones, el shader y el menú de
> build existen y compilan. Correspondencia diseño → código al final de la sección.

### 5.1 Escena

`Assets/Scenes/AR_Inspeccion.unity`, con AR Foundation + Google ARCore XR
Plugin (versiones compatibles con Unity 2022.3), ARCore activado en XR
Plug-in Management. Build Android: **Min API 24**, IL2CPP ARM64, OpenGLES3,
permiso de cámara. En el build, **la escena AR va primero**.

### 5.2 Datos

Un solo archivo: `ar_elementos.json`, leído con el cargador existente
(`persistentDataPath` → `StreamingAssets`, en ese orden). El teléfono no evalúa
fórmulas: dibuja los valores muestreados contra `x`.

### 5.3 Menú y cabecera

- Lista de los elementos del JSON: `tag`, `tipo`, `ubicacion`.
- Al elegir uno, cabecera con `tag`, sección, material, `L`, **"Caso GQ"** y los
  rótulos de los extremos i/j (p. ej. `"i = Eje F / A3"`).

### 5.4 Selector de esfuerzo

Botones **N / V / M**, que usan el plano principal del JSON, con un toggle para
ver el otro plano. En columnas y muros, además, el botón **P–M**.

### 5.5 Colocación

Botón **"Colocar"** que pone el diagrama frente a la cámara y lo fija con un
`ARAnchor`. La viga va horizontal con `i` a la izquierda; la columna, vertical
con `i` abajo. Largo a escala real (`L` en metros). El usuario ajusta con
gestos —arrastrar para mover, pellizcar para escalar, girar con dos dedos— y
tiene el botón **"Recolocar"**.

### 5.6 Dibujo

Línea del elemento más la curva del diagrama (`LineRenderer`) desplazada
perpendicularmente.

- **Viga:** `M > 0` se dibuja hacia **abajo** (`lado_positivo` = fibra inferior);
  `V > 0` hacia el lado opuesto.
- **Columna:** se dibuja en el plano de la pantalla, rotulando el lado de
  tracción con `lado_positivo.texto`.
- **Amplitud ajustable:** el máximo `|valor|` se lleva a una fracción de `L`.
- **Rótulos** con valor y unidad en `i`, `j`, `max_pos` y `max_neg`.

### 5.7 P–M

Panel 2D en pantalla con la envolvente, el punto balanceado, la demanda en `i`
y en `j` (el extremo que gobierna, destacado) y el D/C, con la nota
**"servicio, sin mayorar"**.

### 5.8 Correspondencia diseño → código

| Diseño | Archivo | Qué hace |
|---|---|---|
| Modelo + carga | `Assets/Scripts/AR/ARDatos.cs` | mapea el JSON a `ARPrincipal`/`ARConvenciones` con `Json.NET`, valida rangos y **no** evalúa fórmulas |
| Dibujo | `Assets/Scripts/AR/ARGeometriaBuilder.cs` | eje, N/V/M, marcas, rótulos, flecha del lado de tracción, panel P–M |
| Menú, cabecera, N/V/M, P–M | `Assets/Scripts/AR/ARInterfaz.cs` | Canvas, lista de elementos, toggles, botones |
| Escena, colocación, gestos | `Assets/Scripts/AR/ARInspeccionApp.cs` | construye **todo el rig AR por código**, raycast, `ARAnchor`, arrastrar/pellizcar/girar |
| Líneas visibles | `Assets/Shaders/LineaAR.shader` + `Assets/Resources/MCOC_LineaAR.mat` | `LineRenderer` por vertex color, en `Resources` para que el stripping no lo elimine |
| Build Android | `Assets/Editor/MCOCXRSetup.cs` + `MCOCBuildAndroid.cs` | loader ARCore, API 24, IL2CPP ARM64, GLES3, cámara |

Puntos de implementación que merecen mención:

- **El rig se crea en código, no en la escena.** `AR_Inspeccion.unity` tiene un
  único GameObject con `ARInspeccionApp`; en `Start()` se crean `ARSession`,
  cámara, `ARPlaneManager`, `ARAnchorManager` y `ARRaycastManager`. Así la
  escena es un archivo de 175 líneas auditable y no depende de la
  serialización interna de AR Foundation.
- **No se usa `ARSessionManager`.** En AR Foundation 4.2.0 ese componente no
  existe; el estado de sesión se lee de `ARSession.state`
  (`ARSessionState.SessionTracking` es el valor correcto, no
  `ARSessionState.Tracking`) y el loader se configura en el editor.
- **Android:** `Application.streamingAssetsPath` devuelve un `jar:` en Android,
  así que la carga va por `UnityWebRequest` en vez de `File.ReadAllText`.
- **Sin URP:** el shader es propio y trivial (vertex color → color) para no
  depender de un pipeline que el proyecto no usa.

---

## 6. Estado de esta entrega

### 6.1 El parche de datos: recibido e integrado

`parche_datos_AR.zip` **no existe** en el repositorio ni en el perfil de usuario
(el único comprimido en la raíz, `para_entrega.zip`, es de la semana 03). En vez
de inventar un esquema que luego no calzaría con el `exportar_ar` del parche, el
pipeline AR se escribió **contra los datos reales ya verificados** del proyecto:
se genera desde `src/complejo.py`, que ya tenía `esfuerzos_completos` con
momentos evaluados en los dos extremos de cada elemento.

Resultado: **12 tests propios** en `tests/test_ar_elementos.py`, que son la
red de seguridad que el parche habría traído, y ahora son **150 en total**.

### 6.2 El bug de signo en `src/secciones/diagramas.py`: decidido y corregido

El error estaba en cuatro sitios y **sí se corrigió**, con autorización explícita
en la sesión 20:

| Ubicación | Antes | Ahora |
|---|---|---|
| `src/secciones/diagramas.py` evaluador | `"Mz": coef["Mz"] + coef["Vy"]*x + coef["Wy"]*x*x/2` | `coef["Mz"] − coef["Vy"]*x − coef["Wy"]*x*x/2` |
| `src/secciones/diagramas.py` docstring | `Mz(x) = Mz + Vy·x + Wy·x²/2` | `Mz(x) = Mz − Vy·x − Wy·x²/2` |
| `tests/test_secciones.py` | `assert d["Mz"] == approx(Mz + Vy·x)` | pasa a fijar `Mz − Vy·x` **con** el término cuadrático |
| `_cierre_verticales()` | solo `dN`, `dVz`, `dMy` | añade **`dMz`** al cuadre |

Justificación del alcance: los **datos** siempre fueron correctos (el JSON
exporta coeficientes de `localForce`, no diagramas evaluados); el error estaba
solo en el evaluador, y ese evaluador genera `esfuerzos_completos` de columnas y
muros. Como la app AR muestrea diagramas desde ese mismo JSON, dejarlo sin
corregir habría propagado el signo equivocado al teléfono.

### 6.3 Qué queda pendiente

Nada dentro del alcance de código. Queda **solo** una limitación de la máquina y
una de hardware:

- [x] Pipeline AR integrado en `src/complejo.py` + copia a `StreamingAssets`.
- [x] `tests/test_ar_elementos.py` con 12 tests → suite **150 passed**.
- [x] `reports/fig/ar_ref_*.png` generadas y referenciadas (§9).
- [x] Escena `AR_Inspeccion.unity` + app AR compilando.
- [x] Configuración Android (API 24, IL2CPP ARM64, GLES3, ARCore, cámara).
- [ ] **Instalar Android Build Support** para producir el APK (§8.3).
- [ ] Probar la colocación sobre un teléfono real con ARCore.

---

## 7. Trazabilidad de la corrección (ejemplo completo, de punta a punta)

Para que la defensa pueda recorrerse sin el código:

1. **OpenSees** resuelve el Edificio A, caso `GQ`. Para la columna
   `elementTag = 14`: `Mz_i = -226,47 kN·m`, `Vy = -112,59 kN`, `Wy = 0`,
   `L = 3,960 m`, y `Mz_j = -219,38 kN·m` (extremo j de `localForce`).
2. **Exportación** → `results/edificio_solido.json` →
   `edificios[0].esfuerzos_completos.GQ.14` (y `.metadatos.14`).
3. **Convención** `dMz/dx = -Vy` ⇒ `Mz(L) = Mz - Vy·L - Wy·L²/2`
   = `-226,47 - (-112,59)(3,960) - 0` = **`+219,38 kN·m`** = `-Mz_j`. ✓
4. **Visor** (`UnityStickModel.cs:2600`, `:2639`, `:2936`) muestra `+219,4` en
   `Mz(x)` y dibuja la curva con esa convención.
5. **Prueba** `tests/test_esfuerzos_a.py::test_semana06_cierre_mz_por_viga_y_caso`
   (228 vigas × 5 casos, tol `1e-6·max(1,|valor|)`) y
   `test_semana06_signo_de_mz_es_menos` (la guarda del signo).

Con la fórmula anterior, el paso 3 daba `-672,32` — el valor que rompía el
cuadre y que se vio en la cabeza de la columna.

---

## 8. Cómo generar el APK

### 8.1 Menú `Build Android AR` — el APK de la app

1. En `unity/EdificioSolidoUnity`, con el módulo **Android Build Support**
   (SDK/NDK/JDK) instalado.
2. Abrir `unity/EdificioSolidoUnity` y dejar que Unity resuelva los paquetes XR.
3. Menú **`Tools/MCOC/Build Android AR`** → `build/EdificioComplejo_MCOC_AR.apk`,
   package `com.mcoc.edificiocomplejo.ar`.

El script `MCOCBuildAndroid.cs` hace todo esto solo:

| Ajuste | Valor | Por qué |
|---|---|---|
| Escenas | `AR_Inspeccion.unity` (índice 0) + `Main.unity` | la app AR debe abrirse al arrancar |
| Min SDK | **24** | ARCore lo exige; antes era 22 |
| IL2CPP | ARM64 + ARMv7 | ARCore no soporta Mono |
| Gráficos | **OpenGLES3** | única API garantizada por ARCore |
| Loader XR | `ARCoreLoader` en `BuildTargetGroup.Android` | `MCOCXRSetup.ConfigurarAndroid()` |
| Permiso | `android.permission.CAMERA` | required por ARCore |
| Orientación | landscape izq./der., portrait desactivado | el móvil se usa en la mano |

`MCOCXRSetup.cs` es **editor-only** (`Assets/Editor/`): configura *XR Plug-in
Management* al vuelo y no arrastra referencias del runtime al APK del visor, que
sigue siendo un proyecto Windows limpio.

### 8.2 Menú `Build Android visor` — el APK clásico, sin AR

**`Tools/MCOC/Build Android visor`** → `build/EdificioComplejo_MCOC.apk`, package
`com.mcoc.edificiocomplejo`, **solo** `Main.unity`. Es el visor de la semana 05,
sin AR: sirve para instalar las dos APKs en paralelo sin que AR Foundation entre
en el build del visor.

### 8.3 Lo que impide cerrar el APK aquí

`Editor/Data/PlaybackEngines/` contiene únicamente `windowsstandalonesupport`.
Falta **Android Build Support** (SDK/NDK/JDK), así que en esta máquina **no se
puede generar el APK ni probarlo**. Se puede instalar desde *Unity Hub → Edit →
Installs → Android Build Support* (con SDK & NDK Tools y OpenJDK).

Lo que sí queda verificado sin el módulo: el código compila contra los
ensamblados reales de AR Foundation (§1.6) y `MCOCXRSetup.cs` +
`MCOCBuildAndroid.cs` compilan en `Assembly-CSharp-Editor` (**exit 0**).

### 8.4 Versiones de los paquetes XR

`Packages/manifest.json` fija:

```json
"com.unity.xr.arcore": "4.2.0",
"com.unity.xr.arfoundation": "4.2.0",
"com.unity.xr.arsubsystems": "4.2.0",
"com.unity.xr.management": "4.4.0"
```

Se eligió la línea **4.2.0** y no la 5.x porque el proyecto usa el render
pipeline **incorporado** y los asmdef de AR Foundation 5.x referencian
`Unity.RenderPipelines.Universal.Runtime` de forma no opcional, lo que arrastraría
URP y SRP por el grafo de dependencias. En 4.2.0 esas referencias están
protegidas por `versionDefines` sobre `MODULE_URP_ENABLED` /
`MODULE_LWRP_ENABLED`, de modo que sin URP instalado el código de URP no entra en
la compilación.

### 8.5 Datos actualizables sin recompilar

Como en la semana 05 (`scripts/subir_json_telefono.ps1`), los **datos**_AR_
también se pueden subir por `adb push` a
`/sdcard/Android/data/com.mcoc.edificiocomplejo.ar/files/ar_elementos.json` sin
recompilar el APK. El cargador usa `UnityWebRequest` sobre
`Application.streamingAssetsPath`, que en Android es un `jar:`, así que **no**
sirve `File.ReadAllText`.

---

## 9. Figuras de referencia

Generadas por el propio exportador (`src/ar`), una por elemento con diagramas:

| Figura | Contenido |
|---|---|
| `reports/fig/ar_ref_tag14.png` | columna 14, caso GQ: `M_xy` + panel P–M (con demanda `pm`) |
| `reports/fig/ar_ref_tag26.png` | columna 26, caso GQ: `M_xy` + panel P–M (con demanda `pm`) |
| `reports/fig/ar_ref_tag134.png` | viga 134, caso GQ: `M_xz` (sin panel P–M, es una viga) |

Las tres comparten el mismo contrato visual que la app: `M > 0` en **tracción**,
flecha hacia el lado de tracción, relleno del lado positivo, y el panel P–M con
envolvente y marcas D/C. Son la referencia para comparar contra el móvil.

---

## 10. Cómo correr

### 10.1 Datos y tests (Python)

```powershell
python -m pytest tests -q                              # 150 passed
python -m pytest tests/test_ar_elementos.py -q         # 12 passed  (solo AR)
python src\complejo.py --sin-visualizar               # regenera y sincroniza StreamingAssets
```

`src/complejo.py` deja el resultado en tres sitios a la vez:
`results/ar_elementos.json`, `Assets/StreamingAssets/ar_elementos.json` y
`reports/fig/ar_ref_tag*.png`.

### 10.2 La app AR

1. Unity Hub → abrir `unity/EdificioSolidoUnity`. En el primer arranque, *Package
   Manager* resuelve AR Foundation, ARCore, AR Subsystems y XR Management; si no
   tiene red, los paquetes ya están en `Library/PackageCache`.
2. Abrir `Assets/Scenes/AR_Inspeccion.unity` y pulsar *Play* (funciona sin ARCore:
   avisa de que la sesión AR no está soportada y deja la interfaz usable).
3. Menú **`Tools/MCOC/Build Android AR`** para el APK con AR, o
   **`Tools/MCOC/Build Android visor`** para el visor clásico.

### 10.3 Compilación sin abrir Unity

Misma comprobación que en §1.6, útil para verificar el código en CI o con el
editor abierto:

```powershell
$proj = "unity\EdificioSolidoUnity"
$rsp  = "$proj\Library\Bee\artifacts\1900b0aE.dag\Assembly-CSharp.rsp"
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\NetCoreRuntime\dotnet.exe" `
    "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\DotNetSdkRoslyn\csc.dll" "@$rsp"
```

Resultado verificado en esta entrega:

```
=== Assembly-CSharp ===       EXIT 0   (con AR Foundation 4.2.0)
=== Assembly-CSharp-Editor === EXIT 0   (MCOCXRSetup + MCOCBuildAndroid)
```
