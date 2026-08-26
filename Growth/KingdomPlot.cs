using System;
using System.Collections.Generic;

using XRL;
using XRL.Messages;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// Resolves every field in a zone on the settlement's own clock. Called from
	/// <see cref="KingdomGrowth.OnZoneActivated"/> after every other water-consuming step in the
	/// pass, so a field only ever spends what the day's upkeep and arrivals left behind.
	/// <para>
	/// <b>What a pass here actually does</b> (Addendum 11(b-ii), in order): count the ripenings
	/// that came due since the last one, closed form; gather what is standing; credit the CITY at
	/// once; put the physical crop into a pantry here if there is one, onto the road to another
	/// zone's pantry if the city has room there, and on the ground if it has neither; restamp the
	/// cycle from the harvest rather than from now; and tell the founder ONCE, with a count, no
	/// matter how many seasons went by.
	/// </para>
	/// </summary>
	public static class KingdomPlot
	{
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!KingdomGrowth.Enabled || System == null || !System.Founded || Z == null || Survey == null)
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			// Snapshotted first: gathering spawns crop items into containers in this same zone,
			// and walking the live object list while that happens throws.
			List<GameObject> fields = new List<GameObject>();
			for (int i = 0; i < Survey.Objects.Count; i++)
			{
				GameObject item = Survey.Objects[i];
				if (KingdomCrops.FieldOf(item) != null)
				{
					fields.Add(item);
				}
			}
			int cycles = 0;
			int yield = 0;
			int delivered = 0;
			int pending = 0;
			int lost = 0;
			int seeds = 0;
			long lastDue = 0L;
			for (int i = 0; i < fields.Count; i++)
			{
				Resolve(System, Z, fields[i], Survey, timeTicks, ref cycles, ref yield, ref delivered, ref pending, ref lost, ref seeds, ref lastDue);
			}
			if (cycles <= 0)
			{
				return;
			}
			// STANDARDS 8's "one arrival per day, each attributable" has a chronicle cousin: a
			// season of harvests is ONE line with a count in it. Twelve entries for one farm
			// would spend a sixteenth of the whole register on a field doing exactly what it was
			// built to do.
			int daysAgo = (lastDue > 0L && timeTicks > lastDue) ? KingdomRules.ElapsedDays(timeTicks - lastDue) : 0;
			System.Ledger.Harvested += delivered + pending;
			System.Ledger.HarvestLost += lost;
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			System.RecordDeed("the harvest gathered at " + realm);
			KingdomChronicle.Record(System, KingdomCropRules.HarvestChronicle(cycles, yield, realm, daysAgo));
			string note = KingdomCropRules.HarvestNote(cycles, yield, delivered, pending, lost);
			System.Ledger.Note("{{G|" + note + "}}");
			MessageQueue.AddPlayerMessage("{{G|" + note + "}}");
			if (seeds > 0)
			{
				System.Ledger.Note("{{G|" + seeds + ((seeds == 1) ? " seed came" : " seeds came") + " back out of the harvest, and is in the larders.}}");
			}
			if (KingdomLog.Enabled) KingdomLog.Log("crop pass: cycles=" + cycles + " yield=" + yield + " delivered=" + delivered + " pending=" + pending + " lost=" + lost + " seeds=" + seeds);
		}

		/// <summary>
		/// Walks one field to now. Every early return names a want first (STANDARDS 7b) except
		/// the ones that are not applicable at all &mdash; unsown ground says so, an unworked
		/// field says so, a ripe field with nowhere to put its crop says so, and a field that is
		/// simply still growing says nothing, because nothing is wrong with it.
		/// </summary>
		private static void Resolve(KingdomSystem System, Zone Z, GameObject Work, KingdomSurvey Survey, long TimeTicks,
			ref int Cycles, ref int Yield, ref int Delivered, ref int Pending, ref int Lost, ref int Seeds, ref long LastDue)
		{
			r_KingdomPlot field = KingdomCrops.FieldOf(Work);
			if (field == null)
			{
				return;
			}
			if (field.Stage == KingdomCropRules.PlotStage.Dormant)
			{
				KingdomCrops.Announce(System, Work, KingdomCropRules.FieldWant.Seed);
				return;
			}
			// A crop with nothing left in the ground is not a crop. This heals a field whose rows
			// were burnt, eaten or struck, and a field carried over from a build that stamped no
			// rows at all: it goes back to bare ground and asks for seed, rather than standing
			// forever as a growing field that yields nothing and never says why.
			List<GameObject> rows = KingdomCrops.RowsOf(Z, Work);
			if (rows.Count == 0)
			{
				field.CropBlueprint = null;
				field.NextStageTick = 0L;
				field.ApplyStage(KingdomCropRules.PlotStage.Dormant);
				Work.SetIntProperty(KingdomCrops.RowsProperty, 0);
				KingdomCrops.Announce(System, Work, KingdomCropRules.FieldWant.Seed);
				return;
			}
			if (KingdomCrops.IsCondemned(Work))
			{
				KingdomCrops.Announce(System, Work, KingdomCropRules.FieldWant.Condemned);
				return;
			}
			int effectiveness = KingdomWear.EffectivenessOf(Work);
			if (effectiveness <= 0)
			{
				// Idleness does nothing where labour is the gate (Addendum 11(b-ii)). The stamp is
				// deliberately NOT advanced: the crop is standing there whether anyone weeds it,
				// and the moment hands arrive the whole waiting harvest is theirs.
				KingdomCrops.Announce(System, Work, KingdomCropRules.FieldWant.Hands);
				return;
			}
			// The settlement's own hands wait out the founder's day before they gather, so a
			// founder standing in a field that has just come ripe gets first call on it. The crop
			// they are being left is the NEWEST ripening; everything older than it is gathered
			// now, and the stamp lands on the one being held so the next pass finds it due.
			bool holdLast;
			int gather = KingdomCropRules.GatherableCycles(field.NextStageTick, TimeTicks, out holdLast);
			if (gather <= 0)
			{
				KingdomCrops.Announce(System, Work, KingdomCropRules.FieldWant.None);
				return;
			}
			// The ONE cycle whose rows can have been interfered with is the crop the founder was
			// actually looking at. A field resolved out of an absence was never made ripe at all
			// (TurnTick does not run in a suspended zone), and a field whose newest crop is being
			// HELD is being gathered only of crops older than the one in front of them - so both
			// credit every cycle at what stands.
			bool countsRipeLast = !holdLast && field.Stage == KingdomCropRules.PlotStage.Ripe;
			int standing = rows.Count;
			int yield = KingdomCropRules.GatheredYield(
				standing, countsRipeLast ? KingdomCrops.CountRipe(rows) : standing, gather, countsRipeLast, effectiveness,
				KingdomResearch.MethodPercent(System));
			long lastDue = KingdomCropRules.LastRipeTick(field.NextStageTick, gather);
			field.NextStageTick = KingdomCropRules.RestampedRipeTick(field.NextStageTick, gather);
			if (holdLast)
			{
				// The stamp now names the held ripening itself. Its rows stand ripe and the field
				// reads ripe, whether or not anybody was here when it turned.
				KingdomCrops.SetRipe(rows, Ripe: true);
				field.ApplyStage(KingdomCropRules.PlotStage.Ripe);
			}
			else
			{
				KingdomCrops.SetRipe(rows, Ripe: false);
				field.ApplyStage(KingdomCropRules.PlotStage.Growing);
			}
			ulong firstOrdinal = (ulong)(uint)Work.GetIntProperty(KingdomCrops.CyclesProperty);
			Work.SetIntProperty(KingdomCrops.CyclesProperty, (int)(firstOrdinal + (ulong)gather));
			Cycles += gather;
			Yield += yield;
			if (lastDue > LastDue)
			{
				LastDue = lastDue;
			}
			if (yield <= 0)
			{
				KingdomCrops.Announce(System, Work, KingdomCropRules.FieldWant.None);
				return;
			}
			int deliveredHere;
			int pendingHere;
			int lostHere = KingdomCrops.Deposit(System, Z, Survey, field.CropBlueprint, yield, out deliveredHere, out pendingHere);
			Delivered += deliveredHere;
			Pending += pendingHere;
			Lost += lostHere;
			if (deliveredHere <= 0 && pendingHere <= 0)
			{
				KingdomCrops.Announce(System, Work, KingdomCropRules.FieldWant.Larder);
				field.NoLarderAnnounced = true;
				return;
			}
			field.NoLarderAnnounced = false;
			KingdomCrops.Announce(System, Work, KingdomCropRules.FieldWant.None);
			Seeds += ReturnSeed(System, Survey, Work, field, firstOrdinal, gather, yield);
		}

		/// <summary>
		/// Whether this gathering also handed back sowable seed, and the seed itself into the
		/// larders if it did. One of the three honest ways to get seed, and the one a working farm
		/// eventually lives on: the draw is counter-based on the field and this cycle's own
		/// ordinal, so a reload asks the same question and gets the same answer.
		/// </summary>
		private static int ReturnSeed(KingdomSystem System, KingdomSurvey Survey, GameObject Work, r_KingdomPlot Field, ulong FirstOrdinal, int Cycles, int Yield)
		{
			string seed = Work.GetStringProperty(KingdomCrops.SeedProperty);
			if (string.IsNullOrEmpty(seed))
			{
				seed = KingdomData.SeedForCrop(Field.CropBlueprint);
			}
			if (string.IsNullOrEmpty(seed) || Survey.Larders.Count == 0)
			{
				return 0;
			}
			int seeds = KingdomCropRules.SeedReturned(
				KingdomChronicle.SettlementId(System), Work.ID, FirstOrdinal, Cycles, Yield);
			int placed = 0;
			for (int i = 0; i < seeds; i++)
			{
				GameObject container = Survey.Larders[0];
				if (container?.Inventory == null)
				{
					break;
				}
				GameObject item = GameObject.Create(seed);
				if (item == null)
				{
					break;
				}
				// Seed is not food and is deliberately not counted as any: it goes into the same
				// chest the harvest went into because that is where a farm keeps it, and
				// KingdomSurvey's food count reads Food and PreparedCookingIngredient only, so a
				// larder full of seed still reads as an empty larder to the ration draw.
				container.Inventory.AddObject(item, Silent: true);
				placed++;
			}
			return placed;
		}
	}
}
