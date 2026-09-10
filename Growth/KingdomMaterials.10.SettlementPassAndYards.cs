using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomMaterials
	{

		// --- The pass -------------------------------------------------------------------------

		/// <summary>
		/// Works the settlement's one clearing gang for the days since it last swung. Called from
		/// <c>KingdomGrowth.OnZoneActivated</c> after every water-spending step, because clearing
		/// spends no water at all &mdash; it spends hands, and only the hands the water detail and
		/// the works have left over.
		/// <para>
		/// One gang, one job: strike orders first, because ground a building still stands on
		/// cannot be cleared, then clearance stakes in the order the ground yields them. A second
		/// job waits for the next pass rather than being worked by hands the first one already
		/// spent.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom. Does nothing when unfounded.</param>
		/// <param name="Z">The activated ground. Does nothing when it is not the kingdom's.</param>
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || Survey == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			int hands = KingdomMaterialRules.FreeHands(System.Population, System.AssignedCrew);
			GameObject strike = null;
			GameObject stakeObject = null;
			r_KingdomClearance stake = null;
			List<GameObject> yards = new List<GameObject>();
			List<int> strength = new List<int>();
			List<int> intelligence = new List<int>();
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject item = Survey.Built[i];
				if (strike == null && (item.GetIntProperty(StrikeEffortProperty) > 0
					|| HasActiveStrikeReceipt(System, Z, item)))
				{
					strike = item;
				}
				if (KingdomUpgrade.IsFunctionallyBuilt(item)
					&& TryRefineryOf(item.GetStringProperty(KingdomUpgrade.BuildKeyProperty), out _))
				{
					yards.Add(item);
				}
			}
			if (Survey.Clearances.Count > 0)
			{
				stakeObject = Survey.Clearances[0];
				stake = stakeObject?.GetPart<r_KingdomClearance>();
			}
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject item = Survey.Settlers[i];
				// Who the settlement's people actually are. Read, never assigned: the founder
				// does not pick who stands in the yard, and a city of strong backs dresses
				// stone faster than a city of scribes whether anybody planned it that way.
				strength.Add(StatOf(item, "Strength"));
				intelligence.Add(StatOf(item, "Intelligence"));
			}
			// The yards first and unconditionally: they are staffed works, and the staffing pass
			// spent their crews before this ran. Refining takes no hand the clearing gang was ever
			// going to have, so it neither waits on a strike order nor competes with one.
			//
			// The keepers' method is realm-wide (RESEARCH-SYSTEM-DESIGN 8.2 -- the keepers write to
			// each other), so it is read ONCE for the whole ground rather than per bench: every
			// yard in this zone works to the same method, and asking the roster once a yard would
			// walk the tree once a yard for one answer.
			int method = KingdomResearch.MethodPercent(System);
			for (int i = 0; i < yards.Count; i++)
			{
				WorkYard(System, Z, yards[i], KingdomMaterialRules.AverageStat(strength), KingdomMaterialRules.AverageStat(intelligence), method, timeTicks);
			}
			GameObject forageHeart = ForageHeart(Survey);
			r_KingdomForage forage = forageHeart?.RequirePart<r_KingdomForage>();
			int forageDays = forage == null ? 0
				: KingdomMaterialRules.ForageDays(ref forage.LastWorkedTick, timeTicks);
			if (strike != null)
			{
				WorkStrike(System, Z, strike, hands, timeTicks);
				return;
			}
			if (stake != null)
			{
				WorkClearance(System, Z, stakeObject, stake, hands, timeTicks);
				return;
			}
			if (forage != null && !WorkForage(System, Z, Survey, forageHeart, forage, hands, forageDays))
				KingdomMaterialRules.ForageAnnounce(ref forage.BlockedAnnounced, false);
		}

		/// <summary>Tick a yard last turned raw stock into refined, written as a string for the
		/// reason <see cref="StrikeWorkedProperty"/> is: the engine's object properties are ints
		/// and a tick is not, and a serialized field added to a shipped part would move every field
		/// after it and cost players their saves.</summary>
		public const string RefineWorkedProperty = "KingdomRefineWorked";

		/// <summary>Set once a yard has been chronicled for its first run, so the settlement
		/// remembers the day the saws started and never says it twice.</summary>
		public const string RefineOpenedProperty = "KingdomRefineOpened";

		/// <summary>Set once the founder has been told a yard has nothing to work. Cleared the
		/// moment there is stock again, so the reason is given once per stall (STANDARDS 7b).
		/// </summary>
		public const string RefineIdleProperty = "KingdomRefineIdleSaid";

		/// <summary>Set once the founder has been told a yard has nobody standing at it. Cleared
		/// the moment a crew is drawn for it, so an unstaffed yard names itself once per stall
		/// and not once per pass (STANDARDS 7b).</summary>
		public const string RefineUnstaffedProperty = "KingdomRefineUnstaffedSaid";
	}
}
