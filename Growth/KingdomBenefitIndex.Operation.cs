using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		private static int OperationPercent(GameObject Item, GameObject Root,
			IKingdomBenefitProvider Provider, KingdomBenefitOperation Operation,
			KingdomSurvey Survey, string DesignationBuildKey,
			out string Failure, out bool Unsupported)
		{
			Failure = null; Unsupported = false;
			try
			{
				if (!GameObject.Validate(Item) || Item.IsBroken())
					return Stop("provider is absent or broken", out Failure);
				if (!GameObject.Validate(Root) || Root.IsBroken())
					return Stop("designation root is absent or broken", out Failure);
				string affinity = Item.HasTag("r_KingdomProviderBuildKey")
					? Item.GetTag("r_KingdomProviderBuildKey", null) ?? "" : null;
				if (!KingdomBenefitOperationRules.ProviderMatchesDesign(affinity,
					DesignationBuildKey))
					return Stop("provider belongs to a different building design", out Failure);
				int operation;
				switch (Operation)
				{
				case KingdomBenefitOperation.Present:
					operation = 100; break;
				case KingdomBenefitOperation.Staffed:
					operation = StaffingPercent(Root, out Failure); break;
				case KingdomBenefitOperation.Powered:
					operation = Item.QueryCharge() > 0 ? 100
						: Stop("provider has no live delivered charge", out Failure); break;
				case KingdomBenefitOperation.Filled:
					Unsupported = true;
					return Stop("filled providers require a code predicate for relevant contents",
						out Failure);
				case KingdomBenefitOperation.Sown:
					operation = KingdomCrops.IsSown(Item) ? 100
						: Stop("provider is not sown", out Failure); break;
				case KingdomBenefitOperation.Custom:
					operation = CustomPercent(Item, Root, Provider, Survey, out Failure,
						out Unsupported); break;
				default:
					Unsupported = true;
					return Stop("provider operation is unsupported", out Failure);
				}
				if (operation <= 0) return 0;
				int condition = PhysicalConditionPercent(Root);
				if (!ReferenceEquals(Item, Root))
					condition = KingdomBenefitOperationRules.Compose(condition,
						PhysicalConditionPercent(Item));
				int result = KingdomBenefitOperationRules.Compose(condition, operation);
				return result > 0 ? result
					: Stop("designation root or provider has no current physical condition", out Failure);
			}
			catch (Exception exception)
			{
				return Stop("provider operation threw " + exception.GetType().Name, out Failure);
			}
		}

		private static int PhysicalConditionPercent(GameObject Item)
		{
			return KingdomMaterialRules.ConditionPercent(KingdomWear.WearOf(Item));
		}

		internal static int StaffingPercent(GameObject Root, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Root) || Root.GetIntProperty("KingdomStaffNeeded") <= 0)
				return Stop("staffed provider has no exact staffing contract", out Failure);
			if ((Root.GetIntProperty(KingdomAdopt.AdoptedProperty) == 1
					|| Root.Blueprint == KingdomAdopt.WorkMarkerBlueprint)
				&& !KingdomAdoptionOperation.TryRead(Root, out _, out Failure)) return 0;
			int crew = Root.GetIntProperty("KingdomEffectiveness");
			if (Root.GetIntProperty("KingdomStaffed") != 1 || crew <= 0)
				return Stop("designation root has no current assigned crew", out Failure);
			if (crew > 100) crew = 100;
			int affinity = KingdomCrews.ApplyAffinity(Root, crew);
			return affinity > 100 ? 100 : affinity;
		}

		private static int CustomPercent(GameObject Item, GameObject Root,
			IKingdomBenefitProvider Provider, KingdomSurvey Survey, out string Failure,
			out bool Unsupported)
		{
			Failure = null; Unsupported = false;
			if (Provider == null)
			{
				Unsupported = true;
				return Stop("custom operation has no code provider", out Failure);
			}
			if (Provider is IKingdomQuantitativeBenefitProvider quantitative)
			{
				if (!quantitative.TryKingdomBenefitOperationPercent(Item, Root, Survey,
					out int percent, out Failure))
				{
					Unsupported = true;
					return Stop(Failure ?? "custom provider could not prove operation", out Failure);
				}
				if (!KingdomBenefitOperationRules.IsPercent(percent))
				{
					Unsupported = true;
					return Stop("custom provider returned an out-of-range operation percent",
						out Failure);
				}
				return percent > 0 ? percent
					: Stop(Failure ?? "custom provider refused current operation", out Failure);
			}
			return Provider.IsKingdomBenefitOperational(Item, Root, Survey) ? 100
				: Stop("custom provider refused current operation", out Failure);
		}

		private static int Stop(string Message, out string Failure)
		{
			Failure = Message; return 0;
		}
	}
}
