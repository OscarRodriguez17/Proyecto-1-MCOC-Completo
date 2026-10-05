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
        // Corrección 5: colocar marcando el elemento real.
        public Button btnMarcarBase;
        public Button btnMarcarViga;
        public Button btnCancelarMarcado;
        /// <summary>Mira fija en el centro de la pantalla (Corrección 6): indica dónde se marca.</summary>
        public GameObject mira;
        // Corrección 6, parte 3d: panel 2D con los diagramas del elemento elegido.
        public Button btnPanel2D;
        public GameObject panel2D;
        public RawImage panel2DImagen;
        public Text panel2DTitulo;
        public Text[] panel2DLeyendas = new Text[3];

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
        public event Action MarcarBase;
        public event Action MarcarViga;
        public event Action CancelarMarcado;
        public event Action Panel2D;

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

            // --- Colocación marcando el elemento real (Corrección 5) ---
            // Es el modo principal: se muestra «Marcar base» para columnas y
            // muros, y «Marcar extremos i y j» para vigas (MostrarMarcado).
            ui.btnMarcarBase = ui.CrearBoton(listaD, "Marcar base", yd, 40f);
            ui.btnMarcarBase.onClick.AddListener(() => ui.MarcarBase?.Invoke());
            yd += 44f;

            ui.btnMarcarViga = ui.CrearBoton(listaD, "Marcar extremos i y j", yd, 40f);
            ui.btnMarcarViga.onClick.AddListener(() => ui.MarcarViga?.Invoke());
            yd += 44f;

            ui.btnCancelarMarcado = ui.CrearBoton(listaD, "Cancelar marcado", yd, 40f);
            ui.btnCancelarMarcado.onClick.AddListener(() => ui.CancelarMarcado?.Invoke());
            yd += 44f;

            ui.btnPisoMas = ui.CrearBoton(listaD, "Piso +5 cm", yd, 40f);
            ui.btnPisoMas.onClick.AddListener(() => ui.PisoMas?.Invoke());
            yd += 44f;

            ui.btnPisoMenos = ui.CrearBoton(listaD, "Piso \u22125 cm", yd, 40f);
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

            // Panel 2D: los diagramas en un recuadro fijo, legible siempre (respaldo del AR).
            ui.btnPanel2D = ui.CrearBoton(listaD, "Panel 2D", yd, 40f);
            ui.btnPanel2D.onClick.AddListener(() => ui.Panel2D?.Invoke());
            yd += 44f;

            ui.btnDiag = ui.CrearBoton(listaD, "Diag", yd, 40f);
            ui.btnDiag.onClick.AddListener(() => { if (ui.panelDiagnostico != null) ui.panelDiagnostico.SetActive(!ui.panelDiagnostico.activeSelf); });
            yd += 44f;

            // --- Respaldo: colocar sin marcar (al final de la lista) ---
            // Plan A: raycast contra el piso al centro de la pantalla.
            ui.btnColocar = ui.CrearBoton(listaD, "Colocar en el piso", yd, 40f);
            ui.btnColocar.onClick.AddListener(() => ui.Colocar?.Invoke());
            yd += 44f;

            // Plan B: piso estimado 1,40 m bajo el teléfono.
            ui.btnColocarAqui = ui.CrearBoton(listaD, "Colocar aquí", yd, 40f);
            ui.btnColocarAqui.onClick.AddListener(() => ui.ColocarAqui?.Invoke());
            yd += 44f;

            // Hasta que haya un elemento elegido no se puede marcar nada.
            ui.MostrarMarcado(false, false, false);

            // Mira fija en el centro de la pantalla: el punto que se marca es
            // SIEMPRE el que queda bajo ella. Oculta fuera del modo de marcado.
            ui.mira = ui.CrearMira(ui.canvas.transform);
            ui.MostrarMira(false);

            ui.CrearPanel2D(ui.canvas.transform);
            ui.MostrarPanel2D(false);

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

        /// <summary>
        /// Qué botones de marcado se ven (Corrección 5):
        ///  · «Marcar base» sólo con una columna o muro elegido;
        ///  · «Marcar extremos i y j» sólo con una viga elegida;
        ///  · «Cancelar marcado» sólo mientras se está marcando.
        /// </summary>
        public void MostrarMarcado(bool hayElemento, bool esViga, bool marcando)
        {
            if (btnMarcarBase != null)
                btnMarcarBase.gameObject.SetActive(hayElemento && !esViga && !marcando);
            if (btnMarcarViga != null)
                btnMarcarViga.gameObject.SetActive(hayElemento && esViga && !marcando);
            if (btnCancelarMarcado != null)
                btnCancelarMarcado.gameObject.SetActive(marcando);
        }

        /// <summary>Muestra u oculta el panel 2D de diagramas.</summary>
        public void MostrarPanel2D(bool visible)
        {
            if (panel2D != null) panel2D.SetActive(visible);
        }

        /// <summary>Pone en el panel 2D el título, las tres leyendas y la textura del gráfico.</summary>
        public void ActualizarPanel2D(string titulo, IList<string> leyendas, Texture2D grafico)
        {
            if (panel2DTitulo != null) panel2DTitulo.text = titulo ?? "";
            for (int k = 0; k < panel2DLeyendas.Length; k++)
            {
                if (panel2DLeyendas[k] == null) continue;
                panel2DLeyendas[k].text = leyendas != null && k < leyendas.Count ? leyendas[k] : "";
            }
            if (panel2DImagen != null) panel2DImagen.texture = grafico;
        }

        /// <summary>
        /// Recuadro a la izquierda-centro de la pantalla: título arriba, gráfico de
        /// tres filas (N, V, M) y una leyenda sobre cada fila. Nada de él intercepta
        /// toques (raycastTarget = false), así que no tapa botones ni el marcado.
        /// </summary>
        private void CrearPanel2D(Transform padre)
        {
            var go = new GameObject("Panel2D", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(padre, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.02f, 0.16f);
            rt.anchorMax = new Vector2(0.64f, 0.74f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            panel2D = go;
            panel2DImagen = go.GetComponent<RawImage>();
            panel2DImagen.raycastTarget = false;
            panel2DImagen.color = Color.white;

            panel2DTitulo = CrearTexto(go.transform, "Panel2DTitulo", 22, TextAnchor.LowerLeft,
                                       new Vector2(0f, 1f), new Vector2(1f, 1f),
                                       new Vector2(4f, 4f), new Vector2(0f, 40f));
            for (int k = 0; k < 3; k++)
            {
                // Cada fila ocupa un tercio; la leyenda va en su borde superior.
                float yTop = 1f - k / 3f;
                panel2DLeyendas[k] = CrearTexto(go.transform, "Panel2DLeyenda" + k, 18,
                                                TextAnchor.UpperLeft,
                                                new Vector2(0f, yTop), new Vector2(1f, yTop),
                                                new Vector2(30f, -30f), new Vector2(-10f, -4f));
            }
        }

        /// <summary>Muestra u oculta la mira fija del centro de la pantalla.</summary>
        public void MostrarMira(bool visible)
        {
            if (mira != null) mira.SetActive(visible);
        }

        /// <summary>
        /// Cruz amarilla con contorno oscuro, fija en el centro de la pantalla.
        /// No intercepta toques (raycastTarget = false en todas sus imágenes).
        /// </summary>
        private GameObject CrearMira(Transform padre)
        {
            var go = new GameObject("Mira", typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(90f, 90f);
            rt.anchoredPosition = Vector2.zero;

            // Contorno oscuro (más ancho) y luego la cruz amarilla encima.
            BarraMira(go.transform, new Vector2(94f, 12f), new Color(0f, 0f, 0f, 0.6f));
            BarraMira(go.transform, new Vector2(12f, 94f), new Color(0f, 0f, 0f, 0.6f));
            BarraMira(go.transform, new Vector2(90f, 6f), new Color(1f, 0.85f, 0.10f, 1f));
            BarraMira(go.transform, new Vector2(6f, 90f), new Color(1f, 0.85f, 0.10f, 1f));
            return go;
        }

        private static void BarraMira(Transform padre, Vector2 tam, Color c)
        {
            var b = new GameObject("BarraMira", typeof(RectTransform), typeof(Image));
            b.transform.SetParent(padre, false);
            var rt = b.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = tam;
            rt.anchoredPosition = Vector2.zero;
            var img = b.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
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
