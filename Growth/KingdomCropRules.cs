using System.Text;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for everything the settlement grows: what a field cycles through, what
	/// commits it, how long a crop stands before it is ripe, what a row is worth when it is
	/// gathered, and what seed goes with what crop. The engine-coupled halves are
	/// <see cref="ThousandAndFirst.KingdomCrops"/> (sowing, rows, delivery) and
	/// <see cref="XRL.World.Parts.r_KingdomPlot"/> (the state a field carries), both in this
	/// folder.
	/// <para>
	/// <b>Seeds are ours end to end.</b> The survey of the shipped game
	/// (<c>_notes/VANILLA-PRODUCTION-TRUTH.md</c> &sect;2.1) is unambiguous: twenty-eight
	/// blueprints read as seed, spore, tuber or cutting and <b>not one of them is plantable</b>
	/// &mdash; zero blueprints carry a planting part, zero grow into another object, and there is
	/// no <c>AddAction("Plant"&hellip;)</c> anywhere in the engine. So the seed item, the sowing
	/// verb and the cycle are authored here, in vanilla's idiom and against vanilla's own parts
	/// (<c>Harvestable</c> on the standing rows, <c>Food</c> on what they yield), which is
	/// Addendum 11(c)'s third preference exactly: fill in only where the survey shows vanilla has
	/// nothing.
	/// </para>
	/// <para>
	/// <b>The cycle is stamped, never simulated.</b> A suspended zone runs no clocks
	/// (<c>ActionManager</c>; every vanilla <c>Harvestable.RegenTimer</c> is dead for exactly
	/// that reason), so a field records the tick its crop comes ripe and every question is asked
	/// against that stamp. One reckoning resolves however many cycles actually elapsed.
	/// </para>
	/// </summary>
	public static class KingdomCropRules
	{
		/// <summary>
		/// A field's place in its own cycle.
		/// <para>
		/// <see cref="PlotStage.Dormant"/> is <b>uncommitted ground</b> and nothing else: a field
		/// nobody has put seed in. It is where every field starts and where withdrawing the seed
		/// puts it back. A committed field is never Dormant again until the founder takes their
		/// seed out of it, which is why the gate below can read this one value and be right.
		/// </para>
		/// </summary>
		public enum PlotStage
		{
			Dormant = 0,
			Growing = 1,
			Ripe = 2
		}

		/// <summary>Drams a sowing draws from the settlement's dedicated stores, once, at the
		/// moment the founder commits the seed. Not per cycle: what recurs is the crop's own
		/// growing, and the settlement pays for that in the hands the design asks for.</summary>
		public const int PlantWaterCostDrams = 3;

		/// <summary>
		/// Days a committed crop stands before it is ripe. Six &mdash; near enough a Qud week that
		/// a founder who leaves on an errand comes back to a harvest, far enough from a day that
		/// standing over the field is never the way to farm it.
		/// </summary>
		public const int CropDays = 6;

		/// <summary>Ticks of the same. The cycle length, and the only clock a field keeps.</summary>
		public const long GrowTicks = KingdomRules.TicksPerDay * (long)CropDays;

		/// <summary>
		/// Servings one standing row yields when it is gathered. With <see cref="CropDays"/> this
		/// is the whole of the food economy's denomination: a design that stands
		/// <c>Rows</c> rows makes <c>Rows &times; YieldPerRow / CropDays</c> servings a day, and
		/// that figure is what its <c>Carries="food:N"</c> must equal. <c>_notes/balance-sim.py</c>
		/// re-derives every food design's declared carry from the rows on its own blueprint and
		/// fails the run if one has drifted, exactly as it re-derives every water design's from
		/// the <c>LiquidProducer</c> on its.
		/// </summary>
		public const int YieldPerRow = 3;

		/// <summary>
		/// Grace between a crop coming ripe and the settlement's own hands gathering it in. One
		/// day, and it is the founder's day: a ripe row carries vanilla <c>Harvestable</c>, so
		/// anybody standing in the field can gather it themselves, and whatever they take is not
		/// there for the settlement to count. The window exists so that is a real choice rather
		/// than a race nobody can win.
		/// </summary>
		public const long GatherDelayTicks = KingdomRules.TicksPerDay;

		/// <summary>
		/// Cycles one reckoning will resolve. A hundred and twenty of them is nearly two years of
		/// six-day crops, so this is a bound on arithmetic rather than a forgiveness: what caps
		/// what a long absence actually banks is larder room, which the harvest overflows into
		/// loss exactly as a full cask overflows into the ground.
		/// </summary>
		public const int MaxCyclesPerVisit = 120;

		/// <summary>Chance, per cycle, that a gathering also returns sowable seed. Generous
		/// enough that a farm feeds its own next planting most seasons, and low enough that the
		/// first seed still has to be found or bought.</summary>
		public const int SeedReturnChancePercent = 20;

		/// <summary>Seed one reckoning may return, however many cycles it resolved. Two: an
		/// absence brings back a harvest, never a seed merchant's stock.</summary>
		public const int MaxSeedsPerResolve = 2;

		/// <summary>
		/// Whether the settlement can spare <see cref="PlantWaterCostDrams"/> for a sowing
		/// without touching the water the heaviest possible single upkeep charge could still
		/// need. A field is never the reason a dry streak starts: it may only spend what is left
		/// once <see cref="KingdomRules.ReserveDays"/> days of upkeep, at the settlement's
		/// current population, are set aside untouched.
		/// </summary>
		/// <param name="StoredWater">Drams currently in the dedicated stores.</param>
		/// <param name="Population">Living settlers, for the upkeep reserve.</param>
		public static bool CanAffordPlanting(int StoredWater, int Population)
		{
			int reserve = KingdomRules.UpkeepDrams(Population) * KingdomRules.ReserveDays;
			return StoredWater - PlantWaterCostDrams >= reserve;
		}

		/// <summary>Whether a growing crop has stood long enough to ripen.</summary>
		public static bool HasRipened(long NextStageTick, long TimeTicks)
		{
			return TimeTicks >= NextStageTick;
		}

		/// <summary>The tick a crop sown now comes ripe at.</summary>
		public static long RipenTick(long PlantedTick)
		{
			return PlantedTick + GrowTicks;
		}

		/// <summary>Whether the settlement's own hands have waited out
		/// <see cref="GatherDelayTicks"/> and may gather a field that came ripe at
		/// <paramref name="RipeTick"/>.</summary>
		public static bool MayGather(long RipeTick, long TimeTicks)
		{
			return TimeTicks >= RipeTick + GatherDelayTicks;
		}

		/// <summary>
		/// How many ripenings are due at once, closed form. A field that has stood through a long
		/// absence resolves every cycle it actually completed in one reckoning &mdash; no loop
		/// walks the calendar, and nothing is forgiven.
		/// </summary>
		/// <param name="NextRipeTick">The stamp the field is carrying: the tick its NEXT crop
		/// comes ripe.</param>
		/// <param name="TimeTicks">Now.</param>
		/// <returns>Zero before the first ripening is due; otherwise the number of cycles that
		/// completed, clamped to <see cref="MaxCyclesPerVisit"/>.</returns>
		public static int CyclesDue(long NextRipeTick, long TimeTicks)
		{
			if (TimeTicks < NextRipeTick)
			{
				return 0;
			}
			long over = TimeTicks - NextRipeTick;
			long cycles = 1L + over / GrowTicks;
			return (cycles >= MaxCyclesPerVisit) ? MaxCyclesPerVisit : (int)cycles;
		}

		/// <summary>The tick the last of <paramref name="Cycles"/> ripenings came due at, which is
		/// what dates a harvest resolved out of an absence.</summary>
		public static long LastRipeTick(long NextRipeTick, int Cycles)
		{
			return (Cycles <= 0) ? NextRipeTick : (NextRipeTick + ((long)Cycles - 1L) * GrowTicks);
		}

		/// <summary>Where the field's stamp lands after a reckoning cashed
		/// <paramref name="Cycles"/> of them. Restamped from the harvest, never from now, so a
		/// part-cycle already grown is kept rather than thrown away.</summary>
		public static long RestampedRipeTick(long NextRipeTick, int Cycles)
		{
			return NextRipeTick + ((Cycles < 1) ? 1L : (long)Cycles) * GrowTicks;
		}

		/// <summary>
		/// How many ripenings the settlement's own hands may gather this reckoning, and whether
		/// the newest one is being left standing for the founder.
		/// <para>
		/// The whole of the founder's-day rule in one place, because getting it wrong the other way
		/// round is silent and expensive: unripening a crop that has not been credited loses a
		/// harvest, and gathering one inside the founder's day takes a crop they were promised
		/// first call on.
		/// </para>
		/// </summary>
		/// <param name="NextRipeTick">The field's stamp.</param>
		/// <param name="TimeTicks">Now.</param>
		/// <param name="HoldsLast">True when the newest ripening is still inside
		/// <see cref="GatherDelayTicks"/> and is being left where it stands.</param>
		/// <returns>Cycles to gather now. Zero means nothing is owed yet.</returns>
		public static int GatherableCycles(long NextRipeTick, long TimeTicks, out bool HoldsLast)
		{
			HoldsLast = false;
			int due = CyclesDue(NextRipeTick, TimeTicks);
			if (due <= 0)
			{
				return 0;
			}
			if (MayGather(LastRipeTick(NextRipeTick, due), TimeTicks))
			{
				return due;
			}
			HoldsLast = true;
			return due - 1;
		}

		/// <summary>
		/// Servings a whole reckoning brings in.
		/// <para>
		/// Every cycle but one is credited at what STANDS: nobody can have taken a row off a crop
		/// that ripened and was gathered inside the same reckoning. The exception is the one crop
		/// a founder actually stood in front of, which is credited at what stands RIPE &mdash; a
		/// row they gathered by hand is a row the settlement does not also get.
		/// </para>
		/// </summary>
		/// <param name="StandingRows">Rows still in the ground.</param>
		/// <param name="RipeRows">Of those, the ones standing ripe.</param>
		/// <param name="Cycles">Ripenings being gathered.</param>
		/// <param name="CountsRipeLast">Whether the last of them is the crop the founder was
		/// looking at. False for a reckoning resolved out of an absence, and for one whose newest
		/// crop is being held.</param>
		/// <param name="EffectivenessPercent">What the field is running at.</param>
		public static int GatheredYield(int StandingRows, int RipeRows, int Cycles, bool CountsRipeLast, int EffectivenessPercent)
		{
			if (Cycles <= 0)
			{
				return 0;
			}
			int last = CountsRipeLast ? RipeRows : StandingRows;
			long yield = (long)HarvestYield(StandingRows, EffectivenessPercent) * (Cycles - 1)
				+ HarvestYield(last, EffectivenessPercent);
			return (yield > int.MaxValue) ? int.MaxValue : (int)yield;
		}

		/// <summary>
		/// Ticks one pulse of irrigation is worth. Ten &mdash; a turn's own growing, so a field
		/// standing inside a running <c>Hydraulic Irrigator</c> comes ripe in half the time.
		/// <para>
		/// This is Addendum 11(c)'s SECOND preference exactly: the vanilla machine keeps its own
		/// behaviour (<c>RadiusEventSender Event="AccelerateRipening" Radius="10" ChargeUse="5"</c>
		/// fires on its own turn, at its own radius, off its own charge) and our clock answers it.
		/// The survey found this event fires from DATA and is answered by <c>Harvestable.Ripen</c>,
		/// which returns immediately on every shipped blueprint because none arms <c>RegenTime</c>
		/// &mdash; so the irrigator currently does nothing to any plant in the game. It does
		/// something to ours.
		/// </para>
		/// </summary>
		public const long IrrigationTicksPerPulse = 10L;

		/// <summary>Where one pulse of irrigation leaves a field's stamp. Never before now: a
		/// machine may shorten a wait and may not conjure a harvest out of the past, and ripening
		/// itself stays the tick-stamped cycle's own business.</summary>
		public static long IrrigatedRipeTick(long NextRipeTick, long TimeTicks)
		{
			long pulled = NextRipeTick - IrrigationTicksPerPulse;
			return (pulled < TimeTicks) ? TimeTicks : pulled;
		}

		/// <summary>
		/// Servings a gathering brings in. Rows actually standing, times what a row is worth,
		/// times what the field is running at &mdash; the same effectiveness
		/// <c>KingdomSubsidence.Supports</c> folds its <c>Carries</c> by, so a shorthanded or
		/// half-wrecked field gathers exactly what it was counted for.
		/// </summary>
		/// <param name="Rows">Rows standing in the ground. Zero or fewer yields nothing.</param>
		/// <param name="EffectivenessPercent">0-100, from <c>KingdomWear.EffectivenessOf</c>.</param>
		public static int HarvestYield(int Rows, int EffectivenessPercent)
		{
			if (Rows <= 0 || EffectivenessPercent <= 0)
			{
				return 0;
			}
			int percent = (EffectivenessPercent > 100) ? 100 : EffectivenessPercent;
			long yield = (long)Rows * YieldPerRow * percent / 100L;
			return (yield > int.MaxValue) ? int.MaxValue : (int)yield;
		}

		/// <summary>
		/// The daily food a design standing <paramref name="Rows"/> rows honestly makes, which is
		/// what its catalogue <c>Carries="food:N"</c> is required to equal. The food lane's answer
		/// to the water lane's <c>TicksPerDay / mean(VariableRate)</c>: a number that comes off
		/// what the object visibly does rather than out of an author's head.
		/// </summary>
		public static int FoodPerDayForRows(int Rows)
		{
			return (Rows <= 0) ? 0 : (Rows * YieldPerRow / CropDays);
		}

		/// <summary>The rows a design must stand to carry <paramref name="Food"/> a day. The
		/// inverse of <see cref="FoodPerDayForRows"/>, and what the blueprint's own
		/// <c>r_KingdomCropRows</c> tag is required to say.</summary>
		public static int RowsForFoodPerDay(int Food)
		{
			return (Food <= 0) ? 0 : (Food * CropDays / YieldPerRow);
		}

		/// <summary>
		/// Resolves the ground a settlement stands on to what it grows there. Mirrors
		/// <see cref="KingdomRules.StyleForSite"/>'s total fallback: an unknown, renamed, or
		/// empty style still grows something, because the ground under a field is never the
		/// reason a founder goes hungry.
		/// </summary>
		/// <param name="Style">The settlement's <see cref="KingdomSystem.Style"/>, already
		/// resolved once at founding from the terrain the rite read. Never re-derived here from
		/// terrain directly &mdash; that evidence was gathered once, and a second reading could
		/// only disagree with it.</param>
		/// <returns>A vanilla food item blueprint name, never null or empty.</returns>
		public static string CropBlueprintForStyle(string Style)
		{
			switch (Style)
			{
			case "verdant":
				return "Vinewafer";
			case "fungal":
				return "Plump Mushroom";
			case "gyre":
				return "Godshroom Cap";
			case "eater":
				return "Dreadroot Tuber";
			default:
				return "Starapple";
			}
		}

		/// <summary>
		/// Days this crop stands before it is ripe.
		/// <para>
		/// <b>Every crop answers <see cref="CropDays"/>, and that is a constraint rather than a
		/// coincidence.</b> A design's <c>Carries="food:N"</c> is one number, and the ground a
		/// settlement is founded on is not chosen by the founder &mdash; so a crop that took
		/// longer than another would make the same field carry differently in a marsh than on a
		/// flower field, for a reason nobody chose and nothing states. If a later build wants a
		/// slow crop, the catalogue's food figures have to become per-style with it; the test
		/// table and <c>_notes/balance-sim.py</c> both assert this function is flat, so that
		/// build finds out immediately rather than shipping a silent asymmetry.
		/// </para>
		/// </summary>
		/// <param name="Style">The settlement's style. Unknown styles get the common crop's
		/// days, for the reason <see cref="CropBlueprintForStyle"/> has a default.</param>
		public static int CropDaysForStyle(string Style)
		{
			switch (Style)
			{
			case "verdant":
			case "fungal":
			case "gyre":
			case "eater":
			default:
				return CropDays;
			}
		}

		// ==================================================================================
		// Seeds. One per crop family, and the map runs both ways: a seed knows what it grows,
		// and a crop knows what would sow it again.
		// ==================================================================================

		/// <summary>The seed item that sows <paramref name="CropBlueprint"/>, or null for a crop
		/// this build ships no seed for &mdash; which is not an error, only a crop the settlement
		/// cannot start on its own.</summary>
		public static string SeedForCrop(string CropBlueprint)
		{
			switch (CropBlueprint)
			{
			case "Starapple":
				return "r_KingdomSeedStarapple";
			case "Vinewafer":
				return "r_KingdomSeedVinewafer";
			case "Plump Mushroom":
				return "r_KingdomSeedMushroom";
			case "Godshroom Cap":
				return "r_KingdomSeedGodshroom";
			case "Dreadroot Tuber":
				return "r_KingdomSeedDreadroot";
			default:
				return null;
			}
		}

		/// <summary>What <paramref name="SeedBlueprint"/> grows, or null for anything that is not
		/// one of this build's seeds.</summary>
		public static string CropForSeed(string SeedBlueprint)
		{
			switch (SeedBlueprint)
			{
			case "r_KingdomSeedStarapple":
				return "Starapple";
			case "r_KingdomSeedVinewafer":
				return "Vinewafer";
			case "r_KingdomSeedMushroom":
				return "Plump Mushroom";
			case "r_KingdomSeedGodshroom":
				return "Godshroom Cap";
			case "r_KingdomSeedDreadroot":
				return "Dreadroot Tuber";
			default:
				return null;
			}
		}

		/// <summary>The seed the ground under a settlement of this style would offer, which is
		/// what its own wild plants drop and what its traders carry.</summary>
		public static string SeedForStyle(string Style)
		{
			return SeedForCrop(CropBlueprintForStyle(Style));
		}

		/// <summary>The standing plant one row of <paramref name="CropBlueprint"/> is. These are
		/// our own blueprints wearing vanilla's <c>Harvestable</c> and <c>PlantProperties</c>, so
		/// a sown field is a field of real plants somebody can walk into and gather by hand.
		/// Null for a crop with no row object, which sows nothing.</summary>
		public static string RowForCrop(string CropBlueprint)
		{
			switch (CropBlueprint)
			{
			case "Starapple":
				return "r_KingdomRowStarapple";
			case "Vinewafer":
				return "r_KingdomRowVinewafer";
			case "Plump Mushroom":
				return "r_KingdomRowMushroom";
			case "Godshroom Cap":
				return "r_KingdomRowGodshroom";
			case "Dreadroot Tuber":
				return "r_KingdomRowDreadroot";
			default:
				return null;
			}
		}

		/// <summary>Every seed this build ships, in style order. The extensibility law's limit is
		/// honest here: seeds are a closed family because each one names a crop the catalogue
		/// already grows, and a mod adding a style adds a crop, a row and a seed together.</summary>
		public static readonly string[] SeedBlueprints = new string[5]
		{
			"r_KingdomSeedStarapple",
			"r_KingdomSeedVinewafer",
			"r_KingdomSeedMushroom",
			"r_KingdomSeedGodshroom",
			"r_KingdomSeedDreadroot"
		};

		// ==================================================================================
		// What a field says when it will not grow. STANDARDS 7b: a process that stops short
		// names the want, once, where the founder will see it.
		// ==================================================================================

		/// <summary>Why a field is not producing. Frozen values: a save carries the last reason a
		/// field gave, so it can be unsaid when the block lifts rather than repeated.</summary>
		public enum FieldWant
		{
			/// <summary>Nothing is wrong.</summary>
			None = 0,

			/// <summary>No seed has been committed. The whole of Addendum 11(b)'s gate.</summary>
			Seed = 1,

			/// <summary>Sown, but nobody is working it and the design asks for hands.</summary>
			Hands = 2,

			/// <summary>Gathered, with no dedicated larder anywhere in the realm to put it in.</summary>
			Larder = 3,

			/// <summary>Ruined past the point where anything comes out of it.</summary>
			Condemned = 4
		}

		/// <summary>The one line a blocked field gives, in the ledger's voice. Never empty for a
		/// real want, so a caller cannot accidentally announce silence.</summary>
		/// <param name="Want">What the field is short of.</param>
		/// <param name="FieldName">What the founder calls it, lower case.</param>
		/// <param name="SettlementName">The city it stands in.</param>
		public static string WantNote(FieldWant Want, string FieldName, string SettlementName)
		{
			string field = string.IsNullOrEmpty(FieldName) ? "field" : FieldName;
			string place = string.IsNullOrEmpty(SettlementName) ? "the settlement" : SettlementName;
			switch (Want)
			{
			case FieldWant.Seed:
				return "The " + field + " at " + place + " is bare ground: nothing has been sown in it. Put seed in it, and it will be worked.";
			case FieldWant.Hands:
				return "The " + field + " at " + place + " is sown and nobody is working it. A crop nobody weeds is a crop nobody eats.";
			case FieldWant.Larder:
				return "The " + field + " at " + place + " stands ripe with nowhere to put it. Dedicate a larder, and it will be gathered in.";
			case FieldWant.Condemned:
				return "The " + field + " at " + place + " is past working. Mend it, or strike it and sow somewhere the ground is sound.";
			default:
				return "The " + field + " at " + place + " is not producing.";
			}
		}

		/// <summary>Why a sowing was refused, or that it was allowed.</summary>
		public enum SowVerdict
		{
			Sown = 0,

			/// <summary>The founder is not standing in a field the settlement built.</summary>
			NoField = 1,

			/// <summary>This field already carries somebody's committed seed.</summary>
			AlreadySown = 2,

			/// <summary>The stores cannot spare the water a sowing pours without eating the
			/// settlement's own drinking reserve.</summary>
			NoWater = 3,

			/// <summary>The field is ruined past working.</summary>
			Condemned = 4,

			/// <summary>The seed names a crop this build has no rows for.</summary>
			NoCrop = 5,

			/// <summary>This ground is not the realm's.</summary>
			NotClaimed = 6
		}

		/// <summary>Whether a sowing may go ahead, given everything the caller has already
		/// gathered. Pure, so the whole gate is one tabled decision rather than a ladder of
		/// engine calls nobody can test.</summary>
		/// <param name="HasField">A settlement-built field stands under the founder.</param>
		/// <param name="Claimed">That ground is claimed by the realm.</param>
		/// <param name="AlreadySown">The field already carries seed.</param>
		/// <param name="Condemned">The field is worn past working.</param>
		/// <param name="HasRow">The seed names a crop with a standing row object.</param>
		/// <param name="StoredWater">Drams in the dedicated stores.</param>
		/// <param name="Population">Living settlers, for the reserve.</param>
		public static SowVerdict AssessSow(bool HasField, bool Claimed, bool AlreadySown, bool Condemned, bool HasRow, int StoredWater, int Population)
		{
			if (!HasField)
			{
				return SowVerdict.NoField;
			}
			if (!Claimed)
			{
				return SowVerdict.NotClaimed;
			}
			if (Condemned)
			{
				return SowVerdict.Condemned;
			}
			if (AlreadySown)
			{
				return SowVerdict.AlreadySown;
			}
			if (!HasRow)
			{
				return SowVerdict.NoCrop;
			}
			if (!CanAffordPlanting(StoredWater, Population))
			{
				return SowVerdict.NoWater;
			}
			return SowVerdict.Sown;
		}

		/// <summary>The refusal a founder reads. Never empty, including for
		/// <see cref="SowVerdict.Sown"/>, which no caller should be showing.</summary>
		public static string SowRefusal(SowVerdict Verdict)
		{
			switch (Verdict)
			{
			case SowVerdict.NoField:
				return "There is no field here to sow. Stand in one the settlement has raised - a kitchen garden, a field, a grange - and try again.";
			case SowVerdict.NotClaimed:
				return "This ground is not the realm's. A field is sown where the settlement can work it.";
			case SowVerdict.AlreadySown:
				return "This field is already sown. Withdraw what is in it first, if you mean to change the crop.";
			case SowVerdict.Condemned:
				return "This field is past working. Mend it before you put seed in it.";
			case SowVerdict.NoCrop:
				return "Nothing in this seed knows how to stand in a row here.";
			case SowVerdict.NoWater:
				return "There is not enough water in the stores to wet a seedbed without drinking the settlement's own reserve. Fill the casks first.";
			default:
				return "The seed goes into the ground.";
			}
		}

		/// <summary>What the founder is asked before the seed is spent, in the carry-sign's own
		/// consent-before-cost shape: the exact crop, the exact rows, the exact wait, the exact
		/// water.</summary>
		public static string SowConfirm(string CropName, string FieldName, int Rows, int Drams)
		{
			string field = string.IsNullOrEmpty(FieldName) ? "field" : FieldName;
			string crop = string.IsNullOrEmpty(CropName) ? "the crop" : CropName;
			StringBuilder text = new StringBuilder();
			text.Append("Sow the ").Append(field).Append(" with ").Append(crop).Append("?\n\n");
			text.Append(Rows).Append((Rows == 1) ? " row goes into the ground" : " rows go into the ground");
			text.Append(", and ").Append(Drams).Append((Drams == 1) ? " dram is poured" : " drams are poured");
			text.Append(" over the seedbed. It comes ripe in ").Append(CropDays).Append(" days, and again every ");
			text.Append(CropDays).Append(" days after that, whether or not you are standing here.\n\n");
			text.Append("The seed is yours until you take it back out.");
			return text.ToString();
		}

		/// <summary>The line both registers carry when a field is committed.</summary>
		public static string SownChronicle(string CropName, string FieldName, string SettlementName)
		{
			return "the " + (string.IsNullOrEmpty(FieldName) ? "field" : FieldName) + " at "
				+ (string.IsNullOrEmpty(SettlementName) ? "the settlement" : SettlementName)
				+ " was sown with " + (string.IsNullOrEmpty(CropName) ? "the season's crop" : CropName);
		}

		/// <summary>
		/// The one chronicle line a whole season of gatherings gets. A field harvested twelve
		/// times while the founder was away is one sentence with a count in it, never twelve
		/// &mdash; the register holds two hundred entries and a farm would eat all of them.
		/// </summary>
		/// <param name="Cycles">Gatherings resolved at once. One reads as one harvest.</param>
		/// <param name="Yield">Servings they brought in between them.</param>
		/// <param name="SettlementName">The city.</param>
		/// <param name="DaysAgo">Whole days since the LAST of them came due. Zero and below read
		/// as "today", the same shape <c>KingdomLocusRules.PassageWhen</c> keeps.</param>
		public static string HarvestChronicle(int Cycles, int Yield, string SettlementName, int DaysAgo)
		{
			string place = string.IsNullOrEmpty(SettlementName) ? "the settlement" : SettlementName;
			string when = (DaysAgo <= 0)
				? ""
				: ((DaysAgo == 1) ? ", the last of it a day before you saw it" : (", the last of it " + DaysAgo + " days before you saw it"));
			if (Cycles <= 1)
			{
				return "the fields of " + place + " were gathered in for " + Yield + (Yield == 1 ? " serving" : " servings") + when;
			}
			return Cycles + " harvests came in at " + place + " for " + Yield + (Yield == 1 ? " serving" : " servings") + when;
		}

		/// <summary>The same, in the ledger's shorter voice.</summary>
		public static string HarvestNote(int Cycles, int Yield, int Delivered, int Pending, int Lost)
		{
			StringBuilder text = new StringBuilder();
			text.Append((Cycles <= 1) ? "The harvest came in" : (Cycles + " harvests came in"));
			text.Append(" for ").Append(Yield).Append((Yield == 1) ? " serving" : " servings").Append(". ");
			if (Delivered > 0)
			{
				text.Append(Delivered).Append(" went into the larders");
			}
			else
			{
				text.Append("None of it reached a larder here");
			}
			if (Pending > 0)
			{
				text.Append("; ").Append(Pending).Append(" is on the road to the city's stores");
			}
			if (Lost > 0)
			{
				text.Append("; ").Append(Lost).Append(" was left in the field for want of room");
			}
			text.Append(".");
			return text.ToString();
		}

		/// <summary>The line a cross-zone load gets when it finally reaches a pantry.</summary>
		public static string DeliveryNote(int Delivered, string SettlementName)
		{
			return Delivered + (Delivered == 1 ? " serving" : " servings") + " of the harvest reached the larders of "
				+ (string.IsNullOrEmpty(SettlementName) ? "the settlement" : SettlementName) + ".";
		}

		/// <summary>The line the founder reads when they take their own seed back out.</summary>
		public static string WithdrawnNote(string CropName, string FieldName, string SettlementName)
		{
			return "The " + (string.IsNullOrEmpty(FieldName) ? "field" : FieldName) + " at "
				+ (string.IsNullOrEmpty(SettlementName) ? "the settlement" : SettlementName)
				+ " is turned back to bare ground, and the " + (string.IsNullOrEmpty(CropName) ? "seed" : CropName)
				+ " seed is yours again. It grows nothing until you sow it once more.";
		}

		// ==================================================================================
		// The draw. Whether a gathering hands back sowable seed is asked once per field per
		// cycle, counter-based on a key naming the settlement, the field and that cycle's own
		// ordinal - so a reload asks the same question and gets the same answer, and a founder
		// cannot save-scum a seed out of a harvest they have already seen.
		// ==================================================================================

		private const int CropRulesVersion = 1;

		private const uint CropDrawIndex = 0u;

		/// <summary>Fixed, all-zero seed. Domain separation comes entirely from the settlement id,
		/// the stream and the ordinal baked into the key; whether a row went to seed is not a
		/// question that needs to be unguessable.</summary>
		private static readonly KernelSeed128 CropSeed = default(KernelSeed128);

		private const string StreamPrefix = "taf:crop:";

		private const string StreamSuffix = ":v1";

		/// <summary>The byte budget <c>KernelSemanticId</c> allows an id. Stated here rather than
		/// reached for, the same way <c>KingdomSubsidenceRules</c> states it.</summary>
		private const int KernelSemanticIdBudget = 128;

		/// <summary>Which question a draw answers. Frozen: never zero, never renumbered.</summary>
		public enum CropChannel
		{
			/// <summary>Whether this gathering also returned sowable seed.</summary>
			SeedReturn = 1
		}

		/// <summary>Folds one field's own id into the frozen <c>taf:</c> semantic-id grammar, so
		/// two fields asked about the same cycle are not forced to share one answer.</summary>
		/// <param name="FieldId">The field's persistent <c>GameObject.id</c>. Null and blank yield
		/// the lane an unidentified field would draw on.</param>
		internal static string FieldStream(string FieldId)
		{
			StringBuilder builder = new StringBuilder(StreamPrefix);
			int room = KernelSemanticIdBudget - StreamPrefix.Length - StreamSuffix.Length;
			if (!string.IsNullOrEmpty(FieldId))
			{
				foreach (char c in FieldId)
				{
					if (builder.Length - StreamPrefix.Length >= room)
					{
						break;
					}
					if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
					{
						builder.Append(c);
					}
					else if (c >= 'A' && c <= 'Z')
					{
						builder.Append((char)(c + 32));
					}
					else
					{
						builder.Append('-');
					}
				}
			}
			if (builder.Length == StreamPrefix.Length)
			{
				builder.Append("unidentified");
			}
			builder.Append(StreamSuffix);
			return builder.ToString();
		}

		/// <summary>
		/// Whether this gathering hands back sowable seed. False (never faulting) for a malformed
		/// settlement id, which returns nothing and is the safe answer &mdash; a seed the rules
		/// could not decide on is a seed the founder simply did not get.
		/// </summary>
		/// <param name="SettlementId">The settlement's kernel id.</param>
		/// <param name="FieldId">The field's persistent object id.</param>
		/// <param name="Ordinal">This cycle's own ordinal, counted from the field's first
		/// gathering, so every cycle asks fresh and none is ever re-rolled.</param>
		public static bool RollSeedReturn(string SettlementId, string FieldId, ulong Ordinal)
		{
			if (!SemanticEventKey.TryCreate(CropRulesVersion, SettlementId, FieldStream(FieldId), (uint)CropChannel.SeedReturn, Ordinal, out var key, out var _))
			{
				return false;
			}
			if (!CounterRandom.TryDrawBelow(CropSeed, key, CropDrawIndex, 100uL, out var value, out var _))
			{
				return false;
			}
			return (int)value < SeedReturnChancePercent;
		}

		/// <summary>
		/// Seed a whole reckoning returns: one draw per cycle, capped at
		/// <see cref="MaxSeedsPerResolve"/>. Nothing is returned by a gathering that yielded
		/// nothing &mdash; a field nobody worked does not go to seed either.
		/// </summary>
		public static int SeedReturned(string SettlementId, string FieldId, ulong FirstOrdinal, int Cycles, int Yield)
		{
			if (Cycles <= 0 || Yield <= 0)
			{
				return 0;
			}
			int seeds = 0;
			for (int i = 0; i < Cycles && seeds < MaxSeedsPerResolve; i++)
			{
				if (RollSeedReturn(SettlementId, FieldId, FirstOrdinal + (ulong)i))
				{
					seeds++;
				}
			}
			return seeds;
		}
	}
}
