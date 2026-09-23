using System.IO;
using MCOC.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MCOC.EditorTools
{
    [InitializeOnLoad]
    public static class MCOCSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        static MCOCSceneSetup()
        {
            EditorApplication.delayCall += AutoPreparar;
        }

        private static void AutoPreparar()
        {
            if (!File.Exists(ScenePath))
            {
                PrepararEscena();
                return;
            }

            // Solo abrir Main.unity automáticamente la primera vez que el editor
            // carga en esta sesión (evita secuestrar otra escena en recargas).
            if (!SessionState.GetBool("MCOC_scene_preparada", false))
            {
                SessionState.SetBool("MCOC_scene_preparada", true);
                OpenMainScene();
            }
        }

        [MenuItem("Tools/MCOC/Preparar escena Main")]
        public static void PrepararEscena()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            Scene escena =
                File.Exists(ScenePath)
                    ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camara = Object.FindObjectOfType<Camera>();
            if (camara == null)
            {
                GameObject camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
                camGO.transform.position = new Vector3(90f, 130f, 90f);
                camGO.transform.LookAt(Vector3.zero);
                OrbitCamera orbit = camGO.AddComponent<OrbitCamera>();
                orbit.distancia = 190f;
            }

            camara = Object.FindObjectOfType<Camera>();
            if (camara != null)
            {
                camara.clearFlags = CameraClearFlags.SolidColor;
                camara.backgroundColor = new Color(0.53f, 0.81f, 0.92f);
            }

            Light sol = Object.FindObjectOfType<Light>();
            if (sol == null)
            {
                GameObject solGO = new GameObject("Sol");
                solGO.AddComponent<Light>().type = LightType.Directional;
                sol = solGO.GetComponent<Light>();
                sol.intensity = 1.1f;
                sol.color = new Color(1f, 0.97f, 0.92f);
                sol.shadows = LightShadows.Soft;
                solGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.60f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

            GameObject stick = GameObject.Find("StickModel");
            if (stick == null)
            {
                stick = new GameObject("StickModel");
            }
            if (stick.GetComponent<UnityStickModel>() == null)
            {
                stick.AddComponent<UnityStickModel>();
            }

            if (!File.Exists(ScenePath))
                EditorSceneManager.SaveScene(escena, ScenePath);
            else
                EditorSceneManager.MarkSceneDirty(escena);

            EditorSceneManager.SaveOpenScenes();
            OpenMainScene();
            Debug.Log("[MCOC] Escena Main lista (StickModel + camara orbital). Pulsa Play.");
        }

        private static void OpenMainScene()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}