using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;
using XRL;

namespace ThousandAndFirst
{
	public static partial class KingdomRealmRetirementAuthority
	{
		private const string LabRegistryKey = "r_TAF_LabJobRegistry_v1";

		public static bool TryInspect(KingdomSystem System,
			out KingdomRealmRetirementReport Report,
			out List<KingdomRemovalLocator> Locators, out string Failure)
		{
			Report = NewReport(); Locators = null; Failure = null;
			if (The.Game == null || System == null || !System.Founded)
				return Block(Report, "No founded current realm can be prepared.", out Failure);
			if (!KingdomIdentityFenceRuntime.TryVerify(System, out Failure))
				return Block(Report, "Identity fence: " + Failure, out Failure);
			if (!KingdomRemovalProjectionRuntime.TryInspectPlayer(
				out List<string> _, out Failure))
				return Block(Report, "Player custody: " + Failure, out Failure);
			if (!System.TryRetainedSettlementIds(true, false,
				out List<string> settlementIds, out Failure))
				return Block(Report, "Realm identity/topology: " + Failure, out Failure);
			if (System.Exiled || System.ExiledRealmArchive != null)
				Report.Blockers.Add("A second expelled realm/archive still has retained authority; return or resolve it first.");
			if (!string.IsNullOrEmpty(System.PendingSettlementId)
				|| !string.IsNullOrEmpty(System.PendingSettlementTransactionId)
				|| !string.IsNullOrEmpty(System.PendingSettlementZoneId)
				|| !string.IsNullOrEmpty(System.PendingSettlementAuthority))
				Report.Blockers.Add("A later-city founding transaction is still pending.");
			InspectTrade(System, Report);
			InspectCarry(System, Report);
			InspectCityJobs(System, Report);
			InspectConstruction(Report);
			InspectLab(Report);
			InspectPurpose(Report);
			InspectInheritance(Report);
			InspectLifecycle(System, Report);
			InspectPolityAndExperience(System, Report);
			InspectProjectionAuthorities(System, Report);
			InspectOwnedObjectStates(Report);
			if (!TryBuildLocators(System, out Locators, out Failure))
				return Block(Report, "Ground inventory: " + Failure, out Failure);
			for (int i = 0; i < Locators.Count; i++)
				Report.OutstandingGround.Add(Locators[i].ZoneId);
			Report.CanBegin = Report.Blockers.Count == 0;
			Report.Summary = Report.CanBegin
				? "The realm can enter a paused, visit-by-visit removal plan. Cancel before confirmation changes nothing."
				: "The realm cannot enter removal planning while exact authority or value is in flight.";
			return true;
		}

		private static KingdomRealmRetirementReport NewReport()
		{
			KingdomRealmRetirementReport report = new KingdomRealmRetirementReport();
			report.Disclosures.Add("No remote zone is loaded, thawed, or guessed clean.");
			report.Disclosures.Add("Completed history and ordinary item/body value are preserved.");
			report.Disclosures.Add("Legacy unknowns may remain; this flow never promises a clean uninstall.");
			return report;
		}

		private static void InspectTrade(KingdomSystem System,
			KingdomRealmRetirementReport Report)
		{
			if (System.TradeBook == null || System.TradeBook.SchemaState !=
				KingdomTradeSchemaState.Compatible)
				Report.Blockers.Add("Trade authority is absent, future-versioned, or quarantined.");
			else if (KingdomTradeRules.HasActiveAuthority(System.TradeBook))
				Report.Blockers.Add("Trade owns active charters, cargo, escrow, projections, or a transaction.");
		}

		private static void InspectCarry(KingdomSystem System,
			KingdomRealmRetirementReport Report)
		{
			if (System.Haul != null)
				Report.Blockers.Add("A legacy carry-sign haul still owns physical cargo.");
			KingdomCarryBook book = System.CarryBook;
			if (book == null || !KingdomLifecycleRules.CanOwnAuthority(book))
				Report.Blockers.Add("Carry authority is absent, malformed, or quarantined.");
			else if (book.Open != null)
				Report.Blockers.Add("Carry owns an open exact-object haul.");
		}

		private static void InspectCityJobs(KingdomSystem System,
			KingdomRealmRetirementReport Report)
		{
			KingdomJobTable jobs = null;
			KingdomCityFault fault = default(KingdomCityFault);
			if (System.Jobs == null || !System.Jobs.TryRead(out jobs, out fault))
				Report.Blockers.Add("City itinerary registry cannot be proved (" + fault + ").");
			else if (jobs.Count > 0)
				Report.Blockers.Add(jobs.Count + " city itinerary job(s) still carry people or value.");
		}

		private static void InspectConstruction(KingdomRealmRetirementReport Report)
		{
			if (!KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs,
				out string failure))
			{
				Report.Blockers.Add("Construction registry cannot be proved: " + failure); return;
			}
			int active = 0;
			for (int i = 0; i < jobs.Count; i++)
				if (jobs[i] == null || !KingdomConstructionRules.IsTerminal(jobs[i].Phase)) active++;
			if (active > 0) Report.Blockers.Add(active + " construction receipt(s) are nonterminal.");
		}

		private static void InspectLab(KingdomRealmRetirementReport Report)
		{
			string raw = The.Game.GetStringGameState(LabRegistryKey, "");
			List<KingdomLabRegistryEntry> rows = KingdomLabRules.ParseRegistry(raw,
				out bool quarantined);
			if (quarantined) Report.Blockers.Add("The lab registry is malformed or over capacity.");
			int active = 0;
			for (int i = 0; i < rows.Count; i++)
				if (rows[i].Status == KingdomLabRegistryStatus.Active
					|| rows[i].Status == KingdomLabRegistryStatus.Quarantined) active++;
			if (active > 0) Report.Blockers.Add(active + " lab application receipt(s) retain active or ambiguous value.");
		}

		private static void InspectPurpose(KingdomRealmRetirementReport Report)
		{
			string raw = The.Game.GetStringGameState(KingdomPurpose.PortfolioStateKey, "");
			if (string.IsNullOrEmpty(raw)) return;
			if (!KingdomPurposePortfolioRules.TryDecodePair(raw,
				out KingdomPurposePairReceipt pair))
				Report.Blockers.Add("Purpose-pair authority is malformed.");
			else if (pair.Phase != KingdomPurposePairPhase.Dormant)
				Report.Blockers.Add("Purpose-pair authority is not dormant (" + pair.Phase + ").");
		}

		private static void InspectInheritance(KingdomRealmRetirementReport Report)
		{
			KingdomInheritanceState state = KingdomInheritanceState.Instance;
			if (state == null) return;
			if (state.Phase != KingdomInheritancePhase.Empty)
				Report.Blockers.Add("Inheritance authority must be Empty before retirement; "
					+ state.Phase + " still retains a terminal decision or value history.");
		}

		private static void InspectPolityAndExperience(KingdomSystem System,
			KingdomRealmRetirementReport Report)
		{
			List<KingdomExperienceRetirementLeaseAllowance> allowances =
				new List<KingdomExperienceRetirementLeaseAllowance>();
			if (!KingdomPolityRemovalRuntime.TryDescribeRealmRemovalBlocker(System,
				Math.Max(0L, The.Game?.TimeTicks ?? 0L), out allowances,
				out string polityBlocker, out string polityFailure))
				Report.Blockers.Add("Polity authority cannot be inspected: " + polityFailure);
			else if (!string.IsNullOrEmpty(polityBlocker)) Report.Blockers.Add(polityBlocker);
			if (!KingdomExperienceRules.TryDescribeRealmRemovalBlocker(System?.Experience,
				System?.RealmId, allowances, out string blocker, out string failure))
				Report.Blockers.Add("Experience capacity authority cannot be inspected: " + failure);
			else if (!string.IsNullOrEmpty(blocker)) Report.Blockers.Add(blocker);
		}

		private static bool Block(KingdomRealmRetirementReport Report, string Message,
			out string Failure)
		{
			Failure = Message; Report.Blockers.Add(Message); Report.CanBegin = false;
			Report.Summary = "Realm-removal inspection failed closed."; return false;
		}
	}
}
