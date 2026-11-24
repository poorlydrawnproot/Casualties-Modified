using System;

namespace CasualtiesSaveEditor
{
    /// <summary>
    /// The save stores limbs as a fixed 15-element array. This order was
    /// recovered from the player prefab hierarchy and the game's LimbNum
    /// roots: Head=0, UpTorso=1, DownTorso=2, ArmF=3, ArmB=6, LegF=9, LegB=12.
    /// </summary>
    public static class LimbCatalog
    {
        public static readonly string[] Keys = new string[]
        {
            "Head", "UpTorso", "DownTorso",
            "UpArmF", "DownArmF", "HandF",
            "UpArmB", "DownArmB", "HandB",
            "ThighF", "CrusF", "FootF",
            "ThighB", "CrusB", "FootB"
        };

        public static readonly string[] Labels = new string[]
        {
            "Head", "Thorax", "Abdomen",
            "Upper right arm", "Right forearm", "Right hand",
            "Upper left arm", "Left forearm", "Left hand",
            "Right thigh", "Right crus", "Right foot",
            "Left thigh", "Left crus", "Left foot"
        };

        public static int Count
        {
            get { return Keys.Length; }
        }

        public static bool IsValid(int index)
        {
            return index >= 0 && index < Keys.Length;
        }

        public static string Key(int index)
        {
            return IsValid(index) ? Keys[index] : "";
        }

        public static string Label(int index)
        {
            return IsValid(index) ? Labels[index] : "Limb " + (index + 1);
        }

        public static string FullName(int index)
        {
            if (!IsValid(index)) return "Limb " + (index + 1);
            return Labels[index] + " [" + Keys[index] + "]";
        }
    }
}
