#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomQolRulesTests
	{
		private static QolProfile Profile(string[] Needs, string[] Prefers, string[] Refuses)
		{
			return new QolProfile
			{
				Needs = Needs ?? KingdomQolRules.NoTags,
				Prefers = Prefers ?? KingdomQolRules.NoTags,
				Refuses = Refuses ?? KingdomQolRules.NoTags,
				EatsFood = true,
				DrinksWater = true
			};
		}

		private static bool Holds(string[] Set, string Tag)
		{
			return KingdomQolRules.Has(Set, Tag);
		}

		// --- Folding and parsing -------------------------------------------------------------

		[TestCase("taf:damp", "taf:damp")]
		[TestCase("  taf:damp  ", "taf:damp")]
		[TestCase("TAF:Damp", "taf:damp")]
		[TestCase("", "")]
		[TestCase("   ", "")]
		[TestCase(null, "")]
		public void Fold_TrimsAndLowersAndTreatsBlankAsBlank(string source, string expected)
		{
			Assert.AreEqual(expected, KingdomQolRules.Fold(source));
		}

		[TestCase("taf:damp", true)]
		[TestCase("theirmod:hearthfire", true)]
		[TestCase("damp", false)]
		[TestCase(":damp", false)]
		[TestCase("taf:", false)]
		[TestCase("", false)]
		[TestCase(null, false)]
		public void IsNamespaced_WantsAColonWithSomethingOnBothSides(string tag, bool expected)
		{
			Assert.AreEqual(expected, KingdomQolRules.IsNamespaced(tag));
		}

		[Test]
		public void ParseTags_FoldsSplitsAndDropsBlanks()
		{
			string[] tags = KingdomQolRules.ParseTags(" TAF:Damp , , taf:dark ");
			Assert.AreEqual(2, tags.Length);
			Assert.AreEqual(KingdomQolRules.TagDamp, tags[0]);
			Assert.AreEqual(KingdomQolRules.TagDark, tags[1]);
		}

		[Test]
		public void ParseTags_DropsRepeatsAndKeepsFirstOrder()
		{
			string[] tags = KingdomQolRules.ParseTags("taf:dark,taf:damp,TAF:DARK");
			Assert.AreEqual(2, tags.Length);
			Assert.AreEqual(KingdomQolRules.TagDark, tags[0]);
			Assert.AreEqual(KingdomQolRules.TagDamp, tags[1]);
		}

		[Test]
		public void ParseTags_KeepsATagNothingInThisModHasEverHeardOf()
		{
			string[] tags = KingdomQolRules.ParseTags("theirmod:hearthfire");
			Assert.AreEqual(1, tags.Length);
			Assert.AreEqual("theirmod:hearthfire", tags[0]);
		}

		[Test]
		public void ParseTags_DropsABareRemovalPrefixWithNothingAfterIt()
		{
			Assert.AreEqual(0, KingdomQolRules.ParseTags("-").Length);
		}

		[TestCase("")]
		[TestCase("   ")]
		[TestCase(null)]
		public void ParseTags_EmptySourceIsAnEmptySetAndNotAFailure(string source)
		{
			Assert.AreEqual(0, KingdomQolRules.ParseTags(source).Length);
		}

		// --- Merging an authored refinement onto a derived list ------------------------------

		[Test]
		public void Merge_AddsWhatTheRefinementNames()
		{
			string[] merged = KingdomQolRules.Merge(
				new string[1] { KingdomQolRules.TagDamp },
				new string[1] { KingdomQolRules.TagQuiet });
			Assert.AreEqual(2, merged.Length);
			Assert.IsTrue(Holds(merged, KingdomQolRules.TagDamp));
			Assert.IsTrue(Holds(merged, KingdomQolRules.TagQuiet));
		}

		[Test]
		public void Merge_RemovesADerivedTagNamedWithThePrefix()
		{
			string[] merged = KingdomQolRules.Merge(
				new string[2] { KingdomQolRules.TagSky, KingdomQolRules.TagDamp },
				new string[1] { "-taf:sky" });
			Assert.AreEqual(1, merged.Length);
			Assert.IsFalse(Holds(merged, KingdomQolRules.TagSky));
			Assert.IsTrue(Holds(merged, KingdomQolRules.TagDamp));
		}

		[Test]
		public void Merge_RemovingSomethingNeverDerivedChangesNothing()
		{
			string[] merged = KingdomQolRules.Merge(
				new string[1] { KingdomQolRules.TagDamp },
				new string[1] { "-taf:charge" });
			Assert.AreEqual(1, merged.Length);
			Assert.IsTrue(Holds(merged, KingdomQolRules.TagDamp));
		}

		[Test]
		public void Merge_KeepsWhatTheRefinementDoesNotMention()
		{
			string[] merged = KingdomQolRules.Merge(new string[1] { KingdomQolRules.TagCharge }, null);
			Assert.AreEqual(1, merged.Length);
			Assert.IsTrue(Holds(merged, KingdomQolRules.TagCharge));
		}

		[Test]
		public void Merge_DoesNotModifyEitherArgument()
		{
			string[] derived = new string[1] { KingdomQolRules.TagSky };
			string[] refinement = new string[2] { "-taf:sky", KingdomQolRules.TagDamp };
			KingdomQolRules.Merge(derived, refinement);
			Assert.AreEqual(1, derived.Length);
			Assert.AreEqual(KingdomQolRules.TagSky, derived[0]);
			Assert.AreEqual(2, refinement.Length);
		}

		// --- Derive before authoring ---------------------------------------------------------

		[Test]
		public void Derive_AnOrdinaryPersonAsksNothingAndEatsAndDrinks()
		{
			QolProfile profile = KingdomQolRules.Derive(ResidentTruth.Person);
			Assert.AreEqual(0, profile.Needs.Length);
			Assert.AreEqual(0, profile.Prefers.Length);
			Assert.AreEqual(0, profile.Refuses.Length);
			Assert.IsTrue(profile.EatsFood);
			Assert.IsTrue(profile.DrinksWater);
		}

		[Test]
		public void Derive_CarriesExactSpeciesIntoTheExistingProfile()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Species = "Mopango";
			QolProfile profile = KingdomQolRules.Derive(truth);
			Assert.AreEqual("Mopango", profile.Species);
		}

		[TestCase("Mopango", "species:mopango")]
		[TestCase("  Ooze  ", "species:ooze")]
		[TestCase("", null)]
		[TestCase(null, null)]
		[TestCase("bad|species", null)]
		public void SpeciesTag_IsOpenFoldedAndRosterSafe(string species, string expected)
		{
			Assert.AreEqual(expected, KingdomQolRules.SpeciesTag(species));
		}

		[Test]
		public void SpeciesTag_DropsOverlongIdentityWithoutTruncating()
		{
			Assert.IsNull(KingdomQolRules.SpeciesTag(
				new string('s', KingdomQolRules.MaxSpeciesLength + 1)));
		}

		[Test]
		public void SelfTags_AddsSpeciesWithoutAClosedSpeciesTable()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Species = "a species from another mod";
			QolProfile profile = KingdomQolRules.Derive(truth);
			Assert.IsTrue(Holds(KingdomQolRules.SelfTags(profile),
				"species:a species from another mod"));
		}

		[Test]
		public void SpeciesRefusalUsesTheExistingCohabitationJudgment()
		{
			QolProfile newcomer = Profile(null, null,
				new string[1] { "species:ooze" });
			ResidentTruth truth = ResidentTruth.Person;
			truth.Species = "Ooze";
			QolProfile ooze = KingdomQolRules.Derive(truth);
			Assert.IsTrue(KingdomLodgingRules.Conflicts(
				newcomer.Refuses, KingdomQolRules.SelfTags(newcomer),
				ooze.Refuses, KingdomQolRules.SelfTags(ooze), 0,
				KingdomLodgingRules.Closeness.Packed));
			Assert.IsTrue(KingdomLodgingRules.Intersects(newcomer.Refuses,
				KingdomQolRules.SelfTags(ooze)));
		}

		[Test]
		public void Derive_ARobotNeedsCharge()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Robot = true;
			QolProfile profile = KingdomQolRules.Derive(truth);
			Assert.IsTrue(Holds(profile.Needs, KingdomQolRules.TagCharge));
		}

		[Test]
		public void Derive_ARobotDoesNotEatEvenWhenItSomehowCarriesAStomach()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Robot = true;
			QolProfile profile = KingdomQolRules.Derive(truth);
			Assert.IsFalse(profile.EatsFood);
			Assert.IsFalse(profile.DrinksWater);
		}

		[Test]
		public void Derive_ACreatureWithNoStomachIsNeverFed()
		{
			ResidentTruth truth = default(ResidentTruth);
			QolProfile profile = KingdomQolRules.Derive(truth);
			Assert.IsFalse(profile.EatsFood);
			Assert.IsFalse(profile.DrinksWater);
		}

		[Test]
		public void Derive_AnInorganicCreatureIsNotFedEvenWithAStomach()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Inorganic = true;
			QolProfile profile = KingdomQolRules.Derive(truth);
			Assert.IsFalse(profile.EatsFood);
			Assert.IsFalse(profile.DrinksWater);
		}

		[Test]
		public void Derive_AWaterBoundCreatureNeedsOpenWater()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Aquatic = true;
			QolProfile profile = KingdomQolRules.Derive(truth);
			Assert.IsTrue(Holds(profile.Needs, KingdomQolRules.TagOpenWater));
		}

		[Test]
		public void Derive_AFlyingAquaticCreatureIsNotWaterBound()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Aquatic = true;
			truth.Flying = true;
			QolProfile profile = KingdomQolRules.Derive(truth);
			Assert.IsFalse(Holds(profile.Needs, KingdomQolRules.TagOpenWater));
		}

		[Test]
		public void Derive_AFungusNeedsDampAndOnlyPrefersDark()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Fungal = true;
			QolProfile profile = KingdomQolRules.Derive(truth);
			Assert.IsTrue(Holds(profile.Needs, KingdomQolRules.TagDamp));
			Assert.IsFalse(Holds(profile.Needs, KingdomQolRules.TagDark));
			Assert.IsTrue(Holds(profile.Prefers, KingdomQolRules.TagDark));
		}

		[Test]
		public void Derive_APhotosyntheticSettlerNeedsSky()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Photosynthetic = true;
			QolProfile profile = KingdomQolRules.Derive(truth);
			Assert.IsTrue(Holds(profile.Needs, KingdomQolRules.TagSky));
		}

		[Test]
		public void Derive_NeverProducesARefusal()
		{
			ResidentTruth truth = default(ResidentTruth);
			truth.Robot = true;
			truth.Aquatic = true;
			truth.Fungal = true;
			truth.Photosynthetic = true;
			truth.Inorganic = true;
			Assert.AreEqual(0, KingdomQolRules.Derive(truth).Refuses.Length);
		}

		[Test]
		public void Derive_AFungalRobotAsksForBoth()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Robot = true;
			truth.Fungal = true;
			QolProfile profile = KingdomQolRules.Derive(truth);
			Assert.IsTrue(Holds(profile.Needs, KingdomQolRules.TagCharge));
			Assert.IsTrue(Holds(profile.Needs, KingdomQolRules.TagDamp));
		}

		[Test]
		public void Derive_HandsOutAFreshListEveryTime()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Robot = true;
			QolProfile first = KingdomQolRules.Derive(truth);
			first.Needs[0] = "vandalised";
			QolProfile second = KingdomQolRules.Derive(truth);
			Assert.AreEqual(KingdomQolRules.TagCharge, second.Needs[0]);
		}

		// --- Authored refinement -------------------------------------------------------------

		[Test]
		public void Refine_AddsAuthoredNeedsToDerivedOnes()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Robot = true;
			QolProfile profile = KingdomQolRules.Refine(KingdomQolRules.Derive(truth), "taf:quiet", null, null);
			Assert.IsTrue(Holds(profile.Needs, KingdomQolRules.TagCharge));
			Assert.IsTrue(Holds(profile.Needs, KingdomQolRules.TagQuiet));
		}

		[Test]
		public void Refine_CanArgueWithTheDerivationByName()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Photosynthetic = true;
			QolProfile profile = KingdomQolRules.Refine(KingdomQolRules.Derive(truth), "-taf:sky", null, null);
			Assert.IsFalse(Holds(profile.Needs, KingdomQolRules.TagSky));
		}

		[Test]
		public void Refine_CarriesTheDerivedFeedingFactsThrough()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Robot = true;
			QolProfile profile = KingdomQolRules.Refine(KingdomQolRules.Derive(truth), "taf:quiet", "taf:dark", "taf:damp");
			Assert.IsFalse(profile.EatsFood);
			Assert.IsFalse(profile.DrinksWater);
		}

		[Test]
		public void Refine_CarriesTheDerivedSpeciesThrough()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Species = "Mopango";
			QolProfile profile = KingdomQolRules.Refine(
				KingdomQolRules.Derive(truth), "taf:quiet", null, null);
			Assert.AreEqual("Mopango", profile.Species);
		}

		[Test]
		public void Refine_AuthoringNothingChangesNothing()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Fungal = true;
			QolProfile derived = KingdomQolRules.Derive(truth);
			QolProfile refined = KingdomQolRules.Refine(derived, null, null, null);
			Assert.AreEqual(derived.Needs.Length, refined.Needs.Length);
			Assert.IsTrue(Holds(refined.Needs, KingdomQolRules.TagDamp));
			Assert.IsTrue(Holds(refined.Prefers, KingdomQolRules.TagDark));
		}

		[Test]
		public void Refine_AuthorsARefusalTheDerivationNeverGives()
		{
			QolProfile profile = KingdomQolRules.Refine(KingdomQolRules.Derive(ResidentTruth.Person), null, null, "taf:damp");
			Assert.IsTrue(Holds(profile.Refuses, KingdomQolRules.TagDamp));
		}

		[Test]
		public void Refine_ANullDerivedProfileReadsAsAnOrdinaryPerson()
		{
			QolProfile profile = KingdomQolRules.Refine(null, "taf:quiet", null, null);
			Assert.IsTrue(profile.EatsFood);
			Assert.IsTrue(Holds(profile.Needs, KingdomQolRules.TagQuiet));
		}

		// --- The household a resident keeps ---------------------------------------------------

		[Test]
		public void HouseholdProvides_IsWhatTheResidentNeeds()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Fungal = true;
			string[] household = KingdomQolRules.HouseholdProvides(KingdomQolRules.Derive(truth));
			Assert.IsTrue(Holds(household, KingdomQolRules.TagDamp));
		}

		[Test]
		public void HouseholdProvides_TakesAnAuthoredRefinementToo()
		{
			string[] household = KingdomQolRules.HouseholdProvides(
				KingdomQolRules.Derive(ResidentTruth.Person), "taf:quiet");
			Assert.IsTrue(Holds(household, KingdomQolRules.TagQuiet));
		}

		[Test]
		public void HouseholdProvides_ANullResidentProvidesNothing()
		{
			Assert.AreEqual(0, KingdomQolRules.HouseholdProvides(null).Length);
		}

		// --- What a roof provides on its own --------------------------------------------------

		[TestCase(KingdomPlotRules.RoofState.Open, "taf:sky")]
		[TestCase(KingdomPlotRules.RoofState.Soft, "taf:sky")]
		[TestCase(KingdomPlotRules.RoofState.Walled, "taf:dark")]
		[TestCase(KingdomPlotRules.RoofState.Carved, "taf:dark")]
		public void ProvidedByRoof_FollowsTheSameAdmitsSkyTheRestOfThePlotCodeUses(KingdomPlotRules.RoofState roof, string expected)
		{
			string[] provided = KingdomQolRules.ProvidedByRoof(roof);
			Assert.AreEqual(1, provided.Length);
			Assert.AreEqual(expected, provided[0]);
		}

		[Test]
		public void DesignOffer_APlotOffersItsRoofAsWellAsWhatItDeclared()
		{
			string[] offer = KingdomQolRules.DesignOffer("taf:damp", KingdomPlotRules.RoofState.Carved, IsPlot: true);
			Assert.IsTrue(Holds(offer, KingdomQolRules.TagDamp));
			Assert.IsTrue(Holds(offer, KingdomQolRules.TagDark));
		}

		[Test]
		public void DesignOffer_ATentHousesThePhotosyntheticSettlerAndAStoneHouseDoesNot()
		{
			Assert.IsTrue(Holds(KingdomQolRules.DesignOffer(null, KingdomPlotRules.RoofState.Soft, IsPlot: true), KingdomQolRules.TagSky));
			Assert.IsFalse(Holds(KingdomQolRules.DesignOffer(null, KingdomPlotRules.RoofState.Walled, IsPlot: true), KingdomQolRules.TagSky));
		}

		[Test]
		public void DesignOffer_ASingleCellWorkOffersOnlyWhatItDeclared()
		{
			string[] offer = KingdomQolRules.DesignOffer("taf:charge", KingdomPlotRules.RoofState.Walled, IsPlot: false);
			Assert.AreEqual(1, offer.Length);
			Assert.IsTrue(Holds(offer, KingdomQolRules.TagCharge));
			Assert.IsFalse(Holds(offer, KingdomQolRules.TagDark));
		}

		// --- The stratum: weather reaches nothing under rock (QB-19) ---------------------------

		[TestCase(KingdomPlotRules.RoofState.Open)]
		[TestCase(KingdomPlotRules.RoofState.Soft)]
		[TestCase(KingdomPlotRules.RoofState.Walled)]
		[TestCase(KingdomPlotRules.RoofState.Carved)]
		public void ProvidedByRoof_UndergroundEveryRoofIsShadeAndNoneIsSky(KingdomPlotRules.RoofState roof)
		{
			string[] provided = KingdomQolRules.ProvidedByRoof(roof, Underground: true);
			Assert.AreEqual(1, provided.Length);
			Assert.AreEqual(KingdomQolRules.TagDark, provided[0]);
		}

		[Test]
		public void ProvidedByRoof_AnOpenPlotUnderTheRockDoesNotAdvertiseOpenSky()
		{
			// The defect this rule exists to close: RoofOnGround correctly leaves an open plot open
			// underground -- Open is a claim about walls, and a field in the deep raises none -- so
			// reading AdmitsSky alone sold a cellar-field as sky and housed a photosynthetic
			// settler several hundred feet under the sun.
			Assert.IsTrue(KingdomPlotRules.AdmitsSky(
				KingdomPlotRules.RoofOnGround(KingdomPlotRules.RoofState.Open, Underground: true)));
			string[] provided = KingdomQolRules.ProvidedByRoof(KingdomPlotRules.RoofState.Open, Underground: true);
			Assert.IsFalse(Holds(provided, KingdomQolRules.TagSky));
			Assert.IsTrue(Holds(provided, KingdomQolRules.TagDark));
		}

		[TestCase(KingdomPlotRules.RoofState.Open, "taf:sky")]
		[TestCase(KingdomPlotRules.RoofState.Soft, "taf:sky")]
		[TestCase(KingdomPlotRules.RoofState.Walled, "taf:dark")]
		[TestCase(KingdomPlotRules.RoofState.Carved, "taf:dark")]
		public void ProvidedByRoof_TheSurfaceAnswerIsExactlyWhatItAlwaysWas(KingdomPlotRules.RoofState roof, string expected)
		{
			string[] provided = KingdomQolRules.ProvidedByRoof(roof, Underground: false);
			Assert.AreEqual(1, provided.Length);
			Assert.AreEqual(expected, provided[0]);
			CollectionAssert.AreEqual(KingdomQolRules.ProvidedByRoof(roof), provided);
		}

		[TestCase(KingdomPlotRules.RoofState.Open)]
		[TestCase(KingdomPlotRules.RoofState.Soft)]
		[TestCase(KingdomPlotRules.RoofState.Walled)]
		[TestCase(KingdomPlotRules.RoofState.Carved)]
		public void ProvidedByRoof_DoesNotCareWhetherItIsHandedTheDeclaredRoofOrTheGroundRoof(KingdomPlotRules.RoofState roof)
		{
			for (int i = 0; i < 2; i++)
			{
				bool underground = i == 1;
				CollectionAssert.AreEqual(
					KingdomQolRules.ProvidedByRoof(KingdomPlotRules.RoofOnGround(roof, underground), underground),
					KingdomQolRules.ProvidedByRoof(roof, underground));
			}
		}

		[Test]
		public void DesignOffer_AnOpenPlotInTheDeepOffersShadeAndKeepsWhatItDeclared()
		{
			string[] offer = KingdomQolRules.DesignOffer("taf:damp", KingdomPlotRules.RoofState.Open, IsPlot: true, Underground: true);
			Assert.IsFalse(Holds(offer, KingdomQolRules.TagSky));
			Assert.IsTrue(Holds(offer, KingdomQolRules.TagDark));
			Assert.IsTrue(Holds(offer, KingdomQolRules.TagDamp));
		}

		[Test]
		public void DesignOffer_CanvasUnderTheRockIsNoMoreSkyThanRockIs()
		{
			Assert.IsFalse(Holds(
				KingdomQolRules.DesignOffer(null, KingdomPlotRules.RoofState.Soft, IsPlot: true, Underground: true),
				KingdomQolRules.TagSky));
		}

		[TestCase(KingdomPlotRules.RoofState.Open)]
		[TestCase(KingdomPlotRules.RoofState.Soft)]
		[TestCase(KingdomPlotRules.RoofState.Walled)]
		[TestCase(KingdomPlotRules.RoofState.Carved)]
		public void DesignOffer_TheSurfaceOfferIsUnchangedForEveryRoofState(KingdomPlotRules.RoofState roof)
		{
			CollectionAssert.AreEqual(
				KingdomQolRules.DesignOffer("taf:quiet", roof, IsPlot: true),
				KingdomQolRules.DesignOffer("taf:quiet", roof, IsPlot: true, Underground: false));
		}

		[Test]
		public void DesignOffer_ASingleCellWorkInTheDeepStillOffersOnlyWhatItDeclared()
		{
			string[] offer = KingdomQolRules.DesignOffer("taf:charge", KingdomPlotRules.RoofState.Open, IsPlot: false, Underground: true);
			Assert.AreEqual(1, offer.Length);
			Assert.IsTrue(Holds(offer, KingdomQolRules.TagCharge));
			Assert.IsFalse(Holds(offer, KingdomQolRules.TagDark));
		}

		// --- The same question the lodging pass actually asks ----------------------------------

		[Test]
		public void Lodging_ThePhotosyntheticSettlerTakesAnOpenPlotInTheSunAndRefusesTheSameOneInTheDeep()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Photosynthetic = true;
			List<string> needs = new List<string>(KingdomQolRules.Derive(truth).Needs);
			Assert.IsTrue(KingdomLodgingRules.MeetsNeeds(needs, new List<string>(
				KingdomQolRules.DesignOffer(null, KingdomPlotRules.RoofState.Open, IsPlot: true, Underground: false))));
			Assert.IsFalse(KingdomLodgingRules.MeetsNeeds(needs, new List<string>(
				KingdomQolRules.DesignOffer(null, KingdomPlotRules.RoofState.Open, IsPlot: true, Underground: true))));
		}

		[Test]
		public void Lodging_TheFungalSettlerTakesTheDeepPlotThePhotosyntheticOneRefused()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Fungal = true;
			QolProfile profile = KingdomQolRules.Derive(truth);
			string[] deep = KingdomQolRules.DesignOffer("taf:damp", KingdomPlotRules.RoofState.Open, IsPlot: true, Underground: true);
			Assert.IsTrue(KingdomLodgingRules.MeetsNeeds(new List<string>(profile.Needs), new List<string>(deep)));
			Assert.AreEqual(KingdomCeremonyRules.TasteShadeAmount, KingdomQolRules.PreferShade(deep, profile));
		}

		[Test]
		public void Lodging_TheDarkAFungalSettlerWantedIsOnlyThereOnceTheRockIs()
		{
			ResidentTruth truth = ResidentTruth.Person;
			truth.Fungal = true;
			QolProfile profile = KingdomQolRules.Derive(truth);
			string[] sunlit = KingdomQolRules.DesignOffer("taf:damp", KingdomPlotRules.RoofState.Open, IsPlot: true, Underground: false);
			Assert.AreEqual(0, KingdomQolRules.PreferShade(sunlit, profile));
		}

		[Test]
		public void Lodging_ASettlerWhoWillNotLiveUnderSkyIsRefusedTheOpenPlotOnlyInTheSun()
		{
			QolProfile profile = Profile(null, null, new string[1] { KingdomQolRules.TagSky });
			string tag;
			Assert.AreEqual(QolVerdict.Refused, KingdomQolRules.Judge(
				KingdomQolRules.DesignOffer(null, KingdomPlotRules.RoofState.Open, IsPlot: true, Underground: false),
				profile, out tag));
			Assert.AreEqual(QolVerdict.Match, KingdomQolRules.Judge(
				KingdomQolRules.DesignOffer(null, KingdomPlotRules.RoofState.Open, IsPlot: true, Underground: true),
				profile, out tag));
		}

		// --- The match ------------------------------------------------------------------------

		[Test]
		public void Judge_EverythingMetIsAMatchAndNamesNothing()
		{
			string tag;
			QolVerdict verdict = KingdomQolRules.Judge(
				new string[1] { KingdomQolRules.TagCharge },
				Profile(new string[1] { KingdomQolRules.TagCharge }, null, null), out tag);
			Assert.AreEqual(QolVerdict.Match, verdict);
			Assert.AreEqual("", tag);
			Assert.IsTrue(KingdomQolRules.IsMatch(verdict));
			Assert.IsFalse(KingdomQolRules.IsBlocked(verdict));
		}

		[Test]
		public void Judge_AnUnmetNeedRefusesAndNamesTheTag()
		{
			string tag;
			QolVerdict verdict = KingdomQolRules.Judge(
				KingdomQolRules.NoTags,
				Profile(new string[1] { KingdomQolRules.TagCharge }, null, null), out tag);
			Assert.AreEqual(QolVerdict.NeedUnmet, verdict);
			Assert.AreEqual(KingdomQolRules.TagCharge, tag);
			Assert.IsTrue(KingdomQolRules.IsBlocked(verdict));
		}

		[Test]
		public void Judge_ARefusedTagRefusesAndNamesTheTag()
		{
			string tag;
			QolVerdict verdict = KingdomQolRules.Judge(
				new string[1] { KingdomQolRules.TagDamp },
				Profile(null, null, new string[1] { KingdomQolRules.TagDamp }), out tag);
			Assert.AreEqual(QolVerdict.Refused, verdict);
			Assert.AreEqual(KingdomQolRules.TagDamp, tag);
		}

		[Test]
		public void Judge_ARefusalOutranksAnUnmetNeedSoTheFounderIsNotSentOffToBuild()
		{
			string tag;
			QolVerdict verdict = KingdomQolRules.Judge(
				new string[1] { KingdomQolRules.TagDamp },
				Profile(new string[1] { KingdomQolRules.TagCharge }, null, new string[1] { KingdomQolRules.TagDamp }), out tag);
			Assert.AreEqual(QolVerdict.Refused, verdict);
			Assert.AreEqual(KingdomQolRules.TagDamp, tag);
		}

		[Test]
		public void Judge_ANeedNoBuildingInTheGameProvidesIsNamedRatherThanIgnored()
		{
			string tag;
			QolVerdict verdict = KingdomQolRules.Judge(
				new string[1] { KingdomQolRules.TagSky },
				Profile(new string[1] { "theirmod:hearthfire" }, null, null), out tag);
			Assert.AreEqual(QolVerdict.NeedUnmet, verdict);
			Assert.AreEqual("theirmod:hearthfire", tag);
		}

		[Test]
		public void Judge_ATagNothingConsumesIsInertRatherThanAnObstacle()
		{
			string tag;
			QolVerdict verdict = KingdomQolRules.Judge(
				new string[2] { KingdomQolRules.TagSky, "theirmod:hearthfire" },
				Profile(new string[1] { KingdomQolRules.TagSky }, null, null), out tag);
			Assert.AreEqual(QolVerdict.Match, verdict);
		}

		[Test]
		public void Judge_ARequirementIsMatchedRegardlessOfHowEitherSideSpelledIt()
		{
			string tag;
			QolVerdict verdict = KingdomQolRules.Judge(
				new string[1] { " TAF:Charge " },
				Profile(new string[1] { "taf:charge" }, null, null), out tag);
			Assert.AreEqual(QolVerdict.Match, verdict);
		}

		[Test]
		public void Judge_ANullProfileAsksNothingAndAlwaysMatches()
		{
			string tag;
			Assert.AreEqual(QolVerdict.Match, KingdomQolRules.Judge(null, null, out tag));
		}

		[Test]
		public void Judge_ANullOfferProvidesNothingRatherThanEverything()
		{
			string tag;
			Assert.AreEqual(QolVerdict.NeedUnmet,
				KingdomQolRules.Judge(null, Profile(new string[1] { KingdomQolRules.TagSky }, null, null), out tag));
		}

		[Test]
		public void Judge_TheFirstMissingNeedIsTheOneNamed()
		{
			string tag;
			KingdomQolRules.Judge(KingdomQolRules.NoTags,
				Profile(new string[2] { KingdomQolRules.TagCharge, KingdomQolRules.TagSky }, null, null), out tag);
			Assert.AreEqual(KingdomQolRules.TagCharge, tag);
		}

		// --- Prefers: small, capped, and never a penalty ---------------------------------------

		[Test]
		public void PreferShade_AnUnmetPreferenceIsWorthNothingAtAll()
		{
			Assert.AreEqual(0, KingdomQolRules.PreferShade(
				KingdomQolRules.NoTags,
				Profile(null, new string[1] { KingdomQolRules.TagDark }, null)));
		}

		[Test]
		public void PreferShade_AMetPreferenceIsWorthOneTasteUnit()
		{
			Assert.AreEqual(KingdomCeremonyRules.TasteShadeAmount, KingdomQolRules.PreferShade(
				new string[1] { KingdomQolRules.TagDark },
				Profile(null, new string[1] { KingdomQolRules.TagDark }, null)));
		}

		[Test]
		public void PreferShade_IsCappedNoMatterHowLongTheWishListIs()
		{
			string[] offer = new string[4]
			{
				KingdomQolRules.TagDark, KingdomQolRules.TagQuiet,
				KingdomQolRules.TagDamp, KingdomQolRules.TagCharge
			};
			QolProfile profile = Profile(null, offer, null);
			Assert.AreEqual(KingdomQolRules.MaxPreferShade, KingdomQolRules.PreferShade(offer, profile));
			Assert.AreEqual(KingdomQolRules.MaxPrefersCounted * KingdomCeremonyRules.TasteShadeAmount,
				KingdomQolRules.MaxPreferShade);
		}

		[Test]
		public void PreferFlags_NeverReportsMoreThanTheCap()
		{
			string[] offer = new string[3]
			{
				KingdomQolRules.TagDark, KingdomQolRules.TagQuiet, KingdomQolRules.TagDamp
			};
			List<bool> flags = KingdomQolRules.PreferFlags(offer, Profile(null, offer, null));
			Assert.AreEqual(KingdomQolRules.MaxPrefersCounted, flags.Count);
		}

		[Test]
		public void PreferFlags_RunsThroughTheTastesMachineryRatherThanBesideIt()
		{
			string[] offer = new string[1] { KingdomQolRules.TagDark };
			QolProfile profile = Profile(null, new string[2] { KingdomQolRules.TagDark, KingdomQolRules.TagQuiet }, null);
			List<bool> flags = KingdomQolRules.PreferFlags(offer, profile);
			Assert.AreEqual(KingdomCeremonyRules.TasteShade(flags), KingdomQolRules.PreferShade(offer, profile));
		}

		[Test]
		public void PreferShade_IsNeverNegativeForAnyoneWhoWantedAnything()
		{
			QolProfile profile = Profile(null, new string[2] { KingdomQolRules.TagDark, KingdomQolRules.TagQuiet }, null);
			Assert.GreaterOrEqual(KingdomQolRules.PreferShade(KingdomQolRules.NoTags, profile), 0);
		}

		[Test]
		public void PreferShade_NeverInventsAPenaltyForAPlaceThatRefusedSomebody()
		{
			// Shading and matching are separate questions and the shade is only ever read once a
			// match has happened. Asked about a place that refuses this person, it still reports a
			// plain count of met wants -- never a negative number, and never a debt.
			QolProfile profile = Profile(null, new string[1] { KingdomQolRules.TagDamp }, new string[1] { KingdomQolRules.TagDamp });
			string tag;
			Assert.AreEqual(QolVerdict.Refused,
				KingdomQolRules.Judge(new string[1] { KingdomQolRules.TagDamp }, profile, out tag));
			Assert.GreaterOrEqual(KingdomQolRules.PreferShade(new string[1] { KingdomQolRules.TagDamp }, profile), 0);
		}

		[Test]
		public void PreferShade_AskingTwiceGivesTheSameAnswerBecauseNothingAccumulates()
		{
			string[] offer = new string[1] { KingdomQolRules.TagDark };
			QolProfile profile = Profile(null, new string[1] { KingdomQolRules.TagDark }, null);
			int first = KingdomQolRules.PreferShade(offer, profile);
			int second = KingdomQolRules.PreferShade(offer, profile);
			int third = KingdomQolRules.PreferShade(offer, profile);
			Assert.AreEqual(first, second);
			Assert.AreEqual(second, third);
		}

		// --- Saying so (STANDARDS 7b) -------------------------------------------------------------

		[Test]
		public void WillNotSleepBeside_IsTheSentenceTheBriefItselfWrote()
		{
			Assert.AreEqual("Vashti will not sleep beside the fungal cellar.",
				KingdomQolRules.WillNotSleepBeside("Vashti", "fungal cellar"));
		}

		[Test]
		public void RefusalLine_ForARefusalOpensWithThatSentence()
		{
			string line = KingdomQolRules.RefusalLine(QolVerdict.Refused, "Vashti", "fungal cellar", KingdomQolRules.TagDamp);
			Assert.IsTrue(line.StartsWith("Vashti will not sleep beside the fungal cellar."), line);
		}

		[Test]
		public void RefusalLine_ForAnUnmetNeedNamesTheThingAndWhatWouldLiftIt()
		{
			string line = KingdomQolRules.RefusalLine(QolVerdict.NeedUnmet, "Vashti", "timber hut", KingdomQolRules.TagCharge);
			Assert.IsTrue(line.Contains("Vashti"), line);
			Assert.IsTrue(line.Contains("timber hut"), line);
			Assert.IsTrue(line.Contains(KingdomQolRules.TagPhrase(KingdomQolRules.TagCharge)), line);
			Assert.IsTrue(line.Contains("other quarters"), line);
		}

		[Test]
		public void RefusalLine_SaysNothingAtAllAboutAMatch()
		{
			Assert.IsNull(KingdomQolRules.RefusalLine(QolVerdict.Match, "Vashti", "timber hut", ""));
		}

		[Test]
		public void RefusalLine_ACreedClashReadsAsOneRatherThanAsAMissingTag()
		{
			string line = KingdomQolRules.RefusalLine(QolVerdict.Refused, "Vashti", "fine house", "");
			Assert.IsTrue(line.Contains("believes"), line);
		}

		[Test]
		public void RefusalLine_AnUnknownTagIsQuotedRatherThanGuessedAt()
		{
			string line = KingdomQolRules.RefusalLine(QolVerdict.NeedUnmet, "Vashti", "timber hut", "theirmod:hearthfire");
			Assert.IsTrue(line.Contains("\"theirmod:hearthfire\""), line);
		}

		[Test]
		public void RefusalLine_ANamelessNewcomerIsStillNamedSomething()
		{
			string line = KingdomQolRules.RefusalLine(QolVerdict.NeedUnmet, "", "timber hut", KingdomQolRules.TagSky);
			Assert.IsTrue(line.StartsWith("The newcomer") || line.StartsWith("the newcomer"), line);
		}

		[Test]
		public void NowhereLine_NamesTheSettlementAndTheThingItLacks()
		{
			string line = KingdomQolRules.NowhereLine("Vashti", "Ninefold", KingdomQolRules.TagCharge);
			Assert.IsTrue(line.Contains("Ninefold"), line);
			Assert.IsTrue(line.Contains(KingdomQolRules.TagPhrase(KingdomQolRules.TagCharge)), line);
			Assert.IsTrue(line.Contains("Vashti"), line);
		}

		[TestCase("taf:charge")]
		[TestCase("taf:openwater")]
		[TestCase("taf:damp")]
		[TestCase("taf:dark")]
		[TestCase("taf:sky")]
		[TestCase("taf:quiet")]
		public void TagPhrase_EveryTagThisModShipsHasProseOfItsOwn(string tag)
		{
			string phrase = KingdomQolRules.TagPhrase(tag);
			Assert.IsFalse(string.IsNullOrEmpty(phrase));
			Assert.IsFalse(phrase.Contains("\""), phrase);
			Assert.AreNotEqual(tag, phrase);
		}

		[Test]
		public void TagPhrase_CoversEveryTagInTheShippedVocabulary()
		{
			for (int i = 0; i < KingdomQolRules.OwnTags.Length; i++)
			{
				Assert.AreNotEqual("\"" + KingdomQolRules.OwnTags[i] + "\"",
					KingdomQolRules.TagPhrase(KingdomQolRules.OwnTags[i]));
				Assert.AreNotEqual("\"" + KingdomQolRules.OwnTags[i] + "\"",
					KingdomQolRules.TagObjection(KingdomQolRules.OwnTags[i]));
			}
		}

		[Test]
		public void OwnTags_AreAllNamespacedToUs()
		{
			for (int i = 0; i < KingdomQolRules.OwnTags.Length; i++)
			{
				Assert.IsTrue(KingdomQolRules.IsNamespaced(KingdomQolRules.OwnTags[i]), KingdomQolRules.OwnTags[i]);
				Assert.IsTrue(KingdomQolRules.OwnTags[i].StartsWith(KingdomQolRules.Namespace), KingdomQolRules.OwnTags[i]);
			}
		}
	}
}
#endif
