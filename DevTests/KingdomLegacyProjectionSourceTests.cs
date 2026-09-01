#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomLegacyProjectionSourceTests
	{
		private static string Read(string Relative) =>
			TestMain.ReadRepositoryText(Relative);

		private static string Logical(string DirectoryName, string Pattern)
		{
			string root = Path.Combine(TestMain.RepositoryRoot, DirectoryName);
			List<string> files = new List<string>(Directory.GetFiles(root, Pattern,
				SearchOption.TopDirectoryOnly));
			files.Sort(StringComparer.Ordinal);
			string source = "";
			for (int i = 0; i < files.Count; i++) source += File.ReadAllText(files[i]);
			return source;
		}

		private static int Count(string Source, string Needle)
		{
			int count = 0;
			for (int at = 0; (at = Source.IndexOf(Needle, at,
				StringComparison.Ordinal)) >= 0; at += Needle.Length) count++;
			return count;
		}

		[Test]
		public void ArchiveAwayRemainsWireEvidenceBehindTwoNarrowAccessors()
		{
			string core = Read("Core/KingdomRealmArchive.00Core.cs");
			string capture = Read("Core/KingdomRealmArchive.01Capture.cs");
			string validation = Read("Core/KingdomRealmArchive.03Validation.cs");
			string wire = Read("Core/KingdomRealmArchive.10WireEnvelope.cs");
			string logical = Logical("Core", "KingdomRealmArchive.*.cs");
			StringAssert.Contains("[Obsolete(\"Use SettlementTopology.\")]", core);
			Assert.AreEqual(2, Count(logical, "#pragma warning disable 618"));
			Assert.AreEqual(1, Count(logical, "return Away;"));
			Assert.AreEqual(1, Count(logical, "Away = Value;"));
			StringAssert.Contains("candidate.WriteLegacyAwayProjection(frozenTopology.Get(0))",
				capture);
			StringAssert.Contains("ReadLegacyAwayProjection(), Seceded", validation);
			StringAssert.Contains("WriteArchivedSettlement(Writer, ReadLegacyAwayProjection()",
				wire);
			StringAssert.Contains("KingdomSettlement legacyProjection = " +
				"ReadLegacyAwayProjection()", wire);
			StringAssert.Contains("TryAdoptLegacy(legacyProjection", wire);
			StringAssert.Contains("legacy archive projection differs from topology", wire);
			StringAssert.DoesNotContain("Away = frozenTopology", capture);
			StringAssert.DoesNotContain("SettlementTopology, Away, Seceded", validation);
			StringAssert.DoesNotContain("WriteArchivedSettlement(Writer, Away", wire);
		}

		[Test]
		public void SuccessionSeatBitIsReadOnlyAtExactLegacyMigrationBoundary()
		{
			string root = Read("Experience/KingdomSuccession.cs");
			string repair = Read("Experience/KingdomSuccession.BodyTransferAndRepair.cs");
			string migration = Read("Experience/KingdomSuccession.PendingSeal.cs");
			string validation = Read("Experience/KingdomSuccession.SaveValidation.cs");
			string removal = Read("Experience/KingdomSuccession.RemovalAuthority.cs");
			StringAssert.Contains("[Obsolete(\"Legacy save migration only;", root);
			Assert.AreEqual(2, Count(root, "#pragma warning disable 618"));
			Assert.AreEqual(1, Count(root, "return PendingAccessionRepairSeated;"));
			Assert.AreEqual(1, Count(root, "PendingAccessionRepairSeated = false;"));
			StringAssert.Contains("TryMigrateLegacyAccessionRepairSettlement", repair);
			StringAssert.Contains("System?.NonSeatSettlementCount == 1", repair);
			StringAssert.Contains("PendingAccessionRepairSettlementId = settlementId", repair);
			StringAssert.Contains("TryMigrateLegacyAccessionRepairSettlement(", migration);
			StringAssert.Contains("ReadLegacyAccessionRepairSeated()", validation);
			StringAssert.Contains("ReadLegacyAccessionRepairSeated()", removal);
			StringAssert.DoesNotContain("PendingAccessionRepairSeated ?", repair);

			string directory = Path.Combine(TestMain.RepositoryRoot, "Experience");
			foreach (string file in Directory.GetFiles(directory, "KingdomSuccession*.cs"))
			{
				if (Path.GetFileName(file) == "KingdomSuccession.cs") continue;
				Assert.IsFalse(Regex.IsMatch(File.ReadAllText(file),
					@"(?<![A-Za-z0-9_])PendingAccessionRepairSeated(?![A-Za-z0-9_])"),
					Path.GetFileName(file));
			}
		}

		[Test]
		public void DebugWishesUseOwnedTopologyInsteadOfCompatibilityColumns()
		{
			string wishes = Logical("Debug", "KingdomWishes*.cs");
			StringAssert.DoesNotContain(".Away", wishes);
			StringAssert.DoesNotContain("ExiledAway", wishes);
			StringAssert.Contains("NonSeatSettlements()", wishes);
			StringAssert.Contains("TryReplaceNonSeatSettlement", wishes);
			StringAssert.Contains("NonSeatClaimsZone", wishes);
			StringAssert.Contains("OwnedZone(zoneID)", wishes);
			StringAssert.Contains("ExiledSettlementTopology?.Snapshot()", wishes);
		}
	}
}
#endif
