using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		private const string StagingRootPrefix = "r_TAF_ArchitectureStagingRoot:";

		private static bool RootStagingOutput(GameObject Output)
		{
			string id = Output?.IDIfAssigned;
			string key = StagingRootPrefix + id;
			if (The.Game?.ObjectGameState == null || !GameObject.Validate(Output)
				|| string.IsNullOrEmpty(id)) return false;
			if (The.Game.ObjectGameState.TryGetValue(key, out object prior)
				&& !object.ReferenceEquals(prior, Output)) return false;
			try { The.Game.SetObjectGameState(key, Output); }
			catch { return false; }
			return ExactStagingRoot(id, Output, true)
				&& KingdomPlots.FindGlobalFoundingHeartId(id, out GameObject exact,
					out bool graveyard) == KingdomPhysicalLookupState.Exact
				&& !graveyard && object.ReferenceEquals(exact, Output);
		}

		private static bool TryStagingRoot(string Id, out GameObject Output)
		{
			Output = null;
			if (The.Game?.ObjectGameState == null || string.IsNullOrEmpty(Id)
				|| !The.Game.ObjectGameState.TryGetValue(StagingRootPrefix + Id,
					out object rooted)) return false;
			Output = rooted as GameObject;
			return GameObject.Validate(Output) && Output.IDIfAssigned == Id
				&& ExactStagingRoot(Id, Output, true)
				&& KingdomPlots.FindGlobalFoundingHeartId(Id, out GameObject exact,
					out bool graveyard) == KingdomPhysicalLookupState.Exact
				&& !graveyard && object.ReferenceEquals(exact, Output);
		}

		private static KingdomPhysicalLookupState FindStagingRootForPlacement(string Lot,
			string Hash, ArchitecturePlacement Placement, out GameObject Output)
		{
			Output = null;
			if (The.Game?.ObjectGameState == null || Placement == null
				|| The.Game.ObjectGameState.Count > 65536) return KingdomPhysicalLookupState.Ambiguous;
			int matches = 0;
			try
			{
				foreach (KeyValuePair<string, object> row in The.Game.ObjectGameState)
				{
					if (!row.Key.StartsWith(StagingRootPrefix,
						global::System.StringComparison.Ordinal)) continue;
					GameObject candidate = row.Value as GameObject;
					if (!GameObject.Validate(candidate)) return KingdomPhysicalLookupState.Ambiguous;
					if (candidate.GetStringProperty(KingdomPlots.PlotIdProperty) != Lot
						|| candidate.GetStringProperty(ComponentSlotProperty) != Placement.Slot) continue;
					matches++;
					if (!PreparedStagingComponent(candidate, Lot, Hash, Placement)
						|| row.Key != StagingRootPrefix + candidate.IDIfAssigned
						|| !ExactStagingRoot(candidate.IDIfAssigned, candidate, true))
						return KingdomPhysicalLookupState.Ambiguous;
					Output = candidate;
				}
			}
			catch { return KingdomPhysicalLookupState.Ambiguous; }
			return matches == 0 ? KingdomPhysicalLookupState.Absent
				: matches == 1 ? KingdomPhysicalLookupState.Exact
				: KingdomPhysicalLookupState.Ambiguous;
		}

		private static bool PreparedStagingComponent(GameObject Item, string Lot, string Hash,
			ArchitecturePlacement Placement)
		{
			return GameObject.Validate(Item) && !string.IsNullOrEmpty(Item.IDIfAssigned)
				&& Item.CurrentCell == null && Item.CurrentZone == null && Item.InInventory == null
				&& !Placement.ExistingAuthority && Item.Blueprint == Placement.Blueprint
				&& Item.GetIntProperty(ComponentSchemaProperty) == ComponentSchema
				&& Item.GetStringProperty(KingdomPlots.PlotIdProperty) == Lot
				&& Item.GetStringProperty(ComponentSlotProperty) == Placement.Slot
				&& Item.GetIntProperty(ComponentLayerProperty) == (int)Placement.Layer
				&& Item.GetStringProperty(ComponentHashProperty) == Hash
				&& Item.GetStringProperty(ComponentTokenProperty) == ComponentToken(Lot, Hash, Placement)
				&& Item.GetIntProperty(ComponentExistingProperty) == 0
				&& Item.GetIntProperty(KingdomPlots.PlotPartProperty) == 1
				&& (Item.GetStringProperty(ComponentAnchorProperty) ?? "")
					== (Placement.StatefulAnchor ?? "");
		}

		private static bool RetireStagingRoot(GameObject Output)
		{
			string id = Output?.IDIfAssigned;
			string key = StagingRootPrefix + id;
			if (The.Game?.ObjectGameState == null || string.IsNullOrEmpty(id)) return false;
			if (!The.Game.ObjectGameState.TryGetValue(key, out object rooted))
				return ExactStagingRoot(id, Output, false);
			if (!object.ReferenceEquals(rooted, Output)) return false;
			The.Game.ObjectGameState.Remove(key);
			return ExactStagingRoot(id, Output, false);
		}

		private static bool TryLandStagingRoot(Zone Z, KingdomArchitectureIntent Intent,
			ArchitectureLayoutSnapshot Snapshot, ArchitecturePlacement Placement, string Id,
			out GameObject Output)
		{
			Output = null;
			if (!TryStagingRoot(Id, out GameObject rooted) || rooted.CurrentCell != null
				|| rooted.InInventory != null
				|| !KingdomArchitectureRuntime.TryWorldPlacement(Snapshot, Intent.Rect, Placement,
					out int x, out int y, out _)) return false;
			GameObject accepted = null;
			try { accepted = Z.GetCell(x, y)?.AddObject(rooted, NoStack: true, Silent: true); }
			catch { }
			finally { KingdomSurvey.ObserveAddResultInActive(Z, rooted, accepted); }
			return KingdomConstruction.FindExactId(Z, Id, out Output)
				== KingdomPhysicalLookupState.Exact && object.ReferenceEquals(Output, rooted);
		}

		private static bool ExactStagingRoot(string Id, GameObject Expected, bool Present)
		{
			if (The.Game?.ObjectGameState == null
				|| The.Game.ObjectGameState.Count > 65536) return false;
			int matches = 0;
			int visited = 0;
			try
			{
				foreach (KeyValuePair<string, object> row in The.Game.ObjectGameState)
				{
					GameObject root = row.Value as GameObject;
					if (root == null) continue;
					List<GameObject> pending = new List<GameObject> { root };
					HashSet<GameObject> expanded = new HashSet<GameObject>();
					while (pending.Count > 0)
					{
						GameObject item = pending[pending.Count - 1];
						pending.RemoveAt(pending.Count - 1);
						if (item == null || !expanded.Add(item)) continue;
						if (++visited > 65536) return false;
						if (item.IDIfAssigned == Id)
						{
							matches++;
							if (row.Key != StagingRootPrefix + Id
								|| !object.ReferenceEquals(root, Expected)
								|| !object.ReferenceEquals(item, Expected)) return false;
						}
						List<GameObject> children = item.GetInventoryDirectAndEquipment();
						if (children != null) for (int i = 0; i < children.Count; i++)
							pending.Add(children[i]);
					}
				}
			}
			catch { return false; }
			return Present ? matches == 1 : matches == 0;
		}
	}
}
