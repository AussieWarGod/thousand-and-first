using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public partial class KingdomSurvey
	{
		// The material half of a container's two accounts. CapacityOf/HeldIn answer for food in
		// KingdomSurvey.04; these answer for stone, timber, bits and rare finds, off a different
		// tag, because one chest may be a larder AND a stockpile and neither number may eat the
		// other's room.

		/// <summary>
		/// Units of material one container was built to hold, off its blueprint's own
		/// <see cref="KingdomRules.StockpileCapacityTag"/> &mdash; the material side of a vessel's
		/// <c>MaxVolume</c>. A container that declares nothing gets
		/// <see cref="KingdomRules.DefaultStockpileCapacity"/>, which is the chest a founder
		/// walked up to and dedicated by hand.
		/// </summary>
		/// <param name="Container">Any object. Null holds nothing and was built to hold nothing.
		/// </param>
		public static int StockCapacityOf(GameObject Container)
		{
			if (Container == null)
			{
				return 0;
			}
			int declared;
			// GetTag reads the blueprint's own dictionary, so a modded store declares its size in
			// XML exactly the way a modded cistern declares MaxVolume.
			if (!int.TryParse(Container.GetTag(KingdomRules.StockpileCapacityTag, ""), out declared))
			{
				declared = 0;
			}
			return KingdomRules.StockpileCapacity(declared);
		}

		/// <summary>
		/// Units of material one container holds right now, counted by stack so a stack of twenty
		/// stones reads as twenty. The material vocabulary is WIDE: materials, rare finds, and
		/// anything vanilla can take apart into bits, because the settlement spends bits too.
		/// Most ordinary loot is worth bits, so it DOES occupy stockpile room &mdash; a chest
		/// dedicated as a stockpile is not also a good loot chest. Only a thing worth no bits and
		/// no material at all counts as nothing and takes up no room.
		/// <para>
		/// PHYSICAL, exactly as <see cref="HeldIn"/> is the physical number for a larder while
		/// the ordinary-food authority answers the spendable one. CUSTODY is read here, never
		/// spend eligibility: the wide material read is <see cref="KingdomMaterials.TryMaterialOf"/>
		/// and not the ordinary one, because a stack a work has reserved or a porter has marked as
		/// cargo is still standing in the chest taking up the room it takes up. Were the ordinary
		/// read used instead, stamping a reservation on an unmoved stack would empty that stack out
		/// of the count while it sat there, and the chest would appear to grow and shrink for
		/// reasons nobody standing in front of it could see.
		/// </para>
		/// </summary>
		/// <param name="Container">Any object. Null, or one with no inventory, holds nothing.
		/// </param>
		public static int StockHeldIn(GameObject Container)
		{
			if (Container == null || Container.Inventory == null)
			{
				return 0;
			}
			int held = 0;
			KingdomBitTally bits = new KingdomBitTally();
			foreach (GameObject item in Container.Inventory.Objects)
			{
				if (item == null)
				{
					continue;
				}
				if (KingdomMaterials.TryMaterialOf(item, out _)
					|| KingdomMaterials.TryExoticOf(item, out _)
					|| KingdomMaterials.TryBitsOf(item, bits))
				{
					held += (item.Count > 0) ? item.Count : 1;
				}
			}
			return held;
		}
	}
}
