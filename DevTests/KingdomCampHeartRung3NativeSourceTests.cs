#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The rung 2 -> 3 leg of the sealed camp-heart run (issue #159). Two kinds of case live here
	/// and they are not equals: VALUE tests, which execute production's own rules and the authored
	/// catalogue arithmetic the fixture is sized from, and WIRING pins, which only prove the seam
	/// still asks for what it claims to ask for. Neither is native evidence: the rung itself is
	/// proved only by the persona run, and that run is owed.
	/// </summary>
	public class KingdomCampHeartRung3NativeSourceTests
	{
		private const string Provider = "Harness/KingdomCampHeartNativeProvider.cs";
		private const string Checks = "Harness/KingdomCampHeartNativeChecks.cs";
		private const string Fixture = "Harness/KingdomCampHeartNativeFixture.cs";
		private const string Phases = "Harness/KingdomCampHeartNativeChecksPhases.cs";
		private const string Rung3 = "Harness/KingdomCampHeartNativeRung3.cs";
		private const string Persona = "Tools/personas/camp-heart-rung3-native-check.persona";
		private const string Buildings = "RuntimeData/KingdomBuildings.xml";
		private const string Blueprints = "RuntimeData/ObjectBlueprints.xml";

		private static string Read(string Path)
		{
			return TestMain.ReadRepositoryText(Path);
		}

		/// <summary>The value of one attribute of one authored building, read out of the catalogue
		/// rather than restated here.</summary>
		private static string Authored(string Key, string Attribute)
		{
			string xml = Read(Buildings);
			int at = xml.IndexOf("Key=\"" + Key + "\"", StringComparison.Ordinal);
			Assert.That(at, Is.GreaterThan(-1), Key + " is not an authored building");
			int end = xml.IndexOf("</building>", at, StringComparison.Ordinal);
			int selfClosing = xml.IndexOf("/>", at, StringComparison.Ordinal);
			if (end < 0 || (selfClosing > -1 && selfClosing < end)) end = selfClosing;
			Assert.That(end, Is.GreaterThan(at), Key + " has no closing tag");
			string element = xml.Substring(at, end - at);
			int name = element.IndexOf(Attribute + "=\"", StringComparison.Ordinal);
			Assert.That(name, Is.GreaterThan(-1), Key + " has no " + Attribute);
			int open = element.IndexOf('"', name) + 1;
			int close = element.IndexOf('"', open);
			return element.Substring(open, close - open);
		}

		/// <summary>A <c>kind:units</c> bill as a dictionary, in the catalogue's own spelling.</summary>
		private static Dictionary<string, int> Bill(string Authored)
		{
			Dictionary<string, int> bill = new Dictionary<string, int>();
			foreach (string term in Authored.Split(','))
			{
				string[] parts = term.Split(':');
				Assert.That(parts.Length, Is.EqualTo(2), "malformed bill term '" + term + "'");
				bill[parts[0].Trim()] = int.Parse(parts[1].Trim());
			}
			return bill;
		}

		/// <summary>An <c>internal const int</c> the harness declares, read off its own source so
		/// the arithmetic below is checked against what the fixture will really mint.</summary>
		private static int Constant(string Source, string Name)
		{
			string text = Read(Source);
			int at = text.IndexOf("internal const int " + Name + " = ", StringComparison.Ordinal);
			Assert.That(at, Is.GreaterThan(-1), Name + " is not declared in " + Source);
			int open = text.IndexOf("= ", at, StringComparison.Ordinal) + 2;
			int close = text.IndexOf(';', open);
			return int.Parse(text.Substring(open, close - open).Trim());
		}

		/// <summary>
		/// VALUE. The rung-3 bill the fixture mints is the authored rung 2 -> 3 bill, term for
		/// term, and the store can hold it: the units left standing after the rung-2 bill is spent,
		/// plus the whole rung-3 bill, are exactly the stockpile's declared capacity. A bill that
		/// grew by one unit, or a capacity that shrank, fails here rather than in a native run.
		/// </summary>
		[Test]
		public void TheMintedRungThreeBillIsTheAuthoredBillAndFitsTheDeclaredCapacity()
		{
			Dictionary<string, int> authored = Bill(Authored("heartwaterstone", "UpgradeMaterials"));
			Assert.That(authored.Count, Is.EqualTo(3));
			Assert.That(authored["timber"], Is.EqualTo(Constant(Checks, "Rung3TimberUnits")));
			Assert.That(authored["stone"], Is.EqualTo(Constant(Checks, "Rung3StoneUnits")));
			Assert.That(authored["shapedtimber"],
				Is.EqualTo(Constant(Checks, "Rung3ShapedTimberUnits")));

			int rung3 = authored["timber"] + authored["stone"] + authored["shapedtimber"];
			Assert.That(rung3, Is.EqualTo(25));
			int brush = Constant(Checks, "MintedBrushUnits");
			string blueprints = Read(Blueprints);
			int tag = blueprints.IndexOf("<tag Name=\"r_KingdomStockpileCapacity\" Value=\"",
				StringComparison.Ordinal);
			Assert.That(tag, Is.GreaterThan(-1));
			int open = blueprints.IndexOf("Value=\"", tag, StringComparison.Ordinal) + 7;
			int capacity = int.Parse(blueprints.Substring(open,
				blueprints.IndexOf('"', open) - open));
			// The rung-2 bill has left the store by the time the rung-3 bill is minted, so what
			// stands afterwards is the unasked units plus the new bill, and that must be exactly
			// the declared capacity: one unit more would refuse, one fewer would leave it unproved.
			Assert.That(brush + rung3, Is.EqualTo(capacity));

			// And the rung-2 fill itself still fills the same capacity.
			Dictionary<string, int> rung2 = Bill(Authored("heartbasin", "UpgradeMaterials"));
			Assert.That(rung2["stone"], Is.EqualTo(Constant(Checks, "MintedStoneUnits")));
			Assert.That(rung2["timber"], Is.EqualTo(Constant(Checks, "MintedTimberUnits")));
			Assert.That(rung2["stone"] + rung2["timber"] + brush, Is.EqualTo(capacity));
		}

		/// <summary>
		/// VALUE. The phase-3 predicate, asked of production's own rules: rung three is worth 160
		/// drams, the moot yard asks for a Town and the workshop craft level, and the fixture's
		/// own population, dedicated capacity and taught-design count are exactly what those two
		/// gates need. A threshold that moves fails here.
		/// </summary>
		[Test]
		public void ThePhaseThreePredicateMatchesProductionsOwnRungGates()
		{
			Assert.That(KingdomPlotRules.HeartBasinCapacityForRung(3), Is.EqualTo(160));
			Assert.That(KingdomPlotRules.HeartBasinCapacityForRung(2), Is.EqualTo(48));
			Assert.That(Read(Rung3), Does.Contain("BasinCapacity(standing) == \"160\""));

			Assert.That(Authored("heartmoot", "MinStage"), Is.EqualTo("Town"));
			Assert.That(Authored("heartmoot", "MinTech"), Is.EqualTo("workshop"));
			Assert.That(Authored("heartmoot", "Footprint"), Is.EqualTo("12x10"));
			Assert.That(Authored("heartwaterstone", "Footprint"), Is.EqualTo("8x6"));
			Assert.That(Read(Rung3), Does.Contain("width == 12 && height == 10"));

			int residents = Constant(Checks, "TownResidentCount");
			int drams = Constant(Checks, "TownDedicatedDrams");
			Assert.That(KingdomRules.StageFor(residents, drams), Is.EqualTo(GrowthStage.Town));
			// And the fixture is not merely over the line by accident: one person fewer, or a
			// dedication under the authored capacity gate, is not a Town.
			Assert.That(KingdomRules.StageFor(residents - 1, drams),
				Is.Not.EqualTo(GrowthStage.Town));
			Assert.That(KingdomRules.StageFor(residents, 255), Is.Not.EqualTo(GrowthStage.Town));

			int disks = Constant(Checks, "WorkshopDisks");
			Assert.That(KingdomZoningRules.LevelForPoints(
				disks * KingdomZoningRules.TechPointsPerDisk),
				Is.EqualTo(TechLevel.Workshop));
			Assert.That(KingdomZoningRules.LevelForPoints(
				(disks - 1) * KingdomZoningRules.TechPointsPerDisk),
				Is.LessThan(TechLevel.Workshop));
		}

		/// <summary>
		/// The persona and the provider name the SAME sealed script, word for word. The provider
		/// refuses any other script outright, so a persona that drifted from it could only ever
		/// fail in a native run; this compares the two texts instead.
		/// </summary>
		[Test]
		public void ThePersonaScriptIsExactlyTheProvidersSealedRungThreeScript()
		{
			string persona = Read(Persona);
			int at = persona.IndexOf("\nSCRIPT=", StringComparison.Ordinal);
			Assert.That(at, Is.GreaterThan(-1), "the persona declares no script");
			int end = persona.IndexOf('\n', at + 1);
			string[] words = persona.Substring(at + 8, end - at - 8).Split(';');

			string provider = Read(Provider);
			int array = provider.IndexOf("private static readonly string[] Rung3Script = {",
				StringComparison.Ordinal);
			Assert.That(array, Is.GreaterThan(-1), "the provider seals no rung-3 script");
			int open = provider.IndexOf('{', array) + 1;
			int close = provider.IndexOf("};", open, StringComparison.Ordinal);
			string[] sealedWords = provider.Substring(open, close - open).Split(',');
			Assert.That(sealedWords.Length, Is.EqualTo(words.Length),
				"the persona and the provider seal different script lengths");
			for (int i = 0; i < words.Length; i++)
			{
				string word = sealedWords[i].Trim().Replace("\n", "").Replace("\t", "").Trim();
				if (word == "SetupVerb") word = "\"camp-heart-setup\"";
				if (word == "CheckVerb") word = "\"camp-heart-check\"";
				Assert.That(word, Is.EqualTo("\"" + words[i].Trim() + "\""),
					"script word " + i + " differs between persona and provider");
			}
			// The rung-2 script is untouched, and the provider still accepts exactly two.
			Assert.That(provider, Does.Contain("private static readonly string[] Rung2Script = {"));
			Assert.That(provider, Does.Contain("if (SameScript(script, Rung2Script)) return 2;"));
			Assert.That(provider, Does.Contain("if (SameScript(script, Rung3Script)) return 3;"));
			Assert.That(provider, Does.Contain(
				"Require(false, \"the sealed camp heart script differs\");"));
		}

		/// <summary>
		/// WIRING. The seam still drives nothing: the rung-3 phase reads, and the rung-3 bill is
		/// minted through the same disclosed path as every other fixture unit. Every synthetic
		/// input this leg adds is disclosed in the report line, and the open CASE 3 question is
		/// carried in the persona header and in the shard that exists to answer it.
		/// </summary>
		[Test]
		public void TheRungThreeLegDrivesNothingAndDisclosesEverythingItAdds()
		{
			string rung3 = Read(Rung3);
			foreach (string driver in new[] { "KingdomUpgrade.Begin(", "KingdomPlots.Advance(",
				"TryApplyUpgrade(", "SetIntProperty(", "SetStringProperty(", "HeartRung =" })
				Assert.That(rung3, Does.Not.Contain(driver),
					"the rung-3 phase must read, never drive: " + driver);
			foreach (string read in new[] { "KingdomPlots.HeartRung(Zone) == 3",
				"KingdomUpgrade.IsFunctionallyBuilt(standing)",
				"KingdomConstructionPhase.Complete",
				"KingdomArchitectureStamper.UpgradeFaultProperty" })
				Assert.That(rung3, Does.Contain(read));
			Assert.That(rung3, Does.Contain("TrySettleImprovementHeartRung"));
			Assert.That(rung3, Does.Contain("settlement helper TWICE"));
			Assert.That(rung3, Does.Contain("SECOND consecutive climb"));

			string fixture = Read(Fixture);
			Assert.That(fixture, Does.Contain("Mint(KingdomMaterial.ShapedTimber, "
				+ "Rung3ShapedTimberUnits, MintedRung3);"));
			Assert.That(fixture, Does.Contain("KingdomZoning.Learn(System, \"disk\","));
			Assert.That(fixture, Does.Contain("Require(after >= TechLevel.Workshop,"));
			Assert.That(Read(Phases), Does.Contain("if (TargetRung >= 3) MintRung3Bill();"));

			string checks = Read(Checks);
			Assert.That(checks, Does.Contain("\"; target-rung=\" + Retained.TargetRung"));
			Assert.That(checks, Does.Contain("\"; synthetic-rung3-bill=\""));
			Assert.That(checks, Does.Contain("\"; synthetic-craft-disks=\""));

			string persona = Read(Persona);
			Assert.That(persona, Does.Contain("camp-builder CASE 3"));
			Assert.That(persona, Does.Contain("founded, not commissioned"));
			Assert.That(persona, Does.Contain("TWICE IN A ROW"));
			Assert.That(persona, Does.Contain("That is a reading of the source, not a result."));
			Assert.That(persona, Does.Contain("TIMEOUT=3600"));
			Assert.That(persona, Does.Contain("SET=camp,native-regression,test-only"));
		}
	}
}
#endif
