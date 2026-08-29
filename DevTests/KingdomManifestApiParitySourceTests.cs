#if TAF_TESTS
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomManifestApiParitySourceTests
	{
		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static string Method(string source, string signature, string nextSignature)
		{
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, signature);
			int end = source.IndexOf(nextSignature, start + signature.Length,
				StringComparison.Ordinal);
			Assert.Greater(end, start, nextSignature);
			return source.Substring(start, end - start);
		}

		[Test]
		public void LogicalSystemPreservesCompletePublicFieldShapeAndAttributeCounts()
		{
			string source = KingdomSystemLogicalSource.Read();
			MatchCollection fields = Regex.Matches(source,
				@"^\t\tpublic (?!override\b)(?!static\b)([^\n{(]*?(?: = [^;\n]+)?;)\s*$",
				RegexOptions.Multiline);
			StringBuilder shape = new StringBuilder();
			for (int i = 0; i < fields.Count; i++)
			{
				if (i > 0) shape.Append('\n');
				shape.Append("public ").Append(fields[i].Groups[1].Value.Trim());
			}
			string hash;
			using (SHA256 sha = SHA256.Create())
			{
				hash = BitConverter.ToString(sha.ComputeHash(
					Encoding.UTF8.GetBytes(shape.ToString()))).Replace("-", "").ToLowerInvariant();
			}
			Assert.AreEqual(194, fields.Count);
			Assert.AreEqual(
				"03e08192b52987061f7ed8338f4fe186afa7e210858c5e6dbec7adb395dc7e42",
				hash, "public field names, types, defaults, and declaration order are save ABI");
			Assert.AreEqual(30,
				Regex.Matches(source, "public partial class KingdomSystem").Count,
				"the reviewed logical-source shard set is part of the save-ABI pin");
			Assert.AreEqual(1, Regex.Matches(source, @"^\t\[Serializable\]$",
				RegexOptions.Multiline).Count);
			Assert.AreEqual(8, Regex.Matches(source, @"^\t\t\[NonSerialized\]$",
				RegexOptions.Multiline).Count);
			Assert.AreEqual(6, Regex.Matches(source, @"^\t\t\[Obsolete\(",
				RegexOptions.Multiline).Count);
			Assert.AreEqual(1, Regex.Matches(source,
				@"^\t\tprivate sealed class CharterAbilityObservation$",
				RegexOptions.Multiline).Count);
			Assert.AreEqual(1, Regex.Matches(source,
				@"^\t\tprivate sealed class CharterReferenceSnapshot$",
				RegexOptions.Multiline).Count);
		}

		[Test]
		public void SerializedFieldRemainsObsoleteProjectionAndRefreshesBeforeEverySave()
		{
			string source = KingdomSystemLogicalSource.Read();
			int obsolete = source.IndexOf(
				"[Obsolete(\"Use KingdomTrade.CurrentManifest(KingdomSystem).",
				StringComparison.Ordinal);
			int field = source.IndexOf("public KingdomManifest Manifest;", obsolete,
				StringComparison.Ordinal);
			Assert.Greater(obsolete, 0);
			Assert.Greater(field, obsolete);
			Assert.IsFalse(source.Contains("public KingdomManifest Manifest {"),
				"Replacing named serialized field with property breaks old save wire name.");
			StringAssert.Contains("public override bool WantFieldReflection => false;", source);
			StringAssert.Contains("public KingdomManifest LegacyManifestEvidence;", source);

			string write = Method(source, "public override void Write(SerializationWriter Writer)",
				"public override void Read(SerializationReader Reader)");
			int synchronize = write.IndexOf("SynchronizeLegacyManifestProjection();",
				StringComparison.Ordinal);
			int namedFields = write.IndexOf("Writer.WriteNamedFields(this, typeof(KingdomSystem))",
				StringComparison.Ordinal);
			Assert.Greater(synchronize, 0);
			Assert.Greater(namedFields, synchronize);
		}

		[Test]
		public void ColdLoadPreservesOnlyMismatchedOldEvidenceThenAlwaysRefreshesProjection()
		{
			string source = KingdomSystemLogicalSource.Read();
			string normalize = Method(source, "private void NormalizeTradeBook()",
				"internal void SynchronizeLegacyManifestProjection()");
			StringAssert.Contains(
				"!KingdomTrade.LegacyManifestMatches(Manifest, TradeBook.Manifest)", normalize);
			StringAssert.Contains(
				"LegacyManifestEvidence = KingdomTrade.LegacyManifestSnapshot(Manifest);", normalize);
			StringAssert.Contains("|| DealNextTicks.Count > 0 || LegacyManifestEvidence != null",
				normalize);
			Assert.IsFalse(normalize.Contains("|| DealNextTicks.Count > 0 || Manifest != null"),
				"Exact saved projection must not quarantine authoritative TradeBook on load.");
			StringAssert.Contains("finally", normalize);
			StringAssert.Contains("SynchronizeLegacyManifestProjection();", normalize);

			string projection = Method(source,
				"internal void SynchronizeLegacyManifestProjection()", "\n\t}\n}");
			StringAssert.Contains(
				"Manifest = KingdomTrade.LegacyManifestSnapshot(TradeBook?.Manifest);", projection);
		}

		[Test]
		public void LeasePublishesProjectionBeforeUnlockAndTradeHasNoDirectLegacyWrites()
		{
			string source = KingdomTradeLogicalSource.Read();
			string lease = Method(source, "public void Dispose()", "private sealed class TradeExileCoreSeal");
			int synchronize = lease.IndexOf("System?.SynchronizeLegacyManifestProjection();",
				StringComparison.Ordinal);
			int unlock = lease.IndexOf("InFlight = null;", StringComparison.Ordinal);
			Assert.Greater(synchronize, 0);
			Assert.Greater(unlock, synchronize);
			Assert.IsFalse(source.Contains("System.Manifest ="));
			Assert.IsFalse(source.Contains("system.Manifest ="));
		}

		[Test]
		public void StrikeDeliveryTurnbackAndLapsePublishAtDomainAndRetirementBoundaries()
		{
			string source = KingdomTradeLogicalSource.Read();
			string load = Method(source,
				"private static bool TryLoadManifestCore(KingdomSystem System",
				"public static KingdomManifest ExpireManifestIfStale");
			StringAssert.Contains("ContinueOperation(System, book, Z, survey, now);", load);
			StringAssert.Contains("KingdomTradeManifestState manifest = book.Manifest;", load);

			string activation = Method(source,
				"private static void OnZoneActivatedCore(KingdomSystem System",
				"public static bool TryLoadManifest(KingdomSystem System");
			StringAssert.Contains("PrepareManifestClockOperation(System, book, manifest, Z, now);",
				activation);
			StringAssert.Contains("PrepareManifestDelivery(System, book, manifest, Z, now);",
				activation);

			string expiry = Method(source,
				"private static KingdomManifest ExpireManifestIfStaleCore(KingdomSystem System",
				"public static bool TryOnExile(KingdomSystem System");
			StringAssert.Contains("PrepareManifestClockOperation(System, book, manifest, Here, Now);",
				expiry);
			StringAssert.Contains("ContinueOperation(System, book, Here", expiry);

			string domain = Method(source,
				"private static bool SettleDomain(KingdomSystem System",
				"private static bool SettleManifestCreditAccounting");
			StringAssert.Contains("case KingdomTradeOperationKind.ManifestLoad:", domain);
			StringAssert.Contains("case KingdomTradeOperationKind.ManifestDelivery:", domain);
			StringAssert.Contains("case KingdomTradeOperationKind.ManifestTurnback:", domain);
			StringAssert.Contains("case KingdomTradeOperationKind.ManifestLapse:", domain);
			int refresh = domain.LastIndexOf("RefreshBookDomain(Frame);", StringComparison.Ordinal);
			int synchronize = domain.IndexOf("System.SynchronizeLegacyManifestProjection();",
				refresh, StringComparison.Ordinal);
			Assert.Greater(refresh, 0);
			Assert.Greater(synchronize, refresh);

			string continuation = Method(source,
				"private static void ContinueOperation(KingdomSystem System",
				"private static bool SettleResources");
			int retire = continuation.IndexOf("KingdomTradeRules.Retire(Book, operation",
				StringComparison.Ordinal);
			int retirementProjection = continuation.IndexOf(
				"System.SynchronizeLegacyManifestProjection();", retire, StringComparison.Ordinal);
			Assert.Greater(retire, 0);
			Assert.Greater(retirementProjection, retire,
				"Delivery and second-window lapse clear manifest during retirement.");
		}

		[Test]
		public void QuarantinePublishesBeforeCallbacksAndAgainAfterCleanup()
		{
			string source = KingdomTradeLogicalSource.Read();
			string quarantine = Method(source,
				"private static void FinalizeQuarantine(KingdomSystem System",
				"private static void SettleOutboxAsLost");
			int refresh = quarantine.IndexOf("RefreshBookDomain(Frame);", StringComparison.Ordinal);
			int beforeCallbacks = quarantine.IndexOf(
				"System.SynchronizeLegacyManifestProjection();", refresh, StringComparison.Ordinal);
			int dispatch = quarantine.IndexOf("DispatchOutbox(System, Operation, Frame);",
				StringComparison.Ordinal);
			int retire = quarantine.IndexOf("KingdomTradeRules.Retire(Book, Operation",
				StringComparison.Ordinal);
			int afterCleanup = quarantine.IndexOf(
				"System.SynchronizeLegacyManifestProjection();", retire, StringComparison.Ordinal);
			Assert.Greater(beforeCallbacks, refresh);
			Assert.Greater(dispatch, beforeCallbacks);
			Assert.Greater(retire, dispatch);
			Assert.Greater(afterCleanup, retire);
		}

		[Test]
		public void ProjectionMapsEveryLegacyFieldFromAuthoritativeState()
		{
			string source = KingdomTradeLogicalSource.Read();
			string mapper = Method(source,
				"internal static KingdomManifest LegacyManifestSnapshot(\n\t\t\tKingdomTradeManifestState Manifest)",
				"internal static KingdomManifest LegacyManifestSnapshot(KingdomManifest Manifest)");
			StringAssert.Contains("if (Manifest == null) return null;", mapper);
			StringAssert.Contains("OriginName = Manifest.OriginName", mapper);
			StringAssert.Contains("DestinationName = Manifest.DestinationName", mapper);
			StringAssert.Contains("Drams = Manifest.EscrowDrams", mapper);
			StringAssert.Contains("LoadedTick = Manifest.LoadedTick", mapper);
			StringAssert.Contains("DeadlineTick = Manifest.DeadlineTick", mapper);
			StringAssert.Contains("TurnedBack = Manifest.TurnedBack", mapper);
		}
	}
}
#endif
