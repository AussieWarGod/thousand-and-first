#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPolityActivationSourceTests
	{
		[Test]
		public void FirstFoundingPublishesPolityAfterIdentityAndBeforeLaterProjections()
		{
			string publish = Read("Core/KingdomFoundingTransaction.12PublishFirst.cs");
			AssertBefore(publish, "system.TryBindTradeIdentity", "KingdomPolityRuntime.TryEnsureFoundation");
			AssertBefore(publish, "Projection = KingdomFoundingProjection.Seat;",
				"KingdomPolityRuntime.TryEnsureFoundation");
			AssertBefore(publish, "KingdomPolityRuntime.TryEnsureFoundation", "EnsureAbility(Actor)");
			AssertBefore(publish, "KingdomPolityRuntime.TryEnsureFoundation", "EnsurePlacement(system");
			AssertBefore(publish, "KingdomPolityRuntime.TryEnsureFoundation",
				"KingdomSeal.TryFoundingCompleted");

			string recovery = Read("Core/KingdomFoundingTransaction.11Run.cs");
			AssertBefore(recovery, "KingdomPolityRuntime.TryEnsureFoundation", "if (complete)");
			StringAssert.Contains("KingdomPolityRuntime.FoundationObserved(System, realm)",
				Read("Core/KingdomFoundingTransaction.18ReceiptCompletion.cs"));
		}

		[Test]
		public void ArchiveAdapterCopiesOnlyBoundedFactsNeverRuntimeIdentities()
		{
			string source = Read("World/KingdomInheritanceState.z13.PolityFacts.cs");
			StringAssert.Contains("Phase != KingdomInheritancePhase.Committed", source);
			StringAssert.Contains("TryGetReservation(out legacy, out reserved)", source);
			StringAssert.Contains("TryGetCommittedReceipt(out committed)", source);
			StringAssert.Contains("KingdomRules.InheritedState.Faded", source);
			StringAssert.Contains("legacy.Population <= 0", source);
			StringAssert.DoesNotContain("legacy.RealmId", source);
			StringAssert.DoesNotContain("legacy.SettlementId", source);
			StringAssert.DoesNotContain("legacy.OriginGameId", source);
			StringAssert.DoesNotContain("RealmIdentityLegacyFaction", source);
			StringAssert.Contains("ProfileSchema = legacy.ProfileSchema", source);
			StringAssert.Contains("TechnologyBand = legacy.TechnologyBand", source);
			StringAssert.Contains("CanonicalBodyKeys = Copy(legacy.CanonicalBodyKeys)", source);
			StringAssert.Contains("SourceProfileDigest = legacy.SourceProfileDigest", source);
			StringAssert.Contains("ProfileProvenanceDigest = legacy.ProfileProvenanceDigest", source);
			StringAssert.DoesNotContain("TechnologyBand = legacy.Stage", source);
			string model = Read("Polity/KingdomPolityActivationModels.cs");
			StringAssert.DoesNotContain("public string ActorId", model);
			StringAssert.DoesNotContain("public string FactionId;", SliceLegacy(model));
		}

		[Test]
		public void IrreversibleFactionRegistryMutationFollowsPreparedReceiptAndOwnedRecovery()
		{
			string runtime = Read("Polity/KingdomPolityFactionRuntime.cs");
			AssertBefore(runtime, "TryPrepareLegacyFaction", "Factions.AddNewFaction(candidate)");
			StringAssert.Contains("OwnedExactly(recovered, RealmId, View)", runtime);
			StringAssert.Contains("r_TAF_PolityOwnerRealm_v1", runtime);
			StringAssert.Contains("r_TAF_PolityProjection_v1", runtime);
			StringAssert.Contains("r_TAF_PolityTombstone_v1", runtime);
			StringAssert.DoesNotContain("RemoveFaction", runtime);
			StringAssert.DoesNotContain("SetFactionFeeling", runtime);
			StringAssert.DoesNotContain("PlayerKingdom", runtime);
		}

		[Test]
		public void RegeneratedBodiesAreFreshUnplacedAndUseExactOwnedLoadout()
		{
			string runtime = Read("Polity/KingdomPolityNpcRuntime.cs");
			StringAssert.Contains("created.SetIntProperty(\"NoXP\", 1)", runtime);
			AssertBefore(runtime, "GameObject.Create(spec.BodyBlueprint)",
				"ClearGeneratedLoadout(created");
			AssertBefore(runtime, "ClearGeneratedLoadout(created", "ApplyGear(created, spec");
			StringAssert.Contains("created.CurrentCell != null", runtime);
			StringAssert.Contains("GetInventoryDirectAndEquipment", runtime);
			StringAssert.Contains("item.IsNatural()", runtime);
			StringAssert.Contains("GearOwnerProperty", runtime);
			StringAssert.Contains("created.Brain.Allegiance.Hostile = false;", runtime);
			StringAssert.DoesNotContain("created.Brain.Hostile", runtime);
			StringAssert.DoesNotContain("AddObject(", runtime);
			StringAssert.DoesNotContain("GetZone(", runtime);
			StringAssert.DoesNotContain("DeepCopy", runtime);
			StringAssert.DoesNotContain("Stat.Random", runtime);
			StringAssert.DoesNotContain("ActorId", runtime);
		}

		[Test]
		public void ResidentBridgeReadsOnlyExactGroomedSuccessionAuthority()
		{
			string bridge = Read("Polity/KingdomSuccession.PolityBridge.cs");
			string runtime = Read("Polity/KingdomPolityResidentRuntime.cs");
			StringAssert.Contains("config.Choice != HeirChoice.Groomed", bridge);
			StringAssert.Contains("TryReadRealmGrooming", bridge);
			StringAssert.Contains("TryUniqueHeir", bridge);
			StringAssert.Contains("row.ResidentId != ResidentId", bridge);
			StringAssert.Contains("row.Standing != KingdomResidentStanding.Resident", bridge);
			StringAssert.DoesNotContain("TryHead", bridge + runtime);
			StringAssert.DoesNotContain("GameObject", bridge + runtime);
			StringAssert.Contains("TryRetireResidentSuccessor", runtime);
		}

		[Test]
		public void LoadAndCityPublicationReconcileOwnedPolityProjections()
		{
			string load = Read("Core/KingdomLoader.cs");
			AssertBefore(load, "KingdomPolityRuntime.TryEnsureFoundation",
				"KingdomPolityActiveRuntime.TryReconcile");
			string active = Read("Polity/KingdomPolityActiveRuntime.cs");
			AssertBefore(active, "KingdomPolityResidentRuntime.TryReconcile",
				"KingdomPolityVisitRuntime.TryReconcile");
			string city = Read("Simulation/City/KingdomCity.z01.CheckIn.cs");
			AssertBefore(city, "Publish(System, state);",
				"KingdomPolityResidentRuntime.TryReconcile");
			AssertBefore(city, "KingdomPolityResidentRuntime.TryReconcile",
				"KingdomPolityVisitRuntime.TryReconcile");
		}

		[Test]
		public void ExileReturnAndRefoundUseExactPolityTransitionSeams()
		{
			string exile = Read("Core/KingdomSystem.z09.Exile.Dispatch.cs");
			AssertBefore(exile, "KingdomPolityRealmTransitionRuntime.TryAdvanceExile",
				"ResetCurrentRealmAfterExile");
			string returned = Read("Core/KingdomSystem.z11.Return.Begin.cs");
			AssertBefore(returned, "RestoreArchivedRealmCore(archive",
				"KingdomPolityRealmTransitionRuntime.TryRestoreReturn");
			AssertBefore(returned, "KingdomPolityRealmTransitionRuntime.TryRestoreReturn",
				"KingdomChronicle.TryRestoreRealmRegistry");
			AssertBefore(returned, "KingdomPolityRealmTransitionRuntime.TryCompleteReturn",
				"ExiledRealmArchive = null");
			string transitionRuntime = Read("Polity/KingdomPolityRealmTransitionRuntime.cs");
			AssertBefore(transitionRuntime, "TryReleaseRealmReturnMarkers(transition",
				"KingdomPolityRules.TryCompleteRealmReturn(System.PolityLedger");
			string foundation = Read("Polity/KingdomPolityRuntime.cs");
			AssertBefore(foundation, "TryFoundationLegacy(System",
				"TryRebindEmptyIdentity(System.PolityLedger");
			AssertBefore(foundation, "TryRebindEmptyIdentity(System.PolityLedger",
				"TryPublishFoundation(System.PolityLedger");
			AssertBefore(foundation, "KingdomPolityFactionRuntime.TryReconcile(System",
				"KingdomPolityRealmTransitionRuntime.TryCommitRefound");
		}

		[Test]
		public void FoundationProfileReadsCraftAndNeverInfersBodiesFromPlaceProse()
		{
			string runtime = Read("Polity/KingdomPolityRuntime.cs");
			StringAssert.Contains("TechnologyBand = (int)KingdomZoning.Tech(S) * 2", runtime);
			StringAssert.DoesNotContain("TechnologyBand = (int)S.Stage", runtime);
			string rules = Read("Polity/KingdomPolityProfileRules.cs");
			StringAssert.Contains("CurrentBodyKeys(Facts.SpeciesKeys, Facts.IdentityKeys", rules);
			StringAssert.DoesNotContain("Merge(Facts.OriginKeys, Facts.CultureKeys", rules);
			StringAssert.DoesNotContain("Math.Min(10, Facts.Stage * 2)", rules);
			string revisions = Read("Polity/KingdomPolityProfileRuntime.cs");
			StringAssert.Contains("KingdomZoningRules.TechPoints(roster)", revisions);
			StringAssert.Contains("KingdomPolityProfileFactKind.Population", revisions);
			StringAssert.DoesNotContain("Stage * 2", revisions);
		}

		[Test]
		public void ExileTransformEndsPolitiesWithoutContinuingActorsOrInferringCasualties()
		{
			string rules = Read("Polity/KingdomPolityRules.RealmTransition.cs");
			StringAssert.Contains("retiredCurrent.Lifecycle = KingdomPolityLifecycle.Ended", rules);
			StringAssert.Contains("retiredImported.Lifecycle = KingdomPolityLifecycle.Ended", rules);
			StringAssert.Contains("ReturnLedgerEnvelope = rollback", rules);
			string runtime = Read("Polity/KingdomPolityRealmTransitionRuntime.cs") +
				Read("Polity/KingdomPolityFactionRuntime.RealmTransition.cs");
			StringAssert.Contains("ReturnLedgerEnvelope", rules + runtime);
			StringAssert.DoesNotContain("ActorId", runtime);
			StringAssert.DoesNotContain("GameObject", runtime);
			StringAssert.DoesNotContain("GetZone(", runtime);
			StringAssert.DoesNotContain("KingdomPolityFigurePhase.Dead", rules + runtime);
			StringAssert.DoesNotContain("KingdomPolityFigurePhase.Missing", rules + runtime);
			StringAssert.DoesNotContain("casualt", (rules + runtime).ToLowerInvariant());
			StringAssert.DoesNotContain("conquest", (rules + runtime).ToLowerInvariant());
		}

		[Test]
		public void ExileLegacyCopyUsesFrozenRealmFactsAndNoRuntimeIdentityColumns()
		{
			string facts = Read("Polity/KingdomPolityRealmLegacyFacts.cs");
			StringAssert.Contains("KingdomRealmArchive Archive", facts);
			StringAssert.Contains("KingdomResidentRules.TryProject", facts);
			StringAssert.Contains("polity-exile-legacy-token-v1", facts);
			StringAssert.DoesNotContain("ResidentIds", facts);
			StringAssert.DoesNotContain("GameObject", facts);
			StringAssert.DoesNotContain("Factions.", facts);
			StringAssert.Contains("TryCaptureLegacyProfile(legacy, profile", facts);
			string profile = Read("Polity/KingdomPolityProfileRules.Legacy.cs");
			StringAssert.Contains("CurrentLegacyProfileSchema", profile);
			StringAssert.Contains("new List<string> { \"unresolved\" }", profile);
			StringAssert.DoesNotContain("Facts.Stage * 2", profile);
			StringAssert.DoesNotContain("LegacyBodyKeys", profile);
			string transitions = Read("Polity/KingdomPolityRules.RealmTransition.cs") +
				Read("Polity/KingdomPolityRules.ValidationRealmTransition.cs");
			StringAssert.Contains("MatchesLegacyProfileSource", transitions);
			string model = Read("Polity/KingdomPolityActivationModels.cs");
			string legacy = SliceLegacy(model);
			StringAssert.DoesNotContain("public string RealmId", legacy);
			StringAssert.Contains("next.ReturnLedgerEnvelope = null",
				Read("Polity/KingdomPolityRules.RealmTransition.cs"));
		}

		[Test]
		public void EveryPolityProductionShardStaysBelowThreeHundredLines()
		{
			string root = Path.Combine(TestMain.RepositoryRoot, "Polity");
			foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.TopDirectoryOnly))
			{
				int lines = File.ReadAllLines(file).Length;
				Assert.Less(lines, 300, Path.GetFileName(file) + " must be split");
			}
		}

		private static string SliceLegacy(string source)
		{
			int start = source.IndexOf("public sealed class KingdomPolityLegacySnapshot",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0);
			int end = source.IndexOf("public sealed class KingdomPolityRealmExileFacts",
				start, StringComparison.Ordinal);
			return end < 0 ? source.Substring(start) : source.Substring(start, end - start);
		}

		private static string Read(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static void AssertBefore(string source, string first, string second)
		{
			int a = source.IndexOf(first, StringComparison.Ordinal);
			int b = source.IndexOf(second, StringComparison.Ordinal);
			Assert.GreaterOrEqual(a, 0, first); Assert.Greater(b, a, second);
		}
	}
}
#endif
