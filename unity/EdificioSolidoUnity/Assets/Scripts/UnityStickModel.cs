using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace MCOC.Unity
{
    [ExecuteInEditMode]
    public class UnityStickModel : MonoBehaviour
    {
        public string jsonFileName = "edificio_completo.json";
        public float amplificacion = 400f;
        public float separarBloquesY = 0f;
        public bool mostrarColumnas = true;
        public bool mostrarVigasX = true;
        public bool mostrarVigasY = true;
        public bool mostrarMuros = true;
        public bool mostrarApoyos = true;
        public bool mostrarSuelo = true;
        public bool mostrarNodos = false;
        public bool mostrarCargasG = false;
        public bool mostrarCargasQ = false;
        public bool mostrarCargasSismo = false;
        public bool mostrarTributaria = false;
        public float escalaCargas = 0.02f;

        private enum CasoDeformacion { Ninguno, G, GQ, EX, EY }
        private CasoDeformacion casoActivo = CasoDeformacion.Ninguno;

        // --- Selección y consulta interactiva de esfuerzos en vigas (Edificio B) ---
        private class VigaSeleccionable
        {
            public BloqueVisual bloque;
            public ElementoVisual3D ev;
        }
        private Dictionary<GameObject, VigaSeleccionable> vigasPorObjeto = new Dictionary<GameObject, VigaSeleccionable>();
        private VigaSeleccionable vigaSeleccionada;
        private static Material matVigaSeleccionada;
        private static readonly string[] EtiquetasCaso = { "G", "Q", "GQ", "EX", "EY" };
        private int casoConsulta = 2;              // GQ por defecto
        private float posConsulta = 0.5f;          // fracción 0..1 de la viga
        private bool inspeccionHabilitada = true;

        private List<BloqueVisual> bloques = new List<BloqueVisual>();
        private string mensaje = "Sin cargar";
        private bool sucio = true;

        private class ElementoVisual3D
        {
            public ElementoModelo datos;
            public GameObject objeto;
        }

        private class ApoyoVisual
        {
            public NodoModelo nodo;
            public string tipo;
            public GameObject objeto;
        }

        private class PanelVisual
        {
            public GameObject go;
            public double xc, yc, L, t;
            public bool resisteY;
            public List<double> zs;
            public Vector2 offsetAplicado = new Vector2(float.MinValue, float.MinValue);
            public BloqueVisual bloque;
        }

        private class NivelTributarioVisual
        {
            public GameObject go;
            public List<TextMesh> labels = new List<TextMesh>();
            public int lvl;
            public double z;
            public Vector2 offsetAplicado = new Vector2(float.MinValue, float.MinValue);
            public BloqueVisual bloque;
            public List<Vector2[]> celdasLocales; // 4 esquinas (x,y) por celda, en coords SI
            public List<Color> coloresCelda;
            public List<int> regionDeCelda;      // id de viga por celda (region agrupada)
            public List<Vector2> regionCentro;   // centroide local por region
            public List<double> regionArea;      // area (m2) por region
            public List<double> regionKN;        // tipo carga por region
            public int[] indices;
            public Vector3[] vetAdjuntos;
            public Color[] colsAdjuntos;
            public Vector2[] posRegionesLocal;   // centro de etiqueta por region

            public float paso;
            public List<double> xs;
            public List<double> ys;
            public Vector3[] vetLocal;
            public Vector3[] contLocal;
            public GameObject contorno;
        }

        private class NivelMaestro
        {
            public int nivel;
            public Vector2 pos;
            public NodoModelo nodo;
        }

        private class FlechaVisual
        {
            public GameObject go;
            public Transform shaft;
            public Transform cono;
            public TextMesh txt;
            public ElementoModelo el;
            public bool esQ;
            public double valor;
            public Vector3 offsetPerp;
        }

        private class FlechaSismoVisual
        {
            public GameObject go;
            public Transform shaft;
            public Transform cono;
            public TextMesh txt;
            public NodoModelo nodo;
            public int nivel;
            public bool enX;
            public double valor;
        }

        private class BloqueVisual
        {
            public int indice;
            public ModeloEdificio datos;
            public Vector2 offset;
            public Dictionary<int, NivelMaestro> maestros = new Dictionary<int, NivelMaestro>();
            public Dictionary<string, List<ElementoVisual3D>> porTipo = new Dictionary<string, List<ElementoVisual3D>>();
            public Dictionary<string, GameObject> contenedor = new Dictionary<string, GameObject>();
            public GameObject nodosObj;
            public GameObject apoyosObj;
            public GameObject sueloObj;
            public GameObject suelo;
            public float zMin = float.MaxValue;
            public float zMax = float.MinValue;
            public List<ApoyoVisual> apoyos = new List<ApoyoVisual>();
            public Dictionary<string, GameObject> nodosVis = new Dictionary<string, GameObject>();
            public List<PanelVisual> paneles = new List<PanelVisual>();
            public GameObject cargasObj;
            public List<FlechaVisual> flechas = new List<FlechaVisual>();
            public List<FlechaSismoVisual> flechasSismo = new List<FlechaSismoVisual>();
            public GameObject tributariaObj;
            public List<NivelTributarioVisual> zonas = new List<NivelTributarioVisual>();
            public CasoDeformacion casoDeformacionPrevia = CasoDeformacion.Ninguno;
        }

        private static Mesh meshCaja;
        private static Mesh meshVigaT;
        private static Mesh meshVigaL;
        private static Mesh coneCargaMesh;
        private static Material matColumna;
        private static Material matViga;
        private static Material matMuro;
        private static Material matZapata;

        private static void Destruir(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        private static void LogArchivo(string texto)
        {
            try
            {
                string ruta = Path.Combine(Application.dataPath, "..", "BuildLog_unity.txt");
                File.AppendAllText(ruta, texto + System.Environment.NewLine);
            }
            catch
            {
            }
        }

        private void OnEnable()
        {
            LogArchivo("[OnEnable] bloques=" + bloques.Count);
            if (bloques.Count == 0) Cargar();
        }

        private void Start()
        {
            LogArchivo("[Start] bloques=" + bloques.Count);
            if (bloques.Count == 0) Cargar();
        }

        private void Update()
        {
            if (sucio) AplicarVisual();
            if (Application.isPlaying && casoActivo != CasoDeformacion.Ninguno) ReconstruirPanelesDeformados();
            ApuntarEtiquetas();
            ProcesarSeleccionViga();
        }

        public Vector3 CentroBloque(int idx)
        {
            BloqueVisual bv = ObtenerBloque(idx);
            if (bv == null || bv.apoyos.Count == 0) return Vector3.zero;
            Vector3 acum = Vector3.zero;
            foreach (ApoyoVisual av in bv.apoyos)
            {
                acum.x += (float)av.nodo.x + bv.offset.x;
                acum.z += (float)av.nodo.y + bv.offset.y;
            }
            Vector3 centro = acum / bv.apoyos.Count;
            centro.y = (bv.zMin + bv.zMax) * 0.5f;
            return centro;
        }

        public float SpanBloque(int idx)
        {
            BloqueVisual bv = ObtenerBloque(idx);
            if (bv == null || bv.apoyos.Count == 0) return 20f;
            float xmin = float.MaxValue, xmax = float.MinValue;
            float zmin = float.MaxValue, zmax = float.MinValue;
            foreach (ApoyoVisual av in bv.apoyos)
            {
                xmin = Mathf.Min(xmin, (float)av.nodo.x);
                xmax = Mathf.Max(xmax, (float)av.nodo.x);
                zmin = Mathf.Min(zmin, (float)av.nodo.y);
                zmax = Mathf.Max(zmax, (float)av.nodo.y);
            }
            return Mathf.Max(xmax - xmin, zmax - zmin);
        }

        private BloqueVisual ObtenerBloque(int idx)
        {
            if (bloques == null || idx < 0 || idx >= bloques.Count) return null;
            return bloques[idx];
        }

        private void Cargar()
        {
            string path = Path.Combine(Application.streamingAssetsPath, jsonFileName);
            LogArchivo("[Cargar] path=" + path + " existe=" + File.Exists(path));
            if (!File.Exists(path))
            {
                mensaje = "No existe StreamingAssets/" + jsonFileName;
                LogArchivo("[Cargar] FALTA ARCHIVO");
                return;
            }
            try
            {
                string txt = File.ReadAllText(path);
                ModeloComplejo modelo = JsonConvert.DeserializeObject<ModeloComplejo>(txt);
                LogArchivo("[Cargar] parse OK: " + modelo.proyecto + " edificios=" + (modelo.edificios != null ? modelo.edificios.Count : 0));
                ConstruirEscena(modelo);
                mensaje = "Cargado: " + modelo.proyecto;
                LogArchivo("[Cargar] ConstruirEscena OK bloques=" + bloques.Count);
                Debug.Log("[UnityStickModel] JSON cargado: " + modelo.proyecto
                    + " | edificios=" + (modelo.edificios != null ? modelo.edificios.Count : 0));
                if (modelo.totales != null && modelo.totales.G != null && modelo.totales.EX != null && modelo.totales.EY != null)
                {
                    mensaje += System.Environment.NewLine + string.Format(
                        "G = {0:N0} kN   |   V EX = {1:N0} kN   |   V EY = {2:N0} kN",
                        modelo.totales.G.fz_reaccion, modelo.totales.EX.fx_reaccion, modelo.totales.EY.fy_reaccion);
                }
            }
            catch (System.Exception ex)
            {
                mensaje = "Error al leer JSON: " + ex.Message;
                LogArchivo("[Cargar] EXCEPCION: " + ex);
                Debug.LogException(ex);
            }
            sucio = true;
        }

        private void ConstruirEscena(ModeloComplejo modelo)
        {
            foreach (Transform hijo in transform)
            {
                if (hijo.name != "Main Camera") Destruir(hijo.gameObject);
            }
            bloques.Clear();
            vigasPorObjeto.Clear();
            vigaSeleccionada = null;

            for (int i = 0; i < modelo.edificios.Count; i++)
            {
                ModeloEdificio ed = modelo.edificios[i];
                BloqueVisual bv = new BloqueVisual();
                bv.indice = i;
                bv.datos = ed;
                bv.offset = new Vector2((float)ed.offset.x, (float)ed.offset.y);

                if (i > 0 && separarBloquesY > 0f) bv.offset.y += separarBloquesY;

                GameObject root = new GameObject(ed.bloque);
                root.transform.SetParent(transform, false);

                foreach (string tipo in new[] { "column", "wall", "vigas_x", "vigas_y" })
                {
                    GameObject cont = new GameObject(tipo);
                    cont.transform.SetParent(root.transform, false);
                    bv.contenedor[tipo] = cont;
                    bv.porTipo[tipo] = new List<ElementoVisual3D>();
                }
                bv.nodosObj = new GameObject("nodos");
                bv.nodosObj.transform.SetParent(root.transform, false);
                bv.apoyosObj = new GameObject("apoyos");
                bv.apoyosObj.transform.SetParent(root.transform, false);
                bv.sueloObj = new GameObject("suelo");
                bv.sueloObj.transform.SetParent(root.transform, false);
                bv.cargasObj = new GameObject("cargas");
                bv.cargasObj.transform.SetParent(root.transform, false);
                bv.tributariaObj = new GameObject("tributaria");
                bv.tributariaObj.transform.SetParent(root.transform, false);

                foreach (KeyValuePair<string, NodoModelo> kv in ed.nodos)
                {
                    NodoModelo n = kv.Value;
                    if (n != null && !string.IsNullOrEmpty(n.rol) && n.rol == "maestro_diafragma")
                    {
                        bv.maestros[n.nivel] = new NivelMaestro { nivel = n.nivel, pos = new Vector2((float)n.x, (float)n.y), nodo = n };
                    }
                    if (n != null && (float)n.z < bv.zMin) bv.zMin = (float)n.z;
                    if (n != null && (float)n.z > bv.zMax) bv.zMax = (float)n.z;
                }

                HashSet<int> tagsApoyoHeuristico = new HashSet<int>();
                if (ed.apoyos == null || ed.apoyos.Count == 0)
                {
                    foreach (ElementoModelo el in ed.elementos)
                    {
                        if (el.tipo != "column" && el.tipo != "wall") continue;
                        NodoModelo ni = ObtenerNodo(ed, el.ni);
                        NodoModelo nj = ObtenerNodo(ed, el.nj);
                        if (ni != null && Mathf.Approximately((float)ni.z, bv.zMin)) tagsApoyoHeuristico.Add(el.ni);
                        if (nj != null && Mathf.Approximately((float)nj.z, bv.zMin)) tagsApoyoHeuristico.Add(el.nj);
                    }
                }

                List<ApoyoVisual> apoyosPorDibujar = new List<ApoyoVisual>();
                if (ed.apoyos != null && ed.apoyos.Count > 0)
                {
                    foreach (ApoyoModelo ap in ed.apoyos)
                    {
                        NodoModelo n = ObtenerNodo(ed, ap.tag);
                        if (n == null) continue;
                        apoyosPorDibujar.Add(new ApoyoVisual { nodo = n, tipo = ap.tipo });
                    }
                }
                else
                {
                    foreach (int tag in tagsApoyoHeuristico)
                    {
                        NodoModelo n = ObtenerNodo(ed, tag);
                        if (n == null) continue;
                        apoyosPorDibujar.Add(new ApoyoVisual { nodo = n, tipo = "empotrado" });
                    }
                }
                apoyosPorDibujar.Sort((a, b) => a.nodo.x.CompareTo(b.nodo.x));

                foreach (ApoyoVisual av in apoyosPorDibujar)
                {
                    GameObject obj = av.tipo == "fijo"
                        ? CrearCono(0.5f, 1.5f, new Color(0.95f, 0.55f, 0.10f))
                        : CrearZapata();
                    obj.name = "apoyo_" + (av.tipo == "fijo" ? "fijo" : "empotrado");
                    obj.transform.SetParent(bv.apoyosObj.transform, false);
                    av.objeto = obj;
                    bv.apoyos.Add(av);
                }

                GameObject terreno = GameObject.CreatePrimitive(PrimitiveType.Cube);
                terreno.name = "terreno";
                terreno.transform.SetParent(bv.sueloObj.transform, false);
                Destruir(terreno.GetComponent<Collider>());
                terreno.GetComponent<Renderer>().sharedMaterial = MaterialColor(new Color(0.55f, 0.45f, 0.30f));
                bv.suelo = terreno;

                foreach (ElementoModelo el in ed.elementos)
                {
                    if (el.tipo == "wall") continue;
                    NodoModelo ni = ObtenerNodo(ed, el.ni);
                    NodoModelo nj = ObtenerNodo(ed, el.nj);
                    if (ni == null || nj == null) continue;

                    GameObject elemObj;
                    if (el.tipo == "column")
                    {
                        elemObj = CrearColumna3D(bv.contenedor[el.tipo], ni, nj, bv);
                    }
                    else
                    {
                        bool esVigaX = el.tipo == "vigas_x";
                        elemObj = CrearViga3D(bv.contenedor[el.tipo], ni, nj, bv, esVigaX, ed);
                    }

                    ElementoVisual3D ev = new ElementoVisual3D();
                    ev.datos = el;
                    ev.objeto = elemObj;
                    bv.porTipo[el.tipo].Add(ev);
                    if (el.tipo == "vigas_x" || el.tipo == "vigas_y")
                    {
                        vigasPorObjeto[elemObj] = new VigaSeleccionable { bloque = bv, ev = ev };
                    }
                }

                foreach (KeyValuePair<string, NodoModelo> kv2 in ed.nodos)
                {
                    NodoModelo n = kv2.Value;
                    if (n == null) continue;
                    GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    esfera.name = "nodo_" + kv2.Key;
                    esfera.transform.SetParent(bv.nodosObj.transform, false);
                    esfera.transform.position = Posicion(n, bv);
                    esfera.transform.localScale = Vector3.one * 0.45f;
                    Destruir(esfera.GetComponent<Collider>());
                    Renderer esfR = esfera.GetComponent<Renderer>();
                    esfR.material.shader = Shader.Find("Unlit/Color");
                    esfR.material.color = new Color(0.2f, 0.6f, 0.3f);
                    esfR.material.renderQueue = 4000;
                    esfR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    esfera.SetActive(mostrarNodos);
                    bv.nodosVis[kv2.Key] = esfera;
                }

                CrearPanelesMuro(bv, ed);
                CrearCargas(bv, ed);
                CrearTributaria(bv, ed);

                bloques.Add(bv);
            }

            var __orbit = Object.FindObjectOfType<OrbitCamera>();
            if (__orbit != null) __orbit.EnfocarBloque(0);
        }

        private NodoModelo ObtenerNodo(ModeloEdificio ed, int tag)
        {
            string clave = tag.ToString();
            if (ed.nodos.ContainsKey(clave)) return ed.nodos[clave];
            return null;
        }

        private static double GridVal(MuroModelo w, Dictionary<string, double> grid)
        {
            if (w.axis != null) return grid.ContainsKey(w.axis) ? grid[w.axis] : 0.0;
            if (w.x0 is string s && grid.ContainsKey(s)) return grid[s];
            if (w.x0 is double dd) return dd;
            return 0.0;
        }

        private static double GridValX1(MuroModelo w, Dictionary<string, double> grid)
        {
            if (w.x1 is string s && grid.ContainsKey(s)) return grid[s];
            if (w.x1 is double dd) return dd;
            return 0.0;
        }

        private static Material MaterialMuroReal()
        {
            if (matMuro == null)
            {
                matMuro = new Material(Shader.Find("Standard"));
                if (matMuro == null) matMuro = new Material(Shader.Find("Unlit/Color"));
                matMuro.color = new Color(0.45f, 0.45f, 0.47f);
                matMuro.SetFloat("_Glossiness", 0.1f);
            }
            return matMuro;
        }

        private static Material MaterialColumna()
        {
            if (matColumna == null)
            {
                matColumna = new Material(Shader.Find("Standard"));
                if (matColumna == null) matColumna = new Material(Shader.Find("Unlit/Color"));
                matColumna.color = new Color(0.72f, 0.72f, 0.73f);
                matColumna.SetFloat("_Glossiness", 0.4f);
            }
            return matColumna;
        }

        private static Material MaterialViga()
        {
            if (matViga == null)
            {
                matViga = new Material(Shader.Find("Standard"));
                if (matViga == null) matViga = new Material(Shader.Find("Unlit/Color"));
                matViga.color = new Color(0.95f, 0.55f, 0.15f);
                matViga.SetFloat("_Glossiness", 0.3f);
            }
            return matViga;
        }

        private static Material MaterialZapata()
        {
            if (matZapata == null)
            {
                matZapata = new Material(Shader.Find("Standard"));
                if (matZapata == null) matZapata = new Material(Shader.Find("Unlit/Color"));
                matZapata.color = new Color(0.60f, 0.45f, 0.30f);
                matZapata.SetFloat("_Glossiness", 0.2f);
            }
            return matZapata;
        }

        private static Material MaterialColor(Color color)
        {
            Material m = new Material(Shader.Find("Standard"));
            if (m == null) m = new Material(Shader.Find("Unlit/Color"));
            m.color = color;
            return m;
        }

        private static Material MaterialTranslucido(Color color)
        {
            Material m = new Material(Shader.Find("Transparent/Diffuse"));
            if (m == null) m = new Material(Shader.Find("Standard"));
            m.color = color;
            return m;
        }

        // Material que respeta los colores por vertice (mesh.colors) del mosaico tributario.
        private static Material MaterialTributariaMosaico()
        {
            Material m = new Material(Shader.Find("Sprites/Default"));
            if (m == null) m = new Material(Shader.Find("Transparent/Diffuse"));
            if (m != null) m.color = Color.white;
            return m;
        }

        private static Mesh MeshCaja()
        {
            if (meshCaja != null) return meshCaja;
            meshCaja = new Mesh();
            meshCaja.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),   new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),  new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),    new Vector3(-0.5f, 0.5f, 0.5f)
            };
            meshCaja.triangles = new int[]
            {
                0,2,1, 0,3,2, 5,7,6, 5,6,4,
                1,2,6, 1,6,5, 0,4,7, 0,7,3,
                3,7,6, 3,6,2, 0,1,5, 0,5,4
            };
            meshCaja.RecalculateNormals();
            meshCaja.RecalculateBounds();
            return meshCaja;
        }

        private static Mesh MeshVigaT()
        {
            if (meshVigaT != null) return meshVigaT;
            float anchoFlange = 0.30f, espFlange = 0.12f;
            float anchoWeb = 0.60f, altoWeb = 0.80f;
            float hw = altoWeb - espFlange;
            meshVigaT = new Mesh();
            meshVigaT.vertices = new Vector3[]
            {
                new Vector3(-anchoWeb/2f, 0, -hw/2f), new Vector3(anchoWeb/2f, 0, -hw/2f),
                new Vector3(anchoWeb/2f, 0, hw/2f),   new Vector3(-anchoWeb/2f, 0, hw/2f),
                new Vector3(-anchoFlange/2f, 0, hw/2f), new Vector3(anchoFlange/2f, 0, hw/2f),
                new Vector3(anchoFlange/2f, 0, altoWeb/2f), new Vector3(-anchoFlange/2f, 0, altoWeb/2f)
            };
            meshVigaT.triangles = new int[]
            {
                0,2,1, 0,3,2, 4,6,5, 4,7,6,
                0,1,5, 0,5,4, 1,2,6, 1,6,5,
                2,3,7, 2,7,6, 3,0,4, 3,4,7
            };
            meshVigaT.RecalculateNormals();
            meshVigaT.RecalculateBounds();
            return meshVigaT;
        }

        private static Mesh MeshVigaL()
        {
            if (meshVigaL != null) return meshVigaL;
            float anchoFlange = 0.40f, espFlange = 0.12f;
            float anchoWeb = 0.60f, altoWeb = 0.80f;
            float hw = altoWeb - espFlange;
            meshVigaL = new Mesh();
            meshVigaL.vertices = new Vector3[]
            {
                new Vector3(-anchoWeb/2f, 0, -hw/2f), new Vector3(anchoWeb/2f, 0, -hw/2f),
                new Vector3(anchoWeb/2f, 0, hw/2f),   new Vector3(-anchoWeb/2f, 0, hw/2f),
                new Vector3(-anchoWeb/2f, 0, hw/2f),  new Vector3(-anchoWeb/2f + anchoFlange, 0, hw/2f),
                new Vector3(-anchoWeb/2f + anchoFlange, 0, altoWeb/2f), new Vector3(-anchoWeb/2f, 0, altoWeb/2f)
            };
            meshVigaL.triangles = new int[]
            {
                0,2,1, 0,3,2, 4,6,5, 4,7,6,
                0,1,5, 0,5,4, 1,2,6, 1,6,5,
                2,3,7, 2,7,6, 3,0,4, 3,4,7
            };
            meshVigaL.RecalculateNormals();
            meshVigaL.RecalculateBounds();
            return meshVigaL;
        }

        private GameObject CrearColumna3D(GameObject padre, NodoModelo ni, NodoModelo nj, BloqueVisual bv)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destruir(go.GetComponent<Collider>());
            go.name = "columna";
            go.transform.SetParent(padre.transform, false);
            go.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
            go.GetComponent<Renderer>().sharedMaterial = MaterialColumna();
            return go;
        }

        private GameObject CrearViga3D(GameObject padre, NodoModelo ni, NodoModelo nj, BloqueVisual bv, bool esVigaX, ModeloEdificio ed)
        {
            bool esBorde = EsVigaBorde(ni, nj, esVigaX, ed);
            string sufijo = esBorde ? "_L" : "_T";

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "viga" + sufijo;
            go.transform.SetParent(padre.transform, false);
            go.transform.localScale = new Vector3(0.6f, 1f, 0.8f);
            go.GetComponent<Renderer>().sharedMaterial = MaterialViga();
            return go;
        }

        private bool EsVigaBorde(NodoModelo ni, NodoModelo nj, bool esVigaX, ModeloEdificio ed)
        {
            if (ed.geometria == null) return true;
            var gx = ed.geometria.grid_x;
            var gy = ed.geometria.grid_y;
            if (gx == null || gy == null || gx.Count == 0 || gy.Count == 0) return true;

            double constVal = esVigaX ? ni.y : ni.x;
            double constMin, constMax;
            if (esVigaX)
            {
                constMin = double.PositiveInfinity; constMax = double.NegativeInfinity;
                foreach (double v in gy.Values) { if (v < constMin) constMin = v; if (v > constMax) constMax = v; }
            }
            else
            {
                constMin = double.PositiveInfinity; constMax = double.NegativeInfinity;
                foreach (double v in gx.Values) { if (v < constMin) constMin = v; if (v > constMax) constMax = v; }
            }

            return Mathf.Approximately((float)constVal, (float)constMin)
                || Mathf.Approximately((float)constVal, (float)constMax);
        }

        private void PosicionarElemento3D(GameObject go, NodoModelo ni, NodoModelo nj, BloqueVisual bv)
        {
            if (go == null) return;
            Vector3 a = Posicion(ni, bv);
            Vector3 b = Posicion(nj, bv);
            Vector3 dir = b - a;
            float largo = dir.magnitude;
            if (largo < 0.001f) { go.SetActive(false); return; }
            go.SetActive(true);

            Vector3 baseEscala = go.transform.localScale;
            float seccW = baseEscala.x;
            float seccH = baseEscala.z;

            go.transform.position = (a + b) * 0.5f;

            Vector3 up = Vector3.Normalize(dir);
            Vector3 fwd = Vector3.Cross(Vector3.down, up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(fwd, up);

            go.transform.rotation = Quaternion.LookRotation(fwd, up);

            Vector3 esc = new Vector3(seccW, largo, seccH);
            go.transform.localScale = esc;
        }

        private void CrearPanelesMuro(BloqueVisual bv, ModeloEdificio ed)
        {
            if (ed.geometria == null || ed.geometria.secciones == null
                || ed.geometria.secciones.muros == null
                || !bv.contenedor.ContainsKey("wall")) return;
            var zs = ed.geometria.niveles_z;
            if (zs == null || zs.Count < 2) return;

            foreach (MuroModelo w in ed.geometria.secciones.muros)
            {
                if (w == null || w.t <= 0.0) continue;
                double xc, yc, L;
                bool resisteY;
                if (!string.IsNullOrEmpty(w.axis)
                    && ed.geometria.grid_x.ContainsKey(w.axis)
                    && ed.geometria.grid_y.ContainsKey(w.y0)
                    && ed.geometria.grid_y.ContainsKey(w.y1))
                {
                    double x = ed.geometria.grid_x[w.axis];
                    double y0 = ed.geometria.grid_y[w.y0];
                    double y1 = ed.geometria.grid_y[w.y1];
                    xc = x; yc = (y0 + y1) / 2.0; L = System.Math.Abs(y1 - y0);
                    resisteY = true;
                }
                else if (!string.IsNullOrEmpty(w.y)
                         && ed.geometria.grid_y.ContainsKey(w.y))
                {
                    double x0 = GridVal(w, ed.geometria.grid_x);
                    double x1v = GridValX1(w, ed.geometria.grid_x);
                    double y = ed.geometria.grid_y[w.y];
                    xc = (x0 + x1v) / 2.0; yc = y; L = System.Math.Abs(x1v - x0);
                    resisteY = false;
                }
                else
                {
                    continue;
                }
                if (L <= 0.0) continue;
                CrearPanelMuro(bv, xc, yc, L, w.t, resisteY, zs, w.id);
            }
        }

        private void CrearPanelMuro(BloqueVisual bv, double xc, double yc,
            double L, double t, bool resisteY, List<double> zs, string id)
        {
            GameObject go = new GameObject("panel_" + id);
            go.transform.SetParent(bv.contenedor["wall"].transform, false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialMuroReal();
            PanelVisual p = new PanelVisual
            {
                go = go, xc = xc, yc = yc, L = L, t = t, resisteY = resisteY,
                zs = zs, bloque = bv
            };
            bv.paneles.Add(p);
            ReconstruirPanel(p, bv.offset);
        }

        private void ReconstruirPanel(PanelVisual p, Vector2 off)
        {
            BloqueVisual bv = p.bloque;
            bool deformando = casoActivo != CasoDeformacion.Ninguno
                              && bv != null && bv.datos != null
                              && bv.datos.resultados != null
                              && bv.datos.resultados.ContainsKey(casoActivo.ToString())
                              && bv.datos.resultados[casoActivo.ToString()].desplazamientos_maestro != null;
            Vector2 dummy = new Vector2(float.MinValue, float.MinValue);
            if (!deformando && p.offsetAplicado.x == off.x && p.offsetAplicado.y == off.y) return;
            if (deformando) p.offsetAplicado = dummy;
            else p.offsetAplicado = off;

            ResultadoCaso caso = null;
            if (deformando)
                caso = bv.datos.resultados[casoActivo.ToString()];

            double bx = off.x, by = off.y;
            double u0 = p.resisteY ? p.xc - p.t / 2.0 : p.yc - p.t / 2.0;
            double u1 = p.resisteY ? p.xc + p.t / 2.0 : p.yc + p.t / 2.0;
            double a0 = p.resisteY ? p.yc - p.L / 2.0 : p.xc - p.L / 2.0;
            double a1 = p.resisteY ? p.yc + p.L / 2.0 : p.xc + p.L / 2.0;
            Mesh mesh = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            for (int st = 0; st < p.zs.Count - 1; st++)
            {
                int lvlBot = st;
                int lvlTop = st + 1;

                System.Func<double, double, int, Vector3> P = (u, a, lvl) =>
                {
                    double px, py;
                    if (p.resisteY) { px = u; py = a; }
                    else { px = a; py = u; }

                    if (!deformando)
                        return new Vector3((float)px + (float)bx, (float)p.zs[lvl], (float)py + (float)by);

                    string claveNivel = lvl.ToString();
                    if (!caso.desplazamientos_maestro.ContainsKey(claveNivel))
                        return new Vector3((float)px + (float)bx, (float)p.zs[lvl], (float)py + (float)by);

                    DesplazamientoNivel d = caso.desplazamientos_maestro[claveNivel];
                    double rz = d.rz * amplificacion;

                    double masterX = bv.maestros.ContainsKey(lvl) ? bv.maestros[lvl].pos.x : 0.0;
                    double masterY = bv.maestros.ContainsKey(lvl) ? bv.maestros[lvl].pos.y : 0.0;
                    double dx = px - masterX;
                    double dy = py - masterY;
                    double cos = System.Math.Cos(rz);
                    double sin = System.Math.Sin(rz);
                    double rx = dx * cos - dy * sin;
                    double ry = dx * sin + dy * cos;
                    double fx = masterX + rx + d.ux * amplificacion + bx;
                    double fy = masterY + ry + d.uy * amplificacion + by;
                    return new Vector3((float)fx, (float)p.zs[lvl], (float)fy);
                };

                Vector3[] q = new Vector3[8];
                q[0] = P(u0, a0, lvlBot);
                q[1] = P(u0, a1, lvlBot);
                q[2] = P(u0, a1, lvlTop);
                q[3] = P(u0, a0, lvlTop);
                q[4] = P(u1, a0, lvlBot);
                q[5] = P(u1, a1, lvlBot);
                q[6] = P(u1, a1, lvlTop);
                q[7] = P(u1, a0, lvlTop);
                int v0 = verts.Count;
                for (int i = 0; i < 8; i++) verts.Add(q[i]);
                tris.Add(v0); tris.Add(v0 + 2); tris.Add(v0 + 1);
                tris.Add(v0); tris.Add(v0 + 3); tris.Add(v0 + 2);
                tris.Add(v0 + 4); tris.Add(v0 + 5); tris.Add(v0 + 6);
                tris.Add(v0 + 4); tris.Add(v0 + 6); tris.Add(v0 + 7);
            }
            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            p.go.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private static readonly Color ColorCargaG = new Color(0.22f, 0.22f, 0.24f);
        private static readonly Color ColorCargaQ = new Color(0.95f, 0.55f, 0.10f);
        private static readonly Color ColorCargaSismo = new Color(0.85f, 0.18f, 0.14f);
        private static readonly float EscalaSismoCargas = 0.004f;

        private static readonly Color ColorTributariaX = new Color(0.95f, 0.45f, 0.05f, 0.60f);
        private static readonly Color ColorTributariaY = new Color(0.10f, 0.45f, 0.95f, 0.60f);
        private static readonly Color ColorContornoTributariaX = new Color(0.55f, 0.20f, 0.00f);
        private static readonly Color ColorContornoTributariaY = new Color(0.00f, 0.20f, 0.55f);

        private static Mesh ConeCargaMesh()
        {
            if (coneCargaMesh != null) return coneCargaMesh;
            int seg = 20;
            const float radio = 0.18f, altura = 0.7f;
            Vector3[] verts = new Vector3[seg + 2];
            int[] tris = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                verts[i] = new Vector3(Mathf.Cos(a) * radio, 0f, Mathf.Sin(a) * radio);
            }
            verts[seg] = new Vector3(0f, 0f, 0f);
            verts[seg + 1] = new Vector3(0f, -altura, 0f);
            int t = 0;
            for (int i = 0; i < seg; i++)
            {
                int i2 = (i + 1) % seg;
                tris[t++] = seg; tris[t++] = i; tris[t++] = i2;
                tris[t++] = seg + 1; tris[t++] = i2; tris[t++] = i;
            }
            coneCargaMesh = new Mesh();
            coneCargaMesh.vertices = verts;
            coneCargaMesh.triangles = tris;
            coneCargaMesh.RecalculateNormals();
            coneCargaMesh.RecalculateBounds();
            return coneCargaMesh;
        }

        private static TextMesh CrearEtiqueta()
        {
            GameObject go = new GameObject("etiqueta");
            TextMesh tm = go.AddComponent<TextMesh>();
            Font fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (fuente == null) fuente = Font.CreateDynamicFontFromOSFont("Arial", 16);
            tm.font = fuente;
            tm.fontSize = 42;
            tm.characterSize = 0.085f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            tm.text = "";
            return tm;
        }

        private FlechaVisual CrearFlechaVisual(BloqueVisual bv, Color color)
        {
            FlechaVisual f = new FlechaVisual();
            f.go = new GameObject("flecha_carga");
            f.go.transform.SetParent(bv.cargasObj.transform, false);

            GameObject shaftGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destruir(shaftGo.GetComponent<Collider>());
            f.shaft = shaftGo.transform;
            f.shaft.SetParent(f.go.transform, false);
            f.shaft.localScale = new Vector3(0.1f, 0.5f, 0.1f);
            f.shaft.GetComponent<Renderer>().sharedMaterial = MaterialColor(color);

            GameObject conoGo = new GameObject("punta");
            f.cono = conoGo.transform;
            f.cono.SetParent(f.go.transform, false);
            conoGo.AddComponent<MeshFilter>().sharedMesh = ConeCargaMesh();
            conoGo.AddComponent<MeshRenderer>().sharedMaterial = MaterialColor(color);

            f.txt = CrearEtiqueta();
            f.txt.transform.SetParent(f.go.transform, false);
            bv.flechas.Add(f);
            return f;
        }

        private void CrearCargas(BloqueVisual bv, ModeloEdificio ed)
        {
            if (ed.cargas == null || ed.cargas.vigas == null || ed.cargas.vigas.Count == 0) return;

            foreach (CargaVigaModelo cv in ed.cargas.vigas)
            {
                if (cv.L <= 0.0) continue;
                NodoModelo ni = ObtenerNodo(ed, cv.ni);
                NodoModelo nj = ObtenerNodo(ed, cv.nj);
                if (ni == null || nj == null) continue;

                ElementoModelo elRef = new ElementoModelo { tag = cv.tag, tipo = cv.tipo, ni = cv.ni, nj = cv.nj };
                FlechaVisual g = CrearFlechaVisual(bv, ColorCargaG);
                g.el = elRef; g.esQ = false; g.valor = cv.G;
                FlechaVisual q = CrearFlechaVisual(bv, ColorCargaQ);
                q.el = elRef; q.esQ = true; q.valor = cv.Q;

                Vector3 along = new Vector3((float)(nj.x - ni.x), 0f, (float)(nj.y - ni.y));
                Vector3 perp = Mathf.Abs(along.x) > Mathf.Abs(along.z)
                    ? Vector3.forward * 1.4f
                    : Vector3.right * 1.4f;
                q.offsetPerp = perp;
            }

            if (ed.cargas.pesos_por_nivel != null && ed.cargas.sismo != null)
            {
                foreach (string lvlKey in ed.cargas.pesos_por_nivel.Keys)
                {
                    int nivel;
                    if (!int.TryParse(lvlKey, out nivel)) continue;
                    NivelMaestro nm;
                    if (!bv.maestros.TryGetValue(nivel, out nm) || nm.nodo == null) continue;

                    CargaSismoModelo sEx, sEy;
                    if (ed.cargas.sismo.TryGetValue("EX", out sEx) && sEx.F_por_nivel.ContainsKey(lvlKey))
                    {
                        FlechaSismoVisual f = new FlechaSismoVisual();
                        f.nivel = nivel; f.enX = true; f.nodo = nm.nodo;
                        f.valor = sEx.F_por_nivel[lvlKey];
                        flechasSismoCrear(bv, f, ColorCargaSismo);
                    }
                    if (ed.cargas.sismo.TryGetValue("EY", out sEy) && sEy.F_por_nivel.ContainsKey(lvlKey))
                    {
                        FlechaSismoVisual f = new FlechaSismoVisual();
                        f.nivel = nivel; f.enX = false; f.nodo = nm.nodo;
                        f.valor = sEy.F_por_nivel[lvlKey];
                        flechasSismoCrear(bv, f, ColorCargaSismo);
                    }
                }
            }
        }

        private void flechasSismoCrear(BloqueVisual bv, FlechaSismoVisual f, Color color)
        {
            f.go = new GameObject("flecha_sismo");
            f.go.transform.SetParent(bv.cargasObj.transform, false);

            GameObject shaftGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destruir(shaftGo.GetComponent<Collider>());
            f.shaft = shaftGo.transform;
            f.shaft.SetParent(f.go.transform, false);
            f.shaft.localScale = new Vector3(0.1f, 0.5f, 0.1f);
            f.shaft.GetComponent<Renderer>().sharedMaterial = MaterialColor(color);

            GameObject conoGo = new GameObject("punta");
            f.cono = conoGo.transform;
            f.cono.SetParent(f.go.transform, false);
            conoGo.AddComponent<MeshFilter>().sharedMesh = ConeCargaMesh();
            conoGo.AddComponent<MeshRenderer>().sharedMaterial = MaterialColor(color);

            f.txt = CrearEtiqueta();
            f.txt.transform.SetParent(f.go.transform, false);
            bv.flechasSismo.Add(f);
        }

        private void CrearTributaria(BloqueVisual bv, ModeloEdificio ed)
        {
            if (ed.geometria == null || ed.geometria.grid_x == null || ed.geometria.grid_y == null
                || ed.geometria.grid_x.Count == 0 || ed.geometria.grid_y.Count == 0) return;
            if (ed.geometria.niveles_z == null || ed.geometria.niveles_z.Count < 2) return;

            List<double> xs = new List<double>(ed.geometria.grid_x.Values);
            List<double> ys = new List<double>(ed.geometria.grid_y.Values);
            xs.Sort();
            ys.Sort();

            double paso = 0.50;
            double qG = 0.0, qQ = 0.0;
            if (ed.cargas != null && ed.cargas.q_losa != null)
            {
                qG = ed.cargas.q_losa.G;
                qQ = ed.cargas.q_losa.Q;
            }

            for (int lvl = 1; lvl < ed.geometria.niveles_z.Count; lvl++)
            {
                double z = ed.geometria.niveles_z[lvl] + 0.15;
                NivelTributarioVisual nv = CrearNivelMosaico(bv, xs, ys, lvl, z, paso, qG, qQ);
                if (nv != null) bv.zonas.Add(nv);
            }
        }

        // Subdivide cada panel en una malla fina; cada celda se asigna a la viga
        // (X u Y) mas cercana. Agrupa celdas contiguas por viga -> regiones con
        // contorno limpio y 1 etiqueta kN. Particion exacta, sin huecos.
        private NivelTributarioVisual CrearNivelMosaico(BloqueVisual bv, List<double> xs, List<double> ys,
            int lvl, double z, double paso, double qG, double qQ)
        {
            if (xs.Count < 2 || ys.Count < 2) return null;

            // 1) clasificar cada celda de la malla global por viga mas cercana
            double xMin = xs[0], xMax = xs[xs.Count - 1];
            double yMin = ys[0], yMax = ys[ys.Count - 1];
            int nx = (int)System.Math.Ceiling((xMax - xMin) / paso);
            int ny = (int)System.Math.Ceiling((yMax - yMin) / paso);
            if (nx <= 0 || ny <= 0) return null;

            int[,] vigaId = new int[nx, ny];
            bool[,] esX = new bool[nx, ny];
            int totalCeldas = nx * ny;
            int[,] regionId = new int[nx, ny];
            for (int ix = 0; ix < nx; ix++)
                for (int iy = 0; iy < ny; iy++) regionId[ix, iy] = -1;

            List<Vector2[]> celdas = new List<Vector2[]>(totalCeldas);
            List<Color> colores = new List<Color>(totalCeldas);

            int[,] panelI = new int[nx, ny];
            int[,] panelJ = new int[nx, ny];

            // Detección de huella: celdas sin viga en ningún borde se omiten
            List<double[]> beamsXL = new List<double[]>();
            List<double[]> beamsYL = new List<double[]>();
            if (bv.datos != null && bv.datos.nodos != null && bv.datos.elementos != null)
            {
                foreach (ElementoModelo el in bv.datos.elementos)
                {
                    if (el.tipo != "vigas_x" && el.tipo != "vigas_y") continue;
                    NodoModelo ni, nj;
                    if (!bv.datos.nodos.TryGetValue(el.ni.ToString(), out ni) ||
                        !bv.datos.nodos.TryGetValue(el.nj.ToString(), out nj)) continue;
                    if (el.tipo == "vigas_x")
                        beamsXL.Add(new double[] { ni.y, System.Math.Min(ni.x, nj.x), System.Math.Max(ni.x, nj.x) });
                    else
                        beamsYL.Add(new double[] { ni.x, System.Math.Min(ni.y, nj.y), System.Math.Max(ni.y, nj.y) });
                }
            }
            bool[,] enHuella = new bool[nx, ny];
            for (int ix = 0; ix < nx; ix++)
            {
                double ecx0 = xMin + ix * paso, ecx1 = ecx0 + paso;
                double ecx = (ecx0 + ecx1) * 0.5;
                for (int iy = 0; iy < ny; iy++)
                {
                    double ecy0 = yMin + iy * paso, ecy1 = ecy0 + paso;
                    double ecy = (ecy0 + ecy1) * 0.5;
                    bool ok = false;
                    foreach (var b in beamsXL)
                        if (System.Math.Abs(b[0] - ecy) < paso && b[1] < ecx1 && b[2] > ecx0)
                        { ok = true; break; }
                    if (!ok)
                        foreach (var b in beamsYL)
                            if (System.Math.Abs(b[0] - ecx) < paso && b[1] < ecy1 && b[2] > ecy0)
                            { ok = true; break; }
                    enHuella[ix, iy] = ok;
                }
            }

            for (int ix = 0; ix < nx; ix++)
            {
                double cx = xMin + (ix + 0.5) * paso;
                int ii = IndexEn(xs, cx);
                for (int iy = 0; iy < ny; iy++)
                {
                    if (!enHuella[ix, iy]) continue;
                    double cy = yMin + (iy + 0.5) * paso;
                    int jj = IndexEn(ys, cy);
                    double x0 = xs[ii], x1 = xs[ii + 1];
                    double y0 = ys[jj], y1 = ys[jj + 1];
                    double dx = System.Math.Min(cx - x0, x1 - cx);
                    double dy = System.Math.Min(cy - y0, y1 - cy);

                    int vid; bool ex;
                    if (dy < dx)
                    {
                        ex = true;
                        vid = (cy - y0 < y1 - cy) ? jj : jj + 1; // viga X arriba o abajo
                    }
                    else
                    {
                        ex = false;
                        vid = (cx - x0 < x1 - cx) ? ii : ii + 1; // viga Y izq o der
                    }
                    vigaId[ix, iy] = vid;
                    esX[ix, iy] = ex;
                    panelI[ix, iy] = ii;
                    panelJ[ix, iy] = jj;
                }
            }

            // 2) flood fill para regiones conexas de misma viga
            int nreg = 0;
            Stack<int> pila = new Stack<int>();
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iy = 0; iy < ny; iy++)
                {
                    if (!enHuella[ix, iy]) { regionId[ix, iy] = -2; continue; }
                    if (regionId[ix, iy] >= 0) continue;
                    int rid = nreg++;
                    regionId[ix, iy] = rid;
                    pila.Push(ix); pila.Push(iy);
                    int vid = vigaId[ix, iy];
                    bool ex = esX[ix, iy];
                    while (pila.Count > 0)
                    {
                        int iy2 = pila.Pop();
                        int ix2 = pila.Pop();
                        if (ix2 - 1 >= 0 && regionId[ix2 - 1, iy2] < 0 && esX[ix2 - 1, iy2] == ex && vigaId[ix2 - 1, iy2] == vid)
                        { regionId[ix2 - 1, iy2] = rid; pila.Push(ix2 - 1); pila.Push(iy2); }
                        if (ix2 + 1 < nx && regionId[ix2 + 1, iy2] < 0 && esX[ix2 + 1, iy2] == ex && vigaId[ix2 + 1, iy2] == vid)
                        { regionId[ix2 + 1, iy2] = rid; pila.Push(ix2 + 1); pila.Push(iy2); }
                        if (iy2 - 1 >= 0 && regionId[ix2, iy2 - 1] < 0 && esX[ix2, iy2 - 1] == ex && vigaId[ix2, iy2 - 1] == vid)
                        { regionId[ix2, iy2 - 1] = rid; pila.Push(ix2); pila.Push(iy2 - 1); }
                        if (iy2 + 1 < ny && regionId[ix2, iy2 + 1] < 0 && esX[ix2, iy2 + 1] == ex && vigaId[ix2, iy2 + 1] == vid)
                        { regionId[ix2, iy2 + 1] = rid; pila.Push(ix2); pila.Push(iy2 + 1); }
                    }
                }
            }

            if (nreg <= 0) return null;

            // 3) acumular celdas por region: area, centroide, color dominante
            double stepArea = paso * paso;
            List<long> conteo = new List<long>(new long[nreg]);
            List<double> sumX = new List<double>(new double[nreg]);
            List<double> sumY = new List<double>(new double[nreg]);

            // mesh agregado: verts + colores por celda (2 triangulos)
            int totalV = totalCeldas * 4;
            Vector3[] vet = new Vector3[totalV];
            Color[] col = new Color[totalV];
            int[] tri = new int[totalCeldas * 6];
            int vi = 0, ti = 0;

            for (int ix = 0; ix < nx; ix++)
            {
                for (int iy = 0; iy < ny; iy++)
                {
                    if (regionId[ix, iy] < 0) continue;
                    double x0 = xMin + ix * paso, x1 = x0 + paso;
                    double y0 = yMin + iy * paso, y1 = y0 + paso;
                    double cx = (x0 + x1) * 0.5, cy = (y0 + y1) * 0.5;
                    int rid = regionId[ix, iy];
                    conteo[rid]++;
                    sumX[rid] += cx; sumY[rid] += cy;

                    bool ex = esX[ix, iy];
                    Color c = ex ? ColorTributariaX : ColorTributariaY;
                    // esquinas (x,0,y)
                    int baseV = vi;
                    vet[vi] = new Vector3((float)x0, 0f, (float)y0); col[vi] = c; vi++;
                    vet[vi] = new Vector3((float)x1, 0f, (float)y0); col[vi] = c; vi++;
                    vet[vi] = new Vector3((float)x1, 0f, (float)y1); col[vi] = c; vi++;
                    vet[vi] = new Vector3((float)x0, 0f, (float)y1); col[vi] = c; vi++;
                    tri[ti++] = baseV; tri[ti++] = baseV + 1; tri[ti++] = baseV + 2;
                    tri[ti++] = baseV; tri[ti++] = baseV + 2; tri[ti++] = baseV + 3;
                }
            }

            // 4) contornos: quad delgado alrededor de cada segmento de borde de region
            float wc = 0.06f;
            List<Vector3> cb = new List<Vector3>();
            List<Color> cbcol = new List<Color>();
            List<int> cbt = new List<int>();
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iy = 0; iy < ny; iy++)
                {
                    if (regionId[ix, iy] < 0) continue;
                    int rid = regionId[ix, iy];
                    bool izq = ix == 0 || regionId[ix - 1, iy] != rid;
                    bool der = ix == nx - 1 || regionId[ix + 1, iy] != rid;
                    bool abj = iy == 0 || regionId[ix, iy - 1] != rid;
                    bool arr = iy == ny - 1 || regionId[ix, iy + 1] != rid;
                    if (!izq && !der && !abj && !arr) continue;
                    double x0 = xMin + ix * paso, x1 = x0 + paso;
                    double y0 = yMin + iy * paso, y1 = y0 + paso;
                    Color cc = esX[ix, iy] ? ColorContornoTributariaX : ColorContornoTributariaY;
                    if (izq) AddOutlineQuad(cb, cbcol, cbt, x0, y0, x0, y1, -wc, cc);
                    if (der) AddOutlineQuad(cb, cbcol, cbt, x1, y0, x1, y1, wc, cc);
                    if (abj) AddOutlineQuad(cb, cbcol, cbt, x0, y0, x1, y0, -wc, cc);
                    if (arr) AddOutlineQuad(cb, cbcol, cbt, x0, y1, x1, y1, wc, cc);
                }
            }

            // 5) objetos: mesh relleno + mesh contorno + etiquetas
            System.Array.Resize(ref vet, vi);
            System.Array.Resize(ref col, vi);
            System.Array.Resize(ref tri, ti);
            GameObject go = new GameObject("zona_tributaria_nivel_" + lvl);
            go.transform.SetParent(bv.tributariaObj.transform, false);
            Mesh m = new Mesh();
            m.vertices = vet;
            m.colors = col;
            m.triangles = tri;
            m.RecalculateNormals();
            m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialTributariaMosaico();

            GameObject cgo = new GameObject("contorno_nivel_" + lvl);
            cgo.transform.SetParent(bv.tributariaObj.transform, false);
            Vector3[] contArr = null;
            if (cb.Count > 0)
            {
                Mesh cm = new Mesh();
                cm.vertices = cb.ToArray();
                cm.colors = cbcol.ToArray();
                cm.triangles = cbt.ToArray();
                cm.RecalculateNormals();
                cm.RecalculateBounds();
                cgo.AddComponent<MeshFilter>().sharedMesh = cm;
                cgo.AddComponent<MeshRenderer>().sharedMaterial = MaterialTributariaMosaico();
                contArr = cb.ToArray();
            }

            NivelTributarioVisual nv = new NivelTributarioVisual
            {
                go = go,
                contorno = cgo,
                contLocal = contArr,
                vetLocal = vet,
                lvl = lvl,
                z = z,
                bloque = bv,
                celdasLocales = null,
                coloresCelda = null,
                regionDeCelda = null,
                regionCentro = new List<Vector2>(),
                regionArea = new List<double>(),
                regionKN = new List<double>(),
                paso = (float)paso,
                xs = xs,
                ys = ys
            };

            // etiquetas por region (1 TextMesh por region)
            GameObject labelPadre = go;
            for (int r = 0; r < nreg; r++)
            {
                double sx = sumX[r] / conteo[r];
                double sy = sumY[r] / conteo[r];
                double area = conteo[r] * stepArea;
                double kn = qG * area;
                nv.regionCentro.Add(new Vector2((float)sx, (float)sy));
                nv.regionArea.Add(area);

                TextMesh tm = CrearEtiqueta();
                tm.characterSize = 0.06f;
                tm.transform.SetParent(bv.tributariaObj.transform, false);
                tm.transform.position = new Vector3((float)sx, (float)z, (float)sy);
                tm.text = kn.ToString("F0") + " kN";
                if (Camera.main != null)
                    tm.transform.rotation = Camera.main.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
                nv.labels.Add(tm);
            }

            return nv;
        }

        // Añade un quad delgado (rectangulo) entre (x0,y0)-(x1,y1) con grosor "w"
        private static void AddOutlineQuad(List<Vector3> cb, List<Color> cc, List<int> cbt,
            double ax, double ay, double bx, double by, float w, Color colorRgb)
        {
            double dx = bx - ax, dy = by - ay;
            double len = System.Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return;
            double nx = -dy / len, ny = dx / len;
            int b = cb.Count;
            cb.Add(new Vector3((float)(ax + nx * w), 0f, (float)(ay + ny * w)));
            cb.Add(new Vector3((float)(ax - nx * w), 0f, (float)(ay - ny * w)));
            cb.Add(new Vector3((float)(bx - nx * w), 0f, (float)(by - ny * w)));
            cb.Add(new Vector3((float)(bx + nx * w), 0f, (float)(by + ny * w)));
            cc.Add(colorRgb); cc.Add(colorRgb); cc.Add(colorRgb); cc.Add(colorRgb);
            cbt.Add(b); cbt.Add(b + 1); cbt.Add(b + 2);
            cbt.Add(b); cbt.Add(b + 2); cbt.Add(b + 3);
        }

        private static int IndexEn(List<double> arr, double v)
        {
            for (int i = 0; i < arr.Count - 1; i++)
                if (arr[i] <= v && v <= arr[i + 1]) return i;
            return arr.Count - 2;
        }

        private void ReconstruirNivelTributario(NivelTributarioVisual nv, Vector2 off)
        {
            if (nv.go == null) return;
            BloqueVisual bv = nv.bloque;
            bool deformando = casoActivo != CasoDeformacion.Ninguno
                              && bv != null && bv.datos != null
                              && bv.datos.resultados != null
                              && bv.datos.resultados.ContainsKey(casoActivo.ToString())
                              && bv.datos.resultados[casoActivo.ToString()].desplazamientos_maestro != null;

            double masterX = 0.0, masterY = 0.0, ux = 0.0, uy = 0.0, rz = 0.0;
            bool deformarPunto = false;
            if (deformando)
            {
                ResultadoCaso caso = bv.datos.resultados[casoActivo.ToString()];
                string claveNivel = nv.lvl.ToString();
                DesplazamientoNivel d;
                if (caso.desplazamientos_maestro != null && caso.desplazamientos_maestro.TryGetValue(claveNivel, out d))
                {
                    deformarPunto = true;
                    rz = d.rz * amplificacion;
                    ux = d.ux * amplificacion;
                    uy = d.uy * amplificacion;
                    NivelMaestro nm;
                    if (bv.maestros.TryGetValue(nv.lvl, out nm)) { masterX = nm.pos.x; masterY = nm.pos.y; }
                }
            }

            Vector2 apAplicado = deformarPunto
                ? new Vector2(float.MinValue, float.MinValue)
                : off;
            if (!deformarPunto && nv.offsetAplicado.x == off.x && nv.offsetAplicado.y == off.y) return;
            nv.offsetAplicado = apAplicado;

            // re-transformar mesh relleno
            Vector3[] srcV = nv.vetLocal;
            if (srcV != null && srcV.Length > 0)
            {
                Vector3[] tv = new Vector3[srcV.Length];
                for (int k = 0; k < srcV.Length; k++)
                {
                    Vector3 p = srcV[k];
                    double px = p.x, py = p.z;
                    if (!deformarPunto)
                    {
                        tv[k] = new Vector3((float)px + off.x, (float)nv.z, (float)py + off.y);
                        continue;
                    }
                    double c = System.Math.Cos(rz), s = System.Math.Sin(rz);
                    double dx = px - masterX, dy = py - masterY;
                    double rx = dx * c - dy * s;
                    double ry = dx * s + dy * c;
                    tv[k] = new Vector3((float)(masterX + rx + ux + off.x),
                        (float)nv.z, (float)(masterY + ry + uy + off.y));
                }
                Mesh mm = nv.go.GetComponent<MeshFilter>().sharedMesh;
                mm.vertices = tv;
                mm.RecalculateBounds();
                // las normales ya estan; solo desplazamos
            }

            // re-transformar contorno
            Vector3[] srcC = nv.contLocal;
            if (srcC != null && nv.contorno != null)
            {
                Vector3[] tc = new Vector3[srcC.Length];
                for (int k = 0; k < srcC.Length; k++)
                {
                    Vector3 p = srcC[k];
                    double px = p.x, py = p.z;
                    if (!deformarPunto)
                    {
                        tc[k] = new Vector3((float)px + off.x, (float)nv.z, (float)py + off.y);
                        continue;
                    }
                    double c = System.Math.Cos(rz), s = System.Math.Sin(rz);
                    double dx = px - masterX, dy = py - masterY;
                    double rx = dx * c - dy * s;
                    double ry = dx * s + dy * c;
                    tc[k] = new Vector3((float)(masterX + rx + ux + off.x),
                        (float)nv.z, (float)(masterY + ry + uy + off.y));
                }
                Mesh cm = nv.contorno.GetComponent<MeshFilter>().sharedMesh;
                cm.vertices = tc;
                cm.RecalculateBounds();
            }

            // re-posicionar etiquetas (centro de region)
            List<Vector2> rcs = nv.regionCentro;
            for (int r = 0; r < rcs.Count && r < nv.labels.Count; r++)
            {
                Vector2 rc = rcs[r];
                Vector3 pos;
                if (!deformarPunto)
                {
                    pos = new Vector3(rc.x + off.x, (float)nv.z, rc.y + off.y);
                }
                else
                {
                    double c = System.Math.Cos(rz), s = System.Math.Sin(rz);
                    double dx = rc.x - masterX, dy = rc.y - masterY;
                    double rx = dx * c - dy * s;
                    double ry = dx * s + dy * c;
                    pos = new Vector3((float)(masterX + rx + ux + off.x),
                        (float)nv.z, (float)(masterY + ry + uy + off.y));
                }
                nv.labels[r].transform.position = pos;
            }
        }

        private static void PosicionarFlecha(GameObject go, Transform shaft, Transform cono,
            TextMesh txt, Vector3 origen, Vector3 dir, float largo, double valor)
        {
            if (go == null) return;
            go.transform.position = origen;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.down, dir);
            shaft.localScale = new Vector3(0.1f, Mathf.Max(0.2f, largo * 0.5f), 0.1f);
            shaft.localPosition = new Vector3(0f, -largo * 0.5f, 0f);
            cono.localPosition = new Vector3(0f, -largo, 0f);
            txt.transform.localPosition = new Vector3(0f, -largo - 1.2f, 0f);
            txt.text = valor.ToString("F0") + " kN";
            if (Camera.main != null)
                txt.transform.rotation = Camera.main.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
        }

        private void ApuntarEtiquetas()
        {
            if (Camera.main == null) return;
            foreach (BloqueVisual bv in bloques)
            {
                foreach (FlechaVisual f in bv.flechas)
                {
                    if (f.go != null && f.go.activeSelf)
                        f.txt.transform.rotation = Camera.main.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
                }
                foreach (FlechaSismoVisual f in bv.flechasSismo)
                {
                    if (f.go != null && f.go.activeSelf)
                        f.txt.transform.rotation = Camera.main.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
                }
                if (mostrarTributaria && bv.tributariaObj != null && bv.tributariaObj.activeSelf)
                {
                    foreach (NivelTributarioVisual z in bv.zonas)
                    {
                        foreach (TextMesh lm in z.labels)
                            lm.transform.rotation = Camera.main.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
                    }
                }
            }
        }

        private Vector3 Posicion(NodoModelo n, BloqueVisual bv)
        {
            Vector2 basePos = new Vector2((float)n.x + bv.offset.x, (float)n.y + bv.offset.y);
            if (casoActivo == CasoDeformacion.Ninguno || bv.datos.resultados == null)
                return new Vector3(basePos.x, (float)n.z, basePos.y);

            string claveCaso = casoActivo.ToString();
            if (!bv.datos.resultados.ContainsKey(claveCaso)) return new Vector3(basePos.x, (float)n.z, basePos.y);

            ResultadoCaso caso = bv.datos.resultados[claveCaso];
            if (caso.desplazamientos_maestro == null) return new Vector3(basePos.x, (float)n.z, basePos.y);

            string claveNivel = n.nivel.ToString();
            if (!caso.desplazamientos_maestro.ContainsKey(claveNivel)) return new Vector3(basePos.x, (float)n.z, basePos.y);

            DesplazamientoNivel d = caso.desplazamientos_maestro[claveNivel];
            Vector2 m = new Vector2((float)n.x, (float)n.y);
            if (bv.maestros.ContainsKey(n.nivel)) m = bv.maestros[n.nivel].pos;

            double rz = d.rz * amplificacion;
            double dx = n.x - m.x;
            double dy = n.y - m.y;
            double cos = System.Math.Cos(rz);
            double sin = System.Math.Sin(rz);
            double rx = dx * cos - dy * sin;
            double ry = dx * sin + dy * cos;

            double unx = m.x + rx + d.ux * amplificacion + bv.offset.x;
            double uny = m.y + ry + d.uy * amplificacion + bv.offset.y;
            return new Vector3((float)unx, (float)n.z, (float)uny);
        }

        private void AplicarVisual()
        {
            foreach (BloqueVisual bv in bloques)
            {
                if (bv.indice > 0 && separarBloquesY > 0f)
                {
                    bv.offset.y = (float)bv.datos.offset.y + separarBloquesY;
                }

                foreach (KeyValuePair<string, GameObject> kv in bv.contenedor)
                {
                    bool activo = true;
                    switch (kv.Key)
                    {
                        case "column": activo = mostrarColumnas; break;
                        case "wall": activo = mostrarMuros; break;
                        case "vigas_x": activo = mostrarVigasX; break;
                        case "vigas_y": activo = mostrarVigasY; break;
                        case "cargas": activo = mostrarCargasG || mostrarCargasQ || mostrarCargasSismo; break;
                    }
                    kv.Value.SetActive(activo);
                }
                if (bv.tributariaObj != null) bv.tributariaObj.SetActive(mostrarTributaria);
                bv.nodosObj.SetActive(mostrarNodos);
                bv.apoyosObj.SetActive(mostrarApoyos);
                if (bv.sueloObj != null) bv.sueloObj.SetActive(mostrarSuelo);

                foreach (ApoyoVisual av in bv.apoyos)
                {
                    Vector3 p = Posicion(av.nodo, bv);
                    float abajo = av.tipo == "fijo" ? 1.5f : 0.75f;
                    av.objeto.transform.position = new Vector3(p.x, p.y - abajo, p.z);
                }

                if (bv.suelo != null && bv.apoyos.Count > 0)
                {
                    float gxmin = float.MaxValue, gxmax = float.MinValue;
                    float gymin = float.MaxValue, gymax = float.MinValue;
                    foreach (ApoyoVisual av in bv.apoyos)
                    {
                        float gx = (float)av.nodo.x + bv.offset.x;
                        float gy = (float)av.nodo.y + bv.offset.y;
                        if (gx < gxmin) gxmin = gx;
                        if (gx > gxmax) gxmax = gx;
                        if (gy < gymin) gymin = gy;
                        if (gy > gymax) gymax = gy;
                    }
                    Vector3 centro = new Vector3((gxmin + gxmax) / 2f, bv.zMin - 2.2f, (gymin + gymax) / 2f);
                    Vector3 tamano = new Vector3(gxmax - gxmin + 8f, 0.2f, gymax - gymin + 8f);
                    bv.suelo.transform.position = centro;
                    bv.suelo.transform.localScale = tamano;
                }

                foreach (PanelVisual pv in bv.paneles)
                {
                    ReconstruirPanel(pv, bv.offset);
                }

                foreach (NivelTributarioVisual ztv in bv.zonas)
                {
                    ReconstruirNivelTributario(ztv, bv.offset);
                }

                foreach (KeyValuePair<string, List<ElementoVisual3D>> kv in bv.porTipo)
                {
                    foreach (ElementoVisual3D ev in kv.Value)
                    {
                        NodoModelo ni = ObtenerNodo(bv.datos, ev.datos.ni);
                        NodoModelo nj = ObtenerNodo(bv.datos, ev.datos.nj);
                        if (ni == null || nj == null) continue;
                        PosicionarElemento3D(ev.objeto, ni, nj, bv);
                    }
                }

                foreach (KeyValuePair<string, GameObject> kvn in bv.nodosVis)
                {
                    NodoModelo nd = ObtenerNodo(bv.datos, int.Parse(kvn.Key));
                    if (nd == null) continue;
                    kvn.Value.transform.position = Posicion(nd, bv);
                }

                bv.cargasObj.SetActive(mostrarCargasG || mostrarCargasQ || mostrarCargasSismo);
                foreach (FlechaVisual fv in bv.flechas)
                {
                    bool activa = fv.esQ ? mostrarCargasQ : mostrarCargasG;
                    fv.go.SetActive(activa);
                    if (!activa) continue;
                    NodoModelo ni = ObtenerNodo(bv.datos, fv.el.ni);
                    NodoModelo nj = ObtenerNodo(bv.datos, fv.el.nj);
                    if (ni == null || nj == null) continue;
                    Vector3 a = Posicion(ni, bv);
                    Vector3 b = Posicion(nj, bv);
                    Vector3 medio = (a + b) * 0.5f + new Vector3(0f, 0.35f, 0f);
                    PosicionarFlecha(fv.go, fv.shaft, fv.cono, fv.txt,
                        fv.esQ ? medio + fv.offsetPerp : medio, Vector3.down,
                        (float)fv.valor * escalaCargas, fv.valor);
                }
                foreach (FlechaSismoVisual fv in bv.flechasSismo)
                {
                    bool esSeismo = casoActivo == CasoDeformacion.EX || casoActivo == CasoDeformacion.EY;
                    bool activa = mostrarCargasSismo && esSeismo
                                  && Mathf.Abs((float)fv.valor) > 50f
                                  && (fv.enX ? casoActivo == CasoDeformacion.EX
                                             : casoActivo == CasoDeformacion.EY);
                    fv.go.SetActive(activa);
                    if (!activa) continue;
                    Vector3 dir = fv.enX ? Vector3.right : Vector3.forward;
                    Vector3 p = Posicion(fv.nodo, bv) + new Vector3(0f, 0.7f, 0f);
                    PosicionarFlecha(fv.go, fv.shaft, fv.cono, fv.txt, p, dir,
                        (float)fv.valor * EscalaSismoCargas, fv.valor);
                }
            }
            sucio = false;
        }

        private static Material MaterialVigaSeleccionada()
        {
            if (matVigaSeleccionada == null)
            {
                matVigaSeleccionada = new Material(Shader.Find("Standard"));
                if (matVigaSeleccionada == null) matVigaSeleccionada = new Material(Shader.Find("Unlit/Color"));
                matVigaSeleccionada.color = new Color(1f, 0.90f, 0.25f);
                matVigaSeleccionada.SetFloat("_Glossiness", 0.5f);
                if (matVigaSeleccionada.HasProperty("_EmissionColor"))
                {
                    matVigaSeleccionada.EnableKeyword("_EMISSION");
                    matVigaSeleccionada.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.05f) * 0.6f);
                }
            }
            return matVigaSeleccionada;
        }

        // --- Selección de viga por raycast (click izquierdo) ---
        private void ProcesarSeleccionViga()
        {
            if (!Application.isPlaying) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) return;
            if (!inspeccionHabilitada) return;
            if (Camera.main == null) return;

            Vector3 mp = Input.mousePosition;
            mp.y = Screen.height - mp.y;
            Rect rectPanel = new Rect(16, 16, 380, 700);
            Rect rectCam = new Rect(Screen.width - 270, 16, 270, 200);
            if (rectPanel.Contains(mp) || rectCam.Contains(mp)) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 1e9f))
            {
                GameObject go = hit.collider.gameObject;
                VigaSeleccionable vs;
                if (vigasPorObjeto.TryGetValue(go, out vs))
                {
                    SeleccionarViga(vs);
                    return;
                }
            }
            DeseleccionarViga();
        }

        private void SeleccionarViga(VigaSeleccionable vs)
        {
            if (vigaSeleccionada == vs) return;
            DeseleccionarViga();
            vigaSeleccionada = vs;
            Renderer r = vs.ev != null && vs.ev.objeto != null
                ? vs.ev.objeto.GetComponent<Renderer>() : null;
            if (r != null) r.sharedMaterial = MaterialVigaSeleccionada();
            sucio = false;
        }

        private void DeseleccionarViga()
        {
            if (vigaSeleccionada == null) return;
            Renderer r = vigaSeleccionada.ev != null && vigaSeleccionada.ev.objeto != null
                ? vigaSeleccionada.ev.objeto.GetComponent<Renderer>() : null;
            if (r != null) r.sharedMaterial = MaterialViga();
            vigaSeleccionada = null;
        }

        private EsfuerzosVigaModelo ObtenerEsfuerzosConsulta(int tag)
        {
            if (vigaSeleccionada == null || vigaSeleccionada.bloque == null
                || vigaSeleccionada.bloque.datos == null) return null;
            ModeloEdificio ed = vigaSeleccionada.bloque.datos;
            if (ed.esfuerzos == null || ed.esfuerzos.Count == 0) return null;
            string caso = EtiquetasCaso[casoConsulta];
            Dictionary<string, EsfuerzosVigaModelo> porTag;
            if (!ed.esfuerzos.TryGetValue(caso, out porTag)) return null;
            EsfuerzosVigaModelo esf;
            if (!porTag.TryGetValue(tag.ToString(), out esf)) return null;
            return esf;
        }

        private void ReconstruirPanelesDeformados()
        {
            foreach (BloqueVisual bv in bloques)
            {
                if (bv.casoDeformacionPrevia != casoActivo)
                {
                    foreach (PanelVisual pv in bv.paneles)
                        pv.offsetAplicado = new Vector2(float.MinValue, float.MinValue);
                    bv.casoDeformacionPrevia = casoActivo;
                }
                foreach (PanelVisual pv in bv.paneles)
                    ReconstruirPanel(pv, bv.offset);
            }
        }

        private static GameObject CrearZapata()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destruir(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(1.2f, 1.5f, 1.2f);
            go.GetComponent<Renderer>().sharedMaterial = MaterialZapata();
            return go;
        }

        private static GameObject CrearCono(float radioBase, float altura, Color color)
        {
            GameObject go = new GameObject("cono");
            int seg = 24;
            Vector3[] verts = new Vector3[seg + 2];
            int[] tris = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                verts[i] = new Vector3(Mathf.Cos(a) * radioBase, 0f, Mathf.Sin(a) * radioBase);
            }
            verts[seg] = Vector3.zero;
            verts[seg + 1] = new Vector3(0f, altura, 0f);
            int t = 0;
            for (int i = 0; i < seg; i++)
            {
                int i2 = (i + 1) % seg;
                tris[t++] = seg; tris[t++] = i2; tris[t++] = i;
                tris[t++] = i; tris[t++] = i2; tris[t++] = seg + 1;
            }
            Mesh mesh = new Mesh();
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialColor(color);
            return go;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 380, 700), GUI.skin.box);
            GUILayout.Label("Edificio de Ingenieria - OpenSees -> Unity");

            bool cambio = false;

            // Toggles de visibilidad en grilla compacta de 2 columnas
            GUIStyle togleStyle = new GUIStyle(GUI.skin.toggle);
            togleStyle.margin = new RectOffset(2, 2, 0, 2);
            GUIStyle tituloStyle = new GUIStyle(GUI.skin.label);
            tituloStyle.fontStyle = FontStyle.Bold;
            GUIStyle valorStyle = new GUIStyle(GUI.skin.label);
            valorStyle.fontStyle = FontStyle.Bold;
            valorStyle.fontSize = 13;

            bool col = false, vx = false, vy = false, murs = false, apos = false;
            bool suel = false, nds = false, cg = false, cq = false, csm = false, ctr = false;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            col = GUILayout.Toggle(mostrarColumnas, "Columnas", togleStyle);
            vx = GUILayout.Toggle(mostrarVigasX, "Vigas X", togleStyle);
            murs = GUILayout.Toggle(mostrarMuros, "Muros", togleStyle);
            apos = GUILayout.Toggle(mostrarApoyos, "Apoyos", togleStyle);
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            vy = GUILayout.Toggle(mostrarVigasY, "Vigas Y", togleStyle);
            suel = GUILayout.Toggle(mostrarSuelo, "Terreno", togleStyle);
            nds = GUILayout.Toggle(mostrarNodos, "Nodos", togleStyle);
            ctr = GUILayout.Toggle(mostrarTributaria, "Tributaria 45", togleStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            cg = GUILayout.Toggle(mostrarCargasG, "Cargas G", togleStyle);
            cq = GUILayout.Toggle(mostrarCargasQ, "Cargas Q", togleStyle);
            csm = GUILayout.Toggle(mostrarCargasSismo, "Cargas sismo", togleStyle);
            GUILayout.EndHorizontal();

            if (col != mostrarColumnas) { mostrarColumnas = col; cambio = true; }
            if (vx != mostrarVigasX) { mostrarVigasX = vx; cambio = true; }
            if (vy != mostrarVigasY) { mostrarVigasY = vy; cambio = true; }
            if (murs != mostrarMuros) { mostrarMuros = murs; cambio = true; }
            if (apos != mostrarApoyos) { mostrarApoyos = apos; cambio = true; }
            if (suel != mostrarSuelo) { mostrarSuelo = suel; cambio = true; }
            if (nds != mostrarNodos) { mostrarNodos = nds; cambio = true; }
            if (cg != mostrarCargasG) { mostrarCargasG = cg; cambio = true; }
            if (cq != mostrarCargasQ) { mostrarCargasQ = cq; cambio = true; }
            if (csm != mostrarCargasSismo) { mostrarCargasSismo = csm; cambio = true; }
            if (ctr != mostrarTributaria) { mostrarTributaria = ctr; cambio = true; }

            GUILayout.Space(4);
            GUILayout.Label("Deformada por caso:", tituloStyle);
            float nuevaAmp = amplificacion;
            CasoDeformacion nuevoCaso = casoActivo;
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(casoActivo == CasoDeformacion.Ninguno, "Base")) nuevoCaso = CasoDeformacion.Ninguno;
            if (GUILayout.Toggle(casoActivo == CasoDeformacion.G, "G")) nuevoCaso = CasoDeformacion.G;
            if (GUILayout.Toggle(casoActivo == CasoDeformacion.GQ, "GQ")) nuevoCaso = CasoDeformacion.GQ;
            if (GUILayout.Toggle(casoActivo == CasoDeformacion.EX, "EX")) nuevoCaso = CasoDeformacion.EX;
            if (GUILayout.Toggle(casoActivo == CasoDeformacion.EY, "EY")) nuevoCaso = CasoDeformacion.EY;
            GUILayout.EndHorizontal();
            if (nuevoCaso != casoActivo) { casoActivo = nuevoCaso; cambio = true; }

            if (casoActivo != CasoDeformacion.Ninguno)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Amp:", GUILayout.Width(38));
                nuevaAmp = GUILayout.HorizontalSlider(amplificacion, 0f, 2000f);
                GUILayout.Label(amplificacion.ToString("F0"), GUILayout.Width(40));
                GUILayout.EndHorizontal();
            }

            float nuevaSep = separarBloquesY;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sep bloques Y:", GUILayout.Width(92));
            nuevaSep = GUILayout.HorizontalSlider(separarBloquesY, 0f, 120f);
            GUILayout.Label(separarBloquesY.ToString("F0"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            if (Mathf.Abs(nuevaAmp - amplificacion) > 0.001f) { amplificacion = nuevaAmp; cambio = true; }
            if (Mathf.Abs(nuevaSep - separarBloquesY) > 0.001f) { separarBloquesY = nuevaSep; cambio = true; }

            GUILayout.Space(4);
            if (GUILayout.Button("Recargar JSON")) Cargar();

            GUILayout.Space(4);
            GUILayout.Label(mensaje);

            GUILayout.Space(4);
            bool insp = GUILayout.Toggle(inspeccionHabilitada, "Consulta de viga (click izquierdo)");
            if (insp != inspeccionHabilitada) inspeccionHabilitada = insp;

            if (inspeccionHabilitada && vigaSeleccionada != null)
            {
                ElementoVisual3D ev = vigaSeleccionada.ev;
                ModeloEdificio ed = vigaSeleccionada.bloque.datos;
                NodoModelo ni = null;
                if (ed != null && ev != null && ev.datos != null)
                    ni = ObtenerNodo(ed, ev.datos.ni);

                GUILayout.Space(6);
                GUILayout.Box("Consulta de viga", tituloStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Bloque: " + (ed != null ? ed.bloque : "-"));
                GUILayout.Label("Tag: " + (ev != null && ev.datos != null ? ev.datos.tag.ToString() : "-"));
                GUILayout.Label("Nivel: " + (ni != null ? ni.nivel.ToString() : "-"));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Caso:", GUILayout.Width(38));
                for (int c = 0; c < EtiquetasCaso.Length; c++)
                {
                    if (GUILayout.Toggle(casoConsulta == c, EtiquetasCaso[c])) casoConsulta = c;
                }
                GUILayout.EndHorizontal();

                EsfuerzosVigaModelo esf = null;
                if (ev != null && ev.datos != null) esf = ObtenerEsfuerzosConsulta(ev.datos.tag);
                if (esf == null || esf.L <= 0.0)
                {
                    GUILayout.Label("Sin datos de esfuerzos");
                }
                else
                {
                    float L = (float)esf.L;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("L = " + L.ToString("F2") + " m", GUILayout.Width(80));
                    GUILayout.Label("x = " + (posConsulta * 100f).ToString("F0") + "%", GUILayout.Width(60));
                    posConsulta = GUILayout.HorizontalSlider(posConsulta, 0f, 1f);
                    GUILayout.EndHorizontal();
                    float x = posConsulta * L;
                    double Nx = -esf.N;
                    double Vzx = esf.Vz + esf.Wz * x;
                    double Myx = esf.My + esf.Vz * x + 0.5 * esf.Wz * x * x;
                    double Vyx = esf.Vy + esf.Wy * x;
                    double Mzx = esf.Mz + esf.Vy * x + 0.5 * esf.Wy * x * x;

                    GUILayout.Space(3);
                    GUILayout.Label("N(x)  = " + Nx.ToString("F1") + " kN", valorStyle);
                    GUILayout.Label("Vz(x) = " + Vzx.ToString("F1") + " kN", valorStyle);
                    GUILayout.Label("Vy(x) = " + Vyx.ToString("F1") + " kN", valorStyle);
                    GUILayout.Label("My(x) = " + Myx.ToString("F1") + " kN\u00b7m", valorStyle);
                    GUILayout.Label("Mz(x) = " + Mzx.ToString("F1") + " kN\u00b7m", valorStyle);
                    GUILayout.Label("T(x)  = " + esf.T.ToString("F1") + " kN\u00b7m", valorStyle);
                }
            }
            GUILayout.EndArea();

            if (cambio) sucio = true;
        }
    }
}
