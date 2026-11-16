// P3 > Build Town Scene: builds Assets/Scenes/Town.unity from the Starter Pack's scene.json,
// the same file both original tracks read. Re-run it after editing scene.json.
//
// Coordinates: scene.json is written for three.js (right-handed). glTFast imports the models
// by flipping the X axis, so a position (x, y, z) goes to (-x, y, z) and rotY goes to -rotY.
// Everything the game computes afterwards stays in Unity coordinates.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace P3.EditorTools
{
    public static class TownBuilder
    {
        const string PACK = "Assets/StarterPack";
        const string SCENE_JSON = PACK + "/scene.json";
        const string OUT_SCENE = "Assets/Scenes/Town.unity";
        const string MAT_DIR = "Assets/P3/Materials";
        static readonly Color Sky = new Color32(0xb8, 0xd6, 0xf7, 0xff);

        [MenuItem("P3/Build Town Scene")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static string Build()
        {
            var data = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(SCENE_JSON));
            var kitScale = (Dictionary<string, object>)data["kitScale"];

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(MAT_DIR);

            // ---------------------------------------------------------- materials
            var unlit = MakeMaterial("Unlit", "Universal Render Pipeline/Unlit", Color.white);
            var groundData = (Dictionary<string, object>)data["ground"];
            var ground = MakeMaterial("Ground", "Universal Render Pipeline/Lit", Hex((string)groundData["color"]));
            ground.SetFloat("_Smoothness", 0f);
            var ballMat = MakeMaterial("Ball", "Universal Render Pipeline/Lit", Hex("#e0261a"));
            ballMat.SetFloat("_Smoothness", 0.5f);
            var skyMat = MakeMaterial("SkyHDRI", "Skybox/Panoramic", Color.white);
            skyMat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture>(PACK + "/hdri/kloofendal_48d_partly_cloudy_puresky_1k.hdr"));
            skyMat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(skyMat);

            // ---------------------------------------------------------- light, sky, fog
            // the background is a flat sky colour like the web track; the HDRI only lights the scene
            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.6f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.6f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = Sky;

            var sunData = (Dictionary<string, object>)data["sun"];
            Vector3 sunDir = Vec(sunData["direction"]);
            sunDir.x = -sunDir.x;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.LookRotation(sunDir.normalized);
            var sc = (List<object>)sunData["color"];
            sun.color = new Color((float)(double)sc[0], (float)(double)sc[1], (float)(double)sc[2]);
            sun.intensity = (float)(double)sunData["intensity"];
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            RenderSettings.sun = sun;

            var volume = new GameObject("PostProcess").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = ToneMappingProfile();

            // ---------------------------------------------------------- ground
            float size = (float)(double)groundData["size"];
            var groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundGo.name = "Ground";
            Object.DestroyImmediate(groundGo.GetComponent<Collider>());
            groundGo.transform.position = new Vector3(0, -0.02f, 0);   // 2 cm below y = 0, so road tiles never fight with the grass
            groundGo.transform.localScale = new Vector3(size / 10f, 1, size / 10f);
            groundGo.GetComponent<MeshRenderer>().sharedMaterial = ground;
            groundGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // ---------------------------------------------------------- props
            var propsRoot = new GameObject("Props").transform;
            int missing = 0;
            foreach (var p in (List<object>)data["props"])
                if (Place((Dictionary<string, object>)p, kitScale, propsRoot) == null) missing++;

            var player = Place((Dictionary<string, object>)data["player"], kitScale, null);
            player.name = "Player";
            var dogData = (Dictionary<string, object>)data["dog"];
            var dog = Place(dogData, kitScale, null);
            dog.name = "Dog";

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.transform.localScale = Vector3.one * (2 * Ball.RADIUS);
            ball.GetComponent<MeshRenderer>().sharedMaterial = ballMat;

            // ---------------------------------------------------------- camera
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Sky;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            // ---------------------------------------------------------- sound + game
            var sound = new GameObject("Sound").AddComponent<SoundFx>();
            sound.roles = LoadRoles();

            var game = new GameObject("Game").AddComponent<Game>();
            game.playerObj = player;
            game.dogObj = dog;
            game.ballTf = ball.transform;
            game.propsRoot = propsRoot;
            game.mainCam = cam;
            game.sound = sound;
            game.unlitMat = unlit;
            Vector3 home = dogData.ContainsKey("home") ? Vec(dogData["home"]) : Vec(dogData["pos"]);
            game.dogHome = new Vector3(-home.x, 0, home.z);
            game.dogWanderRadius = dogData.ContainsKey("wanderRadius") ? (float)(double)dogData["wanderRadius"] : 4f;
            float yawDeg = data.ContainsKey("camera") && ((Dictionary<string, object>)data["camera"]).ContainsKey("yaw")
                ? (float)(double)((Dictionary<string, object>)data["camera"])["yaw"] : 0f;
            game.startYaw = -yawDeg * Mathf.Deg2Rad;

            EditorSceneManager.SaveScene(scene, OUT_SCENE);
            var list = new List<EditorBuildSettingsScene> { new(OUT_SCENE, true) };
            foreach (var s in EditorBuildSettings.scenes) if (s.path != OUT_SCENE) list.Add(new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            return $"built {OUT_SCENE}: {propsRoot.childCount} props, {missing} missing models";
        }

        // ---------------------------------------------------------- helpers
        static GameObject Place(Dictionary<string, object> desc, Dictionary<string, object> kitScale, Transform parent)
        {
            string model = (string)desc["model"];
            string kit = model.Split('/')[0];
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PACK}/models/{model}");
            if (asset == null) { Debug.LogWarning("missing model " + model); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.name = Path.GetFileNameWithoutExtension(model);
            if (parent) go.transform.SetParent(parent, false);
            Vector3 pos = desc.ContainsKey("pos") ? Vec(desc["pos"]) : Vector3.zero;
            float rotY = desc.ContainsKey("rotY") ? (float)(double)desc["rotY"] : 0f;
            float scale = (desc.ContainsKey("scale") ? (float)(double)desc["scale"] : 1f)
                        * (kitScale.ContainsKey(kit) ? (float)(double)kitScale[kit] : 1f);
            go.transform.SetLocalPositionAndRotation(new Vector3(-pos.x, pos.y, pos.z), Quaternion.Euler(0, -rotY, 0));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static SoundFx.Role[] LoadRoles()
        {
            var roles = new List<SoundFx.Role>();
            var json = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(PACK + "/sounds/sounds.json"));
            foreach (var kv in json)
            {
                if (kv.Key.StartsWith("_")) continue;
                var r = (Dictionary<string, object>)kv.Value;
                var clips = new List<AudioClip>();
                foreach (var f in (List<object>)r["files"])
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{PACK}/sounds/{f}");
                    if (clip) clips.Add(clip); else Debug.LogWarning("missing sound " + f);
                }
                roles.Add(new SoundFx.Role
                {
                    name = kv.Key,
                    clips = clips.ToArray(),
                    gain = r.ContainsKey("gain") ? (float)(double)r["gain"] : 1f,
                    ui = r.ContainsKey("ui") && (bool)r["ui"],
                });
            }
            return roles.ToArray();
        }

        static VolumeProfile ToneMappingProfile()
        {
            string path = "Assets/P3/Settings/TownPostProcess.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile) return profile;
            Directory.CreateDirectory("Assets/P3/Settings");
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);   // ACES tone mapping, like the web track
            AssetDatabase.AddObjectToAsset(tm, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static Material MakeMaterial(string name, string shader, Color color)
        {
            string path = $"{MAT_DIR}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        static Vector3 Vec(object o)
        {
            var a = (List<object>)o;
            return new Vector3((float)(double)a[0], (float)(double)a[1], (float)(double)a[2]);
        }
    }
}
