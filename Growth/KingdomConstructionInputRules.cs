using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Engine-free routed-construction parent contract.</summary>
	public static partial class KingdomConstructionInputRules
	{
		/// <summary>Schema 3 adds attended semantic-transit custody. Schema-2 receipts remain
		/// readable and are adopted only from their exact bound body in their active source.</summary>
		public const int Schema = 3;
		public const int LegacySchema = 1;
		public const int WaterReserveDays = 3;
		public const int MaxSourceLines = 64;
		public const int MaxCargoLines = 64;
		public const int MaxChildren = 16;
		public const int MaxRequiredObjects = 8;
		public const int MaxCargoPerChild = 12;
		public const int MaxIdentityChars = 128;
		public const int MaxBlueprintChars = 160;
		public const int MaxClaimChars = 512;
		public const int MaxCoordinate = 255;
		public const int MaxPayloadBytes = 131072;
		public const int MaxEncodedChars = 180000;
		public const string WaterClassification = "water-1000";
		public const string WaterCargoBlueprint = "EmptyWaterskin";
		public const int WaterCargoCapacity = 64;

		public static bool TryWaterReserveFloor(int DailyUpkeep, out int Floor)
		{
			Floor = 0;
			if (DailyUpkeep < 0) return false;
			long value = (long)DailyUpkeep * WaterReserveDays;
			if (value > int.MaxValue) return false;
			Floor = (int)value;
			return true;
		}

		public static bool TryCreate(string ReceiptId, string ConstructionJobId,
			string OwnerKey, long OwnerEpoch, string TargetZoneId, int TargetX, int TargetY,
			string ConstructionIntentDigest, string RequiredObjectId, int WaterRequested,
			string MaterialRequestedClaim, int DailyWaterUpkeep,
			int MaterialReservePolicyVersion, int PriorWaterSpent, int PriorWaterLost,
			string PriorMaterialSpentClaim, string PriorMaterialLostClaim,
			IList<KingdomConstructionInputSourceLine> Sources,
			IList<KingdomConstructionInputCargoLine> Cargo,
			IList<KingdomConstructionInputChild> Children,
			out KingdomConstructionInputReceipt Receipt,
			out KingdomConstructionInputFault Fault)
		{
			return TryCreateWithRequiredObjects(ReceiptId, ConstructionJobId, OwnerKey, OwnerEpoch,
				TargetZoneId, TargetX, TargetY, ConstructionIntentDigest,
				string.IsNullOrEmpty(RequiredObjectId) ? new string[0]
					: new[] { RequiredObjectId }, WaterRequested, MaterialRequestedClaim,
				DailyWaterUpkeep, MaterialReservePolicyVersion, PriorWaterSpent,
				PriorWaterLost, PriorMaterialSpentClaim, PriorMaterialLostClaim,
				Sources, Cargo, Children, out Receipt, out Fault);
		}

		public static bool TryCreateWithRequiredObjects(string ReceiptId,
			string ConstructionJobId,
			string OwnerKey, long OwnerEpoch, string TargetZoneId, int TargetX, int TargetY,
			string ConstructionIntentDigest, IList<string> RequiredObjectIds,
			int WaterRequested, string MaterialRequestedClaim, int DailyWaterUpkeep,
			int MaterialReservePolicyVersion, int PriorWaterSpent, int PriorWaterLost,
			string PriorMaterialSpentClaim, string PriorMaterialLostClaim,
			IList<KingdomConstructionInputSourceLine> Sources,
			IList<KingdomConstructionInputCargoLine> Cargo,
			IList<KingdomConstructionInputChild> Children,
			out KingdomConstructionInputReceipt Receipt,
			out KingdomConstructionInputFault Fault)
		{
			Receipt = null;
			string[] required;
			if (!TryRequiredObjectIds(RequiredObjectIds, out required))
				return Refuse(KingdomConstructionInputFault.Identity, out Fault);
			int waterFloor;
			if (!TryWaterReserveFloor(DailyWaterUpkeep, out waterFloor))
				return Refuse(KingdomConstructionInputFault.Amount, out Fault);
			if (Sources == null || Cargo == null || Children == null)
				return Refuse(KingdomConstructionInputFault.Null, out Fault);
			if (Sources.Count < 1 || Sources.Count > MaxSourceLines
				|| Cargo.Count < 1 || Cargo.Count > MaxCargoLines
				|| Children.Count < 1 || Children.Count > MaxChildren)
				return Refuse(KingdomConstructionInputFault.Bounds, out Fault);
			KingdomConstructionInputSourceLine[] sources = new KingdomConstructionInputSourceLine[Sources.Count];
			KingdomConstructionInputCargoLine[] cargo = new KingdomConstructionInputCargoLine[Cargo.Count];
			KingdomConstructionInputChild[] children = new KingdomConstructionInputChild[Children.Count];
			for (int i = 0; i < sources.Length; i++) sources[i] = Sources[i];
			for (int i = 0; i < cargo.Length; i++) cargo[i] = Cargo[i];
			for (int i = 0; i < children.Length; i++) children[i] = Children[i];

			KingdomConstructionInputReceipt provisional = new KingdomConstructionInputReceipt(
				Schema, ReceiptId, ConstructionJobId, OwnerKey, OwnerEpoch, TargetZoneId,
				TargetX, TargetY, ConstructionIntentDigest, required, WaterRequested,
				MaterialRequestedClaim, waterFloor, MaterialReservePolicyVersion,
				PriorWaterSpent, PriorWaterLost, PriorMaterialSpentClaim,
				PriorMaterialLostClaim, KingdomConstructionInputTxPhase.ReservationPrepared,
				0, null, -1L, 0L, sources, cargo, children);
			string digest;
			if (!TryPlanDigest(provisional, out digest))
				return Refuse(KingdomConstructionInputFault.Digest, out Fault);
			Receipt = new KingdomConstructionInputReceipt(Schema, ReceiptId,
				ConstructionJobId, OwnerKey, OwnerEpoch, TargetZoneId, TargetX, TargetY,
				ConstructionIntentDigest, required, WaterRequested,
				MaterialRequestedClaim, waterFloor,
				MaterialReservePolicyVersion, PriorWaterSpent, PriorWaterLost,
				PriorMaterialSpentClaim, PriorMaterialLostClaim,
				KingdomConstructionInputTxPhase.ReservationPrepared, 0, digest, -1L, 0L,
				sources, cargo, children);
			if (!TryValidate(Receipt, out Fault))
			{
				Receipt = null;
				return false;
			}
			return true;
		}

		internal static bool TryRequiredObjectIds(IList<string> Values,
			out string[] Required)
		{
			Required = null;
			if (Values == null || Values.Count > MaxRequiredObjects) return false;
			string[] copy = new string[Values.Count];
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < copy.Length; i++)
			{
				string value = Values[i];
				if (!ValidText(value, MaxIdentityChars, false) || !seen.Add(value)) return false;
				copy[i] = value;
			}
			Required = copy;
			return true;
		}

		internal static bool Refuse(KingdomConstructionInputFault Value,
			out KingdomConstructionInputFault Fault)
		{
			Fault = Value;
			return false;
		}

		internal static bool ValidText(string Value, int Maximum, bool Optional)
		{
			if (Value == null || Value.Length == 0) return Optional;
			if (Value.Length > Maximum) return false;
			for (int i = 0; i < Value.Length; i++)
				if (Value[i] == '\0' || Value[i] == '\r' || Value[i] == '\n') return false;
			return true;
		}

		internal static bool Defined(KingdomConstructionInputKind value)
		{
			return value >= KingdomConstructionInputKind.Water
				&& value <= KingdomConstructionInputKind.Exotic;
		}

		internal static bool Defined(KingdomConstructionInputTopology value)
		{
			return value >= KingdomConstructionInputTopology.Invalid
				&& value <= KingdomConstructionInputTopology.Returned;
		}

		internal static bool Defined(KingdomConstructionInputTxPhase value)
		{
			return value >= KingdomConstructionInputTxPhase.ReservationPrepared
				&& value <= KingdomConstructionInputTxPhase.Cancelled;
		}

		internal static bool Defined(KingdomConstructionInputSourcePhase value)
		{
			return value >= KingdomConstructionInputSourcePhase.Reserved
				&& value <= KingdomConstructionInputSourcePhase.Quarantined;
		}

		internal static bool Defined(KingdomConstructionInputCargoPhase value)
		{
			return value >= KingdomConstructionInputCargoPhase.Planned
				&& value <= KingdomConstructionInputCargoPhase.Quarantined;
		}
	}
}
