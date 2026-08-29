using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSurvey
	{
		/// <summary>
		/// Water lost out of one damaged store, keeping the survey's counters correct the same way
		/// <see cref="Consume"/> does. Loss and not transfer: this water runs into the ground and
		/// is gone (Addendum 10(b)), which is why it does not go through
		/// <c>KingdomLiquids.PourOnGround</c> the way a manifest's surplus does &mdash; that
		/// surplus is water a founder can still walk up to, and this is not.
		/// </summary>
		/// <param name="Store">The damaged vessel. Must be one of <see cref="Stores"/>.</param>
		/// <param name="Drams">Amount the leak is owed.</param>
		/// <returns>Drams actually lost, measured from the vessel rather than assumed.</returns>
		public int LeakFrom(LiquidVolume Store, int Drams)
		{
			int lost;
			return TryLeakFromExact(Store, Drams, out lost) ? lost : 0;
		}

		/// <summary>Exact callback-safe leak from one dedicated pure-water vessel.</summary>
		public bool TryLeakFromExact(LiquidVolume Store, int Drams, out int Lost)
		{
			Lost = 0;
			GameObject owner = (Store == null) ? null : Store.ParentObject;
			Zone zone = GameObject.Validate(owner) ? owner.CurrentZone : null;
			Cell cell = GameObject.Validate(owner) ? owner.CurrentCell : null;
			string ownerId = GameObject.Validate(owner) ? owner.IDIfAssigned : null;
			string zoneId = (zone == null) ? null : zone.ZoneID;
			Dictionary<string, int> dictionary = (Store == null) ? null : Store.ComponentLiquids;
			Dictionary<string, int> components = (dictionary == null)
				? null : new Dictionary<string, int>(dictionary);
			LiquidVolume[] rows = Stores.ToArray();
			int occurrences = 0;
			for (int i = 0; i < rows.Length; i++) if (ReferenceEquals(rows[i], Store)) occurrences++;
			if (Store == null || Drams <= 0 || occurrences != 1 || !GameObject.Validate(owner)
				|| string.IsNullOrEmpty(ownerId)
				|| zone == null || cell == null || cell.ParentZone != zone
				|| owner.GetIntProperty("KingdomStores") != 1 || Store.ParentObject != owner
				|| !ReferenceEquals(owner.GetPart<LiquidVolume>(), Store)
				|| dictionary == null || components == null || Store.MaxVolume < 0
				|| Store.Volume < Drams || !KingdomLiquids.HasFreshWater(Store)
				|| StoredWater < Drams || StorageSpace < 0) return false;
			int before = Store.Volume;
			int max = Store.MaxVolume;
			int oldStored = StoredWater;
			int oldSpace = StorageSpace;
			string leaseFailure;
			if (!KingdomConstructionInputLeaseAuthority.TryObjectAvailableForLocalDebit(
				owner, out leaseFailure)) return false;
			try
			{
				KingdomLiquids.Drain(Store, Drams);
			}
			catch
			{
				// Exact post-state below is authoritative even when a refresh callback throws.
			}
			if (!GameObject.Validate(owner) || owner.IDIfAssigned != ownerId || owner.CurrentZone != zone
				|| zone.ZoneID != zoneId || owner.CurrentCell != cell || cell.ParentZone != zone
				|| owner.GetIntProperty("KingdomStores") != 1
				|| Store.ParentObject != owner || !ReferenceEquals(owner.GetPart<LiquidVolume>(), Store)
				|| Store.MaxVolume != max || Store.Volume != before - Drams
				|| !ReferenceEquals(Store.ComponentLiquids, dictionary)
				|| !LeakComponentsExact(Store.ComponentLiquids, components, Store.Volume == 0)
				|| Stores.Count != rows.Length || StoredWater != oldStored || StorageSpace != oldSpace)
				return false;
			for (int i = 0; i < rows.Length; i++) if (!ReferenceEquals(Stores[i], rows[i])) return false;
			int newSpace;
			try { newSpace = checked(oldSpace + Drams); }
			catch (OverflowException) { return false; }
			StoredWater = oldStored - Drams;
			StorageSpace = newSpace;
			SynchronizeReceiptObject(owner);
			Lost = Drams;
			return true;
		}

		private static bool LeakComponentsExact(Dictionary<string, int> Current,
			Dictionary<string, int> Before, bool Empty)
		{
			if (Current == null || Before == null) return false;
			if (Empty) return Current.Count == 0;
			if (Current.Count != Before.Count) return false;
			foreach (KeyValuePair<string, int> pair in Before)
			{
				int value;
				if (!Current.TryGetValue(pair.Key, out value) || value != pair.Value) return false;
			}
			return true;
		}

	}
}
