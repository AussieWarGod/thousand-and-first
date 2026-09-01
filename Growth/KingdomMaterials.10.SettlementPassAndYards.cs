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
			if (strike != null)
			{
				WorkStrike(System, Z, strike, hands, timeTicks);
				return;
			}
			if (stake != null)
			{
				WorkClearance(System, Z, stakeObject, stake, hands, timeTicks);
			}
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

		/// <summary>
		/// Works one processing yard for the days since it last ran: raw stock out of the
		/// stockpiles, refined material back into them, and the first run of a yard written into
		/// the chronicle.
		/// <para>
		/// Nothing here reads a calendar for anything but "how many days of labour is this crew
		/// owed". A yard nobody staffs makes nothing however long the founder is away, a yard with
		/// no stock makes nothing, both of them say which it was, and neither wears out, because
		/// time is labour and never decay.
		/// </para>
		/// <para>
		/// <b>And a damaged bench shapes less</b> (QB-29). Addendum 10(b) &mdash; damage degrades
		/// function, for every work, in its own kind &mdash; had reached the field
		/// (<c>KingdomCrops</c>) and the network (<c>KingdomNetworks</c>) and never this bench,
		/// which read the staffing pass's crew stretch and stopped there. It reaches it now, in the
		/// EFFORT percent rather than in the head count: those two are the same product, and only
		/// the percent has the resolution to carry it, because every yard in the catalogue stands
		/// two and a condition folded into a head count of two truncates a damaged yard to nobody.
		/// A sound yard is untouched, because a sound work's condition is a hundred; a holed one
		/// shapes the share of its work its condition allows, floored where every other work's is
		/// by <c>KingdomMaterialRules.MaxWearPercent</c>. Mending restores it outright &mdash; the
		/// consequence is of damage, not of history.
		/// </para>
		/// <para>
		/// <b>Idle days are spent, not banked.</b> The day budget advances whether or not anyone
		/// stood at the bench, so an empty yard does not accumulate a debt of labour that a later
		/// crew discharges in one burst. That was already what the code did; what it did not do
		/// was admit it. The gate is now read before the budget is spent, so the two decisions
		/// are made in the order they are explained, and the unstaffed case names itself once
		/// (STANDARDS 7b) instead of leaning on the settlement-wide idle-works tally, which
		/// reports a COUNT and never says which bench it was.
		/// </para>
		/// </summary>
		/// <param name="MethodPercent">What this realm's keepers have worked out, as a percent to
		/// multiply the bench's output by (<c>KingdomResearch.MethodPercent</c>). A hundred is a
		/// realm that has researched nothing, and a hundred changes nothing.</param>
		private static void WorkYard(KingdomSystem System, Zone Z, GameObject Yard, int Strength, int Intelligence, int MethodPercent, long TimeTicks)
		{
			if (!TryRefineryOf(Yard.GetStringProperty(KingdomUpgrade.BuildKeyProperty), out var kind))
			{
				return;
			}
			long worked = ReadTick(Yard, RefineWorkedProperty);
			if (worked <= 0)
			{
				WriteTick(Yard, RefineWorkedProperty, TimeTicks);
				return;
			}
			int days = KingdomRules.ElapsedDays(TimeTicks - worked);
			if (days <= 0)
			{
				return;
			}
			// The gates are read BEFORE the day budget is spent, so the two decisions are made in
			// the order they are explained -- and the budget is spent either way, because idle
			// days are gone rather than banked: an empty bench does not owe its labour to whoever
			// staffs it next. KingdomMaterialRules.AssessYard holds the ordering ("nobody is
			// here" outranks "there is nothing to work"), so the reason the founder is given is
			// the one they can act on.
			//
			// Crew is a HEADCOUNT and answers to the staffing pass alone. The bench's condition is
			// folded into the effort percent below instead of into this count, because every yard
			// in the catalogue stands two: fold a 40% condition into a headcount of two and it
			// truncates to nobody, which would make a damaged yard report "nobody is standing at
			// it" and stop dead, when what is true is that it is holed and shaping less.
			int crew = Yard.GetIntProperty("KingdomStaffNeeded") * Yard.GetIntProperty("KingdomEffectiveness") / 100;
			bool staffed = Yard.GetIntProperty("KingdomStaffed") == 1 && crew > 0;
			MaterialStock stock = null;
			KingdomMaterial raw = default(KingdomMaterial);
			int refinable = 0;
			if (staffed)
			{
				// Reading the whole zone's stockpiles is not free, and there is nobody here to
				// spend them, so an unstaffed yard does not pay for the look.
				stock = Stock(Z);
				refinable = KingdomMaterialRules.RefinableFrom(kind, stock.Tally, out raw);
			}
			KingdomMaterialRules.YardStall stall = KingdomMaterialRules.AssessYard(staffed, crew, refinable);
			WriteTick(Yard, RefineWorkedProperty, KingdomRules.AdvanceCheckpoint(worked, TimeTicks));
			if (stall != KingdomMaterialRules.YardStall.Working)
			{
				bool unstaffed = stall == KingdomMaterialRules.YardStall.Unstaffed;
				string said = unstaffed ? RefineUnstaffedProperty : RefineIdleProperty;
				// Only one stall at a time is true, so the other reason is unsaid: a yard that was
				// short of stock and is now short of hands gets the new sentence, not silence.
				Yard.SetIntProperty(unstaffed ? RefineIdleProperty : RefineUnstaffedProperty, 0);
				if (Yard.GetIntProperty(said) != 1)
				{
					Yard.SetIntProperty(said, 1);
					System.Ledger.Note("{{r|" + KingdomMaterialRules.YardStallLine(stall, kind, KingdomPresentation.Rich(System.SeatName)) + "}}");
				}
				return;
			}
			Yard.SetIntProperty(RefineUnstaffedProperty, 0);
			Yard.SetIntProperty(RefineIdleProperty, 0);
			int capability = KingdomMaterialRules.CrewCapability(kind, Strength, Intelligence);
			// RESEARCH-SYSTEM-DESIGN 8.2, the third factor: crew, then condition, then METHOD --
			// and now all three of them, in that order. Both ride the capability percent into the
			// effort because that is the one percent this bench's arithmetic already carries, and
			// neither is folded into crew -- crew is what makes an unstaffed yard make nothing, and
			// zero times anything is still zero, so no amount of knowledge and no state of repair
			// can staff a bench nobody is standing at (Addendum 8 clause 2).
			//
			// CONDITION is the second of them and it had never been applied at all (QB-29): the
			// KingdomEffectiveness the crew was read off is crew stretch ALONE, and Addendum 10(b)
			// makes each consumer fold its own condition in -- which the crops and the networks do
			// through KingdomWear.EffectivenessOf and this bench, alone among them, did not. The
			// wear comes off KingdomWear.WearOf, which is the single reader EffectivenessOf is
			// itself built on ("absent means sound", stated once); only the SLOT differs, because
			// this is the one consumer whose crew term is a head count rather than a percent.
			//
			// The raw capability is what the founder is TOLD about below: the word describes who is
			// holding the tool, and neither the roof's holes nor the keepers' method is a thing
			// about that crew.
			int conditioned = capability * KingdomMaterialRules.ConditionPercent(KingdomWear.WearOf(Yard)) / 100;
			int methoded = KingdomProductionRules.Methoded(conditioned, MethodPercent);
			int made = KingdomMaterialRules.RefinedThisPass(crew, days, methoded, refinable);
			if (made <= 0)
			{
				return;
			}
			KingdomMaterial refined = KingdomMaterialRules.MadeAt(kind);
			// Take first and count what actually came off the shelf, rather than trusting the
			// reading: something may have emptied it since the survey, and a load that does not
			// make a whole unit goes straight back where it was instead of vanishing.
			int taken = stock.Take(raw, KingdomMaterialRules.RawSpentFor(made));
			made = taken / KingdomMaterialRules.RawPerRefined;
			int returned = taken - KingdomMaterialRules.RawSpentFor(made);
			if (returned > 0)
			{
				stock.Put(raw, returned, Yard.CurrentCell);
			}
			if (made <= 0)
			{
				return;
			}
			int before = stock.Tally.Get(refined);
			int spilled = stock.Put(refined, made, Yard.CurrentCell);
			if (stock.Tally.Get(refined) <= before)
			{
				// Loud rather than quiet: the raw stock is already gone, so an item blueprint that
				// does not exist has eaten it. This is a wiring fault in the mod's own files and
				// nobody's fault in the game, and it must not read as a yard having a slow day.
				MetricsManager.LogError("ThousandAndFirst KingdomMaterials: the " + KingdomMaterialRules.YardName(kind)
					+ " made " + made + " " + KingdomMaterialRules.MaterialName(refined) + " and nothing could be created for it; is "
					+ (BlueprintFor(refined) ?? "its blueprint") + " declared?");
				return;
			}
			string madeLine = made + " " + KingdomMaterialRules.MaterialName(refined);
			if (Yard.GetIntProperty(RefineOpenedProperty) != 1)
			{
				Yard.SetIntProperty(RefineOpenedProperty, 1);
				string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
				KingdomChronicle.Record(System, "the " + KingdomMaterialRules.YardName(kind) + " of " + realm
					+ " ran for the first time, and " + madeLine + " came off it");
				System.RecordDeed("the first work off the " + KingdomMaterialRules.YardName(kind) + " at " + realm);
				MessageQueue.AddPlayerMessage("{{G|The " + KingdomMaterialRules.YardName(kind) + " is working.}} The first "
					+ madeLine + " is stacked where anyone walking past can see it, and the crew is "
					+ KingdomMaterialRules.CapabilityWord(capability) + " at the work.");
			}
			if (spilled > 0)
			{
				System.Ledger.Note("{{r|" + spilled + " off the " + KingdomMaterialRules.YardName(kind)
					+ " was set down on the ground for want of a stockpile to hold it.}}");
			}
			KingdomLog.Log("materials: " + KingdomMaterialRules.YardKey(kind) + " made=" + made + " from=" + KingdomMaterialRules.MaterialKey(raw)
				+ " crew=" + crew + " wear=" + KingdomWear.WearOf(Yard) + " days=" + days + " capability=" + capability
				+ " method=" + MethodPercent + " spilled=" + spilled);
		}

		/// <summary>One settler's stat, or <c>KingdomMaterialRules.BaselineStat</c> when the engine
		/// has none to give. Nobody is punished for being unreadable.</summary>
		private static int StatOf(GameObject Citizen, string Stat)
		{
			Statistic statistic = (Citizen == null) ? null : Citizen.GetStat(Stat);
			return (statistic == null) ? KingdomMaterialRules.BaselineStat : statistic.Value;
		}

	}
}
