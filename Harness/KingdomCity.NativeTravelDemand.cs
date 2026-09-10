using System;
using XRL;
using XRL.World;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomCity
	{
		/// <summary>Developer-only physical observation, including already-settled ground
		/// for which SpendTurn deliberately emits no receipt. No reification or publication.
		/// Unlike the performance helper, a failed measurement is unknown, never zero.</summary>
		internal static bool TryReadNativeTravelDemand(KingdomSystem System, Zone Zone,
			out int Thirds, out bool BookSettled)
		{
			Thirds = -1;
			BookSettled = false;
			var game = The.Game;
			var book = System?.City;
			if (game == null || System == null || !System.Founded || Zone == null || book == null
				|| !ReferenceEquals(game.GetSystem<KingdomSystem>(), System)
				|| !ReferenceEquals(The.Player?.CurrentZone, Zone)
				|| !ReferenceEquals(The.ZoneManager?.ActiveZone, Zone)
				|| !System.ClaimedZones.Contains(Zone.ZoneID)
				|| !book.TryReadExact(out var state, out _)
				|| !IndexOf(state, Zone.ZoneID, out int index) || !state.TryZone(index, out var row)) return false;
			long tick = game.TimeTicks;
			if (!KingdomOrdinaryFoodAuthority.TryCapture(out _, out _)) return false;
			if (!KingdomSurvey.TryTakeUnboundRecovery(Zone, out var survey)) return false;
			ContainerGround ground = ContainerGround.Take(survey);
			bool measured = KingdomContainerCatchUpRules.TryMeasure(ground.Rows, ground.Rows.Length,
				row.OwedWater, row.OwedFood, row.OwedMaterials, out var receipt, out _);
			if (!measured) return false;
			int bodies = Posted(Zone, survey, KingdomStations.Index(Zone)).Count;
			if (!ReferenceEquals(The.Game, game) || game.TimeTicks != tick
				|| !ReferenceEquals(game.GetSystem<KingdomSystem>(), System)
				|| !ReferenceEquals(System.City, book) || !ReferenceEquals(The.Player?.CurrentZone, Zone)
				|| !ReferenceEquals(The.ZoneManager?.ActiveZone, Zone)
				|| !System.Founded || !System.ClaimedZones.Contains(Zone.ZoneID)
				|| !book.TryReadExact(out _, out _)
				|| !book.TryZoneRow(Zone.ZoneID, out int current)
				|| book.ZoneOwedWater[current] != row.OwedWater || book.ZoneOwedFood[current] != row.OwedFood
				|| book.ZoneOwedMaterials[current] != row.OwedMaterials) return false;
			BookSettled = row.OwedWater == 0 && row.OwedFood == 0 && row.OwedMaterials == 0;
			return KingdomScenarioTravelRules.TryPhysicalDemand(measured, receipt.OwedThirds, bodies,
				receipt.WaterBlocked != 0 || receipt.FoodBlocked != 0 || receipt.MaterialsBlocked != 0, out Thirds);
		}
	}
}
