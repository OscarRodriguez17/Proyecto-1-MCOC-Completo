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
        public bool mostrarBrazos = true;
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

        // --- Consulta P–M de pilares (motor de secciones, Edificio B) ---
        private class ElementoConsulta
        {
            public BloqueVisual bloque;
            public ElementoVisual3D ev;
        }
        private Dictionary<GameObject, ElementoConsulta> elementosPorObjeto = new Dictionary<GameObject, ElementoConsulta>();
        private ElementoConsulta elementoSeleccionado;
        private static Material matElementoSeleccionado;

        // --- Consulta unificada Semana 04: CUALQUIER elemento estructural
        //     (column/muro/viga/aspa) con esfuerzos_completos + metadatos.
        //     `ev` es null para los muros (se dibujan por paneles), pero el
        //     selector manual de tag los alcanza igual por `tag`. ---
        private class ConsultaS4
        {
            public BloqueVisual bloque;
            public ElementoVisual3D ev;
            public string tag;
        }
        private Dictionary<GameObject, ConsultaS4> consultasPorObjeto = new Dictionary<GameObject, ConsultaS4>();
        private Dictionary<GameObject, PanelVisual> panelesPorObjeto = new Dictionary<GameObject, PanelVisual>();
        private ConsultaS4 consultaSeleccionada;
        private static Material matConsultaSeleccionada;
        private bool mostrarDiagramas = true;
        private string claveDiagramas = "";
        private Texture2D texPM;
        private string texPMClave = "";
        private const float anchoPM = 240f;
        private const float altoPM = 170f;
        private static readonly string[] EtiquetasCaso = { "G", "Q", "GQ", "EX", "EY" };
        private int casoConsulta = 2;              // GQ por defecto
        private float posConsulta = 0.5f;          // fracción 0..1 de la viga
        private bool inspeccionHabilitada = true;

        // --- Ventana de diagramas (Semana 04, UI extra) ---
        private bool mostrarVentanaD = true;                 // visibilidad
        private Rect rectVentanaD = new Rect(420, 120, 380, 340);
        private const int idVentanaD = 51001;
        private int tipoEsfuerzoD = 0;      // 0=Axial(N), 1=Corte(V), 2=Momento(M)
        private int compEsfuerzoD = 0;      // corte: 0=Vz 1=Vy | momento: 0=My 1=Mz
        private Texture2D texDiagramaD;
        private const float anchoD = 340f, altoD = 170f;

        // --- Ventana P-M (Semana 04): diagrama de interaccion en ventana
        //     propia, arrastrable, para que no quede cortado por el panel. ---
        private bool mostrarVentanaPM = true;
        private Rect rectVentanaPM = new Rect(830, 120, 290, 330);
        private const int idVentanaPM = 51002;

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
            public Dictionary<string, ConsultaS4> consultaPorTag = new Dictionary<string, ConsultaS4>();
            public List<string> tagsEstructurales = new List<string>();
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
            public GameObject diagramasObj;
        }

        private static Mesh meshCaja;
        private static Mesh meshVigaT;
        private static Mesh meshVigaL;
        private static Mesh coneCargaMesh;
        private static Mesh meshCilindroFino;
        private static Material matColumna;
        private static Material matViga;
        private static Material matMuro;
        private static Material matBrazo;
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
            elementosPorObjeto.Clear();
            elementoSeleccionado = null;
            consultasPorObjeto.Clear();
            panelesPorObjeto.Clear();
            consultaSeleccionada = null;
            claveDiagramas = "";
            texPM = null;
            texPMClave = "";

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

                foreach (string tipo in new[] { "column", "wall", "vigas_x", "vigas_y", "brazo" })
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
                bv.diagramasObj = new GameObject("diagramas3d");
                bv.diagramasObj.transform.SetParent(root.transform, false);

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
                    else if (el.tipo == "brazo")
                    {
                        elemObj = CrearBrazo3D(bv.contenedor[el.tipo], ni, nj, bv);
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
                    else if (el.tipo == "column")
                    {
                        elementosPorObjeto[elemObj] = new ElementoConsulta { bloque = bv, ev = ev };
                    }
                }

                // --- Semana 04: diccionario unificado por tag de TODOS los
                //     elementos estructurales (los muros sin `objeto`, ya que
                //     se dibujan por paneles). Sirve de lista y de acceso
                //     rapido para la consulta unificada. ---
                bv.tagsEstructurales.Clear();
                bv.consultaPorTag.Clear();
                foreach (ElementoModelo el in ed.elementos)
                {
                    if (el.tipo != "column" && el.tipo != "wall"
                        && el.tipo != "vigas_x" && el.tipo != "vigas_y") continue;
                    string tk = el.tag.ToString();
                    ElementoVisual3D ev = null;
                    GameObject obj = null;
                    if (el.tipo != "wall")
                    {
                        List<ElementoVisual3D> lista;
                        if (bv.porTipo.TryGetValue(el.tipo, out lista))
                            foreach (ElementoVisual3D e in lista)
                                if (e.datos.tag == el.tag) { ev = e; obj = e.objeto; break; }
                    }
                    ConsultaS4 cs4 = new ConsultaS4 { bloque = bv, ev = ev, tag = tk };
                    bv.consultaPorTag[tk] = cs4;
                    if (obj != null) consultasPorObjeto[obj] = cs4;
                }
                foreach (KeyValuePair<string, ConsultaS4> kv in bv.consultaPorTag)
                    bv.tagsEstructurales.Add(kv.Key);
                bv.tagsEstructurales.Sort((a, b) => int.Parse(a).CompareTo(int.Parse(b)));

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
            // Se CONSERVA el collider para poder consultar el pilar por raycast
            // (la barra de vigas ya tiene su propio collider).
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

        private static Material MaterialBrazo()
        {
            if (matBrazo == null)
            {
                matBrazo = new Material(Shader.Find("Standard"));
                if (matBrazo == null) matBrazo = new Material(Shader.Find("Unlit/Color"));
                matBrazo.color = new Color(0.75f, 0.78f, 0.82f, 0.45f);
                matBrazo.SetFloat("_Glossiness", 0.1f);
                if (matBrazo.HasProperty("_Mode"))
                {
                    matBrazo.SetFloat("_Mode", 3f);
                    matBrazo.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    matBrazo.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    matBrazo.SetInt("_ZWrite", 0);
                    matBrazo.DisableKeyword("_ALPHATEST_ON");
                    matBrazo.EnableKeyword("_ALPHABLEND_ON");
                    matBrazo.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    matBrazo.renderQueue = 3000;
                }
            }
            return matBrazo;
        }

        // Brazos rígidos: cilindro fino que une el muro con el marco, con material
        // tenue, para que la conexión quede visible.
        private GameObject CrearBrazo3D(GameObject padre, NodoModelo ni, NodoModelo nj, BloqueVisual bv)
        {
            GameObject go = new GameObject("brazo_rigido");
            go.transform.SetParent(padre.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = CilindroFinoMesh();
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialBrazo();
            go.transform.localScale = new Vector3(0.20f, 1f, 0.20f);
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

            // --- Semana 04: collider de seleccion por clic. BoxCollider fijo
            //     al muro SIN deformar (la deformada solo reconstruye el mesh);
            //     el transform del GO/contenedor es identidad, asi que las
            //     coords del collider coinciden con el mesh (que ya embebe
            //     bv.offset). Tambien se registra el GO -> panel en el
            //     diccionario de raycast. ---
            float bx = (float)bv.offset.x, by = (float)bv.offset.y;
            BoxCollider bc = go.AddComponent<BoxCollider>();
            float zc0 = (float)zs[0], zc1 = (float)zs[zs.Count - 1];
            bc.center = new Vector3((float)xc + bx, (zc0 + zc1) / 2f, (float)yc + by);
            bc.size = resisteY
                ? new Vector3((float)t, zc1 - zc0, (float)L)
                : new Vector3((float)L, zc1 - zc0, (float)t);
            panelesPorObjeto[go] = p;

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

        // --- Semana 04: clic sobre un panel de muro -> elemento del muro en el
        //     NIVEL clickeado. El panel abarca todas las alturas del muro;
        //     hit.point.y es la cota z (misma referencia que `niveles_z`), que
        //     resuelve el nivel y el elemento de muro de esa linea en ese nivel
        //     (match por posicion de planta contra nodos y por z del nodo base).
        //     Si no se halla, cae al elemento base como respaldo y el ◀/▶ recorre
        //     los demas niveles. ---
        private void SeleccionarElementoDeMuro(PanelVisual p, float yWorld)
        {
            if (p == null || p.bloque == null || p.bloque.datos == null
                || p.zs == null || p.zs.Count < 2) return;
            ModeloEdificio ed = p.bloque.datos;

            int st = 0;
            for (int i = 0; i < p.zs.Count - 1; i++)
                if (yWorld + 1e-3f >= p.zs[i]) st = i;
            if (yWorld > p.zs[p.zs.Count - 1] + 1e-3f) st = p.zs.Count - 2;

            string tag = null;
            string tagBase = null;
            int nivelBase = int.MaxValue;
            double tolEje = p.t / 2.0 + 1e-3;
            foreach (ElementoModelo el in ed.elementos)
            {
                if (el.tipo != "wall") continue;
                NodoModelo ni = ObtenerNodo(ed, el.ni);
                if (ni == null) continue;

                bool plan;
                if (p.resisteY)
                    plan = System.Math.Abs(ni.x - p.xc) <= tolEje
                        && ni.y >= p.yc - p.L / 2.0 - 1e-3
                        && ni.y <= p.yc + p.L / 2.0 + 1e-3;
                else
                    plan = System.Math.Abs(ni.y - p.yc) <= tolEje
                        && ni.x >= p.xc - p.L / 2.0 - 1e-3
                        && ni.x <= p.xc + p.L / 2.0 + 1e-3;
                if (!plan) continue;

                if (tagBase == null || ni.nivel < nivelBase)
                {
                    nivelBase = ni.nivel;
                    tagBase = el.tag.ToString();
                }
                if (System.Math.Abs(ni.z - p.zs[st]) <= 1e-3)
                {
                    tag = el.tag.ToString();
                    break;
                }
            }
            if (tag == null) tag = tagBase;
            if (tag == null) return;

            ConsultaS4 cs4;
            if (p.bloque.consultaPorTag.TryGetValue(tag, out cs4))
                SeleccionarConsulta(cs4);
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

        // Cilindro fino de ALTURA 1 (eje Y) para los brazos rígidos:
        // PosicionarElemento3D escala el eje Y con la longitud real del tramo.
        private static Mesh CilindroFinoMesh()
        {
            if (meshCilindroFino != null) return meshCilindroFino;
            int seg = 12;
            const float radio = 0.5f, altura = 1f;
            Vector3[] verts = new Vector3[seg * 2 + 2];
            int[] tris = new int[seg * 12];
            for (int i = 0; i < seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                verts[i] = new Vector3(Mathf.Cos(a) * radio, altura * 0.5f, Mathf.Sin(a) * radio);
                verts[seg + i] = new Vector3(Mathf.Cos(a) * radio, -altura * 0.5f, Mathf.Sin(a) * radio);
            }
            verts[seg * 2] = new Vector3(0f, altura * 0.5f, 0f);
            verts[seg * 2 + 1] = new Vector3(0f, -altura * 0.5f, 0f);
            int t = 0;
            for (int i = 0; i < seg; i++)
            {
                int i2 = (i + 1) % seg;
                tris[t++] = i; tris[t++] = i2; tris[t++] = i + seg;
                tris[t++] = i2; tris[t++] = i2 + seg; tris[t++] = i + seg;
                tris[t++] = seg * 2; tris[t++] = i2; tris[t++] = i;
                tris[t++] = seg * 2 + 1; tris[t++] = i + seg; tris[t++] = i2 + seg;
            }
            meshCilindroFino = new Mesh();
            meshCilindroFino.vertices = verts;
            meshCilindroFino.triangles = tris;
            meshCilindroFino.RecalculateNormals();
            meshCilindroFino.RecalculateBounds();
            return meshCilindroFino;
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
                        case "brazo": activo = mostrarBrazos; break;
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
                ConsultaS4 cs4;
                if (consultasPorObjeto.TryGetValue(go, out cs4))
                {
                    SeleccionarConsulta(cs4);
                    return;
                }
                PanelVisual pv;
                if (panelesPorObjeto.TryGetValue(go, out pv))
                {
                    SeleccionarElementoDeMuro(pv, hit.point.y);
                    return;
                }
                VigaSeleccionable vs;
                if (vigasPorObjeto.TryGetValue(go, out vs))
                {
                    SeleccionarViga(vs);
                    return;
                }
                ElementoConsulta elc;
                if (elementosPorObjeto.TryGetValue(go, out elc))
                {
                    SeleccionarElemento(elc);
                    return;
                }
            }
            DeseleccionarViga();
            DeseleccionarElemento();
            DeseleccionarConsulta();
        }

        private void SeleccionarElemento(ElementoConsulta elc)
        {
            if (elementoSeleccionado == elc) return;
            DeseleccionarViga();
            DeseleccionarElemento();
            elementoSeleccionado = elc;
            Renderer r = elc.ev != null && elc.ev.objeto != null
                ? elc.ev.objeto.GetComponent<Renderer>() : null;
            if (r != null) r.sharedMaterial = MaterialElementoSeleccionado();
        }

        private void SeleccionarConsulta(ConsultaS4 cs4)
        {
            if (consultaSeleccionada == cs4) return;
            DeseleccionarViga();
            DeseleccionarElemento();
            DeseleccionarConsulta();
            consultaSeleccionada = cs4;
            if (cs4 != null)
            {
                Renderer r = cs4.ev != null && cs4.ev.objeto != null
                    ? cs4.ev.objeto.GetComponent<Renderer>() : null;
                if (r != null) r.sharedMaterial = MaterialConsultaSeleccionada();
            }
        }

        private void DeseleccionarConsulta()
        {
            if (consultaSeleccionada == null) return;
            Renderer r = consultaSeleccionada.ev != null && consultaSeleccionada.ev.objeto != null
                ? consultaSeleccionada.ev.objeto.GetComponent<Renderer>() : null;
            if (r != null)
            {
                bool col = consultaSeleccionada.ev.datos != null
                    && consultaSeleccionada.ev.datos.tipo == "column";
                r.sharedMaterial = col ? MaterialColumna() : MaterialViga();
            }
            consultaSeleccionada = null;
        }

        private static Material MaterialConsultaSeleccionada()
        {
            if (matConsultaSeleccionada == null)
            {
                matConsultaSeleccionada = new Material(Shader.Find("Standard"));
                if (matConsultaSeleccionada == null)
                    matConsultaSeleccionada = new Material(Shader.Find("Unlit/Color"));
                matConsultaSeleccionada.color = new Color(1f, 0.9f, 0.15f);
            }
            return matConsultaSeleccionada;
        }

        private void DeseleccionarElemento()
        {
            if (elementoSeleccionado == null) return;
            Renderer r = elementoSeleccionado.ev != null && elementoSeleccionado.ev.objeto != null
                ? elementoSeleccionado.ev.objeto.GetComponent<Renderer>() : null;
            if (r != null) r.sharedMaterial = MaterialColumna();
            elementoSeleccionado = null;
        }

        private static Material MaterialElementoSeleccionado()
        {
            if (matElementoSeleccionado == null)
            {
                matElementoSeleccionado = new Material(Shader.Find("Standard"));
                if (matElementoSeleccionado == null)
                    matElementoSeleccionado = new Material(Shader.Find("Unlit/Color"));
                matElementoSeleccionado.color = new Color(0.95f, 0.65f, 0.10f);
            }
            return matElementoSeleccionado;
        }

        private void SeleccionarViga(VigaSeleccionable vs)
        {
            if (vigaSeleccionada == vs) return;
            DeseleccionarViga();
            DeseleccionarElemento();
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

        /// <summary>
        /// Coeficientes de esfuerzos del elemento `tag` para el caso activo:
        ///  1) `esfuerzos_completos` (Semana 04, TODOS los elementos), con
        ///     fallback a `esfuerzos` (Semana 03, solo vigas del Edificio B).
        /// Devuelve null si el edificio no tiene datos para ese tag/caso.
        /// </summary>
        private EsfuerzosVigaModelo ObtenerEsfuerzosS4(ModeloEdificio ed, string tag)
        {
            if (ed == null) return null;
            string caso = EtiquetasCaso[casoConsulta];
            Dictionary<string, EsfuerzosVigaModelo> porTag;
            if (ed.esfuerzosCompletos != null
                && ed.esfuerzosCompletos.Count > 0
                && ed.esfuerzosCompletos.TryGetValue(caso, out porTag))
            {
                EsfuerzosVigaModelo esf;
                if (porTag.TryGetValue(tag, out esf)) return esf;
            }
            if (ed.esfuerzos != null && ed.esfuerzos.Count > 0
                && ed.esfuerzos.TryGetValue(caso, out porTag))
            {
                EsfuerzosVigaModelo esf;
                if (porTag.TryGetValue(tag, out esf)) return esf;
            }
            return null;
        }

        private EsfuerzosVigaModelo ObtenerEsfuerzosConsulta(int tag)
        {
            if (vigaSeleccionada == null || vigaSeleccionada.bloque == null
                || vigaSeleccionada.bloque.datos == null) return null;
            return ObtenerEsfuerzosS4(vigaSeleccionada.bloque.datos, tag.ToString());
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

        // ------------------------------------------------------------------
        // Panel P–M (motor de secciones): catalogo + demandas + punto activo.
        // En Semana 04 se consulta POR TAG dentro de la consulta unificada.
        // ------------------------------------------------------------------
        private string ClavePMTag(BloqueVisual bv, string tag)
        {
            return bv != null ? bv.indice + ":" + tag + ":" + casoConsulta : "";
        }

        private ElementoInteraccion SeccionDesdeTag(ModeloEdificio ed, string tag, out SeccionInteraccion sec)
        {
            sec = null;
            if (ed == null || ed.secciones == null || ed.secciones.elementos == null) return null;
            ElementoInteraccion el;
            if (string.IsNullOrEmpty(tag) || !ed.secciones.elementos.TryGetValue(tag, out el)) return null;
            // El catalogo semana03 trae la curva P-M de cada seccion real
            // (col_A_0.70x0.70, muro0.20x11.59, ...). El overlay agrega la clave
            // real del pilar como alias de la representativa ('col0.70x0.70' ->
            // 'col' en el Edificio B). Lookup ROBUSTO: match exacto -> clave
            // representativa por tipo -> cualquier clave del tipo (prefijo).
            string clave = el.seccion;
            if (string.IsNullOrEmpty(clave)
                || !ed.secciones.catalogo.ContainsKey(clave))
            {
                string rep = el.tipo == "muro" ? "muro" : "col";
                if (ed.secciones.catalogo.ContainsKey(rep)) clave = rep;
                else
                {
                    clave = null;
                    foreach (string k in ed.secciones.catalogo.Keys)
                        if (k.StartsWith(rep)) { clave = k; break; }
                }
            }
            if (!string.IsNullOrEmpty(clave))
                ed.secciones.catalogo.TryGetValue(clave, out sec);
            return el;
        }

        private static double? CapacidadM(EnvolventePM env, double P)
        {
            if (env == null || env.P == null || env.P.Count < 2) return null;
            int n = Mathf.Min(env.P.Count, env.M.Count);
            if (P <= env.P[0]) return env.M[0];
            if (P >= env.P[n - 1]) return env.M[n - 1];
            for (int i = 1; i < n; i++)
            {
                if (P <= env.P[i])
                {
                    double t = (P - env.P[i - 1]) / (env.P[i] - env.P[i - 1]);
                    return env.M[i - 1] + t * (env.M[i] - env.M[i - 1]);
                }
            }
            return env.M[n - 1];
        }

        private static void Linea(Color32[] px, int w, int h,
                                  float x0, float y0, float x1, float y1, Color32 c)
        {
            int ix0 = Mathf.RoundToInt(x0), iy0 = Mathf.RoundToInt(y0);
            int ix1 = Mathf.RoundToInt(x1), iy1 = Mathf.RoundToInt(y1);
            int dx = Mathf.Abs(ix1 - ix0), dy = Mathf.Abs(iy1 - iy0);
            int sx = ix0 < ix1 ? 1 : -1, sy = iy0 < iy1 ? 1 : -1;
            int err = dx - dy;
            int guarda = 0;
            while (guarda++ < 10000)
            {
                if (ix0 >= 0 && ix0 < w && iy0 >= 0 && iy0 < h) px[iy0 * w + ix0] = c;
                if (ix0 == ix1 && iy0 == iy1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; ix0 += sx; }
                if (e2 < dx) { err += dx; iy0 += sy; }
            }
        }

        private static void Punto(Color32[] px, int w, int h, int cx, int cy, Color32 c, int rad)
        {
            for (int yy = -rad; yy <= rad; yy++)
                for (int xx = -rad; xx <= rad; xx++)
                {
                    int x = cx + xx, y = cy + yy;
                    if (x >= 0 && x < w && y >= 0 && y < h) px[y * w + x] = c;
                }
        }

        private Texture2D GenerarTexPM(ElementoInteraccion el, SeccionInteraccion sec)
        {
            int w = Mathf.RoundToInt(anchoPM), h = Mathf.RoundToInt(altoPM);
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color32[] px = new Color32[w * h];
            Color32 fondo = new Color32(18, 22, 28, 255);
            for (int i = 0; i < px.Length; i++) px[i] = fondo;

            EnvolventePM env = sec != null ? sec.envelope : null;
            double mMax = 1.0, pMin = 0.0, pMax = 1.0;
            if (env != null && env.P != null && env.P.Count > 0)
            {
                pMin = env.P[0]; pMax = env.P[env.P.Count - 1];
                for (int i = 0; i < env.M.Count; i++) if (env.M[i] > mMax) mMax = env.M[i];
            }
            if (el != null && el.demanda != null)
                foreach (KeyValuePair<string, PuntoPM> kv in el.demanda)
                {
                    if (kv.Value == null) continue;
                    if (kv.Value.M > mMax) mMax = kv.Value.M;
                    if (kv.Value.P < pMin) pMin = kv.Value.P;
                    if (kv.Value.P > pMax) pMax = kv.Value.P;
                }
            if (sec != null && sec.balanceado != null)
            {
                if (sec.balanceado.M > mMax) mMax = sec.balanceado.M;
            }
            if (pMax - pMin < 1e-6) pMax = pMin + 1.0;
            mMax *= 1.10;

            float L = 10f, Rr = w - 6f, B = 6f, T = h - 6f;
            System.Func<double, float> fx = m => L + (float)(m / mMax) * (Rr - L);
            System.Func<double, float> fy = p => T - (float)((p - pMin) / (pMax - pMin)) * (T - B);

            Color32 eje = new Color32(90, 100, 115, 255);
            Linea(px, w, h, L, B, L, T, eje);          // eje M = 0
            Linea(px, w, h, L, B, Rr, B, eje);         // eje P = pMin
            if (pMin < 0 && pMax > 0)
                Linea(px, w, h, L, fy(0), Rr, fy(0), new Color32(60, 70, 85, 255));

            if (env != null && env.P != null && env.P.Count > 1)
            {
                Color32 ce = new Color32(90, 190, 255, 255);
                for (int i = 1; i < env.P.Count && i < env.M.Count; i++)
                    Linea(px, w, h, fx(env.M[i - 1]), fy(env.P[i - 1]),
                          fx(env.M[i]), fy(env.P[i]), ce);
            }
            if (sec != null && sec.balanceado != null)
                Punto(px, w, h, Mathf.RoundToInt(fx(sec.balanceado.M)),
                      Mathf.RoundToInt(fy(sec.balanceado.P)),
                      new Color32(60, 210, 100, 255), 2);

            if (el != null && el.demanda != null)
            {
                for (int c = 0; c < EtiquetasCaso.Length; c++)
                {
                    PuntoPM d;
                    if (!el.demanda.TryGetValue(EtiquetasCaso[c], out d) || d == null) continue;
                    bool activo = (c == casoConsulta);
                    Color32 ccd = activo ? new Color32(240, 70, 60, 255)
                                         : new Color32(160, 170, 185, 255);
                    Punto(px, w, h, Mathf.RoundToInt(fx(d.M)), Mathf.RoundToInt(fy(d.P)),
                          ccd, activo ? 4 : 2);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
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

            bool col = false, vx = false, vy = false, murs = false, brz = false, apos = false;
            bool suel = false, nds = false, cg = false, cq = false, csm = false, ctr = false;

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            col = GUILayout.Toggle(mostrarColumnas, "Columnas", togleStyle);
            vx = GUILayout.Toggle(mostrarVigasX, "Vigas X", togleStyle);
            murs = GUILayout.Toggle(mostrarMuros, "Muros", togleStyle);
            brz = GUILayout.Toggle(mostrarBrazos, "Brazos rígidos", togleStyle);
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
            if (brz != mostrarBrazos) { mostrarBrazos = brz; cambio = true; }
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
            bool insp = GUILayout.Toggle(inspeccionHabilitada, "Consulta de elemento (click izquierdo)");
            if (insp != inspeccionHabilitada) inspeccionHabilitada = insp;

            // --- Panel unificado Semana 04: diagramas + P-M de CUALQUIER
            //     elemento estructural (columna / muro / viga / aspa) ---
            if (inspeccionHabilitada && consultaSeleccionada != null)
                DibujarPanelConsulta(consultaSeleccionada, tituloStyle, valorStyle);

            if (inspeccionHabilitada && consultaSeleccionada != null)
                mostrarVentanaD = GUILayout.Toggle(mostrarVentanaD,
                    "Ventana de diagramas (arrastrable)");

            if (inspeccionHabilitada && consultaSeleccionada != null)
                mostrarVentanaPM = GUILayout.Toggle(mostrarVentanaPM,
                    "Ventana P-M (arrastrable)");

            GUILayout.EndArea();

            if (inspeccionHabilitada && consultaSeleccionada != null && mostrarVentanaD)
                rectVentanaD = GUI.Window(idVentanaD, rectVentanaD,
                    DibujarVentanaDiagramas, "Diagrama de esfuerzos — Semana 04");

            if (inspeccionHabilitada && consultaSeleccionada != null && mostrarVentanaPM)
                rectVentanaPM = GUI.Window(idVentanaPM, rectVentanaPM,
                    DibujarVentanaPM, "Diagrama P-M — Semana 04");

            if (cambio) sucio = true;
        }

        // ------------------------------------------------------------------
        // Semana 04 — panel unificado de consulta por elementTag + diagramas 3D
        // ------------------------------------------------------------------
        private void DibujarPanelConsulta(ConsultaS4 cs4, GUIStyle tituloStyle, GUIStyle valorStyle)
        {
            BloqueVisual bv = cs4 != null ? cs4.bloque : null;
            ModeloEdificio ed = bv != null ? bv.datos : null;
            if (bv == null || ed == null) return;

            int idx = bv.tagsEstructurales.IndexOf(cs4.tag);
            string tag = cs4.tag;
            if (idx < 0 && bv.tagsEstructurales.Count > 0)
            {
                idx = 0;
                tag = bv.tagsEstructurales[0];
                SeleccionarConsulta(bv.consultaPorTag[tag]);
                cs4 = consultaSeleccionada;
            }

            ElementoModelo el = null;
            foreach (ElementoModelo e in ed.elementos)
                if (e.tag.ToString() == tag) { el = e; break; }
            NodoModelo ni = el != null ? ObtenerNodo(ed, el.ni) : null;
            NodoModelo nj = el != null ? ObtenerNodo(ed, el.nj) : null;

            MetadatoElemento md = null;
            if (ed.metadatos != null) ed.metadatos.TryGetValue(tag, out md);

            GUILayout.Space(6);
            GUILayout.Box("Consulta de elemento — Semana 04", tituloStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Bloque: " + ed.bloque);
            GUILayout.Label("Tag: " + tag);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Tipo: " + (md != null ? md.tipo : (el != null ? el.tipo : "-")));
            GUILayout.Label("Material: " + (md != null ? md.material : "-"));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Nivel: " + (ni != null ? ni.nivel.ToString() : "-"));
            GUILayout.Label("Restriccion " + (md != null && md.nodos != null
                ? md.nodos.i + " \u2192 " + md.nodos.j : "-"));
            GUILayout.EndHorizontal();

            // ---- selector manual de tag (los muros solo se eligen aqui) ----
            if (bv.tagsEstructurales.Count > 1)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("\u25C0", GUILayout.Width(30)))
                {
                    int ant = (idx - 1 + bv.tagsEstructurales.Count) % bv.tagsEstructurales.Count;
                    SeleccionarConsulta(bv.consultaPorTag[bv.tagsEstructurales[ant]]);
                }
                GUILayout.Label(tag + "  (" + (idx + 1) + "/" + bv.tagsEstructurales.Count + ")");
                if (GUILayout.Button("\u25B6", GUILayout.Width(30)))
                {
                    int sig = (idx + 1) % bv.tagsEstructurales.Count;
                    SeleccionarConsulta(bv.consultaPorTag[bv.tagsEstructurales[sig]]);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Caso:", GUILayout.Width(38));
            for (int c = 0; c < EtiquetasCaso.Length; c++)
            {
                if (GUILayout.Toggle(casoConsulta == c, EtiquetasCaso[c])) casoConsulta = c;
            }
            GUILayout.EndHorizontal();

            EsfuerzosVigaModelo esf = ObtenerEsfuerzosS4(ed, tag);
            float L = (md != null) ? (float)md.L : (esf != null ? (float)esf.L : 0f);
            if (L <= 0f && ni != null && nj != null)
            {
                double dx = ni.x - nj.x, dy = ni.y - nj.y, dz = ni.z - nj.z;
                L = Mathf.Sqrt((float)(dx * dx + dy * dy + dz * dz));
            }

            if (esf == null || L <= 0f)
            {
                GUILayout.Label("Sin esfuerzos completos (corre semana04).");
            }
            else
            {
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

                bool diag = GUILayout.Toggle(mostrarDiagramas,
                    "Diagramas 3D (N verde, My azul, Mz rojo)");
                if (diag != mostrarDiagramas) { mostrarDiagramas = diag; claveDiagramas = ""; }
                ReconstruirDiagramas3D(cs4, ed, el, md, esf);
            }

            // ---- P-M (motor de secciones): vive en su PROPIA ventana
            //      (DibujarVentanaPM) para no quedar cortado por el panel ----
            ElementoInteraccion eli = SeccionDesdeTag(ed, tag, out SeccionInteraccion sec);
            GUILayout.Space(4);
            GUILayout.Label(eli != null
                ? "P-M de " + eli.seccion + " en la ventana amarilla (arrastrable)."
                : "Sin seccion P-M para este tag.", valorStyle);
        }

        // ------------------------------------------------------------------
        // Ventana de diagramas (Semana 04): GUI.Window ARRASTRABLE que grafica
        // UN esfuerzo elegido (Axial / Corte / Momento) a lo largo del
        // elemento con los coeficientes de `esfuerzos_completos`:
        //   N(x) = -N ;  V(x) = V + W*x ;  M(x) = M + V*x + W*x^2/2
        // (W = 0 en columnas/muros -> lineal; vigas con carga -> parabolico).
        // ------------------------------------------------------------------
        private double ValorDiagramaD(EsfuerzosVigaModelo esf, double x)
        {
            if (tipoEsfuerzoD == 0) return -esf.N;
            if (tipoEsfuerzoD == 1)
                return compEsfuerzoD == 0 ? esf.Vz + esf.Wz * x : esf.Vy + esf.Wy * x;
            return compEsfuerzoD == 0
                ? esf.My + esf.Vz * x + 0.5 * esf.Wz * x * x
                : esf.Mz + esf.Vy * x + 0.5 * esf.Wy * x * x;
        }

        private void NombreYUnidadEsfuerzoD(out string nombre, out string unidad)
        {
            if (tipoEsfuerzoD == 0) { nombre = "N"; unidad = "kN"; }
            else if (tipoEsfuerzoD == 1)
            {
                nombre = compEsfuerzoD == 0 ? "Vz" : "Vy";
                unidad = "kN";
            }
            else
            {
                nombre = compEsfuerzoD == 0 ? "My" : "Mz";
                unidad = "kN\u00b7m";
            }
        }

        private void GenerarTexDiagrama(EsfuerzosVigaModelo esf, double L)
        {
            int w = Mathf.RoundToInt(anchoD), h = Mathf.RoundToInt(altoD);
            if (texDiagramaD == null || texDiagramaD.width != w || texDiagramaD.height != h)
            {
                if (texDiagramaD != null) Destruir(texDiagramaD);
                texDiagramaD = new Texture2D(w, h, TextureFormat.RGBA32, false);
                texDiagramaD.filterMode = FilterMode.Point;
            }
            Color32[] px = new Color32[w * h];
            Color32 fondo = new Color32(18, 22, 28, 255);
            for (int i = 0; i < px.Length; i++) px[i] = fondo;

            int N = 48;
            double[] vals = new double[N + 1];
            double vmin = double.MaxValue, vmax = double.MinValue;
            for (int k = 0; k <= N; k++)
            {
                vals[k] = ValorDiagramaD(esf, (double)k / N * L);
                if (vals[k] < vmin) vmin = vals[k];
                if (vals[k] > vmax) vmax = vals[k];
            }
            if (vmax - vmin < 1e-9) { vmin -= 1.0; vmax += 1.0; }
            double vr = vmax - vmin;
            if (vr < 1e-9) vr = 1.0;
            float Lm = 8f, Rm = w - 8f, Bm = 8f, Tm = h - 8f;
            System.Func<double, float> fx = x => Lm + (float)(x / L) * (Rm - Lm);
            System.Func<double, float> fy = v => Tm - (float)((v - vmin) / vr) * (Tm - Bm);

            Color32 eje = new Color32(90, 100, 115, 255);
            Color32 cero = new Color32(122, 132, 148, 255);
            if (vmin < 0.0 && vmax > 0.0)
                Linea(px, w, h, Lm, fy(0.0), Rm, fy(0.0), cero);
            for (int k = 0; k < N; k++)
                Linea(px, w, h, fx((double)k / N * L), fy(vals[k]),
                              fx((double)(k + 1) / N * L), fy(vals[k + 1]),
                              new Color32(120, 220, 255, 255));
            // extremos i / j (blanco) y posicion del slider (amarillo)
            Punto(px, w, h, Mathf.RoundToInt(fx(0.0)), Mathf.RoundToInt(fy(vals[0])),
                  new Color32(255, 255, 255, 255), 2);
            Punto(px, w, h, Mathf.RoundToInt(fx(L)), Mathf.RoundToInt(fy(vals[N])),
                  new Color32(255, 255, 255, 255), 2);
            Punto(px, w, h, Mathf.RoundToInt(fx(posConsulta * L)),
                  Mathf.RoundToInt(fy(ValorDiagramaD(esf, posConsulta * L))),
                  new Color32(255, 210, 60, 255), 3);
            texDiagramaD.SetPixels32(px);
            texDiagramaD.Apply();
        }

        private void DibujarVentanaDiagramas(int id)
        {
            ConsultaS4 cs4 = consultaSeleccionada;
            BloqueVisual bv = cs4 != null ? cs4.bloque : null;
            ModeloEdificio ed = bv != null ? bv.datos : null;
            if (bv == null || ed == null) return;

            GUIStyle ttl = new GUIStyle(GUI.skin.label);
            ttl.fontStyle = FontStyle.Bold;
            GUIStyle val = new GUIStyle(GUI.skin.label);
            val.fontStyle = FontStyle.Bold;
            val.fontSize = 13;

            string tag = cs4.tag;
            MetadatoElemento md = null;
            if (ed.metadatos != null) ed.metadatos.TryGetValue(tag, out md);
            EsfuerzosVigaModelo esf = ObtenerEsfuerzosS4(ed, tag);
            float L = (md != null) ? (float)md.L : (esf != null ? (float)esf.L : 0f);

            GUILayout.Label("Elemento tag " + tag + "  |  " + (md != null ? md.tipo : "-")
                + "  |  caso " + EtiquetasCaso[casoConsulta], ttl);

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(tipoEsfuerzoD == 0, "Axial (N)")) tipoEsfuerzoD = 0;
            if (GUILayout.Toggle(tipoEsfuerzoD == 1, "Corte (V)")) tipoEsfuerzoD = 1;
            if (GUILayout.Toggle(tipoEsfuerzoD == 2, "Momento (M)")) tipoEsfuerzoD = 2;
            GUILayout.EndHorizontal();

            if (tipoEsfuerzoD == 1)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(compEsfuerzoD == 0, "Vz")) compEsfuerzoD = 0;
                if (GUILayout.Toggle(compEsfuerzoD == 1, "Vy")) compEsfuerzoD = 1;
                GUILayout.EndHorizontal();
            }
            else if (tipoEsfuerzoD == 2)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(compEsfuerzoD == 0, "My")) compEsfuerzoD = 0;
                if (GUILayout.Toggle(compEsfuerzoD == 1, "Mz")) compEsfuerzoD = 1;
                GUILayout.EndHorizontal();
            }

            if (esf == null || esf.L <= 0f || L <= 0f)
            {
                GUILayout.Label("Sin esfuerzos completos para este tag (corre semana04).");
                GUI.DragWindow(new Rect(0f, 0f, Screen.width, 24f));
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("x = " + (posConsulta * 100f).ToString("F0") + " %", GUILayout.Width(64));
            posConsulta = GUILayout.HorizontalSlider(posConsulta, 0f, 1f);
            GUILayout.EndHorizontal();

            GenerarTexDiagrama(esf, L);
            GUILayout.Box(texDiagramaD, GUIStyle.none,
                          GUILayout.Width(anchoD), GUILayout.Height(altoD));

            string nom, uni;
            NombreYUnidadEsfuerzoD(out nom, out uni);
            double vi = ValorDiagramaD(esf, 0.0);
            double vj = ValorDiagramaD(esf, L);
            double vx = ValorDiagramaD(esf, posConsulta * L);
            double vAbs = 0.0, vMax = 0.0, vPos = 0.0;
            for (int k = 0; k <= 64; k++)
            {
                double x = (double)k / 64.0 * L;
                double v = ValorDiagramaD(esf, x);
                if (System.Math.Abs(v) > vAbs) { vAbs = System.Math.Abs(v); vMax = v; vPos = x; }
            }
            GUILayout.Label("i: " + vi.ToString("F1") + " " + uni
                + "   |   j: " + vj.ToString("F1") + " " + uni, val);
            GUILayout.Label("Max |" + nom + "| = " + vMax.ToString("F1") + " " + uni
                + "  en x = " + (vPos / L * 100.0).ToString("F0") + " %", val);
            GUILayout.Label("Valor(x) = " + vx.ToString("F1") + " " + uni
                + "   (cian: curva; \u25CF i/j; \u25CF slider)", val);

            GUI.DragWindow(new Rect(0f, 0f, Screen.width, 24f));
        }

        // ------------------------------------------------------------------
        // Ventana P-M (Semana 04): diagrama de interaccion del elemento activo
        // en su PROPIA GUI.Window arrastrable. Sigue al elemento/caso elegido
        // y evita que el diagrama quede cortado por el panel de metadatos.
        // ------------------------------------------------------------------
        private void DibujarVentanaPM(int id)
        {
            ConsultaS4 cs4 = consultaSeleccionada;
            BloqueVisual bv = cs4 != null ? cs4.bloque : null;
            ModeloEdificio ed = bv != null ? bv.datos : null;
            if (bv == null || ed == null) return;

            GUIStyle ttl = new GUIStyle(GUI.skin.label);
            ttl.fontStyle = FontStyle.Bold;
            GUIStyle val = new GUIStyle(GUI.skin.label);
            val.fontStyle = FontStyle.Bold;
            val.fontSize = 13;

            string tag = cs4.tag;
            MetadatoElemento md = null;
            if (ed.metadatos != null) ed.metadatos.TryGetValue(tag, out md);

            SeccionInteraccion sec;
            ElementoInteraccion eli = SeccionDesdeTag(ed, tag, out sec);
            if (eli == null)
            {
                GUILayout.Label("Sin seccion P-M para el tag " + tag
                    + " (solo columnas/muros del motor de secciones).");
                GUI.DragWindow(new Rect(0f, 0f, Screen.width, 24f));
                return;
            }

            GUILayout.Label("Elemento tag " + tag + "  |  " + (md != null ? md.tipo : "-")
                + "  |  caso " + EtiquetasCaso[casoConsulta], ttl);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Seccion: " + eli.seccion);
            GUILayout.Label("Tipo: " + eli.tipo);
            GUILayout.EndHorizontal();

            if (texPM == null || texPMClave != ClavePMTag(bv, tag))
            {
                texPM = GenerarTexPM(eli, sec);
                texPMClave = ClavePMTag(bv, tag);
            }
            if (texPM != null)
            {
                GUILayout.Box(texPM, GUIStyle.none,
                              GUILayout.Width(anchoPM), GUILayout.Height(altoPM));
            }

            PuntoPM d;
            if (eli.demanda != null
                && eli.demanda.TryGetValue(EtiquetasCaso[casoConsulta], out d) && d != null)
            {
                double? mcap = CapacidadM(sec != null ? sec.envelope : null, d.P);
                double dc = (mcap.HasValue && mcap.Value > 0.0) ? d.M / mcap.Value : 0.0;
                GUILayout.Label("P_d = " + d.P.ToString("F0") + " kN", val);
                GUILayout.Label("M_d = " + d.M.ToString("F0") + " kN\u00b7m", val);
                GUILayout.Label("M_cap(P_d) = "
                    + (mcap.HasValue ? mcap.Value.ToString("F0") : "-") + " kN\u00b7m");
                GUILayout.Label("D/C = " + dc.ToString("F2")
                    + (dc > 1.0 ? "  (excede)" : ""), val);
            }
            else
            {
                GUILayout.Label("Sin demanda para el caso activo.");
            }

            if (sec != null)
            {
                GUILayout.Label("P0 (ACI) = " + sec.P0.ToString("F0")
                    + " kN   |   P0 fibras = " + sec.P0_fibra.ToString("F0") + " kN");
                if (sec.balanceado != null)
                {
                    GUILayout.Label("Balanceado: P = " + sec.balanceado.P.ToString("F0")
                        + " kN, M = " + sec.balanceado.M.ToString("F0") + " kN\u00b7m");
                }
            }
            if (ed.secciones != null && ed.secciones.superposicion != null)
            {
                GUILayout.Label("Superposicion |dP| = "
                    + ed.secciones.superposicion.max_dP.ToString("E1") + " kN");
            }
            GUILayout.Label("Azul: envolvente. Verde: balanceado. Rojo: demanda activa.");

            GUI.DragWindow(new Rect(0f, 0f, Screen.width, 24f));
        }

        // ------------------------------------------------------------------
        // Diagramas 3D de la consulta unificada: N (verde), My (azul) y
        // Mz (rojo) dibujados a lo largo del elemento con las formulas de la
        // semana 03/04. Normaliza cada curva a su propio maximo (auto-escala).
        // ------------------------------------------------------------------
        private void ReconstruirDiagramas3D(ConsultaS4 cs4, ModeloEdificio ed,
                                            ElementoModelo el, MetadatoElemento md,
                                            EsfuerzosVigaModelo esf)
        {
            BloqueVisual bv = cs4.bloque;
            string clave = bv.indice + ":" + cs4.tag + ":" + casoConsulta
                + ":" + posConsulta.ToString("F3") + ":" + (mostrarDiagramas ? 1 : 0)
                + ":" + (int)casoActivo;
            if (claveDiagramas == clave) return;
            claveDiagramas = clave;

            if (bv.diagramasObj != null)
                foreach (Transform hijo in bv.diagramasObj.transform) Destruir(hijo.gameObject);
            if (!mostrarDiagramas || esf == null || esf.L <= 0f || el == null
                || bv.diagramasObj == null) return;

            NodoModelo ni = ObtenerNodo(ed, el.ni);
            NodoModelo nj = ObtenerNodo(ed, el.nj);
            if (ni == null || nj == null) return;

            Vector3 p0 = Posicion(ni, bv);
            Vector3 p1 = Posicion(nj, bv);

            // Ejes locales (SI) -> Unity: (x, y, z)SI = (x, z, y)Unity.
            Vector3 ex, ey, ez;
            if (md != null && md.ejes_locales != null
                && md.ejes_locales.z != null && md.ejes_locales.z.Length == 3
                && md.ejes_locales.y != null && md.ejes_locales.y.Length == 3)
            {
                ez = new Vector3((float)md.ejes_locales.z[0], (float)md.ejes_locales.z[2],
                                 (float)md.ejes_locales.z[1]).normalized;
                ey = new Vector3((float)md.ejes_locales.y[0], (float)md.ejes_locales.y[2],
                                 (float)md.ejes_locales.y[1]).normalized;
                ex = Vector3.Cross(ey, ez).normalized;
            }
            else
            {
                ex = (p1 - p0).normalized;
                ez = Vector3.up;
                ey = Vector3.Cross(ez, ex).normalized;
            }
            if (Vector3.Dot(ex, Vector3.up) < 0f) ex = -ex;

            int puntos = 14;
            Vector3[] ptsN = new Vector3[puntos + 1];
            Vector3[] ptsMy = new Vector3[puntos + 1];
            Vector3[] ptsMz = new Vector3[puntos + 1];
            double maxN = 1e-6, maxMy = 1e-6, maxMz = 1e-6;

            // Pase 1: valores reales (sin escala).
            for (int k = 0; k <= puntos; k++)
            {
                float t = k / (float)puntos;
                double x = t * esf.L;
                double Nx = -esf.N;
                double Myx = esf.My + esf.Vz * x + 0.5 * esf.Wz * x * x;
                double Mzx = esf.Mz + esf.Vy * x + 0.5 * esf.Wy * x * x;
                Vector3 baseP = Vector3.Lerp(p0, p1, t);
                ptsN[k] = baseP + ex * (float)Nx;
                ptsMy[k] = baseP + ez * (float)Myx;
                ptsMz[k] = baseP + ey * (float)Mzx;
                if (System.Math.Abs(Nx) > maxN) maxN = System.Math.Abs(Nx);
                if (System.Math.Abs(Myx) > maxMy) maxMy = System.Math.Abs(Myx);
                if (System.Math.Abs(Mzx) > maxMz) maxMz = System.Math.Abs(Mzx);
            }

            // Pase 2: auto-escala — amplitud visual ~15% de L por curva.
            double amp = esf.L * 0.15;
            double sN = amp / maxN, sMy = amp / maxMy, sMz = amp / maxMz;
            for (int k = 0; k <= puntos; k++)
            {
                Vector3 baseP = Vector3.Lerp(p0, p1, k / (float)puntos);
                ptsN[k] = baseP + (ptsN[k] - baseP) * (float)sN;
                ptsMy[k] = baseP + (ptsMy[k] - baseP) * (float)sMy;
                ptsMz[k] = baseP + (ptsMz[k] - baseP) * (float)sMz;
            }

            CrearLinea3D("N", bv.diagramasObj.transform, ptsN, new Color(0.20f, 0.90f, 0.30f));
            CrearLinea3D("My", bv.diagramasObj.transform, ptsMy, new Color(0.30f, 0.60f, 1.00f));
            CrearLinea3D("Mz", bv.diagramasObj.transform, ptsMz, new Color(1.00f, 0.30f, 0.30f));
        }

        private static GameObject CrearLinea3D(string nombre, Transform padre, Vector3[] pts, Color c)
        {
            GameObject go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
            lr.startWidth = 0.08f;
            lr.endWidth = 0.08f;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sharedMaterial = MaterialColor(c);
            return go;
        }
    }
}
