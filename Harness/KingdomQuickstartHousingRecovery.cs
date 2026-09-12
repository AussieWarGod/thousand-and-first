using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Delays real starter housing with physical obstacles. No clock, citizen,
	/// construction phase or housing assignment is manufactured. Extra provisions isolate roof
	/// pressure from drought and hunger. All setup is disclosed and confined to this profile.</summary>
	internal sealed class KingdomQuickstartHousingRecovery
	{
		internal readonly XRLGame Game;
		internal readonly Zone Zone;
		internal readonly KingdomSystem System;
		private readonly List<GameObject> Obstacles = new List<GameObject>();
		private readonly HashSet<string> Founders = new HashSet<string>(StringComparer.Ordinal);
		private readonly HashSet<string> Departed = new HashSet<string>(StringComparer.Ordinal);
		private HashSet<string> Retained;
		private long Began;
		private int Phase;
		internal string Fault;

		internal KingdomQuickstartHousingRecovery(XRLGame Game, Zone Zone, KingdomSystem System)
		{
			this.Game = Game; this.Zone = Zone; this.System = System;
		}

		internal string Begin()
		{
			Require(Phase == 0, "housing-delay already started");
			Require(KingdomQuickstartSettlementChecks.Observe(Game, Zone, System, "startup", out string failure), failure);
			Require(KingdomQuickstartRules.TryDecode(Game.GetStringGameState(KingdomQuickstartRules.ReceiptState),
				out var receipt), "no canonical Quickstart receipt");
			foreach (string id in receipt.FounderObjectIds) Founders.Add(id);
			KingdomNativeCampFounding.Dedicate(Game, Zone, System, 400, item =>
			{
				Require(!string.IsNullOrEmpty(item.ID), "synthetic provision has no engine identity");
			}, Require);
			GameObject larder = Zone.FindObjectByID(receipt.LarderObjectId);
			Require(larder?.Inventory != null, "the original starter larder is absent");
			for (int i = 0; i < 100; i++)
			{
				GameObject meal = GameObject.Create(receipt.FoodBlueprint);
				Require(KingdomOrdinaryFoodAuthority.IsEdible(meal) && meal.Count == 1,
					"synthetic meal is not one edible serving");
				Require(!string.IsNullOrEmpty(meal.ID)
					&& ReferenceEquals(larder.Inventory.AddObject(meal, null, Silent: true, NoStack: true), meal)
					&& ReferenceEquals(meal.InInventory, larder), "synthetic meal custody differs");
			}
			for (int i = 0; i < KingdomQuickstartRules.ShelterLotCount; i++)
			{
				var rect = KingdomQuickstartRules.ShelterLot(i);
				for (int y = rect.Y1; y <= rect.Y2; y++)
					for (int x = rect.X1; x <= rect.X2; x++)
					{
						GameObject chest = GameObject.Create("Chest");
						Require(GameObject.Validate(chest) && chest.Inventory != null
							&& chest.Inventory.Objects.Count == 0 && !string.IsNullOrEmpty(chest.ID),
							"obstacle is not an empty identified chest");
						Cell cell = Zone.GetCell(x, y);
						Require(ReferenceEquals(cell.AddObject(chest, NoStack: true), chest)
							&& ReferenceEquals(chest.CurrentCell, cell)
							&& KingdomPlots.ReadObject(chest) == KingdomPlotRules.GroundKind.Held,
							"obstacle does not physically reserve its layout cell");
						Obstacles.Add(chest);
					}
			}
			Began = Game.TimeTicks; Phase = 1;
			return Report("delayed", Founders.Count);
		}

		internal void ObserveDeparture(string Id, string Cause, bool Result)
		{
			if (!Result || Phase != 1) return;
			if (Cause != KingdomLodgingRules.DepartureCause || !Founders.Contains(Id ?? "")
				|| !Departed.Add(Id)) Fault = "a departure was not one unique original citizen leaving for lack of housing";
		}

		internal string CheckRetained()
		{
			Require(Phase == 1 && Fault == null, Fault ?? "housing delay is not active");
			Require(Game.TimeTicks - Began > (KingdomLodgingRules.GraceDays + 2) * KingdomRules.TicksPerDay,
				"the ordinary roof-departure window has not been exceeded");
			Require(KingdomGrowth.TryCountBeds(Zone, out int beds, out string failure) && beds == 0,
				"housing was not actually unavailable throughout the delay: " + failure);
			Require(KingdomGrowth.CountStoredWater(Zone) > 0, "drought confounds the roof-departure case");
			HashSet<string> survivors = Citizens();
			Require(survivors.Count == KingdomRules.LoyalCoreSettlers
				&& KingdomResidents.OnRollCount(System) == survivors.Count
				&& Departed.Count == KingdomQuickstartRules.FounderCount - survivors.Count,
				"housing departures did not stop at the loyal-core floor");
			Require(Retained == null || Retained.SetEquals(survivors), "another core citizen left on a later pass");
			Retained = survivors;
			foreach (string id in Departed)
				Require(Zone.FindObjectByID(id) == null, "a departed body still stands on the housing ground: " + id);
			foreach (GameObject obstacle in Obstacles)
				Require(GameObject.Validate(obstacle) && ReferenceEquals(obstacle.CurrentZone, Zone),
					"a physical housing obstacle vanished during the delay");
			return Report("retained", survivors.Count);
		}

		internal string Unblock()
		{
			Require(Phase == 1 && Retained != null, "retention was not proved before recovery");
			CheckRetained();
			foreach (GameObject obstacle in Obstacles)
			{
				Require(obstacle.Inventory.Objects.Count == 0, "an obstacle acquired foreign contents");
				obstacle.CurrentCell.RemoveObject(obstacle);
				Require(obstacle.CurrentCell == null, "the exact obstacle did not leave its layout cell");
			}
			Phase = 2;
			return Report("unblocked", Retained.Count);
		}

		internal string Recovered()
		{
			Require(Phase == 2 && Fault == null, Fault ?? "housing recovery has not begun");
			HashSet<string> survivors = Citizens();
			Require(Retained.SetEquals(survivors), "the loyal core changed during housing recovery");
			Require(KingdomGrowth.TryCountBeds(Zone, out int beds, out string failure) && beds >= survivors.Count,
				"ordinary construction did not restore usable beds: " + failure);
			foreach (string id in survivors)
				Require(KingdomLodging.HomeDesignKeyOf(Zone, Zone.FindObjectByID(id)) == KingdomQuickstartRules.ShelterBuildKey,
					"a retained citizen has no completed starter home: " + id);
			string jobId = Game.GetStringGameState(KingdomQuickstartLifecycleSteps.JobKey);
			Require(KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out failure), failure);
			KingdomConstructionJob job = jobs.Find(item => item.Id == jobId);
			Require(job != null && job.Phase == KingdomConstructionPhase.Complete,
				"the retained citizens did not finish the paid construction job");
			GameObject output = Zone.FindObjectByID(job.OutputId);
			Require(output != null && KingdomUpgrade.IsFunctionallyBuilt(output)
				&& KingdomConstruction.HasReceipt(output, job), "the paid work has no functional physical output");
			Phase = 3;
			return Report("recovered", survivors.Count);
		}

		private HashSet<string> Citizens()
		{
			var result = new HashSet<string>(StringComparer.Ordinal);
			foreach (GameObject body in Zone.GetObjects())
			{
				if (!GameObject.Validate(body) || !Founders.Contains(body.IDIfAssigned ?? "")
					|| !KingdomCitizenship.BelongsTo(System, body)) continue;
				Require(result.Add(body.IDIfAssigned), "duplicate original citizen body");
				Require(KingdomResidents.TryResident(System.City, KingdomResidents.IdOf(body), out var row)
					&& KingdomResidentRules.OnTheRoll(row), "a physical citizen lacks its living roll row");
			}
			return result;
		}

		private string Report(string State, int Citizens)
		{
			return "housing-recovery " + State + "; citizens=" + Citizens + "; roofDepartures=" + Departed.Count
				+ "; tick=" + Game.TimeTicks + "; synthetic-water=400; synthetic-meals=100; physical-obstacles="
				+ Obstacles.Count + "; synthetic-residents=false; forced-housing=false";
		}

		internal static void Require(bool Condition, string Failure)
		{
			if (!Condition) throw new InvalidOperationException(Failure);
		}
	}
}
