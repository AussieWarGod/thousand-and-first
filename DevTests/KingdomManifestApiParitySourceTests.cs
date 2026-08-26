#if TAF_TESTS
using System;
using System.IO;
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
		public void SerializedFieldRemainsObsoleteProjectionAndRefreshesBeforeEverySave()
		{
			string source = Source(Path.Combine("Core", "KingdomSystem.cs"));
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
			string source = Source(Path.Combine("Core", "KingdomSystem.cs"));
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
			string source = Source(Path.Combine("Trade", "KingdomTrade.cs"));
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
			string source = Source(Path.Combine("Trade", "KingdomTrade.cs"));
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
			string source = Source(Path.Combine("Trade", "KingdomTrade.cs"));
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
			string source = Source(Path.Combine("Trade", "KingdomTrade.cs"));
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
