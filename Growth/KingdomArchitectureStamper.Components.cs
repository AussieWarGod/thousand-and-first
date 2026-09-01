using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		private static bool TryPlacementClaim(ArchitecturePlacement Placement,
			TechLevel LiveTech, List<string> Roster, KingdomMaterialDebitCost PaidClaim,
			out string Failure)
		{
			if (Placement == null || !GameObjectFactory.Factory.HasBlueprint(Placement.Blueprint))
				return Fail("added authored slot names a missing blueprint", out Failure);
			int requiredTech;
			if (!KingdomArchitectureRules.TryParseTech(Placement.MinTech, out requiredTech)
				|| requiredTech > (int)LiveTech)
				return Fail("added authored slot " + Placement.Slot + " needs craft rung "
					+ (Placement.MinTech ?? "<missing>"), out Failure);
			if (!string.IsNullOrEmpty(Placement.Knowledge)
				&& KingdomZoningRules.MissingKnowledge(Roster, Placement.Knowledge).Count > 0)
				return Fail("added authored slot " + Placement.Slot + " needs knowledge "
					+ Placement.Knowledge, out Failure);
			if (!string.IsNullOrEmpty(Placement.Power))
				return Fail("added authored slot " + Placement.Slot + " needs power authority "
					+ Placement.Power + ", but this frozen improvement context proves none",
					out Failure);
			KingdomMaterial material;
			if (!KingdomMaterialRules.TryParseMaterial(Placement.Material, out material))
				return Fail("added authored slot " + Placement.Slot + " has unknown material truth",
					out Failure);
			if (!Placement.Natural && !Placement.ExistingAuthority
				&& PaidClaim.Materials.Get(material) <= 0)
				return Fail("added authored slot " + Placement.Slot + " needs "
					+ KingdomMaterialRules.MaterialName(material)
					+ ", absent from the exact paid improvement claim", out Failure);
			Failure = null;
			return true;
		}

		private static bool TryRemovableComponent(GameObject Item,
			ArchitecturePlacement Placement, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Item) || Placement == null || Placement.ExistingAuthority
				|| !string.IsNullOrEmpty(Placement.StatefulAnchor)
				|| Item.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1)
				return Fail("immutable or stateful authored slot "
					+ (Placement == null ? "<missing>" : Placement.Slot) + " cannot be removed",
					out Failure);
			if (Item.Inventory != null && Item.Inventory.Objects.Count != 0)
				return Fail("authored slot " + Placement.Slot
					+ " is a non-empty container and cannot be removed", out Failure);
			LiquidVolume liquid = Item.GetPart<LiquidVolume>();
			if (liquid != null && (liquid.Volume > 0 || liquid.MaxVolume < 0))
				return Fail("authored slot " + Placement.Slot
					+ " contains liquid and cannot be removed", out Failure);
			if (Item.GetIntProperty("KingdomBuilt") == 1
				|| Item.GetIntProperty("KingdomCitizen") == 1
				|| Item.GetIntProperty("KingdomStores") == 1
				|| Item.GetIntProperty("KingdomLarder") == 1
				|| Item.GetIntProperty(KingdomMaterials.StockpileProperty) == 1)
				return Fail("authored slot " + Placement.Slot
					+ " carries protected settlement state", out Failure);
			return true;
		}

		private static bool TryStrikeRemovable(GameObject Item,
			ArchitecturePlacement Placement, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Item) || Placement == null || Placement.ExistingAuthority
				|| Item.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1)
				return Fail("immutable authored slot cannot enter the strike target set", out Failure);
			if (Item.Inventory != null && Item.Inventory.Objects.Count != 0)
				return Fail("authored slot " + Placement.Slot
					+ " must be emptied before strike", out Failure);
			LiquidVolume liquid = Item.GetPart<LiquidVolume>();
			if (liquid != null && liquid.Volume > 0)
				return Fail("authored slot " + Placement.Slot
					+ " contains liquid and cannot be struck", out Failure);
			if (Item.GetIntProperty("KingdomBuilt") == 1
				|| Item.GetIntProperty("KingdomCitizen") == 1
				|| Item.GetIntProperty("KingdomStores") == 1
				|| Item.GetIntProperty("KingdomLarder") == 1
				|| Item.GetIntProperty(KingdomMaterials.StockpileProperty) == 1)
				return Fail("authored slot " + Placement.Slot
					+ " carries protected settlement state", out Failure);
			return true;
		}

		private static bool TryUpgradeBase(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Successor,
			out KingdomArchitectureIntent BeforeIntent, out ArchitectureLayoutSnapshot Before,
			out ArchitectureLayoutSnapshot After, out ArchitectureLayoutDelta Delta,
			out string Lot, out string Failure)
		{
			BeforeIntent = null;
			Before = null;
			After = null;
			Delta = null;
			Lot = null;
			Failure = null;
			if (Owner == null || !KingdomArchitectureRuntime.TryRead(Owner, out BeforeIntent,
				out Failure) || !KingdomArchitectureRuntime.TryDecode(BeforeIntent, out Before,
				out Failure) || !KingdomArchitectureRuntime.TryDecode(Successor, out After,
				out Failure)) return false;
			Lot = Owner.GetStringProperty(LotIdProperty);
			bool heartAccretion;
			if (!KingdomArchitectureRules.IsManagedSnapshotEncoding(BeforeIntent.EncodedSnapshot)
				|| !KingdomArchitectureRules.IsManagedSnapshotEncoding(Successor.EncodedSnapshot)
				|| !ValidLotId(Lot) || Owner.GetStringProperty(HashProperty) != BeforeIntent.SnapshotHash
				|| !TryAuthorizedTransition(Owner, Z, BeforeIntent, Before, Successor, After,
					false, out heartAccretion, out Failure))
				return Fail("authored upgrade receipt crosses its frozen layout set", out Failure);
			return KingdomArchitectureRules.TryBuildDelta(Before, After, out Delta, out Failure);
		}

	}
}
