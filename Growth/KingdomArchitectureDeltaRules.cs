using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRules
	{
		// --- Exact tier delta ---------------------------------------------------------------

		/// <summary>
		/// Builds exact scenery work while refusing a moved main or changed old stateful fixture.
		/// Main behavior object is intentionally outside both snapshots and must survive in runtime.
		/// </summary>
		public static bool TryBuildDelta(ArchitectureLayoutSnapshot Before,
			ArchitectureLayoutSnapshot After, out ArchitectureLayoutDelta Delta, out string Failure)
		{
			Delta = null;
			if (!TryValidateTopology(Before, null, out Failure)
				|| !TryValidateTopology(After, null, out Failure)) return false;
			bool heartAccretion = IsAdjacentHeartAccretion(Before, After);
			if (FoldType(Before.LotType) != FoldType(After.LotType)
				|| Before.Facing != After.Facing
				|| (!heartAccretion && Before.LotSize != After.LotSize))
				return Fail("layout delta crosses a typed lot set or changes its pose", out Failure);
			if (!heartAccretion && (Before.MainX != After.MainX || Before.MainY != After.MainY))
				return Fail("layout delta moves the main behavior anchor", out Failure);
			if (heartAccretion)
				return TryBuildHeartAccretionDelta(Before, After, out Delta, out Failure);
			Dictionary<string, ArchitecturePlacement> oldBySlot = PlacementDictionary(Before.Placements);
			Dictionary<string, ArchitecturePlacement> newBySlot = PlacementDictionary(After.Placements);
			Dictionary<string, ArchitecturePlacement> oldState = StatefulDictionary(Before.Placements);
			Dictionary<string, ArchitecturePlacement> newState = StatefulDictionary(After.Placements);
			foreach (KeyValuePair<string, ArchitecturePlacement> pair in oldState)
			{
				if (!newState.TryGetValue(pair.Key, out ArchitecturePlacement next)
					|| !SamePlacement(pair.Value, next))
					return Fail("stateful anchor " + pair.Key + " would move, change, or disappear", out Failure);
			}
			ArchitectureLayoutDelta delta = new ArchitectureLayoutDelta { Before = Before, After = After };
			foreach (KeyValuePair<string, ArchitecturePlacement> pair in oldBySlot)
			{
				if (newBySlot.TryGetValue(pair.Key, out ArchitecturePlacement next)
					&& SamePlacement(pair.Value, next)) delta.Retained.Add(pair.Value);
				else delta.Removed.Add(pair.Value);
			}
			foreach (KeyValuePair<string, ArchitecturePlacement> pair in newBySlot)
			{
				if (!oldBySlot.TryGetValue(pair.Key, out ArchitecturePlacement previous)
					|| !SamePlacement(previous, pair.Value)) delta.Added.Add(pair.Value);
			}
			delta.Retained.Sort(ComparePlacements);
			for (int i = 0; i < delta.Retained.Count; i++)
				delta.RetainedAfter.Add(newBySlot[delta.Retained[i].Slot]);
			delta.Removed.Sort(ComparePlacementsReverse);
			delta.Added.Sort(ComparePlacements);
			Dictionary<string, ArchitectureCellState> oldCells = CoordinateCells(Before.Cells);
			Dictionary<string, ArchitectureCellState> newCells = CoordinateCells(After.Cells);
			HashSet<string> coordinates = new HashSet<string>(oldCells.Keys, StringComparer.Ordinal);
			coordinates.UnionWith(newCells.Keys);
			List<string> orderedCoordinates = new List<string>(coordinates);
			orderedCoordinates.Sort(StringComparer.Ordinal);
			for (int i = 0; i < orderedCoordinates.Count; i++)
			{
				string coordinate = orderedCoordinates[i];
				oldCells.TryGetValue(coordinate, out ArchitectureCellState before);
				newCells.TryGetValue(coordinate, out ArchitectureCellState after);
				if (!SameCell(before, after))
				{
					ArchitectureCellState source = before ?? after;
					delta.Cells.Add(new ArchitectureCellDelta
						{ X = source.X, Y = source.Y, Before = before, After = after });
				}
			}
			Delta = delta;
			return true;
		}

		private static bool IsAdjacentHeartAccretion(ArchitectureLayoutSnapshot Before,
			ArchitectureLayoutSnapshot After)
		{
			if (Before == null || After == null || Before.PlanKey != "civic-heart"
				|| After.PlanKey != "civic-heart" || FoldType(Before.LotType) != "civic"
				|| FoldType(After.LotType) != "civic") return false;
			int beforeRung = KingdomPlotRules.HeartRungOf(Before.BuildKey);
			int afterRung = KingdomPlotRules.HeartRungOf(After.BuildKey);
			return beforeRung > 0 && afterRung == beforeRung + 1
				&& (int)After.LotSize == (int)Before.LotSize + 1;
		}

		private static bool TryBuildHeartAccretionDelta(ArchitectureLayoutSnapshot Before,
			ArchitectureLayoutSnapshot After, out ArchitectureLayoutDelta Delta,
			out string Failure)
		{
			Delta = null;
			ArchitectureLayoutDelta delta = new ArchitectureLayoutDelta
				{ Before = Before, After = After };
			Dictionary<string, ArchitecturePlacement> afterByRelative =
				RelativePlacements(After);
			HashSet<string> retainedAfterSlots = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Before.Placements.Count; i++)
			{
				ArchitecturePlacement oldPlacement = Before.Placements[i];
				ArchitecturePlacement next;
				if (afterByRelative.TryGetValue(RelativePlacementKey(Before, oldPlacement), out next)
					&& SameHeartPlacement(oldPlacement, next))
				{
					delta.Retained.Add(oldPlacement);
					delta.RetainedAfter.Add(next);
					retainedAfterSlots.Add(next.Slot);
				}
				else delta.Removed.Add(oldPlacement);
			}
			for (int i = 0; i < After.Placements.Count; i++)
				if (!retainedAfterSlots.Contains(After.Placements[i].Slot))
					delta.Added.Add(After.Placements[i]);
			if (delta.Removed.Count != 0)
				return Fail("heart accretion would remove or replace existing authored fabric",
					out Failure);
			for (int stateful = 0; stateful < Before.Placements.Count; stateful++)
			{
				ArchitecturePlacement prior = Before.Placements[stateful];
				if (string.IsNullOrEmpty(prior.StatefulAnchor)) continue;
				int retained = delta.Retained.IndexOf(prior);
				if (retained < 0 || retained >= delta.RetainedAfter.Count
					|| AnchorRole(prior.StatefulAnchor)
						!= AnchorRole(delta.RetainedAfter[retained].StatefulAnchor))
					return Fail("heart stateful anchor " + prior.StatefulAnchor
						+ " would move, change, or disappear", out Failure);
			}
			Dictionary<string, ArchitectureCellState> oldCells = RelativeCells(Before);
			Dictionary<string, ArchitectureCellState> newCells = RelativeCells(After);
			// Heart tiers accrete. Every old claimed-cell contract remains exactly where it was
			// relative to the stable behavior root. A later tier may put a stronger authored roof
			// over retained open/soft yard, but may never reopen it or change natural fabric.
			foreach (KeyValuePair<string, ArchitectureCellState> pair in oldCells)
			{
				ArchitectureCellState next;
				if (!newCells.TryGetValue(pair.Key, out next)
					|| !SameHeartCell(Before, pair.Value, After, next))
					return Fail("heart accretion would remove or alter existing authored cell fabric",
						out Failure);
			}
			List<string> ordered = new List<string>(newCells.Keys);
			ordered.Sort(StringComparer.Ordinal);
			for (int i = 0; i < ordered.Count; i++)
			{
				ArchitectureCellState oldCell;
				ArchitectureCellState newCell;
				oldCells.TryGetValue(ordered[i], out oldCell);
				newCells.TryGetValue(ordered[i], out newCell);
				if (oldCell == null)
				{
					delta.Cells.Add(new ArchitectureCellDelta
						{ X = newCell.X, Y = newCell.Y, Before = null, After = newCell });
				}
				else if (oldCell.Cover != newCell.Cover)
				{
					delta.Cells.Add(new ArchitectureCellDelta
						{ X = newCell.X, Y = newCell.Y, Before = oldCell, After = newCell });
				}
			}
			Delta = delta;
			Failure = null;
			return true;
		}

		private static Dictionary<string, ArchitecturePlacement> RelativePlacements(
			ArchitectureLayoutSnapshot Snapshot)
		{
			Dictionary<string, ArchitecturePlacement> result =
				new Dictionary<string, ArchitecturePlacement>(StringComparer.Ordinal);
			for (int i = 0; i < Snapshot.Placements.Count; i++)
				result[RelativePlacementKey(Snapshot, Snapshot.Placements[i])] =
					Snapshot.Placements[i];
			return result;
		}

		private static string RelativePlacementKey(ArchitectureLayoutSnapshot Snapshot,
			ArchitecturePlacement Placement)
		{
			return ((int)Placement.Layer).ToString(CultureInfo.InvariantCulture) + ":"
				+ (Placement.X - Snapshot.MainX).ToString(CultureInfo.InvariantCulture) + ":"
				+ (Placement.Y - Snapshot.MainY).ToString(CultureInfo.InvariantCulture);
		}

		private static Dictionary<string, ArchitectureCellState> RelativeCells(
			ArchitectureLayoutSnapshot Snapshot)
		{
			Dictionary<string, ArchitectureCellState> result =
				new Dictionary<string, ArchitectureCellState>(StringComparer.Ordinal);
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = Snapshot.Cells[i];
				result[(cell.X - Snapshot.MainX).ToString(CultureInfo.InvariantCulture) + ":"
					+ (cell.Y - Snapshot.MainY).ToString(CultureInfo.InvariantCulture)] = cell;
			}
			return result;
		}

		private static bool SameHeartPlacement(ArchitecturePlacement A,
			ArchitecturePlacement B)
		{
			return A != null && B != null && A.Layer == B.Layer && A.Blueprint == B.Blueprint
				&& A.Material == B.Material && A.MinTech == B.MinTech
				&& A.Knowledge == B.Knowledge && A.Power == B.Power
				&& A.Natural == B.Natural && A.ExistingAuthority == B.ExistingAuthority
				&& AnchorRole(A.StatefulAnchor) == AnchorRole(B.StatefulAnchor);
		}

		private static bool SameHeartCell(ArchitectureLayoutSnapshot ALayout,
			ArchitectureCellState A, ArchitectureLayoutSnapshot BLayout, ArchitectureCellState B)
		{
			if (A == null || B == null) return A == B;
			return A.X - ALayout.MainX == B.X - BLayout.MainX
				&& A.Y - ALayout.MainY == B.Y - BLayout.MainY
				&& A.Claim == B.Claim && A.Passability == B.Passability
				&& PermittedHeartCoverTransition(A.Cover, B.Cover);
		}

		private static bool PermittedHeartCoverTransition(ArchitectureCover Before,
			ArchitectureCover After)
		{
			if (Before == After) return true;
			return (Before == ArchitectureCover.Open
				&& (After == ArchitectureCover.Soft || After == ArchitectureCover.Walled))
				|| (Before == ArchitectureCover.Soft && After == ArchitectureCover.Walled);
		}
		private static Dictionary<string, ArchitecturePlacement> PlacementDictionary(
			IList<ArchitecturePlacement> Placements)
		{
			Dictionary<string, ArchitecturePlacement> result =
				new Dictionary<string, ArchitecturePlacement>(StringComparer.Ordinal);
			for (int i = 0; i < Placements.Count; i++) result[Placements[i].Slot] = Placements[i];
			return result;
		}

		private static Dictionary<string, ArchitecturePlacement> StatefulDictionary(
			IList<ArchitecturePlacement> Placements)
		{
			Dictionary<string, ArchitecturePlacement> result =
				new Dictionary<string, ArchitecturePlacement>(StringComparer.Ordinal);
			for (int i = 0; i < Placements.Count; i++)
				if (!string.IsNullOrEmpty(Placements[i].StatefulAnchor))
					result[Placements[i].StatefulAnchor] = Placements[i];
			return result;
		}

		private static Dictionary<string, ArchitectureCellState> CoordinateCells(
			IList<ArchitectureCellState> Cells)
		{
			Dictionary<string, ArchitectureCellState> result =
				new Dictionary<string, ArchitectureCellState>(StringComparer.Ordinal);
			for (int i = 0; i < Cells.Count; i++)
				result[CoordinateKey(Cells[i].X, Cells[i].Y)] = Cells[i];
			return result;
		}

		private static string CoordinateKey(int X, int Y)
		{
			return X.ToString("D2", CultureInfo.InvariantCulture) + ":"
				+ Y.ToString("D2", CultureInfo.InvariantCulture);
		}

		private static bool SamePlacement(ArchitecturePlacement A, ArchitecturePlacement B)
		{
			return A != null && B != null && A.Layer == B.Layer && A.X == B.X && A.Y == B.Y
				&& A.Slot == B.Slot && A.Blueprint == B.Blueprint
				&& A.Material == B.Material && A.MinTech == B.MinTech
				&& A.Knowledge == B.Knowledge
				&& A.Power == B.Power
				&& A.Natural == B.Natural && A.ExistingAuthority == B.ExistingAuthority
				&& A.StatefulAnchor == B.StatefulAnchor;
		}

		private static bool SameCell(ArchitectureCellState A, ArchitectureCellState B)
		{
			if (A == null || B == null) return A == B;
			return A.X == B.X && A.Y == B.Y && A.Claim == B.Claim
				&& A.Passability == B.Passability && A.Cover == B.Cover;
		}

	}
}
