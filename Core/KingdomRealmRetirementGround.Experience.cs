using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomRealmRetirementGround
	{
		/// <summary>Checks reversible civic projections before any ground mutation. Generic part
		/// stripping is insufficient: office titles and memorial descriptions have exact prior
		/// values which only their owning receipts may restore.</summary>
		private static bool CanRemoveExperienceProjections(GameObject Item, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Item)) return true;
			r_KingdomOfficeProjection office = Item.GetPart<r_KingdomOfficeProjection>();
			if (office != null && !KingdomOfficeRuntime.CanRemoveForRealmRemoval(
				Item, office, out Failure)) return false;
			r_KingdomRemembranceProjection remembrance =
				Item.GetPart<r_KingdomRemembranceProjection>();
			return remembrance == null || KingdomRemembranceRuntime.CanRemoveForRealmRemoval(
				Item, remembrance, out Failure);
		}

		/// <summary>Restores only fields proven by the frozen preflight, before the fallback
		/// blueprint and generic namespaced carriers are removed.</summary>
		private static bool TryRemoveExperienceProjections(GameObject Item, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Item)) return true;
			r_KingdomOfficeProjection office = Item.GetPart<r_KingdomOfficeProjection>();
			if (office != null && !KingdomOfficeRuntime.TryRemoveForRealmRemoval(
				Item, office, out Failure)) return false;
			r_KingdomRemembranceProjection remembrance =
				Item.GetPart<r_KingdomRemembranceProjection>();
			return remembrance == null || KingdomRemembranceRuntime.TryRemoveForRealmRemoval(
				Item, remembrance, out Failure);
		}
	}
}
