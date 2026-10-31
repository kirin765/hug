using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Hug.EditorTools
{
    /// <summary>
    /// One-click project setup: creates the shared materials the runtime needs,
    /// puts GameBootstrap in the scene and applies WebGL player settings.
    /// Menu: HUG > Setup Project.
    /// </summary>
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string MaterialFolder = "Assets/Resources/Materials";

        [MenuItem("HUG/Setup Project")]
        public static void RunAll()
        {
            EnsureFolders();
            CreateMaterials();
            SetupScene();
            ConfigurePlayerSettings();
            ConfigureRenderPipeline();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HUG] Project setup complete.");
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Resources", "Materials");
        }

        // ------------------------------------------------------------ materials

        static void CreateMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");

            if (lit == null || unlit == null)
            {
                Debug.LogError("[HUG] URP shaders not found — is the URP package installed?");
                return;
            }

            // Player: opaque, lightly glossy, colour is tinted per player at runtime.
            var player = LoadOrCreateMaterial($"{MaterialFolder}/Player.mat", lit);
            player.SetColor("_BaseColor", Color.white);
            player.SetFloat("_Smoothness", 0.18f);
            player.SetFloat("_Metallic", 0f);
            player.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(player);

            // Ground: matte, checker texture is assigned at runtime.
            var ground = LoadOrCreateMaterial($"{MaterialFolder}/Ground.mat", lit);
            ground.SetColor("_BaseColor", Color.white);
            ground.SetFloat("_Smoothness", 0.05f);
            ground.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(ground);

            // Fx: unlit, alpha blended, double sided (hearts, shadows, rings).
            var fx = LoadOrCreateMaterial($"{MaterialFolder}/Fx.mat", unlit);
            fx.SetColor("_BaseColor", Color.white);
            fx.SetFloat("_Surface", 1f);
            fx.SetFloat("_Blend", 0f);
            fx.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            fx.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            fx.SetFloat("_ZWrite", 0f);
            fx.SetFloat("_Cull", 0f);
            fx.SetFloat("_AlphaClip", 0f);
            fx.SetOverrideTag("RenderType", "Transparent");
            fx.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            fx.DisableKeyword("_ALPHATEST_ON");
            fx.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(fx);

            AssetDatabase.SaveAssets();
        }

        static Material LoadOrCreateMaterial(string path, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        // ---------------------------------------------------------------- scene

        static void SetupScene()
        {
            var active = SceneManager.GetActiveScene();
            Scene scene = active.path == ScenePath
                ? active
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Object.DestroyImmediate(roots[i]);
            }

            var game = new GameObject("Game");
            game.AddComponent<GameBootstrap>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Make sure the scene is the only one in the build.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Selection.activeGameObject = game;
            Debug.Log("[HUG] Scene rebuilt with a single Game object.");
        }

        // ------------------------------------------------------------- settings

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Hug Club";
            PlayerSettings.productName = "HUG.IO";
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.SplashScreen.show = false;

            PlayerSettings.WebGL.template = "PROJECT:Hug";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithoutStacktrace;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        }

        static void ConfigureRenderPipeline()
        {
            var guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets/Settings" });
            if (guids.Length == 0)
            {
                Debug.LogWarning("[HUG] No URP asset found; skipping render pipeline tuning.");
                return;
            }

            UniversalRenderPipelineAsset first = null;

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (pipeline == null) continue;

                pipeline.shadowDistance = 70f;
                pipeline.supportsHDR = false;
                pipeline.msaaSampleCount = 1;
                EditorUtility.SetDirty(pipeline);

                if (first == null) first = pipeline;
            }

            // Make sure the project never falls back to the built-in pipeline.
            if (GraphicsSettings.defaultRenderPipeline == null && first != null)
            {
                GraphicsSettings.defaultRenderPipeline = first;
            }

            AssetDatabase.SaveAssets();
        }
    }
}
