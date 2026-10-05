using System.IO;
using MCOC.AR;
using NUnit.Framework;
using UnityEngine;

namespace MCOC.AR.Editores
{
    public class ARDosPuntosTests
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

        private static ARGeometriaElemento Viga134()
        {
            var g = ARGeometriaBuilder.ConstruirElemento(Raiz().elementos["134"], new ARGeometriaOpciones());
            Assert.AreEqual("viga", g.tipo);
            return g;
        }

        private static Vector3 Girar(Vector3 p, float grados)
        {
            return Quaternion.Euler(0f, grados, 0f) * p;
        }

        [TestCase(-5f, 0f, 5f, 0f, 0f, 0f, 0f, TestName = "DosPuntos_Simetricos")]
        [TestCase(10f, 2f, 20f, 2f, 0f, 0f, 0f, TestName = "DosPuntos_Desplazados")]
        [TestCase(10f, 2f, 20f, 2f, 90f, 0f, 0f, TestName = "DosPuntos_Girados90")]
        [TestCase(10f, 2f, 20f, 2f, 37f, 0f, 0f, TestName = "DosPuntos_Girados37")]
        [TestCase(10f, 2f, 20f, 2f, 0f, 0.03f, 0f, TestName = "DosPuntos_AlturaDistinta_UsaLaMenor")]
        [TestCase(20f, 2f, 10f, 2f, 0f, 0f, 0f, TestName = "DosPuntos_Invertidos_GiraLaViga180")]
        public void PorDosPuntos_AnclaEnElPuntoMedio_YEjeSobreLaRecta(
            float ax, float az, float bx, float bz, float giro, float yi, float yj)
        {
            var g = Viga134();
            Vector3 Pi = Girar(new Vector3(ax, 0f, az), giro) + Vector3.up * yi;
            Vector3 Pj = Girar(new Vector3(bx, 0f, bz), giro) + Vector3.up * yj;
            var c = ARColocacion.PorDosPuntos(Pi, Pj, g);

            float y = Mathf.Min(Pi.y, Pj.y);
            Assert.AreEqual((Pi.x + Pj.x) * 0.5f, c.posicion.x, TOL);
            Assert.AreEqual(y, c.posicion.y, TOL);
            Assert.AreEqual((Pi.z + Pj.z) * 0.5f, c.posicion.z, TOL);

            Vector3 i = c.posicion + c.rotacion * g.puntoI;
            Vector3 j = c.posicion + c.rotacion * g.puntoJ;
            Vector3 recta = new Vector3(Pj.x - Pi.x, 0f, Pj.z - Pi.z).normalized;
            Assert.Greater(Vector3.Dot((j - i).normalized, recta), 0.999f);
            Assert.AreEqual(10f, (j - i).magnitude, TOL);
            Assert.AreEqual(3.96f, i.y - c.posicion.y, TOL);
            Assert.AreEqual(3.96f, j.y - c.posicion.y, TOL);
            Vector3 centro = (i + j) * 0.5f;
            Assert.AreEqual(c.posicion.x, centro.x, TOL);
            Assert.AreEqual(c.posicion.z, centro.z, TOL);
            Assert.AreEqual(1f, Vector3.Dot(Vector3.up, c.rotacion * Vector3.up), TOL);
            Assert.AreEqual(ARColocacion.DistanciaHorizontal(Pi, Pj), c.distancia, TOL);
        }

        [Test]
        public void DistanciaHorizontal_IgnoraLaAltura()
        {
            Assert.AreEqual(5f, ARColocacion.DistanciaHorizontal(Vector3.zero, new Vector3(3f, 5f, 4f)), 1e-5f);
        }

        [Test]
        public void FueraDeTolerancia_DiezPorCiento()
        {
            Assert.IsFalse(ARColocacion.FueraDeTolerancia(9.5f, 10f));
            Assert.IsTrue(ARColocacion.FueraDeTolerancia(8.9f, 10f));
            Assert.AreEqual(-1.3f, ARColocacion.DiferenciaPorcentual(9.87f, 10f), 1e-3f);
        }

        [Test]
        public void MitadSeccion_LeeLaPrimeraMedidaDelNombre()
        {
            Assert.AreEqual(0.35f, ARColocacion.MitadSeccion("col_A_0.70x0.70"), 1e-5f);
            Assert.AreEqual(0.30f, ARColocacion.MitadSeccion("viga0.60x0.80"), 1e-5f);
            Assert.AreEqual(0.35f, ARColocacion.MitadSeccion("C70x70"), 1e-5f);
            Assert.AreEqual(0f, ARColocacion.MitadSeccion("sin medidas"), 1e-5f);
            Assert.AreEqual(0f, ARColocacion.MitadSeccion(null), 1e-5f);
        }

        [Test]
        public void BaseDesdeCara_EntraMediaSeccionHaciaDondeMiraLaCamara()
        {
            Vector3 cara = new Vector3(3f, 0.02f, -1f);
            Vector3 b = ARColocacion.BaseDesdeCara(cara, new Vector3(0f, -0.6f, 0.8f), 0.35f);
            Assert.AreEqual(3f, b.x, 1e-5f);
            Assert.AreEqual(0.02f, b.y, 1e-5f);
            Assert.AreEqual(-0.65f, b.z, 1e-5f);
            Assert.AreEqual(cara, ARColocacion.BaseDesdeCara(cara, Vector3.forward, 0f));
        }
    }
}
