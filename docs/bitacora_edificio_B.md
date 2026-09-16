# Bitácora — Edificio B (Proyecto 1 MCOC)

> Leer al iniciar cada sesión. Actualizar al terminar.

## Estado actual

**Modelo 3D lineal-elástico ARMADO Y VERIFICADO desde cero.**

- Geometría extraída de los DXF (`2024_22-101` planta tipo, `-300..305`
  cortes) y documentada en `docs/ficha_geometria.md`.
- Modelo OpenSeesPy: 235 nodos (+5 masters), 350 elementos (40 pilares, 60 tramos de muro,
  215 vigas, 35 brazos rígidos).
- Base empotrada en **z = −4.01** (fundaciones ignoradas por indicación del
  profesor). El nivel −7.97 es fundación (V.F. + zapatas), confirmado en
  corte EJE 1.
- Muros modelados como **columna ancha equivalente** (sección real e×L).
- Diafragma rígido por piso (Transformation).
- Verificaciones OK: equilibrio (3e−11), superposición (1e−18),
  Euler-Bernoulli (3e−16 < 1e−10).
- Exporta `results/modelo_resultados.json` (contrato Unity).
- Visualización 3D en `results/modelo_3d.png`.
- Tests: `pytest tests/ -q` → 9 passed.

## Decisiones tomadas

- Escala DXF: 1 unidad = 1 cm (verificada por cotas y niveles).
- 6 niveles, entrepiso 3.96 m; base en −4.01.
- Grilla de ejes armada SOLO con pilares + vigas; muros y extremos de viga se
  ajustan a esa grilla → pilares en posición exacta del DXF y sin "islas" de
  vigas sin bajada (que causaban singularidad).
- Sección de viga por ancho; material H30 (E=25 GPa, ν=0.2).


## Corrección de muros (validada contra DXF crudo)

- Se re-extrajeron los muros por UNIÓN de caras (antes tomaba la intersección
  y truncaba paños). Extensiones corregidas: alas izquierdas completas
  (9.84–12.75 y 25.25–28.17), muro este superior a 26.73, núcleo a 24.05.
- Los muros ya NO se ajustan a la grilla de vigas: quedan en su posición real
  del plano (el muro este vuelve a x=42.58, sin el corrimiento de 0.23 m).
- Bloque escalera/ascensor (y>32) queda en `MUROS_BLOQUE_SUP_*`, excluido por
  defecto.

- Muros conectados al marco con BRAZOS RÍGIDOS (columna ancha + rigid arm):
  enganchan cada muro a las vigas de su eje y componen las L / el núcleo en C.
- Bloque escalera/ascensor INCLUIDO (muros e=0.20), amarrado por diafragma.

## Próximos pasos

1. Validar la ficha con el equipo / profesor (secciones de viga, muros).
2. Refinar cubierta con lámina `2024_22-102`.
3. Decidir incorporación del bloque escalera/ascensor (y ≈ 33–38).
4. Cargar sobrecargas reales de la lámina `2024_22-700`.
5. Descontar vanos en muros si se requiere mayor fidelidad.
6. Conectar `modelo_resultados.json` al visualizador Unity.

## Cómo correr

```bash
python -m venv .venv && .venv\Scripts\Activate.ps1
pip install -r requirements.txt
cd src/benchmark_3d
python analizar.py      # análisis + JSON
python verificar.py     # equilibrio + superposición + Euler-Bernoulli
python visualizar.py    # figura 3D
cd ../.. && python -m pytest tests/ -q
```
