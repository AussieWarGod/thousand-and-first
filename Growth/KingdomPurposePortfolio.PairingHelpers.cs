using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static bool TryPurposeStores(Zone Zone, GameObject Root,
			out GameObject Input, out GameObject Output, out bool Legacy, out string Failure)
		{
			Input = null;
			Output = null;
			Legacy = false;
			Failure = null;
			if (!TryAuthoredPurposeStores(Zone, Root, out Input, out Output,
				out bool declared, out Failure)) return false;
			if (declared) return true;
			Legacy = true;
			return TryLegacyPurposeStores(Zone, out Input, out Output, out Failure);
		}

		/// <summary>Capability branch: receipt-less roots and complete current snapshots with no
		/// purpose-store roles disclose legacy storage. One-sided, duplicate, or torn authored
		/// evidence refuses; a complete role pair resolves through exact layout receipts.</summary>
		private static bool TryAuthoredPurposeStores(Zone Zone, GameObject Root,
			out GameObject Input, out GameObject Output, out bool Declared, out string Failure)
		{
			Input = null;
			Output = null;
			Declared = false;
			Failure = null;
			if (Zone == null)
				return Fail("Purpose-store ground is unavailable.", out Failure);
			if (!HasPurposeArchitectureEvidence(Root)) return true;
			if (!KingdomArchitectureStamper.TryExactLayoutOwner(Root, Zone,
				out _, out ArchitectureLayoutSnapshot snapshot, out _, out Failure))
				return Fail(Failure ?? "The purpose root has a partial or unknown architecture receipt.",
					out Failure);
			int inputs = PurposeRoleCount(snapshot, "purpose:input");
			int outputs = PurposeRoleCount(snapshot, "purpose:output");
			if (inputs == 0 && outputs == 0) return true;
			Declared = true;
			if (inputs != 1 || outputs != 1)
				return Fail("The purpose root must author exactly one purpose:input and one purpose:output fixture.",
					out Failure);
			if (!KingdomArchitectureStamper.TryExactAnchoredComponent(Root, Zone,
				"purpose:input", out Input, out Failure)
				|| !KingdomArchitectureStamper.TryExactAnchoredComponent(Root, Zone,
					"purpose:output", out Output, out Failure)) return false;
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Zone);
			if (!ExactPurposeStore(stock, Input) || !ExactPurposeStore(stock, Output)
				|| ReferenceEquals(Input, Output) || Input.IDIfAssigned == Output.IDIfAssigned)
				return Fail("An authored purpose store lost its exact lot custody, identity, inventory, or stockpile dedication.",
					out Failure);
			return true;
		}

		private static bool TryLegacyPurposeStores(Zone Zone, out GameObject Input,
			out GameObject Output, out string Failure)
		{
			Input = null;
			Output = null;
			Failure = null;
			List<GameObject> stores = new List<GameObject>();
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Zone);
			for (int i = 0; i < stock.Stockpiles.Count; i++)
			{
				GameObject candidate = stock.Stockpiles[i];
				if (GameObject.Validate(candidate) && candidate.Inventory != null
					&& !string.IsNullOrEmpty(candidate.IDIfAssigned)
					&& !stores.Contains(candidate)) stores.Add(candidate);
			}
			stores.Sort((a, b) => string.CompareOrdinal(a.IDIfAssigned, b.IDIfAssigned));
			if (stores.Count < 2)
				return Fail("Each city needs two distinct dedicated material stockpiles for legacy receipt-less binding: the lowest exact identity is input and the next is output.",
					out Failure);
			Input = stores[0];
			Output = stores[1];
			return Input.IDIfAssigned != Output.IDIfAssigned
				|| Fail("Legacy purpose input and output identities collide.", out Failure);
		}

		/// <summary>Re-proves a disclosed legacy binding by its frozen identities. This never
		/// re-runs the ordinal selection after pair freeze.</summary>
		private static bool TryFrozenPurposeStores(Zone Zone, string InputId, string OutputId,
			out GameObject Input, out GameObject Output, out string Failure)
		{
			Input = null;
			Output = null;
			Failure = null;
			if (Zone == null || string.IsNullOrEmpty(InputId) || string.IsNullOrEmpty(OutputId)
				|| InputId == OutputId
				|| FindExactKnown(Zone, InputId, out Input) != KingdomPhysicalLookupState.Exact
				|| FindExactKnown(Zone, OutputId, out Output) != KingdomPhysicalLookupState.Exact)
				return Fail("The frozen legacy purpose stores are absent, duplicated, or collide.",
					out Failure);
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Zone);
			if (!ExactPurposeStore(stock, Input) || !ExactPurposeStore(stock, Output)
				|| !KingdomMaterials.IsStockpile(Input) || !KingdomMaterials.IsStockpile(Output)
				|| ReferenceEquals(Input, Output))
				return Fail("The frozen legacy purpose stores lost exact custody or dedication.",
					out Failure);
			return true;
		}

		private static bool ExactPurposeStore(KingdomMaterials.MaterialStock Stock,
			GameObject Store)
		{
			if (Stock == null || !GameObject.Validate(Store) || Store.Inventory == null
				|| string.IsNullOrEmpty(Store.IDIfAssigned)) return false;
			int matches = 0;
			for (int i = 0; i < Stock.Stockpiles.Count; i++)
				if (ReferenceEquals(Stock.Stockpiles[i], Store)) matches++;
			return matches == 1;
		}

		private static int PurposeRoleCount(ArchitectureLayoutSnapshot Snapshot, string Role)
		{
			int count = 0;
			for (int i = 0; Snapshot != null && i < Snapshot.Placements.Count; i++)
			{
				string key = Snapshot.Placements[i].StatefulAnchor;
				int identity = key == null ? -1 : key.LastIndexOf('@');
				string role = identity < 0 ? key : key.Substring(0, identity);
				if (role == Role) count++;
			}
			return count;
		}

		private static bool HasPurposeArchitectureEvidence(GameObject Root)
		{
			return Root != null && (HasPurposeProperty(Root,
				KingdomArchitectureRuntime.SchemaProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.BuildKeyProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.PlanKeyProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.BindingKeyProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.TierKeyProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.VariantKeyProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.PaletteKeyProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.LotTypeProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.LotSizeProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.FacingProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.SnapshotProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.HashProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.RectX1Property)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.RectY1Property)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.RectX2Property)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.RectY2Property)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.MainXProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureRuntime.MainYProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureStamper.SchemaProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureStamper.LotIdProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureStamper.HashProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureStamper.NextLayerProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureStamper.FaultProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureStamper.UpgradeSchemaProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureStamper.UpgradeTargetProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureStamper.UpgradeHashProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureStamper.UpgradeLotProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureStamper.UpgradePhaseProperty)
				|| HasPurposeProperty(Root, KingdomArchitectureStamper.UpgradeFaultProperty));
		}

		private static bool HasPurposeProperty(GameObject Root, string Property)
		{
			return Root != null && (Root.HasIntProperty(Property)
				|| Root.HasStringProperty(Property));
		}

		private static string PurposeStoreBinding(bool Legacy)
		{
			return Legacy
				? "legacy receipt-less/zero-anchor ordinal stores (lowest exact identity input, next output)"
				: "authored purpose:input and purpose:output fixtures on the root's exact frozen lot";
		}

		private static bool TryStandingPurpose(Zone Zone, out KingdomPurposeKind Kind,
			out GameObject Work, out string Failure)
		{
			Kind = KingdomPurposeKind.None;
			Work = null;
			Failure = null;
			List<GameObject> found = new List<GameObject>();
			foreach (GameObject candidate in Zone?.GetObjects() ?? new List<GameObject>())
				if (KingdomUpgrade.IsFunctionallyBuilt(candidate)
					&& KingdomPurposePortfolioRules.TryBuildKind(
						KingdomUpgrade.DesignKeyOf(candidate), out _))
				{
					if (string.IsNullOrEmpty(candidate.IDIfAssigned))
					{
						Failure = "A standing purpose root lacks assigned identity.";
						return false;
					}
					found.Add(candidate);
				}
			found.Sort((a, b) => string.CompareOrdinal(a.IDIfAssigned, b.IDIfAssigned));
			if (found.Count > 1)
			{
				Failure = "This city holds more than one standing purpose root; pairing is refused until the one-purpose invariant is repaired.";
				return false;
			}
			if (found.Count == 0) return true;
			Work = found[0];
			KingdomPurposePortfolioRules.TryBuildKind(KingdomUpgrade.DesignKeyOf(Work), out Kind);
			return true;
		}

		private static List<KingdomPurposeKind> PairChoices(KingdomPurposeKind First,
			KingdomPurposeKind StandingSecond, bool AllowStanding)
		{
			List<KingdomPurposeKind> choices = new List<KingdomPurposeKind>();
			if (StandingSecond != KingdomPurposeKind.None)
			{
				if (AllowStanding && KingdomPurposePortfolioRules.Compatible(First, StandingSecond))
					choices.Add(StandingSecond);
				return choices;
			}
			IList<KingdomPurposeKind> partners = KingdomPurposePortfolioRules.Partners(First);
			for (int i = 0; i < partners.Count; i++) choices.Add(partners[i]);
			return choices;
		}

		private static string PartnerList(KingdomPurposeKind Kind)
		{
			IList<KingdomPurposeKind> partners = KingdomPurposePortfolioRules.Partners(Kind);
			if (partners.Count != 2) return "no lawful pair";
			return "{{C|" + KingdomPurposePortfolioRules.PurposeName(partners[0])
				+ "}} or {{C|" + KingdomPurposePortfolioRules.PurposeName(partners[1]) + "}}";
		}
	}
}
