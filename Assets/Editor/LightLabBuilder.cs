using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using CoolingGame.Presentation;

namespace CoolingGame.EditorTools
{
    /// <summary>
    /// 生成一个「真 2D 光照」实验场景：桌面 + 几张卡 + 一盏台灯（Light2D 点光源）+ ShadowCaster2D。
    /// 菜单：CoolingGame/Build Light Lab Scene。
    ///
    /// 目的：在动手把卡牌从 UGUI 迁到世界空间 Sprite 之前，先用最小场景验证观感
    /// （真阴影、光衰减、后处理）。项目本来就是 URP 2D Renderer，所以不需要换管线。
    /// </summary>
    public static class LightLabBuilder
    {
        private const string ScenePath = "Assets/Scenes/LightLab.unity";

        [MenuItem("CoolingGame/Build Light Lab Scene")]
        public static void Build()
        {
            EditorSceneManager.SaveOpenScenes();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildDesk();
            BuildLamp();
            BuildCards();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("[LightLab] 场景已生成：" + ScenePath);
        }

        private static Material LitMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            return new Material(shader);
        }

        private static void BuildCamera()
        {
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";

            var camera = camGo.GetComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.035f, 0.03f);
            camGo.transform.position = new Vector3(0f, 0.9f, -15f);
            camGo.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
            camGo.transform.LookAt(new Vector3(0f, 0f, 0f));

            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
        }

        private static void BuildDesk()
        {
            var desk = new GameObject("Desk", typeof(SpriteRenderer));
            var renderer = desk.GetComponent<SpriteRenderer>();
            renderer.sprite = Resources.Load<Sprite>("Art/desk");
            renderer.sharedMaterial = LitMaterial();

            if (renderer.sprite != null)
            {
                // 桌面就是一张 19.2x10.8 的图，直接原尺寸，不做缩放
                desk.transform.localScale = Vector3.one;
            }

            desk.transform.position = new Vector3(0f, 0f, 1f);
        }

        private static void BuildLamp()
        {
            // 台灯：点光源，暖色，带阴影
            var lampGo = new GameObject("Lamp", typeof(Light2D));
            var lamp = lampGo.GetComponent<Light2D>();
            lamp.lightType = Light2D.LightType.Point;
            lamp.color = new Color(1f, 0.90f, 0.74f);
            lamp.intensity = 2.6f;
            lamp.pointLightInnerRadius = 1.5f;
            lamp.pointLightOuterRadius = 16f;
            lamp.shadowsEnabled = true;
            lamp.shadowIntensity = 0.85f;
            lamp.shadowSoftness = 0.7f;
            lampGo.transform.position = new Vector3(5.5f, 4f, -2f);

            // 环境光：很暗的冷色，让画面有明暗对比
            var ambientGo = new GameObject("Ambient", typeof(Light2D));
            var ambient = ambientGo.GetComponent<Light2D>();
            ambient.lightType = Light2D.LightType.Global;
            ambient.color = new Color(0.62f, 0.66f, 0.82f);
            ambient.intensity = 0.55f;
        }

        private static void BuildCards()
        {
            Material lit = LitMaterial();
            // 用工程内置 sprite 资产，避免引用运行时生成的临时 sprite（重开编辑器会丢）
            Sprite white = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

            for (int i = 0; i < 6; i++)
            {
                var cardGo = new GameObject("Card_" + i, typeof(SpriteRenderer), typeof(ShadowCaster2D));
                var renderer = cardGo.GetComponent<SpriteRenderer>();
                renderer.sprite = white;
                renderer.sharedMaterial = lit;
                renderer.color = new Color(0.92f, 0.87f, 0.76f);
                renderer.sortingOrder = i;

                cardGo.transform.localScale = new Vector3(1.7f, 2.35f, 1f);
                cardGo.transform.position = new Vector3(-5.6f + i * 2.3f, -1.2f + (i % 2) * 0.5f, -0.4f);

                var caster = cardGo.GetComponent<ShadowCaster2D>();
                caster.selfShadows = true;
                caster.castsShadows = true;
            }
        }
    }
}
