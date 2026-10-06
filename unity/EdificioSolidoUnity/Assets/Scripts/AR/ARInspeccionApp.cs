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
    /// ExtremoI / ExtremoJ: el piso bajo cada extremo de una viga.
    /// </summary>
    public enum ModoMarcado
    {
        Ninguno,
        Base,
        ExtremoI,
        ExtremoJ
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

            var g = ARGeometriaBuilder.ConstruirElemento(el, Opciones());
            geoPorTag[tagSeleccionado] = g;
            ConstruirContenedor(g, go);
            AplicarVisibilidad();
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
            tm.characterSize = 0.018f;
            tm.anchor = TextAnchor.LowerLeft;
            tm.alignment = TextAlignment.Left;
            tm.color = m.color;
            var mr = t.GetComponent<MeshRenderer>();
            mr.sharedMaterial = fuente.material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.AddComponent<ARBillboard>();
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
        //   · Viga: «Marcar extremos i y j» → tocar el piso bajo el extremo i
        //     → tocar el piso bajo el extremo j. El ancla va al punto medio y el
        //     eje i→j del elemento queda sobre la línea marcada.
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

        /// <summary>Sale del modo de marcado sin tocar el ancla actual.</summary>
        public void CancelarMarcado()
        {
            modo = ModoMarcado.Ninguno;
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
                        ? "Toca «Marcar extremos i y j» antes de marcar en el piso."
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
            return g.esViga
                ? string.Format("Viga {0}: toca «Marcar extremos i y j» y marca el pie de " +
                                "{1} y luego el de {2}.", g.tag,
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
            // Se enciende cuando el raycast encuentra piso (ActualizarReticula).
            if (reticula != null) reticula.SetActive(false);
        }

        private void OcultarReticula()
        {
            reticulaValida = false;
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
            bool seguro = origen == OrigenPunto.PisoDetectado || origen == OrigenPunto.PisoExtendido;
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

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            Orientar(cam);
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
