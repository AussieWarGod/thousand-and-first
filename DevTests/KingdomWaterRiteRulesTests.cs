#if TAF_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
			ClassicAssert.AreEqual(typeof(int), Enum.GetUnderlyingType(type), type.Name + " backing type");
			ClassicAssert.AreEqual("ThousandAndFirst." + type.Name, type.FullName);
			ClassicAssert.IsTrue(type.IsPublic, type.Name + " accessibility changed");
			ClassicAssert.IsFalse(type.IsNested, type.Name + " became nested");
			string[] names = Enum.GetNames(type);
			string[] actual = new string[names.Length];
			for (int i = 0; i < names.Length; i++)
				actual[i] = names[i] + "=" + Convert.ToInt32(Enum.Parse(type, names[i]));
			CollectionAssert.AreEqual(expected, actual, type.Name + " values/order");
		}

		private static void AssertPublicReadonlyStruct(Type type, string[] names, Type[] types)
		{
			ClassicAssert.AreEqual("ThousandAndFirst." + type.Name, type.FullName);
			ClassicAssert.IsTrue(type.IsPublic && type.IsValueType && !type.IsNested);
			FieldInfo[] fields = type.GetFields(PublicInstanceFields);
			ClassicAssert.AreEqual(names.Length, fields.Length, type.Name + " field count");
			object value = Activator.CreateInstance(type);
			for (int i = 0; i < fields.Length; i++)
			{
				ClassicAssert.AreEqual(names[i], fields[i].Name, type.Name + " field order at " + i);
				ClassicAssert.AreEqual(types[i], fields[i].FieldType, type.Name + "." + fields[i].Name + " type");
				ClassicAssert.IsTrue(fields[i].IsInitOnly, type.Name + "." + fields[i].Name + " stopped being readonly");
				object expected = types[i].IsValueType ? Activator.CreateInstance(types[i]) : null;
				ClassicAssert.AreEqual(expected, fields[i].GetValue(value), type.Name + "." + fields[i].Name + " default");
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
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomWaterRiteRules", typeof(KingdomWaterRiteRules).FullName);
			ClassicAssert.IsTrue(typeof(KingdomWaterRiteRules).IsPublic);
			ClassicAssert.IsTrue(typeof(KingdomWaterRiteRules).IsAbstract && typeof(KingdomWaterRiteRules).IsSealed);
			ClassicAssert.AreEqual(24, KingdomWaterRiteRules.CovenantDistance);
			ClassicAssert.AreEqual(16, KingdomWaterRiteRules.CreedHeldDistance);
			ClassicAssert.AreEqual(30, KingdomWaterRiteRules.RivalShrineDistance);
			ClassicAssert.AreEqual(20, KingdomWaterRiteRules.DevotionDistance);
			ClassicAssert.AreEqual(4, KingdomWaterRiteRules.ReachPerSharedPass);
			ClassicAssert.AreEqual(140, KingdomWaterRiteRules.ReachCap);
			ClassicAssert.AreEqual(35, KingdomWaterRiteRules.SharedPassesForFullReach);
			ClassicAssert.AreEqual(105, KingdomWaterRiteRules.MaxCountedDays);
			ClassicAssert.AreEqual(4, KingdomWaterRiteRules.DistancePerDram);
			ClassicAssert.AreEqual(3, KingdomWaterRiteRules.RefusalsBeforeAskingCloses);
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
			ClassicAssert.AreEqual(KingdomWaterRiteRules.CovenantDistance, KingdomWaterRiteRules.Distance(Facts(HoldsACreed: false)));
		}

		[Test]
		public void Distance_HoldingACreedOfTheirOwnIsFurtherThanHoldingNone()
		{
			int none = KingdomWaterRiteRules.Distance(Facts(HoldsACreed: false));
			ClassicAssert.AreEqual(none + KingdomWaterRiteRules.CreedHeldDistance, KingdomWaterRiteRules.Distance(Facts(HoldsACreed: true)));
		}

		[Test]
		public void Distance_HostilityIsAddedInTheFactionTablesOwnUnits()
		{
			int calm = KingdomWaterRiteRules.Distance(Facts(Hostility: 0));
			ClassicAssert.AreEqual(calm + 50, KingdomWaterRiteRules.Distance(Facts(Hostility: 50)));
			ClassicAssert.AreEqual(calm + 100, KingdomWaterRiteRules.Distance(Facts(Hostility: 100)));
		}

		[TestCase(-500, 0)]
		[TestCase(-1, 0)]
		[TestCase(0, 0)]
		[TestCase(100, 100)]
		[TestCase(400, 100)]
		public void Distance_HostilityFromThirdPartyDataIsClampedRatherThanTrusted(int given, int counted)
		{
			int expected = KingdomWaterRiteRules.CovenantDistance + KingdomWaterRiteRules.CreedHeldDistance + counted;
			ClassicAssert.AreEqual(expected, KingdomWaterRiteRules.Distance(Facts(Hostility: given)));
		}

		[Test]
		public void Distance_ARivalShrineInTheirQuarterAddsItsOwnConstant()
		{
			int without = KingdomWaterRiteRules.Distance(Facts());
			ClassicAssert.AreEqual(without + KingdomWaterRiteRules.RivalShrineDistance, KingdomWaterRiteRules.Distance(Facts(RivalShrine: true)));
		}

		[Test]
		public void Distance_DevotionIsAlwaysACostAndNeverADiscount()
		{
			int plain = KingdomWaterRiteRules.Distance(Facts());
			int devout = KingdomWaterRiteRules.Distance(Facts(Devout: true));
			ClassicAssert.Greater(devout, plain);
			ClassicAssert.AreEqual(plain + KingdomWaterRiteRules.DevotionDistance, devout);
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
			ClassicAssert.AreEqual(expected, KingdomWaterRiteRules.Reach(days));
		}

		[Test]
		public void Reach_StopsAtTheCapAndStaysThere()
		{
			ClassicAssert.AreEqual(KingdomWaterRiteRules.ReachCap, KingdomWaterRiteRules.Reach(KingdomWaterRiteRules.MaxCountedDays));
			ClassicAssert.AreEqual(KingdomWaterRiteRules.ReachCap, KingdomWaterRiteRules.Reach(KingdomWaterRiteRules.MaxCountedDays + 500));
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
			ClassicAssert.AreEqual(KingdomWaterRiteRules.ReachCap, KingdomWaterRiteRules.Distance(Facts(Hostility: 100)));
			ClassicAssert.AreEqual(WaterRiteAnswer.Accepted, KingdomWaterRiteRules.Answer(Facts(Hostility: 100, SharedDays: KingdomWaterRiteRules.MaxCountedDays)));
			ClassicAssert.AreNotEqual(WaterRiteAnswer.Accepted, KingdomWaterRiteRules.Answer(Facts(Hostility: 100, SharedDays: KingdomWaterRiteRules.MaxCountedDays - 1)));
		}

		// --- The answer: one branch per obstacle, ordered by what the founder can do about it.

		[Test]
		public void Answer_AcceptsOnceTheSharedLifeCoversTheDistance()
		{
			ClassicAssert.AreEqual(WaterRiteAnswer.Accepted, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: 30)));
		}

		[Test]
		public void Answer_ARefusesTagBeatsEverything_EvenAWholeSharedLife()
		{
			ClassicAssert.AreEqual(WaterRiteAnswer.Accepted, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: KingdomWaterRiteRules.MaxCountedDays)));
			ClassicAssert.AreEqual(WaterRiteAnswer.Steadfast, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: KingdomWaterRiteRules.MaxCountedDays, Steadfast: true)));
		}

		[Test]
		public void Answer_NamesTheShrineWhenTakingItDownWouldByItselfHaveChangedTheAnswer()
		{
			// Thirty days reaches 40, which is the distance without the shrine; with it, 70.
			ClassicAssert.AreEqual(WaterRiteAnswer.RivalShrine, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: 30, RivalShrine: true)));
			ClassicAssert.AreEqual(WaterRiteAnswer.Accepted, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: 30)));
		}

		[Test]
		public void Answer_DoesNotNameTheShrineWhenRemovingItAloneWouldNotHaveHelped()
		{
			// Three days reaches 4 against a distance of 70, so the shrine is not what is standing
			// in the way and naming it would be a lie the founder would act on (7b).
			ClassicAssert.AreEqual(WaterRiteAnswer.TooNew, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: 3, RivalShrine: true)));
		}

		[Test]
		public void Answer_NamesDevotionWhenThatAloneIsWhatIsInTheWay()
		{
			// Thirty days reaches 40, the distance without the devotion; with it, 60.
			ClassicAssert.AreEqual(WaterRiteAnswer.Devout, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: 30, Devout: true)));
		}

		[Test]
		public void Answer_TheShrineIsNamedBeforeTheDevotion_BecauseOneCanBeActedOnToday()
		{
			// Distance 90 against a reach of 80: taking down the shrine or setting aside the
			// devotion would each have closed it, and only one of the two is the founder's to do.
			ClassicAssert.AreEqual(WaterRiteAnswer.RivalShrine, KingdomWaterRiteRules.Answer(Facts(Hostility: 0, SharedDays: 60, RivalShrine: true, Devout: true)));
		}

		[Test]
		public void Answer_TooNewWhenALongerSharedLifeWouldEventuallyDoIt()
		{
			ClassicAssert.AreEqual(WaterRiteAnswer.TooNew, KingdomWaterRiteRules.Answer(Facts(Hostility: 50, SharedDays: 3)));
		}

		[Test]
		public void Answer_TooBitterWhenNoSharedLifeCouldEverCoverIt()
		{
			// A fault line with a rival shrine on top of it is past the cap: no number of passes
			// reaches it, and the honest answer is that one of the two creeds has to move.
			ClassicAssert.AreEqual(WaterRiteAnswer.TooBitter, KingdomWaterRiteRules.Answer(Facts(Hostility: 100, SharedDays: 1, RivalShrine: true)));
		}

		[Test]
		public void Answer_IsAPureFunctionOfTheFacts_AskedTwiceItSaysTheSameThing()
		{
			WaterRiteFacts facts = Facts(Hostility: 50, SharedDays: 7, RivalShrine: true, Devout: true);
			ClassicAssert.AreEqual(KingdomWaterRiteRules.Answer(facts), KingdomWaterRiteRules.Answer(facts));
		}

		[Test]
		public void Converted_IsTrueForAcceptanceAndForNothingElse()
		{
			ClassicAssert.IsTrue(KingdomWaterRiteRules.Converted(WaterRiteAnswer.Accepted));
			ClassicAssert.IsFalse(KingdomWaterRiteRules.Converted(WaterRiteAnswer.TooNew));
			ClassicAssert.IsFalse(KingdomWaterRiteRules.Converted(WaterRiteAnswer.RivalShrine));
			ClassicAssert.IsFalse(KingdomWaterRiteRules.Converted(WaterRiteAnswer.Devout));
			ClassicAssert.IsFalse(KingdomWaterRiteRules.Converted(WaterRiteAnswer.TooBitter));
			ClassicAssert.IsFalse(KingdomWaterRiteRules.Converted(WaterRiteAnswer.Steadfast));
		}

		// --- Needed days: the door a "not yet" leaves open, and it really does open.

		[TestCase(4, 3)]
		[TestCase(5, 4)]
		[TestCase(8, 6)]
		[TestCase(40, 30)]
		public void NeededDays_RoundsUp_SoTheNamedDayActuallyCoversTheDistance(int distance, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomWaterRiteRules.NeededDays(distance));
		}

		[Test]
		public void NeededDays_IsZeroWhenNoSharedLifeWouldEverCoverIt()
		{
			ClassicAssert.AreEqual(0, KingdomWaterRiteRules.NeededDays(KingdomWaterRiteRules.ReachCap + 1));
			ClassicAssert.AreNotEqual(0, KingdomWaterRiteRules.NeededDays(KingdomWaterRiteRules.ReachCap));
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
			ClassicAssert.Greater(needed, 0);
			ClassicAssert.AreEqual(WaterRiteAnswer.Accepted, KingdomWaterRiteRules.Answer(Facts(Hostility: hostility, SharedDays: needed, RivalShrine: shrine, Devout: devout)));
			ClassicAssert.AreNotEqual(WaterRiteAnswer.Accepted, KingdomWaterRiteRules.Answer(Facts(Hostility: hostility, SharedDays: needed - 1, RivalShrine: shrine, Devout: devout)));
		}

		// --- The price: the founding basin, held again, for one person.

		[Test]
		public void Cost_IsTheFoundingBasinPlusAMeasureForWhatIsInTheWay()
		{
			ClassicAssert.AreEqual(KingdomRules.FoundingCostDrams, KingdomWaterRiteRules.Cost(0));
			ClassicAssert.AreEqual(KingdomRules.FoundingCostDrams + 1, KingdomWaterRiteRules.Cost(KingdomWaterRiteRules.DistancePerDram));
		}

		[Test]
		public void Cost_NeverFallsBelowTheBasin_EvenOnNonsenseInput()
		{
			ClassicAssert.AreEqual(KingdomRules.FoundingCostDrams, KingdomWaterRiteRules.Cost(-100));
		}

		[Test]
		public void Cost_RisesWithWhatStandsInTheWay()
		{
			ClassicAssert.Greater(
				KingdomWaterRiteRules.Cost(KingdomWaterRiteRules.Distance(Facts(Hostility: 100))),
				KingdomWaterRiteRules.Cost(KingdomWaterRiteRules.Distance(Facts(Hostility: 0))));
		}

		// --- Asked once, and not again until something is different.

		[Test]
		public void SomethingChanged_IsFalseWhenTheFounderSimplyAsksTheSameQuestionAgain()
		{
			WaterRiteFacts facts = Facts(Hostility: 50, SharedDays: 3);
			ClassicAssert.IsFalse(KingdomWaterRiteRules.SomethingChanged(StampAt(facts), facts));
		}

		[Test]
		public void SomethingChanged_IsFalseWhenTheyLivedOneMorePassButNotEnoughOfThem()
		{
			WaterRiteFacts then = Facts(Hostility: 50, SharedDays: 3);
			ClassicAssert.IsFalse(KingdomWaterRiteRules.SomethingChanged(StampAt(then), Facts(Hostility: 50, SharedDays: 4)));
		}

		[Test]
		public void SomethingChanged_OpensWhenTheSharedLifeHasGrownLongEnoughToCoverTheDistance()
		{
			WaterRiteFacts then = Facts(Hostility: 50, SharedDays: 3);
			WaterRiteStamp stamp = StampAt(then);
			ClassicAssert.IsTrue(KingdomWaterRiteRules.SomethingChanged(stamp, Facts(Hostility: 50, SharedDays: stamp.NeededDays)));
		}

		[Test]
		public void SomethingChanged_OpensWhenTheQuarrelHasEased()
		{
			WaterRiteFacts then = Facts(Hostility: 100, SharedDays: 3);
			ClassicAssert.IsTrue(KingdomWaterRiteRules.SomethingChanged(StampAt(then), Facts(Hostility: 50, SharedDays: 3)));
		}

		[Test]
		public void SomethingChanged_DoesNotOpenWhenTheQuarrelGotWorse()
		{
			WaterRiteFacts then = Facts(Hostility: 50, SharedDays: 3);
			ClassicAssert.IsFalse(KingdomWaterRiteRules.SomethingChanged(StampAt(then), Facts(Hostility: 100, SharedDays: 3)));
		}

		[Test]
		public void SomethingChanged_OpensWhenTheRivalShrineIsGone()
		{
			WaterRiteFacts then = Facts(Hostility: 50, SharedDays: 3, RivalShrine: true);
			ClassicAssert.IsTrue(KingdomWaterRiteRules.SomethingChanged(StampAt(then), Facts(Hostility: 50, SharedDays: 3)));
		}

		[Test]
		public void SomethingChanged_DoesNotOpenWhenAShrineAppearsWhereThereWasNone()
		{
			WaterRiteFacts then = Facts(Hostility: 50, SharedDays: 3);
			ClassicAssert.IsFalse(KingdomWaterRiteRules.SomethingChanged(StampAt(then), Facts(Hostility: 50, SharedDays: 3, RivalShrine: true)));
		}

		[Test]
		public void SomethingChanged_OpensWheneverTheRealmBelievesSomethingElse()
		{
			WaterRiteFacts then = Facts(Hostility: 50, SharedDays: 3);
			ClassicAssert.IsTrue(KingdomWaterRiteRules.SomethingChanged(StampAt(then), Facts(Hostility: 50, SharedDays: 3, RealmCreed: "Joppa")));
		}

		[Test]
		public void SomethingChanged_ASteadfastRefusalIsReopenedOnlyByTheRealmBelievingSomethingElse()
		{
			WaterRiteStamp stamp = StampAt(Facts(Hostility: 100, SharedDays: 1, RivalShrine: true, Steadfast: true));
			ClassicAssert.IsTrue(stamp.Absolute);
			ClassicAssert.IsFalse(KingdomWaterRiteRules.SomethingChanged(stamp, Facts(Hostility: 0, SharedDays: KingdomWaterRiteRules.MaxCountedDays, Steadfast: true)));
			ClassicAssert.IsTrue(KingdomWaterRiteRules.SomethingChanged(stamp, Facts(Hostility: 100, SharedDays: 1, RivalShrine: true, Steadfast: true, RealmCreed: "Joppa")));
		}

		[Test]
		public void StampFor_MarksOnlyASteadfastRefusalAbsolute()
		{
			ClassicAssert.IsFalse(KingdomWaterRiteRules.StampFor(Facts(Hostility: 100), WaterRiteAnswer.TooBitter).Absolute);
			ClassicAssert.IsFalse(KingdomWaterRiteRules.StampFor(Facts(), WaterRiteAnswer.TooNew).Absolute);
			ClassicAssert.IsTrue(KingdomWaterRiteRules.StampFor(Facts(Steadfast: true), WaterRiteAnswer.Steadfast).Absolute);
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
			ClassicAssert.AreEqual(same, KingdomWaterRiteRules.SameCreed(a, b));
			ClassicAssert.AreEqual(same, KingdomWaterRiteRules.SameCreed(b, a));
		}

		// --- Shared living: the days somebody has actually lived here.

		[TestCase(-1, 1, 1)]
		[TestCase(0, 1, 1)]
		[TestCase(5, 1, 6)]
		[TestCase(5, 12, 17)]
		public void SharedDaysAfter_AdvancesByExactlyTheDaysLived(int before, int days, int after)
		{
			ClassicAssert.AreEqual(after, KingdomWaterRiteRules.SharedDaysAfter(before, days));
		}

		[Test]
		public void SharedDaysAfter_ANonPositiveStretchChangesNothing()
		{
			ClassicAssert.AreEqual(5, KingdomWaterRiteRules.SharedDaysAfter(5, 0));
			ClassicAssert.AreEqual(5, KingdomWaterRiteRules.SharedDaysAfter(5, -7));
			ClassicAssert.AreEqual(0, KingdomWaterRiteRules.SharedDaysAfter(-3, 0), "a negative reads as none");
		}

		[Test]
		public void SharedDaysAfter_StopsWhereTheReachStopsMeaningAnything()
		{
			ClassicAssert.AreEqual(KingdomWaterRiteRules.MaxCountedDays, KingdomWaterRiteRules.SharedDaysAfter(KingdomWaterRiteRules.MaxCountedDays, 1));
			ClassicAssert.AreEqual(KingdomWaterRiteRules.MaxCountedDays, KingdomWaterRiteRules.SharedDaysAfter(0, KingdomWaterRiteRules.MaxCountedDays + 9));
			ClassicAssert.AreEqual(KingdomWaterRiteRules.MaxCountedDays, KingdomWaterRiteRules.SharedDaysAfter(0, 1000000000),
				"a thousand days and a hundred and five arrive at the same place");
			ClassicAssert.AreEqual(KingdomWaterRiteRules.ReachCap, KingdomWaterRiteRules.Reach(KingdomWaterRiteRules.MaxCountedDays));
		}

		[Test]
		public void SharedLivingHoldsItsPaceAcrossTheChangeOfUnit()
		{
			// The recalibration, from the founder's side: three cohabited days buy exactly the
			// four of reach one attended pass used to, and a hundred and five buy the whole of
			// what thirty-five visits bought. If either drifts, the water rite silently became a
			// different arc.
			ClassicAssert.AreEqual(KingdomWaterRiteRules.ReachPerSharedPass,
				KingdomWaterRiteRules.Reach(KingdomBrinkRules.CohabitationDaysPerAttendedPass));
			ClassicAssert.AreEqual(KingdomWaterRiteRules.ReachCap,
				KingdomWaterRiteRules.Reach(KingdomBrinkRules.InCohabitationDays(KingdomWaterRiteRules.SharedPassesForFullReach)));
		}

		[Test]
		public void Reach_NeverFallsAsTheDaysRise()
		{
			int last = 0;
			for (int days = 0; days <= KingdomWaterRiteRules.MaxCountedDays + 5; days++)
			{
				int reach = KingdomWaterRiteRules.Reach(days);
				ClassicAssert.GreaterOrEqual(reach, last, "a day lived here can never take reach away");
				ClassicAssert.LessOrEqual(reach, KingdomWaterRiteRules.ReachCap);
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
			ClassicAssert.IsFalse(KingdomConversionRules.IsImposed(ConversionChannel.Diplomacy));
			ClassicAssert.IsTrue(KingdomConversionRules.IsImposed(ConversionChannel.Shrine));
		}

		[TestCase(0, false)]
		[TestCase(1, false)]
		[TestCase(2, false)]
		[TestCase(3, true)]
		[TestCase(9, true)]
		public void AskedTooOften_FiresOnlyOnceTheyHaveRefusedTheNamedNumberOfTimes(int refusals, bool closed)
		{
			ClassicAssert.AreEqual(refusals >= KingdomWaterRiteRules.RefusalsBeforeAskingCloses, closed);
			ClassicAssert.AreEqual(closed, KingdomWaterRiteRules.AskedTooOften(refusals));
		}

		[TestCase(-4, 1)]
		[TestCase(0, 1)]
		[TestCase(1, 2)]
		public void RefusalsAfter_CountsOneMore(int before, int after)
		{
			ClassicAssert.AreEqual(after, KingdomWaterRiteRules.RefusalsAfter(before));
		}

		[Test]
		public void RefusalsAfter_ClampsAtTheThreshold_BecausePastItTheCountStopsMeaningAnything()
		{
			ClassicAssert.AreEqual(KingdomWaterRiteRules.RefusalsBeforeAskingCloses, KingdomWaterRiteRules.RefusalsAfter(KingdomWaterRiteRules.RefusalsBeforeAskingCloses));
			ClassicAssert.AreEqual(KingdomWaterRiteRules.RefusalsBeforeAskingCloses, KingdomWaterRiteRules.RefusalsAfter(KingdomWaterRiteRules.RefusalsBeforeAskingCloses + 20));
		}

		[Test]
		public void ARefusalCountedThreeTimesIsExactlyWhatClosesTheAsking()
		{
			int refusals = 0;
			for (int i = 0; i < KingdomWaterRiteRules.RefusalsBeforeAskingCloses; i++)
			{
				ClassicAssert.IsFalse(KingdomWaterRiteRules.AskedTooOften(refusals));
				refusals = KingdomWaterRiteRules.RefusalsAfter(refusals);
			}
			ClassicAssert.IsTrue(KingdomWaterRiteRules.AskedTooOften(refusals));
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
			ClassicAssert.IsNotEmpty(KingdomWaterRiteRules.BarLine(bar, "Vashti", "the Barathrumites", 14, 3));
		}

		[Test]
		public void BarLine_ReadyHasNothingToSay()
		{
			ClassicAssert.AreEqual("", KingdomWaterRiteRules.BarLine(WaterRiteBar.Ready, "Vashti", "the Barathrumites", 14, 3));
		}

		[Test]
		public void BarLine_TheStoresRefusalNamesBothTheCostAndWhatIsThere()
		{
			string line = KingdomWaterRiteRules.BarLine(WaterRiteBar.StoresCannotBear, "Vashti", "the Barathrumites", 14, 3);
			ClassicAssert.IsTrue(line.Contains("14"));
			ClassicAssert.IsTrue(line.Contains("3"));
		}

		[TestCase(WaterRiteBar.NothingBetweenYou)]
		[TestCase(WaterRiteBar.TheirOffice)]
		[TestCase(WaterRiteBar.NoRoadOut)]
		[TestCase(WaterRiteBar.AskedTooOften)]
		[TestCase(WaterRiteBar.AlreadyAnswered)]
		public void BarLine_EveryBarAboutAPersonNamesThePerson(WaterRiteBar bar)
		{
			ClassicAssert.IsTrue(KingdomWaterRiteRules.BarLine(bar, "Vashti", "the Barathrumites", 14, 3).Contains("Vashti"));
		}

		[TestCase(WaterRiteAnswer.TooNew)]
		[TestCase(WaterRiteAnswer.RivalShrine)]
		[TestCase(WaterRiteAnswer.Devout)]
		[TestCase(WaterRiteAnswer.TooBitter)]
		[TestCase(WaterRiteAnswer.Steadfast)]
		public void RefusalNotice_EveryRefusalNamesThePersonAndIsWorthReading(WaterRiteAnswer answer)
		{
			string text = KingdomWaterRiteRules.RefusalNotice(answer, "Vashti", "the Putus Templar", "the Barathrumites", "the Mechanimists");
			ClassicAssert.IsTrue(text.Contains("Vashti"));
			ClassicAssert.Greater(text.Length, 160);
		}

		[Test]
		public void RefusalNotice_NoTwoRefusalsReadAlike()
		{
			string tooNew = KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.TooNew, "Vashti", "a", "b", "c");
			string shrine = KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.RivalShrine, "Vashti", "a", "b", "c");
			string devout = KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.Devout, "Vashti", "a", "b", "c");
			string bitter = KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.TooBitter, "Vashti", "a", "b", "c");
			string steadfast = KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.Steadfast, "Vashti", "a", "b", "c");
			ClassicAssert.AreNotEqual(tooNew, shrine);
			ClassicAssert.AreNotEqual(shrine, devout);
			ClassicAssert.AreNotEqual(devout, bitter);
			ClassicAssert.AreNotEqual(bitter, steadfast);
			ClassicAssert.AreNotEqual(tooNew, steadfast);
		}

		[Test]
		public void RefusalNotice_TheShrineRefusalNamesWhatTheShrineIsConsecratedTo()
		{
			ClassicAssert.IsTrue(KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.RivalShrine, "Vashti", "the Putus Templar", "the Barathrumites", "the Mechanimists").Contains("the Mechanimists"));
		}

		[Test]
		public void RefusalNotice_AnAcceptanceIsNotARefusal()
		{
			ClassicAssert.AreEqual("", KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.Accepted, "Vashti", "a", "b", "c"));
		}

		[TestCase(WaterRiteAnswer.TooNew)]
		[TestCase(WaterRiteAnswer.RivalShrine)]
		[TestCase(WaterRiteAnswer.Devout)]
		[TestCase(WaterRiteAnswer.TooBitter)]
		[TestCase(WaterRiteAnswer.Steadfast)]
		public void RefusalTelling_EveryRefusalIsChronicledByNameAndWithoutAClosingPeriod(WaterRiteAnswer answer)
		{
			string telling = KingdomWaterRiteRules.RefusalTelling(answer, "Vashti", "Kavvat");
			ClassicAssert.IsTrue(telling.Contains("Vashti"));
			ClassicAssert.IsTrue(telling.Contains("Kavvat"));
			ClassicAssert.IsFalse(telling.EndsWith("."));
		}

		[Test]
		public void RefusalTelling_AnAcceptanceIsNotARefusal()
		{
			// An acceptance is chronicled by KingdomConversion.Convert, which is the one path every
			// conversion in the mod takes; a second telling written here would put two accounts of
			// one night into the book.
			ClassicAssert.AreEqual("", KingdomWaterRiteRules.RefusalTelling(WaterRiteAnswer.Accepted, "Vashti", "Kavvat"));
		}

		[Test]
		public void BothRegistersDisagree_AndNeitherIsTheOtherWithTheColourStrippedOut()
		{
			string official = KingdomWaterRiteRules.RefusalTelling(WaterRiteAnswer.TooBitter, "Vashti", "Kavvat");
			string road = KingdomWaterRiteRules.RefusalRumour("Vashti", "Kavvat", "Ptoh");
			ClassicAssert.AreNotEqual(official, road);
			ClassicAssert.IsTrue(road.Contains("Ptoh"));
			ClassicAssert.AreNotEqual(KingdomWaterRiteRules.ClosedTelling("Vashti", "Kavvat"), KingdomWaterRiteRules.ClosedRumour("Vashti", "Kavvat", "Ptoh"));
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
				ClassicAssert.IsFalse(rumours[i].Contains("you "), rumours[i]);
				ClassicAssert.IsFalse(rumours[i].Contains("your "), rumours[i]);
				ClassicAssert.IsFalse(rumours[i].Contains("You "), rumours[i]);
				ClassicAssert.IsFalse(rumours[i].Contains("Your "), rumours[i]);
			}
		}

		[Test]
		public void OfferPrompt_NamesThePriceAndSaysPlainlyThatItIsSpentEitherWay()
		{
			string prompt = KingdomWaterRiteRules.OfferPrompt("Vashti", "the Putus Templar", "the Barathrumites", "Kavvat", 14);
			ClassicAssert.IsTrue(prompt.Contains("14 drams"));
			ClassicAssert.IsTrue(prompt.Contains("Vashti"));
			ClassicAssert.IsTrue(prompt.Contains("Kavvat"));
			ClassicAssert.IsTrue(prompt.Contains("either way"));
		}

		[Test]
		public void PressedWarning_StatesTheConsequenceBeforeItIsBought_AndDoesNotPromiseALeavingThatIsNotComing()
		{
			string road = KingdomWaterRiteRules.PressedWarning("Vashti", WillTakeTheRoad: true);
			string stays = KingdomWaterRiteRules.PressedWarning("Vashti", WillTakeTheRoad: false);
			ClassicAssert.IsTrue(road.Contains("Vashti"));
			ClassicAssert.IsTrue(stays.Contains("Vashti"));
			ClassicAssert.AreNotEqual(road, stays);
			ClassicAssert.IsTrue(road.Contains("road"));
			ClassicAssert.IsFalse(stays.Contains("road"));
		}

		[Test]
		public void ClosedNotice_TellsTheTruthAboutWhichOfTheTwoThingsIsAboutToHappen()
		{
			string road = KingdomWaterRiteRules.ClosedNotice("Vashti", "Kavvat", WillTakeTheRoad: true);
			string stays = KingdomWaterRiteRules.ClosedNotice("Vashti", "Kavvat", WillTakeTheRoad: false);
			ClassicAssert.AreNotEqual(road, stays);
			ClassicAssert.IsTrue(road.Contains("Vashti"));
			ClassicAssert.IsTrue(stays.Contains("Vashti"));
			ClassicAssert.IsTrue(stays.Contains("last time"));
		}

		[Test]
		public void ClosedLines_NameThePersonAndTheCityAndWhatCanStillBeDone()
		{
			ClassicAssert.IsTrue(KingdomWaterRiteRules.ClosedTelling("Vashti", "Kavvat").Contains("Vashti"));
			ClassicAssert.IsTrue(KingdomWaterRiteRules.ClosedTelling("Vashti", "Kavvat").Contains("Kavvat"));
			ClassicAssert.IsFalse(KingdomWaterRiteRules.ClosedTelling("Vashti", "Kavvat").EndsWith("."));
			string note = KingdomWaterRiteRules.ClosedNote("Vashti", "the Barathrumites");
			ClassicAssert.IsTrue(note.Contains("Vashti"));
			ClassicAssert.IsTrue(note.Contains("the Barathrumites"));
		}

		[Test]
		public void RowLabel_AShutRowIsGreyedAndAnOpenRowNamesThePrice()
		{
			string open = KingdomWaterRiteRules.RowLabel("Vashti", "the Putus Templar", 14, WaterRiteBar.Ready, Pressed: false);
			string shut = KingdomWaterRiteRules.RowLabel("Vashti", "the Putus Templar", 14, WaterRiteBar.AlreadyAnswered, Pressed: false);
			ClassicAssert.IsTrue(open.Contains("14 drams"));
			ClassicAssert.IsFalse(shut.Contains("14 drams"));
			ClassicAssert.IsTrue(shut.StartsWith("{{K|"));
			ClassicAssert.IsTrue(open.Contains("Vashti"));
			ClassicAssert.IsTrue(shut.Contains("Vashti"));
		}

		[Test]
		public void RowLabel_ASettlerOneAskingFromTheEndIsMarkedBeforeTheFounderClicksThem()
		{
			string pressed = KingdomWaterRiteRules.RowLabel("Vashti", "the Putus Templar", 14, WaterRiteBar.Ready, Pressed: true);
			string plain = KingdomWaterRiteRules.RowLabel("Vashti", "the Putus Templar", 14, WaterRiteBar.Ready, Pressed: false);
			ClassicAssert.AreNotEqual(pressed, plain);
			ClassicAssert.IsTrue(pressed.Contains("{{r|"));
		}

		[Test]
		public void RowLabel_ASettlerWhoHoldsNothingIsSaidToHoldNothing()
		{
			ClassicAssert.IsTrue(KingdomWaterRiteRules.RowLabel("Vashti", null, 14, WaterRiteBar.Ready, Pressed: false).Contains("nothing in particular"));
		}

		[Test]
		public void EveryLine_FallsBackToAPersonRatherThanToBlankWhenTheRollCarriesNoName()
		{
			ClassicAssert.IsTrue(KingdomWaterRiteRules.AcceptNotice(null, null).Contains("a settler"));
			ClassicAssert.IsTrue(KingdomWaterRiteRules.RefusalNotice(WaterRiteAnswer.TooNew, "", null, null, null).Contains("a settler"));
			ClassicAssert.IsTrue(KingdomWaterRiteRules.RefusalTelling(WaterRiteAnswer.TooNew, null, null).Contains("a settler"));
			ClassicAssert.IsTrue(KingdomWaterRiteRules.ClosedTelling(null, null).Contains("a settler"));
			ClassicAssert.IsTrue(KingdomWaterRiteRules.ClosedNote(null, null).Contains("a settler"));
			ClassicAssert.IsTrue(KingdomWaterRiteRules.RowLabel(null, null, 8, WaterRiteBar.Ready, Pressed: false).Contains("a settler"));
		}
	}
}
#endif
