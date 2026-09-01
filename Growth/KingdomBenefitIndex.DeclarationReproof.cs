using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		private static string ObserveDescription(IKingdomBenefitProvider Provider,
			out KingdomBenefitProviderDeclaration Declaration, out string Failure)
		{
			Declaration = null; Failure = null;
			KingdomBenefitProviderDeclaration source = null;
			KingdomBenefitProviderDeclaration normalized = null;
			string providerFault = null; string normalizationFault = null;
			bool described = false; bool valid = false; string thrown = null;
			try
			{
				described = Provider != null
					&& Provider.TryDescribeKingdomBenefits(out source, out providerFault);
				valid = KingdomBenefitProviderRules.TryNormalize(source,
					out normalized, out normalizationFault);
			}
			catch (Exception exception) { thrown = exception.GetType().Name; }
			if (thrown == null && described && valid) Declaration = normalized;
			Failure = thrown != null ? "provider threw " + thrown
				: providerFault ?? normalizationFault
					?? (described ? "provider returned a malformed declaration"
						: "provider refused its declaration");
			return (described ? "described|" : "refused|")
				+ (valid ? "normalized|" + KingdomBenefitAllocationRules.DeclarationKey(normalized)
					: "invalid|")
				+ Frame(KingdomBenefitAllocationRules.BoundDetail(providerFault))
				+ Frame(KingdomBenefitAllocationRules.BoundDetail(normalizationFault))
				+ Frame(thrown);
		}

		private static bool ReproveProviderDescriptions(List<ProviderObjectBatch> Batches,
			out string Failure)
		{
			Failure = null;
			for (int b = 0; b < Batches.Count; b++)
			{
				ProviderObjectBatch batch = Batches[b];
				if (!batch.Admitted || batch.ProviderOverflow) continue;
				for (int i = 0; i < batch.Raw.Count; i++)
				{
					RawProviderCandidate raw = batch.Raw[i];
					string current = ObserveDescription(raw.Provider, out _, out _);
					if (current != raw.CanonicalDescription)
						return Fail("admitted provider declaration changed during its snapshot",
							out Failure);
				}
			}
			return true;
		}
	}
}
