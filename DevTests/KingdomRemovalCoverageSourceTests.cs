#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Source-contract proof for <see cref="KingdomRemovalCoverage.OwnedGlobalStatePrefixes"/>:
	/// every listed prefix must be traceable to a literal declared or built by its named producer,
	/// not merely assumed by the registry. Closes the SH-8(a) coverage hole (audit
	/// FOUNDATION-RUNTIME-FULL-AUDIT-CLAUDE.md Part A2 / repair item 6): the sweep must clear
	/// <c>r_TAF_PurposePairCargo:</c> exactly as it clears every sibling escrow prefix.
	/// </summary>
	[TestFixture]
	public sealed class KingdomRemovalCoverageSourceTests
	{
		/// <summary>Every owned global-state prefix paired with the exact file that mints it.</summary>
		private static readonly (string Prefix, string ProducerPath)[] PrefixProducers =
		{
			("$ThousandAndFirst_ConstructionInputRetirement_",
				"Simulation/City/KingdomCentralLogistics.21.ConstructionInputRetirement.cs"),
			("$ThousandAndFirst_ConstructionInputTransit_",
				"Simulation/City/KingdomCentralLogistics.19.ConstructionInputTransitCustody.cs"),
			("r_TAF_BountyManningOption_v1:", "Quests/KingdomBounty.ManningOption.cs"),
			("r_TAF_Crown_", "Growth/KingdomCrownRules.cs"),
			("r_TAF_Delved:", "Growth/KingdomDelve.cs"),
			("r_TAF_DelveLink:", "Growth/KingdomDelveLink.00.ReceiptDeclarationsAndPreflight.cs"),
			("r_TAF_FaithGlobalOption_v1:", "Experience/KingdomFaith.cs"),
			("r_TAF_FoundingHeartRoot:",
				"Growth/KingdomPlot2.07a.FoundingHeartAuthority.cs"),
			("r_TAF_GrowthArrivalEscrow:", "Growth/KingdomGrowth.ArrivalStart.cs"),
			("r_TAF_ImprovementGrowthEscrow:", "Growth/KingdomPlot2.03.RegistryAndDeclarations.cs"),
			("r_TAF_ImprovementHeld:", "Growth/KingdomUpgrade.09.RegistryAndIdentity.cs"),
			("r_TAF_ImprovementItemEscrow:",
				"Growth/KingdomUpgrade.00.r_KingdomImprovement.Declarations.cs"),
			("r_TAF_MirrorGate_", "Growth/KingdomMirrorGateRules.cs"),
			("r_TAF_PurposeCargoEscrow:", "Growth/KingdomPurpose.00.CatalogueAndDispatch.cs"),
			// SH-8(a): the escrow's sibling root key, previously absent from the owned list.
			("r_TAF_PurposePairCargo:", "Growth/KingdomPurposePortfolio.OperationControl.cs"),
			("r_TAF_ReachCity_", "Growth/KingdomReach.GroundCharacter.cs"),
			("r_TAF_ReachRealm_", "Growth/KingdomReach.GroundCharacter.cs"),
			("r_TAF_RelocationEscrow:", "Growth/KingdomRelocation.cs"),
			("r_TAF_ResearchSeedSources:", "Growth/KingdomResearch.SeedSources.cs"),
			("r_TAF_RoadsGlobalOption_v1:", "Growth/KingdomRoads.00.DeclarationsAndRetry.cs"),
			("r_TAF_SubsidenceOption_v1:", "Growth/KingdomSubsidence.cs"),
		};

		[Test]
		public void PurposePairCargoPrefixIsPresentInOwnedList()
		{
			CollectionAssert.Contains(KingdomRemovalCoverage.OwnedGlobalStatePrefixes,
				"r_TAF_PurposePairCargo:",
				"the exact root key ObjectGameState[key] = Cargo writes in "
				+ "KingdomPurposePortfolio.OutputRuntime.cs must be swept by realm removal");
		}

		[Test]
		public void ConstructionInputCustodyStatesAreValueBearingAndPreserved()
		{
			CollectionAssert.Contains(KingdomRemovalCoverage.OwnedGlobalStates,
				"$ThousandAndFirst_ConstructionInputLostAuthorityCursor");
			CollectionAssert.Contains(KingdomRemovalCoverage.OwnedGlobalStates,
				"$ThousandAndFirst_ConstructionInputObservations");
			foreach (string prefix in new[] {
				"$ThousandAndFirst_ConstructionInputTransit_",
				"$ThousandAndFirst_ConstructionInputRetirement_" })
			{
				CollectionAssert.Contains(KingdomRemovalCoverage.OwnedGlobalStatePrefixes, prefix);
				Assert.That(KingdomRemovalCoverage.GlobalDisposition(prefix + "owner_1"),
					Is.EqualTo(KingdomRemovalGlobalDisposition.Preserve));
			}
			Assert.That(KingdomRemovalCoverage.GlobalDisposition(
				"$ThousandAndFirst_ConstructionInputObservations"),
				Is.EqualTo(KingdomRemovalGlobalDisposition.Preserve));
			Assert.That(KingdomRemovalCoverage.GlobalDisposition(
				"$ThousandAndFirst_ConstructionInputLostAuthorityCursor"),
				Is.EqualTo(KingdomRemovalGlobalDisposition.Preserve));
		}

		[Test]
		public void EveryOwnedGlobalStatePrefixHasAProvableProducer()
		{
			Assert.That(PrefixProducers.Length,
				Is.EqualTo(KingdomRemovalCoverage.OwnedGlobalStatePrefixes.Length),
				"this fixture's producer map has drifted from OwnedGlobalStatePrefixes -- "
				+ "every new prefix needs an entry here naming its exact writer, not a guess");
			for (int i = 0; i < PrefixProducers.Length; i++)
			{
				(string prefix, string producerPath) = PrefixProducers[i];
				CollectionAssert.Contains(KingdomRemovalCoverage.OwnedGlobalStatePrefixes, prefix);
				string producerSource = TestMain.ReadRepositoryText(producerPath);
				StringAssert.Contains(prefix, producerSource,
					prefix + " must appear literally in its declared producer " + producerPath);
			}
		}

		[Test]
		public void IsOwnedGlobalStateAcceptsExactPurposePairCargoKeysAndRejectsLookalikes()
		{
			Assert.That(KingdomRemovalCoverage.IsOwnedGlobalState(
				"r_TAF_PurposePairCargo:pair-1:0:purpose-op-pair-1-1"), Is.True,
				"a real rooted-cargo key (pairId:epoch:operationId) must be swept");
			Assert.That(KingdomRemovalCoverage.IsOwnedGlobalState(
				"r_TAF_PurposePairCargoKey"), Is.False,
				"the unrelated per-item object property (no trailing colon) must not be mistaken "
				+ "for the global-state root key");
			Assert.That(KingdomRemovalCoverage.IsOwnedGlobalState(
				"r_TAF_PurposePairCargoSchema"), Is.False,
				"another real object property sharing the same stem must not match without the colon");
			Assert.That(KingdomRemovalCoverage.IsOwnedGlobalState(
				"r_TAF_PurposePair"), Is.False,
				"a truncated key missing the trailing colon must not match");
		}
	}
}
#endif
