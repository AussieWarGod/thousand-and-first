using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Native acceptance for the ordinary fetch carry: a real posted notice, read and taken by a
	/// real settler on real turns, whose loads must actually end up in the dedicated stockpile
	/// and be credited exactly once. The reload leg is a separate profile, as it is for every
	/// other save-shaped native check; this one proves the carry and its revisit.
	/// </summary>
	internal static class KingdomBountyFetchNativeChecks
	{
		internal const string Label = "native-bounty-fetch";
		private static KingdomBountyFetchNativeFixture Fixture;
		private static int Transfers, Refusals, Finishes, Passes;
		private static int CreditedAtFinish = -1;
		private static string FinishExtra, Quarantined;
		private static int CheckedTransfers = -1, CheckedFinishes = -1;

		internal static bool Vacant
		{
			get
			{
				return Fixture == null && Transfers == 0 && Refusals == 0 && Finishes == 0
					&& Passes == 0 && CreditedAtFinish < 0 && FinishExtra == null
					&& Quarantined == null;
			}
		}

		internal static string Run(string verb, XRLGame game, Zone zone, out bool complete)
		{
			complete = false;
			if (verb == KingdomBountyFetchNativeProvider.SetupVerb)
			{
				Require(Vacant, "the bounty fetch fixture is already retained");
				Require(game != null && zone != null, "setup requires an active game and zone");
				KingdomBountyFetchNativeFixture built = new KingdomBountyFetchNativeFixture(game, zone);
				built.Build();
				Fixture = built;
				return Label + " phase=0";
			}
			Require(Fixture != null && ReferenceEquals(Fixture.Game, game)
				&& ReferenceEquals(Fixture.Zone, zone), "the retained fixture is not this game's");
			complete = true;
			return (verb == KingdomBountyFetchNativeProvider.CheckVerb) ? Carried() : Revisited();
		}

		private static string Carried()
		{
			List<string> failures = new List<string>();
			int cases = 0;
			Case(failures, ref cases, "haul-hook-absent", KingdomBounty.HaulHook == null);
			Case(failures, ref cases, "pass-resolved-and-transfers-accepted",
				Passes > 0 && Transfers >= 2 && Refusals == 0 && Quarantined == null);
			Case(failures, ref cases, "credited-sum-exact",
				CreditedAtFinish == Fixture.LoadCounts[0] + Fixture.LoadCounts[1]);
			Case(failures, ref cases, "finished-once-with-the-carried-loads",
				Finishes == 1 && FinishExtra == Expected());
			Case(failures, ref cases, "source-subtracted-to-its-sentinels", SourceExact());
			Case(failures, ref cases, "destination-holds-the-exact-loads", DestinationExact());
			Case(failures, ref cases, "fetch-mark-cleared-once", MarkCleared());
			Case(failures, ref cases, "notice-retired-once", !Standing());
			CheckedTransfers = Transfers; CheckedFinishes = Finishes;
			return Report(cases, failures);
		}

		private static string Revisited()
		{
			List<string> failures = new List<string>();
			int cases = 0;
			Require(CheckedTransfers >= 0 && CheckedFinishes >= 0, "revisit without a recorded carry");
			Case(failures, ref cases, "no-repeat-transfer-callback", Transfers == CheckedTransfers);
			Case(failures, ref cases, "no-repeat-completion", Finishes == CheckedFinishes);
			Case(failures, ref cases, "destination-unmoved", DestinationExact());
			Case(failures, ref cases, "sentinels-unmoved", SourceExact());
			return Report(cases, failures);
		}

		private static string Expected()
		{
			int moved = Fixture.LoadCounts[0] + Fixture.LoadCounts[1];
			return moved + ((moved == 1) ? " load was carried in" : " loads were carried in");
		}

		private static bool SourceExact()
		{
			GameObject pile = Fixture.Pile;
			if (!GameObject.Validate(pile) || pile.Inventory == null
				|| pile.Inventory.Objects.Count != Fixture.Sentinels.Length) return false;
			for (int i = 0; i < Fixture.Sentinels.Length; i++)
			{
				GameObject held = Fixture.Sentinels[i];
				if (!ReferenceEquals(pile.Inventory.Objects[i], held) || !GameObject.Validate(held)
					|| held.IDIfAssigned != Fixture.SentinelIds[i]
					|| held.Count != Fixture.SentinelCounts[i]
					|| held.InInventory != pile || held.CurrentCell != null) return false;
			}
			return true;
		}

		private static bool DestinationExact()
		{
			GameObject destination = Fixture.Destination;
			if (!GameObject.Validate(destination) || destination.Inventory == null
				|| destination.Inventory.Objects.Count != Fixture.Loads.Length) return false;
			for (int i = 0; i < Fixture.Loads.Length; i++)
			{
				GameObject load = Fixture.Loads[i];
				if (!GameObject.Validate(load) || load.IDIfAssigned != Fixture.LoadIds[i]
					|| load.Count != Fixture.LoadCounts[i] || load.InInventory != destination
					|| load.CurrentCell != null
					|| !destination.Inventory.Objects.Contains(load)) return false;
			}
			return true;
		}

		private static bool MarkCleared()
		{
			GameObject pile = Fixture.Pile;
			return GameObject.Validate(pile)
				&& string.IsNullOrEmpty(pile.GetStringProperty(KingdomBounty.FetchMarkProperty));
		}

		private static bool Standing()
		{
			List<GameObject> notices = KingdomBounty.Notices(Fixture.Zone);
			for (int i = 0; i < notices.Count; i++)
				if (notices[i].IDIfAssigned == Fixture.NoticeId) return true;
			return false;
		}

		private static void Case(List<string> failures, ref int cases, string name, bool passed)
		{
			cases++;
			if (!passed) failures.Add(name);
		}

		private static string Report(int cases, List<string> failures)
		{
			StringBuilder report = new StringBuilder(Label);
			report.Append(" cases=").Append(cases).Append(" passed=").Append(cases - failures.Count)
				.Append(" failed=").Append(failures.Count);
			for (int i = 0; i < failures.Count; i++) report.Append(' ').Append(failures[i]);
			return KingdomScenarioRules.Bounded(report.ToString());
		}

		internal static void ObservePass(bool prefix, KingdomSystem system, Zone zone)
		{
			if (Fixture == null || !prefix || !ReferenceEquals(zone, Fixture.Zone)) return;
			if (Passes < int.MaxValue) Passes++;
		}

		internal static void ObserveTransfer(r_KingdomNotice data, bool accepted)
		{
			if (Fixture == null || data == null) return;
			if (accepted) { if (Transfers < int.MaxValue) Transfers++; }
			else if (Refusals < int.MaxValue) Refusals++;
			if (data.LifecycleQuarantined && Quarantined == null)
				Quarantined = data.QuarantineReason ?? "unnamed quarantine";
		}

		internal static void ObserveFinish(r_KingdomNotice data, string extra)
		{
			if (Fixture == null || data == null) return;
			if (Finishes < int.MaxValue) Finishes++;
			CreditedAtFinish = data.TransferredUnits;
			FinishExtra = extra;
		}

		internal static string Fail(Exception error)
		{
			return Label + " refused; evidence retained: " + KingdomScenarioRules.Bounded(
				error.GetType().Name + ": " + error.Message);
		}

		private static void Require(bool value, string failure)
		{ KingdomBountyFetchNativeProvider.Require(value, failure); }
	}

	// Void observations only. No production argument, result, callback, or event is replaced.
	[HarmonyPatch(typeof(KingdomBounty), "ContinueTransfer")]
	internal static class KingdomBountyFetchTransferObserver
	{
		[HarmonyPostfix] internal static void Postfix(r_KingdomNotice Data, bool __result)
		{ KingdomBountyFetchNativeChecks.ObserveTransfer(Data, __result); }
	}

	[HarmonyPatch(typeof(KingdomBounty), "Finish")]
	internal static class KingdomBountyFetchFinishObserver
	{
		[HarmonyPrefix] internal static void Prefix(r_KingdomNotice Data, string Extra)
		{ KingdomBountyFetchNativeChecks.ObserveFinish(Data, Extra); }
	}
}
