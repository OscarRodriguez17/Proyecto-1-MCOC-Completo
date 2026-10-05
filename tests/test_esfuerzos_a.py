# -*- coding: utf-8 -*-
"""Tests de la pasada dedicada de DIAGRAMAS de viga (Edificio A):
  - Cierre del diagrama por viga y caso: |Vz(L) - (-Vzj)|, |My(L) - (-Myj)| y
    |Mz(L) - (-Mzj)| con tol < 1e-6·max(1,|valor|) (fórmulas exactas del
    enunciado; el cierre de Mz es el de la semana 06).
  - Equilibrio global de la pasada por caso: ΣR + Σaplicada ≈ 0.
  - Discriminador del peso propio: Wz(G)+Wz(Q)=Wz(GQ) por viga (la sobrecarga
    Q no arrastra peso propio). 0 fallas.
  - Signo de Mz: Mz(x) = Mz - Vy·x - Wy·x²/2 (dMz/dx = -Vy), no "+".
  - Contrato: el JSON de A (modelo_resultados.json) y el del visor sólido
    (edificio_solido.json) traen los coeficientes por viga y caso.
"""
import json
import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..",
                                "src", "benchmark_3d"))

import datos_edificio as d          # noqa: E402
import esfuerzos                    # noqa: E402
import analizar                     # noqa: E402

CASOS = ("G", "Q", "GQ", "EX", "EY")
CLAVES = {"L", "N", "Vy", "Vz", "T", "My", "Mz", "Wy", "Wz"}


@pytest.fixture(scope="module")
def diagramas():
    """verificar_diagramas una sola vez para toda la suite de A."""
    return esfuerzos.verificar_diagramas(verbose=False)


@pytest.fixture(scope="module")
def tmp_a(tmp_path_factory):
    """Directorio temporal de la suite de A: la suite NO debe pisar
    results/modelo_resultados.json, que es un artefacto de la entrega."""
    return tmp_path_factory.mktemp("edificio_a")


@pytest.fixture(scope="module")
def data_a(tmp_a):
    """Regenera el JSON de A (con el bloque esfuerzos) en el temporal."""
    return analizar.run_analisis(d, "modelo_resultados.json",
                                 "Edificio A - esfuerzos (test)",
                                 (0.0, 0.0), out_dir=str(tmp_a))


@pytest.fixture
def cierre_mz():
    """Pasada GQ de A con el DETALLE del cierre por viga (dMz/tolMz) y los
    esfuerzos del extremo j leídos de `localForce`.

    El detalle se captura dentro del fixture, mientras el modelo de la pasada
    sigue vivo: `verificar_diagramas` no expone `cierre` y el estado de OpenSees
    es global, así que el test no debe volver a preguntar por `localForce`.
    Fixture de función (no de módulo) justamente por eso: cada test que lo pide
    vuelve a resolver el caso GQ y no hereda el modelo de otro test.
    """
    import openseespy.opensees as ops
    tipo, _aplicada, w = esfuerzos._correr_caso("GQ", dat=d, offset=(0.0, 0.0))
    bloque = esfuerzos._extraer(tipo, w)
    cierre = esfuerzos._cierre_por_viga(tipo, bloque, w)
    j = {}
    for k in bloque:
        f = ops.eleResponse(int(k), "localForce")
        j[k] = (float(f[8]), float(f[10]), float(f[11]))   # Vzj, Myj, Mzj
    return bloque, cierre, j


def test_cierre_diagrama_por_viga_y_caso(diagramas):
    """En cada caso y cada viga: Vz(L)=-Vzj y My(L)=-Myj (tol 1e-6·max)."""
    verif, por_caso = diagramas
    for caso in CASOS:
        assert por_caso[caso], f"caso {caso} sin vigas"
        for tag_str, b in por_caso[caso].items():
            assert set(b) == CLAVES, f"viga {tag_str} {caso}: claves incompletas"
            assert b["Wy"] == 0.0
            assert b["L"] > 0
        assert verif[caso]["cierre_ok"], f"cierre FALLA en caso {caso}"
        assert verif[caso]["n_vigas"] == len(por_caso[caso]) == 228


def test_equilibrio_global_de_la_pasada(diagramas):
    """ΣR + Σaplicada ≈ 0 en los 5 casos de la pasada de diagramas."""
    verif, _ = diagramas
    for caso in CASOS:
        v = verif[caso]
        assert v["e_fx"] < 1e-3
        assert v["e_fy"] < 1e-3
        assert v["e_fz"] < 1e-3
        assert v["equilibrio"]


def test_discriminador_peso_propio_fuera_de_Q(diagramas):
    """Wz(G)+Wz(Q)=Wz(GQ) en TODAS las vigas: el PP vive en G/GQ, no en Q."""
    _, por_caso = diagramas
    fallas = []
    for k in por_caso["G"]:
        suma = por_caso["G"][k]["Wz"] + por_caso["Q"][k]["Wz"]
        gq = por_caso["GQ"][k]["Wz"]
        if abs(suma - gq) > 1e-9:
            fallas.append((k, suma, gq))
    assert fallas == [], f"Wz(G)+Wz(Q) != Wz(GQ) en {len(fallas)} vigas"


def test_formulas_evaluacion(diagramas):
    """Las fórmulas del enunciado son las aplicadas en x∈[0,L]."""
    verif, por_caso = diagramas
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


def test_semana06_cierre_mz_por_viga_y_caso(diagramas, cierre_mz):
    """Mz(L) = Mz - Vy·L - Wy·L²/2 = -Mz_j en TODAS las vigas.

    Es el cierre que faltaba: `_cierre_por_viga` solo cubría Vz y My, y por eso
    el error de signo de Mz pasó inadvertido. Ahora `cierre_ok` de los 5 casos
    exige los TRES cierres (dMz entra en `vigas_sin_cierre`).
    """
    verif, _ = diagramas
    for caso in CASOS:
        assert verif[caso]["cierre_ok"], f"cierre (Vz/My/Mz) FALLA en {caso}"
        assert verif[caso]["vigas_sin_cierre"] == []
    bloque, cierre, _j = cierre_mz
    assert len(cierre) == len(bloque) == 228
    malos = {e: (c["dMz"], c["tolMz"]) for e, c in cierre.items()
             if c["dMz"] > c["tolMz"]}
    assert malos == {}, f"Mz(L) != -Mz_j en {len(malos)} vigas de A (caso GQ)"


def test_semana06_signo_de_mz_es_menos(cierre_mz):
    """Guarda de regresión del SIGNO de Mz (semana 06).

    A tiene 14 vigas con Vy != 0 (la torsión del diafragma rígido reparte Vy),
    así que el signo de Vy·x SÍ se puede discriminar contra `localForce`:
    con "+Vy·x" el cierre se va a 6–113 veces la tolerancia, con "-Vy·x" queda
    en ~1e-10 de ella.
    """
    bloque, _cierre, j = cierre_mz
    con_vy = {k: b for k, b in bloque.items() if abs(b["Vy"]) > 1e-9}
    assert con_vy, ("sin vigas con Vy != 0 en A: este test no distinguiría "
                    "el signo de Mz y no sirve como guarda")

    # la viga más sensible:|max(Vy·L)| / max(1,|Mzj|)
    peor = max(con_vy, key=lambda k: abs(con_vy[k]["Vy"] * con_vy[k]["L"]))

    def d(L_old):
        """|Mz(L) - (-Mz_j)| / max(1,|Mz_j|) con la fórmula dada por L_old."""
        b = bloque[peor]
        return abs(L_old(b) + j[peor][2]) / max(1.0, abs(j[peor][2]))

    nuevo = d(lambda b: b["Mz"] - b["Vy"] * b["L"] - b["Wy"] * b["L"] ** 2 / 2.0)
    viejo = d(lambda b: b["Mz"] + b["Vy"] * b["L"] + b["Wy"] * b["L"] ** 2 / 2.0)
    assert nuevo < 1e-6, f"Mz con el signo correcto no cierra (rel {nuevo:.3e})"
    assert viejo > 1.0, (f"el signo viejo cerraría ({viejo:.3e}): la guarda "
                         f"deja de distinguir las dos fórmulas")


def test_json_a_lleva_esfuerzos(data_a):
    """El JSON de A exportado por run_analisis incluye el bloque esfuerzos
    (5 casos; por viga las claves mínimas para evaluar cualquier punto)."""
    data = data_a
    assert "esfuerzos" in data
    for caso in CASOS:
        assert caso in data["esfuerzos"], f"falta caso {caso} en esfuerzos"
        for tag_str, b in data["esfuerzos"][caso].items():
            assert set(b) == CLAVES
            assert b["L"] > 0
    tags_viga = {str(el["tag"]) for el in data["elementos"]
                 if el["tipo"] in ("vigas_x", "vigas_y")}
    assert set(data["esfuerzos"]["G"]) == tags_viga


def test_json_solido_trae_coeficientes(data_a, tmp_a, tmp_path):
    """El JSON del visor sólido trae el bloque esfuerzos de A (por viga/caso)."""
    src = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "src"))
    sys.path.insert(0, os.path.join(src, "benchmark_3d"))
    import exportar_unity_solido
    salida = tmp_path / "solido"
    exportar_unity_solido.exportar(
        offset_b_x=60.0, out_dir=str(salida),
        dat_a=str(tmp_a / "modelo_resultados.json"))
    with open(str(salida / "edificio_solido.json"), encoding="utf-8") as f:
        solido = json.load(f)
    ed_a = solido["edificios"][0]
    assert ed_a["bloque"].startswith("Edificio A")
    assert set(ed_a["esfuerzos"]) == set(CASOS)
    tags_viga_a = {str(el["tag"]) for el in data_a["elementos"]
                   if el["tipo"] in ("vigas_x", "vigas_y")}
    assert set(ed_a["esfuerzos"]["G"]) == tags_viga_a
    solid_vigas = {str(el["tag"]) for el in ed_a["elementos"]
                   if el["tipo"] in ("vigas_x", "vigas_y")}
    assert tags_viga_a <= solid_vigas