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
				"Experience/KingdomGuestbook.cs", "Experience/KingdomGuestLifecycle.cs",
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
				string source = TestMain.ReadRepositoryText(files[i]);
				for (int j = 0; j < forbidden.Length; j++)
					StringAssert.DoesNotContain(forbidden[j], source,
						files[i] + " uses global semantic selection");
			}
		}

		[Test]
		public void CommissionFreezesCounterSelectedGroundIntoJobBeforeFunding()
		{
			string source = TestMain.ReadRepositoryText("Growth/KingdomCommission.cs");
			int job = source.IndexOf("job = KingdomConstruction.NewJob(System, zone,",
				global::System.StringComparison.Ordinal);
			int choose = source.IndexOf(
				"cell = FindBuildCell(zone, System, entry, job.Id", job,
				global::System.StringComparison.Ordinal);
			int freeze = source.IndexOf("job.X = cell.X", choose,
				global::System.StringComparison.Ordinal);
			int fund = source.IndexOf("KingdomConstruction.TryFundNew(job", freeze,
				global::System.StringComparison.Ordinal);
			Assert.That(job, Is.GreaterThanOrEqualTo(0));
			Assert.That(choose, Is.GreaterThan(job));
			Assert.That(freeze, Is.GreaterThan(choose));
			Assert.That(fund, Is.GreaterThan(freeze));
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
			string source = TestMain.ReadRepositoryText("Core/KingdomSemanticSelection.cs");
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
			string source = TestMain.ReadRepositoryText("Growth/KingdomPlot2.cs");
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
			string source = TestMain.ReadRepositoryText(
				"Core/KingdomArchivedSettlementCodec.cs");
			StringAssert.Contains("public const int SemanticSelectionVersion = 11", source);
			StringAssert.Contains("version < SemanticSelectionVersion", source);
			StringAssert.Contains("StageHistoricalSemanticPlan(Value)", source);
			StringAssert.Contains("Type == typeof(KingdomGrowthArrivalCandidate)", source);
			StringAssert.Contains("string.Equals(Name, \"PlannedName\"", source);
		}

		[Test]
		public void GuestLifecyclePersistsSemanticVersionStreamAndTitleBeforeProjection()
		{
			string source = TestMain.ReadRepositoryText("Experience/KingdomGuestLifecycle.cs");
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
	}
}
