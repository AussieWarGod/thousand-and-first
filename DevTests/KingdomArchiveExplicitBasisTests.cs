#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;
using Basis = ThousandAndFirst.KingdomRealmHashBasisRules;
using Codec = ThousandAndFirst.KingdomArchivedSettlementCodec;
using Outcome = ThousandAndFirst.KingdomRealmHashBasisRules.BasisOutcome;

namespace ThousandAndFirst.Tests
{
	// Runs in the standard Taf suite against Fixtures/ArchiveAuthority/base-v18.bin.
	// Optional TAF_ARCHIVE_V18_FIXTURE override must pass the same immutable SHA pin.
	// The small topology dependency in ArchiveExplicitBasisSupport is source-equivalence
	// checked below; all codec and authority methods compile from production files.
	// Scope: the real codec, the real explicit-basis TryAuthorityHash and the real basis
	// selector over the exact prior-receipt graph. NOT executed here: the engine realm reader,
	// the TAG1 live-System hash, the outer return callback or its other preconditions, and
	// native save/load. Proving basis 18 re-cuts the historical hash does NOT make the schema
	// migration solved; it isolates one hash-basis seam. No expected historical hash is
	// modified, corrected or recomputed anywhere in this file.
	[TestFixture]
	public sealed class KingdomArchiveExplicitBasisTests
	{
		private const string BaseCommit = "7d331fe8a77b630c8245889d36f811d1baf84059";
		private const int Magic = 0x41563138;

		// 18 is the settlement-wire schema the retained beta artifact was cut under; it is
		// written as a literal at every call site so the asked basis is visible in the diff.

		/// <summary>SHA256 of the retained actual-beta artifact base-v18.bin (50767 bytes),
		/// as recorded in SI524N/README.md. Pinned, so a substituted artifact fails here.</summary>
		private const string FixtureSha256 =
			"5f3f7074309e62ce61365276a44ac7a7f094c1c4ceccd38b9400a5e558dc2193";

		/// <summary>THE EXPECTED HISTORICAL HASH, reused verbatim and never recomputed. The
		/// SI524N consumer takes it from the artifact envelope (KingdomArchiveVersionAuthority
		/// Tests.cs:180-181, Before/After) and compares against it for all three phases at
		/// :105-108; NUnit reported it as the Expected value in consumer-baseline.log lines 5,
		/// 19 and 33. The consumer also asserts Before == After (:185), so one constant covers
		/// Intent, Attempting and Settled. Every use below cross-checks it against the bytes.</summary>
		private const string HistoricalAuthorityHash =
			"b96cc4a1c95408fbf4c7448fc2d25b788ce2f294a7b9412561e455071c5e5d2b";

		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		/// <summary>Which mutable tick a control case disturbs after the current re-save.</summary>
		public enum ChangedTick { Closed, Subsidence }

		private sealed class Frozen
		{
			internal byte[] Payload;
			internal string Before, After, Changed;
		}

		/// <summary>Records the versions the selector asks for and forwards each to the real
		/// explicit-basis hasher. It computes no hash of its own and reimplements nothing.</summary>
		private sealed class RecordingBasisHasher
		{
			private readonly KingdomRealmArchive Target;
			private readonly List<int> Asked = new List<int>();

			internal RecordingBasisHasher(KingdomRealmArchive Owner) { Target = Owner; }

			internal int[] Order() { return Asked.ToArray(); }

			internal bool Compute(int Version, out string Hash)
			{
				Asked.Add(Version);
				return Target.TryAuthorityHash(Target.ReturnReputation,
					KingdomRealmCallbackScope.Reputation, Version, out Hash, out string _);
			}
		}

		[Test]
		public void ArtifactPinned()
		{
			Frozen frozen = ReadFrozen();
			Assert.AreEqual(18, BitConverter.ToInt32(frozen.Payload, 4));
			Assert.AreEqual(HistoricalAuthorityHash, frozen.Before);
			Assert.AreEqual(HistoricalAuthorityHash, frozen.After);
			Assert.AreNotEqual(HistoricalAuthorityHash, frozen.Changed,
				"The artifact must still carry a real authority-change control.");
		}

		[Test]
		public void TopologyHashDependencyIsIdenticalToProduction()
		{
			string signature = "private static void WriteTopologyGraph(";
			Assert.AreEqual(MethodText(TestMain.ReadRepositoryText(
				"Core/KingdomRealmArchive.13SettlementTopology.cs"), signature),
				MethodText(TestMain.ReadRepositoryText("DevTests/ArchiveExplicitBasisSupport.cs"), signature));
		}

		private static string MethodText(string source, string signature)
		{
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0);
			int open = source.IndexOf('{', start), depth = 0;
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				if (source[i] == '}' && --depth == 0)
					return System.Text.RegularExpressions.Regex.Replace(
						source.Substring(start, i - start + 1), @"\s+", "");
			}
			Assert.Fail("Unterminated topology dependency"); return null;
		}

		[TestCase(KingdomRealmCallbackPhase.Intent)]
		[TestCase(KingdomRealmCallbackPhase.Attempting)]
		[TestCase(KingdomRealmCallbackPhase.Settled)]
		public void HistoricalHashMatchesExplicitBasis18(KingdomRealmCallbackPhase phase)
		{
			Frozen frozen = ReadFrozen();
			KingdomSettlement migrated = Decode(frozen.Payload);
			Assert.AreEqual(4, migrated.City.SchemaVersion);
			Assert.AreEqual("ss1:legacy", migrated.City.SubsidenceModel);
			Assert.AreEqual(9876L, migrated.LastSubsidenceTick);
			KingdomRealmArchive archive = Archive(migrated);
			archive.ReturnReputation.Phase = phase;
			archive.ReturnReputation.BeforeArchiveGraph = frozen.Before;
			if (phase == KingdomRealmCallbackPhase.Settled)
				archive.ReturnReputation.AfterArchiveGraph = frozen.After;
			string persisted = phase == KingdomRealmCallbackPhase.Settled
				? archive.ReturnReputation.AfterArchiveGraph
				: archive.ReturnReputation.BeforeArchiveGraph;
			Assert.AreEqual(HistoricalAuthorityHash, persisted);
			Assert.AreEqual(persisted, Basis18(archive),
				"The persisted hash must re-prove against its own basis version 18. This is one " +
				"hash-basis seam over the actual beta artifact; it does not execute the outer " +
				"engine callback, does not waive its other guards and does not solve migration.");
		}

		[Test]
		public void ExplicitBasis18SurvivesCurrentResave()
		{
			Frozen frozen = ReadFrozen();
			KingdomSettlement migrated = Decode(frozen.Payload);
			KingdomRealmArchive archive = Archive(migrated);
			archive.ReturnReputation.BeforeArchiveGraph = frozen.Before;
			string before = Basis18(archive);
			Assert.AreEqual(HistoricalAuthorityHash, before);
			Assert.IsTrue(Codec.TryEncode(migrated, out byte[] current, out string failure), failure);
			Assert.AreEqual(19, Codec.CurrentVersion);
			Assert.AreEqual(Codec.CurrentVersion, BitConverter.ToInt32(current, 4));
			archive.Seat = Decode(current);
			Assert.AreEqual(before, Basis18(archive),
				"Storage moved to the current schema; the hash basis stays at 18.");
		}

		[TestCase(ChangedTick.Closed)]
		[TestCase(ChangedTick.Subsidence)]
		public void ChangedTickBreaksEquality(ChangedTick field)
		{
			Frozen frozen = ReadFrozen();
			KingdomRealmArchive archive = Archive(Decode(frozen.Payload));
			archive.ReturnReputation.BeforeArchiveGraph = frozen.Before;
			Assert.IsTrue(Codec.TryEncode(archive.Seat, out byte[] current, out string failure), failure);
			archive.Seat = Decode(current);
			string resaved = Basis18(archive);
			if (field == ChangedTick.Closed) archive.ClosedTick++;
			else archive.Seat.LastSubsidenceTick++;
			string changed = Basis18(archive);
			Assert.IsFalse(string.Equals(resaved, changed, StringComparison.Ordinal));
			Assert.IsFalse(string.Equals(HistoricalAuthorityHash, changed, StringComparison.Ordinal),
				"An explicit basis must not launder a changed authority into the historical hash.");
			if (field == ChangedTick.Closed) archive.ClosedTick--;
			else archive.Seat.LastSubsidenceTick--;
			Assert.AreEqual(resaved, Basis18(archive));
		}

		[Test]
		public void UnownedReceiptRefuses()
		{
			KingdomRealmArchive archive = Archive(Decode(ReadFrozen().Payload));
			Assert.IsFalse(archive.TryAuthorityHash(new KingdomRealmCallbackReceipt(),
				KingdomRealmCallbackScope.Reputation, 18,
				out string hash, out string failure));
			Assert.IsNull(hash);
			StringAssert.Contains("not owned", failure);
		}

		[TestCase(0)]
		[TestCase(20)]
		[TestCase(int.MinValue)]
		[TestCase(int.MaxValue)]
		public void InvalidSchemaRefusesEvenEmptyTopology(int schema)
		{
			// No artifact and no seat: the schema is admitted before any seat, topology or
			// seceded work, so an empty topology still refuses. Only the outputs are asserted;
			// no counting hasher or encoder is injected to observe the work that did not run.
			Assert.AreEqual(1, Codec.LegacyVersion);
			Assert.AreEqual(19, Codec.CurrentVersion);
			KingdomRealmArchive archive = Archive(null);
			Assert.AreEqual(0, archive.SettlementTopology.Count);
			Assert.IsFalse(archive.TryAuthorityHash(archive.ReturnReputation,
				KingdomRealmCallbackScope.Reputation, schema, out string hash, out string failure));
			Assert.IsNull(hash);
			// UnacceptedSettlementSchemaFailure is private to KingdomRealmArchive, so the literal
			// is duplicated deliberately. Exact equality separates the archive-level refusal from
			// the codec's own "Archived settlement projection schema version is not accepted.",
			// which is what a removed archive-level gate would surface instead.
			Assert.AreEqual("archived settlement schema version is not accepted", failure);
		}

		[Test]
		public void DefaultOverloadEqualsExplicitCurrent()
		{
			Frozen frozen = ReadFrozen();
			KingdomRealmArchive archive = Archive(Decode(frozen.Payload));
			archive.ReturnReputation.BeforeArchiveGraph = frozen.Before;
			Assert.IsTrue(archive.TryAuthorityHash(archive.ReturnReputation,
				KingdomRealmCallbackScope.Reputation, out string wrapper, out string wrapperFailure),
				wrapperFailure);
			Assert.IsTrue(archive.TryAuthorityHash(archive.ReturnReputation,
				KingdomRealmCallbackScope.Reputation, Codec.CurrentVersion,
				out string explicitCurrent, out string failure), failure);
			Assert.IsTrue(CanonicalHash(wrapper));
			Assert.AreEqual(wrapper, explicitCurrent);
			// Documented, not solved: the default overload cuts at today's schema, so on this
			// artifact it is NOT the historical basis. The migration remains open.
			Assert.AreNotEqual(HistoricalAuthorityHash, explicitCurrent,
				"The current-schema cut of a v18 artifact is not its historical hash.");
		}

		[Test]
		public void SelectorFindsUnique18()
		{
			Frozen frozen = ReadFrozen();
			KingdomRealmArchive archive = Archive(Decode(frozen.Payload));
			archive.ReturnReputation.BeforeArchiveGraph = frozen.Before;
			RecordingBasisHasher unresolved = new RecordingBasisHasher(archive);
			Assert.AreEqual(Outcome.Selected, Basis.Select(HistoricalAuthorityHash,
				Basis.Unresolved, unresolved.Compute, out int searched, out string searchReason),
				searchReason);
			Assert.AreEqual(18, searched);
			CollectionAssert.AreEqual(Basis.SearchOrder(), unresolved.Order());
			RecordingBasisHasher pinned = new RecordingBasisHasher(archive);
			Assert.AreEqual(Outcome.Selected, Basis.Select(HistoricalAuthorityHash,
				18, pinned.Compute, out int fixed18, out string pinnedReason),
				pinnedReason);
			Assert.AreEqual(18, fixed18);
			CollectionAssert.AreEqual(new[] { 18 }, pinned.Order());
			// A basis fixed at the current schema must never rescue itself by widening.
			RecordingBasisHasher current = new RecordingBasisHasher(archive);
			Assert.AreEqual(Outcome.PinnedMismatch, Basis.Select(HistoricalAuthorityHash,
				Codec.CurrentVersion, current.Compute, out int rescued, out string rescueReason));
			Assert.AreEqual(Basis.Unresolved, rescued);
			Assert.IsNotNull(rescueReason);
			CollectionAssert.AreEqual(new[] { Codec.CurrentVersion }, current.Order());
		}

		/// <summary>Bounded outer-envelope reader, structurally the SI524N consumer's
		/// (KingdomArchiveVersionAuthorityTests.cs:159-188). The only change: the artifact
		/// SHA256 is the pinned constant above instead of an environment variable, so a
		/// substituted or truncated artifact fails the fixture rather than skipping it.</summary>
		private static Frozen ReadFrozen()
		{
			string path = FixturePath();
			byte[] bytes;
			using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				Assert.That(file.Length, Is.InRange(1L, 3L * 1024 * 1024));
				bytes = new byte[(int)file.Length];
				int at = 0, read;
				while (at < bytes.Length && (read = file.Read(bytes, at, bytes.Length - at)) > 0) at += read;
				Assert.AreEqual(bytes.Length, at); Assert.AreEqual(-1, file.ReadByte());
			}
			using (SHA256 sha = SHA256.Create())
				Assert.AreEqual(FixtureSha256,
					BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(),
					"Artifact SHA256 does not match the pinned actual-beta base-v18.bin.");
			using (MemoryStream stream = new MemoryStream(bytes, false))
			using (BinaryReader reader = new BinaryReader(stream, Utf8))
			{
				Assert.AreEqual(Magic, reader.ReadInt32()); Assert.AreEqual(BaseCommit, reader.ReadString());
				int count = reader.ReadInt32(); Assert.That(count, Is.InRange(8, 2 * 1024 * 1024));
				Frozen frozen = new Frozen { Payload = reader.ReadBytes(count),
					Before = reader.ReadString(), After = reader.ReadString(), Changed = reader.ReadString() };
				Assert.AreEqual(count, frozen.Payload.Length); Assert.AreEqual(stream.Length, stream.Position);
				Assert.AreEqual(18, BitConverter.ToInt32(frozen.Payload, 4));
				Assert.IsTrue(CanonicalHash(frozen.Before) && CanonicalHash(frozen.After) && CanonicalHash(frozen.Changed));
				Assert.AreEqual(frozen.Before, frozen.After);
				Assert.AreEqual(HistoricalAuthorityHash, frozen.Before);
				return frozen;
			}
		}

		private static KingdomSettlement Decode(byte[] payload)
		{
			Assert.AreEqual(19, Codec.CurrentVersion);
			Assert.IsTrue(Codec.TryDecode(payload, out KingdomSettlement result,
				out int future, out string failure), failure);
			Assert.AreEqual(0, future); Assert.IsNotNull(result); return result;
		}

		/// <summary>Same archive shape as the SI524N consumer (:198-213). SettlementTopology is
		/// left at its default empty instance, which is what the invalid-schema cases rely on.</summary>
		private static KingdomRealmArchive Archive(KingdomSettlement seat)
		{
			return new KingdomRealmArchive {
				Phase = KingdomRealmArchivePhase.Restored, Seat = seat,
				RequiresDirectionalStandingMigration = false,
				RealmId = "component-fixture-realm", FactionName = "component-fixture-faction",
				DisplayName = "Component fixture", ClosedTick = 100L,
				CarryBook = new KingdomCarryBook(),
				SettlementIds = new List<string>(), Bindings = new KingdomBindingRegistry(),
				Jobs = new KingdomJobRegistry(), Standings = new Dictionary<string, int>(),
				RealmPolicyToward = new Dictionary<string, int>(),
				RegardSpilloverRemainders = new Dictionary<string, int>(),
				RegardSpilloverObservedReputation = new Dictionary<string, int>(),
				ChronicleEntries = new List<string>(), OutsiderEntries = new List<string>()
			};
		}

		private static string Basis18(KingdomRealmArchive archive)
		{
			Assert.IsTrue(archive.TryAuthorityHash(archive.ReturnReputation,
				KingdomRealmCallbackScope.Reputation, 18,
				out string hash, out string failure), failure);
			Assert.IsTrue(CanonicalHash(hash)); return hash;
		}

		private static bool CanonicalHash(string value)
		{
			if (value == null || value.Length != 64) return false;
			foreach (char c in value) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
			return true;
		}

		private static string FixturePath()
		{
			string path = Environment.GetEnvironmentVariable("TAF_ARCHIVE_V18_FIXTURE") ??
				Path.Combine(TestMain.RepositoryRoot, "DevTests", "Fixtures", "ArchiveAuthority", "base-v18.bin");
			Assert.IsFalse(string.IsNullOrEmpty(path)); Assert.IsTrue(Path.IsPathRooted(path));
			Assert.AreEqual(Path.GetFullPath(path), path); return path;
		}
	}
}
#endif
