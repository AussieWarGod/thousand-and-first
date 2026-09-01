using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomRemovalCoverageTests
	{
		[Test]
		public void ForeignPrefixCollisionsAreAlwaysPreserved()
		{
			string[] objectKeys =
			{
				"KingdomCome", "TAFForeign", "r_TAFLikeForeign",
				"r_TAF_Foreign", "KingdomGatehouseSatelliteIdentity"
			};
			for (int i = 0; i < objectKeys.Length; i++)
				Assert.That(KingdomRemovalCoverage.IsOwnedObjectProperty(objectKeys[i]),
					Is.False, objectKeys[i]);
			Assert.That(KingdomRemovalCoverage.IsOwnedBlueprint("r_KingdomCome"), Is.False);
			Assert.That(KingdomRemovalCoverage.IsOwnedBlueprint(
				"r_KingdomCropBlueprint"), Is.False,
				"a property-tag key must never become destructive blueprint authority");
			Assert.That(KingdomRemovalCoverage.IsOwnedZoneProperty("KingdomCome"), Is.False);
			Assert.That(KingdomRemovalCoverage.IsOwnedGlobalState("TAFForeign"), Is.False);
		}

		[Test]
		public void ExactAndDelimiterBoundedOwnedKeysAreRecognized()
		{
			Assert.That(KingdomRemovalCoverage.IsOwnedObjectProperty(
				"KingdomGatehouseSatelliteId0"), Is.True);
			Assert.That(KingdomRemovalCoverage.IsOwnedObjectProperty(
				"KingdomGatehouseSatelliteState0"), Is.True);
			Assert.That(KingdomRemovalCoverage.IsOwnedObjectProperty(
				"KingdomGatehouseProjectionFault"), Is.True);
			Assert.That(KingdomRemovalCoverage.IsOwnedObjectProperty(
				"r_TAF_ImprovementHandover:receipt"), Is.True);
			Assert.That(KingdomRemovalCoverage.IsOwnedObjectProperty(
				"r_TAF_ImprovementHandoverForeign"), Is.False);
			Assert.That(KingdomRemovalCoverage.IsOwnedGlobalState(
				"r_TAF_RelocationEscrow:receipt:item"), Is.True);
			const string identityFence = "r_TAF_RealmIdentityFence_v1";
			StringAssert.Contains("StateKey = \"" + identityFence + "\"",
				TestMain.ReadRepositoryText("Core/KingdomIdentityFenceRules.cs"),
				"the preservation assertion must track the runtime fence key");
			Assert.That(KingdomRemovalCoverage.IsOwnedGlobalState(identityFence), Is.False,
				"base high-water/tombstone fence must survive assembly removal");
		}

		[Test]
		public void EveryRegistryIsSortedUniqueAndBounded()
		{
			AssertSortedUnique(KingdomRemovalCoverage.CustomSystems, "systems");
			AssertSortedUnique(KingdomRemovalCoverage.CustomParts, "parts");
			AssertSortedUnique(KingdomRemovalCoverage.CustomZoneParts, "zone parts");
			AssertSortedUnique(KingdomRemovalCoverage.CustomGameStateSingletons,
				"singletons");
			AssertSortedUnique(KingdomRemovalCoverage.CustomCookingRecipes, "recipes");
			AssertSortedUnique(KingdomRemovalCoverage.CustomJournalNotes, "journal");
			AssertSortedUnique(KingdomRemovalCoverage.ProjectedQuestKinds, "quests");
			AssertSortedUnique(KingdomRemovalCoverage.AbilityCommands, "abilities");
			AssertSortedUnique(KingdomRemovalCoverage.OwnedGlobalStates, "global states");
			AssertSortedUnique(KingdomRemovalCoverage.OwnedGlobalStatePrefixes,
				"global prefixes");
			AssertSortedUnique(KingdomRemovalCoverage.HostedArcologyAuthorityStates,
				"hosted authority states");
			AssertSortedUnique(KingdomRemovalCoverage.HostedArcologyDepartureStates,
				"hosted departure states");
			AssertSortedUnique(KingdomRemovalCoverage.OwnedZoneProperties, "zone properties");
			AssertSortedUnique(KingdomRemovalCoverage.OwnedObjectPropertyPrefixes,
				"object prefixes");
			AssertSortedUnique(KingdomRemovalCoverage.OwnedObjectProperties,
				"object properties");
			AssertSortedUnique(KingdomRemovalCoverage.OwnedBlueprints, "blueprints");
		}

		[Test]
		public void EveryCarrierAndGlobalNamespaceHasAnExplicitDisposition()
		{
			for (int i = 0; i < KingdomRemovalCoverage.CustomParts.Length; i++)
				Assert.That(KingdomRemovalCoverage.CarrierDisposition(
					KingdomRemovalCoverage.CustomParts[i]), Is.Not.EqualTo(
						KingdomRemovalCarrierDisposition.Unknown),
					KingdomRemovalCoverage.CustomParts[i]);
			for (int i = 0; i < KingdomRemovalCoverage.OwnedGlobalStates.Length; i++)
				Assert.That(KingdomRemovalCoverage.GlobalDisposition(
					KingdomRemovalCoverage.OwnedGlobalStates[i]), Is.Not.EqualTo(
						KingdomRemovalGlobalDisposition.Unknown),
					KingdomRemovalCoverage.OwnedGlobalStates[i]);
			for (int i = 0; i < KingdomRemovalCoverage.OwnedGlobalStatePrefixes.Length; i++)
				Assert.That(KingdomRemovalCoverage.GlobalDisposition(
					KingdomRemovalCoverage.OwnedGlobalStatePrefixes[i] + "probe"),
					Is.EqualTo(KingdomRemovalGlobalDisposition.Preserve));
			Assert.That(KingdomRemovalCoverage.GlobalDisposition(
				KingdomIdentityFenceRules.StateKey),
				Is.EqualTo(KingdomRemovalGlobalDisposition.Unknown));
			Assert.That(KingdomRemovalCoverage.CarrierDisposition(
				"r_KingdomWitnessWorkProjection"),
				Is.EqualTo(KingdomRemovalCarrierDisposition.ExactValueRelease),
				"fixed witness work has an authenticated C18 owner and is not residue");
		}

		private static void AssertSortedUnique(string[] Values, string Label)
		{
			Assert.That(Values, Is.Not.Null, Label);
			HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Values.Length; i++)
			{
				Assert.That(string.IsNullOrEmpty(Values[i]), Is.False, Label + " empty");
				Assert.That(unique.Add(Values[i]), Is.True, Label + " duplicate " + Values[i]);
				if (i > 0) Assert.That(string.CompareOrdinal(Values[i - 1], Values[i]),
					Is.LessThan(0), Label + " not sorted at " + Values[i]);
			}
		}
	}
}
