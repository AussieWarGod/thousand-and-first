#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomAnnexeRuntimeSourceTests
	{
		[Test]
		public void AnnexeKeeperIsOneRealLodgedPsyberneticistNotRosterRowZero()
		{
			string annexe = KingdomAnnexeLogicalSource.Read();
			string purpose = KingdomPurposeLogicalSource.Read();
			StringAssert.DoesNotContain("Realm.RosterNames[0]", annexe);
			StringAssert.Contains("KingdomPurpose.IsLodgedSpecialist(zone, candidate,", annexe);
			StringAssert.Contains("Psyberneticist: true", annexe);
			StringAssert.Contains("KingdomSurvey.Take(zone, Realm)", annexe);
			StringAssert.Contains("Staffed: !string.IsNullOrEmpty(KeeperAt(Realm, Building))", annexe);
			StringAssert.Contains("KingdomCrews.CapabilityOf(Resident).Intelligence >= 18", purpose);
			StringAssert.Contains("KingdomLodging.HomeDesignKeyOf(Z, Resident)", purpose);
			StringAssert.Contains("Resident.GetIntProperty(\"KingdomCitizen\") == 1", purpose);
			StringAssert.Contains("PsyberneticistTruth(Resident)", purpose);
		}

		[Test]
		public void AnnexePersistsPlainNamesAndEscapesOnlyAtRichTextSinks()
		{
			string annexe = KingdomAnnexeLogicalSource.Read();
			StringAssert.Contains("Who.BaseDisplayNameStripped ?? \"\"", annexe);
			StringAssert.Contains("record.Named = named;", annexe);
			StringAssert.Contains("record.City = city;", annexe);
			StringAssert.Contains("string shownName = KingdomPresentation.Rich(named);", annexe);
			StringAssert.Contains("string shownCity = KingdomPresentation.Rich(city);", annexe);
			StringAssert.Contains("KingdomPresentation.Rich(names[i])", annexe);
			StringAssert.DoesNotContain("record.Named = shownName", annexe);
			StringAssert.DoesNotContain("record.City = shownCity", annexe);
			StringAssert.DoesNotContain("Who.DisplayNameOnly", annexe);
		}

		[Test]
		public void EnrolledDeployedWireWasNamedAndPurposeAuthorityIsAdditive()
		{
			string source = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomAnnexe.cs"));
			string fixture = TestMain.ReadRepositoryText(Path.Combine("DevTests",
				"Compatibility", "KingdomEnrolledNamedWireV1.fixture"))
				.Replace("\r\n", "\n");
			Assert.AreEqual("2aa28675990e80727bac1eb7fe365cd2682c13ea860e9cd2cf7311a3fb1bdfb8",
				Sha256(fixture));
			string[] rows = fixture.Split(new[] { '\n' },
				StringSplitOptions.RemoveEmptyEntries);
			CollectionAssert.AreEqual(new[]
			{
				"base\t1dca10b8b8f58171e03517811d0d3ce0c7ea5277",
				"type\tr_KingdomEnrolled",
				"wire\tWriteNamedFields/ReadNamedFields",
				"field\tstring\tWho\tgene-old",
				"field\tstring\tNamed\tAru",
				"field\tstring\tCity\tOld Ibul",
				"field\tlong\tTick\t9876543210",
				"field\tbool\tLapseAnnounced\ttrue"
			}, rows);

			CollectionAssert.AreEqual(new[] { "Who", "Named", "City", "Tick",
				"LapseAnnounced" }, rows.Skip(3).Select(row => row.Split('\t')[2]));
			StringAssert.Contains("Writer.WriteNamedFields(this, typeof(r_KingdomEnrolled));",
				source);
			StringAssert.Contains("Reader.ReadNamedFields(this, typeof(r_KingdomEnrolled));",
				source);
			StringAssert.Contains("This part has used named fields since its first shipped version.",
				source);
			StringAssert.DoesNotContain("SerializationMagic", source);
			StringAssert.DoesNotContain("Reader.ReadObject()", source);
		}

		[Test]
		public void EnrolledV1FixtureDefaultsAuthorityAndCurrentWireKeepsExactAuthority()
		{
			string source = KingdomAnnexeLogicalSource.Read();
			Dictionary<string, string> loaded = new Dictionary<string, string>
			{
				["PurposePairId"] = "",
				["PurposePairEpoch"] = "0",
				["PurposeOperationId"] = "",
				["PurposeAuthorityId"] = ""
			};
			string fixture = TestMain.ReadRepositoryText(Path.Combine("DevTests",
				"Compatibility", "KingdomEnrolledNamedWireV1.fixture"));
			foreach (string row in fixture.Split(new[] { '\r', '\n' },
				StringSplitOptions.RemoveEmptyEntries).Where(row => row.StartsWith("field\t",
					StringComparison.Ordinal)))
			{
				string[] columns = row.Split('\t');
				loaded[columns[2]] = columns[3];
			}
			Assert.AreEqual("gene-old", loaded["Who"]);
			Assert.AreEqual("Aru", loaded["Named"]);
			Assert.AreEqual("Old Ibul", loaded["City"]);
			Assert.AreEqual("9876543210", loaded["Tick"]);
			Assert.AreEqual("true", loaded["LapseAnnounced"]);
			Assert.AreEqual("", loaded["PurposePairId"]);
			Assert.AreEqual("0", loaded["PurposePairEpoch"]);
			Assert.AreEqual("", loaded["PurposeOperationId"]);
			Assert.AreEqual("", loaded["PurposeAuthorityId"]);

			foreach (string field in new[] { "PurposePairId", "PurposePairEpoch",
				"PurposeOperationId", "PurposeAuthorityId" })
			{
				StringAssert.Contains("record." + field + " = purpose.", source);
				StringAssert.Contains("record." + field + " == Authority.", source);
			}
			StringAssert.Contains("PurposePairId = PurposePairId ?? \"\";", source);
			StringAssert.Contains("PurposeOperationId = PurposeOperationId ?? \"\";", source);
			StringAssert.Contains("PurposeAuthorityId = PurposeAuthorityId ?? \"\";", source);
		}

		private static string Sha256(string Text)
		{
			using (SHA256 hash = SHA256.Create())
				return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Text)))
					.Replace("-", "").ToLowerInvariant();
		}
	}
}
#endif
