using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using MCOC.AR;
using System.Collections.Generic;
using System.IO;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  La LISTA de elementos (Corrección 3, B).
    //
    //  El fallo era que la interfaz se construía al arrancar, sin contrato
    //  leído, así que la lista salía VACÍA para siempre. Comprobar que
    //  `LlenarElementos` existe no dice nada: lo que importa es que el
    //  camino real —el callback OK de `CargarContrato`— la llame y que al
    //  final haya un botón por elemento, con el primero ya seleccionado.
    // =====================================================================

    public class LlenarElementosTests
    {
        private static ARRaiz _raiz;

        /// <summary>Lee el MISMO contrato que lee la app en el teléfono.</summary>
        private static ARRaiz Raiz()
        {
            if (_raiz == null)
            {
                string ruta = Path.Combine(Application.streamingAssetsPath,
                                           ARCargador.Archivo);
                Assert.IsTrue(File.Exists(ruta), "No se encuentra " + ruta);
                _raiz = ARCargador.DesdeJson(File.ReadAllText(ruta));
            }
            return _raiz;
        }

        [Test]
        public void LlenarElementosCreaBotonesConTags()
        {
            var raiz = Raiz();
            var ui = ARInterfaz.Crear(new ARInterfazOpciones());
            int creados = ui.LlenarElementos(raiz);

            Assert.AreEqual(raiz.elementos.Count, creados);

            Button[] botones = ui.root.GetComponentsInChildren<Button>(true);
            Assert.GreaterOrEqual(botones.Length, 3);
            bool tiene134 = false, tiene14 = false, tiene26 = false;
            foreach (var b in botones)
            {
                var t = b.GetComponentInChildren<Text>();
                if (t != null)
                {
                    string s = t.text;
                    if (s.Contains("134")) tiene134 = true;
                    if (s.Contains("14")) tiene14 = true;
                    if (s.Contains("26")) tiene26 = true;
                }
            }
            Assert.IsTrue(tiene134);
            Assert.IsTrue(tiene14);
            Assert.IsTrue(tiene26);
            Object.DestroyImmediate(ui.root);
        }

        /// <summary>La lista va en orden ascendente de tag: 14, 26, 134.</summary>
        [Test]
        public void LaListaVaOrdenadaYElPrimeroEsElTagMenor()
        {
            var ui = ARInterfaz.Crear(new ARInterfazOpciones());
            ui.LlenarElementos(Raiz());

            List<int> tags = ui.tagsElemento;
            Assert.AreEqual(3, tags.Count);
            Assert.AreEqual(14, tags[0], "El primer elemento debe ser el tag 14.");
            Assert.AreEqual(26, tags[1]);
            Assert.AreEqual(134, tags[2]);
            Assert.AreEqual(14, ui.PrimerTag());

            for (int i = 1; i < tags.Count; i++)
                Assert.Greater(tags[i], tags[i - 1], "La lista debe ir de menor a mayor.");

            Object.DestroyImmediate(ui.root);
        }

        /// <summary>
        /// El botón de un elemento dispara el evento con SU tag, no con el
        /// primero ni con el del bucle.
        /// </summary>
        [Test]
        public void CadaBotonEmiteSuPropioTag()
        {
            var ui = ARInterfaz.Crear(new ARInterfazOpciones());
            ui.LlenarElementos(Raiz());

            var emitidos = new List<int>();
            ui.ElementoElegido += t => emitidos.Add(t);

            for (int i = 0; i < ui.botonesElemento.Count; i++)
                ui.botonesElemento[i].onClick.Invoke();

            CollectionAssert.AreEqual(new[] { 14, 26, 134 }, emitidos);

            Object.DestroyImmediate(ui.root);
        }

        /// <summary>
        /// ReLLENAR la lista con otro contrato no duplica botones: la app
        /// puede releer el contrato y la lista tiene que seguir cuadrando.
        /// </summary>
        [Test]
        public void RellenarDosVecesNoDuplicaBotones()
        {
            var raiz = Raiz();
            var ui = ARInterfaz.Crear(new ARInterfazOpciones());

            ui.LlenarElementos(raiz);
            int trasLaPrimera = ui.botonesElemento.Count;
            ui.LlenarElementos(raiz);
            int trasLaSegunda = ui.botonesElemento.Count;

            Assert.AreEqual(trasLaPrimera, trasLaSegunda,
                            "La segunda pasada debe reemplazar, no añadir.");
            Assert.AreEqual(raiz.elementos.Count, trasLaSegunda);

            Object.DestroyImmediate(ui.root);
        }

        // -----------------------------------------------------------------
        //  El camino real: el callback OK de la app
        // -----------------------------------------------------------------

        /// <summary>
        /// El test que faltaba: no llama a `LlenarElementos` directamente, sino
        /// a `AplicarContrato`, que es lo que ejecuta el callback OK de
        /// `CargarContrato` en el teléfono.
        /// </summary>
        [Test]
        public void ElCallbackOkLlenaLaListaYSeleccionaElTag14()
        {
            GameObject go = new GameObject("App");
            var app = go.AddComponent<ARInspeccionApp>();
            app.Preparar();

            // Antes del contrato la lista está vacía: ese era el fallo.
            Assert.AreEqual(0, app.TagsEnLista().Count);

            int creados = app.AplicarContrato(Raiz());

            Assert.AreEqual(Raiz().elementos.Count, creados,
                            "El callback OK debe dejar un botón por elemento.");
            CollectionAssert.AreEqual(new[] { 14, 26, 134 }, app.TagsEnLista());

            // Y el 14 queda visible, que es el primero en orden ascendente.
            Assert.AreEqual(14, app.TagSeleccionado);

            Limpiar(app, go);
        }

        /// <summary>La cabecera lleva el caso, que es lo que hay que leer en el móvil.</summary>
        [Test]
        public void ElCallbackOkPoneLaCabeceraConElCaso()
        {
            GameObject go = new GameObject("App");
            var app = go.AddComponent<ARInspeccionApp>();
            app.Preparar();

            Assert.IsTrue(app.CabeceraActual.Contains("cargando"),
                          "Antes de leer el contrato la cabecera va en cargando.");

            app.AplicarContrato(Raiz());

            Assert.IsTrue(app.CabeceraActual.StartsWith("MCOC · AR · caso "),
                          "La cabecera debe empezar por 'MCOC · AR · caso '. Era: "
                          + app.CabeceraActual);
            Assert.IsTrue(app.CabeceraActual.Contains("GQ"),
                          "El caso del contrato es GQ.");

            Limpiar(app, go);
        }

        /// <summary>Un contrato nulo es un error, no un contrato vacío.</summary>
        [Test]
        public void ElCallbackOkRechazaUnContratoNulo()
        {
            GameObject go = new GameObject("App");
            var app = go.AddComponent<ARInspeccionApp>();
            app.Preparar();

            Assert.Throws<System.ArgumentNullException>(() => app.AplicarContrato(null));

            Limpiar(app, go);
        }

        private static void Limpiar(ARInspeccionApp app, GameObject go)
        {
            var campo = typeof(ARInspeccionApp).GetField("ui",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ui = campo.GetValue(app) as ARInterfaz;
            if (ui != null && ui.root != null) Object.DestroyImmediate(ui.root);
            Object.DestroyImmediate(go);
        }
    }
}