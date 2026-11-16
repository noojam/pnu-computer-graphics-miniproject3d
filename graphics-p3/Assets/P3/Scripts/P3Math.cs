// Small helpers shared by the actors. Same formulas as web/actors.js.
//
// Coordinates: the Starter Pack (three.js / glTF) is right-handed, Unity is left-handed.
// glTFast converts models by flipping the X axis, so scene.json positions become
// (-x, y, z) and a rotation rotY becomes -rotY (see TownBuilder). After that, every
// formula below works unchanged: a heading h means "forward = (sin h, 0, cos h)",
// which is also what Quaternion.Euler(0, h, 0) does in Unity.
using UnityEngine;

namespace P3
{
    public static class P3Math
    {
        // turn `heading` toward `target` by at most rate*dt radians, the short way round
        public static float TurnToward(float heading, float target, float rate, float dt)
        {
            float d = (target - heading + Mathf.PI) % (2 * Mathf.PI);
            if (d < 0) d += 2 * Mathf.PI;
            d -= Mathf.PI;
            float step = rate * dt;
            return Mathf.Abs(d) <= step ? target : heading + Mathf.Sign(d) * step;
        }

        // heading whose forward (+Z of the model) points along v on the ground
        public static float HeadingOf(Vector3 v) => Mathf.Atan2(v.x, v.z);

        public static Vector3 Forward(float h) => new Vector3(Mathf.Sin(h), 0, Mathf.Cos(h));

        public static float Smoothstep(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3 - 2 * x);
        }

        public static float FlatDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static Quaternion Yaw(float heading) => Quaternion.Euler(0, heading * Mathf.Rad2Deg, 0);
    }
}
