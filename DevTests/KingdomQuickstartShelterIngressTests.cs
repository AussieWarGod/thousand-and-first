#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Recomputes the quickstart's declared shelter ingress from the shipped architecture with the
	/// same rules the stake walks. Core/KingdomQuickstartRules.Shelter.cs is engine-free and cannot
	/// read the plot machinery, so this is the proof that what the camp bares is exactly what the
	/// stake's authored public-ingress preflight will require of it.
	/// </summary>
	[TestFixture]
	public sealed class KingdomQuickstartShelterIngressTests
	{
		// The north heartbasin the quickstart founds on: rect (38,11)-(43,14) around the rite at
		// the founder's start cell. Every settled centre the heart can report while the basin is
		// the only work raised lies inside that band.
		private const int HeartBandNorthY = 11;
		private const int HeartBandSouthY = 14;

		[Test]
		public void EveryShelterLotBaresExactlyItsAuthoredDoorToLaneRoute()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			// The lots are Small, so the runtime resolves the tent row's Small binding; the larger
			// realizations of the same design belong to lots this authority never stakes.
			List<ArchitectureCorpusCase> rows = corpus.Cases
				.Where(item => item.Tier.BuildKey == KingdomQuickstartRules.ShelterBuildKey
					&& item.Binding.Size == ArchitectureLotSize.Small)
				.ToList();
			ClassicAssert.AreEqual(1, rows.Count,
				"the tent row is authored by exactly one Small tier");
			ClassicAssert.AreEqual(ArchitectureFrontage.Heart, rows[0].Binding.Frontage,
				"the tent row is posed by the heart frontage law, not by road evidence");
			// The reach the mask declares is the reserved road margin plus its lane endpoint.
			ClassicAssert.AreEqual(2, KingdomPlotRules.RoadMargin + 1);

			HashSet<string> declared = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < KingdomQuickstartRules.ShelterIngressCellCount; i++)
			{
				KingdomQuickstartRules.ShelterIngressCell(i, out int x, out int y);
				ClassicAssert.IsTrue(declared.Add(x + "," + y), "duplicate ingress cell " + x + "," + y);
			}
			HashSet<string> walked = new HashSet<string>(StringComparer.Ordinal);
			int lanes = 0;
			for (int i = 0; i < KingdomQuickstartRules.ShelterLotCount; i++)
				lanes += WalkOneLot(corpus, rows[0], i, walked);
			ClassicAssert.AreEqual(KingdomQuickstartRules.ShelterLotCount, lanes,
				"each lot carries exactly one public threshold");
			CollectionAssert.AreEquivalent(declared, walked,
				"the bared ingress cells are exactly the ones the stake walks");
			ClassicAssert.AreEqual(4, declared.Count);
		}

		private static int WalkOneLot(ArchitectureCorpus corpus, ArchitectureCorpusCase row,
			int Index, HashSet<string> Walked)
		{
			KingdomPlotRules.PlotRect lot = KingdomQuickstartRules.ShelterLot(Index);
			ArchitectureFacing facing = SettledFacing(lot);
			ArchitectureCompileRequest request =
				KingdomArchitectureCorpusFixture.Request(corpus, row, facing);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryCompile(request,
				out ArchitectureLayoutSnapshot snapshot, out string failure), failure);
			ClassicAssert.IsTrue(KingdomArchitectureRules.TryWorldDimensions(snapshot.Width,
				snapshot.Height, facing, out int width, out int height));
			ClassicAssert.AreEqual(lot.Width, width, "lot " + Index + " posed width");
			ClassicAssert.AreEqual(lot.Height, height, "lot " + Index + " posed height");

			KingdomQuickstartRules.ShelterThresholdCell(Index, out int doorX, out int doorY,
				out int stepX, out int stepY);
			int found = 0;
			for (int a = 0; a < snapshot.Anchors.Count; a++)
			{
				ArchitectureAnchor entrance = snapshot.Anchors[a];
				if (entrance.Key != "entrance:public" && !entrance.Key.StartsWith(
					"entrance:public@", StringComparison.Ordinal)) continue;
				found++;
				List<ArchitecturePoint> route = new List<ArchitecturePoint>();
				ClassicAssert.IsTrue(KingdomRoadRules.TryAuthoredLane(snapshot, lot, entrance,
					route, out int worldDoorX, out int worldDoorY, out int laneX, out int laneY),
					"lot " + Index + " has no exact authored DoorToLane route");
				ClassicAssert.AreEqual(doorX, worldDoorX, "lot " + Index + " threshold x");
				ClassicAssert.AreEqual(doorY, worldDoorY, "lot " + Index + " threshold y");
				// The whole route, and its lane endpoint, must stand on ground the camp bares.
				ClassicAssert.IsTrue(KingdomQuickstartRules.RequiresPreparedGround(worldDoorX,
					worldDoorY), "unprepared threshold at " + worldDoorX + "," + worldDoorY);
				for (int r = 0; r < route.Count; r++)
				{
					ArchitecturePoint point = route[r];
					ClassicAssert.IsTrue(KingdomQuickstartRules.RequiresPreparedGround(point.X,
						point.Y), "unprepared ingress at " + point.X + "," + point.Y);
					AddIfOutside(lot, point.X, point.Y, Walked);
					ClassicAssert.AreEqual(stepX * (r + 1), point.X - worldDoorX,
						"lot " + Index + " route step x at " + r);
					ClassicAssert.AreEqual(stepY * (r + 1), point.Y - worldDoorY,
						"lot " + Index + " route step y at " + r);
				}
				ClassicAssert.IsTrue(KingdomQuickstartRules.RequiresPreparedGround(laneX, laneY),
					"unprepared lane at " + laneX + "," + laneY);
				AddIfOutside(lot, laneX, laneY, Walked);
				ClassicAssert.AreEqual(stepX * (route.Count + 1), laneX - worldDoorX,
					"lot " + Index + " lane step x");
				ClassicAssert.AreEqual(stepY * (route.Count + 1), laneY - worldDoorY,
					"lot " + Index + " lane step y");
				ClassicAssert.AreEqual(KingdomPlotRules.RoadMargin, route.Count,
					"the threshold stands on the lot edge, so the route is margin only");
			}
			return found;
		}

		private static void AddIfOutside(KingdomPlotRules.PlotRect Lot, int X, int Y,
			HashSet<string> Walked)
		{
			bool inside = false;
			for (int i = 0; i < KingdomQuickstartRules.ShelterLotCount; i++)
				if (KingdomQuickstartRules.ShelterLot(i).Contains(X, Y)) inside = true;
			if (!inside) Walked.Add(X + "," + Y);
		}

		/// <summary>
		/// The pose KingdomArchitectureRuntime.TryHeartFacing gives a north/south lot, read across
		/// the whole band the founding heart's settled centre can stand in. A lot the band cannot
		/// settle on one answer would make the declared threshold a guess, and fails here.
		/// </summary>
		private static ArchitectureFacing SettledFacing(KingdomPlotRules.PlotRect Lot)
		{
			ArchitectureFacing north = HeartBandNorthY <= Lot.CenterY
				? ArchitectureFacing.North : ArchitectureFacing.South;
			ArchitectureFacing south = HeartBandSouthY <= Lot.CenterY
				? ArchitectureFacing.North : ArchitectureFacing.South;
			ClassicAssert.AreEqual(north, south,
				"the founding heart band does not settle this lot's pose");
			ClassicAssert.IsTrue(HeartBandNorthY <= KingdomQuickstartRules.StartCellY
				&& KingdomQuickstartRules.StartCellY <= HeartBandSouthY,
				"the rite ground stands inside the heart band");
			return north;
		}
	}
}
#endif
