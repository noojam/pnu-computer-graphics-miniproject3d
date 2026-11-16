// Sound effects with 3D placement. Same idea as web/sound.js:
//   - the AudioListener sits on an "ears" object at the player's head, turned like the camera
//   - each placed sound plays on one of 16 pooled 3D AudioSources (logarithmic = inverse distance)
//   - roles, clips and gains come from assets/sounds/sounds.json (copied by TownBuilder); M mutes
// Browsers start audio only after the first click or key press; Unity's WebGL player handles that.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace P3
{
    public class SoundFx : MonoBehaviour
    {
        [Serializable]
        public class Role
        {
            public string name;
            public AudioClip[] clips;
            public float gain = 1f;
            public bool ui;
        }

        const float REF_DISTANCE = 3.0f;   // m, full volume inside this distance
        const float MAX_DISTANCE = 40.0f;  // m
        const int VOICES = 16;             // positional sounds that can play at the same time

        public Role[] roles = Array.Empty<Role>();
        public bool muted;

        readonly Dictionary<string, Role> byName = new();
        AudioSource[] voices;
        AudioSource uiVoice;
        Transform ears;
        int next;

        void Awake()
        {
            foreach (var r in roles) byName[r.name] = r;
            ears = new GameObject("Ears").transform;
            ears.SetParent(transform, false);
            ears.gameObject.AddComponent<AudioListener>();
            uiVoice = gameObject.AddComponent<AudioSource>();
            uiVoice.playOnAwake = false;
            uiVoice.spatialBlend = 0f;
            voices = new AudioSource[VOICES];
            for (int i = 0; i < VOICES; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(transform, false);
                var v = go.AddComponent<AudioSource>();
                v.playOnAwake = false;
                v.spatialBlend = 1f;
                v.rolloffMode = AudioRolloffMode.Logarithmic;   // gain = ref / distance
                v.minDistance = REF_DISTANCE;
                v.maxDistance = MAX_DISTANCE;
                v.dopplerLevel = 0f;
                voices[i] = v;
            }
        }

        // call once per frame: ears at the player's head, facing where the camera faces
        public void SetListener(Vector3 position, Quaternion cameraRotation)
        {
            ears.SetPositionAndRotation(position, cameraRotation);
        }

        // play one take of `role` at world position `pos` (null = UI sound, no placement)
        public void Play(string role, Vector3? pos = null, float volume = 1f)
        {
            if (muted || !byName.TryGetValue(role, out var r) || r.clips == null || r.clips.Length == 0) return;
            var clip = r.clips[UnityEngine.Random.Range(0, r.clips.Length)];
            float gain = r.gain * volume;
            if (pos == null || r.ui)
            {
                uiVoice.PlayOneShot(clip, gain);
                return;
            }
            var v = voices[next];
            next = (next + 1) % VOICES;
            v.Stop();
            v.transform.position = pos.Value;
            v.clip = clip;
            v.volume = gain;
            v.Play();
        }

        public void ToggleMute()
        {
            muted = !muted;
            if (muted) foreach (var v in voices) v.Stop();
        }
    }
}
