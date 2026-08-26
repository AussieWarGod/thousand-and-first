using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomSemanticRuntimeSourceTests
	{
		[Test]
		public void SemanticPersonAndFurnishingPathsNeverUseGlobalSelection()
		{
			string[] files =
			{
				"Growth/KingdomGrowth.cs", "Experience/KingdomLocus.cs",
				"Experience/KingdomGuestbook.cs",
				"Core/KingdomCreed.cs", "Growth/KingdomPlot2.cs",
				"Growth/KingdomCommission.cs", "Growth/KingdomSalvage.cs"
			};
			string[] forbidden =
			{
				"Stat.Random", "GetRandomElement", "PopulationManager.Roll",
				"PopulationManager.Generate", "NameMaker."
			};
			for (int i = 0; i < files.Length; i++)
			{
				string source = files[i] == "Growth/KingdomCommission.cs"
					? KingdomCommissionLogicalSource.Read()
					: files[i] == "Growth/KingdomPlot2.cs"
						? KingdomPlot2LogicalSource.Read()
						: TestMain.ReadRepositoryText(files[i]);
				for (int j = 0; j < forbidden.Length; j++)
					StringAssert.DoesNotContain(forbidden[j], source,
						files[i] + " uses global semantic selection");
			}
			string guestLifecycle = KingdomGuestLifecycleLogicalSource.Read();
			for (int i = 0; i < forbidden.Length; i++)
				StringAssert.DoesNotContain(forbidden[i], guestLifecycle,
					"KingdomGuestLifecycle logical family uses global semantic selection");
		}

		[Test]
		public void CommissionFreezesCounterSelectedGroundIntoJobBeforeFunding()
		{
			string source = KingdomCommissionLogicalSource.Read();
			int job = source.IndexOf("job = KingdomConstruction.NewJob(System, zone,",
				global::System.StringComparison.Ordinal);
			int choose = source.IndexOf(
				"cell = FindBuildCell(zone, System, entry, job.Id", job,
				global::System.StringComparison.Ordinal);
			int freeze = source.IndexOf("job.X = cell.X", choose,
				global::System.StringComparison.Ordinal);
			int fund = source.IndexOf("KingdomConstruction.TryFundNew(job", freeze,
				global::System.StringComparison.Ordinal);
			int project = source.IndexOf("ProjectScaffold(System, zone", fund,
				global::System.StringComparison.Ordinal);
			int commit = source.IndexOf("KingdomGovernanceScope.Commit(\"commission building\")",
				project, global::System.StringComparison.Ordinal);
			Assert.That(job, Is.GreaterThanOrEqualTo(0));
			Assert.That(choose, Is.GreaterThan(job));
			Assert.That(freeze, Is.GreaterThan(choose));
			Assert.That(fund, Is.GreaterThan(freeze));
			Assert.That(project, Is.GreaterThan(fund));
			Assert.That(commit, Is.GreaterThan(project));
			Assert.AreEqual(4, source.Split(new[] { "public static bool Commission(" },
				global::System.StringSplitOptions.None).Length - 1);
			StringAssert.Contains("TryOwnerStreamId(\"commission-placement\"", source);
			StringAssert.Contains("KingdomSemanticSelectionRules.TryProbeStart", source);
		}

		[Test]
		public void SalvageFreezesStableSignatoryBeforeAnyCertificationMutation()
		{
			string source = TestMain.ReadRepositoryText("Growth/KingdomSalvage.cs");
			int signatory = source.IndexOf("string settler = FrozenSignatory(System)",
				global::System.StringComparison.Ordinal);
			int consume = source.IndexOf("survey.Consume(waterCost)", signatory,
				global::System.StringComparison.Ordinal);
			Assert.That(signatory, Is.GreaterThanOrEqualTo(0));
			Assert.That(consume, Is.GreaterThan(signatory));
			StringAssert.Contains("string.CompareOrdinal(name, chosen)", source);
			StringAssert.DoesNotContain("GetRandomElement", source);
		}

		[Test]
		public void GrowthCandidateFreezesAllPersonPayloadBeforeCreation()
		{
			string growth = TestMain.ReadRepositoryText("Growth/KingdomGrowth.cs");
			int prepare = growth.IndexOf("TryPrepareGrowthArrival(system, zone, sequence",
				global::System.StringComparison.Ordinal);
			int publish = growth.IndexOf("TryPublishGrowthArrivalCandidate(", prepare,
				global::System.StringComparison.Ordinal);
			int create = growth.IndexOf("GameObject.Create(candidate.Blueprint)", publish,
				global::System.StringComparison.Ordinal);
			Assert.That(prepare, Is.GreaterThanOrEqualTo(0));
			Assert.That(publish, Is.GreaterThan(prepare));
			Assert.That(create, Is.GreaterThan(publish));
			StringAssert.Contains("person.Origin", growth.Substring(prepare, publish - prepare));
			StringAssert.Contains("person.Name", growth.Substring(prepare, publish - prepare));
			StringAssert.Contains("person.Arrived", growth.Substring(prepare, publish - prepare));
			StringAssert.Contains("person.X", growth.Substring(prepare, publish - prepare));
		}

		[Test]
		public void MergedCatalogueAdapterRejectsDynamicRowsAndNeverGenerates()
		{
			string source = KingdomSemanticSelectionLogicalSource.Read();
			StringAssert.Contains("PopulationManager.TryResolvePopulation", source);
			StringAssert.Contains("population.Items[i] as PopulationObject", source);
			StringAssert.Contains("blueprint.StartsWith(\"$CALL\"", source);
			StringAssert.Contains("table.StartsWith(\"Dynamic\"", source);
			StringAssert.DoesNotContain("PopulationManager.Generate", source);
			StringAssert.DoesNotContain("PopulationManager.Roll", source);
		}

		[Test]
		public void FurnishingPlanIsFrozenBeforeAnyBlueprintCreation()
		{
			string source = KingdomPlot2LogicalSource.Read();
			int freeze = source.IndexOf("TryFreezeFurnishPlan(semanticSystem",
				global::System.StringComparison.Ordinal);
			int publish = source.IndexOf("KingdomPhysicalPhase.FurnishingPending", freeze,
				global::System.StringComparison.Ordinal);
			int create = source.IndexOf("GameObject.Create(row.Blueprint)", publish,
				global::System.StringComparison.Ordinal);
			Assert.That(freeze, Is.GreaterThanOrEqualTo(0));
			Assert.That(publish, Is.GreaterThan(freeze));
			Assert.That(create, Is.GreaterThan(publish));
			StringAssert.Contains("TryOwnerStreamId(", source);
			StringAssert.Contains("\"furnish\", Job.Id", source);
			StringAssert.Contains("LegacyFurnishPlanProperty", source);
		}

		[Test]
		public void ArchiveVersionStagesHistoricalSemanticCandidateFields()
		{
			string source = KingdomArchivedSettlementCodecLogicalSource.Read();
			StringAssert.Contains("public const int SemanticSelectionVersion = 11", source);
			StringAssert.Contains("version < SemanticSelectionVersion", source);
			StringAssert.Contains("StageHistoricalSemanticPlan(Value)", source);
			StringAssert.Contains("Type == typeof(KingdomGrowthArrivalCandidate)", source);
			StringAssert.Contains("string.Equals(Name, \"PlannedName\"", source);
		}

		[Test]
		public void GuestLifecyclePersistsSemanticVersionStreamAndTitleBeforeProjection()
		{
			string source = KingdomGuestLifecycleLogicalSource.Read();
			int prepare = source.IndexOf("PrepareOperation(book, lane,",
				global::System.StringComparison.Ordinal);
			int stream = source.IndexOf("op.Faction = semanticPlan", prepare,
				global::System.StringComparison.Ordinal);
			int projection = source.IndexOf("PrepareProjection(book, op", stream,
				global::System.StringComparison.Ordinal);
			Assert.That(stream, Is.GreaterThan(prepare));
			Assert.That(projection, Is.GreaterThan(stream));
			StringAssert.Contains("op.DisplayFaction = semanticPlan?.Title", source);
		}

		[Test]
		public void GuestLifecycleLogicalAuthorityKeepsNestedIdentityAndMutationOrder()
		{
			string source = KingdomGuestLifecycleLogicalSource.Read();
			Assert.AreEqual(6, Count(source,
				"internal static partial class KingdomGuestLifecycle"));
			Assert.AreEqual(1, Count(source,
				"private sealed class GuestWorld : IKingdomLifecycleTrustedWorld"));
			Assert.AreEqual(1, Count(source, "private sealed class ScheduleReference"));
			Assert.AreEqual(1, Count(source,
				"private sealed class Observation : IKingdomLifecycleTrustedObservation"));
			StringAssert.Contains(
				"internal const string MarkerProperty = \"r_TAF_GuestLifecycleMarker\";", source);
			StringAssert.Contains(
				"internal const string OperationProperty = \"r_TAF_GuestLifecycleOperation\";", source);
			AssertOrdered(source,
				"internal static KingdomLifecycleOperation Open(",
				"internal static bool TryPrepareSpawnPlan(",
				"internal static bool ObserveOption(",
				"internal static bool PublishPassages(",
				"internal static bool PublishSpawn(",
				"internal static bool PublishDeparture(",
				"private static bool PublishRemoval(",
				"internal static bool PublishLodge(",
				"internal static bool Drive(",
				"private static bool SettlePhase(",
				"private static bool SettleProjection(",
				"private static bool SettleWater(",
				"private static bool SettleRemoval(",
				"private static bool SettleDomain(",
				"private static bool SettleSinks(",
				"private static bool SettleSchedule(",
				"private sealed class GuestWorld",
				"private static string PlainObjectName(",
				"private sealed class ScheduleReference",
				"private sealed class Observation");

			int projection = source.IndexOf("public object InvokeLifecycleProjection(",
				global::System.StringComparison.Ordinal);
			Assert.That(projection, Is.GreaterThanOrEqualTo(0));
			AssertOrdered(source.Substring(projection),
				"KingdomLocus.CreateLifecycleGuest(Operation, projection)",
				"Cell cell = Zone.GetCell(projection.X, projection.Y);",
				"body.ID = projection.ObjectId;",
				"body.SetStringProperty(MarkerProperty, projection.Marker);",
				"body.SetStringProperty(OperationProperty, Operation.Id);",
				"accepted = cell.AddObject(body);",
				"KingdomSurvey.ObserveAddResultInActive(Zone, body, accepted);",
				"body.MakeActive();");

			int removal = source.IndexOf("public object InvokeLifecycleRemoval(",
				global::System.StringComparison.Ordinal);
			Assert.That(removal, Is.GreaterThan(projection));
			AssertOrdered(source.Substring(removal),
				"removed = body.Obliterate();",
				"KingdomSurvey.ObserveCurrentTopologyInActive(Zone, body);",
				"if (!removed || GameObject.Validate(body)) return null;",
				"Tombstone = body;");
			AssertOrdered(source,
				"private readonly ScheduleReference Schedule = new ScheduleReference();",
				"private List<IKingdomLifecycleTrustedObservation> Cached;",
				"private GameObject Tombstone;");
			AssertOrdered(source,
				"internal long Value;", "internal long Revision;",
				"internal string LastOperationId;");
		}

		private static void AssertOrdered(string source, params string[] terms)
		{
			int at = -1;
			for (int i = 0; i < terms.Length; i++)
			{
				int next = source.IndexOf(terms[i], at + 1,
					global::System.StringComparison.Ordinal);
				Assert.That(next, Is.GreaterThan(at), terms[i]);
				at = next;
			}
		}

		private static int Count(string source, string term)
		{
			int count = 0;
			int at = 0;
			while ((at = source.IndexOf(term, at,
				global::System.StringComparison.Ordinal)) >= 0)
			{
				count++;
				at += term.Length;
			}
			return count;
		}
	}
}
