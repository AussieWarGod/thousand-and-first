#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	internal class KingdomStationActivityRulesTests
	{
		[TestCase(KingdomWorkKind.Growing, KingdomDayShape.Field, KingdomStationActivity.Tend)]
		[TestCase(KingdomWorkKind.Store, KingdomDayShape.Market, KingdomStationActivity.Sort)]
		[TestCase(KingdomWorkKind.Producer, KingdomDayShape.Craft, KingdomStationActivity.Craft)]
		[TestCase(KingdomWorkKind.Refiner, KingdomDayShape.Craft, KingdomStationActivity.Craft)]
		[TestCase(KingdomWorkKind.Power, KingdomDayShape.Yard, KingdomStationActivity.Maintain)]
		[TestCase(KingdomWorkKind.Construction, KingdomDayShape.Yard, KingdomStationActivity.Build)]
		public void EveryCurrentWorkHasADistinctLegibleAct(
			KingdomWorkKind kind, KingdomDayShape shape, KingdomStationActivity expected)
		{
			Assert.AreEqual(expected, KingdomStationActivityRules.For(kind, shape));
			Assert.IsTrue(KingdomStationActivityRules.Cue(expected).Exists);
		}

		[TestCase(KingdomDayShape.Watch, KingdomStationActivity.Watch)]
		[TestCase(KingdomDayShape.Shrine, KingdomStationActivity.Pray)]
		public void StandingPolicyShapesAlreadyHavePresentationSeams(
			KingdomDayShape shape, KingdomStationActivity expected)
		{
			Assert.AreEqual(expected, KingdomStationActivityRules.For(KingdomWorkKind.Other, shape));
		}

		[Test]
		public void AnUnknownOrHearthPostCannotInventWork()
		{
			Assert.AreEqual(KingdomStationActivity.None,
				KingdomStationActivityRules.For(KingdomWorkKind.Other, KingdomDayShape.Hearth));
			Assert.AreEqual(KingdomStationActivity.None,
				KingdomStationActivityRules.For((KingdomWorkKind)255, (KingdomDayShape)255));
			Assert.IsFalse(KingdomStationActivityRules.Cue((KingdomStationActivity)255).Exists);
		}

		[Test]
		public void EveryPublishedCueIsBoundedAndFormattingBalanced()
		{
			for (int value = (int)KingdomStationActivity.Tend;
				value <= (int)KingdomStationActivity.Pray; value++)
			{
				KingdomStationActivityCue cue = KingdomStationActivityRules.Cue((KingdomStationActivity)value);
				Assert.IsTrue(cue.Exists);
				Assert.LessOrEqual(cue.Text.Length, 24);
				Assert.IsTrue(cue.Text.StartsWith("*") && cue.Text.EndsWith("*"));
				Assert.AreNotEqual(' ', cue.Color);
			}
		}

		[Test]
		public void RuntimeUsesOnlyVanillasAttendedIdlePathAndCosmeticOutput()
		{
			string part = TestMain.ReadRepositoryText("Simulation/City/KingdomStationPart.cs");
			string claims = TestMain.ReadRepositoryText("Simulation/City/KingdomStations.Claims.cs");
			StringAssert.Contains("ID == IdleQueryEvent.ID", part);
			StringAssert.Contains("KingdomStations.Claim(ParentObject, this, E.Actor", part);
			StringAssert.Contains("new DelegateGoal", claims);
			StringAssert.Contains("actor.ParticleText(cue.Text, 0f, -0.2f, cue.Color", claims);
			StringAssert.Contains("PostOf(actor) == WorkId", claims);
			StringAssert.DoesNotContain("UseEnergy(", claims);
			StringAssert.DoesNotContain("Stat.Random", claims);
			StringAssert.DoesNotContain("KingdomMaterials.", claims);
			StringAssert.DoesNotContain("KingdomConstruction.", claims);
		}
	}
}
#endif
