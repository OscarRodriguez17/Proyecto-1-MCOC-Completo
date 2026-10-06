using System.IO;
using MCOC.AR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  Botones de marcado en la interfaz (Corrección 5).
    //
    //  · Viga elegida      → se ve «Marcar extremos i y j», no «Marcar base».
    //  · Columna elegida   → se ve «Marcar base», no el de la viga.
    //  · Durante el marcado → se ve «Cancelar marcado» y se ocultan los otros.
    //  · Los botones están CONECTADOS a la app (onClick → modo de marcado).
    //  · «Colocar en el piso» y «Colocar aquí» quedan al final, como respaldo.
    // =====================================================================

    public class ARMarcadoUITests
    {
        private static ARRaiz _raiz;

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

        private static ARInspeccionApp Montar()
        {
            var go = new GameObject("App");
            var app = go.AddComponent<ARInspeccionApp>();
            app.Preparar();
            app.AplicarContrato(Raiz());
            return app;
        }

        private static void Limpiar(ARInspeccionApp app)
        {
            if (app.Interfaz != null && app.Interfaz.root != null)
                Object.DestroyImmediate(app.Interfaz.root);
            Object.DestroyImmediate(app.gameObject);
        }

        private static bool Visible(Button b)
        {
            return b != null && b.gameObject.activeSelf;
        }

        // Corrección 8 (5a): el encuadre de 4 esquinas reemplaza en la interfaz a
        // «Marcar base» y «Marcar viga: …», que quedan ocultos (pero conectados).

        [Test]
        public void Viga_MuestraEncuadrar_YNoLosMarcadosAntiguos()
        {
            var app = Montar();
            var ui = app.Interfaz;
            app.ElegirElemento(134);
            Assert.IsTrue(Visible(ui.btnEncuadrar));
            Assert.IsFalse(Visible(ui.btnMarcarViga));
            Assert.IsFalse(Visible(ui.btnMarcarTecho));
            Assert.IsFalse(Visible(ui.btnMarcarBase));
            Assert.IsFalse(Visible(ui.btnCancelarMarcado));
            Limpiar(app);
        }

        [Test]
        public void Columna_MuestraEncuadrar_YNoLosMarcadosAntiguos()
        {
            var app = Montar();
            var ui = app.Interfaz;
            app.ElegirElemento(14);
            Assert.IsTrue(Visible(ui.btnEncuadrar));
            Assert.IsFalse(Visible(ui.btnMarcarBase));
            Assert.IsFalse(Visible(ui.btnMarcarViga));
            Assert.IsFalse(Visible(ui.btnCancelarMarcado));
            Limpiar(app);
        }

        [Test]
        public void DuranteElMarcado_SoloSeVeCancelar()
        {
            var app = Montar();
            var ui = app.Interfaz;
            app.ElegirElemento(134);
            ui.btnEncuadrar.onClick.Invoke();           // el botón está conectado
            Assert.AreEqual(ModoMarcado.Cuadro, app.Modo);
            Assert.IsTrue(Visible(ui.btnCancelarMarcado));
            Assert.IsFalse(Visible(ui.btnEncuadrar));
            Assert.IsFalse(Visible(ui.btnMarcarViga));

            ui.btnCancelarMarcado.onClick.Invoke();
            Assert.AreEqual(ModoMarcado.Ninguno, app.Modo);
            Assert.IsTrue(Visible(ui.btnEncuadrar));
            Assert.IsFalse(Visible(ui.btnCancelarMarcado));
            Limpiar(app);
        }

        [Test]
        public void BotonMarcarBase_EstaConectado()
        {
            var app = Montar();
            app.ElegirElemento(26);
            app.Interfaz.btnMarcarBase.onClick.Invoke();
            Assert.AreEqual(ModoMarcado.Base, app.Modo);
            Limpiar(app);
        }

        [Test]
        public void ColocarSinMarcar_QuedaAlFinalDeLaLista()
        {
            var app = Montar();
            var ui = app.Interfaz;
            Transform lista = ui.btnColocarAqui.transform.parent;
            int n = lista.childCount;
            Assert.AreEqual(n - 1, ui.btnColocarAqui.transform.GetSiblingIndex(),
                            "«Colocar aquí» es el último botón.");
            Assert.AreEqual(n - 2, ui.btnColocar.transform.GetSiblingIndex(),
                            "«Colocar en el piso» es el penúltimo.");
            Assert.Less(ui.btnMarcarBase.transform.GetSiblingIndex(),
                        ui.btnPisoMas.transform.GetSiblingIndex(),
                        "Los botones de marcado van antes que los de ajuste.");
            Limpiar(app);
        }
    }
}
