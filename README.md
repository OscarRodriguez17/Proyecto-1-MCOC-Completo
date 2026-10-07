# Proyecto 1 MCOC — COMPLEJO de Ingeniería (Edificios A y B)

Laboratorio estructural digital 3D (OpenSeesPy + Unity + AR). **Tercera
carpeta que unifica el Edificio A y el Edificio B como UN solo proyecto**,
mostrados lado a lado.

- **Edificio A** (`src/benchmark_3d/`): modelo lineal-elástico 3D según los
  planos `2017_67-*` (retícula 50 × 16.15 m con anexo metálico hasta el eje J y
  voladizos trasero A3 y de piso 1 del eje F). Hormigón **G35**. Completo y
  verificado: casos G/Q/GQ/EX/EY, diafragmas rígidos, superposición ~1e-11,
  169 tests.
- **Edificio B** (`src/edificio_b/`): modelo armado desde cero según los
  planos `2024_22-*` (6 niveles × 3.96 m, 8 pilares 70×70, muros columna
  ancha + brazos rígidos, bloque escalera/ascensor). Hormigón **H30**. Completo
  y verificado: G/Q/GQ/EX/EY con sismo pseudoestático α = 0,10.
- **Fusión** (`src/benchmark_3d/fusionar.py`): análisis independientes + un
  solo contrato `results/edificio_completo.json` con el Edificio B desplazado
  en **+X** (offset paramétrico, default 60 m) y renumerado (+100,000).

## Estructura

```
Proyecto 1 MCOC - Complejo (A+B)/
├── data/                  # Esquema JSON (esquema/ejemplo)
├── docs/                  # bitacora.md (unificada) · ficha_geometria(.md/_B) · bitacoras por edificio
├── results/               # edificio_solido.json · edificio_completo.json · ar_elementos.json (9 tags) · PNG/HTML
├── scripts/               # Extracción DXF · subir_json_telefono.ps1 (adb) · refresh de cachés
├── src/
│   ├── complejo.py        # Punto de entrada: A + B + fusión + sincronización
│   ├── benchmark_3d/      # Pipeline del Edificio A (+ fusionar + acero + vigas secundarias)
│   ├── edificio_b/        # Pipeline del Edificio B (paquete, imports relativos)
│   ├── secciones/         # Fibras, M–φ, envolventes P–M, demanda–capacidad, superposición
│   └── ar/                # exportar_ar.py → ar_elementos.json (app de inspección AR)
├── tests/                 # 169 tests (A · B · secciones · contrato · ar · acero · vigas)
└── unity/EdificioSolidoUnity/   # Visor 3D + app AR (escenas Main.unity y AR_Inspeccion.unity)
```

## Estado

**ENTREGA FINAL (Semana 7).** Complejo completo y verificado: análisis
OpenSeesPy de A y B (casos G/Q/GQ/EX/EY), motor de secciones/fibras con
envolventes P–M y demanda–capacidad, visor Unity pre/postprocesador, app AR de
inspección con **9 elementos** del Edificio A, build Android con ARCore y suite
de **169 tests** (pytest) + **153/153** (Unity EditMode).

- Informe final: [`reports/final.md`](reports/final.md) (22 secciones).
- Índice de entregables + cómo regenerar todo (análisis, resultados, visor,
  tests, móvil): [`reports/README.md`](reports/README.md).
- Bitácora completa (Sesiones 1–33): [`docs/bitacora.md`](docs/bitacora.md).
- Producto ejecutable: `unity/EdificioSolidoUnity/build/EdificioComplejo_MCOC_AR.apk`
  (adjunto también a la release **`entrega-final`** de GitHub).

## Ejecución

```bash
# Entorno (Python 3.12; dependencias en requirements.txt)
python -m venv .venv
.venv\Scripts\Activate.ps1
pip install -r requirements.txt

# COMPLETO (analiza A, analiza B, fusiona y sincroniza StreamingAssets)
python src\complejo.py --sin-visualizar

# Contrato AR de 9 elementos (Edificio A, caso GQ)
python src\ar\exportar_ar.py --tags 134 14 26 354 350 337 340 342 105

# Tests
python -m pytest tests\          # → 169 passed
```

Visor Unity: abrir `unity/EdificioSolidoUnity` con **Unity 2022.3 LTS**
(acceso directo `abrir_unity_solido.cmd`) y pulsar **"Recargar JSON"**.
Build Android: menú `Tools/MCOC/Build Android` (visor) / `... Build Android AR`.
Actualización de datos en el teléfono sin recompilar:
`scripts\subir_json_telefono.ps1` (`adb push`).

## Resultados de referencia (canonical final)

| Magnitud | Edificio A | Edificio B |
|---|---|---|
| Nodos | 206 (+maestros diafragma) | 240 (+maestros) |
| Elementos | **354** (80 columnas, 238 vigas, 32 muros, 4 diagonales) | 350 |
| Casos | G / Q / GQ / EX / EY | G / Q / GQ / EX / EY |
| Carga gravitatoria | G = 45.417 kN · Q = 7.671 kN | G = 48.171 kN · Q = 11.030 kN |
| V sísmico (α = 0,10) | 4.542 kN | 4.817 kN |
| Superposición | G+Q≡GQ · G+EX · G+Q+EX: max ΔR ≤ 6,0e-11 kN | ídem |
| Material | G35 | H30 |
| Offset visual B | 0 | +60 m en X |

## Documentación

- `docs/bitacora.md` — bitácora unificada (leer al iniciar cada sesión).
- `docs/ficha_geometria.md` — Edificio A · `docs/ficha_geometria_B.md` — Edificio B.
- `src/benchmark_3d/README.md` — modelo benchmark del Edificio A.

## Unity

El contrato `results/edificio_completo.json` alimenta al visualizador del
complejo (`unity/Scripts/UnityComplejo.cs`). Ver `unity/README.md` para
instalación y puesta en marcha.