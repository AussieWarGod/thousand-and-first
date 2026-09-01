using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		private static int NativeProviderCount(GameObject Item, bool ExplicitRoof)
		{
			if (!GameObject.Validate(Item)) return 0;
			int count = !ExplicitRoof && Item.HasPart("Bed") ? 1 : 0;
			if (Item.HasPart("Campfire")) count++;
			if (Item.HasPart("Shrine")) count += 2;
			if (Item.HasPart("MarkovBookshelf")) count += 2;
			if (Item.HasPart("UniversalCharger")) count++;
			LiquidVolume liquid = Item.GetPart<LiquidVolume>();
			if (liquid != null && liquid.Volume > 0 && KingdomLiquids.HasFreshWater(liquid))
			{
				count++;
				if (liquid.MaxVolume < 0) count++;
			}
			return count;
		}

		private static IEnumerable<KingdomBenefitProviderDeclaration> NativeProviders(
			GameObject Item, bool ExplicitRoof)
		{
			if (!ExplicitRoof && Item.HasPart("Bed"))
				yield return Carry("taf:native-bed", "roof", 1,
					KingdomBenefitScope.Habitable, KingdomBenefitOperation.Present);
			if (Item.HasPart("Campfire"))
				yield return Tag("taf:native-campfire-cooking",
					KingdomBenefitCapabilities.Cooking,
					KingdomBenefitScope.Plot, KingdomBenefitOperation.Present);
			if (Item.HasPart("Shrine"))
			{
				yield return Tag("taf:native-shrine-capability",
					KingdomBenefitCapabilities.Shrine,
					KingdomBenefitScope.Plot, KingdomBenefitOperation.Present);
				yield return Carry("taf:native-shrine", "spirit", 1,
					KingdomBenefitScope.Interior, KingdomBenefitOperation.Present);
			}
			if (Item.HasPart("MarkovBookshelf"))
			{
				yield return Tag("taf:native-bookshelf-education",
					KingdomBenefitCapabilities.Education,
					KingdomBenefitScope.Interior, KingdomBenefitOperation.Staffed);
				yield return Carry("taf:native-bookshelf", "learning", 1,
					KingdomBenefitScope.Interior, KingdomBenefitOperation.Present);
			}
			if (Item.HasPart("UniversalCharger"))
				yield return Tag("taf:native-charger", KingdomQolRules.TagCharge,
					KingdomBenefitScope.Building, KingdomBenefitOperation.Powered);
			LiquidVolume liquid = Item.GetPart<LiquidVolume>();
			if (liquid != null && liquid.Volume > 0 && KingdomLiquids.HasFreshWater(liquid))
			{
				yield return Tag("taf:native-freshwater", KingdomQolRules.TagDamp,
					KingdomBenefitScope.Building, KingdomBenefitOperation.Present);
				if (liquid.MaxVolume < 0)
					yield return Tag("taf:native-openwater", KingdomQolRules.TagOpenWater,
						KingdomBenefitScope.Building, KingdomBenefitOperation.Present);
			}
		}

		private static KingdomBenefitProviderDeclaration Carry(string Key, string Kind, int Amount,
			KingdomBenefitScope Scope, KingdomBenefitOperation Operation)
		{
			KingdomBenefitProviderDeclaration result = new KingdomBenefitProviderDeclaration {
				Key = Key, Scope = Scope, Operation = Operation };
			result.Carries.Add(new KindAmount(Kind, Amount)); return result;
		}

		private static KingdomBenefitProviderDeclaration Tag(string Key, string Tag,
			KingdomBenefitScope Scope, KingdomBenefitOperation Operation)
		{
			KingdomBenefitProviderDeclaration result = new KingdomBenefitProviderDeclaration {
				Key = Key, Scope = Scope, Operation = Operation };
			result.Provides.Add(Tag); return result;
		}

	}
}
