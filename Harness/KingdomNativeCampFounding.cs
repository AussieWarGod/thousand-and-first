using System;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The two synthetic camp steps the observation-only providers share: a REAL founding through
	/// the harness's own founding step, and a REAL dedication of one stocked vessel through the
	/// production check-in.
	/// <para>
	/// Nothing here is an observation. It exists so the claimed-light and first-guest observers ask
	/// the same production APIs for their ground rather than keeping two drifting copies of the
	/// same twelve lines; the assertions each observer makes about what happened stay in its own
	/// checks shard.
	/// </para>
	/// </summary>
	internal static class KingdomNativeCampFounding
	{
		/// <summary>
		/// Founds the stamped plan's city on this zone through the ordinary founding transaction.
		/// The plan names the city, so the fixture never invents a settlement identity.
		/// </summary>
		internal static KingdomSystem Found(XRLGame Game, Zone Zone, Action<bool, string> Require)
		{
			KingdomScenarioPlan plan;
			KingdomScenarioProvenance stamp;
			string failure;
			Require(KingdomScenarioRealizer.TryBindStampedPlan(out plan, out stamp, out failure),
				failure ?? "no stamped plan");
			string name = null;
			foreach (KingdomScenarioResolvedStep step in plan.Steps)
				if (step.Verb == KingdomScenarioVerb.FoundFirstCity)
					Require(name == null && step.Arguments.TryGetValue("CityName", out name)
						&& !string.IsNullOrEmpty(name), "founding name missing or repeated");
			Require(name != null, "the stamped plan names no city to found");
			Require(KingdomScenarioFoundingStep.TryProvePreconditions(Zone, name, out failure),
				failure);
			Require(KingdomScenarioTransactionMarker.TryBegin(out failure), failure);
			string line;
			Require(KingdomScenarioFoundingStep.TryFound(Zone, name, out line, out failure), failure);
			Require(KingdomScenarioTransactionMarker.TryCommit(out failure), failure);
			KingdomSystem system = Game.GetSystem<KingdomSystem>();
			Require(system != null && system.Founded, "the real founding transaction left no realm");
			return system;
		}

		/// <summary>
		/// Places one reservoir holding <paramref name="Drams" /> of fresh water, marks it as the
		/// settlement's store, and dedicates it through the production check-in on a bound survey.
		/// Returns the dedicated volume so the caller can prove the stock it later reads.
		/// <para>
		/// THE GROUND IS NOT EMPTY ANY MORE. A fresh founding now dedicates the founding heart's
		/// relic-slot first basin as a settlement water store - the rite stamps
		/// <c>KingdomStores</c> in <c>KingdomPlot2.07c.FoundingHeartMarks</c> as the slot is
		/// created, which <c>Growth/KingdomPlotHeartRules.Loader.cs</c> documents. So this fixture
		/// measures the settlement's stores BEFORE it places its own vessel and asserts the exact
		/// DELTA: one store added, that store being this liquid, and the stock rising by exactly
		/// the drams asked for. That is strictly stronger than the old count-of-one, which only
		/// held while a camp had no basin, and it stays true whatever the basin happens to hold.
		/// </para></summary>
		internal static LiquidVolume Dedicate(XRLGame Game, Zone Zone, KingdomSystem System,
			int Drams, Action<GameObject> Track, Action<bool, string> Require)
		{
			KingdomSurvey before = KingdomSurvey.Take(Zone, System);
			Require(before != null, "the settlement could not be surveyed before dedication");
			int baseStores = before.Stores.Count;
			int baseWater = before.StoredWater;
			GameObject vessel = GameObject.Create("r_KingdomReservoir");
			Require(GameObject.Validate(vessel), "the reservoir blueprint produced no object");
			Track(vessel);
			LiquidVolume liquid = vessel.GetPart<LiquidVolume>();
			Require(liquid != null && liquid.Volume == 0 && KingdomLiquids.CanReceiveFreshWater(liquid),
				"the reservoir is not an empty fresh-water custody");
			vessel.SetIntProperty("KingdomStores", 1);
			liquid.AddDrams("water", Drams);
			Require(liquid.Volume == Drams && liquid.IsFreshWater(),
				"the synthetic stock is not exactly the fresh water asked for");
			Cell target = Clear(Zone);
			Require(target != null, "no clear cell was available for the synthetic store");
			Require(ReferenceEquals(target.AddObject(vessel, NoStack: true), vessel),
				"native placement substituted the reservoir");
			long tick = Game.TimeTicks;
			KingdomSurvey survey = KingdomSurvey.Take(Zone, System);
			Require(survey != null, "the settlement could not be surveyed after dedication");
			int mine = 0;
			for (int i = 0; i < survey.Stores.Count; i++)
				if (ReferenceEquals(survey.Stores[i], liquid)) mine++;
			Require(survey.Stores.Count == baseStores + 1 && mine == 1
				&& survey.StoredWater == baseWater + Drams,
				"the dedication survey does not read exactly one added stocked store: stores "
					+ baseStores + "->" + survey.Stores.Count + ", this liquid read " + mine
					+ " time(s), stored water " + baseWater + "->" + survey.StoredWater
					+ " for " + Drams + " dram(s) dedicated");
			using (survey.BindPass())
			{
				Require(ReferenceEquals(KingdomSurvey.ActiveFor(Zone), survey),
					"the dedication survey did not bind");
				KingdomCity.CheckIn(System, Zone, survey, tick);
			}
			Require(!KingdomSurvey.HasBoundPass && Game.TimeTicks == tick,
				"dedication left a bound pass or moved the clock");
			Require(vessel.GetIntProperty(KingdomCity.DedicationOrderProperty) > 0,
				"the production check-in did not dedicate the stocked vessel");
			return liquid;
		}

		/// <summary>First bare, passable, creature-free, liquid-free cell inside the border.</summary>
		internal static Cell Clear(Zone Zone)
		{
			for (int y = 1; y < Zone.Height - 1; y++)
				for (int x = 1; x < Zone.Width - 1; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null || !cell.IsEmpty() || !cell.IsPassable()
						|| cell.HasOpenLiquidVolume()) continue;
					bool bare = true;
					foreach (GameObject row in cell.Objects)
						if (!GameObject.Validate(row) || row.IsCreature
							|| KingdomPlots.ReadObject(row) != KingdomPlotRules.GroundKind.Bare)
							bare = false;
					if (bare) return cell;
				}
			return null;
		}
	}
}
