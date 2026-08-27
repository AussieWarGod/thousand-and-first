#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomAnnexeRuntimeSourceTests
	{
		[Test]
		public void AnnexeKeeperIsOneRealLodgedPsyberneticistNotRosterRowZero()
		{
			string annexe = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomAnnexe.cs"));
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
			string annexe = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomAnnexe.cs"));
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
	}
}
#endif
