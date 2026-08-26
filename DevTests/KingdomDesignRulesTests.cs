#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomDesignRulesTests
	{
		// --- TryParseSkinAttributes -------------------------------------------------------------

		[Test]
		public void TryParseSkinAttributes_RejectsMissingKey()
		{
			bool ok = KingdomDesignRules.TryParseSkinAttributes(null, "verdant", "&g", null, null, null, out var entry, out var error);
			Assert.IsFalse(ok);
			Assert.IsNull(entry);
			Assert.IsNotNull(error);
		}

		[Test]
		public void TryParseSkinAttributes_RejectsWhitespaceOnlyKey()
		{
			bool ok = KingdomDesignRules.TryParseSkinAttributes("   ", "verdant", "&g", null, null, null, out var entry, out var error);
			Assert.IsFalse(ok);
			Assert.IsNull(entry);
		}

		[Test]
		public void TryParseSkinAttributes_RejectsASkinThatOverridesNothing()
		{
			// A <skin> with a Key and a Style but none of the four Render fields changes nothing
			// -- that is a bug in the authoring XML, not a valid "no-op" skin, so it is refused
			// rather than silently accepted onto the list.
			bool ok = KingdomDesignRules.TryParseSkinAttributes("verdant", "verdant", null, null, null, null, out var entry, out var error);
			Assert.IsFalse(ok);
			Assert.IsNull(entry);
			Assert.IsNotNull(error);
		}

		[TestCase("&g", null, null, null)]
		[TestCase(null, "G", null, null)]
		[TestCase(null, null, "0", null)]
		[TestCase(null, null, null, "Terrain_reused.png")]
		public void TryParseSkinAttributes_AcceptsAnySingleOverride(string color, string detail, string render, string tile)
		{
			bool ok = KingdomDesignRules.TryParseSkinAttributes("verdant", "verdant", color, detail, render, tile, out var entry, out var error);
			Assert.IsTrue(ok);
			Assert.IsNull(error);
			Assert.AreEqual(color, entry.ColorString);
			Assert.AreEqual(detail, entry.DetailColor);
			Assert.AreEqual(render, entry.RenderString);
			Assert.AreEqual(tile, entry.Tile);
		}

		[Test]
		public void TryParseSkinAttributes_TrimsKeyAndStyleAndBlanksStyleWhenEmpty()
		{
			bool ok = KingdomDesignRules.TryParseSkinAttributes("  fungal  ", "  fungal  ", "&m", null, null, null, out var entry, out _);
			Assert.IsTrue(ok);
			Assert.AreEqual("fungal", entry.Key);
			Assert.AreEqual("fungal", entry.Style);

			bool ok2 = KingdomDesignRules.TryParseSkinAttributes("universal", "   ", "&m", null, null, null, out var entry2, out _);
			Assert.IsTrue(ok2);
			Assert.IsNull(entry2.Style);
		}

		[Test]
		public void TryParseSkinAttributes_AcceptsATileThatDoesNotExist()
		{
			// Engine-free code cannot walk the art pipeline, so it cannot know whether a named
			// tile is reachable -- that is STANDARDS.md's Art/check_wiring.py job, run against
			// the shipped XML, never this file's. Parsing succeeds on a bogus path exactly as it
			// does on a real one; this test documents that this is a deliberate scope boundary,
			// not an oversight.
			bool ok = KingdomDesignRules.TryParseSkinAttributes("bogus", null, null, null, null, "ThousandAndFirst/does_not_exist.png", out var entry, out var error);
			Assert.IsTrue(ok);
			Assert.IsNull(error);
			Assert.AreEqual("ThousandAndFirst/does_not_exist.png", entry.Tile);
		}

		// --- FindSkin -----------------------------------------------------------------------------

		private static List<KingdomDesignRules.SkinEntry> ThreeSkins()
		{
			return new List<KingdomDesignRules.SkinEntry>
			{
				new KingdomDesignRules.SkinEntry { Key = "common", Style = "common", ColorString = "&y" },
				new KingdomDesignRules.SkinEntry { Key = "verdant", Style = "verdant", ColorString = "&g" },
				new KingdomDesignRules.SkinEntry { Key = "fungal", Style = "fungal", ColorString = "&m" }
			};
		}

		[Test]
		public void FindSkin_NullListReturnsNull()
		{
			Assert.IsNull(KingdomDesignRules.FindSkin(null, "verdant"));
		}

		[Test]
		public void FindSkin_EmptyKeyReturnsNull()
		{
			Assert.IsNull(KingdomDesignRules.FindSkin(ThreeSkins(), ""));
			Assert.IsNull(KingdomDesignRules.FindSkin(ThreeSkins(), null));
		}

		[Test]
		public void FindSkin_UnknownKeyReturnsNull()
		{
			Assert.IsNull(KingdomDesignRules.FindSkin(ThreeSkins(), "gyre"));
		}

		[Test]
		public void FindSkin_FindsTheMatchingEntryRegardlessOfPosition()
		{
			List<KingdomDesignRules.SkinEntry> skins = ThreeSkins();
			KingdomDesignRules.SkinEntry found = KingdomDesignRules.FindSkin(skins, "fungal");
			Assert.AreSame(skins[2], found);
		}

		// --- ResolveDefaultSkin: never "whatever the catalogue offers first" --------------------

		[Test]
		public void ResolveDefaultSkin_NullListReturnsNull()
		{
			Assert.IsNull(KingdomDesignRules.ResolveDefaultSkin(null, "verdant"));
		}

		[Test]
		public void ResolveDefaultSkin_BlankStyleReturnsNull()
		{
			Assert.IsNull(KingdomDesignRules.ResolveDefaultSkin(ThreeSkins(), null));
			Assert.IsNull(KingdomDesignRules.ResolveDefaultSkin(ThreeSkins(), ""));
		}

		[Test]
		public void ResolveDefaultSkin_NoMatchingStyleReturnsNullRatherThanTheFirstEntry()
		{
			// This is the mutation this test exists to catch: swap the "return null" for
			// "return Skins[0]" and this assertion fails, because ThreeSkins()[0] is "common",
			// not null, for a style ("gyre") none of the three skins claim.
			Assert.IsNull(KingdomDesignRules.ResolveDefaultSkin(ThreeSkins(), "gyre"));
		}

		[Test]
		public void ResolveDefaultSkin_ReturnsTheExactStyleMatch()
		{
			KingdomDesignRules.SkinEntry resolved = KingdomDesignRules.ResolveDefaultSkin(ThreeSkins(), "fungal");
			Assert.AreEqual("fungal", resolved.Key);
		}

		[Test]
		public void ResolveDefaultSkin_StyleComparisonIsCaseSensitive()
		{
			// Matches KingdomRules.StyleAllows's own exact-match convention for Styles lists;
			// a differently-cased style is simply not a match, not a fuzzy one.
			Assert.IsNull(KingdomDesignRules.ResolveDefaultSkin(ThreeSkins(), "Verdant"));
		}

		// --- DescribeSkinOption ---------------------------------------------------------------

		[Test]
		public void DescribeSkinOption_NullSkinIsBlank()
		{
			Assert.AreEqual("", KingdomDesignRules.DescribeSkinOption(null, Suggested: true));
		}

		[Test]
		public void DescribeSkinOption_SuggestedAddsTheMarker()
		{
			KingdomDesignRules.SkinEntry skin = new KingdomDesignRules.SkinEntry { Key = "verdant" };
			string described = KingdomDesignRules.DescribeSkinOption(skin, Suggested: true);
			StringAssert.Contains("verdant", described);
			StringAssert.Contains("suggested", described);
		}

		[Test]
		public void DescribeSkinOption_NotSuggestedOmitsTheMarker()
		{
			KingdomDesignRules.SkinEntry skin = new KingdomDesignRules.SkinEntry { Key = "verdant" };
			string described = KingdomDesignRules.DescribeSkinOption(skin, Suggested: false);
			Assert.AreEqual("verdant", described);
		}

		// --- IsBlank ----------------------------------------------------------------------------

		[TestCase(null, true)]
		[TestCase("", true)]
		[TestCase("   ", true)]
		[TestCase("\t\n", true)]
		[TestCase("x", false)]
		[TestCase(" x ", false)]
		public void IsBlank_MatchesWhitespaceOnlyText(string raw, bool expected)
		{
			Assert.AreEqual(expected, KingdomDesignRules.IsBlank(raw));
		}

		// --- TryValidateBuildingName: empty and absurd names ------------------------------------

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		public void TryValidateBuildingName_RejectsBlankNames(string raw)
		{
			bool ok = KingdomDesignRules.TryValidateBuildingName(raw, out string cleaned, out string error);
			Assert.IsFalse(ok);
			Assert.IsNull(cleaned);
			Assert.IsNotNull(error);
		}

		[Test]
		public void TryValidateBuildingName_AcceptsExactlyTheMaxLength()
		{
			string thirty = new string('X', KingdomDesignRules.MaxBuildingNameLength);
			Assert.AreEqual(30, thirty.Length);
			bool ok = KingdomDesignRules.TryValidateBuildingName(thirty, out string cleaned, out string error);
			Assert.IsTrue(ok);
			Assert.IsNull(error);
			Assert.AreEqual(thirty, cleaned);
		}

		[Test]
		public void TryValidateBuildingName_RejectsOneOverTheMaxLength()
		{
			string thirtyOne = new string('X', KingdomDesignRules.MaxBuildingNameLength + 1);
			bool ok = KingdomDesignRules.TryValidateBuildingName(thirtyOne, out string cleaned, out string error);
			Assert.IsFalse(ok);
			Assert.IsNull(cleaned);
			Assert.IsNotNull(error);
		}

		[Test]
		public void TryValidateBuildingName_TrimsSurroundingWhitespaceBeforeAccepting()
		{
			bool ok = KingdomDesignRules.TryValidateBuildingName("  Bright Hollow  ", out string cleaned, out string error);
			Assert.IsTrue(ok);
			Assert.AreEqual("Bright Hollow", cleaned);
			Assert.IsNull(error);
		}

		[Test]
		public void TryValidateBuildingName_LengthIsMeasuredAfterTrimming()
		{
			// Thirty real characters plus padding whitespace must still pass -- the trim happens
			// before the length check, not after it.
			string padded = "   " + new string('X', KingdomDesignRules.MaxBuildingNameLength) + "   ";
			bool ok = KingdomDesignRules.TryValidateBuildingName(padded, out string cleaned, out string error);
			Assert.IsTrue(ok);
			Assert.AreEqual(KingdomDesignRules.MaxBuildingNameLength, cleaned.Length);
		}

		[TestCase("Bright { Hollow")]
		[TestCase("Bright } Hollow")]
		[TestCase("{{r|Not markup here}}")]
		public void TryValidateBuildingName_KeepsPlainCurlyBracesForBoundaryEscaping(string raw)
		{
			bool ok = KingdomDesignRules.TryValidateBuildingName(raw, out string cleaned, out string error);
			Assert.IsTrue(ok, error);
			Assert.AreEqual(raw, cleaned);
		}

		[TestCase("Bright\nHollow")]
		[TestCase("Bright\tHollow")]
		[TestCase("Bright\rHollow")]
		public void TryValidateBuildingName_RejectsControlCharacters(string raw)
		{
			bool ok = KingdomDesignRules.TryValidateBuildingName(raw, out string cleaned, out string error);
			Assert.IsFalse(ok);
			Assert.IsNull(cleaned);
		}

		[TestCase("Bright Hollow")]
		[TestCase("Resheph's Landing")]
		[TestCase("!!! The Last Cistern !!!")]
		[TestCase("水")]
		public void TryValidateBuildingName_AcceptsOrdinaryAndUnusualButSafeNames(string raw)
		{
			bool ok = KingdomDesignRules.TryValidateBuildingName(raw, out string cleaned, out string error);
			Assert.IsTrue(ok);
			Assert.AreEqual(raw, cleaned);
			Assert.IsNull(error);
		}

		// --- NamedReference ----------------------------------------------------------------------

		[TestCase(null, "the cistern")]
		[TestCase("", "the cistern")]
		[TestCase("   ", "the cistern")]
		public void NamedReference_FallsBackToTheGenericLabelWhenBlank(string givenName, string genericLabel)
		{
			Assert.AreEqual(genericLabel, KingdomDesignRules.NamedReference(givenName, genericLabel));
		}

		[Test]
		public void NamedReference_PrefersACleanGivenNameOverTheGenericLabel()
		{
			Assert.AreEqual("Bright Hollow", KingdomDesignRules.NamedReference("Bright Hollow", "the cistern"));
		}

		[Test]
		public void NamedReference_TrimsTheGivenName()
		{
			Assert.AreEqual("Bright Hollow", KingdomDesignRules.NamedReference("  Bright Hollow  ", "the cistern"));
		}
	}
}
#endif
