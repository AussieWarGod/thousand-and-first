using System.Collections.Generic;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		private void TrackInspection(KingdomBenefitInspection Inspection,
			string IdentityBase, string StableAnchor)
		{
			Inspection.Detail = KingdomBenefitAllocationRules.BoundDetail(Inspection.Detail);
			AllInspections.Add(Inspection);
			InspectionOrderRows.Add(new KingdomBenefitInspectionOrderRow {
				Inspection = Inspection, IdentityBase = IdentityBase,
				StableAnchor = StableAnchor });
		}

		private bool FinalizeInspectionOrder(out string Failure)
		{
			Failure = null;
			for (int i = 0; i < InspectionOrderRows.Count; i++)
				InspectionOrderRows[i].Inspection.Detail = KingdomBenefitAllocationRules.BoundDetail(
					InspectionOrderRows[i].Inspection.Detail);
			if (!KingdomBenefitAllocationRules.TryOrderInspections(InspectionOrderRows,
				out List<KingdomBenefitInspectionOrderRow> ordered, out Failure)) return false;
			AllInspections.Clear();
			for (int i = 0; i < ordered.Count; i++) AllInspections.Add(ordered[i].Inspection);
			for (int i = 0; i < Rows.Count; i++)
				Rows[i].Providers.Sort((a, b) =>
				{
					int identity = string.CompareOrdinal(a.ProviderIdentity, b.ProviderIdentity);
					return identity != 0 ? identity
						: string.CompareOrdinal(a.ProviderKey, b.ProviderKey);
				});
			return true;
		}
	}
}
