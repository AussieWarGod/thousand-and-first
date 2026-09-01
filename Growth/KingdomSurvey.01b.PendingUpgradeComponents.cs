using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSurvey
	{
		/// <summary>Derives component inertness from the durable predecessor/target authority as
		/// well as the component marker. A callback cannot activate a new store by deleting one
		/// convenience property while the old building still stands.</summary>
		private bool IsPendingUpgradeComponent(GameObject Item)
		{
			if (!GameObject.Validate(Item)
				|| !Item.HasIntProperty(KingdomArchitectureStamper.ComponentSchemaProperty)
				|| Item.HasStringProperty(KingdomArchitectureStamper.ComponentSchemaProperty)
				|| Item.GetIntProperty(KingdomArchitectureStamper.ComponentSchemaProperty)
					!= KingdomArchitectureStamper.ComponentSchema
				|| !ExactComponentString(Item, KingdomPlots.PlotIdProperty, out string lot)
				|| !ExactComponentString(Item,
					KingdomArchitectureStamper.ComponentHashProperty, out string hash)) return false;
			if (r_KingdomScaffold.HasPendingImprovementSuccessorEvidence(Item)) return true;
			if (Item.HasStringProperty(KingdomArchitectureStamper.ComponentCarriedProperty)
				|| Item.HasIntProperty(KingdomArchitectureStamper.ComponentCarriedProperty)
					&& Item.GetIntProperty(KingdomArchitectureStamper.ComponentCarriedProperty) != 1)
				return true;
			for (int i = 0; i < Objects.Count; i++)
			{
				GameObject root = Objects[i];
				if (!GameObject.Validate(root)) continue;
				bool pendingTarget = r_KingdomScaffold
					.HasPendingImprovementSuccessorAuthority(root)
					&& ExactRootString(root, KingdomArchitectureStamper.LotIdProperty, lot)
					&& ExactRootString(root, KingdomArchitectureStamper.HashProperty, hash);
				bool predecessorReceipt = root.HasIntProperty(
						KingdomArchitectureStamper.UpgradeSchemaProperty)
					&& !root.HasStringProperty(
						KingdomArchitectureStamper.UpgradeSchemaProperty)
					&& root.GetIntProperty(KingdomArchitectureStamper.UpgradeSchemaProperty)
						== KingdomArchitectureStamper.UpgradeSchema
					&& ExactRootString(root,
						KingdomArchitectureStamper.UpgradeLotProperty, lot)
					&& ExactRootString(root,
						KingdomArchitectureStamper.UpgradeHashProperty, hash);
				if (pendingTarget || predecessorReceipt) return true;
			}
			return false;
		}

		private static bool ExactComponentString(GameObject Item, string Property,
			out string Value)
		{
			Value = Item.GetStringProperty(Property);
			return Item.HasStringProperty(Property) && !Item.HasIntProperty(Property)
				&& !string.IsNullOrEmpty(Value);
		}

		private static bool ExactRootString(GameObject Item, string Property, string Expected)
		{
			return Item.HasStringProperty(Property) && !Item.HasIntProperty(Property)
				&& string.Equals(Item.GetStringProperty(Property), Expected,
					StringComparison.Ordinal);
		}
	}
}
