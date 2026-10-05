using MCOC.AR;
using NUnit.Framework;
using UnityEngine;

namespace MCOC.AR.Editores
{
    public class ARBillboardTests
    {
        [TestCase(0f, 0f, -3f, TestName = "Billboard_CamaraDetras")]
        [TestCase(3f, 0f, 0f, TestName = "Billboard_CamaraAlLado")]
        [TestCase(-2f, 1.5f, 2f, TestName = "Billboard_CamaraArribaEnDiagonal")]
        public void ElTextoMiraEnDireccionContrariaALaCamara(float cx, float cy, float cz)
        {
            var camGo = new GameObject("CamaraTest");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(cx, cy, cz);
            camGo.transform.rotation = Quaternion.LookRotation(-camGo.transform.position, Vector3.up);
            var texto = new GameObject("Texto");
            var b = texto.AddComponent<ARBillboard>();
            b.Orientar(cam);
            Vector3 haciaAfuera = (texto.transform.position - camGo.transform.position).normalized;
            Assert.Greater(Vector3.Dot(texto.transform.forward, haciaAfuera), 0.99f);
            Assert.Greater(Vector3.Dot(texto.transform.up, camGo.transform.up), 0.9f);
            Object.DestroyImmediate(texto);
            Object.DestroyImmediate(camGo);
        }

        [Test]
        public void SinCamaraNoRevienta()
        {
            var texto = new GameObject("Texto");
            var b = texto.AddComponent<ARBillboard>();
            Assert.DoesNotThrow(() => b.Orientar(null));
            Object.DestroyImmediate(texto);
        }
    }
}
