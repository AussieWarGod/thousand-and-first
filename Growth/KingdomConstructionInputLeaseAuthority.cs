using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>One fail-closed runtime door onto routed-input custody. Local economies must read
	/// this door before selecting or mutating any dedicated source.</summary>
	internal static partial class KingdomConstructionInputLeaseAuthority
	{
		internal static bool TryCapture(out KingdomConstructionInputLeaseSnapshot snapshot,
			out string failure)
		{
			snapshot = null;
			failure = null;
			try
			{
				if (The.Game == null)
				{
					failure = "The durable construction store is unavailable.";
					return false;
				}
				List<KingdomConstructionJob> jobs;
				if (!KingdomConstruction.TryRead(out jobs, out failure)) return false;
				if (jobs == null || jobs.Count > KingdomConstructionRules.MaxRows)
				{
					failure = "The durable construction lease registry exceeds its bound.";
					return false;
				}
				List<KingdomConstructionInputReceipt> receipts =
					new List<KingdomConstructionInputReceipt>();
				for (int i = 0; i < jobs.Count; i++)
				{
					KingdomConstructionJob job = jobs[i];
					if (string.IsNullOrEmpty(job.InputReceipt)) continue;
					KingdomConstructionInputReceipt receipt;
					if (!KingdomConstructionRules.TryGetInputReceipt(job, out receipt))
					{
						failure = "A durable routed-input lease cannot be decoded.";
						return false;
					}
					receipts.Add(receipt);
				}
				KingdomConstructionInputPlanFault fault;
				if (!KingdomConstructionInputLeaseRules.TryBuild(receipts,
					out snapshot, out fault))
				{
					failure = "The durable routed-input leases conflict (" + fault + ").";
					return false;
				}
				return true;
			}
			catch (Exception error)
			{
				failure = "The durable routed-input lease authority failed ("
					+ error.GetType().Name + ").";
				return false;
			}
		}

		internal static bool IsLeased(KingdomConstructionInputLeaseSnapshot snapshot,
			GameObject item)
		{
			if (snapshot == null || !GameObject.Validate(item)) return true;
			string id = item.IDIfAssigned;
			return !string.IsNullOrEmpty(id) && snapshot.ContainsObject(id);
		}

		/// <summary>Fresh fail-closed guard for another durable subsystem immediately before it
		/// freezes or mutates one exact object.</summary>
		internal static bool TryObjectAvailableForLocalDebit(GameObject item,
			out string failure)
		{
			failure = null;
			if (!ActiveLocalCustody(item))
			{
				failure = "The exact local-debit object is outside current active custody.";
				return false;
			}
			if (KingdomPurpose.HasProtectedCargoEvidence(item))
			{
				failure = "A protected purpose-cargo token cannot fund an ordinary local debit.";
				return false;
			}
			if (!KingdomOrdinaryCustody.TryProveEmpty(item, out failure)) return false;
			KingdomConstructionInputLeaseSnapshot snapshot;
			if (!TryCapture(out snapshot, out failure)) return false;
			if (IsLeased(snapshot, item))
			{
				failure = "Another durable construction receipt owns this exact object.";
				return false;
			}
			return true;
		}

		internal static bool CanUseMaterial(KingdomConstructionInputLeaseSnapshot snapshot,
			GameObject item)
		{
			return ActiveLocalCustody(item) && item.Count > 0 && !IsLeased(snapshot, item)
				&& !KingdomPurpose.HasProtectedCargoEvidence(item)
				&& KingdomOrdinaryCustody.TryProveEmpty(item, out _)
				&& item.GetIntProperty("NeverStack") == 0
				&& !item.HasStringProperty(KingdomConstruction.InputMarkerProperty)
				&& !item.HasIntProperty(KingdomConstruction.InputMarkerProperty)
				&& !item.IsImportant() && item.Equipped == null && item.IsTakeable();
		}

		/// <summary>Narrow admission for the purpose-effect callback that owns one exact
		/// persisted reservation. All ordinary callers continue through <see cref="CanUseMaterial"/>
		/// and therefore reject the same object.</summary>
		internal static bool CanUseMaterialForPurpose(
			KingdomConstructionInputLeaseSnapshot snapshot, GameObject item, string witness)
		{
			return ActiveLocalCustody(item) && item.Count > 0 && !IsLeased(snapshot, item)
				&& KingdomPurpose.ExactPurposeEffectDebitReservation(item, witness)
				&& KingdomOrdinaryCustody.TryProveEmpty(item, out _)
				&& item.GetIntProperty("NeverStack") == 0
				&& !item.HasStringProperty(KingdomConstruction.InputMarkerProperty)
				&& !item.HasIntProperty(KingdomConstruction.InputMarkerProperty)
				&& !item.IsImportant() && item.Equipped == null && item.IsTakeable();
		}

		internal static bool TryWaterAllowance(KingdomConstructionInputLeaseSnapshot snapshot,
			KingdomSurvey survey, bool preserveFloor, out int available, out string failure)
		{
			available = 0;
			failure = null;
			if (snapshot == null || survey == null || survey.Ground == null
				|| The.ZoneManager == null
				|| !ReferenceEquals(The.ZoneManager.ActiveZone, survey.Ground)
				|| KingdomSurvey.ActiveFor(survey.Ground) != survey)
			{
				failure = "Only the current active survey may authorize a local water debit.";
				return false;
			}
			KingdomSystem system = The.Game == null ? null : The.Game.GetSystem<KingdomSystem>();
			string settlementId = system?.SettlementIdForOwnedZone(survey.Ground.ZoneID);
			int floor = 0, ignoredReserved;
			if (!string.IsNullOrEmpty(settlementId))
				snapshot.TryWaterHold(settlementId, out ignoredReserved, out floor);
			int exactLeased = 0;
			for (int i = 0; i < survey.Stores.Count; i++)
			{
				LiquidVolume liquid = survey.Stores[i];
				GameObject holder = liquid == null ? null : liquid.ParentObject;
				if (!GameObject.Validate(holder) || liquid.Volume < 0 || !IsLeased(snapshot, holder))
					continue;
				if (exactLeased > int.MaxValue - liquid.Volume)
				{
					failure = "The active exact leased-water aggregate overflowed.";
					return false;
				}
				exactLeased += liquid.Volume;
			}
			int spendable = survey.StoredWater > exactLeased
				? survey.StoredWater - exactLeased : 0;
			if (!KingdomConstructionInputLeaseRules.TryAvailableWater(spendable,
				floor, preserveFloor, out available))
			{
				failure = "The settlement-wide water reserve cannot be represented exactly.";
				return false;
			}
			return true;
		}

		private static bool ActiveLocalCustody(GameObject item)
		{
			Zone active = The.ZoneManager == null ? null : The.ZoneManager.ActiveZone;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(active);
			if (!GameObject.Validate(item) || active == null || survey == null) return false;
			IList<GameObject> loaded;
			if (!survey.TryLoaded(out loaded)) return false;
			for (int i = 0; i < loaded.Count; i++)
				if (ReferenceEquals(loaded[i], item)) return true;
			return false;
		}

	}
}
