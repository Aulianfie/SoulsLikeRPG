using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class EquipmentHUDSetup
{
    public const string WeaponPrefabPath = "Assets/_Game/UI/Prefabs/PF_WeaponSlotUI.prefab";
    public const string FlaskPrefabPath = "Assets/_Game/UI/Prefabs/PF_HealingFlaskUI.prefab";
    public const string TextureFolder = "Assets/_Game/UI/Textures/";
    private const float SlotSize = 144f;

    [MenuItem("Tools/SoulsLike RPG/UI/Build Weapon And Item Slots")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play Mode。");
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity")
            throw new InvalidOperationException("请在主场景中执行槽位设置。");
        PlayerEquipment equipment = Object.FindObjectsOfType<PlayerEquipment>(true).Single(p => p.gameObject.scene == scene);
        HealingFlaskPresenter flaskPresenter = Object.FindObjectsOfType<HealingFlaskPresenter>(true).Single(p => p.gameObject.scene == scene);
        Canvas canvas = flaskPresenter.GetComponentInParent<Canvas>();
        bool wasDirty = scene.isDirty;
        Sprite frame = ImportSprite(TextureFolder + "EquipmentSlot_Frame.png");
        RefreshWeaponIcons();

        GameObject flask = PrefabUtility.LoadPrefabContents(FlaskPrefabPath);
        try
        {
            ConfigureFlask(flask, frame);
            PrefabUtility.SaveAsPrefabAsset(flask, FlaskPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(flask); }

        GameObject weapon = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabPath);
        bool existingWeapon = weapon != null;
        GameObject weaponContent = weapon != null ? PrefabUtility.LoadPrefabContents(WeaponPrefabPath) :
            new GameObject("WeaponSlotUI", typeof(RectTransform), typeof(CanvasGroup), typeof(WeaponSlotView));
        try
        {
            ConfigureWeapon(weaponContent, frame);
            weapon = PrefabUtility.SaveAsPrefabAsset(weaponContent, WeaponPrefabPath);
        }
        finally
        {
            if (existingWeapon) PrefabUtility.UnloadPrefabContents(weaponContent);
            else Object.DestroyImmediate(weaponContent);
        }

        Undo.RecordObjects(flaskPresenter.GetComponentsInChildren<Component>(true), "Restyle item slot");
        ConfigureFlask(flaskPresenter.gameObject, frame);
        foreach (Component component in flaskPresenter.GetComponentsInChildren<Component>(true))
            if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);

        Transform weaponTransform = canvas.transform.Find("WeaponSlotUI");
        if (weaponTransform == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(weapon, canvas.transform);
            instance.name = "WeaponSlotUI";
            Undo.RegisterCreatedObjectUndo(instance, "Add current weapon slot");
            weaponTransform = instance.transform;
        }
        ConfigureWeapon(weaponTransform.gameObject, frame);
        WeaponSlotPresenter presenter = weaponTransform.GetComponent<WeaponSlotPresenter>() ?? Undo.AddComponent<WeaponSlotPresenter>(weaponTransform.gameObject);
        var presenterData = new SerializedObject(presenter);
        presenterData.FindProperty("_equipment").objectReferenceValue = equipment;
        presenterData.ApplyModifiedProperties();
        foreach (Component component in weaponTransform.GetComponentsInChildren<Component>(true))
            if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        RenderPreview();
        bool saved = !wasDirty && EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/EquipmentHUD_Setup.txt", $"Scene={scene.path}\nOriginallyDirty={wasDirty}\nSceneSaved={saved}\nCanvas={canvas.name}\nWeapon=48,64; Item=204,64; Size=144x144; Gap=12\n");
    }

    public static void RefreshWeaponIcons()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = EditorSceneManager.GetActiveScene();
        PlayerEquipment equipment = Object.FindObjectsOfType<PlayerEquipment>(true).Single(p => p.gameObject.scene == scene);
        SerializedProperty slots = new SerializedObject(equipment).FindProperty("_slots");
        foreach (string id in new[] { "LongSword", "GreatSword" })
        {
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Game/Configs/Weapons/WD_" + id + ".asset");
            string iconPath = TextureFolder + "Weapon_" + id + "_Icon.png";
            GameObject source = data.WeaponPrefab;
            for (int i = 0; i < slots.arraySize; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                if (slot.FindPropertyRelative("Data").objectReferenceValue == data &&
                    slot.FindPropertyRelative("Hitbox").objectReferenceValue is WeaponHitbox hitbox)
                    source = hitbox.gameObject;
            }
            RenderWeaponIcon(source, iconPath);
            var serialized = new SerializedObject(data);
            serialized.FindProperty("_icon").objectReferenceValue = ImportSprite(iconPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
    }

    public static void SaveHUDScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Game/Scenes/03_AncientDungeon_Checkpoint.unity")
            throw new InvalidOperationException("只能保存 Edit Mode 的主场景 HUD。");
        EditorSceneManager.SaveScene(scene);
    }

    public static void ValidateSavedHUD()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty) throw new InvalidOperationException("HUD 场景应已保存。");
        if (scene.GetRootGameObjects().Any(root => root.GetComponentsInChildren<Component>(true).Any(c => c == null)))
            throw new InvalidOperationException("场景中有缺失脚本。");
        var equipment = Object.FindObjectsOfType<PlayerEquipment>(true).Single(p => p.gameObject.scene == scene);
        var presenter = Object.FindObjectsOfType<WeaponSlotPresenter>(true).Single(p => p.gameObject.scene == scene);
        if (new SerializedObject(presenter).FindProperty("_equipment").objectReferenceValue != equipment)
            throw new InvalidOperationException("武器 HUD 引用丢失。");
        foreach (string id in new[] { "LongSword", "GreatSword" })
        {
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Game/Configs/Weapons/WD_" + id + ".asset");
            if (data.Icon == null || AssetDatabase.GetAssetPath(data.Icon) != TextureFolder + "Weapon_" + id + "_Icon.png")
                throw new InvalidOperationException("武器图标引用无效：" + id);
        }
        foreach (string path in new[] { WeaponPrefabPath, FlaskPrefabPath })
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab.GetComponentsInChildren<Component>(true).Any(c => c == null))
                throw new InvalidOperationException("UI 预制体有缺失脚本：" + path);
            if (prefab.GetComponentsInChildren<Image>().Any(i => i.sprite == null))
                throw new InvalidOperationException("UI 图标或槽框引用丢失：" + path);
        }
        File.WriteAllText("Logs/EquipmentHUD_AssetValidation.txt", "PASS: Saved main scene; no missing scripts; one weapon presenter wired to scene equipment; both weapon icons imported and assigned; both HUD prefabs have valid scripts and sprites.\n");
    }

    private static void ConfigureFlask(GameObject root, Sprite frame)
    {
        SetRoot(root, new Vector2(204, 64));
        foreach (string old in new[] { "GoldBorder", "Background", "Divider", "Label" })
        {
            Transform child = root.transform.Find(old);
            if (child != null) child.gameObject.SetActive(false);
        }
        Image border = EnsureImage(root.transform, "SlotFrame", new Vector2(72, 72), Vector2.one * SlotSize);
        border.sprite = frame;
        border.transform.SetAsFirstSibling();
        Image icon = root.transform.Find("FlaskIcon").GetComponent<Image>();
        SetRect(icon.rectTransform, root.transform, new Vector2(72, 84), new Vector2(98, 98));
        icon.preserveAspect = true;
        TMP_Text count = root.transform.Find("Count").GetComponent<TMP_Text>();
        SetRect(count.rectTransform, root.transform, new Vector2(72, 29), new Vector2(114, 30));
        count.fontSize = 23;
        count.alignment = TextAlignmentOptions.Center;
        count.color = new Color(0.95f, 0.90f, 0.73f);
        count.transform.SetAsLastSibling();
    }

    private static void ConfigureWeapon(GameObject root, Sprite frame)
    {
        SetRoot(root, new Vector2(48, 64));
        Image border = EnsureImage(root.transform, "SlotFrame", new Vector2(72, 72), Vector2.one * SlotSize);
        border.sprite = frame;
        border.transform.SetAsFirstSibling();
        Image icon = EnsureImage(root.transform, "WeaponIcon", new Vector2(72, 72), new Vector2(114, 114));
        icon.sprite = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Game/Configs/Weapons/WD_LongSword.asset").Icon;
        icon.preserveAspect = true;
        var view = new SerializedObject(root.GetComponent<WeaponSlotView>());
        view.FindProperty("_icon").objectReferenceValue = icon;
        view.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRoot(GameObject root, Vector2 position)
    {
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = Vector2.one * SlotSize;
        CanvasGroup group = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
    }

    private static Image EnsureImage(Transform parent, string name, Vector2 position, Vector2 size)
    {
        Transform child = parent.Find(name);
        GameObject go = child != null ? child.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
        Image image = go.GetComponent<Image>();
        SetRect(image.rectTransform, parent, position, size);
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static void SetRect(RectTransform rect, Transform parent, Vector2 position, Vector2 size)
    {
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static Sprite ImportSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 512;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // 从实际预制体离线烘焙图标，避免 HUD 中显示另一种武器造型。
    private static void RenderWeaponIcon(GameObject prefab, string output)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        GameObject actor = Object.Instantiate(prefab);
        SceneManager.MoveGameObjectToScene(actor, preview);
        GameObject cameraObject = new GameObject("WeaponIconCamera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, preview);
        RenderTexture target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        try
        {
            foreach (Transform transform in actor.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
            foreach (MonoBehaviour script in actor.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            actor.SetActive(true);
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Renderer[] renderers = actor.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.x > bounds.size.y) actor.transform.rotation = Quaternion.Euler(0, 0, 90);
            else if (bounds.size.z > bounds.size.y) actor.transform.rotation = Quaternion.Euler(-90, 0, 0);
            bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.scene = preview;
            camera.enabled = false;
            camera.cullingMask = 1 << 31;
            camera.orthographic = true;
            camera.orthographicSize = bounds.size.y * 0.58f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            Vector3 direction = bounds.size.x < bounds.size.z ? new Vector3(-1, 0, -0.08f) : new Vector3(-0.08f, 0, -1);
            camera.transform.position = bounds.center + direction.normalized * 4;
            camera.transform.LookAt(bounds.center);
            camera.transform.Rotate(0, 0, -28, Space.Self);
            camera.targetTexture = target;
            foreach (float intensity in new[] { 1.15f, 0.45f })
            {
                GameObject lightObject = new GameObject("IconLight", typeof(Light));
                SceneManager.MoveGameObjectToScene(lightObject, preview);
                Light light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = intensity;
                light.cullingMask = 1 << 31;
                light.transform.rotation = camera.transform.rotation * Quaternion.Euler(intensity > 1 ? 25 : -20, intensity > 1 ? -35 : 35, 0);
            }
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                texture.Apply();
                File.WriteAllBytes(output, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    public static void RenderPreview()
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        GameObject cameraObject = new GameObject("EquipmentPreviewCamera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, preview);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.scene = preview;
        camera.enabled = false;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true;
        camera.orthographicSize = 140;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.09f, .105f, .12f);
        RenderTexture target = new RenderTexture(720, 560, 24);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            var canvasObject = new GameObject("PreviewCanvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasObject, preview);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            for (int row = 0; row < 2; row++)
            {
                var weapon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabPath), canvas.transform);
                var flask = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FlaskPrefabPath), canvas.transform);
                weapon.GetComponent<WeaponSlotView>().SetWeapon(AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/_Game/Configs/Weapons/WD_" + (row == 0 ? "LongSword" : "GreatSword") + ".asset"));
                flask.GetComponent<HealingFlaskView>().SetCharges(row == 0 ? 3 : 1, 3);
                foreach (GameObject root in new[] { weapon, flask })
                {
                    RectTransform rect = root.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = new Vector2(root == weapon ? -128 : 128, row == 0 ? 135 : -135);
                    rect.localScale = Vector3.one * 1.65f;
                }
            }
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(720, 560, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, 720, 560), 0, 0);
                texture.Apply();
                File.WriteAllBytes("Docs/EquipmentHUD_Preview.png", texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    public static void CaptureRuntimeFrame()
    {
        Camera camera = Camera.main;
        if (!EditorApplication.isPlaying || camera == null) throw new InvalidOperationException("需要主相机和 Play Mode。");
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>().Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        Camera[] previousCameras = canvases.Select(c => c.worldCamera).ToArray();
        float[] previousDistances = canvases.Select(c => c.planeDistance).ToArray();
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        float previousAspect = camera.aspect;
        var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.aspect = 1920f / 1080f;
            foreach (Canvas canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 0.5f;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            texture.Apply();
            File.WriteAllBytes("Docs/EquipmentHUD_GameView.png", texture.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                canvases[i].worldCamera = previousCameras[i];
                canvases[i].planeDistance = previousDistances[i];
            }
            camera.targetTexture = previousTarget;
            camera.aspect = previousAspect;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(texture);
        }
    }
}
