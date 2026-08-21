using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Why a city rule refused.
	/// <para>
	/// The kernel's <c>KernelFaultCode</c> names the arithmetic refusals and this names the model's
	/// own; <see cref="KingdomCityFaults.FromKernel"/> is the one translation between them, so a
	/// tick fault raised in <c>TickMath</c> reaches a caller here without a second arithmetic
	/// implementation being written to avoid the conversion.
	/// </para>
	/// </summary>
	internal enum KingdomCityFault : byte
	{
		None = 0,
		NullArgument = 1,
		RowCapExceeded = 2,
		InvalidIndex = 3,
		InvalidTick = 4,
		ClockRegression = 5,
		ArithmeticOverflow = 6,
		InvalidInterval = 7,
		InvalidRate = 8,
		InvalidCapacity = 9,
		InvalidLegOrder = 10,
		OutsideItinerary = 11,
		StepBudgetExhausted = 12,
		DuplicateBinding = 13,
		UnknownBinding = 14,
		CauseRequired = 15,
		TerminalStanding = 16
	}

	internal static class KingdomCityFaults
	{
		/// <summary>
		/// A kernel refusal in the city's vocabulary. Anything the kernel can raise that the city
		/// has no narrower word for arrives as <see cref="KingdomCityFault.ArithmeticOverflow"/>
		/// rather than as <see cref="KingdomCityFault.None"/>: a fault must never translate into a
		/// success.
		/// </summary>
		internal static KingdomCityFault FromKernel(KernelFaultCode fault)
		{
			switch (fault)
			{
			case KernelFaultCode.None:
				return KingdomCityFault.None;
			case KernelFaultCode.InvalidTick:
				return KingdomCityFault.InvalidTick;
			case KernelFaultCode.InvalidInterval:
				return KingdomCityFault.InvalidInterval;
			case KernelFaultCode.ClockRegression:
				return KingdomCityFault.ClockRegression;
			default:
				return KingdomCityFault.ArithmeticOverflow;
			}
		}
	}

	/// <summary>Which civic stock a figure speaks for. LIVING-CITY-ARCHITECTURE &sect;1.2(a).</summary>
	internal enum KingdomStockKind : byte
	{
		Water = 0,
		Food = 1,
		Materials = 2
	}

	/// <summary>One stock and the ceiling it fills toward. Two longs, sixteen bytes.</summary>
	internal readonly struct KingdomStockPair
	{
		internal readonly long Level;

		internal readonly long Capacity;

		internal KingdomStockPair(long level, long capacity)
		{
			Level = level;
			Capacity = capacity;
		}
	}

	/// <summary>
	/// The civic share, and nothing else. Player-carried and undedicated stock stays purely
	/// physical and outside the model (LIVING-CITY-ARCHITECTURE &sect;1.2(a)), which is what keeps
	/// the protection law simple: the model only ever speaks for what the founder designated.
	/// <para>
	/// Six longs, forty-eight bytes — the width LIVING-CITY-ARCHITECTURE &sect;0.0(c) budgets on
	/// both the city and the zone row.
	/// </para>
	/// </summary>
	internal readonly struct KingdomStocks
	{
		internal readonly KingdomStockPair Water;

		internal readonly KingdomStockPair Food;

		internal readonly KingdomStockPair Materials;

		internal KingdomStocks(KingdomStockPair water, KingdomStockPair food, KingdomStockPair materials)
		{
			Water = water;
			Food = food;
			Materials = materials;
		}

		internal bool TryGet(KingdomStockKind kind, out KingdomStockPair pair)
		{
			switch (kind)
			{
			case KingdomStockKind.Water:
				pair = Water;
				return true;
			case KingdomStockKind.Food:
				pair = Food;
				return true;
			case KingdomStockKind.Materials:
				pair = Materials;
				return true;
			default:
				pair = default(KingdomStockPair);
				return false;
			}
		}

		internal bool TryWith(KingdomStockKind kind, KingdomStockPair pair, out KingdomStocks next)
		{
			switch (kind)
			{
			case KingdomStockKind.Water:
				next = new KingdomStocks(pair, Food, Materials);
				return true;
			case KingdomStockKind.Food:
				next = new KingdomStocks(Water, pair, Materials);
				return true;
			case KingdomStockKind.Materials:
				next = new KingdomStocks(Water, Food, pair);
				return true;
			default:
				next = this;
				return false;
			}
		}
	}

	/// <summary>What a work is, for the one small discriminated slot of run-state it carries.
	/// LIVING-CITY-ARCHITECTURE &sect;1.2(c).</summary>
	internal enum KingdomWorkKind : byte
	{
		Other = 0,
		Growing = 1,
		Store = 2,
		Producer = 3,
		Refiner = 4,
		Power = 5
	}

	/// <summary>
	/// The state the engine cannot carry for a work, and nothing else. A growing ground's stage and
	/// next-stage tick, a producer's progress, a power work's charge — one slot, read by kind.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;1.2(c): "a work's row carries state the engine cannot carry
	/// for it, and nothing else." Appearance, name, tile and contents stay on the object; the crop
	/// blueprint travels on the row's shared <c>DesignKey</c> reference rather than as a second
	/// string here, which is what holds this slot to the sixteen bytes &sect;0.0(c) budgets.
	/// </para>
	/// </summary>
	internal readonly struct KingdomWorkRunState
	{
		internal readonly KingdomWorkKind Kind;

		/// <summary>Growth stage for a growing ground; unread for every other kind.</summary>
		internal readonly byte Stage;

		/// <summary>Progress ticks for a producer or refiner, charge for a power work.</summary>
		internal readonly int Progress;

		/// <summary>Next stage tick for a growing ground; a breakpoint, never a countdown.</summary>
		internal readonly long NextTick;

		internal KingdomWorkRunState(KingdomWorkKind kind, byte stage, int progress, long nextTick)
		{
			Kind = kind;
			Stage = stage;
			Progress = progress;
			NextTick = nextTick;
		}
	}

	/// <summary>Where a person's day puts them. Derived from job and standing policy, never
	/// authored per settler, and holding no times. LIVING-CITY-ARCHITECTURE &sect;1.2(d).</summary>
	internal enum KingdomDayShape : byte
	{
		Hearth = 0,
		Field = 1,
		Yard = 2,
		Market = 3,
		Craft = 4,
		Watch = 5,
		Shrine = 6
	}

	/// <summary>
	/// What the roll says about one settler. LIVING-CITY-ARCHITECTURE &sect;1.2(d) and &sect;8.3.
	/// <para>
	/// Three states and no fourth, because these are the three things that can be true of a person
	/// the model speaks for: they live here; the founder walked them off the map and they are
	/// somewhere else, on the roll and doing no work; or they are dead and off it. &sect;8.3's whole
	/// answer to "object or row" is that a body is a view and this is the fact.
	/// </para>
	/// </summary>
	internal enum KingdomResidentStanding : byte
	{
		Resident = 0,
		Abroad = 1,
		Dead = 2
	}

	/// <summary>
	/// Why a row left <see cref="KingdomResidentStanding.Resident"/>.
	/// <para>
	/// A small named vocabulary rather than a stored sentence, for the reason the district code is
	/// a code: the prose belongs in one place and the row carries what the prose is derived from.
	/// The four death causes are <c>KingdomOfficeRules.DeathCause</c>'s own, in its own order, so
	/// the funeral the city already tells is the ONE telling &mdash; see
	/// <c>KingdomResidentRules.TryDeathCauseOrdinal</c>, which is the only bridge between them and
	/// exists so no second cause vocabulary is ever written.
	/// </para>
	/// </summary>
	internal enum KingdomStandingCause : byte
	{
		/// <summary>Nothing has happened. The only cause a <c>Resident</c> row may carry.</summary>
		None = 0,

		/// <summary>Dead, and no killer was reported. <c>DeathCause.Unknown</c>.</summary>
		Unwitnessed = 1,

		/// <summary>Dead by a hand the settlement cannot name. <c>DeathCause.Violence</c>.</summary>
		Violence = 2,

		/// <summary>Dead defending the stores when raiders came. <c>DeathCause.Raid</c>.</summary>
		Raid = 3,

		/// <summary>Dead by the founder's own hand. <c>DeathCause.Player</c>.</summary>
		Founder = 4,

		/// <summary>Abroad: walked out following the founder.</summary>
		Followed = 5,

		/// <summary>Abroad: taken by somebody else's hand &mdash; charmed, recruited, carried
		/// off.</summary>
		Taken = 6,

		/// <summary>Abroad: the body is not in the ground the row was bound to, and the realm
		/// cannot say where it went. Honestly unknown rather than guessed at.</summary>
		Astray = 7
	}

	/// <summary>
	/// One brink window as a row carries it: whether one stands, the tick the line was crossed,
	/// and the tick the word went out.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;1.2(d) moves these off the settler's property bag, because a
	/// row is what survives a zone going to disk and a property bag is not. The three fields are
	/// exactly what <c>BrinkRecord</c> is built from, and the reason <b>stands</b> is kept apart
	/// from the warned tick is <c>KingdomBrink</c>'s own: "recorded, and the word has not gone out
	/// yet" and "no brink" are different states rather than the same zero.
	/// </para>
	/// <para>
	/// Seventeen declared bytes; two of them plus a creed reference and a channel are the brink
	/// half of the ninety-six &sect;0.0(c) budgets the resident row.
	/// </para>
	/// </summary>
	internal readonly struct KingdomBrinkWindow
	{
		internal readonly bool Stands;

		internal readonly long ReachedTick;

		/// <summary>The anchor of the window. <c>KingdomBrinkRules.Unwarned</c> until the word
		/// goes out; a brink at that value has no deadline, however old it is.</summary>
		internal readonly long WarnedTick;

		internal KingdomBrinkWindow(bool stands, long reachedTick, long warnedTick)
		{
			Stands = stands;
			ReachedTick = stands ? reachedTick : 0L;
			WarnedTick = stands ? warnedTick : 0L;
		}

		/// <summary>No brink. What every row carries nearly always.</summary>
		internal static KingdomBrinkWindow None
		{
			get { return new KingdomBrinkWindow(false, 0L, 0L); }
		}

		internal KingdomBrinkWindow WithWarned(long warnedTick)
		{
			return new KingdomBrinkWindow(Stands, ReachedTick, warnedTick);
		}
	}

	/// <summary>The named clocks, consolidated off the settlement's loose longs and given an
	/// ordinal, which is what makes their draws reproducible. LIVING-CITY-ARCHITECTURE &sect;1.2(e).</summary>
	internal enum KingdomClockKind : byte
	{
		Harvest = 0,
		Arrival = 1,
		Guest = 2,
		NotableGuest = 3,
		Festival = 4,
		MarketDay = 5,
		Delivery = 6,
		Raid = 7
	}

	/// <summary>
	/// What a told-log line is a line about. LIVING-CITY-ARCHITECTURE &sect;1.2(f).
	/// <para>
	/// Values are appended and never reordered: the ring is serialized as plain ints by
	/// <c>KingdomCityBook</c>, so an older save's <c>10</c> must go on meaning <c>Ceremony</c>
	/// forever.
	/// </para>
	/// </summary>
	internal enum KingdomToldKind : byte
	{
		None = 0,
		Harvest = 1,
		Delivery = 2,
		Arrival = 3,
		Departure = 4,
		Breakdown = 5,
		Mending = 6,
		Raising = 7,
		Shortfall = 8,
		Raid = 9,
		Ceremony = 10,

		/// <summary>W4. Two rows who shared a roof, married. LIVING-CITY-ARCHITECTURE
		/// &sect;7.4.</summary>
		Wedding = 11,

		/// <summary>W4. A row that went <c>Dead</c>, and the rite the city gave it. Written by the
		/// same call that announces the death, never by a second one.</summary>
		Funeral = 12,

		/// <summary>W4. A feast kept on a day of Qud's own calendar &mdash; the Ides, or the
		/// festival of Ut yara Ux. Never an invented holiday.</summary>
		Festival = 13,

		/// <summary>W7. A work stopped because its network could not feed it. The subject is the
		/// work id, and the outcome is the tier it stopped on
		/// (<c>KingdomWorkTier</c>) &mdash; so the ring remembers not only that the lights went
		/// down but how far down the ladder the city had to go. LIVING-CITY-ARCHITECTURE
		/// &sect;3.11.</summary>
		Brownout = 14
	}

	/// <summary>
	/// One claimed zone as the model last read it, what its works make in a day, and what it owes
	/// the ground.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;0.0(c) budgeted eighty bytes: id ref 8 + district 4 +
	/// LastReadTick 8 + six stock/capacity longs 48 + roofs 4 + defence 4 + pad 4. W1 widened it
	/// to ninety-six for two reasons the wave could not ship without, and &sect;0.0(c) carries the
	/// same edit:
	/// </para>
	/// <list type="bullet">
	/// <item><description><see cref="WaterCarry"/> and <see cref="FoodCarry"/>, because the
	/// <c>ZoneSighting</c> the subsidence arithmetic reads is a projection of CARRIES, not of
	/// levels, and a row that cannot answer it cannot replace the game-state keys it retires.
	/// <see cref="Roofs"/> is the third carry and was already budgeted.</description></item>
	/// <item><description>the signed debt, PER STOCK KIND rather than as one net figure. A single
	/// net counter cannot say that a zone owes a food landing and a water draw at once, which is
	/// the ordinary case for a granary zone the city has been drinking out of; three signed
	/// figures can, and they are also the quantity <c>KingdomDrainRules</c> actually needs. The
	/// weighted thirds &sect;3.5 reports as <c>owed</c> are derived from these by
	/// <c>KingdomCityRules.CounterFor</c>, so there is one debt and one home for it.</description></item>
	/// </list>
	/// <para>
	/// The owed figures are <c>int</c> and not <c>long</c> on purpose: a dram and a serving are
	/// counted in <c>int</c> everywhere the ground counts them (<c>LiquidVolume.Volume</c>, an
	/// inventory tally), and a debt wider than the thing it is owed against would be a lie about
	/// what can be paid.
	/// </para>
	/// </summary>
	internal readonly struct KingdomZoneRow
	{
		internal readonly string ZoneId;

		/// <summary>The district's stable code. The name lives in the district registry, which is
		/// data-driven under the extensibility law, so the row carries a code and not a string.</summary>
		internal readonly int DistrictCode;

		internal readonly long LastReadTick;

		internal readonly KingdomStocks Stocks;

		/// <summary>Roof carry: what this zone's works hold up in a day, as the support tally
		/// counts it. A carry and not a level, like <see cref="WaterCarry"/>.</summary>
		internal readonly int Roofs;

		internal readonly int Defence;

		/// <summary>Water carry: drams this zone's works make in a day, at the effectiveness the
		/// pass that read it measured.</summary>
		internal readonly int WaterCarry;

		/// <summary>Food carry: servings this zone's works make in a day, on the same terms.</summary>
		internal readonly int FoodCarry;

		/// <summary>Signed debt in drams. Positive lands into this zone's vessels, negative draws
		/// out of them (LIVING-CITY-ARCHITECTURE &sect;3.9).</summary>
		internal readonly int OwedWater;

		/// <summary>Signed debt in servings, on the same terms.</summary>
		internal readonly int OwedFood;

		/// <summary>Signed debt in refined units, on the same terms.</summary>
		internal readonly int OwedMaterials;

		internal KingdomZoneRow(
			string zoneId,
			int districtCode,
			long lastReadTick,
			KingdomStocks stocks,
			int roofs,
			int defence,
			int waterCarry,
			int foodCarry,
			int owedWater,
			int owedFood,
			int owedMaterials)
		{
			ZoneId = zoneId;
			DistrictCode = districtCode;
			LastReadTick = lastReadTick;
			Stocks = stocks;
			Roofs = roofs;
			Defence = defence;
			WaterCarry = waterCarry;
			FoodCarry = foodCarry;
			OwedWater = owedWater;
			OwedFood = owedFood;
			OwedMaterials = owedMaterials;
		}

		/// <summary>What this zone owes the ground for one kind, signed. Total over the enum: an
		/// unrecognised kind owes nothing rather than reading as water.</summary>
		internal int OwedOf(KingdomStockKind kind)
		{
			switch (kind)
			{
			case KingdomStockKind.Water:
				return OwedWater;
			case KingdomStockKind.Food:
				return OwedFood;
			case KingdomStockKind.Materials:
				return OwedMaterials;
			default:
				return 0;
			}
		}

		internal KingdomZoneRow WithOwed(int owedWater, int owedFood, int owedMaterials)
		{
			return new KingdomZoneRow(ZoneId, DistrictCode, LastReadTick, Stocks, Roofs, Defence,
				WaterCarry, FoodCarry, owedWater, owedFood, owedMaterials);
		}

		internal KingdomZoneRow WithOwedOf(KingdomStockKind kind, int owed)
		{
			switch (kind)
			{
			case KingdomStockKind.Water:
				return WithOwed(owed, OwedFood, OwedMaterials);
			case KingdomStockKind.Food:
				return WithOwed(OwedWater, owed, OwedMaterials);
			case KingdomStockKind.Materials:
				return WithOwed(OwedWater, OwedFood, owed);
			default:
				return this;
			}
		}

		internal KingdomZoneRow WithReading(long lastReadTick, KingdomStocks stocks, int roofs, int defence, int waterCarry, int foodCarry)
		{
			return new KingdomZoneRow(ZoneId, DistrictCode, lastReadTick, stocks, roofs, defence,
				waterCarry, foodCarry, OwedWater, OwedFood, OwedMaterials);
		}

		internal KingdomZoneRow WithDistrictCode(int districtCode)
		{
			return new KingdomZoneRow(ZoneId, districtCode, LastReadTick, Stocks, Roofs, Defence,
				WaterCarry, FoodCarry, OwedWater, OwedFood, OwedMaterials);
		}
	}

	/// <summary>One standing work. LIVING-CITY-ARCHITECTURE &sect;1.2(c); sixty-four bytes at
	/// &sect;0.0(c).</summary>
	internal readonly struct KingdomWorkRow
	{
		internal readonly int WorkId;

		internal readonly string ZoneId;

		internal readonly short AnchorX;

		internal readonly short AnchorY;

		internal readonly string DesignKey;

		/// <summary>The wear percent KingdomWear already owns.</summary>
		internal readonly int ConditionPercent;

		internal readonly int CrewAssigned;

		internal readonly long RanThroughTick;

		internal readonly KingdomWorkRunState RunState;

		internal KingdomWorkRow(
			int workId,
			string zoneId,
			short anchorX,
			short anchorY,
			string designKey,
			int conditionPercent,
			int crewAssigned,
			long ranThroughTick,
			KingdomWorkRunState runState)
		{
			WorkId = workId;
			ZoneId = zoneId;
			AnchorX = anchorX;
			AnchorY = anchorY;
			DesignKey = designKey;
			ConditionPercent = conditionPercent;
			CrewAssigned = crewAssigned;
			RanThroughTick = ranThroughTick;
			RunState = runState;
		}

		internal KingdomWorkRow WithRunState(KingdomWorkRunState runState, long ranThroughTick)
		{
			return new KingdomWorkRow(WorkId, ZoneId, AnchorX, AnchorY, DesignKey, ConditionPercent, CrewAssigned, ranThroughTick, runState);
		}
	}

	/// <summary>
	/// One settler. The brink windows that today live as object properties live here instead
	/// (LIVING-CITY-ARCHITECTURE &sect;1.2(d)), because a row is what survives a zone going to disk.
	/// <para>
	/// Ninety-one declared bytes against the ninety-six &sect;0.0(c) budgets, plus the one unique
	/// heap string per resident. Nothing else here allocates: the bound zone id and the creed a
	/// brink pulls toward are shared references, exactly as the zone row's id and the work row's
	/// design key are.
	/// </para>
	/// <para>
	/// <b>What W2 corrected in W1's draft.</b> The warned tick is the anchor the whole window runs
	/// from (<c>KingdomBrinkRules.WindowSpent</c>), and W1 modelled it as a <c>bool</c>, which
	/// cannot carry an anchor; and "a brink stands and the word has not gone out yet" was not
	/// representable apart from "no brink". Both are now what <c>KingdomBrink</c> always kept on
	/// the property bag, which is what let the storage swap be invisible.
	/// </para>
	/// </summary>
	internal readonly struct KingdomResidentRow
	{
		internal readonly int ResidentId;

		/// <summary>The one unique heap string per resident that &sect;0.0(c) budgets at ~64 bytes.</summary>
		internal readonly string Name;

		internal readonly int OriginCode;

		internal readonly int CreedCode;

		internal readonly long ArrivedTick;

		internal readonly int HomeWorkId;

		internal readonly int JobWorkId;

		internal readonly byte JobRole;

		internal readonly KingdomDayShape DayShape;

		internal readonly KingdomResidentStanding Standing;

		/// <summary>Why the row left <see cref="KingdomResidentStanding.Resident"/>.
		/// <see cref="KingdomStandingCause.None"/> while it has not.</summary>
		internal readonly KingdomStandingCause Cause;

		/// <summary>The zone the body was last bound in. The registry (&sect;3.8) is what answers
		/// whether a body is actually there; this is what the row remembers about where to look.</summary>
		internal readonly string BoundZoneId;

		/// <summary>Roof brink: <c>KingdomBrinkRoofStanding</c>, <c>RoofTick</c> and
		/// <c>RoofWarned</c>, in a row rather than in a property bag.</summary>
		internal readonly KingdomBrinkWindow RoofBrink;

		/// <summary>Creed brink, on the same terms.</summary>
		internal readonly KingdomBrinkWindow CreedBrink;

		/// <summary>The creed a creed brink pulls toward, by faction name. A shared reference:
		/// creeds are open-ended faction names and there is no code to fold one into that could be
		/// read back out again.</summary>
		internal readonly string CreedToward;

		/// <summary>The <c>ConversionChannel</c> a creed brink was reached through, so the
		/// conversion that fires at the end of the window picks the same words it would have picked
		/// on the day.</summary>
		internal readonly byte CreedChannel;

		internal KingdomResidentRow(
			int residentId,
			string name,
			int originCode,
			int creedCode,
			long arrivedTick,
			int homeWorkId,
			int jobWorkId,
			byte jobRole,
			KingdomDayShape dayShape,
			KingdomResidentStanding standing,
			KingdomStandingCause cause,
			string boundZoneId,
			KingdomBrinkWindow roofBrink,
			KingdomBrinkWindow creedBrink,
			string creedToward,
			byte creedChannel)
		{
			ResidentId = residentId;
			Name = name;
			OriginCode = originCode;
			CreedCode = creedCode;
			ArrivedTick = arrivedTick;
			HomeWorkId = homeWorkId;
			JobWorkId = jobWorkId;
			JobRole = jobRole;
			DayShape = dayShape;
			Standing = standing;
			Cause = cause;
			BoundZoneId = boundZoneId;
			RoofBrink = roofBrink;
			CreedBrink = creedBrink;
			// A creed a brink no longer stands toward is not remembered: the row would otherwise
			// keep naming a pull that has been arrested, and KingdomBrink.Lift's whole contract is
			// that a lifted brink is forgotten rather than banked.
			CreedToward = creedBrink.Stands ? (string.IsNullOrEmpty(creedToward) ? null : creedToward) : null;
			CreedChannel = creedBrink.Stands ? creedChannel : (byte)0;
		}

		/// <summary>The brink of this kind as the row holds it. Total over the enum: a kind the row
		/// has no window for reads as no brink rather than as the roof's.</summary>
		internal KingdomBrinkWindow BrinkOf(BrinkKind kind)
		{
			switch (kind)
			{
			case BrinkKind.Roof:
				return RoofBrink;
			case BrinkKind.Creed:
				return CreedBrink;
			default:
				return KingdomBrinkWindow.None;
			}
		}

		/// <summary>This row with one brink window replaced. The creed reference and channel travel
		/// with the creed window and are ignored for any other kind, which is what stops a roof
		/// brink from ever acquiring a creed.</summary>
		internal KingdomResidentRow WithBrink(BrinkKind kind, KingdomBrinkWindow window, string creedToward, byte creedChannel)
		{
			switch (kind)
			{
			case BrinkKind.Roof:
				return new KingdomResidentRow(ResidentId, Name, OriginCode, CreedCode, ArrivedTick, HomeWorkId, JobWorkId,
					JobRole, DayShape, Standing, Cause, BoundZoneId, window, CreedBrink, CreedToward, CreedChannel);
			case BrinkKind.Creed:
				return new KingdomResidentRow(ResidentId, Name, OriginCode, CreedCode, ArrivedTick, HomeWorkId, JobWorkId,
					JobRole, DayShape, Standing, Cause, BoundZoneId, RoofBrink, window, creedToward, creedChannel);
			default:
				return this;
			}
		}

		/// <summary>This row standing somewhere else, with the reason. The transition RULES live in
		/// <c>KingdomResidentRules</c>; this is only how a row is rewritten once they have
		/// allowed it.</summary>
		internal KingdomResidentRow WithStanding(KingdomResidentStanding standing, KingdomStandingCause cause)
		{
			return new KingdomResidentRow(ResidentId, Name, OriginCode, CreedCode, ArrivedTick, HomeWorkId, JobWorkId,
				JobRole, DayShape, standing, cause, BoundZoneId, RoofBrink, CreedBrink, CreedToward, CreedChannel);
		}

		/// <summary>This row bound to other ground. Placement is W3; what W2 ships is the fact that
		/// the row knows where its body was last seen.</summary>
		internal KingdomResidentRow WithBoundZone(string boundZoneId)
		{
			return new KingdomResidentRow(ResidentId, Name, OriginCode, CreedCode, ArrivedTick, HomeWorkId, JobWorkId,
				JobRole, DayShape, Standing, Cause, boundZoneId, RoofBrink, CreedBrink, CreedToward, CreedChannel);
		}

		/// <summary>This row with what the ground says about the person: their name, where they came
		/// from, what they hold with, and the work they are posted to.</summary>
		internal KingdomResidentRow WithReading(string name, int originCode, int creedCode, int homeWorkId, int jobWorkId, byte jobRole, KingdomDayShape dayShape)
		{
			return new KingdomResidentRow(ResidentId, name, originCode, creedCode, ArrivedTick, homeWorkId, jobWorkId,
				jobRole, dayShape, Standing, Cause, BoundZoneId, RoofBrink, CreedBrink, CreedToward, CreedChannel);
		}
	}

	/// <summary>One named clock: kind, when it next falls, and where in its lane it sits.
	/// Sixteen bytes at &sect;0.0(c).</summary>
	internal readonly struct KingdomClockRow
	{
		internal readonly KingdomClockKind Kind;

		internal readonly long NextDueTick;

		/// <summary>The occurrence index within this clock's stream. The whole trick of
		/// LIVING-CITY-ARCHITECTURE &sect;2.4: the seventh harvest of field 3 draws the same numbers
		/// whether it is resolved on the day it fell or six cycles later inside one reckoning.</summary>
		internal readonly int Ordinal;

		internal KingdomClockRow(KingdomClockKind kind, long nextDueTick, int ordinal)
		{
			Kind = kind;
			NextDueTick = nextDueTick;
			Ordinal = ordinal;
		}

		internal KingdomClockRow WithNext(long nextDueTick, int ordinal)
		{
			return new KingdomClockRow(Kind, nextDueTick, ordinal);
		}
	}

	/// <summary>
	/// One line of the told-log ring. Everything in it has already happened — it is historical
	/// identity proof, not a due-job queue, which is the kernel's own distinction stated in
	/// <c>FixedPeriodToy</c> and repeated at LIVING-CITY-ARCHITECTURE &sect;1.2(f).
	/// </summary>
	internal readonly struct KingdomToldRow
	{
		internal readonly KingdomToldKind Kind;

		internal readonly long Tick;

		internal readonly int SubjectA;

		internal readonly int SubjectB;

		internal readonly string PlaceZoneId;

		internal readonly int Outcome;

		internal KingdomToldRow(KingdomToldKind kind, long tick, int subjectA, int subjectB, string placeZoneId, int outcome)
		{
			Kind = kind;
			Tick = tick;
			SubjectA = subjectA;
			SubjectB = subjectB;
			PlaceZoneId = placeZoneId;
			Outcome = outcome;
		}
	}

	/// <summary>
	/// One city's whole book: stocks, zone rows, work rows, resident rows, clocks, and the
	/// told-log ring. LIVING-CITY-ARCHITECTURE &sect;1.2.
	/// <para>
	/// Frozen by the &sect;1.3 doctrine, in the shape this codebase already uses for
	/// <c>FixedPeriodToyState</c>: sealed, <c>readonly struct</c> rows, every array copied in and
	/// never handed back, every transition copy-on-write. Nothing here is ever partially
	/// incremented, so a fault leaves the caller's state byte-identical.
	/// </para>
	/// <para>
	/// This is the pure model, engine-free by construction. The serialized carrier that will hold
	/// it on <c>KingdomSettlement</c> is a W1 deliverable and lives outside this type: an
	/// <c>IComposite</c> must assign fields, and the rules layer must not.
	/// </para>
	/// </summary>
	internal sealed class KingdomCityState
	{
		/// <summary>LIVING-CITY-ARCHITECTURE &sect;1.4: at most four claimed zones today, from
		/// <c>KingdomZoningRules.ZonesForStage</c> at City. A stage-gate constant, never an
		/// architectural limit — raising it raises R linearly and changes nothing else.</summary>
		internal const int MaxZones = 4;

		/// <summary>LIVING-CITY-ARCHITECTURE &sect;1.4, from <c>KingdomRules.MaxBuildings</c>.</summary>
		internal const int MaxWorks = 40;

		/// <summary>LIVING-CITY-ARCHITECTURE &sect;1.4, from <c>KingdomRules.MaxPopulation</c>.</summary>
		internal const int MaxResidents = 60;

		/// <summary>LIVING-CITY-ARCHITECTURE &sect;1.4: a fixed, named set.</summary>
		internal const int MaxClocks = 12;

		/// <summary>LIVING-CITY-ARCHITECTURE &sect;1.2(f) / &sect;1.4: K is 32, and it is a ring.</summary>
		internal const int MaxToldEntries = 32;

		internal readonly int SchemaVersion;

		internal readonly int RulesVersion;

		internal readonly string SettlementId;

		/// <summary>
		/// How far the model has been advanced. Advanced by whole units consumed with the
		/// remainder kept, never re-anchored to now (LIVING-CITY-ARCHITECTURE &sect;2.2), which is
		/// what makes <c>TryAdvance</c> idempotent at a repeated tick and a mid-pass reload safe.
		/// </summary>
		internal readonly long ProcessedThroughTick;

		internal readonly KingdomStocks Stocks;

		private readonly KingdomZoneRow[] zones;

		private readonly KingdomWorkRow[] works;

		private readonly KingdomResidentRow[] residents;

		private readonly KingdomClockRow[] clocks;

		private readonly KingdomToldRow[] told;

		private readonly int toldCount;

		private readonly int toldNext;

		private KingdomCityState(
			int schemaVersion,
			int rulesVersion,
			string settlementId,
			long processedThroughTick,
			KingdomStocks stocks,
			KingdomZoneRow[] zones,
			KingdomWorkRow[] works,
			KingdomResidentRow[] residents,
			KingdomClockRow[] clocks,
			KingdomToldRow[] told,
			int toldCount,
			int toldNext)
		{
			SchemaVersion = schemaVersion;
			RulesVersion = rulesVersion;
			SettlementId = settlementId;
			ProcessedThroughTick = processedThroughTick;
			Stocks = stocks;
			this.zones = zones;
			this.works = works;
			this.residents = residents;
			this.clocks = clocks;
			this.told = told;
			this.toldCount = toldCount;
			this.toldNext = toldNext;
		}

		/// <summary>
		/// Builds a city book, or refuses and publishes nothing.
		/// <para>
		/// Every array is copied, so a caller that keeps its own reference and mutates it later
		/// cannot reach inside a published model. A null array reads as an empty one — a city with
		/// no works yet is an ordinary state, not a fault — but a null settlement id is not.
		/// </para>
		/// </summary>
		internal static bool TryCreate(
			int schemaVersion,
			int rulesVersion,
			string settlementId,
			long processedThroughTick,
			KingdomStocks stocks,
			KingdomZoneRow[] zones,
			KingdomWorkRow[] works,
			KingdomResidentRow[] residents,
			KingdomClockRow[] clocks,
			out KingdomCityState state,
			out KingdomCityFault fault)
		{
			state = null;
			if (settlementId == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (processedThroughTick < 0L)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			if (Length(zones) > MaxZones || Length(works) > MaxWorks
				|| Length(residents) > MaxResidents || Length(clocks) > MaxClocks)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			state = new KingdomCityState(
				schemaVersion,
				rulesVersion,
				settlementId,
				processedThroughTick,
				stocks,
				Copy(zones),
				Copy(works),
				Copy(residents),
				Copy(clocks),
				new KingdomToldRow[MaxToldEntries],
				0,
				0);
			fault = KingdomCityFault.None;
			return true;
		}

		internal int ZoneCount
		{
			get { return zones.Length; }
		}

		internal int WorkCount
		{
			get { return works.Length; }
		}

		internal int ResidentCount
		{
			get { return residents.Length; }
		}

		internal int ClockCount
		{
			get { return clocks.Length; }
		}

		internal int ToldCount
		{
			get { return toldCount; }
		}

		/// <summary>
		/// The live <c>R</c> of LIVING-CITY-ARCHITECTURE &sect;0.0(f): zone rows + work rows +
		/// resident rows + clocks. The told-log is not in it, because a told line is never
		/// proposed against or integrated — it is what an integration left behind.
		/// </summary>
		internal int RowCount
		{
			get { return zones.Length + works.Length + residents.Length + clocks.Length; }
		}

		internal bool TryZone(int index, out KingdomZoneRow row)
		{
			return TryRow(zones, index, out row);
		}

		internal bool TryWork(int index, out KingdomWorkRow row)
		{
			return TryRow(works, index, out row);
		}

		internal bool TryResident(int index, out KingdomResidentRow row)
		{
			return TryRow(residents, index, out row);
		}

		/// <summary>
		/// Where the row for this resident id sits, or false.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;8.3: the id is the identity and the body is a view, so
		/// every reader that starts from a settler starts here. A linear walk over at most sixty
		/// rows and no dictionary, for the reason &sect;0.0(c) gives about per-row object headers:
		/// a map keyed on sixty ints would cost more to hold than the rows it indexes.
		/// </para>
		/// </summary>
		internal bool TryResidentIndex(int residentId, out int index)
		{
			for (index = 0; index < residents.Length; index++)
			{
				if (residents[index].ResidentId == residentId)
				{
					return true;
				}
			}
			index = -1;
			return false;
		}

		/// <summary>
		/// This book with a whole new roster, in one copy-on-write publish. Refuses over the cap
		/// and refuses a duplicated id rather than seating a settler twice &mdash; the row-level
		/// half of invariant I3, checked where the roster is written rather than where it is read.
		/// </summary>
		internal bool TryWithResidents(KingdomResidentRow[] rows, out KingdomCityState next, out KingdomCityFault fault)
		{
			next = null;
			int count = Length(rows);
			if (count > MaxResidents)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			for (int i = 0; i < count; i++)
			{
				for (int j = i + 1; j < count; j++)
				{
					if (rows[i].ResidentId == rows[j].ResidentId)
					{
						fault = KingdomCityFault.DuplicateBinding;
						return false;
					}
				}
			}
			next = new KingdomCityState(SchemaVersion, RulesVersion, SettlementId, ProcessedThroughTick, Stocks,
				zones, works, Copy(rows), clocks, told, toldCount, toldNext);
			fault = KingdomCityFault.None;
			return true;
		}

		internal bool TryClock(int index, out KingdomClockRow row)
		{
			return TryRow(clocks, index, out row);
		}

		/// <summary>The told-log, oldest first. Index 0 is the oldest line still held, not the
		/// oldest line ever written: the ring forgets, and says so by counting.</summary>
		internal bool TryTold(int ordinalFromOldest, out KingdomToldRow row)
		{
			row = default(KingdomToldRow);
			if (ordinalFromOldest < 0 || ordinalFromOldest >= toldCount)
			{
				return false;
			}
			int oldest = (toldCount < MaxToldEntries) ? 0 : toldNext;
			return TryRow(told, (oldest + ordinalFromOldest) % MaxToldEntries, out row);
		}

		internal bool TryWithStocks(KingdomStocks stocks, out KingdomCityState next, out KingdomCityFault fault)
		{
			next = new KingdomCityState(SchemaVersion, RulesVersion, SettlementId, ProcessedThroughTick, stocks,
				zones, works, residents, clocks, told, toldCount, toldNext);
			fault = KingdomCityFault.None;
			return true;
		}

		internal bool TryWithZone(int index, KingdomZoneRow row, out KingdomCityState next, out KingdomCityFault fault)
		{
			next = null;
			KingdomZoneRow[] replaced;
			if (!TryReplace(zones, index, row, out replaced))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			next = new KingdomCityState(SchemaVersion, RulesVersion, SettlementId, ProcessedThroughTick, Stocks,
				replaced, works, residents, clocks, told, toldCount, toldNext);
			fault = KingdomCityFault.None;
			return true;
		}

		internal bool TryWithWork(int index, KingdomWorkRow row, out KingdomCityState next, out KingdomCityFault fault)
		{
			next = null;
			KingdomWorkRow[] replaced;
			if (!TryReplace(works, index, row, out replaced))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			next = new KingdomCityState(SchemaVersion, RulesVersion, SettlementId, ProcessedThroughTick, Stocks,
				zones, replaced, residents, clocks, told, toldCount, toldNext);
			fault = KingdomCityFault.None;
			return true;
		}

		internal bool TryWithResident(int index, KingdomResidentRow row, out KingdomCityState next, out KingdomCityFault fault)
		{
			next = null;
			KingdomResidentRow[] replaced;
			if (!TryReplace(residents, index, row, out replaced))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			next = new KingdomCityState(SchemaVersion, RulesVersion, SettlementId, ProcessedThroughTick, Stocks,
				zones, works, replaced, clocks, told, toldCount, toldNext);
			fault = KingdomCityFault.None;
			return true;
		}

		internal bool TryWithClock(int index, KingdomClockRow row, out KingdomCityState next, out KingdomCityFault fault)
		{
			next = null;
			KingdomClockRow[] replaced;
			if (!TryReplace(clocks, index, row, out replaced))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			next = new KingdomCityState(SchemaVersion, RulesVersion, SettlementId, ProcessedThroughTick, Stocks,
				zones, works, residents, replaced, told, toldCount, toldNext);
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Advances the processed-through mark. Refuses a regression rather than repairing it:
		/// silently accepting a backward clock would let a corrupted save look healthy, which is
		/// the kernel's own ruling in <c>TickMath.TryValidateAdvance</c>.
		/// </summary>
		internal bool TryWithProcessedThroughTick(long processedThroughTick, out KingdomCityState next, out KingdomCityFault fault)
		{
			next = null;
			KernelFaultCode kernelFault;
			if (!TickMath.TryValidateAdvance(ProcessedThroughTick, processedThroughTick, out kernelFault))
			{
				fault = KingdomCityFaults.FromKernel(kernelFault);
				return false;
			}
			next = new KingdomCityState(SchemaVersion, RulesVersion, SettlementId, processedThroughTick, Stocks,
				zones, works, residents, clocks, told, toldCount, toldNext);
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Writes one line into the told-log ring. The ring is bounded at
		/// <see cref="MaxToldEntries"/> and overwrites its oldest line rather than growing, so a
		/// season of happenings and a day of them differ in what is remembered and never in what
		/// is held.
		/// </summary>
		internal bool TryTell(KingdomToldRow row, out KingdomCityState next, out KingdomCityFault fault)
		{
			next = null;
			if (row.Tick < 0L)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			KingdomToldRow[] ring = new KingdomToldRow[MaxToldEntries];
			Array.Copy(told, ring, MaxToldEntries);
			ring[toldNext] = row;
			int count = (toldCount < MaxToldEntries) ? (toldCount + 1) : MaxToldEntries;
			int cursor = (toldNext + 1) % MaxToldEntries;
			next = new KingdomCityState(SchemaVersion, RulesVersion, SettlementId, ProcessedThroughTick, Stocks,
				zones, works, residents, clocks, ring, count, cursor);
			fault = KingdomCityFault.None;
			return true;
		}

		private static bool TryRow<T>(T[] rows, int index, out T row)
		{
			if (index < 0 || index >= rows.Length)
			{
				row = default(T);
				return false;
			}
			row = rows[index];
			return true;
		}

		private static bool TryReplace<T>(T[] rows, int index, T row, out T[] replaced)
		{
			replaced = null;
			if (index < 0 || index >= rows.Length)
			{
				return false;
			}
			T[] copy = new T[rows.Length];
			Array.Copy(rows, copy, rows.Length);
			copy[index] = row;
			replaced = copy;
			return true;
		}

		private static int Length<T>(T[] rows)
		{
			return (rows == null) ? 0 : rows.Length;
		}

		private static T[] Copy<T>(T[] rows)
		{
			if (rows == null)
			{
				return new T[0];
			}
			T[] copy = new T[rows.Length];
			Array.Copy(rows, copy, rows.Length);
			return copy;
		}
	}
}
