#if TAF_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The city book. LIVING-CITY-ARCHITECTURE §1.2 and §1.3: sealed, frozen, copy-on-write, and
	/// publishing nothing on a fault — the same contract the kernel keeps for
	/// <c>FixedPeriodToyState</c>, and for the same reason: a partially advanced model that
	/// survives into a save is a wrong answer that outlives the bug.
	/// </summary>
	public class KingdomCityStateTests
	{
		private const BindingFlags RowFields = BindingFlags.Instance | BindingFlags.Public
			| BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

		private static void AssertByteEnum(Type type, params string[] expected)
		{
			Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(type), type.Name + " backing type");
			AssertTopLevelInternal(type);
			string[] names = Enum.GetNames(type);
			string[] actual = new string[names.Length];
			for (int i = 0; i < names.Length; i++)
			{
				actual[i] = names[i] + "=" + Convert.ToByte(Enum.Parse(type, names[i]));
			}
			CollectionAssert.AreEqual(expected, actual, type.Name + " wire values/order");
		}

		private static void AssertTopLevelInternal(Type type)
		{
			Assert.AreEqual("ThousandAndFirst.Simulation.City." + type.Name, type.FullName);
			Assert.IsFalse(type.IsNested, type.Name + " became nested");
			Assert.IsTrue(type.IsNotPublic, type.Name + " accessibility changed");
		}

		private static void AssertRowShape(Type type, string[] names, Type[] types)
		{
			AssertTopLevelInternal(type);
			Assert.IsTrue(type.IsValueType, type.Name + " stopped being a value type");
			FieldInfo[] fields = type.GetFields(RowFields);
			Assert.AreEqual(names.Length, fields.Length, type.Name + " field count");
			object defaultRow = Activator.CreateInstance(type);
			for (int i = 0; i < fields.Length; i++)
			{
				Assert.AreEqual(names[i], fields[i].Name, type.Name + " field order at " + i);
				Assert.AreEqual(types[i], fields[i].FieldType, type.Name + "." + fields[i].Name + " type");
				Assert.IsTrue(fields[i].IsInitOnly, type.Name + "." + fields[i].Name + " stopped being readonly");
				object expectedDefault = types[i].IsValueType ? Activator.CreateInstance(types[i]) : null;
				Assert.AreEqual(expectedDefault, fields[i].GetValue(defaultRow), type.Name + "." + fields[i].Name + " default");
			}
		}

		private static KingdomStocks Stocks(long water, long food)
		{
			return new KingdomStocks(
				new KingdomStockPair(water, 1000L),
				new KingdomStockPair(food, 500L),
				new KingdomStockPair(0L, 200L));
		}

		private static KingdomZoneRow Zone(string id, long lastRead)
		{
			return new KingdomZoneRow(id, 0, lastRead, Stocks(10L, 20L), 2, 1, 3, 4, 0, 0, 0);
		}

		private static KingdomCityState Build(int zones, int works, int residents, int clocks)
		{
			KingdomZoneRow[] zoneRows = new KingdomZoneRow[zones];
			for (int i = 0; i < zones; i++)
			{
				zoneRows[i] = Zone("taf:zone:" + i, 100L * i);
			}
			KingdomWorkRow[] workRows = new KingdomWorkRow[works];
			for (int i = 0; i < works; i++)
			{
				workRows[i] = new KingdomWorkRow(i, "taf:zone:0", (short)i, (short)i, "taf:design:hut", 100, 0, 0L,
					new KingdomWorkRunState(KingdomWorkKind.Store, 0, 0, 0L));
			}
			KingdomResidentRow[] residentRows = new KingdomResidentRow[residents];
			for (int i = 0; i < residents; i++)
			{
				residentRows[i] = new KingdomResidentRow(i + 1, "settler " + i, 0, 0, 0L, -1, -1, 0,
					KingdomDayShape.Hearth, KingdomResidentStanding.Resident, KingdomStandingCause.None, "taf:zone:0",
					KingdomBrinkWindow.None, KingdomBrinkWindow.None, null, 0);
			}
			KingdomClockRow[] clockRows = new KingdomClockRow[clocks];
			for (int i = 0; i < clocks; i++)
			{
				clockRows[i] = new KingdomClockRow(KingdomClockKind.Harvest, 1200L * (i + 1), i);
			}
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(1, 1, "taf:settlement:test", 0L, Stocks(50L, 60L),
				zoneRows, workRows, residentRows, clockRows, out state, out fault), fault.ToString());
			return state;
		}

		[Test]
		public void PersistedCityEnumsKeepTheirExactByteWireValues()
		{
			AssertByteEnum(typeof(KingdomCityFault), "None=0", "NullArgument=1", "RowCapExceeded=2",
				"InvalidIndex=3", "InvalidTick=4", "ClockRegression=5", "ArithmeticOverflow=6",
				"InvalidInterval=7", "InvalidRate=8", "InvalidCapacity=9", "InvalidLegOrder=10",
				"OutsideItinerary=11", "StepBudgetExhausted=12", "DuplicateBinding=13",
				"UnknownBinding=14", "CauseRequired=15", "TerminalStanding=16");
			AssertByteEnum(typeof(KingdomStockKind), "Water=0", "Food=1", "Materials=2", "OpaqueManifest=3");
			AssertByteEnum(typeof(KingdomWorkKind), "Other=0", "Growing=1", "Store=2", "Producer=3",
				"Refiner=4", "Power=5", "Construction=6");
			AssertByteEnum(typeof(KingdomDayShape), "Hearth=0", "Field=1", "Yard=2", "Market=3",
				"Craft=4", "Watch=5", "Shrine=6");
			AssertByteEnum(typeof(KingdomResidentStanding), "Resident=0", "Abroad=1", "Dead=2", "Expedition=3");
			AssertByteEnum(typeof(KingdomStandingCause), "None=0", "Unwitnessed=1", "Violence=2", "Raid=3",
				"Founder=4", "Followed=5", "Taken=6", "Astray=7");
			AssertByteEnum(typeof(KingdomClockKind), "Harvest=0", "Arrival=1", "Guest=2", "NotableGuest=3",
				"Festival=4", "MarketDay=5", "Delivery=6", "Raid=7");
			AssertByteEnum(typeof(KingdomToldKind), "None=0", "Harvest=1", "Delivery=2", "Arrival=3",
				"Departure=4", "Breakdown=5", "Mending=6", "Raising=7", "Shortfall=8", "Raid=9",
				"Ceremony=10", "Wedding=11", "Funeral=12", "Festival=13", "Brownout=14");
		}

		[Test]
		public void CityRowsKeepExactTopLevelImmutableFieldShapesAndDefaults()
		{
			AssertRowShape(typeof(KingdomGroundReading),
				new[] { "WaterLevel", "WaterCapacity", "FoodLevel", "FoodCapacity", "Defence" },
				new[] { typeof(long), typeof(long), typeof(long), typeof(long), typeof(int) });
			AssertRowShape(typeof(KingdomReckonInput),
				new[] { "State", "ToTick" },
				new[] { typeof(KingdomCityState), typeof(long) });
			AssertRowShape(typeof(KingdomStockPair),
				new[] { "Level", "Capacity" },
				new[] { typeof(long), typeof(long) });
			AssertRowShape(typeof(KingdomStocks),
				new[] { "Water", "Food", "Materials" },
				new[] { typeof(KingdomStockPair), typeof(KingdomStockPair), typeof(KingdomStockPair) });
			AssertRowShape(typeof(KingdomWorkTraits),
				new[] { "Growing", "Construction", "Store", "Power", "Refiner", "Producer" },
				new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
			AssertRowShape(typeof(KingdomWorkRunState),
				new[] { "Kind", "Stage", "Progress", "NextTick" },
				new[] { typeof(KingdomWorkKind), typeof(byte), typeof(int), typeof(long) });
			AssertRowShape(typeof(KingdomBrinkWindow),
				new[] { "Stands", "ReachedTick", "WarnedTick" },
				new[] { typeof(bool), typeof(long), typeof(long) });
			AssertRowShape(typeof(KingdomZoneRow),
				new[] { "ZoneId", "DistrictCode", "LastReadTick", "Stocks", "Roofs", "Defence",
					"WaterCarry", "FoodCarry", "OwedWater", "OwedFood", "OwedMaterials" },
				new[] { typeof(string), typeof(int), typeof(long), typeof(KingdomStocks), typeof(int), typeof(int),
					typeof(int), typeof(int), typeof(int), typeof(int), typeof(int) });
			AssertRowShape(typeof(KingdomWorkRow),
				new[] { "WorkId", "ZoneId", "AnchorX", "AnchorY", "DesignKey", "ConditionPercent",
					"CrewAssigned", "RanThroughTick", "RunState" },
				new[] { typeof(int), typeof(string), typeof(short), typeof(short), typeof(string), typeof(int),
					typeof(int), typeof(long), typeof(KingdomWorkRunState) });
			AssertRowShape(typeof(KingdomResidentRow),
				new[] { "ResidentId", "Name", "Origin", "OriginCode", "CreedCode", "ArrivedTick", "Arrived",
					"HomeWorkId", "JobWorkId", "JobRole", "DayShape", "Standing", "Cause", "BoundZoneId",
					"RoofBrink", "CreedBrink", "CreedToward", "CreedChannel", "KeptCreeds" },
				new[] { typeof(int), typeof(string), typeof(string), typeof(int), typeof(int), typeof(long), typeof(string),
					typeof(int), typeof(int), typeof(byte), typeof(KingdomDayShape), typeof(KingdomResidentStanding),
					typeof(KingdomStandingCause), typeof(string), typeof(KingdomBrinkWindow), typeof(KingdomBrinkWindow),
					typeof(string), typeof(byte), typeof(string) });
			AssertRowShape(typeof(KingdomClockRow),
				new[] { "Kind", "NextDueTick", "Ordinal" },
				new[] { typeof(KingdomClockKind), typeof(long), typeof(int) });
			AssertRowShape(typeof(KingdomToldRow),
				new[] { "Kind", "Tick", "SubjectA", "SubjectB", "PlaceZoneId", "Outcome" },
				new[] { typeof(KingdomToldKind), typeof(long), typeof(int), typeof(int), typeof(string), typeof(int) });
		}

		[Test]
		public void ExtractedAuthoritiesKeepTopLevelInternalTypeIdentity()
		{
			AssertTopLevelInternal(typeof(KingdomCityAdvanceable));
			Assert.IsTrue(typeof(KingdomCityAdvanceable).IsClass
				&& typeof(KingdomCityAdvanceable).IsSealed);
			AssertTopLevelInternal(typeof(KingdomReckonJob));
			Assert.IsTrue(typeof(KingdomReckonJob).IsClass && typeof(KingdomReckonJob).IsSealed);
			AssertTopLevelInternal(typeof(KingdomCityRules));
			Assert.IsTrue(typeof(KingdomCityRules).IsAbstract && typeof(KingdomCityRules).IsSealed);
			AssertTopLevelInternal(typeof(KingdomCityFaults));
			Assert.IsTrue(typeof(KingdomCityFaults).IsAbstract && typeof(KingdomCityFaults).IsSealed);
			AssertTopLevelInternal(typeof(KingdomWorkRules));
			Assert.IsTrue(typeof(KingdomWorkRules).IsAbstract && typeof(KingdomWorkRules).IsSealed);
			AssertTopLevelInternal(typeof(KingdomCityState));
			Assert.IsTrue(typeof(KingdomCityState).IsClass && typeof(KingdomCityState).IsSealed);
		}

		[Test]
		public void ReplacingTheRosterCopiesItsInputAndLeavesTheOldStateUntouched()
		{
			KingdomCityState before = Build(1, 0, 1, 0);
			KingdomResidentRow replacement = new KingdomResidentRow(9, "replacement", 0, 0, 0L,
				-1, -1, 0, KingdomDayShape.Hearth, KingdomResidentStanding.Resident,
				KingdomStandingCause.None, "taf:zone:0", KingdomBrinkWindow.None,
				KingdomBrinkWindow.None, null, 0);
			KingdomResidentRow[] input = new[] { replacement };
			KingdomCityState after;
			KingdomCityFault fault;
			Assert.IsTrue(before.TryWithResidents(input, out after, out fault), fault.ToString());
			input[0] = replacement.WithStanding(KingdomResidentStanding.Abroad, KingdomStandingCause.Followed);
			KingdomResidentRow oldRow;
			KingdomResidentRow heldRow;
			Assert.IsTrue(before.TryResident(0, out oldRow));
			Assert.IsTrue(after.TryResident(0, out heldRow));
			Assert.AreEqual(1, oldRow.ResidentId);
			Assert.AreEqual(KingdomResidentStanding.Resident, oldRow.Standing);
			Assert.AreEqual(9, heldRow.ResidentId);
			Assert.AreEqual(KingdomResidentStanding.Resident, heldRow.Standing);
			Assert.AreNotSame(before, after);
		}

		[Test]
		public void RowCountIsTheLiveRTheReceiptChecksAgainst()
		{
			// Live bound: 4 zone rows + 880 work rows + 60 resident rows + 12
			// clocks = 956. The told-log is not in R -- a told line is what an integration left
			// behind, never a row that proposes or integrates.
			KingdomCityState state = Build(4, KingdomCityState.MaxWorks, 60, 12);
			Assert.AreEqual(956, state.RowCount);
			Assert.AreEqual(0, state.ToldCount);
		}

		[TestCase(5, 0, 0, 0)]
		[TestCase(0, 881, 0, 0)]
		[TestCase(0, 0, 61, 0)]
		[TestCase(0, 0, 0, 13)]
		public void EveryDimensionIsCappedAndAnOverflowPublishesNothing(int zones, int works, int residents, int clocks)
		{
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomCityState.TryCreate(1, 1, "taf:settlement:test", 0L, Stocks(0L, 0L),
				new KingdomZoneRow[zones], new KingdomWorkRow[works], new KingdomResidentRow[residents], new KingdomClockRow[clocks],
				out state, out fault));
			Assert.AreEqual(KingdomCityFault.RowCapExceeded, fault);
			Assert.IsNull(state, "a refused creation published a state");
		}

		[Test]
		public void ANullSettlementIdIsRefusedAndANullRowArrayIsNot()
		{
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomCityState.TryCreate(1, 1, null, 0L, Stocks(0L, 0L), null, null, null, null, out state, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
			Assert.IsTrue(KingdomCityState.TryCreate(1, 1, "taf:settlement:test", 0L, Stocks(0L, 0L), null, null, null, null, out state, out fault));
			Assert.AreEqual(0, state.RowCount, "a city with nothing raised yet is an ordinary state");
		}

		[Test]
		public void ANegativeProcessedTickIsRefused()
		{
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomCityState.TryCreate(1, 1, "taf:settlement:test", -1L, Stocks(0L, 0L), null, null, null, null, out state, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidTick, fault);
		}

		/// <summary>A caller that keeps its own array and mutates it afterwards cannot reach inside
		/// a published model. Without the copy, the frozen doctrine is a comment.</summary>
		[Test]
		public void TheModelCopiesTheRowsItIsHandedAndNeverAliasesThem()
		{
			KingdomZoneRow[] rows = new KingdomZoneRow[1] { Zone("taf:zone:a", 10L) };
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(1, 1, "taf:settlement:test", 0L, Stocks(0L, 0L), rows, null, null, null, out state, out fault));
			rows[0] = Zone("taf:zone:hijacked", 999L);
			KingdomZoneRow held;
			Assert.IsTrue(state.TryZone(0, out held));
			Assert.AreEqual("taf:zone:a", held.ZoneId);
			Assert.AreEqual(10L, held.LastReadTick);
		}

		[Test]
		public void ReplacingARowLeavesTheOriginalStateUntouched()
		{
			KingdomCityState before = Build(2, 1, 1, 1);
			KingdomZoneRow row;
			Assert.IsTrue(before.TryZone(1, out row));
			KingdomCityState after;
			KingdomCityFault fault;
			Assert.IsTrue(before.TryWithZone(1, row.WithOwed(42, -7, 0), out after, out fault));
			KingdomZoneRow originalRow;
			KingdomZoneRow newRow;
			Assert.IsTrue(before.TryZone(1, out originalRow));
			Assert.IsTrue(after.TryZone(1, out newRow));
			Assert.AreEqual(0, originalRow.OwedWater, "copy-on-write mutated the original");
			Assert.AreEqual(0, originalRow.OwedFood, "copy-on-write mutated the original");
			Assert.AreEqual(42, newRow.OwedWater);
			Assert.AreEqual(-7, newRow.OwedFood, "one net figure cannot hold a landing and a draw at once; three signed ones can");
			Assert.AreNotSame(before, after);
		}

		[TestCase(-1)]
		[TestCase(2)]
		public void ReplacingARowOutsideTheModelIsRefused(int index)
		{
			KingdomCityState state = Build(2, 0, 0, 0);
			KingdomCityState next;
			KingdomCityFault fault;
			Assert.IsFalse(state.TryWithZone(index, Zone("taf:zone:x", 0L), out next, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidIndex, fault);
			Assert.IsNull(next);
		}

		/// <summary>The checkpoint is advanced by whole units consumed with the remainder kept,
		/// never re-anchored to now, and a clock that ran backwards is a corrupt save rather than
		/// something to repair. LIVING-CITY-ARCHITECTURE §2.2.</summary>
		[Test]
		public void TheProcessedMarkGoesForwardOrRefuses()
		{
			KingdomCityState state = Build(1, 0, 0, 0);
			KingdomCityState next;
			KingdomCityFault fault;
			Assert.IsTrue(state.TryWithProcessedThroughTick(0L, out next, out fault), "an equal tick is a no-op, not a regression");
			Assert.IsTrue(next.TryWithProcessedThroughTick(5000L, out next, out fault));
			Assert.AreEqual(5000L, next.ProcessedThroughTick);
			KingdomCityState backwards;
			Assert.IsFalse(next.TryWithProcessedThroughTick(4999L, out backwards, out fault));
			Assert.AreEqual(KingdomCityFault.ClockRegression, fault);
			Assert.IsNull(backwards);
			Assert.AreEqual(5000L, next.ProcessedThroughTick, "a refusal moved the mark anyway");
		}

		/// <summary>K is 32 and it is a ring: a season of happenings and a day of them differ in
		/// what is remembered, never in what is held. LIVING-CITY-ARCHITECTURE §1.2(f).</summary>
		[Test]
		public void TheToldLogIsABoundedRingThatForgetsItsOldest()
		{
			KingdomCityState state = Build(1, 0, 0, 0);
			KingdomCityFault fault;
			for (int i = 0; i < KingdomCityState.MaxToldEntries + 5; i++)
			{
				KingdomCityState next;
				Assert.IsTrue(state.TryTell(new KingdomToldRow(KingdomToldKind.Harvest, 100L + i, i, 0, "taf:zone:0", 1), out next, out fault));
				state = next;
			}
			Assert.AreEqual(KingdomCityState.MaxToldEntries, state.ToldCount);
			KingdomToldRow oldest;
			Assert.IsTrue(state.TryTold(0, out oldest));
			Assert.AreEqual(5, oldest.SubjectA, "the ring did not drop its first five");
			KingdomToldRow newest;
			Assert.IsTrue(state.TryTold(KingdomCityState.MaxToldEntries - 1, out newest));
			Assert.AreEqual(KingdomCityState.MaxToldEntries + 4, newest.SubjectA);
			KingdomToldRow past;
			Assert.IsFalse(state.TryTold(KingdomCityState.MaxToldEntries, out past));
		}

		[Test]
		public void TellingRefusesAnUndatedLineAndPublishesNothing()
		{
			KingdomCityState state = Build(1, 0, 0, 0);
			KingdomCityState next;
			KingdomCityFault fault;
			Assert.IsFalse(state.TryTell(new KingdomToldRow(KingdomToldKind.Harvest, -1L, 0, 0, "taf:zone:0", 0), out next, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidTick, fault);
			Assert.IsNull(next);
			Assert.AreEqual(0, state.ToldCount);
		}

		[Test]
		public void StocksAreReadAndReplacedByKindWithoutTouchingTheOthers()
		{
			KingdomStocks stocks = Stocks(50L, 60L);
			KingdomStockPair pair;
			Assert.IsTrue(stocks.TryGet(KingdomStockKind.Food, out pair));
			Assert.AreEqual(60L, pair.Level);
			KingdomStocks next;
			Assert.IsTrue(stocks.TryWith(KingdomStockKind.Food, new KingdomStockPair(0L, 500L), out next));
			Assert.AreEqual(0L, next.Food.Level);
			Assert.AreEqual(50L, next.Water.Level, "replacing one stock moved another");
			Assert.AreEqual(60L, stocks.Food.Level, "the original was mutated");
		}

		[Test]
		public void AnUnknownStockKindIsRefusedRatherThanDefaulted()
		{
			KingdomStocks stocks = Stocks(1L, 1L);
			KingdomStockPair pair;
			Assert.IsFalse(stocks.TryGet((KingdomStockKind)200, out pair));
			KingdomStocks next;
			Assert.IsFalse(stocks.TryWith((KingdomStockKind)200, new KingdomStockPair(9L, 9L), out next));
		}

		/// <summary>Every kernel refusal reaches the city as a refusal. A fault that translated
		/// into None would turn a detectable fault into a wrong answer.</summary>
		[Test]
		public void NoKernelFaultTranslatesIntoSuccess()
		{
			foreach (object value in Enum.GetValues(typeof(ThousandAndFirst.Simulation.Kernel.KernelFaultCode)))
			{
				ThousandAndFirst.Simulation.Kernel.KernelFaultCode code = (ThousandAndFirst.Simulation.Kernel.KernelFaultCode)value;
				KingdomCityFault translated = KingdomCityFaults.FromKernel(code);
				if (code == ThousandAndFirst.Simulation.Kernel.KernelFaultCode.None)
				{
					Assert.AreEqual(KingdomCityFault.None, translated);
					continue;
				}
				Assert.AreNotEqual(KingdomCityFault.None, translated, code + " translated into a success");
			}
		}
	}
}
#endif
