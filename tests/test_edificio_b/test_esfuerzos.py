# -*- coding: utf-8 -*-
"""Tests de la pasada dedicada de DIAGRAMAS de viga (Edificio B):
  - Cierre del diagrama por viga y caso: |Vz(L) - (-Vzj)|, |My(L) - (-Myj)| y
    |Mz(L) - (-Mzj)| con tol < 1e-6·max(1,|valor|) (fórmulas exactas del
    enunciado; el cierre de Mz es el de la semana 06).
  - Equilibrio global de la pasada por caso: ΣR + Σaplicada ≈ 0.
  - Contrato: el JSON de B y el JSON del visor sólido traen los coeficientes
    por viga y caso.
"""
import json
import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", "src"))

from edificio_b import esfuerzos
from edificio_b import analizar

CASOS = ("G", "Q", "GQ", "EX", "EY")
CLAVES = {"L", "N", "Vy", "Vz", "T", "My", "Mz", "Wy", "Wz"}


@pytest.fixture
def cierre_mz():
    """Pasada GQ de B con el DETALLE del cierre por viga (dMz/tolMz) y los
    esfuerzos del extremo j leídos de `localForce`.

    El detalle se captura dentro del fixture, mientras el modelo de la pasada
    sigue vivo: `verificar_diagramas` no expone `cierre` y el estado de OpenSees
    es global, así que el test no debe volver a preguntar por `localForce`.
    Fixture de función (no de módulo) justamente por eso: cada test que lo pide
    vuelve a resolver el caso GQ y no hereda el modelo de otro test.
    """
    import openseespy.opensees as ops
    M, _aplicada, w = esfuerzos._correr_caso("GQ")
    bloque = esfuerzos._extraer(M, w)
    cierre = esfuerzos._cierre_por_viga(bloque, w)
    j = {}
    for k in bloque:
        f = ops.eleResponse(int(k), "localForce")
        j[k] = (float(f[8]), float(f[10]), float(f[11]))   # Vzj, Myj, Mzj
    return bloque, cierre, j


def test_cierre_diagrama_por_viga_y_caso():
    """En cada caso y cada viga: Vz(L)=-Vzj y My(L)=-Myj (tol 1e-6·max)."""
    verif, por_caso = esfuerzos.verificar_diagramas(verbose=False)
    for caso in CASOS:
        assert por_caso[caso], f"caso {caso} sin vigas"
        for tag_str, b in por_caso[caso].items():
            assert set(b) == CLAVES, f"viga {tag_str} {caso}: claves incompletas"
            assert b["Wy"] == 0.0
            assert b["L"] > 0
        assert verif[caso]["cierre_ok"], f"cierre FALLA en caso {caso}"


def test_equilibrio_global_de_la_pasada():
    """ΣR + Σaplicada ≈ 0 en los 5 casos de la pasada de diagramas."""
    verif, _ = esfuerzos.verificar_diagramas(verbose=False)
    for caso in CASOS:
        v = verif[caso]
        assert v["e_fx"] < 1e-3
        assert v["e_fy"] < 1e-3
        assert v["e_fz"] < 1e-3
        assert v["equilibrio"]


def test_formulas_evaluacion():
    """Las fórmulas del enunciado son las aplicadas en x∈[0,L]."""
    verif, por_caso = esfuerzos.verificar_diagramas(verbose=False)
    caso = "GQ"
    b = next(iter(por_caso[caso].values()))
    L, N, Vz, My, Wz = b["L"], b["N"], b["Vz"], b["My"], b["Wz"]
    x = 0.42 * L
    Nx = -N
    Vzx = Vz + Wz * x
    Myx = My + Vz * x + Wz * x * x / 2.0
    assert abs(Nx - (-N)) < 1e-9
    assert abs(Vzx - (Vz + Wz * x)) < 1e-9
    assert abs(Myx - (My + Vz * x + Wz * x * x / 2.0)) < 1e-9
    v = verif[caso]
    assert v["equilibrio"]


def test_semana06_cierre_mz_por_viga_y_caso(cierre_mz):
    """Mz(L) = Mz - Vy·L - Wy·L²/2 = -Mz_j en TODAS las vigas.

    Es el cierre que faltaba: `_cierre_por_viga` solo cubría Vz y My, y por eso
    el error de signo de Mz pasó inadvertido. Ahora `cierre_ok` de los 5 casos
    exige los TRES cierres (dMz entra en `vigas_sin_cierre`).
    """
    verif, por_caso = esfuerzos.verificar_diagramas(verbose=False)
    for caso in CASOS:
        assert por_caso[caso], f"caso {caso} sin vigas"
        assert verif[caso]["cierre_ok"], f"cierre (Vz/My/Mz) FALLA en {caso}"
        assert verif[caso]["vigas_sin_cierre"] == []
    bloque, cierre, _j = cierre_mz
    assert len(cierre) == len(bloque) == 215
    malos = {e: (c["dMz"], c["tolMz"]) for e, c in cierre.items()
             if c["dMz"] > c["tolMz"]}
    assert malos == {}, f"Mz(L) != -Mz_j en {len(malos)} vigas de B (caso GQ)"


def test_semana06_mz_de_b_es_constante(cierre_mz):
    """En B el diagrama de Mz de las vigas es CONSTANTE y cierra en -Mz_j.

    Las vigas de B no tienen carga en local y (Wy = 0) ni torsión del diafragma
    que les reparta Vy, así que Vy = 0 en las 215 vigas y los 5 casos. Por eso
    el cierre de Mz de B comprueba Mz_i = -Mz_j (diagrama constante) pero NO
    puede discriminar el signo de Vy·x: esa guarda vive en el test equivalente
    del Edificio A, que sí tiene 14 vigas con Vy != 0.
    """
    bloque, _cierre, j = cierre_mz
    con_vy = [k for k, b in bloque.items() if abs(b["Vy"]) > 1e-9]
    assert con_vy == [], (f"{len(con_vy)} vigas de B con Vy != 0: este test "
                         f"ya no describe el caso y hay que repetir el de A")
    assert all(b["Wy"] == 0.0 for b in bloque.values())
    # Mz(x) = Mz - Vy·L - Wy·L²/2 = Mz  con Wy = Vy = 0  ->  Mz_i = -Mz_j
    malos = {k: (b["Mz"], j[k][2]) for k, b in bloque.items()
             if abs(b["Mz"] + j[k][2]) > 1e-6 * max(1.0, abs(j[k][2]))}
    assert malos == {}, f"Mz_i != -Mz_j en {len(malos)} vigas de B"


def test_json_b_lleva_esfuerzos():
    """El JSON de B exportado por analizar incluye el bloque esfuerzos
    (5 casos; por viga las claves mínimas para evaluar cualquier punto)."""
    data = analizar.correr(verbose=False)["data"]
    assert "esfuerzos" in data
    for caso in CASOS:
        assert caso in data["esfuerzos"], f"falta caso {caso} en esfuerzos"
        for tag_str, b in data["esfuerzos"][caso].items():
            assert set(b) == CLAVES
            assert b["L"] > 0
    tags_viga = {str(el["id"]) for el in data["elementos"]
                 if el["tipo"] == "viga"}
    assert set(data["esfuerzos"]["G"]) == tags_viga


def test_json_solido_trae_coeficientes(tmp_path):
    """El JSON del visor sólido trae el bloque esfuerzos de B (por viga/caso).

    Se exporta a un TEMPORAL: la suite no debe reescribir
    results/edificio_solido.json, que es un artefacto de la entrega."""
    src = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "src"))
    sys.path.insert(0, os.path.join(src, "benchmark_3d"))
    import exportar_unity_solido
    salida = tmp_path / "solido"
    exportar_unity_solido.exportar(offset_b_x=60.0, out_dir=str(salida))
    with open(str(salida / "edificio_solido.json"), encoding="utf-8") as f:
        solido = json.load(f)
    ed_b = solido["edificios"][1]
    assert ed_b["bloque"].startswith("Edificio B")
    assert set(ed_b["esfuerzos"]) == set(CASOS)
    tags_viga = {str(el["tag"]) for el in ed_b["elementos"]
                 if el["tipo"] in ("vigas_x", "vigas_y")}
    assert set(ed_b["esfuerzos"]["G"]) == tags_viga