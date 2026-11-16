// Mouse picking with rays. Same idea as web/physics.js, but the slab test is written
// out by hand instead of calling Bounds.IntersectRay, so the method is visible.
using UnityEngine;

namespace P3
{
    public static class Picking
    {
        // Slab method: the ray is inside the box where the three [tmin, tmax] intervals
        // (one per axis) overlap. Returns the distance to the first hit, or -1.
        public static float RayAABB(Ray ray, Bounds box)
        {
            Vector3 o = ray.origin, d = ray.direction, lo = box.min, hi = box.max;
            float tNear = 0f, tFar = float.PositiveInfinity;
            for (int a = 0; a < 3; a++)
            {
                if (Mathf.Abs(d[a]) < 1e-9f)
                {   // parallel to this slab: miss unless the origin is between the planes
                    if (o[a] < lo[a] || o[a] > hi[a]) return -1f;
                    continue;
                }
                float inv = 1f / d[a];
                float t0 = (lo[a] - o[a]) * inv, t1 = (hi[a] - o[a]) * inv;
                if (t0 > t1) (t0, t1) = (t1, t0);
                tNear = Mathf.Max(tNear, t0);
                tFar = Mathf.Min(tFar, t1);
                if (tNear > tFar) return -1f;
            }
            return tNear;
        }

        // distance along the ray to the ground plane y = 0, or -1
        public static float RayGround(Ray ray)
        {
            if (Mathf.Abs(ray.direction.y) < 1e-9f) return -1f;
            float t = -ray.origin.y / ray.direction.y;
            return t > 0 ? t : -1f;
        }

        // world AABB of everything drawn under `root`
        public static Bounds WorldBounds(GameObject root)
        {
            var rs = root.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
