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
    [StaticConstructorOnStartup]
    public class CommandAbilityToggle : Command_Toggle
    {
        public static readonly Texture2D CooldownTex = SolidColorMaterials.NewSolidColorTexture(
            new Color(1f, 1f, 1f, 0.1f)
        );

        public Ability ability;

        public Pawn pawn;

        public CommandAbilityToggle(Pawn pawn, Ability ability)
        {
            string reason = "";
            this.pawn = pawn;
            this.ability = ability;
            defaultLabel = ability.def.LabelCap;
            defaultDesc = ability.def.description;
            icon = ability.def.uiIcon;
            disabled = ability.GizmoDisabled(out reason);
            disabledReason = reason.Colorize(Color.red);
            Order = 10f;
        }

        protected override GizmoResult GizmoOnGUIInt(Rect butRect, GizmoRenderParms parms)
        {
            GizmoResult result = base.GizmoOnGUIInt(butRect, parms);
            int min = ability.def.cooldownTicksRange.min;
            if (disabled && min > Find.TickManager.TicksGame)
            {
                GUI.DrawTexture(
                    butRect.RightPartPixels(
                        butRect.width * ((float)ability.CooldownTicksRemaining / (float)min)
                    ),
                    CooldownTex
                );
            }
            return result;
        }
    }

    public class Ability_FuelToggleBuff : Ability
    {
        public bool active;

        private Gene_Fuel cachedGene;

        public Gene_Fuel Gene
        {
            get
            {
                if (cachedGene == null || cachedGene.pawn != pawn)
                {
                    cachedGene = pawn.genes?.GetFirstGeneOfType<Gene_Fuel>();
                }
                return cachedGene;
            }
        }

        public Ability_FuelToggleBuff(Pawn pawn)
            : base(pawn) { }

        public Ability_FuelToggleBuff(Pawn pawn, AbilityDef def)
            : base(pawn, def) { }

        public CompProperties_AbilityFuelBuff BuffProps =>
            def.comps?.OfType<CompProperties_AbilityFuelBuff>().FirstOrDefault();

        public override IEnumerable<Command> GetGizmos()
        {
            CompProperties_AbilityFuelBuff buff = BuffProps;
            string onKey = buff?.labelOnKey ?? "FuelBuff_On";
            string offKey = buff?.labelOffKey ?? "FuelBuff_Off";
            yield return new CommandAbilityToggle(pawn, this)
            {
                defaultLabel = (active ? offKey : onKey).Translate(),
                toggleAction = delegate
                {
                    active = !active;
                    Activate(pawn, LocalTargetInfo.Invalid);
                },
                isActive = () => active,
            };
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            bool result = base.Activate(target, dest);
            ApplyHediff(target, dest);
            if (active)
            {
                StartCooldown(300);
            }
            return result;
        }

        public override void AbilityTick()
        {
            base.AbilityTick();
            if (active)
            {
                if (pawn.Downed)
                {
                    active = false;
                    ApplyHediff(new LocalTargetInfo(pawn), LocalTargetInfo.Invalid);
                }
                else if (pawn.IsHashIntervalTick(300))
                {
                    Gene_Fuel fuel = Gene;
                    if (fuel == null)
                    {
                        active = false;
                        ApplyHediff(new LocalTargetInfo(pawn), LocalTargetInfo.Invalid);
                    }
                    else
                    {
                        fuel.Value += -0.02f;
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref active, "active", false);
        }

        public void ApplyHediff(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn targetPawn = target.Thing as Pawn ?? pawn;
            CompProperties_AbilityFuelBuff buff = BuffProps;
            if (buff?.buffHediff == null)
            {
                return;
            }
            if (active)
            {
                Hediff_FuelBuff buffHediff =
                    HediffMaker.MakeHediff(buff.buffHediff, targetPawn) as Hediff_FuelBuff;
                buffHediff.ability = this;
                targetPawn.health.AddHediff(buffHediff);
            }
            else
            {
                Hediff existing = targetPawn.health.hediffSet.GetFirstHediffOfDef(buff.buffHediff);
                if (existing != null)
                {
                    targetPawn.health.RemoveHediff(existing);
                }
            }
        }
    }

    public class CompProperties_AbilityFuelBuff : CompProperties_AbilityEffect
    {
        public HediffDef buffHediff;

        public string labelOnKey = "FuelBuff_On";

        public string labelOffKey = "FuelBuff_Off";

        public CompProperties_AbilityFuelBuff()
        {
            compClass = typeof(CompAbilityEffect);
        }
    }

    public class Hediff_FuelBuff : HediffWithComps
    {
        public Ability_FuelToggleBuff ability;

        private Gene_Fuel cacheGene;

        public override bool ShouldRemove =>
            gene == null || gene.Value <= 0f || ability == null || !ability.active;

        public override bool Visible => true;

        public Gene_Fuel gene
        {
            get
            {
                if (cacheGene == null || cacheGene.pawn != pawn)
                {
                    cacheGene = pawn.genes?.GetFirstGeneOfType<Gene_Fuel>();
                }
                return cacheGene;
            }
            set { cacheGene = value; }
        }

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            StatDef armorStat = dinfo.Def?.armorCategory?.armorRatingStat;
            Gene_Fuel fuel = gene;
            if (
                fuel != null
                && !dinfo.IgnoreArmor
                && (
                    armorStat == StatDefOf.ArmorRating_Blunt
                    || armorStat == StatDefOf.ArmorRating_Sharp
                )
            )
            {
                float num = Mathf.Clamp(totalDamageDealt / 5f / 100f, 0.005f, 0.03f);
                fuel.Value -= num;
            }
        }

        public override void Tick()
        {
            base.Tick();
            Gene_Fuel fuel = gene;
            if (fuel != null)
            {
                Severity = fuel.Value;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ability, "ability");
        }
    }

    //
    //  **Plasma vent
    //
    public class CompProperties_AbilitySelfBomb : CompProperties_AbilityEffect
    {
        public float radius = 6f;

        public DamageDef damageType;

        public int damageAmount = -1;

        public float damagePenetration = -1f;

        public bool damageFalloff = false;

        public SoundDef soundCreated = null;

        public ThingDef thingCreated = null;

        public float thingCreatedChance = 0f;

        public GasType? gasType = null;

        public float chanceToStartFire = 0f;

        public bool damageSelf = false;

        public CompProperties_AbilitySelfBomb()
        {
            compClass = typeof(CompAbilityEffect_SelfBomb);
        }
    }

    public abstract class CompAbilityEffect_SelfTargetAI : CompAbilityEffect
    {
        protected Pawn Pawn => parent.pawn;

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            if (Pawn.Faction == Faction.OfPlayer)
            {
                return false;
            }
            if (target.HasThing && target.Thing is Pawn pawn)
            {
                return pawn.TargetCurrentlyAimingAt == Pawn;
            }
            return false;
        }
    }

    public class CompAbilityEffect_SelfBomb : CompAbilityEffect_SelfTargetAI
    {
        private new CompProperties_AbilitySelfBomb Props => (CompProperties_AbilitySelfBomb)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            List<Thing> list = new List<Thing>();
            if (!Props.damageSelf)
            {
                list.Add(parent.pawn);
            }

            GenExplosion.DoExplosion(
                center: Pawn.Position,
                map: Pawn.MapHeld,
                radius: Props.radius,
                damType: Props.damageType,
                instigator: Pawn,
                damAmount: Props.damageAmount,
                armorPenetration: Props.damagePenetration,
                explosionSound: Props.soundCreated,
                postExplosionSpawnThingDef: Props.thingCreated,
                postExplosionSpawnChance: Props.thingCreatedChance,
                postExplosionSpawnThingCount: 1,
                postExplosionGasType: Props.gasType,
                applyDamageToExplosionCellsNeighbors: false,
                chanceToStartFire: Props.chanceToStartFire,
                damageFalloff: Props.damageFalloff,
                ignoredThings: list
            );
            base.Apply(target, dest);
        }
    }

    //
    // **Shield**
    //
    public class CompProperties_AbilityShield : CompProperties_AbilityEffect
    {
        public float radius = 2f;

        public CompProperties_AbilityShield()
        {
            compClass = typeof(CompAbilityEffect_DeployShield);
        }
    }

    public class CompAbilityEffect_DeployShield : CompAbilityEffect_SelfTargetAI
    {
        private new CompProperties_AbilityShield Props => (CompProperties_AbilityShield)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Map map = Pawn.Map;
            if (map == null)
            {
                return;
            }
            int spawned = 0;
            int num = GenRadial.NumCellsInRadius(Props.radius);
            for (int i = 0; i < num; i++)
            {
                IntVec3 intVec = Pawn.Position + GenRadial.RadialPattern[i];
                if (
                    intVec.IsValid
                    && intVec.InBounds(map)
                    && intVec.Walkable(map)
                    && intVec.GetFirstThing(map, GS_ThingDefOf.GS_ShieldProjector) == null
                )
                {
                    SpawnEffect(GenSpawn.Spawn(GS_ThingDefOf.GS_ShieldProjector, intVec, map));
                    spawned++;
                }
            }
            if (spawned == 0)
            {
                Messages.Message(
                    "AbilityNotEnoughFreeSpace".Translate(),
                    Pawn,
                    MessageTypeDefOf.RejectInput,
                    historical: false
                );
            }
        }

        private static void SpawnEffect(Thing projector)
        {
            FleckMaker.Static(
                projector.TrueCenter(),
                projector.Map,
                FleckDefOf.BroadshieldActivation
            );
            SoundDefOf.Broadshield_Startup.PlayOneShot(
                new TargetInfo(projector.Position, projector.Map)
            );
        }
    }
}
