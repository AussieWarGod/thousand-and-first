using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRuntime
	{
		/// <summary>
		/// Resolves invariant selection inputs once while ordinary siting tries many rectangles.
		/// It is read-only: no receipt is frozen and no stock, water, object, or zone property is
		/// changed. The winning rectangle is prepared again at the transaction boundary.
		/// </summary>
		internal sealed class SitingProbe
		{
			private readonly Zone zone;
			private readonly string buildKey;
			private readonly KingdomArchitectureMapping mapping;
			private readonly ArchitectureSelectionContext context;
			private readonly ArchitectureLayoutSnapshot[] snapshots =
				new ArchitectureLayoutSnapshot[4];
			private readonly string[] snapshotFailures = new string[4];
			private readonly bool[] snapshotAttempted = new bool[4];
			private readonly Dictionary<int, int> wornEvidence;

			internal SitingProbe(Zone Zone, string BuildKey,
				KingdomArchitectureMapping Mapping, ArchitectureSelectionContext Context)
			{
				zone = Zone;
				buildKey = BuildKey;
				mapping = Mapping;
				context = Context;
				wornEvidence = ReadWornEvidence(Zone);
			}

			internal bool TryAccept(KingdomPlotRules.PlotRect Rect, out string Failure)
			{
				Failure = null;
				if (!ValidRectInZone(Rect, zone))
					return Fail("the authored lot rectangle is malformed or outside the zone",
						out Failure);
				ArchitectureLotSize actualSize;
				if (!TryRectLotSize(Rect, out actualSize) || actualSize != mapping.LotSize)
					return Fail("the staked rectangle is not an exact authored lot size in any pose",
						out Failure);

				if (mapping.Frontage == ArchitectureFrontage.Road)
					return TryRoadRect(Rect, out Failure);

				ArchitectureFacing facing;
				if (!TryHeartFacing(mapping, zone, Rect, out facing, out Failure)) return false;
				if (!TrySnapshot(facing, out ArchitectureLayoutSnapshot snapshot, out Failure))
					return false;
				return TryVerifyPhysicalIngressRoutes(zone, Rect, snapshot, out Failure);
			}

			/// <summary>
			/// Proves one already-frozen pose against an exact candidate envelope. Envelope growth uses
			/// this form so retries cannot reselect a variant or facing, and always requires a surviving
			/// public entrance beside physical or worn road evidence outside the enlarged rectangle.
			/// </summary>
			internal bool TryAcceptExact(KingdomPlotRules.PlotRect Rect,
				ArchitectureLayoutSnapshot Snapshot, bool RequirePublicIngress,
				out string Failure)
			{
				Failure = null;
				if (!ValidRectInZone(Rect, zone))
					return Fail("the authored lot rectangle is malformed or outside the zone",
						out Failure);
				ArchitectureLotSize actualSize;
				if (!TryRectLotSize(Rect, out actualSize) || actualSize != mapping.LotSize)
					return Fail("the staked rectangle is not an exact authored lot size in any pose",
						out Failure);
				if (!MatchesMapping(Snapshot, mapping))
					return Fail("frozen successor disagrees with its exact target mapping",
						out Failure);
				int width;
				int height;
				if (!KingdomArchitectureRules.TryWorldDimensions(Snapshot.Width, Snapshot.Height,
					Snapshot.Facing, out width, out height) || Rect.Width != width
					|| Rect.Height != height)
					return Fail("frozen successor does not fit this exact target pose", out Failure);
				if (!TryValidateFrozenSnapshot(Snapshot, out Failure)) return false;
				if (!RequirePublicIngress) return true;
				int score;
				if (!TryRoadIngressScore(Rect, Snapshot, out score, out Failure)) return false;
				if (score <= 0)
					return Fail("building " + Snapshot.BuildKey
						+ " has no authored public entrance connected to existing road evidence",
						out Failure);
				return true;
			}

			private bool TryRoadRect(KingdomPlotRules.PlotRect Rect, out string Failure)
			{
				Failure = null;
				ArchitectureFacing[] candidates = new ArchitectureFacing[]
				{
					ArchitectureFacing.North, ArchitectureFacing.East,
					ArchitectureFacing.South, ArchitectureFacing.West
				};
				int bestScore = -1;
				for (int i = 0; i < candidates.Length; i++)
				{
					ArchitectureFacing facing = candidates[i];
					int width;
					int height;
					if (!KingdomArchitectureRules.TryDimensions(mapping.LotSize, facing,
						out width, out height) || width != Rect.Width || height != Rect.Height)
						continue;
					ArchitectureLayoutSnapshot snapshot;
					if (!TrySnapshot(facing, out snapshot, out Failure)) return false;
					int score;
					if (!TryRoadIngressScore(Rect, snapshot, out score, out Failure)) return false;
					if (score > bestScore) bestScore = score;
				}
				if (bestScore <= 0)
					return Fail("building " + mapping.BuildKey
						+ " has no authored public entrance connected to existing road evidence",
						out Failure);
				return true;
			}

			private bool TrySnapshot(ArchitectureFacing Facing,
				out ArchitectureLayoutSnapshot Snapshot, out string Failure)
			{
				int index = (int)Facing;
				Snapshot = null;
				Failure = null;
				if (index < 0 || index >= snapshots.Length)
					return Fail("the siting probe received an unknown cardinal pose", out Failure);
				if (!snapshotAttempted[index])
				{
					snapshotAttempted[index] = true;
					ArchitectureLayoutSnapshot resolved;
					string failure;
					if (!KingdomArchitecture.TryResolve(buildKey, mapping.TypeKey,
						mapping.LotSize, context, Facing, out resolved, out failure))
						snapshotFailures[index] = failure;
					else if (!MatchesMapping(resolved, mapping))
						snapshotFailures[index] = mapping.Frontage == ArchitectureFrontage.Road
							? "road-facing candidate disagrees with its frozen mapping"
							: "resolved architecture disagrees with its frozen building mapping";
					else if (!TryValidateFrozenSnapshot(resolved, out failure))
						snapshotFailures[index] = failure;
					else snapshots[index] = resolved;
				}
				Snapshot = snapshots[index];
				if (Snapshot == null)
					return Fail(snapshotFailures[index]
						?? "the authored architecture pose could not be resolved", out Failure);
				return true;
			}

			/// <summary>Proves the invariant receipt once per authored pose. Actual candidate rects
			/// are exact translations of this origin rect and are separately bounded to the zone.</summary>
			private bool TryRoadIngressScore(KingdomPlotRules.PlotRect Rect,
				ArchitectureLayoutSnapshot Snapshot, out int Score, out string Failure)
			{
				Score = 0;
				Failure = null;
				List<ArchitecturePoint> lanes = new List<ArchitecturePoint>();
				if (!TryPhysicalRoadIngressLanes(zone, Rect, Snapshot, lanes, out Failure))
					return false;
				for (int i = 0; i < lanes.Count; i++)
				{
					int evidence;
					if (!TryRoadEvidenceAt(lanes[i].X, lanes[i].Y, out evidence, out Failure))
						return false;
					if (evidence > Score) Score = evidence;
				}
				return true;
			}

			private bool TryRoadEvidenceAt(int X, int Y, out int Score, out string Failure)
			{
				Score = 0;
				Failure = null;
				GameObject floor;
				KingdomPhysicalLookupState floorState =
					KingdomRoads.FindOurFloor(zone.GetCell(X, Y), out floor);
				if (floorState == KingdomPhysicalLookupState.Ambiguous)
					return Fail("road ingress evidence is physically ambiguous", out Failure);
				if (floorState == KingdomPhysicalLookupState.Exact)
					Score = 100000 + 1000 * floor.GetIntProperty(KingdomRoads.PathStateProperty);
				int worn;
				if (wornEvidence.TryGetValue(Y * zone.Width + X, out worn) && worn > Score)
					Score = worn;
				return true;
			}

			private static Dictionary<int, int> ReadWornEvidence(Zone Zone)
			{
				Dictionary<int, int> evidence = new Dictionary<int, int>();
				if (Zone == null) return evidence;
				List<KingdomRoadRules.WornCell> tally = KingdomRoads.ReadTally(Zone);
				for (int i = 0; i < tally.Count; i++)
				{
					KingdomRoadRules.WornCell worn = tally[i];
					if (worn.X < 0 || worn.X >= Zone.Width || worn.Y < 0 || worn.Y >= Zone.Height
						|| KingdomRoadRules.WearAt(worn.Traffic)
							<= KingdomRoadRules.WearState.Untouched) continue;
					int traffic = worn.Traffic > KingdomRoadRules.MaxTraffic
						? KingdomRoadRules.MaxTraffic : worn.Traffic;
					int score = 1000 + traffic;
					int key = worn.Y * Zone.Width + worn.X;
					int prior;
					if (!evidence.TryGetValue(key, out prior) || score > prior)
						evidence[key] = score;
				}
				return evidence;
			}
		}

		internal static bool TryCreateSitingProbe(KingdomSystem System, Zone Z,
			KingdomPlotRules.PlotRect SampleRect, string BuildKey, string LotType,
			out SitingProbe Probe, out string Failure)
		{
			Probe = null;
			Failure = null;
			ArchitectureLotSize actualSize;
			if (!TryRectLotSize(SampleRect, out actualSize))
				return Fail("the staked rectangle is not an exact authored lot size in any pose",
					out Failure);
			KingdomArchitectureMapping mapping;
			if (!KingdomArchitecture.TryGetMapping(BuildKey, LotType, actualSize, out mapping))
				return Fail("no exact frozen architecture maps building " + (BuildKey ?? "<null>")
					+ " to typed lot " + (LotType ?? "<null>") + " " + actualSize,
					out Failure);
			if (System == null || !System.Founded)
				return Fail("authored architecture needs a founded settlement", out Failure);
			if (Z == null)
				return Fail("authored architecture needs an exact zone", out Failure);
			if (!ValidRectInZone(SampleRect, Z))
				return Fail("the authored lot rectangle is malformed or outside the zone",
					out Failure);
			ArchitectureSelectionContext context;
			if (!TrySelectionContext(System, Z, out context, out Failure)) return false;
			Probe = new SitingProbe(Z, BuildKey, mapping, context);
			return true;
		}
	}
}
