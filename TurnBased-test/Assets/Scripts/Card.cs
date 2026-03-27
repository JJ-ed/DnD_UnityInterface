using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Pages {

    [System.Serializable]
    public struct DiceDetails
    {
        public int min;
        public int max;
        public DieType diceType;
        public DmgType dmgType;
    }

    [CreateAssetMenu(fileName = "New Card", menuName = "Card")]
    public class Card : ScriptableObject
    {
        public string cardName;
        public Range range;
        public int lightCost;
        public List<DiceDetails> diceDetails = new List<DiceDetails>();
        public Sprite cardSprite;
    }
    
    // Enums
    public enum Range
    {
        Melee,
        Ranged,
        Special,
        MeleeSingleUse,
        RangedSingleUse,
        SpecialSingleUse,
        MassSummation,
        MassIndividual
    }
    public enum DieType
    {
        Pierce,
        Blunt,
        Slash,
        Evade,
        Shield,
        Counter,
        SlashClash,
        PierceClash,
        BluntClash,
        ShieldClash
    }
    public enum DmgType
    {
        None,
        Red,
        Black,
        Pale,
        White,
        Mixed
    }
}


