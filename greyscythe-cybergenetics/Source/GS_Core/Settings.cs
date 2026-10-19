using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace GS_Core
{
    public class GS_Evade : Mod
    {
        private string maxEvadeChanceBuffer;

        public GS_Evade(ModContentPack content)
            : base(content)
        {
            GS_EvadeModSettings.Instance = GetSettings<GS_EvadeModSettings>();
            if (GS_EvadeModSettings.Instance != null)
            {
                maxEvadeChanceBuffer = GS_EvadeModSettings.Instance.maximumEvadeChance.ToString();
            }
        }

        public override string SettingsCategory()
        {
            return "GS_Evade".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.TextFieldNumericLabeled(
                "GS_Evade_MaxEvadeChance".Translate(),
                ref GS_EvadeModSettings.Instance.maximumEvadeChance,
                ref maxEvadeChanceBuffer,
                0.01f,
                1f
            );
            listing.CheckboxLabeled(
                "GS_Evade_AnimalSizeScaling".Translate(),
                ref GS_EvadeModSettings.Instance.animalSizeScaling
            );
            listing.CheckboxLabeled(
                "GS_Evade_VerboseEvadeReadout".Translate(),
                ref GS_EvadeModSettings.Instance.verboseEvadeReadout
            );
            listing.End();
        }
    }

    public class GS_EvadeModSettings : ModSettings
    {
        public static GS_EvadeModSettings Instance;

        public bool animalSizeScaling = true;

        public float maximumEvadeChance = 0.9f;

        public float EvadeMagnitude = 2f;

        public bool verboseEvadeReadout = false;

        public float MinEvadeMagnitude => 1f / EvadeMagnitude;

        public float MaxEvadeMagnitude => EvadeMagnitude;

        public GS_EvadeModSettings()
        {
            Instance = this;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref verboseEvadeReadout, "verboseEvadeReadout", defaultValue: false);
            Scribe_Values.Look(ref maximumEvadeChance, "maximumEvadeChance", 0.9f);
            Scribe_Values.Look(ref animalSizeScaling, "animalSizeScaling", defaultValue: true);
            Scribe_Values.Look(ref EvadeMagnitude, "EvadeMagnitude", 2f);
        }
    }
}
