using System.Collections.Generic;

using XRL.World;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>One fail-closed read boundary before a civic store designation is removed.</summary>
	internal static class KingdomDesignationReleaseAuthority
	{
		internal static bool TryCanRelease(KingdomSystem system, Zone zone,
			GameObject store, out string failure)
		{
			failure = null;
			if (system == null || zone == null || !GameObject.Validate(store)
				|| store.CurrentZone != zone || store.CurrentCell == null
				|| store.CurrentCell.ParentZone != zone)
			{
				failure = "The exact dedicated store is unavailable; its designation was not changed.";
				return false;
			}

			KingdomConstructionInputLeaseSnapshot leases;
			if (!KingdomConstructionInputLeaseAuthority.TryCapture(out leases, out failure))
				return false;
			string objectId = store.IDIfAssigned;
			if (!TryProveCustodyFree(store, leases, out failure))
			{
				return false;
			}

			if (string.IsNullOrEmpty(objectId)) return true;
			bool cityCanRelease;
			KingdomCityFault cityFault;
			if (!KingdomCentralLogistics.TryCanReleaseDesignation(
				system, objectId, out cityCanRelease, out cityFault))
			{
				failure = "The delivery register cannot prove this store is free ("
					+ cityFault + ").";
				return false;
			}
			if (!cityCanRelease)
			{
				failure = "A delivery receipt still names this store. Let it finish or recover first.";
				return false;
			}
			return KingdomPurpose.TryCanReleasePurposeStore(objectId, out failure);
		}

		private static bool TryProveCustodyFree(GameObject store,
			KingdomConstructionInputLeaseSnapshot leases, out string failure)
		{
			failure = null;
			List<GameObject> custody;
			if (!KingdomOrdinaryCustody.TryCollect(store, out custody, out failure))
			{
				failure = "The store's exact custody cannot be proved: " + failure;
				return false;
			}
			for (int i = 0; i < custody.Count; i++)
			{
				GameObject item = custody[i];
				string itemId = item.IDIfAssigned;
				if ((!string.IsNullOrEmpty(itemId)
						&& (leases.ContainsObject(itemId) || leases.ContainsHolder(itemId)))
					|| KingdomPurpose.HasProtectedCargoEvidence(item))
				{
					failure = "A construction or purpose receipt still owns this store or "
						+ "something in its custody. Finish or cancel that delivery before "
						+ "releasing the designation.";
					return false;
				}
			}
			return true;
		}
	}
}
