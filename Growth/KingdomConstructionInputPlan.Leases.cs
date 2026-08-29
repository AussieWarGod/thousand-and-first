using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Exact identities unavailable while another parent receipt can still recover them.</summary>
	public sealed class KingdomConstructionInputLeaseSet
	{
		private readonly HashSet<string> _physical;
		private readonly HashSet<string> _objects;

		internal KingdomConstructionInputLeaseSet(HashSet<string> physical,
			HashSet<string> objects)
		{
			_physical = physical;
			_objects = objects;
		}

		public int Count { get { return _physical.Count; } }

		public bool Contains(string zoneId, string holderId, string objectId)
		{
			return !string.IsNullOrEmpty(zoneId) && !string.IsNullOrEmpty(holderId)
				&& !string.IsNullOrEmpty(objectId)
				&& _physical.Contains(Key(zoneId, holderId, objectId));
		}

		public bool ContainsObject(string objectId)
		{
			return !string.IsNullOrEmpty(objectId) && _objects.Contains(objectId);
		}

		internal static string Key(string zoneId, string holderId, string objectId)
		{
			return zoneId + "\0" + holderId + "\0" + objectId;
		}
	}

	public static partial class KingdomConstructionInputPlanRules
	{
		public const int MaxDurableLeaseSources = 8192;

		public static bool TryCollectDurableLeases(
			IList<KingdomConstructionInputReceipt> Receipts,
			out KingdomConstructionInputLeaseSet Leases,
			out KingdomConstructionInputPlanFault Fault)
		{
			Leases = null;
			if (Receipts == null) return Refuse(KingdomConstructionInputPlanFault.Null, out Fault);
			Dictionary<string, string> owners = new Dictionary<string, string>(StringComparer.Ordinal);
			HashSet<string> objects = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Receipts.Count; i++)
			{
				KingdomConstructionInputReceipt receipt = Receipts[i];
				KingdomConstructionInputFault validation;
				if (receipt == null || !KingdomConstructionInputRules.TryValidate(receipt,
					out validation))
					return Refuse(KingdomConstructionInputPlanFault.Receipt, out Fault);
				// Quarantine retains ambiguous physical custody. Only terminal outcomes that
				// proved consumption or restoration release exact source/remainder identities.
				if (receipt.TxPhase == KingdomConstructionInputTxPhase.Committed
					|| receipt.TxPhase == KingdomConstructionInputTxPhase.RolledBack
					|| receipt.TxPhase == KingdomConstructionInputTxPhase.Compensated
					|| receipt.TxPhase == KingdomConstructionInputTxPhase.Cancelled) continue;
				for (int j = 0; j < receipt.SourceCount; j++)
				{
					KingdomConstructionInputSourceLine source = receipt.SourceAt(j);
					if (!AddLease(source.SourceZoneId, source.HolderId,
						source.SourceObjectId, receipt.ReceiptId, owners, objects)
						|| (!string.IsNullOrEmpty(source.RemainderObjectId)
							&& !AddLease(source.SourceZoneId, source.HolderId,
								source.RemainderObjectId, receipt.ReceiptId, owners, objects)))
						return Refuse(KingdomConstructionInputPlanFault.Duplicate, out Fault);
					if (owners.Count > MaxDurableLeaseSources)
						return Refuse(KingdomConstructionInputPlanFault.Bounds, out Fault);
				}
			}
			Leases = new KingdomConstructionInputLeaseSet(
				new HashSet<string>(owners.Keys, StringComparer.Ordinal), objects);
			Fault = KingdomConstructionInputPlanFault.None;
			return true;
		}

		private static bool AddLease(string zoneId, string holderId, string objectId,
			string receiptId, Dictionary<string, string> owners, HashSet<string> objects)
		{
			string key = KingdomConstructionInputLeaseSet.Key(zoneId, holderId, objectId);
			string owner;
			if (owners.TryGetValue(key, out owner))
				return string.Equals(owner, receiptId, StringComparison.Ordinal);
			owners.Add(key, receiptId);
			objects.Add(objectId);
			return true;
		}
	}
}
