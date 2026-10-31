using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;
using Imagine.WebAR;
using RayonixAR.Room;

namespace RayonixAR.RoomSetup
{
    /// <summary>
    /// Builds the Room Configurator AR scene (TV, chair, table) on the Imagine WebAR World Tracker.
    ///
    /// Menu:   Rayonix AR / Room Configurator / 1. Create Scene  ->  2. Build WebGL
    /// Batch:  Unity.exe -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod RayonixAR.RoomSetup.RoomConfiguratorSetup.BatchSetup
    ///         Unity.exe -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod RayonixAR.RoomSetup.RoomConfiguratorSetup.BatchBuild
    ///
    /// Everything (textures, materials, prefabs, scene, UI) is generated, so running step 1 again
    /// rebuilds it from scratch. Project-wide settings come from the existing ARTestSetup.ConfigureProject.
    /// </summary>
    public static class RoomConfiguratorSetup
    {
        private const string Root = "Assets/RayonixAR/Room";
        private const string MaterialDir = Root + "/Materials";
        private const string TextureDir = Root + "/Textures";
        private const string PrefabDir = Root + "/Resources/RoomItems"; // Resources: RoomConfigurator loads these by name
        private const string ScenePath = "Assets/Scenes/RoomConfigurator.unity";
        private const string BuildDir = "Builds/RoomWebGL";
        private const string WebDir = "webar/room";
        private const string WorldTrackerPrefab = "Assets/Imagine/WorldTracker/Prefabs/WorldTracker.prefab";
        private const string ARCameraPrefab = "Assets/Imagine/WorldTracker/Prefabs/ARCamera.prefab";

        // UI palette (matches the web version)
        private static readonly Color Glass = new Color(0.055f, 0.07f, 0.11f, 0.9f);
        private static readonly Color Chip = new Color(1f, 1f, 1f, 0.1f);
        private static readonly Color Accent = new Color(0.31f, 0.55f, 1f, 1f);
        private static readonly Color Danger = new Color(1f, 0.36f, 0.42f, 0.25f);
        private static readonly Color TextColor = new Color(0.95f, 0.96f, 0.98f, 1f);
        private static readonly Color Muted = new Color(0.6f, 0.65f, 0.74f, 1f);

        // ------------------------------------------------------------------ menu / batch

        [MenuItem("Rayonix AR/Room Configurator/1. Create Scene", false, 20)]
        public static void CreateScene()
        {
            var prefabs = BuildModels();
            BuildScene(prefabs);
        }

        [MenuItem("Rayonix AR/Room Configurator/2. Build WebGL", false, 21)]
        public static void BuildWebGL()
        {
            var report = DoBuild();
            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(Path.Combine(BuildDir, "index.html"));
        }

        public static void BatchSetup()
        {
            try
            {
                RayonixAR.Editor.ARTestSetup.ConfigureProject();
                CreateScene();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[RoomAR] Setup failed: " + e);
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
                Debug.LogError("[RoomAR] Build failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------ models

        private struct Prefabs
        {
            public RoomItem tv, chair, table;
            public Material selection;
        }

        private static Prefabs BuildModels()
        {
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory(TextureDir);
            Directory.CreateDirectory(PrefabDir);

            var tvPicture = SaveTexture("TvPicture", 512, 288, PaintTvPicture, TextureWrapMode.Clamp);
            var wood = SaveTexture("Wood", 512, 256, PaintWood, TextureWrapMode.Repeat);

            var tvBody = LitMaterial("TvBody", new Color(0.07f, 0.08f, 0.1f), 0.7f, 0.6f, null);
            var tvScreen = UnlitMaterial("TvScreen", Color.white, tvPicture);
            var fabric = LitMaterial("Fabric", new Color(0.18f, 0.5f, 0.46f), 0.12f, 0f, null);
            var legWood = LitMaterial("LegWood", new Color(0.6f, 0.39f, 0.21f), 0.45f, 0f, null);
            var tableTop = LitMaterial("TableTop", Color.white, 0.5f, 0f, wood);
            var metal = LitMaterial("Metal", new Color(0.14f, 0.15f, 0.17f), 0.6f, 0.7f, null);
            var selection = UnlitMaterial("Selection", new Color(0.96f, 0.73f, 0.26f), null);

            // TV, 43 inch class: 0.96 x 0.56 m, 3.5 cm deep. Pivot at the back centre, screen faces +Z.
            var tv = NewItem("TV", RoomItemKind.TV, "43 inch, wall", true, new Vector3(0, 0, 0.0175f), new Vector3(0.96f, 0.56f, 0.035f));
            Part(PrimitiveType.Cube, "Body", tv, new Vector3(0, 0, 0.0175f), new Vector3(0.96f, 0.56f, 0.035f), tvBody);
            var screen = Part(PrimitiveType.Quad, "Screen", tv, new Vector3(0, 0, 0.0356f), new Vector3(0.94f, 0.54f, 1f), tvScreen);
            screen.transform.localRotation = Quaternion.Euler(0, 180f, 0); // Quad faces -Z by default
            screen.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Dining chair, seat at 45 cm. Faces +Z, backrest at -Z.
            var chair = NewItem("Chair", RoomItemKind.Chair, "Dining chair", false, new Vector3(0, 0.455f, 0), new Vector3(0.46f, 0.91f, 0.46f));
            Part(PrimitiveType.Cube, "Seat", chair, new Vector3(0, 0.42f, 0), new Vector3(0.46f, 0.06f, 0.46f), fabric);
            foreach (var x in new[] { -0.19f, 0.19f })
            {
                foreach (var z in new[] { -0.19f, 0.19f })
                    Part(PrimitiveType.Cylinder, "Leg", chair, new Vector3(x, 0.21f, z), new Vector3(0.032f, 0.21f, 0.032f), legWood);
                Part(PrimitiveType.Cylinder, "Post", chair, new Vector3(x, 0.68f, -0.19f), new Vector3(0.03f, 0.23f, 0.03f), legWood);
            }
            var back = Part(PrimitiveType.Cube, "Backrest", chair, new Vector3(0, 0.79f, -0.2f), new Vector3(0.42f, 0.22f, 0.035f), fabric);
            back.transform.localRotation = Quaternion.Euler(-4.6f, 0, 0);

            // Dining table, 120 x 75 cm, 75 cm high.
            var table = NewItem("Table", RoomItemKind.Table, "120 x 75 cm", false, new Vector3(0, 0.375f, 0), new Vector3(1.2f, 0.75f, 0.75f));
            Part(PrimitiveType.Cube, "Top", table, new Vector3(0, 0.7325f, 0), new Vector3(1.2f, 0.035f, 0.75f), tableTop);
            foreach (var x in new[] { -0.54f, 0.54f })
                foreach (var z in new[] { -0.31f, 0.31f })
                    Part(PrimitiveType.Cube, "Leg", table, new Vector3(x, 0.3575f, z), new Vector3(0.045f, 0.715f, 0.045f), metal);
            foreach (var z in new[] { -0.33f, 0.33f })
                Part(PrimitiveType.Cube, "Apron", table, new Vector3(0, 0.7f, z), new Vector3(1.06f, 0.06f, 0.022f), legWood);
            foreach (var x in new[] { -0.55f, 0.55f })
                Part(PrimitiveType.Cube, "Apron", table, new Vector3(x, 0.7f, 0), new Vector3(0.022f, 0.06f, 0.6f), legWood);

            var result = new Prefabs
            {
                tv = SavePrefab(tv),
                chair = SavePrefab(chair),
                table = SavePrefab(table),
                selection = selection,
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[RoomAR] Built TV, chair and table prefabs in " + PrefabDir);
            return result;
        }

        private static RoomItem NewItem(string label, RoomItemKind kind, string detail, bool wall, Vector3 center, Vector3 size)
        {
            var go = new GameObject(label);
            var item = go.AddComponent<RoomItem>();
            item.kind = kind;
            item.label = label;
            item.detail = detail;
            item.wallItem = wall;
            item.boundsCenter = center;
            item.boundsSize = size;
            var box = go.AddComponent<BoxCollider>(); // for tap selection
            box.center = center;
            box.size = size;
            return item;
        }

        private static GameObject Part(PrimitiveType type, string name, RoomItem parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static RoomItem SavePrefab(RoomItem item)
        {
            var path = PrefabDir + "/" + item.label + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(item.gameObject, path);
            UnityEngine.Object.DestroyImmediate(item.gameObject);
            return saved != null ? AssetDatabase.LoadAssetAtPath<RoomItem>(path) : null;
        }

        private static Material LitMaterial(string name, Color color, float smoothness, float metallic, Texture2D map)
        {
            var mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            mat.SetTexture("_BaseMap", map);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material UnlitMaterial(string name, Color color, Texture2D map)
        {
            var mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Unlit");
            mat.SetColor("_BaseColor", color);
            mat.SetTexture("_BaseMap", map);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreateMaterial(string name, string shaderName)
        {
            var path = MaterialDir + "/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Shader not found: " + shaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            return mat;
        }

        // ------------------------------------------------------------------ textures (drawn in code)

        private static Texture2D SaveTexture(string name, int w, int h, Func<float, float, Color> paint, TextureWrapMode wrap)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = paint((x + 0.5f) / w, 1f - (y + 0.5f) / h); // (u, v) with v = 0 at the top
            tex.SetPixels(pixels);
            tex.Apply();

            var path = TextureDir + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = wrap;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        /// <summary>Sunset over mountains and a lake: bright and saturated so the TV reads as switched on.</summary>
        private static Color PaintTvPicture(float u, float v)
        {
            const float horizon = 0.7f;
            Color c;
            if (v < horizon)
            {
                float t = v / horizon;
                c = t < 0.45f ? Color.Lerp(Hex("#16206b"), Hex("#7b36a8"), t / 0.45f)
                  : t < 0.8f ? Color.Lerp(Hex("#7b36a8"), Hex("#ff7a59"), (t - 0.45f) / 0.35f)
                  : Color.Lerp(Hex("#ff7a59"), Hex("#ffd27a"), (t - 0.8f) / 0.2f);

                // Sun glow
                float dx = (u - 0.64f) * 16f / 9f;
                float dy = v - (horizon - 0.08f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float glow = Mathf.Clamp01(1f - d / 0.32f);
                c = Color.Lerp(c, Hex("#fff3cf"), glow * glow);

                // Three mountain ridges, back to front
                if (v > Ridge(u, 0.47f, 0.56f, 1.3f, 0.2f)) c = Hex("#5b3389");
                if (v > Ridge(u, 0.56f, 0.64f, 2.1f, 1.1f)) c = Hex("#3a2266");
                if (v > Ridge(u, 0.65f, 0.69f, 3.3f, 2.4f)) c = Hex("#1f1440");
            }
            else
            {
                float t = (v - horizon) / (1f - horizon);
                c = Color.Lerp(Hex("#3a2a70"), Hex("#0d0a24"), t);
                // Sun reflection streaks
                float halfWidth = 0.09f * (1f - t * 0.8f);
                bool streak = Mathf.Abs(u - 0.64f) < halfWidth && Mathf.Repeat(t * 15f, 1f) < 0.45f;
                if (streak) c = Color.Lerp(c, Hex("#ffbe82"), 0.7f * (1f - t));
            }
            // Faint glass glare
            float glare = Mathf.Clamp01(0.35f - (u * 0.6f + v * 0.4f)) * 0.3f;
            return Color.Lerp(c, Color.white, glare);
        }

        /// <summary>Ridge height (v from the top) at u: a sum of sines between min and max.</summary>
        private static float Ridge(float u, float min, float max, float freq, float phase)
        {
            float s = 0.5f + 0.35f * Mathf.Sin(u * Mathf.PI * 2f * freq + phase) + 0.15f * Mathf.Sin(u * Mathf.PI * 2f * freq * 2.7f + phase * 1.7f);
            return Mathf.Lerp(min, max, Mathf.Clamp01(s));
        }

        /// <summary>Oak-like grain along u.</summary>
        private static Color PaintWood(float u, float v)
        {
            float warp = Mathf.PerlinNoise(u * 3f, v * 8f) * 0.08f;
            float rings = Mathf.Sin((v + warp) * 140f) * 0.5f + 0.5f;
            float fine = Mathf.PerlinNoise(u * 40f, v * 300f);
            var light = Hex("#c08a55");
            var dark = Hex("#8a5a30");
            var c = Color.Lerp(light, dark, rings * 0.55f + fine * 0.25f);
            return c;
        }

        // ------------------------------------------------------------------ scene

        private static void BuildScene(Prefabs prefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            // Names must stay "ARCamera" and "WorldTracker": the JS template sends messages to them by name.
            var arCamGo = InstantiateUnpacked(ARCameraPrefab, "ARCamera");
            var cam = arCamGo.GetComponent<Camera>();
            cam.GetUniversalAdditionalCameraData();
            var debugCam = arCamGo.transform.Find("DebugCam");
            if (debugCam != null) debugCam.gameObject.SetActive(false);

            var trackerGo = InstantiateUnpacked(WorldTrackerPrefab, "WorldTracker");
            var tracker = trackerGo.GetComponent<WorldTracker>();
            var so = new SerializedObject(tracker);
            so.FindProperty("trackerCamera").objectReferenceValue = cam;
            so.FindProperty("mode").enumValueIndex = (int)WorldTracker.TrackingMode.MODE_6DOF;
            so.FindProperty("usePlacementIndicator").boolValue = true;
            so.FindProperty("s3dof.useExtraSmoothing").boolValue = true;
            so.FindProperty("s3dof.smoothenFactor").floatValue = 10f;
            so.FindProperty("s6dof.maxPixels").intValue = 550;
            so.FindProperty("s6dof.maxPoints").intValue = 160;
            so.FindProperty("placementIndicatorSettings.minZ").floatValue = 0.5f;
            so.FindProperty("placementIndicatorSettings.maxZ").floatValue = 3f;
            so.FindProperty("placeAtIndicatorDistance").boolValue = true;
            so.ApplyModifiedProperties();

            ConfigureTrackerInteraction(trackerGo, tracker, cam);
            UseInputSystemUiModule();
            ReplaceTmpLabelsWithLegacyText();

            // Content: drop the demo cube and axis markers, enlarge the AR shadow plane, add the item root.
            var mainObject = tracker.mainObject != null ? tracker.mainObject.transform : trackerGo.transform.Find("MainObject");
            var content = mainObject.Find("Content");
            if (content == null) content = mainObject;
            foreach (var name in new[] { "Cube", "X", "Y", "Z" })
            {
                var t = content.Find(name);
                if (t == null) t = mainObject.Find(name);
                if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
            var shadowPlane = FindDeep(mainObject, "Shadow Plane");
            if (shadowPlane != null) shadowPlane.localScale *= 4f; // items spread around the origin
            var itemRoot = new GameObject("Items").transform;
            itemRoot.SetParent(content, false);

            MovePlacementResetButton(trackerGo);

            var ui = BuildUi(prefabs);
            var configGo = new GameObject("RoomConfigurator");
            var config = configGo.AddComponent<RoomConfigurator>();
            WireConfigurator(config, tracker, cam, itemRoot, prefabs, ui);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[RoomAR] Created " + ScenePath);
        }

        /// <summary>
        /// Removes Imagine's whole-scene touch gestures (pan, pinch, swipe): they move or scale the entire
        /// tracked content when a thumb rests on the screen. Routes Place through PlaceOriginAtIndicator
        /// and adds the project's pose smoother, as in the Diwali test scene.
        /// </summary>
        private static void ConfigureTrackerInteraction(GameObject trackerGo, WorldTracker tracker, Camera cam)
        {
            foreach (var button in trackerGo.GetComponentsInChildren<Button>(true))
            {
                var onClick = button.onClick;
                for (int i = onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    var target = onClick.GetPersistentTarget(i);
                    var method = onClick.GetPersistentMethodName(i);
                    if (target is TwoFingerPan || target is PinchToScale || target is SwipeToRotateY)
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

            foreach (var c in trackerGo.GetComponentsInChildren<TwoFingerPan>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var c in trackerGo.GetComponentsInChildren<PinchToScale>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var c in trackerGo.GetComponentsInChildren<SwipeToRotateY>(true)) UnityEngine.Object.DestroyImmediate(c);

            var smoother = trackerGo.AddComponent<TrackingSmoother>();
            var sso = new SerializedObject(smoother);
            sso.FindProperty("tracker").objectReferenceValue = tracker;
            sso.FindProperty("cameraTransform").objectReferenceValue = cam.transform;
            sso.FindProperty("scaledObject").objectReferenceValue = tracker.mainObject != null ? tracker.mainObject.transform : null;
            sso.ApplyModifiedProperties();
        }

        private static void UseInputSystemUiModule()
        {
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem == null)
                eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            var legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>TMP font resources do not load in a headless import, so plugin labels become legacy Text.</summary>
        private static void ReplaceTmpLabelsWithLegacyText()
        {
            foreach (var tmp in UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = tmp.gameObject;
                string text = tmp.text;
                float size = tmp.fontSize;
                Color color = tmp.color;
                UnityEngine.Object.DestroyImmediate(tmp);
                var label = go.AddComponent<Text>();
                label.font = UiFont;
                label.text = text;
                label.fontSize = Mathf.RoundToInt(size);
                label.color = color;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.raycastTarget = false;
            }
        }

        /// <summary>Moves the plugin's Reset button to the top right so it never sits under the item panels.</summary>
        private static void MovePlacementResetButton(GameObject trackerGo)
        {
            var reset = FindDeep(trackerGo.transform, "Reset Button") as RectTransform;
            if (reset == null) return;
            reset.anchorMin = reset.anchorMax = new Vector2(1f, 1f);
            reset.pivot = new Vector2(1f, 1f);
            reset.anchoredPosition = new Vector2(-24f, -24f);
            reset.sizeDelta = new Vector2(220f, 72f);
            var label = reset.GetComponentInChildren<Text>(true);
            if (label != null) label.text = "Re-place";
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

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // ------------------------------------------------------------------ UI

        private class Ui
        {
            public GameObject cataloguePanel, itemPanel;
            public Text status, guide, itemTitle;
            public Button addTv, addChair, addTable, clear, catalogueUndo;
            public Button left, up, right, closer, down, farther, turnLeft, faceMe, turnRight, smaller, duplicate, bigger;
            public Button done, itemUndo, delete;
        }

        private static Font UiFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static Sprite RoundSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        private static Ui BuildUi(Prefabs prefabs)
        {
            var ui = new Ui();

            var canvasGo = new GameObject("Room UI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 1400f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Status pill, top left
            var status = Box("Status", canvasGo.transform, Glass);
            Anchor(status, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(300, 72));
            status.GetComponent<Image>().raycastTarget = false;
            ui.status = Label(status, "Find the floor", 28, TextAnchor.MiddleCenter, TextColor, FontStyle.Bold);

            // Bottom stack: guide, catalogue, item panel
            var bottom = new GameObject("Bottom", typeof(RectTransform)).GetComponent<RectTransform>();
            bottom.SetParent(canvasGo.transform, false);
            bottom.anchorMin = bottom.anchorMax = new Vector2(0.5f, 0f);
            bottom.pivot = new Vector2(0.5f, 0f);
            bottom.anchoredPosition = new Vector2(0, 24);
            bottom.sizeDelta = new Vector2(760, 0);
            bottom.gameObject.AddComponent<UiMaxWidth>();
            var vlg = bottom.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            bottom.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var guide = Box("Guide", bottom, Glass);
            guide.GetComponent<Image>().raycastTarget = false;
            Pad(guide.gameObject, 18, 14);
            ui.guide = Label(guide, "", 26, TextAnchor.MiddleCenter, TextColor, FontStyle.Normal);
            ui.guide.horizontalOverflow = HorizontalWrapMode.Wrap;
            Grow(ui.guide.gameObject);

            // Catalogue
            var cat = Box("Catalogue", bottom, Glass);
            ui.cataloguePanel = cat.gameObject;
            Stack(cat.gameObject, 16, 12);
            var cards = Row("Items", cat, 12, 130);
            ui.addTv = MakeButton(cards, prefabs.tv.label + "\n<size=22>" + prefabs.tv.detail + "</size>", Accent, 30);
            ui.addChair = MakeButton(cards, prefabs.chair.label + "\n<size=22>" + prefabs.chair.detail + "</size>", Accent, 30);
            ui.addTable = MakeButton(cards, prefabs.table.label + "\n<size=22>" + prefabs.table.detail + "</size>", Accent, 30);
            var catFoot = Row("Actions", cat, 12, 76);
            ui.catalogueUndo = MakeButton(catFoot, "Undo", Chip, 26);
            ui.clear = MakeButton(catFoot, "Clear all", Danger, 26);

            // Selected item panel
            var panel = Box("Item Panel", bottom, Glass);
            ui.itemPanel = panel.gameObject;
            Stack(panel.gameObject, 16, 10);
            var head = Row("Header", panel, 12, 72);
            ui.itemTitle = Label(head, "TV 1", 30, TextAnchor.MiddleLeft, TextColor, FontStyle.Bold);
            ui.itemTitle.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            ui.done = MakeButton(head, "Done", Accent, 28);
            ui.done.GetComponent<LayoutElement>().flexibleWidth = 0;
            ui.done.GetComponent<LayoutElement>().preferredWidth = 180;

            var gridGo = new GameObject("Controls", typeof(RectTransform));
            gridGo.transform.SetParent(panel, false);
            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(205, 80);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;
            var g = gridGo.transform;
            ui.left = MakeButton(g, "Left", Chip, 26);
            ui.up = MakeButton(g, "Up", Chip, 26);
            ui.right = MakeButton(g, "Right", Chip, 26);
            ui.closer = MakeButton(g, "Closer", Chip, 26);
            ui.down = MakeButton(g, "Down", Chip, 26);
            ui.farther = MakeButton(g, "Farther", Chip, 26);
            ui.turnLeft = MakeButton(g, "Turn left", Chip, 26);
            ui.faceMe = MakeButton(g, "Face me", Chip, 26);
            ui.turnRight = MakeButton(g, "Turn right", Chip, 26);
            ui.smaller = MakeButton(g, "Smaller", Chip, 26);
            ui.duplicate = MakeButton(g, "Duplicate", Chip, 26);
            ui.bigger = MakeButton(g, "Bigger", Chip, 26);

            var foot = Row("Footer", panel, 12, 76);
            ui.itemUndo = MakeButton(foot, "Undo", Chip, 26);
            ui.delete = MakeButton(foot, "Delete", Danger, 26);

            ui.cataloguePanel.SetActive(false);
            ui.itemPanel.SetActive(false);
            return ui;
        }

        private static RectTransform Box(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = RoundSprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            return (RectTransform)go.transform;
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static Text Label(Transform parent, string text, int size, TextAnchor anchor, Color color, FontStyle style)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = UiFont;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = color;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Lets a label inside a layout group report its wrapped height.</summary>
        private static void Grow(GameObject label)
        {
            var le = label.AddComponent<LayoutElement>();
            le.minHeight = 64;
        }

        private static void Pad(GameObject go, int horizontal, int vertical)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(horizontal, horizontal, vertical, vertical);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void Stack(GameObject go, int padding, int spacing)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
        }

        private static Transform Row(string name, Transform parent, int spacing, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            return go.transform;
        }

        private static Button MakeButton(Transform parent, string text, Color bg, int fontSize)
        {
            var rt = Box(text.Split('\n')[0], parent, bg);
            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.85f, 1f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            button.colors = colors;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            Label(rt, text, fontSize, TextAnchor.MiddleCenter, TextColor, FontStyle.Bold);
            return button;
        }

        // ------------------------------------------------------------------ wiring

        private static void WireConfigurator(RoomConfigurator config, WorldTracker tracker, Camera cam, Transform itemRoot, Prefabs p, Ui ui)
        {
            var so = new SerializedObject(config);
            so.FindProperty("tracker").objectReferenceValue = tracker;
            so.FindProperty("arCamera").objectReferenceValue = cam;
            so.FindProperty("itemRoot").objectReferenceValue = itemRoot;
            so.FindProperty("tvPrefab").objectReferenceValue = p.tv;
            so.FindProperty("chairPrefab").objectReferenceValue = p.chair;
            so.FindProperty("tablePrefab").objectReferenceValue = p.table;
            so.FindProperty("selectionMaterial").objectReferenceValue = p.selection;
            so.FindProperty("cataloguePanel").objectReferenceValue = ui.cataloguePanel;
            so.FindProperty("itemPanel").objectReferenceValue = ui.itemPanel;
            so.FindProperty("statusText").objectReferenceValue = ui.status;
            so.FindProperty("guideText").objectReferenceValue = ui.guide;
            so.FindProperty("itemTitle").objectReferenceValue = ui.itemTitle;
            so.FindProperty("clearButton").objectReferenceValue = ui.clear;
            var undoButtons = so.FindProperty("undoButtons");
            undoButtons.arraySize = 2;
            undoButtons.GetArrayElementAtIndex(0).objectReferenceValue = ui.catalogueUndo;
            undoButtons.GetArrayElementAtIndex(1).objectReferenceValue = ui.itemUndo;
            so.ApplyModifiedProperties();

            On(ui.addTv, config.AddTV);
            On(ui.addChair, config.AddChair);
            On(ui.addTable, config.AddTable);
            On(ui.catalogueUndo, config.Undo);
            On(ui.clear, config.ClearAll);
            On(ui.left, config.MoveLeft);
            On(ui.up, config.MoveUp);
            On(ui.right, config.MoveRight);
            On(ui.closer, config.MoveCloser);
            On(ui.down, config.MoveDown);
            On(ui.farther, config.MoveFarther);
            On(ui.turnLeft, config.TurnLeft);
            On(ui.faceMe, config.FaceMe);
            On(ui.turnRight, config.TurnRight);
            On(ui.smaller, config.Smaller);
            On(ui.duplicate, config.Duplicate);
            On(ui.bigger, config.Bigger);
            On(ui.done, config.Done);
            On(ui.itemUndo, config.Undo);
            On(ui.delete, config.Delete);
        }

        private static void On(Button button, UnityAction action)
        {
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        // ------------------------------------------------------------------ build

        private static BuildReport DoBuild()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("Run 'Rayonix AR/Room Configurator/1. Create Scene' first: " + ScenePath);

            Directory.CreateDirectory(BuildDir);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log("[RoomAR] WebGL build " + s.result + ": " + (s.totalSize / (1024 * 1024)) + " MB, "
                      + s.totalErrors + " errors, " + s.totalWarnings + " warnings, " + s.totalTime.TotalSeconds.ToString("F0") + "s");

            if (s.result == BuildResult.Succeeded)
            {
                if (Directory.Exists(WebDir)) Directory.Delete(WebDir, true);
                CopyDirectory(BuildDir, WebDir);
                Debug.Log("[RoomAR] Build copied to " + WebDir);
            }
            return report;
        }

        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(src))
                CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
        }
    }
}
