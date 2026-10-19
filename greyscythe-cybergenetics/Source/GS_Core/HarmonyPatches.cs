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
    //
    //  **new evade framework goes here**
    //

    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            Harmony harmony = new Harmony("feaurie.GS_Core");
            Type typeFromHandle = typeof(Pawn);
            harmony.Patch(
                typeFromHandle.GetMethod("PreApplyDamage"),
                new HarmonyMethod(typeof(HarmonyPatches).GetMethod("Patch_Pawn_PreApplyDamage"))
            );
        }

        public static bool Patch_Pawn_PreApplyDamage(
            Pawn __instance,
            ref DamageInfo dinfo,
            out bool absorbed
        )
        {
            absorbed = false;
            try
            {
                if (dinfo.Instigator == null)
                {
                    return true;
                }

                Pawn pawn = __instance;
                if (
                    pawn.Downed
                    || pawn.InBed()
                    || pawn.IsBurning()
                    || (pawn.stances?.stunner?.Stunned).GetValueOrDefault()
                    || (
                        !pawn.Drafted
                        && !(pawn.stances?.curStance is Stance_Busy)
                        && !(pawn.stances?.curStance is Stance_Warmup)
                    )
                )
                {
                    return true;
                }

                if (dinfo.WeaponBodyPartGroup == null)
                {
                    ThingDef weapon = dinfo.Weapon;
                    if (weapon == null || !weapon.IsMeleeWeapon)
                    {
                        if (weapon == null || !weapon.IsRangedWeapon)
                        {
                            return true;
                        }

                        float attackerSkill = 10f;
                        if (dinfo.Instigator is Pawn { skills: not null } shooter)
                        {
                            attackerSkill =
                                shooter.skills.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                        }
                        return !TryEvade(
                            pawn,
                            GS_EvadeStatDefOf.GS_Evade_EvadeProjectileChance,
                            attackerSkill,
                            SoundDefOf.BulletImpact_Ground,
                            FleckDefOf.AirPuff,
                            dinfo,
                            ref absorbed
                        );
                    }
                }

                float meleeSkill = 10f;
                if (dinfo.Instigator is Pawn attacker)
                {
                    if (attacker.skills == null)
                    {
                        if (GS_EvadeModSettings.Instance.animalSizeScaling)
                        {
                            float sizeRatio = attacker.BodySize / pawn.BodySize;
                            meleeSkill = Mathf.Clamp(10f * sizeRatio, 10f, 25f);
                        }
                    }
                    else
                    {
                        meleeSkill = attacker.skills.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                    }
                }
                return !TryEvade(
                    pawn,
                    GS_EvadeStatDefOf.GS_Evade_EvadeMeleeChance,
                    meleeSkill,
                    SoundDefOf.Crunch,
                    FleckDefOf.ExplosionFlash,
                    dinfo,
                    ref absorbed
                );
            }
            catch (Exception arg)
            {
                // Let vanilla damage handling run; returning false would skip armor and shield absorption.
                Log.ErrorOnce($"GS_Core: error in Patch_Pawn_PreApplyDamage: {arg}", 0x65C0DE18);
                absorbed = false;
            }
            return true;
        }

        internal static float ComputeEvadeMagnitude(
            float attackerSkill,
            float minMagnitude,
            float maxMagnitude
        )
        {
            return Mathf.Clamp(10f / Math.Max(attackerSkill, 1f), minMagnitude, maxMagnitude);
        }

        internal static float ComputeEvadeChance(
            float statValue,
            float magnitude,
            float maximumChance
        )
        {
            return Math.Min(statValue * magnitude, maximumChance);
        }

        private static bool TryEvade(
            Pawn pawn,
            StatDef evadeStat,
            float attackerSkill,
            SoundDef evadeSound,
            FleckDef evadeFleck,
            DamageInfo dinfo,
            ref bool absorbed
        )
        {
            if (evadeStat.Worker.IsDisabledFor(pawn))
            {
                return false;
            }

            float statValue = pawn.GetStatValue(evadeStat);
            if (!(statValue > 0f))
            {
                return false;
            }

            float magnitude = ComputeEvadeMagnitude(
                attackerSkill,
                GS_EvadeModSettings.Instance.MinEvadeMagnitude,
                GS_EvadeModSettings.Instance.MaxEvadeMagnitude
            );
            float evadeChance = ComputeEvadeChance(
                statValue,
                magnitude,
                GS_EvadeModSettings.Instance.maximumEvadeChance
            );
            if (!Rand.Chance(evadeChance))
            {
                return false;
            }

            if (pawn.Map != null)
            {
                evadeSound?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                FleckMaker.Static(
                    pawn.TrueCenter()
                        + Vector3Utility.HorizontalVectorFromAngle(dinfo.Angle).RotatedBy(180f)
                            * 0.5f,
                    scale: Mathf.Min(10f, 2f + dinfo.Amount / 10f),
                    map: pawn.Map,
                    fleckDef: evadeFleck
                );
                string moteText = GS_EvadeModSettings.Instance.verboseEvadeReadout
                    ? "GS_Evade_TextMote_Evade".Translate(
                        $"{evadeChance.ToStringPercent()}=min({statValue}*{magnitude}, {GS_EvadeModSettings.Instance.maximumEvadeChance}) (10/{Math.Max(attackerSkill, 1f)}S) "
                    )
                    : "GS_Evade_TextMote_Evade".Translate(evadeChance.ToStringPercent());
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, moteText, 1.9f);
            }

            absorbed = true;
            return true;
        }
    }
}
