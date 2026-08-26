#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Api;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The city book as the founder reads it.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE §5: without this surface the model is invisible. The cases that
	/// matter are the ones where a wrong line would MISLEAD rather than merely read badly — a
	/// capacity of zero rendered as "0 of 0", a signed debt rendered with the wrong verb, a work
	/// the board calls running that the breakdown news calls stopped.
	/// </para>
	/// </summary>
	internal class KingdomBookRulesTests
	{
		private const string Here = "taf:zone:here";

		private static KingdomWorkReading Work(int condition, int crew, KingdomWorkClass workClass)
		{
			return new KingdomWorkReading(1, Here, "mill", condition, crew, workClass, 2, 44, 1200L);
		}

		/// <summary>A city with no dedicated vessels holds NOTHING, not "0 of 0". The protection
		/// law is why: undedicated stock is outside the model, and a zero ceiling means the founder
		/// has designated nothing, which is a different sentence from being empty.</summary>
		[Test]
		public void Pair_NoCeilingReadsAsNothingDedicated()
		{
			StringAssert.Contains("nothing dedicated", KingdomBookRules.Pair(new KingdomStockReading(0L, 0L)));
			Assert.AreEqual("12 of 60", KingdomBookRules.Pair(new KingdomStockReading(12L, 60L)));
		}

		/// <summary>The signed debt keeps its sign in prose: what is owed TO the ground and what is
		/// still to be drawn FROM it are opposite facts, and Addendum 12(d) turns on the
		/// difference.</summary>
		[Test]
		public void Owed_KeepsTheSignsMeaning()
		{
			string landing = KingdomBookRules.Owed(new KingdomZoneReading(Here,
				default(KingdomStockReading), default(KingdomStockReading), default(KingdomStockReading), 0, 0, 6, 0, 0, 0L));
			StringAssert.Contains("6 water still to land", landing);
			string drawing = KingdomBookRules.Owed(new KingdomZoneReading(Here,
				default(KingdomStockReading), default(KingdomStockReading), default(KingdomStockReading), 0, 0, -6, 0, 0, 0L));
			StringAssert.Contains("6 water still to draw", drawing);
		}

		/// <summary>A squared zone says nothing. The clause exists to report a disagreement, and a
		/// line that fired on agreement would be noise on every zone forever.</summary>
		[Test]
		public void Owed_ASquaredZoneIsSilent()
		{
			Assert.AreEqual("", KingdomBookRules.Owed(new KingdomZoneReading(Here,
				default(KingdomStockReading), default(KingdomStockReading), default(KingdomStockReading), 0, 0, 0, 0, 0, 0L)));
		}

		/// <summary>The board's "waiting" and the breakdown news' "stopped" are the SAME two
		/// clauses. Pinned against the happening rules' own predicate so the two can never
		/// drift.</summary>
		[TestCase(KingdomWorkClass.Producer, 0, true)]
		[TestCase(KingdomWorkClass.Refiner, 0, true)]
		[TestCase(KingdomWorkClass.Power, 0, true)]
		[TestCase(KingdomWorkClass.Store, 0, false)]
		[TestCase(KingdomWorkClass.Growing, 0, false)]
		[TestCase(KingdomWorkClass.Construction, 0, true)]
		[TestCase(KingdomWorkClass.Other, 0, false)]
		[TestCase(KingdomWorkClass.Producer, 2, false)]
		public void Waiting_AgreesWithTheBreakdownNews(KingdomWorkClass workClass, int crew, bool waiting)
		{
			Assert.AreEqual(waiting, !string.IsNullOrEmpty(KingdomBookRules.Waiting(Work(100, crew, workClass))));
		}

		/// <summary>Worn past the condemned line is waiting whatever its crew, and the line is the
		/// wear lane's own.</summary>
		[Test]
		public void Waiting_WornPastTheLineOutranksHavingACrew()
		{
			StringAssert.Contains("mending", KingdomBookRules.Waiting(
				Work(KingdomHappeningRules.BreakdownConditionFloor, 4, KingdomWorkClass.Producer)));
			Assert.AreEqual("", KingdomBookRules.Waiting(
				Work(KingdomHappeningRules.BreakdownConditionFloor + 1, 4, KingdomWorkClass.Producer)));
		}

		/// <summary>Condition reads as words at the ends and as a number in the middle, and the
		/// condemned reading is coloured so a founder skimming sees it.</summary>
		[Test]
		public void Condition_NamesSoundAndMarksTheCondemned()
		{
			Assert.AreEqual("sound", KingdomBookRules.Condition(100));
			StringAssert.Contains("{{r|", KingdomBookRules.Condition(KingdomHappeningRules.BreakdownConditionFloor));
			Assert.AreEqual("worn to 70%", KingdomBookRules.Condition(70));
		}

		/// <summary>Each work class says what it is doing off the one slot of run-state its kind
		/// uses, and a producer with no hands says idle rather than making.</summary>
		[Test]
		public void Doing_ReadsTheRightSlotPerClass()
		{
			StringAssert.Contains("stage 2", KingdomBookRules.Doing(Work(100, 1, KingdomWorkClass.Growing)));
			StringAssert.Contains("44 charge", KingdomBookRules.Doing(Work(100, 1, KingdomWorkClass.Power)));
			Assert.AreEqual("Idle.", KingdomBookRules.Doing(Work(100, 0, KingdomWorkClass.Producer)));
			Assert.AreEqual("Making.", KingdomBookRules.Doing(Work(100, 2, KingdomWorkClass.Producer)));
			Assert.AreEqual("Being raised.", KingdomBookRules.Doing(
				Work(100, 2, KingdomWorkClass.Construction)));
		}

		/// <summary>Hands are counted, and none of them is said plainly rather than as "0".</summary>
		[Test]
		public void Hands_CountsAndNamesNone()
		{
			StringAssert.Contains("no hands", KingdomBookRules.Hands(0));
			Assert.AreEqual("1 hand", KingdomBookRules.Hands(1));
			Assert.AreEqual("3 hands", KingdomBookRules.Hands(3));
		}

		/// <summary>An empty book says so instead of printing an empty frame.</summary>
		[Test]
		public void Chapters_AnEmptyBookSaysSo()
		{
			KingdomCityReading empty = KingdomReadingRules.Project("Kavvat", null);
			StringAssert.Contains("Nothing stands", KingdomBookRules.Works(empty, null, null));
			StringAssert.Contains("Nobody is on the roll", KingdomBookRules.Roll(empty, "the eldest", ""));
			StringAssert.Contains("No ground", KingdomBookRules.Stores(empty, null));
		}

		/// <summary>The works chapter counts what is waiting on the founder, not what exists: the
		/// whole point of the board is the difference.</summary>
		[Test]
		public void Works_CountsWhatIsWaitingOnTheFounder()
		{
			KingdomCityReading reading = Read(new KingdomWorkRow[3]
			{
				Row(1, 100, 2, KingdomWorkKind.Producer),
				Row(2, 100, 0, KingdomWorkKind.Producer),
				Row(3, 4, 2, KingdomWorkKind.Store)
			});
			string chapter = KingdomBookRules.Works(reading, null, null);
			StringAssert.Contains("2 of them are waiting on you", chapter);
		}

		/// <summary>The roll counts the living, the away and the buried apart, and names the office
		/// holder when the city has one.</summary>
		[Test]
		public void Roll_SeparatesTheLivingTheAwayAndTheBuried()
		{
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				"taf:city:kavvat", 900L, default(KingdomStocks), null, null, new KingdomResidentRow[3]
				{
					Settler(1, KingdomDayShape.Field, KingdomResidentStanding.Resident),
					Settler(2, KingdomDayShape.Hearth, KingdomResidentStanding.Abroad),
					Settler(3, KingdomDayShape.Hearth, KingdomResidentStanding.Dead)
				}, null, out state, out fault), fault.ToString());
			string chapter = KingdomBookRules.Roll(KingdomReadingRules.Project("Kavvat", state), "the eldest", "Ptoh the Unbent");
			StringAssert.Contains("1 lives here", chapter);
			StringAssert.Contains("1 away with you", chapter);
			StringAssert.Contains("1 buried", chapter);
			StringAssert.Contains("Ptoh the Unbent", chapter);
			StringAssert.Contains("the eldest", chapter);
			StringAssert.Contains("1 in the fields", chapter);
		}

		/// <summary>A city with no office named says nothing about one rather than printing an
		/// empty title.</summary>
		[Test]
		public void Roll_SaysNothingAboutAnOfficeNobodyHolds()
		{
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				"taf:city:kavvat", 900L, default(KingdomStocks), null, null,
				new KingdomResidentRow[1] { Settler(1, KingdomDayShape.Hearth, KingdomResidentStanding.Resident) },
				null, out state, out fault), fault.ToString());
			StringAssert.DoesNotContain("the eldest",
				KingdomBookRules.Roll(KingdomReadingRules.Project("Kavvat", state), "the eldest", ""));
		}

		private static KingdomCityReading Read(KingdomWorkRow[] works)
		{
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				"taf:city:kavvat", 900L, default(KingdomStocks), null, works, null, null, out state, out fault), fault.ToString());
			return KingdomReadingRules.Project("Kavvat", state);
		}

		private static KingdomWorkRow Row(int id, int condition, int crew, KingdomWorkKind kind)
		{
			return new KingdomWorkRow(id, Here, 4, 4, "mill", condition, crew, 700L,
				new KingdomWorkRunState(kind, 0, 0, 0L));
		}

		private static KingdomResidentRow Settler(int id, KingdomDayShape day, KingdomResidentStanding standing)
		{
			return new KingdomResidentRow(id, "Ptoh-" + id, 2, 0, 100L, 0, 0, 0,
				day, standing, KingdomStandingCause.None, Here,
				KingdomBrinkWindow.None, KingdomBrinkWindow.None, null, 0);
		}
	}
}
#endif
