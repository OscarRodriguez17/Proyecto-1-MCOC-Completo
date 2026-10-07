using System.IO;
using MCOC.AR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  Panel 2D de diagramas (Corrección 6, parte 3d).
    //
    //  Respaldo legible del AR: N, V y M del plano principal en un recuadro
    //  fijo en la pantalla, dibujados con los mismos valores del contrato.
    // =====================================================================

    public class ARPanel2DTests
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

        [TestCase("134", "V_xz", "M_xz", TestName = "Series_Viga134_PlanoXZ")]
        [TestCase("14", "V_xy", "M_xy", TestName = "Series_Columna14_PlanoXY")]
        public void LasSeries_SonNVyMdelPlanoPrincipal(string tag, string v, string m)
        {
            var s = ARGrafico.Series(Raiz().elementos[tag]);
            Assert.AreEqual(3, s.Count);
            Assert.AreEqual("N", s[0].nombre);
            Assert.AreEqual(v, s[1].nombre);
            Assert.AreEqual(m, s[2].nombre);
            Assert.AreEqual(tag == "134", s[2].invertir, "Sólo en vigas el M>0 va hacia abajo.");
        }

        [Test]
        public void Leyenda_Viga134_TraeLosValoresDeReferencia()
        {
            var s = ARGrafico.Series(Raiz().elementos["134"]);
            string m = ARGrafico.Leyenda(s[2]);
            StringAssert.StartsWith("M_xz [kN", m);
            // Sesion 29: la 134 recibe a mitad de vano la viga secundaria F-G del
            // cielo piso 2 (la app la muestra como viga fisica F-G = tramos 134+350).
            // Antes: i -114.3 / j -159.6.
            StringAssert.Contains("i -220 ", m);
            StringAssert.Contains("j -265.3", m);
        }

        [Test]
        public void APixel_EjesYSentidos()
        {
            int px, py;
            ARGrafico.APixel(0, 0, 10, 100, 10, 20, 101, 51, false, out px, out py);
            Assert.AreEqual(10, px, "x=0 → borde izquierdo");
            Assert.AreEqual(20 + 25, py, "v=0 → centro");
            ARGrafico.APixel(10, 100, 10, 100, 10, 20, 101, 51, false, out px, out py);
            Assert.AreEqual(110, px, "x=L → borde derecho");
            Assert.Greater(py, 45, "v>0 → arriba");
            ARGrafico.APixel(10, 100, 10, 100, 10, 20, 101, 51, true, out px, out py);
            Assert.Less(py, 45, "invertido: v>0 → abajo");
        }

        [Test]
        public void Dibujar_Viga134_ElMomentoQuedaEnLaFilaDeAbajo()
        {
            var tex = ARGrafico.Dibujar(Raiz().elementos["134"], null, 360, 240);
            Assert.AreEqual(360, tex.width);
            Assert.AreEqual(240, tex.height);
            var p = tex.GetPixels32();
            int rojosAbajo = 0, rojosArriba = 0;
            for (int y = 0; y < 240; y++)
                for (int x = 0; x < 360; x++)
                {
                    var c = p[y * 360 + x];
                    bool rojo = c.r > 200 && c.g < 100 && c.b < 100;
                    if (!rojo) continue;
                    if (y < 80) rojosAbajo++; else if (y >= 160) rojosArriba++;
                }
            Assert.Greater(rojosAbajo, 50, "El M (rojo) se dibuja en la fila de abajo.");
            Assert.AreEqual(0, rojosArriba, "Y no en la fila de N.");
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void ElPanel_SeAbreConElBoton_YSigueAlElemento()
        {
            var go = new GameObject("App");
            var app = go.AddComponent<ARInspeccionApp>();
            app.Preparar();
            app.AplicarContrato(Raiz());
            var ui = app.Interfaz;

            Assert.IsNotNull(ui.panel2D);
            Assert.IsFalse(ui.panel2D.activeSelf, "Oculto al partir.");
            ui.btnPanel2D.onClick.Invoke();
            Assert.IsTrue(app.Panel2DVisible);
            Assert.IsTrue(ui.panel2D.activeSelf);
            StringAssert.Contains("Tag 14", ui.panel2DTitulo.text, "Parte con el tag 14 (el primero).");
            Assert.IsNotNull(ui.panel2DImagen.texture);

            app.ElegirElemento(134);
            StringAssert.Contains("Tag 134", ui.panel2DTitulo.text, "Al cambiar de elemento, el panel lo sigue.");
            StringAssert.StartsWith("M_xz", ui.panel2DLeyendas[2].text);

            ui.btnPanel2D.onClick.Invoke();
            Assert.IsFalse(ui.panel2D.activeSelf);

            foreach (var g in ui.panel2D.GetComponentsInChildren<Graphic>(true))
                Assert.IsFalse(g.raycastTarget, "El panel no puede tapar toques: " + g.name);

            if (ui.root != null) Object.DestroyImmediate(ui.root);
            Object.DestroyImmediate(go);
        }
    }
}
