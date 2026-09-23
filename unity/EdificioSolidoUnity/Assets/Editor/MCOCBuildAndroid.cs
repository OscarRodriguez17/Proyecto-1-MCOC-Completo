using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MCOC.EditorTools
{
    /// <summary>
    /// Build Android de la escena Main del visor sólido (Semana 05 — preparación móvil).
    ///
    /// Uso: menú Tools/MCOC/Build Android. NO usa batchmode ni línea de comandos:
    /// se compila interactivamente desde el editor, con los datos ya presentes en
    /// Assets/StreamingAssets (edificio_completo.json + edificio_solido.json).
    ///
    /// Hoja de prensa del APK resultante:
    ///   - Identifier : com.mcoc.edificiocomplejo
    ///   - Landscape  : fijado por PlayerSettings (LandscapeLeft) → se aplica
    ///                  al AndroidManifest del APK
    ///   - Min SDK    : 22 (Android 5.1)
    ///   - Target SDK : el que tenga instalado el editor (SDK Manager)
    ///   - Escena     : Assets/Scenes/Main.unity (única)
    ///   - Salida     : build/EdificioComplejo_MCOC.apk (raíz del proyecto)
    /// </summary>
    public static class MCOCBuildAndroid
    {
        private const string Menu = "Tools/MCOC/Build Android";
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string PackageId = "com.mcoc.edificiocomplejo";
        private const string BundleVersion = "1.0";

        [MenuItem(Menu)]
        public static void Build()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("MCOC Build Android",
                    "Falta la escena Assets/Scenes/Main.unity.\n" +
                    "Ejecuta primero 'Tools/MCOC/Preparar escena Main'.",
                    "Aceptar");
                return;
            }

            // --- 1. Scene list: asegurar Main.unity como única escena activa ---
            bool presente = false;
            int idx = -1;
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                if (EditorBuildSettings.scenes[i].path == ScenePath)
                {
                    presente = true;
                    idx = i;
                    break;
                }
            }
            if (presente)
            {
                if (!EditorBuildSettings.scenes[idx].enabled)
                    EditorBuildSettings.scenes[idx].enabled = true;
            }
            else
            {
                var lista = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                    EditorBuildSettings.scenes)
                {
                    new EditorBuildSettingsScene(ScenePath, true)
                };
                EditorBuildSettings.scenes = lista.ToArray();
            }

            // --- 2. Player settings obligatorios (solo Android) ---
            var grupo = BuildTargetGroup.Android;
            PlayerSettings.SetApplicationIdentifier(grupo, PackageId);
            PlayerSettings.SetScriptingBackend(grupo, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersion.AndroidApiLevel22;
            PlayerSettings.bundleVersion = BundleVersion;

            // Orientación: se fija en PlayerSettings (LandscapeLeft); Unity la
            // vuelca al AndroidManifest del APK compilado.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            // --- 3. Guardar escena abierta que haya cambiado ---
            if (EditorSceneManager.GetActiveScene().path == ScenePath)
                EditorSceneManager.SaveOpenScenes();

            // --- 4. Ruta de salida build/ (raíz del proyecto) ---
            string raiz = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string buildDir = Path.Combine(raiz, "build");
            if (!Directory.Exists(buildDir))
                Directory.CreateDirectory(buildDir);
            string apk = Path.Combine(buildDir, "EdificioComplejo_MCOC.apk");

            // --- 5. Compilar (interactivo, sin batchmode) ---
            var report = BuildPipeline.BuildPlayer(
                new[] { ScenePath }, apk, BuildTarget.Android, BuildOptions.None);

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[MCOC] APK compilado: {apk} " +
                          $"(package {PackageId}, minSdk 22, landscape, v{BundleVersion}).");
                EditorUtility.RevealInFinder(apk);
            }
            else
            {
                Debug.LogError($"[MCOC] Falló el build Android. Ver BuildReport en Console.");
                EditorUtility.DisplayDialog("MCOC Build Android",
                    "Falló la compilación. Revisa la Consola (BuildReport).", "Aceptar");
            }
        }
    }
}