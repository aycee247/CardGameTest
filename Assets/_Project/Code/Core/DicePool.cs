using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// One player's dice for the current round: a mutable set of faces plus which of them have
    /// already been spent paying for a card.
    ///
    /// This is the counterpart to <see cref="DiceRoll"/>, not a replacement for it. The pool is
    /// mutable because the Shape phase edits individual dice in place and commits address dice by
    /// index; <see cref="DiceRoll"/> stays immutable and is what requirement matchers evaluate.
    /// <see cref="Subset"/> is the bridge between the two.
    /// </summary>
    public sealed class DicePool
    {
        private int[] _faces;
        private bool[] _spent;

        public DicePool(int size)
        {
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
            _faces = new int[size];
            _spent = new bool[size];
            for (int i = 0; i < size; i++) _faces[i] = DiceRoll.MinFace;
        }

        public int Count => _faces.Length;

        public int FaceAt(int index) => _faces[index];

        public bool IsSpent(int index) => _spent[index];

        public bool IsValidIndex(int index) => index >= 0 && index < _faces.Length;

        public IReadOnlyList<int> Faces => _faces;

        public int UnspentCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _spent.Length; i++) if (!_spent[i]) n++;
                return n;
            }
        }

        /// <summary>Grows or shrinks the pool, preserving existing faces. Used when a Capacity card lands.</summary>
        internal void Resize(int size)
        {
            if (size == _faces.Length) return;

            var faces = new int[size];
            var spent = new bool[size];
            int keep = Math.Min(size, _faces.Length);

            Array.Copy(_faces, faces, keep);
            Array.Copy(_spent, spent, keep);
            for (int i = keep; i < size; i++) faces[i] = DiceRoll.MinFace;

            _faces = faces;
            _spent = spent;
        }

        /// <summary>Rolls every die and clears all spent marks. Called once per round in the Roll phase.</summary>
        internal void RollAll(IDiceRoller roller)
        {
            if (roller == null) throw new ArgumentNullException(nameof(roller));
            var roll = roller.Roll(_faces.Length);
            for (int i = 0; i < _faces.Length; i++)
            {
                _faces[i] = roll[i];
                _spent[i] = false;
            }
        }

        internal void SetFace(int index, int face)
        {
            if (face < DiceRoll.MinFace || face > DiceRoll.MaxFace)
                throw new ArgumentOutOfRangeException(nameof(face));
            _faces[index] = face;
        }

        internal void MarkSpent(IReadOnlyList<int> indices)
        {
            for (int i = 0; i < indices.Count; i++) _spent[indices[i]] = true;
        }

        internal void ClearSpent()
        {
            for (int i = 0; i < _spent.Length; i++) _spent[i] = false;
        }

        /// <summary>Faces at the given indices, as the immutable roll a requirement can evaluate.</summary>
        public DiceRoll Subset(IReadOnlyList<int> indices)
        {
            var values = new int[indices.Count];
            for (int i = 0; i < indices.Count; i++) values[i] = _faces[indices[i]];
            return new DiceRoll(values);
        }

        /// <summary>Every die not yet spent this round.</summary>
        public DiceRoll UnspentRoll()
        {
            var values = new List<int>(_faces.Length);
            for (int i = 0; i < _faces.Length; i++) if (!_spent[i]) values.Add(_faces[i]);
            return new DiceRoll(values);
        }

        public int[] FacesCopy() => (int[])_faces.Clone();
        public bool[] SpentCopy() => (bool[])_spent.Clone();

        public override string ToString()
        {
            var parts = new string[_faces.Length];
            for (int i = 0; i < _faces.Length; i++)
                parts[i] = _spent[i] ? $"({_faces[i]})" : _faces[i].ToString();
            return "[" + string.Join(",", parts) + "]";
        }
    }
}
