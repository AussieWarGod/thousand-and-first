#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	// These tests supply exactAuthority explicitly. They do not prove engine ownership or selection completeness.
	public class KingdomSubsidenceRungRulesTests
	{
		[Test]
		public void FullPlanSnapshotsBothInputArraysAndEveryTransitionPreservesPriorState()
		{
			KingdomSubsidenceRungRoof[] roofs = { RungFixture.Roof(1), RungFixture.Roof(2, true) };
			KingdomSubsidenceRungWork first = RungFixture.Work(0, roofs: roofs);
			KingdomSubsidenceRungWork[] works = { first, RungFixture.Work(1) };
			KingdomSubsidenceRungPlan plan = RungFixture.Plan(works);
			string frozen = RungFixture.Wire(plan);
			roofs[0] = null; works[0] = null;
			Assert.AreEqual(frozen, RungFixture.Wire(plan));
			Assert.AreSame(first, plan.Works[0]);
			Assert.AreEqual(1, first.Roofs[0].ResidentId);
			Assert.Throws<NotSupportedException>(() => ((IList<KingdomSubsidenceRungWork>)plan.Works)[0] = null);
			Assert.Throws<NotSupportedException>(() => ((IList<KingdomSubsidenceRungRoof>)first.Roofs)[0] = null);
			Assert.IsTrue(KingdomSubsidenceRungRules.TryArmWear(plan, 0, out KingdomSubsidenceRungPlan armed));
			Assert.AreEqual(frozen, RungFixture.Wire(plan));
			Assert.AreEqual(KingdomSubsidenceEffectPhase.Intent, armed.Works[0].WearPhase);
			Assert.AreSame(first.Roofs[0], armed.Works[0].Roofs[0]);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void CompletedOrCancelledStepMatchesOneFrozenRungAtItsOriginalDueTick(bool cancelled)
		{
			KingdomSubsidenceStepBook book = RungFixture.Settling(cancelled);
			KingdomSubsidenceRungPlan plan = new KingdomSubsidenceRungPlan(book.Active.Id,
				book.RealmId, book.SettlementId, RungFixture.Zone, GrowthStage.City, GrowthStage.Town,
				book.Active.DueTick, RungFixture.Prepared, book.Active.Completed, new KingdomSubsidenceRungWork[0]);
			Assert.IsTrue(KingdomSubsidenceRungRules.Matches(plan, book));
			Assert.AreEqual(cancelled ? 1 : 5, plan.Departed);
			Assert.AreEqual(RungFixture.Due, plan.DueTick);
			Assert.IsFalse(KingdomSubsidenceRungRules.Matches(new KingdomSubsidenceRungPlan(plan.StepId,
				plan.RealmId, plan.SettlementId, plan.ZoneId, plan.From, GrowthStage.Village,
				plan.DueTick, plan.PreparedTick, plan.Departed, plan.Works), book));
			Assert.IsFalse(KingdomSubsidenceRungRules.Matches(new KingdomSubsidenceRungPlan(plan.StepId,
				plan.RealmId, plan.SettlementId, plan.ZoneId, plan.From, plan.To,
				plan.DueTick + 1, plan.PreparedTick, plan.Departed, plan.Works), book));
		}

		[Test]
		public void SelectionAndSeverityUseFullObjectIdentityAndOriginalDueTick()
		{
			string selected = RungFixture.ObjectId(0);
			KingdomSubsidenceRungWork work = RungFixture.ForObject(selected);
			Assert.IsTrue(KingdomSubsidenceRules.RollRuin(RungFixture.Settlement, selected,
				(ulong)RungFixture.Due, GrowthStage.City));
			int increment = KingdomSubsidenceRules.RolledRuinIncrement(RungFixture.Settlement,
				selected, (ulong)RungFixture.Due);
			Assert.AreEqual(KingdomMaterialRules.AddWear(work.BeforeWear, increment), work.AfterWear);
			Assert.IsTrue(KingdomSubsidenceRungRules.Valid(RungFixture.Plan(work)));
			Assert.IsFalse(KingdomSubsidenceRungRules.Valid(RungFixture.Plan(
				RungFixture.ForObject(RungFixture.ObjectId(0, selected: false)))));
			Assert.IsFalse(KingdomSubsidenceRungRules.Valid(RungFixture.Plan(
				RungFixture.CopyWork(work, afterWear: work.AfterWear - 1))));
			Assert.AreEqual(work.AfterWear, RungFixture.ForObject(selected).AfterWear);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void WearRequiresPersistedIntentAndExplicitExactAuthority(bool hadPart)
		{
			KingdomSubsidenceRungWork work = RungFixture.Work(before: hadPart ? 12 : 0, hadPart: hadPart);
			KingdomSubsidenceRungPlan plan = RungFixture.Plan(work);
			Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
				KingdomSubsidenceRungRules.WearAction(plan, 0, true, true, work.AfterWear));
			Assert.IsFalse(KingdomSubsidenceRungRules.TryProveWear(plan, 0, true, true, work.AfterWear, out _));
			Assert.IsTrue(KingdomSubsidenceRungRules.TryArmWear(plan, 0, out plan));
			Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
				KingdomSubsidenceRungRules.WearAction(plan, 0, false, true, work.AfterWear));
			Assert.IsFalse(KingdomSubsidenceRungRules.TryProveWear(plan, 0, false, true, work.AfterWear, out _));
			Assert.AreEqual(KingdomSubsidenceEffectAction.Apply,
				KingdomSubsidenceRungRules.WearAction(plan, 0, true, hadPart, work.BeforeWear));
			Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
				KingdomSubsidenceRungRules.WearAction(plan, 0, true, false, work.AfterWear));
			Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
				KingdomSubsidenceRungRules.WearAction(plan, 0, true, true, work.AfterWear + 1));
		}

		[TestCase("true")]
		[TestCase("false-after")]
		[TestCase("throw-after")]
		[TestCase("throw-before")]
		public void MeasuredWearTupleRecoversCallbackCutsWithoutApplyingWearTwice(string cut)
		{
			KingdomSubsidenceRungPlan plan = RungFixture.Plan(RungFixture.Work(before: 12));
			Assert.IsTrue(KingdomSubsidenceRungRules.TryArmWear(plan, 0, out plan));
			plan = RungFixture.RoundTrip(plan);
			int wear = plan.Works[0].BeforeWear, mutations = 0;
			Func<bool> callback = () =>
			{
				if (cut == "throw-before") throw new InvalidOperationException("before write");
				wear = plan.Works[0].AfterWear; mutations++;
				if (cut == "throw-after") throw new InvalidOperationException("after write");
				return cut != "false-after";
			};
			Assert.AreEqual(KingdomSubsidenceEffectAction.Apply,
				KingdomSubsidenceRungRules.WearAction(plan, 0, true, true, wear));
			try { callback(); } catch (InvalidOperationException) { }
			// A false return or exception is not the receipt; the measured tuple plus the supplied authority is.
			KingdomSubsidenceEffectAction recovery = KingdomSubsidenceRungRules.WearAction(plan, 0, true, true, wear);
			Assert.AreEqual(cut == "throw-before" ? KingdomSubsidenceEffectAction.Apply
				: KingdomSubsidenceEffectAction.Confirm, recovery);
			if (recovery == KingdomSubsidenceEffectAction.Apply) { wear = plan.Works[0].AfterWear; mutations++; }
			Assert.IsTrue(KingdomSubsidenceRungRules.TryProveWear(plan, 0, true, true, wear, out plan));
			plan = RungFixture.RoundTrip(plan);
			Assert.AreEqual(KingdomSubsidenceEffectAction.Confirm,
				KingdomSubsidenceRungRules.WearAction(plan, 0, true, true, wear));
			Assert.IsTrue(KingdomSubsidenceRungRules.TryProveWear(plan, 0, true, true, wear, out plan));
			Assert.AreEqual(1, mutations);
			Assert.IsTrue(KingdomSubsidenceRungRules.PhysicalComplete(plan));
			Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
				KingdomSubsidenceRungRules.WearAction(plan, 0, true, true, plan.Works[0].BeforeWear));
		}

		[Test]
		public void WearAndEachRoofHaveIndependentStrictlySequentialObligations()
		{
			KingdomSubsidenceRungPlan plan = RungFixture.Plan(RungFixture.Work(0,
				roofs: new[] { RungFixture.Roof(1), RungFixture.Roof(2) }), RungFixture.Work(1));
			Assert.IsFalse(KingdomSubsidenceRungRules.TryArmRoof(plan, 0, 0, out _));
			Assert.IsFalse(KingdomSubsidenceRungRules.TryArmWear(plan, 1, out _));
			plan = RungFixture.ProveWear(plan, 0);
			Assert.IsFalse(KingdomSubsidenceRungRules.PhysicalComplete(plan));
			Assert.IsFalse(KingdomSubsidenceRungRules.TryArmRoof(plan, 0, 1, out _));
			for (int i = 0; i < 2; i++)
			{
				Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
					KingdomSubsidenceRungRules.RoofAction(plan, 0, i, true, true, plan.DueTick, 0));
				Assert.IsTrue(KingdomSubsidenceRungRules.TryArmRoof(plan, 0, i, out plan));
				plan = RungFixture.RoundTrip(plan);
				Assert.AreEqual(KingdomSubsidenceEffectAction.Apply,
					KingdomSubsidenceRungRules.RoofAction(plan, 0, i, true, false, 0, 0));
				Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
					KingdomSubsidenceRungRules.RoofAction(plan, 0, i, false, true, plan.DueTick, 0));
				Assert.IsFalse(KingdomSubsidenceRungRules.TryProveRoof(plan, 0, i, false, true, plan.DueTick, 0, out _));
				Assert.IsFalse(KingdomSubsidenceRungRules.TryArmWear(plan, 1, out _));
				Assert.IsTrue(KingdomSubsidenceRungRules.TryProveRoof(plan, 0, i, true, true, plan.DueTick, 0, out plan));
				Assert.AreEqual(KingdomSubsidenceEffectPhase.Proved, plan.Works[0].WearPhase);
				Assert.IsTrue(KingdomSubsidenceRungRules.TryArmWear(plan, 0, out plan));
				Assert.AreEqual(KingdomSubsidenceEffectPhase.Proved, plan.Works[0].Roofs[i].Phase);
			}
			plan = RungFixture.ProveWear(plan, 1);
			Assert.IsTrue(KingdomSubsidenceRungRules.PhysicalComplete(plan));
		}

		[TestCase(0L, 0L)]
		[TestCase(100L, 0L)]
		[TestCase(5310L, 5320L)]
		public void AlreadyStandingRoofRetainsExactPriorTupleEvenAfterDueTick(long reached, long warned)
		{
			KingdomSubsidenceRungRoof roof = new KingdomSubsidenceRungRoof(1, "roof-body", true,
				reached, warned, KingdomSubsidenceEffectPhase.Prepared);
			KingdomSubsidenceRungPlan plan = RungFixture.ProveWear(
				RungFixture.Plan(RungFixture.Work(roofs: new[] { roof })), 0);
			Assert.IsTrue(KingdomSubsidenceRungRules.TryArmRoof(plan, 0, 0, out plan));
			Assert.AreEqual(KingdomSubsidenceEffectAction.Confirm,
				KingdomSubsidenceRungRules.RoofAction(plan, 0, 0, true, true, reached, warned));
			Assert.IsTrue(KingdomSubsidenceRungRules.TryProveRoof(plan, 0, 0, true, true, reached, warned, out plan));
			KingdomSubsidenceRungRoof stored = RungFixture.RoundTrip(plan).Works[0].Roofs[0];
			Assert.AreEqual(reached, stored.BeforeReached);
			Assert.AreEqual(warned, stored.BeforeWarned);
			Assert.AreEqual(KingdomSubsidenceEffectAction.Refuse,
				KingdomSubsidenceRungRules.RoofAction(plan, 0, 0, true, true, reached + 1, warned));
		}

		[TestCase("step")] [TestCase("realm")] [TestCase("settlement")] [TestCase("zone")]
		[TestCase("from")] [TestCase("to")] [TestCase("due")] [TestCase("prepared")]
		[TestCase("zero-departed")] [TestCase("over-departed")] [TestCase("null-works")]
		public void MalformedPlanHeaderRefusesWithoutMutation(string field)
		{
			KingdomSubsidenceRungPlan p = RungFixture.Plan(RungFixture.Work());
			string before = RungFixture.Wire(p);
			KingdomSubsidenceRungPlan bad = new KingdomSubsidenceRungPlan(
				field == "step" ? "bad" : p.StepId, field == "realm" ? "" : p.RealmId,
				field == "settlement" ? null : p.SettlementId, field == "zone" ? "" : p.ZoneId,
				field == "from" ? GrowthStage.Camp : p.From, field == "to" ? GrowthStage.Village : p.To,
				field == "due" ? KingdomSubsidenceStepRules.StepTicks - 1 : p.DueTick,
				field == "prepared" ? p.DueTick - 1 : p.PreparedTick,
				field == "zero-departed" ? 0 : field == "over-departed" ? 6 : p.Departed,
				field == "null-works" ? null : p.Works);
			Assert.IsFalse(KingdomSubsidenceRungRules.Valid(bad));
			Assert.IsFalse(KingdomSubsidenceRungCodec.TryEncode(bad, out _));
			Assert.AreEqual(before, RungFixture.Wire(p));
		}

		[TestCase("null-work")] [TestCase("duplicate-work")] [TestCase("work-order")]
		[TestCase("work-id")] [TestCase("wear-phase")] [TestCase("wear-frontier")]
		[TestCase("null-roof")] [TestCase("duplicate-resident")] [TestCase("duplicate-body")]
		[TestCase("roof-order")] [TestCase("roof-phase")] [TestCase("roof-before-wear")]
		[TestCase("roof-frontier")] [TestCase("off-ticks")] [TestCase("future-tick")]
		[TestCase("no-crossing")] [TestCase("missing-plot")]
		public void InvalidIdentityOrEffectFrontierIsNotNormalized(string field)
		{
			KingdomSubsidenceRungWork a = RungFixture.Work(0), b = RungFixture.Work(1);
			KingdomSubsidenceRungRoof first = RungFixture.Roof(1), second = RungFixture.Roof(2);
			KingdomSubsidenceRungPlan plan;
			switch (field)
			{
				case "null-work": plan = RungFixture.Plan((KingdomSubsidenceRungWork)null); break;
				case "duplicate-work": plan = RungFixture.Plan(a, a); break;
				case "work-order": plan = RungFixture.Plan(b, a); break;
				case "work-id": plan = RungFixture.Plan(RungFixture.CopyWork(a, workId: a.WorkId ^ 1)); break;
				case "wear-phase": plan = RungFixture.Plan(a.With((KingdomSubsidenceEffectPhase)9, a.Roofs)); break;
				case "wear-frontier": plan = RungFixture.Plan(a, b.With(KingdomSubsidenceEffectPhase.Intent, b.Roofs)); break;
				case "null-roof": plan = RungFixture.Plan(RungFixture.Work(roofs: new KingdomSubsidenceRungRoof[] { null })); break;
				case "duplicate-resident": plan = RungFixture.Plan(RungFixture.Work(roofs: new[] { first, first })); break;
				case "duplicate-body": plan = RungFixture.Plan(RungFixture.Work(roofs: new[] { first,
					new KingdomSubsidenceRungRoof(2, first.BodyObjectId, false, 0, 0, first.Phase) })); break;
				case "roof-order": plan = RungFixture.Plan(RungFixture.Work(roofs: new[] { second, first })); break;
				case "roof-phase": plan = RungFixture.Plan(RungFixture.Work(roofs: new[] { first.With((KingdomSubsidenceEffectPhase)9) })); break;
				case "roof-before-wear": plan = RungFixture.Plan(RungFixture.Work(roofs: new[] { first.With(KingdomSubsidenceEffectPhase.Intent) })); break;
				case "roof-frontier": plan = RungFixture.Plan(RungFixture.Work(roofs: new[] { first,
					second.With(KingdomSubsidenceEffectPhase.Intent) }, phase: KingdomSubsidenceEffectPhase.Proved)); break;
				case "off-ticks": plan = RungFixture.Plan(RungFixture.Work(roofs: new[] {
					new KingdomSubsidenceRungRoof(1, "body", false, 1, 0, first.Phase) })); break;
				case "future-tick": plan = RungFixture.Plan(RungFixture.Work(roofs: new[] {
					new KingdomSubsidenceRungRoof(1, "body", true, RungFixture.Prepared + 1, 0, first.Phase) })); break;
				case "no-crossing": plan = RungFixture.Plan(RungFixture.Work(before: 60, roofs: new[] { first })); break;
				default: plan = RungFixture.Plan(RungFixture.CopyWork(RungFixture.Work(roofs: new[] { first }), plot: "")); break;
			}
			Assert.IsFalse(KingdomSubsidenceRungRules.Valid(plan));
			Assert.IsFalse(KingdomSubsidenceRungRules.PhysicalComplete(plan));
			Assert.IsFalse(KingdomSubsidenceRungCodec.TryEncode(plan, out _));
		}

		[TestCase(null)] [TestCase("")] [TestCase("bad\nname")] [TestCase("bad\0name")]
		public void RequiredTextRejectsNullEmptyAndControlCharacters(string text)
		{
			KingdomSubsidenceRungWork work = RungFixture.Work();
			Assert.IsFalse(KingdomSubsidenceRungRules.Valid(RungFixture.Plan(RungFixture.CopyWork(work, name: text, replaceName: true))));
			Assert.IsFalse(KingdomSubsidenceRungRules.Text(text, 512, false));
		}

		[TestCase(0xD800, "")] [TestCase(0xDC00, "")] [TestCase(0xD800, "x")]
		public void UnpairedUnicodeIsRejectedFromActualRuntimeCodeUnits(int codeUnit, string suffix)
		{
			// Attribute metadata uses UTF-8; construct the malformed UTF-16 only after discovery.
			string text = new string((char)codeUnit, 1) + suffix;
			KingdomSubsidenceRungWork work = RungFixture.Work();
			Assert.IsFalse(KingdomSubsidenceRungRules.Valid(RungFixture.Plan(
				RungFixture.CopyWork(work, name: text, replaceName: true))));
			Assert.IsFalse(KingdomSubsidenceRungRules.Text(text, 512, false));
		}

		[Test]
		public void PairedUnicodeAndDelimitersRoundTripWithoutChangingFullIdentity()
		{
			string id = RungFixture.ObjectId(0, suffix: "-\ud83c\udfe0:|");
			KingdomSubsidenceRungPlan plan = RungFixture.Plan(RungFixture.CopyWork(
				RungFixture.ForObject(id), name: "Roof \ud83c\udfe0:|", replaceName: true));
			Assert.IsTrue(KingdomSubsidenceRungRules.Valid(plan));
			Assert.AreEqual(id, RungFixture.RoundTrip(plan).Works[0].ObjectId);
			Assert.AreEqual("Roof \ud83c\udfe0:|", RungFixture.RoundTrip(plan).Works[0].Name);
		}

		[Test]
		public void NestedNullsAndRawRoofBoundsRefuseWithoutDefaulting()
		{
			KingdomSubsidenceRungWork work = RungFixture.Work();
			Assert.IsFalse(KingdomSubsidenceRungRules.Valid(RungFixture.Plan(
				new KingdomSubsidenceRungWork(work.WorkId, work.ObjectId, work.Blueprint, work.PlotId,
					work.DesignStamp, work.Name, work.X, work.Y, work.HadWearPart, work.BeforeWear,
					work.AfterWear, work.WearPhase, null))));
			foreach (KingdomSubsidenceRungRoof roof in new[] {
				new KingdomSubsidenceRungRoof(0, "body", false, 0, 0, KingdomSubsidenceEffectPhase.Prepared),
				new KingdomSubsidenceRungRoof(-1, "body", false, 0, 0, KingdomSubsidenceEffectPhase.Prepared),
				new KingdomSubsidenceRungRoof(1, null, false, 0, 0, KingdomSubsidenceEffectPhase.Prepared),
				new KingdomSubsidenceRungRoof(1, "", false, 0, 0, KingdomSubsidenceEffectPhase.Prepared),
				new KingdomSubsidenceRungRoof(1, "body", true, -1, 0, KingdomSubsidenceEffectPhase.Prepared),
				new KingdomSubsidenceRungRoof(1, "body", true, 0, -1, KingdomSubsidenceEffectPhase.Prepared),
				new KingdomSubsidenceRungRoof(1, "body", true, 0, RungFixture.Prepared + 1, KingdomSubsidenceEffectPhase.Prepared) })
			{
				KingdomSubsidenceRungPlan bad = RungFixture.Plan(RungFixture.Work(roofs: new[] { roof }));
				Assert.IsFalse(KingdomSubsidenceRungRules.Valid(bad));
				Assert.AreSame(roof, bad.Works[0].Roofs[0]);
				Assert.IsFalse(KingdomSubsidenceRungCodec.TryEncode(bad, out _));
			}
		}

		[Test]
		public void MissingPlanAndOutOfRangeIndicesRefuseInsteadOfAdvancing()
		{
			Assert.IsFalse(KingdomSubsidenceRungRules.Valid(null));
			Assert.IsFalse(KingdomSubsidenceRungRules.TryArmWear(null, 0, out _));
			KingdomSubsidenceRungPlan plan = RungFixture.Plan(RungFixture.Work());
			foreach (int index in new[] { -1, 1, int.MaxValue })
			{
				Assert.IsFalse(KingdomSubsidenceRungRules.TryArmWear(plan, index, out _));
				Assert.IsFalse(KingdomSubsidenceRungRules.TryArmRoof(plan, 0, index, out _));
			}
		}
	}

	internal static class RungFixture
	{
		internal const long Due = 5300, Prepared = 5500;
		internal const string Zone = "JoppaWorld.12.24.1.1.10";
		internal static readonly string Realm = KingdomIdentityRules.RealmPrefix + new string('a', 64);
		internal static readonly string Settlement = KingdomIdentityRules.SettlementPrefix + new string('b', 64);
		internal static string ObjectId(int ordinal, bool selected = true, string suffix = "")
		{
			for (int i = 0, found = 0; i < 100000; i++)
			{
				string id = "rung-work-" + i.ToString("D6", CultureInfo.InvariantCulture) + suffix;
				if (KingdomSubsidenceRules.RollRuin(Settlement, id, (ulong)Due, GrowthStage.City) != selected) continue;
				if (found++ == ordinal) return id;
			}
			throw new InvalidOperationException("No deterministic fixture identity found.");
		}
		internal static KingdomSubsidenceRungRoof Roof(int id, bool standing = false)
			=> new KingdomSubsidenceRungRoof(id, "roof-body-" + id, standing, standing ? Due + 10 : 0,
				standing ? Due + 20 : 0, KingdomSubsidenceEffectPhase.Prepared);
		internal static KingdomSubsidenceRungWork Work(int ordinal = 0, int before = 39, bool hadPart = true,
			IEnumerable<KingdomSubsidenceRungRoof> roofs = null, KingdomSubsidenceEffectPhase phase = KingdomSubsidenceEffectPhase.Prepared)
			=> ForObject(ObjectId(ordinal), before, hadPart, roofs, phase);
		internal static KingdomSubsidenceRungWork ForObject(string id, int before = 39, bool hadPart = true,
			IEnumerable<KingdomSubsidenceRungRoof> roofs = null, KingdomSubsidenceEffectPhase phase = KingdomSubsidenceEffectPhase.Prepared)
			=> new KingdomSubsidenceRungWork(KingdomCityRules.StableId(id), id, "r_KingdomHouse",
				"plot-id", "design-stamp", "Fixture roof", 12, 8, hadPart, before,
				KingdomMaterialRules.AddWear(before, KingdomSubsidenceRules.RolledRuinIncrement(Settlement, id, (ulong)Due)),
				phase, roofs ?? new KingdomSubsidenceRungRoof[0]);
		internal static KingdomSubsidenceRungWork CopyWork(KingdomSubsidenceRungWork w,
			int? workId = null, int? afterWear = null, string plot = null, string name = null, bool replaceName = false)
			=> new KingdomSubsidenceRungWork(workId ?? w.WorkId, w.ObjectId, w.Blueprint, plot ?? w.PlotId,
				w.DesignStamp, replaceName ? name : w.Name, w.X, w.Y, w.HadWearPart, w.BeforeWear,
				afterWear ?? w.AfterWear, w.WearPhase, w.Roofs, w.ReleasePhase, w.ReleaseBefore, w.ReleaseAfter);
		internal static KingdomSubsidenceRungPlan Plan(params KingdomSubsidenceRungWork[] works)
			=> new KingdomSubsidenceRungPlan("taf:subsidence-step:v1:" + new string('c', 64), Realm,
				Settlement, Zone, GrowthStage.City, GrowthStage.Town, Due, Prepared, 5, works);
		internal static string Wire(KingdomSubsidenceRungPlan plan)
		{
			Assert.IsTrue(KingdomSubsidenceRungCodec.TryEncode(plan, out string wire));
			return wire;
		}
		internal static KingdomSubsidenceRungPlan RoundTrip(KingdomSubsidenceRungPlan plan)
		{
			string wire = Wire(plan);
			Assert.IsTrue(KingdomSubsidenceRungCodec.TryDecode(wire, out KingdomSubsidenceRungPlan decoded));
			Assert.AreEqual(wire, Wire(decoded));
			return decoded;
		}
		internal static KingdomSubsidenceRungPlan ProveWear(KingdomSubsidenceRungPlan plan, int index)
		{
			Assert.IsTrue(KingdomSubsidenceRungRules.TryArmWear(plan, index, out plan));
			Assert.IsTrue(KingdomSubsidenceRungRules.TryProveWear(plan, index, true, true, plan.Works[index].AfterWear, out plan));
			return plan;
		}
		internal static KingdomSubsidenceStepBook Settling(bool cancelled)
		{
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(KingdomSubsidenceStepCodec.FreshWire, out KingdomSubsidenceStepBook book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(book, Realm, Settlement, out book));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(book, Due - KingdomSubsidenceStepRules.StepTicks,
				Due, GrowthStage.City, 5, out book, 0, "water"));
			for (int id = 1; id <= (cancelled ? 1 : 5); id++)
			{
				string body = "departure-body-" + id;
				KingdomResidentDepartureOperation op = new KingdomResidentDepartureOperation
				{
					Version = KingdomResidentDepartureOperation.CurrentVersion, Revision = 1,
					Phase = (int)KingdomResidentDeparturePhase.Prepared, RealmId = Realm, SettlementId = Settlement,
					ResidentId = id, BodyObjectId = body, ZoneId = Zone, ResidentName = "Fixture resident", PreparedTick = Due,
					OperationId = KingdomResidentDepartureRules.Id(Realm, Settlement, id, body, Due)
				};
				Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, op, out book));
				Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, op.OperationId, GrowthStage.Town, out book));
				Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, op.OperationId, out book));
			}
			if (cancelled) Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, Due + 100, 1, out book));
			return book;
		}
	}
}
#endif
