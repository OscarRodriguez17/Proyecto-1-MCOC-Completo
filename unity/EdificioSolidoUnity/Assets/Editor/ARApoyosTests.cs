using System.IO;
using MCOC.AR;
using NUnit.Framework;
using UnityEngine;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  Viga marcada por el pie de sus columnas (Corrección 6, parte 3b).
    //
    //  La viga 134 va de la columna 14 (Eje F) a la 26 (Eje G). Se marca el pie
    //  de cada columna en la cara que se ve; la app entra media sección (0,35 m)
    //  hacia el eje de la columna, donde llega el eje de la viga.
    // =====================================================================

    public class ARApoyosTests
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

        [Test]
        public void Viga134_ApoyaEnColumna14_YColumna26()
        {
            var viga = Raiz().elementos["134"];
            var ci = ARApoyos.ColumnaBajoExtremo(Raiz(), viga, true);
            var cj = ARApoyos.ColumnaBajoExtremo(Raiz(), viga, false);
            Assert.IsNotNull(ci, "Debe encontrar la columna bajo el extremo i (Eje F).");
            Assert.IsNotNull(cj, "Debe encontrar la columna bajo el extremo j (Eje G).");
            Assert.AreEqual(14, ci.tag);
            Assert.AreEqual(26, cj.tag);
            Assert.AreEqual(0.35f, ARApoyos.MitadApoyo(ci), TOL);
            Assert.AreEqual("la columna 14", ARApoyos.NombreApoyo(ci));
        }

        [Test]
        public void SinColumnaConocida_UsaValoresPorDefecto()
        {
            Assert.IsNull(ARApoyos.ColumnaBajoExtremo(Raiz(), Raiz().elementos["14"], true),
                          "Una columna no tiene 'columna de apoyo' en el contrato.");
            Assert.AreEqual(ARApoyos.MitadColumnaPorDefecto, ARApoyos.MitadApoyo(null), TOL);
            Assert.AreEqual("la columna de apoyo", ARApoyos.NombreApoyo(null));
        }

        [Test]
        public void Viga134_MarcadaPorElPieDeSusColumnas_QuedaSobreSusEjes()
        {
            var go = new GameObject("App");
            var app = go.AddComponent<ARInspeccionApp>();
            app.Preparar();
            app.AplicarContrato(Raiz());
            app.Camara.transform.rotation = Quaternion.Euler(25f, 0f, 0f); // mirando a +Z, hacia el piso
            app.ElegirElemento(134);

            // Pie de las columnas 14 y 26 en la cara que se ve (0,35 m antes de su eje).
            app.IniciarMarcadoViga();
            app.MarcarPunto(new Vector3(10f, 0f, -0.35f));
            app.MarcarPunto(new Vector3(20f, 0f, -0.35f));
            Assert.AreEqual(ModoMarcado.Ninguno, app.Modo);

            Vector3 ancla = app.PosicionAncla();
            Assert.AreEqual(15f, ancla.x, TOL);
            Assert.AreEqual(0f, ancla.z, TOL, "El ancla queda sobre el eje A3, no en la cara de las columnas.");
            StringAssert.Contains("Medido 10", app.Mensaje.Replace(",", "."));

            var f = typeof(ARInspeccionApp).GetField("contenedores",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var mapa = f.GetValue(app) as System.Collections.Generic.Dictionary<int, GameObject>;
            var fg = typeof(ARInspeccionApp).GetField("geoPorTag",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var geo = fg.GetValue(app) as System.Collections.Generic.Dictionary<int, ARGeometriaElemento>;
            Transform c = mapa[134].transform;
            Vector3 i = c.TransformPoint(geo[134].puntoI);
            Vector3 j = c.TransformPoint(geo[134].puntoJ);
            Assert.AreEqual(10f, i.x, TOL); Assert.AreEqual(0f, i.z, TOL);
            Assert.AreEqual(20f, j.x, TOL); Assert.AreEqual(0f, j.z, TOL);
            Assert.AreEqual(3.96f, i.y - ancla.y, TOL);

            if (app.Interfaz != null && app.Interfaz.root != null) Object.DestroyImmediate(app.Interfaz.root);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void LasInstrucciones_NombranLasColumnasDeApoyo()
        {
            var go = new GameObject("App");
            var app = go.AddComponent<ARInspeccionApp>();
            app.Preparar();
            app.AplicarContrato(Raiz());
            app.ElegirElemento(134);
            StringAssert.Contains("columna 14", app.Mensaje);
            StringAssert.Contains("columna 26", app.Mensaje);
            app.IniciarMarcadoViga();
            StringAssert.Contains("PIE de la columna 14", app.Mensaje);
            app.MarcarPunto(new Vector3(10f, 0f, -0.35f));
            StringAssert.Contains("PIE de la columna 26", app.Mensaje);
            if (app.Interfaz != null && app.Interfaz.root != null) Object.DestroyImmediate(app.Interfaz.root);
            Object.DestroyImmediate(go);
        }
    }
}
