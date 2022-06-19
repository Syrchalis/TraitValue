using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using RimWorld;
using Verse;
using UnityEngine;

namespace SyrTraitValue
{
    public static class TraitValueUtility
    {
        public static void CacheTraitDefs()
        {
            allTraits.Clear();
            allTraitDegreeDatas.Clear();
            foreach (TraitDef t in DefDatabase<TraitDef>.AllDefs.Where((TraitDef td) => td?.degreeDatas != null))
            {
                allTraits.Add(t);
                foreach (TraitDegreeData tdd in t.degreeDatas)
                {
                    allTraitDegreeDatas.Add(tdd);
                }
            }
            TraitValueCore.traitCount = allTraitDegreeDatas.Count;
        }
        public static void CacheXMLValues()
        {
            foreach (TraitDef t in allTraits)
            {
                TraitValueExtension modExtension = t.GetModExtension<TraitValueExtension>();
                if (modExtension?.traitValues != null)
                {
                    foreach (TraitDegreeData traitDegreeData in t.degreeDatas)
                    {
                        originalTraitValues.Add(t.defName + ", " + traitDegreeData.degree, modExtension.traitValues.Find(dv => dv.degree == traitDegreeData.degree).value);
                    }
                }
            }
        }

        public static void LoadSavedValues(bool reset)
        {
            if (reset)
            {
                foreach (KeyValuePair<string, int> kvp in originalTraitValues)
                {
                    //We find the last '_' and take everything before into a substring, and everything after. This is so it doesn't break if trait defnames have '_' as part of their name
                    string[] key = kvp.Key.Split(',');
                    if (key.Length != 2)
                    {
                        Log.Message(kvp.Key + " is not correctly formatted.");
                        continue;
                    }
                    TraitValueExtension modExtension = allTraits.Find(t => t.defName == key[0]).GetModExtension<TraitValueExtension>();
                    if (modExtension != null)
                    {
                        modExtension.traitValues.Find(tv => tv.degree == ParseHelper.FromString<int>(key[1])).value = kvp.Value;
                    }
                }
            }
            else
            {
                foreach (KeyValuePair<string, int> kvp in TraitValueSettings.changedTraitValues)
                {
                    string[] key = kvp.Key.Split(',');
                    TraitValueExtension modExtension = allTraits.Find(t => t.defName == key[0]).GetModExtension<TraitValueExtension>();
                    if (modExtension != null)
                    {
                        modExtension.traitValues.Find(tv => tv.degree == ParseHelper.FromString<int>(key[1])).value = kvp.Value;
                    }
                }
            }
        }
        
        public static void ColorTraitLabels()
        {
            foreach (TraitDef t in allTraits)
            {
                TraitValueExtension modExtension = t.GetModExtension<TraitValueExtension>();
                if (modExtension != null)
                {
                    foreach (TraitDegreeData traitDegreeData in t.degreeDatas)
                    {
                        foreach (DegreeValue degreeValue in modExtension.traitValues)
                        {
                            int currentTraitValue = degreeValue.value;
                            if (currentTraitValue > bestTraitValue) bestTraitValue = currentTraitValue;
                            if (currentTraitValue < worstTraitValue) worstTraitValue = currentTraitValue;
                        }
                        if (!modExtension.traitValues.NullOrEmpty() && !traitDegreeData.label.Contains("<color=#"))
                        {
                            Color color;
                            traitDegreeData.label = ValueColor(modExtension.traitValues.Find(dv => dv.degree == traitDegreeData.degree).value, out color) + traitDegreeData.label.CapitalizeFirst() + "</color>";
                        }
                    }
                }
            }
        }
        public static void UncolorTraitLabels()
        {
            foreach (TraitDef t in allTraits)
            {
                foreach (TraitDegreeData traitDegreeData in t.degreeDatas)
                {
                    string label = traitDegreeData.label;
                    if (label.Contains("<color=#"))
                    {
                        traitDegreeData.label = label.Remove(label.IndexOf("<color=#"), 15).Replace("</color>", "");
                    }
                }
            }
        }

        public static string ValueColor(int value, out Color color)
        {
            float bestTraitCutoff = bestTraitValue * 0.9f;
            if (value >= bestTraitCutoff && TraitValueSettings.useBestColor)
            {
                float t = (float)value / bestTraitValue;
                color = Color.Lerp(TraitValueSettings.goodTraitColor, TraitValueSettings.bestTraitColor, t);
                return ColorToRichText(color);
            }
            if (value >= 0)
            {
                float t = (float)value / (TraitValueSettings.useBestColor ? bestTraitCutoff : bestTraitValue);
                color = Color.Lerp(TraitValueSettings.neutralTraitColor, TraitValueSettings.goodTraitColor, t);
                return ColorToRichText(color);
            }
            if (value < 0)
            {
                float t = (float)value / worstTraitValue;
                color = Color.Lerp(TraitValueSettings.neutralTraitColor, TraitValueSettings.badTraitColor, t);
                return ColorToRichText(color);
            }
            color = Color.white;
            return "<color=#FFFFFF>";
        }

        public static string ColorToRichText(Color color)
        {
            return $"<color=#{Mathf.RoundToInt(color.r * 255):X2}{Mathf.RoundToInt(color.g * 255):X2}{Mathf.RoundToInt(color.b * 255):X2}>";
        }

        private static string getTraitsModName(TraitDef t) 
        {
            return t.modContentPack != null ? t.modContentPack.Name : "Unknown mod (trait defined with a patch?)";
        }

        public static void CountTraits()
        {
            int traitsInTotal = 0;
            int traitsWithValue = 0;
            foreach (TraitDef t in allTraits)
            {
                int degrees = t.degreeDatas.Count;
                traitsInTotal += degrees;
                TraitValueExtension modExtension = t.GetModExtension<TraitValueExtension>();
                if (modExtension != null)
                {
                    int traitValues = modExtension.traitValues.Count();
                    traitsWithValue += traitValues;
                    if (degrees != traitValues)
                    {
                        Log.Warning(getTraitsModName(t) + ": TraitDef " + t.defName.ToString() + " has " + degrees + " degrees, but " + traitValues + " values. You should configure one value per trait degree data.");
                        modExtension.traitValues.AddRange(t.degreeDatas.Where(tdd => modExtension.traitValues.FirstOrDefault(tv => tv.degree == tdd.degree) == null).Select(tdd => new DegreeValue(tdd.degree, 0)));
                    }
                    foreach (DegreeValue degreeValue in modExtension.traitValues)
                    {
                        int currentTraitValue = degreeValue.value;
                        if (currentTraitValue > bestTraitValue) bestTraitValue = currentTraitValue;
                        if (currentTraitValue < worstTraitValue) worstTraitValue = currentTraitValue;
                    }
                }
                else
                {
                    //Adds mod extension, creates a new list if no mod extensions present, then creates an extension and for each degree a DegreeValue
                    Log.Warning(getTraitsModName(t) + ": TraitDef" + t.defName.ToString() + " does not have trait values set up for integration with [SYR] Trait Values. You can ask the mod author politely to add integration, but accept if they don't want to.");         
                    t.modExtensions = (t.modExtensions ?? new List<DefModExtension>()).Append(new TraitValueExtension { traitValues = t.degreeDatas.Select(tdd => new DegreeValue(tdd.degree, 0)).ToList() }).ToList();
                }
            }
            Log.Message("[SYR] Trait Value Framework || Traits: " + traitsInTotal + " - Traits with an assigned value: " + traitsWithValue);
        }
        public static int bestTraitValue = 0;
        public static int worstTraitValue = 0;

        public static List<TraitDef> allTraits = new List<TraitDef>();
        public static List<TraitDegreeData> allTraitDegreeDatas = new List<TraitDegreeData>();
        public static Dictionary<string, int> originalTraitValues = new Dictionary<string, int>();
    }
}
