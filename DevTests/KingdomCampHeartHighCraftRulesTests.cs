#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Executed contracts for the arcology's bit mint (#264 review). The mint used to mint every
	/// wanted tier without asking what the store already held, and took the first factory
	/// candidate the store could not even hold. These run the engine-free procedure the native
	/// chain runs, against fakes: every refusal names one fact, a covered tier is never minted for,
	/// a refused body skips its blueprint, an uncounted body is retracted, and the caps end the loop.
	/// </summary>
	public class KingdomCampHeartHighCraftRulesTests
	{
		internal sealed class Fake : KingdomCampHeartHighCraftRules.IBlueprint
		{
			internal readonly HashSet<string> Parts = new HashSet<string> { "TinkerItem", "Physics" };
			internal readonly Dictionary<string, bool> Flags = new Dictionary<string, bool>();
			internal readonly Dictionary<string, string> Texts = new Dictionary<string, string>();
			internal readonly HashSet<string> Marks = new HashSet<string>();
			internal readonly HashSet<string> Roots = new HashSet<string> { "Item" };
			internal Fake(string Key, string Bits) { this.Key = Name = Key; Texts["TinkerItem.Bits"] = Bits; }
			public string Key { get; set; }
			public string Name { get; set; }
			public bool HasPart(string Part) => Parts.Contains(Part);
			public bool? Flag(string Part, string Parameter) =>
				Flags.TryGetValue(Part + "." + Parameter, out bool v) ? v : (bool?)null;
			public string Text(string Part, string Parameter) =>
				Texts.TryGetValue(Part + "." + Parameter, out string v) ? v : null;
			public bool Marked(string Name) => Marks.Contains(Name);
			public bool DescendsFrom(string Root) => Roots.Contains(Root);
		}

		internal static readonly KingdomCampHeartHighCraftRules.Vocabulary Vocabulary =
			new KingdomCampHeartHighCraftRules.Vocabulary("r_KingdomMaterial", "r_KingdomExotic",
				"SemanticScrap", new[] { "Scrap Metal" }, new[] { "Bronze Ingot", "Gemstone" });

		private static KingdomBitTally Bits(string Text)
		{
			Assert.That(KingdomMaterialRules.TryParseBitCost(Text, out KingdomBitTally bits, out string error),
				Is.True, error);
			return bits;
		}

		[TestCase("abstract", "mark:BaseObject")]
		[TestCase("no-tinker", "drop:TinkerItem")]
		[TestCase("no-disassembly", "flag:TinkerItem.CanDisassemble")]
		[TestCase("no-bits", "bits:")]
		[TestCase("substitute", "alias")]
		[TestCase("substitute", "text:TinkerItem.SubstituteBlueprint")]
		[TestCase("substitute", "mark:CreateSubstituteBlueprint")]
		[TestCase("untakeable", "flag:Physics.Takeable")]
		[TestCase("not-item", "root")]
		[TestCase("creature", "mark:Creature")]
		[TestCase("creature", "part:Brain")]
		[TestCase("creature", "part:Body")]
		[TestCase("natural", "mark:Natural")]
		[TestCase("natural", "mark:NaturalGear")]
		[TestCase("holder", "part:Inventory")]
		[TestCase("holder", "part:EnergyCellSocket")]
		[TestCase("holder", "part:MagazineAmmoLoader")]
		[TestCase("vessel", "part:LiquidVolume")]
		[TestCase("important", "mark:Important")]
		[TestCase("important", "mark:QuestItem")]
		[TestCase("always-stack", "mark:AlwaysStack")]
		[TestCase("material", "mark:r_KingdomMaterial")]
		[TestCase("material", "mark:SemanticScrap")]
		[TestCase("material", "name:Scrap Metal")]
		[TestCase("exotic", "mark:r_KingdomExotic")]
		[TestCase("exotic", "name:Gemstone")]
		public void EachDeclaredFactRefusesTheBlueprintByName(string expected, string defect)
		{
			var salvage = new Fake("Mirrorshades", "03");
			Assert.That(KingdomCampHeartHighCraftRules.Refusal(salvage, Vocabulary), Is.Null);
			string what = defect.Contains(":") ? defect.Substring(defect.IndexOf(':') + 1) : "";
			if (defect.StartsWith("mark:")) salvage.Marks.Add(what);
			else if (defect.StartsWith("drop:")) salvage.Parts.Remove(what);
			else if (defect.StartsWith("flag:")) salvage.Flags[what] = false;
			else if (defect.StartsWith("bits:")) salvage.Texts["TinkerItem.Bits"] = "";
			else if (defect == "alias") salvage.Key = "Mirrorshades2";
			else if (defect.StartsWith("text:")) salvage.Texts[what] = "Other";
			else if (defect == "root") salvage.Roots.Clear();
			else if (defect.StartsWith("part:")) salvage.Parts.Add(what);
			else if (defect.StartsWith("name:")) salvage.Key = salvage.Name = what;
			Assert.That(KingdomCampHeartHighCraftRules.Refusal(salvage, Vocabulary), Is.EqualTo(expected));
		}

		[Test]
		public void DeclaredTrueFlagsAndAnUndeclaredTakeableStayCandidates()
		{
			var salvage = new Fake("Goggles", "12");
			salvage.Flags["TinkerItem.CanDisassemble"] = true;
			salvage.Flags["Physics.Takeable"] = true;
			Assert.That(KingdomCampHeartHighCraftRules.Refusal(salvage, Vocabulary), Is.Null);
			Assert.That(KingdomCampHeartHighCraftRules.Refusal(null, Vocabulary), Is.EqualTo("unreadable"));
			Assert.That(KingdomCampHeartHighCraftRules.Refusal(salvage, null), Is.EqualTo("unreadable"));
		}

		[Test]
		public void ResolveWalksFactoryOrderPastSkipsAndRefusalsWithoutPricingARefusal()
		{
			var cist = new Fake("Cist", "00145");
			cist.Flags["Physics.Takeable"] = false;
			var factory = new List<KingdomCampHeartHighCraftRules.IBlueprint>
				{ null, cist, new Fake("Hammer", "0345"), new Fake("Cheek", "3478") };
			var priced = new List<string>();
			Func<KingdomCampHeartHighCraftRules.IBlueprint, KingdomBitTally> worth = b =>
			{
				priced.Add(b.Key);
				return KingdomCampHeartHighCraftRules.DeclaredWorth(b);
			};
			Assert.That(KingdomCampHeartHighCraftRules.Resolve(factory, 4, null, Vocabulary, worth)?.Key,
				Is.EqualTo("Hammer"));
			Assert.That(priced, Is.EqualTo(new[] { "Hammer" }), "the refused cist is never priced");
			Assert.That(KingdomCampHeartHighCraftRules.Resolve(factory, 4, new[] { "Hammer" }, Vocabulary,
				worth)?.Key, Is.EqualTo("Cheek"));
			Assert.That(KingdomCampHeartHighCraftRules.Resolve(factory, 6, null, Vocabulary, worth), Is.Null);
			Assert.That(KingdomCampHeartHighCraftRules.CountWorth(factory, 4, Vocabulary, worth), Is.EqualTo(2));
			Assert.That(KingdomCampHeartHighCraftRules.CountWorth(factory, 0, Vocabulary, worth), Is.EqualTo(1));
		}

		[Test]
		public void ResolveAndCountStopAtTheScanCap()
		{
			var factory = Enumerable.Range(0, KingdomCampHeartHighCraftRules.ScanCap)
				.Select(i => (KingdomCampHeartHighCraftRules.IBlueprint)new Fake("Filler" + i, "1")).ToList();
			factory.Add(new Fake("Beyond", "0006"));
			var worth = (Func<KingdomCampHeartHighCraftRules.IBlueprint, KingdomBitTally>)
				KingdomCampHeartHighCraftRules.DeclaredWorth;
			Assert.That(KingdomCampHeartHighCraftRules.Resolve(factory, 6, null, Vocabulary, worth), Is.Null);
			Assert.That(KingdomCampHeartHighCraftRules.CountWorth(factory, 6, Vocabulary, worth), Is.Zero);
			factory.RemoveAt(0);
			Assert.That(KingdomCampHeartHighCraftRules.Resolve(factory, 6, null, Vocabulary, worth)?.Key,
				Is.EqualTo("Beyond"));
		}

		[TestCase("", "00346", 0)]
		[TestCase("0", "00346", 0)]
		[TestCase("00", "00346", 3)]
		[TestCase("0034", "00346", 6)]
		[TestCase("00346", "00346", -1)]
		[TestCase("0000003456666", "00346", -1)]
		public void TheShortTierIsTheLowestCoverageStillRefuses(string held, string wanted, int tier)
		{
			Assert.That(KingdomCampHeartHighCraftRules.FirstShortTier(Bits(held), Bits(wanted)), Is.EqualTo(tier));
			Assert.That(tier < 0, Is.EqualTo(KingdomMaterialRules.CoversBits(Bits(held), Bits(wanted))));
		}

		[TestCase("Exact", "inexact")]
		[TestCase("Takeable", "untakeable")]
		[TestCase("Important", "important")]
		[TestCase("Empty", "holds")]
		[TestCase("Natural", "natural")]
		[TestCase("Creature", "creature")]
		[TestCase("AlwaysStack", "always-stack")]
		[TestCase("Material", "material")]
		[TestCase("Exotic", "exotic")]
		[TestCase("Unit", "worthless")]
		public void EachReaderAnswerRefusesTheCreatedBodyByName(string fault, string expected)
		{
			var body = Sound();
			Assert.That(KingdomCampHeartHighCraftRules.BodyRefusal(body, 3), Is.Null);
			switch (fault)
			{
				case "Exact": body.Exact = false; break;
				case "Takeable": body.Takeable = false; break;
				case "Important": body.Important = true; break;
				case "Empty": body.Empty = false; break;
				case "Natural": body.Natural = true; break;
				case "Creature": body.Creature = true; break;
				case "AlwaysStack": body.AlwaysStack = true; break;
				case "Material": body.Material = true; break;
				case "Exotic": body.Exotic = true; break;
				default: body.Unit = Bits("0"); break;
			}
			Assert.That(KingdomCampHeartHighCraftRules.BodyRefusal(body, 3), Is.EqualTo(expected));
		}

		[Test]
		public void TheBodyIsReadInProductionsOrderAndAMissingReadingIsInexact()
		{
			var body = Sound();
			body.Empty = false; body.Material = true;
			Assert.That(KingdomCampHeartHighCraftRules.BodyRefusal(body, 3), Is.EqualTo("holds"),
				"CanUseMaterial refuses a holder before any classification is read");
			body.Takeable = false;
			Assert.That(KingdomCampHeartHighCraftRules.BodyRefusal(body, 3), Is.EqualTo("untakeable"));
			Assert.That(KingdomCampHeartHighCraftRules.BodyRefusal(Sound(), 6), Is.EqualTo("worthless"),
				"worth is read for the tier the body was minted for");
			Assert.That(KingdomCampHeartHighCraftRules.BodyRefusal(null, 3), Is.EqualTo("inexact"));
		}

		private static KingdomCampHeartHighCraftRules.BodyReading Sound() =>
			new KingdomCampHeartHighCraftRules.BodyReading
				{ Exact = true, Takeable = true, Empty = true, Unit = Bits("03") };

		[TestCase("0", "003", "03", true)]
		[TestCase("0", "0", "03", false)]
		[TestCase("0", "03", "03", false)]
		[TestCase("0", "0036", "03", false)]
		[TestCase("0", "0", "", false)]
		public void ABodyCountsOnlyWhenTheTallyRisesByExactlyItsWorth(string before, string after,
			string unit, bool counted)
		{
			Assert.That(KingdomCampHeartHighCraftRules.CountedExactly(Bits(before), Bits(after), Bits(unit)),
				Is.EqualTo(counted));
			Assert.That(KingdomCampHeartHighCraftRules.CountedExactly(null, Bits(after), Bits(unit)), Is.False);
			Assert.That(KingdomCampHeartHighCraftRules.CountedExactly(Bits(before), null, Bits(unit)), Is.False);
		}
	}
}
#endif
