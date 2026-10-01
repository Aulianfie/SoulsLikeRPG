using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class Day12Preview
{
    [MenuItem("Tools/SoulsLike RPG/Day12/3 Render Flask Preview")]
    public static void Render()
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        var cameraObject = new GameObject("FlaskPreviewCamera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, preview);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.scene = preview;
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 1.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.075f, .085f, .11f);
        camera.transform.position = new Vector3(0, 0, -8);
        camera.transform.LookAt(Vector3.zero);
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.cullingMask = 1 << 31;
        var target = new RenderTexture(1200, 760, 24);
        RenderTexture previous = RenderTexture.active;
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Items/PF_HealingFlask_Optimized.prefab");
            for (int i = 0; i < 2; i++)
            {
                GameObject actor = Object.Instantiate(source);
                SceneManager.MoveGameObjectToScene(actor, preview);
                foreach (Transform t in actor.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = 31;
                }

                Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Items/" + (i == 0 ? "M_HealingFlask_Optimized.mat" : "M_MPFlask_Blue.mat"));
                Renderer[] renderers = actor.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in renderers)
                {
                    r.sharedMaterials = r.sharedMaterials.Select(_ => material).ToArray();
                }

                actor.transform.rotation = Quaternion.Euler(0, 15, 0);
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers)
                {
                    bounds.Encapsulate(r.bounds);
                }

                actor.transform.localScale *= 2.35f / bounds.size.y;
                bounds = renderers[0].bounds;
                foreach (Renderer r in renderers)
                {
                    bounds.Encapsulate(r.bounds);
                }

                actor.transform.position += new Vector3(i == 0 ? -1.08f : 1.08f, 0, 0) - bounds.center;
            }

            foreach (float intensity in new[]
            {
                1.4f,
                .75f
            }
            )
            {
                var lightObject = new GameObject("FlaskPreviewLight", typeof(Light));
                SceneManager.MoveGameObjectToScene(lightObject, preview);
                Light light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.cullingMask = 1 << 31;
                light.intensity = intensity;
                light.transform.rotation = Quaternion.Euler(20, intensity > 1 ? -30 : 40, 0);
            }

            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1200, 760, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, 1200, 760), 0, 0);
                texture.Apply();
                Directory.CreateDirectory("Docs");
                File.WriteAllBytes("Docs/Day12_FlaskModels.png", texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
