using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// The board's sound set, one clip per beat (STORY-3.3). Generated — clips are synthesized
    /// by <c>Foundry ▸ Generate Sound Effects</c> and assigned here by the same tool, so the
    /// soundscape is code like everything else in this project and cannot drift from it.
    /// </summary>
    [CreateAssetMenu(menuName = "Foundry/Sfx Catalog", fileName = "SfxCatalog")]
    public sealed class SfxCatalog : ScriptableObject
    {
        [Header("Dice")]
        public AudioClip diceClatter;    // the server roll landing
        public AudioClip dieSelect;      // tapping a die
        public AudioClip dieSettle;      // the tray settling after the roll

        [Header("Actions")]
        public AudioClip commitThunk;    // committing a card — the press
        public AudioClip contestLost;    // losing a contested claim
        public AudioClip claimTing;      // winning a card — the anvil ting

        [Header("Round")]
        public AudioClip revealFlip;     // the spotlight card flipping in
        public AudioClip sparksChime;    // sparks paid out
        public AudioClip roundWhistle;   // end of round — the factory whistle
    }
}
