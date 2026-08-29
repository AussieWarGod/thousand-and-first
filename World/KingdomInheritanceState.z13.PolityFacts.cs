using System.Collections.Generic;

namespace ThousandAndFirst
{
	public sealed partial class KingdomInheritanceState
	{
		/// <summary>
		/// Copies bounded presentation and phenotype facts from the exact committed promoted seal.
		/// The old realm, settlement, faction, game, and actor identities never cross this boundary.
		/// </summary>
		internal bool TryPolityLegacySnapshot(out KingdomPolityLegacySnapshot Snapshot,
			out string Failure)
		{
			Snapshot = null; Failure = null;
			if (Phase != KingdomInheritancePhase.Committed)
			{
				Failure = "inheritance is not durably committed"; return false;
			}
			KingdomSealRecord legacy; KingdomSealReceipt reserved;
			KingdomSealReceipt committed;
			if (!TryGetReservation(out legacy, out reserved) ||
				!TryGetCommittedReceipt(out committed) || legacy == null || reserved == null ||
				committed == null || committed.LegacyId != legacy.LegacyId ||
				committed.LineageId != legacy.LineageId)
			{
				Failure = "committed inheritance seal evidence is torn"; return false;
			}
			// Held and faded sites still prove a living institutional successor. Abandoned
			// and ruined sites remain history/architecture only; never invent a live polity.
			if (legacy.InheritedState > (int)KingdomRules.InheritedState.Faded ||
				legacy.Population <= 0) return true;
			KingdomPolityLegacySnapshot candidate = new KingdomPolityLegacySnapshot
			{
				LegacyToken = legacy.LegacyId, LineageToken = legacy.LineageId,
				FounderName = legacy.FounderName, RealmName = legacy.RealmName,
				SettlementName = legacy.SettlementName, Vocation = legacy.Vocation,
				Style = legacy.Style, Stage = legacy.Stage, Population = legacy.Population,
				Defence = legacy.Defence, StoredWater = legacy.StoredWater,
				InheritedState = legacy.InheritedState,
				RollNames = Copy(legacy.RollNames), OriginKeys = Copy(legacy.OriginKeys),
				OriginCounts = Copy(legacy.OriginCounts), CreedKeys = Copy(legacy.CreedKeys),
				CreedCounts = Copy(legacy.CreedCounts)
			};
			if (!KingdomPolityProfileRules.ValidLegacy(candidate, out Failure)) return false;
			Snapshot = candidate; return true;
		}

		private static List<string> Copy(List<string> Source)
		{
			return Source == null ? new List<string>() : new List<string>(Source);
		}

		private static List<int> Copy(List<int> Source)
		{
			return Source == null ? new List<int>() : new List<int>(Source);
		}
	}
}
