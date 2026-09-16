# Benchmark 3D — Edificio de Ingeniería (planos 2017_67, Edificio A)

Modelo espacial lineal-elástico en OpenSeesPy del **Edificio A** (un solo
edificio, retícula 45 × 16.15 m **+ anexo/voladizo metálico I'–J de 5 m en los
2 niveles superiores**). Corregido contra `docs/ficha_geometria.md` y los
planos de corte (300/310) y planta (102/103).

Ejecutar: `.venv\Scripts\python.exe src\benchmark_3d\analizar.py` → `results/modelo_resultados.json`
Tests:    `.venv\Scripts\python.exe -m pytest tests\ -v`

## Archivos

| Archivo | Contenido |
|---|---|
| `datos_edificio.py` | Geometría, secciones, materiales y cargas del edificio |
| `construir.py`      | Nodos, elementos, diafragmas rígidos (`-ndm 3 -ndf 6`) |
| `voladizos.py`      | Inyección ADITIVA del voladizo trasero A3 (verificado en DXF 102/103/303) |
| `cargas.py`         | Áreas tributarias, pesos propios, patrón sísmico V=α·W |
| `analizar.py`       | Casos G/Q/GQ/EX/EY, verificaciones, export JSON (integra `voladizos`) |
| `verificar_cargas.py` | Cross-check del JSON contra la geometría |
| `visualizar.py`     | Figura 3D del modelo |

Salida JSON: `results/modelo_resultados.json` (contrato OpenSees → Unity).

## Datos de los planos

- Retícula X (plano 101): E–I' = 0/10/20/30/40/45 m (luces 10·4 + 5) + **J = 50 m**.
- **Anexo metálico I'–J (5 m)**: **P.M. 300×300×20** (columnas de acero en la
  fachada J, ejes Y 3/2/1) y **V.M. 300×300×5** (vigas de acero), en los
  **2 niveles superiores** (+7.87 y +11.83 = techo), según corte 1-1' (plano 300),
  elevación EJE J (plano 310) y plantas 102/103 (config. `ANEXO` en
  `datos_edificio.py`).
- Retícula Y: 0 / 2.31 / 7.25 / 12.25 / 16.15 m.
- Niveles: base −4.21 · radier −0.05 · +3.91 · +7.87 · techo +11.83 m
  (primer piso 4.16 m, resto 3.96 m). +1.98/+5.94 son descansos de escalera.
- Pilares P.70x70 en ejes E…I' × {3, 2, 1} (18 por piso × 4 = 72) + P.M. de
  acero en J (3); en ejes 2a y 1'' solo vigas transversales + muros cortos.
- Vigas V.60/80 en toda la retícula hasta I' con **sección compuesta T/L** (losa
  colaborante e=15 cm, ancho efectivo ACI 318-19 Tabla 6.3.2.1); en el tramo
  I'–J y fachada J, **V.M. 300×300×5** de acero. Muros RLE-MURO: ME-32, MI-32,
  MI-12, M1c-E, M2a (ver WALLS en datos_edificio.py), repetidos en todos los
  niveles (pendiente verificar por planta 102/103).
- Hormigón **G35** (lámina 100): f'c=35 MPa, Ec=4700√f'c≈27,806 MPa [ACI].
  Acero estructural: E=200 GPa (config `E_STEEL`).
- Cargas (lámina 700, aula típica): q_G = 0.15·25 + 2.55 = 6.30 kN/m²;
  q_Q = 2.50 kN/m². El área del voladizo se carga **solo en los 2 niveles del
  anexo** (el resto en los 4 pisos). Sismo paramétrico α = 0.10 (`ALPHA_EQ`).

## Idealizaciones

1. Lineal elástico SI; base empotrada en −4.21 (un solo plano de fundación;
   escalones −8.42/−9.32 fuera de alcance, documentado).
2. Losas no se mallan: diafragmas rígidos + cargas nodales equivalentes.
3. Muros = columnas anchas equivalentes en su centroide, conectadas al
   diafragma; J torsional de San Venant `(1/3)L·t³(1−0.63t/L)`; deformación
   por cortante incluida vía áreas de cortante A/1.2 (opción `-shear`).
4. Cada panel reparte su carga en ¼ entre las 4 vigas de borde
   (conservación exacta, ver tests). El panel voladizo I'–J solo participa en
   los niveles del anexo (área tributaria por nivel).
5. Sismo pseudoestático Fi ∝ Wi·hi aplicado a nodos esclavos.
6. Los ejes J (50 m) y sus nodos solo existen en los niveles del anexo;
   el acero aporta su propio peso con densidad 78.5 kN/m³.
7. Voladizo trasero (eje A3, y<0): inyectado por `voladizos.py` tras
   `construir.construir()` (REGLA DE ORO: no toca el modelo base; tags
   estrictamente por encima por `ops.getNodeTags/getEleTags`).
   **Verificado contra los DXF** (planos 2017_67-102, -103 y -303): vuelo a
   **−4.30 m** desde el eje de la viga perimetral A3, nervaduras (A3→punta) y
   viga de borde de punta (G↔H) en **HORMIGÓN V.60/80** solo en los ejes
   **G y H**, niveles 3 y 4; postes de punta en **P.M. 300×300×20**
   (cajones RLA de la elevación A3). Marco metálico secundario sobre la punta
   (visto en la elevación 303) NO modelado: falta el corte acotado.

## Verificaciones (impresas al correr + automatizadas en tests)

1. Conservación tributaria exacta (error < 1e-11 kN).
2. Equilibrio por caso ≤ ~6e-10 kN.
3. Superposición R(G)+R(Q)=R(GQ) ≤ ~5e-12 kN.

## Resultados (G35, α = 0.10, Sesión 17b: anexo I'–J + voladizo trasero verificado DXF)

| Caso | ΣFz reacción / V base | Despl. techo | Deriva máx |
|---|---|---|---|
| G   | 45,189.9 kN | – | – |
| Q   | 7,671.3 kN  | – | – |
| GQ  | 52,861.1 kN | – | – |
| EX  | V = 4,519.0 kN | ux = 8.45 mm | 0.653 mrad (piso 3) |
| EY  | V = 4,519.0 kN | uy = 2.34 mm | 0.202 mrad (piso 4) |

Área losa: principal 726.75 m² × 4 pisos + voladizo 80.75 m² × 2 pisos. X más
flexible que Y: en Y trabajan los muros largos E/I en su plano. El anexo añade
≈1,553 kN de reacción G y +0.32 mm de ux en techo vs. Sesión 6 (sin anexo).
El voladizo trasero (nervaduras G/H V.60/80 en hormigón + postes P.M. de
acero, y=−4.30 m) añade ≈**+460 kN** de G vs. la Sesión 17a (versión tentativa
en acero F..I2); equilibrio y superposición ≤ ~1e-11.

## Pendientes

- Verificar muros por nivel en plantas 102/103 (se mantienen en todos los niveles).
- Configurar `.venv` propio del nuevo proyecto.
- Comparar reacciones/derivas contra modelo SAP2000 (verificación cruzada).
