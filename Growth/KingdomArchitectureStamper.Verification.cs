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
		private static bool TryVerifyLayer(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot, string Lot,
			ArchitectureLayer Layer, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (placement.Layer != Layer) continue;
				GameObject exact;
				if (!TryExactOutput(Owner, Z, Intent, Lot, placement, out exact, out Failure)) return false;
			}
			return true;
		}

		private static bool TryExactOutput(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Intent, string Lot, ArchitecturePlacement Placement,
			out GameObject Exact, out string Failure)
		{
			Exact = null;
			Failure = null;
			if (Owner.GetIntProperty(OutputState(Placement)) != 2)
				return Fail("layout slot " + Placement.Slot + " is not settled", out Failure);
			string id = Owner.GetStringProperty(OutputId(Placement));
			KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(Z, id, out Exact);
			if (state != KingdomPhysicalLookupState.Exact
				// No upgrade receipt is in scope here, so no other generation may stand: null is
				// the truth, not a gap. Reached mid-upgrade through TryVerifyComplete, a retagged
				// item fails the element checks above before the census is ever counted.
				|| !ExactComponent(Owner, Exact, Z, Intent, Lot, Placement, id, null))
				return Quarantine(Owner, "settled layout slot " + Placement.Slot
					+ " is absent, moved, duplicated, or changed", out Failure);
			return true;
		}

		/// <summary>
		/// Every element of one settled component, then a census of the slot it stands at.
		/// <para><paramref name="Peer" /> names the single component of the other generation that
		/// may legitimately share this slot name during an authored upgrade, resolved by the
		/// caller that owns the receipt; null means none may.</para>
		/// </summary>
		private static bool ExactComponent(GameObject Owner, GameObject Item, Zone Z,
			KingdomArchitectureIntent Intent, string Lot, ArchitecturePlacement Placement,
			string ExpectedId, ArchitectureComponentPeer Peer)
		{
			if (!GameObject.Validate(Item) || Item.ID != ExpectedId || Item.CurrentZone != Z
				|| Item.Blueprint != Placement.Blueprint
				|| !ExactComponentInt(Item, ComponentSchemaProperty, ComponentSchema)
				|| !ExactComponentString(Item, KingdomPlots.PlotIdProperty, Lot)
				|| !ExactComponentString(Item, ComponentSlotProperty, Placement.Slot)
				|| !ExactComponentInt(Item, ComponentLayerProperty, (int)Placement.Layer)
				|| !ExactComponentString(Item, ComponentHashProperty, Intent.SnapshotHash)
				|| !ExactComponentString(Item, ComponentTokenProperty,
					ComponentToken(Lot, Intent.SnapshotHash, Placement))
				|| !ExactComponentInt(Item, ComponentExistingProperty,
					Placement.ExistingAuthority ? 1 : 0)
				|| !ExactComponentInt(Item, KingdomPlots.PlotPartProperty,
					Placement.ExistingAuthority ? 0 : 1)
				|| !ExactOptionalComponentString(Item, ComponentAnchorProperty,
					Placement.StatefulAnchor)
				|| !ExactOptionalComponentInt(Item, ComponentCarriedProperty, 1)
				|| !ExactPendingComponentState(Owner, Item, Intent)) return false;
			ArchitectureLayoutSnapshot snapshot;
			if (!KingdomArchitectureRuntime.TryDecode(Intent, out snapshot, out _)) return false;
			int x;
			int y;
			if (!KingdomArchitectureRuntime.TryWorldPlacement(snapshot, Intent.Rect, Placement,
				out x, out y, out _) || Item.CurrentCell != Z.GetCell(x, y)) return false;
			// Generation-aware uniqueness. The lot survives an authored upgrade and the slot name
			// is layout-local, so during the retag pass a component of the other generation can
			// legitimately share this slot name while standing on its own world cell. Every
			// candidate at the lot and slot is classified and none is skipped: this generation's
			// token is THIS, the caller's resolved peer -- proved by identity, cell and token
			// together, and only while its retain state allows it -- is OTHER, and anything else
			// is FOREIGN and refuses the census outright.
			string token = ComponentToken(Lot, Intent.SnapshotHash, Placement);
			List<ArchitectureComponentCensusRow> candidates =
				new List<ArchitectureComponentCensusRow>();
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject candidate in survey.ArchitectureComponents)
			{
				if (!GameObject.Validate(candidate)) continue;
				candidates.Add(new ArchitectureComponentCensusRow(
					candidate.GetStringProperty(KingdomPlots.PlotIdProperty),
					candidate.GetStringProperty(ComponentSlotProperty),
					candidate.GetStringProperty(ComponentTokenProperty),
					candidate.IDIfAssigned,
					Peer != null && Peer.Cell != null && candidate.CurrentCell == Peer.Cell));
			}
			return KingdomArchitectureComponentCensusRules.Settles(Lot, Placement.Slot, token,
				candidates, Peer == null ? null : Peer.Id, Peer == null ? null : Peer.Token,
				Peer != null && Peer.Allowed);
		}

		private static bool ExactComponentInt(GameObject Item, string Property, int Expected)
		{
			return KingdomArchitectureReceiptPrefixRules.ExactInt(
				Item.HasIntProperty(Property), Item.GetIntProperty(Property),
				Item.HasStringProperty(Property), Expected);
		}

		private static bool ExactComponentString(GameObject Item, string Property,
			string Expected)
		{
			return KingdomArchitectureReceiptPrefixRules.ExactString(
				Item.HasStringProperty(Property), Item.GetStringProperty(Property),
				Item.HasIntProperty(Property), Expected);
		}

		private static bool ExactOptionalComponentInt(GameObject Item, string Property,
			int Expected)
		{
			return KingdomArchitectureReceiptPrefixRules.ExactOptionalInt(
				Item.HasIntProperty(Property), Item.GetIntProperty(Property),
				Item.HasStringProperty(Property), Expected);
		}

		private static bool ExactOptionalComponentString(GameObject Item, string Property,
			string Expected)
		{
			return KingdomArchitectureReceiptPrefixRules.ExactOptionalString(
				Item.HasStringProperty(Property), Item.GetStringProperty(Property),
				Item.HasIntProperty(Property), Expected);
		}

		private static bool CanInsert(GameObject Owner, Zone Z, Cell Cell, string Lot,
			string Hash, ArchitecturePlacement Placement, out string Failure)
		{
			Failure = null;
			if (Cell == null) return Fail("layout slot lies outside its frozen zone", out Failure);
			List<GameObject> objects = Cell.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (!GameObject.Validate(item) || ReferenceEquals(item, Owner)
					|| item.GetIntProperty(KingdomPlots.HeartStakeProperty) == 1) continue;
				if (item.IsCreature || item.IsPlayer())
					return Fail("a living occupant moved onto layout slot " + Placement.Slot,
						out Failure);
				if (item.GetStringProperty(KingdomPlots.PlotIdProperty) == Lot
					&& item.GetStringProperty(ComponentHashProperty) == Hash
					&& item.GetIntProperty(ComponentSchemaProperty) == ComponentSchema) continue;
				if (KingdomPlots.ReadObject(item) == KingdomPlotRules.GroundKind.Bare) continue;
				return Fail("protected or foreign state moved onto layout slot " + Placement.Slot,
					out Failure);
			}
			return true;
		}

		private static void StampComponent(GameObject Owner, GameObject Item, string Lot, string Hash,
			ArchitecturePlacement Placement)
		{
			Item.SetIntProperty(KingdomPlots.PlotPartProperty,
				Placement.ExistingAuthority ? 0 : 1);
			Item.SetStringProperty(KingdomPlots.PlotIdProperty, Lot);
			Item.SetStringProperty(ComponentSlotProperty, Placement.Slot);
			Item.SetIntProperty(ComponentLayerProperty, (int)Placement.Layer);
			Item.SetStringProperty(ComponentAnchorProperty, Placement.StatefulAnchor,
				RemoveIfNull: true);
			Item.SetStringProperty(ComponentHashProperty, Hash);
			Item.SetStringProperty(ComponentTokenProperty, ComponentToken(Lot, Hash, Placement));
			Item.SetIntProperty(ComponentExistingProperty, Placement.ExistingAuthority ? 1 : 0);
			Item.RemoveIntProperty(ComponentCarriedProperty);
			StampPendingComponentState(Owner, Item);
			Item.SetIntProperty(ComponentSchemaProperty, ComponentSchema);
		}

		private static Dictionary<string, GameObject> EmptyExisting()
		{
			return new Dictionary<string, GameObject>(StringComparer.Ordinal);
		}

		private static bool TryExistingBindings(Zone Z, ArchitectureLayoutSnapshot Snapshot,
			KingdomPlotRules.PlotRect Rect, out Dictionary<string, GameObject> Existing,
			out string Failure)
		{
			Existing = EmptyExisting();
			Failure = null;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (!placement.ExistingAuthority) continue;
				int x;
				int y;
				if (!KingdomArchitectureRuntime.TryWorldPlacement(Snapshot, Rect, placement,
					out x, out y, out Failure)) return false;
				GameObject exact;
				if (!TryFindExistingAt(Z, placement, Z.GetCell(x, y), out exact, out Failure))
					return false;
				Existing[placement.Slot] = exact;
			}
			return true;
		}

		private static bool TryFindExistingAt(Zone Z, ArchitecturePlacement Placement,
			Cell ExpectedCell, out GameObject Exact, out string Failure)
		{
			Exact = null;
			Failure = null;
			if (!Placement.ExistingAuthority || Placement.Blueprint != KingdomPlots.HeartRelicBlueprint
				|| ExpectedCell == null)
				return Fail("existing-authority slot is not the immutable first basin", out Failure);
			int count = 0;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject item in survey.HeartRelics)
			{
				if (!GameObject.Validate(item)
					|| item.GetIntProperty(KingdomPlots.HeartRelicProperty) != 1) continue;
				count++;
				Exact = item;
			}
			if (count != 1 || Exact.Blueprint != Placement.Blueprint
				|| Exact.CurrentCell != ExpectedCell || Exact.CurrentZone != Z)
			{
				Exact = null;
				return Fail("the immutable first basin is absent, duplicated, moved, or misaligned",
					out Failure);
			}
			return true;
		}

		private static bool IsExpectedExisting(GameObject Item,
			Dictionary<string, GameObject> Existing)
		{
			foreach (KeyValuePair<string, GameObject> pair in Existing)
				if (ReferenceEquals(pair.Value, Item)) return true;
			return false;
		}

		private static bool IsExactExistingCore(GameObject Item,
			ArchitecturePlacement Placement, KingdomArchitectureIntent Intent)
		{
			if (!GameObject.Validate(Item) || !Placement.ExistingAuthority
				|| Item.Blueprint != Placement.Blueprint
				|| Item.GetIntProperty(KingdomPlots.HeartRelicProperty) != 1) return false;
			ArchitectureLayoutSnapshot snapshot;
			int x;
			int y;
			return KingdomArchitectureRuntime.TryDecode(Intent, out snapshot, out _)
				&& KingdomArchitectureRuntime.TryWorldPlacement(snapshot, Intent.Rect, Placement,
					out x, out y, out _) && Item.CurrentCell != null
				&& Item.CurrentCell.X == x && Item.CurrentCell.Y == y;
		}

	}
}
