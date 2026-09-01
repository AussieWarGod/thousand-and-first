#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomDirectionalStandingMigrationSourceTests
	{
		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		[Test]
		public void PreAlphaGeometryBreakClosesTheOldDirectionalMigrationLane()
		{
			string system = Source(Path.Combine("Core", "KingdomSystem.cs"));
			string serialization = Source(Path.Combine("Core",
				"KingdomSystem.z19a.Serialization.cs"));
			string normalize = Source(Path.Combine("Core",
				"KingdomSystem.z24a.DirectionalStandingNormalization.cs"));
			string callback = Source(Path.Combine("Core",
				"KingdomSystem.z19.PersistenceAndCallbacks.cs"));
			StringAssert.Contains("private const int CurrentSerializationVersion = 10", system);
			StringAssert.Contains("private const int FirstNamedSerializationVersion = 10", system);
			AssertOrdered(serialization, "LoadedSerializationVersion = version;",
				"Reader.ReadNamedFields(this, typeof(KingdomSystem));",
				"NormalizeState(AllowLegacyIdentityMigration: false);");
			StringAssert.Contains("LoadedSerializationVersion != 8", normalize);
			AssertOrdered(callback, "NormalizeState(AllowLegacyIdentityMigration: false);",
				"MigrateDirectionalStandingStateAfterLoad();",
				"ValidateDirectionalFactionRegistryAfterLoad();");
		}

		[Test]
		public void MigrationReadsOnlyExplicitOwnedOutboundEdgesWithoutSymmetry()
		{
			string migration = Source(Path.Combine("Core",
				"KingdomSystem.z24b.DirectionalStandingMigration.cs"));
			StringAssert.Contains("DirectionalStandingSchemaVersion != 0", migration);
			StringAssert.Contains("RealmPolicyToward.Count != 0", migration);
			StringAssert.Contains("RegardSpilloverRemainders.Count != 0", migration);
			StringAssert.Contains("RegardSpilloverObservedReputation.Count != 0", migration);
			StringAssert.Contains("KingdomFoundingTransaction.FactionRegistryCoherent", migration);
			StringAssert.Contains("realm.FactionFeeling.TryGetValue(row.Key, out int feeling)",
				migration);
			StringAssert.Contains("KingdomStandingRules.TryLegacyFeelingPolicy", migration);
			StringAssert.Contains("row.Key != \"Player\" && !regard.ContainsKey(row.Key)",
				migration);
			StringAssert.DoesNotContain("desired.Add(row.Key, row.Value)", migration);
			StringAssert.DoesNotContain("policy[row.Key] = row.Value", migration);
		}

		[Test]
		public void OlderArchiveWaitsForMigrationThenGainsDigestAndExactMirrors()
		{
			string wire = Source(Path.Combine("Core", "KingdomRealmArchive.10WireEnvelope.cs"));
			string collections = Source(Path.Combine("Core",
				"KingdomSystem.z24.Normalization.Collections.cs"));
			string migration = Source(Path.Combine("Core",
				"KingdomSystem.z24b.DirectionalStandingMigration.cs"));
			string hash = Source(Path.Combine("Core", "KingdomRealmArchive.02AuthorityHash.cs"));
			string validation = Source(Path.Combine("Core", "KingdomRealmArchive.03Validation.cs"));
			AssertOrdered(wire,
				"DirectionalStandingSchemaVersion = 0;",
				"CallbackAuthoritySchemaVersion = 1;",
				"RequiresDirectionalStandingMigration = true;");
			StringAssert.Contains("!ExiledRealmArchive.RequiresDirectionalStandingMigration",
				collections);
			AssertOrdered(migration,
				"KingdomRealmArchive.TryDirectionalStandingDigest",
				"archive.RealmPolicyToward = policy;",
				"archive.DirectionalStandingSchemaVersion = 1;",
				"archive.DirectionalStandingDigest = digest;",
				"TryEnsureExileMirrors(archive",
				"archive.Validate(out failure)",
				"archive.RequiresDirectionalStandingMigration = false;");
			StringAssert.Contains("!cleaning && !ExactExileMirrors(archive)", migration);
			StringAssert.Contains("if (CallbackAuthoritySchemaVersion >= 2)", hash);
			StringAssert.Contains("DirectionalStandingDigestMatches", validation);
		}

		[Test]
		public void MigrationStartsCarryAndObservationEmptyWithoutPersonalInference()
		{
			string migration = Source(Path.Combine("Core",
				"KingdomSystem.z24b.DirectionalStandingMigration.cs"));
			Assert.GreaterOrEqual(Count(migration,
				"RegardSpilloverRemainders.Count != 0"), 2);
			Assert.GreaterOrEqual(Count(migration,
				"RegardSpilloverObservedReputation.Count != 0"), 2);
			StringAssert.Contains("archive.RegardSpilloverRemainders,", migration);
			StringAssert.Contains("archive.RegardSpilloverObservedReputation,", migration);
			StringAssert.Contains("AllowDirectionalMissing: true", migration);
			StringAssert.DoesNotContain("PlayerReputation", migration);
			StringAssert.DoesNotContain("RegardSpilloverObservedReputation[row.Key]", migration);
		}

		[Test]
		public void ArchiveTransitionMatrixCoversEveryReachablePersistedCut()
		{
			string migration = Source(Path.Combine("Core",
				"KingdomSystem.z24b.DirectionalStandingMigration.cs"));
			string matrix = Slice(migration,
				"private static bool ArchiveTransitionPairAdmitted",
				"private bool MigrationFactionObserved");
			string compact = Compact(matrix);
			string a = "archive==KingdomRealmArchivePhase.";
			string t = "transition==KingdomPolityRealmTransitionPhase.";
			StringAssert.Contains("if(" + a + "TradeClosed||" + a + "MirrorsPublished||" +
				a + "ChronicleFrozen||" + a + "ChronicleCleared)return" + t + "None;", compact);
			StringAssert.Contains("if(" + a + "Resetting)return" + t + "None||" + t +
				"Prepared||" + t + "Tombstoned||" + t + "Detached;", compact);
			StringAssert.Contains("if(" + a + "Closed)return" + t + "Detached||" + t +
				"Rebound;", compact);
			StringAssert.Contains("if(" + a + "Restoring)return" + t + "Detached||" + t +
				"Restored;", compact);
			StringAssert.Contains("if(" + a + "Restored)return" + t + "Restored;", compact);
			StringAssert.Contains("return" + a + "ReturnCleaning&&(" + t + "Restored||" + t +
				"None);", compact);
			StringAssert.DoesNotContain("KingdomRealmArchivePhase.Prepared", matrix);
		}

		[Test]
		public void SourceSelectionNeverTreatsDetachedBlankLedgerAsOldAuthority()
		{
			string migration = Source(Path.Combine("Core",
				"KingdomSystem.z24b.DirectionalStandingMigration.cs"));
			StringAssert.Contains("KingdomPolityRules.TryObserveCurrentFoundation(PolityLedger",
				migration);
			StringAssert.Contains("ArchiveTransitionPairAdmitted(archive.Phase, phase)", migration);
			StringAssert.Contains("TransitionMatchesArchive(transition, archive)", migration);
			StringAssert.Contains("KingdomPolityRules.TryValidateRealmTransition", migration);
			StringAssert.Contains("KingdomPolityRules.TryTransitionLedger(transition, out source)",
				migration);
			StringAssert.Contains("ReferenceEquals(archive.Standings, Standings)", migration);
			StringAssert.Contains("KingdomRealmArchive.ExactDictionary(archive.Standings, Standings)",
				migration);
			StringAssert.Contains("refounding destroyed the old source polity envelope", migration);
			StringAssert.DoesNotContain("source = new KingdomPolityLedger", migration);
		}

		[Test]
		public void MirrorRecoveryAndReturnCleanupUseDifferentExactProofs()
		{
			string migration = Source(Path.Combine("Core",
				"KingdomSystem.z24b.DirectionalStandingMigration.cs"));
			AssertOrdered(migration,
				"bool cleaning = archive.Phase == KingdomRealmArchivePhase.ReturnCleaning;",
				"AllowCanonicalMissing: archive.Phase == KingdomRealmArchivePhase.TradeClosed",
				"AllowDirectionalMissing: true",
				"cleaning && !archive.CurrentGraphMatches(this, out failure)",
				"!cleaning && !ExactExileMirrors(archive)",
				"archive.RequiresDirectionalStandingMigration = false;");
			StringAssert.DoesNotContain("(cleaning && !ExactExileMirrors", migration);
		}

		[Test]
		public void OldFactionProofAdmitsOnlyExactTransitionPresentations()
		{
			string migration = Source(Path.Combine("Core",
				"KingdomSystem.z24b.DirectionalStandingMigration.cs"));
			string faction = Source(Path.Combine("Polity",
				"KingdomPolityFactionRuntime.RealmTransition.cs"));
			StringAssert.Contains("realm.GetIntProperty(\"TAFFoundingPending\") != 0", migration);
			StringAssert.Contains("!realm.Visible || realm.GetIntProperty(\"PlayerKingdom\") != 1",
				migration);
			StringAssert.Contains("realm.WaterRitualLiquid != \"water\"", migration);
			StringAssert.Contains(
				"archivePhase == KingdomRealmArchivePhase.Restoring, out failure", migration);
			StringAssert.Contains("F.GetIntProperty(\"TAFFoundingPending\") != 0", faction);
			StringAssert.Contains("bool marked = CurrentMarked(F, T);", faction);
			StringAssert.Contains("bool active = marked && F.Visible", faction);
			StringAssert.Contains("bool hidden = marked && !F.Visible", faction);
			StringAssert.Contains(
				"T.Phase == KingdomPolityRealmTransitionPhase.Prepared) && active", faction);
			StringAssert.Contains("AllowDetachedActive && (active || hidden)", faction);
			StringAssert.Contains(
				"FailObservation(\"old current faction transition presentation differs\"", faction);
		}

		private static void AssertOrdered(string source, params string[] needles)
		{
			int at = -1;
			for (int i = 0; i < needles.Length; i++)
			{
				int next = source.IndexOf(needles[i], at + 1, StringComparison.Ordinal);
				Assert.Greater(next, at, "missing/out-of-order: " + needles[i]);
				at = next;
			}
		}

		private static int Count(string source, string needle)
		{
			int count = 0;
			for (int at = 0; (at = source.IndexOf(needle, at,
				StringComparison.Ordinal)) >= 0; at += needle.Length) count++;
			return count;
		}

		private static string Slice(string source, string startNeedle, string endNeedle)
		{
			int start = source.IndexOf(startNeedle, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, "missing start: " + startNeedle);
			int end = source.IndexOf(endNeedle, start + startNeedle.Length,
				StringComparison.Ordinal);
			Assert.Greater(end, start, "missing end: " + endNeedle);
			return source.Substring(start, end - start);
		}

		private static string Compact(string source)
		{
			return source.Replace(" ", "").Replace("\t", "").Replace("\r", "")
				.Replace("\n", "");
		}
	}
}
#endif
