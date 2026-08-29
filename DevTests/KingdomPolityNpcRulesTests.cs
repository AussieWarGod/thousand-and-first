using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityNpcRulesTests
	{
		[Test]
		public void ResolverIsDeterministicCompleteAndDoesNotMutateProfile()
		{
			KingdomPolityProfileRevision profile = Profile(0, "human", "guard");
			string before = profile.FactsDigest; int roles = profile.RoleKeys.Count;
			Assert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 3,
				out KingdomPolityNpcSpec first, out string failure), failure);
			Assert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 3,
				out KingdomPolityNpcSpec second, out failure), failure);
			Assert.AreEqual(first.ResolverDigest, second.ResolverDigest);
			Assert.AreEqual(first.BodyBlueprint, second.BodyBlueprint);
			Assert.AreEqual(first.Level, second.Level); Assert.AreEqual(first.Strength, second.Strength);
			CollectionAssert.AreEqual(first.Skills, second.Skills);
			CollectionAssert.AreEqual(first.GearBlueprints, second.GearBlueprints);
			Assert.AreEqual(before, profile.FactsDigest); Assert.AreEqual(roles, profile.RoleKeys.Count);
			Assert.AreEqual("WatervineFarmer", first.BodyBlueprint);
			Assert.Greater(first.Hitpoints, 0); Assert.AreEqual(1, first.Mutations.Count);
		}

		[Test]
		public void TechnologyZeroNeverResolvesMetalGearAndSpeciesBlueprintsAreVanilla()
		{
			Dictionary<string, string> expected = new Dictionary<string, string>
			{
				{ "human", "WatervineFarmer" }, { "snapjaw", "Snapjaw Warrior" },
				{ "goatfolk", "Goatfolk" }, { "dromad", "Dromad" },
				{ "hindren", "HindrenVillager" }, { "mechanical", "Scrapbot" }
			};
			foreach (KeyValuePair<string, string> row in expected)
			{
				Assert.IsTrue(KingdomPolityNpcRules.TryResolve(Profile(0, row.Key, "guard"),
					"guard", 0, out KingdomPolityNpcSpec spec, out string failure), failure);
				Assert.AreEqual(row.Value, spec.BodyBlueprint);
				if (row.Key == "mechanical") CollectionAssert.IsEmpty(spec.GearBlueprints);
				else CollectionAssert.AreEqual(
					new[] { "Club", "Leather Armor", "Wooden Buckler" }, spec.GearBlueprints);
				CollectionAssert.DoesNotContain(spec.GearBlueprints, "Long Sword");
			}
		}

		[Test]
		public void RoleAndOrdinalArePinnedInputsNotRuntimeRandomness()
		{
			KingdomPolityProfileRevision profile = Profile(4, "goatfolk", "envoy", "guard");
			Assert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "envoy", 0,
				out KingdomPolityNpcSpec envoy, out string failure), failure);
			Assert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 0,
				out KingdomPolityNpcSpec guard, out failure), failure);
			Assert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 1,
				out KingdomPolityNpcSpec other, out failure), failure);
			Assert.AreNotEqual(envoy.ResolverDigest, guard.ResolverDigest);
			Assert.AreNotEqual(guard.ResolverDigest, other.ResolverDigest);
			Assert.Greater(guard.Strength, envoy.Strength);
			CollectionAssert.Contains(guard.GearBlueprints, "Chain Mail");
			CollectionAssert.Contains(envoy.GearBlueprints, "Leather Armor");
			Assert.IsFalse(KingdomPolityNpcRules.TryResolve(profile, "warband", 0,
				out KingdomPolityNpcSpec _, out failure));
		}

		[Test]
		public void FullProfileExpressionIsPinnedIntoResolverDigest()
		{
			KingdomPolityProfileRevision profile = Profile(2, "human", "guard");
			Assert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 0,
				out KingdomPolityNpcSpec before, out string failure), failure);
			profile.PracticeTags.Add("zz-new-practice");
			Assert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 0,
				out KingdomPolityNpcSpec after, out failure), failure);
			Assert.AreNotEqual(before.ResolverDigest, after.ResolverDigest);
			profile.BodyKeys.Add("unknown-body");
			Assert.IsFalse(KingdomPolityNpcRules.TryResolve(profile, "guard", 0,
				out KingdomPolityNpcSpec _, out failure));
		}

		[Test]
		public void ResidentSuccessorCasTransfersOfficeWithoutInventingDeath()
		{
			KingdomPolityLedger ledger = CurrentOnly(); long source = ledger.Revision;
			Assert.IsTrue(KingdomPolityRules.TryEnsureResidentSuccessor(ledger, source,
				Settlement(), 7, "Mara", 1, 100L, out KingdomPolityPublicationResult first,
				out string failure), failure);
			Assert.IsTrue(KingdomPolityRules.TryEnsureResidentSuccessor(ledger, source,
				Settlement(), 7, "Mara", 1, 101L, out KingdomPolityPublicationResult retry,
				out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			Assert.IsTrue(KingdomPolityRules.TryEnsureResidentSuccessor(ledger, ledger.Revision,
				Settlement(), 9, "Otho", 2, 120L, out KingdomPolityPublicationResult _, out failure), failure);
			Assert.AreEqual(2, ledger.NamedFigures.Count); int active = 0, transferred = 0;
			for (int i = 0; i < ledger.NamedFigures.Count; i++)
			{
				KingdomPolityNamedFigureRecord figure = ledger.NamedFigures[i];
				if (figure.Phase == KingdomPolityFigurePhase.Active)
				{
					active++; Assert.AreEqual(9, figure.ResidentId);
					Assert.AreEqual(Settlement(), figure.ResidentSettlementId);
				}
				if (figure.Phase == KingdomPolityFigurePhase.Transferred)
				{
					transferred++; Assert.IsNotEmpty(figure.ConclusionRef);
					Assert.AreNotEqual(KingdomPolityFigurePhase.Dead, figure.Phase);
					Assert.AreEqual(0, figure.ResidentId);
					Assert.IsNull(figure.ResidentSettlementId);
				}
			}
			Assert.AreEqual(1, active); Assert.AreEqual(1, transferred);
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
			string cause = KingdomPolityRules.ActivationId("taf:fact:test:v1:",
				"successor-retire-test-v1", Realm());
			Assert.IsTrue(KingdomPolityRules.TryRetireResidentSuccessor(ledger,
				ledger.Revision, cause, 130L, out KingdomPolityPublicationResult retired,
				out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.Applied, retired.Outcome);
			Assert.IsTrue(KingdomPolityRules.TryRetireResidentSuccessor(ledger,
				ledger.Revision - 1, cause, 131L, out retired, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retired.Outcome);
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
		}

		private static KingdomPolityProfileRevision Profile(int Technology, string Body,
			params string[] Roles)
		{
			List<string> roles = new List<string>(Roles); roles.Sort(System.StringComparer.Ordinal);
			return new KingdomPolityProfileRevision
			{
				ProfileId = "taf:polity-profile:test", Revision = 1,
				PolityId = "taf:realm:v1:test", RulesVersion = 1,
				FactsDigest = KingdomPolityTestData.DigestA, TechnologyBand = Technology,
				DerivedFromFactIds = new List<string> { "taf:fact:test:v1:profile" },
				BodyKeys = new List<string> { Body }, RoleKeys = roles,
				GearKeys = TestGear(Technology),
				Loadout = TestLoadout(Technology)
			};
		}

		private static List<string> TestGear(int Technology)
		{
			if (Technology <= 0) return new List<string> { "club", "leather-armor", "wooden-buckler" };
			if (Technology <= 2) return new List<string> { "bronze-sword", "leather-armor", "wooden-buckler" };
			if (Technology <= 4) return new List<string> { "chain-mail", "iron-sword", "wooden-buckler" };
			if (Technology <= 6) return new List<string> { "chain-mail", "steel-sword", "wooden-buckler" };
			return new List<string> { "carbide-plate", "carbide-sword", "wooden-buckler" };
		}

		private static KingdomPolityLoadoutPolicy TestLoadout(int Technology)
		{
			return new KingdomPolityLoadoutPolicy
			{
				Kind = KingdomPolityLoadoutPolicyKind.OwnedReplace,
				ExpectedValueBudget = 50 + Technology * 125,
				ExcludedKeys = new List<string> { "natural-gear", "quest", "relic",
					"trader-stock", "unique" },
				SelectedKeys = TestGear(Technology)
			};
		}

		private static KingdomPolityLedger CurrentOnly()
		{
			string realm = Realm(); Assert.IsTrue(KingdomPolityRules.TryCreate(realm,
				KingdomPolityImportPolicy.Off, out KingdomPolityLedger ledger, out string failure), failure);
			KingdomPolityFoundationFacts facts = new KingdomPolityFoundationFacts
			{
				RealmId = realm, FactionId = realm, DisplayName = "House Water",
				FounderName = "Ari", SettlementId = Settlement(), Style = "common",
				Stage = 0, FoundedTick = 1L
			};
			Assert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
				facts, null, out KingdomPolityPublicationResult _, out failure), failure);
			return ledger;
		}

		private static string Realm()
		{
			return "taf:realm:v1:3333333333333333333333333333333333333333333333333333333333333333";
		}

		private static string Settlement()
		{
			return "taf:settlement:v1:4444444444444444444444444444444444444444444444444444444444444444";
		}
	}
}
