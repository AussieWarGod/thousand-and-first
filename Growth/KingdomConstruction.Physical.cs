using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		public static void Bind(GameObject Object, KingdomConstructionJob Job)
		{
			if (GameObject.Validate(Object) && Job != null)
			{
				Object.SetStringProperty(ReceiptProperty, Job.Id);
			}
		}

		public static bool HasReceipt(GameObject Object, KingdomConstructionJob Job)
		{
			return GameObject.Validate(Object) && Job != null
				&& Object.GetStringProperty(ReceiptProperty) == Job.Id;
		}

		public static KingdomPhysicalLookupState FindExactId(Zone Z, string Id,
			out GameObject Exact)
		{
			Exact = null;
			if (Z == null || string.IsNullOrEmpty(Id)) return KingdomPhysicalLookupState.Absent;
			IList<GameObject> loaded;
			if (!TryLoadedZoneObjects(Z, out loaded)) return KingdomPhysicalLookupState.Ambiguous;
			int count = 0;
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject item = loaded[i];
				if (item.IDIfAssigned != Id) continue;
				count++;
				if (count == 1) Exact = item;
			}
			KingdomPhysicalLookupState state = KingdomConstructionRules.PhysicalLookupState(
				count, Exact != null);
			if (state != KingdomPhysicalLookupState.Exact) Exact = null;
			return state;
		}

		public static KingdomPhysicalLookupState FindReceipt(Zone Z, KingdomConstructionJob Job,
			out GameObject Exact)
		{
			Exact = null;
			if (Z == null || Job == null) return KingdomPhysicalLookupState.Absent;
			IList<GameObject> loaded;
			if (!TryLoadedZoneObjects(Z, out loaded)) return KingdomPhysicalLookupState.Ambiguous;
			int count = 0;
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject item = loaded[i];
				if (!HasReceipt(item, Job)) continue;
				count++;
				if (count == 1) Exact = item;
			}
			KingdomPhysicalLookupState state = KingdomConstructionRules.PhysicalLookupState(
				count, Exact != null);
			if (state != KingdomPhysicalLookupState.Exact) Exact = null;
			return state;
		}

		/// <summary>Bounded live-ID proof across active and cached zones, player custody,
		/// durable object roots, and inventories. Graveyard tombstones do not count as live.</summary>
		public static KingdomPhysicalLookupState FindGlobalLiveId(string Id,
			out GameObject Exact)
		{
			KingdomPhysicalLookupState state = KingdomPlots.FindGlobalFoundingHeartId(Id,
				out Exact, out bool graveyard);
			if (state == KingdomPhysicalLookupState.Exact && graveyard)
			{
				Exact = null;
				return KingdomPhysicalLookupState.Ambiguous;
			}
			if (state != KingdomPhysicalLookupState.Exact) Exact = null;
			return state;
		}

		private static bool TryLoadedZoneObjects(Zone Z, out IList<GameObject> Loaded)
		{
			Loaded = null;
			if (Z == null) return false;
			KingdomSurvey active = KingdomSurvey.ActiveFor(Z);
			if (active != null) return active.TryLoaded(out Loaded);
			List<GameObject> pending = new List<GameObject>();
			foreach (GameObject root in Z.GetObjects())
			{
				if (!GameObject.Validate(root)) continue;
				if (root.CurrentZone != Z) return false;
				pending.Add(root);
			}
			List<GameObject> loaded = new List<GameObject>();
			HashSet<GameObject> seen = new HashSet<GameObject>();
			while (pending.Count > 0)
			{
				int last = pending.Count - 1;
				GameObject item = pending[last];
				pending.RemoveAt(last);
				if (!GameObject.Validate(item)) continue;
				if (!seen.Add(item) || loaded.Count >= MaxLoadedLookupObjects) return false;
				loaded.Add(item);
				Inventory inventory = item.Inventory;
				if (inventory == null) continue;
				for (int i = 0; i < inventory.Objects.Count; i++)
					pending.Add(inventory.Objects[i]);
			}
			Loaded = loaded;
			return true;
		}

		public static KingdomPhysicalLookupState FindSubject(Zone Z, KingdomConstructionJob Job,
			out GameObject Exact)
		{
			Exact = null;
			if (Z == null || Job == null) return KingdomPhysicalLookupState.Absent;
			if (!string.IsNullOrEmpty(Job.SubjectId))
			{
				KingdomPhysicalLookupState subject = FindExactId(Z, Job.SubjectId, out Exact);
				if (subject != KingdomPhysicalLookupState.Absent) return subject;
			}
			return FindReceipt(Z, Job, out Exact);
		}

		/// <summary>
		/// Always-running claimed-zone semantic step. Root calls this independently of settler
		/// arrivals. It resumes exact outstanding funding, retries funded projections, and advances
		/// every legacy or receipt-bearing plot from absolute world ticks.
		/// </summary>
	}
}
