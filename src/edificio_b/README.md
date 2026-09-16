# benchmark_3d — Modelo OpenSeesPy del Edificio B

Modelo 3D lineal-elástico (6 GDL/nodo) armado desde los planos DXF.

## Archivos

| Archivo | Rol |
|---------|-----|
| `datos_edificio.py` | Geometría verificada, material y secciones (dataset) |
| `construir.py` | Ensambla nodos, pilares, muros (columna ancha), vigas, diafragmas y apoyos |
| `cargas.py` | Peso propio + sobrecarga |
| `analizar.py` | Corre el análisis y exporta `results/modelo_resultados.json` |
| `verificar.py` | Equilibrio, superposición y test Euler-Bernoulli (<1e−10) |
| `visualizar.py` | Figura 3D del modelo |

## Convenciones

- Unidades **m, kN**. Gravedad en −Z.
- Ejes locales explícitos (`vecxz`): transf 1 verticales, 2 vigas-X, 3 vigas-Y.
- Diafragma rígido por piso con `constraints('Transformation')`.
- Muros como columna ancha + brazos rígidos; bloque escalera/ascensor incluido.
- Base empotrada en z = −4.01 (fundaciones ignoradas).

Ver `../../docs/ficha_geometria.md` para el detalle de la geometría y los
supuestos, y `../../docs/bitacora.md` para el estado del proyecto.
