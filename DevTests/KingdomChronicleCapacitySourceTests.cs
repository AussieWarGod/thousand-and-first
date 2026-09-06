#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source-boundary assertions only. These do not execute engine observation or saving.</summary>
	[TestFixture]
	public sealed class KingdomChronicleCapacitySourceTests
	{
		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static void Ordered(string source, params string[] tokens)
		{
			int start = 0;
			foreach (string token in tokens)
			{
				int found = source.IndexOf(token, start, StringComparison.Ordinal);
				Assert.GreaterOrEqual(found, start, "Missing or out-of-order source boundary: " + token);
				start = found + token.Length;
			}
		}

		[Test]
		public void NativeObservationUsesAllFivePresenceTablesAndRetainsExactTableReferences()
		{
			string source = Read("Chronicle/KingdomChronicle.Capacity.cs");
			foreach (string kind in new[] { "String", "Int", "Int64", "Object", "Boolean" })
			{
				StringAssert.Contains("game.Has" + kind + "GameState(EventRegistryState)", source);
				StringAssert.Contains("game." + kind + "GameState", source);
			}
			Ordered(source, "private static bool ReadCapacityTables", "foreach (object table in tables) if (table == null)",
				"row.HasString =", "if (row.HasString) row.String = game.GetStringGameState(EventRegistryState)",
				"ReferenceEquals(tables[0], game.StringGameState)", "ReferenceEquals(tables[4], game.BooleanGameState)");
			StringAssert.Contains("ReferenceEquals(tables[i], value.Tables[i])", source);
			StringAssert.DoesNotContain("SetStringGameState(", source);
			StringAssert.DoesNotContain(".Remove(", source);
			StringAssert.DoesNotContain(".Clear(", source);
			StringAssert.DoesNotContain("ReportFault(", source);
		}

		[Test]
		public void CapacityRequiresCurrentRegisteredOwnerAndExactCanonicalFullAbsentEventProof()
		{
			string native = Read("Chronicle/KingdomChronicle.Capacity.cs");
			Ordered(native, "internal static bool TryObserveCapacityRefusalAt", "PublicationAllowed(ownerExact)",
				"ReferenceEquals(game.GetSystem<KingdomSystem>(), system)", "system.TryGetCurrentIdentity(",
				"KingdomChronicleCapacityRules.TryObserve(", "ReproveCapacityRefusal(value, ownerExact)");
			Ordered(native, "internal static bool ReproveCapacityRefusal", "PublicationAllowed(ownerExact)",
				"ReferenceEquals(The.Game, value.Game)", "realm != value.Realm || settlement != value.Settlement",
				"KingdomScenarioStateShape.TryAuthorityText(", "string.Equals(raw, value.Raw, StringComparison.Ordinal)");
			string pure = Read("Chronicle/KingdomChronicleCapacityRules.cs");
			Ordered(pure, "KingdomScenarioStateShape.TryAuthorityText(", "!present", "TryParseRegistry(raw,",
				"|| migrated", "rows.Count != KingdomChronicleReceiptRules.MaxReceipts", "TryWriteRegistry(rows,",
				"string.Equals(raw, canonical, StringComparison.Ordinal)", "string.Equals(row.EventId, eventId, StringComparison.Ordinal)",
				"TryHashRegistry(raw, out string hash)");
		}

		[Test]
		public void LedgerSettlesBeforeIndependentCapacityObservationAndOrdinaryPublisherFallback()
		{
			string source = Read("Growth/KingdomSubsidenceStepRuntime.ReportDelivery.cs");
			Ordered(source, "TryProveLedger(", "!save(proved)", "string eventId =",
				"if (!exact()) return false", "KingdomChronicle.TryObserveCapacityRefusalAt(",
				"KingdomSubsidenceReportRules.TryPublishCapacity(", "ReproveCapacityRefusal(capacity, exact)",
				"report = refused;", "continue;", "KingdomChronicle.RecordOnceAt(",
				"KingdomChronicle.TryProveOnceAt(", "KingdomChronicle.TryProveLostOnceAt(");
			StringAssert.Contains("entry.ChronicleProved || entry.ChronicleLost || entry.CapacityRefused", source);
			StringAssert.DoesNotContain("!KingdomChronicle.RecordOnceAt(", source);
		}

		[Test]
		public void ProductionPublicationSeamReprovesWitnessAroundExactParentSave()
		{
			string source = Read("Growth/KingdomSubsidenceReportRules.Capacity.cs");
			Ordered(source, "internal static bool TryPublishCapacity", "reprove == null || save == null",
				"!TryRefuseCapacity(plan, index, witness", "if (!reprove() || !save(value) || !reprove()) return false",
				"next = value; return true;");
			StringAssert.Contains("KingdomChronicleCapacityRules.Valid(witness, EventId(plan, index), fingerprint)", source);
			StringAssert.Contains("entry.CapacityHash != witness.RegistryHash", source);
			StringAssert.Contains("!entry.ChronicleLost && !entry.ChronicleProved", source);
			string rules = Read("Growth/KingdomSubsidenceReportRules.cs");
			StringAssert.Contains("!ValidEntry(entry) || !ValidCapacity(plan, i, entry)", rules);
			StringAssert.Contains("LedgerDelivered(entry.LedgerPhase) && entry.ChronicleProved", rules);
		}

		[Test]
		public void DatedCapacityFingerprintUsesExistingOwnerTextAndTickDomain()
		{
			string existing = Read("Chronicle/KingdomChronicle.At.cs");
			string capacity = Read("Chronicle/KingdomChronicleCapacityRules.cs");
			StringAssert.Contains("TryCanonicalHash(\"taf-chronicle-at-v1\"", existing);
			StringAssert.Contains("new[] { realm, settlement, EventId, Text, AtTick.ToString(CultureInfo.InvariantCulture) }", existing);
			StringAssert.Contains("TryCanonicalHash(\"taf-chronicle-at-v1\"", capacity);
			StringAssert.Contains("new[] { realm, settlement, eventId, text, tick.ToString(CultureInfo.InvariantCulture) }", capacity);
			StringAssert.Contains("taf-chronicle-capacity-registry-v1", capacity);
			StringAssert.Contains("writer.Write(raw)", capacity);
		}

		[Test]
		public void CurrentReportWriterIsVersionThreeWhileOldWireReencodesItsOwnVersion()
		{
			string source = Read("Growth/KingdomSubsidenceReportCodec.cs");
			StringAssert.Contains("return Encode(plan, VersionThree, out wire)", source);
			StringAssert.Contains("Encode(value, version, out canonical)", source);
			StringAssert.Contains("string.Equals(canonical, wire, StringComparison.Ordinal)", source);
			foreach (string token in new[] { "0x31545253", "0x32545253", "0x33545253", "PrefixV1", "PrefixV2", "PrefixV3" })
				StringAssert.Contains(token, source);
			StringAssert.Contains("if (entry.CapacityRefused) return false", source);
			Ordered(source, "writer.Write((byte)entry.ChronicleRefusal)", "writer.Write(entry.CapacityCount)",
				"writer.Write(entry.CapacityHash)", "writer.Write(entry.CapacityFingerprint)");
		}

		[Test]
		public void RetirementRetainsRefusalAndHomecomingAcknowledgesOnlyAfterGuardedDisplay()
		{
			string archive = Read("Growth/KingdomSubsidenceReportArchive.cs");
			StringAssert.Contains("MaxReports = 8", archive);
			StringAssert.Contains("rows.Count > MaxReports", archive);
			StringAssert.Contains("Chronicle not published: registry full", archive);
			StringAssert.Contains("delivery is not claimed", archive);
			StringAssert.DoesNotContain("RemoveAt(", archive);
			string step = Read("Growth/KingdomSubsidenceStepRules.Departures.cs");
			Ordered(step, "internal static bool TryRetire(", "KingdomSubsidenceReportArchive.TryRetain(",
				"next = new KingdomSubsidenceStepBook(", "retained.FailureModel");
			string batch = Read("Growth/KingdomSubsidenceStepRuntime.Reports.cs");
			Ordered(batch, "KingdomSubsidenceReportArchive.TryRetain(", "retained.WithBatch(KingdomSubsidenceBatchRules.None)");
			StringAssert.Contains("KingdomSubsidenceRungRules.ReleasedComplete(rung)", batch);
			string home = Read("Growth/KingdomSubsidenceStepRuntime.Homecoming.cs");
			Ordered(home, "KingdomSubsidenceReportArchive.Digest(", "show(digest)", "if (!HomecomingExact(frame)",
				"next.WithFailures(KingdomSubsidenceReportArchive.None)", "SaveOption(owner, next)",
				"if (!HomecomingExact(frame)) return false", "ledger.Reset()");
		}
	}
}
#endif
