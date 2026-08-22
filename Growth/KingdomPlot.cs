using System;
using System.Collections.Generic;

using XRL;
using XRL.Messages;
using XRL.World;

using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently. Only the part moves; the
// settlement-side resolver below stays where the rest of the mod's code lives.
namespace XRL.World.Parts
{
	/// <summary>
	/// What one field carries: the crop the founder committed to it and the tick that crop next
	/// comes ripe. Every design in the food lane that GROWS anything wears this part &mdash; the
	/// kitchen garden, the garden rows, the field, the ploughed fields, the grange and the home
	/// farm &mdash; so the seed gate, the cycle and the rows are one mechanism rather than one
	/// per rung.
	/// <para>
	/// <b>Four fields and no more, deliberately.</b> Parts serialize by positional reflection, so
	/// appending to one is a save-compatibility hazard for every object that already carries it.
	/// Everything this wave needed to remember beyond these four &mdash; the sowing date, the
	/// rows, the seed, the cycle ordinal, the last want announced &mdash; lives in object int and
	/// string properties (<c>KingdomCrops</c>'s <c>*Property</c> constants), which are a
	/// dictionary the engine already serializes and which no layout depends on.
	/// </para>
	/// <para>
	/// <b>The cycle is a stamp, never a countdown.</b> Vanilla's own <c>Harvestable.RegenTimer</c>
	/// is dead code in every shipped blueprint precisely because its clock is turn-delivered and
	/// stops when the zone suspends. This part compares an absolute tick it stored, so missing
	/// ticks costs nothing and a season away resolves in one reckoning.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomPlot : IPart
	{
		public KingdomCropRules.PlotStage Stage;

		/// <summary>
		/// The tick this field's crop next comes ripe at. Meaningful only while
		/// <see cref="Stage"/> is Growing or Ripe; a Dormant field has no crop in it and no clock
		/// running.
		/// </summary>
		public long NextStageTick;

		/// <summary>
		/// What this field grows, decided by the seed that was committed to it and cached so it
		/// cannot silently change crop mid-cycle. Null on uncommitted ground.
		/// </summary>
		public string CropBlueprint;

		/// <summary>Set once a ripe field has already told the founder it has nowhere to put its
		/// harvest, so the wait is announced once rather than on every visit. Kept beside
		/// <c>KingdomCrops.SaidProperty</c> rather than folded into it because this one predates
		/// the property bag and is read by saves that already carry it.</summary>
		public bool NoLarderAnnounced;

		/// <summary>
		/// A crop also comes ripe while the founder is standing there watching it.
		/// <para>
		/// The settlement pass resolves absence, which is the hard half, but it only runs on zone
		/// activation &mdash; so a founder who sows a field and then stays put would see nothing
		/// happen for as long as they stayed. This is the cheap other half, and it has two due
		/// ticks rather than one: the crop comes RIPE on its own tick (a recolour and a line, no
		/// survey), and the settlement's hands GATHER it a day later
		/// (<c>KingdomCropRules.GatherDelayTicks</c>). The day between them is the founder's: a
		/// ripe row carries vanilla <c>Harvestable</c>, and whatever they gather themselves is not
		/// there for the settlement to count.
		/// </para>
		/// </summary>
		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			if (Stage == KingdomCropRules.PlotStage.Dormant)
			{
				return;
			}
			if (Stage == KingdomCropRules.PlotStage.Growing)
			{
				if (TimeTick < NextStageTick)
				{
					return;
				}
				Zone growing = ParentObject?.CurrentZone;
				if (growing == null)
				{
					return;
				}
				KingdomCrops.SetRipe(KingdomCrops.RowsOf(growing, ParentObject), Ripe: true);
				ApplyStage(KingdomCropRules.PlotStage.Ripe);
				MessageQueue.AddPlayerMessage("{{G|The " + ParentObject.ShortDisplayName + " stands ripe.}}");
				return;
			}
			if (!KingdomCropRules.MayGather(NextStageTick, TimeTick))
			{
				return;
			}
			Zone zone = ParentObject?.CurrentZone;
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (zone == null || system == null || !system.Founded || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				return;
			}
			// Surveying is the expensive part, so it happens only on the tick the settlement's own
			// hands are actually due in the field - once per cycle, not once per turn.
			KingdomSystem.Guard("plot tick", delegate
			{
				KingdomPlot.OnSettlementPass(system, zone, KingdomSurvey.Take(zone, system));
			});
		}

		/// <summary>
		/// Recolors the field for its new stage. Presentation only: the blueprint declares its own
		/// tile throughout and only the accent colors move &mdash; exactly the scheme vanilla
		/// <c>Harvestable</c> uses for its own ripe/unripe swap, borrowed here for the ground
		/// itself while the rows standing on it use the real part.
		/// </summary>
		public void ApplyStage(KingdomCropRules.PlotStage NewStage)
		{
			Stage = NewStage;
			XRL.World.Parts.Render render = ParentObject?.Render;
			if (render == null)
			{
				return;
			}
			switch (NewStage)
			{
			case KingdomCropRules.PlotStage.Growing:
				render.ColorString = "&g";
				render.DetailColor = "K";
				break;
			case KingdomCropRules.PlotStage.Ripe:
				render.ColorString = "&G";
				render.DetailColor = "g";
				break;
			default:
				render.ColorString = "&K";
				render.DetailColor = "K";
				break;
			}
		}

		/// <summary>
		/// Vanilla's own irrigation, answered on our clock.
		/// <para>
		/// <c>Hydraulic Irrigator</c> ships a <c>RadiusEventSender Event="AccelerateRipening"
		/// Radius="10" ChargeUse="5"</c>, and <c>Harvestable</c> answers that event by calling
		/// <c>Ripen()</c> &mdash; which returns immediately on every blueprint the game ships,
		/// because none of them arms <c>RegenTime</c>. The machine is real, powered, sited, and
		/// does nothing to any plant in Qud. It does something to a field of ours: each pulse
		/// pulls the stamp ten ticks earlier, so a crop standing inside a running irrigator comes
		/// ripe in half the time. Nothing else about the cycle changes &mdash; the pull is bounded
		/// at now, and it is the stamp that moves rather than the crop.
		/// </para>
		/// </summary>
		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("AccelerateRipening");
			base.Register(Object, Registrar);
		}

		public override bool FireEvent(Event E)
		{
			if (E.ID == "AccelerateRipening" && Stage == KingdomCropRules.PlotStage.Growing && The.Game != null)
			{
				NextStageTick = KingdomCropRules.IrrigatedRipeTick(NextStageTick, The.Game.TimeTicks);
			}
			return base.FireEvent(E);
		}

		/// <summary>
		/// The founder's own two actions on a field: put seed in it, and take that seed back out.
		/// The withdrawal is the protection law made operable &mdash; a committed seed is a
		/// designation, and a designation the founder made is one only the founder unmakes.
		/// </summary>
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (Stage != KingdomCropRules.PlotStage.Dormant)
			{
				E.AddAction("Withdraw Seed", "withdraw seed", "r_WithdrawSeed", null, 'w', FireOnActor: false, 5);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_WithdrawSeed" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomCrops.Withdraw(E.Actor, ParentObject);
			}
			return base.HandleEvent(E);
		}
	}
}

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
			foreach (GameObject item in Z.GetObjects())
			{
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
			System.RecordDeed("the harvest gathered at " + System.KingdomDisplayName);
			KingdomChronicle.Record(System, KingdomCropRules.HarvestChronicle(cycles, yield, System.KingdomDisplayName, daysAgo));
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
				seed = KingdomCropRules.SeedForCrop(Field.CropBlueprint);
			}
			if (string.IsNullOrEmpty(seed) || Survey.Larders.Count == 0)
			{
				return 0;
			}
			int seeds = KingdomCropRules.SeedReturned(
				KingdomChronicle.SettlementId(System.KingdomFactionName), Work.ID, FirstOrdinal, Cycles, Yield);
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
