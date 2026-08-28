using System;
using System.IO;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Synthesizes the board's sound set from DSP code and writes it as WAV assets, then builds
    /// the <see cref="SfxCatalog"/> that maps beats to clips (STORY-3.3, UI-character P4).
    /// Same philosophy as the icon and theme generators: the soundscape is code, tunable in one
    /// place, with zero licensing and zero binary blobs of unknown origin.
    ///
    /// The palette is deliberately industrial-whimsical: clatters, thunks, ratchets, an anvil
    /// ting and a two-note factory whistle — short, lowpassed, and quiet enough to live under
    /// music later. A fixed seed keeps regeneration byte-stable.
    /// </summary>
    public static class FoundrySfxGenerator
    {
        private const string OutDir = "Assets/_Project/Audio/Generated";
        private const string CatalogPath = "Assets/_Project/Audio/SfxCatalog.asset";
        private const int Rate = 44100;

        [MenuItem("Foundry/Generate Sound Effects")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutDir);

            WriteWav("dice_clatter", DiceClatter());
            WriteWav("die_select", Tick(1800f, 0.05f, 0.5f));
            WriteWav("die_settle", Thock(210f, 0.09f, 0.55f));
            WriteWav("commit_thunk", CommitThunk());
            WriteWav("contest_lost", ContestLost());
            WriteWav("claim_ting", ClaimTing());
            WriteWav("reveal_flip", RevealFlip());
            WriteWav("sparks_chime", SparksChime());
            WriteWav("round_whistle", RoundWhistle());

            AssetDatabase.Refresh();

            var catalog = AssetDatabase.LoadAssetAtPath<SfxCatalog>(CatalogPath);
            bool fresh = catalog == null;
            if (fresh) catalog = ScriptableObject.CreateInstance<SfxCatalog>();

            catalog.diceClatter = Clip("dice_clatter");
            catalog.dieSelect = Clip("die_select");
            catalog.dieSettle = Clip("die_settle");
            catalog.commitThunk = Clip("commit_thunk");
            catalog.contestLost = Clip("contest_lost");
            catalog.claimTing = Clip("claim_ting");
            catalog.revealFlip = Clip("reveal_flip");
            catalog.sparksChime = Clip("sparks_chime");
            catalog.roundWhistle = Clip("round_whistle");

            if (fresh) AssetDatabase.CreateAsset(catalog, CatalogPath);
            else EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Foundry] 9 sound effects synthesized into {OutDir}; catalog at {CatalogPath}.");
        }

        // ------------------------------------------------------------------ the sounds

        /// <summary>Dice in a cup: a handful of staggered, decaying noise knocks.</summary>
        private static float[] DiceClatter()
        {
            var rng = new System.Random(4242);
            var buf = new float[(int)(Rate * 0.55f)];
            float[] knockTimes = { 0f, 0.07f, 0.16f, 0.22f, 0.31f, 0.4f };
            foreach (float start in knockTimes)
            {
                float gain = 0.5f - start * 0.6f;
                AddNoiseBurst(buf, rng, start, 0.05f, gain, lowpass: 0.25f);
            }
            return Normalize(buf, 0.55f);
        }

        /// <summary>The card press: a low sine drop with a soft noise front — a machine engaging.</summary>
        private static float[] CommitThunk()
        {
            var rng = new System.Random(7);
            var buf = new float[(int)(Rate * 0.22f)];
            AddNoiseBurst(buf, rng, 0f, 0.02f, 0.3f, lowpass: 0.15f);
            AddTone(buf, 0f, 0.2f, t => Mathf.Lerp(110f, 70f, t), Mathf.PI * 0.5f,
                t => Mathf.Exp(-9f * t) * 0.9f);
            return Normalize(buf, 0.7f);
        }

        /// <summary>Losing a contest: two descending clanks, metal on metal, resigned.</summary>
        private static float[] ContestLost()
        {
            var buf = new float[(int)(Rate * 0.4f)];
            AddMetal(buf, 0f, 0.16f, 420f, 0.7f);
            AddMetal(buf, 0.17f, 0.2f, 300f, 0.8f);
            return Normalize(buf, 0.55f);
        }

        /// <summary>Winning a card: the anvil ting — a bright partial pair ringing out.</summary>
        private static float[] ClaimTing()
        {
            var buf = new float[(int)(Rate * 0.7f)];
            AddTone(buf, 0f, 0.7f, _ => 1318.5f, 0f, t => Mathf.Exp(-5f * t) * 0.7f);
            AddTone(buf, 0f, 0.7f, _ => 2637f + 6f, 0.4f, t => Mathf.Exp(-7f * t) * 0.35f);
            AddTone(buf, 0f, 0.5f, _ => 3951f, 0.9f, t => Mathf.Exp(-11f * t) * 0.18f);
            return Normalize(buf, 0.5f);
        }

        /// <summary>The spotlight flip: a rising airy sweep ending in a snap.</summary>
        private static float[] RevealFlip()
        {
            var rng = new System.Random(13);
            var buf = new float[(int)(Rate * 0.3f)];
            AddSweepNoise(buf, rng, 0f, 0.24f, from: 0.06f, to: 0.5f, gain: 0.35f);
            AddNoiseBurst(buf, rng, 0.24f, 0.02f, 0.45f, lowpass: 0.6f);
            return Normalize(buf, 0.5f);
        }

        /// <summary>Sparks landing: a quick ascending three-note chime, small and bright.</summary>
        private static float[] SparksChime()
        {
            var buf = new float[(int)(Rate * 0.4f)];
            float[] notes = { 1046.5f, 1318.5f, 1568f };
            for (int n = 0; n < notes.Length; n++)
            {
                float start = n * 0.07f;
                float f = notes[n];
                AddTone(buf, start, 0.28f, _ => f, 0f, t => Mathf.Exp(-8f * t) * 0.5f);
            }
            return Normalize(buf, 0.45f);
        }

        /// <summary>End of round: the two-note factory whistle, breathy, with vibrato.</summary>
        private static float[] RoundWhistle()
        {
            var rng = new System.Random(99);
            var buf = new float[(int)(Rate * 0.85f)];
            AddWhistleNote(buf, rng, 0f, 0.38f, 523.25f);
            AddWhistleNote(buf, rng, 0.4f, 0.42f, 698.46f);
            return Normalize(buf, 0.5f);
        }

        private static float[] Tick(float freq, float length, float gain)
        {
            var buf = new float[(int)(Rate * length)];
            AddTone(buf, 0f, length, _ => freq, 0f, t => Mathf.Exp(-40f * t) * gain);
            return Normalize(buf, 0.5f);
        }

        private static float[] Thock(float freq, float length, float gain)
        {
            var rng = new System.Random(3);
            var buf = new float[(int)(Rate * length)];
            AddTone(buf, 0f, length, t => Mathf.Lerp(freq, freq * 0.8f, t), 0f,
                t => Mathf.Exp(-25f * t) * gain);
            AddNoiseBurst(buf, rng, 0f, 0.015f, 0.2f, lowpass: 0.3f);
            return Normalize(buf, 0.55f);
        }

        // ------------------------------------------------------------------ DSP helpers

        private static void AddTone(float[] buf, float start, float length,
            Func<float, float> freqAt, float phase, Func<float, float> envelope)
        {
            int s0 = (int)(start * Rate);
            int count = Mathf.Min((int)(length * Rate), buf.Length - s0);
            double angle = phase;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                angle += 2.0 * Math.PI * freqAt(t) / Rate;
                buf[s0 + i] += (float)Math.Sin(angle) * envelope(t);
            }
        }

        /// <summary>A struck metal bar: fundamental plus slightly inharmonic partials.</summary>
        private static void AddMetal(float[] buf, float start, float length, float freq, float gain)
        {
            AddTone(buf, start, length, _ => freq, 0f, t => Mathf.Exp(-10f * t) * gain);
            AddTone(buf, start, length, _ => freq * 2.76f, 0.3f, t => Mathf.Exp(-14f * t) * gain * 0.4f);
            AddTone(buf, start, length, _ => freq * 5.4f, 0.7f, t => Mathf.Exp(-20f * t) * gain * 0.2f);
        }

        private static void AddWhistleNote(float[] buf, System.Random rng, float start, float length, float freq)
        {
            AddTone(buf, start, length, t => freq * (1f + 0.012f * Mathf.Sin(t * length * 35f)), 0f,
                t => Mathf.Min(t * 12f, 1f) * Mathf.Exp(-2.2f * t) * 0.6f);
            AddTone(buf, start, length, t => 2f * freq, 0.2f,
                t => Mathf.Min(t * 12f, 1f) * Mathf.Exp(-3f * t) * 0.2f);
            AddSweepNoise(buf, rng, start, length, 0.3f, 0.3f, 0.06f);   // breath
        }

        private static void AddNoiseBurst(float[] buf, System.Random rng, float start, float length,
            float gain, float lowpass)
        {
            int s0 = (int)(start * Rate);
            int count = Mathf.Min((int)(length * Rate), buf.Length - s0);
            float y = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                y += lowpass * (white - y);
                buf[s0 + i] += y * Mathf.Exp(-6f * t) * gain;
            }
        }

        /// <summary>Filtered noise whose lowpass coefficient sweeps — an airy whoosh.</summary>
        private static void AddSweepNoise(float[] buf, System.Random rng, float start, float length,
            float from, float to, float gain)
        {
            int s0 = (int)(start * Rate);
            int count = Mathf.Min((int)(length * Rate), buf.Length - s0);
            float y = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                y += Mathf.Lerp(from, to, t) * (white - y);
                buf[s0 + i] += y * Mathf.Sin(Mathf.PI * t) * gain;
            }
        }

        private static float[] Normalize(float[] buf, float peak)
        {
            float max = 0f;
            for (int i = 0; i < buf.Length; i++) max = Mathf.Max(max, Mathf.Abs(buf[i]));
            if (max <= 0f) return buf;
            float scale = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= scale;
            return buf;
        }

        // ------------------------------------------------------------------ WAV out

        private static void WriteWav(string name, float[] samples)
        {
            string path = $"{OutDir}/{name}.wav";
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                int dataBytes = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataBytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);          // PCM
                writer.Write((short)1);          // mono
                writer.Write(Rate);
                writer.Write(Rate * 2);          // byte rate
                writer.Write((short)2);          // block align
                writer.Write((short)16);         // bits
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataBytes);
                for (int i = 0; i < samples.Length; i++)
                    writer.Write((short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
            }
        }

        private static AudioClip Clip(string name) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"{OutDir}/{name}.wav");
    }
}
