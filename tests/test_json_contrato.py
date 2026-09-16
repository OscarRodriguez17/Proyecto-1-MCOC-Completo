"""
Tests del contrato JSON OpenSees -> Unity (results/modelo_resultados.json).

Protege el consumidor Unity: si el json cambia y estos tests fallan, Unity
romperia. Ejecutar: pytest tests/test_json_contrato.py -v
"""
import json
import os

RUTA_JSON = os.path.join(os.path.dirname(__file__), "..", "results",
                         "modelo_resultados.json")

TIPOS_VALIDOS = {"column", "wall", "vigas_x", "vigas_y", "aspa"}
CASOS = ("G", "Q", "GQ", "EX", "EY")


def _cargar():
    with open(RUTA_JSON, encoding="utf-8") as f:
        return json.load(f)


class TestEstructuraGlobal:
    def test_existe_y_parsea(self):
        assert os.path.isfile(RUTA_JSON)
        data = _cargar()
        assert data["proyecto"]
        assert data["unidades"].startswith("m")

    def test_resultados_presentes(self):
        data = _cargar()
        for caso in CASOS:
            assert caso in data["resultados"]


class TestApoyos:
    def test_existen_apoyos(self):
        data = _cargar()
        assert len(data["apoyos"]) > 0

    def test_tags_referencian_nodos_existentes(self):
        data = _cargar()
        nodos = data["nodos"]
        for ap in data["apoyos"]:
            assert str(ap["tag"]) in nodos
            assert nodos[str(ap["tag"])]["nivel"] == 0

    def test_todos_empotrados(self):
        data = _cargar()
        for ap in data["apoyos"]:
            assert ap["tipo"] == "empotrado"
            assert ap["constraint"] == [1, 1, 1, 1, 1, 1]

    def test_todo_apoyo_es_base_de_columna_o_muro(self):
        data = _cargar()
        ap_tags = {ap["tag"] for ap in data["apoyos"]}
        bases = set()
        for el in data["elementos"]:
            if el["tipo"] not in ("column", "wall"):
                continue
            ni = data["nodos"][str(el["ni"])]
            nj = data["nodos"][str(el["nj"])]
            base = el["nj"] if ni["z"] > nj["z"] else el["ni"]
            if base in ap_tags:
                bases.add(base)
        assert ap_tags == bases


class TestColumnasEnMalla:
    """Regression: los nodos de muro no deben arrastrar columnas fuera de la malla.

    Excepcion legitima: las columnas del voladizo trasero (postes en nodos
    "punta_voladizo") y del voladizo metalico de piso 1 en el eje F (poste en
    nodos "punta_vol_f") nacen en nodos en y < 0, fuera de la reticula Y (ver
    voladizos.py). Se validan aparte (verticales, en y < 0).
    """

    def test_columnas_verticales_y_en_malla(self):
        data = _cargar()
        nodos = {int(k): v for k, v in data["nodos"].items()}
        grid_x = set(data["geometria"]["grid_x"].values())
        grid_y = set(data["geometria"]["grid_y"].values())
        fuera = {int(k) for k, v in data["nodos"].items()
                 if v.get("rol") in ("punta_voladizo", "punta_vol_f")}
        voladizos = 0
        for el in data["elementos"]:
            if el["tipo"] != "column":
                continue
            ni, nj = nodos[el["ni"]], nodos[el["nj"]]
            assert abs(ni["x"] - nj["x"]) < 1e-9
            assert abs(ni["y"] - nj["y"]) < 1e-9
            assert nj["z"] - ni["z"] > 1.0
            if el["ni"] in fuera or el["nj"] in fuera:
                voladizos += 1
                assert ni["y"] < 0.0
                continue
            assert ni["x"] in grid_x
            assert ni["y"] in grid_y
        assert voladizos > 0


class TestVoladizo:
    """Contrato: el voladizo trasero inyectado debe aparecer en el JSON."""

    def test_punta_voladizo_en_nodos(self):
        data = _cargar()
        puntas = [n for n in data["nodos"].values()
                  if n.get("rol") == "punta_voladizo"]
        assert len(puntas) > 0
        for n in puntas:
            assert n["nivel"] in (3, 4)
            assert n["y"] < 0.0

    def test_vigas_vm_y_postes_exportados(self):
        data = _cargar()
        nodos = {int(k): v for k, v in data["nodos"].items()}
        vigas_vm = [e for e in data["elementos"] if e["tipo"] == "vigas_y"]
        postes = [e for e in data["elementos"] if e["tipo"] == "column"]
        assert len(vigas_vm) > 0 and len(postes) > 0
        punta_nivel = {int(k) for k, v in data["nodos"].items()
                       if v.get("rol") == "punta_voladizo"}
        postes_punta = [e for e in postes
                        if e["ni"] in punta_nivel or e["nj"] in punta_nivel]
        assert len(postes_punta) > 0
        for e in postes_punta:
            assert abs(nodos[e["ni"]]["y"]) < 1e-9 or nodos[e["ni"]]["y"] < 0.0


class TestAspasYVoladizoF:
    """Contrato: aspa de arriostramiento y el voladizo metalico de piso 1 en el
    eje F (cordones + poste + diagonal) exportados al JSON. La extension de
    hormigon trasera ya NO esta en el modelo."""

    def test_aspas_presentes(self):
        data = _cargar()
        aspas = [e for e in data["elementos"] if e["tipo"] == "aspa"]
        assert len(aspas) == 4                    # 2 voladizo trasero + 2 eje F
        nodos = data["nodos"]
        for a in aspas:
            ni, nj = nodos[str(a["ni"])], nodos[str(a["nj"])]
            assert nj["z"] > ni["z"]

    def test_punta_vol_f_en_nodos(self):
        data = _cargar()
        puntas = [n for n in data["nodos"].values()
                  if n.get("rol") == "punta_vol_f"]
        assert len(puntas) == 4
        pos = {(round(p["x"], 9), round(p["y"], 9), round(p["z"], 9))
               for p in puntas}
        assert pos == {(10.0, -4.30, -0.05), (10.0, -4.30, 3.91),
                       (17.5, -4.30, -0.05), (17.5, -4.30, 3.91)}
        for p in puntas:
            assert p["nivel"] in (1, 2)

    def test_poste_y_cordones_del_eje_F(self):
        data = _cargar()
        nodos = {int(k): v for k, v in data["nodos"].items()}
        puntas = {int(k) for k, v in data["nodos"].items()
                  if v.get("rol") == "punta_vol_f"}
        poste = [e for e in data["elementos"]
                 if e["tipo"] == "column"
                 and (e["ni"] in puntas or e["nj"] in puntas)
                 and e["ni"] in puntas and e["nj"] in puntas]
        assert len(poste) == 2                    # poste P.M. en cada punta
        cordones = [e for e in data["elementos"]
                    if e["tipo"] == "vigas_y"
                    and (e["ni"] in puntas or e["nj"] in puntas)]
        assert len(cordones) == 4                 # nervaduras V.M. (F y borde) x2
        for c in cordones:
            nj = nodos[c["nj"]]
            assert nj["rol"] == "punta_vol_f"

    def test_no_existe_extension(self):
        data = _cargar()
        roles = {n.get("rol") for n in data["nodos"].values()}
        assert "base_ext" not in roles
        assert "columna_ext" not in roles
        for n in data["nodos"].values():
            # x 34.5/40.5 con y<0 era geometria exclusiva de la extension
            # (el voladizo trasero usa x 30/40 en la misma zona y es legitimo)
            if n["y"] < 0.0 and n["x"] in (34.5, 40.5):
                raise AssertionError(f"geometria de la extension presente: {n}")


class TestCargas:
    """Contrato del bloque 'cargas' (visualizacion de flechas en Unity y verificacion)."""

    def test_estructura_presente(self):
        data = _cargar()
        c = data.get("cargas")
        assert c is not None
        assert set(c) >= {"vigas", "peso_columnas_muros",
                          "pesos_por_nivel", "sismo"}
        for caso in ("EX", "EY"):
            assert set(c["sismo"][caso]) == {"V", "F_por_nivel"}

    def test_vigas_referencian_elementos_y_nodos(self):
        data = _cargar()
        botetipos = {
            "vigas_x": {el["tag"] for el in data["elementos"]
                        if el["tipo"] == "vigas_x"},
            "vigas_y": {el["tag"] for el in data["elementos"]
                        if el["tipo"] == "vigas_y"},
        }
        nodos = data["nodos"]
        for vg in data["cargas"]["vigas"]:
            assert vg["tag"] in botetipos[vg["tipo"]]
            assert str(vg["ni"]) in nodos
            assert str(vg["nj"]) in nodos
            assert vg["L"] > 0.0
            assert vg["G"] >= 0.0 and vg["Q"] >= 0.0

    def test_vigas_mas_columnas_muros_igual_g_aplicada(self):
        data = _cargar()
        c = data["cargas"]
        suma_g = sum(vg["G"] for vg in c["vigas"])
        apl = abs(data["resultados"]["G"]["aplicada"]["fz"])
        assert abs(suma_g + c["peso_columnas_muros"] - apl) < 1e-6 * apl

    def test_vigas_q_suma_a_q_aplicada(self):
        data = _cargar()
        c = data["cargas"]
        suma_q = sum(vg["Q"] for vg in c["vigas"])
        apl = abs(data["resultados"]["Q"]["aplicada"]["fz"])
        assert abs(suma_q - apl) < 1e-6 * apl

    def test_pesos_por_nivel_suman_a_g(self):
        data = _cargar()
        c = data["cargas"]
        suma = sum(c["pesos_por_nivel"].values())
        apl = abs(data["resultados"]["G"]["aplicada"]["fz"])
        assert abs(suma - apl) < 1e-6 * apl

    def test_sismo_v_suma_y_equilibrio(self):
        data = _cargar()
        c = data["cargas"]
        for caso, comp in (("EX", "fx"), ("EY", "fy")):
            s = c["sismo"][caso]
            assert abs(s["V"] - sum(s["F_por_nivel"].values())) < 1e-6 * s["V"]
            apl = abs(data["resultados"][caso]["aplicada"][comp])
            assert abs(abs(s["V"]) - apl) < 1e-6 * max(apl, 1.0)
            reac = data["resultados"][caso]["reacciones_totales"][comp]
            assert abs(reac + s["V"]) < 1e-6 * max(abs(reac), 1.0)


class TestElementos:
    def test_tipos_validos(self):
        data = _cargar()
        tipos = {el["tipo"] for el in data["elementos"]}
        assert tipos.issubset(TIPOS_VALIDOS)

    def test_conectividad_coherente(self):
        data = _cargar()
        nodos = data["nodos"]
        for el in data["elementos"]:
            assert str(el["ni"]) in nodos
            assert str(el["nj"]) in nodos

    def test_maestro_por_nivel_y_resultados(self):
        data = _cargar()
        niveles = data["geometria"]["niveles_z"]
        maestros = {int(v["nivel"]): v["z"] for v in data["nodos"].values()
                    if v.get("rol") == "maestro_diafragma"}
        assert len(maestros) == len(niveles)
        for i, z in enumerate(niveles):
            assert abs(maestros[i] - z) < 1e-9
        for caso in CASOS:
            res = data["resultados"][caso]
            assert set(res["desplazamientos_maestro"]) == set(str(i) for i in range(len(niveles)))
