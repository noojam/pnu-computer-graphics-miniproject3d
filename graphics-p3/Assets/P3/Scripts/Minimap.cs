// Minimap HUD: a second, top-down orthographic camera drawn into a corner viewport.
// Same idea as web/minimap.js:
//   - Camera.pixelRect plays the role of setViewport + setScissor (draw and clear only there)
//   - the camera looks straight down with "up" = -Z, so north is up on the map
//   - icons live on layer 31, which only the minimap camera renders
//   - clicking the map makes the player walk to that spot
//   - fog is switched off while this camera renders (no fog from 80 m above)
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace P3
{
    public class Minimap : MonoBehaviour
    {
        public const float SIZE = 240f;    // pixels (at 1080p; scaled with the window height)
        public const float MARGIN = 16f;   // pixels from the window corner
        public const float RANGE = 24f;    // half width of the map square, m
        public const int ICON_LAYER = 31;
        const float Y = 40f;               // icons float above everything

        public Camera cam;
        Transform playerBack, player, dogBack, dog, ballBack, ball, target;
        LineRenderer wedge;
        bool fogWas;

        public static float UiScale => Mathf.Max(1f, Screen.height / 1080f);

        public void Init(Material unlit)
        {
            cam = new GameObject("MinimapCamera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.orthographic = true;
            cam.orthographicSize = RANGE;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 160f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x1f, 0x29, 0x24, 0xff);
            cam.depth = 10;                                     // drawn after the main camera
            cam.cullingMask = ~0;                               // everything, including the icon layer
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;

            float s = RANGE * 0.07f;                            // icon size in metres (about 8 px)
            var black = new Color(0.1f, 0.1f, 0.1f);
            playerBack = Icon("PlayerBack", MeshShapes.Arrow(s * 1.35f), unlit, black);
            player = Icon("Player", MeshShapes.Arrow(s), unlit, new Color(1f, 0.85f, 0.1f));
            dogBack = Icon("DogBack", MeshShapes.Disc(s * 0.75f), unlit, black);
            dog = Icon("Dog", MeshShapes.Disc(s * 0.6f), unlit, new Color(0.95f, 0.55f, 0.15f));
            ballBack = Icon("BallBack", MeshShapes.Disc(s * 0.55f, 12), unlit, black);
            ball = Icon("Ball", MeshShapes.Disc(s * 0.4f, 12), unlit, new Color(0.9f, 0.1f, 0.06f));
            target = Icon("Target", MeshShapes.Ring(s * 0.45f, s * 0.6f, 20), unlit, new Color(0.2f, 1f, 0.4f));

            wedge = new GameObject("ViewWedge").AddComponent<LineRenderer>();
            wedge.transform.SetParent(transform, false);
            wedge.gameObject.layer = ICON_LAYER;
            wedge.sharedMaterial = unlit;
            wedge.positionCount = 3;
            wedge.widthMultiplier = 0.22f;
            wedge.useWorldSpace = true;
            wedge.shadowCastingMode = ShadowCastingMode.Off;
            MeshShapes.SetColor(wedge, Color.white);

            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
        }

        void OnBeginCamera(ScriptableRenderContext ctx, Camera c)
        {
            if (c != cam) return;
            fogWas = RenderSettings.fog;
            RenderSettings.fog = false;
        }

        void OnEndCamera(ScriptableRenderContext ctx, Camera c)
        {
            if (c == cam) RenderSettings.fog = fogWas;
        }

        Transform Icon(string name, Mesh mesh, Material mat, Color color)
            => MeshShapes.Make(name, mesh, mat, color, transform, ICON_LAYER).transform;

        // screen rectangle of the map, origin bottom-left (Unity screen space)
        public Rect PixelRect
        {
            get
            {
                float k = UiScale, size = SIZE * k, m = MARGIN * k;
                return new Rect(Screen.width - m - size, Screen.height - m - size, size, size);
            }
        }

        public bool Contains(Vector2 mouse) => PixelRect.Contains(mouse);

        // screen pixel inside the map -> point on the ground
        public Vector3 ToWorld(Vector2 mouse)
        {
            Vector3 p = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, 80f));
            return new Vector3(p.x, 0, p.z);
        }

        static void Place(Transform t, Vector3 p, float y, float? heading = null)
        {
            t.position = new Vector3(p.x, y, p.z);
            if (heading.HasValue) t.rotation = P3Math.Yaw(heading.Value);
        }

        public void UpdateMap(PlayerActor pl, DogActor dg, Ball b, Vector3? walkTarget, Camera mainCam)
        {
            cam.pixelRect = PixelRect;
            Vector3 c = pl.Pos;
            cam.transform.SetPositionAndRotation(new Vector3(c.x, 80f, c.z), Quaternion.LookRotation(Vector3.down, Vector3.back));

            // higher icons are nearer to this camera, so they cover the lower ones
            Place(playerBack, pl.Pos, Y - 0.1f, pl.heading);
            Place(player, pl.Pos, Y, pl.heading);
            Place(dogBack, dg.Pos, Y - 0.1f);
            Place(dog, dg.Pos, Y);
            bool showBall = !b.held || dg.State == "RETURN";
            ball.gameObject.SetActive(showBall);
            ballBack.gameObject.SetActive(showBall);
            Place(ballBack, b.Pos, Y - 0.1f);
            Place(ball, b.Pos, Y);
            target.gameObject.SetActive(walkTarget.HasValue);
            if (walkTarget.HasValue) Place(target, walkTarget.Value, Y - 0.2f);

            // what the main camera sees: a wedge of the horizontal field of view
            float half = Mathf.Atan(Mathf.Tan(mainCam.fieldOfView * Mathf.Deg2Rad / 2) * mainCam.aspect);
            float yaw = P3Math.HeadingOf(mainCam.transform.forward);
            Vector3 o = new Vector3(pl.Pos.x, Y - 0.3f, pl.Pos.z);
            wedge.SetPosition(0, o + P3Math.Forward(yaw - half) * 12f);
            wedge.SetPosition(1, o);
            wedge.SetPosition(2, o + P3Math.Forward(yaw + half) * 12f);
        }
    }
}
