using System.IO;
using MCOC.AR;
using NUnit.Framework;
using UnityEngine;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  ORIENTACIÓN sobre la jerarquía REAL (Corrección 4, 1).
    //
    //  El fallo era doble: `ColocarEnAncla` ponía el rumbo de la cámara en la
    //  RAÍZ de diagramas y además la rotación inicial en el CONTENEDOR. Con
    //  ambos sitios rotando, la orientación se aplicaba dos veces y el
    //  elemento acababa en un rumbo arbitrario: mirando a +Z sí cuadraba
    //  (yaw 0), y en cuanto el teléfono miraba a otro lado, no.
    //
    //  `ARColocacionTests` verifica la geometría pura con la cámara mirando a
    //  +Z, que es justo el caso donde el fallo NO se nota. Este test recorre
    //  el camino del teléfono (`Preparar` → `AplicarContrato` → `ColocarAqui`)
    //  con la cámara girada a cinco frentes distintos y mide sobre los
    //  transforms reales de la jerarquía ancla → raíz → contenedor → línea.
    //
    //  Lo que se comprueba, para los cinco frentes:
    //   · la raíz de diagramas NO rota (identidad), que es la mitad del fallo;
    //   · viga 134: (j − i) en el mundo es paralelo a Derecha(frente);
    //   · viga 134: los dos extremos están a 3,96 m sobre el piso;
    //   · columna 14: rot·ladoPrincipal = −Derecha(frente).
    // =====================================================================

    public class AROrientacionTests
    {
        private const float TOL = 1e-3f;

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

        /// <summary>
        /// Monta la app, elige <paramref name="tag"/>, apunta la cámara a
        /// <paramref name="yawGrados"/> y lo ancla. Devuelve la jerarquía real ya
        /// colocada. Desde la Corrección 7 (4a) cada elemento tiene su propia
        /// ancla, así que se elige ANTES de colocar.
        /// </summary>
        private static ARInspeccionApp ColocarMirandoA(float yawGrados, int tag = 14)
        {
            var go = new GameObject("App");
            var app = go.AddComponent<ARInspeccionApp>();
            app.Preparar();
            app.AplicarContrato(Raiz());

            // El frente de la cámara ES lo que lee `FrenteCamara()` al colocar.
            Assert.IsNotNull(app.Camara, "El rig debe montar la cámara del AR.");
            app.Camara.transform.rotation = Quaternion.Euler(0f, yawGrados, 0f);

            if (app.TagSeleccionado != tag) Elegir(app, tag);

            // Plan B: ancla sin sesión AR, que es lo que hace falta aquí.
            app.ColocarAqui();

            Assert.IsNotNull(Contenedor(app), "No se ha creado el contenedor del tag " + tag + ".");
            return app;
        }

        private static void Limpiar(ARInspeccionApp app)
        {
            var campo = typeof(ARInspeccionApp).GetField("ui",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ui = campo.GetValue(app) as ARInterfaz;
            if (ui != null && ui.root != null) Object.DestroyImmediate(ui.root);
            Object.DestroyImmediate(app.gameObject);
        }

        private static Transform Raiz(ARInspeccionApp app)
        {
            var campo = typeof(ARInspeccionApp).GetField("raizVisual",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var go = campo.GetValue(app) as GameObject;
            return go != null ? go.transform : null;
        }

        private static Transform Contenedor(ARInspeccionApp app)
        {
            var campo = typeof(ARInspeccionApp).GetField("contenedores",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var mapa = campo.GetValue(app) as
                System.Collections.Generic.Dictionary<int, GameObject>;
            GameObject go;
            return mapa != null && mapa.TryGetValue(app.TagSeleccionado, out go) && go != null
                ? go.transform : null;
        }

        private static void Elegir(ARInspeccionApp app, int tag)
        {
            var metodo = typeof(ARInspeccionApp).GetMethod("Seleccionar",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            metodo.Invoke(app, new object[] { tag });
            Assert.AreEqual(tag, app.TagSeleccionado);
        }

        /// <summary>
        /// Geometría que la app tiene cargada para ese tag: la MISMA con la que
        /// se construyeron los `LineRenderer` del contenedor, no una copia.
        /// </summary>
        private static ARGeometriaElemento Geometria(ARInspeccionApp app, int tag)
        {
            var campo = typeof(ARInspeccionApp).GetField("geoPorTag",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var mapa = campo.GetValue(app) as
                System.Collections.Generic.Dictionary<int, ARGeometriaElemento>;
            ARGeometriaElemento g = null;
            Assert.IsTrue(mapa != null && mapa.TryGetValue(tag, out g) && g != null,
                          "La app no tiene geometría para el tag " + tag + ".");
            return g;
        }

        /// <summary>
        /// Extremo `i` o `j` del elemento en coordenadas de MUNDO, tal y como
        /// las vería el teléfono. Sale de la geometría cargada por la jerarquía
        /// real (el contenedor cuelga de la raíz, que cuelga del ancla), no de
        /// una fórmula.
        /// </summary>
        private static Vector3 ExtremoMundo(ARInspeccionApp app, int tag, bool j)
        {
            ARGeometriaElemento g = Geometria(app, tag);
            return Contenedor(app).TransformPoint(j ? g.puntoJ : g.puntoI);
        }

        /// <summary>Transform del GameObject del ancla.</summary>
        private static Transform Ancla(ARInspeccionApp app)
        {
            return Contenedor(app).parent.parent;
        }

        // -----------------------------------------------------------------
        //  La viga: el extremo i a la izquierda del espectador y el j a la
        //  derecha, sea cual sea el rumbo al que mire el teléfono.
        // -----------------------------------------------------------------

        [TestCase(0f, TestName = "Viga134_FrenteMasZ")]
        [TestCase(90f, TestName = "Viga134_FrenteMasX")]
        [TestCase(180f, TestName = "Viga134_FrenteMenosZ")]
        [TestCase(-90f, TestName = "Viga134_FrenteMenosX")]
        [TestCase(37f, TestName = "Viga134_Frente37Grados")]
        public void Viga134_A3_96m_ConElEjeXParaleloALaDerechaDelEspectador(float yaw)
        {
            Vector3 frente = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

            var app = ColocarMirandoA(yaw, 134);

            Vector3 i = ExtremoMundo(app, 134, false);
            Vector3 j = ExtremoMundo(app, 134, true);
            Vector3 derecha = ARColocacion.Derecha(frente);

            Vector3 eje = j - i;
            Assert.AreEqual(10f, eje.magnitude, TOL, "La luz sigue siendo 10 m: escala 1:1.");
            Assert.Greater(Vector3.Dot(eje.normalized, derecha), 0.999f,
                "El eje de la viga (de i a j) debe ir hacia la derecha del espectador " +
                "con la cámara mirando a " + frente + ", y quedó en " + eje.normalized +
                " siendo la derecha " + derecha + ".");

            // La altura se mide sobre el ANCLA, que es donde está el piso: la
            // raíz puede llevar el desplazamiento de ±5 cm.
            Transform ancla = Ancla(app);
            Assert.AreEqual(3.96f, i.y - ancla.position.y, TOL,
                "El extremo i a 3,96 m sobre el piso.");
            Assert.AreEqual(3.96f, j.y - ancla.position.y, TOL,
                "El extremo j a 3,96 m sobre el piso.");

            Limpiar(app);
        }

        /// <summary>
        /// La mitad izquierda del fallo: la raíz de diagramas tiene que estar en
        /// identidad. Con el yaw de la cámara ahí, la orientación se suma dos
        /// veces y este assert cae en cuanto el teléfono no mira a +Z.
        /// </summary>
        [TestCase(0f, TestName = "RaizIdentica_FrenteMasZ")]
        [TestCase(90f, TestName = "RaizIdentica_FrenteMasX")]
        [TestCase(180f, TestName = "RaizIdentica_FrenteMenosZ")]
        [TestCase(-90f, TestName = "RaizIdentica_FrenteMenosX")]
        [TestCase(37f, TestName = "RaizIdentica_Frente37Grados")]
        public void LaRaizDeDiagramas_NoRota(float yaw)
        {
            var app = ColocarMirandoA(yaw);

            Transform raiz = Raiz(app);
            Assert.IsNotNull(raiz, "Debe existir la raíz de diagramas.");
            Assert.Less(Quaternion.Angle(raiz.localRotation, Quaternion.identity), 1e-2f,
                "La raíz no debe rotar: el rumbo lo pone sólo el contenedor.");

            // Y el ancla tampoco: la orientación la decide ARColocacion, no la
            // pose del impacto.
            Assert.Less(Quaternion.Angle(raiz.parent.rotation, Quaternion.identity), 1e-2f,
                "El ancla debe quedar sin rotación.");

            Limpiar(app);
        }

        // -----------------------------------------------------------------
        //  La columna: la tracción de M > 0 a la izquierda del espectador,
        //  que es −Derecha(frente).
        // -----------------------------------------------------------------

        [TestCase(0f, TestName = "Columna14_FrenteMasZ")]
        [TestCase(90f, TestName = "Columna14_FrenteMasX")]
        [TestCase(180f, TestName = "Columna14_FrenteMenosZ")]
        [TestCase(-90f, TestName = "Columna14_FrenteMenosX")]
        [TestCase(37f, TestName = "Columna14_Frente37Grados")]
        public void Columna14_LaTraccionDeMpositivoQuedaALaIzquierdaDelEspectador(float yaw)
        {
            Vector3 frente = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 izquierda = -ARColocacion.Derecha(frente);

            var app = ColocarMirandoA(yaw);
            ARGeometriaElemento g = Geometria(app, 14);

            Transform contenedor = Contenedor(app);

            // La rotación del contenedor es la que se aplica a los trazos.
            Vector3 lado = contenedor.localRotation * g.ladoPrincipal;
            Assert.Greater(Vector3.Dot(lado.normalized, izquierda), 0.999f,
                "M > 0 debe traccionar hacia la izquierda del espectador con la " +
                "cámara mirando a " + frente + ", y quedó en " + lado +
                " siendo la izquierda " + izquierda + ".");
            Assert.AreEqual(0f, lado.y, TOL, "El lado de tracción es horizontal.");

            // Y el que de verdad se ve en pantalla: la marca de la flecha de
            // tracción, que es un hijo del contenedor.
            Vector3 flechaMundo = contenedor.TransformPoint(g.ladoPrincipal);
            Vector3 centroMundo = contenedor.TransformPoint(Vector3.zero);
            Vector3 trazado = (flechaMundo - centroMundo).normalized;
            Assert.Greater(Vector3.Dot(trazado, izquierda), 0.999f,
                "La flecha de tracción dibujada debe salir hacia la izquierda.");

            Limpiar(app);
        }
    }
}
