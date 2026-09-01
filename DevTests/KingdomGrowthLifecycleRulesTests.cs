#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomGrowthLifecycleRulesTests
	{
		[Test]
		public void V5BoundReadStagesThenMigrationPublishesAtomically()
		{
			KingdomLifecycleBook source = Bound("city-v5");
			byte[] wire = WriteV5(source);
			KingdomLifecycleBook loaded = ReadLifecycle(wire);
			Assert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion, loaded.FormatVersion);
			Assert.IsTrue(loaded.IdentityBound);
			Assert.IsTrue(loaded.Growth.MigrationPending);
			Assert.AreEqual(KingdomLifecycleRules.LegacyLifecycleFormatVersion,
				loaded.Growth.MigratedFromLifecycleVersion);
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(loaded));

			KingdomGrowthMigrationResult detached = KingdomLifecycleRules.ApplyGrowthMigration(
				loaded, Migration(100L, true, true, 25L, 3));
			Assert.IsTrue(detached.Valid, detached.Failure);
			Assert.IsTrue(loaded.Growth.MigrationPending, "detached preparation did not publish");
			Assert.AreEqual(125L, detached.Growth.NextArrivalTick);
			Assert.AreEqual(100L, detached.Growth.LastHeartbeatTick);
			Assert.AreEqual(100L, detached.Growth.LastFetchTick);
			Assert.AreEqual(100L, detached.Growth.LastMillTick);
			Assert.AreEqual(100L, detached.Growth.LastSubsidenceTick);
			Assert.AreEqual(3, detached.Growth.PendingCrop);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthMigration(loaded, detached));
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(loaded));
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowthMigration(loaded, detached));
		}

		[Test]
		public void GrowthMigrationRequiresWholeParentAuthorityAndRefusalIsAtomic()
		{
			KingdomLifecycleBook parent = ReadLifecycle(WriteV5(Bound("city-migration-root")));
			KingdomGrowthBook staged = parent.Growth;
			KingdomGrowthMigrationInput input = Migration(100L, true, true, 20L, 0);
			KingdomGrowthMigrationResult prepared =
				KingdomLifecycleRules.ApplyGrowthMigration(parent, input);
			Assert.IsTrue(prepared.Valid, prepared.Failure);

			parent.PlainGuestNextSequence = 2L;
			Assert.IsFalse(KingdomLifecycleRules.CanOwnAuthority(parent));
			byte[] before = WriteLifecycle(parent);
			KingdomGrowthMigrationResult refused =
				KingdomLifecycleRules.ApplyGrowthMigration(parent, input);
			Assert.IsFalse(refused.Valid);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowthMigration(parent, prepared));
			Assert.AreSame(staged, parent.Growth);
			CollectionAssert.AreEqual(before, WriteLifecycle(parent));
		}

		[Test]
		public void V5EngineReadPreservesPendingAcrossV6RoundTrip()
		{
			KingdomLifecycleBook staged = ReadLifecycle(WriteV5(Bound("city-cut")));
			KingdomLifecycleBook reloaded = ReadLifecycle(WriteLifecycle(staged));
			Assert.IsTrue(reloaded.Growth.MigrationPending);
			Assert.AreEqual(KingdomLifecycleRules.LegacyLifecycleFormatVersion,
				reloaded.Growth.MigratedFromLifecycleVersion);
			KingdomGrowthMigrationResult result = KingdomLifecycleRules.ApplyGrowthMigration(
				reloaded, Migration(777L, false, false, 30L, 2));
			Assert.IsTrue(result.Valid, result.Failure);
			Assert.IsTrue(result.Growth.WorkPaused);
			Assert.AreEqual(0L, result.Growth.NextArrivalTick);
			Assert.AreEqual(2, result.Growth.PendingCrop);
		}

		[Test]
		public void V5UnboundReadBecomesPristineV6AndCanBind()
		{
			KingdomLifecycleBook loaded = ReadLifecycle(WriteV5(new KingdomLifecycleBook()));
			Assert.IsFalse(loaded.Growth.MigrationPending);
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(loaded, "new-city", false,
				null, new List<string>()));
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(loaded));
		}

		[Test]
		public void ArrivalMigrationAndAvailabilityNeverCreateBackfill()
		{
			KingdomLifecycleBook book = ReadLifecycle(WriteV5(Bound("city-clock")));
			KingdomGrowthMigrationResult migration = KingdomLifecycleRules.ApplyGrowthMigration(book,
				Migration(100L, true, true, 20L, 0));
			Assert.IsTrue(migration.Valid, migration.Failure);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthMigration(book, migration));
			KingdomGrowthAvailabilityDecision disable =
				KingdomLifecycleRules.ObserveGrowthAvailability(book.Growth, false, true, 110L, 20L);
			Assert.IsTrue(disable.Valid);
			Assert.IsFalse(disable.AllowStarters);
			Assert.AreEqual(0L, disable.NextArrivalTick);
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(book.Growth, disable));
			KingdomGrowthAvailabilityDecision enable =
				KingdomLifecycleRules.ObserveGrowthAvailability(book.Growth, true, true, 150L, 20L);
			Assert.IsTrue(enable.Valid);
			Assert.AreEqual(170L, enable.NextArrivalTick, "fresh interval, not immediately due");
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(book.Growth, enable));
			Assert.IsNull(KingdomLifecycleRules.PrepareGrowthOperation(book.Growth,
				KingdomGrowthAction.Arrival, null, 169L));
		}

		[Test]
		public void AvailabilityDecisionIsNonAuthoritativeAndRewindFails()
		{
			KingdomGrowthBook growth = Migrated("city-decision", 100L, true, true, 20L);
			KingdomGrowthAvailabilityDecision decision =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, false, false, 110L, 20L);
			Assert.IsTrue(decision.Valid);
			decision.PausedTicks++;
			Assert.IsFalse(KingdomLifecycleRules.ApplyGrowthAvailability(growth, decision));
			Assert.IsFalse(KingdomLifecycleRules.ObserveGrowthAvailability(growth,
				true, true, 99L, 20L).Valid);
		}

		[Test]
		public void EffectiveWorkClockSubtractsPausedDuration()
		{
			KingdomGrowthBook growth = Migrated("city-effective", 100L, true, true, 20L);
			KingdomGrowthAvailabilityDecision disable =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, false, true, 110L, 20L);
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth, disable));
			Assert.IsFalse(KingdomLifecycleRules.TryEffectiveWorkElapsed(growth, 130L, out _));
			KingdomGrowthAvailabilityDecision enable =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, true, true, 150L, 20L);
			Assert.AreEqual(40L, enable.PausedTicks);
			Assert.AreEqual(110L, enable.EffectiveWorkTick,
				"effective domain does not advance while paused");
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth, enable));
			Assert.IsTrue(KingdomLifecycleRules.TryEffectiveWorkElapsed(growth, 160L,
				out long elapsed));
			Assert.AreEqual(10L, elapsed);
		}

		[Test]
		public void DisabledOrUnhealthyBlocksEveryNewActionExceptProtectiveWithdraw()
		{
			foreach (bool option in new[] { false, true })
			foreach (bool healthy in new[] { false, true })
			{
				KingdomGrowthBook growth = Migrated("city-matrix-" + option + "-" + healthy,
					100L, option, healthy, 20L);
				bool productive = option && healthy;
				Assert.AreEqual(productive, KingdomLifecycleRules.PrepareGrowthOperation(growth,
					KingdomGrowthAction.Arrival, null, 120L) != null, "arrival");
				Assert.AreEqual(productive, KingdomLifecycleRules.PrepareGrowthOperation(growth,
					KingdomGrowthAction.Heartbeat, null, 121L) != null, "heartbeat starter");
				Assert.AreEqual(productive, KingdomLifecycleRules.PrepareGrowthOperation(growth,
					KingdomGrowthAction.Departure, null, 122L) != null, "departure starter");
				Assert.AreEqual(productive, KingdomLifecycleRules.PrepareGrowthOperation(growth,
					KingdomGrowthAction.Delivery, null, 123L) != null, "delivery starter");
				Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
				Assert.AreEqual(productive, KingdomLifecycleRules.PrepareGrowthOperation(growth,
					KingdomGrowthAction.Sow, "field-a", 124L) != null, "field starter");
				Assert.NotNull(KingdomLifecycleRules.PrepareGrowthOperation(growth,
					KingdomGrowthAction.Withdraw, "field-a", 125L), "protective withdraw");
			}
		}

		[Test]
		public void CandidateStarterRequiresCurrentActiveAvailabilityAndPublicationTick()
		{
			KingdomGrowthBook disabled = Migrated("city-candidate-disabled",
				100L, false, true, 20L);
			KingdomGrowthBook unhealthy = Migrated("city-candidate-unhealthy",
				100L, true, false, 20L);
			KingdomGrowthBook unknownOption = Migrated("city-candidate-option-unknown",
				100L, true, true, 20L);
			unknownOption.OptionState = KingdomLifecycleOptionState.Unknown;
			KingdomGrowthBook unknownHealth = Migrated("city-candidate-health-unknown",
				100L, true, true, 20L);
			unknownHealth.HealthState = KingdomGrowthHealthState.Unknown;
			foreach (KingdomGrowthBook blocked in new[]
				{ disabled, unhealthy, unknownOption, unknownHealth })
			{
				Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(blocked,
					blocked.SettlementId));
				byte[] before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(blocked);
				Assert.IsNull(KingdomLifecycleRules.PrepareGrowthArrivalCandidate(blocked,
					"blocked-marker", "Settler", "blocked-escrow", "zone-a", 120L,
					Digest('1'), Digest('2'), Digest('3')));
				CollectionAssert.AreEqual(before,
					KingdomLifecycleWireCodec.GrowthPayloadForWrite(blocked));
			}

			KingdomGrowthBook growth = Migrated("city-candidate-stale-publish",
				100L, true, true, 20L);
			KingdomGrowthArrivalCandidate detached =
				KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth, "stale-marker",
					"Settler", "stale-escrow", "zone-a", 120L,
					Digest('1'), Digest('2'), Digest('3'));
			Assert.NotNull(detached);
			KingdomGrowthAvailabilityDecision pause =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, false, true, 130L, 20L);
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth, pause));
			byte[] paused = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(growth,
				detached));
			CollectionAssert.AreEqual(paused,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			KingdomGrowthAvailabilityDecision resume =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, true, true, 160L, 20L);
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth, resume));
			byte[] resumed = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(growth,
				detached), "detached preparation predates the current availability witness");
			CollectionAssert.AreEqual(resumed,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
		}

		[Test]
		public void GrowthOpaquePayloadsRemainExactAndLocal()
		{
			byte[] validCurrent = KingdomLifecycleWireCodec.GrowthPayloadForWrite(
				Migrated("city-current-trailing", 100L, true, true, 20L));
			byte[] currentTrailing = new byte[validCurrent.Length + 1];
			Array.Copy(validCurrent, currentTrailing, validCurrent.Length);
			currentTrailing[currentTrailing.Length - 1] = 99;
			byte[][] payloads =
			{
				new byte[] { 1, 2, 3 },
				new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 },
				FuturePayload(77, new byte[] { 4, 5, 6 }),
				FuturePayload(-7, new byte[] { 4, 5, 6 }),
				currentTrailing,
				FuturePayload(KingdomLifecycleRules.CurrentGrowthFormatVersion,
					new byte[] { 9, 9, 9 })
			};
			for (int i = 0; i < payloads.Length; i++)
			{
				KingdomGrowthBook growth = KingdomLifecycleWireCodec.ReadGrowthPayload(payloads[i]);
				Assert.IsTrue(growth.Quarantined);
				CollectionAssert.AreEqual(payloads[i], growth.OpaquePayload);
				CollectionAssert.AreEqual(payloads[i],
					KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			}
			Assert.AreEqual(0, KingdomLifecycleWireCodec.ReadGrowthPayload(payloads[0])
				.OpaqueWireVersion, "short payload has no header authority");
			Assert.AreEqual(0, KingdomLifecycleWireCodec.ReadGrowthPayload(payloads[1])
				.OpaqueWireVersion, "bad magic has no header authority");
			Assert.AreEqual(77, KingdomLifecycleWireCodec.ReadGrowthPayload(payloads[2])
				.OpaqueWireVersion);
			Assert.AreEqual(-7, KingdomLifecycleWireCodec.ReadGrowthPayload(payloads[3])
				.OpaqueWireVersion, "signed unsupported header is exact evidence");
			Assert.AreEqual(KingdomLifecycleRules.CurrentGrowthFormatVersion,
				KingdomLifecycleWireCodec.ReadGrowthPayload(payloads[4]).OpaqueWireVersion,
				"current trailing payload retains exact header version");
			Assert.AreEqual(KingdomLifecycleRules.CurrentGrowthFormatVersion,
				KingdomLifecycleWireCodec.ReadGrowthPayload(payloads[5]).OpaqueWireVersion,
				"malformed current payload retains exact header version");
			KingdomLifecycleBook parent = Bound("city-local");
			parent.Growth = KingdomLifecycleWireCodec.ReadGrowthPayload(payloads[0]);
			KingdomLifecycleRules.Normalize(parent);
			Assert.IsFalse(parent.Quarantined, "nested quarantine does not poison lifecycle");
			Assert.IsTrue(parent.Growth.Quarantined);
			KingdomGrowthBook mismatched = KingdomLifecycleWireCodec.ReadGrowthPayload(
				FuturePayload(77, new byte[] { 8, 8 }));
			mismatched.OpaqueWireVersion = 78;
			Assert.IsFalse(KingdomLifecycleRules.GrowthEnvelopeWritable(mismatched));
			Assert.Throws<InvalidDataException>(() =>
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(mismatched));
			mismatched = KingdomLifecycleWireCodec.ReadGrowthPayload(
				FuturePayload(77, new byte[] { 8, 8 }));
			mismatched.OpaquePayload[0] ^= 1;
			Assert.IsFalse(KingdomLifecycleRules.GrowthEnvelopeWritable(mismatched));

			KingdomGrowthBook canonical = KingdomLifecycleWireCodec.ReadGrowthPayload(
				FuturePayload(77, new byte[] { 8, 8 }));
			KingdomGrowthBook hybrid = Migrated("city-opaque-hybrid", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(hybrid, "field-a"));
			KingdomGrowthOperation heartbeat = HeartbeatPlan(hybrid, 121L);
			Assert.NotNull(heartbeat);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(hybrid, heartbeat));
			hybrid.Quarantined = true;
			hybrid.Fault = canonical.Fault;
			hybrid.OpaqueWireVersion = canonical.OpaqueWireVersion;
			hybrid.OpaquePayload = (byte[])canonical.OpaquePayload.Clone();
			byte[] hybridPayload = hybrid.OpaquePayload;
			Assert.IsFalse(KingdomLifecycleRules.GrowthEnvelopeWritable(hybrid));
			Assert.AreSame(heartbeat, hybrid.HeartbeatOp);
			Assert.AreSame(hybridPayload, hybrid.OpaquePayload,
				"writer predicate restores exact caller payload reference");

			KingdomGrowthBook negativeMismatch = KingdomLifecycleWireCodec.ReadGrowthPayload(
				FuturePayload(77, new byte[] { 8, 8 }));
			negativeMismatch.OpaqueWireVersion = -1;
			Assert.IsFalse(KingdomLifecycleRules.GrowthEnvelopeWritable(negativeMismatch));
			KingdomGrowthBook missingFault = KingdomLifecycleWireCodec.ReadGrowthPayload(
				FuturePayload(77, new byte[] { 8, 8 }));
			missingFault.Fault = null;
			Assert.IsFalse(KingdomLifecycleRules.GrowthEnvelopeWritable(missingFault));
			missingFault.Fault = "";
			Assert.IsFalse(KingdomLifecycleRules.GrowthEnvelopeWritable(missingFault));
		}

		[Test]
		public void GrowthFramingOversizeAndTruncationPoisonOuterLifecycle()
		{
			KingdomLifecycleBook source = Bound("city-frame");
			byte[] bytes = WriteLifecycle(source);
			byte[] payload = KingdomLifecycleWireCodec.GrowthPayloadForWrite(source.Growth);
			int lengthOffset = bytes.Length - payload.Length - 4;
			byte[] oversize = (byte[])bytes.Clone();
			Array.Copy(BitConverter.GetBytes(KingdomLifecycleRules.MaxGrowthSectionBytes + 1), 0,
				oversize, lengthOffset, 4);
			KingdomLifecycleBook target = new KingdomLifecycleBook();
			Assert.Throws<InvalidDataException>(() => ReadLifecycleInto(oversize, target));
			Assert.IsTrue(target.WireRejected);
			byte[] truncated = new byte[bytes.Length - 1];
			Array.Copy(bytes, truncated, truncated.Length);
			target = new KingdomLifecycleBook();
			KingdomLifecycleBook captured = target;
			Assert.Throws<EndOfStreamException>(() => ReadLifecycleInto(truncated, captured));
			Assert.IsTrue(target.WireRejected);
		}

		[Test]
		public void FieldAndCropCapsRefuseCapPlusOneWithoutTruncation()
		{
			KingdomGrowthBook growth = Migrated("city-caps", 100L, true, true, 20L);
			for (int i = 0; i < KingdomLifecycleRules.MaxGrowthFields; i++)
				Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-" + i));
			Assert.IsFalse(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-over"));
			Assert.AreEqual(KingdomLifecycleRules.MaxGrowthFields, growth.FieldOps.Count);

			for (int i = 0; i < KingdomLifecycleRules.MaxGrowthCropRows; i++)
				Assert.IsTrue(TryInstallCrop(growth,
					Crop("field-0", i)));
			Assert.IsTrue(KingdomLifecycleRules.GrowthEnvelopeWritable(growth));
			Assert.IsFalse(TryInstallCrop(growth,
				Crop("field-0", 999)));
			growth.CropRows.Add(Crop("field-0", 999)); // hostile loaded/programmatic cap+1
			Assert.IsFalse(KingdomLifecycleRules.GrowthEnvelopeWritable(growth));
			Assert.AreEqual(KingdomLifecycleRules.MaxGrowthCropRows + 1, growth.CropRows.Count,
				"never truncate existing evidence");
		}

		[Test]
		public void OuterAndCarryV5NeverAcceptGrowthResourceKinds()
		{
			KingdomLifecycleBook outer = Bound("city-kind");
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(outer,
				KingdomLifecycleLane.PlainGuest, KingdomLifecycleAction.Passages, 1L);
			Assert.IsNull(KingdomLifecycleRules.PrepareLease(outer, op,
				KingdomLifecycleResourceKind.GrowthClock, "city-kind", "clock", 1L, 1L));

			KingdomCarryBook carry = new KingdomCarryBook();
			carry.Resources.Add(new KingdomLifecycleResourceRevision
			{
				Kind = KingdomLifecycleResourceKind.GrowthClock, ScopeId = "realm",
				SubjectId = "clock", Key = KingdomLifecycleRules.ResourceKey(
					KingdomLifecycleResourceKind.GrowthClock, "realm", "clock")
			});
			KingdomLifecycleRules.Normalize(carry);
			Assert.IsTrue(carry.Quarantined);
		}

		[Test]
		public void CarryV5WireGoldenRemainsStable()
		{
			byte[] bytes;
			using (MemoryStream stream = new MemoryStream())
			{
				KingdomLifecycleWireCodec.WriteCarryV5Fixture(
					new BinaryWriter(stream), new KingdomCarryBook());
				bytes = stream.ToArray();
			}
			Assert.AreEqual("c9d79728032c2f2f427241cf4a4097df7c18a6c711ec0c79fbac0b71ee994e52",
				Sha256(bytes));
		}

		[Test]
		public void HeartbeatPublishClockCutSinksTerminalAndRetireAreOrdered()
		{
			KingdomGrowthBook growth = Migrated("city-heartbeat", 100L, true, true, 20L);
			KingdomGrowthOperation op = HeartbeatPlan(growth, 121L);
			Assert.NotNull(op);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			Assert.IsFalse(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.Sinks, 122L), "no phase skipping");
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainIntent, 122L));
			for (int i = 0; i < op.DomainSteps.Count; i++) ProveDomain(growth, op, i);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainSettled, 123L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.ClockIntent, 124L));
			Assert.IsFalse(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.Sinks, 127L), "clock must settle before sinks");
			Assert.AreEqual(KingdomLifecycleCasAction.Apply,
				KingdomLifecycleRules.GrowthClockAction(growth, op, op.ClockLease.Before));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(growth, op,
				op.ClockLease.Before));

			KingdomGrowthBook reloaded = RoundTripGrowth(growth);
			op = reloaded.HeartbeatOp;
			Assert.AreEqual(KingdomLifecycleCasAction.Quarantine,
				KingdomLifecycleRules.GrowthClockAction(reloaded, op, op.ClockLease.Before),
				"Intent never retries uncertain CAS");
			Assert.AreEqual(KingdomLifecycleCasAction.Confirm,
				KingdomLifecycleRules.GrowthClockAction(reloaded, op, op.ClockLease.After));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(reloaded, op,
				op.ClockLease.After));
			Assert.AreEqual(121L, reloaded.LastHeartbeatTick);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(reloaded, op,
				KingdomGrowthPhase.Sinks, 127L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(reloaded, op,
				KingdomGrowthPhase.Terminal, 128L));
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowth(reloaded, op, 129L));
			Assert.IsNull(reloaded.HeartbeatOp);
			Assert.AreEqual(1L, reloaded.HeartbeatRetiredThrough);
			Assert.AreEqual(1, reloaded.RecentProofs.Count);
		}

		[Test]
		public void EveryGrowthActionHasOneExactAdjacentPhasePath()
		{
			foreach (KingdomGrowthAction action in Enum.GetValues(typeof(KingdomGrowthAction)))
			{
				if (action == KingdomGrowthAction.None) continue;
				List<KingdomGrowthPhase> path = GrowthPath(action);
				Assert.Greater(path.Count, 1, action.ToString());
				for (int i = 0; i + 1 < path.Count; i++)
				{
					Assert.IsTrue(KingdomLifecycleRules.CanTransitionGrowth(action,
						path[i], path[i + 1]), action + " edge " + path[i]);
					for (int j = i + 2; j < path.Count; j++)
						Assert.IsFalse(KingdomLifecycleRules.CanTransitionGrowth(action,
							path[i], path[j]), action + " skip " + path[i] + " -> " + path[j]);
				}
				Assert.AreEqual(KingdomGrowthPhase.Terminal, path[path.Count - 1]);
			}
		}

		[Test]
		public void SixtyFifthRetirementEvictsOldestProofNotAuthority()
		{
			KingdomGrowthBook growth = Migrated("city-fifo", 100L, true, true, 20L);
			for (int i = 1; i <= 65; i++) CompleteHeartbeat(growth, 100L + i);
			Assert.AreEqual(65L, growth.HeartbeatRetiredThrough);
			Assert.AreEqual(66L, growth.HeartbeatNextSequence);
			Assert.AreEqual(KingdomLifecycleRules.MaxRecentProofs, growth.RecentProofs.Count);
			Assert.AreEqual(2L, growth.RecentProofs[0].Sequence);
			Assert.AreEqual(65L, growth.RecentProofs[63].Sequence);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth, growth.SettlementId));
		}

		[Test]
		public void DecreasingRetirementTickFailsWithoutMutatingAnyGrowthBytes()
		{
			KingdomGrowthBook growth = Migrated("city-retire-atomic", 100L, true, true, 20L);
			KingdomGrowthOperation first = HeartbeatPlan(growth, 121L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, first));
			AdvanceHeartbeatToTerminal(growth, first, 121L);
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowth(growth, first, 200L));

			KingdomGrowthOperation second = HeartbeatPlan(growth, 122L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, second));
			AdvanceHeartbeatToTerminal(growth, second, 122L);
			byte[] before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.RetireGrowth(growth, second, 150L));
			CollectionAssert.AreEqual(before,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.AreSame(second, growth.HeartbeatOp);
			Assert.AreEqual(1L, growth.HeartbeatRetiredThrough);
			Assert.AreEqual(1, growth.RecentProofs.Count);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth,
				growth.SettlementId));
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowth(growth, second, 201L));
		}

		[Test]
		public void ResourceCapPreflightLeavesPlanAndRegistryUntouched()
		{
			KingdomGrowthBook growth = Migrated("city-resource-cap", 100L, true, true, 20L);
			for (int i = 0; i < KingdomLifecycleRules.MaxResourceRows; i++)
			{
				string subject = "health-" + i;
				growth.Resources.Add(new KingdomLifecycleResourceRevision
				{
					Kind = KingdomLifecycleResourceKind.GrowthHealth,
					ScopeId = growth.SettlementId, SubjectId = subject,
					Key = KingdomLifecycleRules.ResourceKey(
						KingdomLifecycleResourceKind.GrowthHealth, growth.SettlementId, subject)
				});
			}
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth, growth.SettlementId));
			KingdomGrowthOperation op = HeartbeatPlan(growth, 121L);
			Assert.NotNull(op);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			Assert.IsNull(op.PlanHash);
			Assert.IsNull(growth.HeartbeatOp);
			Assert.AreEqual(KingdomLifecycleRules.MaxResourceRows, growth.Resources.Count);
		}

		[Test]
		public void QuarantinedFieldRetainsBoundedEvidenceAndDoesNotBlockHeartbeat()
		{
			KingdomGrowthBook growth = Migrated("city-field-quarantine", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			Assert.IsTrue(KingdomLifecycleRules.QuarantineGrowthField(growth, "field-a", "bad field"));
			KingdomGrowthOperation evidence = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Heartbeat, null, 121L);
			Assert.NotNull(evidence);
			evidence.FieldId = "field-a";
			growth.FieldOps[0].Operation = evidence;
			KingdomGrowthBook reloaded = RoundTripGrowth(growth);
			Assert.IsTrue(reloaded.FieldOps[0].Quarantined);
			Assert.NotNull(reloaded.FieldOps[0].Operation, "evidence retained exactly");
			Assert.NotNull(KingdomLifecycleRules.PrepareGrowthOperation(reloaded,
				KingdomGrowthAction.Heartbeat, null, 122L));
			Assert.IsNull(KingdomLifecycleRules.PrepareGrowthOperation(reloaded,
				KingdomGrowthAction.Sow, "field-a", 122L));
		}

		[Test]
		public void FieldQuarantineRetainsCropEvidenceRoundTripsAndHeartbeatStillRetires()
		{
			KingdomGrowthBook growth = Migrated("city-crop-quarantine", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			Assert.IsTrue(TryInstallCrop(growth, Crop("field-a", 0)));
			Assert.IsTrue(KingdomLifecycleRules.QuarantineGrowthField(growth, "field-a",
				"field topology uncertain"));
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth, growth.SettlementId));
			KingdomGrowthBook reloaded = RoundTripGrowth(growth);
			Assert.IsTrue(reloaded.FieldOps[0].Quarantined);
			Assert.AreEqual("row-0", reloaded.CropRows[0].RowId);
			Assert.AreEqual("object-0", reloaded.CropRows[0].ObjectId);

			byte[] beforeFailure = KingdomLifecycleWireCodec.GrowthPayloadForWrite(reloaded);
			Assert.IsFalse(TryInstallCrop(reloaded,
				Crop("field-a", 1)), "quarantined fields cannot mint new crop authority");
			Assert.IsFalse(KingdomLifecycleRules.QuarantineGrowthField(reloaded, "field-a",
				"duplicate quarantine"));
			Assert.IsFalse(KingdomLifecycleRules.QuarantineGrowthField(reloaded, "missing-field",
				"missing"));
			CollectionAssert.AreEqual(beforeFailure,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(reloaded));
			CompleteHeartbeat(reloaded, 121L);
			Assert.AreEqual(1L, reloaded.HeartbeatRetiredThrough);
			Assert.IsTrue(reloaded.FieldOps[0].Quarantined);
			Assert.AreEqual(1, reloaded.CropRows.Count);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(reloaded,
				reloaded.SettlementId));
		}

		[Test]
		public void QuarantinedFieldOperationIsEvidenceOnlyAtEveryExecutionCut()
		{
			KingdomGrowthBook prepared = PublishedRichSow("city-field-evidence-prepared",
				out KingdomGrowthOperation preparedOp);
			Assert.IsTrue(KingdomLifecycleRules.QuarantineGrowthField(prepared, "field-a",
				"prepared field proof uncertain"), "quarantine prepared field");
			AssertFieldEvidenceRefusal(prepared, preparedOp, () =>
				KingdomLifecycleRules.AdvanceGrowthPhase(prepared, preparedOp,
					KingdomGrowthPhase.WaterIntent, 122L));
			AssertFieldEvidenceRefusal(prepared, preparedOp, () =>
				KingdomLifecycleRules.QuarantineGrowthField(prepared, "field-a",
					"repeat quarantine"));
			CompleteHeartbeat(prepared, 130L);
			Assert.AreSame(preparedOp, prepared.FieldOps[0].Operation,
				"unrelated survival work preserves field evidence reference");
			Assert.IsTrue(prepared.FieldOps[0].Quarantined);
			Assert.AreEqual(1L, prepared.HeartbeatRetiredThrough);

			KingdomGrowthBook clockPrepared = RichSowAtClockIntent(
				"city-field-evidence-clock-prepared", out KingdomGrowthOperation clockPreparedOp);
			Assert.IsTrue(KingdomLifecycleRules.QuarantineGrowthField(clockPrepared, "field-a",
				"clock callback not started"), "quarantine clock-prepared field");
			Assert.AreEqual(KingdomLifecycleCasAction.Quarantine,
				KingdomLifecycleRules.GrowthClockAction(clockPrepared, clockPreparedOp,
					clockPreparedOp.ClockLease.Before));
			AssertFieldEvidenceRefusal(clockPrepared, clockPreparedOp, () =>
				KingdomLifecycleRules.BeginGrowthClock(clockPrepared, clockPreparedOp,
					clockPreparedOp.ClockLease.Before));

			KingdomGrowthBook clockIntent = RichSowAtClockIntent(
				"city-field-evidence-clock-intent", out KingdomGrowthOperation clockIntentOp);
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(clockIntent, clockIntentOp,
				clockIntentOp.ClockLease.Before), "begin clock before field quarantine");
			Assert.IsTrue(KingdomLifecycleRules.QuarantineGrowthField(clockIntent, "field-a",
				"clock callback result uncertain"), "quarantine clock-intent field");
			AssertFieldEvidenceRefusal(clockIntent, clockIntentOp, () =>
				KingdomLifecycleRules.CommitGrowthClockWitness(clockIntent, clockIntentOp,
					clockIntentOp.ClockLease.After));

			KingdomGrowthBook sinks = RichSowAtClockIntent("city-field-evidence-sinks",
				out KingdomGrowthOperation sinksOp);
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(sinks, sinksOp,
				sinksOp.ClockLease.Before), "begin sinks clock");
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(sinks, sinksOp,
				sinksOp.ClockLease.After), "commit sinks clock");
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(sinks, sinksOp,
				KingdomGrowthPhase.Sinks, 131L));
			Assert.IsTrue(KingdomLifecycleRules.QuarantineGrowthField(sinks, "field-a",
				"sink delivery uncertain"), "quarantine sinks field");
			AssertFieldEvidenceRefusal(sinks, sinksOp, () =>
				KingdomLifecycleRules.RecoverGrowthOutbox(sinks, sinksOp));

			KingdomGrowthBook terminal = RichSowAtClockIntent("city-field-evidence-terminal",
				out KingdomGrowthOperation terminalOp);
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(terminal, terminalOp,
				terminalOp.ClockLease.Before), "begin terminal clock");
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(terminal, terminalOp,
				terminalOp.ClockLease.After), "commit terminal clock");
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(terminal, terminalOp,
				KingdomGrowthPhase.Sinks, 131L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(terminal, terminalOp,
				KingdomGrowthPhase.Terminal, 132L));
			Assert.IsTrue(KingdomLifecycleRules.QuarantineGrowthField(terminal, "field-a",
				"terminal field proof retained"), "quarantine terminal field");
			AssertFieldEvidenceRefusal(terminal, terminalOp, () =>
				KingdomLifecycleRules.RetireGrowth(terminal, terminalOp, 133L));
		}

		[Test]
		public void MigrationPublicationDoesNotAliasDetachedResult()
		{
			KingdomLifecycleBook parent = ReadLifecycle(WriteV5(Bound("city-alias")));
			KingdomGrowthMigrationResult result = KingdomLifecycleRules.ApplyGrowthMigration(parent,
				Migration(100L, true, true, 20L, 0));
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthMigration(parent, result));
			result.Growth.NextArrivalTick = 9999L;
			Assert.AreEqual(120L, parent.Growth.NextArrivalTick);
		}

		[Test]
		public void RepeatedPausedObservationsRemainWritableAndKeepOriginalPauseStart()
		{
			foreach (bool optionFailure in new[] { true, false })
			{
				KingdomGrowthBook growth = Migrated("city-repeat-pause-" + optionFailure,
					100L, !optionFailure, optionFailure, 20L);
				Assert.IsTrue(growth.WorkPaused);
				Assert.AreEqual(100L, growth.WorkPauseStartedTick);
				KingdomGrowthAvailabilityDecision first =
					KingdomLifecycleRules.ObserveGrowthAvailability(growth, !optionFailure,
						optionFailure, 110L, 20L);
				Assert.IsTrue(first.Valid);
				Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth, first));
				growth = RoundTripGrowth(growth);
				KingdomGrowthAvailabilityDecision second =
					KingdomLifecycleRules.ObserveGrowthAvailability(growth, !optionFailure,
						optionFailure, 120L, 20L);
				Assert.IsTrue(second.Valid);
				Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth, second));
				Assert.AreEqual(100L, growth.WorkPauseStartedTick);
				Assert.AreEqual(120L, growth.OptionTick);
				Assert.AreEqual(120L, growth.HealthTick);
				Assert.AreEqual(100L, growth.LastHeartbeatTick);
				Assert.AreEqual(100L, growth.LastFetchTick);
				Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth,
					growth.SettlementId));
			}
		}

		[Test]
		public void ForgedAuthoritativeClockCannotRewindOpenOperation()
		{
			KingdomGrowthBook growth = Migrated("city-no-rewind", 100L, true, true, 20L);
			KingdomGrowthOperation op = HeartbeatPlan(growth, 121L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainIntent, 122L));
			for (int i = 0; i < op.DomainSteps.Count; i++) ProveDomain(growth, op, i);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainSettled, 123L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.ClockIntent, 124L));
			growth.LastHeartbeatTick = 999L;
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(growth,
				growth.SettlementId));
			Assert.AreEqual(KingdomLifecycleCasAction.Quarantine,
				KingdomLifecycleRules.GrowthClockAction(growth, op, 999L));
			Assert.IsFalse(KingdomLifecycleRules.BeginGrowthClock(growth, op, 999L));
			Assert.AreEqual(999L, growth.LastHeartbeatTick, "failed recovery never rewinds");
		}

		[Test]
		public void FailedGenericQuarantineTransitionLeavesExactBytesUnchanged()
		{
			KingdomGrowthBook growth = Migrated("city-atomic-transition", 100L, true, true, 20L);
			KingdomGrowthOperation op = HeartbeatPlan(growth, 121L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			byte[] before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.Quarantined, 122L));
			CollectionAssert.AreEqual(before,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.AreEqual(KingdomGrowthPhase.Prepared, op.Phase);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth,
				growth.SettlementId));
		}

		[Test]
		public void CropRowsRequireExactLiveFieldAndUniqueObjectAndMarker()
		{
			KingdomGrowthBook baseline = Migrated("city-crop-shape", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(baseline, "field-a"));
			Assert.IsTrue(TryInstallCrop(baseline, Crop("field-a", 0)));
			KingdomGrowthCropRow duplicateMarker = Crop("field-a", 1);
			duplicateMarker.Marker = baseline.CropRows[0].Marker;
			Assert.IsFalse(TryInstallCrop(baseline, duplicateMarker));
			Assert.IsFalse(TryInstallCrop(baseline,
				Crop("missing-field", 2)));

			KingdomGrowthBook duplicateObject = RoundTripGrowth(baseline);
			KingdomGrowthCropRow objectRow = Crop("field-a", 3);
			objectRow.ObjectId = duplicateObject.CropRows[0].ObjectId;
			duplicateObject.CropRows.Add(objectRow);
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(duplicateObject,
				duplicateObject.SettlementId));
			KingdomGrowthBook duplicateMarkerWire = RoundTripGrowth(baseline);
			KingdomGrowthCropRow markerRow = Crop("field-a", 4);
			markerRow.Marker = duplicateMarkerWire.CropRows[0].Marker;
			duplicateMarkerWire.CropRows.Add(markerRow);
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(duplicateMarkerWire,
				duplicateMarkerWire.SettlementId));
			KingdomGrowthBook unknownField = RoundTripGrowth(baseline);
			unknownField.CropRows.Add(Crop("missing-field", 5));
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(unknownField,
				unknownField.SettlementId));
		}

		[Test]
		public void FieldPlanCannotMintUnrelatedCropRegistryAuthority()
		{
			KingdomGrowthBook growth = Migrated("city-foreign-row", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-b"));
			KingdomGrowthOperation sow = RichSow(growth, "field-a", 121L, true);
			KingdomGrowthDomainStep registry = sow.DomainSteps[0];
			registry.CropRowsDeclaredAfter.Add(Crop("field-b", 7));
			byte[] before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(growth, sow));
			CollectionAssert.AreEqual(before,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.IsNull(sow.PlanHash);
			Assert.IsNull(growth.FieldOps[0].Operation);
		}

		[Test]
		public void ActiveTargetClaimsRejectCrossSlotAndTwoFieldCollisionsAfterReload()
		{
			KingdomGrowthBook crossSlot = Migrated("city-cross-slot", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(crossSlot, "field-a"));
			KingdomGrowthOperation heartbeat = HeartbeatPlan(crossSlot, 121L);
			SetTarget(heartbeat, "shared-object", "shared-marker");
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(crossSlot, heartbeat));
			crossSlot = RoundTripGrowth(crossSlot);
			KingdomGrowthCropRow crossRow = Crop("field-a", 0);
			crossRow.ObjectId = "shared-object"; crossRow.Marker = "shared-marker";
			crossSlot.CropRows.Add(crossRow);
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(crossSlot,
				crossSlot.SettlementId));
			crossSlot.CropRows.RemoveAt(crossSlot.CropRows.Count - 1);
			KingdomGrowthOperation fieldCandidate = Ripen(crossSlot, "field-a",
				"shared-object", "shared-marker", 122L, false);
			Assert.IsNull(fieldCandidate);
			Assert.IsNull(crossSlot.FieldOps[0].Operation);

			KingdomGrowthBook twoFields = Migrated("city-two-fields", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(twoFields, "field-a"));
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(twoFields, "field-b"));
			KingdomGrowthCropRow firstRow = Crop("field-a", 0);
			firstRow.ObjectId = "crop-object"; firstRow.Marker = "crop-marker";
			KingdomGrowthFieldState firstField = ActiveFieldState(twoFields, "field-a", 1);
			Assert.IsTrue(KingdomLifecycleRules.InstallGrowthFieldBootstrap(twoFields,
				firstField, new List<KingdomGrowthCropRow> { firstRow }), "bootstrap active field-a");
			KingdomGrowthOperation first = Ripen(twoFields, "field-a", "crop-object",
				"crop-marker", 121L);
			Assert.IsTrue(PrivateBool("GrowthFieldActionAuthorityShape", first),
				"ripen field authority");
			Assert.IsTrue(PrivateBool("GrowthGroupsMatchAction", first),
				"ripen groups");
			Assert.IsTrue(PrivateBool("GrowthOperationScalarsValid", twoFields, first,
				KingdomGrowthSlotKind.Field, "field-a"), "ripen scalars");
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(twoFields, first),
				"publish first exact ripen");
			twoFields = RoundTripGrowth(twoFields);
			KingdomGrowthCropRow secondRow = Crop("field-b", 1);
			secondRow.ObjectId = "crop-object"; secondRow.Marker = "crop-marker";
			// Hostile second field shares an active object's exact identity.
			twoFields.CropRows.Add(secondRow);
			KingdomGrowthOperation second = Ripen(twoFields, "field-b", "crop-object",
				"crop-marker", 122L, false);
			Assert.IsNull(second, "active exact object lease rejects before publication");
			Assert.IsNull(twoFields.FieldOps[1].Operation);
		}

		[Test]
		public void GrowthV1CanonicalAbsenceHasOneIdAndOneWritableRepresentation()
		{
			KingdomGrowthBook growth = Migrated("city-null", 100L, true, true, 20L);
			string nullId = KingdomLifecycleRules.GrowthOperationId(growth.SettlementId,
				KingdomGrowthSlotKind.Heartbeat, null, 1L);
			Assert.AreEqual(nullId, KingdomLifecycleRules.GrowthOperationId(growth.SettlementId,
				KingdomGrowthSlotKind.Heartbeat, "", 1L));
			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Heartbeat, "", 121L);
			Assert.NotNull(op);
			Assert.IsNull(op.FieldId);
			Assert.AreEqual(nullId, op.Id);
			KingdomGrowthOutboxEvent emptyEvent =
				KingdomLifecycleRules.PrepareGrowthOutboxEvent(op, 0, "empty",
					"", "", "", "", "");
			Assert.NotNull(emptyEvent);
			op.OutboxEvents.Add(emptyEvent);
			Assert.IsNull(emptyEvent.Outbox.Chronicle);
			Assert.IsNull(emptyEvent.Outbox.Ledger);
			Assert.IsNull(emptyEvent.Outbox.Message);
			Assert.IsNull(emptyEvent.Outbox.Deed);
			Assert.IsNull(emptyEvent.Outbox.GuestbookLine);

			byte[] before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			op.FieldId = "";
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			CollectionAssert.AreEqual(before,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.IsNull(op.PlanHash);

			KingdomLifecycleBook parent = ReadLifecycle(WriteV5(Bound("city-null-migration")));
			KingdomGrowthMigrationInput input = Migration(100L, true, true, 20L, 0);
			input.PendingCropBlueprint = ""; input.PendingCropZoneId = "";
			KingdomGrowthMigrationResult migrated =
				KingdomLifecycleRules.ApplyGrowthMigration(parent, input);
			Assert.IsTrue(migrated.Valid, migrated.Failure);
			Assert.IsNull(migrated.Growth.PendingCropBlueprint);
			Assert.IsNull(migrated.Growth.PendingCropZoneId);

			KingdomGrowthBook receiptBook = Migrated("city-null-receipt", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(receiptBook, "field-a"));
			KingdomGrowthOperation receipt = RichSow(receiptBook, "field-a", 121L, true);
			receipt.WaterLegs[0].ReceiptProofId = "";
			byte[] receiptBefore = KingdomLifecycleWireCodec.GrowthPayloadForWrite(receiptBook);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(receiptBook, receipt));
			CollectionAssert.AreEqual(receiptBefore,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(receiptBook));
			Assert.AreEqual("", receipt.WaterLegs[0].ReceiptProofId);

			KingdomGrowthOperation plan = KingdomLifecycleRules.PrepareGrowthOperation(receiptBook,
				KingdomGrowthAction.Heartbeat, null, 122L);
			plan.PlanHash = "";
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(receiptBook, plan));
			Assert.AreEqual("", plan.PlanHash, "failed publication restores exact caller value");
			KingdomGrowthBook emptyRootFault = RoundTripGrowth(receiptBook);
			emptyRootFault.Fault = "";
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(emptyRootFault,
				emptyRootFault.SettlementId));
			KingdomGrowthBook emptyFieldFault = RoundTripGrowth(receiptBook);
			emptyFieldFault.FieldOps[0].Fault = "";
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(emptyFieldFault,
				emptyFieldFault.SettlementId));
		}

		[Test]
		public void StrictWitnessAndCallbackIdentityRejectForeignOrShortEvidence()
		{
			KingdomGrowthBook growth = Migrated("city-witness", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			KingdomGrowthOperation shortHash = RichSow(growth, "field-a", 121L, false);
			Assert.IsNull(KingdomLifecycleRules.PrepareGrowthWaterLeg(growth, shortHash,
				KingdomGrowthWaterMutationKind.Drain, "water-short",
				KingdomLifecycleTopology.Cell, null, "Waterskin", "zone-a", 1, 1, 10, 10, 2,
				"fresh", "fresh", "before-graph", Digest('b'), Digest('c'), Digest('d'),
				Digest('e'), Digest('f')));
			Assert.IsNull(KingdomLifecycleRules.PrepareGrowthWaterLeg(growth, shortHash,
				KingdomGrowthWaterMutationKind.Drain, "water-upper",
				KingdomLifecycleTopology.Cell, null, "Waterskin", "zone-a", 1, 1, 10, 10, 2,
				"fresh", "fresh", Digest('A'), Digest('b'), Digest('c'), Digest('d'),
				Digest('e'), Digest('f')));
			Assert.IsNull(KingdomLifecycleRules.PrepareGrowthWaterLeg(growth, shortHash,
				KingdomGrowthWaterMutationKind.Drain, "water-nonhex",
				KingdomLifecycleTopology.Cell, null, "Waterskin", "zone-a", 1, 1, 10, 10, 2,
				"fresh", "fresh", Digest('g'), Digest('b'), Digest('c'), Digest('d'),
				Digest('e'), Digest('f')));

			KingdomGrowthOperation op = RichSow(growth, "field-a", 122L, true);
			Assert.IsTrue(PrivateBool("GrowthOperationScalarsValid", growth, op,
				KingdomGrowthSlotKind.Field, "field-a"), "scalars");
			Assert.IsTrue(PrivateBool("GrowthTargetShape", op, KingdomGrowthSlotKind.Field),
				"target");
			Assert.IsTrue(PrivateBool("GrowthPrefixShape", op, true), "prefix");
			Assert.IsTrue(PrivateBool("GrowthOutboxShape", op, true), "outbox");
			Assert.IsTrue(PrivateBool("GrowthGroupsMatchAction", op), "groups");
			Assert.IsTrue(PrivateBool("GrowthPublicationSnapshotsMatch", growth, op,
				growth.FieldOps[0]), "snapshots");
			Assert.IsTrue(PrivateBool("GrowthActiveIdentityClaimsValid", growth, op), "claims");
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op), "witness publish");
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.WaterIntent, 123L), "water phase");
			ProveWater(growth, op, 0);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth,
				growth.SettlementId), "proved water authority");
			KingdomGrowthBook valid = RoundTripGrowth(growth);
			KingdomGrowthBook wrongId = RoundTripGrowth(valid);
			wrongId.FieldOps[0].Operation.WaterLegs[0].ReceiptCallbackContainerId = "foreign";
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(wrongId,
				wrongId.SettlementId));
			KingdomGrowthBook differentReference = RoundTripGrowth(valid);
			differentReference.FieldOps[0].Operation.WaterLegs[0].ReceiptSameReference = false;
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(differentReference,
				differentReference.SettlementId));
			KingdomGrowthBook shortReference = RoundTripGrowth(valid);
			shortReference.FieldOps[0].Operation.WaterLegs[0].ReceiptCallbackReferenceHash = "short";
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(shortReference,
				shortReference.SettlementId));
			KingdomGrowthBook missing = RoundTripGrowth(valid);
			missing.FieldOps[0].Operation.WaterLegs[0].ReceiptCallbackContainerId = null;
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(missing,
				missing.SettlementId));
			KingdomGrowthBook duplicate = RoundTripGrowth(valid);
			duplicate.FieldOps[0].Operation.WaterLegs[0].ReceiptAfterMatches = 2;
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(duplicate,
				duplicate.SettlementId));
		}

		[Test]
		public void PublicationRejectsDuplicateWaterAndObjectClaimsWithoutMutation()
		{
			KingdomGrowthBook growth = Migrated("city-duplicates", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			KingdomGrowthOperation duplicateWater = RichSow(growth, "field-a", 121L, true);
			KingdomGrowthWaterLeg second = KingdomLifecycleRules.PrepareGrowthWaterLeg(growth,
				duplicateWater, KingdomGrowthWaterMutationKind.Drain, "water-a",
				KingdomLifecycleTopology.Cell, null, "Waterskin", "zone-b", 2, 2, 12, 12, 1,
				"fresh", "fresh", Digest('1'), Digest('2'), Digest('3'), Digest('4'),
				Digest('5'), Digest('6'));
			Assert.NotNull(second);
			duplicateWater.WaterLegs.Add(second);
			byte[] before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(growth, duplicateWater));
			CollectionAssert.AreEqual(before,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.IsNull(duplicateWater.PlanHash);

			KingdomGrowthOperation sourceSource = RichSow(growth, "field-a", 122L, true);
			KingdomGrowthObjectLeg extra = KingdomLifecycleRules.PrepareGrowthObjectLeg(growth,
				sourceSource,
				false, KingdomGrowthObjectMutationKind.DestroyOne, "seed-extra", "seed-extra-marker",
				"Seed", KingdomLifecycleTopology.Cell, null, "zone-a", 1, 1, 1, -1, false,
				Digest('1'), Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6'));
			Assert.NotNull(extra);
			extra.ObjectId = sourceSource.Sources[0].ObjectId;
			sourceSource.Sources.Add(extra);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(growth, sourceSource));
			Assert.IsNull(sourceSource.PlanHash);

			KingdomGrowthOperation sourceOutput = RichSow(growth, "field-a", 123L, true);
			sourceOutput.Outputs[0].ObjectId = sourceOutput.Sources[0].ObjectId;
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(growth, sourceOutput));
			Assert.IsNull(sourceOutput.PlanHash);
		}

		[Test]
		public void ObjectReceiptRequiresExactCallbackObjectMarkerAndSameReference()
		{
			KingdomGrowthBook growth = Migrated("city-object-callback", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			KingdomGrowthOperation op = RichSow(growth, "field-a", 121L, true);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.WaterIntent, 122L));
			ProveWater(growth, op, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.WaterSettled, 123L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.SourceIntent, 124L));
			ProveObject(growth, op, false, 0);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth,
				growth.SettlementId));
			KingdomGrowthBook valid = RoundTripGrowth(growth);
			KingdomGrowthBook foreign = RoundTripGrowth(valid);
			foreign.FieldOps[0].Operation.Sources[0].ReceiptCallbackObjectId = "foreign";
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(foreign,
				foreign.SettlementId));
			KingdomGrowthBook marker = RoundTripGrowth(valid);
			marker.FieldOps[0].Operation.Sources[0].ReceiptCallbackMarker = "foreign-marker";
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(marker,
				marker.SettlementId));
			KingdomGrowthBook differentReference = RoundTripGrowth(valid);
			differentReference.FieldOps[0].Operation.Sources[0].ReceiptSameReference = false;
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(differentReference,
				differentReference.SettlementId));
			KingdomGrowthBook missing = RoundTripGrowth(valid);
			missing.FieldOps[0].Operation.Sources[0].ReceiptCallbackObjectId = null;
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(missing,
				missing.SettlementId));
			KingdomGrowthBook duplicate = RoundTripGrowth(valid);
			duplicate.FieldOps[0].Operation.Sources[0].ReceiptAfterIdMatches = 2;
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(duplicate,
				duplicate.SettlementId));
		}

		[Test]
		public void CreateCallbackCollisionRefusalRestoresEveryLegReceiptAndCursorByte()
		{
			KingdomGrowthBook growth = Migrated("city-create-rollback", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			KingdomGrowthOperation heartbeat = HeartbeatPlan(growth, 121L);
			SetTarget(heartbeat, "colliding-created", "heartbeat-target-marker");
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, heartbeat));
			KingdomGrowthOperation sow = RichSow(growth, "field-a", 122L, true);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, sow));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, sow,
				KingdomGrowthPhase.WaterIntent, 123L));
			ProveWater(growth, sow, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, sow,
				KingdomGrowthPhase.WaterSettled, 124L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, sow,
				KingdomGrowthPhase.SourceIntent, 125L));
			ProveObject(growth, sow, false, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, sow,
				KingdomGrowthPhase.SourcesSettled, 126L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, sow,
				KingdomGrowthPhase.OutputIntent, 127L));
			BeginObject(growth, sow, true, 0);
			byte[] before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			KingdomGrowthObjectLeg output = sow.Outputs[0];
			Assert.IsFalse(KingdomLifecycleRules.CommitGrowthObjectCallback(growth, sow, true, 0,
				"colliding-created", output.Marker, Digest('a'), true, Digest('b'), Digest('c'),
				Digest('d')));
			CollectionAssert.AreEqual(before,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.IsNull(output.ObjectId);
			Assert.AreEqual(0, output.CallbackCursor);
			Assert.AreEqual(KingdomLifecyclePhysicalState.Intent, output.State);
		}

		[Test]
		public void CellOwnerEmptyNormalizesOnlyAtPublicPreparationBoundary()
		{
			KingdomGrowthBook growth = Migrated("city-owner-null", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			KingdomGrowthOperation op = RichSow(growth, "field-a", 121L, false);
			KingdomGrowthWaterLeg leg = KingdomLifecycleRules.PrepareGrowthWaterLeg(growth, op,
				KingdomGrowthWaterMutationKind.Drain, "water-a", KingdomLifecycleTopology.Cell,
				"", "Waterskin", "zone-a", 1, 1, 10, 10, 2, "fresh", "fresh",
				Digest('1'), Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6'));
			Assert.NotNull(leg);
			Assert.IsNull(leg.OwnerId);
			Assert.IsNull(KingdomLifecycleRules.TopologyId(KingdomLifecycleTopology.Cell,
				"", "zone-a", 1, 1));
			op = RichSow(growth, "field-a", 122L, true);
			op.TargetOwnerId = "";
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			Assert.IsNull(op.PlanHash);
			KingdomGrowthOperation invalid = RichSow(growth, "field-a", 123L, false);
			Assert.IsNull(KingdomLifecycleRules.PrepareGrowthObjectLeg(growth, invalid, true,
				KingdomGrowthObjectMutationKind.InventoryAdd, "object-a", "marker-a", "Crop",
				KingdomLifecycleTopology.Cell, null, "zone-a", 1, 1, 0, 1, true,
				Digest('1'), Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6')),
				"inventory callback cannot claim cell topology");
			Assert.IsNull(KingdomLifecycleRules.PrepareGrowthObjectLeg(growth, invalid, true,
				KingdomGrowthObjectMutationKind.Create, "object-b", "marker-b", "",
				KingdomLifecycleTopology.Cell, null, "zone-a", 1, 1, 0, 1, true,
				Digest('1'), Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6')),
				"empty blueprint cannot become authority");
		}

		[Test]
		public void OpenArrivalSurvivesPauseCutsThenRetirementClearsAndResumeRestamps()
		{
			KingdomGrowthBook growth = Migrated("city-arrival-pause", 100L, true, true, 20L);
			KingdomGrowthOperation op = RichArrival(growth, 120L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			growth = RoundTripGrowth(growth); op = growth.ArrivalOp;
			Assert.AreEqual(KingdomGrowthPhase.Prepared, op.Phase);
			KingdomGrowthAvailabilityDecision disable =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, false, true, 130L, 20L);
			Assert.IsTrue(disable.Valid);
			Assert.AreEqual(120L, disable.NextArrivalTick,
				"open operation retains its exact before clock while starters pause");
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth, disable));
			growth = RoundTripGrowth(growth); op = growth.ArrivalOp;
			Assert.IsTrue(growth.WorkPaused);
			Assert.AreEqual(120L, growth.NextArrivalTick);

			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.ClockIntent, 137L));
			growth = RoundTripGrowth(growth); op = growth.ArrivalOp;
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(growth, op,
				op.ClockLease.Before));
			growth = RoundTripGrowth(growth); op = growth.ArrivalOp;
			Assert.AreEqual(KingdomLifecyclePhysicalState.Intent, op.ClockState);
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(growth, op,
				op.ClockLease.After));
			Assert.AreEqual(140L, growth.NextArrivalTick);
			growth = RoundTripGrowth(growth); op = growth.ArrivalOp;
			Assert.AreEqual(KingdomLifecyclePhysicalState.Proved, op.ClockState);
			byte[] proved = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.CommitGrowthClockWitness(growth, op,
				op.ClockLease.After));
			CollectionAssert.AreEqual(proved,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.Sinks, 138L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.Terminal, 139L));
			growth = RoundTripGrowth(growth); op = growth.ArrivalOp;
			Assert.AreEqual(KingdomGrowthPhase.Terminal, op.Phase);
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowth(growth, op, 140L));
			Assert.IsNull(growth.ArrivalOp);
			Assert.AreEqual(0L, growth.NextArrivalTick);
			growth = RoundTripGrowth(growth);
			byte[] retired = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.RetireGrowth(growth, op, 141L));
			CollectionAssert.AreEqual(retired,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			KingdomGrowthAvailabilityDecision enable =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, true, true, 160L, 20L);
			Assert.IsTrue(enable.Valid);
			Assert.AreEqual(180L, enable.NextArrivalTick);
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth, enable));
			Assert.AreEqual(180L, RoundTripGrowth(growth).NextArrivalTick);
		}

		[Test]
		public void FieldClockCommitAfterPauseNeverRewindsGlobalEffectiveWork()
		{
			KingdomGrowthBook growth = Migrated("city-field-pause", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			KingdomGrowthOperation op = RichSow(growth, "field-a", 121L, true);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.WaterIntent, 122L));
			ProveWater(growth, op, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.WaterSettled, 123L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.SourceIntent, 124L));
			ProveObject(growth, op, false, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.SourcesSettled, 125L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.OutputIntent, 126L));
			ProveObject(growth, op, true, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.OutputsSettled, 127L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainIntent, 128L));
			for (int i = 0; i < op.DomainSteps.Count; i++) ProveDomain(growth, op, i);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainSettled, 129L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.ClockIntent, 129L));
			KingdomGrowthAvailabilityDecision disable =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, false, true, 130L, 20L);
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth, disable));
			Assert.AreEqual(130L, growth.EffectiveWorkTick);
			growth = RoundTripGrowth(growth); op = growth.FieldOps[0].Operation;
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(growth, op,
				op.ClockLease.Before));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(growth, op,
				op.ClockLease.After));
			Assert.AreEqual(121L, growth.FieldOps[0].ClockTick);
			Assert.AreEqual(130L, growth.EffectiveWorkTick);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth,
				growth.SettlementId));
			Assert.AreEqual(130L, RoundTripGrowth(growth).EffectiveWorkTick);
		}

		[Test]
		public void CanonicalQuarantinedV5BoundAndUnboundGraphsStageWithoutAuthority()
		{
			KingdomLifecycleBook bound = Bound("city-v5-quarantine");
			bound.Quarantined = true; bound.Fault = "legacy city quarantine";
			KingdomLifecycleBook loadedBound = ReadLifecycle(WriteV5(bound));
			Assert.IsTrue(loadedBound.Quarantined);
			Assert.AreEqual("legacy city quarantine", loadedBound.Fault);
			Assert.IsTrue(loadedBound.Growth.MigrationPending);
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(loadedBound));
			KingdomGrowthMigrationResult refused = KingdomLifecycleRules.ApplyGrowthMigration(
				loadedBound, Migration(100L, true, true, 20L, 0));
			Assert.IsFalse(refused.Valid);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowthMigration(loadedBound, refused));
			Assert.IsTrue(loadedBound.Growth.MigrationPending);

			KingdomLifecycleBook unbound = new KingdomLifecycleBook
			{
				Quarantined = true, Fault = "legacy unbound quarantine"
			};
			KingdomLifecycleBook loadedUnbound = ReadLifecycle(WriteV5(unbound));
			Assert.IsTrue(loadedUnbound.Quarantined);
			Assert.AreEqual("legacy unbound quarantine", loadedUnbound.Fault);
			Assert.IsFalse(loadedUnbound.Growth.MigrationPending);
			Assert.IsFalse(loadedUnbound.Growth.IdentityBound);
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(loadedUnbound));

			KingdomSettlement archived = new KingdomSettlement { SettlementName = "quarantined" };
			archived.LifecycleBook = Bound("city-v5-quarantine");
			archived.LifecycleBook.Quarantined = true;
			archived.LifecycleBook.Fault = "legacy city quarantine";
			archived.LifecycleBook.FormatVersion =
				KingdomLifecycleRules.LegacyLifecycleFormatVersion;
			archived.LifecycleBook.Growth = null;
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeLegacyV1ForTests(archived,
				out byte[] v1, out string failure), failure);
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v1,
				out KingdomSettlement restored, out int future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.IsTrue(restored.LifecycleBook.Quarantined);
			Assert.AreEqual("legacy city quarantine", restored.LifecycleBook.Fault);
			Assert.IsTrue(restored.LifecycleBook.Growth.MigrationPending);
		}

		[Test]
		public void StrictUtf8RejectsSurrogatesWithoutMutationAndAllowsNarrowSpace()
		{
			KingdomGrowthBook growth = Migrated("city-utf8", 100L, true, true, 20L);
			byte[] initial = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.TryRegisterGrowthField(growth, "\ud800"));
			CollectionAssert.AreEqual(initial,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			string legalField = "field\u202fnorth";
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, legalField));
			byte[] withField = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			KingdomGrowthCropRow badCrop = Crop(legalField, 1);
			badCrop.Blueprint = "Crop\udc00";
			Assert.IsFalse(TryInstallCrop(growth, badCrop));
			Assert.IsFalse(KingdomLifecycleRules.QuarantineGrowthField(growth, legalField,
				"fault\ud800"));
			CollectionAssert.AreEqual(withField,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			KingdomGrowthCropRow legal = Crop(legalField, 2);
			legal.Blueprint = "Crop\u202fNorth";
			Assert.IsTrue(TryInstallCrop(growth, legal));

			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Heartbeat, null, 121L);
			KingdomGrowthOutboxEvent badEvent =
				KingdomLifecycleRules.PrepareGrowthOutboxEvent(op, 0, "bad-utf8",
					"line\ud800", null, null, null, null);
			Assert.IsNull(badEvent);
			byte[] beforePublish = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			CollectionAssert.AreEqual(beforePublish,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.IsNull(op.PlanHash);
		}

		[Test]
		public void ImpossiblePausedAccountingLoadsAsLocalOpaqueButLongPauseResumes()
		{
			KingdomGrowthBook paused = Migrated("city-pause-wire", 100L, false, true, 20L);
			byte[] payload = KingdomLifecycleWireCodec.GrowthPayloadForWrite(paused);
			int offset = UniqueLongTriple(payload, 100L, 0L, 100L);
			Array.Copy(BitConverter.GetBytes(10L), 0, payload, offset, 8);
			Array.Copy(BitConverter.GetBytes(11L), 0, payload, offset + 8, 8);
			Array.Copy(BitConverter.GetBytes(0L), 0, payload, offset + 16, 8);
			KingdomGrowthBook rejected = KingdomLifecycleWireCodec.ReadGrowthPayload(payload);
			Assert.IsTrue(rejected.Quarantined);
			CollectionAssert.AreEqual(payload, rejected.OpaquePayload);
			CollectionAssert.AreEqual(payload,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(rejected));

			KingdomGrowthBook valid = Migrated("city-long-pause", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(valid,
				KingdomLifecycleRules.ObserveGrowthAvailability(valid, false, true, 110L, 20L)));
			KingdomGrowthAvailabilityDecision resume =
				KingdomLifecycleRules.ObserveGrowthAvailability(valid, true, true, 1000L, 20L);
			Assert.IsTrue(resume.Valid);
			Assert.AreEqual(890L, resume.PausedTicks);
			Assert.AreEqual(110L, resume.EffectiveWorkTick);
			Assert.AreEqual(1020L, resume.NextArrivalTick);
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(valid, resume));
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(RoundTripGrowth(valid),
				valid.SettlementId));
		}

		[Test]
		public void AggregateCapRefusesWideCropAtomicallyAndCappedWriterNeverOverallocates()
		{
			KingdomGrowthBook growth = Migrated("city-aggregate-crop", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			bool refused = false;
			KingdomGrowthCropRow refusedRow = null;
			for (int i = 0; i < KingdomLifecycleRules.MaxGrowthCropRows; i++)
			{
				KingdomGrowthCropRow row = WideCrop("field-a", i);
				byte[] before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
				int count = growth.CropRows.Count;
				if (TryInstallCrop(growth, row)) continue;
				refused = true; refusedRow = row;
				Assert.AreEqual(count, growth.CropRows.Count);
				CollectionAssert.AreEqual(before,
					KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
				break;
			}
			Assert.IsTrue(refused, "legal rows must hit aggregate cap before row-count cap");
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth,
				growth.SettlementId));
			growth.CropRows.Add(refusedRow);
			Assert.IsFalse(KingdomLifecycleRules.GrowthEnvelopeWritable(growth));
			Assert.IsFalse(KingdomLifecycleWireCodec.GrowthPayloadFitsAggregateCap(growth));
			Assert.Throws<InvalidDataException>(() =>
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
		}

		[Test]
		public void AggregatePublicationAndClockCommitFailuresRestoreExactGraph()
		{
			KingdomGrowthBook publishBook = NearCapBook("city-aggregate-publish");
			KingdomGrowthOperation candidate = HeartbeatPlan(publishBook, 121L);
			Assert.NotNull(candidate);
			byte[] publishBefore = KingdomLifecycleWireCodec.GrowthPayloadForWrite(publishBook);
			long nextBefore = publishBook.HeartbeatNextSequence;
			int resourcesBefore = publishBook.Resources.Count;
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(publishBook, candidate));
			CollectionAssert.AreEqual(publishBefore,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(publishBook));
			Assert.AreEqual(nextBefore, publishBook.HeartbeatNextSequence);
			Assert.AreEqual(resourcesBefore, publishBook.Resources.Count);
			Assert.IsNull(publishBook.HeartbeatOp);
			Assert.IsNull(candidate.PlanHash);

			KingdomGrowthBook commitBook = Migrated("city-aggregate-commit", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(commitBook, "field-a"));
			for (int i = 0; i < KingdomLifecycleRules.MaxGrowthCropRows; i++)
				Assert.IsTrue(TryInstallCrop(commitBook,
					Crop("field-a", i)));
			KingdomGrowthOperation op = HeartbeatPlan(commitBook, 121L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(commitBook, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(commitBook, op,
				KingdomGrowthPhase.DomainIntent, 122L));
			for (int i = 0; i < op.DomainSteps.Count; i++) ProveDomain(commitBook, op, i);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(commitBook, op,
				KingdomGrowthPhase.DomainSettled, 123L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(commitBook, op,
				KingdomGrowthPhase.ClockIntent, 124L));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(commitBook, op,
				op.ClockLease.Before));
			PadGrowthNearCap(commitBook);
			byte[] commitBefore = KingdomLifecycleWireCodec.GrowthPayloadForWrite(commitBook);
			KingdomLifecycleResourceRevision clockRow = Resource(commitBook, op.ClockLease.Key);
			long revisionBefore = clockRow.Revision;
			string lastBefore = clockRow.LastOperationId;
			Assert.IsFalse(KingdomLifecycleRules.CommitGrowthClockWitness(commitBook, op,
				op.ClockLease.After));
			CollectionAssert.AreEqual(commitBefore,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(commitBook));
			Assert.AreEqual(revisionBefore, clockRow.Revision);
			Assert.AreEqual(lastBefore, clockRow.LastOperationId);
			Assert.AreEqual(KingdomLifecyclePhysicalState.Intent, op.ClockState);
			Assert.AreEqual(KingdomLifecycleLeaseState.Intent, op.ClockLease.State);
		}

		[Test]
		public void RichSowReceiptIntentAndProvedCutsRoundTripInExactPrefixOrder()
		{
			KingdomGrowthBook growth = Migrated("city-receipt-cuts", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			KingdomGrowthOperation op = RichSow(growth, "field-a", 121L, true);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.WaterIntent, 122L));
			BeginWater(growth, op, 0);
			growth = RoundTripGrowth(growth); op = growth.FieldOps[0].Operation;
			Assert.AreEqual(KingdomLifecyclePhysicalState.Intent, op.WaterLegs[0].State);
			Assert.IsNull(op.WaterLegs[0].ReceiptCallbackContainerId);
			ProveWater(growth, op, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.WaterSettled, 123L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.SourceIntent, 124L));
			BeginObject(growth, op, false, 0);
			growth = RoundTripGrowth(growth); op = growth.FieldOps[0].Operation;
			Assert.AreEqual(KingdomLifecyclePhysicalState.Intent, op.Sources[0].State);
			Assert.IsNull(op.Sources[0].ReceiptCallbackObjectId);
			ProveObject(growth, op, false, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.SourcesSettled, 125L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.OutputIntent, 126L));
			BeginObject(growth, op, true, 0);
			growth = RoundTripGrowth(growth); op = growth.FieldOps[0].Operation;
			ProveObject(growth, op, true, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.OutputsSettled, 127L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainIntent, 128L));
			for (int i = 0; i < op.DomainSteps.Count; i++)
			{
				BeginDomain(growth, op, i);
				growth = RoundTripGrowth(growth); op = growth.FieldOps[0].Operation;
				Assert.AreEqual(i, op.DomainCursor);
				Assert.AreEqual(KingdomLifecyclePhysicalState.Intent, op.DomainSteps[i].State);
				ProveDomain(growth, op, i);
			}
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainSettled, 129L));
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth,
				growth.SettlementId));
		}

		[Test]
		public void LifecycleV6GrowthV4AndHistoricalV3RichWireGoldensPinAllPreparedBranches()
		{
			KingdomLifecycleBook parent = MigratedParent("city-rich-golden", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(parent.Growth, "field-a"));
			KingdomGrowthOperation op = RichSow(parent.Growth, "field-a", 121L, true);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(parent.Growth, op));
			byte[] nested = KingdomLifecycleWireCodec.GrowthPayloadForWrite(parent.Growth);
			byte[] wrapper = WriteV6(parent);
			KingdomLifecycleBook independent = MigratedParent("city-rich-golden", 100L,
				true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(independent.Growth,
				"field-a"));
			KingdomGrowthOperation independentOp = RichSow(independent.Growth, "field-a",
				121L, true);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(independent.Growth,
				independentOp));
			byte[] independentNested =
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(independent.Growth);
			byte[] independentWrapper = WriteV6(independent);
			CollectionAssert.AreEqual(nested, independentNested,
				"independent semantic producer emits exact same nested bytes");
			CollectionAssert.AreEqual(wrapper, independentWrapper,
				"independent semantic producer emits exact same wrapper bytes");
			Console.WriteLine("[TAF] growth-v4-rich nested={0} sha={1} wrapper={2} sha={3}",
				nested.Length, Sha256(nested), wrapper.Length, Sha256(wrapper));
			byte[] semanticV3 = KingdomLifecycleWireCodec.GrowthV3PayloadFixture(parent.Growth);
			Assert.AreEqual(KingdomLifecycleRules.SemanticGrowthFormatVersion,
				BitConverter.ToInt32(semanticV3, 4));
			Assert.AreEqual(10699, semanticV3.Length);
			Assert.AreEqual("0d6b0c056cf2e07ecc9de69bee2d1afb1dd3bf6b27b25bc1e37f333feef6c29d",
				Sha256(semanticV3));
			Assert.IsFalse(KingdomLifecycleWireCodec.ReadGrowthPayload(semanticV3).Quarantined);
			int lengthOffset = wrapper.Length - nested.Length - 4;
			byte[] extractedNested = new byte[nested.Length];
			Buffer.BlockCopy(wrapper, lengthOffset + 4, extractedNested, 0, nested.Length);
			CollectionAssert.AreEqual(nested, extractedNested,
				"independent wrapper framing extraction reproduces nested bytes");
			Assert.AreEqual(KingdomLifecycleWireCodec.LifecycleMagic,
				BitConverter.ToInt32(wrapper, 0));
			Assert.AreEqual(KingdomLifecycleRules.PreviousLifecycleFormatVersion,
				BitConverter.ToInt32(wrapper, 4));
			Assert.AreEqual(nested.Length, BitConverter.ToInt32(wrapper, lengthOffset));
			Assert.AreEqual(KingdomLifecycleWireCodec.GrowthMagic,
				BitConverter.ToInt32(nested, 0));
			Assert.AreEqual(KingdomLifecycleRules.CurrentGrowthFormatVersion,
				BitConverter.ToInt32(nested, 4));
			byte[] physicalV6 = KingdomLifecycleWireCodec.GrowthV6PayloadFixture(parent.Growth);
			Assert.Greater(nested.Length, physicalV6.Length);
			Assert.AreEqual(10700, physicalV6.Length);
			Assert.AreEqual("39f0099ea91787ff4f006c1e3d416cdebf89c6629a32c94731c4de9976765dcb",
				Sha256(physicalV6));
			KingdomGrowthBook migratedV6 =
				KingdomLifecycleWireCodec.ReadGrowthPayload(physicalV6);
			Assert.IsTrue(migratedV6.ArrivalCadenceMigrationPending);
			Assert.AreEqual(0, migratedV6.ArrivalDebtRanges.Count);
			Assert.AreEqual((ulong)migratedV6.ArrivalCandidateRetiredThrough,
				migratedV6.ArrivalOrdinalHighWater);
			byte[] physicalV6Wrapper = new byte[lengthOffset + 4 + physicalV6.Length];
			Buffer.BlockCopy(wrapper, 0, physicalV6Wrapper, 0, lengthOffset);
			Buffer.BlockCopy(BitConverter.GetBytes(physicalV6.Length), 0, physicalV6Wrapper,
				lengthOffset, 4);
			Buffer.BlockCopy(physicalV6, 0, physicalV6Wrapper, lengthOffset + 4,
				physicalV6.Length);
			Assert.AreEqual(10945, physicalV6Wrapper.Length);
			Assert.AreEqual("290c12b831b75a55ad1dc7dfea054d4d9827720104c5a4c922feeb99cbb66bd4",
				Sha256(physicalV6Wrapper));

			// Growth v1 stays byte-exact and readable. V2 added the compatibility bit and dual
			// Chronicle registers; v3 adds semantic-person fields and v4 first-guest authority.
			byte[] legacyNested = KingdomLifecycleWireCodec.GrowthV1PayloadFixture(parent.Growth);
			Assert.AreEqual(KingdomLifecycleRules.LegacyGrowthFormatVersion,
				BitConverter.ToInt32(legacyNested, 4));
			Assert.AreEqual(10698, legacyNested.Length);
			Assert.AreEqual("dce983f6337b71eebbeea78781069b6c6511a9e02c9458645df8dd9e64e6d715",
				Sha256(legacyNested));
			byte[] legacyWrapper = new byte[lengthOffset + 4 + legacyNested.Length];
			Buffer.BlockCopy(wrapper, 0, legacyWrapper, 0, lengthOffset);
			Buffer.BlockCopy(BitConverter.GetBytes(legacyNested.Length), 0, legacyWrapper,
				lengthOffset, 4);
			Buffer.BlockCopy(legacyNested, 0, legacyWrapper, lengthOffset + 4,
				legacyNested.Length);
			Assert.AreEqual(10943, legacyWrapper.Length);
			Assert.AreEqual("1790a41325591d09dc4bb788c667e55f03ea1bb8e45d107027e8c5ae682462a8",
				Sha256(legacyWrapper));
			KingdomLifecycleBook legacyOuterLoaded = ReadLifecycle(legacyWrapper);
			Assert.IsFalse(legacyOuterLoaded.Growth.Quarantined);
			Assert.IsTrue(legacyOuterLoaded.Growth.FieldOps[0].Operation.LegacyGrowthV1Plan);
			KingdomGrowthBook legacyLoaded =
				KingdomLifecycleWireCodec.ReadGrowthPayload(legacyNested);
			Assert.IsFalse(legacyLoaded.Quarantined);
			Assert.AreEqual(KingdomLifecycleRules.CurrentGrowthFormatVersion,
				legacyLoaded.FormatVersion);
			Assert.IsTrue(legacyLoaded.FieldOps[0].Operation.LegacyGrowthV1Plan);
			byte[] rewritten = KingdomLifecycleWireCodec.GrowthPayloadForWrite(legacyLoaded);
			Assert.AreEqual(KingdomLifecycleRules.CurrentGrowthFormatVersion,
				BitConverter.ToInt32(rewritten, 4));
			Assert.IsFalse(KingdomLifecycleWireCodec.ReadGrowthPayload(rewritten).Quarantined);

			KingdomLifecycleBook loaded = ReadLifecycle(wrapper);
			Assert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion, loaded.FormatVersion);
			Assert.IsTrue(KingdomRaidIncidentRules.ValidLedger(loaded.RaidLedger));
			Assert.AreEqual(0, loaded.RaidLedger.Grievances.Count);
			KingdomGrowthOperation loadedOp = loaded.Growth.FieldOps[0].Operation;
			Assert.AreEqual(KingdomGrowthPhase.Prepared, loadedOp.Phase);
			Assert.AreEqual(1, loadedOp.WaterLegs.Count);
			Assert.AreEqual(1, loadedOp.Sources.Count);
			Assert.AreEqual(1, loadedOp.Outputs.Count);
			Assert.AreEqual(2, loadedOp.DomainSteps.Count);
			Assert.IsNull(loadedOp.WaterLegs[0].ReceiptCallbackContainerId);
			Assert.IsFalse(loadedOp.WaterLegs[0].ReceiptSameReference);
			Assert.IsNull(loadedOp.Outputs[0].ReceiptCallbackObjectId);
			Assert.IsFalse(loadedOp.Outputs[0].ReceiptSameReference);
			byte[] currentWrapper = WriteLifecycle(loaded);
			Assert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion,
				BitConverter.ToInt32(currentWrapper, 4));
			Assert.Greater(currentWrapper.Length, wrapper.Length);
		}

		[Test]
		public void GrowthV1ChronicleReceiptMigratesWithoutInventingOutsiderProof()
		{
			KingdomGrowthBook growth = Migrated("city-v1-chronicle", 100L, true, true, 20L);
			KingdomGrowthOperation op = HeartbeatPlan(growth, 121L);
			KingdomGrowthOutboxEvent e = KingdomLifecycleRules.PrepareDeclaredGrowthOutboxEvent(op,
				0, "legacy", "chronicle", "official legacy", "outsider legacy", "ledger", null, null, null,
				2, Digest('1'), 3, Digest('2'), 4, Digest('3'), 5, Digest('4'),
				6, Digest('5'), 7, Digest('6'));
			Assert.NotNull(e); op.OutboxEvents.Add(e);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));

			// Recreate the exact semantic form historical v1 could persist. It remains valid
			// evidence, but current publication cannot mint another one-register promise.
			op.LegacyGrowthV1Plan = true;
			e.LegacySingleRegisterChronicle = true;
			e.ChronicleOfficial = null; e.ChronicleOutsider = null;
			e.OutsiderBeforeCount = 0; e.OutsiderDeclaredAfterCount = 0;
			e.OutsiderObservedCount = -1; e.OutsiderBeforeHash = null;
			e.OutsiderDeclaredAfterHash = null; e.OutsiderObservedHash = null;
			Assert.IsTrue(KingdomLifecycleRules.TryGrowthPlanHash(op, out string planHash));
			op.PlanHash = planHash;
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth,
				growth.SettlementId));

			byte[] v1 = KingdomLifecycleWireCodec.GrowthV1PayloadFixture(growth);
			KingdomGrowthBook loaded = KingdomLifecycleWireCodec.ReadGrowthPayload(v1);
			Assert.IsFalse(loaded.Quarantined);
			Assert.IsTrue(loaded.HeartbeatOp.LegacyGrowthV1Plan);
			Assert.IsTrue(loaded.HeartbeatOp.OutboxEvents[0].LegacySingleRegisterChronicle);
			Assert.AreEqual(-1, loaded.HeartbeatOp.OutboxEvents[0].OutsiderObservedCount);
			Assert.IsNull(loaded.HeartbeatOp.OutboxEvents[0].OutsiderDeclaredAfterHash);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(loaded,
				loaded.SettlementId));
			Assert.IsNull(KingdomLifecycleRules.PrepareGrowthOutboxEvent(
				HeartbeatPlan(Migrated("city-v2-no-legacy", 100L, true, true, 20L), 121L),
				0, "legacy", "chronicle", null, null, null, null,
				2, Digest('1'), 3, Digest('2'), 0, null, 0, null),
				"v2 never mints the dishonest one-register Chronicle receipt");
		}

		[Test]
		public void CurrentChronicleDeclarationsAreFrozenAndCannotMasqueradeAsV1()
		{
			KingdomGrowthBook growth = Migrated("city-v2-frozen-chronicle",
				100L, true, true, 20L);
			KingdomGrowthOperation op = HeartbeatPlan(growth, 121L);
			KingdomGrowthOutboxEvent e = KingdomLifecycleRules.PrepareDeclaredGrowthOutboxEvent(op,
				0, "frozen", "chronicle fingerprint", "official frozen text",
				"outsider frozen text", null, null, null, null,
				2, Digest('1'), 3, Digest('2'), 4, Digest('3'), 5, Digest('4'),
				0, null, 0, null);
			Assert.NotNull(e); op.OutboxEvents.Add(e);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));

			KingdomGrowthBook loaded = RoundTripGrowth(growth);
			Assert.AreEqual("official frozen text",
				loaded.HeartbeatOp.OutboxEvents[0].ChronicleOfficial);
			Assert.AreEqual("outsider frozen text",
				loaded.HeartbeatOp.OutboxEvents[0].ChronicleOutsider);
			Assert.Throws<InvalidDataException>(() =>
				KingdomLifecycleWireCodec.GrowthV1PayloadFixture(loaded),
				"v2 dual-register promises have no honest v1 representation");

			KingdomGrowthBook officialTamper = RoundTripGrowth(growth);
			officialTamper.HeartbeatOp.OutboxEvents[0].ChronicleOfficial += " altered";
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(officialTamper,
				officialTamper.SettlementId));
			KingdomGrowthBook outsiderTamper = RoundTripGrowth(growth);
			outsiderTamper.HeartbeatOp.OutboxEvents[0].ChronicleOutsider += " altered";
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(outsiderTamper,
				outsiderTamper.SettlementId));
		}

		[Test]
		public void GrowthOutboxUsesInspectableCasAndAtMostOnceRecoveryAcrossSaveCuts()
		{
			KingdomGrowthBook growth = Migrated("city-outbox-cas", 100L, true, true, 20L);
			KingdomGrowthOperation op = HeartbeatPlan(growth, 121L);
			KingdomGrowthOutboxEvent e = KingdomLifecycleRules.PrepareDeclaredGrowthOutboxEvent(op,
				0, "heartbeat", "chronicle line", "official heartbeat", "outsider heartbeat",
				"ledger line", "message", null, null,
				2, Digest('1'), 3, Digest('2'), 7, Digest('5'), 8, Digest('6'),
				5, Digest('3'), 6, Digest('4'));
			Assert.NotNull(e); op.OutboxEvents.Add(e);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainIntent, 122L));
			for (int i = 0; i < op.DomainSteps.Count; i++) ProveDomain(growth, op, i);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainSettled, 123L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.ClockIntent, 124L));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(growth, op,
				op.ClockLease.Before));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(growth, op,
				op.ClockLease.After));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.Sinks, 125L));

			Assert.AreEqual(KingdomLifecycleCasAction.Apply,
				KingdomLifecycleRules.GrowthChronicleOutboxAction(growth, op, 0,
					2, Digest('1'), 7, Digest('5')));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthChronicleOutbox(growth, op, 0,
				2, Digest('1'), 7, Digest('5')));
			growth = RoundTripGrowth(growth); op = growth.HeartbeatOp;
			Assert.AreEqual(KingdomLifecycleCasAction.Apply,
				KingdomLifecycleRules.GrowthChronicleOutboxAction(growth, op, 0,
					2, Digest('1'), 7, Digest('5')),
				"Intent plus exact before remains safely applicable");
			byte[] beforeThird = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.CommitGrowthChronicleOutbox(growth, op, 0,
				3, Digest('2'), 9, Digest('6')));
			CollectionAssert.AreEqual(beforeThird,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthChronicleOutbox(growth, op, 0,
				3, Digest('2'), 8, Digest('6')));

			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthInspectableOutbox(growth, op, 0,
				KingdomGrowthOutboxSinkKind.Ledger, 5, Digest('3')));
			growth = RoundTripGrowth(growth); op = growth.HeartbeatOp;
			Assert.AreEqual(KingdomLifecycleCasAction.Confirm,
				KingdomLifecycleRules.GrowthInspectableOutboxAction(growth, op, 0,
					KingdomGrowthOutboxSinkKind.Ledger, 6, Digest('4')));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthInspectableOutbox(growth, op, 0,
				KingdomGrowthOutboxSinkKind.Ledger, 6, Digest('4')));

			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthAtMostOnceOutbox(growth, op, 0,
				KingdomGrowthOutboxSinkKind.Message));
			growth = RoundTripGrowth(growth); op = growth.HeartbeatOp;
			Assert.IsTrue(KingdomLifecycleRules.RecoverGrowthOutbox(growth, op));
			Assert.AreEqual(KingdomLifecycleSinkState.Delivered,
				op.OutboxEvents[0].Outbox.ChronicleState);
			Assert.AreEqual(KingdomLifecycleSinkState.Delivered,
				op.OutboxEvents[0].Outbox.LedgerState);
			Assert.AreEqual(KingdomLifecycleSinkState.Lost,
				op.OutboxEvents[0].Outbox.MessageState);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.Terminal, 126L));
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowth(growth, op, 127L));
		}

		[Test]
		public void GrowthChronicleDualRegisterReceiptsSurviveCapAndOutsiderCut()
		{
			KingdomGrowthBook growth = Migrated("city-outbox-cap", 100L, true, true, 20L);
			KingdomGrowthOperation op = HeartbeatPlan(growth, 121L);
			KingdomGrowthOutboxEvent e = KingdomLifecycleRules.PrepareDeclaredGrowthOutboxEvent(op,
				0, "cap", "chronicle line", "official cap", "outsider cap", null, null, null, null,
				KingdomChronicleReceiptRules.MaxEntries, Digest('1'),
				KingdomChronicleReceiptRules.MaxEntries, Digest('2'),
				KingdomChronicleReceiptRules.MaxEntries, Digest('3'),
				KingdomChronicleReceiptRules.MaxEntries, Digest('4'),
				0, null, 0, null);
			Assert.NotNull(e, "bounded append changes hashes while both counts stay at cap");
			op.OutboxEvents.Add(e);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainIntent, 122L));
			for (int i = 0; i < op.DomainSteps.Count; i++) ProveDomain(growth, op, i);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainSettled, 123L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.ClockIntent, 124L));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(growth, op,
				op.ClockLease.Before));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(growth, op,
				op.ClockLease.After));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.Sinks, 125L));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthChronicleOutbox(growth, op, 0,
				KingdomChronicleReceiptRules.MaxEntries, Digest('1'),
				KingdomChronicleReceiptRules.MaxEntries, Digest('3')));

			growth = RoundTripGrowth(growth); op = growth.HeartbeatOp;
			byte[] intent = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.AreEqual(KingdomLifecycleCasAction.Apply,
				KingdomLifecycleRules.GrowthChronicleOutboxAction(growth, op, 0,
					KingdomChronicleReceiptRules.MaxEntries, Digest('2'),
					KingdomChronicleReceiptRules.MaxEntries, Digest('3')),
				"the ordered official-only cut resumes the frozen outsider delivery");
			Assert.IsFalse(KingdomLifecycleRules.CommitGrowthChronicleOutbox(growth, op, 0,
				KingdomChronicleReceiptRules.MaxEntries, Digest('2'),
				KingdomChronicleReceiptRules.MaxEntries, Digest('3')));
			CollectionAssert.AreEqual(intent,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.AreEqual(KingdomLifecycleCasAction.Quarantine,
				KingdomLifecycleRules.GrowthChronicleOutboxAction(growth, op, 0,
					KingdomChronicleReceiptRules.MaxEntries, Digest('1'),
					KingdomChronicleReceiptRules.MaxEntries, Digest('4')),
				"outsider-before-official cannot be produced by the delivery order");
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthChronicleOutbox(growth, op, 0,
				KingdomChronicleReceiptRules.MaxEntries, Digest('2'),
				KingdomChronicleReceiptRules.MaxEntries, Digest('4')));
			Assert.AreEqual(KingdomChronicleReceiptRules.MaxEntries,
				op.OutboxEvents[0].OutsiderObservedCount);

			Assert.IsNull(KingdomLifecycleRules.PrepareDeclaredGrowthOutboxEvent(
				HeartbeatPlan(Migrated("city-outbox-over-cap", 100L, true, true, 20L), 121L),
				0, "over-cap", "line", "official over-cap", "outsider over-cap",
				null, null, null, null,
				KingdomChronicleReceiptRules.MaxEntries, Digest('1'),
				KingdomChronicleReceiptRules.MaxEntries + 1, Digest('2'),
				KingdomChronicleReceiptRules.MaxEntries, Digest('3'),
				KingdomChronicleReceiptRules.MaxEntries, Digest('4'), 0, null, 0, null));
		}

		[Test]
		public void FetchAndMillPlansBindExactConservationAndRejectReorderedOrPorterEvidence()
		{
			KingdomGrowthBook fetchBook = Migrated("city-fetch-plan", 100L, true, true, 20L);
			KingdomGrowthOperation fetch = KingdomLifecycleRules.PrepareGrowthOperation(fetchBook,
				KingdomGrowthAction.Fetch, null, 121L);
			Assert.NotNull(fetch);
			fetch.WaterLegs.Add(Water(fetchBook, fetch,
				KingdomGrowthWaterMutationKind.Drain, "pool-a", 4, 2));
			fetch.WaterLegs.Add(Water(fetchBook, fetch,
				KingdomGrowthWaterMutationKind.Drain, "pool-b", 5, 3));
			fetch.WaterLegs.Add(Water(fetchBook, fetch,
				KingdomGrowthWaterMutationKind.Fill, "store-a", 0, 2));
			fetch.WaterLegs.Add(Water(fetchBook, fetch,
				KingdomGrowthWaterMutationKind.Fill, "store-b", 0, 1));
			fetch.WaterLegs.Add(Water(fetchBook, fetch,
				KingdomGrowthWaterMutationKind.Fill, "store-c", 0, 2));
			KingdomGrowthAccountingSnapshot fetchAfter = EmptyAccounting();
			fetchAfter.Fetched = 5;
			fetch.DomainSteps.Add(Accounting(fetchBook, fetch, EmptyAccounting(), fetchAfter));
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(fetchBook, fetch),
				"two pools and three stores conserve exactly");
			fetchBook = RoundTripGrowth(fetchBook);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(fetchBook,
				fetchBook.SettlementId));

			KingdomGrowthBook reorderedBook = Migrated("city-fetch-order", 100L, true, true, 20L);
			KingdomGrowthOperation reordered = KingdomLifecycleRules.PrepareGrowthOperation(
				reorderedBook, KingdomGrowthAction.Fetch, null, 121L);
			reordered.WaterLegs.Add(Water(reorderedBook, reordered,
				KingdomGrowthWaterMutationKind.Fill, "store-first", 0, 1));
			reordered.WaterLegs.Add(Water(reorderedBook, reordered,
				KingdomGrowthWaterMutationKind.Drain, "pool-late", 1, 1));
			KingdomGrowthAccountingSnapshot reorderedAfter = EmptyAccounting();
			reorderedAfter.Fetched = 1;
			reordered.DomainSteps.Add(Accounting(reorderedBook, reordered, EmptyAccounting(),
				reorderedAfter));
			byte[] beforeReordered = KingdomLifecycleWireCodec.GrowthPayloadForWrite(reorderedBook);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(reorderedBook, reordered));
			CollectionAssert.AreEqual(beforeReordered,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(reorderedBook));
			Assert.IsNull(reordered.PlanHash);

			KingdomGrowthBook millBook = Migrated("city-mill-plan", 100L, true, true, 20L);
			KingdomGrowthOperation mill = KingdomLifecycleRules.PrepareGrowthOperation(millBook,
				KingdomGrowthAction.Mill, null, 121L);
			mill.MillCropBlueprint = "Crop"; mill.MillStapleBlueprint = "Staple";
			mill.Sources.Add(Destroy(millBook, mill, "crop-a", "crop-marker-a", "Crop"));
			mill.Sources.Add(Destroy(millBook, mill, "crop-b", "crop-marker-b", "Crop"));
			mill.Outputs.Add(Create(millBook, mill, "staple-marker", "Staple", 6));
			KingdomGrowthAccountingSnapshot millAfter = EmptyAccounting();
			millAfter.Milled = 4;
			mill.DomainSteps.Add(Accounting(millBook, mill, EmptyAccounting(), millAfter));
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(millBook, mill));
			KingdomGrowthBook wrongMillBook = Migrated("city-mill-wrong", 100L, true, true, 20L);
			KingdomGrowthOperation wrongMill = KingdomLifecycleRules.PrepareGrowthOperation(
				wrongMillBook, KingdomGrowthAction.Mill, null, 121L);
			wrongMill.MillCropBlueprint = "Crop"; wrongMill.MillStapleBlueprint = "Staple";
			wrongMill.Sources.Add(Destroy(wrongMillBook, wrongMill, "foreign-mill",
				"foreign-mill-marker", "ForeignCrop"));
			wrongMill.Outputs.Add(Create(wrongMillBook, wrongMill, "wrong-staple", "Staple", 3));
			KingdomGrowthAccountingSnapshot wrongMillAfter = EmptyAccounting();
			wrongMillAfter.Milled = 2;
			wrongMill.DomainSteps.Add(Accounting(wrongMillBook, wrongMill, EmptyAccounting(),
				wrongMillAfter));
			byte[] beforeWrongMill = KingdomLifecycleWireCodec.GrowthPayloadForWrite(wrongMillBook);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(wrongMillBook, wrongMill));
			CollectionAssert.AreEqual(beforeWrongMill,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(wrongMillBook));
			Assert.IsNull(wrongMill.PlanHash);

			KingdomGrowthOperation porter = KingdomLifecycleRules.PrepareGrowthOperation(
				Migrated("city-porter-reserved", 100L, true, true, 20L),
				KingdomGrowthAction.Delivery, null, 121L);
			porter.DeliveryMode = KingdomGrowthDeliveryMode.PorterJob;
			Assert.IsFalse(PrivateBool("GrowthVariantScalarsValid", porter),
				"PorterJob remains reserved until transactional porter authority exists");
		}

		[Test]
		public void DeliveryOutputMustMatchFrozenPendingCropTuple()
		{
			KingdomGrowthBook validBook = Migrated("city-delivery", 100L, true, true, 20L, 2);
			KingdomGrowthOperation valid = KingdomLifecycleRules.PrepareGrowthOperation(validBook,
				KingdomGrowthAction.Delivery, null, 121L);
			Assert.NotNull(valid); valid.DeliveryMode = KingdomGrowthDeliveryMode.PlainLarder;
			valid.PendingCropDelta = -2; valid.PendingCropAfter = 0;
			valid.PendingCropBlueprintAfter = null; valid.PendingCropZoneIdAfter = null;
			valid.Outputs.Add(Create(validBook, valid, "delivery-crop", "Crop", 2));
			valid.DomainSteps.Add(Domain(validBook, valid,
				KingdomGrowthDomainStepKind.PendingCrop,
				KingdomGrowthDomainCallbackKind.PendingCropSet, validBook.SettlementId,
				validBook.SettlementId, 2L, 0L));
			KingdomGrowthAccountingSnapshot after = EmptyAccounting(); after.Delivered = 2;
			valid.DomainSteps.Add(Accounting(validBook, valid, EmptyAccounting(), after));
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(validBook, valid));

			KingdomGrowthBook badBook = Migrated("city-delivery-bad", 100L, true, true, 20L, 2);
			KingdomGrowthOperation bad = KingdomLifecycleRules.PrepareGrowthOperation(badBook,
				KingdomGrowthAction.Delivery, null, 121L);
			bad.DeliveryMode = KingdomGrowthDeliveryMode.PlainLarder;
			bad.PendingCropDelta = -2; bad.PendingCropAfter = 0;
			bad.PendingCropBlueprintAfter = null; bad.PendingCropZoneIdAfter = null;
			bad.Outputs.Add(Create(badBook, bad, "delivery-foreign", "ForeignCrop", 2));
			bad.DomainSteps.Add(Domain(badBook, bad, KingdomGrowthDomainStepKind.PendingCrop,
				KingdomGrowthDomainCallbackKind.PendingCropSet, badBook.SettlementId,
				badBook.SettlementId, 2L, 0L));
			bad.DomainSteps.Add(Accounting(badBook, bad, EmptyAccounting(), after));
			byte[] before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(badBook);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(badBook, bad));
			CollectionAssert.AreEqual(before,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(badBook));
			Assert.IsNull(bad.PlanHash);
		}

		[Test]
		public void ArrivalCandidateRefusalHasOneReceiptPerCallbackAcrossEverySaveCut()
		{
			KingdomGrowthBook growth = Migrated("city-candidate-refusal", 100L, true, true, 20L);
			KingdomGrowthArrivalCandidate candidate =
				KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth, "arrival-marker",
					"Settler", "arrival-escrow", "zone-a", 120L, Digest('1'), Digest('2'), Digest('3'));
			Assert.NotNull(candidate); Assert.IsNull(candidate.ObjectId);
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.Prepared, candidate.Phase);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(growth, candidate));
			growth = RoundTripGrowth(growth); candidate = growth.ArrivalCandidate;
			Assert.IsNull(candidate.ObjectId, "prepared save precedes GameObject.Create");

			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateCreate(growth,
				candidate, 121L));
			growth = RoundTripGrowth(growth); candidate = growth.ArrivalCandidate;
			Assert.AreEqual(KingdomLifecycleLeaseState.Intent, candidate.CandidateLease.State);
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalCandidateCreate(growth,
				candidate, "settler-final", Digest('4'), Digest('5'), Digest('6'),
				Digest('7'), true, 122L));
			growth = RoundTripGrowth(growth); candidate = growth.ArrivalCandidate;
			Assert.AreEqual("settler-final", candidate.ObjectId);
			Assert.AreEqual(KingdomLifecycleLeaseState.Proved, candidate.CandidateLease.State);
			string baseCandidatePlanHash = candidate.PlanHash;

			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalLodgingObservation(growth,
				candidate, "zone-a", 2, 3, Digest('8'), 123L));
			growth = RoundTripGrowth(growth); candidate = growth.ArrivalCandidate;
			Assert.AreEqual(KingdomLifecycleLeaseState.Intent, candidate.LodgingLease.State);
			byte[] beforeBadReason = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.CommitGrowthArrivalLodgingObservation(growth,
				candidate, KingdomGrowthArrivalDisposition.NoAcceptableHome,
				KingdomGrowthArrivalRefusalReason.None, Digest('9'), Digest('a'), true, 124L));
			Assert.IsFalse(KingdomLifecycleRules.CommitGrowthArrivalLodgingObservation(growth,
				candidate, KingdomGrowthArrivalDisposition.Joined,
				KingdomGrowthArrivalRefusalReason.Refused, Digest('9'), Digest('a'), true, 124L));
			CollectionAssert.AreEqual(beforeBadReason,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalLodgingObservation(growth,
				candidate, KingdomGrowthArrivalDisposition.NoAcceptableHome,
				KingdomGrowthArrivalRefusalReason.Refused, Digest('9'),
				Digest('a'), true, 124L));
			Assert.AreEqual(KingdomGrowthArrivalRefusalReason.Refused,
				candidate.RefusalReason);
			Assert.AreNotEqual(candidate.LodgingReceiptGraphHash,
				candidate.LodgingDeclaredGraphHash,
				"the declared field is a plan-bound observation proof, not caller echo");
			Assert.AreNotEqual(baseCandidatePlanHash, candidate.PlanHash,
				"the observed plan freezes the exact lodging result");
			KingdomGrowthBook changedOutcome = RoundTripGrowth(growth);
			changedOutcome.ArrivalCandidate.Disposition = KingdomGrowthArrivalDisposition.Joined;
			changedOutcome.ArrivalCandidate.RefusalReason =
				KingdomGrowthArrivalRefusalReason.None;
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(changedOutcome,
				changedOutcome.SettlementId));
			KingdomGrowthBook changedCoordinate = RoundTripGrowth(growth);
			changedCoordinate.ArrivalCandidate.LodgingX++;
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(changedCoordinate,
				changedCoordinate.SettlementId));
			KingdomGrowthBook changedReceipt = RoundTripGrowth(growth);
			changedReceipt.ArrivalCandidate.LodgingReceiptGraphHash = Digest('b');
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(changedReceipt,
				changedReceipt.SettlementId));
			KingdomGrowthBook changedCallback = RoundTripGrowth(growth);
			changedCallback.ArrivalCandidate.LodgingCallbackReferenceHash = Digest('c');
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(changedCallback,
				changedCallback.SettlementId));
			growth = RoundTripGrowth(growth); candidate = growth.ArrivalCandidate;
			Assert.AreEqual(KingdomGrowthArrivalRefusalReason.Refused,
				candidate.RefusalReason);
			growth = RoundTripGrowth(growth); candidate = growth.ArrivalCandidate;
			Assert.AreEqual(KingdomLifecycleLeaseState.Proved, candidate.LodgingLease.State);
			KingdomGrowthOperation arrival = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Arrival, null, 125L);
			Assert.NotNull(arrival);
			arrival.ArrivalDisposition = KingdomGrowthArrivalDisposition.NoAcceptableHome;
			arrival.ArrivalCandidateId = candidate.Id;
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, arrival));

			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateDisposition(growth,
				candidate, arrival.Id, KingdomGrowthObjectMutationKind.Obliterate,
				KingdomGrowthLocationKind.Graveyard, null, null, -1, -1, Digest('b'),
				Digest('c'), Digest('d'), Digest('e'), Digest('f'), Digest('0'), 126L));
			growth = RoundTripGrowth(growth); candidate = growth.ArrivalCandidate;
			arrival = growth.ArrivalOp;
			Assert.AreEqual(KingdomLifecycleLeaseState.Intent, candidate.EscrowLease.State);
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalCandidateDisposition(growth,
				candidate, Digest('1'), false, 127L));
			growth = RoundTripGrowth(growth); candidate = growth.ArrivalCandidate;
			arrival = growth.ArrivalOp;
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.Settled, candidate.Phase);
			Assert.AreEqual(KingdomLifecycleLeaseState.Proved, candidate.EscrowLease.State);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, arrival,
				KingdomGrowthPhase.ClockIntent, 128L));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(growth, arrival,
				arrival.ClockLease.Before));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(growth, arrival,
				arrival.ClockLease.After));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, arrival,
				KingdomGrowthPhase.Sinks, 129L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, arrival,
				KingdomGrowthPhase.Terminal, 130L));
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowth(growth, arrival, 131L));
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowthArrivalCandidate(growth, candidate));
			Assert.IsNull(growth.ArrivalCandidate);
			Assert.AreEqual(1L, growth.ArrivalCandidateRetiredThrough);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth, growth.SettlementId));
		}

		[Test]
		public void JoinedArrivalSharesCandidateIdentityAndOnlyConsumesIntoObservedLodging()
		{
			KingdomGrowthBook growth = Migrated("city-candidate-joined", 100L, true, true, 20L);
			KingdomGrowthArrivalCandidate candidate =
				KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth, "joined-marker",
					"Settler", "joined-escrow", "zone-a", 120L, Digest('1'), Digest('2'), Digest('3'));
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(growth, candidate));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateCreate(growth,
				candidate, 121L));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalCandidateCreate(growth,
				candidate, "joined-object", Digest('4'), Digest('5'), Digest('6'), Digest('7'),
				true, 122L));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalLodgingObservation(growth,
				candidate, "zone-a", 2, 3, Digest('8'), 123L));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalLodgingObservation(growth,
				candidate, KingdomGrowthArrivalDisposition.Joined,
				KingdomGrowthArrivalRefusalReason.None, Digest('9'), Digest('a'), true,
				124L));

			KingdomGrowthOperation arrival = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Arrival, null, 125L);
			arrival.ArrivalDisposition = KingdomGrowthArrivalDisposition.Joined;
			arrival.ArrivalCandidateId = candidate.Id;
			arrival.TargetId = candidate.ObjectId; arrival.TargetMarker = candidate.Marker;
			arrival.Blueprint = candidate.Blueprint; arrival.ZoneId = candidate.LodgingZoneId;
			arrival.TargetTopology = KingdomLifecycleTopology.Cell;
			arrival.TargetLocation = KingdomGrowthLocationKind.Cell;
			arrival.TargetOwnerId = null; arrival.TargetX = candidate.LodgingX;
			arrival.TargetY = candidate.LodgingY;
			arrival.PopulationBefore = 1; arrival.PopulationDelta = 1; arrival.PopulationAfter = 2;
			arrival.WaterLegs.Add(Water(growth, arrival,
				KingdomGrowthWaterMutationKind.Drain, "arrival-water", 2, 2));
			arrival.DomainSteps.Add(Domain(growth, arrival,
				KingdomGrowthDomainStepKind.Enrollment,
				KingdomGrowthDomainCallbackKind.Enroll, candidate.ObjectId, candidate.ObjectId, 0, 1));
			arrival.DomainSteps.Add(Domain(growth, arrival, KingdomGrowthDomainStepKind.Roster,
				KingdomGrowthDomainCallbackKind.RosterAdd, candidate.ObjectId, candidate.ObjectId,
				0, 1));
			arrival.DomainSteps.Add(Domain(growth, arrival, KingdomGrowthDomainStepKind.Creed,
				KingdomGrowthDomainCallbackKind.CreedSet, candidate.ObjectId, candidate.ObjectId,
				0, 1));
			arrival.DomainSteps.Add(Domain(growth, arrival, KingdomGrowthDomainStepKind.Population,
				KingdomGrowthDomainCallbackKind.PopulationAdjust, candidate.ObjectId,
				growth.SettlementId, 1, 2));
			KingdomGrowthAccountingSnapshot after = EmptyAccounting();
			after.ArrivalCost = 2; after.Arrivals = 1;
			arrival.DomainSteps.Add(Accounting(growth, arrival, EmptyAccounting(), after));
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, arrival),
				"linked candidate and operation share one exact physical identity");
			KingdomGrowthBook retained = RoundTripGrowth(growth);
			KingdomGrowthOperation retainedOp = retained.ArrivalOp;
			KingdomGrowthArrivalCandidate retainedCandidate = retained.ArrivalCandidate;
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(retained, retainedOp,
				KingdomGrowthPhase.WaterIntent, 126L));
			Assert.IsTrue(KingdomLifecycleRules.QuarantineGrowthOperation(retained,
				retainedOp, "arrival water entered a third state"));
			Assert.IsTrue(KingdomLifecycleRules.QuarantineGrowthArrivalCandidate(retained,
				retainedCandidate, "linked candidate retained beside failed arrival"));
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.Observed,
				retainedCandidate.EvidencePhase);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(retained,
				retained.SettlementId));
			retained = RoundTripGrowth(retained);
			Assert.AreEqual(KingdomGrowthPhase.Quarantined, retained.ArrivalOp.Phase);
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.Quarantined,
				retained.ArrivalCandidate.Phase);
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.Observed,
				retained.ArrivalCandidate.EvidencePhase);

			byte[] beforeWrong = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.BeginGrowthArrivalCandidateDisposition(growth,
				candidate, arrival.Id, KingdomGrowthObjectMutationKind.InventoryAdd,
				KingdomGrowthLocationKind.Inventory, "foreign-owner", "zone-a", -1, -1,
				Digest('1'), Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6'), 126L));
			CollectionAssert.AreEqual(beforeWrong,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateDisposition(growth,
				candidate, arrival.Id, KingdomGrowthObjectMutationKind.CellAdd,
				KingdomGrowthLocationKind.Cell, null, candidate.LodgingZoneId, candidate.LodgingX,
				candidate.LodgingY, Digest('1'), Digest('2'), Digest('3'), Digest('4'),
				Digest('5'), Digest('6'), 126L));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalCandidateDisposition(growth,
				candidate, Digest('7'), true, 127L));
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.Settled, candidate.Phase);
		}

		[Test]
		public void HistoricalV1ObservedCandidateMigratesItsLodgingIdentityExactly()
		{
			KingdomGrowthBook growth = Migrated("city-candidate-v1", 100L, true, true, 20L);
			KingdomGrowthArrivalCandidate candidate =
				KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth, "v1-marker",
					"Settler", "v1-escrow", "zone-a", 120L, Digest('1'), Digest('2'), Digest('3'));
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(growth, candidate));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateCreate(growth,
				candidate, 121L));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalCandidateCreate(growth,
				candidate, "v1-object", Digest('4'), Digest('5'), Digest('6'), Digest('7'),
				true, 122L));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalLodgingObservation(growth,
				candidate, "zone-a", 2, 3, Digest('8'), 123L));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalLodgingObservation(growth,
				candidate, KingdomGrowthArrivalDisposition.Joined,
				KingdomGrowthArrivalRefusalReason.None, Digest('9'), Digest('a'), true, 124L));

			byte[] historical = KingdomLifecycleWireCodec.GrowthV1PayloadFixture(growth);
			Assert.AreEqual(4327, historical.Length,
				"historical candidate fixture layout is pinned, not merely self-round-tripped");
			Assert.AreEqual("521b8e42ce902e25777ef6198f19159a2cbdf98ca7ca1e6dee66db431b8fb1d8",
				Sha256(historical));
			KingdomGrowthBook loaded = KingdomLifecycleWireCodec.ReadGrowthPayload(historical);
			Assert.IsFalse(loaded.Quarantined);
			Assert.AreEqual(KingdomLifecycleRules.CurrentGrowthFormatVersion,
				loaded.FormatVersion);
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.Observed,
				loaded.ArrivalCandidate.Phase);
			Assert.AreNotEqual(loaded.ArrivalCandidate.LodgingReceiptGraphHash,
				loaded.ArrivalCandidate.LodgingDeclaredGraphHash);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(loaded,
				loaded.SettlementId));
			KingdomGrowthBook current = RoundTripGrowth(loaded);
			Assert.IsFalse(current.Quarantined);
			Assert.AreEqual(loaded.ArrivalCandidate.PlanHash,
				current.ArrivalCandidate.PlanHash);
		}

		[Test]
		public void HistoricalV2ArrivalSemanticPlanUpgradesOnceAcrossEveryPreCreateSaveCut()
		{
			foreach (KingdomGrowthArrivalCandidatePhase phase in new[]
			{
				KingdomGrowthArrivalCandidatePhase.Prepared,
				KingdomGrowthArrivalCandidatePhase.CreateIntent,
				KingdomGrowthArrivalCandidatePhase.Escrowed
			})
			{
				KingdomGrowthBook growth = Migrated("city-v2-semantic-" + phase,
					100L, true, true, 20L);
				KingdomGrowthArrivalCandidate candidate =
					KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth,
						"v2-semantic-marker-" + phase, "Settler",
						"v2-semantic-escrow-" + phase, "zone-a", 120L,
						Digest('1'), Digest('2'), Digest('3'));
				Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(
					growth, candidate));
				if ((byte)phase >= (byte)KingdomGrowthArrivalCandidatePhase.CreateIntent)
					Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateCreate(
						growth, candidate, 121L));
				if (phase == KingdomGrowthArrivalCandidatePhase.Escrowed)
					Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalCandidateCreate(
						growth, candidate, "v2-semantic-object", Digest('4'), Digest('5'),
						Digest('6'), Digest('7'), true, 122L));

				byte[] historical = KingdomLifecycleWireCodec.GrowthV2PayloadFixture(growth);
				KingdomGrowthBook loaded = KingdomLifecycleWireCodec.ReadGrowthPayload(historical);
				candidate = loaded.ArrivalCandidate;
				Assert.IsTrue(candidate.LegacySemanticPlan, phase.ToString());
				string legacyHash = candidate.PlanHash;
				Assert.IsTrue(KingdomLifecycleRules.UpgradeLegacyGrowthArrivalSemanticPlan(
					loaded, candidate, 1, "taf:semantic:growth-arrival:v1", 1U,
					"the salt dunes", "hospitality", "Abar", "1st of Nivvun Ut, 1001 AR",
					4, 5, 130L), phase.ToString());
				Assert.IsFalse(candidate.LegacySemanticPlan);
				Assert.AreEqual("Settler", candidate.Blueprint);
				Assert.AreEqual("Abar", candidate.PlannedName);
				Assert.AreEqual(4, candidate.ArrivalX);
				Assert.AreEqual(5, candidate.ArrivalY);
				Assert.AreNotEqual(legacyHash, candidate.PlanHash);
				Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(loaded,
					loaded.SettlementId));

				loaded = RoundTripGrowth(loaded);
				candidate = loaded.ArrivalCandidate;
				byte[] beforeRetry = KingdomLifecycleWireCodec.GrowthPayloadForWrite(loaded);
				Assert.IsFalse(KingdomLifecycleRules.UpgradeLegacyGrowthArrivalSemanticPlan(
					loaded, candidate, 1, "taf:semantic:growth-arrival:v1", 1U,
					"the salt dunes", "hospitality", "Other", "2nd of Nivvun Ut, 1001 AR",
					6, 7, 131L));
				CollectionAssert.AreEqual(beforeRetry,
					KingdomLifecycleWireCodec.GrowthPayloadForWrite(loaded));
				candidate.PlannedName = "Other";
				Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(loaded,
					loaded.SettlementId), "frozen semantic payload enters candidate authority");
			}
		}

		[Test]
		public void HistoricalV1PreLodgingCandidatesBindOnceToTheirFirstClaimedZone()
		{
			foreach (KingdomGrowthArrivalCandidatePhase phase in new[]
			{
				KingdomGrowthArrivalCandidatePhase.Prepared,
				KingdomGrowthArrivalCandidatePhase.CreateIntent,
				KingdomGrowthArrivalCandidatePhase.Escrowed
			})
			{
				KingdomGrowthBook growth = Migrated("city-v1-unbound-" + phase,
					100L, true, true, 20L);
				KingdomGrowthArrivalCandidate candidate =
					KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth,
						"v1-unbound-marker-" + phase, "Settler",
						"v1-unbound-escrow-" + phase, "zone-original", 120L,
						Digest('1'), Digest('2'), Digest('3'));
				Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(
					growth, candidate));
				if ((byte)phase >= (byte)KingdomGrowthArrivalCandidatePhase.CreateIntent)
					Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateCreate(
						growth, candidate, 121L));
				if (phase == KingdomGrowthArrivalCandidatePhase.Escrowed)
					Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalCandidateCreate(
						growth, candidate, "v1-unbound-object", Digest('4'), Digest('5'),
						Digest('6'), Digest('7'), true, 122L));

				byte[] historical = KingdomLifecycleWireCodec.GrowthV1PayloadFixture(growth);
				int expectedLength = phase == KingdomGrowthArrivalCandidatePhase.Prepared
					? 3171 : phase == KingdomGrowthArrivalCandidatePhase.CreateIntent ? 3415 : 4072;
				string expectedSha = phase == KingdomGrowthArrivalCandidatePhase.Prepared
					? "551099320ca73ef150afc46a64392999a042889a708ede91df91776b79e020a1"
					: phase == KingdomGrowthArrivalCandidatePhase.CreateIntent
						? "49003afaab3fa918a68ba232e3a82b8f4b12a12f3d728f6bd8e81724bc2be361"
						: "c874d96fb64f550f08d75efa8f5790523589f92d2ce80f726879fa7f3d95347d";
				Assert.AreEqual(expectedLength, historical.Length,
					phase + " historical layout changed");
				Assert.AreEqual(expectedSha, Sha256(historical), phase.ToString());
				KingdomGrowthBook loaded = KingdomLifecycleWireCodec.ReadGrowthPayload(historical);
				Assert.IsFalse(loaded.Quarantined, phase.ToString());
				Assert.IsTrue(loaded.ArrivalCandidate.LegacyGrowthV1UnboundZone,
					phase.ToString());
				Assert.IsNull(loaded.ArrivalCandidate.LodgingZoneId, phase.ToString());
				Assert.AreEqual(phase, loaded.ArrivalCandidate.Phase);
				Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(loaded,
					loaded.SettlementId));
				loaded = RoundTripGrowth(loaded);
				string legacyPlan = loaded.ArrivalCandidate.PlanHash;
				Assert.IsTrue(KingdomLifecycleRules.BindLegacyGrowthArrivalCandidateZone(
					loaded, loaded.ArrivalCandidate, "zone-claimed", 130L));
				Assert.IsFalse(loaded.ArrivalCandidate.LegacyGrowthV1UnboundZone);
				Assert.AreEqual("zone-claimed", loaded.ArrivalCandidate.LodgingZoneId);
				Assert.AreNotEqual(legacyPlan, loaded.ArrivalCandidate.PlanHash);
				Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(loaded,
					loaded.SettlementId));
				Assert.IsFalse(KingdomLifecycleRules.BindLegacyGrowthArrivalCandidateZone(
					loaded, loaded.ArrivalCandidate, "zone-other", 131L),
					"zone affinity is a one-way compatibility transition");
			}
		}

		[Test]
		public void HistoricalV1QuarantinedCandidateEvidenceNeverBindsOrRewrites()
		{
			KingdomGrowthBook growth = Migrated("city-v1-quarantined-candidate",
				100L, true, true, 20L);
			KingdomGrowthArrivalCandidate candidate =
				KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth,
					"v1-quarantine-marker", "Settler", "v1-quarantine-escrow",
					"zone-original", 120L, Digest('1'), Digest('2'), Digest('3'));
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(growth,
				candidate));
			Assert.IsTrue(KingdomLifecycleRules.QuarantineGrowthArrivalCandidate(growth,
				candidate, "historical candidate evidence retained"));
			byte[] historical = KingdomLifecycleWireCodec.GrowthV1PayloadFixture(growth);
			KingdomGrowthBook loaded = KingdomLifecycleWireCodec.ReadGrowthPayload(historical);
			Assert.IsFalse(loaded.Quarantined);
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.Quarantined,
				loaded.ArrivalCandidate.Phase);
			Assert.IsTrue(loaded.ArrivalCandidate.LegacyGrowthV1UnboundZone);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(loaded,
				loaded.SettlementId));
			byte[] before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(loaded);
			Assert.IsFalse(KingdomLifecycleRules.BindLegacyGrowthArrivalCandidateZone(
				loaded, loaded.ArrivalCandidate, "zone-claimed", 130L));
			CollectionAssert.AreEqual(before,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(loaded));
		}

		[Test]
		public void ArrivalCandidateIntentPlansBindZoneSnapshotAndExactDispositionEndpoint()
		{
			KingdomGrowthBook growth = Migrated("city-candidate-intents", 100L, true, true, 20L);
			KingdomGrowthArrivalCandidate candidate =
				KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth, "intent-marker",
					"Settler", "intent-escrow", "zone-a", 120L, Digest('1'), Digest('2'),
					Digest('3'));
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(growth, candidate));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateCreate(growth,
				candidate, 121L));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalCandidateCreate(growth,
				candidate, "intent-object", Digest('4'), Digest('5'), Digest('6'), Digest('7'),
				true, 122L));
			string basePlan = candidate.PlanHash;
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalLodgingObservation(growth,
				candidate, "zone-a", 2, 3, Digest('8'), 123L));
			Assert.AreNotEqual(basePlan, candidate.PlanHash,
				"lodging coordinates and snapshot enter authority before observation");
			KingdomGrowthBook lodgingTamper = RoundTripGrowth(growth);
			lodgingTamper.ArrivalCandidate.LodgingX++;
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(lodgingTamper,
				lodgingTamper.SettlementId));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalLodgingObservation(growth,
				candidate, KingdomGrowthArrivalDisposition.Joined,
				KingdomGrowthArrivalRefusalReason.None, Digest('9'), Digest('a'), true, 124L));

			KingdomGrowthOperation arrival = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Arrival, null, 125L);
			arrival.ArrivalDisposition = KingdomGrowthArrivalDisposition.Joined;
			arrival.ArrivalCandidateId = candidate.Id;
			arrival.TargetId = candidate.ObjectId; arrival.TargetMarker = candidate.Marker;
			arrival.Blueprint = candidate.Blueprint; arrival.ZoneId = candidate.LodgingZoneId;
			arrival.TargetTopology = KingdomLifecycleTopology.Cell;
			arrival.TargetLocation = KingdomGrowthLocationKind.Cell;
			arrival.TargetOwnerId = null; arrival.TargetX = 2; arrival.TargetY = 3;
			arrival.PopulationBefore = 1; arrival.PopulationDelta = 1; arrival.PopulationAfter = 2;
			arrival.WaterLegs.Add(Water(growth, arrival, KingdomGrowthWaterMutationKind.Drain,
				"intent-water", 2, 2));
			arrival.DomainSteps.Add(Domain(growth, arrival,
				KingdomGrowthDomainStepKind.Enrollment,
				KingdomGrowthDomainCallbackKind.Enroll, candidate.ObjectId, candidate.ObjectId, 0, 1));
			arrival.DomainSteps.Add(Domain(growth, arrival, KingdomGrowthDomainStepKind.Roster,
				KingdomGrowthDomainCallbackKind.RosterAdd, candidate.ObjectId, candidate.ObjectId, 0, 1));
			arrival.DomainSteps.Add(Domain(growth, arrival, KingdomGrowthDomainStepKind.Creed,
				KingdomGrowthDomainCallbackKind.CreedSet, candidate.ObjectId, candidate.ObjectId, 0, 1));
			arrival.DomainSteps.Add(Domain(growth, arrival, KingdomGrowthDomainStepKind.Population,
				KingdomGrowthDomainCallbackKind.PopulationAdjust, candidate.ObjectId,
				growth.SettlementId, 1, 2));
			KingdomGrowthAccountingSnapshot after = EmptyAccounting();
			after.ArrivalCost = 2; after.Arrivals = 1;
			arrival.DomainSteps.Add(Accounting(growth, arrival, EmptyAccounting(), after));
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, arrival));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateDisposition(growth,
				candidate, arrival.Id, KingdomGrowthObjectMutationKind.CellAdd,
				KingdomGrowthLocationKind.Cell, null, "zone-a", 2, 3, Digest('b'), Digest('c'),
				Digest('d'), Digest('e'), Digest('f'), Digest('0'), 126L));
			KingdomGrowthBook endpointTamper = RoundTripGrowth(growth);
			endpointTamper.ArrivalCandidate.DispositionStep.AfterX++;
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(endpointTamper,
				endpointTamper.SettlementId), "joined callback endpoint is cross-bound to candidate and op");
			KingdomGrowthBook kindTamper = RoundTripGrowth(growth);
			kindTamper.ArrivalCandidate.DispositionStep.Kind =
				KingdomGrowthObjectMutationKind.InventoryAdd;
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(kindTamper,
				kindTamper.SettlementId));
		}

		[Test]
		public void OpenObservedCandidatePreservesClockAcrossOptionAndHealthPauses()
		{
			KingdomGrowthBook growth = Migrated("city-candidate-pause", 100L, true, true, 20L);
			KingdomGrowthArrivalCandidate candidate =
				KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth, "pause-marker", "Settler",
					"pause-escrow", "zone-a", 120L, Digest('1'), Digest('2'), Digest('3'));
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(growth, candidate));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateCreate(growth,
				candidate, 121L));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalCandidateCreate(growth,
				candidate, "pause-object", Digest('4'), Digest('5'), Digest('6'), Digest('7'), true,
				122L));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalLodgingObservation(growth,
				candidate, "zone-a", 2, 3, Digest('8'), 123L));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalLodgingObservation(growth,
				candidate, KingdomGrowthArrivalDisposition.Joined,
				KingdomGrowthArrivalRefusalReason.None, Digest('9'), Digest('a'), true, 124L));
			Assert.AreEqual(120L, growth.NextArrivalTick);

			KingdomGrowthAvailabilityDecision disabled =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, false, true, 130L, 20L);
			Assert.IsTrue(disabled.Valid); Assert.AreEqual(120L, disabled.NextArrivalTick);
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth, disabled));
			Assert.IsTrue(growth.WorkPaused);
			growth = RoundTripGrowth(growth);
			KingdomGrowthAvailabilityDecision enabled =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, true, true, 160L, 20L);
			Assert.IsTrue(enabled.Valid); Assert.AreEqual(120L, enabled.NextArrivalTick);
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth, enabled));
			Assert.NotNull(KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Arrival, null, 160L));

			KingdomGrowthAvailabilityDecision unhealthy =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, true, false, 170L, 20L);
			Assert.IsTrue(unhealthy.Valid); Assert.AreEqual(120L, unhealthy.NextArrivalTick);
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth, unhealthy));
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth, growth.SettlementId));
		}

		[Test]
		public void JoinedArrivalRetiresEveryReceiptExactlyOnceAcrossPhaseCuts()
		{
			KingdomGrowthBook growth = Migrated("city-arrival-complete", 100L, true, true, 20L);
			KingdomGrowthArrivalCandidate candidate =
				KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth, "complete-marker",
					"Settler", "complete-escrow", "zone-a", 120L, Digest('1'), Digest('2'), Digest('3'));
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(growth, candidate));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateCreate(growth,
				candidate, 121L));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalCandidateCreate(growth,
				candidate, "complete-object", Digest('4'), Digest('5'), Digest('6'), Digest('7'),
				true, 122L));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalLodgingObservation(growth,
				candidate, "zone-a", 2, 3, Digest('8'), 123L));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalLodgingObservation(growth,
				candidate, KingdomGrowthArrivalDisposition.Joined,
				KingdomGrowthArrivalRefusalReason.None, Digest('9'), Digest('a'), true, 124L));

			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Arrival, null, 125L);
			op.ArrivalDisposition = KingdomGrowthArrivalDisposition.Joined;
			op.ArrivalCandidateId = candidate.Id;
			op.TargetId = candidate.ObjectId; op.TargetMarker = candidate.Marker;
			op.Blueprint = candidate.Blueprint; op.ZoneId = candidate.LodgingZoneId;
			op.TargetTopology = KingdomLifecycleTopology.Cell;
			op.TargetLocation = KingdomGrowthLocationKind.Cell;
			op.TargetOwnerId = null; op.TargetX = candidate.LodgingX; op.TargetY = candidate.LodgingY;
			op.PopulationBefore = 7; op.PopulationDelta = 1; op.PopulationAfter = 8;
			op.WaterLegs.Add(Water(growth, op, KingdomGrowthWaterMutationKind.Drain,
				"arrival-water-complete", 2, 2));
			op.DomainSteps.Add(Domain(growth, op, KingdomGrowthDomainStepKind.Enrollment,
				KingdomGrowthDomainCallbackKind.Enroll, candidate.ObjectId, candidate.ObjectId, 0, 1));
			op.DomainSteps.Add(Domain(growth, op, KingdomGrowthDomainStepKind.Roster,
				KingdomGrowthDomainCallbackKind.RosterAdd, candidate.ObjectId, candidate.ObjectId, 0, 1));
			op.DomainSteps.Add(Domain(growth, op, KingdomGrowthDomainStepKind.Creed,
				KingdomGrowthDomainCallbackKind.CreedSet, candidate.ObjectId, candidate.ObjectId, 0, 1));
			op.DomainSteps.Add(Domain(growth, op, KingdomGrowthDomainStepKind.Population,
				KingdomGrowthDomainCallbackKind.PopulationAdjust, candidate.ObjectId,
				growth.SettlementId, 7, 8));
			KingdomGrowthAccountingSnapshot accountingAfter = EmptyAccounting();
			accountingAfter.ArrivalCost = 2; accountingAfter.Arrivals = 1;
			op.DomainSteps.Add(Accounting(growth, op, EmptyAccounting(), accountingAfter));
			KingdomGrowthOutboxEvent notice = KingdomLifecycleRules.PrepareDeclaredGrowthOutboxEvent(op,
				0, "joined", "chronicle", "official joined", "outsider joined",
				"ledger", null, null, null,
				2, Digest('b'), 3, Digest('c'), 6, Digest('f'), 7, Digest('0'),
				4, Digest('d'), 5, Digest('e'));
			Assert.NotNull(notice); op.OutboxEvents.Add(notice);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));

			growth = RoundTripGrowth(growth); op = growth.ArrivalOp;
			candidate = growth.ArrivalCandidate;
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.WaterIntent, 126L));
			growth = RoundTripGrowth(growth); op = growth.ArrivalOp;
			candidate = growth.ArrivalCandidate;
			ProveWater(growth, op, 0);
			Assert.AreEqual(1, op.WaterCursor);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.WaterSettled, 127L));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateDisposition(growth,
				candidate, op.Id, KingdomGrowthObjectMutationKind.CellAdd,
				KingdomGrowthLocationKind.Cell, null, "zone-a", 2, 3, Digest('1'), Digest('2'),
				Digest('3'), Digest('4'), Digest('5'), Digest('6'), 128L));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthArrivalCandidateDisposition(growth,
				candidate, Digest('7'), true, 129L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainIntent, 130L));
			for (int i = 0; i < op.DomainSteps.Count; i++) ProveDomain(growth, op, i);
			Assert.AreEqual(5, op.DomainCursor);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainSettled, 131L));
			growth = RoundTripGrowth(growth); op = growth.ArrivalOp;
			candidate = growth.ArrivalCandidate;
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.ClockIntent, 132L));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(growth, op,
				op.ClockLease.Before));
			growth = RoundTripGrowth(growth); op = growth.ArrivalOp;
			candidate = growth.ArrivalCandidate;
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(growth, op,
				op.ClockLease.After));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.Sinks, 133L));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthChronicleOutbox(growth, op, 0,
				2, Digest('b'), 6, Digest('f')));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthChronicleOutbox(growth, op, 0,
				3, Digest('c'), 7, Digest('0')));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthInspectableOutbox(growth, op, 0,
				KingdomGrowthOutboxSinkKind.Ledger, 4, Digest('d')));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthInspectableOutbox(growth, op, 0,
				KingdomGrowthOutboxSinkKind.Ledger, 5, Digest('e')));
			growth = RoundTripGrowth(growth); op = growth.ArrivalOp;
			candidate = growth.ArrivalCandidate;
			Assert.AreEqual(KingdomLifecycleSinkState.Delivered,
				op.OutboxEvents[0].Outbox.ChronicleState);
			Assert.AreEqual(KingdomLifecycleSinkState.Delivered,
				op.OutboxEvents[0].Outbox.LedgerState);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.Terminal, 134L));
			long completedClock = growth.NextArrivalTick;
			KingdomGrowthAvailabilityDecision pause =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, false, true, 135L, 20L);
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth, pause));
			Assert.AreEqual(completedClock, growth.NextArrivalTick);
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowth(growth, op, 136L));
			Assert.IsNull(growth.ArrivalOp);
			Assert.NotNull(growth.ArrivalCandidate);
			Assert.AreEqual(completedClock, growth.NextArrivalTick,
				"linked candidate keeps the proved clock until its barrier retires");
			growth = RoundTripGrowth(growth); candidate = growth.ArrivalCandidate;
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowthArrivalCandidate(growth, candidate));
			Assert.AreEqual(1L, growth.ArrivalRetiredThrough);
			Assert.AreEqual(1L, growth.ArrivalCandidateRetiredThrough);
			Assert.AreEqual(0L, growth.NextArrivalTick);
			Assert.IsNull(growth.ArrivalOp); Assert.IsNull(growth.ArrivalCandidate);
			byte[] retired = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.RetireGrowth(growth, op, 137L));
			Assert.IsFalse(KingdomLifecycleRules.RetireGrowthArrivalCandidate(growth, candidate));
			CollectionAssert.AreEqual(retired,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			KingdomGrowthAvailabilityDecision resume =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, true, true, 160L, 20L);
			Assert.IsTrue(KingdomLifecycleRules.ApplyGrowthAvailability(growth, resume));
			Assert.AreEqual(180L, growth.NextArrivalTick);
		}

		[Test]
		public void CandidateCreateCollisionRollsBackEveryReceiptAndRegistryByte()
		{
			KingdomGrowthBook growth = Migrated("city-candidate-collision", 100L, true, true, 20L);
			KingdomGrowthOperation heartbeat = HeartbeatPlan(growth, 121L);
			SetTarget(heartbeat, "colliding-object", "heartbeat-marker");
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, heartbeat));
			KingdomGrowthArrivalCandidate candidate =
				KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth, "arrival-marker",
					"Settler", "arrival-escrow", "zone-a", 122L, Digest('1'), Digest('2'), Digest('3'));
			Assert.NotNull(candidate);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(growth, candidate));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthArrivalCandidateCreate(growth,
				candidate, 123L));
			byte[] before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.CommitGrowthArrivalCandidateCreate(growth,
				candidate, "colliding-object", Digest('4'), Digest('5'), Digest('6'),
				Digest('7'), true, 124L));
			CollectionAssert.AreEqual(before,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.IsNull(candidate.ObjectId);
			Assert.AreEqual(KingdomGrowthArrivalCandidatePhase.CreateIntent, candidate.Phase);
		}

		[Test]
		public void EverySimpleArrivalDispositionBurnsOneClockAndRetiresExactlyOnce()
		{
			KingdomGrowthArrivalDisposition[] dispositions =
			{
				KingdomGrowthArrivalDisposition.WaterUnavailable,
				KingdomGrowthArrivalDisposition.NoGround,
				KingdomGrowthArrivalDisposition.PopulationCap,
				KingdomGrowthArrivalDisposition.SupportCap
			};
			for (int i = 0; i < dispositions.Length; i++)
			{
				KingdomGrowthBook growth = Migrated("city-simple-arrival-" + i,
					100L, true, true, 20L);
				KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(growth,
					KingdomGrowthAction.Arrival, null, 120L);
				Assert.NotNull(op, dispositions[i].ToString());
				op.ArrivalDisposition = dispositions[i];
				Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
				growth = RoundTripGrowth(growth); op = growth.ArrivalOp;
				Assert.AreEqual(0, op.WaterLegs.Count);
				Assert.AreEqual(0, op.DomainSteps.Count);
				Assert.IsNull(growth.ArrivalCandidate);
				Assert.AreEqual(120L, op.ClockLease.Before);
				Assert.AreEqual(140L, op.ClockLease.After);
				Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
					KingdomGrowthPhase.ClockIntent, 121L));
				Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(growth, op, 120L));
				growth = RoundTripGrowth(growth); op = growth.ArrivalOp;
				Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(growth, op, 140L));
				Assert.AreEqual(140L, growth.NextArrivalTick);
				Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
					KingdomGrowthPhase.Sinks, 122L));
				Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
					KingdomGrowthPhase.Terminal, 123L));
				growth = RoundTripGrowth(growth); op = growth.ArrivalOp;
				Assert.IsTrue(KingdomLifecycleRules.RetireGrowth(growth, op, 124L));
				Assert.IsNull(growth.ArrivalOp);
				Assert.AreEqual(1L, growth.ArrivalRetiredThrough);
				Assert.AreEqual(140L, growth.NextArrivalTick);
				Assert.IsNull(KingdomLifecycleRules.PrepareGrowthOperation(growth,
					KingdomGrowthAction.Arrival, null, 139L), "retry cannot spend the slot twice");
				byte[] retired = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
				Assert.IsFalse(KingdomLifecycleRules.RetireGrowth(growth, op, 125L));
				CollectionAssert.AreEqual(retired,
					KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			}
		}

		[Test]
		public void RuntimeArrivalOwnsEveryIrreversibleMutationWithFrozenReceipts()
		{
			string source = KingdomGrowthLogicalSource.Read();
			string pass = Slice(source, "public static void OnZoneActivated",
				"private static bool ResolveHeartbeat");
			AssertOrdered(pass, "SynchronizeArrivalAuthority(System, Z, survey",
				"ResolveHeartbeat(System, Z, survey", "PublishArrivalHealth(System, Z, timeTicks",
				"ResolveOrStartArrival(System, Z, survey");
			StringAssert.Contains("CurrentSettlementId", source);
			StringAssert.Contains("CanOwnGrowthAuthority(growth, settlementId)", source);
			AssertOrdered(source, "MigrationPending", "ApplyGrowthMigration(parent, input)",
				"TryPublishGrowthMigration(parent, migration)",
				"system.NextArrivalTick = parent.Growth.NextArrivalTick");

			string starter = Slice(source, "private static ArrivalResult ResolveOrStartArrival",
				"private static ArrivalResult StartSimpleArrival");
			AssertOrdered(starter, "PrepareGrowthArrivalCandidate", "TryPublishGrowthArrivalCandidate",
				"ReconcileArrival(system, zone, survey");
			StringAssert.Contains("KingdomGrowthArrivalDisposition.WaterUnavailable", starter);
			StringAssert.Contains("remaining == 0 && operation.WaterLegs.Count > 0", source);

			string reconcile = Slice(source, "private static ArrivalResult ReconcileArrival(",
				"private static bool PrepareCandidateArrivalOperation");
			AssertOrdered(reconcile, "BeginGrowthArrivalCandidateCreate", "GameObject.Create",
				"RootArrivalCandidate", "CommitGrowthArrivalCandidateCreate",
				"ObservePreparedArrival", "BeginGrowthArrivalLodgingObservation");
			int lodgingIntent = reconcile.IndexOf(
				"candidate.Phase == KingdomGrowthArrivalCandidatePhase.LodgingIntent",
				StringComparison.Ordinal);
			int pureObservation = reconcile.IndexOf("ObservePreparedArrival", lodgingIntent,
				StringComparison.Ordinal);
			int lodgingCommit = reconcile.IndexOf("CommitGrowthArrivalLodgingObservation",
				pureObservation, StringComparison.Ordinal);
			Assert.That(lodgingIntent, Is.GreaterThanOrEqualTo(0));
			Assert.That(pureObservation, Is.GreaterThan(lodgingIntent));
			Assert.That(lodgingCommit, Is.GreaterThan(pureObservation));
			StringAssert.DoesNotContain("WouldTakeArrival", reconcile);
			StringAssert.DoesNotContain("PrepareArrivalObservation", reconcile);
			StringAssert.DoesNotContain("Settle(", reconcile);
			StringAssert.Contains("QuarantineArrival(growth", reconcile);
			StringAssert.Contains("!AllowCandidateConsumption || growth.WorkPaused", reconcile);
			AssertOrdered(reconcile,
				"candidate.Phase == KingdomGrowthArrivalCandidatePhase.Quarantined",
				"candidate.LegacyGrowthV1UnboundZone",
				"BindLegacyGrowthArrivalCandidateZone");

			string lodgingSource = KingdomLodgingLogicalSource.Read();
			string observation = Slice(lodgingSource,
				"internal static bool ObservePreparedArrival(KingdomSystem System, Zone Z,",
				"public static string HomeDesignKeyOf");
			StringAssert.Contains("ProjectedOccupancy(Z, benefits)", observation);
			foreach (string mutation in new[] { "SetStringProperty", "SetIntProperty",
				"KingdomChronicle.Record", ".Ledger.Note", "KingdomBrink.", "Settle(" })
				StringAssert.DoesNotContain(mutation, observation);
			string settle = Slice(lodgingSource,
				"private static Dictionary<string, List<GameObject>> Settle(",
				"private static void AssignOne");
			string assign = Slice(lodgingSource, "private static void AssignOne(",
				"private static string ChooseHome(");
			string chooser = Slice(lodgingSource, "private static string ChooseHome(",
				"// --- Addendum 4b");
			string projection = Slice(lodgingSource,
				"private static Dictionary<string, List<GameObject>> ProjectedOccupancy(",
				"private static bool ObserveOccupantConflicts");
			StringAssert.Contains("AssignOne(System, Z, unassigned[i], homes, occupancy",
				settle);
			AssertOrdered(settle,
				"if (!string.IsNullOrEmpty(plotId) && homeByPlot.TryGetValue(plotId,",
				"out GameObject assignedHome) && !IsCondemned(assignedHome))",
				"AddOccupant(occupancy, plotId, resident);", "continue;",
				"unassigned.Add(resident);");
			StringAssert.DoesNotContain("finehouse", settle,
				"soft reservation must never reconsider or evict a standing assignment");
			StringAssert.Contains("ChooseHome(Z, Resident, Homes, Occupancy", assign);
			StringAssert.Contains("ChooseHome(Z, unassigned[i], homes, result", projection);
			StringAssert.Contains("eligibleFineHouses.Add(string.Equals(reading.Designation.BuildingKey, \"finehouse\"",
				chooser);
			StringAssert.Contains("KingdomLodgingRules.ChooseOrdinaryIndex(eligible, eligibleFineHouses)",
				chooser);
			StringAssert.Contains("KingdomGuestbook.LegendaryTraderResidentProperty", chooser);
			StringAssert.Contains("luxuryResident ? KingdomLodgingRules.ChooseIndex(eligible)", chooser);
			foreach (string mutation in new[] { "SetStringProperty", "SetIntProperty",
				"KingdomChronicle.Record", ".Ledger.Note", "KingdomBrink.",
				"ForgetCohabitation" })
			{
				StringAssert.DoesNotContain(mutation, chooser);
				StringAssert.DoesNotContain(mutation, projection);
			}

			string water = Slice(source, "private static bool ReconcileArrivalWater",
				"private static bool ReconcileCandidateDisposition");
			AssertOrdered(water, "BeginGrowthWaterCallback", "KingdomLiquids.Drain",
				"CommitGrowthWaterCallback");
			StringAssert.Contains("removed != leg.Delta", water);
			string disposition = Slice(source, "private static bool ReconcileCandidateDisposition",
				"private static bool ReconcileArrivalDomains");
			AssertOrdered(disposition, "BeginGrowthArrivalCandidateDisposition", "cell.AddObject",
				"CommitGrowthArrivalCandidateDisposition");
			StringAssert.Contains("ExactDispositionEndpoint", disposition);
			StringAssert.Contains("settler.Obliterate()", disposition);

			string domains = Slice(source, "private static bool ReconcileArrivalDomains",
				"private static void ApplyArrivalDomain");
			AssertOrdered(domains, "ArrivalDomainBodyHash", "BeginGrowthDomainCallback",
				"ApplyArrivalDomain",
				"CommitGrowthDomainCallback");
			StringAssert.Contains("operation.LegacyGrowthV1Plan", domains);
			string apply = Slice(source, "private static void ApplyArrivalDomain",
				"private static bool ReconcileArrivalClock");
			StringAssert.Contains("KingdomCitizenshipEnrollmentReason.Arrival", apply);
			StringAssert.Contains("operation.CreatedTick", apply);
			StringAssert.DoesNotContain("addSimpleConversationToObject", apply);
			StringAssert.Contains("system.Population++", apply);
			StringAssert.Contains("system.Ledger.ArrivalCost += KingdomRules.DramsPerArrival", apply);
			string personGraph = Slice(source, "private static string PersonDomainHash",
				"private static string PersonDomainMapHash");
			foreach (string witness in new[] { "Brain?.Allegiance", "allegiance.Previous",
				"allegiance.Reason", "allegiance.Reason.Time", "sourced.Name",
				"allegiance.Flags", "ConversationID", "Quest",
				"PreQuestConversationID", "InQuestConversationID", "PostQuestConversationID",
				"ClearLost", "FilterExtras", "SuppressPowerSwitchTwiddle",
				"blueprint.Attributes", "blueprint.Children", "ArrivalConversationText",
				"ArrivalConversationAnswerPrefix" })
				StringAssert.Contains(witness, personGraph);
			StringAssert.Contains("if (legacyV1)", personGraph);
			StringAssert.Contains("ArrivalAllegianceRepresentable", personGraph);
			StringAssert.Contains("ArrivalAllyReasonRepresentable", personGraph);
			StringAssert.Contains("ArrivalConversationRepresentable", personGraph);
			StringAssert.Contains("ReferenceEquals(seen[i], allegiance)", personGraph);
			StringAssert.DoesNotContain("writer.Write((byte)2)", personGraph);
			StringAssert.DoesNotContain("writer.Write(-2)", personGraph);
			StringAssert.Contains("kind == KingdomGrowthDomainStepKind.Enrollment && !legacyV1",
				source);

			string clock = Slice(source, "private static bool ReconcileArrivalClock",
				"private static bool ReconcileArrivalOutbox");
			AssertOrdered(clock, "BeginGrowthClock", "system.NextArrivalTick = operation.ClockLease.After",
				"CommitGrowthClockWitness");
			string outbox = Slice(source, "private static bool ReconcileInspectableOutbox",
				"private static bool RetireArrivalCandidate");
			AssertOrdered(outbox, "BeginGrowthInspectableOutbox", "append(text)",
				"CommitGrowthInspectableOutbox");
			string chronicle = Slice(source, "private static bool ReconcileChronicleOutbox",
				"private static bool ReconcileInspectableOutbox");
			AssertOrdered(chronicle, "GrowthChronicleOutboxAction",
				"BeginGrowthChronicleOutbox", "KingdomChronicle.RecordDeclaredOnce",
				"CommitGrowthChronicleOutbox");
			StringAssert.Contains("system.OutsiderEntries", chronicle);
			string completion = Slice(source, "private static ArrivalResult CompleteArrivalOperation",
				"private static bool ReconcileArrivalWater");
			AssertOrdered(completion, "ReconcileArrivalWater", "ReconcileCandidateDisposition",
				"ReconcileArrivalDomains", "ReconcileArrivalClock", "ReconcileArrivalOutbox",
				"RetireGrowth(growth, operation", "RetireArrivalCandidate");
			StringAssert.Contains("system.NextArrivalTick = growth.NextArrivalTick", completion);
			string retireCandidate = Slice(source,
				"private static bool RetireArrivalCandidate", "private static bool AppendArrivalOutbox");
			AssertOrdered(retireCandidate, "RetireGrowthArrivalCandidate(growth, candidate)",
				"system.NextArrivalTick = growth.NextArrivalTick");

			string refusal = Slice(source,
				"private static KingdomGrowthArrivalRefusalReason ArrivalRefusalReason",
				"private static ArrivalResult CandidateResult");
			foreach (string reason in new[] { "NoRoofAtAll", "NeedsUnmet", "Full", "Refused",
				"Condemned" })
			{
				StringAssert.Contains("UnhousedReason." + reason, refusal, reason);
				StringAssert.Contains("KingdomGrowthArrivalRefusalReason." + reason, refusal, reason);
			}

			string publicSpawn = Slice(source,
				"public static bool SpawnSettler(KingdomSystem System, Zone Z, KingdomSurvey Survey, out ArrivalRefusal Refusal)",
				"private static bool SynchronizeArrivalAuthority");
			StringAssert.DoesNotContain("GameObject.Create", publicSpawn);
			StringAssert.DoesNotContain("Survey.Consume", publicSpawn);
			StringAssert.DoesNotContain("System.Population++", publicSpawn);
			StringAssert.DoesNotContain("KingdomChronicle.Record", publicSpawn);
			StringAssert.Contains("System.ClaimedZones.Contains(Z.ZoneID)", publicSpawn);
			AssertOrdered(publicSpawn, "reconciledResult != ArrivalResult.Deferred",
				"if (!Enabled)", "ResolveOrStartArrival(System, Z, survey");
			string attended = Slice(source,
				"public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Shared = null)",
				"private static bool ResolveHeartbeat");
			StringAssert.Contains("bool arrivalsEnabled = Enabled", attended);
			StringAssert.Contains("while (arrivalsEnabled && heartbeatHealthy", attended);
			StringAssert.DoesNotContain("if (!Enabled) return;", attended,
				"the arrivals checkbox must not disable unrelated settlement modules");
			string synchronize = Slice(source,
				"private static bool SynchronizeArrivalAuthority", "private static bool TryMigrateArrivalAuthority");
			StringAssert.Contains("lastObservedHealthy", synchronize);
			StringAssert.Contains("out reconciledRefusal, null, false", synchronize);
			StringAssert.Contains("system.ClaimedZones.Contains(zone.ZoneID)", synchronize);
			StringAssert.Contains("QuarantineGrowthArrivalCandidate", source);
			StringAssert.Contains("QuarantineGrowthOperation", source);
		}

		[Test]
		public void TypedFieldAndCropRegistryCasRejectsThirdStateWithoutMutation()
		{
			KingdomGrowthBook growth = PublishedRichSow("city-typed-domain",
				out KingdomGrowthOperation op);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.WaterIntent, 122L)); ProveWater(growth, op, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.WaterSettled, 123L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.SourceIntent, 124L)); ProveObject(growth, op, false, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.SourcesSettled, 125L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.OutputIntent, 126L)); ProveObject(growth, op, true, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.OutputsSettled, 127L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainIntent, 128L));
			KingdomGrowthDomainStep registry = op.DomainSteps[0];
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthDomainCallback(growth, op, 0));
			List<KingdomGrowthCropRow> observed = ObservedCropRows(op, registry);
			List<KingdomGrowthCropRow> foreign = CloneRows(observed);
			foreign[0].ObjectId = "foreign-row";
			byte[] before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.CommitGrowthDomainCallback(growth, op, 0,
				registry.AfterValue, registry.AfterGraphHash, registry.AfterMapHash, null, foreign));
			CollectionAssert.AreEqual(before,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthDomainCallback(growth, op, 0,
				registry.AfterValue, registry.AfterGraphHash, registry.AfterMapHash, null, observed));
			Assert.AreEqual(op.Outputs[0].ObjectId, growth.CropRows[0].ObjectId);
			KingdomGrowthDomainStep field = op.DomainSteps[1];
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthDomainCallback(growth, op, 1));
			KingdomGrowthFieldState wrong = CloneFieldState(field.FieldAfter); wrong.Stage++;
			before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.CommitGrowthDomainCallback(growth, op, 1,
				field.AfterValue, field.AfterGraphHash, field.AfterMapHash, wrong, null));
			CollectionAssert.AreEqual(before,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthDomainCallback(growth, op, 1,
				field.AfterValue, field.AfterGraphHash, field.AfterMapHash,
				CloneFieldState(field.FieldAfter), null));
			Assert.AreEqual(field.FieldAfter.WorkObjectId, growth.FieldOps[0].WorkObjectId);
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(RoundTripGrowth(growth),
				growth.SettlementId));
		}

		[Test]
		public void HarvestOracleIsBoundToExactFieldRowsAndBootstrapIsNotPublic()
		{
			KingdomGrowthBook growth = Migrated("city-harvest-oracle", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			KingdomGrowthFieldState field = FieldState(growth, "field-a");
			field.WorkObjectId = "field-work"; field.WorkPartId = "field-part";
			field.Marker = "field-marker"; field.Blueprint = "FieldWork";
			field.ZoneId = "zone-a"; field.X = 1; field.Y = 1; field.CropBlueprint = "Crop";
			field.Stage = 2; field.NextStageTick = 110L; field.SownTick = 90L;
			field.Cycles = 2; field.DeclaredRows = 1; field.EffectivenessPercent = 100;
			field.MethodPercent = 100; field.SeedBlueprint = "Seed";
			field.PartGraphHash = Digest('a'); field.ObjectGraphHash = Digest('b');
			field.TopologyHash = Digest('c');
			KingdomGrowthCropRow row = Crop("field-a", 0); row.OwnerId = field.WorkObjectId;
			row.Ripe = true; row.RenderTile = "Crop Ripe"; row.RenderColor = "&y";
			row.RenderDetail = "y"; row.TileColor = "&y";
			row.ObjectGraphHash = Digest('3'); row.TopologyHash = Digest('5');
			Assert.IsTrue(KingdomLifecycleRules.InstallGrowthFieldBootstrap(growth, field,
				new List<KingdomGrowthCropRow> { row }), "bootstrap field");
			Assert.IsNull(typeof(KingdomLifecycleRules).GetMethod("InstallGrowthFieldBootstrap",
				BindingFlags.Public | BindingFlags.Static));
			Assert.IsNull(typeof(KingdomLifecycleRules).GetMethod("TryAddGrowthCropRow",
				BindingFlags.Public | BindingFlags.Static));
			KingdomGrowthOperation valid = HarvestPlan(growth, "field-a", 121L);
			Assert.IsTrue(PrivateBool("GrowthHarvestAuthorityShape", growth, valid),
				"harvest authority");
			Assert.IsTrue(PrivateBool("GrowthGroupsMatchAction", valid), "harvest groups");
			Assert.IsTrue(PrivateBool("GrowthOperationScalarsValid", growth, valid,
				KingdomGrowthSlotKind.Field, "field-a"), "harvest scalars");
			Assert.IsTrue(PrivateBool("GrowthTargetShape", valid,
				KingdomGrowthSlotKind.Field), "harvest target");
			Assert.IsTrue(PrivateBool("GrowthPrefixShape", valid, true), "harvest prefix");
			Assert.IsTrue(PrivateBool("GrowthOutboxShape", valid, true), "harvest outbox");
			for (int i = 0; i < valid.Sources.Count; i++)
				Assert.IsTrue(PrivateBool("GrowthObjectShape", valid, valid.Sources[i], i,
					false, true), "harvest source " + i);
			for (int i = 0; i < valid.Outputs.Count; i++)
				Assert.IsTrue(PrivateBool("GrowthObjectShape", valid, valid.Outputs[i], i,
					true, true), "harvest output " + i);
			for (int i = 0; i < valid.DomainSteps.Count; i++)
				Assert.IsTrue(PrivateBool("GrowthDomainShape", valid, valid.DomainSteps[i], i,
					true), "harvest domain " + i);
			Assert.IsTrue(PrivateBool("GrowthOperationShape", growth, valid,
				KingdomGrowthSlotKind.Field, "field-a", true), "harvest operation shape");
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, valid), "publish harvest");
			KingdomGrowthBook hostile = RoundTripGrowth(growth);
			KingdomGrowthOperation open = hostile.FieldOps[0].Operation;
			open.HarvestStandingRows++;
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(hostile,
				hostile.SettlementId));
			KingdomGrowthBook wrongOrdinal = RoundTripGrowth(growth);
			wrongOrdinal.FieldOps[0].Operation.HarvestFirstOrdinal++;
			Assert.IsFalse(KingdomLifecycleRules.CanOwnGrowthAuthority(wrongOrdinal,
				wrongOrdinal.SettlementId));
		}

		[Test]
		public void OperationQuarantineRetainsExactEvidenceAndBlocksEveryExecutionPath()
		{
			KingdomGrowthBook growth = Migrated("city-operation-quarantine", 100L, true, true, 20L);
			KingdomGrowthOperation op = HeartbeatPlan(growth, 121L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			string leaseKey = op.ClockLease.Key;
			Assert.IsTrue(KingdomLifecycleRules.QuarantineGrowthOperation(growth, op,
				"ambiguous heartbeat callback"), "quarantine operation");
			Assert.AreEqual(KingdomGrowthPhase.Quarantined, op.Phase);
			Assert.AreEqual(op.Id, Resource(growth, leaseKey).ActiveOperationId);
			growth = RoundTripGrowth(growth); op = growth.HeartbeatOp;
			Assert.AreEqual(KingdomGrowthPhase.Quarantined, op.Phase);
			byte[] before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainIntent, 122L));
			Assert.IsFalse(KingdomLifecycleRules.RetireGrowth(growth, op, 122L));
			Assert.IsFalse(KingdomLifecycleRules.QuarantineGrowthOperation(growth, op, "again"));
			CollectionAssert.AreEqual(before,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
		}

		[Test]
		public void MultipleInspectableEventsRequireExactCasChain()
		{
			KingdomGrowthBook growth = Migrated("city-outbox-chain", 100L, true, true, 20L);
			KingdomGrowthOperation op = HeartbeatPlan(growth, 121L);
			op.OutboxEvents.Add(KingdomLifecycleRules.PrepareDeclaredGrowthOutboxEvent(op, 0, "first",
				"chronicle one", "official one", "outsider one", "ledger one", null, null, null, 0, Digest('1'), 1,
				Digest('2'), 0, Digest('7'), 1, Digest('8'), 0, Digest('3'), 1,
				Digest('4')));
			op.OutboxEvents.Add(KingdomLifecycleRules.PrepareDeclaredGrowthOutboxEvent(op, 1, "second",
				"chronicle two", "official two", "outsider two", "ledger two", null, null, null, 1, Digest('2'), 2,
				Digest('5'), 1, Digest('8'), 2, Digest('9'), 1, Digest('4'), 2,
				Digest('6')));
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			KingdomGrowthBook hostile = Migrated("city-outbox-chain-bad", 100L, true, true, 20L);
			KingdomGrowthOperation bad = HeartbeatPlan(hostile, 121L);
			bad.OutboxEvents.Add(KingdomLifecycleRules.PrepareDeclaredGrowthOutboxEvent(bad, 0, "first",
				"chronicle one", "official one", "outsider one", "ledger one", null, null, null, 0, Digest('1'), 1,
				Digest('2'), 0, Digest('7'), 1, Digest('8'), 0, Digest('3'), 1,
				Digest('4')));
			bad.OutboxEvents.Add(KingdomLifecycleRules.PrepareDeclaredGrowthOutboxEvent(bad, 1, "second",
				"chronicle two", "official two", "outsider two", "ledger two", null, null, null, 1, Digest('2'), 2,
				Digest('5'), 1, Digest('7'), 2, Digest('9'), 1, Digest('4'), 2,
				Digest('6')));
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(hostile, bad));
			Assert.IsNull(bad.PlanHash); Assert.IsNull(hostile.HeartbeatOp);
		}

		[Test]
		public void PublicGrowthApiCannotMintCallbackOrReceiptProof()
		{
			foreach (MethodInfo method in typeof(KingdomLifecycleRules).GetMethods(
				BindingFlags.Public | BindingFlags.Static))
			{
				if (method.Name.IndexOf("Growth", StringComparison.Ordinal) < 0) continue;
				Assert.IsFalse(method.Name.StartsWith("BeginGrowth", StringComparison.Ordinal)
					|| method.Name.StartsWith("CommitGrowth", StringComparison.Ordinal)
					|| method.Name.StartsWith("RecoverGrowth", StringComparison.Ordinal),
					method.Name + " exposes a receipt transition");
				foreach (ParameterInfo parameter in method.GetParameters())
					Assert.IsFalse(parameter.Name.IndexOf("SameReference", StringComparison.OrdinalIgnoreCase) >= 0
						|| parameter.Name.StartsWith("Observed", StringComparison.OrdinalIgnoreCase)
						|| parameter.Name.StartsWith("Receipt", StringComparison.OrdinalIgnoreCase),
						method.Name + " exposes caller-authored proof field " + parameter.Name);
			}
		}

		private static KingdomGrowthOperation RichSow(KingdomGrowthBook growth,
			string fieldId, long tick, bool completePlan)
		{
			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Sow, fieldId, tick);
			Assert.NotNull(op);
			SetTarget(op, "plot-a", "plot-marker-a");
			if (!completePlan) return op;
			KingdomGrowthWaterLeg water = KingdomLifecycleRules.PrepareGrowthWaterLeg(growth, op,
				KingdomGrowthWaterMutationKind.Drain, "water-a", KingdomLifecycleTopology.Cell,
				null, "Waterskin", "zone-a", 1, 1, 10, 10, 3, "fresh", "fresh",
				Digest('1'), Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6'));
			Assert.NotNull(water); op.WaterLegs.Add(water);
			KingdomGrowthObjectLeg source = KingdomLifecycleRules.PrepareGrowthObjectLeg(growth,
				op, false,
				KingdomGrowthObjectMutationKind.DestroyOne, "seed-a", "seed-marker-a", "Seed",
				KingdomLifecycleTopology.Cell, null, "zone-a", 1, 1, 1, -1, false,
				Digest('1'), Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6'));
			Assert.NotNull(source); op.Sources.Add(source);
			KingdomGrowthObjectLeg output = KingdomLifecycleRules.PrepareGrowthObjectLeg(growth,
				op, true,
				KingdomGrowthObjectMutationKind.Create, null, "crop-marker-a", "Crop",
				KingdomLifecycleTopology.Cell, null, "zone-a", 1, 1, 0, 1, true,
				Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6'), Digest('7'));
			Assert.NotNull(output);
			Assert.IsTrue(KingdomLifecycleRules.TryAppendGrowthObjectPlacement(op, output,
				KingdomGrowthObjectMutationKind.CellAdd, KingdomLifecycleTopology.Cell, null,
				"zone-a", 1, 1, null, Digest('3'), null, Digest('5'), null, Digest('7')));
			op.Outputs.Add(output);
			List<KingdomGrowthCropRow> rowsBefore = CloneRows(growth.CropRows);
			KingdomGrowthCropRow declaredRow = Crop(fieldId, 0);
			declaredRow.ObjectId = null; declaredRow.Marker = output.Marker;
			declaredRow.Blueprint = output.Blueprint; declaredRow.ZoneId = output.ZoneId;
			declaredRow.OwnerId = op.TargetId; declaredRow.X = output.X; declaredRow.Y = output.Y;
			declaredRow.PartGraphHash = null; declaredRow.ObjectGraphHash = null;
			declaredRow.TopologyHash = null; declaredRow.Revision = 1L;
			declaredRow.LastOperationId = op.Id;
			List<KingdomGrowthCropRow> rowsAfter = CloneRows(rowsBefore);
			rowsAfter.Add(declaredRow);
			KingdomGrowthDomainStep registry = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				op, KingdomGrowthDomainStepKind.CropRegistry,
				KingdomGrowthDomainCallbackKind.CropRegistrySet, "plot-a", fieldId,
				0L, 1L, Digest('1'), Digest('2'), Digest('3'), Digest('4'), Digest('5'),
				null, null, null, null, null, null, rowsBefore, rowsAfter);
			Assert.NotNull(registry); op.DomainSteps.Add(registry);
			KingdomGrowthFieldState fieldBefore = FieldState(growth, fieldId);
			KingdomGrowthFieldState fieldAfter = CloneFieldState(fieldBefore);
			fieldAfter.WorkObjectId = op.TargetId; fieldAfter.WorkPartId = "field-part-a";
			fieldAfter.Marker = op.TargetMarker; fieldAfter.Blueprint = op.Blueprint;
			fieldAfter.ZoneId = op.ZoneId; fieldAfter.X = op.TargetX; fieldAfter.Y = op.TargetY;
			fieldAfter.CropBlueprint = output.Blueprint; fieldAfter.Stage = 1;
			fieldAfter.NextStageTick = op.FieldClockAfter + 20L;
			fieldAfter.SownTick = op.CreatedTick; fieldAfter.DeclaredRows = 1;
			fieldAfter.EffectivenessPercent = 100; fieldAfter.MethodPercent = 100;
			fieldAfter.SeedBlueprint = source.Blueprint; fieldAfter.PartGraphHash = Digest('b');
			fieldAfter.ObjectGraphHash = Digest('c'); fieldAfter.TopologyHash = Digest('d');
			KingdomGrowthDomainStep field = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				op, KingdomGrowthDomainStepKind.Field, KingdomGrowthDomainCallbackKind.FieldSet,
				"plot-a", fieldId, 0L, 1L, Digest('6'), Digest('7'), Digest('8'),
				Digest('9'), Digest('a'), null, null, null, null, fieldBefore, fieldAfter);
			Assert.NotNull(field); op.DomainSteps.Add(field);
			return op;
		}

		private static KingdomGrowthOperation HarvestPlan(KingdomGrowthBook growth,
			string fieldId, long tick)
		{
			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Harvest, fieldId, tick);
			Assert.NotNull(op); KingdomGrowthFieldState fieldBefore = FieldState(growth, fieldId);
			op.TargetId = fieldBefore.WorkObjectId; op.TargetMarker = fieldBefore.Marker;
			op.Blueprint = fieldBefore.Blueprint; op.ZoneId = fieldBefore.ZoneId;
			op.TargetTopology = KingdomLifecycleTopology.Cell;
			op.TargetLocation = KingdomGrowthLocationKind.Cell; op.TargetOwnerId = null;
			op.TargetX = fieldBefore.X; op.TargetY = fieldBefore.Y;
			List<KingdomGrowthCropRow> rowsBefore = CloneRows(growth.CropRows);
			List<KingdomGrowthCropRow> rowsAfter = CloneRows(rowsBefore);
			int standing = 0; int ripe = 0;
			for (int i = 0; i < rowsBefore.Count; i++)
			{
				KingdomGrowthCropRow before = rowsBefore[i];
				if (!string.Equals(before.FieldId, fieldId, StringComparison.Ordinal)) continue;
				standing++; if (before.Ripe) ripe++;
				KingdomGrowthCropRow after = rowsAfter[i];
				after.Ripe = false; after.RenderTile = "Crop"; after.RenderColor = "&g";
				after.RenderDetail = "g"; after.TileColor = "&g";
				after.PartGraphHash = Digest('7'); after.ObjectGraphHash = Digest('4');
				after.TopologyHash = Digest('6'); after.Revision++;
				after.LastOperationId = op.Id;
				KingdomGrowthObjectLeg source =
					KingdomLifecycleRules.PrepareGrowthHarvestableMutationLeg(growth, op,
						before.ObjectId, before.Marker, before.Blueprint, before.ZoneId,
						before.X, before.Y, before.Count, before.Ripe, after.Ripe,
						before.RegenTimer, after.RegenTimer, before.RegenTime, after.RegenTime,
						before.TileIndex, after.TileIndex, before.RenderTile, after.RenderTile,
						before.RenderColor, after.RenderColor, before.RenderDetail,
						after.RenderDetail, before.RenderString, after.RenderString,
						before.TileColor, after.TileColor, Digest('2'), Digest('8'),
						before.ObjectGraphHash, after.ObjectGraphHash, before.TopologyHash,
						after.TopologyHash);
				Assert.NotNull(source); op.Sources.Add(source);
			}
			op.HarvestStandingRows = standing; op.HarvestCountsRipeLast = true;
			op.HarvestRipeRows = ripe; op.HarvestCycles = 1;
			op.HarvestEffectivenessPercent = fieldBefore.EffectivenessPercent;
			op.HarvestMethodPercent = fieldBefore.MethodPercent;
			op.HarvestFirstOrdinal = (ulong)(uint)fieldBefore.Cycles;
			op.HarvestCropBlueprint = fieldBefore.CropBlueprint;
			op.HarvestSeedBlueprint = fieldBefore.SeedBlueprint;
			int yield = KingdomCropRules.GatheredYield(standing, ripe, 1, true,
				fieldBefore.EffectivenessPercent, fieldBefore.MethodPercent);
			op.Outputs.Add(Create(growth, op, "harvest-crop", fieldBefore.CropBlueprint, yield));
			int seeds = KingdomCropRules.SeedReturned(op.SettlementId, op.TargetId,
				op.HarvestFirstOrdinal, op.HarvestCycles, yield);
			if (seeds > 0) op.Outputs.Add(Create(growth, op, "harvest-seed",
				fieldBefore.SeedBlueprint, seeds));
			KingdomGrowthDomainStep registry = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				op, KingdomGrowthDomainStepKind.CropRegistry,
				KingdomGrowthDomainCallbackKind.CropRegistrySet, op.TargetId, fieldId,
				0L, 1L, Digest('1'), Digest('2'), Digest('3'), Digest('4'), Digest('5'),
				null, null, null, null, null, null, rowsBefore, rowsAfter);
			Assert.NotNull(registry); op.DomainSteps.Add(registry);
			KingdomGrowthFieldState fieldAfter = CloneFieldState(fieldBefore);
			fieldAfter.Cycles++; fieldAfter.Stage = 1;
			fieldAfter.NextStageTick = KingdomCropRules.RestampedRipeTick(
				fieldBefore.NextStageTick, 1);
			fieldAfter.PartGraphHash = Digest('a'); fieldAfter.ObjectGraphHash = Digest('b');
			fieldAfter.TopologyHash = Digest('c');
			KingdomGrowthDomainStep field = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				op, KingdomGrowthDomainStepKind.Field, KingdomGrowthDomainCallbackKind.FieldSet,
				op.TargetId, fieldId, 0L, 1L, Digest('6'), Digest('7'), Digest('8'),
				Digest('9'), Digest('a'), null, null, null, null, fieldBefore, fieldAfter);
			Assert.NotNull(field); op.DomainSteps.Add(field);
			KingdomGrowthAccountingSnapshot accounting = EmptyAccounting();
			KingdomGrowthAccountingSnapshot accountingAfter = EmptyAccounting();
			accountingAfter.Harvested = yield;
			op.DomainSteps.Add(Accounting(growth, op, accounting, accountingAfter));
			return op;
		}

		private static KingdomGrowthWaterLeg Water(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, KingdomGrowthWaterMutationKind kind,
			string containerId, int before, int delta)
		{
			KingdomGrowthWaterLeg leg = KingdomLifecycleRules.PrepareGrowthWaterLeg(growth,
				operation, kind, containerId, KingdomLifecycleTopology.Cell, null, "Waterskin",
				"zone-a", 1, 1, 10, before, delta, "fresh", "fresh", Digest('1'),
				Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6'));
			Assert.NotNull(leg, containerId); return leg;
		}

		private static KingdomGrowthObjectLeg Destroy(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, string objectId, string marker, string blueprint)
		{
			KingdomGrowthObjectLeg leg = KingdomLifecycleRules.PrepareGrowthObjectLeg(
				growth, operation,
				false, KingdomGrowthObjectMutationKind.DestroyOne, objectId, marker, blueprint,
				KingdomLifecycleTopology.Cell, null, "zone-a", 1, 1, 1, -1, false,
				Digest('1'), Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6'));
			Assert.NotNull(leg, objectId); return leg;
		}

		private static KingdomGrowthObjectLeg Create(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, string marker, string blueprint, int count)
		{
			KingdomGrowthObjectLeg leg = KingdomLifecycleRules.PrepareGrowthObjectLeg(
				growth, operation,
				true, KingdomGrowthObjectMutationKind.Create, null, marker, blueprint,
				KingdomLifecycleTopology.Cell, null, "zone-a", 1, 1, 0, count, true,
				Digest('1'), Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6'));
			Assert.NotNull(leg, marker);
			Assert.IsTrue(KingdomLifecycleRules.TryAppendGrowthObjectPlacement(operation, leg,
				KingdomGrowthObjectMutationKind.CellAdd, KingdomLifecycleTopology.Cell, null,
				"zone-a", 1, 1, null, Digest('2'), null, Digest('4'), null, Digest('6')));
			return leg;
		}

		private static KingdomGrowthDomainStep Accounting(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, KingdomGrowthAccountingSnapshot before,
			KingdomGrowthAccountingSnapshot after)
		{
			KingdomGrowthDomainStep step = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				operation, KingdomGrowthDomainStepKind.Accounting,
				KingdomGrowthDomainCallbackKind.AccountingSet, growth.SettlementId,
				growth.SettlementId, operation.Sequence - 1L, operation.Sequence, Digest('1'),
				Digest('2'), Digest('3'), Digest('4'), Digest('5'), null, null, before, after);
			Assert.NotNull(step, "accounting"); return step;
		}

		private static KingdomGrowthDomainStep Domain(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, KingdomGrowthDomainStepKind kind,
			KingdomGrowthDomainCallbackKind callback, string actor, string subject,
			long before, long after)
		{
			KingdomGrowthDomainStep step = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				operation, kind, callback, actor, subject, before, after, Digest('1'), Digest('2'),
				Digest('3'), Digest('4'), Digest('5'));
			Assert.NotNull(step, kind.ToString()); return step;
		}

		private static KingdomGrowthBook PublishedRichSow(string settlementId,
			out KingdomGrowthOperation operation)
		{
			KingdomGrowthBook growth = Migrated(settlementId, 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			operation = RichSow(growth, "field-a", 121L, true);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, operation), "publish sow");
			return growth;
		}

		private static KingdomGrowthBook RichSowAtClockIntent(string settlementId,
			out KingdomGrowthOperation operation)
		{
			KingdomGrowthBook growth = PublishedRichSow(settlementId, out operation);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.WaterIntent, 122L), "sow water intent");
			ProveWater(growth, operation, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.WaterSettled, 123L), "sow water settled");
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.SourceIntent, 124L), "sow source intent");
			ProveObject(growth, operation, false, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.SourcesSettled, 125L), "sow sources settled");
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.OutputIntent, 126L), "sow output intent");
			ProveObject(growth, operation, true, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.OutputsSettled, 127L), "sow outputs settled");
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.DomainIntent, 128L), "sow domain intent");
			for (int i = 0; i < operation.DomainSteps.Count; i++)
				ProveDomain(growth, operation, i);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.DomainSettled, 129L), "sow domain settled");
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.ClockIntent, 130L), "sow clock intent");
			return growth;
		}

		private static void AssertFieldEvidenceRefusal(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, Func<bool> action)
		{
			KingdomGrowthFieldSlot field = growth.FieldOps[0];
			byte[] before = KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth);
			Assert.IsFalse(action());
			CollectionAssert.AreEqual(before,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			Assert.AreSame(field, growth.FieldOps[0]);
			Assert.AreSame(operation, growth.FieldOps[0].Operation);
		}

		private static KingdomGrowthOperation RichArrival(KingdomGrowthBook growth, long tick)
		{
			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Arrival, null, tick);
			Assert.NotNull(op);
			op.ArrivalDisposition = KingdomGrowthArrivalDisposition.NoGround;
			return op;
		}

		private static void ProveWater(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, int ordinal)
		{
			KingdomGrowthWaterLeg leg = operation.WaterLegs[ordinal];
			if (leg.State == KingdomLifecyclePhysicalState.Prepared)
				Assert.IsTrue(KingdomLifecycleRules.BeginGrowthWaterCallback(growth,
					operation, ordinal));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthWaterCallback(growth, operation,
				ordinal, leg.ContainerId, Digest('a'), true, leg.AfterOwnerGraphHash,
				leg.AfterPartGraphHash, leg.AfterTopologyHash));
		}

		private static void BeginWater(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, int ordinal)
		{
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthWaterCallback(growth,
				operation, ordinal));
		}

		private static void ProveObject(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, bool output, int ordinal)
		{
			KingdomGrowthObjectLeg leg = (output ? operation.Outputs : operation.Sources)[ordinal];
			while (leg.CallbackCursor < leg.Callbacks.Count)
			{
				KingdomGrowthObjectCallbackStep step = leg.Callbacks[leg.CallbackCursor];
				if (step.State == KingdomLifecyclePhysicalState.Prepared)
				{
					bool create = step.Kind == KingdomGrowthObjectMutationKind.Create;
					Assert.IsTrue(KingdomLifecycleRules.BeginGrowthObjectCallback(growth,
						operation, output, ordinal, create ? null : step.BeforeOwnerGraphHash,
						create ? null : step.AfterOwnerGraphHash,
						create ? null : step.BeforeObjectGraphHash,
						create ? null : step.AfterObjectGraphHash,
						create ? null : step.BeforeTopologyHash,
						create ? null : step.AfterTopologyHash));
				}
				string objectId = leg.ObjectId ?? "created-" + leg.Marker;
				string afterOwner = step.AfterOwnerGraphHash ?? Digest('8');
				string afterObject = step.AfterObjectGraphHash ?? Digest('9');
				string afterTopology = step.AfterTopologyHash ?? Digest('a');
				Assert.IsTrue(KingdomLifecycleRules.CommitGrowthObjectCallback(growth,
					operation, output, ordinal, objectId, leg.Marker, Digest('b'), true,
					afterOwner, afterObject, afterTopology));
			}
		}

		private static void BeginObject(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, bool output, int ordinal)
		{
			KingdomGrowthObjectLeg leg = (output ? operation.Outputs : operation.Sources)[ordinal];
			KingdomGrowthObjectCallbackStep step = leg.Callbacks[leg.CallbackCursor];
			bool create = step.Kind == KingdomGrowthObjectMutationKind.Create;
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthObjectCallback(growth,
				operation, output, ordinal, create ? null : step.BeforeOwnerGraphHash,
				create ? null : step.AfterOwnerGraphHash,
				create ? null : step.BeforeObjectGraphHash,
				create ? null : step.AfterObjectGraphHash,
				create ? null : step.BeforeTopologyHash,
				create ? null : step.AfterTopologyHash));
		}

		private static void ProveDomain(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, int ordinal)
		{
			KingdomGrowthDomainStep step = operation.DomainSteps[ordinal];
			if (step.State == KingdomLifecyclePhysicalState.Prepared)
				Assert.IsTrue(KingdomLifecycleRules.BeginGrowthDomainCallback(growth,
					operation, ordinal));
			KingdomGrowthFieldState observedField = null;
			List<KingdomGrowthCropRow> observedRows = null;
			if (step.Kind == KingdomGrowthDomainStepKind.Field)
				observedField = CloneFieldState(step.FieldAfter);
			if (step.Kind == KingdomGrowthDomainStepKind.CropRegistry)
			{
				observedRows = ObservedCropRows(operation, step);
				Assert.IsTrue(PrivateBool("GrowthCropDeclarationMatchesObserved", operation,
					step.CropRowsDeclaredAfter, observedRows), "declared crop after");
			}
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthDomainCallback(growth,
				operation, ordinal, step.AfterValue, step.AfterGraphHash, step.AfterMapHash,
				observedField, observedRows), "commit domain " + step.Kind);
		}

		private static List<KingdomGrowthCropRow> ObservedCropRows(
			KingdomGrowthOperation operation, KingdomGrowthDomainStep step)
		{
			List<KingdomGrowthCropRow> observedRows = CloneRows(step.CropRowsDeclaredAfter);
			for (int i = 0; i < observedRows.Count; i++)
			{
				KingdomGrowthCropRow row = observedRows[i];
				if (row.ObjectId != null) continue;
				KingdomGrowthObjectLeg output = null;
				for (int j = 0; j < operation.Outputs.Count; j++)
					if (string.Equals(operation.Outputs[j].Marker, row.Marker,
						StringComparison.Ordinal)) output = operation.Outputs[j];
				Assert.NotNull(output, row.Marker); row.ObjectId = output.ObjectId;
				row.PartGraphHash = Digest('b');
				row.ObjectGraphHash = output.ReceiptAfterObjectGraphHash;
				row.TopologyHash = output.ReceiptAfterTopologyHash;
			}
			return observedRows;
		}

		private static void BeginDomain(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, int ordinal)
		{
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthDomainCallback(growth,
				operation, ordinal));
		}

		private static void CommitResource(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, KingdomLifecycleResourceLease lease)
		{
			KingdomLifecycleResourceRevision found = null;
			for (int i = 0; i < growth.Resources.Count; i++)
				if (string.Equals(growth.Resources[i].Key, lease.Key, StringComparison.Ordinal))
					found = growth.Resources[i];
			Assert.NotNull(found);
			found.Revision = lease.AfterRevision;
			found.LastOperationId = operation.Id;
		}

		private static string PrivateProof(string method, params object[] args)
		{
			MethodInfo info = typeof(KingdomLifecycleRules).GetMethod(method,
				BindingFlags.NonPublic | BindingFlags.Static);
			Assert.NotNull(info, method);
			return (string)info.Invoke(null, args);
		}

		private static bool PrivateBool(string method, params object[] args)
		{
			MethodInfo info = typeof(KingdomLifecycleRules).GetMethod(method,
				BindingFlags.NonPublic | BindingFlags.Static);
			Assert.NotNull(info, method);
			return (bool)info.Invoke(null, args);
		}

		private static List<KingdomGrowthPhase> GrowthPath(KingdomGrowthAction action)
		{
			List<KingdomGrowthPhase> all = new List<KingdomGrowthPhase>
			{
				KingdomGrowthPhase.Prepared, KingdomGrowthPhase.WaterIntent,
				KingdomGrowthPhase.WaterSettled, KingdomGrowthPhase.SourceIntent,
				KingdomGrowthPhase.SourcesSettled, KingdomGrowthPhase.OutputIntent,
				KingdomGrowthPhase.OutputsSettled, KingdomGrowthPhase.DomainIntent,
				KingdomGrowthPhase.DomainSettled, KingdomGrowthPhase.ClockIntent,
				KingdomGrowthPhase.Sinks, KingdomGrowthPhase.Terminal
			};
			List<KingdomGrowthPhase> result = new List<KingdomGrowthPhase>();
			for (int i = 0; i < all.Count; i++)
				if (KingdomLifecycleRules.GrowthPhaseAllowed(action, all[i])) result.Add(all[i]);
			return result;
		}

		private static KingdomGrowthOperation HeartbeatPlan(KingdomGrowthBook growth, long tick)
		{
			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Heartbeat, null, tick);
			Assert.NotNull(op);
			KingdomGrowthScarcitySnapshot scarcity = HealthyScarcity();
			KingdomGrowthDomainStep scarcityStep = KingdomLifecycleRules.PrepareGrowthDomainStep(
				growth, op, KingdomGrowthDomainStepKind.Scarcity,
				KingdomGrowthDomainCallbackKind.ScarcitySet, growth.SettlementId,
				growth.SettlementId, op.Sequence - 1L, op.Sequence, Digest('1'), Digest('2'),
				Digest('3'), Digest('4'), Digest('5'), scarcity, HealthyScarcity(), null, null);
			Assert.NotNull(scarcityStep); op.DomainSteps.Add(scarcityStep);
			KingdomGrowthAccountingSnapshot accounting = EmptyAccounting();
			KingdomGrowthDomainStep accountingStep = KingdomLifecycleRules.PrepareGrowthDomainStep(
				growth, op, KingdomGrowthDomainStepKind.Accounting,
				KingdomGrowthDomainCallbackKind.AccountingSet, growth.SettlementId,
				growth.SettlementId, op.Sequence - 1L, op.Sequence, Digest('6'), Digest('7'),
				Digest('8'), Digest('9'), Digest('a'), null, null, accounting,
				EmptyAccounting());
			Assert.NotNull(accountingStep); op.DomainSteps.Add(accountingStep);
			return op;
		}

		private static KingdomGrowthScarcitySnapshot HealthyScarcity()
		{
			return new KingdomGrowthScarcitySnapshot
			{
				LastMeal = KingdomRules.MealVerdict.None,
				ThirstOutcome = KingdomGrowthThirstOutcome.Sustained,
				HungerOutcome = KingdomGrowthHungerOutcome.Fed,
				ComposedBite = KingdomGrowthComposedBite.None,
				Healthy = true
			};
		}

		private static KingdomGrowthAccountingSnapshot EmptyAccounting()
		{
			return new KingdomGrowthAccountingSnapshot();
		}

		private static void CompleteHeartbeat(KingdomGrowthBook growth, long tick)
		{
			KingdomGrowthOperation op = HeartbeatPlan(growth, tick);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			AdvanceHeartbeatToTerminal(growth, op, tick);
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowth(growth, op, tick));
		}

		private static void AdvanceHeartbeatToTerminal(KingdomGrowthBook growth,
			KingdomGrowthOperation op, long tick)
		{
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainIntent, tick));
			for (int i = 0; i < op.DomainSteps.Count; i++) ProveDomain(growth, op, i);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainSettled, tick));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.ClockIntent, tick));
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(growth, op, op.ClockLease.Before));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(growth, op,
				op.ClockLease.After));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.Sinks, tick));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.Terminal, tick));
		}

		private static KingdomGrowthBook RoundTripGrowth(KingdomGrowthBook growth)
		{
			return KingdomLifecycleWireCodec.ReadGrowthPayload(
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
		}

		private static KingdomGrowthOperation Ripen(KingdomGrowthBook growth, string fieldId,
			string objectId, string marker, long tick, bool required = true)
		{
			KingdomGrowthOperation operation = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Ripen, fieldId, tick);
			if (operation == null)
			{
				if (required) Assert.Fail("ripen operation");
				return null;
			}
			KingdomGrowthFieldState fieldBefore = FieldState(growth, fieldId);
			operation.TargetId = fieldBefore.WorkObjectId;
			operation.TargetMarker = fieldBefore.Marker;
			operation.Blueprint = fieldBefore.Blueprint;
			operation.ZoneId = fieldBefore.ZoneId;
			operation.TargetTopology = KingdomLifecycleTopology.Cell;
			operation.TargetLocation = KingdomGrowthLocationKind.Cell;
			operation.TargetOwnerId = null;
			operation.TargetX = fieldBefore.X;
			operation.TargetY = fieldBefore.Y;
			KingdomGrowthObjectLeg mutation =
				KingdomLifecycleRules.PrepareGrowthHarvestableMutationLeg(growth, operation,
					objectId, marker, "Crop", "zone-a", 1, 1, 1, false, true,
					int.MaxValue, int.MaxValue, "", "", -1, -1, "Crop", "Crop Ripe",
					"&g", "&y", "g", "y", "\u2663", "\u2663", "&g", "&y",
					Digest('1'), Digest('2'), Digest('2'), Digest('5'), Digest('3'), Digest('6'));
			if (mutation == null)
			{
				if (required) Assert.Fail("harvestable mutation leg");
				return null;
			}
			operation.Sources.Add(mutation);
			List<KingdomGrowthCropRow> rowsBefore = CloneRows(growth.CropRows);
			List<KingdomGrowthCropRow> rowsAfter = CloneRows(rowsBefore);
			KingdomGrowthCropRow changed = null;
			for (int i = 0; i < rowsAfter.Count; i++)
				if (string.Equals(rowsAfter[i].ObjectId, objectId, StringComparison.Ordinal)
					&& string.Equals(rowsAfter[i].Marker, marker, StringComparison.Ordinal))
					changed = rowsAfter[i];
			if (changed == null)
			{
				if (required) Assert.Fail("exact crop row");
				return null;
			}
			changed.Ripe = true; changed.RenderTile = "Crop Ripe";
			changed.RenderColor = "&y"; changed.RenderDetail = "y";
			changed.RenderString = "\u2663"; changed.TileColor = "&y";
			changed.PartGraphHash = Digest('4'); changed.ObjectGraphHash = Digest('5');
			changed.TopologyHash = Digest('6'); changed.Revision++;
			changed.LastOperationId = operation.Id;
			KingdomGrowthDomainStep registry = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				operation, KingdomGrowthDomainStepKind.CropRegistry,
				KingdomGrowthDomainCallbackKind.CropRegistrySet, operation.TargetId, fieldId, 0L, 1L,
				Digest('7'), Digest('8'), Digest('9'), Digest('a'), Digest('b'), null, null,
				null, null, null, null, rowsBefore, rowsAfter);
			Assert.NotNull(registry); operation.DomainSteps.Add(registry);
			KingdomGrowthFieldState fieldAfter = CloneFieldState(fieldBefore);
			KingdomGrowthDomainStep step = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				operation, KingdomGrowthDomainStepKind.Field,
				KingdomGrowthDomainCallbackKind.FieldSet, operation.TargetId, fieldId, 0L, 1L,
				Digest('c'), Digest('d'), Digest('e'), Digest('f'), Digest('0'), null, null,
				null, null, fieldBefore, fieldAfter);
			Assert.NotNull(step);
			operation.DomainSteps.Add(step);
			return operation;
		}

		private static void SetTarget(KingdomGrowthOperation operation, string objectId,
			string marker)
		{
			operation.TargetId = objectId; operation.TargetMarker = marker;
			operation.Blueprint = "Crop"; operation.ZoneId = "zone-a";
			operation.TargetTopology = KingdomLifecycleTopology.Cell;
			operation.TargetLocation = KingdomGrowthLocationKind.Cell;
			operation.TargetOwnerId = null; operation.TargetX = 1; operation.TargetY = 1;
		}

		private static KingdomLifecycleBook Bound(string id)
		{
			KingdomLifecycleBook book = new KingdomLifecycleBook();
			Assert.IsTrue(KingdomLifecycleRules.BindSettlementIdentity(book, id, false, null,
				new List<string>()));
			return book;
		}

		private static KingdomGrowthBook Migrated(string id, long now, bool option,
			bool healthy, long interval)
		{
			return MigratedParent(id, now, option, healthy, interval).Growth;
		}

		private static KingdomGrowthBook Migrated(string id, long now, bool option,
			bool healthy, long interval, int pending)
		{
			KingdomLifecycleBook parent = ReadLifecycle(WriteV5(Bound(id)));
			KingdomGrowthMigrationResult result = KingdomLifecycleRules.ApplyGrowthMigration(parent,
				Migration(now, option, healthy, interval, pending));
			Assert.IsTrue(result.Valid, result.Failure);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthMigration(parent, result));
			return parent.Growth;
		}

		private static KingdomLifecycleBook MigratedParent(string id, long now, bool option,
			bool healthy, long interval)
		{
			KingdomLifecycleBook parent = ReadLifecycle(WriteV5(Bound(id)));
			KingdomGrowthMigrationResult result = KingdomLifecycleRules.ApplyGrowthMigration(parent,
				Migration(now, option, healthy, interval, 0));
			Assert.IsTrue(result.Valid, result.Failure);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowthMigration(parent, result));
			return parent;
		}

		private static KingdomGrowthMigrationInput Migration(long now, bool option,
			bool healthy, long interval, int pending)
		{
			return new KingdomGrowthMigrationInput
			{
				HasNow = true, Now = now, OptionEnabled = option, ScarcityEnabled = false,
				Healthy = healthy,
				ArrivalIntervalTicks = interval, PendingCrop = pending,
				PendingCropBlueprint = pending == 0 ? null : "Crop",
				PendingCropZoneId = pending == 0 ? null : "zone-a"
			};
		}

		private static KingdomGrowthCropRow Crop(string field, int index)
		{
			return new KingdomGrowthCropRow
			{
				FieldId = field, RowId = "row-" + index, ObjectId = "object-" + index,
				Marker = "marker-" + index, Blueprint = "Crop", ZoneId = "zone-a",
				OwnerId = "owner-a", X = 1, Y = 1, Count = 1,
				HasHarvestable = true, Ripe = false, RegenTimer = int.MaxValue,
				RegenTime = "", TileIndex = -1, RenderTile = "Crop",
				RenderColor = "&g", RenderDetail = "g", RenderString = "\u2663",
				TileColor = "&g", PartGraphHash = Digest('1'),
				ObjectGraphHash = Digest('2'), TopologyHash = Digest('3')
			};
		}

		private static KingdomGrowthFieldState FieldState(KingdomGrowthBook growth,
			string fieldId)
		{
			KingdomGrowthFieldSlot field = null;
			for (int i = 0; i < growth.FieldOps.Count; i++)
				if (string.Equals(growth.FieldOps[i].FieldId, fieldId, StringComparison.Ordinal))
					field = growth.FieldOps[i];
			Assert.NotNull(field, fieldId);
			return new KingdomGrowthFieldState
			{
				FieldId = field.FieldId, WorkObjectId = field.WorkObjectId,
				WorkPartId = field.WorkPartId, Marker = field.Marker, Blueprint = field.Blueprint,
				ZoneId = field.ZoneId, X = field.X, Y = field.Y,
				CropBlueprint = field.CropBlueprint, Stage = field.Stage,
				NextStageTick = field.NextStageTick, SownTick = field.SownTick,
				Cycles = field.Cycles, SaidWant = field.SaidWant,
				DeclaredRows = field.DeclaredRows,
				EffectivenessPercent = field.EffectivenessPercent,
				MethodPercent = field.MethodPercent,
				NoLarderAnnounced = field.NoLarderAnnounced,
				SeedBlueprint = field.SeedBlueprint, PartGraphHash = field.PartGraphHash,
				ObjectGraphHash = field.ObjectGraphHash, TopologyHash = field.TopologyHash
			};
		}

		private static KingdomGrowthFieldState ActiveFieldState(KingdomGrowthBook growth,
			string fieldId, int declaredRows)
		{
			KingdomGrowthFieldState state = FieldState(growth, fieldId);
			state.WorkObjectId = "work-" + fieldId;
			state.WorkPartId = "part-" + fieldId;
			state.Marker = "field-marker-" + fieldId;
			state.Blueprint = "FieldWork";
			state.ZoneId = "zone-a"; state.X = 1; state.Y = 1;
			state.CropBlueprint = "Crop"; state.Stage = 1;
			state.NextStageTick = 140L; state.SownTick = 100L;
			state.DeclaredRows = declaredRows; state.EffectivenessPercent = 100;
			state.MethodPercent = 100; state.SeedBlueprint = "Seed";
			state.PartGraphHash = Digest('a'); state.ObjectGraphHash = Digest('b');
			state.TopologyHash = Digest('c');
			return state;
		}

		private static KingdomGrowthFieldState CloneFieldState(KingdomGrowthFieldState x)
		{
			return new KingdomGrowthFieldState
			{
				FieldId = x.FieldId, WorkObjectId = x.WorkObjectId,
				WorkPartId = x.WorkPartId, Marker = x.Marker, Blueprint = x.Blueprint,
				ZoneId = x.ZoneId, X = x.X, Y = x.Y, CropBlueprint = x.CropBlueprint,
				Stage = x.Stage, NextStageTick = x.NextStageTick, SownTick = x.SownTick,
				Cycles = x.Cycles, SaidWant = x.SaidWant, DeclaredRows = x.DeclaredRows,
				EffectivenessPercent = x.EffectivenessPercent,
				MethodPercent = x.MethodPercent,
				NoLarderAnnounced = x.NoLarderAnnounced, SeedBlueprint = x.SeedBlueprint,
				PartGraphHash = x.PartGraphHash, ObjectGraphHash = x.ObjectGraphHash,
				TopologyHash = x.TopologyHash
			};
		}

		private static KingdomGrowthCropRow CloneRow(KingdomGrowthCropRow x)
		{
			return new KingdomGrowthCropRow
			{
				FieldId = x.FieldId, RowId = x.RowId, ObjectId = x.ObjectId,
				Marker = x.Marker, Blueprint = x.Blueprint, ZoneId = x.ZoneId,
				OwnerId = x.OwnerId, X = x.X, Y = x.Y, Count = x.Count,
				HasHarvestable = x.HasHarvestable, Ripe = x.Ripe,
				RegenTimer = x.RegenTimer, RegenTime = x.RegenTime,
				TileIndex = x.TileIndex, RenderTile = x.RenderTile,
				RenderColor = x.RenderColor, RenderDetail = x.RenderDetail,
				RenderString = x.RenderString, TileColor = x.TileColor,
				PartGraphHash = x.PartGraphHash, ObjectGraphHash = x.ObjectGraphHash,
				TopologyHash = x.TopologyHash, Revision = x.Revision,
				LastOperationId = x.LastOperationId
			};
		}

		private static List<KingdomGrowthCropRow> CloneRows(List<KingdomGrowthCropRow> rows)
		{
			List<KingdomGrowthCropRow> clone = new List<KingdomGrowthCropRow>(rows.Count);
			for (int i = 0; i < rows.Count; i++) clone.Add(CloneRow(rows[i]));
			return clone;
		}

		private static void InstallCrop(KingdomGrowthBook growth, KingdomGrowthCropRow row)
		{
			Assert.IsTrue(TryInstallCrop(growth, row), row.RowId);
		}

		private static bool TryInstallCrop(KingdomGrowthBook growth,
			KingdomGrowthCropRow row)
		{
			if (growth == null || row == null) return false;
			KingdomGrowthFieldSlot field = null;
			for (int i = 0; i < growth.FieldOps.Count; i++)
				if (string.Equals(growth.FieldOps[i].FieldId, row.FieldId,
					StringComparison.Ordinal)) field = growth.FieldOps[i];
			if (field == null || field.Quarantined) return false;
			KingdomGrowthCropRow clone = CloneRow(row);
			growth.CropRows.Add(clone);
			if (KingdomLifecycleRules.CanOwnGrowthAuthority(growth, growth.SettlementId))
				return true;
			growth.CropRows.RemoveAt(growth.CropRows.Count - 1);
			return false;
		}

		private static KingdomGrowthCropRow WideCrop(string field, int index)
		{
			return new KingdomGrowthCropRow
			{
				FieldId = field,
				RowId = Wide("row-" + index + "-", KingdomLifecycleRules.MaxIdChars),
				ObjectId = Wide("object-" + index + "-", KingdomLifecycleRules.MaxIdChars),
				Marker = Wide("marker-" + index + "-", KingdomLifecycleRules.MaxIdChars),
				Blueprint = Wide("crop-" + index + "-", KingdomLifecycleRules.MaxNameChars),
				ZoneId = Wide("zone-" + index + "-", KingdomLifecycleRules.MaxNameChars),
				OwnerId = Wide("owner-" + index + "-", KingdomLifecycleRules.MaxIdChars),
				X = 1, Y = 1, Count = 1,
				HasHarvestable = true, RegenTimer = int.MaxValue, RegenTime = "",
				TileIndex = -1,
				RenderTile = Wide("tile-", KingdomLifecycleRules.MaxNameChars),
				RenderColor = Wide("color-", KingdomLifecycleRules.MaxNameChars),
				RenderDetail = Wide("detail-", KingdomLifecycleRules.MaxNameChars),
				RenderString = Wide("render-", KingdomLifecycleRules.MaxNameChars),
				TileColor = Wide("tile-color-", KingdomLifecycleRules.MaxNameChars),
				PartGraphHash = Digest('1'), ObjectGraphHash = Digest('2'),
				TopologyHash = Digest('3')
			};
		}

		private static KingdomGrowthBook NearCapBook(string id)
		{
			KingdomGrowthBook growth = Migrated(id, 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			for (int i = 0; i < KingdomLifecycleRules.MaxGrowthCropRows; i++)
				Assert.IsTrue(TryInstallCrop(growth,
					Crop("field-a", i)));
			PadGrowthNearCap(growth);
			return growth;
		}

		private static void PadGrowthNearCap(KingdomGrowthBook growth)
		{
			string[] names = { "RowId", "ObjectId", "Marker", "Blueprint", "ZoneId", "OwnerId" };
			for (int i = 0; i < growth.CropRows.Count; i++)
			for (int n = 0; n < names.Length; n++)
			{
				FieldInfo field = typeof(KingdomGrowthCropRow).GetField(names[n]);
				Assert.NotNull(field);
				int limit = names[n] == "Blueprint" || names[n] == "ZoneId"
					? KingdomLifecycleRules.MaxNameChars : KingdomLifecycleRules.MaxIdChars;
				string prefix = names[n].Substring(0, 1).ToLowerInvariant() + "-" + i + "-";
				int high = limit - prefix.Length;
				field.SetValue(growth.CropRows[i], Wide(prefix, limit));
				if (KingdomLifecycleWireCodec.GrowthPayloadFitsAggregateCap(growth)) continue;
				int low = 0;
				while (low + 1 < high)
				{
					int mid = low + (high - low) / 2;
					field.SetValue(growth.CropRows[i], prefix + new string('\u202f', mid));
					if (KingdomLifecycleWireCodec.GrowthPayloadFitsAggregateCap(growth)) low = mid;
					else high = mid;
				}
				field.SetValue(growth.CropRows[i], prefix + new string('\u202f', low));
				Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth,
					growth.SettlementId));
				field.SetValue(growth.CropRows[i], prefix + new string('\u202f', low + 1));
				Assert.IsFalse(KingdomLifecycleWireCodec.GrowthPayloadFitsAggregateCap(growth));
				field.SetValue(growth.CropRows[i], prefix + new string('\u202f', low));
				return;
			}
			Assert.Fail("fixture did not reach aggregate cap");
		}

		private static string Wide(string prefix, int length)
		{
			Assert.LessOrEqual(prefix.Length, length);
			return prefix + new string('\u202f', length - prefix.Length);
		}

		private static KingdomLifecycleResourceRevision Resource(KingdomGrowthBook growth,
			string key)
		{
			for (int i = 0; i < growth.Resources.Count; i++)
				if (string.Equals(growth.Resources[i].Key, key, StringComparison.Ordinal))
					return growth.Resources[i];
			return null;
		}

		private static byte[] WriteV5(KingdomLifecycleBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				KingdomLifecycleWireCodec.WriteLifecycleV5Fixture(new BinaryWriter(stream), book);
				return stream.ToArray();
			}
		}

		private static byte[] WriteLifecycle(KingdomLifecycleBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				KingdomLifecycleWireCodec.WriteLifecycle(new BinaryWriter(stream), book);
				return stream.ToArray();
			}
		}

		private static byte[] WriteV6(KingdomLifecycleBook book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				KingdomLifecycleWireCodec.WriteLifecycleV6Fixture(
					new BinaryWriter(stream), book);
				return stream.ToArray();
			}
		}

		private static KingdomLifecycleBook ReadLifecycle(byte[] bytes)
		{
			KingdomLifecycleBook result = new KingdomLifecycleBook();
			ReadLifecycleInto(bytes, result);
			return result;
		}

		private static void ReadLifecycleInto(byte[] bytes, KingdomLifecycleBook target)
		{
			using (MemoryStream stream = new MemoryStream(bytes, false))
				KingdomLifecycleWireCodec.ReadLifecycle(new BinaryReader(stream), target);
		}

		private static byte[] FuturePayload(int version, byte[] tail)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
				{
					writer.Write(KingdomLifecycleWireCodec.GrowthMagic); writer.Write(version);
					writer.Write(tail);
				}
				return stream.ToArray();
			}
		}

		private static string Sha256(byte[] bytes)
		{
			using (SHA256 hash = SHA256.Create())
				return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
		}

		private static string Slice(string source, string first, string after)
		{
			int start = source.IndexOf(first, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, "source start: " + first);
			int end = source.IndexOf(after, start + first.Length, StringComparison.Ordinal);
			Assert.Greater(end, start, "source end: " + after);
			return source.Substring(start, end - start);
		}

		private static void AssertOrdered(string source, params string[] fragments)
		{
			int cursor = -1;
			for (int i = 0; i < fragments.Length; i++)
			{
				int found = source.IndexOf(fragments[i], cursor + 1, StringComparison.Ordinal);
				Assert.Greater(found, cursor, "ordered source fragment: " + fragments[i]);
				cursor = found;
			}
		}

		private static int UniqueLongTriple(byte[] bytes, long a, long b, long c)
		{
			byte[] needle = new byte[24];
			Array.Copy(BitConverter.GetBytes(a), 0, needle, 0, 8);
			Array.Copy(BitConverter.GetBytes(b), 0, needle, 8, 8);
			Array.Copy(BitConverter.GetBytes(c), 0, needle, 16, 8);
			int found = -1;
			for (int i = 0; i + needle.Length <= bytes.Length; i++)
			{
				bool match = true;
				for (int j = 0; j < needle.Length; j++)
					if (bytes[i + j] != needle[j]) { match = false; break; }
				if (!match) continue;
				Assert.AreEqual(-1, found, "fixture tuple must be unique");
				found = i;
			}
			Assert.GreaterOrEqual(found, 0, "fixture tuple absent");
			return found;
		}

		private static string Digest(char value)
		{
			return new string(value, 64);
		}
	}
}
#endif
