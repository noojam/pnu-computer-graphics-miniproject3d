// P3 Starter (Unity): walk around a Kenney town with a dog that greets you and plays fetch.
// Unity port of web/main.js. The scene itself (props, player, dog, lights) is built in the
// Editor from scene.json by P3 > Build Town Scene (Assets/P3/Editor/TownBuilder.cs); this
// component runs the game loop: input, follow camera, picking, click-to-move, observers, HUD.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace P3
{
    public class Game : MonoBehaviour
    {
        const float CLICK_PIXELS = 5f;   // a press that moves less than this is a click, not a camera drag

        [Header("Filled in by TownBuilder")]
        public GameObject playerObj;
        public GameObject dogObj;
        public Transform ballTf;
        public Transform propsRoot;
        public Camera mainCam;
        public SoundFx sound;
        public Material unlitMat;
        public Vector3 dogHome;
        public float dogWanderRadius = 4f;
        public float startYaw;           // radians

        PlayerActor player;
        DogActor dog;
        Ball ball;
        Minimap minimap;
        readonly List<GroundCollider> colliders = new();
        readonly List<(GameObject obj, Bounds box)> props = new();
        readonly List<Bounds> hardGround = new();
        GameObject colliderView, marker;

        // follow camera
        float camYaw, camPitch = 0.35f, camDist = 9f;
        Vector3 camTarget;

        // picking and click-to-move
        GameObject selected;
        Vector3? walkTarget;
        float stuck;
        float dragged;

        // HUD
        float fpsTime; int frames; string fpsText = "";
        GUIStyle hudStyle;

        void Start()
        {
            DynamicGI.UpdateEnvironment();   // ambient light and reflections from the HDRI sky material

            // world-space AABBs (rest pose): picking, and ground-plane colliders
            foreach (Transform t in propsRoot)
            {
                var box = Picking.WorldBounds(t.gameObject);
                props.Add((t.gameObject, box));
                var c = GroundCollision.Make(t.name, box, t.position);
                if (c != null) colliders.Add(c);
                if (t.name.Contains("road") || t.name.Contains("tile")) hardGround.Add(box);
            }

            player = new PlayerActor(playerObj.transform, new ClipPlayer(playerObj), colliders);
            dog = new DogActor(dogObj.transform, new ClipPlayer(dogObj), dogHome, dogWanderRadius, colliders);
            ball = new Ball(ballTf);

            // observers: the player waves back when greeted, the dog chases a thrown ball
            dog.onGreet.Add(d => player.Wave(d.Pos));
            player.onThrow.Add(p => dog.BallThrown(ball, p.Pos));

            // sound effects are observers too: they react to the same events
            player.onStep.Add(p => sound.Play("step_" + Surface(p.Pos), p.Pos));
            player.onThrow.Add(p => sound.Play("throw", p.Pos + Vector3.up * 1.2f));
            ball.onHit.Add((b, kind, v) => sound.Play(kind == "ground" ? "bounce" : "wall", b.Pos, Mathf.Min(1f, v / 5f)));
            dog.onBark.Add(d => sound.Play("bark", d.Pos + Vector3.up * 0.5f));

            // collider outlines for the B key
            var boxes = new List<Bounds>();
            foreach (var c in colliders)
            {
                if (c.isBox) boxes.Add(new Bounds { min = new Vector3(c.lo.x, 0, c.lo.y), max = new Vector3(c.hi.x, c.height, c.hi.y) });
                else boxes.Add(new Bounds { min = new Vector3(c.c.x - c.r, 0, c.c.y - c.r), max = new Vector3(c.c.x + c.r, c.height, c.c.y + c.r) });
            }
            colliderView = MeshShapes.Make("ColliderView", MeshShapes.BoxLines(boxes), unlitMat, new Color(1f, 0.27f, 0.2f), transform);
            colliderView.SetActive(false);

            marker = MeshShapes.Make("WalkMarker", MeshShapes.Ring(0.18f, 0.3f), unlitMat, new Color(0.2f, 1f, 0.4f), transform);
            marker.SetActive(false);

            minimap = new GameObject("Minimap").AddComponent<Minimap>();
            minimap.transform.SetParent(transform, false);
            minimap.Init(unlitMat);
            mainCam.cullingMask &= ~(1 << Minimap.ICON_LAYER);

            camYaw = startYaw;
            camTarget = player.Pos + Vector3.up * 1.4f;
            UpdateCamera(0f);
        }

        // hard ground (roads, pavements, plazas) for footstep sounds
        string Surface(Vector3 q)
        {
            foreach (var b in hardGround)
                if (q.x >= b.min.x && q.x <= b.max.x && q.z >= b.min.z && q.z <= b.max.z) return "hard";
            return "grass";
        }

        // ------------------------------------------------------------ picking and click-to-move
        void Select(GameObject obj)
        {
            if (selected) SetHighlight(selected, false);
            selected = obj;
            if (selected) SetHighlight(selected, true);
        }

        // Tint the base colour toward yellow with a per-material property block. (glTFast's
        // emission needs a shader keyword whose variant a WebGL build may strip, so the
        // base colour, which every material uses, is changed instead.)
        static readonly Color Glow = new(1f, 0.85f, 0.25f);
        static void SetHighlight(GameObject obj, bool on)
        {
            foreach (var r in obj.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!on || mats[i] == null) { r.SetPropertyBlock(null, i); continue; }
                    string prop = mats[i].HasProperty("baseColorFactor") ? "baseColorFactor" : "_BaseColor";
                    if (!mats[i].HasProperty(prop)) continue;
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor(prop, Color.Lerp(mats[i].GetColor(prop), Glow, 0.5f));
                    r.SetPropertyBlock(mpb, i);
                }
            }
        }

        // pick the nearest object under the mouse; if it is the ground, walk there
        void Click(Vector2 mouse)
        {
            if (minimap.Contains(mouse))
            {   // a click on the minimap: walk there
                walkTarget = minimap.ToWorld(mouse);
                stuck = 0;
                sound.Play("move");
                return;
            }
            Ray ray = mainCam.ScreenPointToRay(mouse);
            GameObject best = null;
            float bestT = float.PositiveInfinity;
            foreach (var (obj, box) in props)
            {
                float t = Picking.RayAABB(ray, box);
                if (t >= 0 && t < bestT) { best = obj; bestT = t; }
            }
            float td = Picking.RayAABB(ray, Picking.WorldBounds(dogObj));   // the dog moves: fresh box
            if (td >= 0 && td < bestT) { best = dogObj; bestT = td; }

            float tg = Picking.RayGround(ray);
            if (tg >= 0 && tg < bestT)
            {
                Vector3 p = ray.GetPoint(tg);
                walkTarget = new Vector3(p.x, 0, p.z);
                stuck = 0;
                Select(null);
                sound.Play("move");
            }
            else
            {
                Select(best);
                sound.Play("select");
            }
        }

        // ------------------------------------------------------------ follow camera
        Vector3 CamForward => new Vector3(-Mathf.Sin(camYaw), 0, -Mathf.Cos(camYaw));

        void UpdateCamera(float dt)
        {
            Vector3 goal = player.Pos + Vector3.up * 1.4f;
            camTarget = Vector3.Lerp(camTarget, goal, 1 - Mathf.Exp(-10 * dt));
            float cp = Mathf.Cos(camPitch);
            mainCam.transform.position = camTarget + new Vector3(Mathf.Sin(camYaw) * cp, Mathf.Sin(camPitch), Mathf.Cos(camYaw) * cp) * camDist;
            mainCam.transform.LookAt(camTarget);
        }

        // ------------------------------------------------------------ input
        Vector3 MoveInput(Keyboard kb)
        {
            Vector3 fwd = CamForward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);   // Unity is left-handed
            Vector3 m = Vector3.zero;
            if (kb != null)
            {
                m += fwd * ((kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
                m += right * ((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0));
            }
            if (m.sqrMagnitude > 0) walkTarget = null;                   // keys override click-to-move
            else if (walkTarget.HasValue)
            {
                m = walkTarget.Value - player.Pos; m.y = 0;
                if (m.magnitude < 0.2f) { walkTarget = null; m = Vector3.zero; }
            }
            return m.sqrMagnitude > 0 ? m.normalized : m;
        }

        void HandleInput(Keyboard kb, Mouse mouse)
        {
            if (kb != null)
            {
                if (kb.bKey.wasPressedThisFrame) colliderView.SetActive(!colliderView.activeSelf);
                if (kb.fKey.wasPressedThisFrame) player.Throw(ball, CamForward);
                if (kb.eKey.wasPressedThisFrame) player.Wave();
                if (kb.mKey.wasPressedThisFrame) sound.ToggleMute();
#if !UNITY_WEBGL
                if (kb.f12Key.wasPressedThisFrame) ScreenCapture.CaptureScreenshot($"shot_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
                if (kb.escapeKey.wasPressedThisFrame) Application.Quit();
#endif
            }
            if (mouse == null) return;
            if (mouse.leftButton.wasPressedThisFrame) dragged = 0;
            if (mouse.leftButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue();
                dragged += Mathf.Abs(d.x) + Mathf.Abs(d.y);
                if (dragged >= CLICK_PIXELS)
                {   // the world is mirrored in x compared with three.js, so yaw turns the other way
                    camYaw += d.x * 0.005f;
                    camPitch = Mathf.Clamp(camPitch - d.y * 0.005f, 0.05f, 1.35f);
                }
            }
            if (mouse.leftButton.wasReleasedThisFrame && dragged < CLICK_PIXELS) Click(mouse.position.ReadValue());
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f) camDist = Mathf.Clamp(camDist * Mathf.Pow(0.9f, Mathf.Sign(wheel)), 3f, 30f);
        }

        // ------------------------------------------------------------ loop
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            var kb = Keyboard.current;
            HandleInput(kb, Mouse.current);

            bool sprint = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            Vector3 before = player.Pos;
            player.Update(dt, MoveInput(kb), sprint);
            if (walkTarget.HasValue)
            {   // blocked by a wall for a while: give up
                float moved = P3Math.FlatDist(player.Pos, before);
                stuck = moved < 0.3f * PlayerActor.WALK * dt ? stuck + dt : 0;
                if (stuck > 0.6f) walkTarget = null;
            }
            dog.Update(dt, player.Pos);
            ball.Update(dt, colliders);
            marker.SetActive(walkTarget.HasValue);
            if (walkTarget.HasValue) marker.transform.position = walkTarget.Value + Vector3.up * 0.02f;
            UpdateCamera(dt);
            // third-person games put the ears on the character and turn them with the camera
            sound.SetListener(player.Pos + Vector3.up * 1.4f, mainCam.transform.rotation);
            minimap.UpdateMap(player, dog, ball, walkTarget, mainCam);

            frames++; fpsTime += Time.unscaledDeltaTime;
            if (fpsTime > 0.5f) { fpsText = $"{Mathf.RoundToInt(frames / fpsTime)} fps"; frames = 0; fpsTime = 0; }
        }

        // ------------------------------------------------------------ HUD
        void OnGUI()
        {
            float k = Minimap.UiScale;
            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false };
                hudStyle.normal.textColor = Color.white;
            }
            hudStyle.fontSize = Mathf.RoundToInt(13 * k);
            string text =
                "WASD move, Shift sprint, F throw ball, E wave\n" +
                "Click: select or walk there (also on the minimap), drag: turn camera, wheel: zoom\n" +
                "B colliders, M mute" + (sound.muted ? " (muted)" : "") + "\n" +
                $"dog: <color=#ffd35c><b>{dog?.State ?? "-"}</b></color>   selected: <color=#ffd35c><b>{(selected ? selected.name : "-")}</b></color>   {fpsText}";
            var content = new GUIContent(text);
            Vector2 size = hudStyle.CalcSize(content);
            var box = new Rect(12 * k, 10 * k, size.x + 24 * k, size.y + 16 * k);
            Fill(box, new Color(0.08f, 0.12f, 0.16f, 0.55f));
            GUI.Label(new Rect(box.x + 12 * k, box.y + 8 * k, size.x, size.y), content, hudStyle);

            // minimap frame (GUI y grows downward)
            if (minimap)
            {
                Rect r = minimap.PixelRect;
                float b = 3 * k, top = Screen.height - r.yMax;
                var frame = new Color(0.12f, 0.16f, 0.14f, 1f);
                Fill(new Rect(r.x - b, top - b, r.width + 2 * b, b), frame);
                Fill(new Rect(r.x - b, top + r.height, r.width + 2 * b, b), frame);
                Fill(new Rect(r.x - b, top, b, r.height), frame);
                Fill(new Rect(r.xMax, top, b, r.height), frame);
            }
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
