#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// #264 review, licensed: the arcology's bit mint resolved offline against the INSTALLED Base
	/// corpus, read in the engine's factory order (<see cref="KingdomQudBlueprintCorpus"/>), by
	/// the same engine-free rules the native chain runs, with production's own material and
	/// exotic vocabulary read from its source. Worth here is each blueprint's DECLARED tiers,
	/// exactly what the chain's setup preflight resolves by; at supply the engine's realised cost
	/// can rarely lower one tier (BitType.ToRealBits), which is why each tier must keep a margin.
	/// One test, so hosted CI (no installed Qud) names one exact licensed skip.
	/// </summary>
	public class KingdomCampHeartHighCraftCorpusTests
	{
		private const int Margin = 3;

		[Test]
		public void TheInstalledCorpusSuppliesEveryArcologyBitTierAsStorableSalvage()
		{
			var corpus = KingdomQudBlueprintCorpus.Load(LocateBase());
			var vocabulary = ProductionVocabulary();
			var factory = corpus.Entries.Cast<KingdomCampHeartHighCraftRules.IBlueprint>().ToList();
			Assert.That(corpus.Files.First(), Is.EqualTo("ObjectBlueprints.xml"));
			Assert.That(factory.Count, Is.GreaterThan(KingdomCampHeartHighCraftRules.ScanCap),
				"mods sort after every Base file, so the capped walk reads Base alone");
			ReadsWhatTheLoaderDecides(corpus, vocabulary);
			ReproducesTheReviewedDefect(corpus, vocabulary);
			ResolvesEveryTierWithAMargin(factory, vocabulary);
			CoversTheBillWithoutASkip(corpus, factory, vocabulary);
		}

		/// <summary>Facts the engine's loader decides: a stag files as "Semantic" + name through
		/// inheritance, a "*noinherit" tag stays on its own blueprint, a declared
		/// CanDisassemble="false" survives into the baked part, and inherited parts arrive.</summary>
		private static void ReadsWhatTheLoaderDecides(KingdomQudBlueprintCorpus Corpus,
			KingdomCampHeartHighCraftRules.Vocabulary Vocabulary)
		{
			foreach (var known in new Dictionary<string, string> {
				{ "Biomech Corpse", "material" }, { "Scrap Metal", "material" }, { "Item", "abstract" },
				{ "Electric Generator", "no-disassembly" }, { "HE Missile", "always-stack" },
				{ "Clockthing_QGirl", "not-item" }, { "Flamethrower", "vessel" } })
			{
				var entry = Corpus.Find(known.Key);
				Assert.That(entry, Is.Not.Null, known.Key);
				Assert.That(KingdomCampHeartHighCraftRules.Refusal(entry, Vocabulary), Is.EqualTo(known.Value), known.Key);
			}
			Assert.That(Corpus.Find("Biomech Corpse").Marked("SemanticScrap"), Is.True, "stag Scrap through Scrap 00");
			Assert.That(Corpus.Find("Maghammer").Marked("BaseObject"), Is.False, "Item's own BaseObject is *noinherit");
		}

		/// <summary>The reviewed defect on the installed corpus: 2130d6a4's filter (not a base
		/// blueprint, a TinkerItem that may be disassembled and declares Bits, every entry counted
		/// against the cap) took the natural Point-Defense Laser for tier zero, the socketed
		/// Maghammer for tier three and the Reclamation Cist for tier four, which inherits
		/// MountedFurniture's Takeable="false", so Inventory.AddObject refused it. Today's rules
		/// refuse all three by name.</summary>
		private static void ReproducesTheReviewedDefect(KingdomQudBlueprintCorpus Corpus,
			KingdomCampHeartHighCraftRules.Vocabulary Vocabulary)
		{
			var scanned = Corpus.Entries.Take(KingdomCampHeartHighCraftRules.ScanCap).Where(e => e != null
				&& !e.Marked("BaseObject") && e.HasPart("TinkerItem") && e.Flag("TinkerItem", "CanDisassemble") != false
				&& !string.IsNullOrEmpty(e.Text("TinkerItem", "Bits"))).ToList();
			Func<int, string> old = tier => scanned.FirstOrDefault(e =>
				KingdomCampHeartHighCraftRules.DeclaredWorth(e).Get(tier) > 0)?.Key;
			Assert.That(old(0), Is.EqualTo("Point-Defense Laser"));
			Assert.That(old(3), Is.EqualTo("Maghammer"));
			Assert.That(old(4), Is.EqualTo("Reclamation Cist"));
			var cist = Corpus.Find("Reclamation Cist");
			Assert.That(cist.DescendsFrom("MountedFurniture"), Is.True);
			Assert.That(cist.Flag("Physics", "Takeable"), Is.False, "inherited from MountedFurniture");
			Assert.That(KingdomCampHeartHighCraftRules.Refusal(cist, Vocabulary), Is.EqualTo("untakeable"));
			Assert.That(KingdomCampHeartHighCraftRules.Refusal(Corpus.Find("Point-Defense Laser"), Vocabulary),
				Is.EqualTo("natural"));
			Assert.That(KingdomCampHeartHighCraftRules.Refusal(Corpus.Find("Maghammer"), Vocabulary),
				Is.EqualTo("holder"));
		}

		private static void ResolvesEveryTierWithAMargin(List<KingdomCampHeartHighCraftRules.IBlueprint> Factory,
			KingdomCampHeartHighCraftRules.Vocabulary Vocabulary)
		{
			KingdomBitTally wanted = ArcologyBits();
			Assert.That(wanted.IsEmpty(), Is.False);
			for (int tier = 0; tier < KingdomMaterialRules.BitTierCount; tier++)
			{
				if (wanted.Get(tier) <= 0) continue;
				var found = KingdomCampHeartHighCraftRules.Resolve(Factory, tier, null, Vocabulary,
					KingdomCampHeartHighCraftRules.DeclaredWorth);
				Assert.That(found, Is.Not.Null, "tier " + tier + " is unsourced within the scan cap");
				Assert.That(Factory.IndexOf(found), Is.LessThan(KingdomCampHeartHighCraftRules.ScanCap));
				Assert.That(found.Flag("Physics", "Takeable"), Is.Not.False, found.Key + " cannot be held");
				Assert.That(found.Flag("TinkerItem", "CanDisassemble"), Is.Not.False, found.Key);
				Assert.That(KingdomCampHeartHighCraftRules.DeclaredWorth(found).Get(tier), Is.GreaterThan(0));
				Assert.That(found.Marked(Vocabulary.MaterialTag) || found.Marked(Vocabulary.ScrapTag)
					|| Vocabulary.Materials.Contains(found.Name), Is.False, found.Key + " is a material");
				Assert.That(found.Marked(Vocabulary.ExoticTag) || Vocabulary.Exotics.Contains(found.Name),
					Is.False, found.Key + " is an exotic");
				Assert.That(found.DescendsFrom("Item") && !found.HasPart("Inventory")
					&& !found.HasPart("EnergyCellSocket") && !found.HasPart("Brain"), Is.True, found.Key);
				Assert.That(KingdomCampHeartHighCraftRules.CountWorth(Factory, tier, Vocabulary,
					KingdomCampHeartHighCraftRules.DeclaredWorth), Is.GreaterThanOrEqualTo(Margin),
					"tier " + tier + " has no margin for a realised-cost downgrade or a filled body");
			}
		}

		private static void CoversTheBillWithoutASkip(KingdomQudBlueprintCorpus Corpus,
			List<KingdomCampHeartHighCraftRules.IBlueprint> Factory, KingdomCampHeartHighCraftRules.Vocabulary Vocabulary)
		{
			var held = new KingdomBitTally();
			var book = new KingdomCampHeartHighCraftRules.Ledger();
			KingdomCampHeartHighCraftRules.Offer offer = (string key, int tier, out KingdomBitTally unit) =>
			{
				unit = KingdomCampHeartHighCraftRules.DeclaredWorth(Corpus.Find(key));
				held.AddAll(unit);
				return null;
			};
			string refused = KingdomCampHeartHighCraftRules.Fill(ArcologyBits(), () => held.Copy(),
				(tier, skipped) => KingdomCampHeartHighCraftRules.Resolve(Factory, tier, skipped, Vocabulary,
					KingdomCampHeartHighCraftRules.DeclaredWorth)?.Key,
				offer, () => Assert.Fail("nothing is retracted on declared worth"), book);
			Assert.That(refused, Is.Null);
			Assert.That(book.Skipped, Is.Empty);
			Assert.That(book.Minted.Count, Is.InRange(1, 5), string.Join(",", book.Minted));
			Assert.That(KingdomMaterialRules.CoversBits(held, ArcologyBits()), Is.True);
			foreach (string minted in book.Minted)
				Assert.That(KingdomCampHeartHighCraftRules.Refusal(Corpus.Find(minted.Substring(2)), Vocabulary),
					Is.Null, minted);
		}

		private static KingdomBitTally ArcologyBits()
		{
			var catalogue = new XmlDocument();
			catalogue.LoadXml(TestMain.ReadRepositoryText("KingdomBuildings.xml"));
			var rows = catalogue.GetElementsByTagName("building").Cast<XmlElement>()
				.Where(b => b.GetAttribute("Key") == KingdomCampHeartChainRules.SuccessorKey(5)).ToList();
			Assert.That(rows.Count, Is.EqualTo(1));
			Assert.That(KingdomMaterialRules.TryParseBitCost(rows[0].GetAttribute("Bits"),
				out KingdomBitTally bits, out string error), Is.True, error);
			return bits;
		}

		/// <summary>Production's vocabulary, read from the one place it is declared, so the corpus
		/// walk can never classify with a table of its own.</summary>
		private static KingdomCampHeartHighCraftRules.Vocabulary ProductionVocabulary()
		{
			string declared = TestMain.ReadRepositoryText("Growth/KingdomMaterials.01.Declarations.cs");
			Assert.That(TestMain.ReadRepositoryText("Growth/KingdomMaterials.03.StockClassification.cs"),
				Does.Contain("Object.HasTag(\"SemanticScrap\")"));
			return new KingdomCampHeartHighCraftRules.Vocabulary(Constant(declared, "MaterialTag"),
				Constant(declared, "ExoticTag"), "SemanticScrap", Quoted(declared, "MaterialBlueprints = new string["),
				Quoted(declared, "ExoticBlueprints = new string["));
		}

		private static string Constant(string Text, string Name)
		{
			Match found = Regex.Match(Text, "const string " + Name + " = \"([^\"]+)\";");
			Assert.That(found.Success, Is.True, Name);
			return found.Groups[1].Value;
		}

		private static string[] Quoted(string Text, string Start)
		{
			int at = Text.IndexOf(Start, StringComparison.Ordinal);
			Assert.That(at, Is.GreaterThan(0), Start);
			string block = Text.Substring(at, Text.IndexOf("};", at, StringComparison.Ordinal) - at);
			string[] names = Regex.Matches(block, "\"([^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
			Assert.That(names, Is.Not.Empty, Start);
			return names;
		}

		/// <summary>The installed Base named by TAF_QUD_BASE, which the licensed lane always sets
		/// (docs/DEVELOPMENT.md). No machine path is guessed: unset, the case is the exact licensed
		/// skip hosted CI allows; set but incomplete, it fails rather than falling back.</summary>
		private static string LocateBase()
		{
			string supplied = Environment.GetEnvironmentVariable("TAF_QUD_BASE");
			if (supplied == null)
				Assert.Ignore("High-craft corpus test requires TAF_QUD_BASE naming the installed Caves of Qud base.");
			if (!string.IsNullOrWhiteSpace(supplied) && File.Exists(Path.Combine(supplied,
				"ObjectBlueprints", "Items.xml"))) return supplied;
			throw new InvalidOperationException(
				"TAF_QUD_BASE is set but does not contain ObjectBlueprints/Items.xml: " + supplied);
		}
	}
}
#endif
