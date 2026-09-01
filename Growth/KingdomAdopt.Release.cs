using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomAdopt
	{
		/// <summary>
		/// Releases a structure or marker previously accepted by <see cref="AdoptExisting"/> or
		/// <see cref="AdoptWork"/>. Undoes exactly what adoption itself set &mdash; the built
		/// mark, the adoption mark, and (only if adoption itself was the one to set it)
		/// <see cref="StoresProperty"/> or <see cref="LarderProperty"/> &mdash; and destroys the
		/// object only when it was <see cref="WorkMarkerBlueprint"/>, which existed solely to
		/// carry the mark. Everything the founder actually built stands exactly where it stood.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone the object stands in; must be the kingdom's own claimed ground.</param>
		/// <param name="Adopted">The object to release.</param>
		/// <param name="Failure">Set to a player-facing reason when this returns false.</param>
		/// <returns>True once the object's civic standing has actually changed.</returns>
		public static bool Release(KingdomSystem System, Zone Z, GameObject Adopted, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A building is released on the kingdom's own ground, not in other people's houses.";
				return false;
			}
			if (Adopted == null || !GameObject.Validate(Adopted) || Adopted.CurrentZone == null || Adopted.CurrentZone.ZoneID != Z.ZoneID)
			{
				Failure = "There is nothing here to release.";
				return false;
			}
			if (Adopted.GetIntProperty(AdoptedProperty) != 1)
			{
				Failure = Adopted.ShortDisplayName + " was never adopted; there is nothing to release.";
				return false;
			}
			if (!KingdomDesignationReleaseAuthority.TryCanRelease(System, Z, Adopted,
				out Failure)) return false;
			string key = Adopted.GetStringProperty(AdoptedKeyProperty);
			string mark = Adopted.GetStringProperty(AdoptedMarkProperty);
			string name = Adopted.ShortDisplayName;
			// Plot receipt owns several properties. Retire it while AdoptedProperty still makes
			// an interrupted release retryable; only then publish the general adoption removal.
			KingdomAdoptionDesignation.Clear(Adopted);
			KingdomPlots.ReleaseAdoptedPlot(Adopted);
			if (mark == StoresProperty || mark == LarderProperty)
			{
				ClearTyped(Adopted, mark);
			}
			ClearTyped(Adopted, AdoptedKeyProperty);
			ClearTyped(Adopted, AdoptedMarkProperty);
			ClearTyped(Adopted, BuiltProperty);
			// Positive ownership marker last: any interrupted earlier phase remains retryable.
			ClearTyped(Adopted, AdoptedProperty);
			KingdomGovernanceScope.Commit("release adoption");
			bool wasMarker = Adopted.Blueprint == WorkMarkerBlueprint;
			if (wasMarker)
			{
				// The marker existed only to carry the mark; nothing else about it was ever the
				// founder's. Destroying it is the mirror of AdoptWork placing it, not a wound to
				// anything the founder made.
				Adopted.Destroy(null, Silent: true);
			}
			MessageQueue.AddPlayerMessage("{{K|" + name + " is released" + (wasMarker ? "." : " from " + KingdomPresentation.Rich(System.SeatName) + "'s standing.") + "}}"
				+ (wasMarker ? "" : " It stands exactly where it stood."));
			KingdomLog.Log("adopt: released " + name + " (" + key + ") at " + System.SeatName);
			return true;
		}
	}
}
