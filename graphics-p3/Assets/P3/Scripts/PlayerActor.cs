// The player character. Same numbers and behaviour as web/actors.js Player.
// Events use the observer pattern: OnThrow and OnStep are lists of callbacks.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace P3
{
    public class PlayerActor
    {
        public const float WALK = 2.2f, SPRINT = 5.0f, TURN = 10.0f;
        public const float RADIUS = 0.35f;                  // m, for collision
        static readonly Vector2 ThrowSpeed = new(5.5f, 3.5f);   // m/s forward, up

        public readonly Transform tf;
        readonly ClipPlayer anim;
        readonly IList<GroundCollider> colliders;
        public float heading;            // radians, see P3Math
        float emote;                     // seconds left in a one-shot emote
        Vector3? lookAt;                 // point to turn toward while standing still
        float stride;
        public readonly List<Action<PlayerActor>> onThrow = new();
        public readonly List<Action<PlayerActor>> onStep = new();   // once per footstep

        public PlayerActor(Transform tf, ClipPlayer anim, IList<GroundCollider> colliders)
        {
            this.tf = tf;
            this.anim = anim;
            this.colliders = colliders;
            heading = tf.eulerAngles.y * Mathf.Deg2Rad;
            anim.Play("idle");
        }

        public Vector3 Pos { get => tf.position; set => tf.position = value; }

        void OneShot(string clip, int repeat = 1)
        {
            if (!anim.Has(clip)) return;
            anim.Play(clip, 0.1f, loop: false, restart: true);
            emote = anim.Duration(clip) * repeat;
        }

        public void Wave(Vector3? toward = null)
        {
            OneShot("emote-yes", 2);
            lookAt = toward;
        }

        // turn to `direction` (ground vector) and throw the ball
        public void Throw(Ball ball, Vector3 direction)
        {
            direction.y = 0;
            Vector3 d = direction.normalized;
            heading = P3Math.HeadingOf(d);
            tf.rotation = P3Math.Yaw(heading);
            lookAt = null;
            OneShot("interact-right", 2);
            Vector3 start = Pos + d * 0.4f + new Vector3(0, 1.1f, 0);
            ball.Throw(start, d * ThrowSpeed.x + Vector3.up * ThrowSpeed.y);
            foreach (var f in onThrow) f(this);
        }

        public void Update(float dt, Vector3 move, bool sprint)
        {
            float speed = 0;
            if (move.sqrMagnitude > 1e-12f)
            {
                speed = sprint ? SPRINT : WALK;
                Vector3 before = Pos;
                Pos = GroundCollision.PushOut(Pos + move * (speed * dt), RADIUS, colliders);
                stride += P3Math.FlatDist(Pos, before);
                if (stride > (sprint ? 1.25f : 0.75f))
                {   // metres between footsteps
                    stride = 0;
                    foreach (var f in onStep) f(this);
                }
                heading = P3Math.TurnToward(heading, P3Math.HeadingOf(move), TURN, dt);
                lookAt = null;
            }
            else if (lookAt.HasValue)
            {
                heading = P3Math.TurnToward(heading, P3Math.HeadingOf(lookAt.Value - Pos), TURN * 0.5f, dt);
            }
            tf.rotation = P3Math.Yaw(heading);
            if (emote > 0)
            {
                emote -= dt;
                if (speed > 0) emote = 0;
            }
            if (emote <= 0) anim.Play(speed == 0 ? "idle" : sprint ? "sprint" : "walk");
        }
    }
}
