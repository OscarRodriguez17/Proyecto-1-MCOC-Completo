using System.Collections.Generic;
using System.IO;
using MCOC.AR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  Viga marcada APUNTANDO A ELLA (Corrección 7, parte 4b).
    //
    //  La viga 134 (viga0.60x0.80) va de la columna 14 (Eje F, x = 10) a la 26
    //  (Eje G, x = 20); su nodo está a 3,96 m sobre el piso, así que su cara
    //  inferior queda a 3,96 − 0,80 = 3,16 m. Se apunta a la cara inferior
    //  junto a la cara de cada columna (0,35 m antes de su eje) y la app pone
    //  el eje de la viga sobre los ejes de las columnas.
    // =====================================================================

    public class ARTechoTests
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
            if (app.Interfaz != null && app.Interfaz.root != null) Object.DestroyImmediate(app.Interfaz.root);
            Object.DestroyImmediate(app.gameObject);
        }

        private static Vector3 Extremo(ARInspeccionApp app, int tag, bool j)
        {
            var f = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var cont = typeof(ARInspeccionApp).GetField("contenedores", f).GetValue(app)
                       as Dictionary<int, GameObject>;
            var geo = typeof(ARInspeccionApp).GetField("geoPorTag", f).GetValue(app)
                      as Dictionary<int, ARGeometriaElemento>;
            return cont[tag].transform.TransformPoint(j ? geo[tag].puntoJ : geo[tag].puntoI);
        }

        private static bool Visible(Button b)
        {
            return b != null && b.gameObject.activeSelf;
        }

        // ------------------------- funciones puras -------------------------

        [TestCase("viga0.60x0.80", 0.80f)]
        [TestCase("viga 60x80", 0.80f)]
        [TestCase("V30x55", 0.55f)]
        [TestCase("sin medidas", ARTecho.PeraltePorDefecto)]
        public void Peralte_EsLaSegundaMedida(string seccion, float esperado)
        {
            Assert.AreEqual(esperado, ARTecho.Peralte(seccion), TOL);
        }

        [Test]
        public void Viga134_CaraInferiorA3_16mSobreElPiso()
        {
            var g = ARGeometriaBuilder.ConstruirElemento(Raiz().elementos["134"], new ARGeometriaOpciones());
            Assert.AreEqual(3.16f, ARTecho.AlturaInferiorViga(g), TOL);
        }

        [Test]
        public void EjesDesdeCaras_SeCorrenMediaSeccionHaciaAfuera()
        {
            Vector3 ei, ej;
            ARTecho.EjesDesdeCaras(new Vector3(10.35f, 3.16f, 2f), new Vector3(19.65f, 3.20f, 2f),
                                   0.35f, 0.35f, out ei, out ej);
            Assert.AreEqual(10f, ei.x, TOL);
            Assert.AreEqual(20f, ej.x, TOL);
            Assert.AreEqual(2f, ei.z, TOL);
            Assert.AreEqual(3.16f, ei.y, TOL, "La altura no cambia.");
        }

        [Test]
        public void EsTecho_SoloSobreElTelefono()
        {
            Assert.IsTrue(ARTecho.EsTecho(3.0f, 1.4f));
            Assert.IsFalse(ARTecho.EsTecho(1.5f, 1.4f), "A la altura del teléfono no es techo.");
            Assert.IsFalse(ARTecho.EsTecho(0f, 1.4f), "El piso no es techo.");
        }

        // ------------------------- flujo en la app -------------------------

        [Test]
        public void Viga134_ApuntandoALaCaraInferior_QuedaSobreLosEjesDeSusColumnas()
        {
            var app = Montar();
            app.Camara.transform.position = new Vector3(15f, 1.4f, -4f);
            app.ElegirElemento(134);

            app.IniciarMarcadoTecho();
            Assert.AreEqual(ModoMarcado.TechoI, app.Modo);
            StringAssert.Contains("columna 14", app.Mensaje);
            Assert.IsTrue(app.ProfundidadPedida, "Mientras se marca se pide profundidad a ARCore.");

            // Cara inferior a 3,16 m, junto a la cara de cada columna (0,35 m antes del eje).
            app.MarcarPuntoTecho(new Vector3(10.35f, 3.16f, 0f), OrigenTecho.Profundidad);
            Assert.AreEqual(ModoMarcado.TechoJ, app.Modo);
            StringAssert.Contains("columna 26", app.Mensaje);
            app.MarcarPuntoTecho(new Vector3(19.65f, 3.16f, 0f), OrigenTecho.Profundidad);
            Assert.AreEqual(ModoMarcado.Ninguno, app.Modo);
            Assert.IsFalse(app.ProfundidadPedida, "Al terminar se apaga la profundidad.");

            // Sin piso conocido, el piso sale de la cara inferior medida: 3,16 − 3,16 = 0.
            Vector3 ancla = app.PosicionAncla();
            Assert.AreEqual(15f, ancla.x, TOL);
            Assert.AreEqual(0f, ancla.y, TOL);
            Assert.AreEqual(0f, ancla.z, TOL);

            Vector3 i = Extremo(app, 134, false), j = Extremo(app, 134, true);
            Assert.AreEqual(10f, i.x, TOL); Assert.AreEqual(0f, i.z, TOL);
            Assert.AreEqual(20f, j.x, TOL); Assert.AreEqual(0f, j.z, TOL);
            Assert.AreEqual(3.96f, i.y, TOL, "El nodo de la viga, a 3,96 m sobre el piso.");
            Assert.IsTrue(app.TieneRotacionMarcada);
            StringAssert.Contains("Medido 10.00", app.Mensaje.Replace(",", "."));
            Limpiar(app);
        }

        [Test]
        public void ConPisoConocido_ElAnclaVaAlPiso_YSeInformaLaAlturaBajoViga()
        {
            var app = Montar();
            app.RegistrarAlturaPiso(0f);
            app.ElegirElemento(134);
            app.IniciarMarcadoTecho();
            // Un lugar con la viga más baja que el modelo (2,50 m libres).
            app.MarcarPuntoTecho(new Vector3(10.35f, 2.50f, 0f), OrigenTecho.Plano);
            app.MarcarPuntoTecho(new Vector3(19.65f, 2.50f, 0f), OrigenTecho.Profundidad);

            Assert.AreEqual(0f, app.PosicionAncla().y, TOL, "El ancla va al piso conocido.");
            string m = app.Mensaje.Replace(",", ".");
            StringAssert.Contains("bajo viga 2.50 m", m);
            StringAssert.Contains("modelo 3.16 m", m);
            Limpiar(app);
        }

        [Test]
        public void VigaEnDiagonal_ElRumboSaleDeLosDosPuntos()
        {
            var app = Montar();
            app.ElegirElemento(134);
            app.IniciarMarcadoTecho();
            // Viga a 45° en planta: caras a 0,35 m de cada eje, ejes en (0,0) y (7.071, 7.071).
            float k = 0.35f / Mathf.Sqrt(2f), e = 10f / Mathf.Sqrt(2f);
            app.MarcarPuntoTecho(new Vector3(k, 3.16f, k), OrigenTecho.Profundidad);
            app.MarcarPuntoTecho(new Vector3(e - k, 3.16f, e - k), OrigenTecho.Profundidad);

            Vector3 i = Extremo(app, 134, false), j = Extremo(app, 134, true);
            Assert.AreEqual(0f, i.x, 2e-3f); Assert.AreEqual(0f, i.z, 2e-3f);
            Assert.AreEqual(e, j.x, 2e-3f); Assert.AreEqual(e, j.z, 2e-3f);
            Limpiar(app);
        }

        [Test]
        public void PuntoEstimado_UsaLaAlturaDelModeloSobreElPisoEstimado()
        {
            var app = Montar();
            app.ElegirElemento(134);
            // Teléfono a 1,40 m sobre el piso (estimado en 0), mirando 45° hacia arriba.
            var rayo = new Ray(new Vector3(0f, 1.4f, 0f), new Vector3(0f, 1f, 1f).normalized);
            Vector3 p;
            OrigenTecho o;
            Assert.IsTrue(app.PuntoTechoDesdeRayo(rayo, 1.4f, out p, out o));
            Assert.AreEqual(OrigenTecho.Estimado, o);
            Assert.AreEqual(3.16f, p.y, TOL);
            Assert.AreEqual(1.76f, p.z, TOL);

            var haciaAbajo = new Ray(new Vector3(0f, 1.4f, 0f), new Vector3(0f, -1f, 1f).normalized);
            Assert.IsFalse(app.PuntoTechoDesdeRayo(haciaAbajo, 1.4f, out p, out o),
                           "Mirando al piso no hay cara inferior de viga.");
            Limpiar(app);
        }

        [Test]
        public void ConPuntosEstimados_LoAvisa()
        {
            var app = Montar();
            app.ElegirElemento(134);
            app.IniciarMarcadoTecho();
            app.MarcarPuntoTecho(new Vector3(10.35f, 3.16f, 0f), OrigenTecho.Estimado);
            app.MarcarPuntoTecho(new Vector3(19.65f, 3.16f, 0f), OrigenTecho.Profundidad);
            StringAssert.Contains("ESTIMADO", app.Mensaje);
            Limpiar(app);
        }

        [Test]
        public void PuntosMuyCerca_PideMarcarDeNuevo()
        {
            var app = Montar();
            app.ElegirElemento(134);
            app.IniciarMarcadoTecho();
            app.MarcarPuntoTecho(new Vector3(10f, 3.16f, 0f), OrigenTecho.Profundidad);
            app.MarcarPuntoTecho(new Vector3(10.1f, 3.16f, 0f), OrigenTecho.Profundidad);
            // 0,1 m entre los dos puntos: no se puede leer el rumbo.
            Assert.AreEqual(ModoMarcado.TechoI, app.Modo);
            Assert.IsFalse(app.EstaColocado(134));
            Limpiar(app);
        }

        [Test]
        public void Botones_LaVigaOfreceLasDosFormas_LaColumnaNinguna()
        {
            var app = Montar();
            var ui = app.Interfaz;
            app.ElegirElemento(134);
            Assert.IsTrue(Visible(ui.btnMarcarTecho), "Viga: «apuntar a ella».");
            Assert.IsTrue(Visible(ui.btnMarcarViga), "Viga: «pie de columnas» como alternativa.");
            ui.btnMarcarTecho.onClick.Invoke();
            Assert.AreEqual(ModoMarcado.TechoI, app.Modo, "El botón está conectado.");
            Assert.IsFalse(Visible(ui.btnMarcarTecho), "Mientras se marca, sólo «Cancelar».");

            app.ElegirElemento(14);
            Assert.AreEqual(ModoMarcado.Ninguno, app.Modo, "Cambiar de elemento cancela el marcado.");
            Assert.IsFalse(app.ProfundidadPedida);
            Assert.IsFalse(Visible(ui.btnMarcarTecho), "Una columna no se marca apuntando al techo.");
            Limpiar(app);
        }

        [Test]
        public void ConUnaColumna_ApuntarALaViga_PasaAMarcarBase()
        {
            var app = Montar();
            app.ElegirElemento(26);
            app.IniciarMarcadoTecho();
            Assert.AreEqual(ModoMarcado.Base, app.Modo);
            Limpiar(app);
        }
    }
}
