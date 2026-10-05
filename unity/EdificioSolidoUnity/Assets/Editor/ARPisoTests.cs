using System.IO;
using MCOC.AR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  El punto del piso NO se pierde (Corrección 6, parte 3a).
    //
    //  En terreno el anillo amarillo desaparecía cuando ARCore no detectaba
    //  un plano en el centro de la pantalla. Ahora hay respaldos: el último
    //  piso conocido extendido y, si no hay, el piso estimado 1,40 m bajo el
    //  teléfono. Además hay una mira fija en el centro de la pantalla.
    // =====================================================================

    public class ARPisoTests
    {
        private const float TOL = 1e-3f;
        private static ARRaiz _raiz;

        private static ARRaiz Raiz()
        {
            if (_raiz == null)
            {
                string ruta = Path.Combine(Application.streamingAssetsPath, ARCargador.Archivo);
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

        // ------------------------------ matemática pura

        [Test]
        public void Interseccion_RayoA45Grados_CaeA1_4m()
        {
            Vector3 p;
            Assert.IsTrue(ARPiso.InterseccionPlanoHorizontal(
                new Vector3(0f, 1.4f, 0f), new Vector3(0f, -1f, 1f), 0f, out p));
            Assert.AreEqual(0f, p.x, TOL);
            Assert.AreEqual(0f, p.y, TOL);
            Assert.AreEqual(1.4f, p.z, TOL);
        }

        [Test]
        public void Interseccion_RayoHaciaArribaUHorizontal_NoHayPunto()
        {
            Vector3 p;
            Assert.IsFalse(ARPiso.InterseccionPlanoHorizontal(
                new Vector3(0f, 1.4f, 0f), new Vector3(0f, 0.3f, 1f), 0f, out p), "hacia el cielo");
            Assert.IsFalse(ARPiso.InterseccionPlanoHorizontal(
                new Vector3(0f, 1.4f, 0f), new Vector3(0f, 0f, 1f), 0f, out p), "horizontal");
        }

        [Test]
        public void Interseccion_DemasiadoLejos_SeDescarta()
        {
            Vector3 p;
            // Casi horizontal: el piso quedaría a ~140 m.
            Assert.IsFalse(ARPiso.InterseccionPlanoHorizontal(
                new Vector3(0f, 1.4f, 0f), new Vector3(0f, -0.01f, 1f), 0f, out p));
        }

        [Test]
        public void EsPiso_DescartaCielosYMesasAltas()
        {
            Assert.IsTrue(ARPiso.EsPiso(0f, 1.4f), "piso 1,4 m bajo el teléfono");
            Assert.IsFalse(ARPiso.EsPiso(3.9f, 1.4f), "cielo");
            Assert.IsFalse(ARPiso.EsPiso(1.1f, 1.4f), "mesa a 30 cm del teléfono");
        }

        [Test]
        public void CercaDelPiso_ConPisoConocido_ExigeLaMismaAltura()
        {
            Assert.IsTrue(ARPiso.CercaDelPiso(0.1f, 1.4f, true, 0f));
            Assert.IsFalse(ARPiso.CercaDelPiso(0.5f, 1.4f, true, 0f), "punto 50 cm sobre el piso");
            Assert.IsTrue(ARPiso.CercaDelPiso(0.5f, 1.4f, false, 0f), "sin piso conocido basta con estar bajo el teléfono");
        }

        [Test]
        public void AlturaEstimada_SinPisoConocido_Es1_40BajoElTelefono()
        {
            Assert.AreEqual(0.2f, ARPiso.AlturaPisoEstimada(1.6f, false, 0f), TOL);
            Assert.AreEqual(-0.05f, ARPiso.AlturaPisoEstimada(1.6f, true, -0.05f), TOL);
        }

        // ------------------------------ app

        [Test]
        public void SinPisoConocido_ElPuntoSaleDelPisoEstimado()
        {
            var app = Montar();
            Assert.IsFalse(app.HayPisoConocido);
            var rayo = new Ray(new Vector3(2f, 1.5f, 0f), new Vector3(0f, -1f, 1f));
            Vector3 p;
            OrigenPunto o;
            Assert.IsTrue(app.PuntoPisoDesdeRayo(rayo, 1.5f, out p, out o));
            Assert.AreEqual(OrigenPunto.PisoEstimado, o);
            Assert.AreEqual(0.1f, p.y, TOL, "1,40 m bajo el teléfono");
            Assert.AreEqual(2f, p.x, TOL);
            Assert.AreEqual(1.4f, p.z, TOL);
            Limpiar(app);
        }

        [Test]
        public void ConPisoConocido_ElPuntoSaleDelPisoExtendido()
        {
            var app = Montar();
            app.RegistrarAlturaPiso(-0.05f);
            Assert.IsTrue(app.HayPisoConocido);
            var rayo = new Ray(new Vector3(0f, 1.55f, 0f), new Vector3(0f, -1f, 2f));
            Vector3 p;
            OrigenPunto o;
            Assert.IsTrue(app.PuntoPisoDesdeRayo(rayo, 1.55f, out p, out o));
            Assert.AreEqual(OrigenPunto.PisoExtendido, o);
            Assert.AreEqual(-0.05f, p.y, TOL, "a la altura del piso detectado");
            Assert.AreEqual(3.2f, p.z, TOL);
            Limpiar(app);
        }

        [Test]
        public void ApuntandoAlCielo_NoHayPunto()
        {
            var app = Montar();
            var rayo = new Ray(new Vector3(0f, 1.5f, 0f), new Vector3(0f, 1f, 1f));
            Vector3 p;
            OrigenPunto o;
            Assert.IsFalse(app.PuntoPisoDesdeRayo(rayo, 1.5f, out p, out o));
            Limpiar(app);
        }

        // ------------------------------ mira de la interfaz

        [Test]
        public void LaMira_SoloSeVeMientrasSeMarca()
        {
            var app = Montar();
            var ui = app.Interfaz;
            Assert.IsNotNull(ui.mira, "Debe existir la mira del centro de la pantalla.");
            Assert.IsFalse(ui.mira.activeSelf, "Oculta fuera del modo de marcado.");

            app.ElegirElemento(14);
            app.IniciarMarcadoBase();
            Assert.IsTrue(ui.mira.activeSelf, "Visible al marcar la base.");
            app.CancelarMarcado();
            Assert.IsFalse(ui.mira.activeSelf);

            app.ElegirElemento(134);
            app.IniciarMarcadoViga();
            Assert.IsTrue(ui.mira.activeSelf, "Visible al marcar los extremos.");
            app.MarcarPunto(new Vector3(0f, 0f, 0f));
            Assert.IsTrue(ui.mira.activeSelf, "Sigue visible entre el extremo i y el j.");
            app.MarcarPunto(new Vector3(10f, 0f, 0f));
            Assert.IsFalse(ui.mira.activeSelf, "Se oculta al terminar de marcar.");
            Limpiar(app);
        }

        [Test]
        public void LaMira_NoInterceptaToques()
        {
            var app = Montar();
            foreach (var img in app.Interfaz.mira.GetComponentsInChildren<Image>(true))
                Assert.IsFalse(img.raycastTarget, "La mira no puede tapar los toques.");
            Limpiar(app);
        }
    }
}
