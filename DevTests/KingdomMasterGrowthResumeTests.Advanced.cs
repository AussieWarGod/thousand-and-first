#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// Engine-free protocol cases. Callback facts below are fixture inputs, not native evidence.
	public sealed partial class KingdomMasterGrowthResumeTests
	{
		[Test]
		public void ResumeRetainsEarnedDebtFrozenPayloadAndExactChildOwners()
		{
			KingdomLifecycleBook parent = Active();
			KingdomGrowthBook growth = parent.Growth;
			FreezeResumeHead(growth);
			AdvanceResumeCadence(growth, 160L);
			ClassicAssert.AreEqual(3UL, growth.ArrivalOrdinalHighWater);
			ClassicAssert.AreEqual(2UL, growth.ArrivalDebtRanges[0].Count);
			string children = ResumeChildBytes(growth);
			object[] references = ResumeChildReferences(growth);
			Publish(parent, Prepare(parent, 170L, 270L));
			ClassicAssert.AreEqual(children, ResumeChildBytes(growth));
			AssertResumeChildReferences(references, growth);
			ClassicAssert.AreEqual(120L, growth.NextArrivalTick, "earned head stays ahead of new cadence");
			ClassicAssert.AreEqual(290L, growth.ArrivalCadenceNextDueTick);
			ClassicAssert.AreEqual(2L, growth.ArrivalRateEpoch);
			AdvanceResumeCadence(growth, 289L);
			ClassicAssert.AreEqual(3UL, growth.ArrivalOrdinalHighWater);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryRetireGrowthArrivalOpportunity(growth,
				growth.ArrivalOpportunity));
			ClassicAssert.AreEqual(140L, growth.NextArrivalTick);
			ClassicAssert.AreEqual(1UL, growth.ArrivalOrdinalRetiredThrough);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		public void ByteEquivalentDebtOpportunityLeaseAndResourceReplacementRefuses(int replacement)
		{
			KingdomLifecycleBook parent = Active();
			KingdomGrowthBook growth = parent.Growth;
			FreezeResumeHead(growth); AdvanceResumeCadence(growth, 160L);
			PrepareResumeArrival(growth, 160L);
			KingdomMasterGrowthResumePlan plan = Prepare(parent, 170L, 270L);
			byte[] before = Wire(growth);
			KingdomGrowthBook copy = KingdomLifecycleWireCodec.ReadGrowthPayload(before);
			if (replacement == 0) growth.ArrivalDebtRanges[0] = copy.ArrivalDebtRanges[0];
			if (replacement == 1) growth.ArrivalOpportunity = copy.ArrivalOpportunity;
			if (replacement == 2) growth.ArrivalOp.ClockLease = copy.ArrivalOp.ClockLease;
			if (replacement == 3) growth.Resources[0] = copy.Resources[0];
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
			CollectionAssert.AreEqual(before, Wire(growth));
			ClassicAssert.IsFalse(plan.CanPublish(parent, out _));
			ClassicAssert.IsFalse(plan.TryPublish(parent, out _));
			CollectionAssert.AreEqual(before, Wire(growth));
		}

		[TestCase(false, 0)]
		[TestCase(false, 1)]
		[TestCase(false, 2)]
		[TestCase(true, 0)]
		[TestCase(true, 1)]
		[TestCase(true, 2)]
		public void OpenArrivalClockCutsSurviveResumeThenRetireAndRestart(bool modern, int cut)
		{
			KingdomLifecycleBook parent = Active(modern);
			KingdomGrowthBook growth = parent.Growth;
			if (modern) FreezeResumeHead(growth);
			KingdomGrowthOperation operation = PrepareResumeArrival(growth, 120L);
			if (cut > 0) BeginResumeClock(growth, operation, 121L);
			if (cut > 1) ClassicAssert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(growth,
				operation, operation.ClockLease.After));
			ReloadResumeGrowth(parent); growth = parent.Growth; operation = growth.ArrivalOp;
			string children = ResumeChildBytes(growth);
			object[] references = ResumeChildReferences(growth);
			long clock = growth.NextArrivalTick;
			Publish(parent, Prepare(parent, 130L, 230L));
			ClassicAssert.AreEqual(children, ResumeChildBytes(growth));
			AssertResumeChildReferences(references, growth);
			ClassicAssert.AreEqual(clock, growth.NextArrivalTick);
			ClassicAssert.AreEqual(modern, growth.ArrivalCadenceResumePending);
			if (cut == 0) BeginResumeClock(growth, operation, 231L);
			if (cut < 2) ClassicAssert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(growth,
				operation, operation.ClockLease.After));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.Sinks, 232L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.Terminal, 233L));
			ReloadResumeGrowth(parent); growth = parent.Growth; operation = growth.ArrivalOp;
			string failure;
			if (modern) ClassicAssert.IsTrue(KingdomLifecycleRules.TryTransitionGrowthArrivalCadenceForRetirement(
				growth, growth.ArrivalOpportunity, 233L, 234L, 20L, 0, 3, out failure), failure);
			ClassicAssert.IsTrue(KingdomLifecycleRules.RetireGrowth(growth, operation, 234L));
			if (modern) ClassicAssert.IsTrue(KingdomLifecycleRules.TryRetireGrowthArrivalOpportunity(growth,
				growth.ArrivalOpportunity));
			else ClassicAssert.IsTrue(KingdomLifecycleRules.TryBindHistoricalGrowthArrivalCadence(growth,
				234L, 20L, 0, 3, out failure), failure);
			ClassicAssert.IsNull(growth.ArrivalOp);
			ClassicAssert.AreEqual(1L, growth.ArrivalRetiredThrough);
			ClassicAssert.AreEqual(1, growth.RecentProofs.Count);
			ClassicAssert.IsNull(growth.Resources[0].ActiveOperationId);
			ClassicAssert.AreEqual(254L, growth.NextArrivalTick);
			ClassicAssert.IsFalse(growth.ArrivalCadenceResumePending);
			byte[] retired = Wire(growth);
			ClassicAssert.IsFalse(KingdomLifecycleRules.RetireGrowth(growth, operation, 235L));
			CollectionAssert.AreEqual(retired, Wire(growth));
			ReloadResumeGrowth(parent); growth = parent.Growth;
			ulong high = growth.ArrivalOrdinalHighWater;
			AdvanceResumeCadence(growth, 253L);
			ClassicAssert.AreEqual(high, growth.ArrivalOrdinalHighWater);
			AdvanceResumeCadence(growth, 254L);
			ClassicAssert.AreEqual(high + 1UL, growth.ArrivalOrdinalHighWater);
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
		}

		[TestCase(false, false)]
		[TestCase(false, true)]
		[TestCase(true, false)]
		[TestCase(true, true)]
		public void CandidateOnlyPreparedOrCreateIntentRetainsExactReceiptAndCanContinue(bool modern, bool intent)
		{
			// Candidate payloads use the production semantic-plan version, unlike arbitrary cadence epochs.
			KingdomLifecycleBook parent = Active(modern, 1);
			KingdomGrowthBook growth = parent.Growth;
			if (modern) FreezeResumeHead(growth, 1);
			string hash = new string('a', 64);
			KingdomGrowthArrivalCandidate candidate = modern
				? KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth, "resume-marker", "Settler",
					"resume-escrow", "zone-a", 120L, hash, hash, hash, 1,
					KingdomLifecycleRules.GrowthArrivalEventStreamId,
					KingdomLifecycleRules.GrowthArrivalEventKindCode, "Joppa", "water", "Ari",
					"1 of Nivvun, 1000 AR", 1, 1)
				: KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth, "resume-marker", "Settler",
					"resume-escrow", "zone-a", 120L, hash, hash, hash);
			ClassicAssert.NotNull(candidate);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(growth, candidate));
			if (intent) ClassicAssert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateCreate(growth, candidate, 121L));
			ReloadResumeGrowth(parent); growth = parent.Growth; candidate = growth.ArrivalCandidate;
			string children = ResumeChildBytes(growth);
			object[] references = ResumeChildReferences(growth);
			Publish(parent, Prepare(parent, 130L, 230L));
			ClassicAssert.AreEqual(children, ResumeChildBytes(growth));
			AssertResumeChildReferences(references, growth);
			ClassicAssert.AreEqual(120L, growth.NextArrivalTick);
			ClassicAssert.AreEqual(modern, growth.ArrivalCadenceResumePending);
			if (!intent) ClassicAssert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateCreate(growth, candidate, 231L));
			ClassicAssert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalCandidateCreate(growth, candidate,
				"resume-body", hash, hash, hash, hash, true, 232L));
			ClassicAssert.AreEqual(KingdomGrowthArrivalCandidatePhase.Escrowed, candidate.Phase);
			ReloadResumeGrowth(parent);
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		public void OpaqueCanonicalQuarantineAndStagedGrowthPublishAsExactNoOps(int kind)
		{
			KingdomLifecycleBook parent = Bound();
			if (kind == 0) parent.Growth = KingdomLifecycleWireCodec.ReadGrowthPayload(new byte[] { 1, 2, 3 });
			else if (kind == 1) parent.Growth = KingdomLifecycleWireCodec.ReadGrowthPayload(
				Wire(new KingdomGrowthBook { Quarantined = true, Fault = "fixture quarantine" }));
			else
			{
				using (var stream = new MemoryStream())
				{
					KingdomLifecycleWireCodec.WriteLifecycleV5Fixture(new BinaryWriter(stream), parent);
					stream.Position = 0L;
					parent = new KingdomLifecycleBook();
					KingdomLifecycleWireCodec.ReadLifecycle(new BinaryReader(stream), parent);
				}
				ClassicAssert.IsTrue(parent.Growth.MigrationPending);
			}
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
			KingdomGrowthBook growth = parent.Growth;
			ClassicAssert.AreEqual(kind < 2, growth.Quarantined);
			ClassicAssert.AreEqual(kind == 0, growth.OpaquePayload != null);
			byte[] before = Wire(growth);
			object[] references = ResumeChildReferences(growth);
			KingdomMasterGrowthResumePlan plan = Prepare(parent);
			ClassicAssert.IsFalse(plan.HasArrivalAuthority);
			ClassicAssert.IsTrue(plan.TryPublish(parent, out string failure), failure);
			ClassicAssert.AreSame(growth, parent.Growth);
			AssertResumeChildReferences(references, growth);
			CollectionAssert.AreEqual(before, Wire(growth));
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
			ClassicAssert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(parent));
			ClassicAssert.IsFalse(plan.TryPublish(parent, out _));
			CollectionAssert.AreEqual(before, Wire(growth));
		}

		[Test]
		public void RateEpochOverflowRefusesWithoutRewritingAnyEarnedEvidence()
		{
			KingdomLifecycleBook parent = Active();
			FreezeResumeHead(parent.Growth);
			parent.Growth.ArrivalRateEpoch = long.MaxValue; // Synthetic arithmetic boundary only.
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
			byte[] before = Wire(parent.Growth);
			object[] references = ResumeChildReferences(parent.Growth);
			ClassicAssert.IsFalse(KingdomMasterGrowthResumePlan.TryCreate(parent, 130L, 230L,
				true, true, 20L, 0, 3, out KingdomMasterGrowthResumePlan plan, out string failure));
			ClassicAssert.IsNull(plan); ClassicAssert.IsNotEmpty(failure);
			CollectionAssert.AreEqual(before, Wire(parent.Growth));
			AssertResumeChildReferences(references, parent.Growth);
		}

		private static void AdvanceResumeCadence(KingdomGrowthBook growth, long now, int rulesVersion = 3)
		{
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryAdvanceGrowthArrivalCadence(growth,
				now, 20L, 0, rulesVersion, out string failure), failure);
		}

		private static void FreezeResumeHead(KingdomGrowthBook growth, int rulesVersion = 3)
		{
			AdvanceResumeCadence(growth, 120L, rulesVersion);
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryFreezeGrowthArrivalOpportunity(growth, rulesVersion,
				KingdomLifecycleRules.GrowthArrivalEventStreamId,
				KingdomLifecycleRules.GrowthArrivalEventKindCode, false, "Settler", "Joppa", "water",
				"Ari", "1 of Nivvun, 1000 AR", out _));
		}

		private static KingdomGrowthOperation PrepareResumeArrival(KingdomGrowthBook growth, long tick)
		{
			KingdomGrowthOperation operation = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Arrival, null, tick);
			ClassicAssert.NotNull(operation);
			operation.ArrivalDisposition = KingdomGrowthArrivalDisposition.NoGround;
			ClassicAssert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, operation));
			return operation;
		}

		private static void BeginResumeClock(KingdomGrowthBook growth, KingdomGrowthOperation operation, long tick)
		{
			ClassicAssert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation, KingdomGrowthPhase.ClockIntent, tick));
			ClassicAssert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(growth, operation, operation.ClockLease.Before));
		}

		private static void ReloadResumeGrowth(KingdomLifecycleBook parent)
		{
			byte[] before = Wire(parent.Growth);
			parent.Growth = KingdomLifecycleWireCodec.ReadGrowthPayload(before);
			CollectionAssert.AreEqual(before, Wire(parent.Growth));
			ClassicAssert.IsTrue(KingdomLifecycleRules.CanOwnAuthority(parent));
		}

		private static string ResumeChildBytes(KingdomGrowthBook growth)
		{
			// Independent snapshot of arrival children, including mutable receipt states.
			return JsonSerializer.Serialize(new { growth.ArrivalOpportunity, growth.ArrivalDebtRanges,
				growth.ArrivalOp, growth.ArrivalCandidate, growth.Resources, growth.RecentProofs,
				growth.FieldOps, growth.CropRows, growth.FirstGuestTerminal },
				new JsonSerializerOptions { IncludeFields = true });
		}

		private static object[] ResumeChildReferences(KingdomGrowthBook growth)
		{
			var references = new List<object> { growth.OpaquePayload, growth.ArrivalOpportunity,
				growth.ArrivalDebtRanges, growth.Resources, growth.ArrivalOp, growth.ArrivalOp?.ClockLease,
				growth.ArrivalCandidate, growth.ArrivalCandidate?.CandidateLease, growth.ArrivalCandidate?.LodgingLease,
				growth.ArrivalCandidate?.EscrowLease, growth.ArrivalCandidate?.CreateStep,
				growth.FieldOps, growth.CropRows, growth.RecentProofs };
			references.AddRange(growth.ArrivalDebtRanges); references.AddRange(growth.Resources);
			return references.ToArray();
		}

		private static void AssertResumeChildReferences(object[] expected, KingdomGrowthBook growth)
		{
			object[] actual = ResumeChildReferences(growth);
			ClassicAssert.AreEqual(expected.Length, actual.Length);
			for (int i = 0; i < expected.Length; i++) ClassicAssert.AreSame(expected[i], actual[i], "child " + i);
		}
	}
}
#endif
