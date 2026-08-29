using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomRemembranceRuntime
	{
		private static bool EnsureProjection(KingdomSystem System, CityContext Context,
			KingdomRemembranceReceipt Receipt, GameObject Carrier, DeathChoice Subject,
			out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Carrier) || !IsFixture(Carrier)
				|| Carrier.IDIfAssigned != Receipt.CarrierObjectId
				|| Carrier.CurrentZone?.ZoneID != Receipt.CarrierZoneId
				|| Subject == null || Subject.Row.ResidentId != Receipt.SubjectResidentId
				|| Subject.Row.Name != Receipt.SubjectName)
			{
				Failure = "The exact remembrance carrier or terminal row changed."; return false;
			}
			Description description = Carrier.GetPart<Description>();
			if (description == null)
			{
				Failure = "The exact remembrance carrier has no native description."; return false;
			}
			r_KingdomRemembranceProjection marker =
				Carrier.GetPart<r_KingdomRemembranceProjection>();
			if (marker != null && !marker.MatchesAuthority(System, Receipt, Carrier))
			{
				Failure = "Another remembrance projection marks the exact carrier."; return false;
			}
			if (marker == null)
			{
				if (Receipt.Phase != KingdomRemembrancePhase.ProjectionPrepared
					|| !string.IsNullOrEmpty(Carrier.GetStringProperty(MemorialForProperty)))
				{
					Failure = "The remembrance carrier lacks its exact projection proof."; return false;
				}
				marker = NewMarker(System, Receipt, Carrier, description, Subject);
				Carrier.AddPart(marker);
			}
			if (!KnownProjectionState(Carrier, description, marker))
			{
				Failure = "Foreign changes occupy a remembrance-owned display field."; return false;
			}
			try
			{
				Carrier.DisplayName = marker.ProjectedDisplayName;
				description.Short = marker.ProjectedDescription;
				Carrier.SetStringProperty(MemorialForProperty, marker.ProjectedMemorialFor);
			}
			catch (Exception ex)
			{
				Failure = "Remembrance projection threw " + ex.GetType().Name + "."; return false;
			}
			if (!ProjectedState(Carrier, description, marker))
			{
				Failure = "The remembrance carrier did not match its exact projection."; return false;
			}
			return true;
		}

		private static r_KingdomRemembranceProjection NewMarker(KingdomSystem System,
			KingdomRemembranceReceipt Receipt, GameObject Carrier, Description Description,
			DeathChoice Subject)
		{
			return new r_KingdomRemembranceProjection
			{
				RealmId = System.RealmId, SettlementId = Receipt.SettlementId,
				Generation = Receipt.Generation, SubjectResidentId = Receipt.SubjectResidentId,
				CarrierObjectId = Receipt.CarrierObjectId, CarrierZoneId = Receipt.CarrierZoneId,
				PriorDisplayName = Carrier.DisplayName, PriorDescription = Description.Short,
				HadMemorialProperty = Carrier.HasStringProperty(MemorialForProperty),
				PriorMemorialFor = Carrier.GetStringProperty(MemorialForProperty),
				ProjectedDisplayName = FixtureName(Carrier.Blueprint, Receipt.SubjectName),
				ProjectedDescription = KingdomOfficeRules.Epitaph(
					KingdomPresentation.Rich(Subject.Row.Name), Subject.Row.Origin,
					Subject.Row.Arrived, KingdomPresentation.Rich(Receipt.SettlementName),
					KingdomOfficeRules.CauseClause(Subject.Cause)),
				ProjectedMemorialFor = Receipt.SubjectName
			};
		}

		private static bool TryRestoreProjection(GameObject Carrier,
			r_KingdomRemembranceProjection Marker, out string Failure)
		{
			Failure = null;
			Description description = Carrier?.GetPart<Description>();
			if (!GameObject.Validate(Carrier) || description == null
				|| !MarkerMatchesCarrier(Marker, Carrier)
				|| !KnownProjectionState(Carrier, description, Marker))
			{
				Failure = "A remembrance marker cannot restore foreign display changes."; return false;
			}
			try
			{
				Carrier.DisplayName = Marker.PriorDisplayName;
				description.Short = Marker.PriorDescription;
				if (Marker.HadMemorialProperty)
					Carrier.SetStringProperty(MemorialForProperty, Marker.PriorMemorialFor);
				else Carrier.RemoveStringProperty(MemorialForProperty);
				Carrier.RemovePart(Marker);
			}
			catch (Exception ex)
			{
				Failure = "Remembrance restoration threw " + ex.GetType().Name + "."; return false;
			}
			return Carrier.GetPart<r_KingdomRemembranceProjection>() == null
				&& PriorState(Carrier, description, Marker);
		}

		private static bool KnownProjectionState(GameObject Carrier, Description Description,
			r_KingdomRemembranceProjection Marker)
		{
			return MarkerMatchesCarrier(Marker, Carrier)
				&& (PriorState(Carrier, Description, Marker)
					|| ProjectedState(Carrier, Description, Marker));
		}

		private static bool MarkerMatchesCarrier(r_KingdomRemembranceProjection Marker,
			GameObject Carrier)
		{
			return Marker != null && GameObject.Validate(Carrier)
				&& !string.IsNullOrEmpty(Marker.RealmId)
				&& !string.IsNullOrEmpty(Marker.SettlementId) && Marker.Generation > 0
				&& Marker.SubjectResidentId > 0
				&& !string.IsNullOrEmpty(Marker.CarrierObjectId)
				&& Marker.CarrierObjectId == Carrier.IDIfAssigned
				&& !string.IsNullOrEmpty(Marker.CarrierZoneId)
				&& Marker.CarrierZoneId == Carrier.CurrentZone?.ZoneID;
		}

		private static bool PriorState(GameObject Carrier, Description Description,
			r_KingdomRemembranceProjection Marker)
		{
			if (Carrier == null || Description == null || Marker == null
				|| Carrier.DisplayName != Marker.PriorDisplayName
				|| Description.Short != Marker.PriorDescription) return false;
			bool has = Carrier.HasStringProperty(MemorialForProperty);
			string value = Carrier.GetStringProperty(MemorialForProperty);
			return Marker.HadMemorialProperty ? has && value == Marker.PriorMemorialFor : !has;
		}

		private static bool ProjectedState(GameObject Carrier, Description Description,
			r_KingdomRemembranceProjection Marker)
		{
			return Carrier != null && Description != null && Marker != null
				&& Carrier.DisplayName == Marker.ProjectedDisplayName
				&& Description.Short == Marker.ProjectedDescription
				&& Carrier.HasStringProperty(MemorialForProperty)
				&& Carrier.GetStringProperty(MemorialForProperty) == Marker.ProjectedMemorialFor;
		}

		private static string FixtureName(string Blueprint, string Subject)
		{
			string kind = Blueprint == "r_KingdomGraveGrove" ? "grave-grove"
				: Blueprint == "r_KingdomNicheTomb" ? "niche tomb" : "cairn";
			return "the " + kind + " of " + KingdomPresentation.Rich(Subject);
		}
	}
}
