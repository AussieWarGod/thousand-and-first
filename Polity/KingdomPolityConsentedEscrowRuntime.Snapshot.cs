using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal sealed class KingdomPolityGroundEscrowSnapshot
	{
		internal GameObject Item;
		internal string PlanId;
		internal string StakeRef;
		internal string ActorId;
		internal string ObjectId;
		internal string Blueprint;
		internal string DisplayName;
		internal int Count;
		internal string Owner;
		internal string ZoneId;
		internal int X;
		internal int Y;
		internal string RealmId;
		internal string SettlementId;
		internal string FactionId;
		internal r_KingdomProperty Property;
		internal string Digest;
	}

	internal static partial class KingdomPolityConsentedEscrowRuntime
	{
		internal const int MaxGroundRoots = 16384;

		internal static bool TryCaptureNew(KingdomSystem System,
			KingdomPolityIncidentRecord Plan, GameObject Item,
			out KingdomPolityGroundEscrowSnapshot Snapshot, out string Failure)
		{
			Snapshot = null; Failure = null;
			GameObject actor = The.Player;
			KingdomPolityRouteRecord stake = KingdomPolityConflictRules.ExactEscrowStake(
				System?.PolityLedger, Plan);
			string actorId = actor?.IDIfAssigned;
			if (!GameObject.Validate(actor) || !actor.IsPlayer() || stake == null ||
				string.IsNullOrEmpty(actorId) ||
				!TryCapture(System, Plan, stake.RouteId, actorId, Item, true,
					out Snapshot, out Failure) ||
				!KingdomConstructionInputLeaseAuthority.TryObjectAvailableForLocalDebit(
					Item, out Failure)) return false;
			return true;
		}

		internal static bool TryReproveMarker(KingdomSystem System,
			KingdomPolityProjectionReceipt Projection, GameObject Item,
			out KingdomPolityGroundEscrowSnapshot Snapshot, out string Failure)
		{
			Snapshot = null; Failure = null;
			r_KingdomPolityEscrow marker = Item?.GetPart<r_KingdomPolityEscrow>();
			KingdomPolityIncidentRecord plan = FindPlan(System?.PolityLedger,
				Projection?.SourceRef);
			if (marker == null || marker.Version != r_KingdomPolityEscrow.CurrentVersion ||
				Projection == null || Projection.ObjectIds.Count != 1 ||
				marker.ProjectionId != Projection.ProjectionId ||
				marker.IncidentPlanId != Projection.SourceRef ||
				marker.ObjectId != Projection.ObjectIds[0] ||
				marker.ZoneId != Projection.ZoneId || marker.SnapshotDigest !=
					Projection.PriorDigest || marker.AppliedDigest != Projection.AppliedDigest ||
				!TryCapture(System, plan, marker.StakeRef, marker.ActorId, Item, false,
					out Snapshot, out Failure) || Snapshot.Digest != marker.SnapshotDigest ||
				Snapshot.ObjectId != marker.ObjectId || Snapshot.Blueprint != marker.Blueprint ||
				Snapshot.DisplayName != marker.DisplayName || Snapshot.Count != marker.Count ||
				Snapshot.Owner != marker.Owner || Snapshot.X != marker.X || Snapshot.Y != marker.Y)
			{
				Failure = Failure ?? "escrow marker or exact ground pre-state changed"; return false;
			}
			return true;
		}

		internal static bool TryAttachMarker(KingdomPolityGroundEscrowSnapshot S,
			KingdomPolityProjectionReceipt Projection, out string Failure)
		{
			Failure = null;
			if (S == null || Projection == null || S.Item.GetPart<r_KingdomPolityEscrow>() != null)
			{
				Failure = "collateral already carries escrow custody"; return false;
			}
			r_KingdomPolityEscrow marker = new r_KingdomPolityEscrow
			{
				ProjectionId = Projection.ProjectionId, IncidentPlanId = S.PlanId,
				StakeRef = S.StakeRef, ActorId = S.ActorId, ObjectId = S.ObjectId,
				Blueprint = S.Blueprint, DisplayName = S.DisplayName, Count = S.Count,
				Owner = S.Owner, ZoneId = S.ZoneId, X = S.X, Y = S.Y,
				SnapshotDigest = S.Digest, AppliedDigest = Projection.AppliedDigest
			};
			try { S.Item.AddPart(marker); }
			catch (Exception error)
			{
				Failure = "escrow lease could not attach (" + error.GetType().Name + ")";
				return false;
			}
			return ReferenceEquals(S.Item.GetPart<r_KingdomPolityEscrow>(), marker) ||
				EscrowFail("escrow lease did not attach exactly", out Failure);
		}

		internal static bool TryRemoveMarker(KingdomSystem System,
			KingdomPolityProjectionReceipt Projection, GameObject Item, out string Failure)
		{
			Failure = null;
			if (!TryReproveMarker(System, Projection, Item,
				out KingdomPolityGroundEscrowSnapshot snapshot, out Failure)) return false;
			r_KingdomPolityEscrow marker = Item.GetPart<r_KingdomPolityEscrow>();
			try { Item.RemovePart(marker); }
			catch (Exception error)
			{
				Failure = "escrow lease could not release (" + error.GetType().Name + ")";
				return false;
			}
			if (Item.GetPart<r_KingdomPolityEscrow>() != null ||
				!TryCapture(System, FindPlan(System.PolityLedger, snapshot.PlanId),
					snapshot.StakeRef, snapshot.ActorId, Item, false,
					out KingdomPolityGroundEscrowSnapshot restored, out Failure) ||
				restored.Digest != snapshot.Digest)
				return EscrowFail(Failure ?? "collateral pre-state was not restored",
					out Failure);
			return true;
		}

		private static bool TryCapture(KingdomSystem System,
			KingdomPolityIncidentRecord Plan, string StakeRef, string ActorId, GameObject Item,
			bool RequireNearby, out KingdomPolityGroundEscrowSnapshot S, out string Failure)
		{
			S = null; Failure = null;
			Zone zone = Item?.CurrentZone; Cell cell = Item?.CurrentCell;
			GameObject player = The.Player;
			if (System == null || !System.Founded || System.City == null ||
				!System.TryGetCurrentIdentity(out string realm, out string settlement) ||
				Plan == null || !KingdomPolityAuthority.Contains(Plan.EligibleSurfaceRefs,
					settlement) || !KingdomPolityAuthority.Contains(Plan.DisclosedStakeRefs,
					StakeRef) || !GameObject.Validate(Item) || Item.Physics == null ||
				Item.Holder != null || Item.InInventory != null || Item.IsCreature ||
				Item.Count != 1 || Item.IsImportant() ||
				(RequireNearby && !Item.IsTakeable()) ||
				(RequireNearby && player?.IDIfAssigned != ActorId) ||
				string.IsNullOrEmpty(Item.Blueprint) || cell == null || zone == null ||
				cell.ParentZone != zone || zone != player?.CurrentZone ||
				!System.ClaimedZones.Contains(zone.ZoneID) ||
				(RequireNearby && !Nearby(player, Item)))
				return EscrowFail("collateral must be one exact realm-owned object on loaded ground",
					out Failure);
			r_KingdomProperty property = Item.GetPart<r_KingdomProperty>();
			string objectId = Item.IDIfAssigned;
			if (string.IsNullOrEmpty(objectId))
				return EscrowFail("collateral has no pre-existing exact identity", out Failure);
			if (property == null || property.ReceiptVersion !=
				KingdomPropertyRules.CurrentReceiptVersion || property.Phase !=
					KingdomPropertyPhase.Designated || property.OwnerRealmId != realm ||
				property.OwnerSettlementId != settlement || property.FactionId !=
					System.KingdomFactionName || property.ObjectId != objectId ||
				(Item.Physics.Owner ?? "") != System.KingdomFactionName ||
				!UniqueRoot(zone, Item, objectId))
				return EscrowFail("collateral lacks exact current-realm property authority",
					out Failure);
			S = new KingdomPolityGroundEscrowSnapshot
			{
				Item = Item, PlanId = Plan.IncidentPlanId, StakeRef = StakeRef,
				ActorId = ActorId, ObjectId = objectId, Blueprint = Item.Blueprint,
				DisplayName = Item.ShortDisplayNameStripped, Count = Item.Count,
				Owner = Item.Physics.Owner ?? "", ZoneId = zone.ZoneID, X = cell.X, Y = cell.Y,
				RealmId = realm, SettlementId = settlement,
				FactionId = System.KingdomFactionName, Property = property
			};
			S.Digest = SnapshotDigest(S); return true;
		}

		private static string SnapshotDigest(KingdomPolityGroundEscrowSnapshot S)
		{
			return KingdomPolityRules.ActivationDigest("polity-consented-ground-snapshot-v1",
				S.PlanId, S.StakeRef, S.ActorId, S.ObjectId, S.Blueprint, S.DisplayName,
				S.Count.ToString(CultureInfo.InvariantCulture), S.Owner, S.ZoneId,
				S.X.ToString(CultureInfo.InvariantCulture), S.Y.ToString(CultureInfo.InvariantCulture),
				S.RealmId, S.SettlementId, S.FactionId, S.Property.PriorOwner ?? "",
				S.Property.ReceiptVersion.ToString(CultureInfo.InvariantCulture),
				((int)S.Property.Phase).ToString(CultureInfo.InvariantCulture),
				S.Property.DesignatedTick.ToString(CultureInfo.InvariantCulture),
				S.Property.ReleasedTick.ToString(CultureInfo.InvariantCulture),
				S.Property.Fault ?? "", "ground", "count-1");
		}

		private static bool Nearby(GameObject A, GameObject B)
		{
			return GameObject.Validate(A) && A.CurrentCell != null && B?.CurrentCell != null &&
				Math.Abs(A.CurrentCell.X - B.CurrentCell.X) <= 1 &&
				Math.Abs(A.CurrentCell.Y - B.CurrentCell.Y) <= 1;
		}

		private static bool UniqueRoot(Zone Zone, GameObject Exact, string Id)
		{
			return TryFindExactRoot(Zone, Id, out GameObject found) &&
				ReferenceEquals(found, Exact);
		}

		internal static bool TryFindExactRoot(Zone Zone, string Id, out GameObject Exact)
		{
			Exact = null;
			List<GameObject> roots = Zone?.GetObjects();
			if (roots == null || roots.Count > MaxGroundRoots || string.IsNullOrEmpty(Id))
				return false;
			for (int i = 0; i < roots.Count; i++)
				if (GameObject.Validate(roots[i]) && roots[i].IDIfAssigned == Id)
				{
					if (GameObject.Validate(Exact)) return false; Exact = roots[i];
				}
			GameObject found = GameObject.FindByID(Id);
			return GameObject.Validate(Exact) &&
				(!GameObject.Validate(found) || ReferenceEquals(found, Exact));
		}

		private static KingdomPolityIncidentRecord FindPlan(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; L != null && i < L.Incidents.Count; i++)
				if (L.Incidents[i].IncidentPlanId == Id) return L.Incidents[i];
			return null;
		}

		private static bool EscrowFail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
