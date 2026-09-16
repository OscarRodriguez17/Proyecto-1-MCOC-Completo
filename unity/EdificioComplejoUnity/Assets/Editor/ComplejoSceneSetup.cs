using System.IO;
using MCOC.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MCOC.EditorTools
{
    /// <summary>
    /// Bootstrap editor del COMPLEJO: crea una vez la escena `Assets/Scenes/Complejo.unity`
    /// con cámara orbital, luz y el GameObject "Complejo" (UnityComplejo) que lee
    /// `edificio_completo.json` de StreamingAssets.
    ///
    /// Re-ejecutable bajo el menú Tools/MCOC/Preparar escena COMPLEJO.
    /// </summary>
    [InitializeOnLoad]
    public static class ComplejoSceneSetup
    {
        private const string ScenesDir = "Assets/Scenes";
        private const string ScenePath = ScenesDir + "/Complejo.unity";

        static ComplejoSceneSetup()
        {
            EditorApplication.delayCall += MaybeCreateScene;
        }

        private static void MaybeCreateScene()
        {
            if (File.Exists(ScenePath)) return;
            PrepararEscena();
        }

        [MenuItem("Tools/MCOC/Preparar escena COMPLEJO")]
        public static void PrepararEscena()
        {
            EnsureScenesDir();
            if (File.Exists(ScenePath))
            {
                // Sobrescribir solo si se pide desde el menu.
                var sel = EditorUtility.DisplayDialog("Escena COMPLEJO",
                    "La escena ya existe. Reconstruirla?", "Reconstruir", "Cancelar");
                if (!sel) return;
            }

            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            CrearCamara();
            CrearLuz();
            CrearComplejo();

            EditorSceneManager.SaveScene(escena, ScenePath);
            Debug.Log("[MCOC] Escena COMPLEJO creada en " + ScenePath);
        }

        [MenuItem("Tools/MCOC/Abrir escena COMPLEJO")]
        public static void AbrirEscena()
        {
            if (!File.Exists(ScenePath))
            {
                PrepararEscena();
                return;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void EnsureScenesDir()
        {
            if (!AssetDatabase.IsValidFolder(ScenesDir))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
        }

        private static void CrearCamara()
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            var orbit = camGO.AddComponent<OrbitCamera>();
            orbit.distancia = 260f;
            orbit.minDist = 5f;
        }

        private static void CrearLuz()
        {
            var luzGO = new GameObject("Directional Light");
            var luz = luzGO.AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.intensity = 1.1f;
            luzGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CrearComplejo()
        {
            var complejo = new GameObject("Complejo");
            var visor = complejo.AddComponent<UnityComplejo>();
            visor.jsonRuta = "edificio_completo.json";
        }
    }
}