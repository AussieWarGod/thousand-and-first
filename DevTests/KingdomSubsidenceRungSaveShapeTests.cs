#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomSubsidenceRungSaveShapeTests
	{
		[TestCase(false, 0)] [TestCase(true, 0)]
		[TestCase(false, 1)] [TestCase(true, 1)]
		public void ExactProductionSelectedSetReturnsOnlyTheOriginalCompanion(bool selected, int ordinal)
		{
			Fixture fixture = new Fixture(selected, ordinal);
			string wire = RungFixture.Wire(fixture.Plan);
			ClassicAssert.IsTrue(KingdomSubsidenceRungSaveShape.TryMatch(fixture.Plan,
				fixture.Primary.ObjectId, fixture.Heart.ObjectId, out KingdomSubsidenceRungWork companion));
			if (selected) ClassicAssert.AreSame(fixture.Plan.Works[1], companion);
			else ClassicAssert.IsNull(companion);
			ClassicAssert.AreEqual(wire, RungFixture.Wire(fixture.Plan));
		}

		[TestCase("null-primary")] [TestCase("empty-primary")]
		[TestCase("null-heart")] [TestCase("empty-heart")]
		[TestCase("same")] [TestCase("reversed")]
		[TestCase("wrong-primary")] [TestCase("wrong-heart")] [TestCase("uppercase-primary")]
		public void CallerIdentitiesMustBeDistinctExactAndOrdinallyOrdered(string fault)
		{
			Fixture fixture = new Fixture(true);
			string primary = fixture.Primary.ObjectId, heart = fixture.Heart.ObjectId;
			switch (fault)
			{
				case "null-primary": primary = null; break;
				case "empty-primary": primary = ""; break;
				case "null-heart": heart = null; break;
				case "empty-heart": heart = ""; break;
				case "same": heart = primary; break;
				case "reversed": primary = fixture.Heart.ObjectId; heart = fixture.Primary.ObjectId; break;
				case "wrong-primary": primary += "-foreign"; break;
				case "wrong-heart": heart += "-foreign"; break;
				case "uppercase-primary": primary = primary.ToUpperInvariant(); break;
			}
			Refuses(fixture.Plan, primary, heart, fixture.Heart);
		}

		[TestCase("null-plan")] [TestCase("bad-step")]
		[TestCase("null-works")] [TestCase("null-work")]
		public void MalformedWholePlansRefuseBeforeInspectingTheExpectedRows(string fault)
		{
			Fixture fixture = new Fixture(true);
			KingdomSubsidenceRungPlan plan = fixture.Plan;
			if (fault == "null-plan") plan = null;
			if (fault == "bad-step") plan = new KingdomSubsidenceRungPlan("invalid", plan.RealmId,
				plan.SettlementId, plan.ZoneId, plan.From, plan.To, plan.DueTick, plan.PreparedTick, plan.Departed, plan.Works);
			if (fault == "null-works") plan = Replan(plan, null);
			if (fault == "null-work") plan = Replan(plan, new[] { fixture.Primary, null });
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.Valid(plan));
			Refuses(plan, fixture);
		}

		[Test]
		public void OtherwiseValidTownToSteadingPlanIsNotTheCityToTownWitness()
		{
			Fixture fixture = new Fixture(true, 0, GrowthStage.Town);
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.Valid(fixture.Plan));
			Refuses(fixture.Plan, fixture);
		}

		[Test]
		public void OmittedSelectedHeartRefusesEvenThoughTheRemainingPlanIsProductionValid()
		{
			Fixture fixture = new Fixture(true);
			KingdomSubsidenceRungPlan plan = Replan(fixture.Plan, new[] { fixture.Primary });
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.Valid(plan));
			Refuses(plan, fixture);
		}

		[Test]
		public void InjectedUnselectedHeartCannotBecomeAnOptionalSecondWork()
		{
			Fixture fixture = new Fixture(false);
			KingdomSubsidenceRungPlan plan = Replan(fixture.Plan, new[] { fixture.Primary, fixture.Heart });
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.Valid(plan));
			Refuses(plan, fixture);
		}

		[TestCase("extra", true)] [TestCase("foreign-heart", true)]
		[TestCase("duplicate-primary", false)] [TestCase("duplicate-heart", false)]
		[TestCase("reversed", false)] [TestCase("missing-primary", true)] [TestCase("empty", true)]
		public void EveryUnexpectedWorkSetRefuses(string fault, bool productionValid)
		{
			Fixture fixture = new Fixture(true);
			List<KingdomSubsidenceRungWork> rows = new List<KingdomSubsidenceRungWork>(fixture.Plan.Works);
			switch (fault)
			{
				case "extra": rows.Add(RungFixture.ForObject(Find("zz-extra-", true, 0, GrowthStage.City), 0, false)); break;
				case "foreign-heart": rows[1] = RungFixture.ForObject(Find("taf-heart-v1-", true, 1, GrowthStage.City), 0, false); break;
				case "duplicate-primary": rows.Insert(1, fixture.Primary); break;
				case "duplicate-heart": rows.Add(fixture.Heart); break;
				case "reversed": rows.Reverse(); break;
				case "missing-primary": rows.RemoveAt(0); break;
				case "empty": rows.Clear(); break;
			}
			KingdomSubsidenceRungPlan plan = Replan(fixture.Plan, rows);
			ClassicAssert.AreEqual(productionValid, KingdomSubsidenceRungRules.Valid(plan));
			Refuses(plan, fixture);
		}

		[TestCase("toggle-part")] [TestCase("wear-prepared")] [TestCase("wear-intent")]
		[TestCase("release-pending")] [TestCase("release-released")]
		[TestCase("no-roofs")] [TestCase("extra-roof")]
		[TestCase("roof-prepared")] [TestCase("roof-intent")]
		public void PrimaryMustRemainTheExactProvedWearAndSingleRoofUnderIntent(string fault)
		{
			Fixture fixture = new Fixture(true);
			KingdomSubsidenceRungWork primary = Change(fixture.Primary, fixture.Plan.StepId, fault);
			Refuses(Replan(fixture.Plan, new[] { primary, fixture.Heart }), fixture);
		}

		[TestCase("toggle-part", true)] [TestCase("preworn-part", true)]
		[TestCase("before-wear", false)] [TestCase("severity", false)]
		[TestCase("wear-intent", true)] [TestCase("wear-proved", true)]
		[TestCase("release-intent", false)] [TestCase("release-released", false)]
		[TestCase("before-receipt", false)] [TestCase("after-receipt", false)]
		[TestCase("extra-roof", false)]
		public void CompanionMustBeUntouchedUnwornPreparedAndWithoutReceiptsOrRoofs(string fault, bool productionValid)
		{
			Fixture fixture = new Fixture(true);
			KingdomSubsidenceRungWork heart = Change(fixture.Heart, fixture.Plan.StepId, fault);
			KingdomSubsidenceRungPlan plan = Replan(fixture.Plan, new[] { fixture.Primary, heart });
			ClassicAssert.AreEqual(productionValid, KingdomSubsidenceRungRules.Valid(plan));
			Refuses(plan, fixture);
		}

		private static KingdomSubsidenceRungWork Change(KingdomSubsidenceRungWork work, string stepId, string fault)
		{
			bool hadPart = work.HadWearPart;
			int before = work.BeforeWear, after = work.AfterWear;
			KingdomSubsidenceEffectPhase wear = work.WearPhase;
			KingdomSubsidenceReleasePhase release = work.ReleasePhase;
			KingdomSubsidenceWearReceipt releaseBefore = work.ReleaseBefore, releaseAfter = work.ReleaseAfter;
			List<KingdomSubsidenceRungRoof> roofs = new List<KingdomSubsidenceRungRoof>(work.Roofs);
			switch (fault)
			{
				case "toggle-part": hadPart = !hadPart; break;
				case "preworn-part": hadPart = true; before = 1; after = After(work.ObjectId, before); break;
				case "before-wear": before = 1; after = After(work.ObjectId, before); break;
				case "severity": after++; break;
				case "wear-prepared": wear = KingdomSubsidenceEffectPhase.Prepared; break;
				case "wear-intent": wear = KingdomSubsidenceEffectPhase.Intent; break;
				case "wear-proved": wear = KingdomSubsidenceEffectPhase.Proved; break;
				case "release-pending": release = KingdomSubsidenceReleasePhase.Pending; releaseBefore = null; releaseAfter = null; break;
				case "release-intent": release = KingdomSubsidenceReleasePhase.Intent; break;
				case "release-released": release = KingdomSubsidenceReleasePhase.Released; break;
				case "before-receipt": releaseBefore = Receipt(stepId, work); break;
				case "after-receipt": releaseAfter = Receipt(stepId, work); break;
				case "no-roofs": roofs.Clear(); break;
				case "extra-roof": roofs.Add(RungFixture.Roof(2).With(KingdomSubsidenceEffectPhase.Proved)); break;
				case "roof-prepared": roofs[0] = roofs[0].With(KingdomSubsidenceEffectPhase.Prepared); break;
				case "roof-intent": roofs[0] = roofs[0].With(KingdomSubsidenceEffectPhase.Intent); break;
				default: throw new ArgumentException("Unknown fixture mutation.", nameof(fault));
			}
			return new KingdomSubsidenceRungWork(work.WorkId, work.ObjectId, work.Blueprint, work.PlotId,
				work.DesignStamp, work.Name, work.X, work.Y, hadPart, before, after, wear, roofs,
				release, releaseBefore, releaseAfter);
		}

		private static void Refuses(KingdomSubsidenceRungPlan plan, Fixture fixture)
			=> Refuses(plan, fixture.Primary.ObjectId, fixture.Heart.ObjectId, fixture.Heart);
		private static void Refuses(KingdomSubsidenceRungPlan plan, string primary, string heart,
			KingdomSubsidenceRungWork prior)
		{
			KingdomSubsidenceRungWork companion = prior;
			ClassicAssert.IsFalse(KingdomSubsidenceRungSaveShape.TryMatch(plan, primary, heart, out companion));
			ClassicAssert.IsNull(companion);
		}

		private static KingdomSubsidenceRungPlan Replan(KingdomSubsidenceRungPlan plan,
			IEnumerable<KingdomSubsidenceRungWork> works)
			=> new KingdomSubsidenceRungPlan(plan.StepId, plan.RealmId, plan.SettlementId, plan.ZoneId,
				plan.From, plan.To, plan.DueTick, plan.PreparedTick, plan.Departed, works);
		private static int After(string id, int before)
			=> KingdomMaterialRules.AddWear(before,
				KingdomSubsidenceRules.RolledRuinIncrement(RungFixture.Settlement, id, (ulong)RungFixture.Due));
		private static KingdomSubsidenceWearReceipt Receipt(string stepId, KingdomSubsidenceRungWork work)
			=> new KingdomSubsidenceWearReceipt((int)KingdomWearIncidentPhase.Mutated, stepId,
				(int)KingdomWearRules.WearCause.Subsidence, work.BeforeWear, work.AfterWear, work.AfterWear,
				(int)KingdomWearRules.WearCause.Subsidence, null, null, 0);
		private static string Find(string prefix, bool selected, int ordinal, GrowthStage stage)
		{
			for (int i = 0, found = 0; i < 4096; i++)
			{
				string id = prefix + i.ToString("D6", CultureInfo.InvariantCulture);
				if (KingdomSubsidenceRules.RollRuin(RungFixture.Settlement, id, (ulong)RungFixture.Due, stage) != selected) continue;
				if (found++ == ordinal) return id;
			}
			throw new InvalidOperationException("No deterministic shape fixture identity found.");
		}

		private sealed class Fixture
		{
			internal readonly KingdomSubsidenceRungWork Primary, Heart;
			internal readonly KingdomSubsidenceRungPlan Plan;
			internal Fixture(bool selected, int ordinal = 0, GrowthStage from = GrowthStage.City)
			{
				string primary = Find("native-rung-hut:", true, ordinal, from);
				string heart = Find("taf-heart-v1-", selected, ordinal, from);
				ClassicAssert.Less(string.CompareOrdinal(primary, heart), 0);
				Heart = RungFixture.ForObject(heart, 0, false);
				KingdomSubsidenceRungWork work = RungFixture.ForObject(primary,
					KingdomLodgingRules.CondemnedWearPercent - 1, true,
					new[] { RungFixture.Roof(1).With(KingdomSubsidenceEffectPhase.Proved) }, KingdomSubsidenceEffectPhase.Proved);
				KingdomSubsidenceRungPlan basis = RungFixture.Plan(work);
				KingdomSubsidenceRungPlan prepared = new KingdomSubsidenceRungPlan(basis.StepId, basis.RealmId,
					basis.SettlementId, basis.ZoneId, from, from - 1, basis.DueTick, basis.PreparedTick, 1,
					selected ? new[] { work, Heart } : new[] { work });
				ClassicAssert.IsTrue(KingdomSubsidenceRungRules.Valid(prepared));
				ClassicAssert.IsTrue(KingdomSubsidenceRungRules.TryArmRelease(prepared, 0, true,
					Receipt(prepared.StepId, work), out KingdomSubsidenceRungPlan intent));
				Plan = intent; Primary = Plan.Works[0];
				ClassicAssert.IsTrue(KingdomSubsidenceRungRules.Valid(Plan));
			}
		}
	}
}
#endif
