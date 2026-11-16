// Collision on the ground plane (x, z). Same rules and numbers as web/physics.js.
// Characters only walk, so every obstacle is a 2D shape seen from above:
//   box    from the world AABB of a model (houses, cars, fences)
//   circle a trunk or pole at the model origin (trees, street lights, signs)
// Unity's own physics engine is not used: the pack's push-out method is kept on purpose.
using System.Collections.Generic;
using UnityEngine;

namespace P3
{
    public class GroundCollider
    {
        public bool isBox;
        public Vector2 lo, hi;   // box corners (x, z)
        public Vector2 c;        // circle centre (x, z)
        public float r;          // circle radius
        public float height;     // the ball flies over anything lower than this
    }

    public static class GroundCollision
    {
        // models that never block anyone (flat or tiny)
        static readonly string[] Skip = { "flower", "grass", "plant", "mushroom", "path", "driveway", "ground", "lily", "crop", "road", "tile" };
        // models where only a thin trunk or pole at the model origin blocks: name part, radius in m
        static readonly (string part, float r)[] Poles = { ("tree", 0.35f), ("light", 0.2f), ("sign", 0.15f), ("parasol", 0.15f), ("pole", 0.2f) };

        public static GroundCollider Make(string name, Bounds box, Vector3 pos)
        {
            float height = box.max.y - box.min.y;
            if (height < 0.4f) return null;
            foreach (var k in Skip) if (name.Contains(k)) return null;
            foreach (var (part, r) in Poles)   // wide canopy or arm, only the pole blocks
                if (name.Contains(part)) return new GroundCollider { isBox = false, c = new Vector2(pos.x, pos.z), r = r, height = height };
            return new GroundCollider { isBox = true, lo = new Vector2(box.min.x, box.min.z), hi = new Vector2(box.max.x, box.max.z), height = height };
        }

        // Move a circle (pos.xz, radius) out of every collider. Sliding along walls comes
        // for free: only the component into the wall is removed.
        public static Vector3 PushOut(Vector3 pos, float radius, IList<GroundCollider> colliders)
        {
            float px = pos.x, pz = pos.z;
            for (int i = 0; i < colliders.Count; i++)
            {
                var c = colliders[i];
                if (c.isBox)
                {
                    float qx = Mathf.Clamp(px, c.lo.x, c.hi.x);   // closest point of the box
                    float qz = Mathf.Clamp(pz, c.lo.y, c.hi.y);
                    float dx = px - qx, dz = pz - qz;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dist >= radius) continue;
                    if (dist > 1e-6f)
                    {   // outside the box, but too close
                        px = qx + dx / dist * radius;
                        pz = qz + dz / dist * radius;
                    }
                    else
                    {   // centre inside: leave by the nearest side
                        float g0 = px - c.lo.x, g1 = c.hi.x - px, g2 = pz - c.lo.y, g3 = c.hi.y - pz;
                        float m = Mathf.Min(Mathf.Min(g0, g1), Mathf.Min(g2, g3));
                        if (m == g0) px = c.lo.x - radius;
                        else if (m == g1) px = c.hi.x + radius;
                        else if (m == g2) pz = c.lo.y - radius;
                        else pz = c.hi.y + radius;
                    }
                }
                else
                {
                    float dx = px - c.c.x, dz = pz - c.c.y;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dist < c.r + radius)
                    {
                        float nx = dist > 1e-6f ? dx / dist : 1, nz = dist > 1e-6f ? dz / dist : 0;
                        px = c.c.x + nx * (c.r + radius);
                        pz = c.c.y + nz * (c.r + radius);
                    }
                }
            }
            pos.x = px; pos.z = pz;
            return pos;
        }

        // one collider only (the ball checks walls one by one)
        static readonly GroundCollider[] one = new GroundCollider[1];
        public static Vector3 PushOut(Vector3 pos, float radius, GroundCollider c)
        {
            one[0] = c;
            return PushOut(pos, radius, one);
        }
    }
}
