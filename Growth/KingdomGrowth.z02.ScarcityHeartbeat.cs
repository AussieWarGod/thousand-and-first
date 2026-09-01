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
		/// Draws settlement water for whole elapsed days and runs the water scarcity ladder.
		/// <para>
		/// Water retains its existing physical upkeep flow. Food does not mirror it: elapsed time
		/// creates no ration bill, consumes no larder item, advances no hunger state, and causes no
		/// departure or mark. Meals, recipes, milling, and trade debit physical food only at their
		/// own explicit transaction seams.
		/// </para>
		/// </summary>
		private static bool ResolveHeartbeat(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)
		{
			RetireLegacyFoodState(System);
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
				return true;
			}
			KingdomRules.ThirstOutcome thirst = DrawWater(System, Survey, elapsed, days);
			KingdomRules.ScarcityVerdict verdict = KingdomRules.ComposeScarcity(
				thirst, KingdomRules.HungerOutcome.Fed);
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("scarcity: days=" + days + " thirst=" + thirst
					+ " bite=" + verdict.Bite + " dry=" + System.DryStreak);
			}
			if (verdict.Bite >= KingdomRules.ScarcityBite.Departure)
			{
				Emigrate(System, Z, Survey,
					Cause: KingdomRules.ScarcityDepartureClause(verdict.Thirsting, false),
					Note: KingdomRules.ScarcityDepartureNote(verdict.Thirsting, false));
			}
			if (verdict.Withering && !System.Withered)
			{
				System.Withered = true;
				KingdomChronicle.Record(System, KingdomPresentation.Rich(System.KingdomDisplayName) + " withered in the long thirst");
				System.Ledger.Note("{{R|The settlement is withering in the long thirst.}}");
			}
			return verdict.Healthy;
		}

		/// <summary>
		/// Save migration seam for pre-ruling food state. Kept idempotent and silent: loading an
		/// old hunger streak must neither punish the settlement nor manufacture a recovery deed.
		/// MealShade is retired because capacity gained from food could later become food-caused
		/// subsidence when it disappeared. LastMeal remains as harmless historical evidence.
		/// </summary>
		private static void RetireLegacyFoodState(KingdomSystem System)
		{
			System.HungerStreak = 0;
			System.Famished = false;
			System.ScrapsAnnounced = false;
			System.MealShade = 0;
		}

		/// <summary>
		/// The water half of a resolve: bills the elapsed days against the dedicated stores and
		/// steps the dry streak when they could not cover it. Says its own sentence; applies no
		/// consequence, which belongs to <see cref="ResolveHeartbeat"/>'s one composed verdict.
		/// </summary>
		private static KingdomRules.ThirstOutcome DrawWater(KingdomSystem System, KingdomSurvey Survey, long Elapsed, int Days)
		{
			// Agrarian ground uses water efficiently: it discounts the daily draw before it is made,
			// not after, so a dry agrarian settlement runs its dry streak slower, never zero.
			int upkeep = KingdomRules.PolicyUpkeepForElapsed(System.Population, Elapsed, System.Stores, System.Stage) * KingdomRules.DistrictsUpkeepPercent(System.ZoneDistricts.Values) / 100;
			int paid = Survey.ConsumeUpkeep(upkeep);
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

	}
}
