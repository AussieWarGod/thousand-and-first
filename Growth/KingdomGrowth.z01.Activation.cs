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

		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Shared = null)
		{
			if (!KingdomMaster.AutomaticWorkAllowed(System)) return;
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
			// The Growth option owns arrivals only. Support, food and water, staffing, plots,
			// lodging, power, materials, and roads remain independent modules and must still
			// reconcile when arrivals are disabled.
			bool arrivalsEnabled = Enabled;
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("growth pass " + Z.ZoneID + " tick=" + timeTicks + " next=" + System.NextArrivalTick + " pop=" + System.Population + " stage=" + System.Stage + " stored=" + survey.StoredWater + " open=" + survey.OpenWater + " space=" + survey.StorageSpace + " cap=" + survey.StorageCapacity + " dry=" + System.DryStreak + " withered=" + System.Withered + " food=" + survey.FoodStored + "/" + survey.FoodCapacity);
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
			// Fields deliver physical crops on their own plot cycle. This stamp belongs to mills: a mill
			// does not make food out of the day, it takes real crops off real shelves and puts real
			// staples back, on the seated ground, where the shelves are. That is why it was never
			// in an abstract city rate, and it is why it keeps a stamp of its own. One clock each, and
			// neither can spend the other's days.
			// W7 repair, the second leg of the same defect. This stamp is SETTLEMENT-wide and the
			// mills it pays for stand in a ZONE, so a founder walking through a mill-less quarter
			// used to advance it and spend the mill quarter's days on nothing: the crops were
			// never ground and the days were gone. Gated on the seat actually holding a millstone.
			// Nothing accrues without bound: GrindHarvest can take only exact crop objects currently
			// held in the larders, so a long absence cannot invent an input.
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
			if (!AdvanceArrivalCadence(System, Z, timeTicks)) return;
			// Industry is a physical transformation independent of water heartbeat. Exact input
			// stock and an operating mill bound the elapsed work.
			GrindHarvest(System, survey, grownDays);
			int arrivals = reconciledOpen && reconciledResult != ArrivalResult.Deferred ? 1 : 0;
			while (arrivalsEnabled && heartbeatHealthy
				&& System.LifecycleBook.Growth.ArrivalOpportunity != null
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
					if (result == ArrivalResult.Deferred) break;
				}
				arrivals++;
				if (result == ArrivalResult.Joined) System.NoRoomAnnounced = false;
				if (!AdvanceArrivalCadence(System, Z, timeTicks)) return;
				if (result != ArrivalResult.Joined) break;
			}
			// Physical work is bounded above. Unmaterialized semantic heads remain persisted debt.
			KingdomHostedArcology.PrepareStaffing(System, survey);
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
			KingdomLodging.OnSettlementPass(System, Z, survey);
			// Immediately after lodging, and never before it: who shares a roof this pass is the
			// whole input to osmosis (Addendum 5). Spends no water and no hands -- shared living is
			// the only thing it counts, and it counts it in attended passes, so a founder who is
			// away converts nobody and walks nobody out of town.
			KingdomConversion.OnSettlementPass(System, Z, survey);
			// Shared living WITH THE SETTLEMENT, counted in attended passes and at most one day
			// apiece: the input the water rite reads for how much of this place a settler has
			// actually lived. Not the same quantity as KingdomConversion's shared living TOWARD ONE
			// CREED, which is household-scoped and closeness-scaled; both are attended-pass
			// denominated, and neither reads a clock that could advance while nobody is here.
			KingdomWaterRite.OnSettlementPass(System, Z, survey);
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
			KingdomMaterials.OnSettlementPass(System, Z, survey);
			// Last of all, and it spends neither water nor hands: a path is only what is left
			// behind by people walking to the work the staffing pass already put them on. It runs
			// after the plot and the plan so that a building raised this pass is already somewhere
			// the settlement has a reason to go.
			KingdomRoads.OnSettlementPass(System, Z);
			if (KingdomLog.Enabled) KingdomLog.Log("growth pass done: pop=" + System.Population + " stage=" + System.Stage + " arrivals=" + arrivals + " dry=" + System.DryStreak + " food=" + survey.FoodStored + "/" + survey.FoodCapacity + " next=" + System.NextArrivalTick);
		}
	}
}
