using System.Collections.Generic;
using System.IO;
using MCOC.AR;
using NUnit.Framework;
using UnityEngine;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  Menos ruido en pantalla (Corrección 6, parte 3c).
    //
    //  En terreno los rótulos se encimaban y el toggle «No principal» estaba
    //  AL REVÉS: la app deducía "principal" comparando el texto del plano
    //  ("local x–z") con el nombre del diagrama ("M_xz"), que nunca calzaba, así
    //  que ocultaba el diagrama principal y mostraba los secundarios. Ahora el
    //  builder marca cada trazo y rótulo con `principal` y su `tipo`, y los
    //  rótulos van a alturas distintas del elemento.
    // =====================================================================

    public class ARRotulosTests
    {
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

        private static ARGeometriaElemento Construir(string tag)
        {
            return ARGeometriaBuilder.ConstruirElemento(Raiz().elementos[tag], new ARGeometriaOpciones());
        }

        private static ARTrazo Trazo(ARGeometriaElemento g, ARTipoTrazo tipo, bool principal)
        {
            foreach (var t in g.trazos)
                if (t.tipo == tipo && t.principal == principal) return t;
            return null;
        }

        [TestCase("134", TestName = "Principal_Viga134_EsElPlanoXZ")]
        [TestCase("14", TestName = "Principal_Columna14_EsElPlanoXY")]
        public void ElTrazoPrincipal_EsElDelPlanoPrincipal(string tag)
        {
            var g = Construir(tag);
            var el = Raiz().elementos[tag];
            // Exactamente un M y un V principales, y son los del plano principal.
            int mPrin = 0, vPrin = 0;
            foreach (var t in g.trazos)
            {
                if (t.tipo == ARTipoTrazo.Momento && t.principal) { mPrin++; Assert.AreEqual(el.principal.M, t.etiqueta); }
                if (t.tipo == ARTipoTrazo.Cortante && t.principal) { vPrin++; Assert.AreEqual(el.principal.V, t.etiqueta); }
            }
            Assert.AreEqual(1, mPrin, "Un solo M principal.");
            Assert.AreEqual(1, vPrin, "Un solo V principal.");
            Assert.IsNotNull(Trazo(g, ARTipoTrazo.Momento, false), "El M del otro plano existe, como no principal.");
            Assert.IsTrue(Trazo(g, ARTipoTrazo.Normal, true) != null, "N siempre es principal.");
        }

        [Test]
        public void LosRotulos_VanAAlturasDistintas()
        {
            var g = Construir("134");
            var pos = new List<Vector3>();
            foreach (var m in g.marcas)
                if (m.tipo == ARTipoTrazo.Normal || m.tipo == ARTipoTrazo.Cortante || m.tipo == ARTipoTrazo.Momento)
                    pos.Add(m.posicion);
            Assert.GreaterOrEqual(pos.Count, 5);
            // A lo largo del eje de la viga (x), separados al menos 1 m en una luz de 10 m.
            for (int a = 0; a < pos.Count; a++)
                for (int b = a + 1; b < pos.Count; b++)
                    Assert.Greater(Mathf.Abs(pos[a].x - pos[b].x), 1.0f,
                        "Dos rótulos quedaron a menos de 1 m: se enciman.");
        }

        [Test]
        public void LosRotulos_SonCortos()
        {
            var g = Construir("134");
            foreach (var m in g.marcas)
            {
                if (m.tipo != ARTipoTrazo.Momento || !m.principal) continue;
                StringAssert.StartsWith("M_xz", m.texto);
                StringAssert.DoesNotContain("|max|", m.texto);
                Assert.Less(m.texto.Length, 50, "Rótulo demasiado largo: " + m.texto);
            }
        }

        [Test]
        public void EnLaApp_PorDefectoSeVeElPrincipalYNoElSecundario()
        {
            var go = new GameObject("App");
            var app = go.AddComponent<ARInspeccionApp>();
            app.Preparar();
            app.AplicarContrato(Raiz());
            app.ElegirElemento(134);
            app.ColocarAqui();

            var mapa = typeof(ARInspeccionApp).GetField("contenedores",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(app) as Dictionary<int, GameObject>;
            Transform c = mapa[134].transform;

            bool principalVisible = false, secundarioVisible = false;
            bool rotuloPrincipalVisible = false, rotuloSecundarioVisible = false;
            foreach (var lr in c.GetComponentsInChildren<LineRenderer>(true))
            {
                if (lr.gameObject.name == "Trazo_M_xz") principalVisible |= lr.enabled;
                if (lr.gameObject.name == "Trazo_Momento") secundarioVisible |= lr.enabled;
            }
            foreach (var tm in c.GetComponentsInChildren<TextMesh>(true))
            {
                var mr = tm.GetComponent<MeshRenderer>();
                if (tm.text.StartsWith("M_xz")) rotuloPrincipalVisible |= mr.enabled;
                if (tm.text.StartsWith("M_xy")) rotuloSecundarioVisible |= mr.enabled;
            }
            Assert.IsTrue(principalVisible, "El M principal (M_xz) se debe ver por defecto.");
            Assert.IsFalse(secundarioVisible, "El M secundario (M_xy) va oculto por defecto.");
            Assert.IsTrue(rotuloPrincipalVisible);
            Assert.IsFalse(rotuloSecundarioVisible, "Su rótulo también.");

            // Apagar «M» oculta el diagrama Y su rótulo.
            typeof(ARInspeccionApp).GetMethod("AplicarToggle",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(app, new object[] { "M" });
            foreach (var tm in c.GetComponentsInChildren<TextMesh>(true))
                if (tm.text.StartsWith("M_xz"))
                    Assert.IsFalse(tm.GetComponent<MeshRenderer>().enabled, "El rótulo de M sigue a su toggle.");

            if (app.Interfaz != null && app.Interfaz.root != null) Object.DestroyImmediate(app.Interfaz.root);
            Object.DestroyImmediate(go);
        }
    }
}
