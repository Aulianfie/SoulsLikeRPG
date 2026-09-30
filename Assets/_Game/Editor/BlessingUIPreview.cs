using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class BlessingUIPreview
{
    public static void CaptureRuntime(string output)
    {
        Camera camera = Camera.main;
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>().Where(c=>c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        Camera[] cameras = canvases.Select(c=>c.worldCamera).ToArray();
        float[] distances = canvases.Select(c=>c.planeDistance).ToArray();
        RenderTexture priorTarget = camera.targetTexture, priorActive = RenderTexture.active;
        float priorAspect = camera.aspect;
        var target = new RenderTexture(1920,1080,24);
        var texture = new Texture2D(1920,1080,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target; camera.aspect=1920f/1080f;
            foreach(Canvas canvas in canvases)
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera;
                canvas.planeDistance=camera.nearClipPlane+.5f;
            }
            Canvas.ForceUpdateCanvases();
            foreach(TMP_Text text in canvases.SelectMany(c=>c.GetComponentsInChildren<TMP_Text>())) text.ForceMeshUpdate();
            camera.Render(); RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply(); File.WriteAllBytes(output,texture.EncodeToPNG());
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++)
            {
                canvases[i].renderMode=RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera=cameras[i]; canvases[i].planeDistance=distances[i];
            }
            camera.targetTexture=priorTarget; camera.aspect=priorAspect; RenderTexture.active=priorActive;
            Object.DestroyImmediate(target); Object.DestroyImmediate(texture);
        }
    }

    public static void Render(string prefabPath)
    {
        Directory.CreateDirectory("Docs");
        foreach (BlessingPage page in new[] { BlessingPage.None, BlessingPage.Attributes, BlessingPage.Equipment })
            RenderPage(prefabPath, page, 1920, 1080);
        RenderPage(prefabPath, BlessingPage.Attributes, 1280, 720);
        RenderPage(prefabPath, BlessingPage.Attributes, 1280, 960);
        RenderPage(prefabPath, BlessingPage.Attributes, 2560, 1080);
    }

    private static void RenderPage(string prefabPath, BlessingPage page, int width, int height)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        GameObject cameraObject = new GameObject("BlessingPreviewCamera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, preview);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.scene = preview; camera.enabled = false;
        camera.transform.position = new Vector3(0,0,-10);
        camera.orthographic = true; camera.orthographicSize = 540;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.085f,.10f,.11f);
        RenderTexture target = new RenderTexture(width,height,24);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), preview);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            CanvasGroup group = root.GetComponent<CanvasGroup>(); group.alpha = 1;
            BlessingMenuRoot menu = root.GetComponent<BlessingMenuRoot>();
            root.transform.Find("UnifiedPanel").gameObject.SetActive(true);
            menu.ShowPage(page);
            if (page == BlessingPage.Attributes)
            {
                LevelUpPanel view = root.GetComponentInChildren<LevelUpPanel>();
                PlayerProgression player = Object.FindObjectOfType<PlayerProgression>();
                int coins = player.GetComponent<SoulWallet>().CurrentSouls;
                view.SetSummary(coins, player.Level, player.UpgradeCost, coins >= player.UpgradeCost, "请选择属性并确认升级。");
                for (int i = 0; i < 3; i++) view.SetStatPreview((StatType)i, player.GetUpgradePreview((StatType)i), i==0);
            }
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            camera.Render(); RenderTexture.active = target;
            Texture2D texture = new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
                File.WriteAllBytes($"Docs/BlessingUI_{page}_{width}x{height}.png",texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }
        finally
        {
            RenderTexture.active = previous; Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
