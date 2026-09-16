using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCOC.Unity
{
    /// <summary>
    /// Visualizador del COMPLEJO (Edificios A y B) lado a lado.
    /// Lee `edificio_completo.json` (generado por fusionar.py) y dibuja ambos
    /// edificios como modelo estructural de línea, respetando el offset X de B.
    ///
    /// Coordenadas Unity: X = x (m), Y = z (altura), Z = y (m).
    ///
    /// Montaje: colocar en un GameObject de la escena y asignar `jsonRuta`
    /// (relativa a StreamingAssets). Requiere el paquete newtonsoft-json.
    /// </summary>
    public class UnityComplejo : MonoBehaviour
    {
        [Header("Carga JSON")]
        [Tooltip("Ruta relativa a Assets/StreamingAssets")]
        public string jsonRuta = "edificio_completo.json";

        [Header("Estilo")]
        public float grosorLinea = 0.22f;
        public Color colorColumnas = new Color(0.36f, 0.39f, 0.44f);
        public Color colorVigas = new Color(0.23f, 0.51f, 0.96f);
        public Color colorMuros = new Color(0.94f, 0.27f, 0.27f);
        public Color colorAcero = new Color(0.90f, 0.32f, 0.00f);

        /// <summary>Centro del conjunto (coords Unity) tras renderizar.</summary>
        public Vector3 CentroTotal { get; private set; }

        public void Start()
        {
            if (string.IsNullOrEmpty(jsonRuta) || jsonRuta.StartsWith("UNSET"))
            {
                Debug.LogError("[UnityComplejo] jsonRuta sin asignar");
                return;
            }

            string path = Path.Combine(Application.streamingAssetsPath, jsonRuta);
            if (!File.Exists(path))
            {
                Debug.LogError("[UnityComplejo] no existe " + path +
                    ". Copiar results/edificio_completo.json a StreamingAssets.");
                return;
            }

            string json = File.ReadAllText(path);
            ComplejoModelo complejo;
            try
            {
                complejo = JsonConvert.DeserializeObject<ComplejoModelo>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[UnityComplejo] error parseando JSON: " + e.Message);
                return;
            }

            RenderComplejo(complejo);
        }

        private void RenderComplejo(ComplejoModelo complejo)
        {
            Material mat = new Material(Shader.Find("Sprites/Default"));
            string titulo = string.Format("COMPLEJO — A y B · offset B en X = {0:0.00} m",
                complejo.config != null ? complejo.config.offset_b_x_m : 0.0);

            // Pasada 1: recolectar segmentos por edificio y bbox global.
            var porEdificio = new List<KeyValuePair<EdificioComplejo, List<(Vector3, Vector3, int)>>>();
            var todos = new List<Vector3>();
            foreach (EdificioComplejo ed in complejo.edificios)
            {
                if (ed == null || ed.json == null) continue;
                var segs = ExtraerSegmentos(ed);
                porEdificio.Add(new KeyValuePair<EdificioComplejo, List<(Vector3, Vector3, int)>>(ed, segs));
                foreach (var s in segs) { todos.Add(s.Item1); todos.Add(s.Item2); }
            }

            // Centro del conjunto en coords Unity (X=x, Y=z, Z=y).
            var cTotal = Vector3.zero;
            if (todos.Count > 0)
            {
                var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (var p in todos)
                { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                cTotal = (min + max) * 0.5f;
            }
            CentroTotal = cTotal;
            float alcance = todos.Count > 0 ? cTotal.magnitude + 30f : 120f;

            // Pasada 2: crear cada edificio desplazando el root a -CentroTotal,
            // de modo que el conjunto quede centrado en el origen de la escena.
            foreach (var kv in porEdificio)
            {
                var ed = kv.Key;
                var segs = kv.Value;

                var root = new GameObject("EDIFICIO " + ed.id);
                root.transform.SetParent(transform, false);
                root.transform.localPosition = -cTotal;

                CrearRenderersPorTipo(root, segs, mat);

                var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (var s in segs)
                { min = Vector3.Min(min, s.Item1); max = Vector3.Max(max, s.Item1);
                  min = Vector3.Min(min, s.Item2); max = Vector3.Max(max, s.Item2); }
                if (segs.Count > 0)
                {
                    var lblC = (min + max) * 0.5f;
                    var lbl = root.AddComponent<TextMesh>();
                    lbl.text = "EDIFICIO " + ed.id;
                    lbl.fontSize = 64;
                    lbl.anchor = TextAnchor.MiddleCenter;
                    lbl.characterSize = 0.35f;
                    lbl.color = new Color(0.08f, 0.16f, 0.23f);
                    lbl.transform.localPosition = new Vector3(lblC.x, min.y - 2.0f, lblC.z);
                }
                Debug.Log(string.Format("[UnityComplejo] EDIFICIO {0}: {1} segmentos",
                    ed.id, segs.Count));
            }

            GameObject lblT = new GameObject("Titulo");
            lblT.transform.SetParent(transform, false);
            var tm = lblT.AddComponent<TextMesh>();
            tm.text = titulo;
            tm.fontSize = 80;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.characterSize = 0.45f;
            lblT.transform.localPosition = Vector3.up * 2f;

            // Encaje de cámara apuntando al origen (centro del conjunto).
            GameObject cam = GameObject.FindWithTag("MainCamera");
            if (cam != null)
            {
                float d = Mathf.Max(alcance, 80f);
                cam.transform.position = new Vector3(d * 0.55f, d * 0.45f, -d);
                cam.transform.LookAt(Vector3.up * 8f);
            }
        }

        /// <summary>
        /// Devuelve segmentos (inicio, fin, tipo) entendiendo ambos esquemas
        /// (A: nodos como dict por tag; B: nodos como lista con id).
        /// Los tipos se normalizan a un int categoría (0..3).
        /// </summary>
        private List<(Vector3, Vector3, int)> ExtraerSegmentos(EdificioComplejo ed)
        {
            var res = new List<(Vector3, Vector3, int)>();
            var js = ed.json;
            var nodos = new Dictionary<double, double[]>();

            if (ed.esquema == "A")
            {
                foreach (var kv in ((JObject)js.nodos).Properties())
                {
                    double x = (double)kv.Value["x"];
                    double y = (double)kv.Value["y"];
                    double z = (double)kv.Value["z"];
                    nodos[double.Parse(kv.Name)] = new[] { x, y, z };
                }
                foreach (var el in js.elementos)
                {
                    double ni = (double)el["ni"];
                    double nj = (double)el["nj"];
                    double[] a, b;
                    if (!nodos.TryGetValue(ni, out a) || !nodos.TryGetValue(nj, out b)) continue;
                    res.Add((ParaUnity(a), ParaUnity(b), CategoriaA((string)el["tipo"])));
                }
            }
            else // esquema B
            {
                foreach (var n in (JArray)js.nodos)
                {
                    double id = (double)n["id"];
                    double x = (double)n["x"];
                    double y = (double)n["y"];
                    double z = (double)n["z"];
                    nodos[id] = new[] { x, y, z };
                }
                foreach (var el in js.elementos)
                {
                    double ni = (double)el["ni"];
                    double nj = (double)el["nj"];
                    double[] a, b;
                    if (!nodos.TryGetValue(ni, out a) || !nodos.TryGetValue(nj, out b)) continue;
                    res.Add((ParaUnity(a), ParaUnity(b), CategoriaB((string)el["tipo"])));
                }
            }
            return res;
        }

        private Vector3 ParaUnity(double[] p)
        { return new Vector3((float)p[0], (float)p[2], (float)p[1]); }

        private static int CategoriaA(string tipo)
        {
            switch (tipo)
            {
                case "column": return 0;
                case "wall": return 1;
                case "aspa": return 3;
                default: return 2;   // vigas_x / vigas_y
            }
        }

        private static int CategoriaB(string tipo)
        {
            switch (tipo)
            {
                case "pilar":   return 0;
                case "muro":    return 1;
                case "brazo":   return 3;
                default:        return 2;   // viga
            }
        }

        /// <summary>Crea un LineRenderer por categoría (0..3) con su color.</summary>
        private void CrearRenderersPorTipo(GameObject root,
            List<(Vector3, Vector3, int)> segs, Material matBase)
        {
            var colores = new[] { colorColumnas, colorVigas, colorMuros, colorAcero };
            for (int i = 0; i < 4; i++)
            {
                var puntos = new List<Vector3>();
                foreach (var s in segs) if (s.Item3 == i) { puntos.Add(s.Item1); puntos.Add(s.Item2); }
                if (puntos.Count == 0) continue;

                var go = new GameObject("Lineas_" + i);
                go.transform.SetParent(root.transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.material = matBase;
                lr.startColor = colores[i];
                lr.endColor = colores[i];
                lr.startWidth = grosorLinea;
                lr.endWidth = grosorLinea;
                lr.positionCount = puntos.Count;
                lr.SetPositions(puntos.ToArray());
                lr.useWorldSpace = false;
            }
        }
    }

    // --- Clases espejo del contrato `edificio_completo.json` ---

    [System.Serializable]
    public class ComplejoModelo
    {
        public string proyecto;
        public string unidades;
        public int version;
        public ConfigComplejo config = new ConfigComplejo();
        public List<EdificioComplejo> edificios = new List<EdificioComplejo>();
    }

    [System.Serializable]
    public class ConfigComplejo
    {
        public double offset_b_x_m;
        public long tag_offset_b;
    }

    [System.Serializable]
    public class EdificioComplejo
    {
        public string id;
        public string nombre;
        public string esquema;
        public double offset_x;
        public long tag_offset;
        public int n_nodos;
        public int n_elementos;
        public JsonEdificio json = new JsonEdificio();
    }

    [System.Serializable]
    public class JsonEdificio
    {
        // "nodos" es OBJECT en el esquema A (dict tag->nodo) y ARRAY en el
        // esquema B (lista con id). Por eso se guarda como JToken generico y
        // se castea segun el esquema en ExtraerSegmentos.
        [JsonProperty("nodos")]
        public JToken nodos = new JObject();

        [JsonProperty("elementos")]
        public JArray elementos = new JArray();
    }
}