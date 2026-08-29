using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomOfficeRuntime
	{
		/// <summary>Read-only mod-removal preflight for one exact title projection.</summary>
		internal static bool CanRemoveForRealmRemoval(GameObject Body,
			r_KingdomOfficeProjection Marker, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Body) || Marker == null
				|| Body.GetPart<r_KingdomOfficeProjection>() != Marker
				|| Body.GetPart<r_KingdomRemembranceProjection>() != null
				|| string.IsNullOrEmpty(Body.IDIfAssigned)
				|| Marker.BodyObjectId != Body.IDIfAssigned
				|| string.IsNullOrEmpty(Marker.RoleText)
				|| !HasRole(Body.GetPart<SocialRoles>(), Marker.RoleText))
			{
				Failure = "Civic-office projection identity is divergent."; return false;
			}
			return true;
		}

		/// <summary>Removes only the exact role owned by this marker, then the marker itself.
		/// Foreign pre-existing same-text roles have OwnsRole=false and are preserved.</summary>
		internal static bool TryRemoveForRealmRemoval(GameObject Body,
			r_KingdomOfficeProjection Marker, out string Failure)
		{
			if (!CanRemoveForRealmRemoval(Body, Marker, out Failure)) return false;
			SocialRoles roles = Body.GetPart<SocialRoles>();
			if (Marker.OwnsRole && HasRole(roles, Marker.RoleText))
				roles.RemoveRole(Marker.RoleText);
			if (Marker.OwnsRole && HasRole(Body.GetPart<SocialRoles>(), Marker.RoleText))
			{
				Failure = "Civic-office owned title resisted exact removal."; return false;
			}
			Body.RemovePart(Marker); return true;
		}
	}
}
