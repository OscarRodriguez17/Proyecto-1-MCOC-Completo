"""
Tests del contrato JSON del COMPLEJO (results/edificio_completo.json).

El archivo lo genera src/benchmark_3d/fusionar.py corriendo los analisis de
ambos edificios. Estos tests protegen lo que consumen los visualizadores
(PNG/HTML/Unity) del complejo. Ejecutar: pytest tests/test_complejo.py -v
"""
import json
import os

RUTA_JSON = os.path.join(os.path.dirname(__file__), "..", "results",
                         "edificio_completo.json")


def _cargar():
    assert os.path.isfile(RUTA_JSON), "falta results/edificio_completo.json"
    with open(RUTA_JSON, encoding="utf-8") as f:
        return json.load(f)


class TestEstructuraGlobal:
    def test_parsea_y_metadatos(self):
        data = _cargar()
        assert data["proyecto"]
        assert data["unidades"].startswith("m")
        assert data["version"] == 1
        assert set(data["config"]) == {"offset_b_x_m", "tag_offset_b"}

    def test_dos_edificios_a_y_b(self):
        data = _cargar()
        ids = [e["id"] for e in data["edificios"]]
        assert ids == ["A", "B"]
        assert data["edificios"][0]["esquema"] == "A"
        assert data["edificios"][1]["esquema"] == "B"

    def test_totales_presentes(self):
        data = _cargar()
        t = data["totales"]
        assert set(t["A"]) >= {"G_kN", "Q_kN", "GQ_kN", "V_EX_kN", "V_EY_kN"}
        assert set(t["B"]) >= {"PP_kN", "SC_kN"}


class TestEdificioA:
    def test_esquema_a_completo(self):
        data = _cargar()
        a = data["edificios"][0]["json"]
        assert isinstance(a["nodos"], dict)
        assert a["nodos"] and a["elementos"]
        for caso in ("G", "Q", "GQ", "EX", "EY"):
            assert caso in a["resultados"]

    def test_conectividad_a(self):
        data = _cargar()
        a = data["edificios"][0]["json"]
        for el in a["elementos"]:
            assert str(el["ni"]) in a["nodos"]
            assert str(el["nj"]) in a["nodos"]


class TestEdificioB:
    def test_renumeracion_tags(self):
        data = _cargar()
        b = data["edificios"][1]["json"]
        tag_off = data["config"]["tag_offset_b"]
        ids = [n["id"] for n in b["nodos"]]
        assert min(ids) >= tag_off                    # sin colision con A
        assert len(ids) == b["n_nodos"]

    def test_conectividad_b_renumerada(self):
        data = _cargar()
        b = data["edificios"][1]["json"]
        ids = {n["id"] for n in b["nodos"]}
        for el in b["elementos"]:
            assert el["ni"] in ids
            assert el["nj"] in ids
            assert el["id"] >= data["config"]["tag_offset_b"]

    def test_offset_x_apartado_de_a(self):
        data = _cargar()
        off = data["config"]["offset_b_x_m"]
        a = data["edificios"][0]["json"]
        b = data["edificios"][1]["json"]
        x_a_max = max(v["x"] for v in a["nodos"].values())
        x_b_min = min(n["x"] for n in b["nodos"])
        assert x_b_min >= off + 10.0                    # B parte lejos en +X
        assert x_b_min > x_a_max                        # sin solape
        for n in b["nodos"]:
            assert n["x"] >= off + 10.0
            assert set(n) >= {"id", "x", "y", "z", "ux", "uy", "uz"}