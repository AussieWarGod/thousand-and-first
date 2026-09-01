using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		internal static bool TryBuild(Zone Z, KingdomSurvey Survey,
			KingdomDesignationIndex Designations, out KingdomBenefitIndex Index,
			out string Failure)
		{
			Index = null; Failure = null;
			if (Z == null || Survey == null || Survey.Ground != Z || Designations == null
				|| !Survey.TryLoaded(out var loaded))
				return Fail("physical benefit scan needs one complete exact-zone survey", out Failure);
			KingdomBenefitIndex result = new KingdomBenefitIndex();
			result.Initialize(Designations, Z);
			List<string> sourceFaults = new List<string>(Designations.SourceFaults);
			sourceFaults.Sort(System.StringComparer.Ordinal);
			for (int f = 0; f < sourceFaults.Count; f++)
			{
				KingdomBenefitInspection inspection = new KingdomBenefitInspection {
					ProviderKey = "taf:designation-source", Fault = KingdomBenefitFault.SourceFault,
					Detail = sourceFaults[f] };
				result.TrackInspection(inspection, "<designation-source>#fault",
					"designation-source|" + sourceFaults[f]);
			}
			if (!TryCollectProviders(loaded, result,
				out List<ProviderCandidate> candidates, out Failure)) return false;
			candidates.Sort((a, b) =>
			{
				int stable = string.CompareOrdinal(a.StableKey, b.StableKey);
				return stable != 0 ? stable : string.CompareOrdinal(a.IdentityBase, b.IdentityBase);
			});
			for (int i = 0; i < candidates.Count; i++)
				result.Evaluate(candidates[i], Z, Survey, Designations);
			if (!result.ReproveCollectedProviderSnapshot(Z, Survey, Designations,
				candidates, out Failure)) return false;
			if (!result.AllocatePending(out Failure)) return false;
			result.AddStructuralTags(Z);
			result.AddStructuralDefence(Z);
			result.FinalizeRows();
			if (!result.FinalizeInspectionOrder(out Failure)) return false;
			Index = result; return true;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
