# Project
Laboratorio estructural digital 3D — COMPLEJO de Ingeniería (Edificios A y B).
Un solo proyecto que une el Edificio A (planos 2017_67, G35) y el Edificio B
(planos 2024_22, H30) mostrados lado a lado (offset X paramétrico de B).

# Session Context
- Al iniciar cualquier sesión: leer `docs/bitacora.md` (estado actual, decisiones y próximos pasos).
- Al terminar cada sesión: actualizar `docs/bitacora.md`.
- Historial por edificio: `docs/bitacora_edificio_A.md` y `docs/bitacora_edificio_B.md`.

# Units
SI (Longitud: m, Fuerza: kN, Tensión/Módulo: kN/m², Momento: kN·m, Área: m², Inercia: m⁴)

# Structural Model Rules
- Modelo global por edificio: Lineal elástico 3D (6 GDL por nodo).
- Cada edificio se analiza INDEPENDIENTE (evita doble conteo de áreas
  tributarias); `fusionar.py` los reúne en UN contrato JSON.
- Ejes locales explícitos: vecxz no colineal al elemento (I -> J).
- Diafragmas rígidos: ops.rigidDiaphragm con ops.constraints("Transformation").
- Conservación estricta de áreas tributarias y cargas distribuidas.
- Edificio B vive en `src/edificio_b/` como paquete (imports relativos
  `from .construir import ...`) para no colisionar con los módulos de A.

# Verification Rules
- Verificar equilibrio global: sum(F) + sum(R) ≈ 0.
- Verificar principio de superposición numéricamente.
- Test analítico Euler-Bernoulli de referencia con tolerancia < 1e-10.
- Contrato JSON unificado protegido por `tests/test_complejo.py`.