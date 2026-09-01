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
		/// Builds exact scenery work under the incoming mode frozen by the successor snapshot.
		/// </summary>
		public static bool TryBuildDelta(ArchitectureLayoutSnapshot Before,
			ArchitectureLayoutSnapshot After, out ArchitectureLayoutDelta Delta, out string Failure)
		{
			return TryBuildDelta(Before, After, After == null
				? ArchitectureTransitionMode.None : After.IncomingTransitionMode,
				out Delta, out Failure);
		}

		/// <summary>
		/// Mode-explicit form used by declared socket routes. Additive modes retain every
		/// predecessor placement; AdditiveExpand also admits a larger envelope. Renovate may rebuild
		/// stateless fabric in one envelope; RenovateExpand admits that rebuild while growing it.
		/// Protected state remains the same physical placement; moving it needs a separately
		/// registered handover not present here. Replacement is never disguised as in-place work.
		/// </summary>
		public static bool TryBuildDelta(ArchitectureLayoutSnapshot Before,
			ArchitectureLayoutSnapshot After, ArchitectureTransitionMode Mode,
			out ArchitectureLayoutDelta Delta, out string Failure)
		{
			Delta = null;
			if (!TryValidateTopology(Before, null, out Failure)
				|| !TryValidateTopology(After, null, out Failure)) return false;
			if (Mode == ArchitectureTransitionMode.None)
				return Fail("layout delta has no authored incoming transition mode", out Failure);
			if (!KingdomArchitectureTransitionRules.IsKnown(Mode))
				return Fail("layout delta has an unknown transition mode", out Failure);
			if (After.IncomingTransitionMode != Mode)
				return Fail("layout delta mode does not match frozen successor authority",
					out Failure);
			if (Mode == ArchitectureTransitionMode.Replacement)
				return Fail("replacement is not an in-place upgrade; strike the standing work and "
					+ "commission the successor fresh", out Failure);
			if (!KingdomArchitectureTransitionRules.IsInPlace(Mode))
				return Fail("layout delta mode is not an in-place transition", out Failure);
			bool expanding = Before.LotSize != After.LotSize;
			if (FoldType(Before.LotType) != FoldType(After.LotType)
				|| Before.Facing != After.Facing)
				return Fail("layout delta crosses its typed lot or changes pose", out Failure);
			if (expanding && (!KingdomArchitectureTransitionRules.AllowsLotExpansion(Mode)
				|| (int)After.LotSize <= (int)Before.LotSize))
				return Fail("layout delta changes envelope without additive-expand or "
					+ "renovate-expand authority",
					out Failure);
			if (!expanding && (Before.MainX != After.MainX || Before.MainY != After.MainY))
				return Fail("layout delta moves the main behavior anchor", out Failure);
			if (!TryValidateFootprintTransition(Before, After, Mode, out Failure)) return false;

			ArchitectureLayoutDelta delta = new ArchitectureLayoutDelta
				{ Before = Before, After = After };
			Dictionary<string, ArchitecturePlacement> afterByKey = expanding
				? RelativePlacements(After) : PlacementDictionary(After.Placements);
			HashSet<string> retainedAfterSlots = new HashSet<string>(StringComparer.Ordinal);
			Dictionary<string, ArchitecturePlacement> retainedPartners =
				new Dictionary<string, ArchitecturePlacement>(StringComparer.Ordinal);
			for (int i = 0; i < Before.Placements.Count; i++)
			{
				ArchitecturePlacement oldPlacement = Before.Placements[i];
				string key = expanding ? RelativePlacementKey(Before, oldPlacement)
					: oldPlacement.Slot;
				if (afterByKey.TryGetValue(key, out ArchitecturePlacement next)
					&& SamePlacementInFrame(oldPlacement, next, expanding))
				{
					delta.Retained.Add(oldPlacement);
					retainedPartners.Add(oldPlacement.Slot, next);
					retainedAfterSlots.Add(next.Slot);
				}
				else if (ProtectedPlacement(oldPlacement))
				{
					return Fail("protected anchor "
						+ (oldPlacement.StatefulAnchor ?? oldPlacement.Slot)
						+ " would move, change, or disappear without a registered handover",
						out Failure);
				}
				else delta.Removed.Add(oldPlacement);
			}
			for (int i = 0; i < After.Placements.Count; i++)
				if (!retainedAfterSlots.Contains(After.Placements[i].Slot))
					delta.Added.Add(After.Placements[i]);
			delta.Retained.Sort(ComparePlacements);
			for (int i = 0; i < delta.Retained.Count; i++)
				delta.RetainedAfter.Add(retainedPartners[delta.Retained[i].Slot]);
			delta.Removed.Sort(ComparePlacementsReverse);
			delta.Added.Sort(ComparePlacements);

			Dictionary<string, ArchitectureCellState> oldCells = expanding
				? RelativeCells(Before) : CoordinateCells(Before.Cells);
			Dictionary<string, ArchitectureCellState> newCells = expanding
				? RelativeCells(After) : CoordinateCells(After.Cells);
			HashSet<string> coordinates = new HashSet<string>(oldCells.Keys,
				StringComparer.Ordinal);
			coordinates.UnionWith(newCells.Keys);
			List<string> ordered = new List<string>(coordinates);
			ordered.Sort(StringComparer.Ordinal);
			for (int i = 0; i < ordered.Count; i++)
			{
				ArchitectureCellState oldCell;
				ArchitectureCellState newCell;
				oldCells.TryGetValue(ordered[i], out oldCell);
				newCells.TryGetValue(ordered[i], out newCell);
				if (SameCellInFrame(oldCell, newCell, expanding)) continue;
				ArchitectureCellState source = newCell ?? oldCell;
				delta.Cells.Add(new ArchitectureCellDelta
					{ X = source.X, Y = source.Y, Before = oldCell, After = newCell });
			}
			if (KingdomArchitectureTransitionRules.PreservesStandingFabric(Mode)
				&& delta.Removed.Count != 0)
				return Fail("additive transition would remove or replace standing fabric", out Failure);
			if (KingdomArchitectureTransitionRules.PreservesStandingFabric(Mode))
				for (int i = 0; i < delta.Cells.Count; i++)
				{
					ArchitectureCellDelta changed = delta.Cells[i];
					// New envelope cells have no predecessor fabric to preserve. They may remain
					// deliberately open/unclaimed; an expansion is a reservation as well as a build.
					if (changed.Before == null) continue;
					if (changed.After == null)
						return Fail("additive transition crops standing cell fabric", out Failure);
					if (!PermittedAdditiveCellTransition(changed.Before, changed.After)
						|| !HasAddedPlacementAt(delta.Added, changed.After.X, changed.After.Y))
						return Fail("additive transition weakens cell semantics or changes them "
							+ "without new fabric", out Failure);
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

		private static bool ProtectedPlacement(ArchitecturePlacement Placement)
		{
			return Placement != null && (Placement.ExistingAuthority
				|| !string.IsNullOrEmpty(Placement.StatefulAnchor));
		}

		private static bool SamePlacementInFrame(ArchitecturePlacement A,
			ArchitecturePlacement B, bool Relative)
		{
			if (!Relative) return SamePlacement(A, B);
			return A != null && B != null && A.Layer == B.Layer && A.Blueprint == B.Blueprint
				&& A.Material == B.Material && A.MinTech == B.MinTech
				&& A.Knowledge == B.Knowledge && A.Power == B.Power
				&& A.Natural == B.Natural && A.ExistingAuthority == B.ExistingAuthority
				&& AnchorRole(A.StatefulAnchor) == AnchorRole(B.StatefulAnchor);
		}

		private static bool SameCellInFrame(ArchitectureCellState A,
			ArchitectureCellState B, bool Relative)
		{
			if (!Relative) return SameCell(A, B);
			if (A == null || B == null) return A == B;
			return A.Claim == B.Claim && A.Passability == B.Passability
				&& A.Cover == B.Cover;
		}

		private static bool HasAddedPlacementAt(IList<ArchitecturePlacement> Placements,
			int X, int Y)
		{
			for (int i = 0; i < Placements.Count; i++)
				if (Placements[i].X == X && Placements[i].Y == Y) return true;
			return false;
		}

		/// <summary>
		/// An additive edge may claim, furnish, obstruct, or roof previously softer space, but
		/// never unclaim it, reopen circulation, or weaken its weather/structural cover. Every
		/// actual semantic change is separately required to carry newly paid fabric on that cell.
		/// </summary>
		private static bool PermittedAdditiveCellTransition(ArchitectureCellState Before,
			ArchitectureCellState After)
		{
			if (Before == null || After == null) return false;
			if (IsClaimed(Before.Claim) && !IsClaimed(After.Claim)) return false;
			if (Before.Claim == ArchitectureClaim.Building
				&& After.Claim != ArchitectureClaim.Building) return false;
			if (Before.Passability != After.Passability
				&& Before.Passability != ArchitecturePassability.Walkable) return false;
			if (Before.Cover == After.Cover) return true;
			return (Before.Cover == ArchitectureCover.Open
					&& (After.Cover == ArchitectureCover.Soft
						|| After.Cover == ArchitectureCover.Walled))
				|| (Before.Cover == ArchitectureCover.Soft
					&& After.Cover == ArchitectureCover.Walled);
		}

		private static Dictionary<string, ArchitecturePlacement> PlacementDictionary(
			IList<ArchitecturePlacement> Placements)
		{
			Dictionary<string, ArchitecturePlacement> result =
				new Dictionary<string, ArchitecturePlacement>(StringComparer.Ordinal);
			for (int i = 0; i < Placements.Count; i++) result[Placements[i].Slot] = Placements[i];
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
