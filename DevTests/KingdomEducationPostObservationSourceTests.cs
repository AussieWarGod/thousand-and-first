#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomEducationPostObservationSourceTests
	{
		private static string Read(string Path) => TestMain.ReadRepositoryText(Path);

		private static string Slice(string Source, string Start, string End)
		{
			int at = Source.IndexOf(Start, StringComparison.Ordinal);
			Assert.That(at, Is.GreaterThanOrEqualTo(0), Start);
			int until = Source.IndexOf(End, at + Start.Length, StringComparison.Ordinal);
			Assert.That(until, Is.GreaterThan(at), End);
			return Source.Substring(at, until - at);
		}

		private static void Ordered(string Source, params string[] Terms)
		{
			int prior = -1;
			for (int i = 0; i < Terms.Length; i++)
			{
				int at = Source.IndexOf(Terms[i], prior + 1, StringComparison.Ordinal);
				Assert.That(at, Is.GreaterThan(prior), Terms[i]); prior = at;
			}
		}

		private static int Count(string Source, string Term)
		{
			int count = 0, at = 0;
			while ((at = Source.IndexOf(Term, at, StringComparison.Ordinal)) >= 0)
			{ count++; at += Term.Length; }
			return count;
		}

		[Test]
		public void SuccessionRequiresSchoolingAndOneExactCurrentWorkRow()
		{
			string heirs = Read("Experience/KingdomSuccession.HeirsAndNews.cs");
			string education = Slice(heirs, "private static bool EducationPost(",
				"private static void JudgeActualNews(");
			StringAssert.Contains("SchoolingHeld && EducationPost(System, state, row.JobWorkId",
				heirs);
			StringAssert.Contains("work.WorkId != WorkId", education);
			StringAssert.Contains("matches == 1", education);
			StringAssert.Contains("KingdomEducationPostObservationRuntime.Proves", education);
			foreach (string forbidden in new[] { "KingdomData", "BenchCategory", ".Category",
				"entry.Provides", "TryGetBuilding" }) StringAssert.DoesNotContain(forbidden, education);
			StringAssert.DoesNotContain("BenchCategory",
				Read("Growth/KingdomResearch.Advance.cs"));
			string grooming = Read("Experience/KingdomGroomingRules.cs");
			StringAssert.DoesNotContain("KnowledgePost", grooming);
			StringAssert.DoesNotContain("authored knowledge work", grooming);
			foreach (string presentation in new[] { "Experience/KingdomSuccession.Grooming.cs",
				"Core/KingdomCharterPart.Succession.cs" })
				StringAssert.DoesNotContain("knowledge post", Read(presentation), presentation);
		}

		[Test]
		public void ActiveZoneUsesOnlyExactRootAndEffectiveLiveCapability()
		{
			string runtime = Read("Experience/KingdomEducationPostObservationRuntime.cs");
			string proves = Slice(runtime, "internal static bool Proves(",
				"internal static void OnSemanticPass(");
			Ordered(proves, "Zone active = The.ZoneManager?.ActiveZone",
				"return LiveProves", "TryRaw(Work.ZoneId", "TryReadExact", "TryFindExact");
			string root = Slice(runtime, "private static bool TryExactRoot(",
				"private static bool TryExactReading(");
			foreach (string term in new[] { "Survey.Built",
				"KingdomUpgrade.IsFunctionallyBuilt(candidate)", "candidate.CurrentZone, Zone",
				"cell.X != Work.AnchorX", "cell.Y != Work.AnchorY",
				"candidate.Blueprint != Work.DesignKey",
				"KingdomCityRules.StableId(candidate.IDIfAssigned) != Work.WorkId",
				"Matches != 1" }) StringAssert.Contains(term, root);
			string live = Slice(runtime, "private static bool LiveProves(",
				"private static bool TryRows(");
			StringAssert.Contains("readings == 1", live);
			StringAssert.Contains("KingdomBenefitCapabilities.Has", live);
			StringAssert.Contains("KingdomBenefitCapabilities.Education", live);
			StringAssert.DoesNotContain("KingdomData", runtime);
			StringAssert.DoesNotContain("KingdomOffices", runtime);
		}

		[Test]
		public void AttendedObservationRevokesFirstAndPublishesExactReadbackLast()
		{
			string runtime = Read("Experience/KingdomEducationPostObservationRuntime.cs");
			string observe = Slice(runtime, "internal static void OnSemanticPass(",
				"internal static bool TryRevokeOwned(");
			Ordered(observe, "TryRevokeZone(zoneId", "System.City.TryRead", "Survey.TryBenefits",
				"TryRows", "TryEncode(rows", "KingdomZoneObservationRules.TryCreate",
				"KingdomZoneObservationCodec.TryEncode", "Zone.SetZoneProperty",
				"raw?.GetType() != typeof(string)", "TryReadExact", "TryDecode");
			StringAssert.Contains("The.Game.TimeTicks != tick", observe);
			StringAssert.Contains("ReferenceEquals(Zone, The.ZoneManager?.ActiveZone)", observe);
			StringAssert.Contains("TryBinding(System, zoneId, settlementId", observe);
			StringAssert.Contains("catch (Exception) { TryRemoveRaw(zoneId);", observe);
			StringAssert.Contains("TryRemoveRaw(zoneId);", observe);
		}

		[Test]
		public void ExistingReachStepOwnsObservationWithoutNewWireBit()
		{
			string pass = Read("Core/KingdomSystem.z21.SemanticPass.cs");
			string reach = Slice(pass, "TrySemanticStep(SemanticStepReach", "SemanticStepLocus");
			Ordered(reach, "KingdomEducationPostObservationRuntime.OnSemanticPass",
				"KingdomReach.OnZoneActivated");
			string events = Read("Core/KingdomSystem.z20.Events.cs");
			StringAssert.DoesNotContain("SemanticStepEducation", events);
			StringAssert.Contains("SemanticRequiredMask = (1L << 21) - 1L", events);
		}

		[Test]
		public void OwnershipTransitionsRevokeEveryObservationPurposeBeforeLoss()
		{
			string coordinator = Read("Core/KingdomZoneObservationRevocation.cs");
			Ordered(coordinator, "KingdomReachObservationRuntime.TryRevokeZones",
				"KingdomEducationPostObservationRuntime.TryRevokeZones");
			Ordered(coordinator, "KingdomReachObservationRuntime.TryRevokeOwned",
				"KingdomEducationPostObservationRuntime.TryRevokeOwned");
			string secession = Read("Core/KingdomCreed.04.SecessionAndRejoin.cs");
			Ordered(secession, "IList<string> leavingClaims",
				"KingdomZoneObservationRevocation.TryRevokeZones",
				"KingdomRelocation.BeforeOwnershipLoss", "TryRemoveNonSeatSettlement");
			string exile = Read("Core/KingdomSystem.z09.Exile.Dispatch.cs");
			Ordered(exile, "KingdomZoneObservationRevocation.TryRevokeOwned",
				"KingdomPolityRealmTransitionRuntime.TryAdvanceExile",
				"ResetCurrentRealmAfterExile");
		}

		[Test]
		public void ReceiptHasCoverageWithoutCityBookOrArchiveSchema()
		{
			foreach (string coverage in new[] { "Core/KingdomRemovalCoverage.cs",
				"Core/KingdomRemovalCoverage.Generated.cs" })
				StringAssert.Contains("\"r_TAF_EducationPostObservation_v1\"",
					Read(coverage), coverage);
			foreach (string path in new[] { "Simulation/City/KingdomCityBook.00.CoreZoneAndWorkColumns.cs",
				"Simulation/City/KingdomCityBook.09.ZoneAndStateRead.cs",
				"Core/KingdomArchivedSettlementCodec.cs", "Core/KingdomRealmArchive.10WireEnvelope.cs" })
				StringAssert.DoesNotContain("EducationPostObservation", Read(path), path);
		}

		[Test]
		public void ShardsStayUnderCapAndProjectsRegisterPureAndSourceTestsOnce()
		{
			foreach (string path in new[] { "Experience/KingdomEducationPostObservationRow.cs",
				"Experience/KingdomEducationPostObservationRules.cs",
				"Experience/KingdomEducationPostObservationRuntime.cs",
				"Experience/KingdomSuccession.HeirsAndNews.cs",
				"Experience/KingdomSuccession.Grooming.cs",
				"Experience/KingdomGroomingRules.cs",
				"Growth/KingdomResearch.Advance.cs",
				"Core/KingdomCharterPart.Succession.cs",
				"Core/KingdomZoneObservationRevocation.cs",
				"Core/KingdomSystem.z21.SemanticPass.cs",
				"Core/KingdomCreed.04.SecessionAndRejoin.cs",
				"Core/KingdomSystem.z09.Exile.Dispatch.cs",
				"Core/KingdomRemovalCoverage.cs",
				"Core/KingdomRemovalCoverage.Generated.cs" })
				Assert.That(Read(path).Split('\n').Length, Is.LessThanOrEqualTo(300), path);
			foreach (string project in new[] { "DevTests/TafTests.csproj",
				"DevTests/PortableTests.csproj" })
			{
				string source = Read(project);
				foreach (string file in new[] { "KingdomEducationPostObservationRow.cs",
					"KingdomEducationPostObservationRules.cs",
					"KingdomEducationPostObservationTests.cs",
					"KingdomEducationPostObservationSourceTests.cs" })
					Assert.That(Count(source, file), Is.EqualTo(1), project + ": " + file);
			}
		}
	}
}
#endif
