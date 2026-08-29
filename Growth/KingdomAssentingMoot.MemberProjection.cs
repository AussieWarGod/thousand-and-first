using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMoot
	{
		internal static bool EnsureMemberProjections(KingdomAssentingMootContext Context,
			KingdomAssentingMootReceipt Receipt, bool LoadZones, out string Failure)
		{
			Failure = null;
			Dictionary<int, GameObject> bodies = new Dictionary<int, GameObject>();
			Dictionary<int, int> roles = new Dictionary<int, int>();
			if (!CollectBodies(Context, Receipt, KingdomAssentingMootRole.Assent, 1,
				LoadZones, bodies, roles) || !CollectBodies(Context, Receipt,
					KingdomAssentingMootRole.Exemption, 2, LoadZones, bodies, roles))
				return Fail("A named moot member resolves to conflicting exact bodies.", out Failure);
			foreach (KeyValuePair<int, GameObject> pair in bodies)
			{
				GameObject body = pair.Value;
				r_KingdomAssentingMootMember marker =
					body.GetPart<r_KingdomAssentingMootMember>();
				if (marker != null && !MarkerMatches(marker, Receipt, pair.Key, body))
					return Fail("A different moot membership already marks an exact body.", out Failure);
				if (marker == null)
				{
					marker = new r_KingdomAssentingMootMember();
					body.AddPart(marker);
				}
				marker.Stamp(Receipt, pair.Key, body.IDIfAssigned, roles[pair.Key]);
			}
			return true;
		}

		private static bool CollectBodies(KingdomAssentingMootContext Context,
			KingdomAssentingMootReceipt Receipt, KingdomAssentingMootRole Role, int Bit,
			bool LoadZones, Dictionary<int, GameObject> Bodies, Dictionary<int, int> Roles)
		{
			List<int> ids = Role == KingdomAssentingMootRole.Assent
				? Receipt.AssentResidentIds : Receipt.ExemptResidentIds;
			for (int i = 0; i < ids.Count; i++)
			{
				GameObject body;
				if (!TryMemberBody(Context, Receipt, Role, i, LoadZones, out body)) continue;
				if (Bodies.TryGetValue(ids[i], out GameObject existing)
					&& !ReferenceEquals(existing, body)) return false;
				Bodies[ids[i]] = body;
				Roles[ids[i]] = (Roles.TryGetValue(ids[i], out int prior) ? prior : 0) | Bit;
			}
			return true;
		}

		internal static void RemoveMemberProjections(KingdomAssentingMootReceipt Receipt)
		{
			if (Receipt == null) return;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			AddBodyIds(ids, Receipt.AssentBodyObjectIds);
			AddBodyIds(ids, Receipt.ExemptBodyObjectIds);
			foreach (string id in ids)
			{
				GameObject body = GameObject.FindByID(id);
				r_KingdomAssentingMootMember marker =
					body?.GetPart<r_KingdomAssentingMootMember>();
				if (marker != null && MarkerAuthorityMatches(marker, Receipt, body))
					body.RemovePart(marker);
			}
		}

		internal static void PruneLoadedMemberProjections(KingdomSystem System, Zone Zone)
		{
			if (System == null || Zone == null) return;
			List<GameObject> objects = Zone.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject body = objects[i];
				r_KingdomAssentingMootMember marker =
					body?.GetPart<r_KingdomAssentingMootMember>();
				if (marker == null) continue;
				if (!TryBook(System, marker.SettlementId, out Simulation.City.KingdomCityBook book,
					out bool owned) || !owned)
				{
					body.RemovePart(marker);
					continue;
				}
				book.Normalize();
				KingdomAssentingMootReceipt receipt = book.AssentingMoot;
				string failure;
				if (!KingdomAssentingMootRules.Validate(receipt, out failure)
					|| (receipt.Phase != KingdomAssentingMootPhase.Applied
						&& receipt.Phase != KingdomAssentingMootPhase.Prepared)
					|| !MarkerAuthorityMatches(marker, receipt, body)
					|| ExpectedRoles(receipt, marker.ResidentId, body.IDIfAssigned) != marker.Roles)
					body.RemovePart(marker);
			}
		}

		private static int ExpectedRoles(KingdomAssentingMootReceipt Receipt,
			int ResidentId, string BodyId)
		{
			int roles = ExactRole(Receipt.AssentResidentIds, Receipt.AssentBodyObjectIds,
				ResidentId, BodyId) ? 1 : 0;
			if (ExactRole(Receipt.ExemptResidentIds, Receipt.ExemptBodyObjectIds,
				ResidentId, BodyId)) roles |= 2;
			return roles;
		}

		private static bool ExactRole(List<int> ResidentIds, List<string> BodyIds,
			int ResidentId, string BodyId)
		{
			int at = ResidentIds?.BinarySearch(ResidentId) ?? -1;
			return at >= 0 && BodyIds != null && at < BodyIds.Count
				&& string.Equals(BodyIds[at], BodyId, StringComparison.Ordinal);
		}

		private static void AddBodyIds(HashSet<string> Into, List<string> Values)
		{
			if (Values == null) return;
			for (int i = 0; i < Values.Count; i++)
				if (!string.IsNullOrEmpty(Values[i])) Into.Add(Values[i]);
		}

		internal static bool MarkerMatches(r_KingdomAssentingMootMember Marker,
			KingdomAssentingMootReceipt Receipt, int ResidentId, GameObject Body)
		{
			return Marker != null && MarkerAuthorityMatches(Marker, Receipt, Body)
				&& Marker.ResidentId == ResidentId;
		}

		internal static bool MarkerAuthorityMatches(r_KingdomAssentingMootMember Marker,
			KingdomAssentingMootReceipt Receipt, GameObject Body)
		{
			return Marker != null && Receipt != null && Body != null
				&& Marker.Version == Receipt.Version && Marker.Generation == Receipt.Generation
				&& string.Equals(Marker.RealmId, Receipt.RealmId, StringComparison.Ordinal)
				&& string.Equals(Marker.SettlementId, Receipt.SettlementId,
					StringComparison.Ordinal)
				&& string.Equals(Marker.AuthorityId, Receipt.AuthorityId,
					StringComparison.Ordinal)
				&& string.Equals(Marker.BuildingObjectId, Receipt.BuildingObjectId,
					StringComparison.Ordinal)
				&& string.Equals(Marker.BodyObjectId, Body.IDIfAssigned,
					StringComparison.Ordinal);
		}
	}
}
