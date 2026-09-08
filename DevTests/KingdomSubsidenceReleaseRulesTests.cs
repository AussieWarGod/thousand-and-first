#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// Executable value/transition tests; supplied authority does not prove engine custody or persistence.
	public sealed class KingdomSubsidenceReleaseRulesTests
	{
		private static readonly string Step = "taf:subsidence-step:v1:" + new string('c', 64);

		[TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
		[TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)]
		public void EqualityAndFrozenTargetRequireEveryExactField(int field)
		{
			KingdomSubsidenceWearReceipt before = Receipt(), copy = From(Values(before));
			ClassicAssert.IsTrue(KingdomSubsidenceReleaseRules.Same(before, copy));
			ClassicAssert.IsTrue(KingdomSubsidenceReleaseRules.Same(before, before));
			ClassicAssert.IsFalse(KingdomSubsidenceReleaseRules.Same(before, Different(before, field)));
			KingdomSubsidenceWearReceipt target = Target(before), foreign = Different(target, field);
			ClassicAssert.IsFalse(KingdomSubsidenceReleaseRules.ValidProof(Step, 12, 24, before, foreign));
			ClassicAssert.IsNull(KingdomSubsidenceReleaseRules.AfterWrite(before, foreign, 0));
			ClassicAssert.IsFalse(KingdomSubsidenceReleaseRules.TryNextWrite(before, foreign, before, out int next));
			ClassicAssert.AreEqual(-1, next);
			ClassicAssert.IsFalse(KingdomSubsidenceReleaseRules.TryNextWrite(before, target, foreign, out next));
			ClassicAssert.AreEqual(-1, next);
			CollectionAssert.AreEqual(Values(copy), Values(before));
		}

		[TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
		public void EachCutHasExactSequentialFieldsAndPreservesUnwrittenEvidence(int cut)
		{
			KingdomSubsidenceWearReceipt before = Receipt(), target = Target(before);
			object[] original = Values(before), final = Values(target);
			KingdomSubsidenceWearReceipt observed = KingdomSubsidenceReleaseRules.AfterWrite(before, target, cut);
			ClassicAssert.AreEqual(cut >= 1 ? Step : "previous incident", observed.LastCompletedId);
			ClassicAssert.AreEqual(cut >= 2 ? 0 : 3, observed.Phase);
			ClassicAssert.AreEqual(cut >= 3 ? null : Step, observed.Id);
			ClassicAssert.AreEqual(cut >= 4 ? null : "previous line", observed.Line);
			foreach (int field in new[] { 2, 3, 4, 5, 6, 9 }) ClassicAssert.AreEqual(original[field], Values(observed)[field]);
			ClassicAssert.IsTrue(KingdomSubsidenceReleaseRules.TryNextWrite(before, target, observed, out int next));
			ClassicAssert.AreEqual(cut, next);
			CollectionAssert.AreEqual(original, Values(before)); CollectionAssert.AreEqual(final, Values(target));
		}

		[Test]
		public void AllSixteenLocalWriteCombinationsAdmitOnlyTheFiveOrderedPrefixes()
		{
			KingdomSubsidenceWearReceipt before = Receipt(), target = Target(before);
			int[] order = { 7, 0, 1, 8 }, legal = { 0, 1, 3, 7, 15 };
			for (int mask = 0; mask < 16; mask++)
			{
				object[] values = Values(before), final = Values(target);
				for (int bit = 0; bit < 4; bit++) if ((mask & (1 << bit)) != 0) values[order[bit]] = final[order[bit]];
				int expected = Array.IndexOf(legal, mask);
				ClassicAssert.AreEqual(expected >= 0, KingdomSubsidenceReleaseRules.TryNextWrite(before, target,
					From(values), out int next), "write mask " + mask);
				ClassicAssert.AreEqual(expected, next, "write mask " + mask);
			}
		}

		[TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
		public void NoOpFieldsChooseLargestMatchingPrefix(bool completedAlready, bool lineAlreadyNull)
		{
			KingdomSubsidenceWearReceipt before = Receipt();
			if (completedAlready) before = Change(before, 7, Step);
			if (lineAlreadyNull) before = Change(before, 8, null);
			KingdomSubsidenceWearReceipt target = Target(before);
			for (int cut = 0; cut <= 4; cut++)
			{
				ClassicAssert.IsTrue(KingdomSubsidenceReleaseRules.TryNextWrite(before, target,
					KingdomSubsidenceReleaseRules.AfterWrite(before, target, cut), out int next));
				int expected = cut == 0 && completedAlready ? 1 : cut;
				if (cut >= 3 && lineAlreadyNull) expected = 4;
				ClassicAssert.AreEqual(expected, next);
			}
		}

		[TestCase(0, 0)] [TestCase(0, KingdomMaterialRules.MaxWearPercent)]
		[TestCase(KingdomMaterialRules.MaxWearPercent, KingdomMaterialRules.MaxWearPercent)]
		public void InclusiveWearBoundsRetainEveryLegalCauseAndMessageDisposition(int beforeWear, int afterWear)
		{
			for (int cause = 0; cause <= (int)KingdomWearRules.WearCause.Subsidence; cause++)
				for (int message = 0; message <= (int)KingdomWearSinkDisposition.Lost; message++)
				{
					KingdomSubsidenceWearReceipt before = Change(Change(Receipt(beforeWear, afterWear), 6, cause), 9, message);
					ClassicAssert.IsTrue(KingdomSubsidenceReleaseRules.TryPlan(Step, beforeWear, afterWear, before, out var target));
					ClassicAssert.IsTrue(KingdomSubsidenceReleaseRules.ValidProof(Step, beforeWear, afterWear, before, target));
					ClassicAssert.AreEqual(cause, target.LastCause); ClassicAssert.AreEqual(message, target.MessageState);
				}
		}

		[TestCase(0, -1)] [TestCase(0, 0)] [TestCase(0, 1)] [TestCase(0, 2)] [TestCase(0, 4)]
		[TestCase(0, 5)] [TestCase(0, 6)] [TestCase(0, 7)] [TestCase(0, 8)] [TestCase(0, 9)]
		[TestCase(1, null)] [TestCase(1, "")] [TestCase(1, "foreign incident")]
		[TestCase(2, -1)] [TestCase(2, 0)] [TestCase(2, 1)] [TestCase(2, 5)]
		[TestCase(3, -1)] [TestCase(3, 25)] [TestCase(3, 61)]
		[TestCase(4, -1)] [TestCase(4, 11)] [TestCase(4, 61)]
		[TestCase(5, -1)] [TestCase(5, 23)] [TestCase(5, 61)]
		[TestCase(6, -1)] [TestCase(6, 5)] [TestCase(9, -1)] [TestCase(9, 6)]
		public void MalformedOrForeignReceiptNeverProducesAReleaseProof(int field, object value)
		{
			Refuses(Change(Receipt(), field, value));
		}

		[TestCase(-1, 0)] [TestCase(0, -1)] [TestCase(24, 12)]
		[TestCase(61, 61)] [TestCase(0, 61)] [TestCase(int.MaxValue, int.MaxValue)]
		public void MatchingRequestedTupleStillRejectsInvalidWearBoundsOrReversal(int before, int after)
		{
			Refuses(Receipt(before, after), before, after);
		}

		[TestCase(null)] [TestCase("")] [TestCase("foreign")]
		[TestCase("taf:subsidence-step:v1:CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC")]
		public void StepIdentityAndBothRequestedWearValuesMustMatch(string step)
		{
			ClassicAssert.IsFalse(KingdomSubsidenceReleaseRules.TryPlan(step, 12, 24, Receipt(), out var target));
			ClassicAssert.IsNull(target);
			foreach (int bad in new[] { -1, 0, 13, 25, KingdomMaterialRules.MaxWearPercent + 1, int.MaxValue })
			{
				Refuses(Receipt(), bad, 24); Refuses(Receipt(), 12, bad);
			}
		}

		[TestCase(1)] [TestCase(7)] [TestCase(8)]
		public void NullableStringsNeverConflateAbsentAndEmpty(int field)
		{
			KingdomSubsidenceWearReceipt absent = Change(Receipt(), field, null), empty = Change(Receipt(), field, "");
			ClassicAssert.IsFalse(KingdomSubsidenceReleaseRules.Same(absent, empty));
			ClassicAssert.IsFalse(KingdomSubsidenceReleaseRules.Same(empty, absent));
			if (field == 1) { Refuses(absent); Refuses(empty); return; }
			Target(absent); Target(empty);
		}

		[TestCase(7)] [TestCase(8)]
		public void TextBoundsUnicodeAndControlChecksDoNotNormalizePriorEvidence(int field)
		{
			foreach (string text in new[] { null, "", "a:|\ud83c\udfe0", new string('x', KingdomWearRules.MaxSavedTextChars) })
			{
				KingdomSubsidenceWearReceipt before = Change(Receipt(), field, text);
				Target(before); ClassicAssert.AreEqual(text, Values(before)[field]);
			}
			foreach (string text in new[] { "bad\0text", "bad\ntext", "bad\u007ftext", "bad\u0085text",
				new string((char)0xD800, 1), new string((char)0xDC00, 1), new string((char)0xD800, 1) + "x",
				new string((char)0xD800, 2), new string('x', KingdomWearRules.MaxSavedTextChars + 1) })
				Refuses(Change(Receipt(), field, text));
		}

		[TestCase(-1)] [TestCase(5)] [TestCase(int.MinValue)] [TestCase(int.MaxValue)]
		public void InvalidCutsAndMissingReceiptsRefuseWithoutThrowing(int cut)
		{
			KingdomSubsidenceWearReceipt before = Receipt(), target = Target(before);
			ClassicAssert.IsNull(KingdomSubsidenceReleaseRules.AfterWrite(before, target, cut));
			ClassicAssert.IsTrue(KingdomSubsidenceReleaseRules.Same(null, null));
			ClassicAssert.IsFalse(KingdomSubsidenceReleaseRules.Same(null, before)); Refuses(null);
			foreach (var pair in new[] { new[] { before, null }, new[] { null, target }, new KingdomSubsidenceWearReceipt[2] })
			{
				ClassicAssert.IsFalse(KingdomSubsidenceReleaseRules.ValidProof(Step, 12, 24, pair[0], pair[1]));
				ClassicAssert.IsNull(KingdomSubsidenceReleaseRules.AfterWrite(pair[0], pair[1], 0));
				ClassicAssert.IsFalse(KingdomSubsidenceReleaseRules.TryNextWrite(pair[0], pair[1], before, out int next));
				ClassicAssert.AreEqual(-1, next);
			}
			ClassicAssert.IsFalse(KingdomSubsidenceReleaseRules.TryNextWrite(before, target, null, out int missing));
			ClassicAssert.AreEqual(-1, missing);
		}

		[Test]
		public void PhysicalCompletionStillNeedsExplicitReleaseIntentAndExactTargetProof()
		{
			KingdomSubsidenceRungPlan plan = RungFixture.Plan(RungFixture.Work());
			KingdomSubsidenceWearReceipt observed = Receipt(plan.Works[0].BeforeWear, plan.Works[0].AfterWear);
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.TryArmRelease(plan, 0, true, observed, out _));
			plan = RungFixture.ProveWear(plan, 0);
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.PhysicalComplete(plan));
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.ReleasedComplete(plan));
			string prior = RungFixture.Wire(plan);
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.TryProveRelease(plan, 0, true, observed, out _));
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.TryArmRelease(plan, 0, false, observed, out _));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.TryArmRelease(plan, 0, true, observed, out var intent));
			ClassicAssert.AreEqual(prior, RungFixture.Wire(plan));
			intent = RungFixture.RoundTrip(intent);
			KingdomSubsidenceRungWork row = intent.Works[0], copied = RungFixture.CopyWork(row);
			ClassicAssert.AreEqual(KingdomSubsidenceReleasePhase.Intent, copied.ReleasePhase);
			ClassicAssert.AreSame(row.ReleaseBefore, copied.ReleaseBefore); ClassicAssert.AreSame(row.ReleaseAfter, copied.ReleaseAfter);
			for (int cut = 0; cut < 4; cut++)
				ClassicAssert.IsFalse(KingdomSubsidenceRungRules.TryProveRelease(intent, 0, true,
					KingdomSubsidenceReleaseRules.AfterWrite(row.ReleaseBefore, row.ReleaseAfter, cut), out _));
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.TryProveRelease(intent, 0, false, row.ReleaseAfter, out _));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.TryProveRelease(intent, 0, true, row.ReleaseAfter, out plan));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.ReleasedComplete(RungFixture.RoundTrip(plan)));
			ClassicAssert.AreEqual(KingdomSubsidenceReleasePhase.Intent, intent.Works[0].ReleasePhase);
		}

		[Test]
		public void AlreadyClearedLiveObservationStillArmsNewIntentBeforeAcknowledgement()
		{
			KingdomSubsidenceRungPlan plan = RungFixture.ProveWear(RungFixture.Plan(RungFixture.Work()), 0);
			KingdomSubsidenceWearReceipt cleared = Target(Receipt(plan.Works[0].BeforeWear, plan.Works[0].AfterWear));
			KingdomSubsidenceWearReceipt target = Target(cleared);
			ClassicAssert.IsTrue(KingdomSubsidenceReleaseRules.Same(cleared, target));
			ClassicAssert.IsTrue(KingdomSubsidenceReleaseRules.TryNextWrite(cleared, target, cleared, out int next));
			ClassicAssert.AreEqual(4, next);
			ClassicAssert.AreEqual(KingdomSubsidenceReleasePhase.Pending, plan.Works[0].ReleasePhase);
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.TryProveRelease(plan, 0, true, cleared, out _));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.TryArmRelease(plan, 0, true, cleared, out plan));
			ClassicAssert.AreEqual(KingdomSubsidenceReleasePhase.Intent, plan.Works[0].ReleasePhase);
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.ReleasedComplete(plan));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.TryProveRelease(plan, 0, true, cleared, out plan));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.ReleasedComplete(plan));
			foreach (int field in new[] { 1, 8 }) Refuses(Change(cleared, field, ""), cleared.BeforeWear, cleared.AfterWear);
			Refuses(Change(cleared, 7, "foreign"), cleared.BeforeWear, cleared.AfterWear);
		}

		[Test]
		public void ReleaseAcknowledgementsFormTheirOwnPrefixAcrossPhysicallyCompletedWorks()
		{
			KingdomSubsidenceRungPlan plan = RungFixture.Plan(RungFixture.Work(0), RungFixture.Work(1));
			plan = RungFixture.ProveWear(RungFixture.ProveWear(plan, 0), 1);
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.PhysicalComplete(plan));
			KingdomSubsidenceWearReceipt second = Receipt(plan.Works[1].BeforeWear, plan.Works[1].AfterWear);
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.TryArmRelease(plan, 1, true, second, out _));
			foreach (KingdomSubsidenceReleasePhase phase in new[] { KingdomSubsidenceReleasePhase.Intent,
				KingdomSubsidenceReleasePhase.Released, (KingdomSubsidenceReleasePhase)255 })
				ClassicAssert.IsFalse(KingdomSubsidenceRungRules.Valid(plan.Replace(1,
					plan.Works[1].WithRelease(phase, second, Target(second)))));
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.Valid(plan.Replace(0,
				plan.Works[0].WithRelease(KingdomSubsidenceReleasePhase.Pending, second, Target(second)))));
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.ReleasedComplete(null));
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.TryArmRelease(null, 0, true, second, out _));
			foreach (int index in new[] { -1, 2, int.MaxValue })
			{
				ClassicAssert.IsFalse(KingdomSubsidenceRungRules.TryArmRelease(plan, index, true, second, out _));
				ClassicAssert.IsFalse(KingdomSubsidenceRungRules.TryProveRelease(plan, index, true, second, out _));
			}
			for (int index = 0; index < 2; index++)
			{
				KingdomSubsidenceRungWork row = plan.Works[index];
				ClassicAssert.IsTrue(KingdomSubsidenceRungRules.TryArmRelease(plan, index, true,
					Receipt(row.BeforeWear, row.AfterWear), out plan));
				if (index == 0) ClassicAssert.IsFalse(KingdomSubsidenceRungRules.TryArmRelease(plan, 1, true, second, out _));
				ClassicAssert.IsFalse(KingdomSubsidenceRungRules.ReleasedComplete(plan));
				ClassicAssert.IsTrue(KingdomSubsidenceRungRules.TryProveRelease(plan, index, true,
					plan.Works[index].ReleaseAfter, out plan));
				plan = RungFixture.RoundTrip(plan);
				ClassicAssert.AreEqual(index == 1, KingdomSubsidenceRungRules.ReleasedComplete(plan));
			}
		}

		private static KingdomSubsidenceWearReceipt Receipt(int before = 12, int after = 24)
			=> new KingdomSubsidenceWearReceipt(3, Step, (int)KingdomWearRules.WearCause.Subsidence,
				before, after, after, (int)KingdomWearRules.WearCause.Raid, "previous incident", "previous line", 3);
		private static KingdomSubsidenceWearReceipt Target(KingdomSubsidenceWearReceipt before)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceReleaseRules.TryPlan(Step, before.BeforeWear, before.AfterWear, before, out var target));
			ClassicAssert.IsTrue(KingdomSubsidenceReleaseRules.ValidProof(Step, before.BeforeWear, before.AfterWear, before, target));
			return target;
		}
		private static void Refuses(KingdomSubsidenceWearReceipt receipt, int before = 12, int after = 24)
		{
			object[] original = receipt == null ? null : Values(receipt);
			ClassicAssert.IsFalse(KingdomSubsidenceReleaseRules.TryPlan(Step, before, after, receipt, out var target));
			ClassicAssert.IsNull(target);
			ClassicAssert.IsFalse(KingdomSubsidenceReleaseRules.ValidProof(Step, before, after, receipt, Target(Receipt())));
			if (receipt != null) CollectionAssert.AreEqual(original, Values(receipt));
		}
		private static object[] Values(KingdomSubsidenceWearReceipt r)
			=> new object[] { r.Phase, r.Id, r.Cause, r.BeforeWear, r.AfterWear, r.Wear, r.LastCause, r.LastCompletedId, r.Line, r.MessageState };
		private static KingdomSubsidenceWearReceipt From(object[] v)
			=> new KingdomSubsidenceWearReceipt((int)v[0], (string)v[1], (int)v[2], (int)v[3], (int)v[4],
				(int)v[5], (int)v[6], (string)v[7], (string)v[8], (int)v[9]);
		private static KingdomSubsidenceWearReceipt Change(KingdomSubsidenceWearReceipt r, int field, object value)
		{
			object[] values = Values(r); values[field] = value; return From(values);
		}
		private static KingdomSubsidenceWearReceipt Different(KingdomSubsidenceWearReceipt r, int field)
		{
			object value = Values(r)[field];
			return Change(r, field, value is int ? (object)((int)value + 1) : (string)value + "!");
		}
	}
}
#endif
