using System;
using XRL;
using XRL.World;

namespace XRL.World.Parts
{
	/// <summary>Physical provider states which XML cannot honestly prove by declaration alone.</summary>
	[Serializable]
	public sealed class r_KingdomStateBenefitProvider : IPart,
		ThousandAndFirst.IKingdomBenefitProvider,
		ThousandAndFirst.IKingdomQuantitativeBenefitProvider
	{
		public string ProviderKey;
		public string Carries;
		public string Provides;
		public string Scope = "building";
		public string State;

		public bool TryDescribeKingdomBenefits(
			out ThousandAndFirst.KingdomBenefitProviderDeclaration Declaration,
			out string Failure)
		{
			return ThousandAndFirst.KingdomBenefitProviderRules.TryDescribe(ProviderKey,
				Carries, Provides, Scope, "custom", null, out Declaration, out Failure);
		}

		public bool IsKingdomBenefitOperational(GameObject Provider,
			GameObject DesignationRoot, ThousandAndFirst.KingdomSurvey Survey)
		{
			return TryKingdomBenefitOperationPercent(Provider, DesignationRoot, Survey,
				out int percent, out _) && percent > 0;
		}

		public bool TryKingdomBenefitOperationPercent(GameObject Provider,
			GameObject DesignationRoot, ThousandAndFirst.KingdomSurvey Survey,
			out int Percent, out string Failure)
		{
			Percent = 0; Failure = null;
			if (!GameObject.Validate(Provider) || Provider.IsBroken()
				|| !GameObject.Validate(DesignationRoot))
				return Inactive("state provider or designation root is absent", out Percent,
					out Failure);
			switch ((State ?? "").Trim())
			{
			case "HeldFreshWater":
				return Physical(HoldsFreshWater(Provider), "container holds no fresh water",
					out Percent, out Failure);
			case "HeldFreshWaterAndStaffed":
				if (!HoldsFreshWater(Provider))
					return Inactive("container holds no fresh water", out Percent, out Failure);
				return Staffing(DesignationRoot, out Percent, out Failure);
			case "WetOffal":
				if (!HoldsWetOffal(Provider))
					return Inactive("facility has no wet offal", out Percent, out Failure);
				return Staffing(DesignationRoot, out Percent, out Failure);
			case "OpenBrine":
				if (!HoldsOpenBrine(Provider))
					return Inactive("pool has no open brine", out Percent, out Failure);
				return Staffing(DesignationRoot, out Percent, out Failure);
			case "OpenFreshWater":
				return Physical(HoldsOpenFreshWater(Provider), "pool has no open fresh water",
					out Percent, out Failure);
			case "RootSown":
				if (!ThousandAndFirst.KingdomCrops.IsSown(DesignationRoot))
					return Inactive("designation root is not sown", out Percent, out Failure);
				return Staffing(DesignationRoot, out Percent, out Failure);
			case "MirrorPair":
				return Physical(LiveMirrorPair(DesignationRoot), "mirror pair is not live",
					out Percent, out Failure);
			default:
				Failure = "state provider names an unsupported state"; return false;
			}
		}

		private static bool Staffing(GameObject Root, out int Percent, out string Failure)
		{
			Percent = ThousandAndFirst.KingdomBenefitIndex.StaffingPercent(Root, out Failure);
			return true;
		}

		private static bool Physical(bool Live, string Message,
			out int Percent, out string Failure)
		{
			if (!Live) return Inactive(Message, out Percent, out Failure);
			Percent = 100; Failure = null; return true;
		}

		private static bool Inactive(string Message, out int Percent, out string Failure)
		{
			Percent = 0; Failure = Message; return true;
		}

		private static bool HoldsFreshWater(GameObject Provider)
		{
			if (Provider.Inventory == null) return false;
			for (int i = 0; i < Provider.Inventory.Objects.Count; i++)
			{
				LiquidVolume liquid = Provider.Inventory.Objects[i]?.GetPart<LiquidVolume>();
				if (liquid != null && ThousandAndFirst.KingdomLiquids.HasFreshWater(liquid))
					return true;
			}
			return false;
		}

		private static bool HoldsWetOffal(GameObject Provider)
		{
			if (Provider.Inventory == null) return false;
			bool stock = false;
			bool liquid = false;
			for (int i = 0; i < Provider.Inventory.Objects.Count; i++)
			{
				GameObject item = Provider.Inventory.Objects[i];
				if (!GameObject.Validate(item)) continue;
				LiquidVolume volume = item.GetPart<LiquidVolume>();
				if (volume != null && volume.Volume > 0) liquid = true;
				if (item.GetIntProperty(ThousandAndFirst.KingdomLab.KeptProperty) == 1
					|| item.HasPart("DismemberedProperties")
					|| !string.IsNullOrEmpty(item.GetStringProperty(
						ThousandAndFirst.KingdomProcedures.StampProperty))) stock = true;
			}
			return stock && liquid;
		}

		private static bool HoldsOpenBrine(GameObject Provider)
		{
			LiquidVolume liquid = Provider.GetPart<LiquidVolume>();
			return liquid != null && liquid.MaxVolume < 0 && liquid.Volume > 0
				&& liquid.ComponentLiquids != null && liquid.ComponentLiquids.Count > 0
				&& !ThousandAndFirst.KingdomLiquids.HasFreshWater(liquid);
		}

		private static bool HoldsOpenFreshWater(GameObject Provider)
		{
			LiquidVolume liquid = Provider.GetPart<LiquidVolume>();
			return liquid != null && liquid.MaxVolume < 0
				&& ThousandAndFirst.KingdomLiquids.HasFreshWater(liquid);
		}

		private static bool LiveMirrorPair(GameObject Root)
		{
			r_KingdomMirrorGate gate = Root.GetPart<r_KingdomMirrorGate>();
			ThousandAndFirst.KingdomSystem system = The.Game?.GetSystem<
				ThousandAndFirst.KingdomSystem>();
			return gate != null && system != null
				&& ThousandAndFirst.KingdomMirrorGate.TryPurposeConnection(gate, system,
					out _, out _);
		}
	}
}
