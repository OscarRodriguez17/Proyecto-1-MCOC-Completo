using System.Collections.Generic;
using System.IO;
using MCOC.AR;
using NUnit.Framework;
using UnityEngine;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  Los diagramas no se mueven (Corrección 7, parte 4a).
    //
    //  En terreno el diagrama "se movía":
    //   · re-elegir el elemento, o «Restablecer», lo volvía a orientar con la
    //     cámara de ESE momento;
    //   · al cambiar de viga a columna, la columna aparecía en el ancla de la
    //     viga (y viceversa);
    //   · dos dedos apoyados sin querer lo giraban.
    //  Ahora cada elemento tiene su ancla y su rumbo, fijados al colocarlo.
    // =====================================================================

    public class ARQuietoTests
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

        private static T Campo<T>(ARInspeccionApp app, string nombre) where T : class
        {
            return typeof(ARInspeccionApp).GetField(nombre,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(app) as T;
        }

        private static void Invocar(ARInspeccionApp app, string metodo, params object[] args)
        {
            typeof(ARInspeccionApp).GetMethod(metodo,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(app, args);
        }

        /// <summary>Extremo i (o j) del elemento en coordenadas de mundo, por la jerarquía real.</summary>
        private static Vector3 Extremo(ARInspeccionApp app, int tag, bool j)
        {
            var contenedores = Campo<Dictionary<int, GameObject>>(app, "contenedores");
            var geo = Campo<Dictionary<int, ARGeometriaElemento>>(app, "geoPorTag");
            var g = geo[tag];
            return contenedores[tag].transform.TransformPoint(j ? g.puntoJ : g.puntoI);
        }

        /// <summary>Punto de M>0 de la columna (lado de tracción) en mundo: delata un giro.</summary>
        private static Vector3 LadoTraccion(ARInspeccionApp app, int tag)
        {
            var contenedores = Campo<Dictionary<int, GameObject>>(app, "contenedores");
            var geo = Campo<Dictionary<int, ARGeometriaElemento>>(app, "geoPorTag");
            return contenedores[tag].transform.TransformPoint(geo[tag].ladoPrincipal);
        }

        private static void MarcarColumna(ARInspeccionApp app, int tag, Vector3 cara, float yaw)
        {
            app.ElegirElemento(tag);
            app.Camara.transform.rotation = Quaternion.Euler(20f, yaw, 0f);
            app.IniciarMarcadoBase();
            app.MarcarPunto(cara);
            Assert.AreEqual(ModoMarcado.Ninguno, app.Modo);
        }

        [Test]
        public void Columna_NoGira_AlMoverLaCamaraYReelegirORestablecer()
        {
            var app = Montar();
            MarcarColumna(app, 14, new Vector3(3f, 0f, -1f), 0f);
            Vector3 base0 = Extremo(app, 14, false);
            Vector3 lado0 = LadoTraccion(app, 14);

            // El usuario camina alrededor: la cámara mira hacia otro lado.
            app.Camara.transform.rotation = Quaternion.Euler(10f, 120f, 0f);

            app.ElegirElemento(14);                    // tocar de nuevo su botón
            Invocar(app, "Restablecer");               // «Restablecer»
            Invocar(app, "AplicarToggle", "N");        // un toggle cualquiera
            Invocar(app, "AplicarToggle", "N");
            Invocar(app, "CambiarAmplitud", 1.25f);    // «Amplitud +»

            Vector3 base1 = Extremo(app, 14, false);
            Vector3 lado1 = LadoTraccion(app, 14);
            Assert.Less(Vector3.Distance(base0, base1), TOL, "La base no se puede mover.");
            Assert.Less(Vector3.Distance(lado0, lado1), TOL,
                "El diagrama no puede girar con la cámara: su rumbo se fijó al marcarlo.");
            Limpiar(app);
        }

        [Test]
        public void CadaElemento_TieneSuPropiaAncla()
        {
            var app = Montar();
            app.Camara.transform.rotation = Quaternion.Euler(25f, 0f, 0f);

            // Columna 14 marcada en su pie…
            MarcarColumna(app, 14, new Vector3(10f, 0f, -0.35f), 0f);
            Vector3 col14 = Extremo(app, 14, false);
            Assert.AreEqual(10f, col14.x, TOL);
            Assert.AreEqual(0f, col14.z, TOL);

            // …la viga 134 por el pie de sus columnas…
            app.ElegirElemento(134);
            Assert.IsFalse(app.EstaColocado(134), "La viga todavía no está colocada.");
            app.IniciarMarcadoViga();
            app.MarcarPunto(new Vector3(10f, 0f, -0.35f));
            app.MarcarPunto(new Vector3(20f, 0f, -0.35f));
            Vector3 i134 = Extremo(app, 134, false);
            Vector3 j134 = Extremo(app, 134, true);

            // …y al volver a la columna, ESTÁ DONDE SE MARCÓ, no en el ancla de la viga.
            app.ElegirElemento(14);
            Assert.IsTrue(app.EstaColocado(14));
            Assert.IsTrue(app.EstaColocado(134));
            Assert.Less(Vector3.Distance(col14, Extremo(app, 14, false)), TOL,
                "La columna 14 tiene que volver a su pie, no al punto medio de la viga.");

            // Y la viga, también en su lugar.
            app.ElegirElemento(134);
            Assert.Less(Vector3.Distance(i134, Extremo(app, 134, false)), TOL);
            Assert.Less(Vector3.Distance(j134, Extremo(app, 134, true)), TOL);
            Assert.IsTrue(app.TieneRotacionMarcada, "La viga conserva su rumbo marcado.");
            Limpiar(app);
        }

        [Test]
        public void ElementoSinColocar_NoApareceEnElAnclaDeOtro()
        {
            var app = Montar();
            MarcarColumna(app, 14, new Vector3(3f, 0f, -1f), 0f);

            app.ElegirElemento(26);
            Assert.IsFalse(app.EstaColocado(26));
            var raiz = Campo<GameObject>(app, "raizVisual");
            Assert.IsFalse(raiz.activeSelf, "La columna 26 no se coloca sola en el ancla de la 14.");
            StringAssert.Contains("Marcar base", app.Mensaje);
            Limpiar(app);
        }

        [Test]
        public void QuitarAncla_SoloQuitaLaDelElementoElegido()
        {
            var app = Montar();
            MarcarColumna(app, 14, new Vector3(3f, 0f, -1f), 0f);
            MarcarColumna(app, 26, new Vector3(13f, 0f, -1f), 0f);

            Invocar(app, "QuitarAncla");
            Assert.IsFalse(app.EstaColocado(26));
            Assert.IsTrue(app.EstaColocado(14), "La columna 14 conserva su ancla.");

            app.ElegirElemento(14);
            var raiz = Campo<GameObject>(app, "raizVisual");
            Assert.IsTrue(raiz.activeSelf);
            Vector3 b14 = Extremo(app, 14, false);
            Assert.AreEqual(3f, b14.x, TOL);
            Assert.AreEqual(-0.65f, b14.z, TOL, "Media sección (0,35 m) más adentro de la cara.");
            Limpiar(app);
        }
    }
}
