#if TAF_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomWaterRiteRulesTests
	{
		private const string Realm = "Barathrumites";
		private const BindingFlags PublicInstanceFields = BindingFlags.Instance
			| BindingFlags.Public | BindingFlags.DeclaredOnly;

		private static void AssertPublicIntEnum(Type type, params string[] expected)
		{
			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(type), type.Name + " backing type");
			Assert.AreEqual("ThousandAndFirst." + type.Name, type.FullName);
			Assert.IsTrue(type.IsPublic, type.Name + " accessibility changed");
			Assert.IsFalse(type.IsNested, type.Name + " became nested");
			string[] names = Enum.GetNames(type);
			string[] actual = new string[names.Length];
			for (int i = 0; i < names.Length; i++)
				actual[i] = names[i] + "=" + Convert.ToInt32(Enum.Parse(type, names[i]));
			CollectionAssert.AreEqual(expected, actual, type.Name + " values/order");
		}

		private static void AssertPublicReadonlyStruct(Type type, string[] names, Type[] types)
		{
			Assert.AreEqual("ThousandAndFirst." + type.Name, type.FullName);
			Assert.IsTrue(type.IsPublic && type.IsValueType && !type.IsNested);
			FieldInfo[] fields = type.GetFields(PublicInstanceFields);
			Assert.AreEqual(names.Length, fields.Length, type.Name + " field count");
			object value = Activator.CreateInstance(type);
			for (int i = 0; i < fields.Length; i++)
			{
				Assert.AreEqual(names[i], fields[i].Name, type.Name + " field order at " + i);
				Assert.AreEqual(types[i], fields[i].FieldType, type.Name + "." + fields[i].Name + " type");
				Assert.IsTrue(fields[i].IsInitOnly, type.Name + "." + fields[i].Name + " stopped being readonly");
				object expected = types[i].IsValueType ? Activator.CreateInstance(types[i]) : null;
				Assert.AreEqual(expected, fields[i].GetValue(value), type.Name + "." + fields[i].Name + " default");
			}
		}

		[Test]
		public void WaterRitePublicAbiKeepsExactEnumsRowsAndConstants()
		{
			AssertPublicIntEnum(typeof(WaterRiteBar), "Ready=0", "NotOnOurGround=1",
				"RealmBelievesNothing=2", "NothingBetweenYou=3", "TheirOffice=4", "NoRoadOut=5",
				"AskedTooOften=6", "AlreadyAnswered=7", "PouredTooRecently=8", "StoresCannotBear=9");
			AssertPublicIntEnum(typeof(WaterRiteAnswer), "Accepted=0", "TooNew=1", "RivalShrine=2",
				"Devout=3", "TooBitter=4", "Steadfast=5");
			AssertPublicReadonlyStruct(typeof(WaterRiteFacts),
				new[] { "Hostility", "SharedDays", "HoldsACreed", "RivalShrine", "Devout", "Steadfast", "RealmCreed" },
				new[] { typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(string) });
			AssertPublicReadonlyStruct(typeof(WaterRiteStamp),
				new[] { "Answer", "Hostility", "RivalShrine", "Absolute", "NeededDays", "RealmCreed" },
				new[] { typeof(WaterRiteAnswer), typeof(int), typeof(bool), typeof(bool), typeof(int), typeof(string) });
			Assert.AreEqual("ThousandAndFirst.KingdomWaterRiteRules", typeof(KingdomWaterRiteRules).FullName);
			Assert.IsTrue(typeof(KingdomWaterRiteRules).IsPublic);
			Assert.IsTrue(typeof(KingdomWaterRiteRules).IsAbstract && typeof(KingdomWaterRiteRules).IsSealed);
			Assert.AreEqual(24, KingdomWaterRiteRules.CovenantDistance);
			Assert.AreEqual(16, KingdomWaterRiteRules.CreedHeldDistance);
			Assert.AreEqual(30, KingdomWaterRiteRules.RivalShrineDistance);
			Assert.AreEqual(20, KingdomWaterRiteRules.DevotionDistance);
			Assert.AreEqual(4, KingdomWaterRiteRules.ReachPerSharedPass);
			Assert.AreEqual(140, KingdomWaterRiteRules.ReachCap);
			Assert.AreEqual(35, KingdomWaterRiteRules.SharedPassesForFullReach);
			Assert.AreEqual(105, KingdomWaterRiteRules.MaxCountedDays);
			Assert.AreEqual(4, KingdomWaterRiteRules.DistancePerDram);
			Assert.AreEqual(3, KingdomWaterRiteRules.RefusalsBeforeAskingCloses);
		}

		private static WaterRiteFacts Facts(int Hostility = 0, int SharedDays = 0, bool HoldsACreed = true, bool RivalShrine = false, bool Devout = false, bool Steadfast = false, string RealmCreed = Realm)
		{
			return new WaterRiteFacts(Hostility, SharedDays, HoldsACreed, RivalShrine, Devout, Steadfast, RealmCreed);
		}

		private static WaterRiteStamp StampAt(WaterRiteFacts F)
		{
			return KingdomWaterRiteRules.StampFor(F, KingdomWaterRiteRules.Answer(F));
		}

		// --- The distance: every term is its own named constant, and together they are the whole
		// --- of what stands between one settler and the realm's creed.

		[Test]
		public void Distance_ASettlerWhoHoldsNothingCostsTheBareCovenantAndNothingElse()
		{
			Assert.AreEqual(KingdomWaterRiteRules.CovenantDistance, KingdomWaterRiteRules.Distance(Facts(HoldsACreed: false)));
		}

		[Test]
		public void Distance_HoldingACreedOfTheirOwnIsFurtherThanHoldingNone()
		{
			int none = KingdomWaterRiteRules.Distance(Facts(HoldsACreed: false));
			Assert.AreEqual(none + KingdomWaterRiteRules.CreedHeldDistance, KingdomWaterRiteRules.Distance(Facts(HoldsACreed: true)));
		}

		[Test]
		public void Distance_HostilityIsAddedInTheFactionTablesOwnUnits()
		{
			int calm = KingdomWaterRiteRules.Distance(Facts(Hostility: 0));
			Assert.AreEqual(calm + 50, KingdomWaterRiteRules.Distance(Facts(Hostility: 50)));
			Assert.AreEqual(calm + 100, KingdomWaterRiteRules.Distance(Facts(Hostility: 100)));
		}

		[TestCase(-500, 0)]
		[TestCase(-1, 0)]
		[TestCase(0, 0)]
		[TestCase(100, 100)]
		[TestCase(400, 100)]
		public void Distance_HostilityFromThirdPartyDataIsClampedRatherThanTrusted(int given, int counted)
		{
			int expected = KingdomWaterRiteRules.CovenantDistance + KingdomWaterRiteRules.CreedHeldDistance + counted;
			Assert.AreEqual(expected, KingdomWaterRiteRules.Distance(Facts(Hostility: given)));
		}

		[Test]
		public void Distance_ARivalShrineInTheirQuarterAddsItsOwnConstant()
		{
			int without = KingdomWaterRiteRules.Distance(Facts());
			Assert.AreEqual(without + KingdomWaterRiteRules.RivalShrineDistance, KingdomWaterRiteRules.Distance(Facts(RivalShrine: true)));
		}

		[Test]
		public void Distance_DevotionIsAlwaysACostAndNeverADiscount()
		{
			int plain = KingdomWaterRiteRules.Distance(Facts());
			int devout = KingdomWaterRiteRules.Distance(Facts(Devout: true));
			Assert.Greater(devout, plain);
			Assert.AreEqual(plain + KingdomWaterRiteRules.DevotionDistance, devout);
		}

		// --- The reach: shared living, capped, with the fault line sitting exactly at the cap.

		[TestCase(-3, 0)]
		[TestCase(0, 0)]
		[TestCase(3, KingdomWaterRiteRules.ReachPerSharedPass)]
		[TestCase(30, 10 * KingdomWaterRiteRules.ReachPerSharedPass)]
		public void Reach_GrowsThreeCohabitedDaysAtATime(int days, int expected)
		{
			// Three days buy what one attended pass used to buy, which is the whole of the
			// recalibration expressed at the smallest scale it can be expressed at.
			Assert.AreEqual(expected, KingdomWaterRiteRules.Reach(days));
		}

		[Test]
		public void Reach_StopsAtTheCapAndStaysThere()
		{
			Assert.AreEqual(KingdomWaterRiteRules.ReachCap, KingdomWaterRiteRules.Reach(KingdomWaterRiteRules.MaxCountedDays));
			Assert.AreEqual(KingdomWaterRiteRules.ReachCap, KingdomWaterRiteRules.Reach(KingdomWaterRiteRules.MaxCountedDays + 500));
		}

		[Test]
		public void Reach_TheCapIsExactlyTheDistanceToAFaultLine_WhichIsTheWholeArc()
		{
			// Addendum 4d makes the flat -100 fault lines refuse every shared roof at every tier,
			// which puts osmosis and the shared table out of reach of them by construction.
			// Addendum 5 makes conversion the healing arc that ceiling requires, so SOMETHING has
			// to be able to cross one, and this rite is it: at the very end of a whole shared life
			// and not one pass sooner. Break this identity and either the fault line becomes
			// uncrossable by any channel in the mod, or it becomes cheap.
			Assert.AreEqual(KingdomWaterRiteRules.ReachCap, KingdomWaterRiteRules.Distance(Facts(Hostility: 100)));
			Assert.AreEqual(WaterRiteAnswer.Accepted, KingdomWaterRiteRules.Answer(Facts(Hostility: 100, SharedDays: KingdomWaterRiteRules.MaxCountedDays)));
			Assert.AreNotEqual(WaterRiteAnswer.Accepted, KingdomWaterRiteRules.Answer(Facts(Hostility: 100, SharedDays: KingdomWaterRiteRules.MaxCountedDays - 1)));
		}

		// --- The answer: one branch per obstacle, ordered by what the founder can do about it.

		[Test]
		public void Answer_AcceptsOnceTheSharedLifeCoversTheDistance()
		{
			Assert.AreEqual(WaterRiteAnswer.Accepted, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: 30)));
		}

		[Test]
		public void Answer_ARefusesTagBeatsEverything_EvenAWholeSharedLife()
		{
			Assert.AreEqual(WaterRiteAnswer.Accepted, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: KingdomWaterRiteRules.MaxCountedDays)));
			Assert.AreEqual(WaterRiteAnswer.Steadfast, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: KingdomWaterRiteRules.MaxCountedDays, Steadfast: true)));
		}

		[Test]
		public void Answer_NamesTheShrineWhenTakingItDownWouldByItselfHaveChangedTheAnswer()
		{
			// Thirty days reaches 40, which is the distance without the shrine; with it, 70.
			Assert.AreEqual(WaterRiteAnswer.RivalShrine, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: 30, RivalShrine: true)));
			Assert.AreEqual(WaterRiteAnswer.Accepted, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: 30)));
		}

		[Test]
		public void Answer_DoesNotNameTheShrineWhenRemovingItAloneWouldNotHaveHelped()
		{
			// Three days reaches 4 against a distance of 70, so the shrine is not what is standing
			// in the way and naming it would be a lie the founder would act on (7b).
			Assert.AreEqual(WaterRiteAnswer.TooNew, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: 3, RivalShrine: true)));
		}

		[Test]
		public void Answer_NamesDevotionWhenThatAloneIsWhatIsInTheWay()
		{
			// Thirty days reaches 40, the distance without the devotion; with it, 60.
			Assert.AreEqual(WaterRiteAnswer.Devout, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: 30, Devout: true)));
		}

		[Test]
		public void Answer_TheShrineIsNamedBeforeTheDevotion_BecauseOneCanBeActedOnToday()
		{
			// Distance 90 against a reach of 80: taking down the shrine or setting aside the
			// devotion would each have closed it, and only one of the two is the founder's to do.
			Assert.AreEqual(WaterRiteAnswer.RivalShrine, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: 60, RivalShrine: true, Devout: true)));
		}

		[Test]
		public void Answer_TooNewWhenALongerSharedLifeWouldEventuallyDoIt()
		{
			Assert.AreEqual(WaterRiteAnswer.TooNew, KingdomWaterRiteRules.Answer(Facts(Hostility: 50, SharedDays: 3)));
		}

		[Test]
		public void Answer_TooBitterWhenNoSharedLifeCouldEverCoverIt()
		{
			// A fault line with a rival shrine on top of it is past the cap: no number of passes
			// reaches it, and the honest answer is that one of the two creeds has to move.
			Assert.AreEqual(WaterRiteAnswer.TooBitter, KingdomWaterRiteRules.Answer(Facts(Hostility: 100, SharedDays: 1, RivalShrine: true)));
		}

		[Test]
		public void Answer_IsAPureFunctionOfTheFacts_AskedTwiceItSaysTheSameThing()
		{
			WaterRiteFacts facts = Facts(Hostility: 50, SharedDays: 7, RivalShrine: true, Devout: true);
			Assert.AreEqual(KingdomWaterRiteRules.Answer(facts), KingdomWaterRiteRules.Answer(facts));
		}

		[Test]
		public void Converted_IsTrueForAcceptanceAndForNothingElse()
		{
			Assert.IsTrue(KingdomWaterRiteRules.Converted(WaterRiteAnswer.Accepted));
			Assert.IsFalse(KingdomWaterRiteRules.Converted(WaterRiteAnswer.TooNew));
			Assert.IsFalse(KingdomWaterRiteRules.Converted(WaterRiteAnswer.RivalShrine));
			Assert.IsFalse(KingdomWaterRiteRules.Converted(WaterRiteAnswer.Devout));
			Assert.IsFalse(KingdomWaterRiteRules.Converted(WaterRiteAnswer.TooBitter));
			Assert.IsFalse(KingdomWaterRiteRules.Converted(WaterRiteAnswer.Steadfast));
		}

		// --- Needed days: the door a "not yet" leaves open, and it really does open.

		[TestCase(4, 3)]
		[TestCase(5, 4)]
		[TestCase(8, 6)]
		[TestCase(40, 30)]
		public void NeededDays_RoundsUp_SoTheNamedDayActuallyCoversTheDistance(int distance, int expected)
		{
			Assert.AreEqual(expected, KingdomWaterRiteRules.NeededDays(distance));
		}

		[Test]
		public void NeededDays_IsZeroWhenNoSharedLifeWouldEverCoverIt()
		{
			Assert.AreEqual(0, KingdomWaterRiteRules.NeededDays(KingdomWaterRiteRules.ReachCap + 1));
			Assert.AreNotEqual(0, KingdomWaterRiteRules.NeededDays(KingdomWaterRiteRules.ReachCap));
		}

		[TestCase(0, false, false)]
		[TestCase(25, false, false)]
		[TestCase(50, true, false)]
		[TestCase(0, false, true)]
		[TestCase(50, false, true)]
		[TestCase(0, true, true)]
		public void NeededDays_LivingExactlyThatManyDaysIsAcceptedAndOneFewerIsNot(int hostility, bool shrine, bool devout)
		{
			WaterRiteFacts atZero = Facts(Hostility: hostility, SharedDays: 0, RivalShrine: shrine, Devout: devout);
			int needed = KingdomWaterRiteRules.NeededDays(KingdomWaterRiteRules.Distance(atZero));
			Assert.Greater(needed, 0);
			Assert.AreEqual(WaterRiteAnswer.Accepted, KingdomWaterRiteRules.Answer(Facts(Hostility: hostility, SharedDays: needed, RivalShrine: shrine, Devout: devout)));
			Assert.AreNotEqual(WaterRiteAnswer.Accepted, KingdomWaterRiteRules.Answer(Facts(Hostility: hostility, SharedDays: needed - 1, RivalShrine: shrine, Devout: devout)));
		}

		// --- The price: the founding basin, held again, for one person.

		[Test]
		public void Cost_IsTheFoundingBasinPlusAMeasureForWhatIsInTheWay()
		{
			Assert.AreEqual(KingdomRules.FoundingCostDrams, KingdomWaterRiteRules.Cost(0));
			Assert.AreEqual(KingdomRules.FoundingCostDrams + 1, KingdomWaterRiteRules.Cost(KingdomWaterRiteRules.DistancePerDram));
		}

		[Test]
		public void Cost_NeverFallsBelowTheBasin_EvenOnNonsenseInput()
		{
			Assert.AreEqual(KingdomRules.FoundingCostDrams, KingdomWaterRiteRules.Cost(-100));
		}

		[Test]
		public void Cost_RisesWithWhatStandsInTheWay()
		{
			Assert.Greater(
				KingdomWaterRiteRules.Cost(KingdomWaterRiteRules.Distance(Facts(Hostility: 100))),
				KingdomWaterRiteRules.Cost(KingdomWaterRiteRules.Distance(Facts(Hostility: 0))));
		}

		// --- Asked once, and not again until something is different.

		[Test]
		public void SomethingChanged_IsFalseWhenTheFounderSimplyAsksTheSameQuestionAgain()
		{
			WaterRiteFacts facts = Facts(Hostility: 50, SharedDays: 3);
			Assert.IsFalse(KingdomWaterRiteRules.SomethingChanged(StampAt(facts), facts));
		}

		[Test]
		public void SomethingChanged_IsFalseWhenTheyLivedOneMorePassButNotEnoughOfThem()
		{
			WaterRiteFacts then = Facts(Hostility: 50, SharedDays: 3);
			Assert.IsFalse(KingdomWaterRiteRules.SomethingChanged(StampAt(then), Facts(Hostility: 50, SharedDays: 4)));
		}

		[Test]
		public void SomethingChanged_OpensWhenTheSharedLifeHasGrownLongEnoughToCoverTheDistance()
		{
			WaterRiteFacts then = Facts(Hostility: 50, SharedDays: 3);
			WaterRiteStamp stamp = StampAt(then);
			Assert.IsTrue(KingdomWaterRiteRules.SomethingChanged(stamp, Facts(Hostility: 50, SharedDays: stamp.NeededDays)));
		}

		[Test]
		public void SomethingChanged_OpensWhenTheQuarrelHasEased()
		{
			WaterRiteFacts then = Facts(Hostility: 100, SharedDays: 3);
			Assert.IsTrue(KingdomWaterRiteRules.SomethingChanged(StampAt(then), Facts(Hostility: 50, SharedDays: 3)));
		}

		[Test]
		public void SomethingChanged_DoesNotOpenWhenTheQuarrelGotWorse()
		{
			WaterRiteFacts then = Facts(Hostility: 50, SharedDays: 3);
			Assert.IsFalse(KingdomWaterRiteRules.SomethingChanged(StampAt(then), Facts(Hostility: 100, SharedDays: 3)));
		}

		[Test]
		public void SomethingChanged_OpensWhenTheRivalShrineIsGone()
		{
			WaterRiteFacts then = Facts(Hostility: 50, SharedDays: 3, RivalShrine: true);
			Assert.IsTrue(KingdomWaterRiteRules.SomethingChanged(StampAt(then), Facts(Hostility: 50, SharedDays: 3)));
		}

		[Test]
		public void SomethingChanged_DoesNotOpenWhenAShrineAppearsWhereThereWasNone()
		{
			WaterRiteFacts then = Facts(Hostility: 50, SharedDays: 3);
			Assert.IsFalse(KingdomWaterRiteRules.SomethingChanged(StampAt(then), Facts(Hostility: 50, SharedDays: 3, RivalShrine: true)));
		}

		[Test]
		public void SomethingChanged_OpensWheneverTheRealmBelievesSomethingElse()
		{
			WaterRiteFacts then = Facts(Hostility: 50, SharedDays: 3);
			Assert.IsTrue(KingdomWaterRiteRules.SomethingChanged(StampAt(then), Facts(Hostility: 50, SharedDays: 3, RealmCreed: "Joppa")));
		}

		[Test]
		public void SomethingChanged_ASteadfastRefusalIsReopenedOnlyByTheRealmBelievingSomethingElse()
		{
			WaterRiteStamp stamp = StampAt(Facts(Hostility: 100, SharedDays: 1, RivalShrine: true, Steadfast: true));
			Assert.IsTrue(stamp.Absolute);
			Assert.IsFalse(KingdomWaterRiteRules.SomethingChanged(stamp, Facts(Hostility: 0, SharedDays: KingdomWaterRiteRules.MaxCountedDays, Steadfast: true)));
			Assert.IsTrue(KingdomWaterRiteRules.SomethingChanged(stamp, Facts(Hostility: 100, SharedDays: 1, RivalShrine: true, Steadfast: true, RealmCreed: "Joppa")));
		}

		[Test]
		public void StampFor_MarksOnlyASteadfastRefusalAbsolute()
		{
			Assert.IsFalse(KingdomWaterRiteRules.StampFor(Facts(Hostility: 100), WaterRiteAnswer.TooBitter).Absolute);
			Assert.IsFalse(KingdomWaterRiteRules.StampFor(Facts(), WaterRiteAnswer.TooNew).Absolute);
			Assert.IsTrue(KingdomWaterRiteRules.StampFor(Facts(Steadfast: true), WaterRiteAnswer.Steadfast).Absolute);
		}

		// --- Creed keys: null and empty both mean no affiliation or belief.

		[TestCase(null, null, true)]
		[TestCase(null, "", true)]
		[TestCase("", "", true)]
		[TestCase("Joppa", "Joppa", true)]
		[TestCase("Joppa", "joppa", false)]
		[TestCase("Joppa", null, false)]
		[TestCase("Joppa", "Barathrumites", false)]
		public void SameCreed_TreatsNullAndEmptyAsHoldingNothingInParticular(string a, string b, bool same)
		{
			Assert.AreEqual(same, KingdomWaterRiteRules.SameCreed(a, b));
			Assert.AreEqual(same, KingdomWaterRiteRules.SameCreed(b, a));
		}

		// --- Shared living: the days somebody has actually lived here.

		[TestCase(-1, 1, 1)]
		[TestCase(0, 1, 1)]
		[TestCase(5, 1, 6)]
		[TestCase(5, 12, 17)]
		public void SharedDaysAfter_AdvancesByExactlyTheDaysLived(int before, int days, int after)
		{
			Assert.AreEqual(after, KingdomWaterRiteRules.SharedDaysAfter(before, days));
		}

		[Test]
		public void SharedDaysAfter_ANonPositiveStretchChangesNothing()
		{
			Assert.AreEqual(5, KingdomWaterRiteRules.SharedDaysAfter(5, 0));
			Assert.AreEqual(5, KingdomWaterRiteRules.SharedDaysAfter(5, -7));
			Assert.AreEqual(0, KingdomWaterRiteRules.SharedDaysAfter(-3, 0), "a negative reads as none");
		}

		[Test]
		public void SharedDaysAfter_StopsWhereTheReachStopsMeaningAnything()
		{
			Assert.AreEqual(KingdomWaterRiteRules.MaxCountedDays, KingdomWaterRiteRules.SharedDaysAfter(KingdomWaterRiteRules.MaxCountedDays, 1));
			Assert.AreEqual(KingdomWaterRiteRules.MaxCountedDays, KingdomWaterRiteRules.SharedDaysAfter(0, KingdomWaterRiteRules.MaxCountedDays + 9));
			Assert.AreEqual(KingdomWaterRiteRules.MaxCountedDays, KingdomWaterRiteRules.SharedDaysAfter(0, 1000000000),
				"a thousand days and a hundred and five arrive at the same place");
			Assert.AreEqual(KingdomWaterRiteRules.ReachCap, KingdomWaterRiteRules.Reach(KingdomWaterRiteRules.MaxCountedDays));
		}

		[Test]
		public void SharedLivingHoldsItsPaceAcrossTheChangeOfUnit()
		{
			// The recalibration, from the founder's side: three cohabited days buy exactly the
			// four of reach one attended pass used to, and a hundred and five buy the whole of
			// what thirty-five visits bought. If either drifts, the water rite silently became a
			// different arc.
			Assert.AreEqual(KingdomWaterRiteRules.ReachPerSharedPass,
				KingdomWaterRiteRules.Reach(KingdomBrinkRules.CohabitationDaysPerAttendedPass));
			Assert.AreEqual(KingdomWaterRiteRules.ReachCap,
				KingdomWaterRiteRules.Reach(KingdomBrinkRules.InCohabitationDays(KingdomWaterRiteRules.SharedPassesForFullReach)));
		}

		[Test]
		public void Reach_NeverFallsAsTheDaysRise()
		{
			int last = 0;
			for (int days = 0; days <= KingdomWaterRiteRules.MaxCountedDays + 5; days++)
			{
				int reach = KingdomWaterRiteRules.Reach(days);
				Assert.GreaterOrEqual(reach, last, "a day lived here can never take reach away");
				Assert.LessOrEqual(reach, KingdomWaterRiteRules.ReachCap);
				last = reach;
			}
		}

		// --- The exit. A settler may always emigrate rather than convert.

		[Test]
		public void TheRiteItselfIsNotAnImposedChannel_WhichIsWhyRepetitionHasToBeReportedSeparately()
		{
			// One invitation is not pressure, and KingdomConversionRules says so about this
			// channel by name. The shell therefore reports REPEATED asking to that file's own
			// pressure surface rather than growing an exit of its own; if this ever flipped, the
			// shell would be registering pressure a settler was already resenting twice over.
			Assert.IsFalse(KingdomConversionRules.IsImposed(ConversionChannel.Diplomacy));
			Assert.IsTrue(KingdomConversionRules.IsImposed(ConversionChannel.Shrine));
		}

		[TestCase(0, false)]
		[TestCase(1, false)]
		[TestCase(2, false)]
		[TestCase(3, true)]
		[TestCase(9, true)]
		public void AskedTooOften_FiresOnlyOnceTheyHaveRefusedTheNamedNumberOfTimes(int refusals, bool closed)
		{
			Assert.AreEqual(refusals >= KingdomWaterRiteRules.RefusalsBeforeAskingCloses, closed);
			Assert.AreEqual(closed, KingdomWaterRiteRules.AskedTooOften(refusals));
		}

		[TestCase(-4, 1)]
		[TestCase(0, 1)]
		[TestCase(1, 2)]
		public void RefusalsAfter_CountsOneMore(int before, int after)
		{
			Assert.AreEqual(after, KingdomWaterRiteRules.RefusalsAfter(before));
		}

		[Test]
		public void RefusalsAfter_ClampsAtTheThreshold_BecausePastItTheCountStopsMeaningAnything()
		{
			Assert.AreEqual(KingdomWaterRiteRules.RefusalsBeforeAskingCloses, KingdomWaterRiteRules.RefusalsAfter(KingdomWaterRiteRules.RefusalsBeforeAskingCloses));
			Assert.AreEqual(KingdomWaterRiteRules.RefusalsBeforeAskingCloses, KingdomWaterRiteRules.RefusalsAfter(KingdomWaterRiteRules.RefusalsBeforeAskingCloses + 20));
		}

		[Test]
		public void ARefusalCountedThreeTimesIsExactlyWhatClosesTheAsking()
		{
			int refusals = 0;
			for (int i = 0; i < KingdomWaterRiteRules.RefusalsBeforeAskingCloses; i++)
			{
				Assert.IsFalse(KingdomWaterRiteRules.AskedTooOften(refusals));
				refusals = KingdomWaterRiteRules.RefusalsAfter(refusals);
			}
			Assert.IsTrue(KingdomWaterRiteRules.AskedTooOften(refusals));
		}

		// --- Prose. Nothing stalls in silence, and a refusal is worth reading.

		[TestCase(WaterRiteBar.NotOnOurGround)]
		[TestCase(WaterRiteBar.RealmBelievesNothing)]
		[TestCase(WaterRiteBar.NothingBetweenYou)]
		[TestCase(WaterRiteBar.TheirOffice)]
		[TestCase(WaterRiteBar.NoRoadOut)]
		[TestCase(WaterRiteBar.AskedTooOften)]
		[TestCase(WaterRiteBar.AlreadyAnswered)]
		[TestCase(WaterRiteBar.PouredTooRecently)]
		[TestCase(WaterRiteBar.StoresCannotBear)]
		public void BarLine_EveryBarSaysWhy(WaterRiteBar bar)
		{
			Assert.IsNotEmpty(KingdomWaterRiteRules.BarLine(bar, "Vashti", "the Barathrumites", 14, 3));
		}

		[Test]
		public void BarLine_ReadyHasNothingToSay()
		{
			Assert.AreEqual("", KingdomWaterRiteRules.BarLine(WaterRiteBar.Ready, "Vashti", "the Barathrumites", 14, 3));
		}

		[Test]
		public void BarLine_TheStoresRefusalNamesBothTheCostAndWhatIsThere()
		{
			string line = KingdomWaterRiteRules.BarLine(WaterRiteBar.StoresCannotBear, "Vashti", "the Barathrumites", 14, 3);
			Assert.IsTrue(line.Contains("14"));
			Assert.IsTrue(line.Contains("3"));
		}

		[TestCase(WaterRiteBar.NothingBetweenYou)]
		[TestCase(WaterRiteBar.TheirOffice)]
		[TestCase(WaterRiteBar.NoRoadOut)]
		[TestCase(WaterRiteBar.AskedTooOften)]
		[TestCase(WaterRiteBar.AlreadyAnswered)]
		public void BarLine_EveryBarAboutAPersonNamesThePerson(WaterRiteBar bar)
		{
			Assert.IsTrue(KingdomWaterRiteRules.BarLine(bar, "Vashti", "the Barathrumites", 14, 3).Contains("Vashti"));
		}

		[TestCase(WaterRiteAnswer.TooNew)]
		[TestCase(WaterRiteAnswer.RivalShrine)]
		[TestCase(WaterRiteAnswer.Devout)]
		[TestCase(WaterRiteAnswer.TooBitter)]
		[TestCase(WaterRiteAnswer.Steadfast)]
		public void RefusalNotice_EveryRefusalNamesThePersonAndIsWorthReading(WaterRiteAnswer answer)
		{
			string text = KingdomWaterRiteRules.RefusalNotice(answer, "Vashti", "the Putus Templar", "the Barathrumites", "the Mechanimists");
			Assert.IsTrue(text.Contains("Vashti"));
			Assert.Greater(text.Length, 160);
		}

		[Test]
		public void RefusalNotice_NoTwoRefusalsReadAlike()
		{
			string tooNew = KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.TooNew, "Vashti", "a", "b", "c");
			string shrine = KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.RivalShrine, "Vashti", "a", "b", "c");
			string devout = KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.Devout, "Vashti", "a", "b", "c");
			string bitter = KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.TooBitter, "Vashti", "a", "b", "c");
			string steadfast = KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.Steadfast, "Vashti", "a", "b", "c");
			Assert.AreNotEqual(tooNew, shrine);
			Assert.AreNotEqual(shrine, devout);
			Assert.AreNotEqual(devout, bitter);
			Assert.AreNotEqual(bitter, steadfast);
			Assert.AreNotEqual(tooNew, steadfast);
		}

		[Test]
		public void RefusalNotice_TheShrineRefusalNamesWhatTheShrineIsConsecratedTo()
		{
			Assert.IsTrue(KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.RivalShrine, "Vashti", "the Putus Templar", "the Barathrumites", "the Mechanimists").Contains("the Mechanimists"));
		}

		[Test]
		public void RefusalNotice_AnAcceptanceIsNotARefusal()
		{
			Assert.AreEqual("", KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.Accepted, "Vashti", "a", "b", "c"));
		}

		[TestCase(WaterRiteAnswer.TooNew)]
		[TestCase(WaterRiteAnswer.RivalShrine)]
		[TestCase(WaterRiteAnswer.Devout)]
		[TestCase(WaterRiteAnswer.TooBitter)]
		[TestCase(WaterRiteAnswer.Steadfast)]
		public void RefusalTelling_EveryRefusalIsChronicledByNameAndWithoutAClosingPeriod(WaterRiteAnswer answer)
		{
			string telling = KingdomWaterRiteRules.RefusalTelling(answer, "Vashti", "Kavvat");
			Assert.IsTrue(telling.Contains("Vashti"));
			Assert.IsTrue(telling.Contains("Kavvat"));
			Assert.IsFalse(telling.EndsWith("."));
		}

		[Test]
		public void RefusalTelling_AnAcceptanceIsNotARefusal()
		{
			// An acceptance is chronicled by KingdomConversion.Convert, which is the one path every
			// conversion in the mod takes; a second telling written here would put two accounts of
			// one night into the book.
			Assert.AreEqual("", KingdomWaterRiteRules.RefusalTelling(WaterRiteAnswer.Accepted, "Vashti", "Kavvat"));
		}

		[Test]
		public void BothRegistersDisagree_AndNeitherIsTheOtherWithTheColourStrippedOut()
		{
			string official = KingdomWaterRiteRules.RefusalTelling(WaterRiteAnswer.TooBitter, "Vashti", "Kavvat");
			string road = KingdomWaterRiteRules.RefusalRumour("Vashti", "Kavvat", "Ptoh");
			Assert.AreNotEqual(official, road);
			Assert.IsTrue(road.Contains("Ptoh"));
			Assert.AreNotEqual(KingdomWaterRiteRules.ClosedTelling("Vashti", "Kavvat"), KingdomWaterRiteRules.ClosedRumour("Vashti", "Kavvat", "Ptoh"));
		}

		[TestCase("Vashti", "Kavvat", "Ptoh")]
		public void RumourLines_NeverSpeakToTheFounderInTheSecondPerson(string name, string city, string founder)
		{
			// The rumour register is rewritten by KingdomRules.ToThirdPerson, which turns the word
			// "you" into the founder's name wherever it finds it. An authored rumour containing one
			// would put the founder's own voice into the register that exists to argue with it.
			string[] rumours = new string[2]
			{
				KingdomWaterRiteRules.RefusalRumour(name, city, founder),
				KingdomWaterRiteRules.ClosedRumour(name, city, founder)
			};
			for (int i = 0; i < rumours.Length; i++)
			{
				Assert.IsFalse(rumours[i].Contains("you "), rumours[i]);
				Assert.IsFalse(rumours[i].Contains("your "), rumours[i]);
				Assert.IsFalse(rumours[i].Contains("You "), rumours[i]);
				Assert.IsFalse(rumours[i].Contains("Your "), rumours[i]);
			}
		}

		[Test]
		public void OfferPrompt_NamesThePriceAndSaysPlainlyThatItIsSpentEitherWay()
		{
			string prompt = KingdomWaterRiteRules.OfferPrompt("Vashti", "the Putus Templar", "the Barathrumites", "Kavvat", 14);
			Assert.IsTrue(prompt.Contains("14 drams"));
			Assert.IsTrue(prompt.Contains("Vashti"));
			Assert.IsTrue(prompt.Contains("Kavvat"));
			Assert.IsTrue(prompt.Contains("either way"));
		}

		[Test]
		public void PressedWarning_StatesTheConsequenceBeforeItIsBought_AndDoesNotPromiseALeavingThatIsNotComing()
		{
			string road = KingdomWaterRiteRules.PressedWarning("Vashti", WillTakeTheRoad: true);
			string stays = KingdomWaterRiteRules.PressedWarning("Vashti", WillTakeTheRoad: false);
			Assert.IsTrue(road.Contains("Vashti"));
			Assert.IsTrue(stays.Contains("Vashti"));
			Assert.AreNotEqual(road, stays);
			Assert.IsTrue(road.Contains("road"));
			Assert.IsFalse(stays.Contains("road"));
		}

		[Test]
		public void ClosedNotice_TellsTheTruthAboutWhichOfTheTwoThingsIsAboutToHappen()
		{
			string road = KingdomWaterRiteRules.ClosedNotice("Vashti", "Kavvat", WillTakeTheRoad: true);
			string stays = KingdomWaterRiteRules.ClosedNotice("Vashti", "Kavvat", WillTakeTheRoad: false);
			Assert.AreNotEqual(road, stays);
			Assert.IsTrue(road.Contains("Vashti"));
			Assert.IsTrue(stays.Contains("Vashti"));
			Assert.IsTrue(stays.Contains("last time"));
		}

		[Test]
		public void ClosedLines_NameThePersonAndTheCityAndWhatCanStillBeDone()
		{
			Assert.IsTrue(KingdomWaterRiteRules.ClosedTelling("Vashti", "Kavvat").Contains("Vashti"));
			Assert.IsTrue(KingdomWaterRiteRules.ClosedTelling("Vashti", "Kavvat").Contains("Kavvat"));
			Assert.IsFalse(KingdomWaterRiteRules.ClosedTelling("Vashti", "Kavvat").EndsWith("."));
			string note = KingdomWaterRiteRules.ClosedNote("Vashti", "the Barathrumites");
			Assert.IsTrue(note.Contains("Vashti"));
			Assert.IsTrue(note.Contains("the Barathrumites"));
		}

		[Test]
		public void RowLabel_AShutRowIsGreyedAndAnOpenRowNamesThePrice()
		{
			string open = KingdomWaterRiteRules.RowLabel("Vashti", "the Putus Templar", 14, WaterRiteBar.Ready, Pressed: false);
			string shut = KingdomWaterRiteRules.RowLabel("Vashti", "the Putus Templar", 14, WaterRiteBar.AlreadyAnswered, Pressed: false);
			Assert.IsTrue(open.Contains("14 drams"));
			Assert.IsFalse(shut.Contains("14 drams"));
			Assert.IsTrue(shut.StartsWith("{{K|"));
			Assert.IsTrue(open.Contains("Vashti"));
			Assert.IsTrue(shut.Contains("Vashti"));
		}

		[Test]
		public void RowLabel_ASettlerOneAskingFromTheEndIsMarkedBeforeTheFounderClicksThem()
		{
			string pressed = KingdomWaterRiteRules.RowLabel("Vashti", "the Putus Templar", 14, WaterRiteBar.Ready, Pressed: true);
			string plain = KingdomWaterRiteRules.RowLabel("Vashti", "the Putus Templar", 14, WaterRiteBar.Ready, Pressed: false);
			Assert.AreNotEqual(pressed, plain);
			Assert.IsTrue(pressed.Contains("{{r|"));
		}

		[Test]
		public void RowLabel_ASettlerWhoHoldsNothingIsSaidToHoldNothing()
		{
			Assert.IsTrue(KingdomWaterRiteRules.RowLabel("Vashti", null, 14, WaterRiteBar.Ready, Pressed: false).Contains("nothing in particular"));
		}

		[Test]
		public void EveryLine_FallsBackToAPersonRatherThanToBlankWhenTheRollCarriesNoName()
		{
			Assert.IsTrue(KingdomWaterRiteRules.AcceptNotice(null, null).Contains("a settler"));
			Assert.IsTrue(KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.TooNew, "", null, null, null).Contains("a settler"));
			Assert.IsTrue(KingdomWaterRiteRules.RefusalTelling(WaterRiteAnswer.TooNew, null, null).Contains("a settler"));
			Assert.IsTrue(KingdomWaterRiteRules.ClosedTelling(null, null).Contains("a settler"));
			Assert.IsTrue(KingdomWaterRiteRules.ClosedNote(null, null).Contains("a settler"));
			Assert.IsTrue(KingdomWaterRiteRules.RowLabel(null, null, 8, WaterRiteBar.Ready, Pressed: false).Contains("a settler"));
		}
	}
}
#endif
