using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    public enum UiEase { Linear, SmoothStep, OutCubic, OutBack }

    /// <summary>Identifies a running animation. <c>default</c> is the always-finished handle.</summary>
    public readonly struct AnimHandle
    {
        internal readonly int Id;
        internal AnimHandle(int id) { Id = id; }
        public bool IsValid => Id != 0;
    }

    /// <summary>
    /// The one place gameplay tweens run (docs/design/ui-conventions.md): because everything
    /// routes through here, the reduced-motion and animation-speed settings apply to all of it
    /// rather than most of it. <see cref="Play"/> drives an <c>apply(t)</c> callback with eased
    /// t 0→1; <see cref="Skip"/> finishes immediately — every animation is interruptible, and
    /// interruption snaps to the end state, never abandons mid-flight. Presentation only: an
    /// apply callback must never touch match state.
    /// </summary>
    public sealed class UiAnimationService : MonoBehaviour
    {
        private sealed class Anim
        {
            public int Id;
            public float Elapsed;
            public float Duration;
            public UiEase Ease;
            public Action<float> Apply;
            public Action Completed;
            public bool Loop;   // loops emit the raw 0..1 phase forever and never complete
        }

        private readonly List<Anim> _anims = new List<Anim>();
        private int _nextId = 1;

        /// <summary>Collapses every Play to its end state and parks every Loop at rest.</summary>
        public bool ReducedMotion { get; set; }

        /// <summary>Scales every duration; 2 means twice as fast.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        public AnimHandle Play(float duration, UiEase ease, Action<float> apply, Action completed = null)
        {
            if (apply == null) return default;

            if (ReducedMotion || duration <= 0f)
            {
                apply(1f);
                completed?.Invoke();
                return default;
            }

            var anim = new Anim
            {
                Id = _nextId++, Duration = duration, Ease = ease,
                Apply = apply, Completed = completed
            };
            _anims.Add(anim);
            apply(0f);
            return new AnimHandle(anim.Id);
        }

        /// <summary>
        /// Endless pulse/glow: <c>apply</c> receives the 0→1 phase once per period. The caller
        /// owns the lifetime and stops it with <see cref="Skip"/>; phase 0 is the rest state.
        /// </summary>
        public AnimHandle Loop(float period, Action<float> apply)
        {
            if (apply == null) return default;

            if (ReducedMotion || period <= 0f)
            {
                apply(0f);
                return default;
            }

            var anim = new Anim { Id = _nextId++, Duration = period, Apply = apply, Loop = true };
            _anims.Add(anim);
            apply(0f);
            return new AnimHandle(anim.Id);
        }

        /// <summary>Finish now: a Play applies t=1 and completes; a Loop settles at rest.</summary>
        public void Skip(AnimHandle handle)
        {
            if (!handle.IsValid) return;

            for (int i = 0; i < _anims.Count; i++)
            {
                if (_anims[i].Id != handle.Id) continue;
                var anim = _anims[i];
                _anims.RemoveAt(i);
                Finish(anim);
                return;
            }
        }

        public void SkipAll()
        {
            if (_anims.Count == 0) return;
            var finishing = _anims.ToArray();
            _anims.Clear();
            foreach (var anim in finishing) Finish(anim);
        }

        private static void Finish(Anim anim)
        {
            if (anim.Loop) anim.Apply(0f);
            else
            {
                anim.Apply(1f);
                anim.Completed?.Invoke();
            }
        }

        private void Update()
        {
            // Unscaled: pausing gameplay must not freeze UI feedback. Count is captured once per
            // frame, so an apply callback that starts a new animation schedules it for next frame.
            float dt = Time.unscaledDeltaTime * Mathf.Max(0.01f, SpeedMultiplier);

            for (int i = _anims.Count - 1; i >= 0; i--)
            {
                var anim = _anims[i];
                anim.Elapsed += dt;

                if (anim.Loop)
                {
                    anim.Apply(Mathf.Repeat(anim.Elapsed / anim.Duration, 1f));
                    continue;
                }

                float t = Mathf.Clamp01(anim.Elapsed / anim.Duration);

                if (t < 1f)
                {
                    anim.Apply(Evaluate(anim.Ease, t));
                    continue;
                }

                _anims.RemoveAt(i);
                anim.Apply(1f);
                anim.Completed?.Invoke();
            }
        }

        private static float Evaluate(UiEase ease, float t)
        {
            switch (ease)
            {
                case UiEase.SmoothStep: return Mathf.SmoothStep(0f, 1f, t);
                case UiEase.OutCubic: { float u = 1f - t; return 1f - u * u * u; }
                case UiEase.OutBack: { float u = t - 1f; return 1f + u * u * (2.70158f * u + 1.70158f); }
                default: return t;
            }
        }
    }
}
