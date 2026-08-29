using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomBounty
	{
		private static bool ContinuePayment(KingdomSystem System, Zone Z, KingdomSurvey Survey,
			GameObject Notice, r_KingdomNotice Data, int Owed)
		{
			if ((BountyPaymentPhase)Data.PaymentPhase == BountyPaymentPhase.Credited)
			{
				if (Data.Paid >= Data.Price) return true;
				ResetPaymentReceipt(Data);
			}
			BountyPaymentPhase startingPhase = (BountyPaymentPhase)Data.PaymentPhase;
			if (startingPhase == BountyPaymentPhase.DebitIntent
				|| startingPhase == BountyPaymentPhase.Debited)
			{
				QuarantinePayment(Data, 0,
					"A payout reloaded after debit intent; physical shape cannot authorize credit or another draw.");
				return false;
			}
			if ((BountyPaymentPhase)Data.PaymentPhase == BountyPaymentPhase.None)
			{
				int request = (Survey.StoredWater < Owed) ? Survey.StoredWater : Owed;
				if (request <= 0) return false;
				string ids;
				string originals;
				string capacities;
				string allocations;
				if (!TryPaymentPlan(Survey, Z, request, out ids, out originals,
					out capacities, out allocations)) return false;
				Data.PaymentAmount = request;
				Data.PaymentPaidBefore = Data.Paid;
				Data.PaymentProved = 0;
				Data.PaymentZoneId = Z.ZoneID;
				Data.PaymentVesselIds = ids;
				Data.PaymentOriginalVolumes = originals;
				Data.PaymentMaxVolumes = capacities;
				Data.PaymentAllocations = allocations;
				Data.PaymentPhase = (int)BountyPaymentPhase.Bound;
			}
			BountyPaymentObservation observation;
			int proved;
			if (!ObserveBoundPayment(Data, Z, out observation, out proved))
			{
				QuarantinePayment(Data, 0, "The payout receipt cannot be decoded or rebound to its vessels.");
				return false;
			}
			BountyPaymentAction action = KingdomBountyRules.PaymentAction(
				(BountyPaymentPhase)Data.PaymentPhase, observation);
			if (action == BountyPaymentAction.Debit)
			{
				string ids;
				string originals;
				string capacities;
				string allocations;
				if (!TryPaymentPlan(Survey, Z, Data.PaymentAmount, out ids, out originals,
						out capacities, out allocations)
					|| ids != Data.PaymentVesselIds || originals != Data.PaymentOriginalVolumes
					|| capacities != Data.PaymentMaxVolumes || allocations != Data.PaymentAllocations)
				{
					QuarantinePayment(Data, proved,
						"The stores changed before the bound payout could begin.");
					return false;
				}
				PaymentFrame frame;
				if (!TryCaptureBoundPayment(Data, Z, Survey, Notice, out frame))
				{
					QuarantinePayment(Data, 0,
						"The exact payout vessels could not be captured before debit.");
					return false;
				}
				KingdomWaterDebit debit = Survey.ReserveExactWater(Data.PaymentAmount);
				Data.PaymentPhase = (int)BountyPaymentPhase.DebitIntent;
				bool committed = debit.Commit();
				BountyPaymentObservation after;
				int afterProved;
				if (!ObserveCapturedPayment(frame, out after, out afterProved))
				{
					QuarantinePayment(Data, 0,
						"The payout callback changed an exact notice, owner, vessel, dictionary, stores-list, cell, zone, capacity, or receipt witness.");
				}
				else if (afterProved > 0 && !ReconcilePaymentCounters(frame, committed, afterProved))
				{
					QuarantinePayment(Data, 0,
						"The payout's exact physical delta did not match its survey-counter transition.");
				}
				else if (afterProved > 0)
				{
					Data.PaymentProved = afterProved;
					Data.PaymentPhase = (int)BountyPaymentPhase.Debited;
					long paid = (long)Data.PaymentPaidBefore + afterProved;
					Data.Paid = (paid > Data.Price) ? Data.Price : (int)paid;
					if (after == BountyPaymentObservation.Debited
						&& afterProved == Data.PaymentAmount)
					{
						Data.PaymentPhase = (int)BountyPaymentPhase.Credited;
						return Data.Paid >= Data.Price;
					}
					Data.PaymentPhase = (int)BountyPaymentPhase.Quarantined;
					Quarantine(Data,
						"Only part of the exact live payout remained; that proved amount was credited and the rest was not retried.");
				}
				else QuarantinePayment(Data, 0,
					"The live payout attempt left no proved debit and was quarantined rather than retried.");
				return false;
			}
			if (action == BountyPaymentAction.Quarantine)
			{
				QuarantinePayment(Data, 0,
					"The bound payout is physically ambiguous; no further water will be drawn.");
			}
			return false;
		}

		private static bool TryPaymentPlan(KingdomSurvey Survey, Zone Z, int Amount, out string Ids,
			out string Originals, out string Capacities, out string Allocations)
		{
			Ids = Originals = Capacities = Allocations = null;
			if (Survey == null || Z == null || Amount <= 0) return false;
			int count = Survey.Stores.Count;
			int[] volumes = new int[count];
			bool[] pure = new bool[count];
			bool[] dedicated = new bool[count];
			GameObject[] owners = new GameObject[count];
			for (int i = 0; i < count; i++)
			{
				LiquidVolume vessel = Survey.Stores[i];
				bool duplicate = false;
				for (int j = 0; j < i; j++) if (ReferenceEquals(Survey.Stores[j], vessel)) duplicate = true;
				if (vessel == null || duplicate) continue;
				GameObject owner = vessel.ParentObject;
				owners[i] = owner;
				volumes[i] = vessel.Volume;
				pure[i] = KingdomLiquids.HasFreshWater(vessel);
				dedicated[i] = GameObject.Validate(owner) && owner.GetIntProperty("KingdomStores") == 1
					&& owner.CurrentZone == Z
					&& ReferenceEquals(owner.GetPart<LiquidVolume>(), vessel)
					&& vessel.ParentObject == owner;
			}
			int[] plan;
			int total;
			KingdomWaterDebitFault fault;
			if (!KingdomWaterDebitRules.TryPlan(Amount, volumes, pure, dedicated,
				out plan, out total, out fault) || total != Amount) return false;
			List<string> ids = new List<string>();
			List<int> original = new List<int>();
			List<int> capacity = new List<int>();
			List<int> allocated = new List<int>();
			for (int i = 0; i < plan.Length; i++)
			{
				if (plan[i] <= 0) continue;
				string ownerId = owners[i]?.IDIfAssigned;
				if (!GameObject.Validate(owners[i]) || string.IsNullOrEmpty(ownerId)
					|| ownerId.IndexOf('|') >= 0
					|| ownerId.Length > KingdomBountyRules.MaxObjectIdChars
					|| ids.Count >= KingdomBountyRules.MaxPaymentRows) return false;
				ids.Add(ownerId);
				original.Add(volumes[i]);
				capacity.Add(Survey.Stores[i].MaxVolume);
				allocated.Add(plan[i]);
			}
			Ids = string.Join("|", ids.ToArray());
			Originals = JoinInts(original);
			Capacities = JoinInts(capacity);
			Allocations = JoinInts(allocated);
			return ids.Count > 0 && Ids.Length <= KingdomBountyRules.MaxPaymentRowsChars
				&& Originals.Length <= KingdomBountyRules.MaxPaymentRowsChars
				&& Capacities.Length <= KingdomBountyRules.MaxPaymentRowsChars
				&& Allocations.Length <= KingdomBountyRules.MaxPaymentRowsChars;
		}

		private static bool ObserveBoundPayment(r_KingdomNotice Data, Zone Z,
			out BountyPaymentObservation Observation, out int Proved)
		{
			Observation = BountyPaymentObservation.Malformed;
			Proved = 0;
			string[] ids;
			int[] original;
			int[] capacity;
			int[] allocation;
			if (!KingdomBountyRules.TryObjectIdRows(Data.PaymentVesselIds, out ids)
				|| !TryInts(Data.PaymentOriginalVolumes, out original)
				|| !TryInts(Data.PaymentMaxVolumes, out capacity)
				|| !TryInts(Data.PaymentAllocations, out allocation)
				|| ids.Length == 0 || ids.Length != original.Length
				|| ids.Length != capacity.Length || ids.Length != allocation.Length) return false;
			int[] current = new int[ids.Length];
			bool[] same = new bool[ids.Length];
			bool[] pure = new bool[ids.Length];
			for (int i = 0; i < ids.Length; i++)
			{
				GameObject owner = GameObject.FindByID(ids[i]);
				LiquidVolume vessel = GameObject.Validate(owner) ? owner.GetPart<LiquidVolume>() : null;
				same[i] = vessel != null && vessel.ParentObject == owner
					&& Z != null && Data.PaymentZoneId == Z.ZoneID && owner.CurrentZone == Z
					&& vessel.MaxVolume == capacity[i]
					&& owner.GetIntProperty("KingdomStores") == 1;
				current[i] = (vessel == null) ? -1 : vessel.Volume;
				pure[i] = vessel != null && (vessel.Volume == 0 || vessel.IsFreshWater());
			}
			Observation = KingdomBountyRules.ObservePayment(Data.PaymentAmount,
				original, current, allocation, same, pure, out Proved);
			return Observation != BountyPaymentObservation.Malformed;
		}

	}
}
