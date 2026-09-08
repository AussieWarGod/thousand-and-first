using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed partial class KingdomPolityNpcRulesTests
	{
		[Test]
		public void ResolverIsDeterministicCompleteAndDoesNotMutateProfile()
		{
			KingdomPolityProfileRevision profile = Profile(0, "human", "guard");
			string before = profile.FactsDigest; int roles = profile.RoleKeys.Count;
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 3,
				out KingdomPolityNpcSpec first, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 3,
				out KingdomPolityNpcSpec second, out failure), failure);
			ClassicAssert.AreEqual(first.ResolverDigest, second.ResolverDigest);
			ClassicAssert.AreEqual(first.BodyBlueprint, second.BodyBlueprint);
			ClassicAssert.AreEqual(first.Level, second.Level); ClassicAssert.AreEqual(first.Strength, second.Strength);
			CollectionAssert.AreEqual(first.Skills, second.Skills);
			CollectionAssert.AreEqual(first.GearBlueprints, second.GearBlueprints);
			ClassicAssert.AreEqual(before, profile.FactsDigest); ClassicAssert.AreEqual(roles, profile.RoleKeys.Count);
			ClassicAssert.AreEqual("WatervineFarmer", first.BodyBlueprint);
			ClassicAssert.Greater(first.Hitpoints, 0); ClassicAssert.AreEqual(1, first.Mutations.Count);
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
				ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(Profile(0, row.Key, "guard"),
					"guard", 0, out KingdomPolityNpcSpec spec, out string failure), failure);
				ClassicAssert.AreEqual(row.Value, spec.BodyBlueprint);
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
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "envoy", 0,
				out KingdomPolityNpcSpec envoy, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 0,
				out KingdomPolityNpcSpec guard, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 1,
				out KingdomPolityNpcSpec other, out failure), failure);
			ClassicAssert.AreNotEqual(envoy.ResolverDigest, guard.ResolverDigest);
			ClassicAssert.AreNotEqual(guard.ResolverDigest, other.ResolverDigest);
			ClassicAssert.Greater(guard.Strength, envoy.Strength);
			CollectionAssert.Contains(guard.GearBlueprints, "Chain Mail");
			CollectionAssert.Contains(envoy.GearBlueprints, "Leather Armor");
			ClassicAssert.IsFalse(KingdomPolityNpcRules.TryResolve(profile, "warband", 0,
				out KingdomPolityNpcSpec _, out failure));
		}

		[Test]
		public void StyleExpressionIsRecognizableWithoutInventingBiologyOrTraining()
		{
			KingdomPolityProfileRevision verdant = ExpressionProfile("style=verdant", "band=2");
			KingdomPolityProfileRevision common = ExpressionProfile("style=common", "band=2");
			int witnessedStyle = 0;
			for (int ordinal = 0; ordinal < 8; ordinal++)
			{
				ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(verdant, "guard", ordinal, 8, 11,
					out KingdomPolityNpcSpec first, out string failure), failure);
				ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(verdant, "guard", ordinal, 8, 11,
					out KingdomPolityNpcSpec retry, out failure), failure);
				ClassicAssert.AreEqual(first.ResolverDigest, retry.ResolverDigest);
				CollectionAssert.AreEqual(first.Skills, retry.Skills);
				if (first.SignatureCues.Contains("verdant-bearing") ||
					first.DialogueCues.Contains("reed-and-canopy")) witnessedStyle++;
				CollectionAssert.DoesNotContain(first.Skills, "Survival");
				ClassicAssert.IsEmpty(first.Mutations);
				ClassicAssert.AreEqual(0, first.Strength); ClassicAssert.AreEqual(0, first.Hitpoints);
				ClassicAssert.GreaterOrEqual(first.ReasonFactIds.Count, 2);
			}
			ClassicAssert.GreaterOrEqual(witnessedStyle, 4,
				"style presentation should remain recognizable across a bounded cohort");
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(verdant, "guard", 0, 8, 11,
				out KingdomPolityNpcSpec green, out string greenFailure), greenFailure);
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(common, "guard", 0, 8, 11,
				out KingdomPolityNpcSpec plain, out string plainFailure), plainFailure);
			ClassicAssert.AreNotEqual(green.ResolverDigest, plain.ResolverDigest);
			ClassicAssert.IsEmpty(plain.Mutations);
		}

		[Test]
		public void RelationshipDeltaChangesBoundedExpressionAndEveryCueIsLegal()
		{
			KingdomPolityProfileRevision neutral = ExpressionProfile("style=common", "band=2");
			KingdomPolityProfileRevision hostile = ExpressionProfile("style=common", "band=5");
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(neutral, "guard", 2, 8, 11,
				out KingdomPolityNpcSpec calm, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(hostile, "guard", 2, 8, 11,
				out KingdomPolityNpcSpec border, out failure), failure);
			ClassicAssert.AreNotEqual(calm.ResolverDigest, border.ResolverDigest);
			CollectionAssert.Contains(border.DialogueCues, "border-grievance");
			for (int i = 0; i < hostile.ExpressionCues.Count; i++)
				ClassicAssert.IsTrue(KingdomPolityProfileExpressionCatalogue.ValidCue(
					hostile.ExpressionCues[i]));
			ClassicAssert.LessOrEqual(border.GearBlueprints.Count, 4);
			ClassicAssert.LessOrEqual(border.Mutations.Count, 2);
		}

		[Test]
		public void LegacyProfileResolverRemainsBytePinnedAndRejectsInjectedTypedCues()
		{
			KingdomPolityProfileRevision legacy = Profile(2, "human", "guard");
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(legacy, "guard", 3,
				out KingdomPolityNpcSpec before, out string failure), failure);
			KingdomPolityProfileRevision typed = ExpressionProfile("style=verdant", "band=2");
			legacy.ExpressionCues.Add(typed.ExpressionCues[0]);
			ClassicAssert.IsFalse(KingdomPolityNpcRules.TryResolve(legacy, "guard", 3,
				out KingdomPolityNpcSpec _, out failure));
			legacy.ExpressionCues.Clear();
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(legacy, "guard", 3,
				out KingdomPolityNpcSpec after, out failure), failure);
			ClassicAssert.AreEqual(before.ResolverDigest, after.ResolverDigest);
			CollectionAssert.AreEqual(before.GearBlueprints, after.GearBlueprints);
			CollectionAssert.AreEqual(before.Skills, after.Skills);
		}

		[Test]
		public void CurrentResolverObeysSelectedGearBudgetExclusionsAndRole()
		{
			KingdomPolityProfileRevision profile = CurrentProfile(4);
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "migrant", 0, 13, 16,
				out KingdomPolityNpcSpec migrant, out string failure), failure);
			CollectionAssert.AreEquivalent(new[] { "Chain Mail", "Long Sword2" },
				migrant.GearBlueprints);
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 0, 16, 19,
				out KingdomPolityNpcSpec guard, out failure), failure);
			CollectionAssert.AreEquivalent(
				new[] { "Chain Mail", "Long Sword2", "Wooden Buckler" },
				guard.GearBlueprints);

			profile.Loadout.ExpectedValueBudget = 369;
			ClassicAssert.IsFalse(KingdomPolityNpcRules.TryResolve(profile, "guard", 0, 16, 19,
				out _, out failure));
			StringAssert.Contains("budget", failure);
			profile.Loadout.ExpectedValueBudget = 550;
			profile.Loadout.ExcludedKeys.Remove("quest");
			ClassicAssert.IsFalse(KingdomPolityNpcRules.TryResolve(profile, "guard", 0, 16, 19,
				out _, out failure));
			StringAssert.Contains("exclusion", failure);
		}

		[Test]
		public void CurrentResolverKeepsCommittedGearWhenAnExtensionAddsAnotherLegalGearCue()
		{
			KingdomPolityProfileRevision profile = CurrentProfile(4);
			KingdomPolityExpressionCue extension = new KingdomPolityExpressionCue
			{
				Kind = KingdomPolityExpressionKind.Gear, ExpressionKey = "Club", Weight = 8,
				SourceKind = KingdomPolityProfileFactKind.Relationship,
				SourceValueKey = "band=5", SourceRef = "taf:source:test:hostile-relation",
				ReasonFactId = "taf:fact:profile:test:hostile-gear"
			};
			ClassicAssert.IsTrue(KingdomPolityProfileExpressionCatalogue.TryMerge(
				profile.ExpressionCues, new[] { extension },
				out List<KingdomPolityExpressionCue> merged, out string failure), failure);
			profile.ExpressionCues = merged;
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolve(profile, "guard", 0, 16, 19,
				out KingdomPolityNpcSpec resolved, out failure), failure);
			CollectionAssert.AreEquivalent(
				new[] { "Chain Mail", "Long Sword2", "Wooden Buckler" },
				resolved.GearBlueprints);
			CollectionAssert.DoesNotContain(resolved.GearBlueprints, "Club");
			CollectionAssert.DoesNotContain(resolved.ReasonFactIds, extension.ReasonFactId);
		}

		[Test]
		public void ResolverTwoReplaysWhileNewPlansPinPolicyResolverThree()
		{
			KingdomPolityProfileRevision profile = CurrentProfile(4);
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolvePinned(profile, "migrant", 2,
				KingdomPolityLoadoutCatalogue.PriorResolverVersion, 13, 16,
				out KingdomPolityNpcSpec prior, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityNpcRules.TryResolvePinned(profile, "migrant", 2,
				KingdomPolityNpcRules.RulesVersion, 13, 16,
				out KingdomPolityNpcSpec current, out failure), failure);
			CollectionAssert.Contains(prior.GearBlueprints, "Leather Armor");
			CollectionAssert.DoesNotContain(prior.GearBlueprints, "Chain Mail");
			CollectionAssert.Contains(current.GearBlueprints, "Chain Mail");
			CollectionAssert.DoesNotContain(current.GearBlueprints, "Leather Armor");
			ClassicAssert.AreNotEqual(prior.ResolverDigest, current.ResolverDigest);
			ClassicAssert.AreEqual(3, KingdomPolityNpcRules.RulesVersion);
		}

		[Test]
		public void TypedProfileWireRoundTripPreservesEveryReasonAndFutureStillQuarantines()
		{
			KingdomPolityLedger source = CurrentOnly();
			ClassicAssert.Greater(source.Profiles[0].ExpressionCues.Count, 1);
			byte[] wire = KingdomPolityCodec.EncodeEnvelope(source);
			KingdomPolityLedger decoded = KingdomPolityCodec.DecodeEnvelope(wire);
			CollectionAssert.AreEqual(wire, KingdomPolityCodec.EncodeEnvelope(decoded));
			ClassicAssert.AreEqual(source.Profiles[0].ExpressionCues.Count,
				decoded.Profiles[0].ExpressionCues.Count);
			for (int i = 0; i < source.Profiles[0].ExpressionCues.Count; i++)
			{
				ClassicAssert.AreEqual(source.Profiles[0].ExpressionCues[i].ReasonFactId,
					decoded.Profiles[0].ExpressionCues[i].ReasonFactId);
				ClassicAssert.AreEqual(source.Profiles[0].ExpressionCues[i].SourceValueKey,
					decoded.Profiles[0].ExpressionCues[i].SourceValueKey);
			}
			wire[4] = 99; wire[5] = wire[6] = wire[7] = 0;
			KingdomPolityLedger future = KingdomPolityCodec.DecodeEnvelope(wire);
			ClassicAssert.AreEqual(KingdomPolitySchemaState.Unknown, future.SchemaState);
			CollectionAssert.AreEqual(wire, KingdomPolityCodec.EncodeEnvelope(future));
		}

		[Test]
		public void ResidentSuccessorCasTransfersOfficeWithoutInventingDeath()
		{
			KingdomPolityLedger ledger = CurrentOnly(); long source = ledger.Revision;
			ClassicAssert.IsTrue(KingdomPolityRules.TryEnsureResidentSuccessor(ledger, source,
				Settlement(), 7, "Mara", 1, 100L, out KingdomPolityPublicationResult first,
				out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRules.TryEnsureResidentSuccessor(ledger, source,
				Settlement(), 7, "Mara", 1, 101L, out KingdomPolityPublicationResult retry,
				out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
			ClassicAssert.IsTrue(KingdomPolityRules.TryEnsureResidentSuccessor(ledger, ledger.Revision,
				Settlement(), 9, "Otho", 2, 120L, out KingdomPolityPublicationResult _, out failure), failure);
			ClassicAssert.AreEqual(2, ledger.NamedFigures.Count); int active = 0, transferred = 0;
			for (int i = 0; i < ledger.NamedFigures.Count; i++)
			{
				KingdomPolityNamedFigureRecord figure = ledger.NamedFigures[i];
				if (figure.Phase == KingdomPolityFigurePhase.Active)
				{
					active++; ClassicAssert.AreEqual(9, figure.ResidentId);
					ClassicAssert.AreEqual(Settlement(), figure.ResidentSettlementId);
				}
				if (figure.Phase == KingdomPolityFigurePhase.Transferred)
				{
					transferred++; ClassicAssert.IsNotEmpty(figure.ConclusionRef);
					ClassicAssert.AreNotEqual(KingdomPolityFigurePhase.Dead, figure.Phase);
					ClassicAssert.AreEqual(0, figure.ResidentId);
					ClassicAssert.IsNull(figure.ResidentSettlementId);
				}
			}
			ClassicAssert.AreEqual(1, active); ClassicAssert.AreEqual(1, transferred);
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
			string cause = KingdomPolityRules.ActivationId("taf:fact:test:v1:",
				"successor-retire-test-v1", Realm());
			ClassicAssert.IsTrue(KingdomPolityRules.TryRetireResidentSuccessor(ledger,
				ledger.Revision, cause, 130L, out KingdomPolityPublicationResult retired,
				out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Applied, retired.Outcome);
			ClassicAssert.IsTrue(KingdomPolityRules.TryRetireResidentSuccessor(ledger,
				ledger.Revision - 1, cause, 131L, out retired, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retired.Outcome);
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
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

		private static KingdomPolityProfileRevision ExpressionProfile(string Style,
			string Relation)
		{
			List<KingdomPolityProfileFact> facts = new List<KingdomPolityProfileFact>
			{
				ExpressionFact(KingdomPolityProfileFactKind.Style, Style, "style"),
				ExpressionFact(KingdomPolityProfileFactKind.Relationship, Relation, "relation"),
				ExpressionFact(KingdomPolityProfileFactKind.Technology, "band=0", "technology")
			};
			facts.Sort((a, b) => string.CompareOrdinal(a.FactId, b.FactId));
			KingdomPolityProfileRevision result = Profile(0, "human", "guard");
			result.RulesVersion = KingdomPolityProfileRules.RulesVersion;
			result.FactsDigest = KingdomPolityRules.ActivationDigest("test-expression-v1",
				Style, Relation);
			result.DerivedFromFactIds.Clear();
			for (int i = 0; i < facts.Count; i++) result.DerivedFromFactIds.Add(facts[i].FactId);
			result.ExpressionCues = KingdomPolityProfileExpressionCatalogue.Resolve(facts, 0);
			return result;
		}

		private static KingdomPolityProfileRevision CurrentProfile(int Technology)
		{
			KingdomPolityFoundationFacts facts = new KingdomPolityFoundationFacts
			{
				RealmId = Realm(), FactionId = Realm(), DisplayName = "House Water",
				FounderName = "Ari", SettlementId = Settlement(), Vocation = "holding",
				Style = "common", Creed = "water covenant", Stage = 3,
				TechnologyBand = Technology, Population = 8, FoundedTick = 30L,
				OriginKeys = new List<string> { "salt-born" },
				CultureKeys = new List<string> { "Joppa" },
				SpeciesKeys = new List<string> { "human" }
			};
			ClassicAssert.IsTrue(KingdomPolityProfileRules.TryCreateCurrent(facts,
				out KingdomPolityProfileRevision profile, out string failure), failure);
			return profile;
		}

		private static KingdomPolityProfileFact ExpressionFact(KingdomPolityProfileFactKind Kind,
			string Value, string Suffix)
		{
			return new KingdomPolityProfileFact { Kind = Kind, ValueKey = Value,
				SourceRef = "taf:source:test:" + Suffix,
				FactId = "taf:fact:profile:test:" + Suffix };
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
			string realm = Realm(); ClassicAssert.IsTrue(KingdomPolityRules.TryCreate(realm,
				KingdomPolityImportPolicy.Off, out KingdomPolityLedger ledger, out string failure), failure);
			KingdomPolityFoundationFacts facts = new KingdomPolityFoundationFacts
			{
				RealmId = realm, FactionId = realm, DisplayName = "House Water",
				FounderName = "Ari", SettlementId = Settlement(), Style = "common",
				Stage = 0, TechnologyBand = 0, FoundedTick = 1L,
				SpeciesKeys = new List<string> { "human" }
			};
			ClassicAssert.IsTrue(KingdomPolityRules.TryPublishFoundation(ledger, ledger.Revision,
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
