using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Conversations;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static class KingdomGrowth
	{
		private const string ArrivalMarkerProperty = "r_TAF_GrowthArrivalMarker";
		private const string ArrivalOriginPlanProperty = "r_TAF_GrowthArrivalOriginPlan";
		private const string ArrivalCreedPlanProperty = "r_TAF_GrowthArrivalCreedPlan";
		private const string ArrivalNamePlanProperty = "r_TAF_GrowthArrivalNamePlan";
		private const string ArrivalDatePlanProperty = "r_TAF_GrowthArrivalDatePlan";
		private const string ArrivalEnrollmentReceiptProperty = "r_TAF_GrowthArrivalEnrollment";
		private const string ArrivalRosterReceiptProperty = "r_TAF_GrowthArrivalRoster";
		private const string ArrivalCreedReceiptProperty = "r_TAF_GrowthArrivalCreed";
		private const string ArrivalConversationReceiptProperty = "r_TAF_GrowthArrivalConversation";
		private const string ArrivalConversationText =
			"Live and drink, friend. We heard there was water here, and a place worth the walk.";
		private const string ArrivalConversationGoodbye = "Live and drink.";
		private const string ArrivalConversationQuestion = "Why did you come?";
		private const string ArrivalConversationAnswerPrefix = "The road from ";
		private const string ArrivalConversationAnswerSuffix =
			" was long, and the wells there are bitter. Here the water is shared. That is the whole of it.";
		private const int MaxArrivalConversationNodes = 64;
		private const int MaxArrivalConversationDepth = 16;
		private const int MaxArrivalConversationAttributes = 64;
		private const int MaxArrivalAllegianceDepth = 16;
		private const int MaxArrivalFactionMemberships = 64;

		private enum ArrivalResult
		{
			Failed,
			Deferred,
			Joined,
			Refused,
			WaterUnavailable,
			NoGround,
			PopulationCap,
			SupportCap
		}

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
			if (System == null || !System.Founded || Z == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			KingdomSurvey survey = Shared ?? KingdomSurvey.Take(Z, System);
			int reconciledArrivals;
			bool reconciledOpen;
			ArrivalResult reconciledResult;
			ArrivalRefusal reconciledRefusal;
			if (!SynchronizeArrivalAuthority(System, Z, survey, timeTicks,
				out reconciledArrivals, out reconciledOpen, out reconciledResult,
				out reconciledRefusal))
			{
				return;
			}
			if (!Enabled) return;
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("growth pass " + Z.ZoneID + " tick=" + timeTicks + " next=" + System.NextArrivalTick + " pop=" + System.Population + " stage=" + System.Stage + " stored=" + survey.StoredWater + " open=" + survey.OpenWater + " space=" + survey.StorageSpace + " cap=" + survey.StorageCapacity + " dry=" + System.DryStreak + " withered=" + System.Withered + " food=" + survey.FoodStored + "/" + survey.FoodCapacity + " hunger=" + System.HungerStreak + " famished=" + System.Famished);
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
			if (!PublishArrivalHealth(System, Z, timeTicks, heartbeatHealthy)) return;
			// AFTER the day is eaten, and never before it: industry consumes foodstuffs
			// (Addendum 11(b)) and residents eat first. The order is the whole guarantee - a
			// settlement cannot go hungry because its mill was busy - and KingdomRules.MillableStock
			// keeps a day's rations back on top of it. Same elapsed days the fields were paid for,
			// off the same checkpoint, which is why grownDays is read once and used twice.
			GrindHarvest(System, survey, grownDays);
			int arrivals = reconciledArrivals;
			while (heartbeatHealthy && timeTicks >= System.NextArrivalTick
				&& arrivals < KingdomRules.MaxArrivalsPerVisit)
			{
				// Addendum 4b: the arrival gate is assignment-level, not a bed tally. A settler
				// joins only if a home exists that THEY would take, and the refusal names the real
				// reason -- a city with ten empty beds and no charging post has no room for a
				// robot, and a bed count could never say so.
				ArrivalRefusal refusal;
				ArrivalResult result = ResolveOrStartArrival(System, Z, survey, timeTicks,
					out refusal);
				if (result != ArrivalResult.Joined)
				{
					if (result == ArrivalResult.Failed)
					{
						return;
					}
					break;
				}
				System.NoRoomAnnounced = false;
				arrivals++;
			}
			// The queue still stands due and this pass could seat nobody else - the visit budget
			// is spent, the population is capped, or the band's edge is reached. The overshoot
			// is burned rather than banked, through the same KingdomRules.RestampDeadline the
			// manifest turn-back and the raid re-warn read: a fresh full interval from now, with
			// no witness band, because an arrival slot is spent the instant it comes due. A
			// hundred days away is a settler at the gate, never a hundred of them.
			// Arrival operation owns deadline burn/restamp. Never write this mirror directly: a
			// save between real-world clock CAS and authority receipt must remain reconcilable.
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
			if (System == null || Z == null || The.Game == null
				|| System.ClaimedZones == null || !System.ClaimedZones.Contains(Z.ZoneID)) return false;
			KingdomSurvey survey = Survey ?? KingdomSurvey.Take(Z, System);
			int reconciled;
			bool reconciledOpen;
			ArrivalResult reconciledResult;
			if (!SynchronizeArrivalAuthority(System, Z, survey, The.Game.TimeTicks,
				out reconciled, out reconciledOpen, out reconciledResult, out Refusal)) return false;
			if (reconciledOpen && reconciledResult != ArrivalResult.Deferred)
				return reconciledResult == ArrivalResult.Joined;
			if (!Enabled) return false;
			return ResolveOrStartArrival(System, Z, survey, The.Game.TimeTicks,
				out Refusal) == ArrivalResult.Joined;
		}

		private static bool SynchronizeArrivalAuthority(KingdomSystem system, Zone zone,
			KingdomSurvey survey, long tick, out int reconciledArrivals,
			out bool reconciledOpen, out ArrivalResult reconciledResult,
			out ArrivalRefusal reconciledRefusal)
		{
			reconciledArrivals = 0;
			reconciledOpen = false;
			reconciledResult = ArrivalResult.Failed;
			reconciledRefusal = default(ArrivalRefusal);
			KingdomLifecycleBook parent = system?.LifecycleBook;
			string settlementId = system?.CurrentSettlementId;
			if (parent == null || zone == null || system.ClaimedZones == null
				|| !system.ClaimedZones.Contains(zone.ZoneID) || string.IsNullOrEmpty(settlementId)
				|| !string.Equals(parent.SettlementId, settlementId, StringComparison.Ordinal)
				|| !KingdomLifecycleRules.CanOwnAuthority(parent))
			{
				KingdomLog.Log("growth arrival refused: lifecycle authority is invalid or quarantined");
				return false;
			}
			long interval = Interval(system, zone);
			if (parent.Growth != null && parent.Growth.MigrationPending
				&& !TryMigrateArrivalAuthority(system, parent, tick, interval))
			{
				KingdomLog.Log("growth arrival refused: staged lifecycle migration did not publish");
				return false;
			}
			KingdomGrowthBook growth = parent.Growth;
			if (!KingdomLifecycleRules.CanOwnGrowthAuthority(parent)
				|| !KingdomLifecycleRules.CanOwnGrowthAuthority(growth, settlementId))
			{
				KingdomLog.Log("growth arrival refused: growth authority is invalid or quarantined");
				return false;
			}
			bool lastObservedHealthy = growth.HealthState == KingdomGrowthHealthState.Healthy;
			KingdomGrowthAvailabilityDecision decision =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, Enabled,
					lastObservedHealthy, tick, interval);
			bool open = growth.ArrivalCandidate != null || growth.ArrivalOp != null;
			if (!decision.Valid || (open && system.NextArrivalTick != growth.NextArrivalTick)
				|| (!open && !decision.RestampClocks && system.NextArrivalTick > 0
					&& system.NextArrivalTick != growth.NextArrivalTick))
			{
				KingdomLog.Log("growth arrival refused: real clock cannot bind availability decision");
				return false;
			}
			if (!KingdomLifecycleRules.ApplyGrowthAvailability(growth, decision))
			{
				KingdomLog.Log("growth arrival refused: availability observation did not publish");
				return false;
			}
			if (open)
			{
				reconciledResult = ReconcileArrival(system, zone, survey, tick,
					out reconciledRefusal, null, false);
				if (reconciledResult == ArrivalResult.Failed) return false;
				reconciledOpen = true;
				reconciledArrivals = reconciledResult == ArrivalResult.Joined ? 1 : 0;
			}
			else if (decision.RestampClocks || system.NextArrivalTick <= 0)
			{
				system.NextArrivalTick = growth.NextArrivalTick;
			}
			else if (system.NextArrivalTick != growth.NextArrivalTick)
			{
				KingdomLog.Log("growth arrival refused: real arrival clock differs from lifecycle authority");
				return false;
			}
			return KingdomLifecycleRules.CanOwnGrowthAuthority(parent)
				&& system.NextArrivalTick == growth.NextArrivalTick;
		}

		private static bool TryMigrateArrivalAuthority(KingdomSystem system,
			KingdomLifecycleBook parent, long tick, long interval)
		{
			KingdomGrowthMigrationInput input = new KingdomGrowthMigrationInput
			{
				HasNow = true,
				Now = tick,
				PendingCrop = system.PendingCrop,
				PendingCropBlueprint = system.PendingCropBlueprint,
				PendingCropZoneId = system.PendingCropZoneId,
				OptionEnabled = Enabled,
				ScarcityEnabled = ScarcityEnabled,
				Healthy = false,
				ArrivalIntervalTicks = interval
			};
			KingdomGrowthMigrationResult migration =
				KingdomLifecycleRules.ApplyGrowthMigration(parent, input);
			if (!migration.Valid
				|| !KingdomLifecycleRules.TryPublishGrowthMigration(parent, migration))
				return false;
			system.NextArrivalTick = parent.Growth.NextArrivalTick;
			return KingdomLifecycleRules.CanOwnGrowthAuthority(parent)
				&& system.NextArrivalTick == parent.Growth.NextArrivalTick;
		}

		private static bool PublishArrivalHealth(KingdomSystem system, Zone zone,
			long tick, bool healthy)
		{
			KingdomLifecycleBook parent = system?.LifecycleBook;
			KingdomGrowthBook growth = parent?.Growth;
			string settlementId = system?.CurrentSettlementId;
			if (growth == null || string.IsNullOrEmpty(settlementId)
				|| system.NextArrivalTick != growth.NextArrivalTick
				|| !KingdomLifecycleRules.CanOwnGrowthAuthority(parent)
				|| !KingdomLifecycleRules.CanOwnGrowthAuthority(growth, settlementId)) return false;
			KingdomGrowthAvailabilityDecision decision =
				KingdomLifecycleRules.ObserveGrowthAvailability(growth, Enabled, healthy, tick,
					Interval(system, zone));
			if (!decision.Valid || !KingdomLifecycleRules.ApplyGrowthAvailability(growth,
				decision)) return false;
			system.NextArrivalTick = growth.NextArrivalTick;
			return KingdomLifecycleRules.CanOwnGrowthAuthority(parent)
				&& system.NextArrivalTick == growth.NextArrivalTick;
		}

		private static ArrivalResult ResolveOrStartArrival(KingdomSystem system, Zone zone,
			KingdomSurvey survey, long tick, out ArrivalRefusal refusal)
		{
			refusal = default(ArrivalRefusal);
			KingdomGrowthBook growth = system?.LifecycleBook?.Growth;
			string settlementId = system?.CurrentSettlementId;
			if (growth == null || string.IsNullOrEmpty(settlementId)
				|| !KingdomLifecycleRules.CanOwnGrowthAuthority(system.LifecycleBook)
				|| !KingdomLifecycleRules.CanOwnGrowthAuthority(growth, settlementId)
				|| system.NextArrivalTick != growth.NextArrivalTick
				|| tick < growth.NextArrivalTick) return ArrivalResult.Failed;
			if (growth.ArrivalCandidate != null || growth.ArrivalOp != null)
				return ReconcileArrival(system, zone, survey, tick, out refusal);
			if (system.Population >= KingdomRules.MaxPopulation)
				return StartSimpleArrival(system, zone, tick,
					KingdomGrowthArrivalDisposition.PopulationCap);
			if (system.SupportedLevel > 0 && system.Population >=
				KingdomSubsidenceRules.SlideBeginsAbove(system.SupportedLevel))
				return StartSimpleArrival(system, zone, tick,
					KingdomGrowthArrivalDisposition.SupportCap);
			if (survey == null || survey.StoredWater < KingdomRules.DramsPerArrival)
				return StartSimpleArrival(system, zone, tick,
					KingdomGrowthArrivalDisposition.WaterUnavailable);
			Cell cell = ChooseArrivalCell(zone);
			if (cell == null)
				return StartSimpleArrival(system, zone, tick,
					KingdomGrowthArrivalDisposition.NoGround);
			long sequence = growth.ArrivalCandidateNextSequence;
			string id = KingdomLifecycleRules.GrowthArrivalCandidateId(growth.SettlementId,
				sequence);
			string marker = StableId("arrival-marker", id);
			string escrow = "r_TAF_GrowthArrivalEscrow:" + StableId("arrival-escrow", id);
			string blueprint = SettlerBlueprint();
			string beforeOwner = HashText("arrival-create-owner-before", escrow, zone.ZoneID);
			string beforeObject = HashText("arrival-create-object-before", marker, blueprint);
			string beforeTopology = ArrivalZoneIdentityHash(zone, null, marker, escrow,
				KingdomGrowthLocationKind.Absent, -1, -1);
			KingdomGrowthArrivalCandidate candidate =
				KingdomLifecycleRules.PrepareGrowthArrivalCandidate(growth, marker, blueprint,
					escrow, zone.ZoneID, tick, beforeOwner, beforeObject, beforeTopology);
			if (candidate == null || !KingdomLifecycleRules.TryPublishGrowthArrivalCandidate(
				growth, candidate)) return ArrivalResult.Failed;
			return ReconcileArrival(system, zone, survey, tick, out refusal, cell);
		}

		private static ArrivalResult StartSimpleArrival(KingdomSystem system, Zone zone,
			long tick, KingdomGrowthArrivalDisposition disposition)
		{
			KingdomGrowthBook growth = system.LifecycleBook.Growth;
			KingdomGrowthOperation operation = KingdomLifecycleRules.PrepareGrowthOperation(
				growth, KingdomGrowthAction.Arrival, null, tick);
			if (operation == null) return ArrivalResult.Failed;
			operation.ArrivalDisposition = disposition;
			if (disposition == KingdomGrowthArrivalDisposition.NoGround
				&& !system.NoRoomAnnounced
				&& !AppendArrivalOutbox(system, operation, "no-ground",
					"a settler reached " + system.KingdomDisplayName
						+ " and found nowhere to stand",
					"{{r|A settler came and found nowhere to stand. There is no open ground left here.}}"))
				return ArrivalResult.Failed;
			if (!KingdomLifecycleRules.TryPublishGrowth(growth, operation))
				return ArrivalResult.Failed;
			ArrivalResult result = ReconcileArrival(system, zone, null, tick,
				out ArrivalRefusal ignored);
			if (result == ArrivalResult.NoGround) system.NoRoomAnnounced = true;
			return result;
		}

		private static ArrivalResult ReconcileArrival(KingdomSystem system, Zone zone,
			KingdomSurvey survey, long tick, out ArrivalRefusal refusal, Cell preferred = null,
			bool AllowCandidateConsumption = true)
		{
			refusal = default(ArrivalRefusal);
			KingdomGrowthBook growth = system.LifecycleBook.Growth;
			try
			{
				KingdomGrowthArrivalCandidate candidate = growth.ArrivalCandidate;
				if (candidate == null)
				{
					if (growth.ArrivalOp == null) return ArrivalResult.Failed;
					return CompleteArrivalOperation(system, zone, survey, growth.ArrivalOp,
						null, tick, out refusal);
				}
				if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Quarantined)
					return ArrivalResult.Failed;
				if (candidate.LegacyGrowthV1UnboundZone
					&& !KingdomLifecycleRules.BindLegacyGrowthArrivalCandidateZone(growth,
						candidate, zone.ZoneID, tick))
					return CandidateFault(growth, candidate,
						"historical candidate origin zone could not bind");
				if (!string.Equals(candidate.LodgingZoneId, zone.ZoneID,
					StringComparison.Ordinal)) return ArrivalResult.Deferred;
				GameObject settler = null;
				if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Prepared
					|| candidate.Phase == KingdomGrowthArrivalCandidatePhase.CreateIntent)
				{
					if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Prepared
						&& !KingdomLifecycleRules.BeginGrowthArrivalCandidateCreate(growth,
							candidate, tick)) return CandidateFault(growth, candidate,
							"candidate create intent could not publish");
					if (!TryExactArrivalRoot(candidate, out settler))
					{
						settler = GameObject.Create(candidate.Blueprint);
						if (!GameObject.Validate(settler) || settler.Count != 1)
							return CandidateFault(growth, candidate,
								"candidate create callback did not make one exact object");
						settler.SetStringProperty(ArrivalMarkerProperty, candidate.Marker);
						if (!RootArrivalCandidate(candidate, settler))
							return CandidateFault(growth, candidate,
								"candidate escrow root did not retain the exact object");
					}
					if (!ExactFreshEscrowedCandidate(candidate, settler))
						return CandidateFault(growth, candidate,
							"candidate escrow object is missing, replaced, stacked, or placed");
					if (!PrepareArrivalPersonPlan(system, settler, candidate))
						return CandidateFault(growth, candidate,
							"candidate person plan could not freeze before creation receipt");
					string afterOwner = ArrivalObjectHash(candidate, settler,
						KingdomGrowthLocationKind.Escrow, -1, -1);
					string afterObject = ArrivalPersonHash(settler);
					string afterTopology = ArrivalZoneIdentityHash(zone, settler,
						candidate.Marker, candidate.EscrowKey,
						KingdomGrowthLocationKind.Escrow, -1, -1);
					if (!KingdomLifecycleRules.CommitGrowthArrivalCandidateCreate(growth,
						candidate, settler.ID, afterOwner, afterObject, afterTopology,
						ReferenceHash("candidate-create", candidate, settler), true, tick))
						return CandidateFault(growth, candidate,
							"candidate create receipt did not commit");
				}
				if (!TryArrivalObject(candidate, zone, out settler))
					return CandidateFault(growth, candidate,
						"saved candidate phase cannot prove its exact object");
				if ((candidate.Phase == KingdomGrowthArrivalCandidatePhase.Escrowed
					|| candidate.Phase == KingdomGrowthArrivalCandidatePhase.LodgingIntent
					|| candidate.Phase == KingdomGrowthArrivalCandidatePhase.Observed)
					&& !ExactCreatedCandidate(candidate, settler, zone))
					return CandidateFault(growth, candidate,
						"candidate person plan or creation endpoint changed after receipt");
				if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Escrowed)
				{
					Cell cell = preferred ?? ChooseArrivalCell(zone);
					if (cell == null)
						return CandidateFault(growth, candidate,
							"candidate was escrowed but its lodging cell disappeared");
					KingdomLodgingRules.UnhousedReason ignoredReason;
					string before;
					KingdomLodging.ObservePreparedArrival(system, zone, settler,
						PlannedCreed(settler),
						out ignoredReason, out before);
					if (before == null)
						return CandidateFault(growth, candidate,
							"lodging observation snapshot could not be frozen");
					if (!KingdomLifecycleRules.BeginGrowthArrivalLodgingObservation(growth,
						candidate, zone.ZoneID, cell.X, cell.Y, before, tick))
						return CandidateFault(growth, candidate,
							"lodging observation intent did not publish");
				}
				if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.LodgingIntent)
				{
					KingdomLodgingRules.UnhousedReason reason;
					string observed;
					bool joined = KingdomLodging.ObservePreparedArrival(system, zone, settler,
						PlannedCreed(settler),
						out reason, out observed);
					if (!string.Equals(candidate.LodgingZoneId, zone.ZoneID,
						StringComparison.Ordinal) || zone.GetCell(candidate.LodgingX,
						candidate.LodgingY) == null || !string.Equals(candidate.LodgingBeforeGraphHash,
						observed, StringComparison.Ordinal))
						return CandidateFault(growth, candidate,
							"saved lodging observation no longer matches the real settlement");
					KingdomGrowthArrivalDisposition disposition = joined
						? KingdomGrowthArrivalDisposition.Joined
						: KingdomGrowthArrivalDisposition.NoAcceptableHome;
					KingdomGrowthArrivalRefusalReason frozen = joined
						? KingdomGrowthArrivalRefusalReason.None : ArrivalRefusalReason(reason);
					string receipt = HashText("arrival-lodging-observation",
						candidate.LodgingBeforeGraphHash, disposition.ToString(), frozen.ToString());
					if (!KingdomLifecycleRules.CommitGrowthArrivalLodgingObservation(growth,
						candidate, disposition, frozen, receipt,
						ReferenceHash("candidate-lodging", candidate, settler), true, tick))
						return CandidateFault(growth, candidate,
							"lodging observation receipt did not commit");
				}
				if (candidate.Disposition == KingdomGrowthArrivalDisposition.NoAcceptableHome)
				{
					refusal.NoAcceptableHome = true;
					refusal.Reason = LodgingRefusalReason(candidate.RefusalReason);
				}
				if (growth.ArrivalOp == null
					&& candidate.Phase == KingdomGrowthArrivalCandidatePhase.Observed)
				{
					if (!AllowCandidateConsumption || growth.WorkPaused)
						return ArrivalResult.Deferred;
					if (!PrepareCandidateArrivalOperation(system, zone, survey, candidate,
						settler, tick)) return CandidateFault(growth, candidate,
							"candidate arrival operation could not publish");
				}
				if (growth.ArrivalOp == null)
				{
					if (candidate.Phase != KingdomGrowthArrivalCandidatePhase.Settled)
						return CandidateFault(growth, candidate,
							"candidate lost its consuming arrival operation");
					return RetireArrivalCandidate(system, zone, growth, candidate)
						? CandidateResult(candidate) : ArrivalResult.Failed;
				}
				return CompleteArrivalOperation(system, zone, survey, growth.ArrivalOp,
					candidate, tick, out refusal);
			}
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst transactional arrival", error);
				return QuarantineArrival(growth, "arrival callback threw: " + error.Message);
			}
		}

		private static bool PrepareCandidateArrivalOperation(KingdomSystem system, Zone zone,
			KingdomSurvey survey, KingdomGrowthArrivalCandidate candidate, GameObject settler,
			long tick)
		{
			KingdomGrowthBook growth = system.LifecycleBook.Growth;
			KingdomGrowthOperation operation = KingdomLifecycleRules.PrepareGrowthOperation(
				growth, KingdomGrowthAction.Arrival, null, tick);
			if (operation == null) return false;
			operation.ArrivalDisposition = candidate.Disposition;
			operation.ArrivalCandidateId = candidate.Id;
			if (candidate.Disposition == KingdomGrowthArrivalDisposition.NoAcceptableHome)
			{
				KingdomLodgingRules.UnhousedReason reason =
					LodgingRefusalReason(candidate.RefusalReason);
				if (!system.NoRoomAnnounced && !AppendArrivalOutbox(system, operation,
					"lodging-refusal", KingdomLodgingRules.ArrivalRefusedChronicle(
						system.KingdomDisplayName, reason), "{{r|"
						+ KingdomLodgingRules.ArrivalRefusedNote(reason) + "}}")) return false;
				return KingdomLifecycleRules.TryPublishGrowth(growth, operation);
			}
			if (candidate.Disposition != KingdomGrowthArrivalDisposition.Joined
				|| survey == null || !ExactCreatedCandidate(candidate, settler, zone))
				return false;
			operation.TargetId = candidate.ObjectId;
			operation.TargetMarker = candidate.Marker;
			operation.Blueprint = candidate.Blueprint;
			operation.ZoneId = candidate.LodgingZoneId;
			operation.TargetTopology = KingdomLifecycleTopology.Cell;
			operation.TargetLocation = KingdomGrowthLocationKind.Cell;
			operation.TargetOwnerId = null;
			operation.TargetX = candidate.LodgingX;
			operation.TargetY = candidate.LodgingY;
			operation.PopulationBefore = system.Population;
			operation.PopulationDelta = 1;
			operation.PopulationAfter = system.Population + 1;
			if (!PrepareArrivalWaterLegs(growth, operation, zone, survey,
				KingdomRules.DramsPerArrival)) return false;
			if (!PrepareArrivalDomainSteps(system, growth, operation, settler)) return false;
			string origin = settler.GetStringProperty(ArrivalOriginPlanProperty);
			string reasonText = KingdomRules.ArrivalReason(system.LastDeed,
				tick - system.LastDeedTick, origin);
			if (!AppendArrivalOutbox(system, operation, "joined",
				reasonText + ", and a settler came to " + system.KingdomDisplayName
					+ " and drank of the shared water",
				"{{G|" + XRL.Language.Grammar.InitCap(reasonText)
					+ " - a settler has come.}}")) return false;
			return KingdomLifecycleRules.TryPublishGrowth(growth, operation);
		}

		private static bool PrepareArrivalPersonPlan(KingdomSystem system, GameObject settler,
			KingdomGrowthArrivalCandidate candidate)
		{
			if (!ExactFreshEscrowedCandidate(candidate, settler)
				&& !ExactEscrowedCandidate(candidate, settler)) return false;
			string origin = settler.GetStringProperty(ArrivalOriginPlanProperty);
			if (string.IsNullOrEmpty(origin))
			{
				origin = KingdomRules.Origins[Stat.Random(0, KingdomRules.Origins.Length - 1)];
				settler.SetStringProperty(ArrivalOriginPlanProperty, origin);
			}
			string creed = settler.GetStringProperty(ArrivalCreedPlanProperty);
			if (string.IsNullOrEmpty(creed))
			{
				creed = KingdomCreed.Draw(system) ?? "";
				if (creed.Length == 0) creed = "-";
				settler.SetStringProperty(ArrivalCreedPlanProperty, creed);
			}
			string given = settler.GetStringProperty(ArrivalNamePlanProperty);
			if (string.IsNullOrEmpty(given))
			{
				given = XRL.Names.NameMaker.MakeName(settler, null, null, "human", null,
					system.KingdomFactionName, null, null, null, null, null, null, null,
					FailureOkay: true);
				if (string.IsNullOrEmpty(given)) given = "Settler "
					+ candidate.Sequence.ToString(CultureInfo.InvariantCulture);
				settler.SetStringProperty(ArrivalNamePlanProperty, given);
			}
			string arrived = settler.GetStringProperty(ArrivalDatePlanProperty);
			if (string.IsNullOrEmpty(arrived))
			{
				arrived = XRL.World.Calendar.GetDay() + " of " + XRL.World.Calendar.GetMonth()
					+ ", " + XRL.World.Calendar.GetYear() + " AR";
				settler.SetStringProperty(ArrivalDatePlanProperty, arrived);
			}
			return settler.GetStringProperty(ArrivalOriginPlanProperty) == origin
				&& settler.GetStringProperty(ArrivalCreedPlanProperty) == creed
				&& settler.GetStringProperty(ArrivalNamePlanProperty) == given
				&& settler.GetStringProperty(ArrivalDatePlanProperty) == arrived;
		}

		private static bool PrepareArrivalWaterLegs(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, Zone zone, KingdomSurvey survey, int amount)
		{
			int remaining = amount;
			HashSet<LiquidVolume> seen = new HashSet<LiquidVolume>();
			for (int i = 0; i < survey.Stores.Count && remaining > 0; i++)
			{
				LiquidVolume vessel = survey.Stores[i];
				GameObject owner = vessel?.ParentObject;
				if (vessel == null || !seen.Add(vessel) || !GameObject.Validate(owner)
					|| !ReferenceEquals(owner.GetPart<LiquidVolume>(), vessel)
					|| owner.GetIntProperty("KingdomStores") != 1
					|| owner.CurrentCell == null || !ReferenceEquals(owner.CurrentZone, zone)
					|| !KingdomLiquids.HasFreshWater(vessel) || vessel.MaxVolume <= 0) continue;
				int take = Math.Min(remaining, vessel.Volume);
				int after = vessel.Volume - take;
				string beforeComposition = LiquidComposition(vessel, vessel.Volume);
				string afterComposition = LiquidComposition(vessel, after);
				KingdomGrowthWaterLeg leg = KingdomLifecycleRules.PrepareGrowthWaterLeg(
					growth, operation, KingdomGrowthWaterMutationKind.Drain, owner.ID,
					KingdomLifecycleTopology.Cell, null, owner.Blueprint, zone.ZoneID,
					owner.CurrentCell.X, owner.CurrentCell.Y, vessel.MaxVolume, vessel.Volume,
					take, beforeComposition, afterComposition,
					WaterOwnerHash(owner, vessel.Volume, beforeComposition),
					WaterOwnerHash(owner, after, afterComposition),
					WaterPartHash(owner, vessel.Volume, beforeComposition),
					WaterPartHash(owner, after, afterComposition),
					WaterTopologyHash(zone, owner, vessel.Volume),
					WaterTopologyHash(zone, owner, after));
				if (leg == null) return false;
				operation.WaterLegs.Add(leg);
				remaining -= take;
			}
			return remaining == 0 && operation.WaterLegs.Count > 0;
		}

		private static bool PrepareArrivalDomainSteps(KingdomSystem system,
			KingdomGrowthBook growth, KingdomGrowthOperation operation, GameObject settler)
		{
			KingdomGrowthDomainStep enrollment = PreparePersonDomain(system, growth,
				operation, settler, KingdomGrowthDomainStepKind.Enrollment,
				KingdomGrowthDomainCallbackKind.Enroll, 0L, 1L);
			KingdomGrowthDomainStep roster = PreparePersonDomain(system, growth, operation,
				settler, KingdomGrowthDomainStepKind.Roster,
				KingdomGrowthDomainCallbackKind.RosterAdd, 0L, 1L);
			KingdomGrowthDomainStep creed = PreparePersonDomain(system, growth, operation,
				settler, KingdomGrowthDomainStepKind.Creed,
				KingdomGrowthDomainCallbackKind.CreedSet, 0L, 1L);
			KingdomGrowthDomainStep population = PreparePersonDomain(system, growth,
				operation, settler, KingdomGrowthDomainStepKind.Population,
				KingdomGrowthDomainCallbackKind.PopulationAdjust, system.Population,
				system.Population + 1L);
			KingdomGrowthAccountingSnapshot accountingBefore = AccountingSnapshot(system);
			KingdomGrowthAccountingSnapshot accountingAfter = AccountingSnapshot(system);
			accountingAfter.ArrivalCost += KingdomRules.DramsPerArrival;
			accountingAfter.Arrivals++;
			KingdomGrowthDomainStep accounting = KingdomLifecycleRules.PrepareGrowthDomainStep(
				growth, operation, KingdomGrowthDomainStepKind.Accounting,
				KingdomGrowthDomainCallbackKind.AccountingSet, growth.SettlementId,
				growth.SettlementId, operation.Sequence - 1L, operation.Sequence,
				ArrivalDomainBodyHash(system, operation, settler,
					KingdomGrowthDomainStepKind.Accounting),
				AccountingHash(system, false), AccountingHash(system, true),
				AccountingMapHash(system, false), AccountingMapHash(system, true),
				null, null, accountingBefore, accountingAfter);
			if (enrollment == null || roster == null || creed == null || population == null
				|| accounting == null) return false;
			operation.DomainSteps.Add(enrollment);
			operation.DomainSteps.Add(roster);
			operation.DomainSteps.Add(creed);
			operation.DomainSteps.Add(population);
			operation.DomainSteps.Add(accounting);
			return true;
		}

		private static KingdomGrowthDomainStep PreparePersonDomain(KingdomSystem system,
			KingdomGrowthBook growth, KingdomGrowthOperation operation, GameObject settler,
			KingdomGrowthDomainStepKind kind, KingdomGrowthDomainCallbackKind callback,
			long before, long after)
		{
			return KingdomLifecycleRules.PrepareGrowthDomainStep(growth, operation, kind,
				callback, settler.ID, kind == KingdomGrowthDomainStepKind.Population
					? growth.SettlementId : settler.ID, before, after,
				ArrivalDomainBodyHash(system, operation, settler, kind),
				PersonDomainHash(system, settler, kind, false, operation.Id),
				PersonDomainHash(system, settler, kind, true, operation.Id),
				PersonDomainMapHash(system, settler, kind, false, operation.Id),
				PersonDomainMapHash(system, settler, kind, true, operation.Id));
		}

		private static ArrivalResult CompleteArrivalOperation(KingdomSystem system, Zone zone,
			KingdomSurvey survey, KingdomGrowthOperation operation,
			KingdomGrowthArrivalCandidate candidate, long tick, out ArrivalRefusal refusal)
		{
			refusal = default(ArrivalRefusal);
			KingdomGrowthBook growth = system.LifecycleBook.Growth;
			if (operation == null || operation.Phase == KingdomGrowthPhase.Quarantined)
				return ArrivalResult.Failed;
			if (candidate != null && candidate.Disposition ==
				KingdomGrowthArrivalDisposition.NoAcceptableHome)
			{
				refusal.NoAcceptableHome = true;
				refusal.Reason = LodgingRefusalReason(candidate.RefusalReason);
			}
			if (operation.Phase == KingdomGrowthPhase.Prepared
				&& operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.Joined
				&& !KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.WaterIntent, tick))
				return OperationFault(growth, operation, "water phase did not open");
			if (operation.Phase == KingdomGrowthPhase.WaterIntent)
			{
				if (!ReconcileArrivalWater(growth, operation, zone, survey))
					return OperationFault(growth, operation,
						"real water vessels did not match the saved arrival debit");
				if (!KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.WaterSettled, tick))
					return OperationFault(growth, operation, "water settlement did not publish");
			}
			if (candidate != null && candidate.Phase !=
				KingdomGrowthArrivalCandidatePhase.Settled)
			{
				if (!ReconcileCandidateDisposition(growth, operation, candidate, zone, tick))
					return OperationFault(growth, operation,
						"candidate disposition did not prove one exact object");
			}
			if (operation.Phase == KingdomGrowthPhase.Prepared
				|| operation.Phase == KingdomGrowthPhase.WaterSettled)
			{
				KingdomGrowthPhase next = operation.ArrivalDisposition ==
					KingdomGrowthArrivalDisposition.Joined
						? KingdomGrowthPhase.DomainIntent : KingdomGrowthPhase.ClockIntent;
				if (!KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation, next, tick))
					return OperationFault(growth, operation, "arrival domain/clock phase did not open");
			}
			if (operation.Phase == KingdomGrowthPhase.DomainIntent)
			{
				GameObject settler;
				if (candidate == null || !TryArrivalObject(candidate, zone, out settler)
					|| !ReconcileArrivalDomains(system, growth, operation, settler))
					return OperationFault(growth, operation,
						"arrival domain CAS found a third real-world state");
				if (!KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.DomainSettled, tick))
					return OperationFault(growth, operation, "arrival domain settlement did not publish");
			}
			if (operation.Phase == KingdomGrowthPhase.DomainSettled
				&& !KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.ClockIntent, tick))
				return OperationFault(growth, operation, "arrival clock phase did not open");
			if (operation.Phase == KingdomGrowthPhase.ClockIntent
				&& !ReconcileArrivalClock(system, growth, operation))
				return OperationFault(growth, operation,
					"real arrival clock did not match its saved before/after CAS");
			if (operation.Phase == KingdomGrowthPhase.ClockIntent
				&& !KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.Sinks, tick))
				return OperationFault(growth, operation, "arrival outbox phase did not open");
			if (operation.Phase == KingdomGrowthPhase.Sinks)
			{
				if (!ReconcileArrivalOutbox(system, growth, operation))
					return OperationFault(growth, operation,
						"arrival outbox did not match its saved before/after lists");
				if (!KingdomLifecycleRules.AdvanceGrowthPhase(growth, operation,
					KingdomGrowthPhase.Terminal, tick))
					return OperationFault(growth, operation, "arrival terminal did not publish");
			}
			ArrivalResult result = OperationResult(operation.ArrivalDisposition);
			if (operation.Phase == KingdomGrowthPhase.Terminal)
			{
				if (!KingdomLifecycleRules.RetireGrowth(growth, operation, tick))
					return OperationFault(growth, operation,
						"arrival operation retirement failed");
				system.NextArrivalTick = growth.NextArrivalTick;
			}
			if (candidate != null)
			{
				if (!RetireArrivalCandidate(system, zone, growth, candidate))
					return ArrivalResult.Failed;
				if (result == ArrivalResult.Refused) system.NoRoomAnnounced = true;
				else if (result == ArrivalResult.Joined) system.NoRoomAnnounced = false;
			}
			else if (result == ArrivalResult.NoGround) system.NoRoomAnnounced = true;
			return result;
		}

		private static bool ReconcileArrivalWater(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, Zone zone, KingdomSurvey survey)
		{
			while (operation.WaterCursor < operation.WaterLegs.Count)
			{
				int ordinal = operation.WaterCursor;
				KingdomGrowthWaterLeg leg = operation.WaterLegs[ordinal];
				GameObject owner = zone?.FindObjectByID(leg.ContainerId);
				LiquidVolume vessel = owner?.GetPart<LiquidVolume>();
				if (!ExactWaterEndpoint(zone, owner, vessel, leg, leg.Before))
				{
					if (leg.State == KingdomLifecyclePhysicalState.Intent
						&& ExactWaterEndpoint(zone, owner, vessel, leg, leg.After))
					{
						if (!KingdomLifecycleRules.CommitGrowthWaterCallback(growth,
							operation, ordinal, leg.ContainerId,
							ReferenceHash("arrival-water", leg, vessel), true,
							leg.AfterOwnerGraphHash, leg.AfterPartGraphHash,
							leg.AfterTopologyHash)) return false;
						continue;
					}
					return false;
				}
				if (leg.State == KingdomLifecyclePhysicalState.Prepared
					&& !KingdomLifecycleRules.BeginGrowthWaterCallback(growth, operation,
						ordinal)) return false;
				int removed = KingdomLiquids.Drain(vessel, leg.Delta);
				if (removed != leg.Delta || !ExactWaterEndpoint(zone, owner, vessel, leg,
					leg.After)) return false;
				if (survey != null && survey.Stores.Contains(vessel))
				{
					survey.StoredWater -= removed;
					survey.StorageSpace += removed;
				}
				if (!KingdomLifecycleRules.CommitGrowthWaterCallback(growth, operation,
					ordinal, leg.ContainerId, ReferenceHash("arrival-water", leg, vessel),
					true, leg.AfterOwnerGraphHash, leg.AfterPartGraphHash,
					leg.AfterTopologyHash)) return false;
			}
			return operation.WaterCursor == operation.WaterLegs.Count;
		}

		private static bool ReconcileCandidateDisposition(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, KingdomGrowthArrivalCandidate candidate,
			Zone zone, long tick)
		{
			GameObject settler;
			TryArrivalObject(candidate, zone, out settler);
			bool joined = candidate.Disposition == KingdomGrowthArrivalDisposition.Joined;
			if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Observed)
			{
				if (!ExactEscrowedCandidate(candidate, settler)) return false;
				string beforeOwner = ArrivalObjectHash(candidate, settler,
					KingdomGrowthLocationKind.Escrow, -1, -1);
				string beforeObject = ArrivalPersonHash(settler);
				string beforeTopology = ArrivalZoneIdentityHash(zone, settler,
					candidate.Marker, candidate.EscrowKey,
					KingdomGrowthLocationKind.Escrow, -1, -1);
				string afterOwner = joined ? ArrivalObjectHash(candidate, settler,
					KingdomGrowthLocationKind.Cell, candidate.LodgingX, candidate.LodgingY)
					: HashText("arrival-object-absent", candidate.ObjectId, candidate.Marker,
						candidate.Blueprint);
				string afterObject = joined ? beforeObject : HashText(
					"arrival-person-absent", candidate.ObjectId, candidate.Marker,
					candidate.Blueprint);
				string afterTopology = ArrivalTopologyHash(zone, candidate.ObjectId,
					candidate.Marker, candidate.EscrowKey,
					joined ? KingdomGrowthLocationKind.Cell
						: KingdomGrowthLocationKind.Graveyard,
					joined ? candidate.LodgingX : -1, joined ? candidate.LodgingY : -1);
				if (!KingdomLifecycleRules.BeginGrowthArrivalCandidateDisposition(growth,
					candidate, operation.Id, joined
						? KingdomGrowthObjectMutationKind.CellAdd
						: KingdomGrowthObjectMutationKind.Obliterate,
					joined ? KingdomGrowthLocationKind.Cell
						: KingdomGrowthLocationKind.Graveyard,
					null, joined ? candidate.LodgingZoneId : null,
					joined ? candidate.LodgingX : -1, joined ? candidate.LodgingY : -1,
					beforeOwner, afterOwner, beforeObject, afterObject,
					beforeTopology, afterTopology, tick)) return false;
			}
			KingdomGrowthArrivalCandidatePhase phase = candidate.Phase;
			if (phase != KingdomGrowthArrivalCandidatePhase.ConsumeIntent
				&& phase != KingdomGrowthArrivalCandidatePhase.RefusalIntent) return false;
			KingdomGrowthObjectCallbackStep step = candidate.DispositionStep;
			bool beforeEndpoint = ExactDispositionEndpoint(candidate, settler, zone, step, false);
			bool afterEndpoint = ExactDispositionEndpoint(candidate, settler, zone, step, true);
			if (!beforeEndpoint && !afterEndpoint) return false;
			if (beforeEndpoint && joined)
			{
				if (!ArrivalCellIsStillOpen(zone?.GetCell(candidate.LodgingX,
					candidate.LodgingY))) return false;
				Cell cell = zone?.GetCell(candidate.LodgingX, candidate.LodgingY);
				GameObject accepted = cell.AddObject(settler, NoStack: true, Silent: true);
				if (!ReferenceEquals(accepted, settler)) return false;
				settler.MakeActive();
			}
			else if (beforeEndpoint)
			{
				settler.Obliterate();
			}
			if (!ExactDispositionEndpoint(candidate, settler, zone, step, true)) return false;
			string callbackReference = joined
				? ReferenceHash("candidate-disposition", candidate, settler)
				: HashText("candidate-disposition-absence", candidate.Id, candidate.ObjectId,
					candidate.Marker, candidate.Blueprint);
			return KingdomLifecycleRules.CommitGrowthArrivalCandidateDisposition(growth,
				candidate, callbackReference, joined,
				tick);
		}

		private static bool ReconcileArrivalDomains(KingdomSystem system,
			KingdomGrowthBook growth, KingdomGrowthOperation operation, GameObject settler)
		{
			KingdomGrowthArrivalCandidate candidate = growth?.ArrivalCandidate;
			if (candidate?.CreateStep == null || !string.Equals(
				candidate.CreateStep.AfterObjectGraphHash, ArrivalPersonHash(settler),
				StringComparison.Ordinal)) return false;
			while (operation.DomainCursor < operation.DomainSteps.Count)
			{
				int ordinal = operation.DomainCursor;
				KingdomGrowthDomainStep step = operation.DomainSteps[ordinal];
				if (!string.Equals(step.CallbackBodyHash,
					ArrivalDomainBodyHash(system, operation, settler, step.Kind,
						operation.LegacyGrowthV1Plan),
					StringComparison.Ordinal)) return false;
				string currentGraph = CurrentDomainGraphHash(system, settler, step.Kind,
					operation.Id, operation.LegacyGrowthV1Plan);
				string currentMap = CurrentDomainMapHash(system, settler, step.Kind,
					operation.Id);
				bool before = string.Equals(currentGraph, step.BeforeGraphHash,
					StringComparison.Ordinal) && string.Equals(currentMap,
					step.BeforeMapHash, StringComparison.Ordinal);
				bool after = string.Equals(currentGraph, step.AfterGraphHash,
					StringComparison.Ordinal) && string.Equals(currentMap,
					step.AfterMapHash, StringComparison.Ordinal);
				if (!before && !after) return false;
				if (step.State == KingdomLifecyclePhysicalState.Prepared)
				{
					if (!before || !KingdomLifecycleRules.BeginGrowthDomainCallback(growth,
						operation, ordinal)) return false;
				}
				if (before)
				{
					ApplyArrivalDomain(system, settler, operation, step);
					if (!string.Equals(candidate.CreateStep.AfterObjectGraphHash,
						ArrivalPersonHash(settler), StringComparison.Ordinal)) return false;
					currentGraph = CurrentDomainGraphHash(system, settler, step.Kind,
						operation.Id, operation.LegacyGrowthV1Plan);
					currentMap = CurrentDomainMapHash(system, settler, step.Kind,
						operation.Id);
					if (!string.Equals(currentGraph, step.AfterGraphHash,
						StringComparison.Ordinal) || !string.Equals(currentMap,
						step.AfterMapHash, StringComparison.Ordinal)) return false;
				}
				if (!KingdomLifecycleRules.CommitGrowthDomainCallback(growth, operation,
					ordinal, step.AfterValue, step.AfterGraphHash, step.AfterMapHash))
					return false;
			}
			return operation.DomainCursor == operation.DomainSteps.Count;
		}

		private static string ArrivalDomainBodyHash(KingdomSystem system,
			KingdomGrowthOperation operation, GameObject settler,
			KingdomGrowthDomainStepKind kind, bool legacyV1 = false)
		{
			if (kind == KingdomGrowthDomainStepKind.Accounting)
				return HashText("arrival-domain-body", operation?.Id, "accounting");
			return kind == KingdomGrowthDomainStepKind.Enrollment && !legacyV1
				? HashText("arrival-domain-body:v2", operation?.Id, kind.ToString(),
					settler?.GetStringProperty(ArrivalOriginPlanProperty),
					settler?.GetStringProperty(ArrivalCreedPlanProperty),
					settler?.GetStringProperty(ArrivalNamePlanProperty),
					settler?.GetStringProperty(ArrivalDatePlanProperty),
					system?.KingdomFactionName + "-100", "calm=true", "hostile=false",
					ArrivalConversationText, ArrivalConversationGoodbye,
					ArrivalConversationQuestion, ArrivalConversationAnswerPrefix,
					ArrivalConversationAnswerSuffix)
				: HashText("arrival-domain-body", operation?.Id, kind.ToString(),
					settler?.GetStringProperty(ArrivalOriginPlanProperty),
					settler?.GetStringProperty(ArrivalCreedPlanProperty),
					settler?.GetStringProperty(ArrivalNamePlanProperty),
					settler?.GetStringProperty(ArrivalDatePlanProperty));
		}

		private static void ApplyArrivalDomain(KingdomSystem system, GameObject settler,
			KingdomGrowthOperation operation, KingdomGrowthDomainStep step)
		{
			switch (step.Kind)
			{
			case KingdomGrowthDomainStepKind.Enrollment:
				if (!KingdomFounding.EnrollCitizen(settler))
					throw new InvalidOperationException("citizen enrollment callback refused");
				settler.SetIntProperty("KingdomBorn", 1);
				string origin = settler.GetStringProperty(ArrivalOriginPlanProperty);
				settler.SetStringProperty("KingdomOrigin", origin);
				system.OriginCounts.TryGetValue(origin, out int origins);
				system.OriginCounts[origin] = origins + 1;
				if (settler.GetStringProperty(ArrivalConversationReceiptProperty) != operation.Id)
				{
					Qud.API.ConversationsAPI.addSimpleConversationToObject(settler,
						ArrivalConversationText, ArrivalConversationGoodbye,
						Question: ArrivalConversationQuestion,
						Answer: ArrivalConversationAnswerPrefix + origin
							+ ArrivalConversationAnswerSuffix);
					settler.SetStringProperty(ArrivalConversationReceiptProperty, operation.Id);
				}
				settler.SetStringProperty(ArrivalEnrollmentReceiptProperty, operation.Id);
				break;
			case KingdomGrowthDomainStepKind.Roster:
				string given = settler.GetStringProperty(ArrivalNamePlanProperty);
				settler.DisplayName = given;
				settler.SetStringProperty("KingdomName", given);
				system.RosterNames.Add(given);
				system.RosterOrigins.Add(settler.GetStringProperty(ArrivalOriginPlanProperty));
				system.RosterArrived.Add(settler.GetStringProperty(ArrivalDatePlanProperty));
				settler.SetStringProperty(ArrivalRosterReceiptProperty, operation.Id);
				break;
			case KingdomGrowthDomainStepKind.Creed:
				string creed = PlannedCreed(settler);
				KingdomCreed.Record(system, settler, creed);
				settler.SetStringProperty(ArrivalCreedReceiptProperty, operation.Id);
				break;
			case KingdomGrowthDomainStepKind.Population:
				system.Population++;
				break;
			case KingdomGrowthDomainStepKind.Accounting:
				system.Ledger.ArrivalCost += KingdomRules.DramsPerArrival;
				system.Ledger.Arrivals++;
				break;
			default:
				throw new InvalidOperationException("unexpected arrival domain " + step.Kind);
			}
		}

		private static bool ReconcileArrivalClock(KingdomSystem system, KingdomGrowthBook growth,
			KingdomGrowthOperation operation)
		{
			long current = system.NextArrivalTick;
			KingdomLifecycleCasAction action = KingdomLifecycleRules.GrowthClockAction(growth,
				operation, current);
			if (action == KingdomLifecycleCasAction.Apply)
			{
				if (!KingdomLifecycleRules.BeginGrowthClock(growth, operation, current)) return false;
				system.NextArrivalTick = operation.ClockLease.After;
				current = system.NextArrivalTick;
			}
			else if (operation.ClockState == KingdomLifecyclePhysicalState.Intent
				&& current == operation.ClockLease.Before)
			{
				system.NextArrivalTick = operation.ClockLease.After;
				current = system.NextArrivalTick;
			}
			if (current != operation.ClockLease.After) return false;
			if (operation.ClockState == KingdomLifecyclePhysicalState.Intent
				&& !KingdomLifecycleRules.CommitGrowthClockWitness(growth, operation, current))
				return false;
			return operation.ClockState == KingdomLifecyclePhysicalState.Proved
				&& growth.NextArrivalTick == current;
		}

		private static bool ReconcileArrivalOutbox(KingdomSystem system,
			KingdomGrowthBook growth, KingdomGrowthOperation operation)
		{
			for (int i = 0; i < operation.OutboxEvents.Count; i++)
			{
				KingdomGrowthOutboxEvent e = operation.OutboxEvents[i];
				if (!ReconcileChronicleOutbox(system, growth, operation, e, i)) return false;
				if (!ReconcileInspectableOutbox(system.Ledger.Notes, e.Outbox.Ledger,
					e.LedgerBeforeCount, e.LedgerBeforeHash,
					e.LedgerDeclaredAfterCount, e.LedgerDeclaredAfterHash,
					growth, operation, i, KingdomGrowthOutboxSinkKind.Ledger,
					delegate(string text) { system.Ledger.Note(text); })) return false;
			}
			return true;
		}

		private static bool ReconcileChronicleOutbox(KingdomSystem system,
			KingdomGrowthBook growth, KingdomGrowthOperation operation,
			KingdomGrowthOutboxEvent e, int ordinal)
		{
			if (e.Outbox.Chronicle == null) return e.Outbox.ChronicleState ==
				KingdomLifecycleSinkState.Skipped;
			if (e.LegacySingleRegisterChronicle || system.ChronicleEntries == null
				|| system.OutsiderEntries == null) return false;
			if (!KingdomChronicleReceiptRules.TryHashList("official", system.ChronicleEntries,
				out string official) || !KingdomChronicleReceiptRules.TryHashList("outsider",
					system.OutsiderEntries, out string outsider)) return false;
			KingdomLifecycleCasAction action = KingdomLifecycleRules.GrowthChronicleOutboxAction(
				growth, operation, ordinal, system.ChronicleEntries.Count, official,
				system.OutsiderEntries.Count, outsider);
			if (e.Outbox.ChronicleState == KingdomLifecycleSinkState.Delivered)
				return action == KingdomLifecycleCasAction.Confirm;
			if (action == KingdomLifecycleCasAction.Apply
				&& e.Outbox.ChronicleState == KingdomLifecycleSinkState.Pending)
			{
				if (!KingdomLifecycleRules.BeginGrowthChronicleOutbox(growth, operation,
					ordinal, system.ChronicleEntries.Count, official,
					system.OutsiderEntries.Count, outsider)) return false;
			}
			else if (action != KingdomLifecycleCasAction.Apply
				&& action != KingdomLifecycleCasAction.Confirm) return false;
			string fingerprint;
			if (!KingdomChronicleReceiptRules.TryFingerprint(e.Outbox.ChronicleReceiptId,
				e.Outbox.Chronicle, false, null, out fingerprint)) return false;
			KingdomChronicleDeclaration declaration = new KingdomChronicleDeclaration(
				e.Outbox.ChronicleReceiptId, e.Outbox.Chronicle, false, null, fingerprint,
				e.ChronicleOfficial, e.ChronicleOutsider, e.ChronicleBeforeHash,
				e.ChronicleDeclaredAfterHash, e.OutsiderBeforeHash,
				e.OutsiderDeclaredAfterHash);
			if (!KingdomChronicle.RecordDeclaredOnce(system, declaration)) return false;
			if (!KingdomChronicleReceiptRules.TryHashList("official", system.ChronicleEntries,
				out official) || !KingdomChronicleReceiptRules.TryHashList("outsider",
					system.OutsiderEntries, out outsider)) return false;
			return KingdomLifecycleRules.CommitGrowthChronicleOutbox(growth, operation,
				ordinal, system.ChronicleEntries.Count, official, system.OutsiderEntries.Count,
				outsider);
		}

		private static bool ReconcileInspectableOutbox(List<string> list, string text,
			int beforeCount, string beforeHash, int afterCount, string afterHash,
			KingdomGrowthBook growth, KingdomGrowthOperation operation, int ordinal,
			KingdomGrowthOutboxSinkKind sink, Action<string> append)
		{
			if (text == null) return true;
			if (!TryHashStringList(list, out string current)) return false;
			bool before = list.Count == beforeCount && current == beforeHash;
			bool after = list.Count == afterCount && current == afterHash;
			KingdomLifecycleSinkState state = sink == KingdomGrowthOutboxSinkKind.Chronicle
				? operation.OutboxEvents[ordinal].Outbox.ChronicleState
				: operation.OutboxEvents[ordinal].Outbox.LedgerState;
			if (state == KingdomLifecycleSinkState.Delivered) return after;
			if (!before && !after) return false;
			if (state == KingdomLifecycleSinkState.Pending)
			{
				if (!before || !KingdomLifecycleRules.BeginGrowthInspectableOutbox(growth,
					operation, ordinal, sink, beforeCount, beforeHash)) return false;
				state = KingdomLifecycleSinkState.Intent;
			}
			if (state == KingdomLifecycleSinkState.Intent && before)
			{
				append(text);
				if (!TryHashStringList(list, out current)) return false;
				after = list.Count == afterCount && current == afterHash;
			}
			return state == KingdomLifecycleSinkState.Intent && after
				&& KingdomLifecycleRules.CommitGrowthInspectableOutbox(growth, operation,
					ordinal, sink, afterCount, afterHash);
		}

		private static bool RetireArrivalCandidate(KingdomSystem system, Zone zone,
			KingdomGrowthBook growth, KingdomGrowthArrivalCandidate candidate)
		{
			if (candidate == null || candidate.Phase != KingdomGrowthArrivalCandidatePhase.Settled
				|| growth.ArrivalOp != null) return false;
			GameObject settler;
			TryArrivalObject(candidate, zone, out settler);
			bool physical = candidate.Disposition == KingdomGrowthArrivalDisposition.Joined
				? ExactPlacedCandidate(candidate, settler, zone)
				: ExactRefusedCandidate(candidate, settler, zone);
			if (!physical)
			{
				CandidateFault(growth, candidate,
					"candidate retirement could not prove its settled disposition");
				return false;
			}
			if (The.Game.ObjectGameState.ContainsKey(candidate.EscrowKey))
			{
				object rooted = The.Game.GetObjectGameState(candidate.EscrowKey);
				if (settler != null && !ReferenceEquals(rooted, settler)) return false;
				The.Game.ObjectGameState.Remove(candidate.EscrowKey);
				if (The.Game.ObjectGameState.ContainsKey(candidate.EscrowKey)) return false;
			}
			if (!KingdomLifecycleRules.RetireGrowthArrivalCandidate(growth, candidate))
				return false;
			system.NextArrivalTick = growth.NextArrivalTick;
			return true;
		}

		private static bool AppendArrivalOutbox(KingdomSystem system,
			KingdomGrowthOperation operation, string kind, string chronicleClause,
			string ledger)
		{
			if (system?.ChronicleEntries == null || system.OutsiderEntries == null
				|| system.Ledger?.Notes == null) return false;
			string chronicle = null;
			int chronicleBeforeCount = 0;
			int chronicleAfterCount = 0;
			string chronicleBeforeHash = null;
			string chronicleAfterHash = null;
			int outsiderBeforeCount = 0;
			int outsiderAfterCount = 0;
			string outsiderBeforeHash = null;
			string outsiderAfterHash = null;
			string chronicleOfficial = null;
			string chronicleOutsider = null;
			if (chronicleClause != null)
			{
				string receiptId = KingdomLifecycleRules.GrowthChronicleOutboxReceiptId(
					operation, operation.OutboxEvents.Count);
				KingdomChronicleDeclaration declaration;
				if (receiptId == null || !KingdomChronicle.TryDeclareOnce(system, receiptId,
					chronicleClause, false, null, out declaration)) return false;
				chronicle = chronicleClause;
				chronicleBeforeCount = system.ChronicleEntries.Count;
				chronicleAfterCount = Math.Min(KingdomChronicle.MaxEntries,
					chronicleBeforeCount + 1);
				chronicleBeforeHash = declaration.OfficialBefore;
				chronicleAfterHash = declaration.OfficialAfter;
				outsiderBeforeCount = system.OutsiderEntries.Count;
				outsiderAfterCount = Math.Min(KingdomChronicle.MaxEntries,
					outsiderBeforeCount + 1);
				outsiderBeforeHash = declaration.OutsiderBefore;
				outsiderAfterHash = declaration.OutsiderAfter;
				chronicleOfficial = declaration.Official;
				chronicleOutsider = declaration.Outsider;
			}
			int ledgerBeforeCount = 0;
			int ledgerAfterCount = 0;
			string ledgerBeforeHash = null;
			string ledgerAfterHash = null;
			if (system.Ledger.Notes.Count >= 12) ledger = null;
			if (ledger != null)
			{
				ledgerBeforeCount = system.Ledger.Notes.Count;
				ledgerAfterCount = ledgerBeforeCount + 1;
				if (!TryHashStringList(system.Ledger.Notes, out ledgerBeforeHash)
					|| !TryHashStringListAfter(system.Ledger.Notes, ledger,
						out ledgerAfterHash)) return false;
			}
			KingdomGrowthOutboxEvent e = KingdomLifecycleRules.PrepareDeclaredGrowthOutboxEvent(
				operation, operation.OutboxEvents.Count, "arrival-" + kind, chronicle,
				chronicleOfficial, chronicleOutsider, ledger, null, null, null,
				chronicleBeforeCount, chronicleBeforeHash,
				chronicleAfterCount, chronicleAfterHash, outsiderBeforeCount,
				outsiderBeforeHash, outsiderAfterCount, outsiderAfterHash, ledgerBeforeCount,
				ledgerBeforeHash, ledgerAfterCount, ledgerAfterHash);
			if (e == null) return false;
			operation.OutboxEvents.Add(e);
			return true;
		}

		private static Cell ChooseArrivalCell(Zone zone)
		{
			if (zone == null) return null;
			List<Cell> cells = zone.GetEmptyCells((Cell c) => c.IsPassable()
				&& !c.HasObjectWithPart("LiquidVolume"));
			return cells == null || cells.Count == 0 ? null : cells.GetRandomElement();
		}

		private static bool RootArrivalCandidate(KingdomGrowthArrivalCandidate candidate,
			GameObject settler)
		{
			if (The.Game == null || candidate == null || !GameObject.Validate(settler)) return false;
			object rooted;
			if (The.Game.ObjectGameState.TryGetValue(candidate.EscrowKey, out rooted)
				&& !ReferenceEquals(rooted, settler)) return false;
			The.Game.SetObjectGameState(candidate.EscrowKey, settler);
			return The.Game.ObjectGameState.TryGetValue(candidate.EscrowKey, out rooted)
				&& ReferenceEquals(rooted, settler);
		}

		private static bool TryExactArrivalRoot(KingdomGrowthArrivalCandidate candidate,
			out GameObject settler)
		{
			settler = null;
			object rooted;
			if (The.Game == null || candidate == null
				|| !The.Game.ObjectGameState.TryGetValue(candidate.EscrowKey, out rooted))
				return false;
			settler = rooted as GameObject;
			return settler != null;
		}

		private static bool TryArrivalObject(KingdomGrowthArrivalCandidate candidate,
			Zone zone, out GameObject settler)
		{
			if (TryExactArrivalRoot(candidate, out settler)) return true;
			settler = zone?.FindObjectByID(candidate?.ObjectId);
			return settler != null;
		}

		private static bool ExactEscrowedCandidate(KingdomGrowthArrivalCandidate candidate,
			GameObject settler)
		{
			GameObject rooted;
			return candidate != null && GameObject.Validate(settler)
				&& TryExactArrivalRoot(candidate, out rooted) && ReferenceEquals(rooted, settler)
				&& settler.ID == candidate.ObjectId && settler.Blueprint == candidate.Blueprint
				&& settler.Count == 1 && settler.CurrentCell == null
				&& (settler.Physics == null || settler.Physics.InInventory == null)
				&& settler.GetStringProperty(ArrivalMarkerProperty) == candidate.Marker;
		}

		private static bool ExactFreshEscrowedCandidate(KingdomGrowthArrivalCandidate candidate,
			GameObject settler)
		{
			GameObject rooted;
			return candidate != null && candidate.ObjectId == null
				&& GameObject.Validate(settler)
				&& TryExactArrivalRoot(candidate, out rooted) && ReferenceEquals(rooted, settler)
				&& !string.IsNullOrEmpty(settler.ID)
				&& settler.Blueprint == candidate.Blueprint
				&& settler.Count == 1 && settler.CurrentCell == null
				&& (settler.Physics == null || settler.Physics.InInventory == null)
				&& settler.GetStringProperty(ArrivalMarkerProperty) == candidate.Marker;
		}

		private static bool ExactCreatedCandidate(KingdomGrowthArrivalCandidate candidate,
			GameObject settler, Zone zone)
		{
			KingdomGrowthObjectCallbackStep step = candidate?.CreateStep;
			return step != null && ExactEscrowedCandidate(candidate, settler)
				&& string.Equals(step.AfterOwnerGraphHash,
					ArrivalObjectHash(candidate, settler,
						KingdomGrowthLocationKind.Escrow, -1, -1), StringComparison.Ordinal)
				&& string.Equals(step.AfterObjectGraphHash, ArrivalPersonHash(settler),
					StringComparison.Ordinal)
				&& string.Equals(step.AfterTopologyHash,
					ArrivalZoneIdentityHash(zone, settler, candidate.Marker,
						candidate.EscrowKey, KingdomGrowthLocationKind.Escrow, -1, -1),
					StringComparison.Ordinal);
		}

		private static bool ArrivalCellIsStillOpen(Cell cell)
		{
			return cell != null && cell.IsEmpty() && cell.IsPassable()
				&& !cell.HasObjectWithPart("LiquidVolume");
		}

		private static bool ExactPlacedCandidate(KingdomGrowthArrivalCandidate candidate,
			GameObject settler, Zone zone)
		{
			if (candidate == null || !GameObject.Validate(settler) || zone == null
				|| settler.ID != candidate.ObjectId || settler.Blueprint != candidate.Blueprint
				|| settler.Count != 1 || settler.GetStringProperty(ArrivalMarkerProperty)
					!= candidate.Marker || settler.CurrentCell == null
				|| !ReferenceEquals(settler.CurrentZone, zone)
				|| settler.CurrentCell.X != candidate.LodgingX
				|| settler.CurrentCell.Y != candidate.LodgingY) return false;
			GameObject found = zone.FindObjectByID(candidate.ObjectId);
			return ReferenceEquals(found, settler) && CountArrivalMarker(zone,
				candidate.Marker) == 1;
		}

		private static bool ExactRefusedCandidate(KingdomGrowthArrivalCandidate candidate,
			GameObject settler, Zone zone)
		{
			return candidate != null && candidate.Disposition ==
				KingdomGrowthArrivalDisposition.NoAcceptableHome
				&& (settler == null || !GameObject.Validate(settler))
				&& zone?.FindObjectByID(candidate.ObjectId) == null
				&& CountArrivalMarker(zone, candidate.Marker) == 0;
		}

		private static bool ExactDispositionEndpoint(KingdomGrowthArrivalCandidate candidate,
			GameObject settler, Zone zone, KingdomGrowthObjectCallbackStep step, bool after)
		{
			if (candidate == null || step == null || zone == null) return false;
			bool joined = candidate.Disposition == KingdomGrowthArrivalDisposition.Joined;
			if (!after)
				return ExactEscrowedCandidate(candidate, settler)
					&& string.Equals(step.BeforeOwnerGraphHash,
						ArrivalObjectHash(candidate, settler,
							KingdomGrowthLocationKind.Escrow, -1, -1),
						StringComparison.Ordinal)
					&& string.Equals(step.BeforeObjectGraphHash, ArrivalPersonHash(settler),
						StringComparison.Ordinal)
					&& string.Equals(step.BeforeTopologyHash,
						ArrivalTopologyHash(zone, candidate.ObjectId, candidate.Marker,
							candidate.EscrowKey, KingdomGrowthLocationKind.Escrow, -1, -1),
						StringComparison.Ordinal);
			if (joined)
				return ExactPlacedCandidate(candidate, settler, zone)
					&& string.Equals(step.AfterOwnerGraphHash,
						ArrivalObjectHash(candidate, settler,
							KingdomGrowthLocationKind.Cell, candidate.LodgingX,
							candidate.LodgingY), StringComparison.Ordinal)
					&& string.Equals(step.AfterObjectGraphHash, ArrivalPersonHash(settler),
						StringComparison.Ordinal)
					&& string.Equals(step.AfterTopologyHash,
						ArrivalTopologyHash(zone, candidate.ObjectId, candidate.Marker,
							candidate.EscrowKey, KingdomGrowthLocationKind.Cell,
							candidate.LodgingX, candidate.LodgingY), StringComparison.Ordinal);
			return ExactRefusedCandidate(candidate, settler, zone)
				&& string.Equals(step.AfterOwnerGraphHash,
					HashText("arrival-object-absent", candidate.ObjectId, candidate.Marker,
						candidate.Blueprint), StringComparison.Ordinal)
				&& string.Equals(step.AfterObjectGraphHash,
					HashText("arrival-person-absent", candidate.ObjectId, candidate.Marker,
						candidate.Blueprint), StringComparison.Ordinal)
				&& string.Equals(step.AfterTopologyHash,
					ArrivalTopologyHash(zone, candidate.ObjectId, candidate.Marker,
						candidate.EscrowKey, KingdomGrowthLocationKind.Graveyard, -1, -1),
					StringComparison.Ordinal);
		}

		private static int CountArrivalMarker(Zone zone, string marker)
		{
			if (zone == null || string.IsNullOrEmpty(marker)) return -1;
			int count = 0;
			foreach (GameObject item in zone.GetObjects())
				if (item.GetStringProperty(ArrivalMarkerProperty) == marker) count++;
			return count;
		}

		private static bool ExactWaterEndpoint(Zone zone, GameObject owner,
			LiquidVolume vessel, KingdomGrowthWaterLeg leg, int volume)
		{
			if (zone == null || !GameObject.Validate(owner) || vessel == null
				|| !ReferenceEquals(owner.GetPart<LiquidVolume>(), vessel)
				|| !ReferenceEquals(owner.CurrentZone, zone) || owner.CurrentCell == null
				|| owner.ID != leg.ContainerId || owner.Blueprint != leg.Blueprint
				|| owner.CurrentCell.X != leg.X || owner.CurrentCell.Y != leg.Y
				|| owner.GetIntProperty("KingdomStores") != 1 || vessel.MaxVolume != leg.Capacity
				|| vessel.Volume != volume) return false;
			string composition = LiquidComposition(vessel, volume);
			bool before = volume == leg.Before;
			return composition == (before ? leg.BeforeComposition : leg.AfterComposition)
				&& WaterOwnerHash(owner, volume, composition) == (before
					? leg.BeforeOwnerGraphHash : leg.AfterOwnerGraphHash)
				&& WaterPartHash(owner, volume, composition) == (before
					? leg.BeforePartGraphHash : leg.AfterPartGraphHash)
				&& WaterTopologyHash(zone, owner, volume) == (before
					? leg.BeforeTopologyHash : leg.AfterTopologyHash);
		}

		private static KingdomGrowthArrivalRefusalReason ArrivalRefusalReason(
			KingdomLodgingRules.UnhousedReason reason)
		{
			switch (reason)
			{
			case KingdomLodgingRules.UnhousedReason.NoRoofAtAll:
				return KingdomGrowthArrivalRefusalReason.NoRoofAtAll;
			case KingdomLodgingRules.UnhousedReason.NeedsUnmet:
				return KingdomGrowthArrivalRefusalReason.NeedsUnmet;
			case KingdomLodgingRules.UnhousedReason.Full:
				return KingdomGrowthArrivalRefusalReason.Full;
			case KingdomLodgingRules.UnhousedReason.Refused:
				return KingdomGrowthArrivalRefusalReason.Refused;
			case KingdomLodgingRules.UnhousedReason.Condemned:
				return KingdomGrowthArrivalRefusalReason.Condemned;
			default: return KingdomGrowthArrivalRefusalReason.Refused;
			}
		}

		private static KingdomLodgingRules.UnhousedReason LodgingRefusalReason(
			KingdomGrowthArrivalRefusalReason reason)
		{
			switch (reason)
			{
			case KingdomGrowthArrivalRefusalReason.NoRoofAtAll:
				return KingdomLodgingRules.UnhousedReason.NoRoofAtAll;
			case KingdomGrowthArrivalRefusalReason.NeedsUnmet:
				return KingdomLodgingRules.UnhousedReason.NeedsUnmet;
			case KingdomGrowthArrivalRefusalReason.Full:
				return KingdomLodgingRules.UnhousedReason.Full;
			case KingdomGrowthArrivalRefusalReason.Condemned:
				return KingdomLodgingRules.UnhousedReason.Condemned;
			default: return KingdomLodgingRules.UnhousedReason.Refused;
			}
		}

		private static ArrivalResult CandidateResult(KingdomGrowthArrivalCandidate candidate)
		{
			return candidate.Disposition == KingdomGrowthArrivalDisposition.Joined
				? ArrivalResult.Joined : ArrivalResult.Refused;
		}

		private static ArrivalResult OperationResult(KingdomGrowthArrivalDisposition disposition)
		{
			switch (disposition)
			{
			case KingdomGrowthArrivalDisposition.Joined: return ArrivalResult.Joined;
			case KingdomGrowthArrivalDisposition.NoAcceptableHome: return ArrivalResult.Refused;
			case KingdomGrowthArrivalDisposition.WaterUnavailable: return ArrivalResult.WaterUnavailable;
			case KingdomGrowthArrivalDisposition.NoGround: return ArrivalResult.NoGround;
			case KingdomGrowthArrivalDisposition.PopulationCap: return ArrivalResult.PopulationCap;
			case KingdomGrowthArrivalDisposition.SupportCap: return ArrivalResult.SupportCap;
			default: return ArrivalResult.Failed;
			}
		}

		private static ArrivalResult CandidateFault(KingdomGrowthBook growth,
			KingdomGrowthArrivalCandidate candidate, string fault)
		{
			string safe = BoundedFault(fault);
			bool quarantined = KingdomLifecycleRules.QuarantineGrowthArrivalCandidate(
				growth, candidate, safe);
			KingdomLog.Log("growth arrival candidate " + (quarantined ? "quarantined: "
				: "stopped with retained evidence: ") + safe);
			return ArrivalResult.Failed;
		}

		private static ArrivalResult OperationFault(KingdomGrowthBook growth,
			KingdomGrowthOperation operation, string fault)
		{
			string safe = BoundedFault(fault);
			bool quarantined = KingdomLifecycleRules.QuarantineGrowthOperation(growth,
				operation, safe);
			KingdomLog.Log("growth arrival operation " + (quarantined ? "quarantined: "
				: "stopped with retained evidence: ") + safe);
			return ArrivalResult.Failed;
		}

		private static ArrivalResult QuarantineArrival(KingdomGrowthBook growth, string fault)
		{
			if (growth?.ArrivalOp != null)
				return OperationFault(growth, growth.ArrivalOp, fault);
			if (growth?.ArrivalCandidate != null)
				return CandidateFault(growth, growth.ArrivalCandidate, fault);
			KingdomLog.Log("growth arrival failed before quarantine evidence could bind: " + fault);
			return ArrivalResult.Failed;
		}

		private static string BoundedFault(string fault)
		{
			if (string.IsNullOrEmpty(fault)) return "arrival callback failed";
			int length = Math.Min(fault.Length, KingdomLifecycleRules.MaxTextChars);
			if (length > 0 && length < fault.Length && char.IsHighSurrogate(fault[length - 1]))
				length--;
			return fault.Substring(0, length);
		}

		private static string CurrentDomainGraphHash(KingdomSystem system,
			GameObject settler, KingdomGrowthDomainStepKind kind, string operationId,
			bool legacyV1 = false)
		{
			return kind == KingdomGrowthDomainStepKind.Accounting
				? AccountingHash(system, false)
				: PersonDomainHash(system, settler, kind, false, operationId, legacyV1);
		}

		private static string CurrentDomainMapHash(KingdomSystem system,
			GameObject settler, KingdomGrowthDomainStepKind kind, string operationId)
		{
			return kind == KingdomGrowthDomainStepKind.Accounting
				? AccountingMapHash(system, false)
				: PersonDomainMapHash(system, settler, kind, false, operationId);
		}

		private static string PersonDomainHash(KingdomSystem system, GameObject settler,
			KingdomGrowthDomainStepKind kind, bool projectedAfter, string operationId,
			bool legacyV1 = false)
		{
			if (kind == KingdomGrowthDomainStepKind.Enrollment && !legacyV1)
			{
				if (!ArrivalAllegianceRepresentable(settler?.Brain?.Allegiance)) return null;
				ConversationScript actual = projectedAfter
					? null : settler?.GetPart<ConversationScript>();
				ConversationXMLBlueprint conversation = projectedAfter
					? ExpectedArrivalConversationBlueprint(settler?.ID,
						settler?.GetStringProperty(ArrivalOriginPlanProperty))
					: actual?.Blueprint;
				if (!ArrivalConversationRepresentable(conversation))
					return null;
			}
			return Hash(delegate(BinaryWriter writer)
			{
				WriteString(writer, "arrival-domain-graph");
				writer.Write((byte)kind); WriteString(writer, settler?.ID);
				WriteString(writer, settler?.Blueprint);
				switch (kind)
				{
				case KingdomGrowthDomainStepKind.Enrollment:
					bool hasBrain = settler?.Brain != null;
					writer.Write(hasBrain);
					if (legacyV1)
					{
						WriteString(writer, projectedAfter && hasBrain
							? system.KingdomFactionName : settler?.GetPrimaryFaction());
						writer.Write(hasBrain && (projectedAfter
							|| settler.Brain.Allegiance.Calm));
						writer.Write(hasBrain && !projectedAfter
							&& settler.Brain.Allegiance.Hostile);
						writer.Write(projectedAfter ? 1
							: settler.GetIntProperty("KingdomCitizen"));
						writer.Write(projectedAfter ? 1
							: settler.GetIntProperty("KingdomBorn"));
						WriteString(writer, projectedAfter
							? settler.GetStringProperty(ArrivalOriginPlanProperty)
							: settler.GetStringProperty("KingdomOrigin"));
						WriteString(writer, projectedAfter ? operationId
							: settler.GetStringProperty(ArrivalEnrollmentReceiptProperty));
						WriteString(writer, projectedAfter ? operationId
							: settler.GetStringProperty(ArrivalConversationReceiptProperty));
						writer.Write(projectedAfter
							|| settler != null && settler.HasPart<ConversationScript>());
						break;
					}
					bool baseReplaced = false;
					WriteAllegianceGraph(writer, settler?.Brain?.Allegiance, projectedAfter,
						system?.KingdomFactionName, true, 0, ref baseReplaced);
					WriteString(writer, projectedAfter && hasBrain
						? system.KingdomFactionName : settler?.GetPrimaryFaction());
					writer.Write(hasBrain && (projectedAfter
						|| settler.Brain.Allegiance.Calm));
					writer.Write(hasBrain && !projectedAfter
						&& settler.Brain.Allegiance.Hostile);
					writer.Write(projectedAfter ? 1 : settler.GetIntProperty("KingdomCitizen"));
					writer.Write(projectedAfter ? 1 : settler.GetIntProperty("KingdomBorn"));
					string origin = projectedAfter
						? settler.GetStringProperty(ArrivalOriginPlanProperty)
						: settler.GetStringProperty("KingdomOrigin");
					WriteString(writer, origin);
					WriteString(writer, projectedAfter ? operationId
						: settler.GetStringProperty(ArrivalEnrollmentReceiptProperty));
					WriteString(writer, projectedAfter ? operationId
						: settler.GetStringProperty(ArrivalConversationReceiptProperty));
					WriteArrivalConversationGraph(writer, settler, projectedAfter, origin);
					break;
				case KingdomGrowthDomainStepKind.Roster:
					WriteString(writer, projectedAfter
						? settler.GetStringProperty(ArrivalNamePlanProperty)
						: settler.DisplayName);
					WriteString(writer, projectedAfter
						? settler.GetStringProperty(ArrivalNamePlanProperty)
						: settler.GetStringProperty("KingdomName"));
					WriteString(writer, projectedAfter ? operationId
						: settler.GetStringProperty(ArrivalRosterReceiptProperty));
					break;
				case KingdomGrowthDomainStepKind.Creed:
					WriteString(writer, projectedAfter ? PlannedCreed(settler)
						: settler.GetStringProperty(KingdomCreed.CreedProperty));
					WriteString(writer, projectedAfter ? operationId
						: settler.GetStringProperty(ArrivalCreedReceiptProperty));
					break;
				case KingdomGrowthDomainStepKind.Population:
					writer.Write(projectedAfter ? system.Population + 1 : system.Population);
					break;
				}
			});
		}

		private static bool ArrivalAllegianceRepresentable(AllegianceSet allegiance)
		{
			List<AllegianceSet> seen = new List<AllegianceSet>();
			int depth = 0;
			while (allegiance != null)
			{
				if (depth > MaxArrivalAllegianceDepth
					|| allegiance.Count > MaxArrivalFactionMemberships
					|| !ArrivalAllyReasonRepresentable(allegiance.Reason)) return false;
				for (int i = 0; i < seen.Count; i++)
					if (ReferenceEquals(seen[i], allegiance)) return false;
				seen.Add(allegiance);
				allegiance = allegiance.Previous;
				depth++;
			}
			return true;
		}

		private static bool ArrivalAllyReasonRepresentable(IAllyReason reason)
		{
			if (reason == null) return true;
			switch (reason.GetType().FullName)
			{
			case "XRL.World.AI.AllyAscend":
			case "XRL.World.AI.AllyBeguile":
			case "XRL.World.AI.AllyBirth":
			case "XRL.World.AI.AllyBond":
			case "XRL.World.AI.AllyClan":
			case "XRL.World.AI.AllyClone":
			case "XRL.World.AI.AllyConstructed":
			case "XRL.World.AI.AllyCurio":
			case "XRL.World.AI.AllyDefault":
			case "XRL.World.AI.AllyHoundmaster":
			case "XRL.World.AI.AllyPack":
			case "XRL.World.AI.AllyPet":
			case "XRL.World.AI.AllyPilot":
			case "XRL.World.AI.AllyProselytize":
			case "XRL.World.AI.AllyRebuke":
			case "XRL.World.AI.AllyRetinue":
			case "XRL.World.AI.AllySummon":
			case "XRL.World.AI.AllyWish":
				return true;
			default:
				return false;
			}
		}

		private static bool ArrivalConversationRepresentable(
			ConversationXMLBlueprint blueprint)
		{
			int remaining = MaxArrivalConversationNodes;
			return ArrivalConversationRepresentable(blueprint, 0, ref remaining,
				new List<ConversationXMLBlueprint>());
		}

		private static bool ArrivalConversationRepresentable(
			ConversationXMLBlueprint blueprint, int depth, ref int remaining,
			List<ConversationXMLBlueprint> lineage)
		{
			if (blueprint == null) return true;
			if (depth > MaxArrivalConversationDepth || remaining <= 0
				|| blueprint.Attributes != null && blueprint.Attributes.Count
					> MaxArrivalConversationAttributes
				|| blueprint.Children != null && blueprint.Children.Count
					> MaxArrivalConversationNodes) return false;
			for (int i = 0; i < lineage.Count; i++)
				if (ReferenceEquals(lineage[i], blueprint)) return false;
			remaining--;
			lineage.Add(blueprint);
			if (blueprint.Children != null)
				for (int i = 0; i < blueprint.Children.Count; i++)
					if (!ArrivalConversationRepresentable(blueprint.Children[i], depth + 1,
						ref remaining, lineage)) return false;
			lineage.RemoveAt(lineage.Count - 1);
			return true;
		}

		private static void WriteAllegianceGraph(BinaryWriter writer, AllegianceSet allegiance,
			bool projectedAfter, string kingdomFaction, bool top, int depth,
			ref bool baseReplaced)
		{
			if (allegiance == null) { writer.Write((byte)0); return; }
			writer.Write((byte)1);
			writer.Write(allegiance.SourceID);
			int flags = allegiance.Flags;
			if (projectedAfter && top) flags = (flags | 2) & -2;
			writer.Write(flags);
			WriteString(writer, allegiance.Reason?.GetType().FullName);
			if (allegiance.Reason != null)
			{
				writer.Write(allegiance.Reason.Time);
				IAllyReasonSourced sourced = allegiance.Reason as IAllyReasonSourced;
				writer.Write(sourced != null);
				if (sourced != null) WriteString(writer, sourced.Name);
			}
			bool replace = projectedAfter && !baseReplaced && allegiance.SourceID == 0;
			if (replace)
			{
				baseReplaced = true;
				writer.Write(1); WriteString(writer, kingdomFaction); writer.Write(100);
			}
			else
			{
				List<KeyValuePair<string, int>> memberships =
					new List<KeyValuePair<string, int>>(allegiance);
				memberships.Sort(delegate(KeyValuePair<string, int> a,
					KeyValuePair<string, int> b)
				{
					int byName = string.CompareOrdinal(a.Key, b.Key);
					return byName != 0 ? byName : a.Value.CompareTo(b.Value);
				});
				writer.Write(memberships.Count);
				for (int i = 0; i < memberships.Count; i++)
				{
					WriteString(writer, memberships[i].Key);
					writer.Write(memberships[i].Value);
				}
			}
			WriteAllegianceGraph(writer, allegiance.Previous, projectedAfter, kingdomFaction,
				false, depth + 1, ref baseReplaced);
		}

		private static void WriteArrivalConversationGraph(BinaryWriter writer,
			GameObject settler, bool projectedAfter, string origin)
		{
			ConversationScript conversation = projectedAfter
				? new ConversationScript
				{
					Blueprint = ExpectedArrivalConversationBlueprint(settler?.ID, origin)
				}
				: settler?.GetPart<ConversationScript>();
			writer.Write(conversation != null);
			if (conversation == null) return;
			writer.Write(conversation.RecordConversationAsProperty);
			WriteString(writer, conversation.ConversationID);
			WriteString(writer, conversation.Quest);
			WriteString(writer, conversation.PreQuestConversationID);
			WriteString(writer, conversation.InQuestConversationID);
			WriteString(writer, conversation.PostQuestConversationID);
			writer.Write(conversation.ClearLost); writer.Write(conversation.ChargeUse);
			WriteString(writer, conversation.Filter);
			WriteString(writer, conversation.FilterExtras);
			WriteString(writer, conversation.Color);
			WriteString(writer, conversation.Append);
			WriteString(writer, projectedAfter ? "1"
				: settler?.GetStringProperty("SuppressPowerSwitchTwiddle"));
			int remaining = MaxArrivalConversationNodes;
			WriteConversationBlueprint(writer, conversation.Blueprint, 0, ref remaining);
		}

		private static ConversationXMLBlueprint ExpectedArrivalConversationBlueprint(
			string objectId, string origin)
		{
			ConversationXMLBlueprint blueprint = new ConversationXMLBlueprint
			{
				ID = "CustomConversation::" + objectId,
				Name = "Conversation"
			};
			Qud.API.ConversationsAPI.AddChoice(
				Qud.API.ConversationsAPI.AddStart(blueprint, ArrivalConversationText),
				null, "End", ArrivalConversationGoodbye);
			ConversationXMLBlueprint answer = Qud.API.ConversationsAPI.AddNode(blueprint,
				null, ArrivalConversationAnswerPrefix + origin + ArrivalConversationAnswerSuffix);
			Qud.API.ConversationsAPI.AddChoice(answer, null, "End", ArrivalConversationGoodbye);
			Qud.API.ConversationsAPI.AddChoice(blueprint.GetChild("Start"), null, answer,
				ArrivalConversationQuestion);
			return blueprint;
		}

		private static void WriteConversationBlueprint(BinaryWriter writer,
			ConversationXMLBlueprint blueprint, int depth, ref int remaining)
		{
			if (blueprint == null) { writer.Write((byte)0); return; }
			remaining--;
			writer.Write((byte)1);
			WriteString(writer, blueprint.ID); WriteString(writer, blueprint.Name);
			WriteString(writer, blueprint.Text); WriteString(writer, blueprint.Inherits);
			writer.Write(blueprint.Cardinal); writer.Write(blueprint.References);
			WriteString(writer, blueprint.Distribute);
			writer.Write(blueprint.Qualifier); writer.Write(blueprint.Load);
			if (blueprint.Attributes == null) writer.Write(-1);
			else
			{
				List<string> keys = new List<string>(blueprint.Attributes.Keys);
				keys.Sort(StringComparer.Ordinal);
				writer.Write(keys.Count);
				for (int i = 0; i < keys.Count; i++)
				{
					WriteString(writer, keys[i]);
					WriteString(writer, blueprint.Attributes[keys[i]]);
				}
			}
			if (blueprint.Children == null) writer.Write(-1);
			else
			{
				writer.Write(blueprint.Children.Count);
				for (int i = 0; i < blueprint.Children.Count; i++)
					WriteConversationBlueprint(writer, blueprint.Children[i], depth + 1,
						ref remaining);
			}
		}

		private static string PersonDomainMapHash(KingdomSystem system, GameObject settler,
			KingdomGrowthDomainStepKind kind, bool projectedAfter, string operationId)
		{
			return Hash(delegate(BinaryWriter writer)
			{
				WriteString(writer, "arrival-domain-map"); writer.Write((byte)kind);
				switch (kind)
				{
				case KingdomGrowthDomainStepKind.Enrollment:
					WriteDictionary(writer, system.OriginCounts,
						projectedAfter ? settler.GetStringProperty(ArrivalOriginPlanProperty) : null,
						projectedAfter ? 1 : 0);
					break;
				case KingdomGrowthDomainStepKind.Roster:
					WriteList(writer, system.RosterNames, projectedAfter
						? settler.GetStringProperty(ArrivalNamePlanProperty) : null);
					WriteList(writer, system.RosterOrigins, projectedAfter
						? settler.GetStringProperty(ArrivalOriginPlanProperty) : null);
					WriteList(writer, system.RosterArrived, projectedAfter
						? settler.GetStringProperty(ArrivalDatePlanProperty) : null);
					break;
				case KingdomGrowthDomainStepKind.Creed:
					WriteDictionary(writer, system.CreedCounts,
						projectedAfter && !string.IsNullOrEmpty(PlannedCreed(settler))
							? PlannedCreed(settler) : null,
						projectedAfter && !string.IsNullOrEmpty(PlannedCreed(settler)) ? 1 : 0);
					WriteString(writer, projectedAfter ? operationId
						: settler.GetStringProperty(ArrivalCreedReceiptProperty));
					break;
				case KingdomGrowthDomainStepKind.Population:
					writer.Write(projectedAfter ? system.Population + 1 : system.Population);
					break;
				}
			});
		}

		private static KingdomGrowthAccountingSnapshot AccountingSnapshot(KingdomSystem system)
		{
			KingdomLedger ledger = system.Ledger;
			return new KingdomGrowthAccountingSnapshot
			{
				Fetched = ledger.Fetched, UpkeepDrawn = ledger.UpkeepDrawn,
				ArrivalCost = ledger.ArrivalCost, Delivered = ledger.Delivered,
				Harvested = ledger.Harvested, Foraged = ledger.Foraged,
				RationsDrawn = ledger.RationsDrawn, Milled = ledger.Milled,
				HarvestLost = ledger.HarvestLost, Plundered = ledger.Plundered,
				Arrivals = ledger.Arrivals, Departures = ledger.Departures
			};
		}

		private static string AccountingHash(KingdomSystem system, bool projectedAfter)
		{
			KingdomGrowthAccountingSnapshot x = AccountingSnapshot(system);
			if (projectedAfter)
			{
				x.ArrivalCost += KingdomRules.DramsPerArrival;
				x.Arrivals++;
			}
			return Hash(delegate(BinaryWriter writer)
			{
				WriteString(writer, "arrival-accounting-graph");
				writer.Write(x.Fetched); writer.Write(x.UpkeepDrawn); writer.Write(x.ArrivalCost);
				writer.Write(x.Delivered); writer.Write(x.Harvested); writer.Write(x.Foraged);
				writer.Write(x.RationsDrawn); writer.Write(x.Milled); writer.Write(x.HarvestLost);
				writer.Write(x.Plundered); writer.Write(x.Arrivals); writer.Write(x.Departures);
			});
		}

		private static string AccountingMapHash(KingdomSystem system, bool projectedAfter)
		{
			return HashText("arrival-accounting-map", AccountingHash(system, projectedAfter),
				system.Ledger.Notes.Count.ToString(CultureInfo.InvariantCulture));
		}

		private static string PlannedCreed(GameObject settler)
		{
			string value = settler?.GetStringProperty(ArrivalCreedPlanProperty);
			return value == "-" || value == null ? "" : value;
		}

		private static string ArrivalPersonHash(GameObject settler)
		{
			return Hash(delegate(BinaryWriter writer)
			{
				WriteString(writer, "arrival-person"); WriteString(writer, settler?.ID);
				WriteString(writer, settler?.Blueprint); writer.Write(settler?.Count ?? -1);
				WriteString(writer, settler?.GetStringProperty(ArrivalMarkerProperty));
				WriteString(writer, settler?.GetStringProperty(ArrivalOriginPlanProperty));
				WriteString(writer, settler?.GetStringProperty(ArrivalCreedPlanProperty));
				WriteString(writer, settler?.GetStringProperty(ArrivalNamePlanProperty));
				WriteString(writer, settler?.GetStringProperty(ArrivalDatePlanProperty));
			});
		}

		private static string ArrivalObjectHash(KingdomGrowthArrivalCandidate candidate,
			GameObject settler, KingdomGrowthLocationKind location, int x, int y)
		{
			return HashText("arrival-object-location", settler?.ID ?? candidate?.ObjectId,
				candidate?.Marker,
				candidate?.Blueprint, location.ToString(),
				location == KingdomGrowthLocationKind.Cell ? candidate?.LodgingZoneId : null,
				x.ToString(CultureInfo.InvariantCulture), y.ToString(CultureInfo.InvariantCulture),
				ArrivalPersonHash(settler));
		}

		private static string ArrivalZoneIdentityHash(Zone zone, GameObject settler,
			string marker, string escrow, KingdomGrowthLocationKind location, int x, int y)
		{
			return ArrivalTopologyHash(zone, settler?.ID, marker, escrow, location, x, y);
		}

		private static string ArrivalTopologyHash(Zone zone, string objectId,
			string marker, string escrow, KingdomGrowthLocationKind location, int x, int y)
		{
			return HashText("arrival-topology", zone?.ZoneID, objectId, marker, escrow,
				location.ToString(), x.ToString(CultureInfo.InvariantCulture),
				y.ToString(CultureInfo.InvariantCulture));
		}

		private static string LiquidComposition(LiquidVolume vessel, int projectedVolume)
		{
			if (projectedVolume == 0) return "empty";
			if (vessel?.ComponentLiquids == null) return "invalid";
			List<string> keys = new List<string>(vessel.ComponentLiquids.Keys);
			keys.Sort(StringComparer.Ordinal);
			StringBuilder result = new StringBuilder();
			for (int i = 0; i < keys.Count; i++)
			{
				if (i > 0) result.Append(';');
				result.Append(keys[i]).Append('=').Append(vessel.ComponentLiquids[keys[i]]
					.ToString(CultureInfo.InvariantCulture));
			}
			return result.Length == 0 ? "empty" : result.ToString();
		}

		private static string WaterOwnerHash(GameObject owner, int volume, string composition)
		{
			return HashText("arrival-water-owner", owner?.ID, owner?.Blueprint,
				owner?.GetIntProperty("KingdomStores").ToString(CultureInfo.InvariantCulture),
				owner?.CurrentZone?.ZoneID,
				owner?.CurrentCell?.X.ToString(CultureInfo.InvariantCulture),
				owner?.CurrentCell?.Y.ToString(CultureInfo.InvariantCulture),
				volume.ToString(CultureInfo.InvariantCulture), composition);
		}

		private static string WaterPartHash(GameObject owner, int volume, string composition)
		{
			LiquidVolume vessel = owner?.GetPart<LiquidVolume>();
			return HashText("arrival-water-part", owner?.ID,
				vessel?.MaxVolume.ToString(CultureInfo.InvariantCulture),
				volume.ToString(CultureInfo.InvariantCulture), composition);
		}

		private static string WaterTopologyHash(Zone zone, GameObject owner, int volume)
		{
			return HashText("arrival-water-topology", zone?.ZoneID, owner?.ID,
				owner?.CurrentCell?.X.ToString(CultureInfo.InvariantCulture),
				owner?.CurrentCell?.Y.ToString(CultureInfo.InvariantCulture),
				volume.ToString(CultureInfo.InvariantCulture));
		}

		private static string StableId(string domain, string value)
		{
			return HashText(domain, value);
		}

		private static string ReferenceHash(string domain, object authority, object reference)
		{
			string id = reference is GameObject obj ? obj.ID
				: reference is LiquidVolume liquid ? liquid.ParentObject?.ID
				: reference?.GetType().FullName;
			return HashText("arrival-reference", domain, authority?.GetType().FullName, id);
		}

		private static string HashText(params string[] values)
		{
			return Hash(delegate(BinaryWriter writer)
			{
				writer.Write(values == null ? -1 : values.Length);
				if (values != null) for (int i = 0; i < values.Length; i++)
					WriteString(writer, values[i]);
			});
		}

		private static string Hash(Action<BinaryWriter> write)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
			{
				WriteString(writer, "taf:growth-arrival-runtime:v1");
				write(writer); writer.Flush();
				using (SHA256 sha = SHA256.Create())
				{
					byte[] digest = sha.ComputeHash(stream.ToArray());
					StringBuilder text = new StringBuilder(64);
					for (int i = 0; i < digest.Length; i++)
						text.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
					return text.ToString();
				}
			}
		}

		private static void WriteString(BinaryWriter writer, string value)
		{
			if (value == null) { writer.Write(-1); return; }
			byte[] bytes = Encoding.UTF8.GetBytes(value);
			writer.Write(bytes.Length); writer.Write(bytes);
		}

		private static void WriteList(BinaryWriter writer, List<string> list, string append)
		{
			int count = (list?.Count ?? 0) + (append == null ? 0 : 1);
			writer.Write(count);
			if (list != null) for (int i = 0; i < list.Count; i++) WriteString(writer, list[i]);
			if (append != null) WriteString(writer, append);
		}

		private static void WriteDictionary(BinaryWriter writer,
			Dictionary<string, int> dictionary, string incrementKey, int increment)
		{
			Dictionary<string, int> projected = new Dictionary<string, int>(
				dictionary ?? new Dictionary<string, int>(), StringComparer.Ordinal);
			if (incrementKey != null && increment != 0)
			{
				projected.TryGetValue(incrementKey, out int before);
				projected[incrementKey] = before + increment;
			}
			List<string> keys = new List<string>(projected.Keys);
			keys.Sort(StringComparer.Ordinal); writer.Write(keys.Count);
			for (int i = 0; i < keys.Count; i++)
			{
				WriteString(writer, keys[i]); writer.Write(projected[keys[i]]);
			}
		}

		private static bool TryHashStringList(List<string> list, out string hash)
		{
			hash = null;
			if (list == null) return false;
			try
			{
				hash = Hash(delegate(BinaryWriter writer)
				{
					WriteString(writer, "arrival-outbox-list"); WriteList(writer, list, null);
				});
				return true;
			}
			catch { hash = null; return false; }
		}

		private static bool TryHashStringListAfter(List<string> list, string append,
			out string hash)
		{
			hash = null;
			if (list == null || append == null) return false;
			try
			{
				hash = Hash(delegate(BinaryWriter writer)
				{
					WriteString(writer, "arrival-outbox-list"); WriteList(writer, list, append);
				});
				return true;
			}
			catch { hash = null; return false; }
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
