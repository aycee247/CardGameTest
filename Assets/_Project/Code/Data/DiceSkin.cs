using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Visual skin for a set of dice: one sprite per face 1..6. A player owns 6 dice; a skin is
    /// cosmetic and unlockable. The rules engine only deals in face values, never these sprites.
    /// </summary>
    [CreateAssetMenu(fileName = "DiceSkin_", menuName = "Foundry/Dice Skin")]
    public sealed class DiceSkin : ScriptableObject
    {
        [SerializeField] private string skinId;
        [SerializeField] private string displayName;

        [Tooltip("Exactly 6 sprites, index 0 = face 1 ... index 5 = face 6.")]
        [SerializeField] private Sprite[] faceSprites = new Sprite[6];

        public string SkinId => skinId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public Sprite FaceSprite(int face)
        {
            int index = Mathf.Clamp(face, 1, 6) - 1;
            return faceSprites != null && index < faceSprites.Length ? faceSprites[index] : null;
        }
    }
}
