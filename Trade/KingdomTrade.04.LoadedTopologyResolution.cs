using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		private static LoadedObjectResolution ResolveLoadedObject(string Id, Zone ExpectedZone,
			out GameObject Object, out LoadedTopologyWitness Topology)
		{
			Object = null;
			Topology = CaptureLoadedTopology();
			if (!KingdomTradeRules.ValidId(Id) || ExpectedZone == null || Topology == null)
				return LoadedObjectResolution.Incomplete;
			bool zoneLoaded = false;
			LoadedObjectWitness exact;
			for (int i = 0; i < Topology.Zones.Count; i++)
				if (ReferenceEquals(Topology.Zones[i].Zone, ExpectedZone)) zoneLoaded = true;
			if (!zoneLoaded) return LoadedObjectResolution.Incomplete;
			KingdomTradeExactLookup result = KingdomTradeRules.ResolveExactUnique(
				Topology.Objects, Id, row => row.Object.ID, out exact);
			if (result == KingdomTradeExactLookup.Incomplete) return LoadedObjectResolution.Incomplete;
			if (result == KingdomTradeExactLookup.Missing) return LoadedObjectResolution.Missing;
			if (result == KingdomTradeExactLookup.Ambiguous) return LoadedObjectResolution.Ambiguous;
			if (!ReferenceEquals(exact.Zone, ExpectedZone)) return LoadedObjectResolution.Ambiguous;
			Object = exact.Object;
			return LoadedObjectResolution.ExactUnique;
		}

		private static bool ExactLoadedTopologyWithDelta(LoadedTopologyWitness Expected,
			GameObject Added, GameObject Removed, GameObject ChangedInventoryOwner,
			bool RootDelta)
		{
			LoadedTopologyWitness current = CaptureLoadedTopology();
			if (!ExactLoadedInfrastructure(Expected, current)) return false;
			List<GameObject> addedTree = Added == null ? new List<GameObject>()
				: LoadedSubtree(Current: current, Root: Added, RootDelta: RootDelta);
			int removedCount = 0;
			for (int i = 0; i < Expected.Objects.Count; i++)
				if (Removed != null && ReferenceEquals(Expected.Objects[i].Root, Removed)) removedCount++;
			if (current.Objects.Count != Expected.Objects.Count - removedCount + addedTree.Count)
				return false;
			for (int i = 0; i < Expected.Objects.Count; i++)
			{
				LoadedObjectWitness prior = Expected.Objects[i];
				if (Removed != null && ReferenceEquals(prior.Root, Removed))
				{
					if (FindLoadedRow(current, prior.Object) != null) return false;
					continue;
				}
				LoadedObjectWitness now = FindLoadedRow(current, prior.Object);
				if (now == null) return false;
				if (ReferenceEquals(prior.Object, ChangedInventoryOwner))
				{
					if (!ReferenceEquals(prior.Object, now.Object)
						|| !ReferenceEquals(prior.Root, now.Root)
						|| !ReferenceEquals(prior.Zone, now.Zone)
						|| !ReferenceEquals(prior.Inventory, now.Inventory)
						|| !ReferenceEquals(prior.InventoryObjects, now.InventoryObjects)
						|| prior.InventoryRows == null || now.InventoryRows == null
						|| now.InventoryRows.Length != prior.InventoryRows.Length + 1)
						return false;
					for (int j = 0; j < prior.InventoryRows.Length; j++)
						if (!ReferenceEquals(prior.InventoryRows[j], now.InventoryRows[j])) return false;
					if (!ReferenceEquals(now.InventoryRows[now.InventoryRows.Length - 1], Added))
						return false;
				}
				else if (!ExactLoadedRow(prior, now)) return false;
			}
			for (int i = 0; i < current.Objects.Count; i++)
			{
				GameObject item = current.Objects[i].Object;
				if (FindLoadedRow(Expected, item) == null
					&& !ContainsObjectReference(addedTree, item)) return false;
			}
			for (int i = 0; i < Expected.Zones.Count; i++)
			{
				GameObject[] prior = Expected.Zones[i].Roots;
				GameObject[] now = current.Zones[i].Roots;
				int delta = 0;
				if (RootDelta && Added != null
					&& Added.CurrentZone == current.Zones[i].Zone) delta++;
				if (RootDelta && Removed != null
					&& ReferenceEquals(Expected.Zones[i].Zone,
						FindLoadedRow(Expected, Removed)?.Zone)) delta--;
				if (now.Length != prior.Length + delta) return false;
				for (int j = 0; j < prior.Length; j++)
					if (!ReferenceEquals(prior[j], Removed)
						&& !ContainsObjectReference(now, prior[j])) return false;
				if (delta > 0 && !ContainsObjectReference(now, Added)) return false;
			}
			return true;
		}

		private static List<GameObject> LoadedSubtree(LoadedTopologyWitness Current,
			GameObject Root, bool RootDelta)
		{
			List<GameObject> result = new List<GameObject>();
			if (Current == null || Root == null) return result;
			for (int i = 0; i < Current.Objects.Count; i++)
			{
				LoadedObjectWitness row = Current.Objects[i];
				if ((RootDelta && ReferenceEquals(row.Root, Root))
					|| (!RootDelta && (ReferenceEquals(row.Object, Root)
						|| IsContentDescendant(Current, row.Object, Root)))) result.Add(row.Object);
			}
			return result;
		}

		private static bool IsContentDescendant(LoadedTopologyWitness Topology,
			GameObject Candidate, GameObject Ancestor)
		{
			HashSet<GameObject> frontier = new HashSet<GameObject> { Ancestor };
			for (int pass = 0; pass < Topology.Objects.Count; pass++)
			{
				bool changed = false;
				for (int i = 0; i < Topology.Objects.Count; i++)
				{
					LoadedObjectWitness row = Topology.Objects[i];
					if (!frontier.Contains(row.Object)) continue;
					for (int j = 0; j < row.Contents.Length; j++)
						if (frontier.Add(row.Contents[j])) changed = true;
				}
				if (frontier.Contains(Candidate)) return true;
				if (!changed) return false;
			}
			return frontier.Contains(Candidate);
		}

	}
}
