using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Immutable, process-local view of every physical identity and water policy still
	/// owned by a durable routed-input receipt.</summary>
	public sealed class KingdomConstructionInputLeaseSnapshot
	{
		private readonly KingdomConstructionInputLeaseSet _physical;
		private readonly HashSet<string> _objects;
		private readonly HashSet<string> _holders;
		private readonly Dictionary<string, KingdomConstructionInputWaterHold> _water;

		internal KingdomConstructionInputLeaseSnapshot(
			KingdomConstructionInputLeaseSet physical, HashSet<string> objects,
			HashSet<string> holders,
			Dictionary<string, KingdomConstructionInputWaterHold> water)
		{
			_physical = physical;
			_objects = objects;
			_holders = holders;
			_water = water;
		}

		internal KingdomConstructionInputLeaseSet Physical { get { return _physical; } }

		public int Count { get { return _objects.Count; } }

		public bool Contains(string zoneId, string holderId, string objectId)
		{
			return ContainsObject(objectId) || _physical.Contains(zoneId, holderId, objectId);
		}

		public bool ContainsObject(string objectId)
		{
			return !string.IsNullOrEmpty(objectId) && _objects.Contains(objectId);
		}

		public bool ContainsHolder(string objectId)
		{
			return !string.IsNullOrEmpty(objectId) && _holders.Contains(objectId);
		}

		public bool TryWaterHold(string settlementId, out int reservedAtSource,
			out int reserveFloor)
		{
			reservedAtSource = 0;
			reserveFloor = 0;
			KingdomConstructionInputWaterHold hold;
			if (string.IsNullOrEmpty(settlementId)
				|| !_water.TryGetValue(settlementId, out hold)) return false;
			reservedAtSource = hold.ReservedAtSource;
			reserveFloor = hold.ReserveFloor;
			return true;
		}

		internal IEnumerable<string> WaterSettlements { get { return _water.Keys; } }
	}

	internal sealed class KingdomConstructionInputWaterHold
	{
		internal int ReservedAtSource;
		internal int ReserveFloor;
	}

	/// <summary>Pure fold used by the runtime authority and portable conservation tests.</summary>
	public static class KingdomConstructionInputLeaseRules
	{
		public const int MaxSharedLeaseObjects =
			KingdomConstructionInputPlanRules.MaxDurableLeaseSources * 2;

		public static bool TryBuild(IList<KingdomConstructionInputReceipt> receipts,
			out KingdomConstructionInputLeaseSnapshot snapshot,
			out KingdomConstructionInputPlanFault fault)
		{
			snapshot = null;
			KingdomConstructionInputLeaseSet physical;
			if (!KingdomConstructionInputPlanRules.TryCollectDurableLeases(receipts,
				out physical, out fault)) return false;
			Dictionary<string, string> owners =
				new Dictionary<string, string>(StringComparer.Ordinal);
			HashSet<string> objects = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> holders = new HashSet<string>(StringComparer.Ordinal);
			Dictionary<string, KingdomConstructionInputWaterHold> water =
				new Dictionary<string, KingdomConstructionInputWaterHold>(StringComparer.Ordinal);
			int identities = 0;
			for (int i = 0; i < receipts.Count; i++)
			{
				KingdomConstructionInputReceipt receipt = receipts[i];
				if (Released(receipt.TxPhase)) continue;
				for (int j = 0; j < receipt.RequiredObjectCount; j++)
					if (!Add(receipt.RequiredObjectAt(j), receipt.ReceiptId,
						owners, objects, ref identities, out fault)) return false;
				for (int j = 0; j < receipt.SourceCount; j++)
				{
					KingdomConstructionInputSourceLine source = receipt.SourceAt(j);
					if (string.IsNullOrEmpty(source.HolderId))
						return Refuse(KingdomConstructionInputPlanFault.Receipt,
							out snapshot, out fault);
					holders.Add(source.HolderId);
					if (!Add(source.SourceObjectId, receipt.ReceiptId, owners, objects,
						ref identities, out fault)
						|| !Add(source.RemainderObjectId, receipt.ReceiptId, owners, objects,
							ref identities, out fault)) return false;
					if (source.Kind != KingdomConstructionInputKind.Water) continue;
					KingdomConstructionInputWaterHold hold;
					if (!water.TryGetValue(source.SourceSettlementId, out hold))
					{
						hold = new KingdomConstructionInputWaterHold();
						water.Add(source.SourceSettlementId, hold);
					}
					if (source.ReserveFloor > hold.ReserveFloor)
						hold.ReserveFloor = source.ReserveFloor;
					if (AtSource(source.Phase))
					{
						if (hold.ReservedAtSource > int.MaxValue - source.Take)
							return Refuse(KingdomConstructionInputPlanFault.Bounds,
								out snapshot, out fault);
						hold.ReservedAtSource += source.Take;
					}
				}
				for (int j = 0; j < receipt.CargoCount; j++)
					if (!Add(receipt.CargoAt(j).ObjectId, receipt.ReceiptId,
						owners, objects, ref identities, out fault)) return false;
			}
			snapshot = new KingdomConstructionInputLeaseSnapshot(
				physical, objects, holders, water);
			fault = KingdomConstructionInputPlanFault.None;
			return true;
		}

		/// <summary>Computes the settlement-wide amount an ordinary draw may still consume.
		/// <paramref name="spendableStored"/> already excludes exact leased vessels.</summary>
		public static bool TryAvailableWater(int spendableStored, int reserveFloor,
			bool preserveFloor, out int available)
		{
			available = 0;
			if (spendableStored < 0 || reserveFloor < 0) return false;
			int floor = preserveFloor ? reserveFloor : 0;
			available = spendableStored > floor ? spendableStored - floor : 0;
			return true;
		}

		private static bool Add(string objectId, string receiptId,
			Dictionary<string, string> owners, HashSet<string> objects,
			ref int identities, out KingdomConstructionInputPlanFault fault)
		{
			fault = KingdomConstructionInputPlanFault.None;
			if (string.IsNullOrEmpty(objectId)) return true;
			string owner;
			if (owners.TryGetValue(objectId, out owner))
			{
				if (owner == receiptId) return true;
				fault = KingdomConstructionInputPlanFault.Duplicate;
				return false;
			}
			if (identities >= MaxSharedLeaseObjects)
			{
				fault = KingdomConstructionInputPlanFault.Bounds;
				return false;
			}
			owners.Add(objectId, receiptId);
			objects.Add(objectId);
			identities++;
			return true;
		}

		private static bool AtSource(KingdomConstructionInputSourcePhase phase)
		{
			return phase == KingdomConstructionInputSourcePhase.Reserved
				|| phase == KingdomConstructionInputSourcePhase.SplitIntent
				|| phase == KingdomConstructionInputSourcePhase.SplitProved
				|| phase == KingdomConstructionInputSourcePhase.TransferIntent
				|| phase == KingdomConstructionInputSourcePhase.RestoreIntent
				|| phase == KingdomConstructionInputSourcePhase.Restored
				|| phase == KingdomConstructionInputSourcePhase.Quarantined;
		}

		private static bool Released(KingdomConstructionInputTxPhase phase)
		{
			return phase == KingdomConstructionInputTxPhase.Committed
				|| phase == KingdomConstructionInputTxPhase.RolledBack
				|| phase == KingdomConstructionInputTxPhase.Compensated
				|| phase == KingdomConstructionInputTxPhase.Cancelled;
		}

		private static bool Refuse(KingdomConstructionInputPlanFault reason,
			out KingdomConstructionInputLeaseSnapshot snapshot,
			out KingdomConstructionInputPlanFault fault)
		{
			snapshot = null;
			fault = reason;
			return false;
		}
	}
}
