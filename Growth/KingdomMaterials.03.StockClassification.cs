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
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
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
		/// Which rare find an item counts as. Read the way <see cref="TryMaterialOf"/> reads a
		/// material: a third party's own tag first, then the vanilla blueprints the base game
		/// scatters. Anything else is somebody's jewellery and is never counted or spent.
		/// </summary>
		public static bool TryExoticOf(GameObject Object, out KingdomExotic Exotic)
		{
			Exotic = KingdomExotic.Ingot;
			if (Object == null)
			{
				return false;
			}
			string tagged = Object.GetTag(ExoticTag);
			if (!string.IsNullOrEmpty(tagged) && KingdomMaterialRules.TryParseExotic(tagged, out Exotic))
			{
				return true;
			}
			for (int i = 0; i < ExoticBlueprints.Length; i++)
			{
				string[] blueprints = ExoticBlueprints[i];
				for (int j = 0; j < blueprints.Length; j++)
				{
					if (Object.Blueprint == blueprints[j])
					{
						Exotic = (KingdomExotic)i;
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// What one item is worth to the settlement in bits, added into a tally.
		/// <para>
		/// Derived before authored, per Addendum 4's creed principle applied to things rather than
		/// people: vanilla's own <c>TinkerItem</c> already knows what every piece of scrap in the
		/// game disassembles into, and <c>BitType</c> already knows which tier each of those bits
		/// belongs to. A corroded circuit board is two tier-zero bits because the base game says so
		/// and not because we wrote a table. Our own <see cref="BitTag"/> is read only for items
		/// that carry no <c>TinkerItem</c> at all &mdash; a mod's raw ingot of pure alloy, say.
		/// </para>
		/// </summary>
		/// <param name="Object">The item to read.</param>
		/// <param name="Into">Tally to add to. Null is a no-op.</param>
		/// <returns>True when the item was worth any bits at all.</returns>
		public static bool TryBitsOf(GameObject Object, KingdomBitTally Into)
		{
			if (Object == null || Into == null)
			{
				return false;
			}
			KingdomBitTally unit = UnitBits(Object);
			if (unit.IsEmpty())
			{
				return false;
			}
			int count = (Object.Count > 0) ? Object.Count : 1;
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				Into.Add(i, unit.Get(i) * count);
			}
			return true;
		}

		/// <summary>
		/// What ONE of a thing is worth in bits, ignoring how many of it are stacked there. The
		/// unit the spending path works in: a stack of four bent metal sheets is four separate
		/// answers to a price, and only as many of them are broken up as the price actually wants.
		/// </summary>
		/// <returns>An empty tally for anything that is worth no bits, which is most things.
		/// </returns>
		public static KingdomBitTally UnitBits(GameObject Object)
		{
			KingdomBitTally worth = new KingdomBitTally();
			if (Object == null)
			{
				return worth;
			}
			TinkerItem tinker = Object.GetPart<TinkerItem>();
			if (tinker != null && tinker.CanDisassemble)
			{
				// GetBitCostFor rather than the instance property on purpose. The property answers
				// out of BitCostMap and returns a bare "0" for a blueprint nothing has primed yet
				// (TinkerItem.cs:56-62), while the static fills the map from the blueprint and
				// hands back real bit colours (TinkerItem.cs:133-157). It also leaves the item's
				// own modifications out of the count, which is right here: the settlement is
				// reading a heap of scrap, not pricing somebody's modded rifle.
				string bits = TinkerItem.GetBitCostFor(tinker.ActiveBlueprint);
				if (!string.IsNullOrEmpty(bits))
				{
					for (int i = 0; i < bits.Length; i++)
					{
						if (KingdomMaterialRules.TryBitTier(bits[i], out var tier))
						{
							worth.Add(tier, 1);
						}
					}
				}
				if (!worth.IsEmpty())
				{
					return worth;
				}
			}
			string tagged = Object.GetTag(BitTag);
			if (!string.IsNullOrEmpty(tagged) && KingdomMaterialRules.TryParseBitCost(tagged, out var declared, out _))
			{
				return declared;
			}
			return worth;
		}
	}
}
