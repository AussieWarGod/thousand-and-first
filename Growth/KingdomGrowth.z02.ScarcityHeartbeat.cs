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
	public static partial class KingdomGrowth
	{

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
				KingdomChronicle.Record(System, KingdomPresentation.Rich(System.KingdomDisplayName) + " withered in the long thirst");
				System.Ledger.Note("{{R|The settlement is withering in the long thirst.}}");
			}
			if (verdict.Famishing && !System.Famished)
			{
				System.Famished = true;
				KingdomChronicle.Record(System, KingdomPresentation.Rich(System.KingdomDisplayName) + " famished in the long hunger");
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
			KingdomChronicle.Record(System, "the stores ran low, and " + KingdomPresentation.Rich(System.KingdomDisplayName) + " thirsted");
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
			KingdomChronicle.Record(System, "the larders ran empty, and " + KingdomPresentation.Rich(System.KingdomDisplayName) + " went hungry");
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
				string note = KingdomRules.FavoredMealNote(KingdomPresentation.Rich(System.KingdomDisplayName), System.DishName);
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
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			System.Ledger.Note(KingdomRules.ScrapsNote(realm));
			KingdomChronicle.Record(System, "the larders of " + realm + " gave nothing, and the settlement ate what it could find");
		}

		private static void RecoverFromThirst(KingdomSystem System)
		{
			System.DryStreak = 0;
			if (System.Withered)
			{
				System.Withered = false;
				KingdomChronicle.Record(System, "the water returned, and " + KingdomPresentation.Rich(System.KingdomDisplayName) + " drank deep and recovered");
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
				KingdomChronicle.Record(System, "the harvest came in, and " + KingdomPresentation.Rich(System.KingdomDisplayName) + " ate its fill again");
				System.Ledger.Note("{{G|The harvest came in, and the settlement ate its fill again.}}");
			}
		}
	}
}
