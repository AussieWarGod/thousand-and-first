#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;
using ThousandAndFirst.Api;
using ThousandAndFirst.Simulation.City;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Tests
{
	/// <summary>External-shaped API-v3 fixture. It has no access to a system, zone, object, clock,
	/// sidecar carrier, or mutable row: only published contracts.</summary>
	[KingdomExtension]
	internal sealed class ExternalBehaviourFixture : IResourceKind, ICarrierKind, IJobKind,
		INetworkKind, IWorkBehaviour, IHappeningGenerator
	{
		public int ApiVersion { get { return KingdomApiRules.Version; } }

		public KingdomResourceDefinition[] Resources(KingdomCityReading City,
			KingdomBehaviourReading Model, IKingdomDraws Draws)
		{
			int capacity; Draws.TryBetween("ore-capacity", 0u, 100, 100, out capacity);
			return new[] { new KingdomResourceDefinition("ore", "ore", "FixtureOreStore",
				"ore-grid", "", 10L, capacity) };
		}

		public KingdomCarrierDefinition[] Carriers(KingdomCityReading City,
			KingdomBehaviourReading Model, IKingdomDraws Draws)
		{
			return new[] { new KingdomCarrierDefinition("mule", "Dromad Merchant", 2, 8) };
		}

		public KingdomJobPlan[] Jobs(KingdomCityReading City, KingdomBehaviourReading Model,
			IKingdomDraws Draws)
		{
			if (Model.JobCount > 0) return null;
			return new[] { new KingdomJobPlan("first-haul", "mule", "ore", 4,
				City.ProcessedThroughTick,
				new[] { new KingdomExtensionLeg("zone-a", 1, 1, 4, 1) },
				new[] { new KingdomResourceChange("ore", 6L) }) };
		}

		public KingdomNetworkPlan[] Networks(KingdomCityReading City, KingdomBehaviourReading Model,
			IKingdomDraws Draws)
		{
			return new[] { new KingdomNetworkPlan("ore-grid", "ore",
				new[]
				{
					new KingdomExtensionNetworkNode("shaft", "zone-a",
						KingdomExtensionNetworkRole.Source, 4, 0),
					new KingdomExtensionNetworkNode("mill", "zone-b",
						KingdomExtensionNetworkRole.Sink, 2, 1)
				},
				new[] { new KingdomExtensionNetworkEdge(0, 1, 3) }) };
		}

		public KingdomWorkAdvance[] Advance(KingdomCityReading City, KingdomBehaviourReading Model,
			IKingdomDraws Draws)
		{
			return new[] { new KingdomWorkAdvance(7, "crusher", 1L,
				City.ProcessedThroughTick + 1200L,
				new[] { new KingdomResourceChange("ore", 5L) },
				new[] { new KingdomMaterialisation("Copper Nugget", 1) }) };
		}

		public KingdomNotice[] Happen(KingdomCityReading City, long SinceTick, IKingdomDraws Draws)
		{
			int word; Draws.TryBetween("telling", 0u, 0, 0, out word);
			return new[] { new KingdomNotice("ore-song", City.ProcessedThroughTick,
				"the crusher sang over the new ore", "The crusher is singing.") };
		}
	}

	internal sealed class FixtureDraws : IKingdomDraws
	{
		internal int Calls;
		public bool TryBetween(string Lane, uint Ordinal, int Low, int High, out int Value)
		{
			Calls++;
			Value = Low;
			return !string.IsNullOrEmpty(Lane) && High >= Low;
		}
	}

	internal sealed class ExternalResourceEnvelopeJob
		: IKingdomComputation<KingdomCityReading, KingdomResourceDefinition[]>
	{
		private readonly bool throws;
		private readonly int draws;

		internal ExternalResourceEnvelopeJob(bool Throws, int Draws)
		{
			throws = Throws;
			draws = Draws;
		}

		public string Label { get { return "external-fixture:resource-envelope"; } }
		public KingdomBudgetLane Lane { get { return KingdomBudgetLane.Reckon; } }

		public bool TryRun(KingdomCityReading input, out KingdomResourceDefinition[] output,
			out KingdomComputeCounters counters, out KingdomCityFault fault)
		{
			if (throws) throw new InvalidOperationException("fixture throw");
			output = new ExternalBehaviourFixture().Resources(input,
				new KingdomBehaviourReading(null, null, null, null), new FixtureDraws());
			counters = new KingdomComputeCounters(0, output.Length, draws, 0, 0L);
			fault = KingdomCityFault.None;
			return true;
		}
	}

	[TestFixture]
	internal class KingdomBehaviourApiTests
	{
		private const string Owner = "External Fixture";

		private static KingdomCityReading City(long tick)
		{
			return new KingdomCityReading("Kavvat", "taf:settlement-a", tick,
				new KingdomStockReading(0, 0), new KingdomStockReading(0, 0),
				new KingdomStockReading(0, 0),
				new[]
				{
					new KingdomZoneReading("zone-a", default(KingdomStockReading),
						default(KingdomStockReading), default(KingdomStockReading), 0, 0, 0, 0, 0, tick),
					new KingdomZoneReading("zone-b", default(KingdomStockReading),
						default(KingdomStockReading), default(KingdomStockReading), 0, 0, 0, 0, 0, tick)
				},
				new[] { new KingdomWorkReading(7, "zone-a", "fixture-crusher", 100, 1,
					KingdomWorkClass.Refiner, 0, 0, tick) }, null);
		}

		[Test]
		public void ExternalFixtureExercisesAllFiveDimensionsAndCanonicalHappening()
		{
			ExternalBehaviourFixture fixture = new ExternalBehaviourFixture();
			FixtureDraws draws = new FixtureDraws();
			KingdomBehaviourState state = KingdomBehaviourState.Empty;
			KingdomBehaviourState next; int kept;

			Assert.IsTrue(KingdomBehaviourRules.TryApplyResources(state, Owner,
				fixture.Resources(City(0), state.Reading(), draws), out next, out kept));
			Assert.AreEqual(1, kept); state = next;
			int carrierCount;
			KingdomCarrierKindRow[] carriers = KingdomBehaviourRules.NormalizeCarriers(Owner,
				fixture.Carriers(City(0), state.Reading(), draws), out carrierCount);
			Assert.AreEqual(1, carrierCount);

			Assert.IsTrue(KingdomBehaviourRules.TryApplyNetworks(state, Owner,
				fixture.Networks(City(0), state.Reading(), draws), City(0), 0L, out next, out kept));
			Assert.AreEqual(1, kept); state = next;
			Assert.IsTrue(KingdomBehaviourRules.TryApplyWorks(state, Owner,
				fixture.Advance(City(0), state.Reading(), draws), City(0), 0L, out next, out kept));
			Assert.AreEqual(1, kept); state = next;
			Assert.IsTrue(KingdomBehaviourRules.TryApplyJobs(state, Owner,
				fixture.Jobs(City(0), state.Reading(), draws), carriers, City(0), 0L, out next, out kept));
			Assert.AreEqual(1, kept); state = next;

			KingdomResourceReading ore;
			Assert.IsTrue(state.TryResource(0, out ore));
			Assert.AreEqual("external-fixture:ore", ore.Key);
			Assert.AreEqual(11L, ore.Level, "10 initial + 5 work - 4 reserved cargo");
			KingdomWorkBehaviourReading work;
			Assert.IsTrue(state.TryWork(0, out work));
			Assert.AreEqual("Copper Nugget", work.OwedBlueprint);
			Assert.AreEqual(1, work.OwedCount);

			string pausedWire, resumedWire;
			Assert.IsTrue(KingdomBehaviourRules.TryEncode(state, out pausedWire));
			Assert.IsTrue(KingdomBehaviourRules.TryRebaseAfterPause(pausedWire, 100L, 1300L,
				out resumedWire));
			KingdomBehaviourState resumed;
			Assert.IsTrue(KingdomBehaviourRules.TryDecode(resumedWire, out resumed));
			KingdomExtensionNetworkReading resumedNetwork;
			Assert.IsTrue(resumed.TryNetwork(0, out resumedNetwork));
			Assert.AreEqual(1300L, resumedNetwork.ProcessedThroughTick,
				"paused days must not produce network stock on resume");
			KingdomWorkBehaviourReading resumedWork;
			Assert.IsTrue(resumed.TryWork(0, out resumedWork));
			Assert.AreEqual(2400L, resumedWork.NextTick,
				"a future work deadline keeps its remaining duration across pause");
			Assert.AreEqual(work.MaterialisationSequence, resumedWork.MaterialisationSequence);
			Assert.AreEqual(work.OwedCount, resumedWork.OwedCount,
				"committed physical debt survives the standing-clock rebase");
			KingdomExtensionJobReading resumedJob;
			Assert.IsTrue(resumed.Reading().TryJob(0, out resumedJob));
			Assert.AreEqual(6L, resumedJob.DueTick,
				"host-owned open jobs remain committed recovery");

			int completed, failed;
			Assert.IsTrue(KingdomBehaviourRules.TryCompleteJobs(state, 6L, out next,
				out completed, out failed));
			Assert.AreEqual(1, completed); Assert.AreEqual(0, failed); state = next;
			Assert.IsTrue(state.TryResource(0, out ore)); Assert.AreEqual(17L, ore.Level);

			Assert.IsTrue(KingdomBehaviourRules.TryApplyNetworks(state, Owner,
				fixture.Networks(City(1200), state.Reading(), draws), City(1200), 1200L,
				out next, out kept));
			state = next; Assert.IsTrue(state.TryResource(0, out ore));
			Assert.AreEqual(19L, ore.Level, "4 supplied - 2 served = 2 stored across one day");

			KingdomNotice[] notices = fixture.Happen(City(1200), 0L, draws);
			Assert.AreEqual("ore-song", notices[0].Kind);
			Assert.GreaterOrEqual(draws.Calls, 2, "fixture bypassed the supplied deterministic draw handle");
		}

		[Test]
		public void SidecarWireRoundTripsExactlyAndRejectsTrailingOrOversizedInput()
		{
			KingdomBehaviourState state, next; int kept;
			Assert.IsTrue(KingdomBehaviourRules.TryApplyResources(KingdomBehaviourState.Empty,
				Owner, new[] { new KingdomResourceDefinition("ore", "ore", "", "", "", 3, 9) },
				out state, out kept));
			string wire, second;
			Assert.IsTrue(KingdomBehaviourRules.TryEncode(state, out wire));
			Assert.IsTrue(KingdomBehaviourRules.TryDecode(wire, out next));
			Assert.IsTrue(KingdomBehaviourRules.TryEncode(next, out second));
			Assert.AreEqual(wire, second);
			Assert.IsFalse(KingdomBehaviourRules.TryDecode(wire + "AAAA", out next));
			Assert.IsFalse(KingdomBehaviourRules.TryRebaseAfterPause(wire + "AAAA", 10L,
				20L, out second), "resume must not silently default malformed authority");
			Assert.IsFalse(KingdomBehaviourRules.TryDecode(new string('A',
				((KingdomApiRules.MaxBehaviourModelBytes + 2) / 3) * 4 + 1), out next));
		}

		[Test]
		public void LegacyV1WireDefaultsReceiptGenerationAndRewritesCanonically()
		{
			Assert.AreEqual(16384, KingdomApiRules.LegacyBehaviourModelBytes);
			Assert.AreEqual(16896, KingdomApiRules.MaxBehaviourModelBytes);
			KingdomBehaviourState state; int kept;
			Assert.IsTrue(KingdomBehaviourRules.TryApplyWorks(KingdomBehaviourState.Empty, Owner,
				new[] { new KingdomWorkAdvance(7, "crusher", 1L, 1200L, null,
					new[] { new KingdomMaterialisation("Copper Nugget", 1) }) },
				City(0), 0L, out state, out kept));
			string current;
			Assert.IsTrue(KingdomBehaviourRules.TryEncode(state, out current));
			byte[] v2 = Convert.FromBase64String(current);
			byte[] v1 = new byte[v2.Length - sizeof(long)];
			Array.Copy(v2, v1, v1.Length);
			using (MemoryStream stream = new MemoryStream(v1))
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				stream.Position = sizeof(int);
				writer.Write(1);
			}
			KingdomBehaviourState migrated;
			Assert.IsTrue(KingdomBehaviourRules.TryDecode(Convert.ToBase64String(v1), out migrated));
			KingdomWorkBehaviourReading row;
			Assert.IsTrue(migrated.TryWork(0, out row));
			Assert.AreEqual(0L, row.MaterialisationSequence);
			string rewritten;
			Assert.IsTrue(KingdomBehaviourRules.TryEncode(migrated, out rewritten));
			Assert.AreNotEqual(Convert.ToBase64String(v1), rewritten);
			Assert.IsTrue(KingdomBehaviourRules.TryDecode(rewritten, out migrated));

			KingdomWorkBehaviourReading[] works =
				new KingdomWorkBehaviourReading[KingdomApiRules.MaxWorkBehavioursPerCity];
			for (int i = 0; i < works.Length; i++)
			{
				string owner = "owner" + (i / KingdomApiRules.MaxWorkBehavioursPerOwner);
				string key = owner + ":" + new string('a',
					KingdomApiRules.MaxBehaviourIdentifierLength - owner.Length - 1);
				works[i] = new KingdomWorkBehaviourReading(key, i + 1, 0L, 1L,
					new string('B', 100), 1, 0L);
			}
			KingdomBehaviourState nearLegacyCap = new KingdomBehaviourState(null, null, null, works);
			string legacyNearCap;
			Assert.IsTrue(KingdomBehaviourRules.TryEncodeLegacyV1ForTests(nearLegacyCap,
				out legacyNearCap));
			int legacyBytes = Convert.FromBase64String(legacyNearCap).Length;
			Assert.Greater(legacyBytes, KingdomApiRules.LegacyBehaviourModelBytes
				- KingdomApiRules.MaxWorkBehavioursPerCity * sizeof(long));
			Assert.LessOrEqual(legacyBytes, KingdomApiRules.LegacyBehaviourModelBytes);
			Assert.IsTrue(KingdomBehaviourRules.TryDecode(legacyNearCap, out migrated));
			Assert.AreEqual(KingdomApiRules.MaxWorkBehavioursPerCity, migrated.WorkCount);
			Assert.IsTrue(KingdomBehaviourRules.TryEncode(migrated, out rewritten),
				"every formerly valid v1 carrier must fit after v2 generation receipts expand it");
			int rewrittenBytes = Convert.FromBase64String(rewritten).Length;
			Assert.Greater(rewrittenBytes, KingdomApiRules.LegacyBehaviourModelBytes,
				"fixture must prove the old 16 KiB current cap would have frozen this save");
			Assert.LessOrEqual(rewrittenBytes, KingdomApiRules.MaxBehaviourModelBytes);
		}

		[Test]
		public void OwnerCapsAndCandidateBudgetCountMalformedSlots()
		{
			KingdomResourceDefinition[] offered = new KingdomResourceDefinition[100];
			for (int i = 0; i < offered.Length; i++)
				offered[i] = i < 31
					? new KingdomResourceDefinition("bad key " + i, "ore", "", "", "", 0, 1)
					: new KingdomResourceDefinition("good" + i, "ore", "", "", "", 0, 1);
			KingdomBehaviourState state; int kept;
			Assert.IsTrue(KingdomBehaviourRules.TryApplyResources(KingdomBehaviourState.Empty,
				Owner, offered, out state, out kept));
			Assert.AreEqual(1, kept, "only slot 31 is both inspected and valid");
			Assert.AreEqual(1, state.ResourceCount);

			offered = new KingdomResourceDefinition[8];
			for (int i = 0; i < offered.Length; i++) offered[i] =
				new KingdomResourceDefinition("more" + i, "ore", "", "", "", 0, 1);
			KingdomBehaviourState next;
			Assert.IsTrue(KingdomBehaviourRules.TryApplyResources(state, Owner, offered,
				out next, out kept));
			Assert.AreEqual(KingdomApiRules.MaxResourceKindsPerOwner - 1, kept);
			Assert.AreEqual(KingdomApiRules.MaxResourceKindsPerOwner, next.ResourceCount);
		}

		[Test]
		public void CityCapCountsRowsAcrossOwnersWithoutDisturbingExistingState()
		{
			KingdomBehaviourState state = KingdomBehaviourState.Empty;
			for (int owner = 0; owner < 5; owner++)
			{
				KingdomResourceDefinition[] offered = new KingdomResourceDefinition[4];
				for (int i = 0; i < offered.Length; i++) offered[i] =
					new KingdomResourceDefinition("ore" + i, "ore", "", "", "", 0, 1);
				KingdomBehaviourState next; int kept;
				Assert.IsTrue(KingdomBehaviourRules.TryApplyResources(state, "Fixture Owner " + owner,
					offered, out next, out kept));
				Assert.AreEqual(owner < 4 ? 4 : 0, kept);
				state = next;
			}
			Assert.AreEqual(KingdomApiRules.MaxResourceKindsPerCity, state.ResourceCount);
		}

		[Test]
		public void MalformedWorkChangeCannotPartiallyPublish()
		{
			KingdomBehaviourState state; int kept;
			KingdomBehaviourRules.TryApplyResources(KingdomBehaviourState.Empty, Owner,
				new[] { new KingdomResourceDefinition("ore", "ore", "", "", "", 5, 10) },
				out state, out kept);
			KingdomWorkAdvance result = new KingdomWorkAdvance(7, "crusher", 4, 1200,
				new[] { new KingdomResourceChange("ore", 2), new KingdomResourceChange("foreign:gold", 1) }, null);
			KingdomBehaviourState next;
			Assert.IsTrue(KingdomBehaviourRules.TryApplyWorks(state, Owner, new[] { result },
				City(0), 0, out next, out kept));
			Assert.AreEqual(0, kept);
			KingdomResourceReading before, after;
			state.TryResource(0, out before); next.TryResource(0, out after);
			Assert.AreEqual(before.Level, after.Level);
			Assert.AreEqual(0, next.WorkCount);
		}

		[Test]
		public void WorkBreakpointMustAdvancePastTheCurrentTick()
		{
			KingdomBehaviourState state; int kept;
			Assert.IsTrue(KingdomBehaviourRules.TryApplyWorks(KingdomBehaviourState.Empty, Owner,
				new[] { new KingdomWorkAdvance(7, "crusher", 1L, 1200L, null,
					new[] { new KingdomMaterialisation("Copper Nugget", 1) }) },
				City(1200L), 1200L, out state, out kept));
			Assert.AreEqual(0, kept);
			Assert.AreEqual(0, state.WorkCount,
				"same-tick check-ins must not replay a zero-length work interval");
		}

		[Test]
		public void ContractArraysAreFrozenAtConstruction()
		{
			KingdomExtensionLeg[] legs = { new KingdomExtensionLeg("zone-a", 1, 1, 2, 2) };
			KingdomResourceChange[] changes = { new KingdomResourceChange("ore", 1) };
			KingdomJobPlan plan = new KingdomJobPlan("job", "mule", "ore", 1, 0, legs, changes);
			legs[0] = new KingdomExtensionLeg("foreign", 9, 9, 9, 9);
			changes[0] = new KingdomResourceChange("foreign", 999);
			KingdomExtensionLeg leg; KingdomResourceChange change;
			Assert.IsTrue(plan.TryLeg(0, out leg)); Assert.AreEqual("zone-a", leg.ZoneId);
			Assert.IsTrue(plan.TryCompletionChange(0, out change)); Assert.AreEqual("ore", change.ResourceKey);
		}

		[Test]
		public void V3ContractsRequireV3WhileV1AndV2CompatibilityRemain()
		{
			Assert.AreEqual(3, KingdomApiRules.Version);
			Assert.AreEqual(KingdomExtensionVerdict.Accepted,
				KingdomApiRules.Judge(Owner, 1, true, 1));
			Assert.AreEqual(KingdomExtensionVerdict.Accepted,
				KingdomApiRules.Judge(Owner, 2, true, 2));
			Assert.AreEqual(KingdomExtensionVerdict.RefusedBehind,
				KingdomApiRules.Judge(Owner, 2, true, KingdomApiRules.BehaviourVersion));
			string line = KingdomApiRules.RefusalLine(KingdomExtensionVerdict.RefusedBehind,
				Owner, 2, KingdomApiRules.BehaviourVersion);
			StringAssert.Contains(Owner, line); StringAssert.Contains("require version 3", line);
		}

		[Test]
		public void ExternalFixtureThrowAndOverBudgetPublishNothing()
		{
			KingdomExecutor executor = KingdomExecutor.CreateSynchronous();
			KingdomComputeResult<KingdomResourceDefinition[]> thrown = executor.Submit(City(0),
				new ExternalResourceEnvelopeJob(true, 0));
			Assert.AreEqual(KingdomComputeStatus.Faulted, thrown.Status);
			Assert.AreEqual(KingdomComputeRefusal.Threw, thrown.Refusal);
			Assert.IsNull(thrown.Value);
			KingdomComputeResult<KingdomResourceDefinition[]> healthy = executor.Submit(City(0),
				new ExternalResourceEnvelopeJob(false, 0));
			Assert.AreEqual(KingdomComputeStatus.Ok, healthy.Status,
				"one throwing owner must not poison the shared executor for the next owner");
			Assert.AreEqual(1, healthy.Value.Length);

			KingdomComputeResult<KingdomResourceDefinition[]> over = executor.Submit(City(0),
				new ExternalResourceEnvelopeJob(false, KingdomBudgetRules.MaxDrawsPerCityPass + 1));
			Assert.AreEqual(KingdomComputeStatus.OverBudget, over.Status);
			Assert.IsNull(over.Value);
		}

		[Test]
		public void ActualDrawHandleReplaysAndAttemptThirtyThreeRefusesPublication()
		{
			KernelSeed128 seed = new KernelSeed128(0x0123456789abcdefUL, 0xfedcba9876543210UL);
			KingdomExtensionDraws first = new KingdomExtensionDraws(seed, "taf:settlement-a", Owner);
			KingdomExtensionDraws replay = new KingdomExtensionDraws(seed, "taf:settlement-a", Owner);
			for (uint i = 0; i < KingdomApiRules.MaxDrawsPerSourceCall; i++)
			{
				int a, b;
				Assert.IsTrue(first.TryBetween("fixture", i, -1000, 1000, out a));
				Assert.IsTrue(replay.TryBetween("fixture", i, -1000, 1000, out b));
				Assert.AreEqual(a, b, "reload replay drifted at ordinal " + i);
			}
			Assert.AreEqual(KingdomApiRules.MaxDrawsPerSourceCall, first.ReportedDraws);
			int refused;
			Assert.IsFalse(first.TryBetween("fixture", 32u, 0, 1, out refused));
			Assert.Greater(first.ReportedDraws, KingdomBudgetRules.MaxDrawsPerCityPass,
				"executor must see an explicit over-budget counter, not a quiet false draw");

			KingdomExtensionDraws malformed = new KingdomExtensionDraws(seed, "taf:settlement-a", Owner);
			for (uint i = 0; i < KingdomApiRules.MaxDrawsPerSourceCall; i++)
				Assert.IsFalse(malformed.TryBetween("fixture", i, 1, 0, out refused));
			Assert.IsFalse(malformed.TryBetween("fixture", 32u, 0, 1, out refused),
				"malformed attempts consume the same hostile-input budget");
		}

		[Test]
		public void TerminalReceiptRingDoesNotPermanentlyCloseTheJobLane()
		{
			KingdomBehaviourState state; int kept;
			KingdomBehaviourRules.TryApplyResources(KingdomBehaviourState.Empty, Owner,
				new[] { new KingdomResourceDefinition("ore", "ore", "", "", "", 50, 100) },
				out state, out kept);
			int carrierCount;
			KingdomCarrierKindRow[] carriers = KingdomBehaviourRules.NormalizeCarriers(Owner,
				new[] { new KingdomCarrierDefinition("mule", "Dromad Merchant", 1, 1) },
				out carrierCount);
			for (int i = 0; i < 10; i++)
			{
				long tick = i * 10L;
				KingdomJobPlan plan = new KingdomJobPlan("haul-" + i, "mule", "ore", 1, tick,
					new[] { new KingdomExtensionLeg("zone-a", 1, 1, 2, 1) },
					new[] { new KingdomResourceChange("ore", 1) });
				KingdomBehaviourState next;
				Assert.IsTrue(KingdomBehaviourRules.TryApplyJobs(state, Owner, new[] { plan },
					carriers, City(tick), tick, out next, out kept));
				Assert.AreEqual(1, kept, "job lane stuck after terminal receipt " + i);
				state = next;
				int completed, failed;
				Assert.IsTrue(KingdomBehaviourRules.TryCompleteJobs(state, tick + 1L, out next,
					out completed, out failed));
				Assert.AreEqual(1, completed);
				Assert.AreEqual(0, failed);
				state = next;
				Assert.LessOrEqual(state.JobCount,
					KingdomApiRules.MaxTerminalJobReceiptsPerOwner);
			}
			KingdomResourceReading ore;
			Assert.IsTrue(state.TryResource(0, out ore));
			Assert.AreEqual(50L, ore.Level, "reserve and completion should net to zero");
		}

		[Test]
		public void MaterialisationAcknowledgementRequiresExactDebtIdentity()
		{
			KingdomBehaviourState state; int kept;
			KingdomBehaviourRules.TryApplyWorks(KingdomBehaviourState.Empty, Owner,
				new[] { new KingdomWorkAdvance(7, "crusher", 1, 1200, null,
					new[] { new KingdomMaterialisation("Copper Nugget", 2) }) },
				City(0), 0, out state, out kept);
			KingdomBehaviourState next;
			KingdomWorkBehaviourReading original;
			Assert.IsTrue(state.TryWork(0, out original));
			Assert.AreEqual(1L, original.MaterialisationSequence);
			string originalReceipt = KingdomBehaviourRules.MaterialisationReceipt(original);
			Assert.IsFalse(KingdomBehaviourRules.TryAcknowledgeMaterialisation(state,
				"external-fixture:crusher", 7, "Lead Slug", 1, out next));
			Assert.AreSame(state, next);
			Assert.IsTrue(KingdomBehaviourRules.TryAcknowledgeMaterialisation(state,
				"external-fixture:crusher", 7, "Copper Nugget", 1, out next));
			KingdomWorkBehaviourReading row;
			Assert.IsTrue(next.TryWork(0, out row));
			Assert.AreEqual(1, row.OwedCount);
			Assert.AreEqual(original.MaterialisationSequence, row.MaterialisationSequence);
			Assert.AreNotEqual(originalReceipt, KingdomBehaviourRules.MaterialisationReceipt(row),
				"each unit in one generation needs a distinct interruption receipt");

			Assert.IsTrue(KingdomBehaviourRules.TryApplyWorks(next, Owner,
				new[] { new KingdomWorkAdvance(7, "crusher", 2, 2400, null,
					new[] { new KingdomMaterialisation("Copper Nugget", 1) }) },
				City(1200), 1200, out state, out kept));
			Assert.IsTrue(state.TryWork(0, out row));
			Assert.AreEqual(2L, row.MaterialisationSequence);
			Assert.AreNotEqual(originalReceipt, KingdomBehaviourRules.MaterialisationReceipt(row),
				"a stale marker cannot settle a later output generation with the same count");
		}

		[Test]
		public void BehaviourBoundariesContainNoEngineOrMutableType()
		{
			Type[] types =
			{
				typeof(KingdomResourceDefinition[]), typeof(KingdomCarrierDefinition[]),
				typeof(KingdomJobPlan[]), typeof(KingdomNetworkPlan[]),
				typeof(KingdomWorkAdvance[]), typeof(KingdomBehaviourReading)
			};
			for (int i = 0; i < types.Length; i++)
			{
				KingdomComputeRefusal refusal; string offender;
				Assert.IsTrue(KingdomComputeSeam.TryValidateType(types[i], out refusal, out offender),
					types[i].Name + ": " + offender + " (" + refusal + ")");
			}
		}

		[Test]
		public void RuntimeRegistrationAndEveryBehaviourCallUseTheSharedExecutor()
		{
			string registry = TestMain.ReadRepositoryText(Path.Combine("Api", "KingdomExtensions.cs"));
			StringAssert.Contains("extension is IResourceKind", registry);
			StringAssert.Contains("extension is IJobKind", registry);
			StringAssert.Contains("extension is ICarrierKind", registry);
			StringAssert.Contains("extension is INetworkKind", registry);
			StringAssert.Contains("extension is IWorkBehaviour", registry);
			StringAssert.Contains("KingdomApiRules.BehaviourVersion", registry);

			string runtime = TestMain.ReadRepositoryText(Path.Combine("Api", "KingdomBehaviourExtensions.cs"));
			string behaviourRules = TestMain.ReadRepositoryText(Path.Combine("Api",
				"KingdomBehaviourRules.cs"));
			StringAssert.Contains(
				"bytes.Length > KingdomApiRules.LegacyBehaviourModelBytes", behaviourRules);
			StringAssert.Contains("KingdomComputeResult<KingdomResourceDefinition[]>", runtime);
			StringAssert.Contains("KingdomComputeResult<KingdomCarrierDefinition[]>", runtime);
			StringAssert.Contains("KingdomComputeResult<KingdomJobPlan[]>", runtime);
			StringAssert.Contains("KingdomComputeResult<KingdomNetworkPlan[]>", runtime);
			StringAssert.Contains("KingdomComputeResult<KingdomWorkAdvance[]>", runtime);
			Assert.AreEqual(5, Count(runtime, "KingdomCity.Seam.Submit(input, job)"));
			Assert.AreEqual(4, Count(runtime, "TryAdmitBehaviourState(ref state, posted"),
				"every durable callback family must pass its own final-size transaction gate");
			StringAssert.Contains("EncodeCapAfterCompletion", runtime);
			Assert.Less(runtime.IndexOf("TryEncode(state, out durable)", StringComparison.Ordinal),
				runtime.IndexOf("// Phase 1:", StringComparison.Ordinal),
				"host-completed jobs need an encodable baseline before any owner callback");
			StringAssert.DoesNotContain("return Wire ?? \"\";\n\t\t\t}\n\t\t\treturn encoded;", runtime);
			string drawSource = TestMain.ReadRepositoryText(Path.Combine("Api",
				"KingdomExtensionDraws.cs"));
			StringAssert.Contains("CounterRandom.TryDrawBelow", drawSource);
			StringAssert.Contains("MaxDrawsPerSourceCall", drawSource);
			StringAssert.Contains("ReportedDraws", runtime);

			string consumer = TestMain.ReadRepositoryText(Path.Combine("Simulation", "City",
				"KingdomBehaviourRuntime.cs"));
			StringAssert.Contains("AdvanceBehaviourModel", consumer);
			StringAssert.Contains("TryAcknowledgeMaterialisation", consumer);
			StringAssert.Contains("FindLandedReceipt", consumer);
			StringAssert.Contains("MaterialisationReceipt", consumer);
			StringAssert.Contains("RemoveStringProperty(MaterialisationMarker)", consumer);
			StringAssert.Contains("work.CurrentCell.AddObject", consumer);
			Assert.Less(consumer.IndexOf("FindLandedReceipt", StringComparison.Ordinal),
				consumer.IndexOf("GameObject.Create", StringComparison.Ordinal),
				"reentrant-save receipt must reconcile before another object can be minted");
			int recoveryPublish = consumer.IndexOf("Book.ExtensionModel = replacement",
				consumer.IndexOf("FindLandedReceipt", StringComparison.Ordinal), StringComparison.Ordinal);
			Assert.Less(recoveryPublish, consumer.IndexOf("RemoveStringProperty(MaterialisationMarker)",
				recoveryPublish, StringComparison.Ordinal),
				"receipt marker retires only after exact sidecar acknowledgement publishes");
		}

		private static int Count(string source, string needle)
		{
			int count = 0, at = 0;
			while ((at = source.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
			{ count++; at += needle.Length; }
			return count;
		}
	}
}
#endif
