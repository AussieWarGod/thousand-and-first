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
	/// Ground the founder has ordered cleared: a rect, the effort still owed on it, and the day
	/// the crew last swung at it. Carries no <c>WantTurnTick</c> and never will &mdash; clearing
	/// is crew work, and crew work is resolved only from the settlement's ordinary
	/// <c>ZoneActivatedEvent</c> pass, through
	/// <see cref="ThousandAndFirst.KingdomMaterials.OnSettlementPass"/>, so a founder who is not
	/// there is never spending hands they never assigned.
	/// </summary>
	[Serializable]
	public class r_KingdomClearance : IPart
	{
		/// <summary>West edge of the ordered rect, in zone cells, inclusive.</summary>
		public int X1;

		/// <summary>North edge of the ordered rect, in zone cells, inclusive.</summary>
		public int Y1;

		/// <summary>East edge of the ordered rect, in zone cells, inclusive.</summary>
		public int X2;

		/// <summary>South edge of the ordered rect, in zone cells, inclusive.</summary>
		public int Y2;

		/// <summary>Effort still owed. The order is finished the pass this reaches zero.</summary>
		public int EffortLeft;

		/// <summary>Effort the order was assessed at, kept so the founder can be told how far
		/// along it is without the settlement having to re-walk the ground to find out.</summary>
		public int EffortTotal;

		/// <summary>Tick the crew last worked this ground. Zero until the first pass sees it.</summary>
		public long LastWorkedTick;

		/// <summary>Set once the founder has been told there is nobody free to clear. Cleared the
		/// moment hands are free again, so the reason is given once per stall and not once per
		/// visit. STANDARDS 7b.</summary>
		public bool NoHandsAnnounced;

		/// <summary>Set once the founder has been told something is standing in the finished
		/// rect that the settlement will not touch. Cleared when the obstruction goes.</summary>
		public bool BlockedAnnounced;
	}
}

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// The material economy: clearance as extraction, stockpiles as marks, building costs paid
	/// in water and material both, striking as the honest way to take something down again.
	/// <para>
	/// Follows the <see cref="KingdomLarder"/> idiom throughout &mdash; each founder-facing entry
	/// point does its own eligibility check, its own success messaging, and its own chronicle
	/// entry, and surfaces only a decline through <c>Failure</c>. A refusal changes nothing.
	/// </para>
	/// <para>
	/// Nothing here mints a material. Everything the stockpiles hold was carried out of ground
	/// somebody cleared, salvaged from something somebody struck, or delivered under a charter.
	/// And nothing here touches an object the settlement did not create: a rect holding anything
	/// else is refused whole, by name, rather than cleared around.
	/// </para>
	/// </summary>
	public static class KingdomMaterials
	{
		/// <summary>Property marking a container the founder dedicated as a stockpile. The mark
		/// is not a transfer: what is inside stays where it is and stays the founder's, and the
		/// settlement only counts it &mdash; exactly as <c>KingdomLarder</c> works for food.</summary>
		public const string StockpileProperty = "KingdomStockpile";

		/// <summary>
		/// Blueprint tag by which a third-party item declares itself one of the settlement's
		/// materials: <c>&lt;tag Name="r_KingdomMaterial" Value="timber" /&gt;</c>. The extension
		/// point for the material vocabulary, per STANDARDS 6 &mdash; another mod's ironwood beam
		/// counts as timber the moment it carries the tag, with no code here changing.
		/// </summary>
		public const string MaterialTag = "r_KingdomMaterial";

		/// <summary>Effort still owed on a building the founder ordered struck. Absent or zero on
		/// every building nobody has condemned.</summary>
		public const string StrikeEffortProperty = "KingdomStrikeEffort";

		/// <summary>Effort a strike order was assessed at, for the founder's report.</summary>
		public const string StrikeTotalProperty = "KingdomStrikeTotal";

		/// <summary>Set once the founder has been told a strike order is waiting on hands.</summary>
		public const string StrikeAnnouncedProperty = "KingdomStrikeAnnounced";

		/// <summary>
		/// Tick the crew last worked a strike order, written as a string because the engine's
		/// object properties are ints and a tick is not. Kept on the condemned building itself
		/// rather than appended to any existing part &mdash; a serialized field added to a part
		/// that already ships would move every field after it and cost players their saves.
		/// </summary>
		public const string StrikeWorkedProperty = "KingdomStrikeWorked";

		/// <summary>Blueprint of the marker a clearance order stands as.</summary>
		public const string ClearanceStakeBlueprint = "r_KingdomClearanceStake";

		/// <summary>
		/// Item blueprints the settlement stores each material as, indexed by
		/// <see cref="KingdomMaterial"/>. Scrap is vanilla's own <c>Scrap Metal</c>, because scrap
		/// metal is already a real item in this game and a second one would be a lie; the other
		/// five are ours, because vanilla has no timber, no cut stone, and no bundle of brush.
		/// </summary>
		public static readonly string[] MaterialBlueprints = new string[KingdomMaterialRules.MaterialCount]
		{
			"r_KingdomMud",
			"r_KingdomBrush",
			"r_KingdomTimber",
			"r_KingdomCutStone",
			"r_KingdomMarbleBlock",
			"Scrap Metal"
		};

		/// <summary>Stockpiles one settlement's keepers can account for on one ground. Mirrors
		/// <c>KingdomRules.MaxDedicatedLarders</c>: a separate cap from water and from food,
		/// because these are separate accounts kept by separate people.</summary>
		public const int MaxStockpiles = 8;

		/// <summary>Whether the material economy resolves at all. Rides the growth toggle rather
		/// than adding a switch of its own: materials are what growth costs.</summary>
		public static bool Enabled => KingdomGrowth.Enabled;

		// --- The registry: material costs, kept beside the catalogue ------------------------

		private static readonly Dictionary<string, KingdomMaterialTally> _costs = new Dictionary<string, KingdomMaterialTally>();

		private static readonly Dictionary<string, KingdomMaterialTally> _upgradeCosts = new Dictionary<string, KingdomMaterialTally>();

		private static readonly Dictionary<string, KingdomMaterialTally> _dealMaterials = new Dictionary<string, KingdomMaterialTally>();

		private static readonly KingdomMaterialTally _empty = new KingdomMaterialTally();

		/// <summary>
		/// Empties every material cost keyed by a registry key. Called from
		/// <c>KingdomData.EnsureLoaded</c> beside <c>KingdomZoning.ClearGates</c> and
		/// <c>KingdomUpgrade.ClearChains</c>, in the same single pass over the streams, because a
		/// second pass would make the engine warn about every attribute the first pass did not
		/// happen to want.
		/// </summary>
		public static void ClearCosts()
		{
			_costs.Clear();
			_upgradeCosts.Clear();
			_dealMaterials.Clear();
		}

		/// <summary>
		/// Records what one catalogue entry costs in material, and what improving into it costs.
		/// Both attributes are optional and both are read whether or not they are present, for
		/// the reason above. A malformed value disables itself with a logged reason and leaves
		/// the design costing water alone; it never crashes the registry and never half-registers.
		/// </summary>
		/// <param name="Key">The design's registry key. Null and empty are ignored.</param>
		/// <param name="Materials">The <c>Materials</c> attribute, or null for water-only.</param>
		/// <param name="UpgradeMaterials">The <c>UpgradeMaterials</c> attribute, or null.</param>
		public static void RegisterCost(string Key, string Materials, string UpgradeMaterials)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			if (!KingdomMaterialRules.TryParseMaterialCost(Materials, out var cost, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + Key + " has a bad Materials: " + error);
			}
			else if (!cost.IsEmpty())
			{
				_costs[Key] = cost;
			}
			if (!KingdomMaterialRules.TryParseMaterialCost(UpgradeMaterials, out var upgrade, out var upgradeError))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + Key + " has a bad UpgradeMaterials: " + upgradeError);
			}
			else if (!upgrade.IsEmpty())
			{
				_upgradeCosts[Key] = upgrade;
			}
		}

		/// <summary>
		/// Records what one charter carries in material per caravan. Optional; absent means the
		/// charter carries water alone, which is every charter written before this existed.
		/// </summary>
		public static void RegisterDealMaterials(string Key, string Materials)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			if (!KingdomMaterialRules.TryParseMaterialCost(Materials, out var carried, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomDeals: deal " + Key + " has a bad Materials: " + error);
			}
			else if (!carried.IsEmpty())
			{
				_dealMaterials[Key] = carried;
			}
		}

		/// <summary>What a design costs in material. Never null; empty for a water-only design,
		/// which is the default and the whole of the compatibility guarantee.</summary>
		public static KingdomMaterialTally CostFor(string Key)
		{
			KingdomData.EnsureBuildings();
			if (!string.IsNullOrEmpty(Key) && _costs.TryGetValue(Key, out var cost))
			{
				return cost;
			}
			return _empty;
		}

		/// <summary>What improving a work into the design named by Key costs in material. Never
		/// null.</summary>
		public static KingdomMaterialTally UpgradeCostFor(string Key)
		{
			KingdomData.EnsureBuildings();
			if (!string.IsNullOrEmpty(Key) && _upgradeCosts.TryGetValue(Key, out var cost))
			{
				return cost;
			}
			return _empty;
		}

		/// <summary>What one caravan under the charter named by Key carries in material. Never
		/// null.</summary>
		public static KingdomMaterialTally DealMaterialsFor(string Key)
		{
			KingdomData.EnsureBuildings();
			if (!string.IsNullOrEmpty(Key) && _dealMaterials.TryGetValue(Key, out var carried))
			{
				return carried;
			}
			return _empty;
		}

		// --- The stockpiles ------------------------------------------------------------------

		/// <summary>True for a container the founder has dedicated as a stockpile.</summary>
		public static bool IsStockpile(GameObject Object)
		{
			return Object != null && Object.GetIntProperty(StockpileProperty) == 1;
		}

		/// <summary>Stockpiles currently dedicated on this ground, for the dedication cap.</summary>
		public static int CountStockpiles(Zone Z)
		{
			int total = 0;
			if (Z == null)
			{
				return 0;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (IsStockpile(item))
				{
					total++;
				}
			}
			return total;
		}

		/// <summary>
		/// Which material an item counts as, by the tag a third-party blueprint may carry, then
		/// by our own blueprints, then by vanilla's own scrap tag. Anything else is not a
		/// material and is never counted, spent, or destroyed.
		/// </summary>
		/// <param name="Object">The item to read. Null is not a material.</param>
		/// <param name="Material">Set on success.</param>
		public static bool TryMaterialOf(GameObject Object, out KingdomMaterial Material)
		{
			Material = KingdomMaterial.Mud;
			if (Object == null)
			{
				return false;
			}
			string tagged = Object.GetTag(MaterialTag);
			if (!string.IsNullOrEmpty(tagged) && KingdomMaterialRules.TryParseMaterial(tagged, out Material))
			{
				return true;
			}
			for (int i = 0; i < MaterialBlueprints.Length; i++)
			{
				if (Object.Blueprint == MaterialBlueprints[i])
				{
					Material = (KingdomMaterial)i;
					return true;
				}
			}
			// Vanilla's own scrap, whatever variant of it: "Scrap Metal" is the piece the
			// settlement stores, but a founder who dedicates a chest of salvaged bits has
			// dedicated scrap, and the keepers can tell the difference between that and a tool.
			if (Object.HasTag("SemanticScrap"))
			{
				Material = KingdomMaterial.Scrap;
				return true;
			}
			return false;
		}

		/// <summary>
		/// What the dedicated stockpiles on one ground hold, and the means to spend it and fill
		/// it. A snapshot: spending and delivering through this object keep its tally correct,
		/// but an item placed after it was taken does not retroactively appear in it &mdash; the
		/// same contract <c>KingdomSurvey</c> holds for water and food.
		/// </summary>
		public sealed class MaterialStock
		{
			/// <summary>Units of each material the dedicated stockpiles hold.</summary>
			public readonly KingdomMaterialTally Tally = new KingdomMaterialTally();

			/// <summary>The dedicated containers, in the order found. Spending walks these, so
			/// nothing is ever drawn from something the founder did not dedicate.</summary>
			public readonly List<GameObject> Stockpiles = new List<GameObject>();

			/// <summary>Zone this stock was taken from. Null for an empty stock.</summary>
			public Zone Zone;

			/// <summary>True when the founder has dedicated no stockpile here at all, which is a
			/// different thing from having dedicated an empty one.</summary>
			public bool None => Stockpiles.Count == 0;

			/// <summary>
			/// Destroys up to Units of one material from the stockpiles, in the order found.
			/// Counts what actually left rather than what was asked for: an item whose
			/// destruction something else vetoes stops the draw instead of being counted as
			/// spent.
			/// </summary>
			/// <returns>Units actually taken, never more than were there.</returns>
			public int Take(KingdomMaterial Material, int Units)
			{
				int remaining = Units;
				for (int i = 0; i < Stockpiles.Count && remaining > 0; i++)
				{
					GameObject container = Stockpiles[i];
					if (container.Inventory == null)
					{
						continue;
					}
					// Snapshot first: destroying an item below removes it from this same
					// Inventory list, and mutating a collection mid-foreach throws.
					List<GameObject> held = new List<GameObject>(container.Inventory.Objects);
					for (int j = 0; j < held.Count && remaining > 0; j++)
					{
						GameObject item = held[j];
						if (!TryMaterialOf(item, out var kind) || kind != Material)
						{
							continue;
						}
						// Destroy() on a stack of more than one decrements it by exactly one and
						// leaves the object in place (Stacker.HandleEvent(BeforeDestroyObjectEvent));
						// only the last unit removes it. The count is read before and after rather
						// than inferred from the call, so a refused destruction never reads as a
						// unit spent.
						while (remaining > 0 && GameObject.Validate(item))
						{
							int before = item.Count;
							item.Destroy(null, Silent: true);
							if (GameObject.Validate(item) && item.Count >= before)
							{
								break;
							}
							remaining--;
						}
					}
				}
				int taken = Units - remaining;
				Tally.Add(Material, -taken);
				return taken;
			}

			/// <summary>
			/// Spends a whole cost, or spends nothing. Checked against the tally first, so a
			/// settlement is never left holding half a house's worth of missing timber.
			/// </summary>
			/// <returns>False when the stockpiles do not cover the cost; nothing was taken.</returns>
			public bool Spend(KingdomMaterialTally Cost)
			{
				if (Cost == null || Cost.IsEmpty())
				{
					return true;
				}
				if (!KingdomMaterialRules.Covers(Tally, Cost))
				{
					return false;
				}
				for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				{
					KingdomMaterial material = (KingdomMaterial)i;
					int wanted = Cost.Get(material);
					if (wanted > 0)
					{
						Take(material, wanted);
					}
				}
				return true;
			}

			/// <summary>
			/// Puts real items into the stockpiles, and onto the ground when there is nowhere
			/// else for them. Material is never held in the abstract: what the settlement earned
			/// exists somewhere a founder can walk to and pick up.
			/// </summary>
			/// <param name="Material">Which material.</param>
			/// <param name="Units">How many units. Zero and negative do nothing.</param>
			/// <param name="Fallback">Cell the overflow is dropped in when no stockpile can take
			/// it. Null discards the overflow rather than losing track of it, and is only ever
			/// passed by a caller with no ground to drop on.</param>
			/// <returns>Units that went on the ground instead of into a stockpile.</returns>
			public int Put(KingdomMaterial Material, int Units, Cell Fallback)
			{
				if (Units <= 0)
				{
					return 0;
				}
				string blueprint = BlueprintFor(Material);
				if (string.IsNullOrEmpty(blueprint))
				{
					return 0;
				}
				GameObject container = null;
				for (int i = 0; i < Stockpiles.Count; i++)
				{
					if (Stockpiles[i].Inventory != null)
					{
						container = Stockpiles[i];
						break;
					}
				}
				int placed = 0;
				int spilled = 0;
				int remaining = Units;
				while (remaining > 0)
				{
					GameObject item = GameObject.Create(blueprint);
					if (item == null)
					{
						break;
					}
					int batch = 1;
					if (item.HasPart("Stacker") && remaining > 1)
					{
						batch = remaining;
						item.Count = batch;
					}
					if (container != null)
					{
						container.Inventory.AddObject(item);
						placed += batch;
					}
					else if (Fallback != null)
					{
						Fallback.AddObject(item);
						spilled += batch;
					}
					else
					{
						item.Obliterate();
					}
					remaining -= batch;
				}
				Tally.Add(Material, placed + spilled);
				return spilled;
			}

			/// <summary>Puts a whole tally away, reporting how much of it ended up on the
			/// ground.</summary>
			public int PutAll(KingdomMaterialTally Yield, Cell Fallback)
			{
				int spilled = 0;
				if (Yield == null)
				{
					return 0;
				}
				for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				{
					KingdomMaterial material = (KingdomMaterial)i;
					spilled += Put(material, Yield.Get(material), Fallback);
				}
				return spilled;
			}
		}

		/// <summary>Walks a zone once and reads every dedicated stockpile in it.</summary>
		/// <param name="Z">Zone to read. Null yields an empty stock.</param>
		public static MaterialStock Stock(Zone Z)
		{
			MaterialStock stock = new MaterialStock();
			if (Z == null)
			{
				return stock;
			}
			stock.Zone = Z;
			foreach (GameObject item in Z.GetObjects())
			{
				if (!IsStockpile(item) || item.Inventory == null)
				{
					continue;
				}
				stock.Stockpiles.Add(item);
				foreach (GameObject held in item.Inventory.Objects)
				{
					if (TryMaterialOf(held, out var material))
					{
						stock.Tally.Add(material, held.Count);
					}
				}
			}
			return stock;
		}

		/// <summary>The item blueprint a material is stored as, or null for a value outside the
		/// enum.</summary>
		public static string BlueprintFor(KingdomMaterial Material)
		{
			int index = (int)Material;
			if (index < 0 || index >= MaterialBlueprints.Length)
			{
				return null;
			}
			return MaterialBlueprints[index];
		}

		/// <summary>
		/// Dedicates a container to the settlement's stockpiles, or releases one already
		/// dedicated. Dedication is a mark and never a transfer &mdash; what is inside stays
		/// where it is and stays the founder's, and releasing it un-counts it without moving a
		/// thing.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone the container stands in; must be the kingdom's own ground.</param>
		/// <param name="Container">The container to mark. Must hold things rather than liquid.</param>
		/// <param name="Failure">A founder-facing reason when this returns false. Nothing is
		/// marked when it does.</param>
		/// <returns>True once the container's standing has actually changed.</returns>
		public static bool DedicateStockpile(KingdomSystem System, Zone Z, GameObject Container, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A stockpile is kept on the kingdom's own ground, not in other people's yards.";
				return false;
			}
			if (Container == null || Container.Inventory == null)
			{
				Failure = "A stockpile has to be something that holds things.";
				return false;
			}
			if (IsStockpile(Container))
			{
				Container.SetIntProperty(StockpileProperty, 0);
				MessageQueue.AddPlayerMessage("The " + Container.ShortDisplayName + " is yours alone again. Nothing in it will be counted.");
				return true;
			}
			if (CountStockpiles(Z) >= MaxStockpiles)
			{
				Failure = "The settlement keeps as many stockpiles as anyone can keep an honest account of.";
				return false;
			}
			Container.SetIntProperty(StockpileProperty, 1);
			MessageQueue.AddPlayerMessage("The " + Container.ShortDisplayName + " is a stockpile of " + System.SeatName + " now. What is in it is counted, and still yours.");
			KingdomLog.Log("materials: stockpile dedicated at " + System.SeatName);
			return true;
		}

		/// <summary>One line for the status report: what the stockpiles hold, or why they hold
		/// nothing. Never null.</summary>
		public static string StockLine(Zone Z)
		{
			MaterialStock stock = Stock(Z);
			if (stock.None)
			{
				return "Nothing here is dedicated as a stockpile. Materials cleared from the ground will lie where they fall.";
			}
			string held = stock.Tally.Describe();
			if (held == null)
			{
				return "The stockpiles stand empty.";
			}
			return "The stockpiles hold {{C|" + held + "}}.";
		}

		// --- Paying for a building in material ------------------------------------------------

		/// <summary>
		/// Whether the dedicated stockpiles on this ground cover a design's material cost. A
		/// design with no material cost is always affordable, which is every design the catalogue
		/// carried before materials existed.
		/// </summary>
		/// <param name="Z">Ground the commission would be issued on.</param>
		/// <param name="Key">The design's registry key.</param>
		/// <param name="Failure">A founder-facing reason when this returns false, naming the
		/// shortfall rather than restating the whole price.</param>
		public static bool CanPay(Zone Z, string Key, out string Failure)
		{
			Failure = null;
			KingdomMaterialTally cost = CostFor(Key);
			if (cost.IsEmpty())
			{
				return true;
			}
			MaterialStock stock = Stock(Z);
			if (KingdomMaterialRules.Covers(stock.Tally, cost))
			{
				return true;
			}
			string missing = KingdomMaterialRules.Missing(stock.Tally, cost).Describe();
			Failure = "The work wants {{C|" + cost.Describe() + "}}, and the stockpiles are short "
				+ ((missing == null) ? "of it" : ("{{C|" + missing + "}}"))
				+ ". Clear ground for it, trade for it, or strike something that was built of it."
				+ (stock.None ? " Nothing here is dedicated as a stockpile yet." : "");
			return false;
		}

		/// <summary>
		/// Spends a design's material cost from this ground's stockpiles. Spends the whole cost
		/// or none of it.
		/// </summary>
		/// <returns>False when the stockpiles did not cover it; nothing was taken.</returns>
		public static bool Pay(Zone Z, string Key)
		{
			KingdomMaterialTally cost = CostFor(Key);
			if (cost.IsEmpty())
			{
				return true;
			}
			MaterialStock stock = Stock(Z);
			if (!stock.Spend(cost))
			{
				return false;
			}
			KingdomLog.Log("materials: spent " + cost.Describe() + " on " + Key);
			return true;
		}

		/// <summary>
		/// Whether the dedicated stockpiles on this ground cover an IMPROVEMENT's material cost,
		/// without spending any of it. The absorption law asks this before a work is judged ready,
		/// so an improvement short of material is refused by name rather than begun and abandoned
		/// at the moment <see cref="PayUpgrade"/> would have taken the cost.
		/// </summary>
		/// <param name="Z">Ground the work stands on.</param>
		/// <param name="SuccessorKey">Registry key of the design it would become.</param>
		/// <param name="Missing">What the stockpiles are short of, or null when they cover it.
		/// </param>
		public static bool CanPayUpgrade(Zone Z, string SuccessorKey, out string Missing)
		{
			Missing = null;
			KingdomMaterialTally cost = UpgradeCostFor(SuccessorKey);
			if (cost.IsEmpty())
			{
				return true;
			}
			MaterialStock stock = Stock(Z);
			if (KingdomMaterialRules.Covers(stock.Tally, cost))
			{
				return true;
			}
			Missing = KingdomMaterialRules.Missing(stock.Tally, cost).Describe();
			return false;
		}

		/// <summary>
		/// Spends an improvement's material cost, and says once why an improvement is waiting
		/// when it cannot. The automatic improvement path has no founder standing at it, so the
		/// reason goes in the ledger where the homecoming report will read it out. STANDARDS 7b.
		/// </summary>
		/// <returns>False when the stockpiles did not cover it; nothing was taken.</returns>
		public static bool PayUpgrade(KingdomSystem System, Zone Z, string SuccessorKey)
		{
			KingdomMaterialTally cost = UpgradeCostFor(SuccessorKey);
			if (cost.IsEmpty())
			{
				return true;
			}
			MaterialStock stock = Stock(Z);
			if (stock.Spend(cost))
			{
				return true;
			}
			if (System != null)
			{
				string missing = KingdomMaterialRules.Missing(stock.Tally, cost).Describe();
				System.Ledger.Note("{{r|A work is ready to be bettered, and the stockpiles are short " + ((missing == null) ? "of what it wants" : missing) + ".}}");
			}
			return false;
		}

		/// <summary>
		/// Puts a charter's carried material into this ground's stockpiles, dropping whatever
		/// will not fit at the founder's feet rather than keeping it on the cart. Mirrors what
		/// <c>KingdomTrade</c> already does with water it cannot store.
		/// </summary>
		/// <returns>Units that ended up on the ground rather than in a stockpile.</returns>
		public static int Deliver(KingdomSystem System, Zone Z, KingdomMaterialTally Carried)
		{
			if (Carried == null || Carried.IsEmpty() || Z == null)
			{
				return 0;
			}
			MaterialStock stock = Stock(Z);
			Cell founderCell = The.Player?.CurrentCell;
			Cell fallback = (founderCell != null && founderCell.ParentZone == Z) ? founderCell : Z.GetCell(Z.Width / 2, Z.Height / 2);
			int spilled = stock.PutAll(Carried, fallback);
			if (spilled > 0 && System != null)
			{
				System.Ledger.Note("{{r|" + spilled + " loads came under charter with no stockpile to go in, and were set down on the ground.}}");
			}
			return spilled;
		}

		// --- Clearance ------------------------------------------------------------------------

		/// <summary>What a rect would cost to clear and what it would yield, or why it will not
		/// be cleared at all.</summary>
		public struct ClearanceAssessment
		{
			/// <summary>False only when there was nothing to assess: no zone, no kingdom, or a
			/// rect outside the ground. Every other field is meaningless when this is false.</summary>
			public bool Valid;

			/// <summary>A founder-facing reason the rect will not be cleared, or null when it
			/// will. Names the object standing in the way, because "no" without a name is the one
			/// answer a building system is not allowed to give.</summary>
			public string Refusal;

			/// <summary>Cells in the rect.</summary>
			public int Cells;

			/// <summary>Cells holding something that has to come down.</summary>
			public int Standing;

			/// <summary>Total effort the clearing costs.</summary>
			public int Effort;

			/// <summary>What the clearing would put in the stockpiles.</summary>
			public KingdomMaterialTally Yield;
		}

		/// <summary>
		/// Reads a rect without changing anything: what stands in it, what clearing it costs, and
		/// what it yields. Safe to call for a confirmation popup.
		/// </summary>
		/// <param name="System">The kingdom; must be founded and must hold this ground.</param>
		/// <param name="Z">The ground.</param>
		/// <param name="X1">West edge, inclusive.</param>
		/// <param name="Y1">North edge, inclusive.</param>
		/// <param name="X2">East edge, inclusive.</param>
		/// <param name="Y2">South edge, inclusive.</param>
		public static ClearanceAssessment Assess(KingdomSystem System, Zone Z, int X1, int Y1, int X2, int Y2)
		{
			ClearanceAssessment assessment = default;
			assessment.Yield = new KingdomMaterialTally();
			if (System == null || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return assessment;
			}
			if (X1 > X2 || Y1 > Y2 || X1 < 0 || Y1 < 0 || X2 >= Z.Width || Y2 >= Z.Height)
			{
				return assessment;
			}
			assessment.Valid = true;
			for (int y = Y1; y <= Y2; y++)
			{
				for (int x = X1; x <= X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null)
					{
						assessment.Valid = false;
						return assessment;
					}
					assessment.Cells++;
					KingdomStanding standing = KingdomStanding.Nothing;
					int hitpoints = 0;
					foreach (GameObject item in cell.GetObjects())
					{
						if (IsProtected(item, out var reason))
						{
							assessment.Refusal = reason;
							return assessment;
						}
						if (!TryClassify(item, out var kind))
						{
							continue;
						}
						if (kind > standing)
						{
							standing = kind;
							hitpoints = BaseHitpoints(item);
						}
					}
					assessment.Effort += KingdomMaterialRules.ClearanceEffort(standing, hitpoints);
					if (standing != KingdomStanding.Nothing)
					{
						assessment.Standing++;
						assessment.Yield.Add(KingdomMaterialRules.YieldMaterial(standing), KingdomMaterialRules.YieldUnits(standing));
					}
				}
			}
			assessment.Yield.Add(KingdomMaterial.Mud, KingdomMaterialRules.GroundMud(assessment.Cells));
			return assessment;
		}

		/// <summary>
		/// Stakes a clearance order over a rect. The order does nothing on its own: crew works it
		/// down on the settlement's ordinary passes, and only ever with hands the water detail and
		/// the works have not already claimed.
		/// </summary>
		/// <param name="System">The kingdom; must be founded and must hold this ground.</param>
		/// <param name="Z">The ground.</param>
		/// <param name="X1">West edge, inclusive.</param>
		/// <param name="Y1">North edge, inclusive.</param>
		/// <param name="X2">East edge, inclusive.</param>
		/// <param name="Y2">South edge, inclusive.</param>
		/// <param name="Failure">A founder-facing reason when this returns false. Nothing is
		/// staked and nothing is spent when it does.</param>
		/// <returns>True once the stake is standing.</returns>
		public static bool StakeClearance(KingdomSystem System, Zone Z, int X1, int Y1, int X2, int Y2, out string Failure)
		{
			Failure = null;
			if (!Enabled)
			{
				Failure = "The settlement is not growing.";
				return false;
			}
			ClearanceAssessment assessment = Assess(System, Z, X1, Y1, X2, Y2);
			if (!assessment.Valid)
			{
				Failure = "That ground is not the settlement's to clear.";
				return false;
			}
			if (assessment.Refusal != null)
			{
				Failure = assessment.Refusal;
				return false;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				r_KingdomClearance existing = item.GetPart<r_KingdomClearance>();
				if (existing != null && Overlaps(existing, X1, Y1, X2, Y2))
				{
					Failure = "That ground is already ordered cleared.";
					return false;
				}
			}
			Cell cell = StakeCell(Z, X1, Y1, X2, Y2);
			if (cell == null)
			{
				Failure = "There is nowhere to drive the stake.";
				return false;
			}
			GameObject stake = GameObject.Create(ClearanceStakeBlueprint);
			if (stake == null)
			{
				Failure = "The stake could not be driven.";
				return false;
			}
			r_KingdomClearance order = stake.GetPart<r_KingdomClearance>();
			if (order == null)
			{
				stake.Obliterate();
				Failure = "The stake could not be driven.";
				return false;
			}
			order.X1 = X1;
			order.Y1 = Y1;
			order.X2 = X2;
			order.Y2 = Y2;
			order.EffortTotal = assessment.Effort;
			order.EffortLeft = assessment.Effort;
			order.LastWorkedTick = The.Game.TimeTicks;
			stake.DisplayName = "ground ordered cleared";
			cell.AddObject(stake);
			stake.MakeActive();
			string yield = assessment.Yield.Describe();
			KingdomChronicle.Record(System, System.KingdomDisplayName + " set its people to clearing " + assessment.Cells + " paces of ground");
			int days = KingdomMaterialRules.DaysForOneHand(assessment.Effort);
			MessageQueue.AddPlayerMessage("{{G|The ground is staked for clearing.}} " + assessment.Cells + " paces, "
				+ days + ((days == 1) ? " day" : " days") + " of work for a single pair of hands"
				+ ((yield == null) ? "" : (", and " + yield + " out of it")) + ".");
			KingdomLog.Log("materials: clearance staked " + X1 + "," + Y1 + " to " + X2 + "," + Y2 + " effort=" + assessment.Effort);
			return true;
		}

		/// <summary>
		/// Where the stake goes: the first open, passable cell inside the ordered rect, so the
		/// marker stands in the ground it names rather than on top of the tree it condemns.
		/// Falls back to the rect's north-west corner, which always exists once the rect has been
		/// bounds-checked.
		/// </summary>
		private static Cell StakeCell(Zone Z, int X1, int Y1, int X2, int Y2)
		{
			for (int y = Y1; y <= Y2; y++)
			{
				for (int x = X1; x <= X2; x++)
				{
					Cell candidate = Z.GetCell(x, y);
					if (candidate != null && candidate.IsEmpty() && candidate.IsPassable())
					{
						return candidate;
					}
				}
			}
			return Z.GetCell(X1, Y1);
		}

		// --- Striking -------------------------------------------------------------------------

		/// <summary>
		/// Orders a building the settlement raised taken down, or calls off an order already
		/// standing. Founder-ordered and nothing else: no system anywhere condemns a building on
		/// its own. Nothing comes down the moment this is called &mdash; crew works the order off
		/// over days, and the ceremony is at the end of it.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone the building stands in; must be the kingdom's own ground.</param>
		/// <param name="Building">The building to strike. Must be one the settlement built.</param>
		/// <param name="Failure">A founder-facing reason when this returns false. Nothing is
		/// marked when it does.</param>
		/// <returns>True once the order stands, or once it has been called off.</returns>
		public static bool OrderStrike(KingdomSystem System, Zone Z, GameObject Building, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A building is struck on the kingdom's own ground, not in other people's streets.";
				return false;
			}
			if (Building == null || !GameObject.Validate(Building) || Building.CurrentZone == null || Building.CurrentZone.ZoneID != Z.ZoneID)
			{
				Failure = "There is nothing there to strike.";
				return false;
			}
			if (Building.GetIntProperty("KingdomBuilt") != 1)
			{
				Failure = "The settlement strikes what it raised. That is not one of its buildings.";
				return false;
			}
			if (Building.GetIntProperty(StrikeEffortProperty) > 0)
			{
				Building.SetIntProperty(StrikeEffortProperty, 0);
				Building.SetIntProperty(StrikeTotalProperty, 0);
				Building.SetIntProperty(StrikeAnnouncedProperty, 0);
				Building.SetStringProperty(StrikeWorkedProperty, null);
				MessageQueue.AddPlayerMessage("{{K|The order to strike the " + Building.ShortDisplayName + " is called off.}} It stands exactly where it stood.");
				return true;
			}
			string key = Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			KingdomMaterialTally cost = CostFor(key);
			int drams = (KingdomData.TryGetBuilding(key, out var entry) ? entry.CostDrams : 0);
			int effort = KingdomMaterialRules.StrikeEffort(cost.Total(), drams);
			Building.SetIntProperty(StrikeEffortProperty, effort);
			Building.SetIntProperty(StrikeTotalProperty, effort);
			Building.SetIntProperty(StrikeAnnouncedProperty, 0);
			WriteTick(Building, StrikeWorkedProperty, The.Game.TimeTicks);
			string salvage = KingdomMaterialRules.StrikeSalvage(cost).Describe();
			KingdomChronicle.Record(System, "the " + Building.ShortDisplayName + " of " + System.KingdomDisplayName + " was condemned, and the crew set to taking it down");
			int days = KingdomMaterialRules.DaysForOneHand(effort);
			MessageQueue.AddPlayerMessage("{{W|The " + Building.ShortDisplayName + " is condemned.}} The crew will take it down over "
				+ days + ((days == 1) ? " day" : " days") + " of work for a single pair of hands"
				+ ((salvage == null) ? ", and there is nothing in it worth keeping" : (", and " + salvage + " comes back")) + ". No water is refunded.");
			KingdomLog.Log("materials: strike ordered on " + Building.ShortDisplayName + " effort=" + effort);
			return true;
		}

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
		public static void OnSettlementPass(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			int hands = KingdomMaterialRules.FreeHands(System.Population, System.AssignedCrew);
			GameObject strike = null;
			GameObject stakeObject = null;
			r_KingdomClearance stake = null;
			foreach (GameObject item in Z.GetObjects())
			{
				if (strike == null && item.GetIntProperty(StrikeEffortProperty) > 0)
				{
					strike = item;
				}
				if (stake == null)
				{
					r_KingdomClearance order = item.GetPart<r_KingdomClearance>();
					if (order != null)
					{
						stake = order;
						stakeObject = item;
					}
				}
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

		private static void WorkStrike(KingdomSystem System, Zone Z, GameObject Building, int Hands, long TimeTicks)
		{
			// The order keeps its own checkpoint, for the same reason the water detail had to
			// grow one: charged per zone activation instead, a founder could work a building down
			// by stepping out of the zone and back in, without a day passing.
			long worked = ReadTick(Building, StrikeWorkedProperty);
			if (worked <= 0)
			{
				WriteTick(Building, StrikeWorkedProperty, TimeTicks);
				return;
			}
			int days = KingdomRules.HeartbeatDays(TimeTicks - worked);
			if (days <= 0)
			{
				return;
			}
			if (Hands <= 0)
			{
				if (Building.GetIntProperty(StrikeAnnouncedProperty) != 1)
				{
					Building.SetIntProperty(StrikeAnnouncedProperty, 1);
					System.Ledger.Note("{{r|The " + Building.ShortDisplayName + " is condemned, and there is nobody free to take it down. Stand a settler down off the water or a work.}}");
				}
				WriteTick(Building, StrikeWorkedProperty, KingdomRules.HeartbeatCheckpoint(worked, TimeTicks));
				return;
			}
			Building.SetIntProperty(StrikeAnnouncedProperty, 0);
			WriteTick(Building, StrikeWorkedProperty, KingdomRules.HeartbeatCheckpoint(worked, TimeTicks));
			int left = Building.GetIntProperty(StrikeEffortProperty) - KingdomMaterialRules.EffortWorked(Hands, days);
			if (left > 0)
			{
				Building.SetIntProperty(StrikeEffortProperty, left);
				return;
			}
			string name = Building.ShortDisplayName;
			string key = Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			KingdomMaterialTally salvage = KingdomMaterialRules.StrikeSalvage(CostFor(key));
			Cell cell = Building.CurrentCell;
			MaterialStock stock = Stock(Z);
			int spilled = stock.PutAll(salvage, cell);
			// Only ever an object the settlement created and marked, per the protection law:
			// OrderStrike refuses anything without KingdomBuilt, and nothing else sets this.
			// The socket layer reads the plot's own rect off Building before it goes, and reports
			// whether it took over the telling: a live conversion supplies its own single combined
			// line, an ordinary strike leaves the rect a re-buildable socket and says nothing here.
			bool convertedInPlace = KingdomSocket.OnCleared(System, Z, Building);
			Building.Obliterate();
			string returned = salvage.Describe();
			if (!convertedInPlace)
			{
				KingdomChronicle.Record(System, "the " + name + " of " + System.KingdomDisplayName + " was struck, and the crew stood in the gap where it had been");
				System.RecordDeed("the striking of the " + name + " at " + System.KingdomDisplayName);
				MessageQueue.AddPlayerMessage("{{W|The " + name + " comes down.}} "
					+ ((returned == null) ? "Nothing of it was worth keeping." : (returned + " is carried to the stockpiles."))
					+ ((spilled > 0) ? " Some of it went on the ground for want of a stockpile." : ""));
			}
			KingdomLog.Log("materials: struck " + name + " salvage=" + (returned ?? "none") + " spilled=" + spilled);
		}

		private static void WorkClearance(KingdomSystem System, Zone Z, GameObject StakeObject, r_KingdomClearance Order, int Hands, long TimeTicks)
		{
			if (Order.LastWorkedTick <= 0)
			{
				Order.LastWorkedTick = TimeTicks;
				return;
			}
			int days = KingdomRules.HeartbeatDays(TimeTicks - Order.LastWorkedTick);
			if (days <= 0)
			{
				return;
			}
			if (Hands <= 0)
			{
				if (!Order.NoHandsAnnounced)
				{
					Order.NoHandsAnnounced = true;
					System.Ledger.Note("{{r|Ground stands staked for clearing at " + System.SeatName + ", and there is nobody free to swing at it. Stand a settler down off the water or a work.}}");
				}
				Order.LastWorkedTick = KingdomRules.HeartbeatCheckpoint(Order.LastWorkedTick, TimeTicks);
				return;
			}
			Order.NoHandsAnnounced = false;
			Order.LastWorkedTick = KingdomRules.HeartbeatCheckpoint(Order.LastWorkedTick, TimeTicks);
			if (Order.EffortLeft > 0)
			{
				Order.EffortLeft -= KingdomMaterialRules.EffortWorked(Hands, days);
				if (Order.EffortLeft > 0)
				{
					return;
				}
			}
			// Re-read the ground rather than trusting what was assessed at staking: a tree may
			// have burned down since, and something the settlement will not touch may have been
			// set down in the rect. The yield is whatever actually comes out, and an obstruction
			// stops the finish rather than being cleared around.
			ClearanceAssessment assessment = Assess(System, Z, Order.X1, Order.Y1, Order.X2, Order.Y2);
			if (!assessment.Valid || assessment.Refusal != null)
			{
				if (!Order.BlockedAnnounced)
				{
					Order.BlockedAnnounced = true;
					System.Ledger.Note("{{r|" + (assessment.Refusal ?? "The staked ground cannot be read.") + " The clearing waits.}}");
				}
				return;
			}
			Order.BlockedAnnounced = false;
			KingdomMaterialTally yield = new KingdomMaterialTally();
			int removed = 0;
			for (int y = Order.Y1; y <= Order.Y2; y++)
			{
				for (int x = Order.X1; x <= Order.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null)
					{
						continue;
					}
					List<GameObject> standing = cell.GetObjects();
					for (int i = 0; i < standing.Count; i++)
					{
						GameObject item = standing[i];
						if (!GameObject.Validate(item) || !TryClassify(item, out var kind))
						{
							continue;
						}
						yield.Add(KingdomMaterialRules.YieldMaterial(kind), KingdomMaterialRules.YieldUnits(kind));
						item.Obliterate();
						removed++;
					}
				}
			}
			yield.Add(KingdomMaterial.Mud, KingdomMaterialRules.GroundMud(assessment.Cells));
			Cell stakeCell = StakeObject.CurrentCell;
			MaterialStock stock = Stock(Z);
			int spilled = stock.PutAll(yield, stakeCell);
			StakeObject.Obliterate();
			string carried = yield.Describe();
			KingdomChronicle.Record(System, assessment.Cells + " paces of ground were cleared at " + System.KingdomDisplayName + ", and " + ((carried == null) ? "nothing came of it but turned earth" : ("the settlement was the richer by " + carried)));
			System.RecordDeed("the ground cleared at " + System.KingdomDisplayName);
			MessageQueue.AddPlayerMessage("{{G|The ground is clear.}} " + removed + ((removed == 1) ? " thing came down" : " things came down")
				+ ((carried == null) ? "." : (", and " + carried + " went to the stockpiles."))
				+ ((spilled > 0) ? " Some of it lies on the ground for want of a stockpile." : ""));
			KingdomLog.Log("materials: clearance done cells=" + assessment.Cells + " removed=" + removed + " yield=" + (carried ?? "none") + " spilled=" + spilled);
		}

		// --- Reading the ground ---------------------------------------------------------------

		/// <summary>
		/// Whether one object standing in a rect stops the whole rect from being cleared, and
		/// why. The protection law in one method: the settlement's own works, the founder's own
		/// things, open water, and anything the mod cannot confidently name are all refusals.
		/// Creatures are not &mdash; a settler standing on the ground walks off it.
		/// </summary>
		/// <param name="Object">The object to judge.</param>
		/// <param name="Reason">A founder-facing sentence when this returns true.</param>
		public static bool IsProtected(GameObject Object, out string Reason)
		{
			Reason = null;
			if (Object == null || !GameObject.Validate(Object))
			{
				return false;
			}
			if (Object.IsCreature || Object.IsPlayer())
			{
				return false;
			}
			if (Object.GetPart<XRL.World.Parts.Physics>() == null)
			{
				return false;
			}
			if (Object.GetIntProperty("KingdomBuilt") == 1 || Object.GetIntProperty("KingdomCitizen") == 1
				|| Object.GetIntProperty("KingdomStores") == 1 || Object.GetIntProperty("KingdomLarder") == 1
				|| Object.GetIntProperty(StockpileProperty) == 1
				|| Object.HasPart("r_KingdomScaffold") || Object.HasPart("r_KingdomPlanMarker")
				|| Object.HasPart("r_KingdomPlot"))
			{
				Reason = "The " + Object.ShortDisplayName + " stands on that ground, and the settlement does not clear away its own. Strike it if you want the ground back.";
				return true;
			}
			XRL.World.Parts.LiquidVolume liquid = Object.GetPart<XRL.World.Parts.LiquidVolume>();
			if (liquid != null)
			{
				Reason = (liquid.MaxVolume < 0)
					? "There is open water on that ground. Water is an asset, not an obstacle, and the settlement will not fill it in."
					: ("The " + Object.ShortDisplayName + " stands on that ground, and it is yours, not the settlement's.");
				return true;
			}
			if (TryClassify(Object, out var kind))
			{
				return false;
			}
			if (Object.HasPart("HologramMaterial") || Object.IsDoor())
			{
				Reason = "The " + Object.ShortDisplayName + " on that ground is nothing the settlement knows how to take apart.";
				return true;
			}
			if (Object.IsTakeable() || Object.Inventory != null)
			{
				Reason = "The " + Object.ShortDisplayName + " lies on that ground, and nothing you put down is ever cleared away. Move it, and ask again.";
				return true;
			}
			if (Object.IsWall() || Object.HasTag("Wall"))
			{
				Reason = "The " + Object.ShortDisplayName + " stands on that ground, and the settlement cannot tell what it is made of.";
				return true;
			}
			return false;
		}

		/// <summary>
		/// What one object is worth clearing, by what it is made of. Reads vanilla's own
		/// vocabulary: the <c>Tree</c> and <c>Plant</c> tags, the <c>SemanticGeological</c> tag
		/// rock walls and boulders carry, the <c>Metal</c> part on every manufactured metal wall,
		/// and the <c>PaintedWall</c> and <c>BodyType</c> tags that tell marble from brinestalk
		/// from canvas.
		/// </summary>
		/// <param name="Object">The object to read.</param>
		/// <param name="Standing">Set on success.</param>
		/// <returns>False for anything that is not the settlement's to take &mdash; which is
		/// everything <see cref="IsProtected"/> refuses, plus everything nobody would call
		/// terrain.</returns>
		public static bool TryClassify(GameObject Object, out KingdomStanding Standing)
		{
			Standing = KingdomStanding.Nothing;
			if (Object == null || !GameObject.Validate(Object) || Object.IsCreature || Object.IsPlayer())
			{
				return false;
			}
			if (Object.HasPart("HologramMaterial") || Object.IsDoor())
			{
				return false;
			}
			if (Object.GetIntProperty("KingdomBuilt") == 1 || Object.HasPart("r_KingdomScaffold")
				|| Object.HasPart("r_KingdomPlanMarker") || Object.HasPart("r_KingdomPlot")
				|| Object.HasPart("r_KingdomClearance"))
			{
				return false;
			}
			if (Object.HasTag("Tree"))
			{
				Standing = KingdomStanding.Tree;
				return true;
			}
			bool isWall = Object.IsWall() || Object.HasTag("Wall");
			if (!isWall && (Object.HasTag("Plant") || Object.HasTag("LivePlant")))
			{
				Standing = KingdomStanding.Brush;
				return true;
			}
			if (Object.Blueprint == "Rubble" || Object.Blueprint == "Rubble Grey")
			{
				Standing = KingdomStanding.Rubble;
				return true;
			}
			bool geological = Object.HasTag("SemanticGeological");
			if (geological && !isWall && Object.IsTakeable())
			{
				// A boulder: naturally occurring, portable in principle, and the reason a rocky
				// site is a stone site.
				Standing = KingdomStanding.Rock;
				return true;
			}
			if (!isWall)
			{
				return false;
			}
			string painted = Object.GetTag("PaintedWall", "");
			string bodyType = Object.GetTag("BodyType", "");
			if (painted == "wall_marble")
			{
				Standing = KingdomStanding.MarbleSeam;
				return true;
			}
			if (Object.HasPart("Metal"))
			{
				Standing = KingdomStanding.Ruin;
				return true;
			}
			if (bodyType == "WoodWall" || painted == "wall_wood" || painted == "wall_wood_worn" || painted == "wall_brinestalk")
			{
				Standing = KingdomStanding.Tree;
				return true;
			}
			if (bodyType == "ClothWall" || bodyType == "PlantWall" || bodyType == "FungusWall" || painted == "wall_plant" || painted == "wall_mushroom")
			{
				Standing = KingdomStanding.Brush;
				return true;
			}
			if (geological || painted == "wall_rock" || painted == "wall_stone" || painted == "wall_brick" || painted == "wall_granite")
			{
				Standing = KingdomStanding.Rock;
				return true;
			}
			Standing = KingdomStanding.Ruin;
			return true;
		}

		/// <summary>The base Hitpoints a blueprint declares, which is how hard a thing is to bring
		/// down. Zero when the object carries no such stat.</summary>
		public static int BaseHitpoints(GameObject Object)
		{
			if (Object == null)
			{
				return 0;
			}
			Statistic stat = Object.GetStat("Hitpoints");
			return (stat != null) ? stat.BaseValue : 0;
		}

		/// <summary>Reads a tick stored as a string property. Zero for absent or unreadable,
		/// which every caller treats as "not stamped yet" rather than as an error.</summary>
		public static long ReadTick(GameObject Object, string Property)
		{
			string text = Object?.GetStringProperty(Property);
			if (string.IsNullOrEmpty(text) || !long.TryParse(text, out var tick) || tick < 0)
			{
				return 0L;
			}
			return tick;
		}

		/// <summary>Stamps a tick into a string property.</summary>
		public static void WriteTick(GameObject Object, string Property, long Tick)
		{
			Object?.SetStringProperty(Property, Tick.ToString());
		}

		private static bool Overlaps(r_KingdomClearance Order, int X1, int Y1, int X2, int Y2)
		{
			return Order.X1 <= X2 && Order.X2 >= X1 && Order.Y1 <= Y2 && Order.Y2 >= Y1;
		}

		// --- Wall material: the theme ---------------------------------------------------------

		/// <summary>
		/// The material this settlement's walls are made of, from its style's taste and what its
		/// own quarrying has actually earned it. Never fails: a settlement that has quarried
		/// nothing walls itself in mud, which is what a camp looks like.
		/// </summary>
		/// <param name="System">The kingdom, for its style. Null reads as styleless.</param>
		/// <param name="Z">The ground whose stockpiles are read. Null reads as empty.</param>
		public static KingdomMaterial WallMaterialFor(KingdomSystem System, Zone Z)
		{
			return KingdomMaterialRules.WallMaterialFor(Stock(Z).Tally, (System != null) ? System.Style : null);
		}

		/// <summary>
		/// The vanilla wall blueprint a material builds as, in this settlement's style. Every
		/// name here is one of vanilla's own settlement walls &mdash; the same vocabulary
		/// <c>Village_StructureWall_*Default</c> draws from &mdash; so a stamped plot reads as
		/// part of the world rather than as something the mod invented.
		/// </summary>
		/// <param name="Material">What the settlement has to build in.</param>
		/// <param name="Style">The city style key, or null.</param>
		/// <returns>A blueprint name; never null.</returns>
		public static string WallBlueprint(KingdomMaterial Material, string Style)
		{
			switch (Material)
			{
			case KingdomMaterial.Marble:
				return "Marble";
			case KingdomMaterial.Stone:
				return "Limestone";
			case KingdomMaterial.Scrap:
				return "Verdigris";
			case KingdomMaterial.Timber:
				if (Style == "fungal")
				{
					return "MushroomWall-White";
				}
				if (Style == "verdant")
				{
					return "PlantWall";
				}
				return "BrinestalkWall";
			case KingdomMaterial.Brush:
				return "CanvasWall";
			default:
				return "BrickWall";
			}
		}
	}
}
