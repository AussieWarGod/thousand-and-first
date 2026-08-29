using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		private static bool TryFreezeMove(KingdomSystem System, Zone Zone,
			KingdomSurvey Survey, GameObject Root, KingdomPlotRules.PlotRect Destination,
			long Now, out KingdomRelocationMove Move, out string Failure)
		{
			Move = null; Failure = null;
			if (!GameObject.Validate(Root) || Root.CurrentZone != Zone
				|| Root.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1
				|| Root.GetIntProperty(KingdomUpgrade.AdoptedProperty) == 1
				|| !KingdomPlots.IsYielding(Root)
				|| Root.GetPart<r_KingdomPlotWorks>() != null
				|| !KingdomPlots.TryReadRect(Root, out KingdomPlotRules.PlotRect source)
				|| source.Width != Destination.Width || source.Height != Destination.Height
				|| !KingdomPlots.TryReadFootprint(Root, out KingdomPlotRules.PlotRect footprint))
			{
				Failure = "Only an exact finished yielding plot raised by the settlement can move.";
				return false;
			}
			string lot = Root.GetStringProperty(KingdomPlots.PlotIdProperty);
			string key = KingdomUpgrade.DesignKeyOf(Root);
			if (string.IsNullOrEmpty(lot) || string.IsNullOrEmpty(key))
			{
				Failure = "The yielding plot has no exact lot or build identity.";
				return false;
			}
			int roots = 0;
			for (int i = 0; i < Survey.PlotRoots.Count; i++)
				if (GameObject.Validate(Survey.PlotRoots[i])
					&& Survey.PlotRoots[i].GetStringProperty(KingdomPlots.PlotIdProperty) == lot)
					roots++;
			if (roots != 1)
			{
				Failure = "The yielding lot identity is absent or duplicated.";
				return false;
			}

			List<KingdomRelocationRow> rows = new List<KingdomRelocationRow>();
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			string rootId = Root.IDIfAssigned;
			if (string.IsNullOrEmpty(rootId)
				|| rootId.Length > KingdomRelocationRules.MaxIdChars)
			{
				Failure = "The yielding plot has no assigned physical identity.";
				return false;
			}
			int hardness = 100;
			for (int i = 0; i < Survey.Objects.Count; i++)
			{
				GameObject item = Survey.Objects[i];
				bool member = ReferenceEquals(item, Root)
					|| (item.GetIntProperty(KingdomPlots.PlotPartProperty) == 1
						&& item.GetStringProperty(KingdomPlots.PlotIdProperty) == lot);
				if (!member) continue;
				Cell cell = item.CurrentCell;
				GameObject exact;
				string itemId = item.IDIfAssigned;
				if (!GameObject.Validate(item) || item.CurrentZone != Zone || cell == null
					|| !source.Contains(cell.X, cell.Y) || string.IsNullOrEmpty(itemId)
					|| itemId.Length > KingdomRelocationRules.MaxIdChars
					|| string.IsNullOrEmpty(item.Blueprint)
					|| item.Blueprint.Length > KingdomRelocationRules.MaxKeyChars
					|| !ids.Add(itemId)
					|| KingdomConstruction.FindExactId(Zone, itemId, out exact)
						!= KingdomPhysicalLookupState.Exact
					|| !ReferenceEquals(exact, item))
				{
					Failure = "The yielding plot's physical identity is malformed, duplicated, or outside its lot.";
					return false;
				}
				rows.Add(new KingdomRelocationRow
				{
					ObjectId = itemId, Blueprint = item.Blueprint,
					OffsetX = cell.X - source.X1, OffsetY = cell.Y - source.Y1,
					Root = ReferenceEquals(item, Root), State = KingdomRelocationRowState.Source
				});
				int material = Hardness(item);
				if (material > hardness) hardness = material;
			}
			if (rows.Count < 1 || rows.Count > KingdomRelocationRules.MaxRowsPerMove
				|| !ids.Contains(rootId))
			{
				Failure = "The yielding plot has no bounded, exact physical fabric.";
				return false;
			}
			rows.Sort(CompareRows);
			if (!TryFreezeArchitecture(System, Zone, Root, source, Destination,
				rows, out KingdomRelocationArchitecture architecture, out Failure)) return false;
			List<KingdomRelocationClearRow> clearance;
			if (!TryFreezeClearance(Zone, Survey, Destination, out clearance, out Failure)) return false;
			long required = KingdomRelocationRules.LabourTicks(source.Area, rows.Count,
				hardness, KingdomRules.TicksPerDay);
			if (required < 1L)
			{
				Failure = "The yielding plot produced no lawful labour quote.";
				return false;
			}
			Move = new KingdomRelocationMove
			{
				RootId = rootId, PlotId = lot, BuildKey = key,
				DisplayName = Root.ShortDisplayNameStripped, Source = Frozen(source),
				Destination = Frozen(Destination), Footprint = Frozen(footprint),
				Roof = (int)KingdomPlots.RoofOf(Root), StartedTick = Now, LastTick = Now,
				RequiredTicks = required, RemainingTicks = required, CompletionTick = 0L,
				Phase = KingdomRelocationMovePhase.Waiting,
				FrameId = Guid.NewGuid().ToString("N"), StakeIds = NewStakeIds(),
				Architecture = architecture, Rows = rows, Clearance = clearance
			};
			return true;
		}

		private static string[] NewStakeIds()
		{
			string[] ids = new string[KingdomRelocationRules.MaxStakeIds];
			for (int i = 0; i < ids.Length; i++) ids[i] = Guid.NewGuid().ToString("N");
			return ids;
		}

		private static int CompareRows(KingdomRelocationRow A, KingdomRelocationRow B)
		{
			if (A.Root != B.Root) return A.Root ? 1 : -1; // behavior root moves last.
			int compared = A.OffsetY.CompareTo(B.OffsetY);
			if (compared != 0) return compared;
			compared = A.OffsetX.CompareTo(B.OffsetX);
			if (compared != 0) return compared;
			compared = string.CompareOrdinal(A.Blueprint, B.Blueprint);
			return compared != 0 ? compared : string.CompareOrdinal(A.ObjectId, B.ObjectId);
		}

		private static int Hardness(GameObject Item)
		{
			string blueprint = (Item?.Blueprint ?? "").ToLowerInvariant();
			if (Item != null && (Item.HasPart("Metal") || blueprint.Contains("metal")
				|| blueprint.Contains("steel") || blueprint.Contains("fulcrete"))) return 180;
			if (Item != null && (Item.IsWall() || blueprint.Contains("stone")
				|| blueprint.Contains("marble") || blueprint.Contains("rock"))) return 145;
			return 100;
		}
	}
}
