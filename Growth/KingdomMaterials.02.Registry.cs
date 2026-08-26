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
		public static void ClearCosts()
		{
			_costs.Clear();
			_upgradeCosts.Clear();
			_dealMaterials.Clear();
			_bitCosts.Clear();
			_exoticCosts.Clear();
			_refineries.Clear();
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
		/// Records what one catalogue entry costs in the high-craft stock: vanilla's own tinkering
		/// bits, and the rare finds only a great work asks for. Registered beside the material cost
		/// and read out of the same merged draft, so a later file that re-prices a design in bits
		/// layers exactly the way one that re-prices it in timber does.
		/// <para>
		/// Both attributes are optional and both are read whether or not they are present, for the
		/// reason <see cref="RegisterCost"/> gives. A malformed value disables itself with a logged
		/// reason and leaves the design costing what it already cost; it never half-registers.
		/// </para>
		/// </summary>
		/// <param name="Key">The design's registry key. Null and empty are ignored.</param>
		/// <param name="Bits">The <c>Bits</c> attribute, or null for a design that wants none.
		/// </param>
		/// <param name="Exotics">The <c>Exotics</c> attribute, or null.</param>
		public static void RegisterHighCraft(string Key, string Bits, string Exotics)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			if (!KingdomMaterialRules.TryParseBitCost(Bits, out var bits, out var bitError))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + Key + " has a bad Bits: " + bitError);
			}
			else if (!bits.IsEmpty())
			{
				_bitCosts[Key] = bits;
			}
			if (!KingdomMaterialRules.TryParseExoticCost(Exotics, out var exotics, out var exoticError))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + Key + " has a bad Exotics: " + exoticError);
			}
			else if (!exotics.IsEmpty())
			{
				_exoticCosts[Key] = exotics;
			}
		}

		/// <summary>
		/// Records that one catalogue entry is a processing work: a design that turns raw stock
		/// into the refined material named by its <c>Refines</c> attribute. Optional everywhere;
		/// a design that declares nothing is not a yard and never was.
		/// <para>
		/// This is the whole of what makes a yard a yard. A third party's own sawmill is a
		/// sawyer's yard the moment it writes <c>Refines="shapedtimber"</c>, and the infrastructure
		/// gate counts it exactly like ours, because the gate asks the registry what stands and
		/// never asks for a blueprint by name.
		/// </para>
		/// </summary>
		public static void RegisterRefinery(string Key, string Refines)
		{
			if (string.IsNullOrEmpty(Key) || string.IsNullOrEmpty(Refines) || Refines.Trim().Length == 0)
			{
				return;
			}
			if (!KingdomMaterialRules.TryParseYard(Refines, out var yard))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + Key + " refines \"" + Refines
					+ "\", which is neither a yard (" + string.Join(", ", KingdomMaterialRules.YardKeys) + ") nor a refined material");
				return;
			}
			_refineries[Key] = yard;
		}

		/// <summary>What a design costs in bits. Never null; empty for everything that wants
		/// none, which is nearly the whole catalogue.</summary>
		public static KingdomBitTally BitCostFor(string Key)
		{
			KingdomData.EnsureBuildings();
			if (!string.IsNullOrEmpty(Key) && _bitCosts.TryGetValue(Key, out var cost))
			{
				return cost;
			}
			return _emptyBits;
		}

		/// <summary>What a design costs in rare finds. Never null; empty for everything but the
		/// great works.</summary>
		public static KingdomExoticTally ExoticCostFor(string Key)
		{
			KingdomData.EnsureBuildings();
			if (!string.IsNullOrEmpty(Key) && _exoticCosts.TryGetValue(Key, out var cost))
			{
				return cost;
			}
			return _emptyExotics;
		}

		/// <summary>Which yard a design is, if it is one at all.</summary>
		public static bool TryRefineryOf(string Key, out KingdomYard Yard)
		{
			KingdomData.EnsureBuildings();
			Yard = KingdomYard.Sawyer;
			return !string.IsNullOrEmpty(Key) && _refineries.TryGetValue(Key, out Yard);
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

		/// <summary>What improving the standing design named by Key into its declared successor
		/// costs in material. The price belongs to the transition source, because two different
		/// predecessors may reach the same successor without retaining the same fabric. Never null.
		/// </summary>
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
	}
}
