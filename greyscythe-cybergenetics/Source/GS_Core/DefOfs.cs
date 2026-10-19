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
    [DefOf]
    public static class GS_HediffDefOf
    {
        public static HediffDef GS_Powered;

        public static HediffDef GS_ChargeBoostHediff;

        public static HediffDef GS_OverdriveHediff;

        public static HediffDef GS_GuardianHediff;

        static GS_HediffDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(GS_HediffDefOf));
        }
    }

    [DefOf]
    public static class GS_StatDefOf
    {
        public static StatDef GS_FuelGainFactor;

        static GS_StatDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(GS_StatDefOf));
        }
    }

    [DefOf]
    public static class GS_ThingDefOf
    {
        public static ThingDef GS_Fuel;

        [MayRequire("feaurie.GreyscytheBionics")]
        public static ThingDef GS_ShieldProjector;

        static GS_ThingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(GS_ThingDefOf));
        }
    }

    [DefOf]
    public static class GS_EvadeStatDefOf
    {
        public static StatDef GS_Evade_EvadeProjectileChance;

        public static StatDef GS_Evade_EvadeMeleeChance;
    }
}
