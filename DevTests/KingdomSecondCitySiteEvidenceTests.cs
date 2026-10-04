#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Behavioural coverage row 12: the second-city-site row must keep every rejected candidate
	/// and its reason, whether a site qualified or the search refused, within the journal's row
	/// cap. Real execution of the engine-free row builders in KingdomSecondCitySiteRules, plus
	/// source pins tying the live search and its journal call to them. Not native acceptance.
	/// </summary>
	public class KingdomSecondCitySiteEvidenceTests
	{
		private const string Home = "JoppaWorld.8.22.1.1.10";
		private const string Border = "JoppaWorld.8.22.2.1.10";
		private const string Site =
			"site=JoppaWorld.8.20.1.1.10 rite=40,12 heart=38,11-43,14 centred=true probes=3 rejected=2";
		private const string Foreign = "JoppaWorld.6.20.1.1.10";
		private const string Unseatable = "JoppaWorld.7.20.1.1.10";
		private const string UnseatableReason = "no seatable rite: 1596 cells, 12 occupied, 0 wet, "
			+ "1584 refused by the heart preflight, last: no authored founding-heart pose binds its "
			+ "basin to the poured rite";
		private const string Persona = "Tools/personas/second-city-native-check.persona";
		private const string SiteSource = "Harness/KingdomSecondCityNativeSite.cs";
		private const string ChecksSource = "Harness/KingdomSecondCityNativeChecks.cs";

		private static List<string> Rejections()
		{
			return new List<string> {
				KingdomSecondCitySiteRules.Rejection(Foreign, "foreign"),
				KingdomSecondCitySiteRules.Rejection(Unseatable, UnseatableReason) };
		}

		[Test]
		public void ASuccessfulSiteRowNamesEveryRejectedCandidateWithItsReason()
		{
			string row = KingdomSecondCitySiteRules.SiteRow(Home, 40, 12, Border, Site,
				KingdomSecondCitySiteRules.RejectedList(Rejections()));
			Assert.That(row, Is.EqualTo("home=" + Home + " home-rite=40,12; border=" + Border
				+ " border-verdict=GroundIsTooClose; " + Site
				+ " adjacent=false claimed=false verdict=Allowed"
				+ "; synthetic-travel=true synthetic-water=false-spent force=false"
				+ "; rejected-candidates=" + Foreign + " (foreign) | " + Unseatable + " ("
				+ UnseatableReason + ")"));
		}

		[Test]
		public void AFirstQualifiedCandidateRecordsThatNothingWasRejected()
		{
			Assert.That(KingdomSecondCitySiteRules.RejectedList(new List<string>()), Is.EqualTo("none"));
			Assert.That(KingdomSecondCitySiteRules.RejectedList(null), Is.EqualTo("none"));
			Assert.That(KingdomSecondCitySiteRules.SiteRow(Home, 40, 12, Border, Site,
				KingdomSecondCitySiteRules.RejectedList(null)), Does.EndWith("; rejected-candidates=none"));
		}

		[Test]
		public void ARefusedSiteRowNamesEveryCandidateAndClaimsNoChosenSite()
		{
			string refusal = KingdomSecondCitySiteRules.Refusal(8, 54);
			string row = KingdomSecondCitySiteRules.RefusedSiteRow(Home, 40, 12, Border, refusal,
				KingdomSecondCitySiteRules.RejectedList(Rejections()));
			Assert.That(row, Is.EqualTo("home=" + Home + " home-rite=40,12; border=" + Border
				+ " border-verdict=GroundIsTooClose; refused=" + refusal
				+ "; rejected-candidates=" + Foreign + " (foreign) | " + Unseatable + " ("
				+ UnseatableReason + ")"));
			Assert.That(row, Does.Not.Contain("site="));
			Assert.That(row, Does.Not.Contain("verdict=Allowed"));
		}

		[Test]
		public void TheRefusalNamesTheProbeLimitAndSurvivesTheVerbRowFailureBound()
		{
			string refusal = KingdomSecondCitySiteRules.Refusal(8, 72);
			Assert.That(refusal, Is.EqualTo("no eligible second-city site: probed 8 of at most 8 "
				+ "parasangs (72 candidates in rings 2..4); every rejected candidate and its reason "
				+ "is in the second-city-site row"));
			// KingdomSecondCityNativeChecks.Fail bounds the exception's type name plus its message.
			string failure = "InvalidOperationException: " + refusal;
			Assert.That(KingdomScenarioRules.Bounded(failure), Is.EqualTo(failure));
		}

		[Test]
		public void AReasonWithinTheCapIsKeptVerbatimAndALongerOneNamesItsCut()
		{
			Assert.That(KingdomSecondCitySiteRules.Rejection(Foreign, "foreign"),
				Is.EqualTo(Foreign + " (foreign)"));
			string full = new string('r', KingdomSecondCitySiteRules.ReasonChars);
			Assert.That(KingdomSecondCitySiteRules.Rejection("z", full), Is.EqualTo("z (" + full + ")"));
			string cut = KingdomSecondCitySiteRules.Rejection("z", full + "s");
			Assert.That(cut.Length, Is.EqualTo("z ()".Length + KingdomSecondCitySiteRules.ReasonChars));
			Assert.That(cut, Does.StartWith("z (rrr"));
			Assert.That(cut, Does.Contain(KingdomScenarioJournalRules.TruncatedOpen));
			Assert.That(cut, Does.EndWith(KingdomScenarioJournalRules.TruncatedClose + ")"));
		}

		[Test]
		public void TheWidestRowKeepsEveryCandidateUnderTheJournalCap()
		{
			// Two-digit parasang coordinates on every ring and no ring clipped by the map edge:
			// all 72 candidates of rings 2..4, each at its widest id.
			string home = "JoppaWorld.40.14.1.1.10";
			IList<string> candidates = KingdomSecondCitySiteRules.Candidates(home);
			Assert.That(candidates.Count, Is.EqualTo(72));
			string huge = new string('s', 10 * KingdomScenarioJournalRules.MaxMessageChars);
			List<string> tried = new List<string>();
			for (int i = 0; i < candidates.Count; i++)
				tried.Add(KingdomSecondCitySiteRules.Rejection(candidates[i],
					i < KingdomSecondCitySiteRules.MaxProbes ? huge : "adjacent"));
			string rejected = KingdomSecondCitySiteRules.RejectedList(tried);
			string site = "site=" + candidates[candidates.Count - 1]
				+ " rite=77,22 heart=72,19-77,22 centred=false probes=8 rejected=72";
			string[] rows = {
				KingdomSecondCitySiteRules.SiteRow(home, 79, 24, "JoppaWorld.40.14.2.1.10", site,
					rejected),
				KingdomSecondCitySiteRules.RefusedSiteRow(home, 79, 24, "JoppaWorld.40.14.2.1.10",
					KingdomSecondCitySiteRules.Refusal(8, 72), rejected) };
			foreach (string row in rows)
			{
				Assert.That(row.Length, Is.LessThanOrEqualTo(KingdomScenarioJournalRules.MaxMessageChars));
				Assert.That(KingdomScenarioJournalRules.Bound(row,
					KingdomScenarioJournalRules.MaxMessageChars), Is.EqualTo(row));
				foreach (string id in candidates) Assert.That(row, Does.Contain(id + " ("));
				Assert.That(Count(row, KingdomScenarioJournalRules.TruncatedOpen),
					Is.EqualTo(KingdomSecondCitySiteRules.MaxProbes));
			}
		}

		[Test]
		public void ThePersonasSiteBindingIsMetOnSuccessAndNeverByARefusal()
		{
			string wanted = PersonaBinding("second-city-site:OK~");
			Assert.That(wanted, Is.Not.Empty);
			string rejected = KingdomSecondCitySiteRules.RejectedList(Rejections());
			Assert.That(KingdomSecondCitySiteRules.SiteRow(Home, 40, 12, Border, Site, rejected),
				Does.Contain(wanted));
			Assert.That(KingdomSecondCitySiteRules.RefusedSiteRow(Home, 40, 12, Border,
				KingdomSecondCitySiteRules.Refusal(8, 54), rejected), Does.Not.Contain(wanted));
		}

		[Test]
		public void TheLiveSearchJournalsEveryRejectionOnBothPathsBeforeRefusing()
		{
			string site = TestMain.ReadRepositoryText(SiteSource);
			Assert.That(site, Does.Contain(
				"Tried.Add(KingdomSecondCitySiteRules.Rejection(ZoneId, Reason));"));
			Assert.That(site, Does.Contain(
				"KingdomLog.Log(\"native-second-city rejected candidate \" + ZoneId + \": \" + Reason);"));
			// Every rejection goes through Reject, and nothing cuts the list to a roster width.
			Assert.That(site, Does.Not.Contain("tried.Add("));
			Assert.That(site, Does.Not.Contain("KingdomScenarioRules.Bounded("));
			Assert.That(Count(site, "Rejected = KingdomSecondCitySiteRules.RejectedList(tried);"),
				Is.EqualTo(2));
			string checks = TestMain.ReadRepositoryText(ChecksSource);
			Assert.That(Squash(checks), Does.Contain(Squash("string row = found"
				+ " ? KingdomSecondCitySiteRules.SiteRow(HomeZoneId, HomeCell.X, HomeCell.Y, border,"
				+ " site, rejected) : KingdomSecondCitySiteRules.RefusedSiteRow(HomeZoneId,"
				+ " HomeCell.X, HomeCell.Y, border, refusal, rejected);")));
			int journal = checks.IndexOf(
				"KingdomScenarioJournal.Append(KingdomSecondCityScript.SiteRow, found, row);");
			Assert.That(journal, Is.GreaterThan(0));
			Assert.That(checks.IndexOf("Require(found, refusal", journal), Is.GreaterThan(journal));
		}

		private static string PersonaBinding(string Prefix)
		{
			foreach (string line in TestMain.ReadRepositoryText(Persona).Split('\n'))
			{
				string trimmed = line.TrimEnd('\r');
				if (!trimmed.StartsWith("EXPECT=")) continue;
				foreach (string item in trimmed.Substring("EXPECT=".Length).Split(','))
					if (item.StartsWith(Prefix)) return item.Substring(Prefix.Length);
			}
			Assert.Fail("the second-city persona binds no " + Prefix + " expectation");
			return null;
		}

		private static int Count(string Text, string Part)
		{
			int count = 0;
			for (int at = Text.IndexOf(Part); at >= 0; at = Text.IndexOf(Part, at + Part.Length))
				count++;
			return count;
		}

		private static string Squash(string Source)
		{
			System.Text.StringBuilder kept = new System.Text.StringBuilder(Source.Length);
			foreach (char c in Source)
				if (c != ' ' && c != '\t' && c != '\r' && c != '\n') kept.Append(c);
			return kept.ToString();
		}
	}
}
#endif
