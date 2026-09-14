using System;
using HarmonyLib;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>One reversible physical fault at actual paid handover; no production verdict or job phase is replaced.</summary>
	internal static class KingdomPaidHousingFault
	{
		internal static bool Injected, Refused, Outstanding;
		private static string Refusal, Contents;
		private static string LastDetail;
		internal static bool Place(GameObject owner)
		{
			if (Injected || !KingdomPaidHousingCatalogue.Changed
				|| !ReferenceEquals(The.Game, KingdomPaidHousingNativeProvider.Owner)
				|| owner?.GetStringProperty(KingdomConstruction.ReceiptProperty) != KingdomPaidHousingNativeProvider.JobId) return false;
			var store = KingdomPaidHousingWitness.Storage;
			Require(GameObject.Validate(store) && store.CurrentCell == KingdomPaidHousingWitness.StorageCell,
				"controlled fault lacks the original anchored basket");
			Require(KingdomArchitectureStamper.TryVerifyComplete(owner, owner.CurrentZone, out string failure), failure);
			Contents = KingdomPaidHousingWitness.ContentDigest(store);
			Injected = true;
			store.CurrentCell.RemoveObject(store);
			Require(store.CurrentCell == null && store.InInventory == null, "test basket removal failed");
			return true;
		}
		internal static void Restore()
		{
			var store = KingdomPaidHousingWitness.Storage;
			if (!Injected || store == null || store.CurrentCell == KingdomPaidHousingWitness.StorageCell) return;
			KingdomPaidHousingWitness.StorageCell.AddObject(store, NoStack: true);
			Require(store.CurrentCell == KingdomPaidHousingWitness.StorageCell
				&& KingdomPaidHousingWitness.ContentDigest(store) == Contents, "test basket restoration changed custody");
		}
		internal static void Observe(bool accepted, string failure)
		{
			try
			{
				Require(!accepted && !string.IsNullOrEmpty(failure), "missing retained basket did not refuse actual handover");
				Refused = true; Refusal = failure;
			}
			finally { Restore(); }
		}
		internal static void AfterHandover()
		{
			if (!Refused || Outstanding || !ReferenceEquals(The.Game, KingdomPaidHousingNativeProvider.Owner)) return;
			Require(KingdomConstruction.TryFind(KingdomPaidHousingNativeProvider.JobId, out var job) && job != null
				&& job.Phase == KingdomConstructionPhase.Outstanding && job.Failure == Refusal
				&& KingdomQuickstartBuildClaims.CleanFirstPayment(job.Claims, 7,
					new KingdomMaterialDebitCost(KingdomPaidHousingNativeProvider.Paid.Materials)),
				"physical refusal did not preserve an Outstanding paid conversion");
			Outstanding = true;
			Require(KingdomScenarioJournal.Append("paid-housing-retry", true,
				"storage-removed=true; real-handover-refused=true; storage-restored=true; phase=Outstanding; same-payment=true") == null,
				"housing retry journal unavailable");
		}
		internal static void Trace(GameObject owner, GameObject target, string step, bool accepted, string failure)
		{
			if (!GameObject.Validate(owner) || !ReferenceEquals(The.Game, KingdomPaidHousingNativeProvider.Owner)
				|| owner?.GetStringProperty(KingdomConstruction.ReceiptProperty) != KingdomPaidHousingNativeProvider.JobId) return;
			Require(KingdomConstruction.TryFind(KingdomPaidHousingNativeProvider.JobId, out var job), "trace job absent");
			var part = owner.GetPart<XRL.World.Parts.r_KingdomImprovement>();
			string detail = "step=" + step + "; result=" + (step == "handover" ? "void" : accepted.ToString()) + "; reason=" + failure
				+ "; phase=" + job.Phase + "; physical=" + job.PhysicalPhase + "; job-failure=" + job.Failure
				+ "; handover-failure=" + part?.HandoverFailure + "; working=" + part?.Working
				+ "; effects=" + part?.HandoverEffectsDone + "; scaffold=" + part?.Scaffold?.IDIfAssigned
				+ "; owner=" + owner.IDIfAssigned + "; target=" + target?.IDIfAssigned + "; output=" + job.OutputId
				+ "; upgrade-phase=" + owner.GetIntProperty(KingdomArchitectureStamper.UpgradePhaseProperty)
				+ "; owner-fault=" + owner.GetStringProperty(KingdomArchitectureStamper.FaultProperty)
				+ "; upgrade-fault=" + owner.GetStringProperty(KingdomArchitectureStamper.UpgradeFaultProperty);
			if (detail == LastDetail) return;
			LastDetail = detail;
			Require(KingdomScenarioJournal.Append("paid-housing-detail", true, detail) == null, "housing trace unavailable");
		}
		internal static void Fault(Exception error)
		{
			Restore();
			KingdomScenarioJournal.Append("paid-housing-fault", false, error.GetType().Name + ": " + error.Message);
		}
		private static void Require(bool value, string failure) => KingdomPaidHousingNativeProvider.Require(value, failure);
	}
	[HarmonyPatch(typeof(KingdomArchitectureStamper), nameof(KingdomArchitectureStamper.TryApplyUpgrade))]
	internal static class KingdomPaidHousingApplicationPatch
	{
		[HarmonyPrefix]
		internal static void Prefix(GameObject Owner, out bool __state)
		{
			__state = false;
			try { __state = KingdomPaidHousingFault.Place(Owner); }
			catch (Exception error) { KingdomPaidHousingFault.Fault(error); }
		}
		[HarmonyPostfix]
		internal static void Postfix(GameObject Owner, GameObject Target, bool __state, bool __result, string Failure)
		{
			try
			{
				if (__state) KingdomPaidHousingFault.Observe(__result, Failure);
				KingdomPaidHousingFault.Trace(Owner, Target, "apply", __result, Failure);
			}
			catch (Exception error) { KingdomPaidHousingFault.Fault(error); }
		}
		[HarmonyFinalizer]
		internal static void Finalizer(bool __state)
		{
			if (__state) KingdomPaidHousingFault.Restore();
		}
	}
	[HarmonyPatch(typeof(KingdomUpgrade), nameof(KingdomUpgrade.HandOver))]
	internal static class KingdomPaidHousingHandoverPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(GameObject Predecessor, GameObject Successor)
		{
			try
			{
				KingdomPaidHousingFault.AfterHandover();
				KingdomPaidHousingFault.Trace(Predecessor, Successor, "handover", true, null);
			}
			catch (Exception error) { KingdomPaidHousingFault.Fault(error); }
		}
	}
	[HarmonyPatch(typeof(KingdomPlots), nameof(KingdomPlots.TryStampAuthoredGrowth))]
	internal static class KingdomPaidHousingGrowthStampPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(GameObject Predecessor, GameObject Successor, bool __result, string Failure)
		{
			try { KingdomPaidHousingFault.Trace(Predecessor, Successor, "stamp", __result, Failure); }
			catch (Exception error) { KingdomPaidHousingFault.Fault(error); }
		}
	}
}
