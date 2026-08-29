using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		private const string PlotFinalRootPrefix = "r_TAF_PlotFinalRoot:";
		private const string PlotFinalPredecessorProperty = "r_TAF_PlotFinalPredecessor";

		private static bool RootPlotFinalOutput(string Id, GameObject Output)
		{
			string key = PlotFinalRootPrefix + Id;
			if (The.Game?.ObjectGameState == null || !GameObject.Validate(Output)
				|| string.IsNullOrEmpty(Id) || Output.IDIfAssigned != Id) return false;
			if (The.Game.ObjectGameState.TryGetValue(key, out object prior)
				&& !object.ReferenceEquals(prior, Output)) return false;
			try { The.Game.SetObjectGameState(key, Output); }
			catch { return false; }
			return ExactPlotFinalRoot(Id, Output, true)
				&& FindGlobalFoundingHeartId(Id, out GameObject exact, out bool graveyard)
					== KingdomPhysicalLookupState.Exact
				&& !graveyard && object.ReferenceEquals(exact, Output);
		}

		private static bool TryPlotFinalRoot(string Id, out GameObject Output)
		{
			Output = null;
			if (The.Game?.ObjectGameState == null || string.IsNullOrEmpty(Id)
				|| !The.Game.ObjectGameState.TryGetValue(PlotFinalRootPrefix + Id,
					out object rooted)) return false;
			Output = rooted as GameObject;
			return GameObject.Validate(Output) && Output.IDIfAssigned == Id
				&& ExactPlotFinalRoot(Id, Output, true)
				&& FindGlobalFoundingHeartId(Id, out GameObject exact, out bool graveyard)
					== KingdomPhysicalLookupState.Exact
				&& !graveyard && object.ReferenceEquals(exact, Output);
		}

		private static bool ExactPlotFinalRootCustody(string Id, GameObject Expected)
		{
			return TryPlotFinalRoot(Id, out GameObject rooted)
				&& object.ReferenceEquals(rooted, Expected);
		}

		private static bool RetirePlotFinalRoot(string Id, GameObject Output)
		{
			string key = PlotFinalRootPrefix + Id;
			if (The.Game?.ObjectGameState == null) return false;
			if (!The.Game.ObjectGameState.TryGetValue(key, out object rooted))
				return ExactPlotFinalRoot(Id, Output, false);
			if (!object.ReferenceEquals(rooted, Output)) return false;
			The.Game.ObjectGameState.Remove(key);
			return ExactPlotFinalRoot(Id, Output, false);
		}

		private static bool ExactPlotFinalRoot(string Id, GameObject Expected, bool Present)
		{
			if (The.Game?.ObjectGameState == null || string.IsNullOrEmpty(Id)) return false;
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
							if (row.Key != PlotFinalRootPrefix + Id
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

		private static KingdomPhysicalLookupState FindPlotFinalRootForPredecessor(
			string PredecessorId, out GameObject Output)
		{
			Output = null;
			if (The.Game?.ObjectGameState == null || string.IsNullOrEmpty(PredecessorId)
				|| The.Game.ObjectGameState.Count > 65536)
				return KingdomPhysicalLookupState.Ambiguous;
			int matches = 0;
			try
			{
				foreach (KeyValuePair<string, object> row in The.Game.ObjectGameState)
				{
					if (!row.Key.StartsWith(PlotFinalRootPrefix,
						global::System.StringComparison.Ordinal)) continue;
					GameObject candidate = row.Value as GameObject;
					if (!GameObject.Validate(candidate)) return KingdomPhysicalLookupState.Ambiguous;
					if (candidate.GetStringProperty(PlotFinalPredecessorProperty) != PredecessorId)
						continue;
					matches++;
					if (!GameObject.Validate(candidate)
						|| row.Key != PlotFinalRootPrefix + candidate.IDIfAssigned
						|| !ExactPlotFinalRoot(candidate.IDIfAssigned, candidate, true))
						return KingdomPhysicalLookupState.Ambiguous;
					Output = candidate;
				}
			}
			catch { return KingdomPhysicalLookupState.Ambiguous; }
			return matches == 0 ? KingdomPhysicalLookupState.Absent
				: matches == 1 ? KingdomPhysicalLookupState.Exact
				: KingdomPhysicalLookupState.Ambiguous;
		}

		private static bool PreparedPlotFinalOutput(GameObject Building, GameObject Parent,
			KingdomRules.BuildEntry Entry, string Receipt, string PlotId,
			KingdomPlotRules.PlotRect Rect, KingdomPlotRules.PlotRect Footprint,
			KingdomPlotRules.RoofState Roof, string ExpectedId,
			KingdomConstructionJob Job)
		{
			return GameObject.Validate(Building) && GameObject.Validate(Parent)
				&& Building.IDIfAssigned == ExpectedId && Building.Blueprint == Entry.Blueprint
				&& Building.GetStringProperty(PlotFinalPredecessorProperty) == Parent.IDIfAssigned
				&& Building.CurrentCell == null && Building.CurrentZone == null
				&& Building.InInventory == null && Building.GetIntProperty("KingdomBuilt") == 1
				&& Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty) == Entry.Key
				&& Building.GetStringProperty(PlotIdProperty) == PlotId
				&& (string.IsNullOrEmpty(Receipt)
					|| Building.GetStringProperty(KingdomConstruction.ReceiptProperty) == Receipt)
				&& TryReadRect(Building, out KingdomPlotRules.PlotRect rect) && SameRect(rect, Rect)
				&& TryReadFootprint(Building, out KingdomPlotRules.PlotRect foot)
				&& SameRect(foot, Footprint) && RoofOf(Building) == Roof
				&& (Job == null || KingdomConstruction.HasReceipt(Building, Job)
					&& KingdomConstruction.PaidBuildMatches(Building, Job))
				&& PlotPlanMarkerRemovalProofMatches(Parent, Building);
		}
	}
}
