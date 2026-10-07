using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MCOC.AR
{
    // =====================================================================
    //  App AR de inspección (Semana 06)
    //
    //  El rig AR se construye por código en Awake(), de modo que la escena
    //  AR_Inspeccion.unity sólo necesita un GameObject con este
    //  componente: sesión, cámara, plano, anclas, raycast e interfaz se
    //  crean al arrancar. Eso evita mantener referencias de script escritas
    //  a mano en el .unity.
    //
    //  El teléfono NO resuelve ningún elemento: lee los diagramas ya
    //  muestreados de StreamingAssets/ar_elementos.json.
    // =====================================================================

    /// <summary>Metadato de un renderer para aplicar los toggles sin recorrer nombres.</summary>
    internal class ARVisor
    {
        public GameObject go;
        public ARTipoTrazo tipo;
        public bool principal;
        public bool texto;
    }

    /// <summary>
    /// Qué está esperando la app del próximo toque en pantalla (Corrección 5).
    /// Ninguno: el toque no marca nada. Base: el pie de una columna/muro.
    /// ExtremoI / ExtremoJ: el pie de cada columna de apoyo de una viga.
    /// TechoI / TechoJ: la cara inferior de la viga junto a cada columna de
    /// apoyo, apuntando a la viga (Corrección 7, 4b).
    /// Cuadro: las 4 esquinas de la cara del elemento (Corrección 8, 5a).
    /// </summary>
    public enum ModoMarcado
    {
        Ninguno,
        Base,
        ExtremoI,
        ExtremoJ,
        TechoI,
        TechoJ,
        Cuadro
    }

    public class ARInspeccionApp : MonoBehaviour
    {
        [Header("Presentación")]
        public float amplitudPorDefecto = 0.45f;
        public float amplitudMin = 0.10f;
        public float amplitudMax = 2.00f;
        public float carril = 0.28f;

        [Header("Interacción")]
        public bool colocarAlTocar = true;
        public float margenToleranciaToque = 40f;

        private ARRaiz datos;
        private List<ARGeometriaElemento> geometria;
        private ARInterfaz ui;
        private GameObject raizVisual;
        private ARAnchor ancla;

        private Camera camara;
        private ARRaycastManager raycastMgr;
        private ARSessionOrigin sesionOrigen;
        private ARSession arSession;
        private ARAnchorManager anchorMgr;
        private ARPlaneManager planeMgr;
        private AROcclusionManager oclusion;

        private Font fuente;
        private Material matLinea;

        private int tagSeleccionado = -1;
        private bool verEje = true;
        private bool verN = true;
        private bool verV = true;
        private bool verM = true;
        private bool verNoPrincipal = false;
        private bool verPm = true;

        /// <summary>
        /// Amplitud del diagrama (altura visual del mayor valor). NO es un
        /// escalado del elemento: la escala del elemento es 1:1 y no se toca.
        /// </summary>
        private float amplitudVisual = 0.45f;

        private readonly Dictionary<int, ARGeometriaElemento> geoPorTag =
            new Dictionary<int, ARGeometriaElemento>();
        private readonly Dictionary<int, GameObject> contenedores =
            new Dictionary<int, GameObject>();
        private readonly List<ARVisor> visores = new List<ARVisor>();

        private Vector2 toqueInicio;
        private float toqueMomento;
        private bool placeRequested;
        private string mensaje = "Iniciando…";
        private bool avisoSinTracking = false;

        /// <summary>
        /// Desplazamiento acumulado de los botones «Piso ±5 cm», en metros.
        ///
        /// NO se guarda en `ancla.transform`: el `ARAnchor` de ARCore reescribe
        /// la pose de su GameObject en cuanto el subsystem se la actualiza, así
        /// que un ajuste hecho ahí se pierde. Se guarda aquí y se aplica como
        /// `raizVisual.localPosition = (0, offsetPiso, 0)`: la raíz cuelga del
        /// ancla, así que sigue al piso, pero no lo toca (Corrección 4, 2).
        /// </summary>
        private float offsetPiso;

        // --- Colocación marcando el elemento real (Corrección 5) -----------
        private ModoMarcado modo = ModoMarcado.Ninguno;
        /// <summary>Punto del piso marcado bajo el extremo i (viga).</summary>
        private Vector3 puntoI;
        /// <summary>
        /// Rotación que salió de marcar los dos extremos. Mientras haya ancla
        /// marcada, ColocarEnAncla y «Restablecer» vuelven a ELLA y no a la
        /// orientación por cámara (RotacionInicial).
        /// </summary>
        private Quaternion rotacionMarcada = Quaternion.identity;
        private bool hayRotacionMarcada;

        // --- Cada elemento en su lugar (Corrección 7, parte 4a) ---------------
        /// <summary>
        /// Rumbo del elemento colocado, FIJADO al colocarlo: por cámara al usar
        /// «Marcar base» / «Colocar aquí», o el de los dos extremos marcados en una
        /// viga. Ya no se recalcula con la cámara al re-elegir el elemento, al
        /// «Restablecer» ni con gestos: el diagrama no se mueve una vez puesto.
        /// </summary>
        private Quaternion rotacionFija = Quaternion.identity;

        /// <summary>Colocación guardada de un elemento: su ancla, su piso y su rumbo.</summary>
        private class Colocacion
        {
            public ARAnchor ancla;
            public float offsetPiso;
            public Quaternion rotacion;
            public bool marcadaViga;
        }

        /// <summary>
        /// Colocación de CADA elemento (tag → ancla propia). Al cambiar de
        /// elemento, el anterior conserva la suya y el nuevo vuelve a la suya; si
        /// el nuevo no se ha colocado, no aparece en el ancla de otro.
        /// </summary>
        private readonly Dictionary<int, Colocacion> colocaciones = new Dictionary<int, Colocacion>();
        /// <summary>Anillo sobre el piso que indica qué punto se va a marcar.</summary>
        private GameObject reticula;
        private bool reticulaValida;
        private Vector3 puntoReticula;
        private bool toqueSobreUI;
        private readonly List<ARRaycastHit> hitsPiso = new List<ARRaycastHit>();

        // --- Punto del piso que nunca se pierde (Corrección 6, parte 3a) ----
        /// <summary>Altura del último piso detectado por ARCore (bajo el teléfono).</summary>
        private bool hayPisoConocido;
        private float yPisoConocido;
        /// <summary>De dónde salió el punto actual de la retícula.</summary>
        private OrigenPunto origenReticula = OrigenPunto.Ninguno;

        // --- Viga apuntando a ella (Corrección 7, 4b) ------------------------
        /// <summary>Punto de la cara inferior marcado junto a la columna del extremo i.</summary>
        private Vector3 caraTechoI;
        private OrigenTecho origenTechoI = OrigenTecho.Ninguno;
        /// <summary>De dónde salió el punto de la retícula cuando se apunta al techo.</summary>
        private OrigenTecho origenReticulaTecho = OrigenTecho.Ninguno;

        // --- Encuadre por 4 esquinas (Corrección 8, 5a) -------------------------
        /// <summary>Esquinas ya marcadas del recuadro en curso.</summary>
        private readonly List<Vector3> esquinas = new List<Vector3>();
        /// <summary>
        /// Cambio 01: plano del recuadro en curso, vertical y DE FRENTE a la cámara,
        /// fijado por la primera esquina. Todas las esquinas quedan en él.
        /// </summary>
        private bool hayPlanoCuadro;
        private Vector3 planoCuadroPunto, planoCuadroNormal;
        private int esquinasEstimadas;
        /// <summary>Recuadro de cada elemento encuadrado (tag → recuadro). Sin entrada: dibujo 1:1 del modelo.</summary>
        private readonly Dictionary<int, ARCuadroGeom> cuadroPorTag = new Dictionary<int, ARCuadroGeom>();
        private OrigenCuadro origenReticulaCuadro = OrigenCuadro.Ninguno;
        /// <summary>Cruces y líneas de las esquinas ya marcadas.</summary>
        private GameObject vistaPrevia;
        /// <summary>Guía en vivo: línea / recuadro desde las esquinas marcadas hasta la mira.</summary>
        private GameObject guia;

        // --- Seguimiento (Corrección 8, 5b) -------------------------------------
        private bool ocultoPorSeguimiento;
        private float tiempoSinSeguimiento;

        // =================================================================
        //  Ciclo de vida
        // =================================================================

        private float diagnosticoTimer;
        private bool preparado;

        private void Awake()
        {
            Preparar();
        }

        private void Start()
        {
            Preparar();
            StartCoroutine(CargarContrato());
        }

        /// <summary>
        /// Deja la app montada: fuente, material de líneas, rig AR e interfaz.
        ///
        /// Es idempotente a propósito. `Awake` y `Start` lo llaman, y también
        /// lo puede llamar un test de EditMode sobre un componente creado a
        /// mano; sólo el primero que llega construye. Así el test ejercita el
        /// mismo camino que el teléfono, no una copia.
        /// </summary>
        public void Preparar()
        {
            if (preparado) return;
            preparado = true;

            fuente = ARInterfaz.CargarFuente();
            matLinea = MaterialLinea();
            ConstruirRigAR();
            raizVisual = new GameObject("Diagramas");
            raizVisual.transform.SetParent(transform, false);
            raizVisual.SetActive(false);
            DestroyOtherCameras();
            ConstruirInterfaz();

            // El contador de frames se activa aquí y no en Start: desde el
            // primer frame dibujado ya hay un número que comparar con la pose.
            ARFramesCamara.Activar();
        }

        private IEnumerator CargarContrato()
        {
            yield return ARCargador.Cargar(
                ok =>
                {
                    try
                    {
                        AplicarContrato(ok);
                    }
                    catch (Exception ex)
                    {
                        mensaje = "ERROR: " + ex.Message;
                        if (ui != null) ui.estado.text = "ERROR: " + ex.Message + "\n" + ex.StackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    }
                },
                err =>
                {
                    try
                    {
                        mensaje = "ERROR: " + err;
                        if (ui != null) ui.estado.text = mensaje;
                    }
                    catch (Exception ex)
                    {
                        mensaje = "ERROR: " + ex.Message;
                        if (ui != null) ui.estado.text = "ERROR: " + ex.Message + "\n" + ex.StackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    }
                });
        }

        /// <summary>
        /// Callback OK de <see cref="CargarContrato"/>: ya se tiene el contrato.
        ///
        /// Construye los diagramas, rellena la lista de la interfaz con los
        /// tags ordenados, pone la cabecera con el caso y selecciona el primer
        /// tag (14, el menor). Antes de esta función la lista salía vacía
        /// porque la interfaz se construía en Start(), sin datos.
        ///
        /// Devuelve cuántos elementos quedaron en la lista.
        /// </summary>
        public int AplicarContrato(ARRaiz ok)
        {
            if (ok == null) throw new ArgumentNullException("ok");
            Preparar();

            datos = ok;
            ConstruirVisual();

            if (ui != null)
            {
                ui.cabecera.text = Cabecera(ok);
                ui.LlenarElementos(ok);
            }

            mensaje = "Apunta al piso y toca «Colocar», o usa «Colocar aquí».";
            RefrescarEstado();

            // El primer tag en orden ascendente es el que queda seleccionado.
            int primero = geometria != null && geometria.Count > 0 ? geometria[0].tag : -1;
            if (primero >= 0) Seleccionar(primero);
            ActualizarInfo();
            return ui != null ? ui.tagsElemento.Count : 0;
        }

        /// <summary>
        /// Cabecera de la app. El caso va siempre, porque es lo primero que
        /// hay que comprobar en el teléfono antes de fiarse de lo que se ve.
        /// </summary>
        public static string Cabecera(ARRaiz d)
        {
            if (d == null) return "MCOC · AR · cargando…";
            return string.Format("MCOC · AR · caso {0}\n{1}\n{2}", d.caso, d.edificio,
                                 string.IsNullOrEmpty(d.casoDescripcion)
                                     ? "-" : d.casoDescripcion);
        }

        // =================================================================
        //  Rig AR
        // =================================================================

        private void ConstruirRigAR()
        {
            var rig = ARRig.Construir(transform);
            arSession = rig.sesion;
            sesionOrigen = rig.origen;
            camara = rig.camara;
            raycastMgr = rig.raycastMgr;
            anchorMgr = rig.anchorMgr;
            planeMgr = rig.planeMgr;
            oclusion = rig.oclusion;
        }

        /// <summary>
        /// Deja UNA sola cámara activa: la del rig AR.
        ///
        /// La escena trae su propia cámara y, con dos cámaras habilitadas, la
        /// <c>Camera.main</c> puede ser la equivocada y el texto 3D se orienta
        /// hacia un punto que no se está viendo. Se desactivan en vez de
        /// destruirse para que el cambio sea reversible desde el Inspector.
        /// </summary>
        private void DestroyOtherCameras()
        {
            if (camara == null) return;
            foreach (var c in FindObjectsOfType<Camera>(true))
            {
                if (c == null || c == camara) continue;
                c.enabled = false;
            }
            camara.enabled = true;
            camara.gameObject.SetActive(true);
        }

        private static Material MaterialLinea()
        {
            var m = Resources.Load<Material>("MCOC_LineaAR");
            if (m != null) return m;
            var sh = Shader.Find("MCOC/LineaAR");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("UI/Default");
            // `Hidden/InternalErrorShader` está siempre en el motor: es el
            // último recurso para que LineRenderer tenga material. Sin material,
            // `SetPositions` revienta con NullReferenceException.
            if (sh == null) sh = Shader.Find("Hidden/InternalErrorShader");
            if (sh == null) return null;
            return new Material(sh) { name = "MCOC_LineaAR (runtime)" };
        }

        // =================================================================
        //  Construcción visual
        // =================================================================

        /// <summary>
        /// Destruye un GameObject tanto en Play como en el Editor.
        ///
        /// `Destroy` en modo edición lanza un error ("Destroy may not be called
        /// from edit mode") que Unity marca como log no gestionado y hace
        /// fallar los tests de EditMode aunque la operación fuese correcta.
        /// </summary>
        private static void Destruir(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private void ConstruirVisual()
        {
            Destruir(raizVisual);
            raizVisual = new GameObject("Diagramas");
            if (sesionOrigen != null)
                raizVisual.transform.SetParent(sesionOrigen.transform, false);
            else
                raizVisual.transform.SetParent(transform, false);
            raizVisual.SetActive(false);

            amplitudVisual = Mathf.Clamp(amplitudPorDefecto, amplitudMin, amplitudMax);
            geometria = ARGeometriaBuilder.Construir(datos, Opciones());

            geoPorTag.Clear();
            contenedores.Clear();
            visores.Clear();
            cuadroPorTag.Clear();   // un contrato nuevo vuelve al dibujo del modelo
            foreach (var g in geometria)
            {
                geoPorTag[g.tag] = g;
                var go = new GameObject("Tag" + g.tag);
                go.transform.SetParent(raizVisual.transform, false);
                contenedores[g.tag] = go;
                ConstruirContenedor(g, go);
            }
            AplicarVisibilidad();
        }

        private ARGeometriaOpciones Opciones()
        {
            return new ARGeometriaOpciones { amplitud = amplitudVisual, carril = carril };
        }

        /// <summary>
        /// (Re)construye los trazos de UN contenedor. Se vuelve a llamar cuando
        /// cambia la amplitud: los LineRenderer se rehacen con la nueva escala
        /// de diagrama, pero el tamaño del ELEMENTO no cambia (sigue 1:1).
        /// </summary>
        private void ConstruirContenedor(ARGeometriaElemento g, GameObject go)
        {
            visores.RemoveAll(v => v.go != null && v.go.transform.IsChildOf(go.transform));
            for (int k = go.transform.childCount - 1; k >= 0; k--)
                Destruir(go.transform.GetChild(k).gameObject);

            foreach (var t in g.trazos)
            {
                if (t.puntos.Count < 2) continue;
                if (matLinea == null) break;   // sin material no hay trazos
                var lrgo = new GameObject("Trazo_" + (t.etiqueta ?? t.tipo.ToString()));
                lrgo.transform.SetParent(go.transform, false);
                var lr = lrgo.AddComponent<LineRenderer>();
                lr.sharedMaterial = matLinea;
                lr.useWorldSpace = false;
                lr.loop = false;
                lr.alignment = LineAlignment.View;
                lr.textureMode = LineTextureMode.Stretch;
                lr.numCapVertices = 4;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.startWidth = t.ancho;
                lr.endWidth = t.ancho;
                lr.startColor = t.color;
                lr.endColor = t.color;
                lr.positionCount = t.puntos.Count;
                var buf = new Vector3[t.puntos.Count];
                t.puntos.CopyTo(buf);
                lr.SetPositions(buf);

                visores.Add(new ARVisor
                {
                    go = lrgo,
                    tipo = t.tipo,
                    principal = t.principal
                });
            }

            foreach (var m in g.marcas) ConstruirMarca(go.transform, m);
        }

        /// <summary>Rehace sólo el diagrama del elemento visible, con otra amplitud.</summary>
        private void ReconstruirSeleccion()
        {
            if (datos == null || tagSeleccionado < 0) return;
            ARElemento el;
            if (!datos.elementos.TryGetValue(tagSeleccionado.ToString(), out el)) return;
            GameObject go;
            if (!contenedores.TryGetValue(tagSeleccionado, out go) || go == null) return;

            ARCuadroGeom q;
            var g = cuadroPorTag.TryGetValue(tagSeleccionado, out q)
                ? ARGeometriaBuilder.ConstruirEnCuadro(el, q, Opciones(), AmplitudRelativa())
                : ARGeometriaBuilder.ConstruirElemento(el, Opciones());
            geoPorTag[tagSeleccionado] = g;
            ConstruirContenedor(g, go);
            AplicarVisibilidad();
            if (ancla != null) CongelarTextos();   // «Amplitud ±» no puede soltar los rótulos
        }

        /// <summary>Amplitud de «Amplitud ±» relativa a la de partida (1 = por defecto).</summary>
        private float AmplitudRelativa()
        {
            return amplitudPorDefecto > 1e-6f ? amplitudVisual / amplitudPorDefecto : 1f;
        }


        private void ConstruirMarca(Transform padre, ARMarca m)
        {
            var c = new GameObject("Marca");
            c.transform.SetParent(padre, false);
            c.transform.localPosition = m.posicion;
            // Un LineRenderer POR GameObject, como en ConstruirContenedor. Meter los tres
            // ejes (X, Y, Z) en un mismo GameObject hace que el 2º
            // AddComponent devuelva un componente inválido y el NRE salte al
            // asignarle el material.
            if (matLinea != null)
            {
                int eje = 0;
                foreach (Vector3 d in new[] { Vector3.right, Vector3.up, Vector3.forward })
                {
                    var ejeGO = new GameObject("MarcaEje" + (++eje));
                    ejeGO.transform.SetParent(c.transform, false);
                    var lr = ejeGO.AddComponent<LineRenderer>();
                    lr.sharedMaterial = matLinea;
                    lr.useWorldSpace = false;
                    lr.positionCount = 2;
                    lr.startWidth = Mathf.Min(0.02f, m.radio * 0.5f);
                    lr.endWidth = Mathf.Min(0.02f, m.radio * 0.5f);
                    lr.startColor = m.color;
                    lr.endColor = m.color;
                    lr.SetPositions(new[] { -d * m.radio, d * m.radio });
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    // La cruz sigue al toggle de su diagrama, igual que el rótulo.
                    visores.Add(new ARVisor { go = ejeGO, tipo = m.tipo, principal = m.principal });
                }
            }
            else
            {
                Debug.LogWarning("[MCOC] Sin material de línea: las marcas no se dibujan.");
            }

            // Sin fuente no se puede poner texto 3D; en un Editor en batchmode
            // (-nographics) puede no haber ninguna cargada.
            if (string.IsNullOrEmpty(m.texto) || fuente == null) return;

            var t = new GameObject("Texto");
            t.transform.SetParent(padre, false);
            t.transform.localPosition = m.posicion + m.alturaTexto;
            var tm = t.AddComponent<TextMesh>();
            tm.text = m.texto;
            tm.font = fuente;
            tm.fontSize = 48;
            // Alto de letra ≈ characterSize · fontSize / 10 (m).
            tm.characterSize = m.alturaLetra > 0f ? m.alturaLetra / 4.8f : 0.018f;
            tm.anchor = m.centrado ? TextAnchor.MiddleCenter : TextAnchor.LowerLeft;
            tm.alignment = m.centrado ? TextAlignment.Center : TextAlignment.Left;
            tm.color = m.color;
            var mr = t.GetComponent<MeshRenderer>();
            mr.sharedMaterial = fuente.material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (m.normalPlano.sqrMagnitude > 1e-6f)
            {
                // Cambio 01: rótulo IMPRESO sobre el recuadro: plano, fijo, sin girar
                // ni cambiar de tamaño. +Z del TextMesh hacia el lado contrario al que mira.
                t.transform.localRotation = Quaternion.LookRotation(-m.normalPlano, Vector3.up);
            }
            else
            {
                // Rótulos del dibujo 1:1: se orientan hacia la cámara UNA vez, al
                // colocar (CongelarTextos), y después quedan fijos.
                t.AddComponent<ARBillboard>();
            }
            visores.Add(new ARVisor { go = t, tipo = m.tipo, principal = m.principal, texto = true });
        }

        // =================================================================
        //  Interfaz
        // =================================================================

        private void ConstruirInterfaz()
        {
            var o = new ARInterfazOpciones();
            // La lista de elementos NO se rellena aquí: se construye antes de
            // leer el contrato. La rellena AplicarContrato con LlenarElementos,
            // que es donde ya se saben los tags.
            o.cabecera = Cabecera(datos);

            o.conToggle.AddRange(new[] { "Eje", "N", "V", "M", "No principal", "P-M" });
            o.toggleInicial["Eje"] = true;
            o.toggleInicial["N"] = true;
            o.toggleInicial["V"] = true;
            o.toggleInicial["M"] = true;
            o.toggleInicial["No principal"] = false;
            o.toggleInicial["P-M"] = true;

            ui = ARInterfaz.Construir(o);
            var canvas = ui.root.GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                if (scaler == null) scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
            }
            ui.estado.text = mensaje;
            ui.frames.text = ARFramesCamara.Texto();
            ui.info.text = datos != null && datos.convenciones != null
                ? "Convenciones\n" + datos.convenciones.N + "\n" + datos.convenciones.dibujo
                : "";

            ui.ElementoElegido += Seleccionar;
            ui.ToggleCambiado += AplicarToggle;
            ui.Colocar += () => placeRequested = true;
            ui.ColocarAqui += () => ColocarAqui();
            ui.PisoMas += () => DesplazarPiso(1);
            ui.PisoMenos += () => DesplazarPiso(-1);
            ui.Reiniciar += QuitarAncla;
            ui.Restablecer += Restablecer;
            ui.AmplitudMas += () => CambiarAmplitud(1.25f);
            ui.AmplitudMenos += () => CambiarAmplitud(1f / 1.25f);
            ui.MarcarBase += IniciarMarcadoBase;
            ui.MarcarViga += IniciarMarcadoViga;
            ui.MarcarTecho += IniciarMarcadoTecho;
            ui.Encuadrar += IniciarEncuadre;
            ui.CancelarMarcado += CancelarMarcado;
            ui.Panel2D += AlternarPanel2D;
            ActualizarBotonesMarcado();
        }

        // =================================================================
        //  Selección: un solo elemento visible a la vez
        // =================================================================

        /// <summary>Cámara del rig AR: la que escribe la pose.</summary>
        public Camera Camara
        {
            get { return camara; }
        }

        /// <summary>Tag visible ahora mismo, o −1 si todavía no hay contrato.</summary>
        public int TagSeleccionado
        {
            get { return tagSeleccionado; }
        }

        /// <summary>Tags de la lista de la interfaz, en el orden shown.</summary>
        public System.Collections.Generic.List<int> TagsEnLista()
        {
            return ui != null ? new System.Collections.Generic.List<int>(ui.tagsElemento)
                              : new System.Collections.Generic.List<int>();
        }

        /// <summary>Cabecera que se está mostrando ahora mismo.</summary>
        public string CabeceraActual
        {
            get { return ui != null && ui.cabecera != null ? ui.cabecera.text : ""; }
        }

        /// <summary>Texto del contador de frames en pantalla.</summary>
        public string TextoFrames
        {
            get { return ui != null && ui.frames != null ? ui.frames.text : ""; }
        }

        /// <summary>
        /// Plan B a la vista, sin necesidad de pulsar el botón: mismo camino que
        /// el botón «Colocar aquí», que es lo que lo hace verificable.
        ///
        /// Supone el piso <see cref="ARColocacion.AlturaCamaraSobrePiso"/> m por
        /// debajo del teléfono. Funciona aunque no haya plano ni tracking, que
        /// es justo cuando el raycast falla.
        /// </summary>
        public Vector3 ColocarAqui()
        {
            if (camara == null)
            {
                mensaje = "No hay cámara del rig para estimar el piso.";
                RefrescarEstado();
                return Vector3.zero;
            }

            Vector3 pos = ARColocacion.AnclaEstimadaBajoCamara(camara.transform.position);
            CrearAnclaEn(pos);
            mensaje = string.Format(
                "Piso estimado {0:F2} m bajo el teléfono (sin raycast). " +
                "Ajústalo con «Piso ±5 cm».", ARColocacion.AlturaCamaraSobrePiso);
            RefrescarEstado();
            return pos;
        }

        /// <summary>
        /// Plan B: <paramref name="pasos"/> pasos de 5 cm sobre el piso.
        /// Positivo sube el piso, negativo lo baja. Sólo cambia la altura: la
        /// escala del elemento sigue 1:1.
        ///
        /// El ajuste se acumula en <see cref="offsetPiso"/> y se aplica a la
        /// raíz VISUAL, nunca al ancla: ARCore reescribe la pose del ancla y el
        /// desplazamiento se perdería al primer update del subsystem.
        /// </summary>
        public Vector3 DesplazarPiso(int pasos)
        {
            if (ancla == null)
            {
                mensaje = "No hay ancla: primero toca «Colocar aquí».";
                RefrescarEstado();
                return Vector3.zero;
            }

            offsetPiso = ARColocacion.AcumularPiso(offsetPiso, pasos);
            // Sólo cambia la altura: no hace falta pasar por ColocarEnAncla.
            AplicarOffsetPiso();

            Vector3 pos = PosicionPiso();
            mensaje = string.Format("Piso {0}5 cm · altura {1:F2} m",
                                    pasos > 0 ? "+" : "−", pos.y);
            RefrescarEstado();
            return pos;
        }

        /// <summary>Posición mundial del ancla, o <c>Vector3.zero</c> si no hay.</summary>
        public Vector3 PosicionAncla()
        {
            return ancla != null ? ancla.transform.position : Vector3.zero;
        }

        /// <summary>
        /// Piso EFECTIVO: la pose del ancla más el desplazamiento acumulado de
        /// los botones «±5 cm». Es la altura a la que queda el elemento.
        /// </summary>
        public Vector3 PosicionPiso()
        {
            return ancla != null
                ? ancla.transform.position + Vector3.up * offsetPiso
                : Vector3.zero;
        }

        /// <summary>Desplazamiento acumulado de «Piso ±5 cm» (m), en cualquier estado.</summary>
        public float OffsetPiso()
        {
            return offsetPiso;
        }

        /// <summary>
        /// Pega el desplazamiento de ±5 cm en la raíz de diagramas. La raíz es
        /// hija del ancla, así que el diagrama se mueve con él sin que haya que
        /// escribir en la pose que controla ARCore.
        /// </summary>
        private void AplicarOffsetPiso()
        {
            if (raizVisual == null) return;
            raizVisual.transform.localPosition = Vector3.up * offsetPiso;
        }

        private ARGeometriaElemento Seleccionado()
        {
            ARGeometriaElemento g;
            return geoPorTag.TryGetValue(tagSeleccionado, out g) ? g : null;
        }

        private GameObject ContenedorSeleccionado()
        {
            GameObject go;
            return contenedores.TryGetValue(tagSeleccionado, out go) ? go : null;
        }

        private Vector3 FrenteCamara()
        {
            return camara != null ? camara.transform.forward : Vector3.forward;
        }

        private void Seleccionar(int tag)
        {
            bool cambia = tag != tagSeleccionado;
            if (cambia)
            {
                // Cada elemento tiene SU colocación (Corrección 7, 4a): la del que
                // se deja se guarda tal cual, y la del nuevo se recupera. Antes el
                // nuevo aparecía en el ancla del anterior (la columna en el punto
                // medio de la viga) y se reorientaba con la cámara: "se movía".
                GuardarColocacion(tagSeleccionado);
                tagSeleccionado = tag;
                // El marcado a medias era del elemento anterior.
                modo = ModoMarcado.Ninguno;
                esquinas.Clear();
                hayPlanoCuadro = false;
                esquinasEstimadas = 0;
                LimpiarVistaPrevia();
                OcultarReticula();
                CargarColocacion(tag);
            }
            AplicarVisibilidad();          // sólo el contenedor elegido queda activo
            // Re-elegir el MISMO elemento no toca nada: ni posición ni rumbo.
            if (cambia && ancla != null) ColocarEnAncla();
            ActualizarInfo();
            ActualizarBotonesMarcado();
            if (panel2DVisible) RefrescarPanel2D();   // el panel sigue al elemento elegido
            if (cambia && datos != null)
            {
                mensaje = ancla != null
                    ? string.Format("{0} {1}: en su lugar. «Quitar ancla» para marcarlo de nuevo.",
                                    Mayuscula(Seleccionado() != null ? Seleccionado().tipo : "elemento"), tag)
                    : InstruccionInicial();
                RefrescarEstado();
            }
        }

        /// <summary>True si el elemento <paramref name="tag"/> ya tiene su propia ancla.</summary>
        public bool EstaColocado(int tag)
        {
            if (tag == tagSeleccionado) return ancla != null;
            Colocacion c;
            return colocaciones.TryGetValue(tag, out c) && c != null && c.ancla != null;
        }

        /// <summary>Guarda la colocación del elemento que se deja de ver.</summary>
        private void GuardarColocacion(int tag)
        {
            if (tag < 0) return;
            if (ancla == null)
            {
                colocaciones.Remove(tag);
                return;
            }
            colocaciones[tag] = new Colocacion
            {
                ancla = ancla,
                offsetPiso = offsetPiso,
                rotacion = rotacionFija,
                marcadaViga = hayRotacionMarcada
            };
        }

        /// <summary>
        /// Recupera la colocación de <paramref name="tag"/>. Si no tiene, la raíz
        /// de diagramas se suelta (oculta) y el elemento espera a ser marcado.
        /// </summary>
        private void CargarColocacion(int tag)
        {
            SoltarRaiz();
            Colocacion c;
            if (colocaciones.TryGetValue(tag, out c) && c != null && c.ancla != null)
            {
                ancla = c.ancla;
                offsetPiso = c.offsetPiso;
                rotacionFija = c.rotacion;
                hayRotacionMarcada = c.marcadaViga;
                rotacionMarcada = c.marcadaViga ? c.rotacion : Quaternion.identity;
            }
            else
            {
                ancla = null;
                offsetPiso = 0f;
                rotacionFija = Quaternion.identity;
                hayRotacionMarcada = false;
                rotacionMarcada = Quaternion.identity;
            }
        }

        /// <summary>
        /// Saca la raíz de diagramas del ancla actual SIN destruir el ancla: el
        /// ancla sigue siendo del elemento al que pertenece.
        /// </summary>
        private void SoltarRaiz()
        {
            if (raizVisual == null) return;
            if (sesionOrigen != null)
                raizVisual.transform.SetParent(sesionOrigen.transform, false);
            else
                raizVisual.transform.SetParent(transform, false);
            raizVisual.transform.localPosition = Vector3.zero;
            raizVisual.transform.localScale = Vector3.one;
            raizVisual.transform.localRotation = Quaternion.identity;
            raizVisual.SetActive(false);
        }

        // --- Panel 2D (Corrección 6, parte 3d) --------------------------------
        private bool panel2DVisible;
        private Texture2D texPanel2D;

        /// <summary>True si el panel 2D de diagramas está a la vista.</summary>
        public bool Panel2DVisible
        {
            get { return panel2DVisible; }
        }

        /// <summary>Muestra/oculta el panel 2D (botón «Panel 2D»).</summary>
        public void AlternarPanel2D()
        {
            panel2DVisible = !panel2DVisible;
            if (ui != null) ui.MostrarPanel2D(panel2DVisible);
            if (panel2DVisible) RefrescarPanel2D();
        }

        /// <summary>Redibuja el panel 2D con el elemento elegido (mismos datos que el AR).</summary>
        private void RefrescarPanel2D()
        {
            if (ui == null) return;
            ARElemento el = null;
            if (datos != null && datos.elementos != null && tagSeleccionado >= 0)
                datos.elementos.TryGetValue(tagSeleccionado.ToString(), out el);
            if (el == null)
            {
                ui.ActualizarPanel2D("Elige un elemento en la lista.", null, null);
                return;
            }
            texPanel2D = ARGrafico.Dibujar(el, texPanel2D);
            var leyendas = new List<string>();
            foreach (var s in ARGrafico.Series(el)) leyendas.Add(ARGrafico.Leyenda(s));
            string titulo = string.Format("Tag {0} · {1} {2} · L = {3:0.##} m · caso {4}",
                                          el.tag, el.tipo, el.seccion, el.L,
                                          datos.caso);
            ui.ActualizarPanel2D(titulo, leyendas, texPanel2D);
        }

        /// <summary>Selecciona un elemento por su tag (lo mismo que tocar su botón).</summary>
        public void ElegirElemento(int tag)
        {
            Seleccionar(tag);
        }

        /// <summary>
        /// Vuelve a la amplitud por defecto y al rumbo con que se COLOCÓ el
        /// elemento (no al de la cámara de ahora: eso lo movía). NO toca
        /// <see cref="offsetPiso"/>: «Restablecer» es sobre el ELEMENTO
        /// (amplitud y rumbo), no sobre el cuadre del piso, que tiene sus
        /// propios botones.
        /// </summary>
        private void Restablecer()
        {
            amplitudVisual = Mathf.Clamp(amplitudPorDefecto, amplitudMin, amplitudMax);
            ReconstruirSeleccion();
            if (ancla != null) ColocarEnAncla();   // ColocarEnAncla reaplica la orientación
            mensaje = hayRotacionMarcada
                ? "Restablecido: amplitud y orientación marcada."
                : "Restablecido: amplitud y orientación con que se colocó.";
            RefrescarEstado();
        }

        private void CambiarAmplitud(float factor)
        {
            amplitudVisual = Mathf.Clamp(amplitudVisual * factor, amplitudMin, amplitudMax);
            ReconstruirSeleccion();
        }

        private void AplicarToggle(string nombre)
        {
            switch (nombre)
            {
                case "Eje": verEje = !verEje; break;
                case "N": verN = !verN; break;
                case "V": verV = !verV; break;
                case "M": verM = !verM; break;
                case "No principal": verNoPrincipal = !verNoPrincipal; break;
                case "P-M": verPm = !verPm; break;
            }
            AplicarVisibilidad();
        }

        private void ActualizarInfo()
        {
            if (ui == null || geometria == null) return;
            ARGeometriaElemento g = null;
            foreach (var e in geometria) if (e.tag == tagSeleccionado) g = e;
            if (g == null) return;
            ui.info.text = string.Format(
                "<b>Tag {0}</b> · {1}\n{2}\nSección {3} · {4}\n" +
                "Plano principal {5}\nLongitud {6:0.###} m",
                g.tag, g.tipo, g.ubicacion, g.seccion, g.material, g.planoPrincipal, g.longitud);
        }

        // =================================================================
        //  Visibilidad y escala
        // =================================================================

        private void AplicarVisibilidad()
        {
            // Un solo elemento a la vez: cada contenedor se activa o se apaga.
            foreach (var kv in contenedores)
            {
                if (kv.Value != null) kv.Value.SetActive(kv.Key == tagSeleccionado);
            }

            foreach (var v in visores)
            {
                if (v.go == null) continue;
                // Los rótulos siguen al toggle de su diagrama (antes se veían siempre).
                bool vis = Visible(v.tipo, v.principal);
                var mr = v.go.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = vis;
                var lr = v.go.GetComponent<LineRenderer>();
                if (lr != null) lr.enabled = vis;
            }
        }

        private bool Visible(ARTipoTrazo tipo, bool principal)
        {
            if (!principal && (tipo == ARTipoTrazo.Cortante || tipo == ARTipoTrazo.Momento))
                return verNoPrincipal;
            switch (tipo)
            {
                case ARTipoTrazo.Eje: return verEje;
                case ARTipoTrazo.Normal: return verN;
                case ARTipoTrazo.Cortante: return verV;
                case ARTipoTrazo.Momento: return verM;
                case ARTipoTrazo.PM:
                case ARTipoTrazo.Demanda: return verPm;
                default: return true;
            }
        }

        // =================================================================
        //  Entrada
        // =================================================================

        private void Update()
        {
            if (Input.touchCount == 2) GestosDosDedos();
            else if (Input.touchCount == 1) ToqueSimple();

            // El gesto de dos dedos sólo vale mientras hay dos dedos: en
            // cuanto el recuento deja de ser 2 (soltar uno, o todos) se
            // reinicia, si no el siguiente giro compararía contra un ángulo
            // de hace rato.
            if (Input.touchCount != 2) gestoIniciado = false;

            // Mientras se marca, la retícula sigue al centro de la pantalla.
            if (modo != ModoMarcado.Ninguno) ActualizarReticula();

            VigilarSeguimiento(Time.deltaTime);

            diagnosticoTimer += Time.deltaTime;
            if (diagnosticoTimer > 0.5f)
            {
                diagnosticoTimer = 0f;
                int planos = 0;
                if (planeMgr != null)
                {
                    // `ARPlaneManager.trackables` es el recuento real en AR Foundation 4.x.
                    // Ni `subsystem.trackableCount` ni `manager.planes`
                    // existen ya en esta versión.
                    planos = planeMgr.trackables.count;
                }
                string permiso = "desconocido";
                if (camara != null)
                {
                    var camMgr = camara.GetComponent<ARCameraManager>();
                    if (camMgr != null)
                        permiso = camMgr.permissionGranted ? "concedido" : "denegado";
                }
                string estado = "desconocido";
                // `ARSession.state` es ESTÁTICO: no se puede leer como
                // `arSession.state`, que es el error CS0176.
                if (ARSession.state != ARSessionState.None)
                    estado = ARSession.state.ToString();

                int camCount = 0;
                System.Text.StringBuilder camNames = new System.Text.StringBuilder();
                Camera[] camsAll = FindObjectsOfType<Camera>(true);
                for (int i = 0; i < camsAll.Length; i++)
                {
                    if (camsAll[i].enabled)
                    {
                        camCount++;
                        if (camNames.Length > 0) camNames.Append(", ");
                        camNames.Append(camsAll[i].name);
                    }
                }

                string fondo = "ARCameraBackground: ?";
                string fondoMat = "null";
                if (camara != null)
                {
                    var acb = camara.GetComponent<ARCameraBackground>();
                    if (acb != null)
                    {
                        fondo = "ARCameraBackground enabled=" + acb.enabled.ToString();
                        fondoMat = acb.material != null ? (acb.material.shader != null ? acb.material.shader.name : "mat-sin-shader") : "null";
                    }
                }

                string pose = "0,0,0 / 0";
                if (camara != null)
                {
                    var p = camara.transform.position;
                    float yaw = camara.transform.eulerAngles.y;
                    pose = p.x.ToString("F2") + "," + p.y.ToString("F2") + "," + p.z.ToString("F2") + " / " + yaw.ToString("F1");
                }

                string anclaStr = ancla != null ? "si" : "no";
                anclaStr += " · profundidad: " + (oclusion == null ? "sin gestor"
                    : oclusion.currentEnvironmentDepthMode == EnvironmentDepthMode.Disabled
                        ? (ProfundidadPedida ? "pedida, no disponible aún" : "apagada")
                        : "activa (" + oclusion.currentEnvironmentDepthMode + ")");
                string diagText = "ARSession: " + estado + " · planos: " + planos + " · camara: " + permiso + "\n"
                    + ARFramesCamara.Texto() + "\n"
                    + "Camaras: " + camCount + " (" + camNames.ToString() + ")\n" + fondo + " / material=" + fondoMat + "\n" + "Pose camara: " + pose + "\n" + "Planos detectados: " + planos + "\n" + "Ancla: " + anclaStr;

                if (ui != null)
                {
                    if (ui.estado != null)
                        ui.estado.text = mensaje;
                    if (ui.diagnostico != null)
                        ui.diagnostico.text = diagText;
                    // El contador va FUERA del panel de diagnóstico: tiene que
                    // leerse siempre, sin tener que abrir nada.
                    if (ui.frames != null) ui.frames.text = ARFramesCamara.Texto();
                }
            }

            if (placeRequested)
            {
                placeRequested = false;
                ColocarAncla(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            }
        }

        private void ToqueSimple()
        {
            Touch t0 = Input.GetTouch(0);
            if (t0.phase == TouchPhase.Began)
            {
                toqueInicio = t0.position;
                toqueMomento = Time.time;
                // Se decide al EMPEZAR el toque: al terminar, el EventSystem
                // ya pudo soltar el puntero. Un toque que empieza sobre un
                // botón es del botón, nunca del piso.
                toqueSobreUI = SobreUI(t0.fingerId);
            }
            else if (t0.phase == TouchPhase.Ended)
            {
                if (toqueSobreUI) return;
                float dt = Time.time - toqueMomento;
                float d = Vector2.Distance(toqueInicio, t0.position);
                if (dt >= 0.4f || d >= margenToleranciaToque) return;

                if (modo != ModoMarcado.Ninguno)
                {
                    ConfirmarMarcado(t0.position);
                    return;
                }
                // Tocar el piso sólo coloca si todavía no hay nada colocado:
                // un toque accidental no debe mover un elemento ya marcado.
                if (colocarAlTocar && ancla == null)
                    ColocarAncla(t0.position);
            }
        }

        private float anguloPrevio;
        private float distanciaPrevia;
        private bool gestoIniciado;

        private void GestosDosDedos()
        {
            Touch a = Input.GetTouch(0);
            Touch b = Input.GetTouch(1);
            float ang = Mathf.Atan2(b.position.y - a.position.y, b.position.x - a.position.x)
                        * Mathf.Rad2Deg;
            float dist = Vector2.Distance(a.position, b.position);

            if (!gestoIniciado)
            {
                anguloPrevio = ang;
                distanciaPrevia = dist;
                gestoIniciado = true;
                return;
            }

            var contenedor = ContenedorSeleccionado();
            if (contenedor != null && ancla != null)
            {
                // SIN giro con dos dedos (Corrección 7, 4a): sujetando el teléfono
                // es fácil apoyar dos dedos sin querer, y el diagrama giraba. El
                // rumbo sale del marcado y queda fijo.
                //
                // El pellizco cambia SÓLO la amplitud del diagrama. El tamaño
                // del elemento se queda en 1:1: escalar el GameObject
                // falsearía las longitudes y las alturas sobre el piso.
                if (distanciaPrevia > 1f && dist > 1f)
                {
                    float factor = dist / distanciaPrevia;
                    if (Mathf.Abs(factor - 1f) > 0.02f) CambiarAmplitud(factor);
                }
            }

            anguloPrevio = ang;
            distanciaPrevia = dist;
        }

        // =================================================================
        //  Anclaje
        // =================================================================

        private void ColocarAncla(Vector2 pantalla)
        {
            if (raycastMgr == null || datos == null) return;

            if (ARSession.state != ARSessionState.SessionTracking)
            {
                mensaje = avisoSinTracking
                    ? "La sesión AR no está siguiendo: revisa los permisos de cámara " +
                      "y que Play > AR Supported esté marcado en Project Settings."
                    : "La sesión AR aún no está siguiendo…";
                avisoSinTracking = true;
                RefrescarEstado();
                return;
            }

            // Plan A: plano detectado y, si no, puntos de referencia de ARCore
            // (ver RaycastPiso).
            Vector3 pos;
            bool porPunto;
            if (!RaycastPiso(pantalla, out pos, out porPunto))
            {
                mensaje = "No se detectó piso en esa pantalla. " +
                          "Prueba «Colocar aquí» para anclar 1,40 m bajo el teléfono.";
                RefrescarEstado();
                return;
            }

            // Patrón de AR Foundation 4.x: el ancla es un GameObject con el
            // componente ARAnchor, que se registra solo en su OnEnable usando
            // la pose de su transform. AddAnchor(Pose)/RemoveAnchor(anchor)
            // están obsoletas desde 2020-10-06.
            //
            // Se toma la POSICIÓN del impacto con el piso, pero no su rotación:
            // la orientación la decide ARColocacion, no el raycast.
            CrearAnclaEn(pos);

            mensaje = string.Format("Anclado en {0:F2}, {1:F2}, {2:F2} ({3})",
                                    pos.x, pos.y, pos.z,
                                    porPunto ? "punto de referencia" : "raycast");
            RefrescarEstado();
        }

        /// <summary>
        /// Pega el diagrama al ancla actual: en el origen (más el desplazamiento
        /// de ±5 cm), escala 1:1 y con la orientación inicial. Se llama también
        /// al cambiar de tag, para que el elemento nuevo aparezca en el MISMO
        /// ancla.
        /// </summary>
        private void ColocarEnAncla()
        {
            if (raizVisual == null || ancla == null) return;
            raizVisual.transform.SetParent(ancla.transform, false);
            // El desplazamiento de «Piso ±5 cm» va AQUÍ y no en el ancla: ARCore
            // reescribe la pose del ARAnchor y lo perderíamos.
            AplicarOffsetPiso();
            // SIN rumbo de cámara en la raíz (Corrección 4, 1). Con el yaw de la
            // cámara aquí Y la rotación inicial dentro del contenedor, el rumbo
            // se aplicaba DOS veces y el elemento acababa girado un ángulo
            // arbitrario respecto de la cámara. La orientación la pone SOLO el
            // contenedor, que es donde acumula el gesto de dos dedos.
            raizVisual.transform.localRotation = Quaternion.identity;
            raizVisual.transform.localScale = Vector3.one;   // 1:1, siempre
            raizVisual.SetActive(true);

            var contenedor = ContenedorSeleccionado();
            // El rumbo es el FIJADO al colocar (CrearAnclaEn / marcado de la viga):
            // aquí ya no se lee la cámara, si no el elemento giraba cada vez.
            if (contenedor != null)
                contenedor.transform.localRotation = rotacionFija;
            AplicarVisibilidad();
            CongelarTextos();
        }

        /// <summary>
        /// Cambio 01: deja los rótulos del elemento elegido FIJOS (orientados una vez
        /// hacia la cámara de ahora). Desde ahí no giran ni cambian de tamaño.
        /// </summary>
        private void CongelarTextos()
        {
            var c = ContenedorSeleccionado();
            if (c == null) return;
            foreach (var b in c.GetComponentsInChildren<ARBillboard>(true))
                if (!b.congelado) b.Congelar(camara);
        }

        private void QuitarAncla()
        {
            // Sin ancla no hay piso que cuadrar: el desplazamiento acumulado se
            // pierde con ella, y si no el siguiente «Colocar aquí» saldría
            // desplazado el número de pasos que se hubieran pulsado antes.
            offsetPiso = 0f;
            hayRotacionMarcada = false;
            rotacionFija = Quaternion.identity;
            // Sólo el ancla del elemento ELEGIDO: los demás conservan la suya.
            colocaciones.Remove(tagSeleccionado);
            // Sin ancla tampoco hay recuadro: el elemento vuelve al dibujo del modelo.
            if (cuadroPorTag.Remove(tagSeleccionado)) ReconstruirSeleccion();

            // La raíz de diagramas se SACA del ancla ANTES de destruirlo: es
            // hija suya, y `Destroy` de un GameObject se lleva por delante a
            // todos sus hijos. Si se destrujera primero, el diagrama entero se
            // perdía y el siguiente «Colocar aquí» no tenía nada que mostrar.
            if (raizVisual != null)
            {
                if (sesionOrigen != null)
                    raizVisual.transform.SetParent(sesionOrigen.transform, false);
                else
                    raizVisual.transform.SetParent(transform, false);
                raizVisual.transform.localPosition = Vector3.zero;
                raizVisual.transform.localScale = Vector3.one;
                raizVisual.transform.localRotation = Quaternion.identity;
                raizVisual.SetActive(false);
            }

            // Destruir el GameObject del ancla basta: ARAnchor.OnDisable se
            // desregistra del ARAnchorManager por su cuenta.
            if (ancla != null)
            {
                Destruir(ancla.gameObject);
                ancla = null;
            }

            mensaje = "Ancla retirada. Toca para recolocar.";
            RefrescarEstado();
        }

        private void RefrescarEstado()
        {
            if (ui != null) ui.estado.text = mensaje;
        }

        // =================================================================
        //  Plan B: colocar sin raycast (Corrección 3, C)
        //
        //  `ColocarAqui` y `DesplazarPiso` viven arriba, junto a los otros
        //  miembros públicos que también usan los tests de EditMode.
        // =================================================================

        /// <summary>
        /// Crea el GameObject del ancla en <paramref name="pos"/> y coloca el
        /// diagrama. Compartido por el Plan A (impacto del raycast) y el
        /// Plan B (estimación bajo la cámara): sólo cambia de dónde sale el
        /// punto, nunca cómo se orienta o se escala.
        /// </summary>
        private void CrearAnclaEn(Vector3 pos)
        {
            QuitarAncla();

            var go = new GameObject("Ancla");
            go.transform.position = pos;
            ancla = go.AddComponent<ARAnchor>();

            // El ancla se deja SIN rotación a propósito: el punto es el que
            // aporta el Plan A o el Plan B, y el rumbo lo pone ColocarEnAncla
            // en el CONTENEDOR del elemento elegido.
            //
            // El rumbo por cámara se calcula AQUÍ, una sola vez (Corrección 7, 4a).
            // Si el elemento es una viga marcada, MarcarPunto lo reemplaza enseguida
            // por el de sus dos extremos.
            var g = Seleccionado();
            rotacionFija = g != null ? ARColocacion.RotacionInicial(g, FrenteCamara())
                                     : Quaternion.identity;
            ColocarEnAncla();
        }

        // =================================================================
        //  Colocación MARCANDO el elemento real (Corrección 5)
        //
        //  Flujo en terreno:
        //   · Columna / muro: «Marcar base» → apuntar el centro de la pantalla
        //     al pie de la columna → tocar. El ancla queda en el centro de su
        //     base (media sección hacia adentro de la cara que se ve).
        //   · Viga: «Marcar viga: pie de columnas» → tocar el pie de la columna
        //     del extremo i → el de la del extremo j. El ancla va al punto medio
        //     y el eje i→j del elemento queda sobre la línea marcada.
        //   · Viga, sin bajar al piso (Corrección 7, 4b): «Marcar viga: apuntar
        //     a ella» → la cara inferior junto a cada columna (MarcarPuntoTecho).
        //
        //  Así la posición y el rumbo salen del EDIFICIO, no de dónde está
        //  parado el usuario ni de hacia dónde apunta la cámara: el diagrama
        //  queda pegado al elemento al caminar alrededor.
        // =================================================================

        /// <summary>Qué espera la app del próximo toque.</summary>
        public ModoMarcado Modo
        {
            get { return modo; }
        }

        /// <summary>Interfaz construida (para los tests).</summary>
        public ARInterfaz Interfaz
        {
            get { return ui; }
        }

        /// <summary>True si el ancla actual salió de marcar los dos extremos de una viga.</summary>
        public bool TieneRotacionMarcada
        {
            get { return hayRotacionMarcada; }
        }

        /// <summary>Último mensaje mostrado al usuario.</summary>
        public string Mensaje
        {
            get { return mensaje; }
        }

        /// <summary>Empieza a marcar el pie de la columna / muro elegido.</summary>
        public void IniciarMarcadoBase()
        {
            var g = Seleccionado();
            if (g == null)
            {
                mensaje = "Primero elige un elemento en la lista.";
                RefrescarEstado();
                return;
            }
            if (g.esViga)
            {
                IniciarMarcadoViga();
                return;
            }
            modo = ModoMarcado.Base;
            MostrarReticula();
            mensaje = InstruccionMarcado();
            RefrescarEstado();
            ActualizarBotonesMarcado();
        }

        /// <summary>Empieza a marcar los dos extremos de la viga elegida.</summary>
        public void IniciarMarcadoViga()
        {
            var g = Seleccionado();
            if (g == null)
            {
                mensaje = "Primero elige un elemento en la lista.";
                RefrescarEstado();
                return;
            }
            if (!g.esViga)
            {
                IniciarMarcadoBase();
                return;
            }
            modo = ModoMarcado.ExtremoI;
            MostrarReticula();
            mensaje = InstruccionMarcado();
            RefrescarEstado();
            ActualizarBotonesMarcado();
        }

        /// <summary>
        /// Empieza a marcar la viga APUNTANDO A ELLA (Corrección 7, 4b): la mira
        /// a su cara inferior junto a la columna del extremo i, tocar; luego
        /// junto a la del extremo j, tocar. No hace falta ver el piso.
        /// </summary>
        public void IniciarMarcadoTecho()
        {
            var g = Seleccionado();
            if (g == null)
            {
                mensaje = "Primero elige un elemento en la lista.";
                RefrescarEstado();
                return;
            }
            if (!g.esViga)
            {
                IniciarMarcadoBase();
                return;
            }
            modo = ModoMarcado.TechoI;
            MostrarReticula();
            mensaje = InstruccionMarcado();
            RefrescarEstado();
            ActualizarBotonesMarcado();
        }

        /// <summary>
        /// Registra un punto de la cara inferior de la viga (modo TechoI / TechoJ).
        /// <paramref name="origen"/> dice si se midió (profundidad, plano, punto
        /// de ARCore) o se estimó con la altura del modelo. Público para que los
        /// tests recorran el mismo camino sin sesión AR.
        /// </summary>
        public void MarcarPuntoTecho(Vector3 p, OrigenTecho origen)
        {
            var g = Seleccionado();
            if (g == null || !g.esViga)
            {
                mensaje = "Elige una viga para marcarla apuntando a ella.";
                RefrescarEstado();
                return;
            }

            if (modo == ModoMarcado.TechoI)
            {
                caraTechoI = p;
                origenTechoI = origen;
                modo = ModoMarcado.TechoJ;
                mensaje = InstruccionMarcado();
                RefrescarEstado();
                ActualizarBotonesMarcado();
                return;
            }
            if (modo != ModoMarcado.TechoJ)
            {
                mensaje = "Toca «Marcar viga: apuntar a ella» antes de marcar.";
                RefrescarEstado();
                return;
            }

            // Los dos puntos tienen que estar separados: si no, el rumbo no se
            // puede leer (se mide ENTRE CARAS, antes de correrlos a los ejes).
            float entreCaras = ARColocacion.DistanciaHorizontal(caraTechoI, p);
            if (entreCaras < ARColocacion.DistanciaMinimaPuntos)
            {
                modo = ModoMarcado.TechoI;
                mensaje = string.Format(
                    "Los dos puntos quedaron a {0:F2} m. Marca de nuevo, primero junto a {1}.",
                    entreCaras, ARApoyos.NombreApoyo(Apoyo(true)));
                RefrescarEstado();
                return;
            }

            // Ejes de las columnas de apoyo: media sección más allá de cada cara.
            Vector3 ejeI, ejeJ;
            ARTecho.EjesDesdeCaras(caraTechoI, p,
                                   ARApoyos.MitadApoyo(Apoyo(true)), ARApoyos.MitadApoyo(Apoyo(false)),
                                   out ejeI, out ejeJ);

            // Piso: el conocido; si no hay, el que corresponde a la cara inferior.
            float hInf = ARTecho.AlturaInferiorViga(g);
            float yInf = 0.5f * (caraTechoI.y + p.y);
            bool medidos = ARTecho.EsMedido(origenTechoI) && ARTecho.EsMedido(origen);
            float yPiso = hayPisoConocido ? yPisoConocido : ARTecho.PisoDesdeInferior(yInf, hInf);

            var c = ARColocacion.PorDosPuntos(new Vector3(ejeI.x, yPiso, ejeI.z),
                                              new Vector3(ejeJ.x, yPiso, ejeJ.z), g);

            modo = ModoMarcado.Ninguno;
            OcultarReticula();
            CrearAnclaEn(c.posicion);
            rotacionMarcada = c.rotacion;
            hayRotacionMarcada = true;
            rotacionFija = c.rotacion;
            ColocarEnAncla();

            float L = (float)g.longitud;
            mensaje = string.Format(
                "Viga por su cara inferior. Medido {0:F2} m entre ejes · modelo {1:F2} m ({2:+0.0;-0.0;0.0} %)",
                c.distancia, L, ARColocacion.DiferenciaPorcentual(c.distancia, L));
            if (ARColocacion.FueraDeTolerancia(c.distancia, L))
                mensaje += " — revisa los puntos: no coincide con L";
            if (hayPisoConocido && medidos)
                mensaje += string.Format(" · bajo viga {0:F2} m (modelo {1:F2} m)", yInf - yPiso, hInf);
            if (!medidos)
                mensaje += " · punto ESTIMADO: si no calza la altura, usa «Piso ±5 cm»";
            RefrescarEstado();
            ActualizarBotonesMarcado();
        }

        // =================================================================
        //  Encuadre por 4 esquinas (Corrección 8, parte 5a)
        // =================================================================

        /// <summary>
        /// Radio del anillo como fracción de su distancia a la cámara (Corrección 9,
        /// 6b): así se ve SIEMPRE del mismo tamaño en pantalla, cerca o lejos. Antes
        /// tenía radio fijo en metros y salía enorme cerca y diminuto lejos.
        /// </summary>
        public const float RadioReticulaRelativo = 0.045f;

        /// <summary>Escala del anillo para que su radio sea <see cref="RadioReticulaRelativo"/>·distancia.</summary>
        public static float EscalaReticula(float distancia)
        {
            float r = Mathf.Clamp(distancia, 0.2f, 30f) * RadioReticulaRelativo;
            return r / RadioReticula;
        }

        private void EscalarReticula(Vector3 p)
        {
            if (reticula == null || camara == null) return;
            reticula.transform.localScale =
                Vector3.one * EscalaReticula(Vector3.Distance(camara.transform.position, p));
        }

        /// <summary>
        /// Guía mientras se marca: con 1 esquina, la línea hasta la mira; con 2 o más,
        /// el recuadro que va a quedar (base marcada + altura de la mira).
        /// </summary>
        private void ActualizarGuia(Vector3? actual)
        {
            if (matLinea == null) return;
            if (guia == null)
            {
                guia = new GameObject("GuiaCuadro");
                guia.transform.SetParent(transform, false);
                var lr0 = guia.AddComponent<LineRenderer>();
                PrepararLineaMundo(lr0, new Color(1f, 0.85f, 0.10f, 0.9f), 0.004f);
            }
            var lr = guia.GetComponent<LineRenderer>();
            if (!actual.HasValue || esquinas.Count == 0 || modo != ModoMarcado.Cuadro)
            {
                lr.positionCount = 0;
                return;
            }
            // Cambio 01: las esquinas marcadas + la mira, cerrado; todo en el plano.
            var pts = new List<Vector3>(esquinas);
            pts.Add(actual.Value);
            if (pts.Count >= 3) pts.Add(pts[0]);
            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
        }

        // =================================================================
        //  Diagrama SIEMPRE vertical (Corrección 9, 6d)
        //
        //  ARCore corrige la pose de las anclas mientras mapea; con poca luz esa
        //  corrección puede inclinar el ancla unos grados y el diagrama se ve
        //  torcido respecto de la columna. La raíz de diagramas conserva SOLO el
        //  giro en planta del ancla: la vertical es siempre la de la gravedad, y
        //  el «Piso ±5 cm» se aplica en vertical verdadera.
        // =================================================================

        private void LateUpdate()
        {
            NivelarRaiz();
        }

        /// <summary>Quita a la raíz de diagramas la inclinación del ancla (deja el giro en planta).</summary>
        public void NivelarRaiz()
        {
            if (ancla == null || raizVisual == null || raizVisual.transform.parent != ancla.transform) return;
            Quaternion qa = ancla.transform.rotation;
            Vector3 f = qa * Vector3.forward;
            f.y = 0f;
            Quaternion soloPlanta = f.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(f.normalized, Vector3.up)
                : Quaternion.identity;
            raizVisual.transform.localRotation = Quaternion.Inverse(qa) * soloPlanta;
            raizVisual.transform.position = ancla.transform.position + Vector3.up * offsetPiso;
        }

        /// <summary>Cuántas esquinas lleva marcadas el encuadre en curso.</summary>
        public int EsquinasMarcadas
        {
            get { return esquinas.Count; }
        }

        /// <summary>True si el elemento <paramref name="tag"/> está dibujado dentro de un recuadro.</summary>
        public bool TieneEncuadre(int tag)
        {
            return cuadroPorTag.ContainsKey(tag);
        }

        /// <summary>Recuadro del elemento <paramref name="tag"/> (sólo vale si <see cref="TieneEncuadre"/>).</summary>
        public ARCuadroGeom Encuadre(int tag)
        {
            ARCuadroGeom q;
            return cuadroPorTag.TryGetValue(tag, out q) ? q : new ARCuadroGeom();
        }

        /// <summary>
        /// Empieza a marcar las 4 esquinas de la cara del elemento elegido
        /// (columna o viga, en cualquier orden).
        /// </summary>
        public void IniciarEncuadre()
        {
            var g = Seleccionado();
            if (g == null)
            {
                mensaje = "Primero elige un elemento en la lista.";
                RefrescarEstado();
                return;
            }
            modo = ModoMarcado.Cuadro;
            esquinas.Clear();
            hayPlanoCuadro = false;
            esquinasEstimadas = 0;
            LimpiarVistaPrevia();
            MostrarReticula();
            mensaje = InstruccionMarcado();
            RefrescarEstado();
            ActualizarBotonesMarcado();
        }

        /// <summary>
        /// Registra una esquina. Con la cuarta arma el recuadro, ancla el elemento
        /// en su centro y lo redibuja dentro. Público para los tests.
        /// </summary>
        public void MarcarEsquina(Vector3 p, OrigenCuadro origen = OrigenCuadro.Profundidad)
        {
            var g = Seleccionado();
            if (g == null || modo != ModoMarcado.Cuadro)
            {
                mensaje = "Toca «Encuadrar: 4 esquinas» antes de marcar.";
                RefrescarEstado();
                return;
            }
            // Cambio 01: la PRIMERA esquina fija el plano (vertical, de frente a la
            // cámara); las demás van siempre sobre él. El recuadro queda plano, como
            // una imagen impresa: nada más cerca ni más lejos para el que mira.
            if (!hayPlanoCuadro || esquinas.Count == 0)
            {
                if (!ARCuadro.PlanoDeFrente(p, FrenteCamara(), out planoCuadroPunto, out planoCuadroNormal))
                {
                    planoCuadroPunto = p;
                    planoCuadroNormal = Vector3.back;
                }
                hayPlanoCuadro = true;
                if (origen == OrigenCuadro.Estimado || origen == OrigenCuadro.PuntoCaracteristico)
                    esquinasEstimadas++;
            }
            else
            {
                p = ARCuadro.ProyectarEnPlano(p, planoCuadroPunto, planoCuadroNormal);
            }
            esquinas.Add(p);
            ActualizarVistaPrevia();

            if (esquinas.Count < ARCuadro.Esquinas)
            {
                mensaje = InstruccionMarcado();
                RefrescarEstado();
                return;
            }
            TerminarEncuadre(g);
        }

        private void TerminarEncuadre(ARGeometriaElemento g)
        {
            Vector3 frente = FrenteCamara();
            var q = ARCuadro.Desde4Puntos(esquinas, g.esViga, ARColocacion.Derecha(frente), frente);
            string error = ARCuadro.ValidarRecuadro(q);
            if (error != null)
            {
                esquinas.Clear();
                hayPlanoCuadro = false;
                esquinasEstimadas = 0;
                ActualizarVistaPrevia();
                mensaje = error + " Vuelve a marcar las 4 esquinas.";
                RefrescarEstado();
                return;
            }

            int tag = g.tag;
            int estimadas = esquinasEstimadas;
            modo = ModoMarcado.Ninguno;
            OcultarReticula();
            LimpiarVistaPrevia();
            esquinas.Clear();
            hayPlanoCuadro = false;
            esquinasEstimadas = 0;

            // Ancla en el CENTRO del recuadro (cerca de lo dibujado: menos brazo
            // para los errores de giro) y sin rotación: el recuadro ya está en
            // ejes del mundo.
            CrearAnclaEn(q.centro);
            cuadroPorTag[tag] = q;
            rotacionFija = Quaternion.identity;
            hayRotacionMarcada = false;
            ReconstruirSeleccion();
            ColocarEnAncla();

            mensaje = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Encuadre listo: {0:0.00} m × {1:0.00} m{2}. Diagramas dentro del recuadro " +
                "(modelo: L = {3:0.00} m).",
                q.largo, q.ancho, q.caraInferior ? " (cara inferior)" : "", g.longitud);
            if (estimadas > 0)
                mensaje += string.Format(" Ojo: {0} esquina(s) sin medir; si no calza, encuadra de nuevo.", estimadas);
            // Con el diagrama puesto, el menú se pliega para no taparlo (Corrección 9, 6c).
            if (ui != null)
            {
                ui.MostrarControles(false);
                mensaje += " Toca «Menú» para ver los botones.";
            }
            RefrescarEstado();
            ActualizarBotonesMarcado();
        }

        private string InstruccionCuadro()
        {
            var g = Seleccionado();
            int n = esquinas.Count + 1;
            string que = g == null ? "del elemento"
                : g.esViga ? "de la viga (entre las caras de " + ARApoyos.NombreApoyo(Apoyo(true)) +
                             " y " + ARApoyos.NombreApoyo(Apoyo(false)) + ")"
                           : "de la " + g.tipo;
            return string.Format("Esquina {0} de 4 {1}: pon el CENTRO de la mira en la esquina y toca " +
                                 "(en cualquier orden).", n, que);
        }

        /// <summary>
        /// Esquina bajo <paramref name="pantalla"/>: profundidad → plano detectado
        /// → plano de las esquinas ya marcadas → punto de ARCore → estimado.
        /// </summary>
        private bool ResolverPuntoCuadro(Vector2 pantalla, out Vector3 punto, out OrigenCuadro origen)
        {
            punto = Vector3.zero;
            origen = OrigenCuadro.Ninguno;
            if (camara == null || raycastMgr == null) return false;
            if (ARSession.state != ARSessionState.SessionTracking) return false;
            if (Seleccionado() == null) return false;
            Ray rayo = camara.ScreenPointToRay(pantalla);

            // Cambio 01: desde la 2ª esquina, SOLO el plano de frente fijado por la
            // 1ª. Nada de raycast ni profundidad: todas quedan a la misma distancia.
            if (esquinas.Count >= 1 && hayPlanoCuadro)
            {
                if (!ARCuadro.InterseccionPlano(rayo.origin, rayo.direction,
                                                planoCuadroPunto, planoCuadroNormal, out punto))
                    return false;
                origen = OrigenCuadro.PlanoDelRecuadro;
                return true;
            }

            // 1ª esquina: la superficie real bajo la mira (sólo para saber a qué
            // DISTANCIA está el elemento): profundidad → plano → punto de ARCore →
            // estimado con las alturas del modelo.
            if (ProfundidadPedida && PrimerImpacto(pantalla, TrackableType.Depth, out punto))
            {
                origen = OrigenCuadro.Profundidad;
                return true;
            }
            if (PrimerImpacto(pantalla, TrackableType.PlaneWithinPolygon, out punto))
            {
                origen = OrigenCuadro.Plano;
                return true;
            }
            if (PrimerImpacto(pantalla, TrackableType.FeaturePoint, out punto))
            {
                origen = OrigenCuadro.PuntoCaracteristico;
                return true;
            }
            return PuntoCuadroDesdeRayo(rayo, camara.transform.position.y, out punto, out origen);
        }

        private bool PrimerImpacto(Vector2 pantalla, TrackableType tipo, out Vector3 punto)
        {
            punto = Vector3.zero;
            hitsPiso.Clear();
            if (!raycastMgr.Raycast(pantalla, hitsPiso, tipo) || hitsPiso.Count == 0) return false;
            punto = hitsPiso[0].pose.position;
            return true;
        }

        /// <summary>
        /// Respaldo sin ARCore: mirando hacia abajo, el piso (conocido o 1,40 m bajo
        /// el teléfono); mirando hacia arriba, el cielo a la altura del MODELO sobre
        /// ese piso (cabeza de la columna, cara inferior de la viga). Público para
        /// los tests.
        /// </summary>
        public bool PuntoCuadroDesdeRayo(Ray rayo, float yCamara, out Vector3 punto, out OrigenCuadro origen)
        {
            origen = OrigenCuadro.Ninguno;
            punto = Vector3.zero;
            float yPiso = ARPiso.AlturaPisoEstimada(yCamara, hayPisoConocido, yPisoConocido);
            float y = yPiso;
            if (rayo.direction.y > 0f)
            {
                ARElemento el = ElementoSeleccionado();
                if (el == null) return false;
                var gm = ARGeometriaBuilder.ConstruirElemento(el, new ARGeometriaOpciones());
                float alto = gm.esViga ? ARTecho.AlturaInferiorViga(gm)
                                       : Mathf.Max(gm.puntoI.y, gm.puntoJ.y);
                y = yPiso + alto;
            }
            if (!ARPiso.InterseccionPlanoHorizontal(rayo.origin, rayo.direction, y, out punto))
                return false;
            origen = OrigenCuadro.Estimado;
            return true;
        }

        /// <summary>Dibuja las esquinas ya marcadas (cruces) unidas en el orden en que se marcaron.</summary>
        private void ActualizarVistaPrevia()
        {
            LimpiarVistaPrevia();
            if (matLinea == null || esquinas.Count == 0) return;
            vistaPrevia = new GameObject("VistaPreviaCuadro");
            vistaPrevia.transform.SetParent(transform, false);
            var blanco = new Color(1f, 1f, 1f, 0.95f);
            foreach (var e in esquinas)
            {
                foreach (Vector3 d in new[] { Vector3.right, Vector3.up, Vector3.forward })
                {
                    var go = new GameObject("Esquina");
                    go.transform.SetParent(vistaPrevia.transform, false);
                    var lr = go.AddComponent<LineRenderer>();
                    // Cambio 01: la esquina es un PUNTO; la cruz es mínima, sólo para verla.
                    PrepararLineaMundo(lr, blanco, 0.004f);
                    lr.positionCount = 2;
                    lr.SetPositions(new[] { e - d * 0.015f, e + d * 0.015f });
                }
            }
            if (esquinas.Count >= 2)
            {
                var go = new GameObject("Lados");
                go.transform.SetParent(vistaPrevia.transform, false);
                var lr = go.AddComponent<LineRenderer>();
                PrepararLineaMundo(lr, blanco, 0.004f);
                lr.positionCount = esquinas.Count;
                lr.SetPositions(esquinas.ToArray());
            }
        }

        private void PrepararLineaMundo(LineRenderer lr, Color c, float ancho)
        {
            lr.sharedMaterial = matLinea;
            lr.useWorldSpace = true;
            lr.startWidth = ancho;
            lr.endWidth = ancho;
            lr.startColor = c;
            lr.endColor = c;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void LimpiarVistaPrevia()
        {
            if (vistaPrevia != null) Destruir(vistaPrevia);
            vistaPrevia = null;
            if (guia != null) Destruir(guia);
            guia = null;
        }

        // =================================================================
        //  Seguimiento (Corrección 8, parte 5b)
        //
        //  Si ARCore pierde el seguimiento del ancla (cámara tapada, movimiento
        //  brusco, pared lisa), el diagrama se queda en una pose vieja y "se
        //  mueve" respecto de la escena. Mejor ocultarlo y avisar: cuando ARCore
        //  se vuelve a ubicar (se apunta de nuevo a la zona marcada), el ancla
        //  recupera su lugar y el diagrama reaparece en el recuadro.
        // =================================================================

        /// <summary>Segundos sin seguimiento antes de ocultar (evita parpadeos).</summary>
        public const float EsperaSinSeguimiento = 0.5f;

        /// <summary>True mientras el diagrama está oculto porque ARCore perdió el ancla.</summary>
        public bool OcultoPorSeguimiento
        {
            get { return ocultoPorSeguimiento; }
        }

        /// <summary>
        /// Decide si el diagrama se ve según el estado del ancla. Público (con el
        /// estado como parámetro) para probarlo sin sesión AR.
        /// </summary>
        public void AplicarSeguimiento(bool anclaSeguida, float dt)
        {
            if (ancla == null || raizVisual == null)
            {
                ocultoPorSeguimiento = false;
                tiempoSinSeguimiento = 0f;
                return;
            }
            tiempoSinSeguimiento = anclaSeguida ? 0f : tiempoSinSeguimiento + dt;
            bool ocultar = tiempoSinSeguimiento >= EsperaSinSeguimiento;
            if (ocultar == ocultoPorSeguimiento) return;
            ocultoPorSeguimiento = ocultar;
            raizVisual.SetActive(!ocultar);
            mensaje = ocultar
                ? "Se perdió el seguimiento: apunta de nuevo a la zona donde encuadraste y el diagrama vuelve a su lugar."
                : "Seguimiento recuperado: el diagrama está donde lo encuadraste.";
            RefrescarEstado();
        }

        private void VigilarSeguimiento(float dt)
        {
            if (ancla == null) { AplicarSeguimiento(true, dt); return; }
            // Sólo cuenta con la sesión en marcha: en el Editor sin AR no hay
            // seguimiento y el diagrama tiene que seguir viéndose.
            if (ARSession.state != ARSessionState.SessionTracking) { AplicarSeguimiento(true, dt); return; }
            AplicarSeguimiento(ancla.trackingState != TrackingState.None, dt);
        }

        /// <summary>Sale del modo de marcado sin tocar el ancla actual.</summary>
        public void CancelarMarcado()
        {
            modo = ModoMarcado.Ninguno;
            esquinas.Clear();
            hayPlanoCuadro = false;
            esquinasEstimadas = 0;
            LimpiarVistaPrevia();
            OcultarReticula();
            mensaje = "Marcado cancelado.";
            RefrescarEstado();
            ActualizarBotonesMarcado();
        }

        /// <summary>
        /// Registra un punto del piso según el modo actual. Es lo que hace el
        /// toque en pantalla con el punto de la retícula; es público para que
        /// los tests de EditMode recorran el mismo camino sin sesión AR.
        /// </summary>
        public void MarcarPunto(Vector3 p)
        {
            var g = Seleccionado();
            if (g == null)
            {
                mensaje = "Primero elige un elemento en la lista.";
                RefrescarEstado();
                return;
            }

            switch (modo)
            {
                case ModoMarcado.Base:
                {
                    Vector3 centro = ARColocacion.BaseDesdeCara(
                        p, FrenteCamara(), ARColocacion.MitadSeccion(g.seccion));
                    modo = ModoMarcado.Ninguno;
                    OcultarReticula();
                    CrearAnclaEn(centro);   // orientación por cámara (RotacionInicial)
                    mensaje = string.Format(
                        "Base marcada: {0} {1}. Si no calza la altura, usa «Piso ±5 cm».",
                        g.tipo, g.tag);
                    break;
                }

                case ModoMarcado.ExtremoI:
                    // Se marca el PIE de la columna que sostiene el extremo: se entra
                    // media sección hacia su eje, que es donde llega el eje de la viga.
                    puntoI = CentroApoyo(p, true);
                    modo = ModoMarcado.ExtremoJ;
                    mensaje = InstruccionMarcado();
                    break;

                case ModoMarcado.ExtremoJ:
                {
                    Vector3 pj = CentroApoyo(p, false);
                    var c = ARColocacion.PorDosPuntos(puntoI, pj, g);
                    if (c.distancia < ARColocacion.DistanciaMinimaPuntos)
                    {
                        modo = ModoMarcado.ExtremoI;
                        mensaje = string.Format(
                            "Los dos puntos quedaron a {0:F2} m. Marca de nuevo, " +
                            "primero el pie de la columna del extremo i.", c.distancia);
                        break;
                    }

                    modo = ModoMarcado.Ninguno;
                    OcultarReticula();
                    CrearAnclaEn(c.posicion);          // QuitarAncla borra la rotación anterior
                    rotacionMarcada = c.rotacion;
                    hayRotacionMarcada = true;
                    rotacionFija = c.rotacion;         // el rumbo de la viga es el marcado
                    ColocarEnAncla();                  // aplica la rotación marcada al contenedor

                    float L = (float)g.longitud;
                    mensaje = string.Format(
                        "Medido {0:F2} m · modelo {1:F2} m ({2:+0.0;-0.0;0.0} %)",
                        c.distancia, L, ARColocacion.DiferenciaPorcentual(c.distancia, L));
                    if (ARColocacion.FueraDeTolerancia(c.distancia, L))
                        mensaje += " — revisa los puntos: no coincide con L";
                    break;
                }

                default:
                    mensaje = g.esViga
                        ? "Toca «Marcar viga: pie de columnas» antes de marcar en el piso."
                        : "Toca «Marcar base» antes de marcar en el piso.";
                    break;
            }

            RefrescarEstado();
            ActualizarBotonesMarcado();
        }

        /// <summary>
        /// Toque en pantalla durante el marcado: usa el punto de la retícula
        /// (centro de la pantalla) y, si no hay, un raycast donde se tocó.
        /// </summary>
        private void ConfirmarMarcado(Vector2 pantalla)
        {
            if (modo == ModoMarcado.Cuadro)
            {
                Vector3 pc;
                OrigenCuadro oc;
                if (reticulaValida)
                {
                    pc = puntoReticula;
                    oc = origenReticulaCuadro;
                }
                else if (!ResolverPuntoCuadro(pantalla, out pc, out oc))
                {
                    mensaje = "No encuentro esa esquina: apunta la mira al elemento y vuelve a tocar.";
                    RefrescarEstado();
                    return;
                }
                MarcarEsquina(pc, oc);
                return;
            }
            if (EsModoTecho(modo))
            {
                Vector3 pt;
                OrigenTecho ot;
                if (reticulaValida)
                {
                    pt = puntoReticula;
                    ot = origenReticulaTecho;
                }
                else if (!ResolverPuntoTecho(pantalla, out pt, out ot))
                {
                    mensaje = "No encuentro la viga ahí: apunta la mira a su cara INFERIOR y vuelve a tocar.";
                    RefrescarEstado();
                    return;
                }
                MarcarPuntoTecho(pt, ot);
                return;
            }

            Vector3 p;
            bool porPunto;
            if (reticulaValida)
            {
                p = puntoReticula;
            }
            else if (!RaycastPiso(pantalla, out p, out porPunto))
            {
                mensaje = "No encuentro el piso ahí: inclina el teléfono hacia el piso " +
                          "hasta que aparezca el anillo y vuelve a tocar.";
                RefrescarEstado();
                return;
            }
            OrigenPunto origen = reticulaValida ? origenReticula : OrigenPunto.Ninguno;
            MarcarPunto(p);
            // Si el punto salió del piso ESTIMADO, avisarlo: la altura puede no calzar.
            if (origen == OrigenPunto.PisoEstimado || origen == OrigenPunto.PuntoCaracteristico)
            {
                mensaje += "  (" + ARPiso.Describir(origen) + ": si no calza, usa «Piso ±5 cm»)";
                RefrescarEstado();
            }
        }

        /// <summary>
        /// Punto del piso bajo el centro (o el punto) de la pantalla. Usa la
        /// cadena de respaldos de <see cref="ARPiso"/>: plano detectado bajo el
        /// teléfono → último piso conocido extendido → punto característico a
        /// la altura del piso → piso estimado 1,40 m bajo el teléfono. Así el
        /// anillo no se pierde aunque ARCore no vea el piso en ese punto.
        /// </summary>
        private bool RaycastPiso(Vector2 pantalla, out Vector3 punto, out bool porPunto)
        {
            OrigenPunto origen;
            bool ok = ResolverPuntoPiso(pantalla, out punto, out origen);
            porPunto = origen != OrigenPunto.PisoDetectado && origen != OrigenPunto.PisoExtendido;
            return ok;
        }

        private bool ResolverPuntoPiso(Vector2 pantalla, out Vector3 punto, out OrigenPunto origen)
        {
            punto = Vector3.zero;
            origen = OrigenPunto.Ninguno;
            if (camara == null || raycastMgr == null) return false;
            if (ARSession.state != ARSessionState.SessionTracking) return false;

            float yCam = camara.transform.position.y;

            // 1) Plano detectado, sólo si está BAJO el teléfono (no cielos).
            hitsPiso.Clear();
            if (raycastMgr.Raycast(pantalla, hitsPiso, TrackableType.PlaneWithinPolygon))
            {
                foreach (var h in hitsPiso)
                {
                    if (!ARPiso.EsPiso(h.pose.position.y, yCam)) continue;
                    punto = h.pose.position;
                    RegistrarAlturaPiso(punto.y);
                    origen = OrigenPunto.PisoDetectado;
                    return true;
                }
            }

            // 1b) Profundidad de ARCore (Corrección 7, 4b): la distancia REAL al
            //     piso bajo la mira, aunque ARCore no haya armado un plano ahí.
            if (ProfundidadPedida)
            {
                hitsPiso.Clear();
                if (raycastMgr.Raycast(pantalla, hitsPiso, TrackableType.Depth))
                {
                    foreach (var h in hitsPiso)
                    {
                        if (!ARPiso.CercaDelPiso(h.pose.position.y, yCam, hayPisoConocido, yPisoConocido))
                            continue;
                        punto = h.pose.position;
                        origen = OrigenPunto.Profundidad;
                        return true;
                    }
                }
            }

            Ray rayo = camara.ScreenPointToRay(pantalla);

            // 2) Último piso conocido, prolongado como plano infinito.
            if (hayPisoConocido &&
                ARPiso.InterseccionPlanoHorizontal(rayo.origin, rayo.direction, yPisoConocido, out punto))
            {
                origen = OrigenPunto.PisoExtendido;
                return true;
            }

            // 3) Punto característico a la altura del piso.
            hitsPiso.Clear();
            if (raycastMgr.Raycast(pantalla, hitsPiso, TrackableType.FeaturePoint))
            {
                foreach (var h in hitsPiso)
                {
                    if (!ARPiso.CercaDelPiso(h.pose.position.y, yCam, hayPisoConocido, yPisoConocido))
                        continue;
                    punto = h.pose.position;
                    origen = OrigenPunto.PuntoCaracteristico;
                    return true;
                }
            }

            // 4) Piso estimado.
            return PuntoPisoDesdeRayo(rayo, yCam, out punto, out origen);
        }

        /// <summary>
        /// Parte de la cadena que NO necesita ARCore (pasos 2 y 4): piso
        /// conocido extendido o, si no hay, piso estimado bajo el teléfono.
        /// Pública para los tests de EditMode.
        /// </summary>
        public bool PuntoPisoDesdeRayo(Ray rayo, float yCamara, out Vector3 punto, out OrigenPunto origen)
        {
            origen = OrigenPunto.Ninguno;
            if (hayPisoConocido &&
                ARPiso.InterseccionPlanoHorizontal(rayo.origin, rayo.direction, yPisoConocido, out punto))
            {
                origen = OrigenPunto.PisoExtendido;
                return true;
            }
            float y = ARPiso.AlturaPisoEstimada(yCamara, hayPisoConocido, yPisoConocido);
            if (ARPiso.InterseccionPlanoHorizontal(rayo.origin, rayo.direction, y, out punto))
            {
                origen = hayPisoConocido ? OrigenPunto.PisoExtendido : OrigenPunto.PisoEstimado;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Punto de la cara inferior de la viga bajo <paramref name="pantalla"/>
        /// (Corrección 7, 4b): profundidad → plano arriba del teléfono → punto
        /// de ARCore arriba del teléfono → altura estimada del modelo.
        /// </summary>
        private bool ResolverPuntoTecho(Vector2 pantalla, out Vector3 punto, out OrigenTecho origen)
        {
            punto = Vector3.zero;
            origen = OrigenTecho.Ninguno;
            if (camara == null || raycastMgr == null) return false;
            if (ARSession.state != ARSessionState.SessionTracking) return false;
            float yCam = camara.transform.position.y;

            if (ProfundidadPedida && RaycastSobreCamara(pantalla, TrackableType.Depth, yCam, out punto))
            {
                origen = OrigenTecho.Profundidad;
                return true;
            }
            if (RaycastSobreCamara(pantalla, TrackableType.PlaneWithinPolygon, yCam, out punto))
            {
                origen = OrigenTecho.Plano;
                return true;
            }
            if (RaycastSobreCamara(pantalla, TrackableType.FeaturePoint, yCam, out punto))
            {
                origen = OrigenTecho.PuntoCaracteristico;
                return true;
            }
            return PuntoTechoDesdeRayo(camara.ScreenPointToRay(pantalla), yCam, out punto, out origen);
        }

        /// <summary>Primer impacto del tipo dado que esté sobre el teléfono (descarta piso y mesas).</summary>
        private bool RaycastSobreCamara(Vector2 pantalla, TrackableType tipo, float yCam, out Vector3 punto)
        {
            punto = Vector3.zero;
            hitsPiso.Clear();
            if (!raycastMgr.Raycast(pantalla, hitsPiso, tipo)) return false;
            foreach (var h in hitsPiso)
            {
                if (!ARTecho.EsTecho(h.pose.position.y, yCam)) continue;
                punto = h.pose.position;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Respaldo sin ARCore: el rayo cortado con el plano horizontal de la
        /// cara inferior de la viga elegida, a su altura del MODELO sobre el piso
        /// (el conocido o, si no hay, 1,40 m bajo el teléfono). Público para los
        /// tests de EditMode.
        /// </summary>
        public bool PuntoTechoDesdeRayo(Ray rayo, float yCamara, out Vector3 punto, out OrigenTecho origen)
        {
            origen = OrigenTecho.Ninguno;
            punto = Vector3.zero;
            var g = Seleccionado();
            if (g == null || !g.esViga) return false;
            float yPiso = ARPiso.AlturaPisoEstimada(yCamara, hayPisoConocido, yPisoConocido);
            float yInf = yPiso + ARTecho.AlturaInferiorViga(g);
            if (!ARTecho.EsTecho(yInf, yCamara)) return false;
            if (!ARPiso.InterseccionPlanoHorizontal(rayo.origin, rayo.direction, yInf, out punto))
                return false;
            origen = OrigenTecho.Estimado;
            return true;
        }

        private static bool EsModoTecho(ModoMarcado m)
        {
            return m == ModoMarcado.TechoI || m == ModoMarcado.TechoJ;
        }

        /// <summary>True mientras la app le pide profundidad a ARCore (sólo al marcar).</summary>
        public bool ProfundidadPedida
        {
            get { return oclusion != null && oclusion.requestedEnvironmentDepthMode != EnvironmentDepthMode.Disabled; }
        }

        /// <summary>
        /// Enciende/apaga la profundidad de ARCore. Sólo se usa mientras se marca:
        /// gasta cámara y batería, y fuera del marcado no hace falta.
        /// </summary>
        private void PedirProfundidad(bool si)
        {
            if (oclusion == null) return;
            oclusion.requestedEnvironmentDepthMode = si ? EnvironmentDepthMode.Medium
                                                        : EnvironmentDepthMode.Disabled;
        }

        /// <summary>Recuerda la altura del piso detectado (la usa el respaldo 2).</summary>
        public void RegistrarAlturaPiso(float y)
        {
            hayPisoConocido = true;
            yPisoConocido = y;
        }

        /// <summary>True si ARCore ya detectó un piso bajo el teléfono en esta sesión.</summary>
        public bool HayPisoConocido
        {
            get { return hayPisoConocido; }
        }

        /// <summary>Altura del último piso detectado (sólo vale si <see cref="HayPisoConocido"/>).</summary>
        public float AlturaPisoConocida
        {
            get { return yPisoConocido; }
        }

        private static bool SobreUI(int fingerId)
        {
            return EventSystem.current != null &&
                   EventSystem.current.IsPointerOverGameObject(fingerId);
        }

        private string InstruccionInicial()
        {
            var g = Seleccionado();
            if (g == null) return "Elige un elemento en la lista.";
            if (!EstaColocado(g.tag))
                return string.Format("{0} {1}: toca «Encuadrar: 4 esquinas» y marca las 4 esquinas " +
                                     "de la cara que ves{2}.", Mayuscula(g.tipo), g.tag,
                                     g.esViga ? string.Format(" (entre {0} y {1})",
                                                              ARApoyos.NombreApoyo(Apoyo(true)),
                                                              ARApoyos.NombreApoyo(Apoyo(false))) : "");
            return g.esViga
                ? string.Format("Viga {0}: toca «Marcar viga: apuntar a ella» y apunta a su cara " +
                                "inferior junto a {1} y luego junto a {2} (o, si se ven, marca el " +
                                "pie de esas columnas).", g.tag,
                                ARApoyos.NombreApoyo(Apoyo(true)), ARApoyos.NombreApoyo(Apoyo(false)))
                : string.Format("{0} {1}: toca «Marcar base» y marca su pie en el piso.",
                                Mayuscula(g.tipo), g.tag);
        }

        private string InstruccionMarcado()
        {
            var g = Seleccionado();
            switch (modo)
            {
                case ModoMarcado.Base:
                    return string.Format(
                        "Apunta el centro de la pantalla al pie de la {0} {1} (la cara " +
                        "que ves) y toca la pantalla.", g != null ? g.tipo : "columna",
                        g != null ? g.tag : 0);
                case ModoMarcado.ExtremoI:
                    return "Apunta la mira al PIE de " + ARApoyos.NombreApoyo(Apoyo(true)) +
                           " (extremo i, " + Etiqueta(g, true) + "), en la cara que ves, y toca la pantalla.";
                case ModoMarcado.ExtremoJ:
                    return "Ahora al PIE de " + ARApoyos.NombreApoyo(Apoyo(false)) +
                           " (extremo j, " + Etiqueta(g, false) + ") y toca la pantalla.";
                case ModoMarcado.Cuadro:
                    return InstruccionCuadro();
                case ModoMarcado.TechoI:
                    return "Apunta la mira a la cara INFERIOR de la viga, junto a " +
                           ARApoyos.NombreApoyo(Apoyo(true)) + " (extremo i, " + Etiqueta(g, true) +
                           "), y toca la pantalla.";
                case ModoMarcado.TechoJ:
                    return "Ahora a la cara inferior junto a " + ARApoyos.NombreApoyo(Apoyo(false)) +
                           " (extremo j, " + Etiqueta(g, false) + ") y toca la pantalla.";
                default:
                    return InstruccionInicial();
            }
        }

        /// <summary>Elemento del contrato que está seleccionado (o null).</summary>
        private ARElemento ElementoSeleccionado()
        {
            if (datos == null || datos.elementos == null || tagSeleccionado < 0) return null;
            ARElemento el;
            return datos.elementos.TryGetValue(tagSeleccionado.ToString(), out el) ? el : null;
        }

        /// <summary>Columna del contrato bajo el extremo i (o j) de la viga elegida, o null.</summary>
        private ARElemento Apoyo(bool extremoI)
        {
            return ARApoyos.ColumnaBajoExtremo(datos, ElementoSeleccionado(), extremoI);
        }

        /// <summary>
        /// Centro de la columna de apoyo a partir del punto tocado al pie de su
        /// cara visible (media sección hacia adentro, en la dirección de la cámara).
        /// </summary>
        private Vector3 CentroApoyo(Vector3 puntoCara, bool extremoI)
        {
            return ARColocacion.BaseDesdeCara(puntoCara, FrenteCamara(),
                                              ARApoyos.MitadApoyo(Apoyo(extremoI)));
        }

        private static string Etiqueta(ARGeometriaElemento g, bool i)
        {
            if (g == null) return i ? "i" : "j";
            string e = i ? g.etiquetaI : g.etiquetaJ;
            return string.IsNullOrEmpty(e) ? (i ? "i" : "j") : e;
        }

        private static string Mayuscula(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Elemento";
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        private void ActualizarBotonesMarcado()
        {
            if (ui == null) return;
            var g = Seleccionado();
            ui.MostrarMarcado(g != null, g != null && g.esViga, modo != ModoMarcado.Ninguno);
            // La mira fija del centro de la pantalla se ve SIEMPRE mientras se marca,
            // aunque el anillo del piso todavía no aparezca.
            ui.MostrarMira(modo != ModoMarcado.Ninguno);
        }

        // ---------------------------- retícula ---------------------------

        /// <summary>Radio del anillo que marca el punto del piso (m).</summary>
        public const float RadioReticula = 0.25f;

        private void MostrarReticula()
        {
            if (reticula == null) reticula = CrearReticula();
            reticulaValida = false;
            PedirProfundidad(true);      // la profundidad, sólo mientras se marca
            // Se enciende cuando el raycast encuentra piso (ActualizarReticula).
            if (reticula != null) reticula.SetActive(false);
        }

        private void OcultarReticula()
        {
            reticulaValida = false;
            PedirProfundidad(false);
            if (reticula != null) reticula.SetActive(false);
        }

        private GameObject CrearReticula()
        {
            var go = new GameObject("Reticula");
            go.transform.SetParent(transform, false);
            if (matLinea == null) return go;

            // Anillo horizontal (plano XZ) más una cruz pequeña al centro.
            const int n = 32;
            var anillo = new GameObject("Anillo");
            anillo.transform.SetParent(go.transform, false);
            var lr = anillo.AddComponent<LineRenderer>();
            lr.sharedMaterial = matLinea;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.startWidth = 0.03f;
            lr.endWidth = 0.03f;
            lr.startColor = new Color(1f, 0.85f, 0.10f, 1f);
            lr.endColor = lr.startColor;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.positionCount = n;
            for (int k = 0; k < n; k++)
            {
                float a = 2f * Mathf.PI * k / n;
                lr.SetPosition(k, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * RadioReticula);
            }

            foreach (Vector3 d in new[] { Vector3.right, Vector3.forward })
            {
                var cruz = new GameObject("Cruz");
                cruz.transform.SetParent(go.transform, false);
                var lc = cruz.AddComponent<LineRenderer>();
                lc.sharedMaterial = matLinea;
                lc.useWorldSpace = false;
                lc.positionCount = 2;
                lc.startWidth = 0.02f;
                lc.endWidth = 0.02f;
                lc.startColor = lr.startColor;
                lc.endColor = lr.startColor;
                lc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lc.SetPositions(new[] { -d * 0.10f, d * 0.10f });
            }
            go.SetActive(false);
            return go;
        }

        /// <summary>Cada frame, mientras se marca: la retícula sigue al centro de la pantalla.</summary>
        private void ActualizarReticula()
        {
            if (reticula == null) reticula = CrearReticula();
            Vector3 p;
            OrigenPunto origen;
            Vector2 centro = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (modo == ModoMarcado.Cuadro)
            {
                OrigenCuadro oc;
                if (ResolverPuntoCuadro(centro, out p, out oc))
                {
                    reticulaValida = true;
                    puntoReticula = p;
                    origenReticulaCuadro = oc;
                    // Cambio 01: en el encuadre NO hay anillo. El punto es exactamente
                    // el centro de la mira (sin tamaño); la guía muestra el recuadro.
                    if (reticula != null && reticula.activeSelf) reticula.SetActive(false);
                    ActualizarGuia(p);
                    mensaje = InstruccionMarcado() + "  [" + ARCuadro.Describir(oc) + "]";
                }
                else
                {
                    reticulaValida = false;
                    origenReticulaCuadro = OrigenCuadro.Ninguno;
                    if (reticula != null && reticula.activeSelf) reticula.SetActive(false);
                    ActualizarGuia(null);
                    mensaje = InstruccionMarcado();
                }
                RefrescarEstado();
                return;
            }
            if (EsModoTecho(modo))
            {
                OrigenTecho ot;
                if (ResolverPuntoTecho(centro, out p, out ot))
                {
                    reticulaValida = true;
                    puntoReticula = p;
                    origenReticulaTecho = ot;
                    if (reticula != null)
                    {
                        // 5 mm BAJO la cara inferior, para que se vea desde abajo.
                        reticula.transform.position = p - Vector3.up * 0.005f;
                        reticula.transform.rotation = Quaternion.identity;
                        EscalarReticula(p);
                        if (!reticula.activeSelf) reticula.SetActive(true);
                        ColorearReticula(ARTecho.EsSeguro(ot));
                    }
                    mensaje = InstruccionMarcado() + "  [" + ARTecho.Describir(ot) + "]";
                }
                else
                {
                    reticulaValida = false;
                    origenReticulaTecho = OrigenTecho.Ninguno;
                    if (reticula != null && reticula.activeSelf) reticula.SetActive(false);
                    mensaje = "Apunta la mira (centro de la pantalla) a la cara INFERIOR de la viga.";
                }
                RefrescarEstado();
                return;
            }
            if (ResolverPuntoPiso(centro, out p, out origen))
            {
                reticulaValida = true;
                puntoReticula = p;
                origenReticula = origen;
                if (reticula != null)
                {
                    // 5 mm sobre el piso para que no parpadee contra el plano.
                    reticula.transform.position = p + Vector3.up * 0.005f;
                    reticula.transform.rotation = Quaternion.identity;
                    EscalarReticula(p);
                    if (!reticula.activeSelf) reticula.SetActive(true);
                    ColorearReticula(origen);
                }
                mensaje = InstruccionMarcado() + "  [" + ARPiso.Describir(origen) + "]";
            }
            else
            {
                reticulaValida = false;
                origenReticula = OrigenPunto.Ninguno;
                if (reticula != null && reticula.activeSelf) reticula.SetActive(false);
                mensaje = "Inclina el teléfono hacia el PISO (la mira del centro debe apuntar al piso).";
            }
            RefrescarEstado();
        }

        /// <summary>Amarillo: piso detectado. Naranjo: piso estimado o punto suelto (revisar).</summary>
        private void ColorearReticula(OrigenPunto origen)
        {
            ColorearReticula(origen == OrigenPunto.PisoDetectado || origen == OrigenPunto.PisoExtendido ||
                             origen == OrigenPunto.Profundidad);
        }

        private void ColorearReticula(bool seguro)
        {
            Color c = seguro ? new Color(1f, 0.85f, 0.10f, 1f) : new Color(1f, 0.45f, 0.05f, 1f);
            foreach (var lr in reticula.GetComponentsInChildren<LineRenderer>(true))
            {
                lr.startColor = c;
                lr.endColor = c;
            }
        }
    }

    /// <summary>Orienta el texto 3D hacia la cámara.</summary>
    public class ARBillboard : MonoBehaviour
    {
        private Camera cam;

        /// <summary>
        /// Cambio 01: una vez colocado el diagrama, el rótulo queda FIJO (no gira ni
        /// cambia de tamaño con la cámara): el diagrama se ve igual al volver a él.
        /// </summary>
        public bool congelado;

        private void LateUpdate()
        {
            if (congelado) return;
            if (cam == null) cam = Camera.main;
            Orientar(cam);
        }

        /// <summary>Orienta el rótulo hacia <paramref name="camara"/> (derecho, vertical) y lo deja fijo.</summary>
        public void Congelar(Camera camara)
        {
            if (camara != null)
            {
                Vector3 dir = transform.position - camara.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 1e-8f)
                    transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
            transform.localScale = Vector3.one;
            congelado = true;
        }

        /// Un TextMesh se lee al derecho cuando su +Z apunta en dirección CONTRARIA a la cámara.
        public void Orientar(Camera camara)
        {
            if (camara == null) return;
            Vector3 dir = transform.position - camara.transform.position;
            if (dir.sqrMagnitude < 1e-8f) return;
            transform.rotation = Quaternion.LookRotation(dir, camara.transform.up);
        }
    }
}
