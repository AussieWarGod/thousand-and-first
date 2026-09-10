using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The food-conservation half of <see cref="KingdomScenarioContainerStress"/>: proves each of
	/// the eight larders' physical contents are IDENTICAL before and after the pause -- same body
	/// identities, same exact holder, same raw count -- because legacy food debt retires inert
	/// (<c>Simulation/City/KingdomCity.z05.Reify.cs:41-61</c>) rather than landing a delivery.
	/// Every read here is the raw census seam
	/// (<see cref="KingdomMaterials.RawCensusCountOf"/>, <c>Growth/KingdomMaterials.RawObservation.cs:48</c>),
	/// never the ordinary, dispatching <c>GameObject.Count</c> that <c>KingdomSurvey.HeldIn</c>
	/// uses (<c>Growth/KingdomSurvey.04.FoodConsumption.cs:176-190</c>) -- this fixture must not
	/// let a repaired or dispatched count manufacture the very conservation it is proving.
	/// </summary>
	public sealed partial class KingdomScenarioContainerStress
	{
		/// <summary>One larder's exact food census: body id -&gt; (holder id, raw count).</summary>
		private static readonly Dictionary<string, Dictionary<string, int>> FoodCensus =
			new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

		/// <summary>
		/// Records the exact set of food bodies a freshly-populated larder holds, right after
		/// <c>Populate</c> filled it to capacity-1. The larder was just created by this fixture
		/// (<c>Create</c>), so every food body found in it now is one this fixture made -- no
		/// pre-existing content to distinguish from.
		/// </summary>
		private static void BindFoodBodies(GameObject Larder, string Crop)
		{
			Require(Larder.Inventory != null, "populated larder lost its inventory");
			var census = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (GameObject item in Larder.Inventory.Objects)
			{
				if (!GameObject.Validate(item) || !(item.HasPart("Food") || item.HasPart("PreparedCookingIngredient")))
					continue;
				Require(item.Blueprint == Crop && item.InInventory == Larder && item.CurrentCell == null,
					"a populated food body's own custody does not name this exact larder");
				Require(!string.IsNullOrEmpty(item.ID) && census.ContainsKey(item.ID) == false,
					"a populated food body's identity repeats");
				census.Add(item.ID, KingdomMaterials.RawCensusCountOf(item));
			}
			int raw = 0;
			foreach (int count in census.Values) raw += count;
			Require(raw == KingdomSurvey.HeldIn(Larder), "raw census differs from the ordinary held count at bind time");
			FoodCensus[Larder.ID] = census;
		}

		/// <summary>
		/// Re-proves every larder's food census is EXACTLY what <see cref="BindFoodBodies"/>
		/// recorded: same body identities, same exact holder, same raw count. Refuses (does not
		/// guess) on any unknown custody -- a missing body, an extra body, a body whose holder
		/// changed, or a raw count that moved -- because a bare total could rise or fall for
		/// reasons unrelated to this fixture's own debt and must never be read as proof either
		/// way (the same "never clear/credit on a coincidental count" law issue #121 states for
		/// forage's own custody proofs).
		/// </summary>
		private static void VerifyFoodConserved(Zone zone, string phase)
		{
			foreach (string larderId in FoodIds)
			{
				GameObject larder = zone.FindObjectByID(larderId);
				Require(larder != null && larder.Inventory != null, phase + ": larder custody lost");
				Require(FoodCensus.TryGetValue(larderId, out var expected), phase + ": no recorded food census for larder");
				var observed = new Dictionary<string, int>(StringComparer.Ordinal);
				foreach (GameObject item in larder.Inventory.Objects)
				{
					if (!GameObject.Validate(item) || !(item.HasPart("Food") || item.HasPart("PreparedCookingIngredient")))
						continue;
					Require(item.InInventory == larder && item.CurrentCell == null,
						phase + ": a larder food body's custody no longer names this exact larder");
					Require(!string.IsNullOrEmpty(item.ID) && !observed.ContainsKey(item.ID),
						phase + ": a larder food body's identity repeats");
					observed.Add(item.ID, KingdomMaterials.RawCensusCountOf(item));
				}
				Require(observed.Count == expected.Count, phase + ": larder food body count changed -- minted or deleted");
				foreach (var row in expected)
				{
					Require(observed.TryGetValue(row.Key, out int rawNow), phase + ": a bound food body is missing -- relocated or deleted");
					Require(rawNow == row.Value, phase + ": a bound food body's raw count changed");
				}
			}
		}
	}
}
