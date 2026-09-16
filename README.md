# Proyecto 1 MCOC — COMPLEJO de Ingeniería (Edificios A y B)

Laboratorio estructural digital 3D (OpenSeesPy + Unity + AR). **Tercera
carpeta que unifica el Edificio A y el Edificio B como UN solo proyecto**,
mostrados lado a lado.

- **Edificio A** (`src/benchmark_3d/`): modelo lineal-elástico 3D según los
  planos `2017_67-*` (retícula 45 × 16.15 m + anexo metálico I'–J + voladizos
  trasero A3 y de piso 1 del eje F). Hormigón **G35**. Completo y verificado:
  casos G/Q/GQ/EX/EY, diafragmas rígidos, T/L, cortante de muros, 60 tests.
- **Edificio B** (`src/edificio_b/`): modelo armado desde cero según los
  planos `2024_22-*` (6 niveles × 3.96 m, 8 pilares 70×70, muros columna
  ancha + brazos rígidos, bloque escalera/ascensor). Hormigón **H30**. Por
  ahora **PP + SC** (pendiente de subirlo a la paridad sísmica del A).
- **Fusión** (`src/benchmark_3d/fusionar.py`): análisis independientes + un
  solo contrato `results/edificio_completo.json` con el Edificio B desplazado
  en **+X** (offset paramétrico, default 60 m) y renumerado (+100,000).

## Estructura

```
Proyecto 1 MCOC - Complejo (A+B)/
├── data/                  # Esquema JSON (esquema/ejemplo)
├── docs/                  # bitacora.md (unificada) · ficha_geometria(.md/_B) · bitacoras por edificio
├── results/               # modelo_resultados(_b).json · edificio_completo.json · PNG/HTML
├── scripts/               # Extracción DXF (infraestructura del Edificio A)
├── src/
│   ├── complejo.py        # Punto de entrada: A + B + fusión + visualización
│   ├── benchmark_3d/      # Pipeline del Edificio A (+ fusionar + visualizadores complejo)
│   └── edificio_b/        # Pipeline del Edificio B (paquete, imports relativos)
├── tests/                 # A (test_benchmark, test_json_contrato) · B (test_edificio_b) · complejo
└── unity/Scripts/         # C#: ModeloEdificio, UnityStickModel, OrbitCamera, UnityComplejo
```

## Estado

En el cierre de la Sesión 1 del complejo: dos análisis OpenSees independientes
reproducidos, `edificio_completo.json` generado, PNG + HTML lado a lado y
tests del contrato. Ver `docs/bitacora.md` para el detalle.

## Ejecución

```bash
# Entorno (Python 3.12)
python -m venv .venv
.venv\Scripts\Activate.ps1
pip install -r requirements.txt

# COMPLETO (analiza A, analiza B, fusiona y visualiza)
python src\complejo.py

# Re-fusionar sin re-analizar
python src\benchmark_3d\fusionar.py --sin-correr

# Análisis individuales
python src\benchmark_3d.py                 # Edificio A
python src\edificio_b\analizar.py          # Edificio B

# Verificación de cargas cross-check (A)
python src\benchmark_3d\verificar_cargas.py

# Tests
python -m pytest tests\ -v
```

## Resultados de referencia (cierre Sesión 1 del complejo)

| Magnitud | Edificio A | Edificio B |
|---|---|---|
| Nodos | 200 | 235 (+5 masters) |
| Elementos | 343 | 350 |
| Casos | G / Q / GQ / EX / EY | PP + SC |
| Carga gravitatoria total | G = 45,236 kN · Q = 7,671 kN | PP = 21,446 kN · SC = 9,333 kN |
| V sísmico (α = 0.10) | 4,524 kN | — |
| Material | G35 (f'c = 35 MPa) | H30 (f'c = 30 MPa) |
| Offset visual B | 0 | +60 m en X |

## Documentación

- `docs/bitacora.md` — bitácora unificada (leer al iniciar cada sesión).
- `docs/ficha_geometria.md` — Edificio A · `docs/ficha_geometria_B.md` — Edificio B.
- `src/benchmark_3d/README.md` — modelo benchmark del Edificio A.

## Unity

El contrato `results/edificio_completo.json` alimenta al visualizador del
complejo (`unity/Scripts/UnityComplejo.cs`). Ver `unity/README.md` para
instalación y puesta en marcha.