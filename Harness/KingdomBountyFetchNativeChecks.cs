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
	/// Native acceptance for the ordinary fetch carry: a real posted notice, read and taken by a real
	/// settler on real turns, whose loads must end up in the dedicated stockpile, be credited exactly
	/// once, and be paid out of the realm's own funded store. Every observation binds this fixture's
	/// exact game, realm, ground and notice data part, so no second notice or realm can stand in for
	/// the one under test. The revisit leg is SAME-PROCESS: further ordinary turns in the same
	/// session. A cold load is the separate operator route (Tools/prepare-scenario-load.py), never
	/// performed or claimed here.
	/// </summary>
	internal static class KingdomBountyFetchNativeChecks
	{
		internal const string Label = "native-bounty-fetch";
		private static KingdomBountyFetchNativeFixture Fixture;
		private static int Transfers, Refusals, Finishes, Completions, Payments, Passes;
		private static int CreditedAtFinish = -1, PaidAfterPay = -1, StoredBeforePay = -1, StoredAfterPay = -1;
		private static string FinishExtra, Quarantined, WorkerAtFinish;
		private static long TakenAtFinish = -1L, DueAtFinish = -1L, TickAtFinish = -1L;
		private static bool PaymentSettled, DoneAtPay, QuarantinedAtPay;
		private static int CheckedTransfers = -1, CheckedFinishes = -1, CheckedCompletions = -1;
		private static int CheckedPayments = -1, CheckedCredited = -1, CheckedPaid = -1;

		internal static bool Vacant
		{
			get
			{
				return Fixture == null && Transfers == 0 && Refusals == 0 && Finishes == 0
					&& Completions == 0 && Payments == 0 && Passes == 0 && CreditedAtFinish < 0
					&& PaidAfterPay < 0 && StoredBeforePay < 0 && StoredAfterPay < 0 && FinishExtra == null
					&& Quarantined == null && WorkerAtFinish == null && TakenAtFinish < 0L
					&& DueAtFinish < 0L && TickAtFinish < 0L && !PaymentSettled && !DoneAtPay
					&& !QuarantinedAtPay;
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
				Fixture = built; return Label + " phase=0";
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
				Finishes == 1 && Completions == 1 && FinishExtra == Expected());
			Case(failures, ref cases, "source-subtracted-to-its-sentinels", SourceExact());
			Case(failures, ref cases, "destination-holds-the-exact-loads", DestinationExact());
			Case(failures, ref cases, "fetch-mark-cleared-once", MarkCleared());
			Case(failures, ref cases, "notice-retired-once", !Standing());
			Case(failures, ref cases, "accepted-worker-is-on-the-roll", WorkerOnRoll());
			Case(failures, ref cases, "carried-after-its-own-due-tick", DueTimeExecution());
			Case(failures, ref cases, "paid-the-exact-price-and-completed", PaidExactly());
			Case(failures, ref cases, "stores-debited-the-exact-price", StoresDebited());
			Case(failures, ref cases, "not-quarantined-after-payment",
				Quarantined == null && !QuarantinedAtPay);
			CheckedTransfers = Transfers; CheckedFinishes = Finishes; CheckedPayments = Payments;
			CheckedCompletions = Completions; CheckedCredited = CreditedAtFinish; CheckedPaid = PaidAfterPay;
			return Report(cases, failures);
		}

		private static string Revisited()
		{
			List<string> failures = new List<string>();
			int cases = 0;
			Require(CheckedTransfers >= 0 && CheckedFinishes >= 0, "revisit without a recorded carry");
			Case(failures, ref cases, "no-repeat-transfer-callback", Transfers == CheckedTransfers);
			Case(failures, ref cases, "no-repeat-completion",
				Finishes == CheckedFinishes && Completions == CheckedCompletions);
			Case(failures, ref cases, "destination-unmoved", DestinationExact());
			Case(failures, ref cases, "sentinels-unmoved", SourceExact());
			Case(failures, ref cases, "credit-and-payment-unmoved",
				Payments == CheckedPayments && PaidAfterPay == CheckedPaid
				&& PaidAfterPay == Fixture.Price && CreditedAtFinish == CheckedCredited);
			Case(failures, ref cases, "still-retired-and-unquarantined",
				!Standing() && Quarantined == null && !QuarantinedAtPay && DoneAtPay);
			return Report(cases, failures);
		}

		private static string Expected()
		{
			int moved = Fixture.LoadCounts[0] + Fixture.LoadCounts[1];
			return moved + ((moved == 1) ? " load was carried in" : " loads were carried in");
		}

		/// <summary>The settler the shipped take accepted must be one the realm's own book names.</summary>
		private static bool WorkerOnRoll()
		{
			List<string> roll = Fixture.System?.City?.ResidentNames;
			return !string.IsNullOrEmpty(WorkerAtFinish) && roll != null && roll.Contains(WorkerAtFinish);
		}

		/// <summary>Taken after posting, due after taking, executed no earlier than the due tick.</summary>
		private static bool DueTimeExecution()
		{
			return TakenAtFinish > Fixture.PostedTick && DueAtFinish > TakenAtFinish && TickAtFinish >= DueAtFinish;
		}

		private static bool PaidExactly()
		{ return Payments == 1 && PaymentSettled && DoneAtPay && PaidAfterPay == Fixture.Price; }

		private static bool StoresDebited()
		{
			return StoredBeforePay >= Fixture.Price && StoredAfterPay >= 0 && StoredBeforePay - StoredAfterPay == Fixture.Price;
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
			return GameObject.Validate(Fixture.Pile)
				&& string.IsNullOrEmpty(Fixture.Pile.GetStringProperty(KingdomBounty.FetchMarkProperty));
		}

		private static bool Standing()
		{
			List<GameObject> notices = KingdomBounty.Notices(Fixture.Zone);
			for (int i = 0; i < notices.Count; i++) if (notices[i].IDIfAssigned == Fixture.NoticeId) return true;
			return false;
		}

		private static void Case(List<string> failures, ref int cases, string name, bool passed)
		{ cases++; if (!passed) failures.Add(name); }

		private static string Report(int cases, List<string> failures)
		{
			StringBuilder report = new StringBuilder(Label);
			report.Append(" cases=").Append(cases).Append(" passed=").Append(cases - failures.Count)
				.Append(" failed=").Append(failures.Count);
			for (int i = 0; i < failures.Count; i++) report.Append(' ').Append(failures[i]);
			return KingdomScenarioRules.Bounded(report.ToString());
		}

		/// <summary>This fixture's own live game and realm, or nothing is recorded at all.</summary>
		private static bool Bound()
		{ return Fixture != null && Fixture.System != null && ReferenceEquals(The.Game, Fixture.Game); }

		/// <summary>The same, plus the exact staked notice data part this fixture owns.</summary>
		private static bool Bound(r_KingdomNotice data)
		{ return Bound() && data != null && ReferenceEquals(data, Fixture.Data); }

		/// <summary>The funded store's live volume, or -1 when its exact vessel is not provable.</summary>
		private static int Stored()
		{
			LiquidVolume water = Fixture.StoreWater;
			return (GameObject.Validate(Fixture.Store) && water != null
				&& ReferenceEquals(water.ParentObject, Fixture.Store)) ? water.Volume : -1;
		}

		internal static void ObservePass(bool prefix, KingdomSystem system, Zone zone)
		{
			if (!Bound() || !prefix || !ReferenceEquals(zone, Fixture.Zone)
				|| !ReferenceEquals(system, Fixture.System)) return;
			if (Passes < int.MaxValue) Passes++;
		}

		internal static void ObserveTransfer(r_KingdomNotice data, bool accepted)
		{
			if (!Bound(data)) return;
			if (accepted) { if (Transfers < int.MaxValue) Transfers++; }
			else if (Refusals < int.MaxValue) Refusals++;
			RecordQuarantine(data);
		}

		internal static void ObserveFinish(r_KingdomNotice data, string extra)
		{
			if (!Bound(data)) return;
			if (Finishes < int.MaxValue) Finishes++;
			CreditedAtFinish = data.TransferredUnits;
			FinishExtra = extra;
			WorkerAtFinish = data.WorkerName;
			TakenAtFinish = data.TakenTick;
			DueAtFinish = data.DueTick;
			TickAtFinish = Fixture.Game.TimeTicks;
		}

		internal static void ObserveFinished(r_KingdomNotice data)
		{ if (Bound(data) && Finishes == 1 && Completions == 0) Completions++; }

		/// <summary>The store as it stands immediately before the shipped payout is attempted.</summary>
		internal static void ObservePayment(r_KingdomNotice data)
		{ if (Bound(data) && Payments == 0) StoredBeforePay = Stored(); }

		/// <summary>Terminal payment read at the payout's own boundary, not after the notice is retired:
		/// what was credited, what the store lost, and whether the lifecycle came out clean.</summary>
		internal static void ObservePaid(r_KingdomNotice data, bool settled)
		{
			if (!Bound(data)) return;
			if (Payments < int.MaxValue) Payments++;
			if (Payments == 1)
			{
				PaidAfterPay = data.Paid;
				PaymentSettled = settled;
				DoneAtPay = data.Done;
				StoredAfterPay = Stored();
			}
			QuarantinedAtPay = QuarantinedAtPay || data.LifecycleQuarantined;
			RecordQuarantine(data);
		}

		private static void RecordQuarantine(r_KingdomNotice data)
		{
			if (data.LifecycleQuarantined && Quarantined == null)
				Quarantined = data.QuarantineReason ?? "unnamed quarantine";
		}

		internal static string Fail(Exception error)
		{
			return Label + " refused; evidence retained: "
				+ KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
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
		[HarmonyPostfix] internal static void Postfix(r_KingdomNotice Data)
		{ KingdomBountyFetchNativeChecks.ObserveFinished(Data); }
	}

	[HarmonyPatch(typeof(KingdomBounty), "ContinuePayment")]
	internal static class KingdomBountyFetchPaymentObserver
	{
		[HarmonyPrefix] internal static void Prefix(r_KingdomNotice Data)
		{ KingdomBountyFetchNativeChecks.ObservePayment(Data); }
		[HarmonyPostfix] internal static void Postfix(r_KingdomNotice Data, bool __result)
		{ KingdomBountyFetchNativeChecks.ObservePaid(Data, __result); }
	}
}
