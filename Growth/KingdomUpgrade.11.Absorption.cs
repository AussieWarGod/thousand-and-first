using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		public static bool ContentsWouldFit(GameObject Work, string SuccessorBlueprint)
		{
			if (Work == null)
			{
				return false;
			}
			int storedLiquid = 0;
			LiquidVolume volume = Work.GetPart<LiquidVolume>();
			if (volume != null && volume.Volume > 0)
			{
				storedLiquid = volume.Volume;
			}
			int heldItems = (Work.Inventory != null) ? Work.Inventory.Objects.Count : 0;
			GameObjectBlueprint blueprint = string.IsNullOrEmpty(SuccessorBlueprint) ? null : GameObjectFactory.Factory.GetBlueprintIfExists(SuccessorBlueprint);
			if (blueprint == null)
			{
				return storedLiquid <= 0 && heldItems <= 0;
			}
			int capacity = 0;
			if (blueprint.HasPart("LiquidVolume"))
			{
				capacity = blueprint.HasPartParameter("LiquidVolume", "MaxVolume")
					? blueprint.GetPartParameter("LiquidVolume", "MaxVolume", KingdomUpgradeRules.UnknownCapacity)
					: KingdomUpgradeRules.UnknownCapacity;
			}
			return KingdomUpgradeRules.ContentsWouldFit(storedLiquid, capacity, heldItems, blueprint.HasPart("Inventory"));
		}

		/// <summary>
		/// Measures everything the absorption law (brief, Addendum 3) judges, off real ground.
		/// Nothing here reads the clock, the age of the work, or how long anything has stood: the
		/// figures are what the settlement holds right now and what the designs declare. The only
		/// duration involved is the improvement's own build time, which sizes the outage and never
		/// causes the trigger.
		/// </summary>
		/// <param name="System">The kingdom, for its population.</param>
		/// <param name="Z">Zone the work stands in, walked once for the lodging elsewhere.</param>
		/// <param name="Work">The standing work.</param>
		/// <param name="Predecessor">Its registry entry, or null when it did not resolve.</param>
		/// <param name="SuccessorKey">Registry key of the design it would become.</param>
		/// <param name="BuildTicks">The improvement's build time, from
		/// <c>KingdomUpgradeRules.BuildTicks</c>.</param>
		public static KingdomUpgradeRules.AbsorptionDemand MeasureAbsorption(KingdomSystem System,
			Zone Z, GameObject Work, KingdomRules.BuildEntry Predecessor, string SuccessorKey,
			long BuildTicks, KingdomSurvey Survey)
		{
			KingdomUpgradeRules.AbsorptionDemand demand = KingdomUpgradeRules.AbsorptionDemand.None;
			demand.BuildTicks = BuildTicks;
			if (Predecessor == null)
			{
				return demand;
			}
			List<KindAmount> carries;
			if (!KingdomCatalogueRules.TryParseTally(Predecessor.Carries, out carries, out _))
			{
				// A malformed Carries is already reported by the catalogue validator. Everything it
				// managed to parse still counts, which is what TryParseTally hands back.
			}
			demand.IsHousing = string.Equals(Predecessor.Category, HousingCategory, StringComparison.OrdinalIgnoreCase);
			demand.Residents = KingdomCatalogueRules.AmountOf(carries, KingdomCatalogueRules.SupportRoof);
			demand.LuxuryCarried = KingdomCatalogueRules.AmountOf(carries, LuxurySupport);
			demand.SupportPerDay = KingdomCatalogueRules.AmountOf(carries, KingdomCatalogueRules.SupportWater);
			demand.CurrentShelter = ShelterOf(Predecessor.Key);
			int lodgingElsewhere = 0;
			int bestShelter = 0;
			string bestKey = null;
			if (Survey != null)
			{
				for (int i = 0; i < Survey.Built.Count; i++)
				{
					GameObject item = Survey.Built[i];
					if (item == Work || item.GetIntProperty(BuiltProperty) != 1)
					{
						continue;
					}
					string key = DesignKeyOf(item);
					KingdomRules.BuildEntry entry;
					if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
					{
						continue;
					}
					List<KindAmount> theirs;
					KingdomCatalogueRules.TryParseTally(entry.Carries, out theirs, out _);
					int roof = KingdomCatalogueRules.AmountOf(theirs, KingdomCatalogueRules.SupportRoof);
					if (roof <= 0)
					{
						continue;
					}
					lodgingElsewhere += roof;
					int shelter = ShelterOf(key);
					if (shelter > bestShelter || bestKey == null)
					{
						bestShelter = (shelter > bestShelter) ? shelter : bestShelter;
						bestKey = key;
					}
				}
			}
			int spare = lodgingElsewhere - ((System == null) ? 0 : System.Population);
			demand.SpareLodging = (spare > 0) ? spare : 0;
			demand.OfferedShelter = bestShelter;
			// Addendum 4: the best roof on offer must also be somewhere these people would actually
			// live. One citizen with nowhere to charge holds the rebuild exactly as a missing roof
			// does -- and holds it only: nobody is moved, and the refusal is named by the verdict.
			demand.QuartersRefused = demand.IsHousing
					&& KingdomUpgradeRules.QuartersRefused(KingdomQol.OfferOf(bestKey, Z),
						ResidentProfilesIn(Survey), out _);
			demand.MaterialsInHand = Z == null || KingdomMaterials.CanPayUpgrade(Z, Predecessor.Key, out _);
			demand.CraftMet = CraftReaches(System, Z, SuccessorKey);
			return demand;
		}

		/// <summary>
		/// The quality-of-life profiles of the citizens standing on this ground, for the Needs
		/// check Addendum 4 re-bases displacement tolerance onto. Read fresh every time, because
		/// nothing in that vocabulary is stored anywhere.
		/// </summary>
		/// <returns>Never null; empty for a null zone or a zone with nobody in it, which refuses
		/// nothing.</returns>
		public static KingdomUpgradeRules.AbsorptionDemand MeasureAbsorption(KingdomSystem System,
			Zone Z, GameObject Work, KingdomRules.BuildEntry Predecessor, string SuccessorKey,
			long BuildTicks)
		{
			return MeasureAbsorption(System, Z, Work, Predecessor, SuccessorKey, BuildTicks,
				Z == null ? null : KingdomSurvey.Take(Z, System));
		}

		private static List<QolProfile> ResidentProfilesIn(KingdomSurvey Survey)
		{
			List<QolProfile> profiles = new List<QolProfile>();
			if (Survey == null)
			{
				return profiles;
			}
			for (int i = 0; i < Survey.CitizenBodies.Count; i++)
			{
				profiles.Add(KingdomQol.ProfileOf(Survey.CitizenBodies[i]));
			}
			return profiles;
		}

		/// <summary>The catalogue category housing is filed under, which the absorption law judges
		/// by displacement rather than by the output margin.</summary>
	}
}
