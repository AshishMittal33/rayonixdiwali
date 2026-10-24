using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
using Imagine.WebAR;
using Imagine.WebAR.Samples;

namespace RayonixAR.Editor
{
    /// <summary>
    /// One-click setup for an Imagine WebAR World Tracker test scene and its WebGL build.
    ///
    /// Menu:   Rayonix AR / 1. Configure Project  ->  2. Create AR Test Scene  ->  3. Build WebGL
    /// Batch:  Unity.exe -batchmode -quit -projectPath . -executeMethod RayonixAR.Editor.ARTestSetup.BatchConfigureAndScene
    ///         Unity.exe -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod RayonixAR.Editor.ARTestSetup.BatchBuild
    /// </summary>
    public static class ARTestSetup
    {
        private const string ScenePath = "Assets/Scenes/ARTest.unity";
        private const string MaterialDir = "Assets/RayonixAR/Materials";
        private const string BuildDir = "Builds/WebGL";
        private const string WebDir = "webar/unity";
        private const string UrpDefine = "IMAGINE_URP";
        private const string WorldTrackerPrefab = "Assets/Imagine/WorldTracker/Prefabs/WorldTracker.prefab";
        private const string ARCameraPrefab = "Assets/Imagine/WorldTracker/Prefabs/ARCamera.prefab";
        private const string TmpEssentials = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        // ------------------------------------------------------------------ 1. Configure

        [MenuItem("Rayonix AR/1. Configure Project (URP + WebGL)", false, 1)]
        public static void ConfigureProject()
        {
            ConvertImagineMaterialsToUrp();
            AddDefine(NamedBuildTarget.WebGL, UrpDefine);
            AddDefine(NamedBuildTarget.Standalone, UrpDefine);

            PlayerSettings.WebGL.template = "PROJECT:wTracker";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.productName = "Rayonix Diwali AR";
            // Smaller wasm for faster mobile loads (the default "faster runtime" roughly doubles the size)
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);

            bool inputChanged = SetActiveInputHandlerBoth();
            ImportTmpEssentialsIfMissing();

            AssetDatabase.SaveAssets();
            Debug.Log("[RayonixAR] Project configured: wTracker template, compression disabled, IMAGINE_URP define, input handler = Both.");
            if (inputChanged)
                Debug.LogWarning("[RayonixAR] Active Input Handler changed to Both. Restart the Unity editor before building.");
        }

        private static void ConvertImagineMaterialsToUrp()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var shadowUrp = Shader.Find("Imagine/ARShadowURP");
            if (shadowUrp == null)
                shadowUrp = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Imagine/WorldTracker/Demos/Shaders/ARShadow URP.shader");

            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Imagine" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;
                if (mat.shader.name == "Standard" && lit != null) { mat.shader = lit; count++; }
                else if (mat.shader.name == "Imagine/ARShadow" && shadowUrp != null) { mat.shader = shadowUrp; count++; }
                EditorUtility.SetDirty(mat);
            }
            Debug.Log("[RayonixAR] Converted " + count + " Imagine materials to URP.");
        }

        private static void AddDefine(NamedBuildTarget target, string symbol)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (defines.Contains(symbol)) return;
            defines.Add(symbol);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
        }

        /// <summary>The Imagine scripts use the legacy Input class, so the project must allow it (2 = Both).</summary>
        private static bool SetActiveInputHandlerBoth()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (settings == null) { Debug.LogWarning("[RayonixAR] Could not load ProjectSettings.asset"); return false; }
            var so = new SerializedObject(settings);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null || prop.intValue == 2) return false;
            prop.intValue = 2;
            so.ApplyModifiedProperties();
            return true;
        }

        private static void ImportTmpEssentialsIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/TextMesh Pro/Resources/TMP Settings.asset") != null) return;
            if (!File.Exists(TmpEssentials)) { Debug.LogWarning("[RayonixAR] TMP Essential Resources package not found."); return; }
            AssetDatabase.ImportPackage(TmpEssentials, false);
            Debug.Log("[RayonixAR] Imported TMP Essential Resources (needed by the Imagine placement UI labels).");
        }

        // ------------------------------------------------------------------ 2. Scene

        [MenuItem("Rayonix AR/2. Create AR Test Scene", false, 2)]
        public static void CreateTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // AR camera (name must stay "ARCamera": the JS template sends messages to it by name)
            var arCamGo = InstantiateUnpacked(ARCameraPrefab, "ARCamera");
            var cam = arCamGo.GetComponent<Camera>();
            cam.GetUniversalAdditionalCameraData();              // make sure URP camera data is serialized
            var debugCam = arCamGo.transform.Find("DebugCam");
            if (debugCam != null) debugCam.gameObject.SetActive(false);

            // World tracker (name must stay "WorldTracker": the JS tracker sends messages to it by name)
            var trackerGo = InstantiateUnpacked(WorldTrackerPrefab, "WorldTracker");
            var tracker = trackerGo.GetComponent<WorldTracker>();
            var so = new SerializedObject(tracker);
            so.FindProperty("trackerCamera").objectReferenceValue = cam;
            so.FindProperty("mode").enumValueIndex = (int)WorldTracker.TrackingMode.MODE_6DOF;
            so.FindProperty("usePlacementIndicator").boolValue = true;
            // Stability tuning: smoother placement phase, more tracking budget, shorter placement range
            so.FindProperty("s3dof.useExtraSmoothing").boolValue = true;
            so.FindProperty("s3dof.smoothenFactor").floatValue = 10f;
            so.FindProperty("s6dof.maxPixels").intValue = 550;
            so.FindProperty("s6dof.maxPoints").intValue = 160;
            so.FindProperty("placementIndicatorSettings.minZ").floatValue = 0.5f;
            so.FindProperty("placementIndicatorSettings.maxZ").floatValue = 3f;
            so.FindProperty("placeAtIndicatorDistance").boolValue = true;
            so.ApplyModifiedProperties();

            ConfigureInteractionAndSmoothing(trackerGo, tracker, cam);

            // Use the new Input System UI module so the Place / Reset buttons work in every input mode
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem != null)
            {
                var legacy = eventSystem.GetComponent<StandaloneInputModule>();
                if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
                if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                    eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            // The plugin's Place / Reset labels are TextMeshPro and need the TMP font resources, which do not
            // load in a headless import. Swap them for legacy UI Text so the labels always render.
            ReplaceTmpLabelsWithLegacyText();

            // Content root under MainObject: drop the placeholder cube and axis markers, keep the shadow plane
            var mainObject = tracker.mainObject != null ? tracker.mainObject.transform : trackerGo.transform.Find("MainObject");
            var content = mainObject.Find("Content");
            if (content == null) content = mainObject;
            foreach (var name in new[] { "Cube", "X", "Y", "Z" })
            {
                var t = content.Find(name);
                if (t == null) t = mainObject.Find(name);
                if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
            }

            BuildDiya(content, cam);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[RayonixAR] Created " + ScenePath + " and set it as the only scene in Build Settings.");
        }

        /// <summary>
        /// Drops two-finger pan (dragging the object away from the origin makes 6DOF drift badly),
        /// routes the Place button through PlaceOriginAtIndicator, and adds pose smoothing.
        /// </summary>
        private static void ConfigureInteractionAndSmoothing(GameObject trackerGo, WorldTracker tracker, Camera cam)
        {
            var pan = trackerGo.GetComponent<TwoFingerPan>();

            foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var onClick = button.onClick;
                for (int i = onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    var target = onClick.GetPersistentTarget(i);
                    var method = onClick.GetPersistentMethodName(i);
                    if (pan != null && target == pan)
                    {
                        UnityEventTools.RemovePersistentListener(onClick, i);
                    }
                    else if (target == tracker && method == "PlaceOrigin")
                    {
                        UnityEventTools.RemovePersistentListener(onClick, i);
                        UnityEventTools.AddPersistentListener(onClick, tracker.PlaceOriginAtIndicator);
                    }
                }
            }

            if (pan != null) UnityEngine.Object.DestroyImmediate(pan);

            var smoother = trackerGo.AddComponent<TrackingSmoother>();
            var sso = new SerializedObject(smoother);
            sso.FindProperty("tracker").objectReferenceValue = tracker;
            sso.FindProperty("cameraTransform").objectReferenceValue = cam.transform;
            sso.FindProperty("scaledObject").objectReferenceValue = tracker.mainObject != null ? tracker.mainObject.transform : null;
            sso.ApplyModifiedProperties();
        }

        private static void ReplaceTmpLabelsWithLegacyText()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int count = 0;
            foreach (var tmp in UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = tmp.gameObject;
                string text = tmp.text;
                float size = tmp.fontSize;
                Color color = tmp.color;
                UnityEngine.Object.DestroyImmediate(tmp);

                var label = go.AddComponent<Text>();
                label.font = font;
                label.text = text;
                label.fontSize = Mathf.RoundToInt(size);
                label.color = color;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.raycastTarget = false;
                count++;
            }
            Debug.Log("[RayonixAR] Replaced " + count + " TextMeshPro labels with legacy UI Text.");
        }

        private static GameObject InstantiateUnpacked(string prefabPath, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new FileNotFoundException("Prefab not found: " + prefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            return go;
        }

        /// <summary>A clay diya (oil lamp) with a flickering flame and a "Happy Diwali" label.</summary>
        private static void BuildDiya(Transform parent, Camera arCamera)
        {
            Directory.CreateDirectory(MaterialDir);
            var clay   = MakeMaterial("Clay",      new Color(0.71f, 0.33f, 0.16f), 0.9f, 0f, null);
            var gold   = MakeMaterial("Gold",      new Color(0.95f, 0.70f, 0.25f), 0.35f, 0.6f, null);
            var wick   = MakeMaterial("Wick",      new Color(0.15f, 0.12f, 0.10f), 1f, 0f, null);
            var flameM = MakeMaterial("Flame",     new Color(1f, 0.62f, 0.15f), 0.6f, 0f, new Color(1f, 0.45f, 0.05f) * 3f);
            var coreM  = MakeMaterial("FlameCore", new Color(1f, 0.95f, 0.7f), 0.6f, 0f, new Color(1f, 0.9f, 0.5f) * 4f);

            var root = new GameObject("Diya");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;

            // Only the lamp spins; the greeting stays upright and billboards toward the camera
            var lamp = new GameObject("Lamp");
            lamp.transform.SetParent(root.transform, false);

            Primitive(PrimitiveType.Cylinder, "Bowl", lamp.transform, new Vector3(0, 0.06f, 0), new Vector3(0.45f, 0.06f, 0.45f), clay);
            Primitive(PrimitiveType.Cylinder, "Rim",  lamp.transform, new Vector3(0, 0.125f, 0), new Vector3(0.50f, 0.01f, 0.50f), clay);
            Primitive(PrimitiveType.Cylinder, "Oil",  lamp.transform, new Vector3(0, 0.13f, 0), new Vector3(0.40f, 0.005f, 0.40f), gold);
            Primitive(PrimitiveType.Cylinder, "Wick", lamp.transform, new Vector3(0, 0.17f, 0), new Vector3(0.03f, 0.035f, 0.03f), wick);

            var flame = Primitive(PrimitiveType.Sphere, "Flame", lamp.transform, new Vector3(0, 0.32f, 0), new Vector3(0.14f, 0.26f, 0.14f), flameM);
            flame.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var core = Primitive(PrimitiveType.Sphere, "Core", flame.transform, new Vector3(0, -0.15f, 0), new Vector3(0.5f, 0.5f, 0.5f), coreM);
            core.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lightGo = new GameObject("Flame Light");
            lightGo.transform.SetParent(lamp.transform, false);
            lightGo.transform.localPosition = new Vector3(0, 0.45f, 0);
            var pl = lightGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(1f, 0.70f, 0.35f);
            pl.intensity = 1.5f;
            pl.range = 3f;

            var textGo = new GameObject("Greeting");
            textGo.transform.SetParent(root.transform, false);
            textGo.transform.localPosition = new Vector3(0, 0.75f, 0);
            textGo.transform.localScale = Vector3.one * 0.05f;
            var tm = textGo.AddComponent<TextMesh>();
            tm.text = "Happy Diwali";
            tm.fontSize = 64;
            tm.characterSize = 1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.82f, 0.4f);
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textGo.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            var billboard = textGo.AddComponent<Billboard>();
            var bso = new SerializedObject(billboard);
            bso.FindProperty("mainCamera").objectReferenceValue = arCamera;
            bso.ApplyModifiedProperties();

            var anim = root.AddComponent<DiyaFlame>();
            var aso = new SerializedObject(anim);
            aso.FindProperty("flame").objectReferenceValue = flame.transform;
            aso.FindProperty("flameLight").objectReferenceValue = pl;
            aso.FindProperty("spinRoot").objectReferenceValue = lamp.transform;
            aso.ApplyModifiedProperties();
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static Material MakeMaterial(string name, Color baseColor, float smoothness, float metallic, Color? emission)
        {
            var path = MaterialDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ 3. Build

        [MenuItem("Rayonix AR/3. Build WebGL", false, 3)]
        public static void BuildWebGL()
        {
            var report = DoBuild();
            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(Path.Combine(BuildDir, "index.html"));
        }

        private static BuildReport DoBuild()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            Directory.CreateDirectory(BuildDir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log("[RayonixAR] WebGL build " + s.result + ": " + (s.totalSize / (1024 * 1024)) + " MB, "
                      + s.totalErrors + " errors, " + s.totalWarnings + " warnings, " + s.totalTime.TotalSeconds.ToString("F0") + "s");

            if (s.result == BuildResult.Succeeded)
                MirrorBuildToWebFolder();
            return report;
        }

        /// <summary>Copies the finished build into webar/unity so GitHub Pages can serve it.</summary>
        private static void MirrorBuildToWebFolder()
        {
            if (Directory.Exists(WebDir)) Directory.Delete(WebDir, true);
            CopyDirectory(BuildDir, WebDir);
            Debug.Log("[RayonixAR] Build copied to " + WebDir);
        }

        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(src))
                CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
        }

        // ------------------------------------------------------------------ Batch entry points

        public static void BatchConfigureAndScene()
        {
            try
            {
                ConfigureProject();
                CreateTestScene();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[RayonixAR] Setup failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static void BatchBuild()
        {
            try
            {
                var report = DoBuild();
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError("[RayonixAR] Build failed: " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
