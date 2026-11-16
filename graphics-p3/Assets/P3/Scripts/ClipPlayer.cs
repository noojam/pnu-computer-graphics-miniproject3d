// Wraps a legacy Animation component so that Play(name) cross-fades like web/actors.js Animator.
// glTFast imports the GLB clips (idle, walk, run, ...) as legacy clips on the model root.
using UnityEngine;

namespace P3
{
    public class ClipPlayer
    {
        readonly Animation anim;
        string current;

        public ClipPlayer(GameObject root)
        {
            anim = root.GetComponentInChildren<Animation>();
            if (anim != null) anim.cullingType = AnimationCullingType.AlwaysAnimate;
        }

        public bool Has(string name) => anim != null && anim[name] != null;

        public float Duration(string name) => Has(name) ? anim[name].length : 0f;

        public void Play(string name, float fade = 0.2f, bool loop = true, bool restart = false)
        {
            if (!Has(name) || (name == current && !restart)) return;
            var s = anim[name];
            s.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
            if (restart || name != current) s.time = 0f;
            anim.CrossFade(name, fade);
            current = name;
        }
    }
}
