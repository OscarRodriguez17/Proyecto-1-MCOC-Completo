# Unity — Visualización estructural (Edificio A y COMPLEJO A+B)

> **PROYECTO OFICIAL / ESCENA QUE HAY QUE ABRIR:**
> `unity/EdificioSolidoUnity/` → escena `Assets/Scenes/Main.unity`
> (doble clic en `abrir_unity_solido.cmd`, luego Play). Ahí se ve el **visor
> sólido 3D completo del Complejo A+B** (columnas/vigas/muros con cuerpo,
> paneles, cargas, áreas tributarias y consulta de esfuerzos).
>
> El proyecto `unity/EdificioComplejoUnity/` es el **visor antiguo** (líneas
> `UnityComplejo`/`UnityStickModel` sobre `modelo_resultados.json`) y **NO** es
> el que se entrega.

El visor oficial (`EdificioSolidoUnity`) lee `Assets/StreamingAssets/edificio_completo.json`
y reconstruye la escena mínima `Main.unity` desde el JSON al abrir/cargar,
así todos ven exactamente el mismo modelo en cualquier máquina.

## Scripts

| Archivo | Contenido |
|---|---|
| `ModeloEdificio.cs` | Clases espejo del contrato JSON (Newtonsoft Json) |
| `UnityStickModel.cs` | Dibuja el modelo 3D, deformada por caso, cargas, tribunaria |
| `OrbitCamera.cs`    | Cámara orbital (botón derecho = rotar, rueda = zoom, medio = pan) |
| `MCOCSceneSetup.cs` | Prepara la escena Main (Editor menu Tools > MCOC) |

## Instalación / puesta en marcha

1. Instalar Unity Hub + Editor **2022.3 LTS** y crear un proyecto (p.ej.
   `unity/EdificioIngUnity/`).
2. Añadir el paquete `com.unity.nuget.newtonsoft-json` (Window > Package Manager).
3. Copiar los scripts de `unity/Scripts/` a `Assets/Scripts/` y
   `MCOCSceneSetup.cs` a `Assets/Editor/`.
4. Regenerar el JSON y copiarlo a `Assets/StreamingAssets/modelo_resultados.json`:
   ```
   .venv\Scripts\python.exe src\benchmark_3d\analizar.py
   Copy-Item results\modelo_resultados.json unity\EdificioIngUnity\Assets\StreamingAssets\
   ```
5. Ejecutar **Tools > MCOC > Preparar escena Main** (o dejar que se cree sola) y
   pulsar Play.

## Mapeo de coordenadas

- `X = x` (m), `Y = z` (altura, m), `Z = y` (m).
- Unidades SI (m, kN).

## Contrato JSON

Esquema de `results/modelo_resultados.json` (protegido por
`tests/test_json_contrato.py`):

```jsonc
{
  "proyecto": "...",
  "unidades": "m, kN",
  "geometria": { "grid_x": {}, "grid_y": {}, "niveles_z": [], "secciones": {} },
  "nodos": { "<tag>": { "x","y","z","nivel","rol" } },
  "elementos": [ { "tag","tipo","ni","nj" } ],
  "cargas": { "vigas": [], "q_losa": {}, "pesos_por_nivel": {}, "sismo": {} },
  "apoyos": [ { "tag","tipo","constraint" } ],
  "resultados": { "<caso>": { "desplazamientos_maestro": {}, "aplicada": {}, "reacciones_totales": {} } }
}
```

Tipos de elemento: `column`, `wall`, `vigas_x`, `vigas_y`. Casos: `G`, `Q`, `GQ`,
`EX`, `EY`.

## Visualizador del COMPLEJO (Edificios A y B)

`UnityComplejo.cs` lee `edificio_completo.json` (generado por
`fusionar.py`) y dibuja **ambos edificios lado a lado** con el offset X del B
ya aplicado en el JSON. Entiende los dos esquemas de nodos internos (A: dict de
tags; B: lista con id). El conjunto se **centra en el origen** de la escena
(`CentroTotal` se calcula en dos pasadas y cada edificio se desplaza según sea
necesario) para que la cámara orbital encuadre bien.

### Proyecto Unity autocontenido: `EdificioComplejoUnity/`

El complejo incluye un proyecto Unity completo y listo para abrir:

```
unity/EdificioComplejoUnity/
  Assets/
    Scripts/          ModeloEdificio.cs, UnityStickModel.cs, OrbitCamera.cs, UnityComplejo.cs
    Editor/           ComplejoSceneSetup.cs  (bootstrap, crea Assets/Scenes/Complejo.unity)
    StreamingAssets/  edificio_completo.json, modelo_resultados.json
  Packages/manifest.json                 (com.unity.nuget.newtonsoft-json 3.2.1)
  ProjectSettings/ProjectVersion.txt     (2022.3.62f3)
```

Para abrir:

1. Doble clic en `abrir_unity_complejo.cmd` (usa el editor 2022.3 LTS de
   `C:\Program Files\Unity\Hub\Editor\2022.3.62f3`) **o** `Unity.exe -projectPath
   "…\unity\EdificioComplejoUnity"`.
2. El bootstrap crea `Assets/Scenes/Complejo.unity` (cámara + cámara orbital +
   luz + GameObject "Complejo" con `UnityComplejo`). Si no, menú
   **Tools > MCOC > Preparar escena COMPLEJO**.
3. Añadir la escena al Build Settings (File > Build Settings > Add Open Scenes)
   y pulsar Play. Atajos: botón derecho ratón = rotar, rueda = zoom, medio = pan,
   W/A/S/D = moverse, V/F/C = vistas, R = reset.

No se copian `MCOCSceneSetup.cs` ni `UnityStickModel` como visor activo del
complejo: el primero crearía `Main.unity` (Edificio A) que entraría en conflicto
con el bootstrap, y el visor de la escena es `UnityComplejo`.

Paleta compartida con los visualizadores PNG/HTML: columnas gris, vigas azul,
muros rojo, acero/aspas/brazos naranja.
