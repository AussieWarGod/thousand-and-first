#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomLabRegistrationEvidenceSourceTests
	{
		[Test]
		public void BaseCatalogueAndNativeTracePinTheSameTwelvePartGrantFamilies()
		{
			XDocument catalogue = XDocument.Parse(TestMain.ReadRepositoryText(
				"RuntimeData/KingdomProcedures.xml"));
			string[] loaded = catalogue.Descendants("procedure")
				.Where(row => string.Equals((string)row.Attribute("Source"), "part",
					StringComparison.OrdinalIgnoreCase))
				.Select(row => (string)row.Attribute("Grants"))
				.Distinct(StringComparer.Ordinal).OrderBy(value => value,
					StringComparer.Ordinal).ToArray();
			string[] expected =
			{
				"ActiveLightSource", "DrunkOnHit", "GiantHands", "LifeDrainOnHit",
				"NephalChord", "ReflectDamage", "SapChargeOnHit", "SapOnPenetration",
				"SporePuffer", "StickOnHit", "Swarmer", "TemperatureVenting"
			};
			CollectionAssert.AreEqual(expected, loaded);

			string wish = TestMain.ReadRepositoryText(
				"Debug/KingdomWishes.LabRegistrationEvidence.cs");
			MatchCollection rows = Regex.Matches(wish,
				"new LabRegistrationExpectation\\(\"([^\"]+)\", \"([^\"]+)\", (true|false)\\)");
			Assert.AreEqual(17, rows.Count, "six concrete chords expand one reviewed family");
			Dictionary<string, bool> families = new Dictionary<string, bool>(
				StringComparer.Ordinal);
			HashSet<string> chords = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < rows.Count; i++)
			{
				string family = rows[i].Groups[1].Value;
				string runtime = rows[i].Groups[2].Value;
				bool allowStatic = bool.Parse(rows[i].Groups[3].Value);
				if (families.TryGetValue(family, out bool held))
					Assert.AreEqual(held, allowStatic, family);
				else families.Add(family, allowStatic);
				if (family == "NephalChord") chords.Add(runtime);
			}
			CollectionAssert.AreEqual(expected, families.Keys.OrderBy(value => value,
				StringComparer.Ordinal).ToArray());
			Assert.AreEqual(7, families.Count(row => !row.Value));
			Assert.AreEqual(5, families.Count(row => row.Value));
			CollectionAssert.AreEquivalent(new[] { "AgolgotChord", "BethsaidaChord",
				"QasChord", "QonChord", "RermadonChord", "ShugruithChord" }, chords);
		}

		[Test]
		public void WishIsReadOnlyUnsignedAndTracesThePinnedEngineSaveBranch()
		{
			string wish = TestMain.ReadRepositoryText(
				"Debug/KingdomWishes.LabRegistrationEvidence.cs");
			StringAssert.Contains("GameObject.cs:1992-2051", wish);
			StringAssert.Contains("IPart.cs:167-170", wish);
			StringAssert.Contains("KingdomProcedures.All", wish);
			StringAssert.Contains("procedures[i].Source == LabSource.Part", wish);
			StringAssert.Contains("loaded.SetEquals(expected)", wish);
			StringAssert.Contains("Activator.CreateInstance(type) as IPart", wish);
			StringAssert.Contains("part.AllowStaticRegistration()", wish);
			StringAssert.Contains("type?.AssemblyQualifiedName", wish);
			StringAssert.Contains("save-row=", wish);
			StringAssert.Contains("No evidence receipt was written", wish);
			StringAssert.DoesNotContain("AddPart(", wish);
			StringAssert.DoesNotContain("SetStringProperty(", wish);
			StringAssert.DoesNotContain("RecordOnce(", wish);
			StringAssert.DoesNotContain("The.Player", wish);
		}
	}
}
#endif
