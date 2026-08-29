using System;
using System.Collections.Generic;
using System.Globalization;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityNpcRuntime
	{
		private static bool ApplyGear(GameObject Body, KingdomPolityNpcSpec S, string RealmId,
			string CohortId, string ProjectionId, string BodyId, out string Failure)
		{
			Failure = null; string profile = ProfileRef(S.ProfileId, S.ProfileRevision);
			for (int i = 0; i < S.GearBlueprints.Count; i++)
			{
				string blueprint = S.GearBlueprints[i];
				string objectId = KingdomPolityPhysicalCustodyRules.GearObjectId(RealmId,
					CohortId, ProjectionId, BodyId, S.Ordinal, i, profile,
					S.ResolverDigest, blueprint);
				if (!TryFindResidentGear(objectId, out GameObject collision, out Failure)) return false;
				if (GameObject.Validate(collision)) return KingdomPolityRules.Fail(
					"prepared polity gear id already resolves to another object", out Failure);
				GameObject item = GameObject.Create(blueprint);
				if (!GameObject.Validate(item) || item.Blueprint != blueprint || item.Count != 1 ||
					item.IsNatural() || item.Physics == null)
				{
					QuarantineUnboundGeneratedObject(Body, item); return KingdomPolityRules.Fail(
						"regenerated gear blueprint was not one exact whole object", out Failure);
				}
				item.ID = objectId;
				StampGear(item, RealmId, CohortId, ProjectionId, BodyId, S.Ordinal, i,
					profile, S.ResolverDigest, blueprint);
				Commerce commerce = item.GetPart<Commerce>();
				if (commerce != null) commerce.Value = 0.0;
				bool accepted = false;
				try
				{
					accepted = Body.ReceiveObject(item, NoStack: true,
						Context: "Polity regeneration");
				}
				catch (Exception ex)
				{
					Failure = "regenerated gear receipt callback failed: " + ex.Message;
				}
				if (!TryFindResidentGear(objectId, out GameObject receivedResident, out string lookupFailure))
				{
					QuarantineFailedGear(Body, item); return KingdomPolityRules.Fail(
						lookupFailure, out Failure);
				}
				bool exactReceived = accepted && GameObject.Validate(item) &&
					ReferenceEquals(item.InInventory, Body) && item.Equipped == null &&
					item.CurrentCell == null && item.Count == 1 && item.Blueprint == blueprint &&
					item.IDIfAssigned == objectId && ReferenceEquals(receivedResident, item);
				if (!exactReceived)
				{
					QuarantineFailedGear(Body, item);
					return KingdomPolityRules.Fail(Failure ??
						"regenerated gear callback did not leave exact direct custody", out Failure);
				}
				item.Physics.Takeable = false;
				bool equipped;
				try { equipped = Body.AutoEquip(item, Silent: true); }
				catch (Exception ex)
				{
					QuarantineFailedGear(Body, item);
					return KingdomPolityRules.Fail(
						"regenerated gear equip callback failed: " + ex.Message, out Failure);
				}
				bool direct = ReferenceEquals(item.InInventory, Body) ||
					ReferenceEquals(item.Equipped, Body);
				if (!ExactGear(item, S, RealmId, CohortId, ProjectionId, BodyId, i, direct) ||
					(!equipped && !ReferenceEquals(item.InInventory, Body)))
				{
					QuarantineFailedGear(Body, item);
					return KingdomPolityRules.Fail(
						"regenerated gear left exact custody after equip callbacks", out Failure);
				}
			}
			return true;
		}

		internal static bool ExactGear(GameObject Item, KingdomPolityNpcSpec S,
			string RealmId, string CohortId, string ProjectionId, string BodyId, int GearOrdinal,
			bool ExactCustody, bool RequireResidentIndex = true)
		{
			if (S == null || GearOrdinal < 0 || GearOrdinal >= S.GearBlueprints.Count) return false;
			string blueprint = S.GearBlueprints[GearOrdinal];
			string profile = ProfileRef(S.ProfileId, S.ProfileRevision);
			string objectId = KingdomPolityPhysicalCustodyRules.GearObjectId(RealmId,
				CohortId, ProjectionId, BodyId, S.Ordinal, GearOrdinal, profile,
				S.ResolverDigest, blueprint);
			string receipt = KingdomPolityPhysicalCustodyRules.GearReceipt(RealmId,
				CohortId, ProjectionId, BodyId, S.Ordinal, GearOrdinal, profile,
				S.ResolverDigest, blueprint);
			Commerce commerce = Item == null ? null : Item.GetPart<Commerce>();
			GameObject resident = null;
			if (RequireResidentIndex && !TryFindResidentGear(objectId, out resident, out string _))
				return false;
			return KingdomPolityPhysicalCustodyRules.ExactGearBinding(RealmId, CohortId,
				ProjectionId, BodyId, S.Ordinal, GearOrdinal, profile, S.ResolverDigest,
				blueprint, objectId, receipt, Item?.GetStringProperty(GearRealmProperty),
				Item?.GetStringProperty(GearCohortProperty),
				Item?.GetStringProperty(GearProjectionProperty),
				Item?.GetStringProperty(GearBodyProperty),
				Item?.GetIntProperty(GearMemberOrdinalProperty, -1) ?? -1,
				Item?.GetIntProperty(GearOrdinalProperty, -1) ?? -1,
				Item?.GetStringProperty(GearProfileProperty),
				Item?.GetStringProperty(GearOwnerProperty), Item?.Blueprint,
				Item?.IDIfAssigned, Item?.GetStringProperty(GearReceiptProperty),
					GameObject.Validate(Item) && (!RequireResidentIndex ||
						ReferenceEquals(resident, Item)),
				Item?.IsNatural() ?? false, Item?.Count == 1,
				commerce == null || commerce.Value == 0.0,
				Item?.Physics != null && !Item.Physics.Takeable, ExactCustody);
		}

		internal static bool HasAnyGearMark(GameObject Item)
		{
			return Item != null && (!string.IsNullOrEmpty(Item.GetStringProperty(GearOwnerProperty)) ||
				!string.IsNullOrEmpty(Item.GetStringProperty(GearReceiptProperty)) ||
				!string.IsNullOrEmpty(Item.GetStringProperty(GearRealmProperty)) ||
				!string.IsNullOrEmpty(Item.GetStringProperty(GearCohortProperty)) ||
				!string.IsNullOrEmpty(Item.GetStringProperty(GearProjectionProperty)) ||
				!string.IsNullOrEmpty(Item.GetStringProperty(GearBodyProperty)) ||
				!string.IsNullOrEmpty(Item.GetStringProperty(GearProfileProperty)) ||
				Item.GetIntProperty(GearMemberOrdinalProperty, -1) >= 0 ||
				Item.GetIntProperty(GearOrdinalProperty, -1) >= 0);
		}

		private static void StampGear(GameObject Item, string RealmId, string CohortId,
			string ProjectionId, string BodyId, int MemberOrdinal, int GearOrdinal,
			string Profile, string Resolver, string Blueprint)
		{
			Item.SetStringProperty(GearOwnerProperty, Resolver);
			Item.SetStringProperty(GearReceiptProperty,
				KingdomPolityPhysicalCustodyRules.GearReceipt(RealmId, CohortId, ProjectionId,
					BodyId, MemberOrdinal, GearOrdinal, Profile, Resolver, Blueprint));
			Item.SetStringProperty(GearRealmProperty, RealmId);
			Item.SetStringProperty(GearCohortProperty, CohortId);
			Item.SetStringProperty(GearProjectionProperty, ProjectionId);
			Item.SetStringProperty(GearBodyProperty, BodyId);
			Item.SetStringProperty(GearProfileProperty, Profile);
			Item.SetIntProperty(GearMemberOrdinalProperty, MemberOrdinal);
			Item.SetIntProperty(GearOrdinalProperty, GearOrdinal);
		}

		private static string ProfileRef(string ProfileId, int Revision)
		{
			return ProfileId + ":" + Revision.ToString(CultureInfo.InvariantCulture);
		}

		private static bool CanDestroyFailedBody(GameObject Body)
		{
			if (!GameObject.Validate(Body) || Body.CurrentCell != null || Body.InInventory != null ||
				Body.Equipped != null) return false;
			List<GameObject> contents = Body.GetInventoryDirectAndEquipment();
			if (contents == null) return true;
			for (int i = 0; i < contents.Count; i++)
				if (GameObject.Validate(contents[i]) && (!contents[i].IsNatural() ||
					HasAnyGearMark(contents[i]))) return false;
			return true;
		}

		private static void QuarantineFailedGear(GameObject Body, GameObject Item)
		{
			if (GameObject.Validate(Body)) Body.SetIntProperty(ContestedProperty, 1);
			if (!GameObject.Validate(Item) || Item.CurrentCell != null || Item.InInventory != null ||
				Item.Equipped != null) return;
			// A callback-mutated object is foreign evidence. Pin the same reference to the
			// contested body without firing another receipt callback; it must never be cleaned.
			QuarantineUnboundGeneratedObject(Body, Item);
		}

		private static void QuarantineUnboundGeneratedObject(GameObject Body, GameObject Item)
		{
			if (GameObject.Validate(Body)) Body.SetIntProperty(ContestedProperty, 1);
			if (!GameObject.Validate(Item) || Item.CurrentCell != null || Item.InInventory != null ||
				Item.Equipped != null) return;
			if (!GameObject.Validate(Body) || Body.Inventory == null ||
				Body.Inventory.Objects.Contains(Item)) return;
			Body.Inventory.Objects.Add(Item);
			if (Item.Physics != null) Item.Physics.InInventory = Body;
		}

		private static bool TryFindResidentGear(string ObjectId, out GameObject Found,
			out string Failure)
		{
			Found = null; Failure = null;
			if (string.IsNullOrEmpty(ObjectId)) return KingdomPolityRules.Fail(
				"resident gear lookup lacks an exact id", out Failure);
			try
			{
				GameObject found = GameObject.FindByID(ObjectId);
				if (GameObject.Validate(found) && found.IDIfAssigned != ObjectId)
					return KingdomPolityRules.Fail(
						"resident gear index returned a foreign id", out Failure);
				Found = GameObject.Validate(found) ? found : null; return true;
			}
			catch (Exception ex)
			{
				return KingdomPolityRules.Fail("resident gear lookup failed: " + ex.Message,
					out Failure);
			}
		}
	}
}
