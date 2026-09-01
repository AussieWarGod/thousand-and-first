#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomArchitectureEnclosureTests
	{
		[Test]
		public void ShippedCorpusCompilesEveryVariantInEveryFacing()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			Assert.AreEqual(89, corpus.Palettes.Count);
			Assert.AreEqual(333, corpus.Maps.Count);
			Assert.AreEqual(344, corpus.Cases.Count);
			Assert.IsTrue(corpus.Maps.ContainsKey("defense-watchhouse-l0"),
				"the large predecessor map is part of the authored garrison renovation");
			Assert.AreEqual(1, corpus.Cases.Count(item => item.Tier.BuildKey == "watchhouse"
				&& item.Tier.MapKey == "defense-watchhouse-l0"),
				"the garrison expansion adds one deliberate predecessor case, not a duplicate");
			int compiled = 0;
			for (int i = 0; i < corpus.Cases.Count; i++)
				foreach (ArchitectureFacing facing in Enum.GetValues(typeof(ArchitectureFacing)))
				{
					ArchitectureCompileRequest request =
						KingdomArchitectureCorpusFixture.Request(corpus, corpus.Cases[i], facing);
					Assert.IsTrue(KingdomArchitectureRules.TryCompile(request,
						out _, out string failure), request.Map.Key + ": " + failure);
					compiled++;
				}
			Assert.AreEqual(corpus.Cases.Count
				* Enum.GetValues(typeof(ArchitectureFacing)).Length, compiled);
		}

		[Test]
		public void EveryShippedPoseGivesEveryRoadEntranceAnExactLiveDoorToLaneRoute()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			HashSet<string> generatedMaps = XDocument.Load(Path.Combine(
				TestMain.RepositoryRoot, "Architecture",
				"KingdomArchitectures-LotRealizations.xml")).Root.Elements("map")
				.Select(item => (string)item.Attribute("Key")).ToHashSet(StringComparer.Ordinal);
			int poses = 0;
			int routes = 0;
			int publicRoutes = 0;
			int serviceRoutes = 0;
			int interiorEntrances = 0;
			int roadCases = 0;
			int roadPoses = 0;
			int generatedCases = 0;
			int generatedPoses = 0;
			for (int i = 0; i < corpus.Cases.Count; i++)
			{
				ArchitectureCompileRequest canonical =
					KingdomArchitectureCorpusFixture.Request(corpus, corpus.Cases[i],
						ArchitectureFacing.North);
				bool road = corpus.Cases[i].Binding.Frontage == ArchitectureFrontage.Road;
				bool generated = generatedMaps.Contains(canonical.Map.Key);
				if (road) roadCases++;
				if (generated) generatedCases++;
				foreach (ArchitectureFacing facing in Enum.GetValues(typeof(ArchitectureFacing)))
				{
					ArchitectureCompileRequest request =
						KingdomArchitectureCorpusFixture.Request(corpus, corpus.Cases[i], facing);
					Assert.IsTrue(KingdomArchitectureRules.TryCompile(request,
						out ArchitectureLayoutSnapshot snapshot, out string failure),
						request.Map.Key + " " + facing + ": " + failure);
					Assert.IsTrue(KingdomArchitectureRules.TryWorldDimensions(snapshot.Width,
						snapshot.Height, facing, out int width, out int height));
					KingdomPlotRules.PlotRect rect = new KingdomPlotRules.PlotRect(
						20, 20, 19 + width, 19 + height);
					int poseRoutes = 0;
					for (int a = 0; a < snapshot.Anchors.Count; a++)
					{
						ArchitectureAnchor entrance = snapshot.Anchors[a];
						if (!IsRoadEntrance(entrance.Key)) continue;
						List<ArchitecturePoint> exact = new List<ArchitecturePoint>();
						Assert.IsTrue(KingdomRoadRules.TryAuthoredLane(snapshot, rect, entrance,
							exact, out int doorX, out int doorY, out int laneX, out int laneY),
							request.Map.Key + " " + facing + " " + entrance.Key);
						Assert.IsNotEmpty(exact, "every authored lane includes its reserved margin");
						Assert.AreEqual(1, Math.Abs(exact[exact.Count - 1].X - laneX)
							+ Math.Abs(exact[exact.Count - 1].Y - laneY),
							"lane must be cardinally beyond its reserved margin");
						HashSet<int> claimed = ClaimedWorldCells(snapshot, rect);
						Assert.IsTrue(KingdomRoadRules.TryExactTrace(
							delegate(int x, int y)
							{
								return !claimed.Contains(KingdomRoadRules.Pack(x, y, 80));
							}, 80, 50, doorX, doorY, laneX, laneY,
							KingdomRoadRules.MaxRouteCells, exact, new List<int>()),
							"exact route crossed claimed fabric: " + request.Map.Key + " " + facing);
						if (entrance.X > 0 && entrance.X < snapshot.Width - 1
							&& entrance.Y > 0 && entrance.Y < snapshot.Height - 1)
							interiorEntrances++;
						if (entrance.Key == "entrance:service" || entrance.Key.StartsWith(
							"entrance:service@", StringComparison.Ordinal)) serviceRoutes++;
						else publicRoutes++;
						poseRoutes++;
						routes++;
					}
					Assert.Greater(poseRoutes, 0, request.Map.Key + " " + facing);
					if (road) roadPoses++;
					if (generated) generatedPoses++;
					poses++;
				}
			}
			int facingCount = Enum.GetValues(typeof(ArchitectureFacing)).Length;
			Assert.AreEqual(corpus.Cases.Count * facingCount, poses);
			Assert.AreEqual(publicRoutes + serviceRoutes, routes);
			Assert.GreaterOrEqual(publicRoutes, poses,
				"every shipped pose retains at least one public road ingress");
			Assert.Greater(serviceRoutes, 0,
				"service ingress must exercise the same exact route law");
			Assert.Greater(interiorEntrances, 0,
				"claim-interior entrances exercise the egress search, not only edge doors");
			Assert.AreEqual(roadCases * facingCount, roadPoses);
			Assert.AreEqual(generatedCases * facingCount, generatedPoses);
		}

		private static bool IsRoadEntrance(string key)
		{
			return key == "entrance:public" || key == "entrance:service"
				|| (key != null && (key.StartsWith("entrance:public@",
					StringComparison.Ordinal) || key.StartsWith("entrance:service@",
					StringComparison.Ordinal)));
		}

		[Test]
		public void ShippedFirstHeartHasOneRitePoseAndPairedWalkableEntrancesToBasin()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			ArchitectureCorpusCase heart = corpus.Cases.Single(item =>
				item.Tier.BuildKey == "heartbasin" && item.Variant.Key == "fallback");
			int riteX = 40;
			int riteY = 12;
			Assert.IsTrue(KingdomPlotRules.TrySurveyedHeart(riteX, riteY, 80, 25,
				out KingdomPlotRules.PlotRect survey));
			Assert.IsTrue(KingdomPlotRules.TryHeartRect(survey, riteX, riteY,
				KingdomPlotRules.HeartSizeForRung(1), out KingdomPlotRules.PlotRect rect));
			Assert.AreEqual(6, rect.Width);
			Assert.AreEqual(4, rect.Height);
			Assert.AreEqual(38, rect.X1);
			Assert.AreEqual(11, rect.Y1);
			Assert.AreEqual(43, rect.X2);
			Assert.AreEqual(14, rect.Y2);
			int matchingPoses = 0;
			foreach (ArchitectureFacing facing in Enum.GetValues(typeof(ArchitectureFacing)))
			{
				Assert.IsTrue(KingdomArchitectureRules.TryCompile(
					KingdomArchitectureCorpusFixture.Request(corpus, heart, facing),
					out ArchitectureLayoutSnapshot snapshot, out string failure), failure);
				Assert.AreEqual(1, snapshot.Placements.Count(item => item.ExistingAuthority));
				ArchitecturePlacement basin = snapshot.Placements.Single(item => item.ExistingAuthority);
				ArchitectureCellState basinCell = Cell(snapshot, basin.X, basin.Y);
				Assert.AreEqual(ArchitecturePassability.Walkable, basinCell.Passability);
				List<ArchitectureAnchor> entrances = snapshot.Anchors.Where(item =>
					item.Key == "entrance:public"
						|| item.Key.StartsWith("entrance:public@", StringComparison.Ordinal)).ToList();
				Assert.AreEqual(2, entrances.Count, facing.ToString());
				foreach (ArchitectureAnchor entrance in entrances)
					Assert.IsTrue(ReachableWalkCell(snapshot, entrance.X, entrance.Y,
						basin.X, basin.Y), facing + " " + entrance.Key);
				Assert.AreEqual(1, Math.Abs(snapshot.MainX - basin.X)
					+ Math.Abs(snapshot.MainY - basin.Y));
				Assert.IsTrue(KingdomArchitectureRules.TryWorldDimensions(snapshot.Width,
					snapshot.Height, facing, out int width, out int height));
				if (width != rect.Width || height != rect.Height) continue;
				Assert.IsTrue(KingdomArchitectureRules.TryToWorld(rect.X1, rect.Y1,
					snapshot.Width, snapshot.Height, facing, basin.X, basin.Y,
					out int basinX, out int basinY));
				if (basinX == riteX && basinY == riteY) matchingPoses++;
			}
			Assert.AreEqual(1, matchingPoses);
		}

		private static bool ReachableWalkCell(ArchitectureLayoutSnapshot snapshot,
			int startX, int startY, int targetX, int targetY)
		{
			Queue<int> pending = new Queue<int>();
			HashSet<int> seen = new HashSet<int>();
			pending.Enqueue(startY * snapshot.Width + startX);
			while (pending.Count > 0)
			{
				int packed = pending.Dequeue();
				if (!seen.Add(packed)) continue;
				int x = packed % snapshot.Width;
				int y = packed / snapshot.Width;
				if (x == targetX && y == targetY) return true;
				int[] dx = new int[] { 0, 1, 0, -1 };
				int[] dy = new int[] { -1, 0, 1, 0 };
				for (int direction = 0; direction < 4; direction++)
				{
					int nextX = x + dx[direction];
					int nextY = y + dy[direction];
					ArchitectureCellState cell = Cell(snapshot, nextX, nextY);
					if (cell != null && cell.Passability == ArchitecturePassability.Walkable)
						pending.Enqueue(nextY * snapshot.Width + nextX);
				}
			}
			return false;
		}

		private static HashSet<int> ClaimedWorldCells(ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect)
		{
			HashSet<int> result = new HashSet<int>();
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = Snapshot.Cells[i];
				if (!KingdomArchitectureRules.IsClaimed(cell.Claim)) continue;
				Assert.IsTrue(KingdomArchitectureRules.TryToWorld(Rect.X1, Rect.Y1,
					Snapshot.Width, Snapshot.Height, Snapshot.Facing, cell.X, cell.Y,
					out int x, out int y));
				result.Add(KingdomRoadRules.Pack(x, y, 80));
			}
			return result;
		}

		[Test]
		public void EveryCataloguePlotUpgradeBuildsALawfulRuntimeDeltaForEverySharedVariantAndFacing()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			List<KeyValuePair<string, string>> edges = CataloguePlotUpgradeEdges();
			edges = edges.OrderBy(edge => LowestTierLevel(corpus, edge.Key))
				.ThenBy(edge => edge.Key, StringComparer.Ordinal)
				.ThenBy(edge => edge.Value, StringComparer.Ordinal).ToList();
			HashSet<string> coveredEdges = new HashSet<string>(StringComparer.Ordinal);
			int transitions = 0;
			for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
			{
				KeyValuePair<string, string> edge = edges[edgeIndex];
				List<ArchitectureCorpusCase> beforeCases = CasesFor(corpus, edge.Key);
				List<ArchitectureCorpusCase> afterCases = CasesFor(corpus, edge.Value);
				Assert.IsNotEmpty(beforeCases, edge.Key + " has no authored architecture tier");
				Assert.IsNotEmpty(afterCases, edge.Value + " has no authored architecture tier");
				int edgeTransitions = 0;
				for (int beforeIndex = 0; beforeIndex < beforeCases.Count; beforeIndex++)
				{
					ArchitectureCorpusCase beforeCase = beforeCases[beforeIndex];
					List<ArchitectureCorpusCase> shared = afterCases.Where(afterCase =>
						CompatibleUpgradeBinding(beforeCase, afterCase)
						&& afterCase.Variant.Key == beforeCase.Variant.Key).ToList();
					if (shared.Count == 0) continue;
					Assert.AreEqual(1, shared.Count, edge.Key + "->" + edge.Value + " "
						+ beforeCase.PlanKey + "/" + beforeCase.Binding.Key + "/"
						+ beforeCase.Variant.Key + " has ambiguous successor architecture");
					ArchitectureCorpusCase afterCase = shared[0];
					Assert.Greater(afterCase.Tier.Level, beforeCase.Tier.Level,
						edge.Key + "->" + edge.Value + " is not in production level order");
					foreach (ArchitectureFacing facing in Enum.GetValues(typeof(ArchitectureFacing)))
					{
						Assert.IsTrue(KingdomArchitectureRules.TryCompile(
							KingdomArchitectureCorpusFixture.Request(corpus, beforeCase, facing),
							out ArchitectureLayoutSnapshot before, out string failure),
							beforeCase.Tier.MapKey + " " + facing + ": " + failure);
						Assert.IsTrue(KingdomArchitectureRules.TryCompile(
							KingdomArchitectureCorpusFixture.Request(corpus, afterCase, facing),
							out ArchitectureLayoutSnapshot after, out failure),
							afterCase.Tier.MapKey + " " + facing + ": " + failure);
						Assert.IsTrue(KingdomArchitectureRules.TryBuildDelta(before, after,
							out _, out failure), edge.Key + "->" + edge.Value + " "
								+ beforeCase.PlanKey + "/" + beforeCase.Binding.Key + "/"
								+ beforeCase.Variant.Key + " " + facing + ": " + failure);
						transitions++;
						edgeTransitions++;
					}
				}
				Assert.Greater(edgeTransitions, 0,
					edge.Key + "->" + edge.Value + " has no shared reachable architecture variant");
				coveredEdges.Add(edge.Key + "->" + edge.Value);
			}
			Assert.AreEqual(edges.Count, coveredEdges.Count,
				"every catalogue plot upgrade edge must be exercised");
			Assert.GreaterOrEqual(transitions, edges.Count * 4,
				"every covered edge must compile in all four facings");
		}

		[Test]
		public void CorpusMutationCreatesNamedEnclosureRefusal()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			ArchitectureCorpusCase item = corpus.Cases.Find(value =>
				value.Tier.MapKey == "housing-hut-s0");
			Assert.IsNotNull(item);
			ArchitectureCompileRequest request = KingdomArchitectureCorpusFixture.Request(
				corpus, item, ArchitectureFacing.North);
			Assert.IsTrue(KingdomArchitectureRules.TryCompile(request,
				out _, out string failure), failure);
			request.Map.Rows[1] = ".#b@i.";
			Assert.IsFalse(KingdomArchitectureRules.TryCompile(request, out _, out failure));
			StringAssert.Contains("bare leak", failure);
			StringAssert.Contains("4,1", failure);
		}

		[Test]
		public void CurrentSnapshotRefusesAnEntranceIntoAnEnclosedUnclaimedPocket()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			ArchitectureCorpusCase item = corpus.Cases.Find(value =>
				value.Tier.MapKey == "housing-hut-s0");
			Assert.IsNotNull(item);
			ArchitectureCompileRequest request = KingdomArchitectureCorpusFixture.Request(
				corpus, item, ArchitectureFacing.North);
			request.Map.Rows[2] = ".#+.#.";
			Assert.IsFalse(KingdomArchitectureRules.TryCompile(request, out _, out string failure));
			StringAssert.Contains("no bounded unclaimed walk to the lot exterior", failure);
		}

		[Test]
		public void PureLawDistinguishesBareLeakBarrierOpeningAndRooflessPlan()
		{
			ArchitectureLayoutSnapshot snapshot = BoundarySnapshot();
			Assert.IsFalse(KingdomArchitectureRules.TryValidateEnclosure(snapshot,
				out string failure));
			StringAssert.Contains("bare leak", failure);

			snapshot.Placements.Add(new ArchitecturePlacement
				{ Layer = ArchitectureLayer.Structure, X = 1, Y = 1 });
			Assert.IsTrue(KingdomArchitectureRules.TryValidateEnclosure(snapshot, out failure), failure);

			snapshot.Placements.RemoveAt(snapshot.Placements.Count - 1);
			snapshot.Anchors.Add(new ArchitectureAnchor
				{ Key = "threshold:test", X = 1, Y = 1 });
			Assert.IsTrue(KingdomArchitectureRules.TryValidateEnclosure(snapshot, out failure), failure);

			snapshot.Anchors.Clear();
			Cell(snapshot, 1, 1).Cover = ArchitectureCover.Open;
			Assert.IsTrue(KingdomArchitectureRules.TryValidateEnclosure(snapshot, out failure), failure);

			Cell(snapshot, 1, 1).Cover = ArchitectureCover.Walled;
			Cell(snapshot, 2, 1).Claim = ArchitectureClaim.Building;
			Cell(snapshot, 2, 1).Cover = ArchitectureCover.Soft;
			snapshot.Placements.Add(new ArchitecturePlacement
				{ Layer = ArchitectureLayer.Structure, X = 2, Y = 1 });
			Assert.IsTrue(KingdomArchitectureRules.TryValidateEnclosure(snapshot, out failure), failure);
		}

		[Test]
		public void PureLawFailsClosedOnMalformedInput()
		{
			Assert.IsFalse(KingdomArchitectureRules.TryValidateEnclosure(null,
				out string failure));
			StringAssert.Contains("malformed", failure);
			ArchitectureLayoutSnapshot snapshot = BoundarySnapshot();
			snapshot.Cells.RemoveAt(0);
			Assert.IsFalse(KingdomArchitectureRules.TryValidateEnclosure(snapshot, out failure));
			StringAssert.Contains("incomplete", failure);
		}

		[Test]
		public void RemovingEdgeBarrierCreatesNamedExteriorLeak()
		{
			ArchitectureLayoutSnapshot snapshot = EdgeSnapshot();
			Assert.IsTrue(KingdomArchitectureRules.TryValidateEnclosure(snapshot,
				out string failure), failure);
			snapshot.Placements.RemoveAt(snapshot.Placements.Count - 1);
			Assert.IsFalse(KingdomArchitectureRules.TryValidateEnclosure(snapshot, out failure));
			StringAssert.Contains("bare leak at 0,1 toward -1,1", failure);
		}

		private static ArchitectureLayoutSnapshot BoundarySnapshot()
		{
			ArchitectureLayoutSnapshot result = new ArchitectureLayoutSnapshot
				{ Width = 3, Height = 3 };
			for (int y = 0; y < 3; y++)
				for (int x = 0; x < 3; x++)
					result.Cells.Add(new ArchitectureCellState
						{
							X = x, Y = y, Claim = ArchitectureClaim.Building,
							Passability = ArchitecturePassability.Walkable,
							Cover = ArchitectureCover.Open
						});
			Cell(result, 1, 1).Cover = ArchitectureCover.Walled;
			Cell(result, 2, 1).Claim = ArchitectureClaim.Unclaimed;
			result.Placements.Add(new ArchitecturePlacement
				{ Layer = ArchitectureLayer.Structure, X = 1, Y = 0 });
			result.Placements.Add(new ArchitecturePlacement
				{ Layer = ArchitectureLayer.Structure, X = 0, Y = 1 });
			result.Placements.Add(new ArchitecturePlacement
				{ Layer = ArchitectureLayer.Structure, X = 1, Y = 2 });
			return result;
		}

		private static ArchitectureLayoutSnapshot EdgeSnapshot()
		{
			ArchitectureLayoutSnapshot result = new ArchitectureLayoutSnapshot
				{ Width = 3, Height = 3 };
			for (int y = 0; y < 3; y++)
				for (int x = 0; x < 3; x++)
					result.Cells.Add(new ArchitectureCellState
						{
							X = x, Y = y, Claim = ArchitectureClaim.Building,
							Passability = ArchitecturePassability.Walkable,
							Cover = ArchitectureCover.Open
						});
			Cell(result, 0, 1).Cover = ArchitectureCover.Walled;
			result.Placements.Add(new ArchitecturePlacement
				{ Layer = ArchitectureLayer.Structure, X = 0, Y = 0 });
			result.Placements.Add(new ArchitecturePlacement
				{ Layer = ArchitectureLayer.Structure, X = 1, Y = 1 });
			result.Placements.Add(new ArchitecturePlacement
				{ Layer = ArchitectureLayer.Structure, X = 0, Y = 2 });
			result.Placements.Add(new ArchitecturePlacement
				{ Layer = ArchitectureLayer.Structure, X = 0, Y = 1 });
			return result;
		}

		private static ArchitectureCellState Cell(ArchitectureLayoutSnapshot snapshot,
			int x, int y)
		{
			return snapshot.Cells.Find(value => value.X == x && value.Y == y);
		}

		private static List<KeyValuePair<string, string>> CataloguePlotUpgradeEdges()
		{
			XDocument catalogue = XDocument.Load(Path.Combine(TestMain.RepositoryRoot,
				"RuntimeData", "KingdomBuildings.xml"));
			List<KeyValuePair<string, string>> result =
				new List<KeyValuePair<string, string>>();
			foreach (XElement building in catalogue.Root.Elements("building"))
			{
				string plot = (string)building.Attribute("Plot");
				string from = (string)building.Attribute("Key");
				string upgrades = (string)building.Attribute("UpgradesTo");
				if (string.IsNullOrEmpty(plot) || string.IsNullOrEmpty(from)
					|| string.IsNullOrEmpty(upgrades)) continue;
				string[] successors = upgrades.Split(new char[] { ',' },
					StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < successors.Length; i++)
					result.Add(new KeyValuePair<string, string>(from, successors[i].Trim()));
			}
			return result;
		}

		private static List<ArchitectureCorpusCase> CasesFor(ArchitectureCorpus corpus,
			string buildKey)
		{
			return corpus.Cases.Where(item => item.Tier.BuildKey == buildKey)
				.OrderBy(item => item.Tier.Level)
				.ThenBy(item => item.PlanKey, StringComparer.Ordinal)
				.ThenBy(item => item.Binding.Key, StringComparer.Ordinal)
				.ThenBy(item => item.Variant.Key, StringComparer.Ordinal).ToList();
		}

		private static int LowestTierLevel(ArchitectureCorpus corpus, string buildKey)
		{
			List<ArchitectureCorpusCase> matches = CasesFor(corpus, buildKey);
			return matches.Count == 0 ? int.MaxValue : matches[0].Tier.Level;
		}

		private static bool CompatibleUpgradeBinding(ArchitectureCorpusCase before,
			ArchitectureCorpusCase after)
		{
			if (before.PlanKey != after.PlanKey) return false;
			if (ReferenceEquals(before.Binding, after.Binding)) return true;
			return before.PlanKey == "civic-heart"
				&& before.Binding.TypeKey == after.Binding.TypeKey;
		}
	}
}
#endif
