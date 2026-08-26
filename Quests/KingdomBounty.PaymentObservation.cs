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
		private sealed class PaymentFrame
		{
			internal r_KingdomNotice Data;
			internal GameObject Notice;
			internal Zone Zone;
			internal Cell NoticeCell;
			internal KingdomSurvey Survey;
			internal List<LiquidVolume> Stores;
			internal LiquidVolume[] StoreRows;
			internal int StoredWater;
			internal int StorageSpace;
			internal string VesselIds;
			internal string OriginalVolumes;
			internal string MaxVolumes;
			internal string AllocationsText;
			internal int Amount;
			internal int PaidBefore;
			internal GameObject[] Owners;
			internal string[] OwnerIds;
			internal Cell[] OwnerCells;
			internal LiquidVolume[] Vessels;
			internal Dictionary<string, int>[] Dictionaries;
			internal Dictionary<string, int>[] Components;
			internal int[] Originals;
			internal int[] Capacities;
			internal int[] Allocations;
		}

		private static bool TryCaptureBoundPayment(r_KingdomNotice Data, Zone Z,
			KingdomSurvey Survey, GameObject Notice, out PaymentFrame Frame)
		{
			Frame = null;
			string[] ids;
			int[] originals;
			int[] capacities;
			int[] allocations;
			Cell noticeCell = (Notice != null) ? Notice.CurrentCell : null;
			if (Survey == null || Survey.Stores == null || Z == null || Data == null
				|| Data.PaymentZoneId != Z.ZoneID
				|| !NoticeBindingExact(Notice, Data, Z, noticeCell)
				|| !KingdomBountyRules.TryObjectIdRows(Data.PaymentVesselIds, out ids)
				|| !TryInts(Data.PaymentOriginalVolumes, out originals)
				|| !TryInts(Data.PaymentMaxVolumes, out capacities)
				|| !TryInts(Data.PaymentAllocations, out allocations)
				|| ids.Length == 0 || ids.Length != originals.Length
				|| ids.Length != capacities.Length || ids.Length != allocations.Length) return false;
			Frame = new PaymentFrame
			{
				Data = Data,
				Notice = Notice,
				Zone = Z,
				NoticeCell = noticeCell,
				Survey = Survey,
				Stores = Survey.Stores,
				StoreRows = Survey.Stores.ToArray(),
				StoredWater = Survey.StoredWater,
				StorageSpace = Survey.StorageSpace,
				VesselIds = Data.PaymentVesselIds,
				OriginalVolumes = Data.PaymentOriginalVolumes,
				MaxVolumes = Data.PaymentMaxVolumes,
				AllocationsText = Data.PaymentAllocations,
				Amount = Data.PaymentAmount,
				PaidBefore = Data.PaymentPaidBefore,
				Owners = new GameObject[ids.Length],
				OwnerIds = ids,
				OwnerCells = new Cell[ids.Length],
				Vessels = new LiquidVolume[ids.Length],
				Dictionaries = new Dictionary<string, int>[ids.Length],
				Components = new Dictionary<string, int>[ids.Length],
				Originals = originals,
				Capacities = capacities,
				Allocations = allocations
			};
			for (int i = 0; i < ids.Length; i++)
			{
				GameObject owner = GameObject.FindByID(ids[i]);
				LiquidVolume vessel = GameObject.Validate(owner) ? owner.GetPart<LiquidVolume>() : null;
				if (vessel == null || owner.ID != ids[i] || owner.CurrentZone != Z
					|| owner.CurrentCell == null || owner.CurrentCell.ParentZone != Z
					|| vessel.ParentObject != owner
					|| !ReferenceEquals(owner.GetPart<LiquidVolume>(), vessel)
					|| !Survey.Stores.Contains(vessel)
					|| owner.GetIntProperty("KingdomStores") != 1
					|| vessel.ComponentLiquids == null
					|| vessel.MaxVolume != capacities[i] || vessel.Volume != originals[i]
					|| !vessel.IsFreshWater() || allocations[i] <= 0
					|| allocations[i] > originals[i]) return false;
				Frame.Owners[i] = owner;
				Frame.OwnerCells[i] = owner.CurrentCell;
				Frame.Vessels[i] = vessel;
				Frame.Dictionaries[i] = vessel.ComponentLiquids;
				Frame.Components[i] = new Dictionary<string, int>(vessel.ComponentLiquids);
			}
			return true;
		}

		private static bool ObserveCapturedPayment(PaymentFrame Frame,
			out BountyPaymentObservation Observation, out int Proved)
		{
			Observation = BountyPaymentObservation.Uncertain;
			Proved = 0;
			if (Frame == null || Frame.Data == null || Frame.Survey == null
				|| !ReferenceEquals(Frame.Survey.Stores, Frame.Stores)
				|| Frame.Stores.Count != Frame.StoreRows.Length
				|| !NoticeBindingExact(Frame.Notice, Frame.Data, Frame.Zone, Frame.NoticeCell)
				|| (BountyPaymentPhase)Frame.Data.PaymentPhase != BountyPaymentPhase.DebitIntent
				|| Frame.Data.PaymentAmount != Frame.Amount
				|| Frame.Data.PaymentPaidBefore != Frame.PaidBefore
				|| Frame.Data.PaymentVesselIds != Frame.VesselIds
				|| Frame.Data.PaymentOriginalVolumes != Frame.OriginalVolumes
				|| Frame.Data.PaymentMaxVolumes != Frame.MaxVolumes
				|| Frame.Data.PaymentAllocations != Frame.AllocationsText) return false;
			for (int i = 0; i < Frame.StoreRows.Length; i++)
				if (!ReferenceEquals(Frame.Stores[i], Frame.StoreRows[i])) return false;
			int[] current = new int[Frame.Owners.Length];
			bool[] same = new bool[Frame.Owners.Length];
			bool[] pure = new bool[Frame.Owners.Length];
			for (int i = 0; i < Frame.Owners.Length; i++)
			{
				GameObject owner = Frame.Owners[i];
				LiquidVolume vessel = Frame.Vessels[i];
				same[i] = GameObject.Validate(owner) && owner.ID == Frame.OwnerIds[i]
					&& owner.CurrentZone == Frame.Zone && owner.CurrentCell == Frame.OwnerCells[i]
					&& Frame.OwnerCells[i] != null && Frame.OwnerCells[i].ParentZone == Frame.Zone
					&& vessel != null && vessel.ParentObject == owner
					&& ReferenceEquals(owner.GetPart<LiquidVolume>(), vessel)
					&& owner.GetIntProperty("KingdomStores") == 1
					&& vessel.MaxVolume == Frame.Capacities[i]
					&& ReferenceEquals(vessel.ComponentLiquids, Frame.Dictionaries[i])
					&& ComponentsExact(vessel.ComponentLiquids, Frame.Components[i]);
				current[i] = (vessel == null) ? -1 : vessel.Volume;
				pure[i] = vessel != null && (vessel.Volume == 0 || vessel.IsFreshWater());
			}
			Observation = KingdomBountyRules.ObservePayment(Frame.Amount,
				Frame.Originals, current, Frame.Allocations, same, pure, out Proved);
			return Observation != BountyPaymentObservation.Malformed
				&& Observation != BountyPaymentObservation.Uncertain;
		}

		private static bool ComponentsExact(Dictionary<string, int> Current,
			Dictionary<string, int> Expected)
		{
			if (Current == null || Expected == null || Current.Count != Expected.Count) return false;
			foreach (KeyValuePair<string, int> pair in Expected)
			{
				int value;
				if (!Current.TryGetValue(pair.Key, out value) || value != pair.Value) return false;
			}
			return true;
		}

		private static bool ReconcilePaymentCounters(PaymentFrame Frame, bool Committed,
			int Proved)
		{
			if (Frame == null || Proved <= 0 || Proved > Frame.Amount
				|| Frame.StoredWater < Proved || Frame.StorageSpace < 0) return false;
			int expectedStored = Frame.StoredWater - Proved;
			int expectedSpace;
			try { expectedSpace = checked(Frame.StorageSpace + Proved); }
			catch (OverflowException) { return false; }
			if (Frame.Survey.StoredWater == expectedStored
				&& Frame.Survey.StorageSpace == expectedSpace) return true;
			if (Committed || Frame.Survey.StoredWater != Frame.StoredWater
				|| Frame.Survey.StorageSpace != Frame.StorageSpace) return false;
			Frame.Survey.StoredWater = expectedStored;
			Frame.Survey.StorageSpace = expectedSpace;
			return true;
		}

		private static string JoinInts(List<int> Values)
		{
			string[] rows = new string[Values.Count];
			for (int i = 0; i < Values.Count; i++) rows[i] = Values[i].ToString(
				global::System.Globalization.CultureInfo.InvariantCulture);
			return string.Join("|", rows);
		}

		private static bool TryInts(string Text, out int[] Values)
		{
			return KingdomBountyRules.TryCanonicalIntRows(Text, out Values);
		}

		private static void QuarantinePayment(r_KingdomNotice Data, int Proved, string Reason)
		{
			Data.PaymentProved = (Proved > 0) ? Proved : 0;
			long paid = (long)Data.PaymentPaidBefore + Data.PaymentProved;
			Data.Paid = (paid > Data.Price) ? Data.Price : (int)paid;
			Data.PaymentPhase = (int)BountyPaymentPhase.Quarantined;
			Quarantine(Data, Reason);
		}

	}
}
