using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The food-conservation half of <see cref="KingdomScenarioContainerStress"/>: proves each of
	/// the eight larders' physical contents are IDENTICAL before and after the pause -- same body
	/// identity, same blueprint, same exact holder, same positive raw count -- because legacy
	/// food debt retires inert (<c>Simulation/City/KingdomCity.z05.Reify.cs:41-61</c>) rather than
	/// landing a delivery.
	/// <para>
	/// NEVER MINTS AN ID. <c>GameObject.ID</c> assigns one on first read
	/// (<c>[decompile] XRL/World/GameObject.cs:436-449</c>); every observation here reads
	/// <c>IDIfAssigned</c> instead, and an unassigned id is a refusal, not a value to invent.
	/// </para>
	/// <para>
	/// NO SILENT SKIP. Every object standing in a larder is classified and accounted for; an
	/// invalid object, an unclassified/unexpected blueprint, a missing bound body, or an extra
	/// unbound body each refuse by a named reason rather than being passed over.
	/// </para>
	/// <para>
	/// RAW SEAM ONLY, EXCLUSIVE CUSTODY. Counts come from
	/// <see cref="KingdomMaterials.RawCensusCountOf"/> (<c>Growth/KingdomMaterials.RawObservation.cs:48</c>),
	/// never the ordinary, dispatching <c>Count</c>/<c>HeldIn</c>
	/// (<c>Growth/KingdomSurvey.04.FoodConsumption.cs:176-190</c>). A body's own custody must
	/// name the exact larder (<c>Physics.InInventory</c> reference-equals it, and the body sits
	/// in no cell); the larder itself must be un-held ground (<c>InInventory == null</c>) at its
	/// recorded zone and cell.
	/// </para>
	/// <para>
	/// CROSS-UNLOAD HONESTY. No <c>GameObject</c> or <c>Zone</c> reference is retained across the
	/// away leg -- every read here is a fresh <c>Zone.FindObjectByID</c> lookup, so a genuine
	/// zone unload/reload recreating an object is not itself a fault. But that also means a body
	/// found again by the SAME id, blueprint and count after a real unload is NOT proof it is the
	/// same allocation -- ids do not carry CLR reference identity across a reload, only across a
	/// single loaded interval. This fixture therefore claims durable-evidence conservation
	/// (id + blueprint + raw count + custody unchanged), never reference-identity conservation,
	/// across an away leg; a same-id replacement during travel is ambiguous under this evidence,
	/// not a proven pass, and the present-mode comparator (which never unloads the home zone) is
	/// the only leg where <c>ReferenceEquals</c> on a freshly re-fetched object would mean
	/// anything, and even there this fixture does not rely on it.
	/// </para>
	/// </summary>
	public sealed partial class KingdomScenarioContainerStress
	{
		private readonly struct FoodBodyRecord
		{
			internal readonly string Blueprint;
			internal readonly int Raw;
			internal FoodBodyRecord(string blueprint, int raw) { Blueprint = blueprint; Raw = raw; }
		}

		private readonly struct LarderAnchor
		{
			internal readonly string ZoneId;
			internal readonly int X, Y;
			internal LarderAnchor(string zoneId, int x, int y) { ZoneId = zoneId; X = x; Y = y; }
		}

		/// <summary>One larder's exact food census: body id -&gt; (blueprint, raw count).</summary>
		private static readonly Dictionary<string, Dictionary<string, FoodBodyRecord>> FoodCensus =
			new Dictionary<string, Dictionary<string, FoodBodyRecord>>(StringComparer.Ordinal);

		private static readonly Dictionary<string, LarderAnchor> LarderAnchors =
			new Dictionary<string, LarderAnchor>(StringComparer.Ordinal);

		/// <summary>
		/// Records the exact set of food bodies a freshly-populated larder holds, right after
		/// <c>Populate</c> filled it to capacity-1, and the larder's own ground. The larder was
		/// just created by this fixture (<c>Create</c>), so every object found in it now is one
		/// this fixture made -- no pre-existing content to distinguish from, and none is skipped.
		/// </summary>
		private static void BindFoodBodies(GameObject Larder, string Crop)
		{
			Require(Larder.Inventory != null, "populated larder lost its inventory");
			Require(Larder.InInventory == null && Larder.CurrentCell != null && Larder.CurrentZone != null,
				"the larder itself is held rather than standing on its own ground");
			string larderId = Larder.IDIfAssigned;
			Require(!string.IsNullOrEmpty(larderId), "the larder has no assigned identity at bind time");
			LarderAnchors[larderId] = new LarderAnchor(Larder.CurrentZone.ZoneID,
				Larder.CurrentCell.X, Larder.CurrentCell.Y);
			var census = new Dictionary<string, FoodBodyRecord>(StringComparer.Ordinal);
			foreach (GameObject item in Larder.Inventory.Objects)
			{
				Require(GameObject.Validate(item), "an invalid object stands in a freshly populated larder");
				Require(item.HasPart("Food") || item.HasPart("PreparedCookingIngredient"),
					"an unexpected non-food object stands in a freshly populated larder: blueprint=" + item.Blueprint);
				Require(item.Blueprint == Crop, "a populated food body is not the expected crop blueprint");
				string id = item.IDIfAssigned;
				Require(!string.IsNullOrEmpty(id) && !census.ContainsKey(id),
					"a populated food body has no assigned identity, or its identity repeats");
				Require(item.Physics != null && ReferenceEquals(item.Physics.InInventory, Larder)
					&& item.CurrentCell == null, "a populated food body's own custody does not name this exact larder");
				int raw = KingdomMaterials.RawCensusCountOf(item);
				Require(raw > 0, "a populated food body's raw count is not positive");
				census.Add(id, new FoodBodyRecord(item.Blueprint, raw));
			}
			FoodCensus[larderId] = census;
		}

		/// <summary>
		/// Re-proves every larder's ground and food census is EXACTLY what
		/// <see cref="BindFoodBodies"/> recorded: same zone/cell for the larder, same body
		/// identities, same blueprint, same exact holder, same positive raw count. Refuses (never
		/// guesses, never skips) on any unclassified object, missing body, extra body, changed
		/// holder, or changed count -- a bare total could rise or fall for reasons unrelated to
		/// this fixture's own debt and must never be read as proof either way (the same "never
		/// clear/credit on a coincidental count" law issue #121 states for forage's own custody
		/// proofs).
		/// </summary>
		private static void VerifyFoodConserved(Zone zone, string phase)
		{
			foreach (string larderId in FoodIds)
			{
				GameObject larder = zone.FindObjectByID(larderId);
				Require(larder != null && larder.Inventory != null, phase + ": larder custody lost");
				Require(larder.InInventory == null, phase + ": the larder itself is now held rather than standing on its own ground");
				Require(LarderAnchors.TryGetValue(larderId, out var anchor), phase + ": no recorded ground for larder");
				Require(larder.CurrentZone != null && larder.CurrentZone.ZoneID == anchor.ZoneId
					&& larder.CurrentCell != null && larder.CurrentCell.X == anchor.X && larder.CurrentCell.Y == anchor.Y,
					phase + ": the larder itself moved off its recorded ground");
				Require(FoodCensus.TryGetValue(larderId, out var expected), phase + ": no recorded food census for larder");
				var observed = new Dictionary<string, FoodBodyRecord>(StringComparer.Ordinal);
				foreach (GameObject item in larder.Inventory.Objects)
				{
					Require(GameObject.Validate(item), phase + ": an invalid object stands in the larder");
					Require(item.HasPart("Food") || item.HasPart("PreparedCookingIngredient"),
						phase + ": an unexpected non-food object stands in the larder: blueprint=" + item.Blueprint);
					string id = item.IDIfAssigned;
					Require(!string.IsNullOrEmpty(id) && !observed.ContainsKey(id),
						phase + ": a larder food body has no assigned identity, or its identity repeats");
					Require(expected.ContainsKey(id), phase + ": an extra, unbound food body stands in the larder: id=" + id);
					Require(item.Physics != null && ReferenceEquals(item.Physics.InInventory, larder)
						&& item.CurrentCell == null, phase + ": a larder food body's custody no longer names this exact larder");
					int raw = KingdomMaterials.RawCensusCountOf(item);
					Require(raw > 0, phase + ": a larder food body's raw count is not positive");
					observed.Add(id, new FoodBodyRecord(item.Blueprint, raw));
				}
				foreach (var row in expected)
				{
					Require(observed.TryGetValue(row.Key, out var now),
						phase + ": a bound food body is missing -- relocated or deleted: id=" + row.Key);
					Require(now.Blueprint == row.Value.Blueprint, phase + ": a bound food body's blueprint changed: id=" + row.Key);
					Require(now.Raw == row.Value.Raw, phase + ": a bound food body's raw count changed: id=" + row.Key);
				}
			}
		}
	}
}
