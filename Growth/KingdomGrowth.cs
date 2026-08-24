using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static class KingdomGrowth
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionGrowth") != "No";

		/// <summary>
		/// Whether the settlement's people consume what they need and can suffer for the want of
		/// it. ONE switch for both binding goods, deliberately: water and food are the same
		/// promise to the player ("this place has needs and can fail them"), and a founder who
		/// turned scarcity off did not ask to keep half of it. The option ID is unchanged so no
		/// save or settings file notices; only its display text moved.
		/// </summary>
		public static bool ScarcityEnabled => Options.GetOption("r_TAF_OptionThirst") != "No";

		/// <summary>The water half of <see cref="ScarcityEnabled"/>, under the name every caller
		/// written before food was a flow reads.</summary>
		public static bool ThirstEnabled => ScarcityEnabled;

		/// <summary>The food half of <see cref="ScarcityEnabled"/>, named so a reader of the
		/// hunger path is not left wondering whether it has a switch of its own.</summary>
		public static bool HungerEnabled => ScarcityEnabled;

		public static long Interval(KingdomSystem System, Zone Z)
		{
			System.ZoneDistricts.TryGetValue(Z.ZoneID, out var district);
			return KingdomRules.PolicyInterval(KingdomRules.ArrivalIntervalTicks(System.Population, district), System.Gate, System.Stores);
		}

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Shared = null)
		{
			if (!Enabled || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			KingdomSurvey survey = Shared ?? KingdomSurvey.Take(Z, System);
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("growth pass " + Z.ZoneID + " tick=" + timeTicks + " next=" + System.NextArrivalTick + " pop=" + System.Population + " stage=" + System.Stage + " stored=" + survey.StoredWater + " open=" + survey.OpenWater + " space=" + survey.StorageSpace + " cap=" + survey.StorageCapacity + " dry=" + System.DryStreak + " withered=" + System.Withered + " food=" + survey.FoodStored + "/" + survey.FoodCapacity + " hunger=" + System.HungerStreak + " famished=" + System.Famished);
			}
			if (System.NextArrivalTick <= 0)
			{
				System.NextArrivalTick = timeTicks + Interval(System, Z);
			}
			// Fetch is charged per day, from the same checkpoint idiom upkeep uses, and only by
			// citizens who are not already crewing a work. Before this it ran once per zone
			// activation with no clock, so stepping out and back in fetched again without limit.
			// Uncapped, and it has to be: the bill below runs the full elapsed, so a fetch that
			// stopped at three days would turn every absence into a guaranteed loss. The detail
			// walks to the river for as long as the settlement drinks, and the two net.
			// The stamp is planted BEFORE the days are counted, and that order is load-bearing now
			// that the count is uncapped: LastFetchTick is zero on a settlement's first pass and
			// on a second city's first seating, and "ticks since tick zero" is the whole age of
			// the world. Under the retired cap that read three days and nobody noticed; uncapped
			// it would fill the cisterns out of the pool the moment the settlement was founded.
			if (System.LastFetchTick <= 0)
			{
				System.LastFetchTick = timeTicks;
			}
			int fetchDays = KingdomRules.ElapsedDays(timeTicks - System.LastFetchTick);
			// Only the water detail fetches. Nobody assigned means nobody walks to the river, and
			// the settlement lives on what the founder pours in - see KingdomSystem.WaterCrew.
			int hands = System.WaterCrew;
			if (hands > System.Population)
			{
				hands = System.Population;
			}
			int fetched = (fetchDays > 0)
				? survey.Store(survey.DrawFromPools(KingdomRules.FetchableDrams(hands, survey.OpenWater, survey.StorageSpace, fetchDays)))
				: 0;
			if (fetchDays > 0)
			{
				System.LastFetchTick = KingdomRules.AdvanceCheckpoint(System.LastFetchTick, timeTicks);
			}
			System.Ledger.Fetched += fetched;
			if (fetched > 0 && KingdomLog.Enabled)
			{
				KingdomLog.Log("growth: fetched " + fetched + " drams from open water into stores");
			}
			// The water works make what their Carries promise, on world-time like everything else
			// (Addendum 8): a reservoir's day is a reservoir's day whether anyone watched it.
			//
			// W6 MOVED THAT ARITHMETIC, it did not duplicate it. This block used to credit the
			// SEATED zone's works for the settlement's whole elapsed, off a settlement-wide stamp,
			// which is why W1 shipped the city model at a net rate of zero: two owners of one day
			// is a day billed twice. There is now one owner. Every zone's per-day make is measured
			// onto its own row at the pass that reads it, KingdomCity's reckon integrates all of
			// them off the model's single ProcessedThroughTick, and what the works made lands in
			// real vessels through §3.9's amortised reify - here, and in the next zone over, on the
			// same terms. System.LastWaterWorkTick is now the published mirror of that one tick
			// (KingdomCity.Stamp) and nothing reads it to bill from.
			// STANDARDS 7's "commissioned storage auto-flags", the food half. A granary the
			// settlement paid for is the settlement's pantry the same way a commissioned cask
			// rack is its cistern; nothing the founder placed is touched, because only a
			// KingdomBuilt work whose blueprint the catalogue calls a pantry is taken.
			AdoptCivicLarders(survey);
			// Before the day is drawn, and cheap: the realm's favourite dish is derived from
			// who lives here and what the ground grows, and the ration draw below reaches for its
			// staple first. Called every pass rather than once at founding because the creed a
			// city holds is a thing that MOVES - people arrive holding with somebody - and a
			// kitchen that changed its mind is worth a line (Addendum 11(b)).
			KingdomDish.Ensure(System);
			// Whatever of the city's harvest was still on the road lands NOW, before the day's
			// rations are drawn: a load that arrived is a load the settlement can eat, and this is
			// the crystallise-at-awareness half of Addendum 11(b-ii)'s cross-zone delivery. The
			// record of what room this zone has is written straight after, so the next harvest
			// anywhere in the city knows where it can be sent. With the ground in hand it may
			// arrive EMBODIED, carried in by a porter the founder watches (LIVING-CITY-ARCHITECTURE
			// §3.7) — one effect, two renderings, and the rendering is chosen by attendance rather
			// than drawn for.
			KingdomCrops.DeliverPending(System, Z, survey);
			// The fields bring in what their Carries promise, on world-time exactly as the water
			// works do (Addendum 8): a field's day is a field's day whether anyone watched it.
			// This is the one missing line the coverage map named, and its checkpoint is planted
			// before the first count for the reason LastFetchTick's is - unplanted, an uncapped
			// read is the whole age of the world, and the granaries would fill on the founding
			// day.
			// W6: the FIELDS' clocked make moved onto the model with the water works' (see above).
			// What is left on this stamp is the MILLS, which are a different kind of thing: a mill
			// does not make food out of the day, it takes real crops off real shelves and puts real
			// staples back, on the seated ground, where the shelves are. That is why it was never
			// in the model's rate to begin with - KingdomCrops.MilledFoodPerDay is subtracted out
			// of FoodMadePerDay - and it is why it keeps a stamp of its own. One clock each, and
			// neither can spend the other's days.
			// W7 repair, the second leg of the same defect. This stamp is SETTLEMENT-wide and the
			// mills it pays for stand in a ZONE, so a founder walking through a mill-less quarter
			// used to advance it and spend the mill quarter's days on nothing: the crops were
			// never ground and the days were gone. Gated on the seat actually holding a millstone.
			// Nothing accrues without bound - GrindHarvest is capped by KingdomRules.MillableStock,
			// the larders' own spare above a day's rations - so a long absence buys milling only as
			// far as there are crops to grind, which is the bound Addendum 8 clause 2 asks for.
			int milling = KingdomCrops.MilledFoodPerDay(survey);
			int grownDays = (milling > 0) ? KingdomRules.ElapsedDays(timeTicks - System.LastFoodWorkTick) : 0;
			if (System.LastFoodWorkTick <= 0)
			{
				System.LastFoodWorkTick = timeTicks;
				// Planted, and the count zeroed with it. An unplanted stamp reads as "ticks since
				// tick zero", which is the whole age of the world - harmless while only the block
				// below read it, and a first-pass windfall the moment anything downstream does.
				// GrindHarvest is downstream.
				grownDays = 0;
			}
			else if (grownDays > 0)
			{
				System.LastFoodWorkTick = KingdomRules.AdvanceCheckpoint(System.LastFoodWorkTick, timeTicks);
			}
			bool heartbeatHealthy = ResolveHeartbeat(System, Z, survey, timeTicks);
			// AFTER the day is eaten, and never before it: industry consumes foodstuffs
			// (Addendum 11(b)) and residents eat first. The order is the whole guarantee - a
			// settlement cannot go hungry because its mill was busy - and KingdomRules.MillableStock
			// keeps a day's rations back on top of it. Same elapsed days the fields were paid for,
			// off the same checkpoint, which is why grownDays is read once and used twice.
			GrindHarvest(System, survey, grownDays);
			int arrivals = 0;
			while (heartbeatHealthy && timeTicks >= System.NextArrivalTick && arrivals < KingdomRules.MaxArrivalsPerVisit && System.Population < KingdomRules.MaxPopulation
				&& (System.SupportedLevel <= 0 || System.Population < KingdomSubsidenceRules.SlideBeginsAbove(System.SupportedLevel)))
			{
				if (survey.StoredWater < KingdomRules.DramsPerArrival)
				{
					System.NextArrivalTick = timeTicks + Interval(System, Z);
					break;
				}
				// Addendum 4b: the arrival gate is assignment-level, not a bed tally. A settler
				// joins only if a home exists that THEY would take, and the refusal names the real
				// reason -- a city with ten empty beds and no charging post has no room for a
				// robot, and a bed count could never say so.
				ArrivalRefusal refusal;
				if (!SpawnSettler(System, Z, survey, out refusal))
				{
					if (!System.NoRoomAnnounced)
					{
						System.NoRoomAnnounced = true;
						if (refusal.NoAcceptableHome)
						{
							KingdomChronicle.Record(System, KingdomLodgingRules.ArrivalRefusedChronicle(System.KingdomDisplayName, refusal.Reason));
							System.Ledger.Note("{{r|" + KingdomLodgingRules.ArrivalRefusedNote(refusal.Reason) + "}}");
						}
						else
						{
							KingdomChronicle.Record(System, "a settler reached " + System.KingdomDisplayName + " and found nowhere to stand");
							System.Ledger.Note("{{r|A settler came and found nowhere to stand. There is no open ground left here.}}");
						}
					}
					System.NextArrivalTick = timeTicks + Interval(System, Z);
					break;
				}
				System.NoRoomAnnounced = false;
				arrivals++;
				System.NextArrivalTick += Interval(System, Z);
			}
			// The queue still stands due and this pass could seat nobody else - the visit budget
			// is spent, the population is capped, or the band's edge is reached. The overshoot
			// is burned rather than banked, through the same KingdomRules.RestampDeadline the
			// manifest turn-back and the raid re-warn read: a fresh full interval from now, with
			// no witness band, because an arrival slot is spent the instant it comes due. A
			// hundred days away is a settler at the gate, never a hundred of them.
			System.NextArrivalTick = KingdomRules.RestampDeadline(
				System.NextArrivalTick, timeTicks, Interval(System, Z), 0);
			AssignWork(System, survey);
			UpdateStage(System, Z, survey);
			// Last of the water-consuming steps in the pass, on purpose: a plot only ever
			// spends what the day's upkeep and arrivals left in the stores, so it can never be
			// the reason the thirst ladder fires.
			KingdomPlot.OnSettlementPass(System, Z, survey);
			// Written down straight after the fields have been gathered and the day has been
			// eaten, so what this zone is recorded as having room for is what it actually has room
			// for. This is the sighting machinery KingdomSubsidence.RecordZone established, with
			// its own prefix and one slot: a harvest anywhere in the city can ask whether another
			// zone can take it without that zone being loaded.
			KingdomCrops.RecordLarders(System, Z, survey, timeTicks);
			// Right after the plot, so a house finished raising this very pass is already a
			// candidate: who sleeps where, spending neither water nor hands. This is the ONE
			// attended pass Addendum 4b's grace is counted in.
			KingdomLodging.OnSettlementPass(System, Z);
			// Immediately after lodging, and never before it: who shares a roof this pass is the
			// whole input to osmosis (Addendum 5). Spends no water and no hands -- shared living is
			// the only thing it counts, and it counts it in attended passes, so a founder who is
			// away converts nobody and walks nobody out of town.
			KingdomConversion.OnSettlementPass(System, Z);
			// Shared living WITH THE SETTLEMENT, counted in attended passes and at most one day
			// apiece: the input the water rite reads for how much of this place a settler has
			// actually lived. Not the same quantity as KingdomConversion's shared living TOWARD ONE
			// CREED, which is household-scoped and closeness-scaled; both are attended-pass
			// denominated, and neither reads a clock that could advance while nobody is here.
			KingdomWaterRite.OnSettlementPass(System, Z);
			// After the plot, for the same reason: a staked plan only ever spends what the
			// plot's own draw left behind.
			KingdomPlanMarker.OnSettlementPass(System, Z, survey);
			// After the plot and the plan, and last of all. Power spends no water and takes no
			// hands the staffing pass has not already assigned, so it can only ever read a
			// settlement that has finished feeding, watering, and building itself.
			KingdomPower.OnSettlementPass(System, Z, survey);
			// Last of all, and it spends no water at any point: clearing ground and striking a
			// building spend hands, and only the hands the water detail and the staffing pass have
			// already finished with (KingdomSystem.AssignedCrew, set by AssignWork above).
			KingdomMaterials.OnSettlementPass(System, Z);
			// Last of all, and it spends neither water nor hands: a path is only what is left
			// behind by people walking to the work the staffing pass already put them on. It runs
			// after the plot and the plan so that a building raised this pass is already somewhere
			// the settlement has a reason to go.
			KingdomRoads.OnSettlementPass(System, Z);
			if (KingdomLog.Enabled) KingdomLog.Log("growth pass done: pop=" + System.Population + " stage=" + System.Stage + " arrivals=" + arrivals + " dry=" + System.DryStreak + " hunger=" + System.HungerStreak + " food=" + survey.FoodStored + "/" + survey.FoodCapacity + " next=" + System.NextArrivalTick);
		}

		/// <summary>
		/// Draws the settlement's drinking AND its eating for every day that actually passed, and
		/// runs whichever scarcity ladder the stores could not cover.
		/// <para>
		/// The days are uncapped (Addendum 8 clause 1) and neither bill is a debt: each is drawn
		/// through a <c>KingdomSurvey</c> draw that takes what is there and no more, so what a
		/// settlement could not pay it simply did not drink or eat. Nothing carries forward,
		/// nothing goes negative, and the checkpoint advances by the whole days charged either
		/// way &mdash; a season away costs a season of water and a season of bread, never a
		/// season of owing.
		/// </para>
		/// <para>
		/// <b>The two ladders bite once between them.</b> Each keeps its own streak and says its
		/// own sentence, but the cost of the resolve is <c>KingdomRules.ComposeScarcity</c>'s
		/// maximum and never their sum: one departure per resolve however many things are wrong,
		/// so a settlement that is dry AND starving empties no faster than the worse of the two
		/// alone would. What bounds the loss is still deliberately NOT the length of the absence,
		/// and <see cref="Emigrate"/> still floors at <c>KingdomRules.LoyalCoreSettlers</c>.
		/// Subsidence is left entirely alone underneath both of them: it is the structural
		/// consequence of standing above what the works carry, and this is the immediate one.
		/// </para>
		/// </summary>
		private static bool ResolveHeartbeat(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)
		{
			if (System.LastHeartbeatTick <= 0 || TimeTicks <= System.LastHeartbeatTick)
			{
				System.LastHeartbeatTick = TimeTicks;
				return true;
			}
			long elapsed = TimeTicks - System.LastHeartbeatTick;
			int days = KingdomRules.ElapsedDays(elapsed);
			if (days <= 0)
			{
				return true;
			}
			System.LastHeartbeatTick = KingdomRules.AdvanceCheckpoint(System.LastHeartbeatTick, TimeTicks);
			if (!ScarcityEnabled)
			{
				RecoverFromThirst(System);
				RecoverFromHunger(System);
				// No bill was drawn, so no meal was eaten. A shade left standing from the last
				// day scarcity WAS on would be a lift the settlement is no longer earning.
				SettleMeal(System, KingdomRules.MealVerdict.None);
				return true;
			}
			KingdomRules.ThirstOutcome thirst = DrawWater(System, Survey, elapsed, days);
			KingdomRules.HungerOutcome hunger = DrawRations(System, Survey, elapsed, days);
			KingdomRules.ScarcityVerdict verdict = KingdomRules.ComposeScarcity(thirst, hunger);
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("scarcity: days=" + days + " thirst=" + thirst + " hunger=" + hunger
					+ " bite=" + verdict.Bite + " dry=" + System.DryStreak + " hungry=" + System.HungerStreak);
			}
			if (verdict.Bite >= KingdomRules.ScarcityBite.Departure)
			{
				// ONE departure, named for everything that is actually wrong. Both registers get
				// the same fact in their own length, which is what the two clauses are for.
				Emigrate(System, Z, Survey,
					Cause: KingdomRules.ScarcityDepartureClause(verdict.Thirsting, verdict.Starving),
					Note: KingdomRules.ScarcityDepartureNote(verdict.Thirsting, verdict.Starving));
			}
			// The marks are states rather than costs, so both may stand at once on a settlement
			// that has genuinely earned both. Each is said once and unsaid by its own recovery.
			if (verdict.Withering && !System.Withered)
			{
				System.Withered = true;
				KingdomChronicle.Record(System, System.KingdomDisplayName + " withered in the long thirst");
				System.Ledger.Note("{{R|The settlement is withering in the long thirst.}}");
			}
			if (verdict.Famishing && !System.Famished)
			{
				System.Famished = true;
				KingdomChronicle.Record(System, System.KingdomDisplayName + " famished in the long hunger");
				System.Ledger.Note("{{R|The settlement is famishing. The fields are not feeding it.}}");
			}
			return verdict.Healthy;
		}

		/// <summary>
		/// The water half of a resolve: bills the elapsed days against the dedicated stores and
		/// steps the dry streak when they could not cover it. Says its own sentence; applies no
		/// consequence, which belongs to <see cref="ResolveHeartbeat"/>'s one composed verdict.
		/// </summary>
		private static KingdomRules.ThirstOutcome DrawWater(KingdomSystem System, KingdomSurvey Survey, long Elapsed, int Days)
		{
			// Agrarian ground feeds itself: it discounts the daily draw before the draw is made,
			// not after, so a dry agrarian settlement runs its dry streak slower, never zero.
			int upkeep = KingdomRules.PolicyUpkeepForElapsed(System.Population, Elapsed, System.Stores, System.Stage) * KingdomRules.DistrictsUpkeepPercent(System.ZoneDistricts.Values) / 100;
			int paid = Survey.Consume(upkeep);
			System.Ledger.UpkeepDrawn += paid;
			if (paid >= upkeep)
			{
				RecoverFromThirst(System);
				return KingdomRules.ThirstOutcome.Sustained;
			}
			System.DryStreak++;
			KingdomChronicle.Record(System, "the stores ran low, and " + System.KingdomDisplayName + " thirsted");
			System.Ledger.Note("{{r|The cistern ran dry. Settlers will leave if the water does not return.}}");
			if (KingdomLog.Enabled) KingdomLog.Log("thirst: days=" + Days + " upkeep=" + paid + "/" + upkeep + " streak=" + System.DryStreak);
			return KingdomRules.ResolveThirst(System.DryStreak, System.Stage, System.Population);
		}

		/// <summary>
		/// The food half of a resolve, and the water half's mirror with one deliberate difference:
		/// the ration bill is paid off the day's FORAGING first and only then out of the larders.
		/// <para>
		/// That is not a discount, it is where a camp's food comes from. The water lane's
		/// equivalent is the detail walking to the river (<c>KingdomRules.FetchableDrams</c>),
		/// except that hauled water goes into a cask and foraged food goes straight into a mouth
		/// &mdash; so the settlement that has dedicated no larder at all still eats, exactly as
		/// the settlement that has dedicated no cask still drinks what the founder pours in.
		/// <c>KingdomRules.MaxForagedRationsPerDay</c> is what stops that being an answer above a
		/// Camp: the ground gives four a day whoever walks it.
		/// </para>
		/// </summary>
		private static KingdomRules.HungerOutcome DrawRations(KingdomSystem System, KingdomSurvey Survey, long Elapsed, int Days)
		{
			int owed = KingdomRules.RationsForElapsed(System.Population, Elapsed);
			if (owed <= 0)
			{
				SettleMeal(System, KingdomRules.MealVerdict.None);
				RecoverFromHunger(System);
				return KingdomRules.HungerOutcome.Fed;
			}
			// Hands are spent once here as everywhere: whoever is on the water detail or crewing a
			// work is not also out on the ridge with a basket. AssignedCrew is last pass's
			// reading, which is the same staleness KingdomWear's own free-hands read accepts.
			int foraged = KingdomRules.ForagedRations(KingdomMaterialRules.FreeHands(System.Population, System.AssignedCrew), Days);
			int fromWild = (foraged < owed) ? foraged : owed;
			System.Ledger.Foraged += fromWild;
			int shortfall = owed - fromWild;
			// The draw is MEAL-SHAPED (Addendum 11(b)): it reaches for the staple the settlement's
			// own favourite dish is made of before it reaches for anything else, and reports how
			// much of the day actually came off it. Same servings either way - a meal is a
			// rendering of the ration, not a second bill - and the only thing riding on the
			// distinction is what the day was worth afterwards.
			int fromDish = 0;
			int eaten = (shortfall > 0) ? Survey.ConsumeFood(shortfall, System.DishStaple, out fromDish) : 0;
			System.Ledger.RationsDrawn += eaten;
			SettleMeal(System, KingdomRules.JudgeMeal(owed, fromDish, eaten, Survey.Kitchens > 0, System.Stage));
			if (fromWild + eaten >= owed)
			{
				RecoverFromHunger(System);
				return KingdomRules.HungerOutcome.Fed;
			}
			System.HungerStreak++;
			KingdomChronicle.Record(System, "the larders ran empty, and " + System.KingdomDisplayName + " went hungry");
			System.Ledger.Note("{{r|The larders are empty. Settlers will leave if the fields do not feed them.}}");
			if (KingdomLog.Enabled) KingdomLog.Log("hunger: days=" + Days + " rations=" + (fromWild + eaten) + "/" + owed + " foraged=" + fromWild + " streak=" + System.HungerStreak);
			return KingdomRules.ResolveHunger(System.HungerStreak, System.Stage, System.Population);
		}

		/// <summary>
		/// Records what the day's eating was and what it left behind: the one-day shade a
		/// settlement that ate its own dish is worth, and STANDARDS 7b's once-said sentence for a
		/// settlement whose larders gave nothing.
		/// <para>
		/// <b>The shade is re-drawn every single heartbeat, never accumulated.</b> A meal effect
		/// on a non-player eater expires at <c>StartTick + 1200</c> ticks
		/// (<c>D/…/ProceduralCookingEffect.cs:212-223</c>), which is exactly
		/// <c>KingdomRules.TicksPerDay</c>, and only one such effect stands at a time
		/// (<c>D/…/Campfire.cs:740</c>). So a settlement is well fed for the day it ate and no
		/// longer, and tomorrow has to earn it again. That is vanilla's number, not a balance
		/// dial.
		/// </para>
		/// </summary>
		private static void SettleMeal(KingdomSystem System, KingdomRules.MealVerdict Verdict)
		{
			System.LastMeal = Verdict;
			System.MealShade = KingdomRules.MealShadeFor(Verdict);
			if (Verdict == KingdomRules.MealVerdict.Favored)
			{
				string note = KingdomRules.FavoredMealNote(System.KingdomDisplayName, System.DishName);
				if (note != null)
				{
					System.Ledger.Note(note);
				}
			}
			if (Verdict != KingdomRules.MealVerdict.Scraps)
			{
				// The block lifted: something came out of the settlement's own stores, so the
				// sentence below is unsaid and may be said again the next time it is true.
				System.ScrapsAnnounced = false;
				return;
			}
			if (System.ScrapsAnnounced)
			{
				return;
			}
			System.ScrapsAnnounced = true;
			System.Ledger.Note(KingdomRules.ScrapsNote(System.KingdomDisplayName));
			KingdomChronicle.Record(System, "the larders of " + System.KingdomDisplayName + " gave nothing, and the settlement ate what it could find");
		}

		private static void RecoverFromThirst(KingdomSystem System)
		{
			System.DryStreak = 0;
			if (System.Withered)
			{
				System.Withered = false;
				KingdomChronicle.Record(System, "the water returned, and " + System.KingdomDisplayName + " drank deep and recovered");
				System.Ledger.Note(KingdomVoices.Say(System, VoiceOccasion.ThirstBroken, "{{G|The water returned, and the settlement recovered.}}"));
			}
		}

		/// <summary>The food mirror of <see cref="RecoverFromThirst"/>: the streak clears the
		/// moment a resolve is paid, and the mark is unsaid the moment the settlement eats
		/// again.</summary>
		private static void RecoverFromHunger(KingdomSystem System)
		{
			System.HungerStreak = 0;
			if (System.Famished)
			{
				System.Famished = false;
				KingdomChronicle.Record(System, "the harvest came in, and " + System.KingdomDisplayName + " ate its fill again");
				System.Ledger.Note("{{G|The harvest came in, and the settlement ate its fill again.}}");
			}
		}

		// ==================================================================================
		// What the fields make, and where it goes. The mirror of the water works' own daily
		// make (the KingdomSubsidence.Supports(survey).Water line at the top of the pass), with
		// the one subtraction that keeps it honest.
		// ==================================================================================

		/// <summary>
		/// Servings the settlement's works bring in in a day: exactly the <c>food</c> Carries
		/// <c>KingdomSubsidence</c> sums for the level, at exactly the effectiveness it sums them
		/// at. The mirror of the water works' own daily make, one line above the call site.
		/// <para>
		/// <b>The invariant this exists to keep.</b> One point of <c>food</c> is one settler fed
		/// for one day and <c>KingdomRules.RationsPerDay</c> charges one ration a settler a day,
		/// so a settlement standing at its own supported level makes precisely the bill it is
		/// charged. That only holds if EVERY food work is counted here, which is why nothing is
		/// excluded &mdash; a design counted for the level and not for the flow would be a level
		/// the settlement could reach and then starve at.
		/// </para>
		/// <para>
		/// <b>And the fields that gather themselves are subtracted here, exactly once</b>
		/// (Addendum 11(b-ii)). Every design that GROWS &mdash; the kitchen garden, the garden
		/// rows, the field, the ploughed fields, the grange, the home farm &mdash; carries
		/// <c>r_KingdomPlot</c>, stands real rows, and delivers its food physically on the crop's
		/// own six-day cycle (<c>KingdomPlot.OnSettlementPass</c>). A sown one is therefore
		/// removed from the clocked daily make by <c>KingdomCrops.CycledFoodPerDay</c>, folded at
		/// the same effectiveness and through the same <c>KingdomCatalogueRules.Carried</c>, so
		/// the subtraction cancels the addition to the unit. What is left in this figure is the
		/// food a settlement makes without growing it.
		/// </para>
		/// <para>
		/// <b>And so is every mill</b> (Addendum 11(b), Wave G3). A grinding mill now carries
		/// vanilla's own <c>Mill</c> part and delivers its <c>food</c> the same physical way a
		/// field does: <see cref="GrindHarvest"/> takes real crops off the larder shelves and puts
		/// real preserved staples back. So it is subtracted here too, by
		/// <c>KingdomCrops.MilledFoodPerDay</c>, at the same effectiveness and through the same
		/// <c>Carried</c>. What is left in this figure after BOTH subtractions is the food a
		/// settlement makes without growing it and without grinding it: the larder and the
		/// granary, which refuse to waste what came in.
		/// </para>
		/// <para>
		/// An UNSOWN field is already zero here, and not by subtraction: it carries no food at all
		/// (<c>KingdomCrops.WithoutUnsownFood</c>, folded inside <c>KingdomSubsidence.Supports</c>),
		/// so bare ground is worth nothing to the level and nothing to the day. That is Addendum
		/// 11(b)'s gate, kept in one place so the level and the flow cannot disagree about it.
		/// </para>
		/// </summary>
		/// <param name="Survey">The pass's survey. Null makes nothing.</param>
		public static int FoodMadePerDay(KingdomSurvey Survey)
		{
			if (Survey == null)
			{
				return 0;
			}
			int made = KingdomSubsidence.Supports(Survey).Food
				- KingdomCrops.CycledFoodPerDay(Survey)
				- KingdomCrops.MilledFoodPerDay(Survey);
			return (made > 0) ? made : 0;
		}

		/// <summary>
		/// Puts a making into the larders and is honest about whatever would not fit
		/// (STANDARDS 7b). Loss, not a queue: a harvest with nowhere to go is left in the field,
		/// the same way water the casks cannot take runs into the ground.
		/// <para>
		/// W6 moved its CALLER, not its rule. The clocked daily make is the city model's now
		/// (&sect;7.4), and it reaches real shelves through &sect;3.5's amortised reify - so this is
		/// what that landing calls, rather than a second implementation of "put food away and say
		/// what was lost" growing beside it in <c>KingdomCity</c>. The once-per-block flag and the
		/// harvest ledger stay exactly where they were.
		/// </para>
		/// </summary>
		/// <returns>What actually reached a larder.</returns>
		public static int StoreHarvest(KingdomSystem System, KingdomSurvey Survey, int Amount)
		{
			if (Amount <= 0)
			{
				// Nothing made, so nothing was lost. If there is room now the block is over
				// anyway, and 7b's "once" has to be able to become "once more" the next time the
				// sentence is actually true - otherwise a settlement whose fields were struck
				// while its larders were full would never be told again.
				if (Survey.FoodSpace > 0)
				{
					System.HarvestUnstoredAnnounced = false;
				}
				return 0;
			}
			int stored = Survey.StoreFood(Amount, KingdomCropRules.CropBlueprintForStyle(System.Style));
			System.Ledger.Harvested += stored;
			int lost = Amount - stored;
			if (lost <= 0)
			{
				// The block lifted: room was found, so the sentence below is unsaid and may be
				// said again the next time it is true.
				System.HarvestUnstoredAnnounced = false;
				return stored;
			}
			System.Ledger.HarvestLost += lost;
			if (System.HarvestUnstoredAnnounced)
			{
				return stored;
			}
			System.HarvestUnstoredAnnounced = true;
			// One flag for one block - "the harvest has nowhere to go" - with the sentence chosen
			// for which shape the block currently has. A founder who fixes the first by
			// dedicating a chest and then fills it hears the line once more only after the room
			// they made ran out and was found again.
			string line = (Survey.FoodCapacity <= 0)
				? ("The fields of " + System.KingdomDisplayName + " brought in a harvest and there was nowhere to put it. Dedicate a larder, or commission one, and it will be kept.")
				: ("The larders of " + System.KingdomDisplayName + " are full, and " + lost + " of the harvest was left in the field. A granary is what makes a good year last into a bad one.");
			System.Ledger.Note("{{r|" + line + "}}");
			MessageQueue.AddPlayerMessage("{{r|" + line + "}}");
			if (KingdomLog.Enabled) KingdomLog.Log("harvest: made=" + Amount + " stored=" + stored + " lost=" + lost + " cap=" + Survey.FoodCapacity);
			return stored;
		}

		/// <summary>
		/// The industry half of Addendum 11(b): the settlement's mills eat food and produce
		/// things. Real crops leave the real larders and real preserved staples go back into them
		/// &mdash; the same physical honesty the harvest already keeps, and what Addendum 12(d)
		/// asks of any consumption that lands on containers a founder can walk up to and open.
		/// <para>
		/// <b>What the machine actually does, in vanilla's own numbers.</b> Vanilla's
		/// <c>Millstone</c> carries <c>Mill</c> with blank transformation targets, so its one item
		/// per powered turn falls through to <c>Campfire.PerformPreserve</c>: a vinewafer becomes
		/// three vinewafer sheaves (<c>B/ObjectBlueprints/Foods.xml:424</c>). Our mill books the
		/// same ratio, flat across styles &mdash; <c>KingdomRules.PreserveMultiple</c>, and the
		/// reasoning for the flatness is on that constant. Two crops in, six staples back, a net
		/// of four servings, which is exactly the <c>food:4</c> the grinding mill declares.
		/// </para>
		/// <para>
		/// <b>Residents eat first, and the reserve is kept on top of that.</b> This runs after
		/// <see cref="ResolveHeartbeat"/> has drawn the day's rations, and even then it grinds
		/// only what stands above one more day's bill
		/// (<c>KingdomRules.MillableStock</c>). A settlement cannot be starved by its own
		/// industry, on this pass or the next one.
		/// </para>
		/// <para>
		/// <b>The visible machine and the accounting are different stock, on purpose.</b> The
		/// <c>Mill</c> part on the object grinds what is in the MILL'S OWN inventory while a
		/// founder is standing there (<c>WorksOnInventory</c>, <c>D/…/Mill.cs:47-51</c>), at
		/// vanilla's own per-crop numbers; this grinds the settlement's larders on the
		/// settlement's own clock. Nothing is counted twice, and a founder who hand-feeds the
		/// millstone gets vanilla's answer for their own goods.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom. Its style names the crop, and its dish the staple.</param>
		/// <param name="Survey">The pass's survey, whose counters this keeps correct.</param>
		/// <param name="Days">Whole days since the food works were last paid. Zero grinds nothing.</param>
		private static void GrindHarvest(KingdomSystem System, KingdomSurvey Survey, int Days)
		{
			if (Survey == null || Days <= 0)
			{
				return;
			}
			int owed = KingdomCrops.MilledFoodPerDay(Survey) * Days;
			if (owed <= 0)
			{
				return;
			}
			string crop = KingdomCropRules.CropBlueprintForStyle(System.Style);
			string staple = KingdomCrops.StapleFor(crop);
			if (string.IsNullOrEmpty(staple))
			{
				// A crop nothing in the game can bind to keep. The mill stands and turns; it
				// simply has nothing to make out of this harvest, and says so in the log rather
				// than minting a serving from nowhere.
				if (KingdomLog.Enabled) KingdomLog.Log("mill: " + crop + " has no staple to bind into; nothing ground");
				return;
			}
			// The reserve, read off the larders AS THEY STAND after the day was eaten.
			int spare = KingdomRules.MillableStock(Survey.FoodStored, System.Population);
			int wanted = KingdomRules.CropsForGain(owed);
			if (wanted > spare)
			{
				wanted = spare;
			}
			int ground = (wanted > 0) ? Survey.ConsumeCrop(crop, wanted) : 0;
			if (ground <= 0)
			{
				return;
			}
			// What came back: the crops themselves, bound, plus the gain. Conservation is stated
			// here in one line so it cannot drift - out is IN TIMES the multiple, never a figure
			// arrived at some other way.
			int made = ground * KingdomRules.PreserveMultiple;
			int stored = Survey.StoreFood(made, staple);
			System.Ledger.Milled += (stored > ground) ? (stored - ground) : 0;
			int lost = made - stored;
			if (lost > 0)
			{
				// Nowhere to put it, exactly as a harvest with a full larder has nowhere to go.
				// The same once-flag speaks for both, because it is the same block: the pantries
				// are full, and the settlement is losing what it made.
				System.Ledger.HarvestLost += lost;
			}
			if (KingdomLog.Enabled) KingdomLog.Log("mill: days=" + Days + " owed=" + owed + " spare=" + spare + " ground=" + ground + " " + crop + " -> " + made + " " + staple + " stored=" + stored);
		}

		/// <summary>
		/// Dedicates every finished work the catalogue calls a pantry that is not dedicated
		/// already, and folds it into this pass's survey so a granary raised before today is a
		/// pantry from the moment the pass notices it.
		/// <para>
		/// STANDARDS 7 is the warrant and also the whole limit: only a <c>KingdomBuilt</c> work
		/// whose blueprint is one of <c>KingdomRules.CivicLarderBlueprints</c> is taken, so a
		/// chest the player carried in and set down is never swept up. Idempotent, and a repair
		/// as much as a rule &mdash; a granary raised by a build that only knew how to auto-flag
		/// the larder shed becomes a pantry the next time its city is walked into.
		/// </para>
		/// </summary>
		private static void AdoptCivicLarders(KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work) || work.Inventory == null || work.GetIntProperty("KingdomLarder") == 1)
				{
					continue;
				}
				if (KingdomRules.IsCivicLarderBlueprint(work.Blueprint) && Survey.AdoptLarder(work))
				{
					KingdomLog.Log("larder: dedicated commissioned " + work.Blueprint);
				}
			}
		}

		/// <summary>
		/// Picks which of the settler blueprints is walking up the road. The roster lives in the
		/// <c>r_KingdomSettlers</c> population table so other mods can put their own people on it
		/// by merging a line, and so the mix can be retuned without touching code.
		/// <para>
		/// Falls back to the base blueprint if the table is missing or rolls nothing: a settlement
		/// that stops growing because a table was overridden badly is a worse failure than a
		/// settlement whose arrivals all look alike.
		/// </para>
		/// </summary>
		public static string SettlerBlueprint()
		{
			try
			{
				PopulationResult result = PopulationManager.RollOneFrom("r_KingdomSettlers");
				if (result != null && !string.IsNullOrEmpty(result.Blueprint) && GameObjectFactory.Factory.HasBlueprint(result.Blueprint))
				{
					return result.Blueprint;
				}
			}
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst settler roll", error);
			}
			return "r_KingdomSettler";
		}

		/// <summary>
		/// Why an arrival did not join, when one did not. Addendum 4b splits the one old "no
		/// room" into the two honest answers: there was nowhere to stand, or there was no home
		/// this settler would take.
		/// </summary>
		public struct ArrivalRefusal
		{
			/// <summary>True when the settlement has housing but none of it would take this
			/// person, from <c>KingdomLodging.WouldTakeArrival</c>. False means there was simply
			/// no ground to put them on.</summary>
			public bool NoAcceptableHome;

			/// <summary>Which of the lodging reasons decided it, for the founder's line.</summary>
			public KingdomLodgingRules.UnhousedReason Reason;
		}

		public static bool SpawnSettler(KingdomSystem System, Zone Z, KingdomSurvey Survey = null)
		{
			ArrivalRefusal refusal;
			return SpawnSettler(System, Z, Survey, out refusal);
		}

		/// <summary>
		/// Brings one settler in, or says why not. The lodging gate is asked of the settler
		/// themselves &mdash; created, judged, and let go again if the settlement has no home they
		/// would take &mdash; because what a person needs of a roof is a fact about that person
		/// and not about the blueprint they were rolled from.
		/// </summary>
		public static bool SpawnSettler(KingdomSystem System, Zone Z, KingdomSurvey Survey, out ArrivalRefusal Refusal)
		{
			Refusal = default(ArrivalRefusal);
			List<Cell> emptyCells = Z.GetEmptyCells((Cell c) => c.IsPassable() && !c.HasObjectWithPart("LiquidVolume"));
			if (emptyCells == null || emptyCells.Count == 0)
			{
				emptyCells = Z.GetEmptyCells();
			}
			if (emptyCells == null || emptyCells.Count == 0)
			{
				return false;
			}
			Cell cell = emptyCells.GetRandomElement();
			GameObject settler = GameObject.Create(SettlerBlueprint());
			if (settler == null)
			{
				return false;
			}
			// Addendum 4b, and before the settler is placed, enrolled, named or counted: a home
			// they would take must already be standing. Nobody is moved and nothing is destroyed
			// by the refusal -- the person simply never arrived.
			KingdomLodgingRules.UnhousedReason lodgingReason;
			if (!KingdomLodging.WouldTakeArrival(System, Z, settler, out lodgingReason))
			{
				settler.Obliterate();
				Refusal.NoAcceptableHome = true;
				Refusal.Reason = lodgingReason;
				return false;
			}
			cell.AddObject(settler);
			settler.MakeActive();
			KingdomFounding.EnrollCitizen(settler);
			settler.SetIntProperty("KingdomBorn", 1);
			string origin = KingdomRules.Origins[Stat.Random(0, KingdomRules.Origins.Length - 1)];
			settler.SetStringProperty("KingdomOrigin", origin);
			KingdomCreed.Record(System, settler, KingdomCreed.Draw(System));
			string given = XRL.Names.NameMaker.MakeName(settler, null, null, "human", null, System.KingdomFactionName, null, null, null, null, null, null, null, FailureOkay: true);
			if (!string.IsNullOrEmpty(given))
			{
				settler.DisplayName = given;
				settler.SetStringProperty("KingdomName", given);
				System.RosterNames.Add(given);
				System.RosterOrigins.Add(origin);
				System.RosterArrived.Add(XRL.World.Calendar.GetDay() + " of " + XRL.World.Calendar.GetMonth() + ", " + XRL.World.Calendar.GetYear() + " AR");
			}
			Qud.API.ConversationsAPI.addSimpleConversationToObject(settler, "Live and drink, friend. We heard there was water here, and a place worth the walk.", "Live and drink.", Question: "Why did you come?", Answer: "The road from " + origin + " was long, and the wells there are bitter. Here the water is shared. That is the whole of it.");
			System.OriginCounts.TryGetValue(origin, out var count);
			System.OriginCounts[origin] = count + 1;
			if (Survey != null) { Survey.Consume(KingdomRules.DramsPerArrival); } else { ConsumeStoredWater(Z, KingdomRules.DramsPerArrival); }
			System.Population++;
			string reason = KingdomRules.ArrivalReason(System.LastDeed, The.Game.TimeTicks - System.LastDeedTick, origin);
			KingdomChronicle.Record(System, reason + ", and a settler came to " + System.KingdomDisplayName + " and drank of the shared water");
			System.Ledger.Arrivals++;
			System.Ledger.Note("{{G|" + XRL.Language.Grammar.InitCap(reason) + " - a settler has come.}}");
			System.Ledger.ArrivalCost += KingdomRules.DramsPerArrival;
			return true;
		}

		/// <summary>
		/// Crews the settlement's works from its citizens, in placement order. A work without
		/// its crew is idle, not broken: it keeps its charge and its contents and simply does
		/// not run, and the settlement says which works want hands.
		/// </summary>
		public static void AssignWork(KingdomSystem System, KingdomSurvey Survey)
		{
			if (Survey.Works.Count == 0)
			{
				return;
			}
			int[] demands = new int[Survey.Works.Count];
			for (int i = 0; i < Survey.Works.Count; i++)
			{
				demands[i] = Survey.Works[i].GetIntProperty("KingdomStaffNeeded");
			}
			// The water detail is spent before the works are: a settler carrying buckets is not
			// also turning a mill.
			int forWorks = System.Population - System.WaterCrew;
			if (forWorks < 0)
			{
				forWorks = 0;
			}
			// Addendum 7: capability-aware, ablest-first, deterministic (KingdomCrewRules /
			// KingdomCrews). The pool is exactly the forWorks-many settlers hands-spent-once has
			// left for these works; who is capable of what is read off them, never assigned by the
			// founder. Threshold manning is read per work inside AssignWorks, off the same
			// KingdomThresholdManning property the old int[] path passed along beside it.
			KingdomCrewRules.SettlerCapability[] pool = KingdomCrews.CapabilitiesOf(Survey.Settlers, forWorks);
			KingdomCrewRules.CrewOutcome[] outcomes = KingdomCrews.AssignWorks(Survey.Works, pool);
			int idle = 0;
			int shorthanded = 0;
			// LIVING-CITY-ARCHITECTURE §3.2(b) needs a settler's day to be a fact about the PERSON,
			// and until this wave crewing was only ever a fact about the work: every resident row
			// read JobWorkId = 0 and every day shape derived honestly, and uselessly, to the hearth.
			// The stamp is cleared for everybody first so that a settler taken off a mill this pass
			// does not keep walking to it.
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				Simulation.City.KingdomStations.Post(Survey.Settlers[i], 0, Simulation.City.KingdomWorkKind.Other);
			}
			for (int j = 0; j < Survey.Works.Count; j++)
			{
				GameObject work = Survey.Works[j];
				KingdomCrewRules.CrewOutcome outcome = outcomes[j];
				// The pool is CapabilitiesOf(Survey.Settlers, forWorks), built index-for-index off
				// the survey's own list, so an outcome's SettlerIndices name settlers directly.
				int postId = Simulation.City.KingdomCityRules.StableId(work.ID);
				Simulation.City.KingdomWorkKind postKind = Simulation.City.KingdomStations.KindOf(work);
				for (int k = 0; outcome.SettlerIndices != null && k < outcome.SettlerIndices.Length; k++)
				{
					int at = outcome.SettlerIndices[k];
					if (at >= 0 && at < Survey.Settlers.Count)
					{
						Simulation.City.KingdomStations.Post(Survey.Settlers[at], postId, postKind);
					}
				}
				int headcountEffectiveness = KingdomRules.CrewEffectiveness(outcome.Assigned, demands[j]);
				int capabilityEffectiveness = KingdomCrewRules.CapabilityEffectiveness(outcome.BestCapability, outcome.CapabilityThreshold);
				int effectiveness = KingdomCrewRules.CombinedEffectiveness(headcountEffectiveness, capabilityEffectiveness);
				work.SetIntProperty("KingdomStaffed", (effectiveness > 0) ? 1 : 0);
				work.SetIntProperty("KingdomEffectiveness", effectiveness);
				if (effectiveness <= 0)
				{
					idle++;
				}
				else
				{
					if (effectiveness < 100)
					{
						shorthanded++;
					}
					// STANDARDS 7b: a capability shortfall is named once, and unsaid the moment a
					// later pass draws a crew that meets it.
					if (outcome.CapabilityThreshold > 0 && capabilityEffectiveness < 100)
					{
						KingdomCrews.AnnounceShortfall(work, work.ShortDisplayName, outcome.CapabilityKind, outcome.BestCapability, outcome.CapabilityThreshold);
					}
					else
					{
						KingdomCrews.ClearShortfall(work);
					}
					if (work.GetIntProperty("KingdomHandCranked") == 1)
					{
						Capacitor capacitor = work.GetPart<Capacitor>();
						if (capacitor != null)
						{
							int target = capacitor.MaxCharge * effectiveness / 100;
							if (capacitor.Charge < target)
							{
								capacitor.Charge = target;
							}
						}
					}
				}
			}
			System.ShorthandedWorks = shorthanded;
			System.IdleWorks = idle;
			// Hands are spent once. Whatever is crewing a work this pass is not available to walk
			// to the water next pass, which is what turns staffing into a real choice rather than
			// a free bonus.
			int crewed = 0;
			for (int i = 0; i < outcomes.Length; i++)
			{
				crewed += outcomes[i].Assigned;
			}
			System.AssignedCrew = crewed + System.WaterCrew;
			if (idle > 0 && !System.IdleWorksAnnounced)
			{
				System.IdleWorksAnnounced = true;
				MessageQueue.AddPlayerMessage("{{r|" + idle + " of the works of " + System.KingdomDisplayName + " stand idle for want of hands.}}");
			}
			else if (idle == 0)
			{
				System.IdleWorksAnnounced = false;
			}
		}

		/// <param name="System">The realm.</param>
		/// <param name="Z">The zone they walk out of.</param>
		/// <param name="Survey">The pass's survey, or null.</param>
		/// <param name="Leaver">A particular settler, for a departure that is about THEM &mdash;
		/// Addendum 4b's settler who has no home they would live in. Null takes whoever the zone
		/// offers first, which is the drought's own indifference and is right for it.</param>
		/// <param name="Cause">The clause both registers name the departure by. Null is the
		/// drought, which is what this machinery was built for and reads exactly as it always
		/// did.</param>
		/// <param name="Chronicled">Whether this departure gets its own line in both registers and
		/// the ledger. True for every ordinary departure, and for the sampled ones of a long
		/// subsidence slide; false for the ones a slide is carrying in its summary line instead
		/// (<c>KingdomSubsidenceRules.TellsDeparture</c>). The person still leaves, the ledger's
		/// departure COUNT still rises, and the log still records it &mdash; what is saved is a
		/// chronicle entry, because a City falling to Camp would otherwise spend a quarter of the
		/// two-hundred-entry register on one event.</param>
		/// <param name="Note">The same departure in the ledger's shorter voice. Null falls back to
		/// <paramref name="Cause"/>, which is what a caller with only one phrasing wants, and
		/// what every caller written before the two registers wanted different lengths passed.</param>
		public static bool Emigrate(KingdomSystem System, Zone Z, KingdomSurvey Survey = null, GameObject Leaver = null, string Cause = null, bool Chronicled = true, string Note = null)
		{
			if (System.Population <= KingdomRules.LoyalCoreSettlers)
			{
				return false;
			}
			GameObject leaver = null;
			if (Leaver != null)
			{
				// A named departure still answers to the same law as any other: the settlement
				// never empties itself, and a settler the machinery would not take is one who
				// stays and is asked again next pass.
				if (Leaver.GetIntProperty("KingdomBorn") == 1 && Leaver.GetIntProperty("VillageMerchant") == 0 && !Leaver.IsPlayer() && !Leaver.IsPlayerLed())
				{
					leaver = Leaver;
				}
			}
			else
			{
				foreach (GameObject item in Z.GetObjects())
				{
					if (item.GetIntProperty("KingdomBorn") == 1 && item.GetIntProperty("VillageMerchant") == 0 && !item.IsPlayer() && !item.IsPlayerLed())
					{
						leaver = item;
						break;
					}
				}
			}
			if (leaver == null)
			{
				return false;
			}
			string name = leaver.ShortDisplayName;
			string origin = leaver.GetStringProperty("KingdomOrigin");
			if (!string.IsNullOrEmpty(origin))
			{
				System.OriginCounts.TryGetValue(origin, out var count);
				if (count > 0)
				{
					System.OriginCounts[origin] = count - 1;
				}
			}
			string roll = leaver.GetStringProperty("KingdomName");
			if (!string.IsNullOrEmpty(roll))
			{
				int at = System.RosterNames.IndexOf(roll);
				if (at >= 0)
				{
					System.RosterNames.RemoveAt(at);
					if (at < System.RosterOrigins.Count)
					{
						System.RosterOrigins.RemoveAt(at);
					}
					if (at < System.RosterArrived.Count)
					{
						System.RosterArrived.RemoveAt(at);
					}
				}
			}
			KingdomCreed.Forget(System, leaver);
			leaver.Obliterate();
			System.Population--;
			// Both registers name the person and the cause. The default clause is the drought's,
			// word for word as it always read; a caller that hands one in replaces it in both
			// places at once, so the chronicle and the ledger can never disagree about why
			// somebody left.
			string chronicled = string.IsNullOrEmpty(Cause) ? "for wetter country, the cisterns having run dry" : Cause;
			string noted = string.IsNullOrEmpty(Note) ? (string.IsNullOrEmpty(Cause) ? "for wetter country" : Cause) : Note;
			// The count is never sampled, only the telling: a founder who reads the ledger's
			// departure tally gets the true number however the story of it was told.
			System.Ledger.Departures++;
			if (Chronicled)
			{
				KingdomChronicle.Record(System, XRL.Language.Grammar.A(name) + " left " + System.KingdomDisplayName + " " + chronicled);
				System.Ledger.Note(KingdomVoices.Say(System, VoiceOccasion.CitizenLost, "{{R|" + XRL.Language.Grammar.A(name, Capitalize: true) + " left " + System.KingdomDisplayName + " " + noted + ".}}"));
			}
			if (KingdomLog.Enabled) KingdomLog.Log("emigrate: pop now " + System.Population + " origin=" + (origin ?? "-") + " cause=" + (Cause ?? "drought"));
			return true;
		}

		public static int CountStoredWater(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume > 0 && item.GetIntProperty("KingdomStores") == 1 && KingdomLiquids.HasFreshWater(part))
				{
					total += part.Volume;
				}
			}
			return total;
		}

		public static int CountOpenWater(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume < 0 && KingdomLiquids.HasFreshWater(part))
				{
					total += part.Volume;
				}
			}
			return total;
		}

		public static int CountStorageSpace(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume > 0 && item.GetIntProperty("KingdomStores") == 1 && part.Volume < part.MaxVolume && KingdomLiquids.CanReceiveFreshWater(part))
				{
					total += part.MaxVolume - part.Volume;
				}
			}
			return total;
		}

		public static int ConsumeStoredWater(Zone Z, int Drams)
		{
			int remaining = Drams;
			foreach (GameObject item in Z.GetObjects())
			{
				if (remaining <= 0)
				{
					break;
				}
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume > 0 && item.GetIntProperty("KingdomStores") == 1 && KingdomLiquids.HasFreshWater(part))
				{
					remaining -= KingdomLiquids.Drain(part, remaining);
				}
			}
			return Drams - remaining;
		}

		/// <summary>Counts vessels currently dedicated to the settlement's stores in a zone.</summary>
		public static int CountDedicatedVessels(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomStores") == 1)
				{
					total++;
				}
			}
			return total;
		}

		/// <summary>Counts larders currently dedicated to the settlement's food stores in a zone.</summary>
		public static int CountDedicatedLarders(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomLarder") == 1)
				{
					total++;
				}
			}
			return total;
		}

		/// <summary>Counts beds the settlement built. These are the population ceiling.</summary>
		public static int CountBeds(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomBuilt") == 1 && item.HasPart("Bed"))
				{
					total += KingdomRules.BedsPerBunk;
				}
			}
			return total;
		}

		public static int CountStorageCapacity(Zone Z)
		{
			int total = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				LiquidVolume part = item.GetPart<LiquidVolume>();
				if (part != null && part.MaxVolume > 0 && item.GetIntProperty("KingdomStores") == 1)
				{
					total += part.MaxVolume;
				}
			}
			return total;
		}

		/// <summary>
		/// What the settlement has become, both ways, and the reckoning that can move it.
		/// <para>
		/// The ratchet this replaced only ever climbed (<c>if (stage &gt; System.Stage)</c>), so a
		/// City could hold four people and <c>StageFor</c>'s own answer for a collapsed settlement
		/// was computed and thrown away. It now runs in both directions, with a band on the way
		/// down (<c>KingdomSubsidenceRules.StageWithHysteresis</c>) so a rung cannot flap on a
		/// single arrival, and the way DOWN is driven by subsidence rather than by this line: the
		/// reckoning below moves the people, and the stage follows them.
		/// </para>
		/// <para>
		/// Order is load-bearing. The reckoning runs first, because it is what may change the
		/// population and the stage; the rise is then asked of the figures that reckoning left,
		/// so a settlement cannot be promoted on people who have already gone. Raising is
		/// deliberately NOT gated on the supported level: hauling may still carry a settlement to
		/// City, because the pillar promises that a city held up by your own hauling settles
		/// back, not that it could never be raised at all.
		/// </para>
		/// <para>
		/// And the whole of this runs after <see cref="AssignWork"/>, which is what makes the
		/// summation honest: a crewed work carries what the staffing pass says it is running at,
		/// so an unmanned field feeds nobody. The cost of that order is that a departure here
		/// leaves the pass's <c>Survey.Settlers</c> holding an obliterated object &mdash; the same
		/// bargain <see cref="ResolveHeartbeat"/>'s own <see cref="Emigrate"/> already makes, and
		/// safe for the same reason: the only reader of that list is the staffing pass, which has
		/// already run.
		/// </para>
		/// </summary>
		public static void UpdateStage(KingdomSystem System, Zone Z, KingdomSurvey Survey = null)
		{
			int zoneCapacity = (Survey != null) ? Survey.StorageCapacity : CountStorageCapacity(Z);
			KingdomSubsidence.Reckon(System, Z, Survey, The.Game.TimeTicks);
			// Read AFTER Reckon, which writes this zone's own sighting. The ladder measures the
			// city's casks, not the casks of whichever zone the founder walked in through.
			int capacity = KingdomSubsidence.CityStorageCapacity(System, Z, zoneCapacity);
			GrowthStage stage = KingdomSubsidenceRules.StageWithHysteresis(System.Stage, System.Population, capacity);
			if (stage > System.Stage)
			{
				System.Stage = stage;
				string text = System.KingdomDisplayName + " has grown into a " + stage.ToString().ToLower();
				System.RecordDeed("the growth of " + System.KingdomDisplayName + "");
				KingdomChronicle.Record(System, text, Accomplishment: true);
				Popup.Show(KingdomVoices.Say(System, VoiceOccasion.StageUp, "{{C|" + text + ".}}"));
			}
			else if (stage < System.Stage)
			{
				// The rung a settlement loses without a slide: its people were taken by the
				// drought, or its casks were undedicated, and the place is honestly smaller than
				// the ladder says. Said plainly and never popped up - a stage-up is an
				// achievement and interrupts; a stage-down is news, and the ledger is where news
				// belongs.
				GrowthStage lost = System.Stage;
				System.Stage = stage;
				string text = System.KingdomDisplayName + " is a " + stage.ToString().ToLower() + " again, and no longer a " + lost.ToString().ToLower();
				KingdomChronicle.Record(System, text);
				System.Ledger.Note("{{r|" + XRL.Language.Grammar.InitCap(text) + ".}}");
			}
			if (System.HasShopkeeper)
			{
				// The survey already answered this in its single pass; only a call site with
				// no survey (a direct wish, say) needs the fallback scan.
				bool stillTrading = (Survey != null) ? Survey.HasTradePost : StillHasTradePost(Z);
				if (!stillTrading)
				{
					System.HasShopkeeper = false;
					KingdomLog.Log("shopkeeper lost; the post reopens");
				}
			}
			if (System.Stage >= GrowthStage.Steading && !System.HasShopkeeper)
			{
				PromoteShopkeeper(System, Z);
			}
			// A market district stocks the stalls a rung above what the settlement's raw size
			// would otherwise carry.
			int tier = KingdomRules.ShopTierForStage(System.Stage) + KingdomRules.DistrictsShopTierBonus(System.ZoneDistricts.Values);
			if (System.HasShopkeeper && tier > System.ShopTier)
			{
				RestockShops(System, Z, tier);
			}
		}

		private static bool StillHasTradePost(Zone Z)
		{
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("VillageMerchant") == 1 && item.GetIntProperty("KingdomCitizen") == 1)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Raises the settlement's shops to a new stock tier: the trader's stock table and the
		/// per-creature InventoryTier both climb, and the shelves are restocked at once so the
		/// change is visible the moment the player next trades.
		/// </summary>
		public static void RestockShops(KingdomSystem System, Zone Z, int Tier)
		{
			int raised = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("VillageMerchant") != 1 || item.GetIntProperty("KingdomCitizen") != 1)
				{
					continue;
				}
				GenericInventoryRestocker restocker = item.GetPart<GenericInventoryRestocker>();
				if (restocker == null)
				{
					continue;
				}
				restocker.Clear();
				restocker.AddTable("Tier" + Tier + "Wares");
				restocker.Chance = 100;
				item.SetIntProperty("InventoryTier", Tier);
				restocker.PerformRestock(Silent: true);
				raised++;
			}
			if (raised > 0)
			{
				System.ShopTier = Tier;
				KingdomChronicle.Record(System, "the stalls of " + System.KingdomDisplayName + " began carrying finer goods");
				MessageQueue.AddPlayerMessage("{{G|The traders of " + System.KingdomDisplayName + " have better wares to show you.}}");
				if (KingdomLog.Enabled) KingdomLog.Log("shops raised to tier " + Tier + " (" + raised + " traders)");
			}
		}

		public static void PromoteShopkeeper(KingdomSystem System, Zone Z)
		{
			GameObject citizen = null;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomCitizen") == 1 && item.GetIntProperty("VillageMerchant") == 0 && !item.IsPlayer())
				{
					citizen = item;
					break;
				}
			}
			if (citizen == null)
			{
				return;
			}
			GenericInventoryRestocker restocker = citizen.RequirePart<GenericInventoryRestocker>();
			restocker.Clear();
			restocker.AddTable("Tier1Wares");
			restocker.Chance = 100;
			restocker.PerformRestock(Silent: true);
			citizen.SetIntProperty("VillageMerchant", 1);
			TakeOnRoleEvent.Send(citizen, "Merchant");
			System.HasShopkeeper = true;
			KingdomChronicle.Record(System, "a settler took up the trade, and the first stall opened at " + System.KingdomDisplayName);
			MessageQueue.AddPlayerMessage("{{G|A settler has taken up the trade. The first stall of " + System.KingdomDisplayName + " is open.}}");
		}
	}
}
