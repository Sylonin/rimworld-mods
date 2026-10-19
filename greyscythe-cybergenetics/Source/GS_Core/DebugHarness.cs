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
    // Dev-mode test harness: one button per subsystem from the manual test list.
    // Buttons appear on any pawn's gizmo bar when dev gizmos are visible, grant
    // what they need, and drive the behavior so each system can be exercised
    // without the debug palette. No-op in normal play.
    [StaticConstructorOnStartup]
    public static class DebugTestHarness
    {
        static DebugTestHarness()
        {
            Harmony harmony = new Harmony("feaurie.GS_Core.TestHarness");
            harmony.Patch(
                typeof(Pawn).GetMethod("GetGizmos"),
                postfix: new HarmonyMethod(typeof(DebugTestHarness).GetMethod("GetGizmosPostfix"))
            );
        }

        public static void GetGizmosPostfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (!DebugSettings.ShowDevGizmos)
            {
                return;
            }
            Pawn pawn = __instance;
            if (pawn?.Map == null || pawn.Dead)
            {
                return;
            }
            List<Gizmo> gizmos = __result?.ToList() ?? new List<Gizmo>();
            gizmos.Add(
                TestButton(
                    pawn,
                    "DEV: GS fuel setup",
                    "Grant GS_Powered, fill fuel, drop 5 cells nearby.",
                    () => FuelSetup(pawn)
                )
            );
            gizmos.Add(
                TestButton(
                    pawn,
                    "DEV: GS toggle buff",
                    "Grant Charge gene and flip its ability on.",
                    () => ToggleBuff(pawn)
                )
            );
            gizmos.Add(
                TestButton(
                    pawn,
                    "DEV: GS vent blast",
                    "Grant plasma vent gene and cast it at own position.",
                    () => VentBlast(pawn)
                )
            );
            gizmos.Add(
                TestButton(
                    pawn,
                    "DEV: GS leap",
                    "Grant leap gene and cast it 10 cells east.",
                    () => Leap(pawn)
                )
            );
            gizmos.Add(
                TestButton(
                    pawn,
                    "DEV: GS overshield",
                    "Cast the overshield ring at own position.",
                    () => Overshield(pawn)
                )
            );
            gizmos.Add(
                TestButton(
                    pawn,
                    "DEV: GS force evade",
                    "Max both evade stats, then hit self for 10 ranged damage.",
                    () => ForceEvade(pawn)
                )
            );
            gizmos.Add(
                TestButton(
                    pawn,
                    "DEV: GS down self",
                    "Down self to check buff auto-removal.",
                    () => DownSelf(pawn)
                )
            );
            gizmos.Add(
                TestButton(
                    pawn,
                    "DEV: GS care setup",
                    "Down self, allow fuel, drop cells for warden/doctor.",
                    () => CareSetup(pawn)
                )
            );
            gizmos.Add(
                TestButton(
                    pawn,
                    "DEV: GS inject fuel",
                    "Run fuel injection on self with cells in inventory.",
                    () => InjectFuel(pawn)
                )
            );
            __result = gizmos;
        }

        private static Command_Action TestButton(Pawn pawn, string label, string desc, Action act)
        {
            return new Command_Action
            {
                defaultLabel = label,
                defaultDesc = desc,
                action = delegate
                {
                    try
                    {
                        act();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"GS test harness '{label}' failed: {ex}");
                    }
                },
            };
        }

        private static GeneDef NamedGene(string defName)
        {
            return DefDatabase<GeneDef>.GetNamed(defName, false);
        }

        private static AbilityDef NamedAbility(string defName)
        {
            return DefDatabase<AbilityDef>.GetNamed(defName, false);
        }

        private static void EnsureGene(Pawn pawn, string defName)
        {
            GeneDef def = NamedGene(defName);
            if (def == null || pawn.genes == null || pawn.genes.HasActiveGene(def))
            {
                return;
            }
            pawn.genes.AddGene(def, false);
        }

        private static void EnsureAbility(Pawn pawn, string defName)
        {
            AbilityDef def = NamedAbility(defName);
            if (def == null || pawn.abilities == null || pawn.abilities.GetAbility(def) != null)
            {
                return;
            }
            pawn.abilities.GainAbility(def);
        }

        private static void DropFuel(Pawn pawn, int count)
        {
            Thing cells = ThingMaker.MakeThing(GS_ThingDefOf.GS_Fuel);
            cells.stackCount = count;
            GenPlace.TryPlaceThing(cells, pawn.Position, pawn.Map, ThingPlaceMode.Near);
        }

        private static void FuelSetup(Pawn pawn)
        {
            EnsureGene(pawn, "GS_Powered");
            Gene_Fuel fuel = pawn.genes?.GetFirstGeneOfType<Gene_Fuel>();
            if (fuel != null)
            {
                fuel.Value = fuel.Max;
            }
            DropFuel(pawn, 5);
        }

        private static void ToggleBuff(Pawn pawn)
        {
            EnsureGene(pawn, "GS_Powered");
            EnsureGene(pawn, "GS_Powered_Charge");
            FuelSetup(pawn);
            EnsureAbility(pawn, "GS_ChargeBoostA");
            Ability ability = pawn.abilities?.GetAbility(NamedAbility("GS_ChargeBoostA"));
            if (ability is Ability_FuelToggleBuff toggle && !toggle.active)
            {
                toggle.Activate(pawn, LocalTargetInfo.Invalid);
            }
        }

        private static void VentBlast(Pawn pawn)
        {
            EnsureGene(pawn, "GS_Powered");
            EnsureGene(pawn, "GS_Powered_VentPlas");
            FuelSetup(pawn);
            EnsureAbility(pawn, "GS_Powered_PlasmaVentA");
            Ability ability = pawn.abilities?.GetAbility(NamedAbility("GS_Powered_PlasmaVentA"));
            ability?.Activate(pawn, LocalTargetInfo.Invalid);
        }

        private static void Leap(Pawn pawn)
        {
            EnsureGene(pawn, "GS_Powered");
            EnsureGene(pawn, "GS_Powered_Leap");
            FuelSetup(pawn);
            EnsureAbility(pawn, "GS_Powered_LeapA");
            Ability ability = pawn.abilities?.GetAbility(NamedAbility("GS_Powered_LeapA"));
            IntVec3 dest = pawn.Position + IntVec3.East * 10;
            if (ability != null && dest.InBounds(pawn.Map))
            {
                ability.Activate(new LocalTargetInfo(dest), LocalTargetInfo.Invalid);
            }
        }

        private static void Overshield(Pawn pawn)
        {
            AbilityDef def = DefDatabase<AbilityDef>.AllDefsListForReading.FirstOrDefault(
                (AbilityDef d) => d.defName == "GS_Bionics_overshield"
            );
            if (def == null)
            {
                Log.Warning(
                    "GS test harness: GS_Bionics_overshield not loaded (bionics mod missing?)."
                );
                return;
            }
            FuelSetup(pawn);
            EnsureAbility(pawn, def.defName);
            pawn.abilities?.GetAbility(def)?.Activate(pawn, LocalTargetInfo.Invalid);
        }

        private static void ForceEvade(Pawn pawn)
        {
            pawn.health.AddHediff(GS_HediffDefOf.GS_ChargeBoostHediff);
            DamageInfo dinfo = new DamageInfo(
                DamageDefOf.Bullet,
                10f,
                0f,
                -1f,
                pawn,
                null,
                DefDatabase<ThingDef>.GetNamed("Gun_ChargeRifle", false)
            );
            pawn.TakeDamage(dinfo);
        }

        private static void DownSelf(Pawn pawn)
        {
            pawn.health.AddHediff(GS_HediffDefOf.GS_ChargeBoostHediff);
            HealthUtility.DamageUntilDowned(pawn, false);
        }

        private static void CareSetup(Pawn pawn)
        {
            EnsureGene(pawn, "GS_Powered");
            Gene_Fuel fuel = pawn.genes?.GetFirstGeneOfType<Gene_Fuel>();
            if (fuel != null)
            {
                fuel.FuelAllowed = true;
                fuel.Value = 0f;
            }
            HealthUtility.DamageUntilDowned(pawn, false);
            DropFuel(pawn, 5);
        }

        private static void InjectFuel(Pawn pawn)
        {
            EnsureGene(pawn, "GS_Powered");
            Thing cells = ThingMaker.MakeThing(GS_ThingDefOf.GS_Fuel);
            cells.stackCount = 3;
            pawn.inventory.innerContainer.TryAdd(cells);
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamed("Recipe_FuelInjection", false);
            if (recipe == null)
            {
                Log.Warning("GS test harness: Recipe_FuelInjection missing.");
                return;
            }
            Bill_Medical bill = new Bill_Medical(recipe, null);
            pawn.BillStack?.AddBill(bill);
        }
    }
}
