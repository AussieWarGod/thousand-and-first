using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;
using XRL;

namespace ThousandAndFirst
{
	public static partial class KingdomRealmRetirementAuthority
	{
		private static void InspectProjectionAuthorities(KingdomSystem System,
			KingdomRealmRetirementReport Report)
		{
			InspectFounderHistory(System, Report);
			InspectSuccession(Report);
			List<KingdomCityBook> books = System.OwnedCityBooks();
			for (int i = 0; i < books.Count; i++) InspectCityProjectionAuthority(
				System, books[i], Report);
			InspectHostedArcologyAuthority(System, Report);
		}

		private static void InspectSuccession(KingdomRealmRetirementReport Report)
		{
			KingdomSuccession succession = The.Game?.GetSystem<KingdomSuccession>();
			if (succession != null
				&& succession.TryDescribeRealmRemovalBlocker(out string detail))
				Report.Blockers.Add("Succession authority is not quiescent: " + detail + ".");
		}

		private static void InspectFounderHistory(KingdomSystem System,
			KingdomRealmRetirementReport Report)
		{
			KingdomFounderHistoryReceipt receipt = System?.FounderHistory;
			string failure = null;
			if (receipt == null || !KingdomFounderHistoryRules.Validate(receipt, out failure))
			{
				Report.Blockers.Add("Founder-history authority is absent or malformed: " +
					(failure ?? "absent")); return;
			}
			if (receipt.Phase != KingdomFounderHistoryPhase.None
				&& receipt.Phase != KingdomFounderHistoryPhase.Suppressed
				&& receipt.Phase != KingdomFounderHistoryPhase.Committed)
				Report.Blockers.Add("Founder-history publication is not terminal (" +
					receipt.Phase + ").");
		}

		private static void InspectCityProjectionAuthority(KingdomSystem System,
			KingdomCityBook Book, KingdomRealmRetirementReport Report)
		{
			if (Book == null)
			{
				Report.Blockers.Add("A retained city has no simulation authority book."); return;
			}
			KingdomNamedCookReceipt cook = Book.NamedCook;
			string cookFailure = null;
			if (cook == null || !KingdomNamedCookRules.Validate(cook, out cookFailure))
				Report.Blockers.Add("A named-cook receipt is absent or malformed: " +
					(cookFailure ?? "absent"));
			else if (cook.Phase == KingdomNamedCookPhase.Quarantined)
				Report.Blockers.Add("A named-cook receipt is quarantined: " + cook.Fault);
			else if (cook.Phase != KingdomNamedCookPhase.None
				&& !KingdomNamedCookRules.IsVacant(cook.Phase))
			{
				if (cook.RealmId != System.RealmId || cook.SettlementId != Book.SettlementId)
					Report.Blockers.Add("A named-cook receipt belongs to another realm or city.");
				else Report.Disclosures.Add("The exact named-cook teaching will be released " +
						"when its resident's loaded city ground is cleaned.");
			}

			KingdomAssentingMootReceipt moot = Book.AssentingMoot?.Copy();
			string mootFailure = null;
			if (moot == null || !KingdomAssentingMootRules.Validate(moot, out mootFailure))
				Report.Blockers.Add("An assenting-moot receipt is absent or malformed: " +
					(mootFailure ?? "absent"));
			else if (moot.Phase == KingdomAssentingMootPhase.Quarantined)
				Report.Blockers.Add("An assenting-moot receipt is quarantined: " + moot.Fault);
			else if (moot.Phase != KingdomAssentingMootPhase.None)
			{
				if (moot.RealmId != System.RealmId || moot.SettlementId != Book.SettlementId
					|| string.IsNullOrEmpty(System.SettlementIdForOwnedZone(moot.ZoneId)))
					Report.Blockers.Add("An assenting-moot receipt lacks exact current-realm ground.");
				else Report.Disclosures.Add("The exact assenting ward, native stabilization, " +
						"and loaded member markers will be retired on " + moot.ZoneId + ".");
			}
		}

		private static void InspectHostedArcologyAuthority(KingdomSystem System,
			KingdomRealmRetirementReport Report)
		{
			string[] keys = KingdomRemovalCoverage.HostedArcologyAuthorityStates;
			for (int i = 0; i < keys.Length; i++)
			{
				string raw = The.Game?.GetStringGameState(keys[i], "");
				if (string.IsNullOrEmpty(raw)) continue;
				if (!KingdomHostedArcologyReceiptCodec.TryDecodeAuthority(raw,
					out KingdomHostedArcologyAuthority row))
				{
					Report.Blockers.Add("Hosted-arcology authority slot " + i +
						" is malformed and was left untouched."); continue;
				}
				if (row.RealmId != System.RealmId) continue;
				if (row.Phase != KingdomHostedAuthorityPhase.Active)
				{
					Report.Blockers.Add("Hosted-arcology authority is not terminally active (" +
						row.Phase + ")."); continue;
				}
				if (string.IsNullOrEmpty(System.SettlementIdForOwnedZone(row.ZoneId)))
					Report.Blockers.Add("Hosted-arcology authority names untracked realm ground.");
				else Report.Disclosures.Add("The hosted shell on " + row.ZoneId +
						" will remain as ordinary converted contents; only its current-realm " +
						"authority slot is cleared.");
			}
		}

		private static void InspectOwnedObjectStates(KingdomRealmRetirementReport Report)
		{
			if (The.Game?.ObjectGameState == null)
			{
				Report.Blockers.Add("The global object-state registry is unavailable."); return;
			}
			foreach (KeyValuePair<string, object> row in The.Game.ObjectGameState)
			{
				if (!KingdomRemovalCoverage.IsOwnedGlobalState(row.Key) || row.Value == null
					|| row.Key == "r_TAF_Inheritance") continue;
				Report.Blockers.Add("Value-bearing TAF object authority must be recovered by " +
					"its owner before removal planning: " + row.Key + ".");
			}
		}
	}
}
