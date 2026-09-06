using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Exact absence across the supplied owned city carriers and realm binding registry.
	/// Reads raw storage without normalization; never repairs missing evidence into permission.</summary>
	internal static class KingdomResidentCarrierAbsenceRules
	{
		internal static bool ProvesAbsent(KingdomCityBook intended, int residentId,
			KingdomBindingRegistry registry, IReadOnlyList<KingdomCityBook> exactOwnedBooks,
			int expectedBookCount)
		{
			if (intended == null || residentId <= 0 || registry == null || exactOwnedBooks == null
				|| expectedBookCount < 1 || expectedBookCount > KingdomIdentityRules.MaxSettlements
				|| exactOwnedBooks.Count != expectedBookCount
				|| !registry.TryReadExact(out KingdomBindingTable bindings, out KingdomCityFault _)
				|| !bindings.TryAudit(out KingdomCityFault _)
				|| bindings.TryGet(residentId, KingdomBindingKind.Resident, out KingdomBinding _)) return false;
			HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
			bool foundIntended = false;
			for (int i = 0; i < exactOwnedBooks.Count; i++)
			{
				KingdomCityBook city = exactOwnedBooks[i];
				if (city == null || !KingdomIdentityRules.IsSettlementId(city.SettlementId)
					|| !identities.Add(city.SettlementId)) return false;
				for (int j = 0; j < i; j++)
					if (ReferenceEquals(city, exactOwnedBooks[j])) return false;
				if (!city.TryReadExact(out KingdomCityState state, out KingdomCityFault _)
					|| state.TryResidentIndex(residentId, out int _)) return false;
				if (ReferenceEquals(city, intended)) foundIntended = true;
			}
			return foundIntended;
		}
	}
}
