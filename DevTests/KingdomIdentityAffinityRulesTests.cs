#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomIdentityAffinityRulesTests
	{
		private static KingdomIdentityAffinityRules.WorkerIdentity Mopango()
		{
			return new KingdomIdentityAffinityRules.WorkerIdentity("Mopango", "mopango",
				"finding weird artifacts", "communing with objects", null,
				"the Kasaphescence", null);
		}

		[Test]
		public void VanillaMopangoTextFavorsItsStatedPracticesAndLeavesFoodNeutral()
		{
			KingdomIdentityAffinityRules.WorkerIdentity identity = Mopango();
			ClassicAssert.AreEqual(130, identity.Affinity("knowledge"));
			ClassicAssert.AreEqual(120, identity.Affinity("craft"));
			ClassicAssert.AreEqual(100, identity.Affinity("food"));
			ClassicAssert.AreEqual("Mopango", identity.Culture);
			ClassicAssert.AreEqual("mopango", identity.Species);
		}

		[Test]
		public void AffinityIsNeutralForUnknownVocabularyAndClampedForDenseEvidence()
		{
			ClassicAssert.AreEqual(100, KingdomIdentityAffinityRules.Percent("other",
				"artifact machine chrome craft build", null, null, null, null));
			ClassicAssert.AreEqual(130, KingdomIdentityAffinityRules.Percent("craft",
				"artifact machine chrome craft build reinforce weave pottery ore", null,
				null, null, null));
			ClassicAssert.AreEqual(70, KingdomIdentityAffinityRules.Clamp(-100));
			ClassicAssert.AreEqual(130, KingdomIdentityAffinityRules.Clamp(1000));
			ClassicAssert.AreEqual(0, KingdomIdentityAffinityRules.Apply(0, 130));
			ClassicAssert.AreEqual(100, KingdomIdentityAffinityRules.Compose(100, 100));
			ClassicAssert.AreEqual(130, KingdomIdentityAffinityRules.Compose(120, 120));
			ClassicAssert.AreEqual(70, KingdomIdentityAffinityRules.Compose(80, 80));
		}

		[Test]
		public void FrozenExtensionAffinityUsesTheSameRankingAndOutcomeLane()
		{
			KingdomCrewRules.SettlerCapability[] pool = new[]
			{
				new KingdomCrewRules.SettlerCapability(20, 10, false),
				new KingdomCrewRules.SettlerCapability(16, 10, false)
			};
			KingdomCrewRules.CrewDemand[] demands = new[]
			{
				new KingdomCrewRules.CrewDemand(1, false,
					KingdomCrewRules.KindStrength, 16, "craft")
			};
			int[,] extensions = new int[1, 2] { { 70, 130 } };
			KingdomCrewRules.CrewOutcome[] result = KingdomCrewRules.AssignCrew(pool,
				demands, extensions);

			ClassicAssert.AreEqual(1, result[0].SettlerIndices[0],
				"extension affinity must enter the existing ablest-first ranking");
			ClassicAssert.AreEqual(16, result[0].BestCapability,
				"affinity changes assignment, never the raw tier/capability fact");
			ClassicAssert.AreEqual(130, result[0].IdentityAffinity);
		}

		[Test]
		public void IdentityChangesAssignmentButNeverChangesRawIntelligenceTierValue()
		{
			KingdomCrewRules.SettlerCapability ordinary =
				new KingdomCrewRules.SettlerCapability(16, 18, false);
			KingdomCrewRules.SettlerCapability mopango =
				new KingdomCrewRules.SettlerCapability(16, 18, false, Mopango());
			ClassicAssert.AreEqual(18, ordinary.ValueOf(KingdomCrewRules.KindIntelligence));
			ClassicAssert.AreEqual(18, mopango.ValueOf(KingdomCrewRules.KindIntelligence),
				"culture/species affinity must not skip the Intelligence tier ladder");

			KingdomCrewRules.CrewOutcome[] result = KingdomCrewRules.AssignCrew(
				new[] { ordinary, mopango },
				new[] { new KingdomCrewRules.CrewDemand(1, false,
					KingdomCrewRules.KindIntelligence, 18, "knowledge") });
			ClassicAssert.AreEqual(1, result[0].SettlerIndices[0]);
			ClassicAssert.AreEqual(130, result[0].IdentityAffinity);
			ClassicAssert.AreEqual(18, result[0].BestCapability);
		}

		[Test]
		public void OldConstructorsRemainExactlyNeutralAndStable()
		{
			KingdomCrewRules.CrewOutcome[] result = KingdomCrewRules.AssignCrew(
				new[]
				{
					new KingdomCrewRules.SettlerCapability(20, 10, false),
					new KingdomCrewRules.SettlerCapability(16, 10, false)
				},
				new[] { new KingdomCrewRules.CrewDemand(1, false,
					KingdomCrewRules.KindStrength, 16) });
			ClassicAssert.AreEqual(0, result[0].SettlerIndices[0]);
			ClassicAssert.AreEqual(100, result[0].IdentityAffinity);
			ClassicAssert.IsNull(result[0].WorkKind);
		}
	}
}
#endif
