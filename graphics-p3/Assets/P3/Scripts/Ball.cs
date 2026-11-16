// A bouncing ball with hand-written physics. Same numbers as web/physics.js Ball:
// gravity with semi-implicit Euler, restitution on ground and walls, rolling friction.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace P3
{
    public class Ball
    {
        public const float RADIUS = 0.18f;      // m
        public const float GRAVITY = 9.8f;      // m/s^2
        public const float BOUNCE = 0.55f;      // restitution on the ground
        public const float WALL_BOUNCE = 0.5f;  // restitution on walls
        public const float ROLL_DRAG = 2.5f;    // 1/s, rolling friction while on the ground

        public readonly Transform tf;           // the sphere that gets drawn
        public Vector3 vel;
        public bool held = true;                // in someone's hand or mouth: physics off
        public readonly List<Action<Ball, string, float>> onHit = new();   // (ball, "ground"|"wall", speed)

        public Ball(Transform tf)
        {
            this.tf = tf;
            tf.position = new Vector3(0, -10, 0);
        }

        public Vector3 Pos { get => tf.position; set => tf.position = value; }
        public float Speed => vel.magnitude;
        public bool Resting => !held && Pos.y <= RADIUS + 1e-3f && Speed < 0.6f;

        public void Throw(Vector3 start, Vector3 velocity) { Pos = start; vel = velocity; held = false; }
        public void HoldAt(Vector3 p) { held = true; vel = Vector3.zero; Pos = p; }
        public void DropAt(Vector3 p) { held = false; vel = Vector3.zero; Pos = new Vector3(p.x, RADIUS, p.z); }

        public void Update(float dt, IList<GroundCollider> colliders)
        {
            if (held) return;
            // semi-implicit Euler: update velocity first, then position
            vel.y -= GRAVITY * dt;
            Vector3 p = Pos + vel * dt;
            if (p.y < RADIUS)
            {   // ground
                p.y = RADIUS;
                if (vel.y < 0)
                {
                    if (vel.y < -0.8f) Fire("ground", -vel.y);
                    vel.y = -vel.y * BOUNCE;
                    if (vel.y < 0.4f) vel.y = 0;
                }
            }
            if (p.y <= RADIUS + 1e-3f)
            {   // rolling
                float k = Mathf.Exp(-ROLL_DRAG * dt);
                vel.x *= k; vel.z *= k;
                if (Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z) < 0.05f) { vel.x = 0; vel.z = 0; }
            }
            foreach (var c in colliders)
            {   // walls: push out and reflect
                if (p.y > c.height) continue;
                float bx = p.x, bz = p.z;
                p = GroundCollision.PushOut(p, RADIUS, c);
                float mx = p.x - bx, mz = p.z - bz, n = Mathf.Sqrt(mx * mx + mz * mz);
                if (n > 1e-6f)
                {
                    float nx = mx / n, nz = mz / n, vn = vel.x * nx + vel.z * nz;
                    if (vn < 0)
                    {
                        if (vn < -0.8f) Fire("wall", -vn);
                        vel.x -= (1 + WALL_BOUNCE) * vn * nx;
                        vel.z -= (1 + WALL_BOUNCE) * vn * nz;
                    }
                }
            }
            Pos = p;
        }

        void Fire(string kind, float speed) { foreach (var f in onHit) f(this, kind, speed); }
    }
}
