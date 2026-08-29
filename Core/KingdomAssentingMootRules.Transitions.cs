using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMootRules
	{
		public static bool TryPrepare(string RealmId, string SettlementId,
			string SettlementName, string ZoneId, string BuildingObjectId, string LotId,
			int BaselineHitpoints, int Generation, long Tick,
			out KingdomAssentingMootReceipt Receipt, out string Failure)
		{
			Receipt = null;
			Failure = "";
			string realm = SingleLine(RealmId, MaxIdentityChars);
			string settlement = SingleLine(SettlementId, MaxIdentityChars);
			string city = SingleLine(SettlementName, MaxNameChars);
			string zone = SingleLine(ZoneId, MaxIdentityChars);
			string building = SingleLine(BuildingObjectId, MaxIdentityChars);
			string lot = SingleLine(LotId, MaxIdentityChars);
			if (string.IsNullOrEmpty(realm) || string.IsNullOrEmpty(settlement)
				|| string.IsNullOrEmpty(city) || string.IsNullOrEmpty(zone)
				|| string.IsNullOrEmpty(building) || string.IsNullOrEmpty(lot)
				|| BaselineHitpoints <= 0 || Generation <= 0 || Tick < 0L)
				return Fail("assenting-moot preparation lacks bounded exact identity", out Failure);
			Receipt = new KingdomAssentingMootReceipt
			{
				Version = CurrentReceiptVersion,
				Phase = KingdomAssentingMootPhase.Prepared,
				Generation = Generation,
				RealmId = realm,
				SettlementId = settlement,
				SettlementName = city,
				ZoneId = zone,
				BuildingObjectId = building,
				LotId = lot,
				BaselineHitpoints = BaselineHitpoints,
				PreparedTick = Tick
			};
			Seal(Receipt);
			return Validate(Receipt, out Failure);
		}

		public static bool TryChangeMember(KingdomAssentingMootReceipt Current,
			KingdomAssentingMootRole Role, bool Add, int ResidentId, string ResidentName,
			string BodyObjectId, long Tick, out KingdomAssentingMootReceipt Next,
			out string Failure)
		{
			Next = null;
			if (!Validate(Current, out Failure)
				|| Current.Phase == KingdomAssentingMootPhase.None
				|| Current.Phase == KingdomAssentingMootPhase.Quarantined)
				return Fail("no mutable assenting-moot authority exists", out Failure);
			if (Role != KingdomAssentingMootRole.Assent
				&& Role != KingdomAssentingMootRole.Exemption)
				return Fail("unknown assenting-moot membership role", out Failure);
			if (ResidentId <= 0 || Tick < 0L || Current.Generation == int.MaxValue)
				return Fail("assenting-moot member identity or generation is exhausted", out Failure);
			KingdomAssentingMootReceipt copy = Current.Copy();
			List<int> ids = Role == KingdomAssentingMootRole.Assent
				? copy.AssentResidentIds : copy.ExemptResidentIds;
			List<string> names = Role == KingdomAssentingMootRole.Assent
				? copy.AssentResidentNames : copy.ExemptResidentNames;
			List<string> bodies = Role == KingdomAssentingMootRole.Assent
				? copy.AssentBodyObjectIds : copy.ExemptBodyObjectIds;
			int at = ids.BinarySearch(ResidentId);
			if (Add)
			{
				int cap = Role == KingdomAssentingMootRole.Assent ? MaxAssents : MaxExemptions;
				string name = SingleLine(ResidentName, MaxNameChars);
				string body = SingleLine(BodyObjectId, MaxIdentityChars);
				if (at >= 0) return Fail("resident already holds that moot role", out Failure);
				if (ids.Count >= cap) return Fail("that moot membership is full", out Failure);
				if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(body))
					return Fail("moot membership needs exact named body identity", out Failure);
				at = ~at;
				ids.Insert(at, ResidentId);
				names.Insert(at, name);
				bodies.Insert(at, body);
			}
			else
			{
				if (at < 0) return Fail("resident does not hold that moot role", out Failure);
				ids.RemoveAt(at);
				names.RemoveAt(at);
				bodies.RemoveAt(at);
			}
			ResetPrepared(copy, Current.Generation + 1, Tick);
			Seal(copy);
			if (!Validate(copy, out Failure)) return false;
			Next = copy;
			return true;
		}

		public static bool TryRebind(KingdomAssentingMootReceipt Current, string ZoneId,
			string BuildingObjectId, string LotId, int BaselineHitpoints, long Tick,
			out KingdomAssentingMootReceipt Next, out string Failure)
		{
			Next = null;
			if (!Validate(Current, out Failure)
				|| Current.Phase == KingdomAssentingMootPhase.None
				|| Current.Phase == KingdomAssentingMootPhase.Quarantined
				|| Current.Generation == int.MaxValue)
				return Fail("assenting-moot authority cannot be rebound", out Failure);
			string zone = SingleLine(ZoneId, MaxIdentityChars);
			string building = SingleLine(BuildingObjectId, MaxIdentityChars);
			string lot = SingleLine(LotId, MaxIdentityChars);
			if (string.IsNullOrEmpty(zone) || string.IsNullOrEmpty(building)
				|| string.IsNullOrEmpty(lot) || BaselineHitpoints <= 0 || Tick < 0L)
				return Fail("replacement moot lacks exact physical identity", out Failure);
			KingdomAssentingMootReceipt copy = Current.Copy();
			copy.ZoneId = zone;
			copy.BuildingObjectId = building;
			copy.LotId = lot;
			copy.BaselineHitpoints = BaselineHitpoints;
			ResetPrepared(copy, Current.Generation + 1, Tick);
			Seal(copy);
			if (!Validate(copy, out Failure)) return false;
			Next = copy;
			return true;
		}

		public static KingdomAssentingMootReceipt PrepareProjection(
			KingdomAssentingMootReceipt Current, long Tick)
		{
			if (!Mutable(Current) || Tick < 0L) return null;
			KingdomAssentingMootReceipt copy = Current.Copy();
			ResetPrepared(copy, copy.Generation, Tick);
			Seal(copy);
			string failure;
			return Validate(copy, out failure) ? copy : null;
		}

		public static KingdomAssentingMootReceipt Applied(
			KingdomAssentingMootReceipt Current, int Strength, long Tick)
		{
			if (Current == null || Current.Phase != KingdomAssentingMootPhase.Prepared
				|| Tick < Current.PreparedTick) return null;
			KingdomAssentingMootReceipt copy = Current.Copy();
			copy.Phase = KingdomAssentingMootPhase.Applied;
			copy.Strength = Strength;
			copy.AppliedTick = Tick;
			string failure;
			return Validate(copy, out failure) ? copy : null;
		}

		public static KingdomAssentingMootReceipt Suspended(
			KingdomAssentingMootReceipt Current, string Reason, long Tick)
		{
			if (!Mutable(Current) || Tick < 0L) return null;
			KingdomAssentingMootReceipt copy = Current.Copy();
			copy.Phase = KingdomAssentingMootPhase.Suspended;
			copy.Strength = 0;
			copy.SuspendedTick = Math.Max(Tick,
				Math.Max(copy.PreparedTick, copy.AppliedTick));
			copy.SuspendedReason = SingleLine(Reason, MaxReasonChars);
			if (string.IsNullOrEmpty(copy.SuspendedReason))
				copy.SuspendedReason = "ward evidence is not presently complete";
			string failure;
			return Validate(copy, out failure) ? copy : null;
		}

		private static void ResetPrepared(KingdomAssentingMootReceipt R,
			int Generation, long Tick)
		{
			R.Phase = KingdomAssentingMootPhase.Prepared;
			R.Generation = Generation;
			R.Strength = 0;
			R.PreparedTick = Math.Max(0L, Tick);
			R.AppliedTick = R.SuspendedTick = 0L;
			R.SuspendedReason = R.Fault = "";
		}

		private static bool Mutable(KingdomAssentingMootReceipt Receipt)
		{
			string failure;
			return Receipt != null && Validate(Receipt, out failure)
				&& Receipt.Phase != KingdomAssentingMootPhase.None
				&& Receipt.Phase != KingdomAssentingMootPhase.Quarantined;
		}
	}
}
