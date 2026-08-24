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
		public void SurvivalActionsContinueButProductiveStartersPause()
		{
			foreach (bool option in new[] { false, true })
			foreach (bool healthy in new[] { false, true })
			{
				KingdomGrowthBook growth = Migrated("city-matrix-" + option + "-" + healthy,
					100L, option, healthy, 20L);
				bool productive = option && healthy;
				Assert.AreEqual(productive, KingdomLifecycleRules.PrepareGrowthOperation(growth,
					KingdomGrowthAction.Arrival, null, 120L) != null, "arrival");
				Assert.NotNull(KingdomLifecycleRules.PrepareGrowthOperation(growth,
					KingdomGrowthAction.Heartbeat, null, 121L), "heartbeat recovery/survival");
				Assert.NotNull(KingdomLifecycleRules.PrepareGrowthOperation(growth,
					KingdomGrowthAction.Departure, null, 122L), "health departure");
				Assert.NotNull(KingdomLifecycleRules.PrepareGrowthOperation(growth,
					KingdomGrowthAction.Delivery, null, 123L), "delivery/fetch survival");
				Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
				Assert.AreEqual(productive, KingdomLifecycleRules.PrepareGrowthOperation(growth,
					KingdomGrowthAction.Sow, "field-a", 124L) != null, "field starter");
			}
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
			KingdomGrowthOperation heartbeat = KingdomLifecycleRules.PrepareGrowthOperation(
				hybrid, KingdomGrowthAction.Heartbeat, null, 121L);
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
				Assert.IsTrue(KingdomLifecycleRules.TryAddGrowthCropRow(growth,
					Crop("field-0", i)));
			Assert.IsTrue(KingdomLifecycleRules.GrowthEnvelopeWritable(growth));
			Assert.IsFalse(KingdomLifecycleRules.TryAddGrowthCropRow(growth,
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
				KingdomLifecycleWireCodec.WriteCarry(new BinaryWriter(stream), new KingdomCarryBook());
				bytes = stream.ToArray();
			}
			Assert.AreEqual("c9d79728032c2f2f427241cf4a4097df7c18a6c711ec0c79fbac0b71ee994e52",
				Sha256(bytes));
		}

		[Test]
		public void HeartbeatPublishClockCutSinksTerminalAndRetireAreOrdered()
		{
			KingdomGrowthBook growth = Migrated("city-heartbeat", 100L, true, true, 20L);
			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Heartbeat, null, 121L);
			Assert.NotNull(op);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			Assert.IsFalse(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.Sinks, 122L), "no phase skipping");
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainIntent, 122L));
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
			KingdomGrowthOperation first = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Heartbeat, null, 121L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, first));
			AdvanceHeartbeatToTerminal(growth, first, 121L);
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowth(growth, first, 200L));

			KingdomGrowthOperation second = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Heartbeat, null, 122L);
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
			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Heartbeat, null, 121L);
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
			Assert.IsTrue(KingdomLifecycleRules.TryAddGrowthCropRow(growth, Crop("field-a", 0)));
			Assert.IsTrue(KingdomLifecycleRules.QuarantineGrowthField(growth, "field-a",
				"field topology uncertain"));
			Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth, growth.SettlementId));
			KingdomGrowthBook reloaded = RoundTripGrowth(growth);
			Assert.IsTrue(reloaded.FieldOps[0].Quarantined);
			Assert.AreEqual("row-0", reloaded.CropRows[0].RowId);
			Assert.AreEqual("object-0", reloaded.CropRows[0].ObjectId);

			byte[] beforeFailure = KingdomLifecycleWireCodec.GrowthPayloadForWrite(reloaded);
			Assert.IsFalse(KingdomLifecycleRules.TryAddGrowthCropRow(reloaded,
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
				"prepared field proof uncertain"));
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
				"clock callback not started"));
			Assert.AreEqual(KingdomLifecycleCasAction.Quarantine,
				KingdomLifecycleRules.GrowthClockAction(clockPrepared, clockPreparedOp,
					clockPreparedOp.ClockLease.Before));
			AssertFieldEvidenceRefusal(clockPrepared, clockPreparedOp, () =>
				KingdomLifecycleRules.BeginGrowthClock(clockPrepared, clockPreparedOp,
					clockPreparedOp.ClockLease.Before));

			KingdomGrowthBook clockIntent = RichSowAtClockIntent(
				"city-field-evidence-clock-intent", out KingdomGrowthOperation clockIntentOp);
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(clockIntent, clockIntentOp,
				clockIntentOp.ClockLease.Before));
			Assert.IsTrue(KingdomLifecycleRules.QuarantineGrowthField(clockIntent, "field-a",
				"clock callback result uncertain"));
			AssertFieldEvidenceRefusal(clockIntent, clockIntentOp, () =>
				KingdomLifecycleRules.CommitGrowthClockWitness(clockIntent, clockIntentOp,
					clockIntentOp.ClockLease.After));

			KingdomGrowthBook sinks = RichSowAtClockIntent("city-field-evidence-sinks",
				out KingdomGrowthOperation sinksOp);
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(sinks, sinksOp,
				sinksOp.ClockLease.Before));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(sinks, sinksOp,
				sinksOp.ClockLease.After));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(sinks, sinksOp,
				KingdomGrowthPhase.Sinks, 131L));
			Assert.IsTrue(KingdomLifecycleRules.QuarantineGrowthField(sinks, "field-a",
				"sink delivery uncertain"));
			AssertFieldEvidenceRefusal(sinks, sinksOp, () =>
				KingdomLifecycleRules.RecoverGrowthOutbox(sinks, sinksOp));

			KingdomGrowthBook terminal = RichSowAtClockIntent("city-field-evidence-terminal",
				out KingdomGrowthOperation terminalOp);
			Assert.IsTrue(KingdomLifecycleRules.BeginGrowthClock(terminal, terminalOp,
				terminalOp.ClockLease.Before));
			Assert.IsTrue(KingdomLifecycleRules.CommitGrowthClockWitness(terminal, terminalOp,
				terminalOp.ClockLease.After));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(terminal, terminalOp,
				KingdomGrowthPhase.Sinks, 131L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(terminal, terminalOp,
				KingdomGrowthPhase.Terminal, 132L));
			Assert.IsTrue(KingdomLifecycleRules.QuarantineGrowthField(terminal, "field-a",
				"terminal field proof retained"));
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
			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Heartbeat, null, 121L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainIntent, 122L));
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
			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Heartbeat, null, 121L);
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
			Assert.IsTrue(KingdomLifecycleRules.TryAddGrowthCropRow(baseline, Crop("field-a", 0)));
			KingdomGrowthCropRow duplicateMarker = Crop("field-a", 1);
			duplicateMarker.Marker = baseline.CropRows[0].Marker;
			Assert.IsFalse(KingdomLifecycleRules.TryAddGrowthCropRow(baseline, duplicateMarker));
			Assert.IsFalse(KingdomLifecycleRules.TryAddGrowthCropRow(baseline,
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
		public void ActiveTargetClaimsRejectCrossSlotAndTwoFieldCollisionsAfterReload()
		{
			KingdomGrowthBook crossSlot = Migrated("city-cross-slot", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(crossSlot, "field-a"));
			KingdomGrowthOperation heartbeat = KingdomLifecycleRules.PrepareGrowthOperation(crossSlot,
				KingdomGrowthAction.Heartbeat, null, 121L);
			SetTarget(heartbeat, "shared-object", "shared-marker");
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(crossSlot, heartbeat));
			crossSlot = RoundTripGrowth(crossSlot);
			KingdomGrowthOperation fieldCandidate = Ripen(crossSlot, "field-a",
				"shared-object", "shared-marker", 122L);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(crossSlot, fieldCandidate));
			Assert.IsNull(crossSlot.FieldOps[0].Operation);
			Assert.IsNull(fieldCandidate.PlanHash);

			KingdomGrowthBook twoFields = Migrated("city-two-fields", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(twoFields, "field-a"));
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(twoFields, "field-b"));
			KingdomGrowthOperation first = Ripen(twoFields, "field-a", "crop-object",
				"crop-marker", 121L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(twoFields, first));
			twoFields = RoundTripGrowth(twoFields);
			KingdomGrowthOperation second = Ripen(twoFields, "field-b", "crop-object",
				"crop-marker", 122L);
			Assert.IsFalse(KingdomLifecycleRules.TryPublishGrowth(twoFields, second));
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
			op.Outbox = KingdomLifecycleRules.PrepareGrowthOutbox(op, "", "", "", "", "");
			Assert.IsNull(op.Outbox.Chronicle);
			Assert.IsNull(op.Outbox.Ledger);
			Assert.IsNull(op.Outbox.Message);
			Assert.IsNull(op.Outbox.Deed);
			Assert.IsNull(op.Outbox.GuestbookLine);

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
			KingdomGrowthObjectLeg extra = KingdomLifecycleRules.PrepareGrowthObjectLeg(sourceSource,
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
			ProveObject(op, false, 0);
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
			Assert.IsNull(KingdomLifecycleRules.PrepareGrowthObjectLeg(invalid, true,
				KingdomGrowthObjectMutationKind.InventoryAdd, "object-a", "marker-a", "Crop",
				KingdomLifecycleTopology.Cell, null, "zone-a", 1, 1, 0, 1, true,
				Digest('1'), Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6')),
				"inventory callback cannot claim cell topology");
			Assert.IsNull(KingdomLifecycleRules.PrepareGrowthObjectLeg(invalid, true,
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
				KingdomGrowthPhase.WaterIntent, 131L));
			ProveWater(growth, op, 0);
			growth = RoundTripGrowth(growth); op = growth.ArrivalOp;
			Assert.AreEqual(KingdomLifecyclePhysicalState.Proved, op.WaterLegs[0].State);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.WaterSettled, 132L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.OutputIntent, 133L));
			ProveObject(op, true, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.OutputsSettled, 134L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainIntent, 135L));
			for (int i = 0; i < op.DomainSteps.Count; i++) ProveDomain(growth, op, i);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainSettled, 136L));
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
			ProveObject(op, false, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.SourcesSettled, 125L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.OutputIntent, 126L));
			ProveObject(op, true, 0);
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
			Assert.IsFalse(KingdomLifecycleRules.TryAddGrowthCropRow(growth, badCrop));
			Assert.IsFalse(KingdomLifecycleRules.QuarantineGrowthField(growth, legalField,
				"fault\ud800"));
			CollectionAssert.AreEqual(withField,
				KingdomLifecycleWireCodec.GrowthPayloadForWrite(growth));
			KingdomGrowthCropRow legal = Crop(legalField, 2);
			legal.Blueprint = "Crop\u202fNorth";
			Assert.IsTrue(KingdomLifecycleRules.TryAddGrowthCropRow(growth, legal));

			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Heartbeat, null, 121L);
			op.Outbox = KingdomLifecycleRules.PrepareGrowthOutbox(op, "line\ud800", null,
				null, null, null);
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
				if (KingdomLifecycleRules.TryAddGrowthCropRow(growth, row)) continue;
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
			KingdomGrowthOperation candidate = KingdomLifecycleRules.PrepareGrowthOperation(
				publishBook, KingdomGrowthAction.Heartbeat, null, 121L);
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
				Assert.IsTrue(KingdomLifecycleRules.TryAddGrowthCropRow(commitBook,
					Crop("field-a", i)));
			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(commitBook,
				KingdomGrowthAction.Heartbeat, null, 121L);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(commitBook, op));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(commitBook, op,
				KingdomGrowthPhase.DomainIntent, 122L));
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
			BeginWater(op, 0);
			growth = RoundTripGrowth(growth); op = growth.FieldOps[0].Operation;
			Assert.AreEqual(KingdomLifecyclePhysicalState.Intent, op.WaterLegs[0].State);
			Assert.IsNull(op.WaterLegs[0].ReceiptCallbackContainerId);
			ProveWater(growth, op, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.WaterSettled, 123L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.SourceIntent, 124L));
			BeginObject(op, false, 0);
			growth = RoundTripGrowth(growth); op = growth.FieldOps[0].Operation;
			Assert.AreEqual(KingdomLifecyclePhysicalState.Intent, op.Sources[0].State);
			Assert.IsNull(op.Sources[0].ReceiptCallbackObjectId);
			ProveObject(op, false, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.SourcesSettled, 125L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.OutputIntent, 126L));
			BeginObject(op, true, 0);
			growth = RoundTripGrowth(growth); op = growth.FieldOps[0].Operation;
			ProveObject(op, true, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.OutputsSettled, 127L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainIntent, 128L));
			for (int i = 0; i < op.DomainSteps.Count; i++)
			{
				BeginDomain(op, i);
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
		public void LifecycleV6GrowthV1RichWireGoldenPinsAllPreparedBranches()
		{
			KingdomLifecycleBook parent = MigratedParent("city-rich-golden", 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(parent.Growth, "field-a"));
			KingdomGrowthOperation op = RichSow(parent.Growth, "field-a", 121L, true);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(parent.Growth, op));
			byte[] nested = KingdomLifecycleWireCodec.GrowthPayloadForWrite(parent.Growth);
			byte[] wrapper = WriteLifecycle(parent);
			int lengthOffset = wrapper.Length - nested.Length - 4;
			Assert.AreEqual(KingdomLifecycleWireCodec.LifecycleMagic,
				BitConverter.ToInt32(wrapper, 0));
			Assert.AreEqual(KingdomLifecycleRules.CurrentFormatVersion,
				BitConverter.ToInt32(wrapper, 4));
			Assert.AreEqual(nested.Length, BitConverter.ToInt32(wrapper, lengthOffset));
			Assert.AreEqual(KingdomLifecycleWireCodec.GrowthMagic,
				BitConverter.ToInt32(nested, 0));
			Assert.AreEqual(KingdomLifecycleRules.CurrentGrowthFormatVersion,
				BitConverter.ToInt32(nested, 4));
			Assert.AreEqual(6927, nested.Length);
			Assert.AreEqual("32ed9fa7b7095d979604c2d3b575f5ccad882e12130dfbf809a1fda9d5ca19fb",
				Sha256(nested));
			Assert.AreEqual(7172, wrapper.Length);
			Assert.AreEqual("368a49d5e8f41bddea1a3d04bea35ed8d6be72fd6a8307a0de8f07a3489887b1",
				Sha256(wrapper));

			KingdomLifecycleBook loaded = ReadLifecycle(wrapper);
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
			CollectionAssert.AreEqual(wrapper, WriteLifecycle(loaded));
		}

		private static KingdomGrowthOperation RichSow(KingdomGrowthBook growth,
			string fieldId, long tick, bool completePlan)
		{
			if (growth.PendingCrop == 0)
			{
				growth.PendingCrop = 1;
				growth.PendingCropBlueprint = "Crop";
				growth.PendingCropZoneId = "zone-a";
				Assert.IsTrue(KingdomLifecycleRules.CanOwnGrowthAuthority(growth,
					growth.SettlementId));
			}
			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Sow, fieldId, tick);
			Assert.NotNull(op);
			SetTarget(op, "plot-a", "plot-marker-a");
			if (!completePlan) return op;
			op.PendingCropDelta = -1;
			op.PendingCropAfter = op.PendingCropBefore - 1;
			KingdomGrowthWaterLeg water = KingdomLifecycleRules.PrepareGrowthWaterLeg(growth, op,
				KingdomGrowthWaterMutationKind.Drain, "water-a", KingdomLifecycleTopology.Cell,
				null, "Waterskin", "zone-a", 1, 1, 10, 10, 2, "fresh", "fresh",
				Digest('1'), Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6'));
			Assert.NotNull(water); op.WaterLegs.Add(water);
			KingdomGrowthObjectLeg source = KingdomLifecycleRules.PrepareGrowthObjectLeg(op, false,
				KingdomGrowthObjectMutationKind.DestroyOne, "seed-a", "seed-marker-a", "Seed",
				KingdomLifecycleTopology.Cell, null, "zone-a", 1, 1, 1, -1, false,
				Digest('1'), Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6'));
			Assert.NotNull(source); op.Sources.Add(source);
			KingdomGrowthObjectLeg output = KingdomLifecycleRules.PrepareGrowthObjectLeg(op, true,
				KingdomGrowthObjectMutationKind.Create, "crop-a", "crop-marker-a", "Crop",
				KingdomLifecycleTopology.Cell, null, "zone-a", 1, 1, 0, 1, true,
				Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6'), Digest('7'));
			Assert.NotNull(output); op.Outputs.Add(output);
			KingdomGrowthDomainStep pending = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				op, KingdomGrowthDomainStepKind.PendingCrop,
				KingdomGrowthDomainCallbackKind.PendingCropSet, "plot-a", growth.SettlementId,
				op.PendingCropBefore, op.PendingCropAfter, Digest('1'), Digest('2'), Digest('3'),
				Digest('4'), Digest('5'));
			Assert.NotNull(pending); op.DomainSteps.Add(pending);
			KingdomGrowthDomainStep field = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				op, KingdomGrowthDomainStepKind.Field, KingdomGrowthDomainCallbackKind.FieldSet,
				"plot-a", fieldId, 0L, 1L, Digest('2'), Digest('3'), Digest('4'),
				Digest('5'), Digest('6'));
			Assert.NotNull(field); op.DomainSteps.Add(field);
			return op;
		}

		private static KingdomGrowthBook PublishedRichSow(string settlementId,
			out KingdomGrowthOperation operation)
		{
			KingdomGrowthBook growth = Migrated(settlementId, 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			operation = RichSow(growth, "field-a", 121L, true);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, operation));
			return growth;
		}

		private static KingdomGrowthBook RichSowAtClockIntent(string settlementId,
			out KingdomGrowthOperation operation)
		{
			KingdomGrowthBook growth = PublishedRichSow(settlementId, out operation);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.WaterIntent, 122L));
			ProveWater(growth, operation, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.WaterSettled, 123L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.SourceIntent, 124L));
			ProveObject(operation, false, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.SourcesSettled, 125L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.OutputIntent, 126L));
			ProveObject(operation, true, 0);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.OutputsSettled, 127L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.DomainIntent, 128L));
			for (int i = 0; i < operation.DomainSteps.Count; i++)
				ProveDomain(growth, operation, i);
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.DomainSettled, 129L));
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
				KingdomGrowthPhase.ClockIntent, 130L));
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
			op.PopulationBefore = 0; op.PopulationDelta = 1; op.PopulationAfter = 1;
			KingdomGrowthWaterLeg water = KingdomLifecycleRules.PrepareGrowthWaterLeg(growth, op,
				KingdomGrowthWaterMutationKind.Drain, "arrival-water",
				KingdomLifecycleTopology.Cell, null, "Waterskin", "zone-a", 2, 2, 10, 10, 1,
				"fresh", "fresh", Digest('1'), Digest('2'), Digest('3'), Digest('4'),
				Digest('5'), Digest('6'));
			Assert.NotNull(water); op.WaterLegs.Add(water);
			KingdomGrowthObjectLeg output = KingdomLifecycleRules.PrepareGrowthObjectLeg(op, true,
				KingdomGrowthObjectMutationKind.Create, "settler-a", "settler-marker-a", "Settler",
				KingdomLifecycleTopology.Cell, null, "zone-a", 2, 2, 0, 1, true,
				Digest('2'), Digest('3'), Digest('4'), Digest('5'), Digest('6'), Digest('7'));
			Assert.NotNull(output); op.Outputs.Add(output);
			KingdomGrowthDomainStep enrollment = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				op, KingdomGrowthDomainStepKind.Enrollment, KingdomGrowthDomainCallbackKind.Enroll,
				"settler-a", "settler-a", 0L, 1L, Digest('1'), Digest('2'), Digest('3'),
				Digest('4'), Digest('5'));
			Assert.NotNull(enrollment); op.DomainSteps.Add(enrollment);
			KingdomGrowthDomainStep roster = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				op, KingdomGrowthDomainStepKind.Roster, KingdomGrowthDomainCallbackKind.RosterAdd,
				"settler-a", "settler-a", 0L, 1L, Digest('2'), Digest('3'), Digest('4'),
				Digest('5'), Digest('6'));
			Assert.NotNull(roster); op.DomainSteps.Add(roster);
			KingdomGrowthDomainStep creed = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				op, KingdomGrowthDomainStepKind.Creed, KingdomGrowthDomainCallbackKind.CreedSet,
				"settler-a", "settler-a", 0L, 1L, Digest('3'), Digest('4'), Digest('5'),
				Digest('6'), Digest('7'));
			Assert.NotNull(creed); op.DomainSteps.Add(creed);
			KingdomGrowthDomainStep population = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				op, KingdomGrowthDomainStepKind.Population,
				KingdomGrowthDomainCallbackKind.PopulationAdjust, "settler-a", growth.SettlementId,
				0L, 1L, Digest('4'), Digest('5'), Digest('6'), Digest('7'), Digest('8'));
			Assert.NotNull(population); op.DomainSteps.Add(population);
			return op;
		}

		private static void ProveWater(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, int ordinal)
		{
			KingdomGrowthWaterLeg leg = operation.WaterLegs[ordinal];
			leg.State = KingdomLifecyclePhysicalState.Proved;
			leg.ReceiptState = KingdomLifecyclePhysicalState.Proved;
			leg.Lease.State = KingdomLifecycleLeaseState.Proved;
			leg.ReceiptBeforeMatches = 1; leg.ReceiptAfterMatches = 1;
			leg.ReceiptBeforeOwnerGraphHash = leg.BeforeOwnerGraphHash;
			leg.ReceiptAfterOwnerGraphHash = leg.AfterOwnerGraphHash;
			leg.ReceiptBeforePartGraphHash = leg.BeforePartGraphHash;
			leg.ReceiptAfterPartGraphHash = leg.AfterPartGraphHash;
			leg.ReceiptBeforeTopologyHash = leg.BeforeTopologyHash;
			leg.ReceiptAfterTopologyHash = leg.AfterTopologyHash;
			leg.ReceiptCallbackContainerId = leg.ContainerId;
			leg.ReceiptCallbackReferenceHash = Digest('a');
			leg.ReceiptSameReference = true;
			leg.ReceiptProofId = PrivateProof("GrowthWaterReceiptProof", operation, leg, ordinal);
			CommitResource(growth, operation, leg.Lease);
			operation.WaterCursor = ordinal + 1;
		}

		private static void BeginWater(KingdomGrowthOperation operation, int ordinal)
		{
			KingdomGrowthWaterLeg leg = operation.WaterLegs[ordinal];
			leg.State = KingdomLifecyclePhysicalState.Intent;
			leg.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			leg.Lease.State = KingdomLifecycleLeaseState.Intent;
			leg.ReceiptBeforeMatches = 1;
			leg.ReceiptBeforeOwnerGraphHash = leg.BeforeOwnerGraphHash;
			leg.ReceiptBeforePartGraphHash = leg.BeforePartGraphHash;
			leg.ReceiptBeforeTopologyHash = leg.BeforeTopologyHash;
		}

		private static void ProveObject(KingdomGrowthOperation operation, bool output, int ordinal)
		{
			KingdomGrowthObjectLeg leg = (output ? operation.Outputs : operation.Sources)[ordinal];
			leg.State = KingdomLifecyclePhysicalState.Proved;
			leg.ReceiptState = KingdomLifecyclePhysicalState.Proved;
			int beforeMatches = output && leg.MutationKind == KingdomGrowthObjectMutationKind.Create
				? 0 : 1;
			int afterMatches = leg.AfterCount == 0 ? 0 : 1;
			leg.ReceiptBeforeIdMatches = beforeMatches;
			leg.ReceiptBeforeMarkerMatches = beforeMatches;
			leg.ReceiptBeforeCount = leg.BeforeCount;
			leg.ReceiptAfterIdMatches = afterMatches;
			leg.ReceiptAfterMarkerMatches = afterMatches;
			leg.ReceiptAfterCount = leg.AfterCount;
			leg.ReceiptBeforeOwnerGraphHash = leg.BeforeOwnerGraphHash;
			leg.ReceiptAfterOwnerGraphHash = leg.AfterOwnerGraphHash;
			leg.ReceiptBeforeObjectGraphHash = leg.BeforeObjectGraphHash;
			leg.ReceiptAfterObjectGraphHash = leg.AfterObjectGraphHash;
			leg.ReceiptBeforeTopologyHash = leg.BeforeTopologyHash;
			leg.ReceiptAfterTopologyHash = leg.AfterTopologyHash;
			leg.ReceiptCallbackObjectId = leg.ObjectId;
			leg.ReceiptCallbackMarker = leg.Marker;
			leg.ReceiptCallbackReferenceHash = Digest('b');
			leg.ReceiptSameReference = true;
			leg.ReceiptProofId = PrivateProof("GrowthObjectReceiptProof", operation, leg,
				ordinal, output);
			if (output) operation.OutputCursor = ordinal + 1;
			else operation.SourceCursor = ordinal + 1;
		}

		private static void BeginObject(KingdomGrowthOperation operation, bool output, int ordinal)
		{
			KingdomGrowthObjectLeg leg = (output ? operation.Outputs : operation.Sources)[ordinal];
			int beforeMatches = output && leg.MutationKind == KingdomGrowthObjectMutationKind.Create
				? 0 : 1;
			leg.State = KingdomLifecyclePhysicalState.Intent;
			leg.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			leg.ReceiptBeforeIdMatches = beforeMatches;
			leg.ReceiptBeforeMarkerMatches = beforeMatches;
			leg.ReceiptBeforeCount = leg.BeforeCount;
			leg.ReceiptBeforeOwnerGraphHash = leg.BeforeOwnerGraphHash;
			leg.ReceiptBeforeObjectGraphHash = leg.BeforeObjectGraphHash;
			leg.ReceiptBeforeTopologyHash = leg.BeforeTopologyHash;
		}

		private static void ProveDomain(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, int ordinal)
		{
			KingdomGrowthDomainStep step = operation.DomainSteps[ordinal];
			step.State = KingdomLifecyclePhysicalState.Proved;
			step.ReceiptState = KingdomLifecyclePhysicalState.Proved;
			step.Lease.State = KingdomLifecycleLeaseState.Proved;
			step.ReceiptBeforeValue = step.BeforeValue;
			step.ReceiptAfterValue = step.AfterValue;
			step.ReceiptBeforeGraphHash = step.BeforeGraphHash;
			step.ReceiptAfterGraphHash = step.AfterGraphHash;
			step.ReceiptBeforeMapHash = step.BeforeMapHash;
			step.ReceiptAfterMapHash = step.AfterMapHash;
			step.ReceiptProofId = PrivateProof("GrowthDomainReceiptProof", operation, step, ordinal);
			CommitResource(growth, operation, step.Lease);
			operation.DomainCursor = ordinal + 1;
		}

		private static void BeginDomain(KingdomGrowthOperation operation, int ordinal)
		{
			KingdomGrowthDomainStep step = operation.DomainSteps[ordinal];
			step.State = KingdomLifecyclePhysicalState.Intent;
			step.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			step.Lease.State = KingdomLifecycleLeaseState.Intent;
			step.ReceiptBeforeValue = step.BeforeValue;
			step.ReceiptBeforeGraphHash = step.BeforeGraphHash;
			step.ReceiptBeforeMapHash = step.BeforeMapHash;
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

		private static void CompleteHeartbeat(KingdomGrowthBook growth, long tick)
		{
			KingdomGrowthOperation op = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Heartbeat, null, tick);
			Assert.NotNull(op);
			Assert.IsTrue(KingdomLifecycleRules.TryPublishGrowth(growth, op));
			AdvanceHeartbeatToTerminal(growth, op, tick);
			Assert.IsTrue(KingdomLifecycleRules.RetireGrowth(growth, op, tick));
		}

		private static void AdvanceHeartbeatToTerminal(KingdomGrowthBook growth,
			KingdomGrowthOperation op, long tick)
		{
			Assert.IsTrue(KingdomLifecycleRules.AdvanceGrowthPhase(growth, op,
				KingdomGrowthPhase.DomainIntent, tick));
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
			string objectId, string marker, long tick)
		{
			KingdomGrowthOperation operation = KingdomLifecycleRules.PrepareGrowthOperation(growth,
				KingdomGrowthAction.Ripen, fieldId, tick);
			Assert.NotNull(operation);
			SetTarget(operation, objectId, marker);
			KingdomGrowthDomainStep step = KingdomLifecycleRules.PrepareGrowthDomainStep(growth,
				operation, KingdomGrowthDomainStepKind.Field,
				KingdomGrowthDomainCallbackKind.FieldSet, objectId, fieldId, 0L, 1L,
				Digest('a'), Digest('b'), Digest('c'), Digest('d'), Digest('e'));
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
				HasNow = true, Now = now, OptionEnabled = option, Healthy = healthy,
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
				OwnerId = "owner-a", X = 1, Y = 1, Count = 1
			};
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
				X = 1, Y = 1, Count = 1
			};
		}

		private static KingdomGrowthBook NearCapBook(string id)
		{
			KingdomGrowthBook growth = Migrated(id, 100L, true, true, 20L);
			Assert.IsTrue(KingdomLifecycleRules.TryRegisterGrowthField(growth, "field-a"));
			for (int i = 0; i < KingdomLifecycleRules.MaxGrowthCropRows; i++)
				Assert.IsTrue(KingdomLifecycleRules.TryAddGrowthCropRow(growth,
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
