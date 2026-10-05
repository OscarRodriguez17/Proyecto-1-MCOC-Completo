using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using MCOC.AR;

namespace MCOC.AR.Editores
{
    public class UIOverlapTests
    {
        [Test]
        public void NoRaycastTargetImageSolapaBotones()
        {
            GameObject padre = new GameObject("Padre");
            var rig = ARRig.Construir(padre.transform);
            var ui = ARInterfaz.Crear(new ARInterfazOpciones());
            Canvas canvas = ui.canvas;
            Camera cam = rig.camara;

            Button[] botones = ui.root.GetComponentsInChildren<Button>(true);
            Image[] imagenes = ui.root.GetComponentsInChildren<Image>(true);

            foreach (var img in imagenes)
            {
                if (img.raycastTarget)
                {
                    Rect rectImg = GetScreenRect(img.rectTransform, canvas, new Vector2(2772, 1280));
                    foreach (var btn in botones)
                    {
                        Rect rectBtn = GetScreenRect(btn.GetComponent<RectTransform>(), canvas, new Vector2(2772, 1280));
                        Assert.IsFalse(rectImg.Overlaps(rectBtn));
                    }
                }
            }

            Camera[] cams = Object.FindObjectsOfType<Camera>(true);
            int enabledCount = 0;
            foreach (var c in cams)
            {
                if (c.enabled) enabledCount++;
            }
            Assert.AreEqual(1, enabledCount);

            Object.DestroyImmediate(padre);
            Object.DestroyImmediate(ui.root);
        }

        private Rect GetScreenRect(RectTransform rt, Canvas canvas, Vector2 refRes)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }
    }
}
