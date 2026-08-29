using System;
using System.Collections.Generic;

using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Fail-closed authority for ordinary food and carrier work. Physical food counts
	/// remain separate: purpose cargo and durable receipts still occupy their containers.</summary>
	internal static class KingdomOrdinaryFoodAuthority
	{
		internal const string ExpeditionReceiptProperty = "r_TAF_ExpeditionProvisionJob";
		internal const string DeliveryReceiptProperty = "KingdomDeliveryReceiptJob";
		internal const string PorterReceiptProperty = "KingdomPorterReceiptJob";

		internal static bool IsEdible(GameObject item)
		{
			return GameObject.Validate(item) && item.Count > 0
				&& (item.HasPart("Food") || item.HasPart("PreparedCookingIngredient"));
		}

		internal static bool TryCapture(out KingdomConstructionInputLeaseSnapshot leases,
			out string failure)
		{
			return KingdomConstructionInputLeaseAuthority.TryCapture(out leases, out failure);
		}

		internal static bool CanSpend(KingdomConstructionInputLeaseSnapshot leases,
			GameObject item)
		{
			return IsEdible(item) && CanMutate(leases, item);
		}

		internal static bool CanSpend(KingdomConstructionInputLeaseSnapshot leases,
			GameObject item, string ownedReceiptProperty, int ownedReceipt)
		{
			return IsEdible(item)
				&& TreeIsOrdinary(leases, item, ownedReceiptProperty, ownedReceipt);
		}

		internal static bool CanMutate(KingdomConstructionInputLeaseSnapshot leases,
			GameObject item)
		{
			return TreeIsOrdinary(leases, item, null, 0);
		}

		internal static bool TrySpendNow(GameObject item, out string failure)
		{
			return TrySpendNow(item, null, 0, out failure);
		}

		/// <summary>Fresh guard immediately before one callback. One exact receipt marker may be
		/// admitted for its owning recovery path; every other marker and every marked descendant
		/// remains unavailable.</summary>
		internal static bool TrySpendNow(GameObject item, string ownedReceiptProperty,
			int ownedReceipt, out string failure)
		{
			if (!IsEdible(item))
			{
				failure = "The proposed debit is not one exact edible stack.";
				return false;
			}
			return TryObjectNow(item, ownedReceiptProperty, ownedReceipt, out failure);
		}

		/// <summary>Purpose-effect-only admission for the exact string reservation already
		/// reproved against its frozen roster. Ordinary food paths never call this overload.</summary>
		internal static bool TrySpendPurposeNow(GameObject item, string witness,
			out string failure)
		{
			failure = null;
			if (!IsEdible(item)
				|| !KingdomPurpose.ExactPurposeEffectDebitReservation(item, witness))
			{
				failure = "The exact purpose crop reservation is unavailable.";
				return false;
			}
			if (!TryCapture(out KingdomConstructionInputLeaseSnapshot leases, out failure))
				return false;
			if (KingdomConstructionInputLeaseAuthority.IsLeased(leases, item)
				|| !KingdomOrdinaryCustody.TryProveEmpty(item, out failure))
			{
				failure = failure ?? "Another durable owner reaches the reserved purpose crop.";
				return false;
			}
			return true;
		}

		internal static bool TryObjectNow(GameObject item, out string failure)
		{
			return TryObjectNow(item, null, 0, out failure);
		}

		internal static bool TryObjectNow(GameObject item, string ownedReceiptProperty,
			int ownedReceipt, out string failure)
		{
			failure = null;
			KingdomConstructionInputLeaseSnapshot leases;
			if (!TryCapture(out leases, out failure)) return false;
			if (TreeIsOrdinary(leases, item, ownedReceiptProperty, ownedReceipt)) return true;
			failure = "Protected or receipt-bound custody cannot fund an ordinary mutation.";
			return false;
		}

		internal static bool TryCustodyAvailable(GameObject root, out string failure)
		{
			failure = null;
			KingdomConstructionInputLeaseSnapshot leases;
			if (!TryCapture(out leases, out failure)) return false;
			if (!TreeIsOrdinary(leases, root, null, 0))
			{
				failure = "Protected or receipt-bound nested custody is present.";
				return false;
			}
			return true;
		}

		internal static bool TryCustodyAvailable(GameObject root, string ownedReceiptProperty,
			int ownedReceipt, out string failure)
		{
			failure = null;
			KingdomConstructionInputLeaseSnapshot leases;
			if (!TryCapture(out leases, out failure)) return false;
			if (TreeIsOrdinary(leases, root, ownedReceiptProperty, ownedReceipt,
				allowReceiptThroughout: true)) return true;
			failure = "Protected or foreign receipt-bound nested custody is present.";
			return false;
		}

		internal static bool TryAvailable(KingdomSurvey survey, out int available,
			out string failure)
		{
			available = 0;
			failure = null;
			if (survey == null) return false;
			KingdomConstructionInputLeaseSnapshot leases;
			if (!TryCapture(out leases, out failure)) return false;
			for (int i = 0; i < survey.Larders.Count; i++)
			{
				GameObject larder = survey.Larders[i];
				if (!GameObject.Validate(larder) || larder.Inventory == null) continue;
				List<GameObject> items = new List<GameObject>(larder.Inventory.Objects);
				for (int j = 0; j < items.Count; j++)
				{
					GameObject item = items[j];
					if (!ReferenceEquals(item == null ? null : item.InInventory, larder)
						|| !CanSpend(leases, item)) continue;
					if (available > int.MaxValue - item.Count)
					{
						available = int.MaxValue;
						return true;
					}
					available += item.Count;
				}
			}
			return true;
		}

		internal static int AvailableIn(GameObject container,
			KingdomConstructionInputLeaseSnapshot leases)
		{
			if (!GameObject.Validate(container) || container.Inventory == null || leases == null)
				return 0;
			int available = 0;
			List<GameObject> items = new List<GameObject>(container.Inventory.Objects);
			for (int i = 0; i < items.Count; i++)
			{
				GameObject item = items[i];
				if (!ReferenceEquals(item == null ? null : item.InInventory, container)
					|| !CanSpend(leases, item)) continue;
				if (available > int.MaxValue - item.Count) return int.MaxValue;
				available += item.Count;
			}
			return available;
		}

		internal static int EffectiveCapacity(int physical, int available, int capacity)
		{
			int room = capacity > physical ? capacity - physical : 0;
			return available > int.MaxValue - room ? int.MaxValue : available + room;
		}

		private static bool TreeIsOrdinary(KingdomConstructionInputLeaseSnapshot leases,
			GameObject root, string ownedReceiptProperty, int ownedReceipt)
		{
			return TreeIsOrdinary(leases, root, ownedReceiptProperty, ownedReceipt,
				allowReceiptThroughout: false);
		}

		private static bool TreeIsOrdinary(KingdomConstructionInputLeaseSnapshot leases,
			GameObject root, string ownedReceiptProperty, int ownedReceipt,
			bool allowReceiptThroughout)
		{
			if (leases == null || !GameObject.Validate(root)) return false;
			List<GameObject> graph;
			string failure;
			if (!KingdomOrdinaryCustody.TryCollect(root, out graph, out failure)) return false;
			for (int i = 0; i < graph.Count; i++)
			{
				GameObject item = graph[i];
				if (KingdomPurpose.HasProtectedCargoEvidence(item)
					|| KingdomConstructionInputLeaseAuthority.IsLeased(leases, item)
					|| !ReceiptEvidenceAllowed(item, (i == 0 || allowReceiptThroughout)
						? ownedReceiptProperty : null, ownedReceipt)) return false;
			}
			return true;
		}

		private static bool ReceiptEvidenceAllowed(GameObject item, string allowed, int value)
		{
			return ReceiptFieldAllowed(item, ExpeditionReceiptProperty, allowed, value)
				&& ReceiptFieldAllowed(item, DeliveryReceiptProperty, allowed, value)
				&& ReceiptFieldAllowed(item, PorterReceiptProperty, allowed, value);
		}

		private static bool ReceiptFieldAllowed(GameObject item, string field, string allowed,
			int value)
		{
			bool hasInt = item.HasIntProperty(field);
			bool hasString = item.HasStringProperty(field);
			if (!hasInt && !hasString) return true;
			return field == allowed && value > 0 && hasInt && !hasString
				&& item.GetIntProperty(field) == value;
		}
	}
}
