using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomRemembranceRuntime
	{
		/// <summary>Read-only mod-removal preflight. The marker may restore only fields still equal
		/// to its exact prior/projected snapshots; foreign edits fail closed.</summary>
		internal static bool CanRemoveForRealmRemoval(GameObject Carrier,
			r_KingdomRemembranceProjection Marker, out string Failure)
		{
			Failure = null;
			Description description = Carrier?.GetPart<Description>();
			if (!GameObject.Validate(Carrier) || Marker == null || description == null
				|| Carrier.GetPart<r_KingdomRemembranceProjection>() != Marker
				|| Carrier.GetPart<r_KingdomOfficeProjection>() != null
				|| string.IsNullOrEmpty(Carrier.IDIfAssigned)
				|| Marker.CarrierObjectId != Carrier.IDIfAssigned
				|| !KnownProjectionState(Carrier, description, Marker))
			{
				Failure = "Remembrance projection contains foreign or divergent display state.";
				return false;
			}
			return true;
		}

		internal static bool TryRemoveForRealmRemoval(GameObject Carrier,
			r_KingdomRemembranceProjection Marker, out string Failure)
		{
			if (!CanRemoveForRealmRemoval(Carrier, Marker, out Failure)) return false;
			return TryRestoreProjection(Carrier, Marker, out Failure);
		}
	}
}
