using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRuntime
	{
		/// <summary>
		/// Post-debit envelope proof. Everything architectural comes from the authenticated
		/// successor snapshot; only current zone bounds and physical road evidence are read.
		/// Catalogue mappings, selection context, technology, materials, and live variants are
		/// deliberately outside this boundary.
		/// </summary>
		internal static bool TryAcceptFrozenEnvelope(Zone Z,
			KingdomPlotRules.PlotRect Rect, ArchitectureLayoutSnapshot Snapshot,
			bool RequirePublicIngress, out string Failure)
		{
			Failure = null;
			if (!ValidRectInZone(Rect, Z) || Snapshot == null)
				return Fail("the frozen authored envelope is malformed or outside the zone",
					out Failure);
			if (!TryRectLotSize(Rect, out ArchitectureLotSize actualSize)
				|| actualSize != Snapshot.LotSize)
				return Fail("the frozen authored envelope is not its exact recorded lot size",
					out Failure);
			if (!KingdomArchitectureRules.TryWorldDimensions(Snapshot.Width, Snapshot.Height,
				Snapshot.Facing, out int width, out int height)
				|| Rect.Width != width || Rect.Height != height)
				return Fail("the frozen successor does not fit its exact recorded pose", out Failure);
			if (!TryValidateFrozenSnapshot(Snapshot, out Failure)) return false;
			if (!RequirePublicIngress) return true;
			if (!TryPhysicalRoadIngressScore(Z, Rect, Snapshot, out int score, out Failure))
				return false;
			if (score <= 0)
				return Fail("building " + Snapshot.BuildKey
					+ " has no authored public entrance connected to existing road evidence",
					out Failure);
			return true;
		}

		private static bool TryValidateFrozenSnapshot(ArchitectureLayoutSnapshot Snapshot,
			out string Failure)
		{
			Failure = null;
			if (!KingdomArchitectureRules.TryEncodeSnapshot(Snapshot, out string encoded,
				out Failure)
				|| !KingdomArchitectureRules.TrySnapshotHash(Snapshot, out string hash,
					out Failure)) return false;
			if (!KingdomArchitectureRules.TryWorldDimensions(Snapshot.Width, Snapshot.Height,
				Snapshot.Facing, out int width, out int height))
				return Fail("the frozen authored pose has no exact world envelope", out Failure);
			KingdomPlotRules.PlotRect origin =
				new KingdomPlotRules.PlotRect(0, 0, width - 1, height - 1);
			if (!TryWorldCoordinate(Snapshot, origin, Snapshot.MainX, Snapshot.MainY,
				out int mainX, out int mainY, out Failure)) return false;
			KingdomArchitectureIntent intent = KingdomArchitectureIntent.Create(
				Snapshot, encoded, hash, origin, mainX, mainY);
			return TryValidateIntent(intent, out _, out Failure);
		}

		private static bool TryPhysicalRoadIngressScore(Zone Z,
			KingdomPlotRules.PlotRect Rect, ArchitectureLayoutSnapshot Snapshot,
			out int Score, out string Failure)
		{
			Score = 0;
			Failure = null;
			List<ArchitecturePoint> lanes = new List<ArchitecturePoint>();
			if (!TryPhysicalRoadIngressLanes(Z, Rect, Snapshot, lanes, out Failure))
				return false;
			for (int i = 0; i < lanes.Count; i++)
			{
				if (!TryPhysicalRoadEvidenceAt(Z, lanes[i].X, lanes[i].Y,
					out int evidence, out Failure)) return false;
				if (evidence > Score) Score = evidence;
			}
			return true;
		}

		private static bool TryPhysicalRoadEvidenceAt(Zone Z, int X, int Y,
			out int Score, out string Failure)
		{
			Score = 0;
			Failure = null;
			GameObject floor;
			KingdomPhysicalLookupState state =
				KingdomRoads.FindOurFloor(Z.GetCell(X, Y), out floor);
			if (state == KingdomPhysicalLookupState.Ambiguous)
				return Fail("road ingress evidence is physically ambiguous", out Failure);
			if (state == KingdomPhysicalLookupState.Exact)
				Score = 100000 + 1000 * floor.GetIntProperty(KingdomRoads.PathStateProperty);
			System.Collections.Generic.List<KingdomRoadRules.WornCell> tally =
				KingdomRoads.ReadTally(Z);
			for (int i = 0; i < tally.Count; i++)
			{
				KingdomRoadRules.WornCell worn = tally[i];
				if (worn.X != X || worn.Y != Y
					|| KingdomRoadRules.WearAt(worn.Traffic)
						<= KingdomRoadRules.WearState.Untouched) continue;
				int traffic = worn.Traffic > KingdomRoadRules.MaxTraffic
					? KingdomRoadRules.MaxTraffic : worn.Traffic;
				int evidence = 1000 + traffic;
				if (evidence > Score) Score = evidence;
			}
			return true;
		}
	}
}
