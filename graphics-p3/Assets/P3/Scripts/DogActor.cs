// A dog that greets the player and plays fetch. Same states and numbers as web/actors.js Dog:
//
//   WANDER --player within notice radius--> NOTICE --> APPROACH --close--> GREET --> FOLLOW
//      ^                                                                             |
//      +-------------------------- player farther than leave radius ------------------+
//
//   ball thrown (from any state, if the player is near) --> CHASE --> RETURN --> DROP --> FOLLOW
//
// Observers: OnGreet when it starts a greeting, OnBark when it barks.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace P3
{
    public class DogActor
    {
        public const float NOTICE_RADIUS = 6.0f;    // starts paying attention inside this distance
        public const float GREET_DISTANCE = 1.5f;   // stops this close to the player and greets
        public const float FOLLOW_DISTANCE = 2.4f;  // in FOLLOW, catches up when the player is farther than this
        public const float LEAVE_RADIUS = 11.0f;    // gives up and goes home beyond this distance
        public const float WALK = 1.0f, RUN = 4.2f, TURN = 7.0f;
        public const float RADIUS = 0.3f;           // m, for collision
        public const float HOP_TIME = 1.0f;         // greeting part 1: three happy hops
        public const float SPIN_TIME = 1.2f;        // greeting part 2: dance and spin once
        public const float GREET_TIME = 2.6f;       // whole greeting, then FOLLOW
        public const float HOP_HEIGHT = 0.3f;       // m
        public const float PICK_DISTANCE = 0.55f;   // mouth reaches the ball within this distance
        public const float DROP_TIME = 1.0f;        // wag after dropping the ball
        static readonly Vector3 Mouth = new(0.0f, 0.42f, 0.42f);   // ball position in the dog's frame (right, up, forward), m

        public readonly Transform tf;
        readonly ClipPlayer anim;
        readonly Vector3 home;
        readonly float wanderRadius;
        readonly IList<GroundCollider> colliders;
        uint seed;
        public string State { get; private set; } = "WANDER";
        float timer;
        Vector3? goal;
        float hop;
        bool greeted;                // greet once per visit, not every time it catches up
        float restFor = 2;
        float greetHeading;
        public float heading;
        Ball ball;                   // the ball being chased or carried
        public readonly List<Action<DogActor>> onGreet = new();
        public readonly List<Action<DogActor>> onBark = new();

        public DogActor(Transform tf, ClipPlayer anim, Vector3 home, float wanderRadius, IList<GroundCollider> colliders, uint seed = 1)
        {
            this.tf = tf;
            this.anim = anim;
            this.home = home;
            this.wanderRadius = wanderRadius;
            this.colliders = colliders;
            this.seed = seed;
            heading = tf.eulerAngles.y * Mathf.Deg2Rad;
            anim.Play("idle");
        }

        public Vector3 Pos { get => tf.position; set => tf.position = value; }

        // small deterministic random numbers, so runs repeat (same generator as actors.js)
        float Rand()
        {
            seed = seed * 1664525u + 1013904223u;
            return seed / 4294967296f;
        }

        void SetState(string s)
        {
            State = s; timer = 0;
            if (s == "NOTICE" || s == "DROP") Bark();   // a bark when it spots you and when it brings the ball
        }

        void Bark() { foreach (var f in onBark) f(this); }

        Vector3 MouthPos()
        {
            Vector3 f = P3Math.Forward(heading);
            Vector3 r = Vector3.Cross(Vector3.up, f);
            return new Vector3(Pos.x, 0, Pos.z) + r * Mouth.x + Vector3.up * Mouth.y + f * Mouth.z;
        }

        bool MoveTo(Vector3 target, float speed, float dt, float stop = 0.05f)
        {
            Vector3 d = target - Pos; d.y = 0;
            float dist = d.magnitude;
            if (dist <= stop + 0.05f) return true;   // small tolerance: the target may be moving
            heading = P3Math.TurnToward(heading, P3Math.HeadingOf(d), TURN, dt);
            // move mostly forward so the dog curves instead of sliding sideways
            Vector3 fwd = P3Math.Forward(heading);
            float step = Mathf.Min(speed * dt, dist - stop);
            Vector3 p = Pos + fwd * (step * Mathf.Max(0.2f, Vector3.Dot(fwd, d / dist)));
            Pos = GroundCollision.PushOut(p, RADIUS, colliders);
            return false;
        }

        void Face(Vector3 target, float dt)
        {
            heading = P3Math.TurnToward(heading, P3Math.HeadingOf(target - Pos), TURN, dt);
        }

        void StartGreeting(Vector3 playerPos)
        {
            SetState("GREET");
            greetHeading = P3Math.HeadingOf(playerPos - Pos);
            anim.Play("gesture-positive", 0.1f);
            Bark();
            foreach (var f in onGreet) f(this);
        }

        // observer callback: the player threw the ball
        public void BallThrown(Ball b, Vector3 playerPos)
        {
            if (P3Math.FlatDist(playerPos, Pos) < LEAVE_RADIUS && State != "CHASE" && State != "RETURN")
            {
                ball = b;
                greeted = true;       // playing counts as having said hello
                SetState("CHASE");
            }
        }

        public void Update(float dt, Vector3 playerPos)
        {
            timer += dt;
            float dist = P3Math.FlatDist(playerPos, Pos);
            hop = 0;

            switch (State)
            {
                case "WANDER":
                    if (dist < NOTICE_RADIUS) { SetState("NOTICE"); break; }
                    if (goal == null && P3Math.FlatDist(Pos, home) > wanderRadius + 1)
                    {
                        goal = home;                                   // far from home: walk back first
                    }
                    else if (goal == null)
                    {                                                  // resting
                        anim.Play("idle");
                        if (timer > restFor)
                        {
                            float a = Rand() * 2 * Mathf.PI, r = wanderRadius * Mathf.Sqrt(Rand());
                            goal = home + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                            timer = 0;
                            restFor = 1.5f + 2 * Rand();
                        }
                    }
                    else
                    {                                                  // trotting to a random spot
                        anim.Play("walk");
                        if (MoveTo(goal.Value, WALK, dt) || timer > 8) { goal = null; timer = 0; }
                    }
                    break;

                case "NOTICE":                                         // stop, turn, look
                    anim.Play("idle", 0.1f);
                    Face(playerPos, dt);
                    if (timer > 0.5f) SetState("APPROACH");
                    break;

                case "APPROACH":
                    anim.Play("run");
                    if (dist > LEAVE_RADIUS) SetState("WANDER");
                    else if (MoveTo(playerPos, RUN, dt, GREET_DISTANCE))
                    {
                        if (greeted) SetState("FOLLOW"); else StartGreeting(playerPos);
                    }
                    break;

                case "GREET":
                {
                    float t = timer;
                    if (t < HOP_TIME)
                    {                                                  // three hops, wagging
                        anim.Play("gesture-positive", 0.1f);
                        hop = Mathf.Abs(Mathf.Sin(Mathf.PI * 3 * t / HOP_TIME)) * HOP_HEIGHT;
                        heading = greetHeading;
                    }
                    else if (t < HOP_TIME + SPIN_TIME)
                    {                                                  // dance and spin once
                        if (t - dt < HOP_TIME) Bark();                 // one more happy bark as the spin starts
                        anim.Play("dance", 0.15f);
                        heading = greetHeading + 2 * Mathf.PI * P3Math.Smoothstep((t - HOP_TIME) / SPIN_TIME);
                    }
                    else
                    {                                                  // sit still and look up
                        anim.Play("idle", 0.2f);
                        heading = greetHeading;
                    }
                    if (t > GREET_TIME) { greeted = true; SetState("FOLLOW"); }
                    break;
                }

                case "FOLLOW":
                    if (dist > LEAVE_RADIUS)
                    {
                        greeted = false; goal = null; SetState("WANDER");
                    }
                    else if (dist > FOLLOW_DISTANCE)
                    {
                        bool fast = dist > 2 * FOLLOW_DISTANCE;
                        anim.Play(fast ? "run" : "walk");
                        MoveTo(playerPos, fast ? RUN : WALK * 2.2f, dt, GREET_DISTANCE);
                    }
                    else
                    {
                        anim.Play("idle");
                        Face(playerPos, dt);
                    }
                    break;

                case "CHASE":
                {                                                      // run to where the ball is now
                    anim.Play("run");
                    float d = P3Math.FlatDist(ball.Pos, Pos);
                    if (ball.Resting && d < PICK_DISTANCE) SetState("RETURN");   // got it
                    else MoveTo(ball.Pos, d > 1.5f ? RUN : RUN * 0.6f, dt, PICK_DISTANCE - 0.15f);
                    if (State == "CHASE" && timer > 15) { ball = null; SetState("FOLLOW"); }   // out of reach
                    break;
                }

                case "RETURN":                                         // carry it back
                    anim.Play("run");
                    if (MoveTo(playerPos, RUN, dt, GREET_DISTANCE))
                    {
                        SetState("DROP");
                        ball.DropAt(MouthPos());
                        anim.Play("gesture-positive", 0.1f);
                    }
                    break;

                case "DROP":                                           // proud wiggle, then follow
                    Face(playerPos, dt);
                    hop = Mathf.Abs(Mathf.Sin(Mathf.PI * 2 * timer / DROP_TIME)) * HOP_HEIGHT * 0.5f;
                    if (timer > DROP_TIME) { ball = null; SetState("FOLLOW"); }
                    break;
            }
            Vector3 p = Pos; p.y = hop; Pos = p;
            tf.rotation = P3Math.Yaw(heading);
            if (State == "RETURN") ball.HoldAt(MouthPos());
        }
    }
}
