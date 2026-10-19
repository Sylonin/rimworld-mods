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
    public class JobGiver_GetFuel : ThinkNode_JobGiver
    {
        private static float? cachedFuelPackFuelGain;

        public static float FuelPackFuelGain
        {
            get
            {
                if (!cachedFuelPackFuelGain.HasValue)
                {
                    if (!ModsConfig.BiotechActive)
                    {
                        cachedFuelPackFuelGain = 0f;
                    }
                    else
                    {
                        object obj = GS_ThingDefOf.GS_Fuel.ingestible?.outcomeDoers?.FirstOrDefault(
                            (IngestionOutcomeDoer x) => x is IngestionOutcomeDoer_OffsetFuel
                        );
                        if (
                            !(
                                obj
                                is IngestionOutcomeDoer_OffsetFuel ingestionOutcomeDoer_OffsetFuel
                            )
                        )
                        {
                            cachedFuelPackFuelGain = 0f;
                        }
                        else
                        {
                            cachedFuelPackFuelGain = ingestionOutcomeDoer_OffsetFuel.offset;
                        }
                    }
                }
                return cachedFuelPackFuelGain.Value;
            }
        }

        public static void ResetStaticData()
        {
            cachedFuelPackFuelGain = null;
        }

        public override float GetPriority(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive)
            {
                return 0f;
            }
            if (pawn.genes?.GetFirstGeneOfType<Gene_Fuel>() == null)
            {
                return 0f;
            }
            return 9.1f;
        }

        internal static int ComputeFuelPackCount(int stackCount, float missingFuel, float fuelGain)
        {
            if (!(fuelGain > 0f))
            {
                return 0;
            }
            return Mathf.Min(stackCount, Mathf.FloorToInt(missingFuel / fuelGain));
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive)
            {
                return null;
            }
            Gene_Fuel gene_Fuel = pawn.genes?.GetFirstGeneOfType<Gene_Fuel>();
            if (gene_Fuel == null)
            {
                return null;
            }
            if (!gene_Fuel.ShouldConsumeFuelNow())
            {
                return null;
            }
            if (gene_Fuel.FuelAllowed)
            {
                Thing FuelPack = GetFuelPack(pawn);
                if (FuelPack != null)
                {
                    int count = ComputeFuelPackCount(
                        FuelPack.stackCount,
                        gene_Fuel.Max - gene_Fuel.Value,
                        FuelPackFuelGain
                    );
                    if (count <= 0)
                    {
                        return null;
                    }
                    Job job = JobMaker.MakeJob(JobDefOf.Ingest, FuelPack);
                    job.count = count;
                    return job;
                }
            }
            return null;
        }

        private Thing GetFuelPack(Pawn pawn)
        {
            Thing carriedThing = pawn.carryTracker.CarriedThing;
            if (carriedThing != null && carriedThing.def == GS_ThingDefOf.GS_Fuel)
            {
                return carriedThing;
            }
            for (int i = 0; i < pawn.inventory.innerContainer.Count; i++)
            {
                if (pawn.inventory.innerContainer[i].def == GS_ThingDefOf.GS_Fuel)
                {
                    return pawn.inventory.innerContainer[i];
                }
            }
            return GenClosest.ClosestThing_Global_Reachable(
                pawn.Position,
                pawn.Map,
                pawn.Map.listerThings.ThingsOfDef(GS_ThingDefOf.GS_Fuel),
                PathEndMode.OnCell,
                TraverseParms.For(pawn),
                9999f,
                (Thing t) => pawn.CanReserve(t) && !t.IsForbidden(pawn)
            );
        }
    }

    public class Recipe_FuelInjection : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            return thing.MapHeld != null
                && thing.MapHeld.listerThings.ThingsOfDef(GS_ThingDefOf.GS_Fuel).Count != 0
                && base.AvailableOnNow(thing, part);
        }

        public override void ConsumeIngredient(Thing ingredient, RecipeDef recipe, Map map) { }

        public override void ApplyOnPawn(
            Pawn pawn,
            BodyPartRecord part,
            Pawn billDoer,
            List<Thing> ingredients,
            Bill bill
        )
        {
            if (!ModsConfig.BiotechActive)
            {
                return;
            }
            float num = 0f;
            for (int i = 0; i < ingredients.Count; i++)
            {
                if (!ingredients[i].def.IsMedicine)
                {
                    num += JobGiver_GetFuel.FuelPackFuelGain * (float)ingredients[i].stackCount;
                }
            }
            if (num > 0f && pawn.genes?.GetFirstGeneOfType<Gene_Fuel>() != null)
            {
                FuelUtility.OffsetFuel(pawn, num);
            }
            for (int j = 0; j < ingredients.Count; j++)
            {
                ingredients[j].Destroy();
            }
        }
    }

    public class Workgiver_AdministerFuel : WorkGiver_Scanner
    {
        private const float MinLevelForFeedingFuelUnforced = 0.25f;

        private const float FuelPctMax = 0.95f;

        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Pawn pawn2) || pawn2 == pawn)
            {
                return false;
            }
            Gene_Fuel gene_Fuel = pawn2.genes?.GetFirstGeneOfType<Gene_Fuel>();
            if (gene_Fuel == null)
            {
                return false;
            }
            if (gene_Fuel.ValuePercent >= 0.95f)
            {
                return false;
            }
            if (!forced && gene_Fuel.Value >= 0.25f)
            {
                return false;
            }
            if (!FeedPatientUtility.ShouldBeFed(pawn2))
            {
                return false;
            }
            if (!gene_Fuel.FuelAllowed)
            {
                return false;
            }
            if (!gene_Fuel.ShouldConsumeFuelNow())
            {
                JobFailReason.Is("Not allowed to be refueled");
                return false;
            }
            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }
            if (
                GenClosest.ClosestThingReachable(
                    pawn.Position,
                    pawn.Map,
                    ThingRequest.ForDef(GS_ThingDefOf.GS_Fuel),
                    PathEndMode.OnCell,
                    TraverseParms.For(pawn),
                    9999f,
                    (Thing pack) => !pack.IsForbidden(pawn) && pawn.CanReserve(pack)
                ) == null
            )
            {
                JobFailReason.Is("No Fuel.");
                return false;
            }
            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn pawn2 = (Pawn)t;
            Thing thing = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(GS_ThingDefOf.GS_Fuel),
                PathEndMode.OnCell,
                TraverseParms.For(pawn),
                9999f,
                (Thing pack) => !pack.IsForbidden(pawn) && pawn.CanReserve(pack)
            );
            if (thing != null)
            {
                Job job = JobMaker.MakeJob(JobDefOf.FeedPatient, thing, pawn2);
                job.count = 1;
                return job;
            }
            return null;
        }
    }

    public class WorkGiver_Warden_DeliverFuel : WorkGiver_Warden
    {
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!ModsConfig.BiotechActive)
            {
                return null;
            }
            if (!ShouldTakeCareOfPrisoner(pawn, t))
            {
                return null;
            }
            Pawn prisoner = (Pawn)t;
            if (!prisoner.guest.CanBeBroughtFood || !prisoner.Position.IsInPrisonCell(prisoner.Map))
            {
                return null;
            }
            if (WardenFeedUtility.ShouldBeFed(prisoner))
            {
                return null;
            }
            if (!(prisoner.genes?.GetFirstGeneOfType<Gene_Fuel>() is Gene_Fuel gene_Fuel))
            {
                return null;
            }
            if (!gene_Fuel.FuelAllowed)
            {
                return null;
            }
            if (!gene_Fuel.ShouldConsumeFuelNow())
            {
                return null;
            }
            if (FuelPackAlreadyAvailableFor(prisoner))
            {
                return null;
            }
            Thing thing = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(GS_ThingDefOf.GS_Fuel),
                PathEndMode.OnCell,
                TraverseParms.For(pawn),
                9999f,
                (Thing pack) =>
                    !pack.IsForbidden(pawn)
                    && pawn.CanReserve(pack)
                    && pack.GetRoom() != prisoner.GetRoom()
            );
            if (thing == null)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(JobDefOf.DeliverFood, thing, prisoner);
            job.count = 1;
            job.targetC = RCellFinder.SpotToChewStandingNear(prisoner, thing);
            return job;
        }

        private bool FuelPackAlreadyAvailableFor(Pawn prisoner)
        {
            if (prisoner.carryTracker.CarriedCount(GS_ThingDefOf.GS_Fuel) > 0)
            {
                return true;
            }
            if (prisoner.inventory.Count(GS_ThingDefOf.GS_Fuel) > 0)
            {
                return true;
            }
            Room room = prisoner.GetRoom();
            if (room != null)
            {
                List<Region> regions = room.Regions;
                for (int i = 0; i < regions.Count; i++)
                {
                    if (regions[i].ListerThings.ThingsOfDef(GS_ThingDefOf.GS_Fuel).Count > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
