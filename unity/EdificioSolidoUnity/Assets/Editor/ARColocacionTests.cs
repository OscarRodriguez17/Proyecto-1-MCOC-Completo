using System.IO;
using MCOC.AR;
using NUnit.Framework;
using UnityEngine;

namespace MCOC.AR.Editores
{
    // =====================================================================
    //  Prueba de COLOCACIÓN, en el Editor y sin teléfono.
    //
    //  No hay sesión AR aquí: se comprueba la geometría que la app colocaría,
    //  es decir la que sale de `ARGeometriaBuilder` (que resta
    //  anclaje.punto_unity) más la rotación que decide `ARColocacion`.
    //  Ambas clases son estáticas y sin estado justamente para poder
    //  verificarlas aquí.
    //
    //  Ancla simulada en (0,0,0) y cámara mirando a +Z, con f = +Z y
    //  r = up × f = +X.
    // =====================================================================

    public class ARColocacionTests
    {
        private const float TOL = 1e-3f;
        private static readonly Vector3 FrenteMasZ = new Vector3(0f, 0f, 1f);

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

        private static ARGeometriaElemento Construir(int tag)
        {
            return ARGeometriaBuilder.ConstruirElemento(
                Raiz().elementos[tag.ToString()],
                new ARGeometriaOpciones { amplitud = 0.45f, carril = 0.28f });
        }

        /// <summary>Extremo del elemento ya en el ancla (0,0,0), orientado.</summary>
        private static Vector3 Extremo(ARGeometriaElemento g, bool i)
        {
            Quaternion rot = ARColocacion.RotacionInicial(g, FrenteMasZ);
            return ARColocacion.PuntoEnAncla(g, rot, Vector3.zero,
                                            i ? g.puntoI : g.puntoJ);
        }

        // -----------------------------------------------------------------

        [Test]
        public void Viga134_IzquierdaY_JDerecha_A3_96m()
        {
            var g = Construir(134);
            Assert.AreEqual("viga", g.tipo);

            Vector3 i = Extremo(g, true);
            Vector3 j = Extremo(g, false);

            // ar_ref_tag134.png: el extremo i a la izquierda, el j a la derecha.
            Assert.Less(i.x, 0f, "El extremo i debe quedar a la izquierda (x < 0).");
            Assert.Greater(j.x, 0f, "El extremo j debe quedar a la derecha (x > 0).");

            // La viga 134 va a 3,96 m sobre el piso, en los dos extremos.
            Assert.AreEqual(3.96f, i.y, TOL, "Altura del extremo i sobre el piso.");
            Assert.AreEqual(3.96f, j.y, TOL, "Altura del extremo j sobre el piso.");

            // Sin inclinación: la vertical se mapea en sí misma (dot = 1), lo que
            // además descarta cualquier roll o pitch.
            Quaternion rot = ARColocacion.RotacionInicial(g, FrenteMasZ);
            Assert.AreEqual(1f, Vector3.Dot(Vector3.up, rot * Vector3.up), TOL,
                            "La rotación debe ser sólo en torno a la vertical.");
            Assert.AreEqual(10f, Vector3.Distance(Extremo(g, true), Extremo(g, false)), TOL,
                            "Escala 1:1: la viga debe medir 10 m de luz.");
        }

        [Test]
        public void Columna14_BaseEnElPiso_CabezaA3_96m()
        {
            var g = Construir(14);
            Assert.AreEqual("columna", g.tipo);

            Vector3 basePiso = Extremo(g, true);
            Vector3 cabeza = Extremo(g, false);

            Assert.AreEqual(0f, basePiso.y, TOL, "La base debe quedar en el piso (y = 0).");
            Assert.AreEqual(3.96f, cabeza.y, TOL, "La cabeza debe quedar a 3,96 m.");

            // Centrada en el ancla: la base queda en el propio punto tocado.
            Assert.AreEqual(0f, basePiso.x, TOL);
            Assert.AreEqual(0f, basePiso.z, TOL);
        }

        [Test]
        public void DerechaDelEspectador_EsUpCruzadoConElFrente()
        {
            // f = +Z  ->  r = up × f = +X
            Assert.AreEqual(Vector3.right, ARColocacion.Derecha(FrenteMasZ));
            // f = +X  ->  r = up × f = -Z
            Assert.AreEqual(Vector3.back, ARColocacion.Derecha(Vector3.right));
            // f = -Z  ->  r = up × f = -X
            Assert.AreEqual(Vector3.left, ARColocacion.Derecha(Vector3.back));
            // Mirar al cielo no define una derecha: se degrada a +X.
            Assert.AreEqual(Vector3.right, ARColocacion.Derecha(Vector3.up));
        }

        [Test]
        public void Columna_LaTraccionDeMpositivoQuedaALaIzquierda()
        {
            var g = Construir(14);
            Quaternion rot = ARColocacion.RotacionInicial(g, FrenteMasZ);

            // El lado_positivo del momento principal mira a la izquierda del
            // espectador, que es -r = -X con la cámara mirando a +Z.
            Vector3 lado = rot * g.ladoPrincipal;
            Assert.AreEqual(-1f, lado.x, TOL, "M > 0 debe traccionar hacia la izquierda.");
            Assert.AreEqual(0f, lado.y, TOL);
            Assert.AreEqual(0f, lado.z, TOL);
        }

        [Test]
        public void GiroDelGesto_SeAcumulaSobreLaOrientacionInicial()
        {
            var g = Construir(134);
            Quaternion inicial = ARColocacion.RotacionInicial(g, FrenteMasZ);
            float delta = 30f;

            // Lo que hace GestosDosDedos: el delta antecede a la actual.
            Quaternion tras = Quaternion.Euler(0f, delta, 0f) * inicial;

            // Tres pasos de 10° dejan el mismo resultado que uno de 30°.
            Quaternion acumulado = inicial;
            for (int k = 0; k < 3; k++)
                acumulado = Quaternion.Euler(0f, 10f, 0f) * acumulado;

            Assert.Less(Quaternion.Angle(tras, acumulado), 1e-2f);
        }

        // -----------------------------------------------------------------
        //  Plan B: colocar sin raycast (Corrección 3, C)
        // -----------------------------------------------------------------

        /// <summary>
        /// El ancla estimada cae 1,40 m bajo la cámara y conserva las
        /// coordenadas horizontales: es el piso, no un punto a la vista.
        /// </summary>
        [Test]
        public void AnclaEstimada_Cae1_40mBajoLaCamara()
        {
            Assert.AreEqual(1.40f, ARColocacion.AlturaCamaraSobrePiso, 1e-6f);
            Assert.AreEqual(0.05f, ARColocacion.PasoPiso, 1e-6f);

            Vector3 camara = new Vector3(3f, 1.55f, -7f);
            Vector3 piso = ARColocacion.AnclaEstimadaBajoCamara(camara);

            Assert.AreEqual(camara.x, piso.x, TOL, "La x no se toca.");
            Assert.AreEqual(camara.z, piso.z, TOL, "La z no se toca.");
            Assert.AreEqual(camara.y - 1.40f, piso.y, TOL);
        }

        /// <summary>Los botones de 5 cm mueven SÓLO la altura.</summary>
        [Test]
        public void DesplazarPiso_MueveSoloLaAlturaEnPasosDe5cm()
        {
            Vector3 inicio = new Vector3(2f, 0f, -4f);

            Vector3 mas = ARColocacion.DesplazarPiso(inicio, 1);
            Assert.AreEqual(0.05f, mas.y - inicio.y, TOL, "+1 paso son +5 cm.");
            Assert.AreEqual(inicio.x, mas.x, TOL);
            Assert.AreEqual(inicio.z, mas.z, TOL);

            Vector3 menos = ARColocacion.DesplazarPiso(inicio, -1);
            Assert.AreEqual(-0.05f, menos.y - inicio.y, TOL, "−1 paso son −5 cm.");

            // Tres pasos arriba y uno abajo dejan dos pasos: 10 cm netos.
            Vector3 neto = ARColocacion.DesplazarPiso(ARColocacion.DesplazarPiso(inicio, 3), -1);
            Assert.AreEqual(0.10f, neto.y - inicio.y, TOL);

            // Cero pasos no mueve nada.
            Assert.AreEqual(inicio.y, ARColocacion.DesplazarPiso(inicio, 0).y, TOL);
        }

        /// <summary>
        /// El Plan B no toca la escala: el elemento sigue midiendo lo mismo que
        /// con el Plan A. Si se escalara, las longitudes dejarían de ser ciertas.
        /// </summary>
        [Test]
        public void PlanB_NoAlteraLaEscalaNiLaAlturaSobreElPiso()
        {
            var g = Construir(134);
            Quaternion rot = ARColocacion.RotacionInicial(g, FrenteMasZ);

            Vector3 anclaB = ARColocacion.AnclaEstimadaBajoCamara(new Vector3(1f, 1.40f, 2f));
            Vector3 i = ARColocacion.PuntoEnAncla(g, rot, anclaB, g.puntoI);

            // La viga sigue a 3,96 m sobre el ancla, se ponga donde se ponga.
            Assert.AreEqual(anclaB.y + 3.96f, i.y, TOL,
                            "El Plan B cambia dónde está el piso, no la altura del elemento.");
            Assert.AreEqual(10f, Vector3.Distance(
                ARColocacion.PuntoEnAncla(g, rot, anclaB, g.puntoI),
                ARColocacion.PuntoEnAncla(g, rot, anclaB, g.puntoJ)), TOL,
                            "La luz sigue siendo 10 m: escala 1:1.");
        }
    }
}
