// TASK 52 deliverable: authored and self-checked only — DECLARED UNEXECUTED (no compile, no run); root executes and integrates.
#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Basis = ThousandAndFirst.KingdomRealmHashBasisRules;
using Codec = ThousandAndFirst.KingdomArchivedSettlementCodec;
using Outcome = ThousandAndFirst.KingdomRealmHashBasisRules.BasisOutcome;
using Storage = ThousandAndFirst.KingdomSubsidenceStepCodec;

namespace ThousandAndFirst.Tests
{
	/// <summary>Basis selection is pure, so every case drives the real selector with a recording
	/// hasher and asserts both the outcome and the exact sequence of versions it was asked for.
	/// The last group drives the real TryEncodeVersion projection seam.
	/// NOTE: none of the tests in this file were executed by their author.</summary>
	[TestFixture]
	public class KingdomRealmHashBasisTests
	{
		private const string Hex = "0123456789abcdef";

		/// <summary>A distinct canonical hash per seed: 64 lowercase hexadecimal digits.</summary>
		private static string Digest(int Seed)
		{
			return new string('0', 62) + Hex[(Seed >> 4) & 0xF] + Hex[Seed & 0xF];
		}

		/// <summary>The persisted hash under test; no candidate seed below can collide.</summary>
		private static string Expected() { return Digest(200); }

		private static Dictionary<int, string> Disagreeing()
		{
			Dictionary<int, string> answers = new Dictionary<int, string>();
			for (int version = Basis.MinVersion; version <= Basis.MaxVersion; version++)
				answers.Add(version, Digest(version));
			return answers;
		}

		private static Dictionary<int, string> Agreeing(params int[] Versions)
		{
			Dictionary<int, string> answers = Disagreeing();
			for (int index = 0; index < Versions.Length; index++)
				answers[Versions[index]] = Expected();
			return answers;
		}

		private static int[] Descending(int From, int To)
		{
			int[] order = new int[From - To + 1];
			for (int index = 0; index < order.Length; index++) order[index] = From - index;
			return order;
		}

		/// <summary>Records the exact versions it is asked for. A version absent from Answers is
		/// unrepresentable (returns false); ThrowAtVersion raises instead of answering.</summary>
		private sealed class RecordingHasher
		{
			private readonly List<int> Asked = new List<int>();
			private readonly Dictionary<int, string> Answers;
			private readonly int ThrowAtVersion;

			internal RecordingHasher(Dictionary<int, string> Answers)
				: this(Answers, Basis.Unresolved) { }

			internal RecordingHasher(Dictionary<int, string> Answers, int ThrowAtVersion)
			{
				this.Answers = Answers;
				this.ThrowAtVersion = ThrowAtVersion;
			}

			internal int[] Order() { return Asked.ToArray(); }

			internal bool Compute(int Version, out string Hash)
			{
				Asked.Add(Version);
				if (Version == ThrowAtVersion)
					throw new InvalidOperationException("hasher failed at version " + Version);
				string answer;
				if (!Answers.TryGetValue(Version, out answer)) { Hash = null; return false; }
				Hash = answer;
				return true;
			}
		}

		[TestCase(1)] [TestCase(10)] [TestCase(18)] [TestCase(19)]
		public void PinnedBasisReproducingTheHashIsSelectedAfterExactlyOneCall(int pinned)
		{
			RecordingHasher hasher = new RecordingHasher(Agreeing(pinned));
			Outcome outcome = Basis.Select(Expected(), pinned, hasher.Compute,
				out int basis, out string reason);
			ClassicAssert.AreEqual(Outcome.Selected, outcome);
			ClassicAssert.AreEqual(pinned, basis); ClassicAssert.IsNull(reason);
			ClassicAssert.AreEqual(Basis.CanonicalHashLength, Expected().Length);
			CollectionAssert.AreEqual(new[] { pinned }, hasher.Order());
		}

		[TestCase(1)] [TestCase(10)] [TestCase(19)]
		public void PinnedBasisThatDisagreesRefusesInsteadOfSearching(int pinned)
		{
			// Every accepted version would match the searched hash, yet a pinned slot must never
			// widen into a search: otherwise a tampered receipt buys itself a fresh basis.
			RecordingHasher hasher = new RecordingHasher(
				Agreeing(Descending(Basis.MaxVersion, Basis.MinVersion)));
			Outcome outcome = Basis.Select(Digest(201), pinned, hasher.Compute,
				out int basis, out string reason);
			ClassicAssert.AreEqual(Outcome.PinnedMismatch, outcome);
			ClassicAssert.AreEqual(Basis.Unresolved, basis); ClassicAssert.IsNotNull(reason);
			CollectionAssert.AreEqual(new[] { pinned }, hasher.Order());
		}

		[TestCase(1)] [TestCase(10)] [TestCase(19)]
		public void UnrepresentablePinnedBasisFailsAndNeverFallsBackToSearch(int pinned)
		{
			RecordingHasher hasher = new RecordingHasher(new Dictionary<int, string>());
			Outcome outcome = Basis.Select(Expected(), pinned, hasher.Compute,
				out int basis, out string reason);
			ClassicAssert.AreEqual(Outcome.ComputationFailure, outcome);
			ClassicAssert.AreEqual(Basis.Unresolved, basis); ClassicAssert.IsNotNull(reason);
			CollectionAssert.AreEqual(new[] { pinned }, hasher.Order());
		}

		[Test]
		public void PinnedHasherThatThrowsReportsTheExceptionTypeOnly()
		{
			RecordingHasher hasher = new RecordingHasher(Agreeing(12), 12);
			Outcome outcome = Basis.Select(Expected(), 12, hasher.Compute,
				out int basis, out string reason);
			ClassicAssert.AreEqual(Outcome.ComputationFailure, outcome);
			ClassicAssert.AreEqual(Basis.Unresolved, basis);
			StringAssert.Contains("InvalidOperationException", reason);
			StringAssert.DoesNotContain("hasher failed at version", reason);
			CollectionAssert.AreEqual(new[] { 12 }, hasher.Order());
		}

		[Test]
		public void UnresolvedSearchAsksEveryAcceptedVersionNewestFirstBeforeRefusing()
		{
			RecordingHasher hasher = new RecordingHasher(Disagreeing());
			Outcome outcome = Basis.Select(Expected(), Basis.Unresolved, hasher.Compute,
				out int basis, out string reason);
			ClassicAssert.AreEqual(Outcome.NoMatch, outcome);
			ClassicAssert.AreEqual(Basis.Unresolved, basis); ClassicAssert.IsNotNull(reason);
			CollectionAssert.AreEqual(Basis.SearchOrder(), hasher.Order());
		}

		[TestCase(1)] [TestCase(10)] [TestCase(19)]
		public void UnresolvedSearchSelectsAUniqueMatchWithoutStoppingEarly(int matching)
		{
			RecordingHasher hasher = new RecordingHasher(Agreeing(matching));
			Outcome outcome = Basis.Select(Expected(), Basis.Unresolved, hasher.Compute,
				out int basis, out string reason);
			ClassicAssert.AreEqual(Outcome.Selected, outcome);
			ClassicAssert.AreEqual(matching, basis); ClassicAssert.IsNull(reason);
			CollectionAssert.AreEqual(Basis.SearchOrder(), hasher.Order());
		}

		[TestCase(19, 18)] [TestCase(19, 1)] [TestCase(10, 9)] [TestCase(2, 1)]
		public void UnresolvedSearchRefusesWhenTwoVersionsReproduceTheHash(int first, int second)
		{
			RecordingHasher hasher = new RecordingHasher(Agreeing(first, second));
			Outcome outcome = Basis.Select(Expected(), Basis.Unresolved, hasher.Compute,
				out int basis, out string reason);
			ClassicAssert.AreEqual(Outcome.MultipleMatches, outcome);
			ClassicAssert.AreEqual(Basis.Unresolved, basis); ClassicAssert.IsNotNull(reason);
			CollectionAssert.AreEqual(Basis.SearchOrder(), hasher.Order());
		}

		[Test]
		public void UnrepresentableVersionsAreSkippedRatherThanEndingTheSearch()
		{
			// Only v3 can represent this archive; the other eighteen are asked and skipped.
			Dictionary<int, string> answers = new Dictionary<int, string> { { 3, Expected() } };
			Outcome outcome = Basis.Select(Expected(), Basis.Unresolved,
				new RecordingHasher(answers).Compute, out int basis, out string reason);
			ClassicAssert.AreEqual(Outcome.Selected, outcome);
			ClassicAssert.AreEqual(3, basis); ClassicAssert.IsNull(reason);
		}

		[Test]
		public void ThrowingHasherStopsTheUnresolvedSearchAtThatVersion()
		{
			RecordingHasher hasher = new RecordingHasher(Agreeing(19, 1), 15);
			Outcome outcome = Basis.Select(Expected(), Basis.Unresolved, hasher.Compute,
				out int basis, out string reason);
			ClassicAssert.AreEqual(Outcome.ComputationFailure, outcome);
			ClassicAssert.AreEqual(Basis.Unresolved, basis);
			StringAssert.Contains("InvalidOperationException", reason);
			CollectionAssert.AreEqual(new[] { 19, 18, 17, 16, 15 }, hasher.Order());
		}

		[TestCase(0)] [TestCase(7)]
		public void HasherReturningANonCanonicalHashIsAComputationFailure(int stored)
		{
			Dictionary<int, string> answers = Disagreeing(); answers[7] = "NOTAHASH";
			Outcome outcome = Basis.Select(Expected(), stored,
				new RecordingHasher(answers).Compute, out int basis, out string reason);
			ClassicAssert.AreEqual(Outcome.ComputationFailure, outcome);
			ClassicAssert.AreEqual(Basis.Unresolved, basis); ClassicAssert.IsNotNull(reason);
		}

		[TestCase(-1)] [TestCase(20)] [TestCase(int.MinValue)] [TestCase(int.MaxValue)]
		public void StoredBasisOutsideTheAcceptedSetIsMalformedAndAsksNothing(int stored)
		{
			RecordingHasher hasher = new RecordingHasher(Agreeing(19));
			Outcome outcome = Basis.Select(Expected(), stored, hasher.Compute,
				out int basis, out string reason);
			ClassicAssert.AreEqual(Outcome.Malformed, outcome);
			ClassicAssert.AreEqual(Basis.Unresolved, basis); ClassicAssert.IsNotNull(reason);
			CollectionAssert.IsEmpty(hasher.Order());
		}

		[TestCase((string)null)]
		[TestCase("")]
		[TestCase("0000000000000000000000000000000000000000000000000000000000000000 ")]
		[TestCase("000000000000000000000000000000000000000000000000000000000000000")]
		[TestCase("0000000000000000000000000000000000000000000000000000000000000C8")]
		[TestCase("00000000000000000000000000000000000000000000000000000000000000C8")]
		[TestCase("0000000000000000000000000000000000000000000000000000000000000zz8")]
		public void NonCanonicalPersistedHashIsMalformedAndAsksNothing(string persisted)
		{
			RecordingHasher hasher = new RecordingHasher(Agreeing(19));
			Outcome outcome = Basis.Select(persisted, Basis.Unresolved, hasher.Compute,
				out int basis, out string reason);
			ClassicAssert.AreEqual(Outcome.Malformed, outcome);
			ClassicAssert.AreEqual(Basis.Unresolved, basis); ClassicAssert.IsNotNull(reason);
			CollectionAssert.IsEmpty(hasher.Order());
		}

		[TestCase(0)] [TestCase(19)]
		public void AbsentHasherIsAComputationFailureAndNeverASelection(int stored)
		{
			Outcome outcome = Basis.Select(Expected(), stored, null,
				out int basis, out string reason);
			ClassicAssert.AreEqual(Outcome.ComputationFailure, outcome);
			ClassicAssert.AreEqual(Basis.Unresolved, basis); ClassicAssert.IsNotNull(reason);
		}

		[Test]
		public void SearchOrderIsTheWholeAcceptedSetNewestFirstAndIsNotShared()
		{
			int[] order = Basis.SearchOrder();
			CollectionAssert.AreEqual(
				Descending(Codec.CurrentVersion, Codec.LegacyVersion), order);
			ClassicAssert.AreEqual(19, order.Length); ClassicAssert.AreEqual(19, order[0]);
			ClassicAssert.AreEqual(1, order[order.Length - 1]);
			order[0] = -1;
			ClassicAssert.AreEqual(19, Basis.SearchOrder()[0]);
		}

		[Test]
		public void NoOutcomeOtherThanSelectedEverReturnsABasis()
		{
			List<Outcome> seen = new List<Outcome>
			{
				Refused(Expected(), -1, new RecordingHasher(Disagreeing())),
				Refused("nope", 0, new RecordingHasher(Disagreeing())),
				Refused(Expected(), 0, new RecordingHasher(Disagreeing())),
				Refused(Expected(), 5, new RecordingHasher(Disagreeing())),
				Refused(Expected(), 0, new RecordingHasher(Agreeing(4, 9))),
				Refused(Expected(), 0, new RecordingHasher(Agreeing(9), 9)),
				Refused(Expected(), 9, new RecordingHasher(new Dictionary<int, string>()))
			};
			CollectionAssert.DoesNotContain(seen, Outcome.Selected);
			CollectionAssert.AreEquivalent(new[]
			{
				Outcome.Malformed, Outcome.Malformed, Outcome.NoMatch, Outcome.PinnedMismatch,
				Outcome.MultipleMatches, Outcome.ComputationFailure, Outcome.ComputationFailure
			}, seen);
		}

		private static Outcome Refused(string Persisted, int Stored, RecordingHasher Hasher)
		{
			Outcome outcome = Basis.Select(Persisted, Stored, Hasher.Compute,
				out int basis, out string reason);
			ClassicAssert.AreEqual(Basis.Unresolved, basis, "a refusal must not hand back a basis");
			ClassicAssert.IsNotNull(reason);
			return outcome;
		}

		// Versioned codec seam. Builder-backed: the historical wire is produced here by the
		// existing test-only v18 writer, so no external artifact is needed. The equivalent proof
		// against the real shipped 0.3.0 wire (base-v18.bin) lives on root's fixture path and is
		// deliberately not referenced here, because this runner executes every [Test] regardless
		// of [Explicit] or [Category]: an absolute-path fixture test would hard-fail a plain run.

		private static KingdomSettlement MigratedFromHistoricalV18(out byte[] Historical)
		{
			KingdomSettlement source = new KingdomSettlement { LastSubsidenceTick = 9876L };
			ClassicAssert.IsTrue(Codec.TryEncodeExpeditionResultV18ForTests(source, out Historical,
				out string failure), failure);
			ClassicAssert.IsTrue(Codec.TryDecode(Historical, out KingdomSettlement migrated,
				out int future, out failure), failure);
			ClassicAssert.AreEqual(0, future);
			ClassicAssert.AreEqual(Storage.LegacyWire, migrated.City.SubsidenceModel); return migrated;
		}

		[Test]
		public void CurrentSchemaDelegatesToTheUnchangedCurrentWriter()
		{
			KingdomSettlement value = new KingdomSettlement { LastSubsidenceTick = 4242L };
			ClassicAssert.IsTrue(Codec.TryEncode(value, out byte[] expected, out string failure),
				failure);
			ClassicAssert.IsTrue(Codec.TryEncodeVersion(value, Codec.CurrentVersion, out byte[] actual,
				out failure), failure);
			CollectionAssert.AreEqual(expected, actual);
			ClassicAssert.AreEqual(19, BitConverter.ToInt32(actual, 4));
			ClassicAssert.AreEqual(Storage.FreshWire, value.City.SubsidenceModel);
		}

		[TestCase(0)] [TestCase(-1)] [TestCase(20)] [TestCase(int.MinValue)]
		[TestCase(int.MaxValue)]
		public void SchemaOutsideTheAcceptedSetIsRefusedWithoutBytes(int schema)
		{
			KingdomSettlement value = new KingdomSettlement { LastSubsidenceTick = 4242L };
			ClassicAssert.IsFalse(Codec.TryEncodeVersion(value, schema, out byte[] bytes,
				out string failure));
			ClassicAssert.IsNull(bytes); ClassicAssert.IsNotNull(failure);
			ClassicAssert.AreEqual(4242L, value.LastSubsidenceTick);
		}

		[Test]
		public void HistoricalProjectionReproducesTheHistoricalWireExactly()
		{
			KingdomSettlement migrated = MigratedFromHistoricalV18(out byte[] historical);
			ClassicAssert.IsTrue(Codec.TryEncodeVersion(migrated, Codec.ExpeditionResultVersion,
				out byte[] projected, out string failure), failure);
			CollectionAssert.AreEqual(historical, projected);
			ClassicAssert.AreEqual(18, BitConverter.ToInt32(projected, 4));
			ClassicAssert.AreEqual(9876L, migrated.LastSubsidenceTick);
		}

		[Test]
		public void ProjectionBindsLiveFieldsInsteadOfReturningCachedBytes()
		{
			KingdomSettlement migrated = MigratedFromHistoricalV18(out byte[] historical);
			migrated.LastSubsidenceTick++;
			ClassicAssert.IsTrue(Codec.TryEncodeVersion(migrated, Codec.ExpeditionResultVersion,
				out byte[] projected, out string failure), failure);
			CollectionAssert.AreNotEqual(historical, projected);
			ClassicAssert.AreEqual(9877L, migrated.LastSubsidenceTick);
		}

		[Test]
		public void ProjectionRefusesToDiscardANonDefaultCurrentOnlyField()
		{
			KingdomSettlement migrated = MigratedFromHistoricalV18(out byte[] _);
			migrated.City.SubsidenceModel = Storage.FreshWire;
			ClassicAssert.IsTrue(migrated.City.HasValidSubsidenceStorage());
			ClassicAssert.IsFalse(Codec.TryEncodeVersion(migrated, Codec.ExpeditionResultVersion,
				out byte[] projected, out string failure));
			ClassicAssert.IsNull(projected); ClassicAssert.IsNotNull(failure);
			ClassicAssert.AreEqual(Storage.FreshWire, migrated.City.SubsidenceModel);
			// The unproved test-only writer still emits those bytes; the production seam is the
			// only path that refuses to publish a wire which would lose the live value.
			ClassicAssert.IsTrue(Codec.TryEncodeExpeditionResultV18ForTests(migrated,
				out byte[] unproved, out failure), failure);
			ClassicAssert.IsNotNull(unproved);
		}

		[TestCase(false)] [TestCase(true)]
		public void ProjectionSurvivesACurrentResaveWithoutChangingTheOldBytes(bool nullSeat)
		{
			KingdomSettlement value = nullSeat ? null : MigratedFromHistoricalV18(out byte[] _);
			ClassicAssert.IsTrue(Codec.TryEncodeVersion(value, Codec.ExpeditionResultVersion,
				out byte[] before, out string failure), failure);
			ClassicAssert.IsTrue(Codec.TryEncode(value, out byte[] saved, out failure), failure);
			ClassicAssert.AreEqual(19, BitConverter.ToInt32(saved, 4));
			ClassicAssert.IsTrue(Codec.TryDecode(saved, out value, out int future, out failure),
				failure);
			ClassicAssert.AreEqual(0, future);
			ClassicAssert.IsTrue(Codec.TryEncodeVersion(value, Codec.ExpeditionResultVersion,
				out byte[] after, out failure), failure);
			CollectionAssert.AreEqual(before, after);
		}

		[TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(7)] [TestCase(8)] [TestCase(14)]
		[TestCase(17)] [TestCase(18)]
		public void EveryAcceptedSchemaEitherProjectsExactlyOrRefusesWithoutBytes(int schema)
		{
			KingdomSettlement migrated = MigratedFromHistoricalV18(out byte[] _);
			bool projected = Codec.TryEncodeVersion(migrated, schema, out byte[] bytes,
				out string failure);
			if (projected)
			{
				ClassicAssert.IsNotNull(bytes);
				ClassicAssert.AreEqual(schema, BitConverter.ToInt32(bytes, 4));
				ClassicAssert.IsTrue(Codec.TryDecode(bytes, out KingdomSettlement round,
					out int future, out string proof), proof);
				ClassicAssert.AreEqual(0, future);
				ClassicAssert.IsTrue(Codec.ExactGraph(migrated, round, out proof), proof);
			}
			else
			{
				ClassicAssert.IsNull(bytes); ClassicAssert.IsNotNull(failure);
			}
			ClassicAssert.AreEqual(9876L, migrated.LastSubsidenceTick);
			ClassicAssert.AreEqual(Storage.LegacyWire, migrated.City.SubsidenceModel);
		}
	}
}
#endif
