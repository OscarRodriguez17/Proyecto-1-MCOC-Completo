using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MCOC.AR
{
    // =====================================================================
    //  Interfaz AR (Semana 06)
    //  Se construye por código para que la escena .unity sea mínima: un
    //  único GameObject con ARInspeccionApp. Así no hay referencias de
    //  script que mantener a mano ni objetos UI huérfanos.
    //
    //  La lista de elementos NO se rellena al construir: la rellena
    //  `LlenarElementos(ARRaiz)` cuando el contrato ya está leído, que es lo
    //  que hace `ARInspeccionApp.AplicarContrato` en el callback OK.
    // =====================================================================

    public class ARInterfazOpciones
    {
        public string cabecera = "";
        public List<KeyValuePair<int, string>> elementos =
            new List<KeyValuePair<int, string>>();
        public List<string> conToggle = new List<string>();
        public Dictionary<string, bool> toggleInicial = new Dictionary<string, bool>();
        public bool conAmplitud = true;
    }

    public class ARInterfaz
    {
        public Canvas canvas;
        public GameObject root;
        public Text cabecera;
        public Text estado;
        public Text frames;
        public Text diagnostico;
        public GameObject panelDiagnostico;
        public Button btnDiag;
        public GameObject panelElementos;
        public RectTransform listaElementos;
        public Text info;
        public List<Button> botonesElemento = new List<Button>();
        public List<int> tagsElemento = new List<int>();
        public Dictionary<string, Button> toggles = new Dictionary<string, Button>();
        public Dictionary<string, Text> toggleTexto = new Dictionary<string, Text>();
        public Button btnColocar;
        public Button btnColocarAqui;
        public Button btnPisoMas;
        public Button btnPisoMenos;
        public Button btnReiniciar;
        public Button btnRestablecer;
        public Button btnMas;
        public Button btnMenos;

        public event Action<int> ElementoElegido;
        public event Action<string> ToggleCambiado;
        public event Action Colocar;
        public event Action ColocarAqui;
        public event Action PisoMas;
        public event Action PisoMenos;
        public event Action Reiniciar;
        public event Action Restablecer;
        public event Action AmplitudMas;
        public event Action AmplitudMenos;

        private Font fuente;

        public static Font CargarFuente()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch (Exception) { f = null; }
            if (f == null)
            {
                try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch (Exception) { f = null; }
            }
            if (f == null)
            {
                // Puede no haber ninguna fuente cargada (Editor en -nographics).
                // Antes esto reventaba con IndexOutOfRange al indexar [0].
                var todas = Resources.FindObjectsOfTypeAll<Font>();
                if (todas != null && todas.Length > 0) f = todas[0];
            }
            return f;
        }

        // -----------------------------------------------------------------

        public static ARInterfaz Construir(ARInterfazOpciones o)
        {
            if (o == null) o = new ARInterfazOpciones();
            var ui = new ARInterfaz();
            ui.fuente = CargarFuente();
            ui.canvas = ui.CrearCanvas();
            ui.root = ui.canvas.gameObject;
            ui.cabecera = ui.CrearTexto(ui.canvas.transform, "Cabecera", 26, TextAnchor.UpperCenter,
                                        new Vector2(0f, 1f), new Vector2(1f, 1f),
                                        new Vector2(0f, -14f), new Vector2(0f, 92f));
            ui.cabecera.text = o.cabecera;

            // Contador de frames: SIEMPRE visible, sin panel detrás. Es la
            // única lectura que dice si la app está dibujando de verdad.
            ui.frames = ui.CrearTexto(ui.canvas.transform, "Frames", 18, TextAnchor.UpperRight,
                                       new Vector2(0.70f, 1f), new Vector2(1f, 1f),
                                       new Vector2(0f, -98f), new Vector2(-10f, -130f));
            ui.frames.text = ARFramesCamara.Texto();

            ui.estado = ui.CrearTexto(ui.canvas.transform, "Estado", 20, TextAnchor.UpperLeft,
                                       new Vector2(0f, 1f), new Vector2(1f, 1f),
                                       new Vector2(14f, -100f), new Vector2(0f, 150f));

            ui.info = ui.CrearTexto(ui.canvas.transform, "Info", 18, TextAnchor.LowerLeft,
                                    new Vector2(0f, 0f), new Vector2(0.62f, 0f),
                                    new Vector2(14f, 14f), new Vector2(0f, 170f));

            // Panel diagnóstico abajo izquierda, ancho <= 40%
            var diagPanel = ui.CrearPanel(ui.canvas.transform, "PanelDiagnostico",
                                         new Vector2(0f, 0f), new Vector2(0.40f, 0f),
                                         new Vector2(10f, 10f), new Vector2(0f, 120f));
            var diagImg = diagPanel.GetComponent<Image>();
            diagImg.raycastTarget = false;
            ui.panelDiagnostico = diagPanel.gameObject;
            var diagVert = ui.CrearVertical(diagPanel);
            ui.diagnostico = ui.CrearTexto((Transform)diagVert, "Diagnostico", 16, TextAnchor.LowerLeft,
                                          new Vector2(0f, 0f), new Vector2(1f, 1f),
                                          new Vector2(4f, 4f), new Vector2(-4f, -4f));
            ui.diagnostico.raycastTarget = false;
            diagPanel.gameObject.SetActive(false);

            // --- columna izquierda: elementos ---
            var panelElem = ui.CrearPanel(ui.canvas.transform, "Elementos",
                                      new Vector2(0f, 1f), new Vector2(0.30f, 1f),
                                      new Vector2(10f, -160f), new Vector2(0f, -10f));
            ui.panelElementos = panelElem.gameObject;
            ui.listaElementos = ui.CrearVertical(panelElem);
            float y = 0f;
            foreach (var kv in o.elementos)
            {
                var b = ui.CrearBoton(ui.listaElementos, kv.Value, y);
                int tag = kv.Key;
                b.onClick.AddListener(() => ui.ElementoElegido?.Invoke(tag));
                ui.botonesElemento.Add(b);
                ui.tagsElemento.Add(tag);
                y += 62f;
            }

            // --- columna derecha: toggles y acciones ---
            var der = ui.CrearPanel(ui.canvas.transform, "Controles",
                                     new Vector2(0.70f, 1f), new Vector2(1f, 1f),
                                     new Vector2(0f, -140f), new Vector2(-10f, -10f));
            var listaD = ui.CrearVertical(der, 4f);
            float yd = 0f;
            foreach (var t in o.conToggle)
            {
                bool inicial;
                string nombre = t;
                if (!o.toggleInicial.TryGetValue(t, out inicial)) inicial = true;
                var b = ui.CrearBoton(listaD, t + ": " + (inicial ? "si" : "no"), yd, 40f);
                var txt = b.GetComponentInChildren<Text>();
                ui.toggleTexto[nombre] = txt;
                b.onClick.AddListener(() => ui.ToggleAlternado(nombre));
                ui.toggles[nombre] = b;
                yd += 44f;
            }

            // --- Plan A: raycast contra el piso detectado ---
            ui.btnColocar = ui.CrearBoton(listaD, "Colocar en el piso", yd, 40f);
            ui.btnColocar.onClick.AddListener(() => ui.Colocar?.Invoke());
            yd += 44f;

            // --- Plan B: piso estimado 1,40 m bajo el teléfono ---
            ui.btnColocarAqui = ui.CrearBoton(listaD, "Colocar aquí", yd, 40f);
            ui.btnColocarAqui.onClick.AddListener(() => ui.ColocarAqui?.Invoke());
            yd += 44f;

            ui.btnPisoMas = ui.CrearBoton(listaD, "Piso +5 cm", yd, 40f);
            ui.btnPisoMas.onClick.AddListener(() => ui.PisoMas?.Invoke());
            yd += 44f;

            ui.btnPisoMenos = ui.CrearBoton(listaD, "Piso −5 cm", yd, 40f);
            ui.btnPisoMenos.onClick.AddListener(() => ui.PisoMenos?.Invoke());
            yd += 44f;

            ui.btnReiniciar = ui.CrearBoton(listaD, "Quitar ancla", yd, 40f);
            ui.btnReiniciar.onClick.AddListener(() => ui.Reiniciar?.Invoke());
            yd += 44f;

            ui.btnRestablecer = ui.CrearBoton(listaD, "Restablecer", yd, 40f);
            ui.btnRestablecer.onClick.AddListener(() => ui.Restablecer?.Invoke());
            yd += 44f;

            if (o.conAmplitud)
            {
                // Amplitud del DIAGRAMA, no el tamaño del elemento: el
                // elemento se queda 1:1 (1 unidad = 1 m) para que las
                // longitudes y las alturas sobre el piso sigan siendo ciertas.
                ui.btnMenos = ui.CrearBoton(listaD, "Amplitud -", yd, 40f);
                ui.btnMenos.onClick.AddListener(() => ui.AmplitudMenos?.Invoke());
                yd += 44f;

                ui.btnMas = ui.CrearBoton(listaD, "Amplitud +", yd, 40f);
                ui.btnMas.onClick.AddListener(() => ui.AmplitudMas?.Invoke());
                yd += 44f;
            }

            ui.btnDiag = ui.CrearBoton(listaD, "Diag", yd, 40f);
            ui.btnDiag.onClick.AddListener(() => { if (ui.panelDiagnostico != null) ui.panelDiagnostico.SetActive(!ui.panelDiagnostico.activeSelf); });
            yd += 44f;

            return ui;
        }

        /// <summary>
        /// Rellena la lista de elementos con los tags del contrato, ordenados
        /// de menor a mayor, y devuelve cuántos botones ha creado.
        ///
        /// Se llama DESDE `ARInspeccionApp.AplicarContrato`, que es el callback
        /// OK de `CargarContrato`: hasta que el contrato no está leído no hay
        /// nada que listar. Los botones reconstruyen la lista desde cero, así
        /// que se puede volver a llamar con otro contrato sin duplicar nada.
        /// </summary>
        public int LlenarElementos(ARRaiz datos)
        {
            LimpiarElementos();
            if (datos == null || datos.elementos == null) return 0;

            foreach (var el in datos.EnOrden())
            {
                string etiqueta = string.Format("Tag {0} · {1}", el.tag, el.tipo);
                if (!string.IsNullOrEmpty(el.seccion)) etiqueta += "\n" + el.seccion;
                var b = CrearBoton(listaElementos, etiqueta, 0f);
                int tag = el.tag;
                b.onClick.AddListener(() => ElementoElegido?.Invoke(tag));
                botonesElemento.Add(b);
                tagsElemento.Add(tag);
            }
            return botonesElemento.Count;
        }

        /// <summary>Borra los botones de elemento anteriores.</summary>
        public void LimpiarElementos()
        {
            for (int i = botonesElemento.Count - 1; i >= 0; i--)
            {
                if (botonesElemento[i] == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(botonesElemento[i].gameObject);
                else UnityEngine.Object.DestroyImmediate(botonesElemento[i].gameObject);
            }
            botonesElemento.Clear();
            tagsElemento.Clear();
        }

        /// <summary>
        /// Tag del primer elemento de la lista, o −1 si la lista está vacía.
        /// Es lo que la app selecciona tras leer el contrato: el primer tag
        /// en orden ascendente (14, antes que 26 y 134).
        /// </summary>
        public int PrimerTag()
        {
            return (tagsElemento != null && tagsElemento.Count > 0) ? tagsElemento[0] : -1;
        }

        /// <summary>True si <paramref name="tag"/> tiene botón en la lista.</summary>
        public bool TieneElemento(int tag)
        {
            return tagsElemento != null && tagsElemento.Contains(tag);
        }

        private void ToggleAlternado(string nombre)
        {
            Text t;
            if (!toggleTexto.TryGetValue(nombre, out t)) return;
            bool actual = t.text.EndsWith(": si");
            t.text = nombre + ": " + (actual ? "no" : "si");
            ToggleCambiado?.Invoke(nombre);
        }

        // -----------------------------------------------------------------
        //  Fábricas de UI
        // -----------------------------------------------------------------

        private Canvas CrearCanvas()
        {
            var go = new GameObject("CanvasAR",
                                    typeof(Canvas), typeof(CanvasScaler),
                                    typeof(GraphicRaycaster));
            var cv = go.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            var cs = go.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 0.5f;

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem",
                                        typeof(EventSystem),
                                        typeof(StandaloneInputModule));
                es.transform.SetParent(go.transform, false);
            }
            return cv;
        }

        private Text CrearTexto(Transform padre, string nombre, int tam, TextAnchor anchor,
                                Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(padre, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            var t = go.GetComponent<Text>();
            t.font = fuente;
            t.fontSize = tam;
            t.alignment = anchor;
            t.color = new Color(1f, 1f, 1f, 0.95f);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        private RectTransform CrearPanel(Transform padre, string nombre,
                                         Vector2 aMin, Vector2 aMax,
                                         Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(padre, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.35f);
            // El panel es sólo el fondo: NO intercepta toques. Si se dejara
            // raycastTarget = true, su rectángulo contendría a todos los
            // botones hijos y el test de solapamiento de UI fallaría; además
            // los toques en la zona vacía deben pasar al mundo 3D para poder
            // colocar el ancla tocando la pantalla.
            img.raycastTarget = false;
            return rt;
        }

        private RectTransform CrearVertical(RectTransform padre, float espaciado = 8f)
        {
            var go = new GameObject("Vertical", typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(8f, 8f);
            rt.offsetMax = new Vector2(-8f, -8f);
            var l = go.AddComponent<VerticalLayoutGroup>();
            l.spacing = espaciado;
            l.childAlignment = TextAnchor.UpperCenter;
            l.childControlHeight = false;
            l.childControlWidth = true;
            l.childForceExpandHeight = false;
            l.childForceExpandWidth = true;
            var f = go.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        private Button CrearBoton(RectTransform padre, string texto, float altura,
                                  float alto = 56f)
        {
            var go = new GameObject("B_" + texto.Replace('\n', ' '), typeof(RectTransform),
                                    typeof(Image), typeof(Button));
            go.transform.SetParent(padre, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, alto);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.10f, 0.14f, 0.22f, 0.80f);

            var lab = new GameObject("Texto", typeof(RectTransform), typeof(Text));
            lab.transform.SetParent(go.transform, false);
            var lrt = lab.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var t = lab.GetComponent<Text>();
            t.font = fuente;
            t.fontSize = 22;
            // `Text.alignment` es de tipo TextAnchor, no TextAlignment (ese es el de
            // TextMesh). Con TextAlignment no compila.
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = texto;
            t.raycastTarget = false;

            var b = go.GetComponent<Button>();
            b.targetGraphic = img;
            return b;
        }

        /// <summary>Alias de <see cref="Construir"/>, que es como se llama desde los tests.</summary>
        public static ARInterfaz Crear(ARInterfazOpciones o)
        {
            return Construir(o);
        }
    }
}
