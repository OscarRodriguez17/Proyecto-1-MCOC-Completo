using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace MCOC.Unity
{
    public class UnityStickModel : MonoBehaviour
    {
        public string jsonFileName = "modelo_resultados.json";
        public float amplificacion = 400f;
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

        private ModeloEdificio datos;
        private Dictionary<int, NivelMaestro> maestros = new Dictionary<int, NivelMaestro>();
        private Dictionary<string, List<ElementoVisual3D>> porTipo = new Dictionary<string, List<ElementoVisual3D>>();
        private Dictionary<string, GameObject> contenedor = new Dictionary<string, GameObject>();
        private GameObject nodosObj;
        private GameObject apoyosObj;
        private GameObject sueloObj;
        private GameObject suelo;
        private float zMin = float.MaxValue;
        private float zMax = float.MinValue;
        private List<ApoyoVisual> apoyos = new List<ApoyoVisual>();
        private Dictionary<string, GameObject> nodosVis = new Dictionary<string, GameObject>();
        private List<PanelVisual> paneles = new List<PanelVisual>();
        private GameObject cargasObj;
        private List<FlechaVisual> flechas = new List<FlechaVisual>();
        private List<FlechaSismoVisual> flechasSismo = new List<FlechaSismoVisual>();
        private GameObject tributariaObj;
        private List<NivelTributarioVisual> zonas = new List<NivelTributarioVisual>();
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
        }

        private class NivelTributarioVisual
        {
            public GameObject go;
            public List<TextMesh> labels = new List<TextMesh>();
            public int lvl;
            public double z;
            public List<double> xs;
            public List<double> ys;
            public float paso;
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

        private static Material matColumna;
        private static Material matViga;
        private static Material matAspa;
        private static Material matMuro;
        private static Material matZapata;

        private void Start()
        {
            Cargar();
        }

        private void Update()
        {
            if (sucio) AplicarVisual();
            ApuntarEtiquetas();
        }

        public Vector3 CentroEdificio()
        {
            if (apoyos.Count == 0) return Vector3.zero;
            Vector3 acum = Vector3.zero;
            foreach (ApoyoVisual av in apoyos)
            {
                acum.x += (float)av.nodo.x;
                acum.z += (float)av.nodo.y;
            }
            Vector3 centro = acum / apoyos.Count;
            centro.y = (zMin + zMax) * 0.5f;
            return centro;
        }

        public float SpanEdificio()
        {
            if (apoyos.Count == 0) return 20f;
            float xmin = float.MaxValue, xmax = float.MinValue;
            float zmin = float.MaxValue, zmax = float.MinValue;
            foreach (ApoyoVisual av in apoyos)
            {
                xmin = Mathf.Min(xmin, (float)av.nodo.x);
                xmax = Mathf.Max(xmax, (float)av.nodo.x);
                zmin = Mathf.Min(zmin, (float)av.nodo.y);
                zmax = Mathf.Max(zmax, (float)av.nodo.y);
            }
            return Mathf.Max(xmax - xmin, zmax - zmin);
        }

        private void Cargar()
        {
            string path = Path.Combine(Application.streamingAssetsPath, jsonFileName);
            if (!File.Exists(path))
            {
                mensaje = "No existe StreamingAssets/" + jsonFileName;
                return;
            }
            try
            {
                string txt = File.ReadAllText(path);
                datos = JsonConvert.DeserializeObject<ModeloEdificio>(txt);
                ConstruirEscena();
                mensaje = "Cargado: " + datos.proyecto;
            }
            catch (System.Exception ex)
            {
                mensaje = "Error al leer JSON: " + ex.Message;
            }
            sucio = true;
        }

        private void ConstruirEscena()
        {
            foreach (Transform hijo in transform)
                if (hijo.name != "Main Camera") Destroy(hijo.gameObject);

            foreach (string tipo in new[] { "column", "wall", "vigas_x", "vigas_y", "aspa" })
            {
                GameObject cont = new GameObject(tipo);
                cont.transform.SetParent(transform, false);
                contenedor[tipo] = cont;
                porTipo[tipo] = new List<ElementoVisual3D>();
            }
            nodosObj = new GameObject("nodos");
            nodosObj.transform.SetParent(transform, false);
            apoyosObj = new GameObject("apoyos");
            apoyosObj.transform.SetParent(transform, false);
            sueloObj = new GameObject("suelo");
            sueloObj.transform.SetParent(transform, false);
            cargasObj = new GameObject("cargas");
            cargasObj.transform.SetParent(transform, false);
            tributariaObj = new GameObject("tributaria");
            tributariaObj.transform.SetParent(transform, false);

            foreach (KeyValuePair<string, NodoModelo> kv in datos.nodos)
            {
                NodoModelo n = kv.Value;
                if (n != null && !string.IsNullOrEmpty(n.rol) && n.rol == "maestro_diafragma")
                    maestros[n.nivel] = new NivelMaestro { nivel = n.nivel, pos = new Vector2((float)n.x, (float)n.y), nodo = n };
                if (n != null && (float)n.z < zMin) zMin = (float)n.z;
                if (n != null && (float)n.z > zMax) zMax = (float)n.z;
            }

            foreach (ApoyoModelo ap in datos.apoyos)
            {
                NodoModelo n = ObtenerNodo(ap.tag);
                if (n == null) continue;
                GameObject obj = CrearZapata();
                obj.name = "apoyo_empotrado";
                obj.transform.SetParent(apoyosObj.transform, false);
                AvPosicionar(obj, n);
                apoyos.Add(new ApoyoVisual { nodo = n, tipo = ap.tipo, objeto = obj });
            }

            GameObject terreno = GameObject.CreatePrimitive(PrimitiveType.Cube);
            terreno.name = "terreno";
            terreno.transform.SetParent(sueloObj.transform, false);
            Object.Destroy(terreno.GetComponent<Collider>());
            terreno.GetComponent<Renderer>().sharedMaterial = MaterialColor(new Color(0.55f, 0.45f, 0.30f));
            suelo = terreno;

            foreach (ElementoModelo el in datos.elementos)
            {
                if (el.tipo == "wall") continue;
                NodoModelo ni = ObtenerNodo(el.ni);
                NodoModelo nj = ObtenerNodo(el.nj);
                if (ni == null || nj == null) continue;

                GameObject elemObj;
                if (el.tipo == "column")
                    elemObj = CrearColumna3D(contenedor[el.tipo], ni, nj);
                else if (el.tipo == "aspa")
                    elemObj = CrearAspa3D(contenedor[el.tipo], ni, nj);
                else
                    elemObj = CrearViga3D(contenedor[el.tipo], ni, nj, el.tipo == "vigas_x");

                ElementoVisual3D ev = new ElementoVisual3D { datos = el, objeto = elemObj };
                porTipo[el.tipo].Add(ev);
            }

            foreach (KeyValuePair<string, NodoModelo> kv2 in datos.nodos)
            {
                NodoModelo n = kv2.Value;
                if (n == null) continue;
                GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                esfera.name = "nodo_" + kv2.Key;
                esfera.transform.SetParent(nodosObj.transform, false);
                esfera.transform.position = Posicion(n);
                esfera.transform.localScale = Vector3.one * 0.45f;
                Destroy(esfera.GetComponent<Collider>());
                Renderer esfR = esfera.GetComponent<Renderer>();
                esfR.material.shader = Shader.Find("Unlit/Color");
                esfR.material.color = new Color(0.2f, 0.6f, 0.3f);
                esfR.material.renderQueue = 4000;
                esfR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                esfera.SetActive(mostrarNodos);
                nodosVis[kv2.Key] = esfera;
            }

            CrearPanelesMuro();
            CrearCargas();
            CrearTributaria();
        }

        private NodoModelo ObtenerNodo(int tag)
        {
            string clave = tag.ToString();
            if (datos.nodos.ContainsKey(clave)) return datos.nodos[clave];
            return null;
        }

        private Vector3 Posicion(NodoModelo n)
        {
            return new Vector3((float)n.x, (float)n.z, (float)n.y);
        }

        private void AvPosicionar(GameObject go, NodoModelo n)
        {
            go.transform.position = Posicion(n) + new Vector3(0, -0.6f, 0);
        }

        private void AplicarVisual()
        {
            foreach (var kvp in porTipo)
            {
                foreach (var ev in kvp.Value)
                {
                    NodoModelo ni = ObtenerNodo(ev.datos.ni);
                    NodoModelo nj = ObtenerNodo(ev.datos.nj);
                    if (ni == null || nj == null) continue;
                    PosicionarElemento3D(ev.objeto, ni, nj);
                }
            }

            foreach (var kv in nodosVis)
            {
                NodoModelo n = ObtenerNodo(int.Parse(kv.Key));
                if (n != null) kv.Value.transform.position = Posicion(n);
                kv.Value.SetActive(mostrarNodos);
            }

            foreach (PanelVisual p in paneles)
                ReconstruirPanel(p);

            foreach (var f in flechas)
            {
                if (f.el == null) continue;
                NodoModelo ni = ObtenerNodo(f.el.ni);
                NodoModelo nj = ObtenerNodo(f.el.nj);
                if (ni == null || nj == null) continue;
                Vector3 mid = (Posicion(ni) + Posicion(nj)) * 0.5f;
                bool activo = (f.esQ && mostrarCargasQ) || (!f.esQ && mostrarCargasG);
                f.go.SetActive(activo);
                if (activo)
                {
                    float esc = f.valor != 0 ? Mathf.Max(0.5f, (float)f.valor * escalaCargas) : 0.5f;
                    f.shaft.localScale = new Vector3(0.1f, esc * 0.5f, 0.1f);
                    f.go.transform.position = mid + f.offsetPerp;
                }
            }

            foreach (var f in flechasSismo)
            {
                bool activo = mostrarCargasSismo && (casoActivo == CasoDeformacion.EX || casoActivo == CasoDeformacion.EY);
                f.go.SetActive(activo);
                if (activo && f.nodo != null)
                {
                    Vector3 pos = Posicion(f.nodo);
                    float esc = f.valor != 0 ? Mathf.Max(0.5f, (float)f.valor * 0.004f) : 0.5f;
                    f.shaft.localScale = new Vector3(0.1f, esc * 0.5f, 0.1f);
                    f.go.transform.position = pos;
                }
            }

            suelo.SetActive(mostrarSuelo);
            if (suelo != null)
            {
                float xmin = float.MaxValue, xmax = float.MinValue;
                float zmin = float.MaxValue, zmax = float.MinValue;
                foreach (ApoyoVisual av in apoyos)
                {
                    xmin = Mathf.Min(xmin, (float)av.nodo.x);
                    xmax = Mathf.Max(xmax, (float)av.nodo.x);
                    zmin = Mathf.Min(zmin, (float)av.nodo.y);
                    zmax = Mathf.Max(zmax, (float)av.nodo.y);
                }
                float dx = xmax - xmin + 8f;
                float dz = zmax - zmin + 8f;
                suelo.transform.position = new Vector3((xmin + xmax) * 0.5f, zMin - 2.2f, (zmin + zmax) * 0.5f);
                suelo.transform.localScale = new Vector3(dx, 0.3f, dz);
            }

            sucio = false;
        }

        private void ReconstruirPanel(PanelVisual p)
        {
            double bx = 0, by = 0;
            double u0 = p.resisteY ? p.xc - p.t / 2.0 : p.yc - p.t / 2.0;
            double u1 = p.resisteY ? p.xc + p.t / 2.0 : p.yc + p.t / 2.0;
            double a0 = p.resisteY ? p.yc - p.L / 2.0 : p.xc - p.L / 2.0;
            double a1 = p.resisteY ? p.yc + p.L / 2.0 : p.xc + p.L / 2.0;
            Mesh mesh = new Mesh();
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            for (int st = 0; st < p.zs.Count - 1; st++)
            {
                int lvlBot = st, lvlTop = st + 1;
                System.Func<double, double, int, Vector3> P = (u, a, lvl) =>
                {
                    double px = p.resisteY ? u : a;
                    double py = p.resisteY ? a : u;
                    return new Vector3((float)px + (float)bx, (float)p.zs[lvl], (float)py + (float)by);
                };
                Vector3[] q = new Vector3[8];
                q[0] = P(u0, a0, lvlBot); q[1] = P(u0, a1, lvlBot);
                q[2] = P(u0, a1, lvlTop); q[3] = P(u0, a0, lvlTop);
                q[4] = P(u1, a0, lvlBot); q[5] = P(u1, a1, lvlBot);
                q[6] = P(u1, a1, lvlTop); q[7] = P(u1, a0, lvlTop);
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

        private void PosicionarElemento3D(GameObject go, NodoModelo ni, NodoModelo nj)
        {
            if (go == null) return;
            Vector3 a = Posicion(ni);
            Vector3 b = Posicion(nj);
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
            go.transform.localScale = new Vector3(seccW, largo, seccH);
        }

        private GameObject CrearColumna3D(GameObject padre, NodoModelo ni, NodoModelo nj)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "columna";
            go.transform.SetParent(padre.transform, false);
            go.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
            go.GetComponent<Renderer>().sharedMaterial = MaterialColumna();
            return go;
        }

        private GameObject CrearViga3D(GameObject padre, NodoModelo ni, NodoModelo nj, bool esVigaX)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "viga";
            go.transform.SetParent(padre.transform, false);
            go.transform.localScale = new Vector3(0.6f, 1f, 0.8f);
            go.GetComponent<Renderer>().sharedMaterial = MaterialViga();
            return go;
        }

        private GameObject CrearAspa3D(GameObject padre, NodoModelo ni, NodoModelo nj)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "aspa";
            go.transform.SetParent(padre.transform, false);
            go.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
            go.GetComponent<Renderer>().sharedMaterial = MaterialAspa();
            return go;
        }

        private void CrearPanelesMuro()
        {
            if (datos.geometria == null || datos.geometria.secciones == null
                || datos.geometria.secciones.muros == null) return;
            var zs = datos.geometria.niveles_z;
            if (zs == null || zs.Count < 2) return;

            foreach (MuroModelo w in datos.geometria.secciones.muros)
            {
                if (w == null || w.t <= 0.0) continue;
                double xc, yc, L;
                bool resisteY;
                if (!string.IsNullOrEmpty(w.axis)
                    && datos.geometria.grid_x.ContainsKey(w.axis)
                    && datos.geometria.grid_y.ContainsKey(w.y0)
                    && datos.geometria.grid_y.ContainsKey(w.y1))
                {
                    double x = datos.geometria.grid_x[w.axis];
                    double y0 = datos.geometria.grid_y[w.y0];
                    double y1 = datos.geometria.grid_y[w.y1];
                    xc = x; yc = (y0 + y1) / 2.0; L = System.Math.Abs(y1 - y0);
                    resisteY = true;
                }
                else if (!string.IsNullOrEmpty(w.y) && datos.geometria.grid_y.ContainsKey(w.y))
                {
                    double x0 = GridVal(w, datos.geometria.grid_x);
                    double x1v = GridValX1(w, datos.geometria.grid_x);
                    double y = datos.geometria.grid_y[w.y];
                    xc = (x0 + x1v) / 2.0; yc = y; L = System.Math.Abs(x1v - x0);
                    resisteY = false;
                }
                else continue;
                if (L <= 0.0) continue;

                GameObject go = new GameObject("panel_" + w.id);
                go.transform.SetParent(contenedor["wall"].transform, false);
                go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>().sharedMaterial = MaterialMuroReal();
                PanelVisual p = new PanelVisual
                {
                    go = go, xc = xc, yc = yc, L = L, t = w.t,
                    resisteY = resisteY, zs = zs
                };
                paneles.Add(p);
                ReconstruirPanel(p);
            }
        }

        private void CrearCargas()
        {
            if (datos.cargas == null || datos.cargas.vigas == null || datos.cargas.vigas.Count == 0) return;

            foreach (CargaVigaModelo cv in datos.cargas.vigas)
            {
                if (cv.L <= 0.0) continue;
                NodoModelo ni = ObtenerNodo(cv.ni);
                NodoModelo nj = ObtenerNodo(cv.nj);
                if (ni == null || nj == null) continue;

                ElementoModelo elRef = new ElementoModelo { tag = cv.tag, tipo = cv.tipo, ni = cv.ni, nj = cv.nj };
                FlechaVisual g = CrearFlechaVisual(ColorCargaG);
                g.el = elRef; g.esQ = false; g.valor = cv.G;
                FlechaVisual q = CrearFlechaVisual(ColorCargaQ);
                q.el = elRef; q.esQ = true; q.valor = cv.Q;

                Vector3 along = new Vector3((float)(nj.x - ni.x), 0f, (float)(nj.y - ni.y));
                Vector3 perp = Mathf.Abs(along.x) > Mathf.Abs(along.z)
                    ? Vector3.forward * 1.4f
                    : Vector3.right * 1.4f;
                q.offsetPerp = perp;
            }

            if (datos.cargas.pesos_por_nivel != null && datos.cargas.sismo != null)
            {
                foreach (string lvlKey in datos.cargas.pesos_por_nivel.Keys)
                {
                    int nivel;
                    if (!int.TryParse(lvlKey, out nivel)) continue;
                    NivelMaestro nm;
                    if (!maestros.TryGetValue(nivel, out nm) || nm.nodo == null) continue;

                    CargaSismoModelo sEx, sEy;
                    if (datos.cargas.sismo.TryGetValue("EX", out sEx) && sEx.F_por_nivel.ContainsKey(lvlKey))
                    {
                        FlechaSismoVisual f = new FlechaSismoVisual();
                        f.nivel = nivel; f.enX = true; f.nodo = nm.nodo;
                        f.valor = sEx.F_por_nivel[lvlKey];
                        flechasSismoCrear(f, ColorCargaSismo);
                    }
                    if (datos.cargas.sismo.TryGetValue("EY", out sEy) && sEy.F_por_nivel.ContainsKey(lvlKey))
                    {
                        FlechaSismoVisual f = new FlechaSismoVisual();
                        f.nivel = nivel; f.enX = false; f.nodo = nm.nodo;
                        f.valor = sEy.F_por_nivel[lvlKey];
                        flechasSismoCrear(f, ColorCargaSismo);
                    }
                }
            }
        }

        private FlechaVisual CrearFlechaVisual(Color color)
        {
            FlechaVisual f = new FlechaVisual();
            f.go = new GameObject("flecha_carga");
            f.go.transform.SetParent(cargasObj.transform, false);

            GameObject shaftGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(shaftGo.GetComponent<Collider>());
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
            flechas.Add(f);
            return f;
        }

        private void flechasSismoCrear(FlechaSismoVisual f, Color color)
        {
            f.go = new GameObject("flecha_sismo");
            f.go.transform.SetParent(cargasObj.transform, false);

            GameObject shaftGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(shaftGo.GetComponent<Collider>());
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
            flechasSismo.Add(f);
        }

        private void CrearTributaria()
        {
            if (datos.geometria == null || datos.geometria.grid_x == null || datos.geometria.grid_y == null) return;
            if (datos.geometria.niveles_z == null || datos.geometria.niveles_z.Count < 2) return;

            List<double> xs = new List<double>(datos.geometria.grid_x.Values);
            List<double> ys = new List<double>(datos.geometria.grid_y.Values);
            xs.Sort();
            ys.Sort();

            double paso = 0.50;
            double qG = 0.0;
            if (datos.cargas != null && datos.cargas.q_losa != null)
                qG = datos.cargas.q_losa.G;

            for (int lvl = 1; lvl < datos.geometria.niveles_z.Count; lvl++)
            {
                double z = datos.geometria.niveles_z[lvl] + 0.15;
                CrearNivelMosaico(xs, ys, lvl, z, paso, qG);
            }
        }

        private void CrearNivelMosaico(List<double> xs, List<double> ys, int lvl, double z, double paso, double qG)
        {
            if (xs.Count < 2 || ys.Count < 2) return;

            double xMin = xs[0], xMax = xs[xs.Count - 1];
            double yMin = ys[0], yMax = ys[ys.Count - 1];
            int nx = (int)System.Math.Ceiling((xMax - xMin) / paso);
            int ny = (int)System.Math.Ceiling((yMax - yMin) / paso);
            if (nx <= 0 || ny <= 0) return;

            int[,] vigaId = new int[nx, ny];
            bool[,] esX = new bool[nx, ny];

            for (int ix = 0; ix < nx; ix++)
            {
                double cx = xMin + (ix + 0.5) * paso;
                int ii = IndexEn(xs, cx);
                for (int iy = 0; iy < ny; iy++)
                {
                    double cy = yMin + (iy + 0.5) * paso;
                    int jj = IndexEn(ys, cy);
                    double x0 = xs[ii], x1 = xs[ii + 1];
                    double y0 = ys[jj], y1 = ys[jj + 1];
                    double dx = System.Math.Min(cx - x0, x1 - cx);
                    double dy = System.Math.Min(cy - y0, y1 - cy);

                    if (dy < dx)
                    {
                        esX[ix, iy] = true;
                        vigaId[ix, iy] = (cy - y0 < y1 - cy) ? jj : jj + 1;
                    }
                    else
                    {
                        esX[ix, iy] = false;
                        vigaId[ix, iy] = (cx - x0 < x1 - cx) ? ii : ii + 1;
                    }
                }
            }

            int nreg = 0;
            int[,] regionId = new int[nx, ny];
            for (int ix = 0; ix < nx; ix++)
                for (int iy = 0; iy < ny; iy++) regionId[ix, iy] = -1;

            Stack<int> pila = new Stack<int>();
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iy = 0; iy < ny; iy++)
                {
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

            if (nreg <= 0) return;

            double stepArea = paso * paso;
            long[] conteo = new long[nreg];
            double[] sumX = new double[nreg];
            double[] sumY = new double[nreg];

            int totalV = nx * ny * 4;
            Vector3[] vet = new Vector3[totalV];
            Color[] col = new Color[totalV];
            int[] tri = new int[nx * ny * 6];
            int vi = 0, ti = 0;

            for (int ix = 0; ix < nx; ix++)
            {
                for (int iy = 0; iy < ny; iy++)
                {
                    double x0 = xMin + ix * paso, x1 = x0 + paso;
                    double y0 = yMin + iy * paso, y1 = y0 + paso;
                    double cx = (x0 + x1) * 0.5, cy = (y0 + y1) * 0.5;
                    int rid = regionId[ix, iy];
                    conteo[rid]++;
                    sumX[rid] += cx; sumY[rid] += cy;

                    Color c = esX[ix, iy] ? ColorTributariaX : ColorTributariaY;
                    int baseV = vi;
                    vet[vi] = new Vector3((float)x0, 0f, (float)y0); col[vi] = c; vi++;
                    vet[vi] = new Vector3((float)x1, 0f, (float)y0); col[vi] = c; vi++;
                    vet[vi] = new Vector3((float)x1, 0f, (float)y1); col[vi] = c; vi++;
                    vet[vi] = new Vector3((float)x0, 0f, (float)y1); col[vi] = c; vi++;
                    tri[ti++] = baseV; tri[ti++] = baseV + 1; tri[ti++] = baseV + 2;
                    tri[ti++] = baseV; tri[ti++] = baseV + 2; tri[ti++] = baseV + 3;
                }
            }

            GameObject go = new GameObject("zona_tributaria_nivel_" + lvl);
            go.transform.SetParent(tributariaObj.transform, false);
            Mesh m = new Mesh();
            m.vertices = vet;
            m.colors = col;
            m.triangles = tri;
            m.RecalculateNormals();
            m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialTributariaMosaico();

            for (int r = 0; r < nreg; r++)
            {
                double sx = sumX[r] / conteo[r];
                double sy = sumY[r] / conteo[r];
                double area = conteo[r] * stepArea;
                double kn = qG * area;

                TextMesh tm = CrearEtiqueta();
                tm.characterSize = 0.06f;
                tm.transform.SetParent(tributariaObj.transform, false);
                tm.transform.position = new Vector3((float)sx, (float)z, (float)sy);
                tm.text = kn.ToString("F0") + " kN";
                if (Camera.main != null)
                    tm.transform.rotation = Camera.main.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
            }
        }

        private static int IndexEn(List<double> arr, double val)
        {
            for (int i = 0; i < arr.Count - 1; i++)
                if (val >= arr[i] && val <= arr[i + 1]) return i;
            return arr.Count - 2;
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

        private void ApuntarEtiquetas()
        {
            if (Camera.main == null) return;
            foreach (var z in zonas)
                foreach (var lbl in z.labels)
                    if (lbl != null)
                        lbl.transform.rotation = Camera.main.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
        }

        // ── Materiales ──
        private static readonly Color ColorCargaG = new Color(0.22f, 0.22f, 0.24f);
        private static readonly Color ColorCargaQ = new Color(0.95f, 0.55f, 0.10f);
        private static readonly Color ColorCargaSismo = new Color(0.85f, 0.18f, 0.14f);
        private static readonly Color ColorTributariaX = new Color(0.95f, 0.45f, 0.05f, 0.60f);
        private static readonly Color ColorTributariaY = new Color(0.10f, 0.45f, 0.95f, 0.60f);

        private static Material MaterialAspa()
        {
            if (matAspa == null)
            {
                matAspa = new Material(Shader.Find("Standard"));
                if (matAspa == null) matAspa = new Material(Shader.Find("Unlit/Color"));
                matAspa.color = new Color(0.35f, 0.42f, 0.55f);
                matAspa.SetFloat("_Glossiness", 0.6f);
            }
            return matAspa;
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

        private static Material MaterialTributariaMosaico()
        {
            Material m = new Material(Shader.Find("Sprites/Default"));
            if (m == null) m = new Material(Shader.Find("Transparent/Diffuse"));
            if (m != null) m.color = Color.white;
            return m;
        }

        private static Mesh coneCargaMesh;
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
            verts[seg] = Vector3.zero;
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

        private GameObject CrearZapata()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(1.2f, 1.2f, 1.5f);
            go.GetComponent<Renderer>().sharedMaterial = MaterialZapata();
            return go;
        }
    }
}
