using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private const int MaxGlobalRemovalAuthorityObjects = 65536;

		/// <summary>Finds one live improvement predecessor by ID, construction receipt, or
		/// frozen handover-source authority across every durable loaded custody root.</summary>
		public static KingdomPhysicalLookupState FindGlobalPredecessorAuthority(
			KingdomConstructionJob Job, GameObject Successor, out GameObject Exact)
		{
			Exact = null;
			if (Job == null || Job.Route != KingdomConstructionRoute.Improvement
				|| string.IsNullOrEmpty(Job.Id) || string.IsNullOrEmpty(Job.SubjectId)
				|| Job.Id.Length > KingdomConstructionRules.MaxSubjectChars
				|| Job.SubjectId.Length > KingdomConstructionRules.MaxSubjectChars
				|| !string.IsNullOrEmpty(Job.SourceId)
					&& Job.SourceId.Length > KingdomConstructionRules.MaxSubjectChars
				|| The.ZoneManager == null)
				return KingdomPhysicalLookupState.Ambiguous;
			return FindGlobalRemovalAuthority(Job, null, Successor, null, out Exact);
		}

		/// <summary>Finds one live bearer of an exact construction receipt, apart from up to
		/// two explicitly allowed endpoints. Allowed inventories are still traversed.</summary>
		public static KingdomPhysicalLookupState FindGlobalLiveReceipt(string Receipt,
			GameObject AllowedA, GameObject AllowedB, out GameObject Exact)
		{
			Exact = null;
			if (string.IsNullOrEmpty(Receipt)
				|| Receipt.Length > KingdomConstructionRules.MaxSubjectChars
				|| The.ZoneManager == null) return KingdomPhysicalLookupState.Ambiguous;
			return FindGlobalRemovalAuthority(null, Receipt, AllowedA, AllowedB, out Exact);
		}

		private static KingdomPhysicalLookupState FindGlobalRemovalAuthority(
			KingdomConstructionJob Job, string Receipt, GameObject AllowedA,
			GameObject AllowedB, out GameObject Exact)
		{
			Exact = null;
			try
			{
				List<GameObject> pending = new List<GameObject>();
				HashSet<GameObject> graveyard = new HashSet<GameObject>();
				if (!TryGlobalRemovalRoots(pending, graveyard))
					return KingdomPhysicalLookupState.Ambiguous;
				HashSet<GameObject> expanded = new HashSet<GameObject>();
				HashSet<GameObject> found = new HashSet<GameObject>();
				while (pending.Count > 0)
				{
					GameObject candidate = pending[pending.Count - 1];
					pending.RemoveAt(pending.Count - 1);
					if (candidate == null || !expanded.Add(candidate)) continue;
					if (expanded.Count > MaxGlobalRemovalAuthorityObjects)
						return KingdomPhysicalLookupState.Ambiguous;
					if (graveyard.Contains(candidate)) continue;
					bool allowed = ReferenceEquals(candidate, AllowedA)
						|| ReferenceEquals(candidate, AllowedB);
					bool authority = Job == null
						? !allowed && candidate.GetStringProperty(ReceiptProperty) == Receipt
						: CarriesPredecessorAuthority(candidate, Job, allowed);
					if (authority)
					{
						if (!GameObject.Validate(candidate))
							return KingdomPhysicalLookupState.Ambiguous;
						found.Add(candidate);
						if (found.Count > 1) return KingdomPhysicalLookupState.Ambiguous;
					}
					List<GameObject> children = candidate.GetInventoryDirectAndEquipment();
					if (children == null) continue;
					for (int i = 0; i < children.Count; i++) pending.Add(children[i]);
					if (pending.Count > MaxGlobalRemovalAuthorityObjects)
						return KingdomPhysicalLookupState.Ambiguous;
				}
				if (found.Count == 0) return KingdomPhysicalLookupState.Absent;
				foreach (GameObject item in found) Exact = item;
				return KingdomPhysicalLookupState.Exact;
			}
			catch
			{
				Exact = null;
				return KingdomPhysicalLookupState.Ambiguous;
			}
		}

		private static bool CarriesPredecessorAuthority(GameObject Candidate,
			KingdomConstructionJob Job, bool IgnoreConstructionReceipt)
		{
			r_KingdomImprovement intent = Candidate.GetPart<r_KingdomImprovement>();
			return KingdomConstructionRules.ImprovementPredecessorAuthority(
				Candidate.IDIfAssigned, IgnoreConstructionReceipt ? null
					: Candidate.GetStringProperty(ReceiptProperty),
				intent?.HandoverSourceId, intent?.HandoverConstructionReceipt,
				Job.Id, Job.SubjectId, Job.SourceId);
		}

		private static bool TryGlobalRemovalRoots(List<GameObject> Pending,
			HashSet<GameObject> Graveyard)
		{
			HashSet<Zone> zones = new HashSet<Zone>();
			if (The.ZoneManager.ActiveZone != null) zones.Add(The.ZoneManager.ActiveZone);
			if (The.ZoneManager.CachedZones != null)
				foreach (Zone zone in The.ZoneManager.CachedZones.Values)
					if (zone != null) zones.Add(zone);
			foreach (Zone zone in zones)
			{
				List<GameObject> roots = zone.GetObjects();
				if (roots == null) return false;
				for (int i = 0; i < roots.Count; i++) Pending.Add(roots[i]);
				if (Pending.Count > MaxGlobalRemovalAuthorityObjects) return false;
			}
			if (The.ZoneManager.Graveyard?.Objects != null)
				for (int i = 0; i < The.ZoneManager.Graveyard.Objects.Count; i++)
				{
					GameObject item = The.ZoneManager.Graveyard.Objects[i];
					if (item != null) { Pending.Add(item); Graveyard.Add(item); }
				}
			if (The.Player != null) Pending.Add(The.Player);
			if (The.Game?.ObjectGameState == null
				|| The.Game.ObjectGameState.Count > MaxGlobalRemovalAuthorityObjects)
				return false;
			foreach (KeyValuePair<string, object> row in The.Game.ObjectGameState)
				if (row.Value is GameObject item) Pending.Add(item);
			return Pending.Count <= MaxGlobalRemovalAuthorityObjects;
		}
	}
}
