#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Api;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// What the city asks for, derived from the model and from nothing else.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE §5. Every case here is a STRUCTURAL fact — a stock at zero, more
	/// people than roofs, a work past the condemned line, a store at its ceiling — because the
	/// board is forbidden a balance number of its own. A case that needed a tuned threshold to
	/// pass would be evidence the rule had grown one.
	/// </para>
	/// </summary>
	internal class KingdomAskRulesTests
	{
		private const string Here = "taf:zone:here";

		private const string There = "taf:zone:there";

		private static KingdomCityReading Read(KingdomStocks stocks, KingdomZoneRow[] zones, KingdomWorkRow[] works, KingdomResidentRow[] residents)
		{
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				"taf:city:kavvat", 900L, stocks, zones, works, residents, null, out state, out fault), fault.ToString());
			return KingdomReadingRules.Project("Kavvat", state);
		}

		private static KingdomStocks Stocks(long water, long waterCap, long food, long foodCap)
		{
			return new KingdomStocks(new KingdomStockPair(water, waterCap),
				new KingdomStockPair(food, foodCap), new KingdomStockPair(0L, 0L));
		}

		private static KingdomZoneRow Zone(string id, int roofs, long food, long foodCap)
		{
			return new KingdomZoneRow(id, 0, 800L,
				new KingdomStocks(new KingdomStockPair(0L, 0L), new KingdomStockPair(food, foodCap), new KingdomStockPair(0L, 0L)),
				roofs, 0, 0, 0, 0, 0, 0);
		}

		private static KingdomWorkRow Work(int id, int condition, int crew, KingdomWorkKind kind)
		{
			return new KingdomWorkRow(id, Here, 4, 4, "mill", condition, crew, 700L,
				new KingdomWorkRunState(kind, 0, 0, 0L));
		}

		private static KingdomResidentRow Settler(int id)
		{
			return new KingdomResidentRow(id, "Ptoh-" + id, 2, 0, 100L, 0, 0, 0,
				KingdomDayShape.Hearth, KingdomResidentStanding.Resident, KingdomStandingCause.None, Here,
				KingdomBrinkWindow.None, KingdomBrinkWindow.None, null, 0);
		}

		private static bool Has(KingdomAsk[] asks, string kind)
		{
			for (int i = 0; i < asks.Length; i++)
			{
				if (asks[i].Kind == KingdomAskRules.OwnKindPrefix + kind)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>A dry cistern is grave, and it is the floor rather than a threshold: zero, not
		/// "low".</summary>
		[Test]
		public void Derive_ADryCisternIsGrave()
		{
			KingdomAsk[] asks = KingdomAskRules.Derive(Read(Stocks(0L, 240L, 5L, 60L), null, null, null));
			Assert.IsTrue(Has(asks, "thirst"));
			Assert.AreEqual(KingdomAskWeight.Grave, asks[0].Weight);
		}

		/// <summary>One dram is not an ask. The board fires on empty, never on nearly empty, which
		/// is what keeps it from having an economy of its own.</summary>
		[Test]
		public void Derive_OneDramIsNotAnAsk()
		{
			Assert.IsFalse(Has(KingdomAskRules.Derive(Read(Stocks(1L, 240L, 5L, 60L), null, null, null)), "thirst"));
		}

		/// <summary>A city that has dedicated no vessels is not thirsty. It has no cisterns, which
		/// is a different thing, and the city book says so on its own line.</summary>
		[Test]
		public void Derive_NoVesselsIsNotThirst()
		{
			Assert.IsFalse(Has(KingdomAskRules.Derive(Read(Stocks(0L, 0L, 0L, 0L), null, null, null)), "thirst"));
		}

		/// <summary>Bare larders with residents report that the optional meal is unavailable.</summary>
		[Test]
		public void Derive_BareLardersAskOnlyWhenSomebodyLivesHere()
		{
			Assert.IsFalse(Has(KingdomAskRules.Derive(Read(Stocks(9L, 9L, 0L, 60L), null, null, null)), "meal"));
			Assert.IsTrue(Has(KingdomAskRules.Derive(Read(Stocks(9L, 9L, 0L, 60L), null, null,
				new KingdomResidentRow[1] { Settler(1) })), "meal"));
		}

		/// <summary>More people than roofs asks for exactly the shortfall, counted across every
		/// zone rather than the one the founder is standing in.</summary>
		[Test]
		public void Derive_ShelterCountsRoofsAcrossTheWholeCity()
		{
			KingdomAsk[] asks = KingdomAskRules.Derive(Read(default(KingdomStocks),
				new KingdomZoneRow[2] { Zone(Here, 1, 0L, 0L), Zone(There, 1, 0L, 0L) }, null,
				new KingdomResidentRow[4] { Settler(1), Settler(2), Settler(3), Settler(4) }));
			Assert.IsTrue(Has(asks, "shelter"));
			for (int i = 0; i < asks.Length; i++)
			{
				if (asks[i].Kind == KingdomAskRules.OwnKindPrefix + "shelter")
				{
					StringAssert.Contains("2", asks[i].Title);
				}
			}
		}

		/// <summary>Roofs enough is no ask at all.</summary>
		[Test]
		public void Derive_EnoughRoofsIsSilence()
		{
			Assert.IsFalse(Has(KingdomAskRules.Derive(Read(default(KingdomStocks),
				new KingdomZoneRow[1] { Zone(Here, 4, 0L, 0L) }, null,
				new KingdomResidentRow[1] { Settler(1) })), "shelter"));
		}

		/// <summary>A work past the condemned line is pressing; one merely idle for want of hands
		/// is passing. Both use the breakdown happening's OWN definition of stopped, so the board
		/// and the news cannot disagree.</summary>
		[Test]
		public void Derive_WornAndCrewlessAreDifferentWeights()
		{
			KingdomAsk[] worn = KingdomAskRules.Derive(Read(default(KingdomStocks), null,
				new KingdomWorkRow[1] { Work(1, KingdomHappeningRules.BreakdownConditionFloor, 2, KingdomWorkKind.Producer) }, null));
			Assert.AreEqual(KingdomAskWeight.Pressing, worn[0].Weight);

			KingdomAsk[] idle = KingdomAskRules.Derive(Read(default(KingdomStocks), null,
				new KingdomWorkRow[1] { Work(1, 100, 0, KingdomWorkKind.Producer) }, null));
			Assert.AreEqual(KingdomAskWeight.Passing, idle[0].Weight);
		}

		/// <summary>A store and a growing ground with nobody on them are not asks. A larder with
		/// nobody standing in it is a larder.</summary>
		[TestCase(KingdomWorkKind.Store)]
		[TestCase(KingdomWorkKind.Growing)]
		[TestCase(KingdomWorkKind.Other)]
		public void Derive_KindsThatDoNotNeedHandsAreNotAsks(KingdomWorkKind kind)
		{
			Assert.IsFalse(Has(KingdomAskRules.Derive(Read(default(KingdomStocks), null,
				new KingdomWorkRow[1] { Work(1, 100, 0, kind) }, null)), "stopped"));
		}

		/// <summary>A full larder is only an ask when somewhere else has room for what it holds.
		/// A city with nowhere to put anything is not asking for haulage; it is asking for a
		/// larder, which is a different sentence.</summary>
		[Test]
		public void Derive_AFullStoreAsksOnlyWhenThereIsRoomElsewhere()
		{
			Assert.IsFalse(Has(KingdomAskRules.Derive(Read(default(KingdomStocks),
				new KingdomZoneRow[1] { Zone(Here, 9, 20L, 20L) }, null, null)), "haulage"));
			Assert.IsTrue(Has(KingdomAskRules.Derive(Read(default(KingdomStocks),
				new KingdomZoneRow[2] { Zone(Here, 9, 20L, 20L), Zone(There, 9, 0L, 20L) }, null, null)), "haulage"));
		}

		/// <summary>Worst first, and the tie-break is a fixed table rather than the alphabet, so
		/// two founders with the same book read the same board in the same order.</summary>
		[Test]
		public void Derive_SortsWorstFirstThenByAFixedKindOrder()
		{
			KingdomAsk[] asks = KingdomAskRules.Derive(Read(Stocks(0L, 240L, 0L, 60L),
				new KingdomZoneRow[1] { Zone(Here, 0, 0L, 0L) }, new KingdomWorkRow[1] { Work(1, 100, 0, KingdomWorkKind.Producer) },
				new KingdomResidentRow[1] { Settler(1) }));
			Assert.AreEqual(KingdomAskRules.OwnKindPrefix + "thirst", asks[0].Kind);
			Assert.AreEqual(KingdomAskRules.OwnKindPrefix + "shelter", asks[1].Kind);
			Assert.AreEqual(KingdomAskRules.OwnKindPrefix + "meal", asks[2].Kind);
			for (int i = 1; i < asks.Length; i++)
			{
				Assert.IsTrue(asks[i - 1].Weight >= asks[i].Weight, "weights must not ascend");
			}
		}

		/// <summary>The board is capped. A city of forty stopped works is a spreadsheet, and
		/// VISION forbids one.</summary>
		[Test]
		public void Derive_IsCappedAtTheBoardsCeiling()
		{
			KingdomWorkRow[] works = new KingdomWorkRow[20];
			for (int i = 0; i < works.Length; i++)
			{
				works[i] = Work(i + 1, 10, 0, KingdomWorkKind.Producer);
			}
			Assert.AreEqual(KingdomAskRules.MaxAsks, KingdomAskRules.Derive(Read(default(KingdomStocks), null, works, null)).Length);
		}

		/// <summary>A contented city asks for nothing, and says nothing.</summary>
		[Test]
		public void Derive_AContentedCityIsSilent()
		{
			Assert.AreEqual(0, KingdomAskRules.Derive(Read(Stocks(100L, 240L, 30L, 60L),
				new KingdomZoneRow[1] { Zone(Here, 4, 5L, 20L) },
				new KingdomWorkRow[1] { Work(1, 100, 2, KingdomWorkKind.Producer) },
				new KingdomResidentRow[1] { Settler(1) })).Length);
		}

		/// <summary>A null reading is an empty board, never a crash.</summary>
		[Test]
		public void Derive_ANullReadingIsAnEmptyBoard()
		{
			Assert.AreEqual(0, KingdomAskRules.Derive(null).Length);
		}

		/// <summary>Every ask says what would settle it. STANDARDS §7b applied forward: an ask that
		/// cannot name its remedy is a complaint.</summary>
		[Test]
		public void Derive_EveryAskNamesWhatWouldSettleIt()
		{
			KingdomAsk[] asks = KingdomAskRules.Derive(Read(Stocks(0L, 240L, 0L, 60L),
				new KingdomZoneRow[2] { Zone(Here, 0, 20L, 20L), Zone(There, 0, 0L, 20L) },
				new KingdomWorkRow[1] { Work(1, 5, 0, KingdomWorkKind.Refiner) },
				new KingdomResidentRow[1] { Settler(1) }));
			Assert.Greater(asks.Length, 3);
			for (int i = 0; i < asks.Length; i++)
			{
				Assert.IsFalse(string.IsNullOrEmpty(asks[i].Title), "every ask has a title");
				Assert.IsFalse(string.IsNullOrEmpty(asks[i].Want), "every ask names its remedy");
			}
		}

		/// <summary>A resolver puts the name the founder sees on the building into the ask; without
		/// one, the design key stands rather than a blank.</summary>
		[Test]
		public void Name_PrefersTheResolvedNameAndFallsBackToTheKey()
		{
			KingdomWorkReading work = new KingdomWorkReading(1, Here, "mill", 100, 0, KingdomWorkClass.Producer, 0, 0, 0L);
			Assert.AreEqual("The mill", KingdomAskRules.Name(work, null));
			Assert.AreEqual("Stone mill", KingdomAskRules.Name(work, delegate(string key) { return "stone mill"; }));
			Assert.AreEqual("A work", KingdomAskRules.Name(
				new KingdomWorkReading(1, Here, null, 100, 0, KingdomWorkClass.Producer, 0, 0, 0L), null));
		}

		/// <summary>
		/// The board's order is TOTAL: two asks that agree on weight, kind and title are still
		/// separated, by the ground they are on. List.Sort is an introsort and is not stable, so a
		/// comparer that returned zero here would leave the order to the algorithm.
		/// </summary>
		[Test]
		public void SortBoard_OrderIsTotalDownToTheGround()
		{
			System.Collections.Generic.List<KingdomAsk> board = new System.Collections.Generic.List<KingdomAsk>
			{
				new KingdomAsk("mod:x", "same", "w", "taf:zone:b", KingdomAskWeight.Passing),
				new KingdomAsk("mod:x", "same", "w", "taf:zone:a", KingdomAskWeight.Passing),
				new KingdomAsk("mod:x", "same", "w", "taf:zone:c", KingdomAskWeight.Grave)
			};
			KingdomAskRules.SortBoard(board);
			Assert.AreEqual("taf:zone:c", board[0].ZoneId);
			Assert.AreEqual("taf:zone:a", board[1].ZoneId);
			Assert.AreEqual("taf:zone:b", board[2].ZoneId);
		}

		/// <summary>A mod's grave ask outranks the city's passing one. Ours are GATHERED first for
		/// isolation, never ranked first.</summary>
		[Test]
		public void SortBoard_AModsGraveAskOutranksTheCitysPassingOne()
		{
			System.Collections.Generic.List<KingdomAsk> board = new System.Collections.Generic.List<KingdomAsk>
			{
				new KingdomAsk(KingdomAskRules.OwnKindPrefix + "haulage", "ours", "w", null, KingdomAskWeight.Passing),
				new KingdomAsk("mod:x", "theirs", "w", null, KingdomAskWeight.Grave)
			};
			KingdomAskRules.SortBoard(board);
			Assert.AreEqual("theirs", board[0].Title);
		}

		/// <summary>Everything a mod teaches the city sorts after everything the city says itself,
		/// among asks of the same weight.</summary>
		[Test]
		public void KindOrder_PutsTheCitysOwnVoiceFirst()
		{
			Assert.Less(KingdomAskRules.KindOrder(KingdomAskRules.OwnKindPrefix + "haulage"),
				KingdomAskRules.KindOrder("their-mod:weather"));
		}
	}
}
#endif
