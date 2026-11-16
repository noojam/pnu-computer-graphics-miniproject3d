// Flat shapes built in code for the minimap icons, the click marker and the collider view.
// All shapes lie on the ground plane (x, z) and face up (+Y).
using System.Collections.Generic;
using UnityEngine;

namespace P3
{
    public static class MeshShapes
    {
        // triangle pointing along +Z (the model's forward)
        public static Mesh Arrow(float size)
        {
            var m = new Mesh { name = "Arrow" };
            m.vertices = new[] { new Vector3(0, 0, size), new Vector3(0.7f * size, 0, -0.6f * size), new Vector3(-0.7f * size, 0, -0.6f * size) };
            m.triangles = new[] { 0, 1, 2 };   // clockwise seen from above = front face in Unity
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static Mesh Disc(float r, int seg = 16) => Ring(0f, r, seg);

        public static Mesh Ring(float inner, float outer, int seg = 24)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = 2 * Mathf.PI * i / seg;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(dir * inner);
                v.Add(dir * outer);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = 2 * i, b = a + 1, c = a + 2, d = a + 3;
                t.AddRange(new[] { a, c, b, b, c, d });   // clockwise seen from above
            }
            var m = new Mesh { name = "Ring" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // the 12 edges of each box, as one line mesh
        public static Mesh BoxLines(IEnumerable<Bounds> boxes)
        {
            var v = new List<Vector3>();
            var idx = new List<int>();
            int[] edges = { 0, 1, 1, 3, 3, 2, 2, 0, 4, 5, 5, 7, 7, 6, 6, 4, 0, 4, 1, 5, 2, 6, 3, 7 };
            foreach (var b in boxes)
            {
                int o = v.Count;
                for (int k = 0; k < 8; k++)
                    v.Add(new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 4) == 0 ? b.min.y : b.max.y, (k & 2) == 0 ? b.min.z : b.max.z));
                foreach (var e in edges) idx.Add(o + e);
            }
            var m = new Mesh { name = "ColliderLines", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v);
            m.SetIndices(idx, MeshTopology.Lines, 0);
            m.RecalculateBounds();
            return m;
        }

        // a child object that draws `mesh` with `mat` in a flat colour
        public static GameObject Make(string name, Mesh mesh, Material mat, Color color, Transform parent = null, int layer = 0)
        {
            var go = new GameObject(name);
            go.layer = layer;
            if (parent) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            SetColor(r, color);
            return go;
        }

        public static void SetColor(Renderer r, Color c)
        {
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            r.SetPropertyBlock(mpb);
        }
    }
}
