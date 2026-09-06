#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	// Executes the production driver with an injected custody port, not native objects or game saves.
	public sealed class KingdomSubsidenceReleaseDriverTests
	{
		[Test]
		public void DurableIntentPrecedesEveryLocalWriteAndMeasuredTargetPrecedesRelease()
		{
			Port port = new Port();
			string original = Wire(port.Plan);
			Assert.IsTrue(Resume(port));
			CollectionAssert.AreEqual(new[] { "publish:Intent", "write:0", "write:1", "write:2",
				"write:3", "publish:Released" }, port.Events);
			Assert.AreEqual(KingdomSubsidenceReleasePhase.Released, port.Plan.Works[0].ReleasePhase);
			Assert.IsTrue(KingdomSubsidenceRungRules.ReleasedComplete(port.Plan));
			Assert.AreNotEqual(original, Wire(port.Plan));
			AssertPrefix(port, 4);
			CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, port.Mutations);
		}

		[TestCase(0, false, false)] [TestCase(0, false, true)]
		[TestCase(0, true, false)] [TestCase(0, true, true)]
		[TestCase(1, false, false)] [TestCase(1, false, true)]
		[TestCase(1, true, false)] [TestCase(1, true, true)]
		[TestCase(2, false, false)] [TestCase(2, false, true)]
		[TestCase(2, true, false)] [TestCase(2, true, true)]
		[TestCase(3, false, false)] [TestCase(3, false, true)]
		[TestCase(3, true, false)] [TestCase(3, true, true)]
		public void EveryBeforeAndAfterWriteFalseOrThrowRetainsPrefixAndRetriesOnce(int field, bool after, bool throws)
		{
			Port port = new Port();
			Func<Port, int, bool> cut = (p, current) => current != field || Cut(throws);
			if (after) port.AfterWrite = cut; else port.BeforeWrite = cut;
			Assert.IsFalse(Resume(port));
			Assert.AreEqual(KingdomSubsidenceReleasePhase.Intent, port.Plan.Works[0].ReleasePhase);
			Assert.IsTrue(KingdomSubsidenceReleaseRules.Same(port.Initial, port.Plan.Works[0].ReleaseBefore));
			AssertPrefix(port, field + (after ? 1 : 0));
			port.ClearHooks(); port.ReloadParent();
			Assert.IsTrue(Resume(port));
			AssertPrefix(port, 4);
			CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, port.Mutations);
		}

		[TestCase(null)] [TestCase("retained telling")]
		public void SecondWriteCutKeepsNullableLineAndRecoverySkipsAnAlreadyNullTarget(string line)
		{
			Port port = new Port(line);
			port.AfterWrite = (p, field) => field != 1 || Cut(true);
			Assert.IsFalse(Resume(port));
			Assert.AreEqual(KingdomSubsidenceReleasePhase.Intent, port.Plan.Works[0].ReleasePhase);
			AssertPrefix(port, 2);
			Assert.AreEqual(line, port.Plan.Works[0].ReleaseBefore.Line);
			CollectionAssert.AreEqual(new[] { "publish:Intent", "write:0", "write:1" }, port.Events);
			port.ClearHooks(); port.ReloadParent(); port.Events.Clear();
			Assert.IsTrue(Resume(port));
			AssertPrefix(port, 4);
			CollectionAssert.AreEqual(line == null
				? new[] { "write:2", "publish:Released" }
				: new[] { "write:2", "write:3", "publish:Released" }, port.Events);
			CollectionAssert.AreEqual(new[] { 1, 1, 1, line == null ? 0 : 1 }, port.Mutations);
		}

		[TestCase(false, false, false)] [TestCase(false, false, true)]
		[TestCase(false, true, false)] [TestCase(false, true, true)]
		[TestCase(true, false, false)] [TestCase(true, false, true)]
		[TestCase(true, true, false)] [TestCase(true, true, true)]
		public void EveryParentPublicationCutKeepsOnlyActuallyPublishedAuthority(bool released, bool after, bool throws)
		{
			Port port = new Port();
			KingdomSubsidenceReleasePhase phase = released ? KingdomSubsidenceReleasePhase.Released
				: KingdomSubsidenceReleasePhase.Intent;
			Func<Port, KingdomSubsidenceRungPlan, bool> cut = (p, next) => next.Works[0].ReleasePhase != phase || Cut(throws);
			if (after) port.AfterPublish = cut; else port.BeforePublish = cut;
			Assert.IsFalse(Resume(port));
			Assert.AreEqual(after ? phase : released ? KingdomSubsidenceReleasePhase.Intent
				: KingdomSubsidenceReleasePhase.Pending, port.Plan.Works[0].ReleasePhase);
			AssertPrefix(port, released ? 4 : 0);
			if (!released) Assert.AreEqual(0, port.WriteAttempts);
			port.ClearHooks(); port.ReloadParent();
			if (released && after) port.CarrierPresent = false;
			int observations = port.Observations;
			Assert.IsTrue(Resume(port));
			if (released && after) Assert.AreEqual(observations, port.Observations);
			CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, port.Mutations);
		}

		[TestCase(false)] [TestCase(true)]
		public void PublicationReturningTrueWithoutChangingParentCannotAcknowledge(bool released)
		{
			Port port = new Port { LiePublish = released ? KingdomSubsidenceReleasePhase.Released
				: KingdomSubsidenceReleasePhase.Intent };
			Assert.IsFalse(Resume(port));
			Assert.AreEqual(released ? KingdomSubsidenceReleasePhase.Intent : KingdomSubsidenceReleasePhase.Pending,
				port.Plan.Works[0].ReleasePhase);
			AssertPrefix(port, released ? 4 : 0);
			port.LiePublish = null; port.ReloadParent();
			Assert.IsTrue(Resume(port));
			CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, port.Mutations);
		}

		[TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
		public void WriteReturningTrueWithoutMutationCannotAdvanceItsPrefix(int field)
		{
			Port port = new Port { LieWrite = field };
			Assert.IsFalse(Resume(port));
			Assert.AreEqual(KingdomSubsidenceReleasePhase.Intent, port.Plan.Works[0].ReleasePhase);
			AssertPrefix(port, field);
			port.LieWrite = -1; port.ReloadParent();
			Assert.IsTrue(Resume(port));
			CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, port.Mutations);
		}

		[Test]
		public void ForeignMutationAfterAWriteRefusesAndIsNeverRepairedByRetry()
		{
			Port port = new Port();
			port.AfterWrite = (p, field) =>
			{
				p.Receipt = Receipt(p.Receipt, line: "foreign replacement telling"); return true;
			};
			Assert.IsFalse(Resume(port));
			string wire = Wire(port.Plan);
			KingdomSubsidenceWearReceipt foreign = port.Receipt;
			int writes = port.WriteAttempts;
			port.ClearHooks(); port.ReloadParent();
			Assert.IsFalse(Resume(port));
			Assert.AreEqual(wire, Wire(port.Plan)); Assert.AreSame(foreign, port.Receipt);
			Assert.AreEqual(writes, port.WriteAttempts);
			Assert.AreEqual("foreign replacement telling", port.Receipt.Line);
		}

		[TestCase("observe")] [TestCase("write")] [TestCase("publish")]
		public void CallbackPlanSwapCannotBeOverwrittenOrReportedSuccessful(string callback)
		{
			Port port = new Port();
			Action<Port> swap = p => p.Stored = Foreign(p.Plan);
			if (callback == "observe") port.AfterObserve = p => { swap(p); return true; };
			if (callback == "write") port.AfterWrite = (p, field) => { swap(p); return true; };
			if (callback == "publish") port.AfterPublish = (p, next) => { swap(p); return true; };
			Assert.IsFalse(Resume(port));
			Assert.AreEqual(RungFixture.Zone + "-foreign", port.Plan.ZoneId);
			Assert.AreEqual(callback == "write" ? 1 : 0, port.WriteAttempts);
			Assert.AreNotEqual(KingdomSubsidenceReleasePhase.Released, port.Plan.Works[0].ReleasePhase);
		}

		[TestCase("owner")] [TestCase("carrier")] [TestCase("part")]
		public void MissingOwnerCarrierOrPartRefusesWithoutAnyPublicationOrWrite(string missing)
		{
			Port port = new Port();
			if (missing == "owner") port.OwnerPresent = false;
			if (missing == "carrier") port.CarrierPresent = false;
			if (missing == "part") port.PartPresent = false;
			string wire = Wire(port.Plan);
			Assert.IsFalse(Resume(port));
			Assert.AreEqual(wire, Wire(port.Plan)); Assert.AreSame(port.Initial, port.Receipt);
			Assert.AreEqual(0, port.Events.Count); Assert.AreEqual(0, port.WriteAttempts);
			Assert.AreEqual(missing == "owner" ? 0 : 1, port.Observations);
		}

		[TestCase("owner")] [TestCase("carrier")] [TestCase("part")]
		public void LossAfterLocalMutationRetainsIntentAndOnlyOwnedRestorationAllowsRetry(string missing)
		{
			Port port = new Port();
			port.AfterWrite = (p, field) =>
			{
				if (missing == "owner") p.OwnerPresent = false;
				if (missing == "carrier") p.CarrierPresent = false;
				if (missing == "part") p.PartPresent = false;
				return true;
			};
			Assert.IsFalse(Resume(port)); AssertPrefix(port, 1);
			string wire = Wire(port.Plan);
			port.ClearHooks();
			Assert.IsFalse(Resume(port)); Assert.AreEqual(wire, Wire(port.Plan));
			Assert.AreEqual(1, port.WriteAttempts);
			port.OwnerPresent = true; port.CarrierPresent = true; port.PartPresent = true; port.ReloadParent();
			Assert.IsTrue(Resume(port));
			CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, port.Mutations);
		}

		[Test]
		public void PersistedIntentAndMissingCarrierNeverInferReleaseFromAbsence()
		{
			Port port = new Port { BeforeWrite = (p, field) => false };
			Assert.IsFalse(Resume(port));
			port.ClearHooks(); port.ReloadParent(); port.CarrierPresent = false;
			string wire = Wire(port.Plan); int writes = port.WriteAttempts;
			Assert.IsFalse(Resume(port));
			Assert.AreEqual(KingdomSubsidenceReleasePhase.Intent, port.Plan.Works[0].ReleasePhase);
			Assert.AreEqual(wire, Wire(port.Plan)); Assert.AreEqual(writes, port.WriteAttempts);
		}

		[TestCase(false)] [TestCase(true)]
		public void ReleasedParentNeverObservesMissingCarrierButStillRequiresOwner(bool loseOwner)
		{
			Port port = new Port(); Assert.IsTrue(Resume(port)); port.ReloadParent();
			port.CarrierPresent = false; port.Receipt = null; port.OwnerPresent = !loseOwner;
			port.AfterObserve = p => throw new InvalidOperationException("released carrier must not be read");
			string wire = Wire(port.Plan); int observations = port.Observations, writes = port.WriteAttempts;
			int publications = port.Events.Count;
			Assert.AreEqual(!loseOwner, Resume(port));
			Assert.AreEqual(observations, port.Observations); Assert.AreEqual(writes, port.WriteAttempts);
			Assert.AreEqual(publications, port.Events.Count); Assert.AreEqual(wire, Wire(port.Plan));
		}

		[Test]
		public void AlreadyClearedReceiptNeedsFreshObservationAndTwoParentPublicationsButNoLocalWrites()
		{
			Port port = new Port();
			Assert.IsTrue(KingdomSubsidenceReleaseRules.TryPlan(port.Plan.StepId,
				port.Initial.BeforeWear, port.Initial.AfterWear, port.Initial, out port.Receipt));
			KingdomSubsidenceWearReceipt cleared = port.Receipt;
			Assert.IsTrue(Resume(port));
			Assert.Greater(port.Observations, 0); Assert.AreEqual(0, port.WriteAttempts);
			CollectionAssert.AreEqual(new[] { "publish:Intent", "publish:Released" }, port.Events);
			Assert.IsTrue(KingdomSubsidenceReleaseRules.Same(cleared, port.Plan.Works[0].ReleaseBefore));
			Assert.AreSame(cleared, port.Receipt);
		}

		[TestCase("null-port")] [TestCase("null-plan")] [TestCase("negative-index")]
		[TestCase("past-end")] [TestCase("physical-pending")]
		public void InvalidOrPhysicallyUnfinishedInputCannotPublishOrWrite(string input)
		{
			Port port = new Port(); int index = input == "negative-index" ? -1 : input == "past-end" ? 1 : 0;
			if (input == "null-plan") port.Stored = null;
			if (input == "physical-pending") port.Stored = RungFixture.Plan(RungFixture.Work());
			Assert.IsFalse(KingdomSubsidenceReleaseDriver.Resume(input == "null-port" ? null : port, index));
			Assert.IsNull(port.Violation); Assert.AreEqual(0, port.WriteAttempts);
			Assert.AreEqual(0, port.Events.Count); Assert.AreSame(port.Initial, port.Receipt);
		}

		private static bool Resume(Port port)
		{
			bool result = KingdomSubsidenceReleaseDriver.Resume(port, 0);
			Assert.IsNull(port.Violation, "A caught port-contract failure must not count as an expected refusal.");
			return result;
		}
		private static bool Cut(bool throws)
		{ if (throws) throw new InvalidOperationException("injected release cut"); return false; }
		private static string Wire(KingdomSubsidenceRungPlan plan) => RungFixture.Wire(plan);
		private static KingdomSubsidenceRungPlan Foreign(KingdomSubsidenceRungPlan plan)
			=> new KingdomSubsidenceRungPlan(plan.StepId, plan.RealmId, plan.SettlementId,
				RungFixture.Zone + "-foreign", plan.From, plan.To, plan.DueTick, plan.PreparedTick, plan.Departed, plan.Works);
		private static KingdomSubsidenceWearReceipt Receipt(KingdomSubsidenceWearReceipt value, string line)
			=> new KingdomSubsidenceWearReceipt(value.Phase, value.Id, value.Cause, value.BeforeWear,
				value.AfterWear, value.Wear, value.LastCause, value.LastCompletedId, line, value.MessageState);
		private static void AssertPrefix(Port port, int cut)
		{
			KingdomSubsidenceWearReceipt actual = port.Receipt, initial = port.Initial;
			Assert.AreEqual(cut >= 1 ? port.Plan.StepId : initial.LastCompletedId, actual.LastCompletedId);
			Assert.AreEqual(cut >= 2 ? (int)KingdomWearIncidentPhase.None : initial.Phase, actual.Phase);
			Assert.AreEqual(cut >= 3 ? null : initial.Id, actual.Id);
			Assert.AreEqual(cut >= 4 ? null : initial.Line, actual.Line);
			Assert.AreEqual(initial.Cause, actual.Cause); Assert.AreEqual(initial.Wear, actual.Wear);
			Assert.AreEqual(initial.BeforeWear, actual.BeforeWear); Assert.AreEqual(initial.AfterWear, actual.AfterWear);
			Assert.AreEqual(initial.LastCause, actual.LastCause); Assert.AreEqual(initial.MessageState, actual.MessageState);
		}

		private sealed class Port : IKingdomSubsidenceReleasePort
		{
			internal KingdomSubsidenceRungPlan Stored;
			internal readonly KingdomSubsidenceWearReceipt Initial;
			internal KingdomSubsidenceWearReceipt Receipt;
			internal bool OwnerPresent = true, CarrierPresent = true, PartPresent = true;
			internal string Violation;
			internal int Observations, WriteAttempts, LieWrite = -1;
			internal KingdomSubsidenceReleasePhase? LiePublish;
			internal readonly int[] Mutations = new int[4];
			internal readonly List<string> Events = new List<string>();
			internal Func<Port, bool> AfterObserve;
			internal Func<Port, int, bool> BeforeWrite, AfterWrite;
			internal Func<Port, KingdomSubsidenceRungPlan, bool> BeforePublish, AfterPublish;
			public KingdomSubsidenceRungPlan Plan => Stored;
			public bool Current => OwnerPresent;

			internal Port(string line = "retained telling")
			{
				Stored = RungFixture.ProveWear(RungFixture.Plan(RungFixture.Work()), 0);
				KingdomSubsidenceRungWork work = Stored.Works[0];
				Initial = Receipt = new KingdomSubsidenceWearReceipt((int)KingdomWearIncidentPhase.Mutated,
					Stored.StepId, (int)KingdomWearRules.WearCause.Subsidence, work.BeforeWear, work.AfterWear,
					work.AfterWear, (int)KingdomWearRules.WearCause.Subsidence, "prior-incident", line,
					(int)KingdomWearSinkDisposition.Lost);
			}
			public bool TryObserve(out KingdomSubsidenceWearReceipt receipt)
			{
				Observations++; receipt = null;
				if (!Current || !CarrierPresent || !PartPresent) return false;
				receipt = Receipt;
				return AfterObserve == null || AfterObserve(this);
			}
			public bool Publish(KingdomSubsidenceRungPlan expected, KingdomSubsidenceRungPlan next)
			{
				Events.Add("publish:" + next.Works[0].ReleasePhase);
				if (!Current || !ReferenceEquals(Stored, expected)) return false;
				if (BeforePublish != null && !BeforePublish(this, next)) return false;
				if (LiePublish != next.Works[0].ReleasePhase)
				{
					if (!KingdomSubsidenceRungCodec.TryEncode(next, out string wire)
						|| !KingdomSubsidenceRungCodec.TryDecode(wire, out Stored))
					{ Violation = "publication must store an actual canonical parent"; return false; }
				}
				return AfterPublish == null || AfterPublish(this, next);
			}
			public bool Write(int field, KingdomSubsidenceWearReceipt expected, KingdomSubsidenceWearReceipt target)
			{
				WriteAttempts++;
				if (!Current || !CarrierPresent || !PartPresent
					|| !KingdomSubsidenceReleaseRules.Same(Receipt, expected)) return false;
				if (field < 0 || field > 3 || Stored.Works[0].ReleasePhase != KingdomSubsidenceReleasePhase.Intent)
				{ Violation = "local mutation preceded a durable release intent"; return false; }
				if (BeforeWrite != null && !BeforeWrite(this, field)) return false;
				Events.Add("write:" + field);
				if (LieWrite != field)
				{
					Receipt = new KingdomSubsidenceWearReceipt(field == 1 ? target.Phase : Receipt.Phase,
						field == 2 ? target.Id : Receipt.Id, Receipt.Cause, Receipt.BeforeWear, Receipt.AfterWear,
						Receipt.Wear, Receipt.LastCause, field == 0 ? target.LastCompletedId : Receipt.LastCompletedId,
						field == 3 ? target.Line : Receipt.Line, Receipt.MessageState);
					Mutations[field]++;
				}
				return AfterWrite == null || AfterWrite(this, field);
			}
			internal void ReloadParent()
			{
				Assert.IsTrue(KingdomSubsidenceRungCodec.TryEncode(Stored, out string wire));
				Assert.IsTrue(KingdomSubsidenceRungCodec.TryDecode(wire, out Stored));
			}
			internal void ClearHooks()
			{ BeforeWrite = null; AfterWrite = null; BeforePublish = null; AfterPublish = null; AfterObserve = null; }
		}
	}
}
#endif
