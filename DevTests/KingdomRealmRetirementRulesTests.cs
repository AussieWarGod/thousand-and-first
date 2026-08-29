using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomRealmRetirementRulesTests
	{
		private const string Game = "game-c2";
		private const string Realm = "taf:realm:c2";
		private const string Faction = "TAF C2";
		private static readonly string Authority = Digest('a');
		private static readonly string Ground = Digest('b');

		[Test]
		public void PlanCodecRoundTripIsCanonicalAndDetached()
		{
			KingdomRealmRetirementState state = Planned();
			string wire = KingdomRealmRetirementCodec.Encode(state);
			Assert.That(KingdomRealmRetirementCodec.TryDecode(wire,
				out KingdomRealmRetirementState decoded, out string failure), Is.True, failure);
			Assert.That(KingdomRealmRetirementCodec.Encode(decoded), Is.EqualTo(wire));
			decoded.Locators[0].ZoneId = "changed";
			Assert.That(state.Locators[0].ZoneId, Is.EqualTo("JoppaWorld.11.22.1.1.10"));
		}

		[Test]
		public void ReceiptRejectsTrailingFutureBytesAndBadBounds()
		{
			string wire = KingdomRealmRetirementCodec.Encode(Planned());
			Assert.That(KingdomRealmRetirementCodec.TryDecode(wire + "AA",
				out KingdomRealmRetirementState _, out string _), Is.False);
			KingdomRealmRetirementState state = Planned();
			state.ReceiptId = new string('a', 31);
			Assert.That(KingdomRealmRetirementRules.Valid(state, out string _), Is.False);
			state = Planned();
			state.Records = null;
			Assert.That(KingdomRealmRetirementRules.Valid(state, out string _), Is.False);
		}

		[Test]
		public void ValidReceiptAlwaysFitsTheExactPayloadByteCap()
		{
			KingdomRealmRetirementState valid = Cleaning();
			for (int i = 0; i < 200; i++)
			{
				KingdomRemovalRecord row = Record(KingdomRemovalProjectionKind.Object,
					"bounded:" + i.ToString("D4"), KingdomRemovalDisposition.Preserved,
					null, null, i, new string('\u0800', 1024));
				if (!KingdomRealmRetirementRules.TryRecord(valid, valid.Revision, row, 10L,
					out KingdomRealmRetirementState next, out string _)) break;
				valid = next;
			}
			Assert.That(KingdomRealmRetirementRules.Valid(valid, out string failure), Is.True,
				failure);
			Assert.DoesNotThrow(() => KingdomRealmRetirementCodec.Encode(valid));

			KingdomRealmRetirementState oversized = Cleaning();
			for (int i = 0; i < KingdomRealmRetirementState.MaxRecords; i++)
				oversized.Records.Add(Record(KingdomRemovalProjectionKind.Object,
					"oversized:" + i.ToString("D4"), KingdomRemovalDisposition.Preserved,
					null, null, i, new string('\u0800', 1024)));
			Assert.That(KingdomRealmRetirementRules.Valid(oversized, out failure), Is.False);
			StringAssert.Contains("wire bounds", failure);
		}

		[Test]
		public void PhaseLawRequiresExactGroundAndAuthorityClosure()
		{
			KingdomRealmRetirementState cleaning = Cleaning();
			Assert.That(KingdomRealmRetirementRules.TrySetPhase(cleaning, cleaning.Revision,
				KingdomRealmRetirementPhase.CleaningGround,
				KingdomRealmRetirementPhase.ReadyForFence, 10L,
				out KingdomRealmRetirementState _, out string failure), Is.False, failure);
			KingdomRealmRetirementState closed = ClosedCleaning();
			Assert.That(KingdomRealmRetirementRules.TrySetPhase(closed, closed.Revision,
				KingdomRealmRetirementPhase.CleaningGround,
				KingdomRealmRetirementPhase.ReadyForFence, 10L,
				out KingdomRealmRetirementState ready, out failure), Is.True, failure);
			Assert.That(ready.Phase, Is.EqualTo(KingdomRealmRetirementPhase.ReadyForFence));
			Assert.That(KingdomRealmRetirementRules.TrySetPhase(ready, ready.Revision,
				KingdomRealmRetirementPhase.ReadyForFence,
				KingdomRealmRetirementPhase.FenceCommitted, 10L,
				out KingdomRealmRetirementState _, out failure), Is.False, failure);
		}

		[Test]
		public void RecordIdempotenceRequiresEveryByteToMatch()
		{
			KingdomRealmRetirementState current = Cleaning();
			KingdomRemovalRecord record = Record(KingdomRemovalProjectionKind.Object,
				"ground:one", KingdomRemovalDisposition.Converted, Ground, Authority, 7L, "exact");
			Assert.That(KingdomRealmRetirementRules.TryRecord(current, current.Revision,
				record, 10L, out KingdomRealmRetirementState once, out string failure), Is.True,
				failure);
			Assert.That(KingdomRealmRetirementRules.TryRecord(once, once.Revision,
				record, 10L, out KingdomRealmRetirementState retry, out failure), Is.True, failure);
			Assert.That(retry.Revision, Is.EqualTo(once.Revision));
			KingdomRemovalRecord mismatch = record.Clone();
			mismatch.Detail = "different";
			Assert.That(KingdomRealmRetirementRules.TryRecord(once, once.Revision,
				mismatch, 10L, out KingdomRealmRetirementState _, out failure), Is.False, failure);
		}

		[Test]
		public void RecordCapacityIsProvedBeforeMutation()
		{
			KingdomRealmRetirementState state = Cleaning();
			for (int i = 0; i < KingdomRealmRetirementState.MaxRecords; i++)
			{
				KingdomRemovalRecord row = Record(KingdomRemovalProjectionKind.Object,
					"row:" + i.ToString("D4"), KingdomRemovalDisposition.Preserved,
					null, null, i, "capacity");
				Assert.That(KingdomRealmRetirementRules.TryRecord(state, state.Revision,
					row, 10L, out KingdomRealmRetirementState next, out string failure),
					Is.True, failure);
				state = next;
			}
			Assert.That(state.Records.Count, Is.EqualTo(KingdomRealmRetirementState.MaxRecords));
			Assert.That(KingdomRealmRetirementRules.TryRecord(state, state.Revision,
				Record(KingdomRemovalProjectionKind.Object, "overflow",
					KingdomRemovalDisposition.Preserved, null, null, 0L, "capacity"),
				10L, out KingdomRealmRetirementState _, out string _), Is.False);
		}

		[Test]
		public void GroundTransitionsAreMonotoneWithExplicitRecoveryEdges()
		{
			KingdomRealmRetirementState state = Cleaning();
			Assert.That(Mark(state, KingdomRemovalLocatorState.Cleaning, null,
				out state), Is.True);
			Assert.That(Mark(state, KingdomRemovalLocatorState.OutstandingVisit, null,
				out KingdomRealmRetirementState _), Is.False);
			Assert.That(Mark(state, KingdomRemovalLocatorState.Contested, null,
				out state), Is.True);
			Assert.That(Mark(state, KingdomRemovalLocatorState.Cleaned, Ground,
				out KingdomRealmRetirementState _), Is.False);
			Assert.That(Mark(state, KingdomRemovalLocatorState.Cleaning, null,
				out state), Is.True);
			Assert.That(Mark(state, KingdomRemovalLocatorState.Cleaned, Ground,
				out state), Is.True);
			Assert.That(Mark(state, KingdomRemovalLocatorState.Diverged, null,
				out KingdomRealmRetirementState _), Is.False);
			Assert.That(Mark(state, KingdomRemovalLocatorState.Cleaned, Ground,
				out KingdomRealmRetirementState idempotent), Is.True);
			Assert.That(idempotent.Revision, Is.EqualTo(state.Revision));
		}

		[Test]
		public void PreparedReceiptIsTerminalAndRequiresFenceEvidence()
		{
			KingdomRealmRetirementState ready = Ready();
			KingdomRemovalRecord fence = Record(KingdomRemovalProjectionKind.GlobalState,
				KingdomRealmRetirementRules.FenceRecordId,
				KingdomRemovalDisposition.Preserved, Digest('c'), Digest('d'), 0L, "CAS");
			Assert.That(KingdomRealmRetirementRules.TryRecord(ready, ready.Revision, fence,
				10L, out KingdomRealmRetirementState recorded, out string failure), Is.True, failure);
			Assert.That(KingdomRealmRetirementRules.TrySetPhase(recorded, recorded.Revision,
				KingdomRealmRetirementPhase.ReadyForFence,
				KingdomRealmRetirementPhase.FenceCommitted, 10L,
				out KingdomRealmRetirementState committed, out failure), Is.True, failure);
			Assert.That(KingdomRealmRetirementRules.TrySetPhase(committed, committed.Revision,
				KingdomRealmRetirementPhase.FenceCommitted,
				KingdomRealmRetirementPhase.PreparedForRemoval, 10L,
				out KingdomRealmRetirementState prepared, out failure), Is.True, failure);
			Assert.That(KingdomRealmRetirementRules.TryRecord(prepared, prepared.Revision,
				Record(KingdomRemovalProjectionKind.Object, "late",
					KingdomRemovalDisposition.Stripped, null, null, 0L, "late"),
				10L, out KingdomRealmRetirementState _, out failure), Is.False, failure);
			Assert.That(KingdomRealmRetirementRules.TryMarkGround(prepared, prepared.Revision,
				prepared.Locators[0].ZoneId, KingdomRemovalLocatorState.Cleaned, 10L, 1,
				Ground, out KingdomRealmRetirementState idempotent, out failure), Is.True, failure);
			Assert.That(idempotent.Revision, Is.EqualTo(prepared.Revision));
		}

		[Test]
		public void LegacyDisclosurePreventsCleanUninstallClaimButAllowsPreparation()
		{
			KingdomRealmRetirementState state = ClosedCleaning();
			KingdomRemovalRecord legacy = Record(KingdomRemovalProjectionKind.LegacyArtifact,
				"legacy", KingdomRemovalDisposition.Untracked, null, null, 1L, "unknown");
			Assert.That(KingdomRealmRetirementRules.TryRecord(state, state.Revision, legacy,
				10L, out state, out string failure), Is.True, failure);
			Assert.That(KingdomRealmRetirementRules.KnownProjectionClosurePermitsPreparation(
				state, out failure), Is.True, failure);
			Assert.That(KingdomRealmRetirementRules.CleanRemovalProvable(state), Is.False);
		}

		[Test]
		public void FenceInitializationCannotForgePreparedOrPartialPending()
		{
			Assert.That(KingdomIdentityFenceRules.TryInitialize(Game,
				KingdomIdentityFenceDisposition.PreparedForRemoval, 1L, Realm, Digest('e'),
				out KingdomIdentityFence _, out string _), Is.False);
			Assert.That(KingdomIdentityFenceRules.TryInitialize(Game,
				KingdomIdentityFenceDisposition.Unfounded, 0L, null, null,
				out KingdomIdentityFence fence, out string failure), Is.True, failure);
			fence.PendingTransactionId = new string('1', 32);
			Assert.That(KingdomIdentityFenceRules.Valid(fence, out failure), Is.False, failure);
			fence = OperationalFence(Digest('e'));
			fence.PendingTransactionId = new string('1', 32);
			fence.PendingIncarnation = 2L;
			Assert.That(KingdomIdentityFenceRules.Valid(fence, out failure), Is.False, failure);
		}

		[Test]
		public void FenceObservationFailsClosedForWrongGameAndLostAuthority()
		{
			KingdomIdentityFence fence = OperationalFence(Digest('e'));
			Assert.That(KingdomIdentityFenceRules.Observe(fence, "other", true),
				Is.EqualTo(KingdomIdentityFenceObservation.WrongGame));
			Assert.That(KingdomIdentityFenceRules.Observe(fence, Game, false),
				Is.EqualTo(KingdomIdentityFenceObservation.LostAuthority));
			fence.Revision = 0;
			Assert.That(KingdomIdentityFenceRules.Observe(fence, Game, false),
				Is.EqualTo(KingdomIdentityFenceObservation.Malformed));
		}

		[Test]
		public void TwoRetireRefoundCyclesPreserveExactPredecessorsAndHighWater()
		{
			KingdomIdentityFence first = OperationalFence(Digest('e'));
			string firstWire = KingdomRealmRetirementCodec.EncodeFence(first);
			string firstBefore = KingdomRetirementDigestRules.Evidence(
				"identity-fence-wire", new List<string> { firstWire });
			Assert.That(KingdomIdentityFenceRules.TryPrepareRemoval(first, first.Revision,
				Game, Realm, first.LastRealmDigest, Digest('f'), firstBefore, Digest('a'),
				out KingdomIdentityFence retiredOne, out string failure), Is.True, failure);
			Assert.That(retiredOne.PreparedFromDigest, Is.EqualTo(firstBefore));

			string transaction = new string('1', 32);
			Assert.That(KingdomIdentityFenceRules.TryReserveIncarnation(retiredOne,
				retiredOne.Revision, transaction, out KingdomIdentityFence reserved,
				out long incarnation, out failure), Is.True, failure);
			Assert.That(incarnation, Is.EqualTo(2L));
			Assert.That(reserved.TombstoneChainDigest, Is.EqualTo(Digest('f')));
			string secondRealm = "taf:realm:c2:second";
			string secondDigest = Digest('7');
			Assert.That(KingdomIdentityFenceRules.TryCommitOperational(reserved,
				reserved.Revision, transaction, secondRealm, secondDigest,
				out KingdomIdentityFence liveTwo, out failure), Is.True, failure);
			string secondWire = KingdomRealmRetirementCodec.EncodeFence(liveTwo);
			string secondBefore = KingdomRetirementDigestRules.Evidence(
				"identity-fence-wire", new List<string> { secondWire });
			Assert.That(KingdomIdentityFenceRules.TryPrepareRemoval(liveTwo,
				liveTwo.Revision, Game, secondRealm, secondDigest, Digest('8'), secondBefore,
				Digest('b'),
				out KingdomIdentityFence retiredTwo, out failure), Is.True, failure);
			Assert.That(retiredTwo.PreparedFromDigest, Is.EqualTo(secondBefore));
			Assert.That(retiredTwo.PreparedFromDigest, Is.Not.EqualTo(firstBefore));
			Assert.That(retiredTwo.NextRealmIncarnation, Is.EqualTo(2L));
		}

		[Test]
		public void PreparedFenceCodecRetainsExactRawCasRecoveryEvidence()
		{
			KingdomIdentityFence live = OperationalFence(Digest('e'));
			string before = KingdomRetirementDigestRules.Evidence("wire",
				new List<string> { KingdomRealmRetirementCodec.EncodeFence(live) });
			Assert.That(KingdomIdentityFenceRules.TryPrepareRemoval(live, live.Revision,
				Game, Realm, live.LastRealmDigest, Digest('f'), before, Digest('a'),
				out KingdomIdentityFence prepared, out string failure), Is.True, failure);
			string wire = KingdomRealmRetirementCodec.EncodeFence(prepared);
			Assert.That(KingdomRealmRetirementCodec.TryDecodeFence(wire,
				out KingdomIdentityFence decoded, out failure), Is.True, failure);
			Assert.That(decoded.PreparedFromDigest, Is.EqualTo(before));
			Assert.That(decoded.PreparedReceiptDigest, Is.EqualTo(Digest('a')));
			Assert.That(decoded.TombstoneChainDigest, Is.EqualTo(Digest('f')));
			Assert.That(KingdomRealmRetirementCodec.EncodeFence(decoded), Is.EqualTo(wire));
		}

		[Test]
		public void ProjectionRetryAcceptsOnlyExactFrozenRowsOrCompleteRemoval()
		{
			Assert.That(KingdomRealmRemovalRetryRules.ExactOrRemoved(2L, Digest('a'),
				2, Digest('a')), Is.True);
			Assert.That(KingdomRealmRemovalRetryRules.ExactOrRemoved(2L, Digest('a'),
				0, Digest('b')), Is.True, "a completed family has no live rows to substitute");
			Assert.That(KingdomRealmRemovalRetryRules.ExactOrRemoved(2L, Digest('a'),
				1, Digest('a')), Is.False, "partial removal cannot authorize an unknown remainder");
			Assert.That(KingdomRealmRemovalRetryRules.ExactOrRemoved(2L, Digest('a'),
				2, Digest('b')), Is.False, "same-cardinality substitution must refuse");
		}

		[Test]
		public void ProjectionRetryAcceptsOnlyExactRemainingIdentities()
		{
			Assert.That(KingdomRealmRemovalRetryRules.ExactRemainingSubset(
				new[] { "one", "two" }, new[] { "two" }), Is.True,
				"a crash after one exact row was removed must remain retryable");
			Assert.That(KingdomRealmRemovalRetryRules.ExactRemainingSubset(
				new[] { "one", "two" }, new[] { "replacement" }), Is.False);
			Assert.That(KingdomRealmRemovalRetryRules.ExactRemainingSubset(
				new[] { "one", "two" }, new[] { "two", "two" }), Is.False);
			string[] civic = { "body-a|r_KingdomOfficeProjection",
				"fixture-b|r_KingdomRemembranceProjection" };
			Assert.That(KingdomRealmRemovalRetryRules.ExactRemainingSubset(civic,
				new[] { civic[1] }), Is.True,
				"an interruption after one exact owner removal remains resumable");
			Assert.That(KingdomRealmRemovalRetryRules.ExactRemainingSubset(civic,
				new string[0]), Is.True,
				"repeating both exact owner removals after absence is idempotent");
		}

		[Test]
		public void CallbackAndTerminalFamiliesResumeFirstMiddleAndLastCuts()
		{
			string[] frozen = { "first", "middle", "last" };
			foreach (string family in new[] { "systems", "global-state", "quests", "recipes",
				"journal", "civic-semantics", "factions" })
			{
				Assert.That(KingdomRealmRemovalRetryRules.CutProgress(frozen,
					new[] { "middle", "last" }, true),
					Is.EqualTo(KingdomRemovalCutProgress.InvokeOrResume), family + " first cut");
				Assert.That(KingdomRealmRemovalRetryRules.CutProgress(frozen,
					new[] { "last" }, true),
					Is.EqualTo(KingdomRemovalCutProgress.InvokeOrResume), family + " middle cut");
				Assert.That(KingdomRealmRemovalRetryRules.CutProgress(frozen,
					new string[0], true), Is.EqualTo(KingdomRemovalCutProgress.Settled),
					family + " last cut");
				Assert.That(KingdomRealmRemovalRetryRules.CutProgress(frozen,
					new[] { "foreign" }, true), Is.EqualTo(KingdomRemovalCutProgress.Quarantine),
					family + " foreign remainder");
				Assert.That(KingdomRealmRemovalRetryRules.CutProgress(frozen, frozen, false),
					Is.EqualTo(KingdomRemovalCutProgress.Quarantine), family + " no attempt");
			}
		}

		[Test]
		public void WorstCaseCapacityIncludesPreviewCompletionAuthorityAndFence()
		{
			Assert.That(KingdomRealmRemovalRetryRules.WorstCaseCapacityReserved(
				500, 3, 3, 5), Is.True);
			Assert.That(KingdomRealmRemovalRetryRules.WorstCaseCapacityReserved(
				500, 3, 3, 6), Is.False);
		}

		[Test]
		public void NestedCustodyAndOwnerEvidenceFailClosed()
		{
			Assert.That(KingdomRealmRemovalRetryRules.ClassifyOwnerEvidence(Realm,
				new[] { Realm, Realm }, true, false),
				Is.EqualTo(KingdomRemovalOwnerVerdict.CurrentRealm));
			Assert.That(KingdomRealmRemovalRetryRules.ClassifyOwnerEvidence(Realm,
				new string[0], true, false),
				Is.EqualTo(KingdomRemovalOwnerVerdict.Ambiguous));
			Assert.That(KingdomRealmRemovalRetryRules.ClassifyOwnerEvidence(Realm,
				new[] { Realm, "taf:realm:foreign" }, true, false),
				Is.EqualTo(KingdomRemovalOwnerVerdict.ForeignOrDivergent));
			Assert.That(KingdomRealmRemovalRetryRules.ClassifyOwnerEvidence(Realm,
				new[] { Realm }, true, true),
				Is.EqualTo(KingdomRemovalOwnerVerdict.ValueBearing));
			Assert.That(KingdomRealmRemovalRetryRules.GroundMutationAllowed(true,
				KingdomRemovalOwnerVerdict.CurrentRealm), Is.False,
				"nested player custody is never city-ground mutation authority");
		}

		[Test]
		public void CallbackOutcomesFollowExactNativeRegistryState()
		{
			Assert.That(KingdomRealmRemovalRetryRules.AuthenticatedPlayerTerminalProgress(
				2L, true, true, true), Is.True, "the exact frozen pair may begin its cut");
			Assert.That(KingdomRealmRemovalRetryRules.AuthenticatedPlayerTerminalProgress(
				2L, true, false, true), Is.True,
				"ability-success/part-throw resumes from its authenticated suffix");
			Assert.That(KingdomRealmRemovalRetryRules.AuthenticatedPlayerTerminalProgress(
				2L, false, false, false), Is.True,
				"part-absent is terminal even when its callback later throws");
			Assert.That(KingdomRealmRemovalRetryRules.AuthenticatedPlayerTerminalProgress(
				2L, false, true, true), Is.False,
				"an out-of-order ability cannot borrow the frozen authority");
			Assert.That(KingdomRealmRemovalRetryRules.AuthenticatedPlayerTerminalProgress(
				2L, true, false, false), Is.False,
				"a substituted part or retained identity fails closed");
			Assert.That(KingdomRealmRemovalRetryRules.TerminalSystemRemovalSettled(false, true),
				Is.True, "native registry absence is terminal despite a later callback throw");
			Assert.That(KingdomRealmRemovalRetryRules.TerminalSystemRemovalSettled(true, false),
				Is.False);
		}

		[Test]
		public void FixedWitnessRetirementIsTerminalIdempotentAndIdentityPreserving()
		{
			KingdomWitnessWorkSource source = new KingdomWitnessWorkSource
			{
				EventId = "taf:event:c2-witness", SettlementId = "taf:settlement:c2",
				EventKind = KingdomWitnessWorkRules.RaisingAdapterKind,
				EventText = "raised a civic work", ClosedTick = 10L,
				MakerResidentId = 1, MakerName = "Eshkind"
			};
			source.SnapshotDigest = KingdomWitnessWorkRules.SnapshotDigest(source);
			KingdomWitnessWorkBook book = new KingdomWitnessWorkBook();
			Assert.That(KingdomWitnessWorkRules.TryCapture(book, book.Revision, source,
				out KingdomWitnessWorkReceipt row, out string failure), Is.True, failure);
			Assert.That(KingdomWitnessWorkRules.TryPrepareCarrier(book, book.Revision,
				row.WorkId, "taf:object:c2-carrier", "taf:zone:c2-zone",
				"taf:construction:c2-carrier", 4, 5, 20L, out failure), Is.True, failure);
			row = book.Rows[0];
			Assert.That(KingdomWitnessWorkRules.TryCommitCarrier(book, book.Revision,
				row.WorkId, row.CarrierReceiptId, 20L, out failure), Is.True, failure);
			string carrier = book.Rows[0].CarrierReceiptId;
			KingdomWitnessWorkBook lost = KingdomWitnessWorkCodec.Decode(
				KingdomWitnessWorkCodec.Encode(book));
			Assert.That(KingdomWitnessWorkRules.TryReconcileCarrier(lost, lost.Revision,
				row.WorkId, false, false, 25L, out failure), Is.True, failure);
			Assert.That(lost.Rows[0].Phase, Is.EqualTo(KingdomWitnessWorkPhase.Lost));
			byte[] lostTerminal = KingdomWitnessWorkCodec.Encode(lost);
			Assert.That(KingdomWitnessWorkRules.TryReconcileCarrier(lost, lost.Revision,
				row.WorkId, true, true, 30L, out failure), Is.False,
				"retirement must preserve a previously witnessed loss, not rewrite it as removal");
			CollectionAssert.AreEqual(lostTerminal, KingdomWitnessWorkCodec.Encode(lost));
			Assert.That(KingdomWitnessWorkRules.TryReconcileCarrier(book, book.Revision,
				row.WorkId, true, true, 30L, out failure), Is.True, failure);
			Assert.That(book.Rows[0].Phase, Is.EqualTo(KingdomWitnessWorkPhase.Removed));
			Assert.That(book.Rows[0].CarrierReceiptId, Is.EqualTo(carrier));
			byte[] terminal = KingdomWitnessWorkCodec.Encode(book);
			Assert.That(KingdomWitnessWorkRules.TryReconcileCarrier(book, 0L,
				row.WorkId, true, true, 30L, out failure), Is.True, failure,
				"terminal retry does not reopen or rewrite the durable row");
			CollectionAssert.AreEqual(terminal, KingdomWitnessWorkCodec.Encode(book));
		}

		[Test]
		public void NativeCivicArchiveUsesNineNotesAndStaysInsideSaveBudget()
		{
			int[] caps =
			{
				KingdomCivicMemoryLimits.MaxCivicArtifactsBytes,
				KingdomCivicMemoryLimits.MaxCivicPracticeBytes,
				KingdomCivicMemoryLimits.MaxBodyHistoryBytes,
				KingdomCivicMemoryLimits.MaxCuriosityBytes,
				KingdomCivicMemoryLimits.MaxCivicLeadsBytes,
				KingdomCivicMemoryLimits.MaxTreatyBytes,
				KingdomCivicMemoryLimits.MaxCommunalRiteBytes,
				KingdomCivicMemoryLimits.MaxGuestFeastBytes,
				KingdomCivicMemoryLimits.MaxVillageCovenantBytes
			};
			long encoded = 0L;
			for (int i = 0; i < caps.Length; i++) encoded += ((caps[i] + 2L) / 3L) * 4L;
			Assert.That(caps.Length, Is.EqualTo(KingdomCivicMemoryLimits.KnownSectionCount));
			Assert.That(encoded, Is.EqualTo(1119828L));
			Assert.That(KingdomCivicMemoryLimits.MaxEnvelopeBytes + encoded,
				Is.EqualTo(1959876L).And.LessThan(4L * 1024L * 1024L),
				"the transient source-plus-native archive remains inside the 4 MiB contract");
		}

		[Test]
		public void PendingIncarnationMustEqualFenceHighWater()
		{
			KingdomIdentityFence fence = OperationalFence(Digest('e'));
			fence.Disposition = KingdomIdentityFenceDisposition.Unfounded;
			fence.LastRealmId = null; fence.LastRealmDigest = null;
			fence.NextRealmIncarnation = 2L;
			fence.PendingTransactionId = new string('1', 32);
			fence.PendingIncarnation = fence.NextRealmIncarnation - 1L;
			Assert.That(KingdomIdentityFenceRules.Valid(fence, out string _), Is.False);
		}

		[Test]
		public void TombstoneIsBoundToExactPredecessorWireAndHighWater()
		{
			KingdomRealmRetirementState ready = Ready();
			string exact = KingdomRetirementDigestRules.Tombstone(Digest('a'), ready, Digest('b'));
			Assert.That(KingdomRetirementDigestRules.Tombstone(Digest('c'), ready, Digest('b')),
				Is.Not.EqualTo(exact), "forged predecessor wire must change the tombstone");
			ready.RealmIncarnation++;
			Assert.That(KingdomRetirementDigestRules.Tombstone(Digest('a'), ready, Digest('b')),
				Is.Not.EqualTo(exact), "forged incarnation must change the tombstone");
		}

		[Test]
		public void TombstoneBindsTheFullCanonicalReadyReceipt()
		{
			KingdomRealmRetirementState ready = Ready();
			string exact = KingdomRetirementDigestRules.Tombstone(Digest('a'), ready, Digest('b'));
			AssertTombstoneChanged(exact, ready, state => state.ReceiptId = new string('2', 32));
			AssertTombstoneChanged(exact, ready, state => state.RealmId += ":changed");
			AssertTombstoneChanged(exact, ready, state => state.FactionId += " changed");
			AssertTombstoneChanged(exact, ready, state => state.GameId += "-changed");
			AssertTombstoneChanged(exact, ready, state => state.StartedTick--);
			AssertTombstoneChanged(exact, ready, state => state.UpdatedTick++);
			AssertTombstoneChanged(exact, ready, state => state.AuthorityDigest = Digest('c'));
			AssertTombstoneChanged(exact, ready, state => state.Fault = "bounded fault");
			AssertTombstoneChanged(exact, ready,
				state => state.Locators[0].SettlementId += ":changed");
			AssertTombstoneChanged(exact, ready,
				state => state.Locators[0].ObjectCount++);
			AssertTombstoneChanged(exact, ready,
				state => state.Locators[0].EvidenceDigest = Digest('d'));
			AssertTombstoneChanged(exact, ready,
				state => state.Records[0].Amount++);
			AssertTombstoneChanged(exact, ready,
				state => state.Records[0].Detail += " changed");
		}

		[Test]
		public void PreparedProofRejectsForgedTombstoneAndHighWater()
		{
			KingdomRealmRetirementState ready = Ready();
			string realmDigest = Digest('e');
			KingdomIdentityFence live = OperationalFence(realmDigest);
			string predecessor = Digest('a');
			string tombstone = KingdomRetirementDigestRules.Tombstone(predecessor,
				ready, realmDigest);
			string binding = KingdomIdentityFenceReceiptRules.PreparedReceiptBinding(ready);
			Assert.That(KingdomIdentityFenceRules.TryPrepareRemoval(live, live.Revision,
				Game, Realm, realmDigest, tombstone, predecessor, binding,
				out KingdomIdentityFence fence, out string failure), Is.True, failure);
			Assert.That(KingdomIdentityFenceReceiptRules.PreparedProofMatches(fence,
				ready, realmDigest, 1L), Is.True);
			fence.TombstoneChainDigest = Digest('f');
			Assert.That(KingdomIdentityFenceReceiptRules.PreparedProofMatches(fence,
				ready, realmDigest, 1L), Is.False);
			fence.TombstoneChainDigest = tombstone; fence.NextRealmIncarnation = 2L;
			Assert.That(KingdomIdentityFenceReceiptRules.PreparedProofMatches(fence,
				ready, realmDigest, 1L), Is.False);
		}

		[Test]
		public void TerminalIntentAllowsFenceButNeverClaimsCleanCompletion()
		{
			KingdomRealmRetirementState state = ClosedCleaning();
			Assert.That(KingdomRealmRetirementRules.TryRecord(state, state.Revision,
				Record(KingdomRemovalProjectionKind.Ability, "player-intent",
					KingdomRemovalDisposition.TerminalIntent, Digest('a'), Digest('b'), 1L,
					"authorized"), 10L, out state, out string failure), Is.True, failure);
			Assert.That(KingdomRealmRetirementRules.CanCommitFence(state, out failure),
				Is.True, failure);
			Assert.That(KingdomRealmRetirementRules.CleanRemovalProvable(state), Is.False);
		}

		[Test]
		public void FenceCapacityAlwaysReservesOneTerminalRecord()
		{
			Assert.That(KingdomRealmRemovalRetryRules.FenceCapacityReserved(
				KingdomRealmRetirementState.MaxRecords - 1), Is.True);
			Assert.That(KingdomRealmRemovalRetryRules.FenceCapacityReserved(
				KingdomRealmRetirementState.MaxRecords), Is.False);
		}

		[Test]
		public void PreparedReceiptBindingSurvivesOnlyItsExactFenceTransitions()
		{
			KingdomRealmRetirementState ready = Ready();
			string binding = KingdomIdentityFenceReceiptRules.PreparedReceiptBinding(ready);
			Assert.That(KingdomRealmRetirementRules.Digest(binding), Is.True);
			KingdomRemovalRecord fence = Record(KingdomRemovalProjectionKind.GlobalState,
				KingdomRealmRetirementRules.FenceRecordId, KingdomRemovalDisposition.Preserved,
				Digest('a'), Digest('b'), 0L, "fence");
			Assert.That(KingdomRealmRetirementRules.TryRecord(ready, ready.Revision, fence,
				10L, out KingdomRealmRetirementState recorded, out string failure), Is.True, failure);
			Assert.That(KingdomRealmRetirementRules.TrySetPhase(recorded, recorded.Revision,
				KingdomRealmRetirementPhase.ReadyForFence,
				KingdomRealmRetirementPhase.FenceCommitted, 10L,
				out KingdomRealmRetirementState committed, out failure), Is.True, failure);
			Assert.That(KingdomRealmRetirementRules.TrySetPhase(committed, committed.Revision,
				KingdomRealmRetirementPhase.FenceCommitted,
				KingdomRealmRetirementPhase.PreparedForRemoval, 10L,
				out KingdomRealmRetirementState prepared, out failure), Is.True, failure);
			Assert.That(KingdomIdentityFenceReceiptRules.PreparedReceiptBinding(prepared),
				Is.EqualTo(binding));
			prepared.Records.Find(row => row.Id == KingdomRealmRetirementRules.AuthorityRecordId)
				.Detail = "different";
			Assert.That(KingdomIdentityFenceReceiptRules.PreparedReceiptBinding(prepared),
				Is.Not.EqualTo(binding));
		}

		private static KingdomRealmRetirementState Planned()
		{
			Assert.That(KingdomRealmRetirementRules.TryPlan(new string('1', 32), Realm,
				Faction, Game, 1L, 10L, Authority, new List<KingdomRemovalLocator>
				{
					new KingdomRemovalLocator
					{
						ZoneId = "JoppaWorld.11.22.1.1.10", SettlementId = "taf:settlement:v1:c2"
					}
				}, out KingdomRealmRetirementState state, out string failure), Is.True, failure);
			return state;
		}

		private static KingdomRealmRetirementState Cleaning()
		{
			KingdomRealmRetirementState state = Planned();
			Assert.That(KingdomRealmRetirementRules.TrySetPhase(state, state.Revision,
				KingdomRealmRetirementPhase.Planning, KingdomRealmRetirementPhase.Paused,
				10L, out state, out string failure), Is.True, failure);
			Assert.That(KingdomRealmRetirementRules.TrySetPhase(state, state.Revision,
				KingdomRealmRetirementPhase.Paused,
				KingdomRealmRetirementPhase.CleaningGround, 10L,
				out state, out failure), Is.True, failure);
			return state;
		}

		private static KingdomRealmRetirementState ClosedCleaning()
		{
			KingdomRealmRetirementState state = Cleaning();
			Assert.That(Mark(state, KingdomRemovalLocatorState.Cleaning, null,
				out state), Is.True);
			Assert.That(Mark(state, KingdomRemovalLocatorState.Cleaned, Ground,
				out state), Is.True);
			KingdomRemovalRecord authority = Record(KingdomRemovalProjectionKind.Authority,
				KingdomRealmRetirementRules.AuthorityRecordId,
				KingdomRemovalDisposition.Closed, Authority, Ground, 1L, "closed");
			Assert.That(KingdomRealmRetirementRules.TryRecord(state, state.Revision,
				authority, 10L, out state, out string failure), Is.True, failure);
			return state;
		}

		private static KingdomRealmRetirementState Ready()
		{
			KingdomRealmRetirementState state = ClosedCleaning();
			Assert.That(KingdomRealmRetirementRules.TrySetPhase(state, state.Revision,
				KingdomRealmRetirementPhase.CleaningGround,
				KingdomRealmRetirementPhase.ReadyForFence, 10L,
				out state, out string failure), Is.True, failure);
			return state;
		}

		private static bool Mark(KingdomRealmRetirementState State,
			KingdomRemovalLocatorState Next, string Evidence,
			out KingdomRealmRetirementState Updated)
		{
			return KingdomRealmRetirementRules.TryMarkGround(State, State.Revision,
				State.Locators[0].ZoneId, Next, 10L, Next == KingdomRemovalLocatorState.Cleaned
					? 1 : 0, Evidence, out Updated, out string _);
		}

		private static KingdomRemovalRecord Record(KingdomRemovalProjectionKind Kind,
			string Id, KingdomRemovalDisposition Disposition, string Before, string After,
			long Amount, string Detail)
		{
			return new KingdomRemovalRecord
			{
				Kind = Kind, Id = Id, Disposition = Disposition,
				BeforeDigest = Before, AfterDigest = After, Amount = Amount, Detail = Detail
			};
		}

		private static KingdomIdentityFence OperationalFence(string RealmDigest)
		{
			Assert.That(KingdomIdentityFenceRules.TryInitialize(Game,
				KingdomIdentityFenceDisposition.Operational, 1L, Realm, RealmDigest,
				out KingdomIdentityFence fence, out string failure), Is.True, failure);
			return fence;
		}

		private static string Digest(char Value)
		{
			return new string(Value, 64);
		}

		private static void AssertTombstoneChanged(string Exact,
			KingdomRealmRetirementState Ready, Action<KingdomRealmRetirementState> Change)
		{
			KingdomRealmRetirementState changed = Ready.Clone(); Change(changed);
			Assert.That(KingdomRealmRetirementRules.Valid(changed, out string failure),
				Is.True, failure);
			Assert.That(KingdomRetirementDigestRules.Tombstone(Digest('a'), changed,
				Digest('b')), Is.Not.EqualTo(Exact));
		}
	}
}
