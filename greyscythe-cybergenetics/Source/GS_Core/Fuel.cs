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
    /// <summary>
    ///     **Fuel system
    /// </summary>
    [StaticConstructorOnStartup]
    public static class FuelUtility
    {
        public static void OffsetFuel(Pawn pawn, float offset)
        {
            if (!ModsConfig.BiotechActive)
            {
                return;
            }
            Pawn_GeneTracker genes = pawn.genes;
            Gene_Fuel gene_Fuel = genes?.GetFirstGeneOfType<Gene_Fuel>();
            if (gene_Fuel != null)
            {
                gene_Fuel.Value += offset;
            }
        }

        public static void OffsetFuelWithModifier(Pawn pawn, float offset)
        {
            offset *= pawn.GetStatValue(GS_StatDefOf.GS_FuelGainFactor);
            OffsetFuel(pawn, offset);
        }

        public static float FuelCost(Ability ab)
        {
            if (ab.comps != null)
            {
                foreach (AbilityComp comp in ab.comps)
                {
                    if (comp is CompAbilityEffect_FuelCost compAbilityEffect_FuelCost)
                    {
                        return compAbilityEffect_FuelCost.Props.FuelCost;
                    }
                }
            }
            return 0f;
        }

        public static void TickResourceDrain(IGeneResourceDrain drain)
        {
            if (drain.CanOffset && drain.Resource != null)
            {
                OffsetResource(drain, (0f - drain.ResourceLossPerDay) / 60000f);
            }
        }

        public static void PostResourceOffset(IGeneResourceDrain drain, float oldValue)
        {
            if (oldValue > 0f && drain.Resource.Value <= 0f)
            {
                Pawn pawn = drain.Pawn;
                if (!pawn.health.hediffSet.HasHediff(GS_HediffDefOf.GS_Powered))
                {
                    pawn.health.AddHediff(GS_HediffDefOf.GS_Powered);
                }
            }
        }

        public static void OffsetResource(IGeneResourceDrain drain, float amnt)
        {
            float value = drain.Resource.Value;
            drain.Resource.Value += amnt;
            PostResourceOffset(drain, value);
        }

        public static IEnumerable<Gizmo> GetResourceDrainGizmos(IGeneResourceDrain drain)
        {
            if (DebugSettings.ShowDevGizmos && drain.Resource != null)
            {
                Gene_Resource resource = drain.Resource;
                yield return new Command_Action
                {
                    defaultLabel = "DEV: " + resource.ResourceLabel + " -10",
                    action = delegate
                    {
                        OffsetResource(drain, -0.1f);
                    },
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: " + resource.ResourceLabel + " +10",
                    action = delegate
                    {
                        OffsetResource(drain, 0.1f);
                    },
                };
            }
        }
    }

    public class CompAbilityEffect_FuelCost : CompAbilityEffect
    {
        public new CompProperties_AbilityFuelCost Props => (CompProperties_AbilityFuelCost)props;

        private bool HasEnoughFuel
        {
            get
            {
                Gene_Fuel gene_Fuel = parent.pawn.genes?.GetFirstGeneOfType<Gene_Fuel>();
                return gene_Fuel != null && gene_Fuel.Value >= Props.FuelCost;
            }
        }

        public override bool CanCast => HasEnoughFuel && base.CanCast;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            FuelUtility.OffsetFuel(parent.pawn, 0f - Props.FuelCost);
        }

        public override bool GizmoDisabled(out string reason)
        {
            Gene_Fuel gene_Fuel = parent.pawn.genes?.GetFirstGeneOfType<Gene_Fuel>();
            if (gene_Fuel == null)
            {
                reason = "Ability Disabled: No Fuel Gene";
                return true;
            }
            if (gene_Fuel.Value < Props.FuelCost)
            {
                reason = "Ability Disabled: No Fuel";
                return true;
            }
            float num = TotalFuelCostOfQueuedAbilities();
            float num2 = Props.FuelCost + num;
            if (Props.FuelCost > float.Epsilon && num2 > gene_Fuel.Value)
            {
                reason = "Ability Disabled: No Fuel";
                return true;
            }
            reason = null;
            return false;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return HasEnoughFuel;
        }

        private float TotalFuelCostOfQueuedAbilities()
        {
            object obj = parent.pawn.jobs?.curJob?.verbToUse;
            float num = (
                (obj is Verb_CastAbility { ability: var ability })
                    ? ((ability != null) ? FuelUtility.FuelCost(ability) : 0f)
                    : 0f
            );
            float num2 = num;
            if (parent.pawn.jobs != null)
            {
                for (int i = 0; i < parent.pawn.jobs.jobQueue.Count; i++)
                {
                    if (
                        parent.pawn.jobs.jobQueue[i].job.verbToUse
                        is Verb_CastAbility verb_CastAbility2
                    )
                    {
                        float num3 = num2;
                        Ability ability2 = verb_CastAbility2.ability;
                        num2 = num3 + ((ability2 != null) ? FuelUtility.FuelCost(ability2) : 0f);
                    }
                }
            }
            return num2;
        }
    }

    public class CompProperties_AbilityFuelCost : CompProperties_AbilityEffect
    {
        public float FuelCost;

        public CompProperties_AbilityFuelCost()
        {
            compClass = typeof(CompAbilityEffect_FuelCost);
        }

        public override IEnumerable<string> ExtraStatSummary()
        {
            yield return "Fuel cost: " + Mathf.RoundToInt(FuelCost * 100f);
        }
    }

    public class Gene_Fuel : Gene_Resource, IGeneResourceDrain
    {
        public bool FuelAllowed = true;

        public Gene_Resource Resource => this;

        public Pawn Pawn => pawn;

        public bool CanOffset => !pawn.Deathresting;

        public string DisplayLabel => Label + " (Gene)";

        public float ResourceLossPerDay => def.resourceLossPerDay;

        public override float InitialResourceMax => 1f;

        public override float MinLevelForAlert => 0.15f;

        public override float MaxLevelOffset => 0.1f;

        protected override Color BarColor => new ColorInt(255, 200, 10).ToColor;

        protected override Color BarHighlightColor => new ColorInt(255, 255, 122).ToColor;

        public override void PostAdd()
        {
            base.PostAdd();
            Reset();
        }

        public override void Tick()
        {
            base.Tick();
            FuelUtility.TickResourceDrain(this);
        }

        public override void SetTargetValuePct(float val)
        {
            targetValue = Mathf.Clamp(val * Max, 0f, Max - MaxLevelOffset);
        }

        public bool ShouldConsumeFuelNow()
        {
            return Value < targetValue;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            foreach (Gizmo resourceDrainGizmo in FuelUtility.GetResourceDrainGizmos(this))
            {
                yield return resourceDrainGizmo;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref FuelAllowed, "FuelAllowed", defaultValue: true);
        }
    }

    [StaticConstructorOnStartup]
    public class GeneGizmo_ResourceFuel : GeneGizmo_Resource
    {
        private static readonly Texture2D FuelCostTex = SolidColorMaterials.NewSolidColorTexture(
            new Color(0.2f, 0.9f, 0.9f)
        );

        private const float TotalPulsateTime = 0.85f;

        private List<Pair<IGeneResourceDrain, float>> tmpDrainGenes =
            new List<Pair<IGeneResourceDrain, float>>();

        public GeneGizmo_ResourceFuel(
            Gene_Resource gene,
            List<IGeneResourceDrain> drainGenes,
            Color barColor,
            Color barhighlightColor
        )
            : base(gene, drainGenes, barColor, barhighlightColor) { }

        public override GizmoResult GizmoOnGUI(
            Vector2 topLeft,
            float maxWidth,
            GizmoRenderParms parms
        )
        {
            GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);
            float num = Mathf.Repeat(Time.time, 0.85f);
            float num2 = 1f;
            if (num < 0.1f)
            {
                num2 = num / 0.1f;
            }
            else if (num >= 0.25f)
            {
                num2 = 1f - (num - 0.25f) / 0.6f;
            }
            // https://github.com/Lennoxite/bte-mythic/blob/master/1.6/Source/BiotechExpansion_Mythic/GeneGizmo_ResourceReverence.cs#L32-L52
            if (
                MapGizmoUtility.LastMouseOverGizmo is Command_Ability command_Ability
                && gene.Max != 0f
            )
            {
                foreach (CompAbilityEffect effectComp in command_Ability.Ability.EffectComps)
                {
                    if (
                        effectComp is CompAbilityEffect_FuelCost compAbilityEffect_FuelCost
                        && compAbilityEffect_FuelCost.Props.FuelCost > float.Epsilon
                    )
                    {
                        Rect rect = barRect.ContractedBy(3f);
                        float width = rect.width;
                        float num3 = gene.Value / gene.Max;
                        rect.xMax = rect.xMin + width * num3;
                        float num4 = Mathf.Min(
                            compAbilityEffect_FuelCost.Props.FuelCost / gene.Max,
                            1f
                        );
                        rect.xMin = Mathf.Max(rect.xMin, rect.xMax - width * num4);
                        GUI.color = new Color(1f, 1f, 1f, num2 * 0.7f);
                        GenUI.DrawTextureWithMaterial(rect, FuelCostTex, null);
                        GUI.color = Color.white;
                        break;
                    }
                }
            }
            return result;
        }

        // thank you https://github.com/Lennoxite/bte-mythic/commit/34025f0a81215d80f6262990071e72e8f58025b0#diff-977e91f76f650a2585dcc67d7351955efa13df3c6bbc4265517f85487fa3f82bR54-R66
        private static bool draggingBar;
        protected override bool DraggingBar
        {
            get { return draggingBar; }
            set { draggingBar = value; }
        }

        protected override void DrawHeader(
            Rect labelRect,
            ref bool mouseOverAnyHighlightableElement
        )
        {
            if (
                (gene.pawn.IsColonistPlayerControlled || gene.pawn.IsPrisonerOfColony)
                && gene is Gene_Fuel gene_Fuel
            )
            {
                labelRect.xMax -= 24f;
                Rect rect = new Rect(labelRect.xMax, labelRect.y, 24f, 24f);
                Widgets.DefIcon(rect, GS_ThingDefOf.GS_Fuel);
                GUI.DrawTexture(
                    new Rect(rect.center.x, rect.y, rect.width / 2f, rect.height / 2f),
                    gene_Fuel.FuelAllowed ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex
                );
                if (Widgets.ButtonInvisible(rect))
                {
                    gene_Fuel.FuelAllowed = !gene_Fuel.FuelAllowed;
                    if (gene_Fuel.FuelAllowed)
                    {
                        SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    }
                    else
                    {
                        SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                    }
                }
                if (Mouse.IsOver(rect))
                {
                    Widgets.DrawHighlight(rect);
                    string onOff = (gene_Fuel.FuelAllowed ? "On" : "Off");
                    TooltipHandler.TipRegion(
                        rect,
                        () => "Pawn will consume Fuel material: " + onOff,
                        828267373
                    );
                    mouseOverAnyHighlightableElement = true;
                }
            }
            base.DrawHeader(labelRect, ref mouseOverAnyHighlightableElement);
        }

        protected override string GetTooltip()
        {
            tmpDrainGenes.Clear();
            string text =
                $"{gene.ResourceLabel.CapitalizeFirst().Colorize(ColoredText.TipSectionTitleColor)}: {gene.ValueForDisplay} / {gene.MaxForDisplay}\n";
            if (gene.pawn.IsColonistPlayerControlled || gene.pawn.IsPrisonerOfColony)
            {
                text = (
                    (!(gene.targetValue <= 0f))
                        ? (text + "Consume Fuel Below: " + gene.PostProcessValue(gene.targetValue))
                        : (text + "Never Consume Fuel".ToString())
                );
            }
            if (!drainGenes.NullOrEmpty())
            {
                float num = 0f;
                foreach (IGeneResourceDrain drainGene in drainGenes)
                {
                    if (drainGene.CanOffset)
                    {
                        tmpDrainGenes.Add(
                            new Pair<IGeneResourceDrain, float>(
                                drainGene,
                                drainGene.ResourceLossPerDay
                            )
                        );
                        num += drainGene.ResourceLossPerDay;
                    }
                }
                if (num != 0f)
                {
                    string text2 = (
                        (num < 0f) ? "RegenerationRate".Translate() : "DrainRate".Translate()
                    );
                    text =
                        text
                        + "\n\n"
                        + text2
                        + ": "
                        + "PerDay".Translate(Mathf.Abs(gene.PostProcessValue(num))).Resolve();
                    foreach (Pair<IGeneResourceDrain, float> tmpDrainGene in tmpDrainGenes)
                    {
                        text =
                            text
                            + "\n  - "
                            + tmpDrainGene.First.DisplayLabel.CapitalizeFirst()
                            + ": "
                            + "PerDay"
                                .Translate(
                                    gene.PostProcessValue(0f - tmpDrainGene.Second)
                                        .ToStringWithSign()
                                )
                                .Resolve();
                    }
                }
            }
            if (!gene.def.resourceDescription.NullOrEmpty())
            {
                text =
                    text
                    + "\n\n"
                    + gene.def.resourceDescription.Formatted(gene.pawn.Named("PAWN")).Resolve();
            }
            return text;
        }
    }

    public class HediffComp_SeverityFromFuel : HediffComp
    {
        private Gene_Fuel cachedFuelGene;

        public HediffCompProperties_SeverityFromFuel Props =>
            (HediffCompProperties_SeverityFromFuel)props;

        public override bool CompShouldRemove =>
            base.Pawn.genes?.GetFirstGeneOfType<Gene_Fuel>() == null;

        private Gene_Fuel Fuel
        {
            get
            {
                if (cachedFuelGene == null || cachedFuelGene.pawn != base.Pawn)
                {
                    cachedFuelGene = base.Pawn.genes?.GetFirstGeneOfType<Gene_Fuel>();
                }
                return cachedFuelGene;
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            Gene_Fuel fuel = Fuel;
            if (fuel == null)
            {
                return;
            }
            severityAdjustment +=
                ((fuel.Value > 0f) ? Props.severityPerHourFuel : Props.severityPerHourEmpty)
                / 2500f;
        }
    }

    public class HediffCompProperties_SeverityFromFuel : HediffCompProperties
    {
        public float severityPerHourEmpty;

        public float severityPerHourFuel;

        public HediffCompProperties_SeverityFromFuel()
        {
            compClass = typeof(HediffComp_SeverityFromFuel);
        }
    }

    public class IngestionOutcomeDoer_OffsetFuel : IngestionOutcomeDoer
    {
        public float offset;

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int amt)
        {
            FuelUtility.OffsetFuelWithModifier(pawn, offset * (float)amt);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(ThingDef parentDef)
        {
            yield return new StatDrawEntry(
                valueString: ((offset >= 0f) ? "+" : string.Empty)
                    + Mathf.RoundToInt(offset * 100f),
                category: StatCategoryDefOf.BasicsNonPawnImportant,
                label: "Fuel",
                reportText: "Amount of Fuel this restores.",
                displayPriorityWithinCategory: 1000
            );
        }
    }
}
