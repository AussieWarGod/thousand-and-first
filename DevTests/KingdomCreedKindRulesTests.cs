#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomCreedKindRulesTests
	{
		private static KingdomCreedDefinition Parse(string name, string kind,
			string theology = null)
		{
			Assert.IsTrue(KingdomCreedKindRules.TryParse(new KingdomCreedDraft
			{
				Name = name, Kind = kind, Theology = theology
			}, out KingdomCreedDefinition parsed, out string error), error);
			return parsed;
		}

		[Test]
		public void PublicKindsAndDtosHaveStableExactShape()
		{
			Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(KingdomCreedKind)));
			CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5 },
				Enum.GetValues(typeof(KingdomCreedKind)).Cast<KingdomCreedKind>()
					.Select(x => (byte)x).ToArray());
			CollectionAssert.AreEqual(new string[] { "Name", "Kind", "Theology" },
				typeof(KingdomCreedDraft).GetFields().Select(x => x.Name).ToArray());
			CollectionAssert.AreEqual(new string[] { "Name", "Kind", "Theological" },
				typeof(KingdomCreedDefinition).GetFields().Select(x => x.Name).ToArray());
		}

		[TestCase("community", KingdomCreedKind.Community)]
		[TestCase("people", KingdomCreedKind.People)]
		[TestCase("polity", KingdomCreedKind.Polity)]
		[TestCase("order", KingdomCreedKind.Order)]
		[TestCase("doctrine", KingdomCreedKind.Doctrine)]
		[TestCase("cult", KingdomCreedKind.Cult)]
		public void ExactSixKindsParseCaseInsensitively(string token, KingdomCreedKind expected)
		{
			Assert.AreEqual(expected, Parse("Example", token.ToUpperInvariant()).Kind);
		}

		[Test]
		public void TheologyBoundaryIsFailClosedAndUnknownIsNeutral()
		{
			List<KingdomCreedDefinition> definitions = new List<KingdomCreedDefinition>
			{
				Parse("Village", "community"), Parse("Guild", "order"),
				Parse("Church", "order", "yes"), Parse("Way", "doctrine"),
				Parse("Lamb", "cult")
			};
			Assert.IsFalse(KingdomCreedKindRules.UsesTheology(definitions, "Village"));
			Assert.IsFalse(KingdomCreedKindRules.UsesTheology(definitions, "Guild"));
			Assert.IsTrue(KingdomCreedKindRules.UsesTheology(definitions, "Church"));
			Assert.IsTrue(KingdomCreedKindRules.UsesTheology(definitions, "Way"));
			Assert.IsTrue(KingdomCreedKindRules.UsesTheology(definitions, "Lamb"));
			Assert.IsFalse(KingdomCreedKindRules.UsesTheology(definitions, "ThirdParty"));
			Assert.IsFalse(KingdomCreedKindRules.TryFind(definitions, "ThirdParty", out _));

			foreach (string kind in new string[] { "community", "people", "polity" })
				Assert.IsFalse(KingdomCreedKindRules.TryParse(new KingdomCreedDraft
				{
					Name = "Bad", Kind = kind, Theology = "yes"
				}, out _, out _), kind);
			Assert.IsFalse(KingdomCreedKindRules.TryParse(new KingdomCreedDraft
			{
				Name = "Bad", Kind = "doctrine", Theology = "no"
			}, out _, out _));
		}

		[Test]
		public void LayeredMergeInheritsClearsAndRejectsSemanticReclassification()
		{
			KingdomCreedDraft first = new KingdomCreedDraft
			{
				Name = "Order", Kind = "order", Theology = "yes"
			};
			Assert.IsTrue(KingdomCreedKindRules.TryMerge(first,
				new KingdomCreedDraft { Name = "Order" }, out KingdomCreedDraft inherited,
				out string error), error);
			Assert.AreEqual("yes", inherited.Theology);
			Assert.IsTrue(KingdomCreedKindRules.TryMerge(inherited,
				new KingdomCreedDraft { Name = "Order", Theology = "" },
				out KingdomCreedDraft cleared, out error), error);
			Assert.IsFalse(Parse(cleared.Name, cleared.Kind, cleared.Theology).Theological);
			Assert.IsFalse(KingdomCreedKindRules.TryMerge(first,
				new KingdomCreedDraft { Name = "Order", Kind = "cult" }, out _, out error));
			StringAssert.Contains("cannot clear or change", error);
			Assert.IsFalse(KingdomCreedKindRules.TryMerge(first,
				new KingdomCreedDraft { Name = "Other" }, out _, out error));
			StringAssert.Contains("cannot merge", error);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase(" ")]
		[TestCase("cult,cult")]
		[TestCase("religion")]
		public void MissingMalformedAndDuplicateKindTokensAreRejected(string kind)
		{
			Assert.IsFalse(KingdomCreedKindRules.TryParse(new KingdomCreedDraft
			{
				Name = "Bad", Kind = kind
			}, out _, out string error));
			StringAssert.Contains("bad Kind", error);
		}

		[Test]
		public void ShippedRegistryMapsExactCensusAndOnlyFourTheologies()
		{
			XDocument document = XDocument.Parse(
				TestMain.ReadRepositoryText("KingdomCreeds.xml"));
			XElement[] rows = document.Root.Elements("creed").ToArray();
			Assert.AreEqual(33, rows.Length);
			foreach (XElement row in rows)
				Assert.IsTrue(KingdomCreedKindRules.TryParse(new KingdomCreedDraft
				{
					Name = (string)row.Attribute("Name"), Kind = (string)row.Attribute("Kind"),
					Theology = (string)row.Attribute("Theology")
				}, out _, out string error), error);
			Assert.AreEqual(33, rows.Select(x => (string)x.Attribute("Name"))
				.Distinct(StringComparer.OrdinalIgnoreCase).Count());
			var counts = rows.GroupBy(x => (string)x.Attribute("Kind"))
				.ToDictionary(x => x.Key, x => x.Count());
			Assert.AreEqual(4, counts["community"]);
			Assert.AreEqual(16, counts["people"]);
			Assert.AreEqual(2, counts["polity"]);
			Assert.AreEqual(7, counts["order"]);
			Assert.AreEqual(2, counts["doctrine"]);
			Assert.AreEqual(2, counts["cult"]);
			string[] theological = rows.Where(x =>
				(string)x.Attribute("Kind") == "doctrine"
				|| (string)x.Attribute("Kind") == "cult"
				|| (string)x.Attribute("Theology") == "yes")
				.Select(x => (string)x.Attribute("Name")).OrderBy(x => x).ToArray();
			CollectionAssert.AreEqual(new string[]
			{
				"Mamon", "Mechanimists", "Resheph", "Seekers"
			}, theological);
			CollectionAssert.IsSubsetOf(new string[]
			{
				"Baetyls", "Girsh", "Gyre Wights", "Naphtaali", "Robots", "Snapjaws"
			}, rows.Where(x => (string)x.Attribute("Kind") == "people")
				.Select(x => (string)x.Attribute("Name")).ToArray());
			KingdomCreedDefinition gyre = Parse("Gyre Wights",
				(string)rows.Single(x => (string)x.Attribute("Name") == "Gyre Wights")
					.Attribute("Kind"));
			Assert.AreEqual(KingdomCreedKind.People, gyre.Kind);
			Assert.IsFalse(gyre.Theological,
				"the people who worship Girsh cannot themselves become shrine theology");
		}

		[Test]
		public void FoundingHandbookIsSituatedContestedAndMechanicallyUseful()
		{
			XDocument document = XDocument.Parse(TestMain.ReadRepositoryText("Books.xml"));
			XElement book = document.Root.Elements("book").Single(x =>
				(string)x.Attribute("ID") == "r_OnTheFoundingOfPlaces");
			string prose = book.Value;
			StringAssert.Contains("Set down by Neseva Cask-Hand", prose);
			StringAssert.Contains("Open Basin fellowship", prose);
			StringAssert.Contains("Uru Ux of 1000 AR", prose);
			StringAssert.Contains("Other places began otherwise", prose);
			StringAssert.Contains("A different hand", prose);
			StringAssert.Contains("A custom is not a", prose);
			StringAssert.Contains("In the settlements whose accounts I keep", prose);
			foreach (string instruction in new string[]
			{
				"founder's basin", "requires the basin", "eight drams", "First, beds", "Second, water held",
				"Third, hands", "tribute", "raid", "Write down"
			}) StringAssert.Contains(instruction, prose, instruction);
			string lower = prose.ToLowerInvariant();
			foreach (string rejected in new string[]
			{
				"every place", "all places", "no place ever", "every settlement",
				"all settlements", "no settlement ever",
				"there is no law out here", "The world will notice. It always does",
				"the writing is the only part that will still be there"
			}) StringAssert.DoesNotContain(rejected.ToLowerInvariant(), lower, rejected);
		}

		[Test]
		public void NeutralAdoptionProseNeverClaimsBeliefConversionOrConsecration()
		{
			string prose = KingdomCreedKindRules.AdoptionTelling("Yara", "Joppa") + " "
				+ KingdomCreedKindRules.AdoptionRumour("Yara", "Joppa") + " "
				+ KingdomCreedKindRules.AdoptionNote("Yara", "Joppa");
			StringAssert.Contains("adopt", prose);
			StringAssert.Contains("affiliation", prose);
			StringAssert.DoesNotContain("belie", prose.ToLowerInvariant());
			StringAssert.DoesNotContain("convert", prose.ToLowerInvariant());
			StringAssert.DoesNotContain("consecr", prose.ToLowerInvariant());
		}
	}
}
#endif
