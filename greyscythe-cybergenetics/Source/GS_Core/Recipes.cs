using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace GS_Core
{
    // GS implants are meant to sit on bionics too. Vanilla Recipe_InstallImplant rejects any part that
    // is itself an added part, so this keeps every other vanilla check and only rejects a part whose
    // ancestor was replaced. The name matches the 1.6 DLL so both versions share the XML.
    public class GS_Recipe_InstallImplant : Recipe_InstallImplant
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            HediffSet hediffSet = pawn.health.hediffSet;
            return MedicalRecipesUtility.GetFixedPartsToApplyOn(
                recipe,
                pawn,
                (BodyPartRecord record) =>
                    hediffSet.GetNotMissingParts().Contains(record)
                    && !hediffSet.AncestorHasDirectlyAddedParts(record)
                    && !hediffSet.hediffs.Any(
                        (Hediff x) =>
                            x.Part == record
                            && (x.def == recipe.addsHediff || !recipe.CompatibleWithHediff(x.def))
                    )
            );
        }
    }
}
