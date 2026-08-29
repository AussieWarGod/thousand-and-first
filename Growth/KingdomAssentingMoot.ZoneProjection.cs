using System;
using XRL;
using XRL.World;
using XRL.World.Effects;
using XRL.World.ZoneParts;

namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMoot
	{
		internal static bool EnsureZoneProjection(Zone Zone, GameObject Building,
			KingdomAssentingMootReceipt AppliedReceipt, out string Failure)
		{
			Failure = null;
			if (Zone == null || Building == null || AppliedReceipt == null
				|| AppliedReceipt.Phase != KingdomAssentingMootPhase.Applied)
				return Fail("No applied exact ward projection was prepared.", out Failure);
			KingdomAssentingWardAuthority marker =
				Zone.GetPart<KingdomAssentingWardAuthority>();
			AmbientStabilization anyNative = Zone.GetPart<AmbientStabilization>();
			AmbientStabilization native = OwnedNative(Zone, marker);
			if (HasOtherAuthority(Zone, marker))
				return Fail("Different assenting-moot ward markers occupy this zone.", out Failure);
			if (HasOtherNative(Zone, native))
				return Fail("Ambient stabilization owned elsewhere also occupies this zone.", out Failure);
			if (marker == null && anyNative != null)
				return Fail("This zone already has ambient stabilization owned elsewhere.", out Failure);
			if (marker != null && !marker.Matches(AppliedReceipt))
				return Fail("A different assenting-moot ward authority occupies this zone.", out Failure);
			if (marker != null && native == null && anyNative != null)
				return Fail("The ward marker is separated from its exact native zone part.", out Failure);
			if (marker == null && PlayerHasConflictingStabilization(Zone, Building))
				return Fail("Existing astral friction must diffuse before this ward can answer.",
					out Failure);
			if (marker != null && native != null
				&& !EffectsClaimable(Zone, Building, native.Strength))
				return Fail("A stabilization effect owned elsewhere answers in this zone.",
					out Failure);
			try
			{
				if (marker != null && native == null)
				{
					Zone.RemovePart(marker);
					marker = null;
				}
				if (marker == null)
				{
					marker = new KingdomAssentingWardAuthority();
					marker.Stamp(AppliedReceipt);
					Zone.AddPart(marker);
				}
				if (native == null)
				{
					native = new AmbientStabilization
					{
						Strength = AppliedReceipt.Strength
					};
					Zone.AddPart(native);
				}
				native.Strength = AppliedReceipt.Strength;
				marker.Stamp(AppliedReceipt);
				if (!StampEffectOwner(Zone, Building, AppliedReceipt.Strength))
					return Fail("Native stabilization effects could not prove exact ownership.",
						out Failure);
			}
			catch (Exception ex)
			{
				Failure = "Native ambient ward projection threw " + ex.GetType().Name + ".";
				return false;
			}
			return marker.Matches(AppliedReceipt)
				&& ReferenceEquals(OwnedNative(Zone, marker), native)
				&& native.Strength == AppliedReceipt.Strength
				|| Fail("Native ambient ward projection did not match its receipt.", out Failure);
		}

		internal static bool RemoveZoneProjection(Zone Zone,
			KingdomAssentingMootReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (Zone == null) return true;
			KingdomAssentingWardAuthority marker =
				Zone.GetPart<KingdomAssentingWardAuthority>();
			if (marker == null) return true;
			if (Receipt != null && !marker.Matches(Receipt))
				return Fail("Ward cleanup found a different exact authority.", out Failure);
			try
			{
				AmbientStabilization native = OwnedNative(Zone, marker);
				if (native != null) Zone.RemovePart(native);
				RemoveOwnedEffects(Zone, marker.BuildingObjectId);
				Zone.RemovePart(marker);
			}
			catch (Exception ex)
			{
				Failure = "Native ambient ward cleanup threw " + ex.GetType().Name + ".";
				return false;
			}
			return Zone.GetPart<KingdomAssentingWardAuthority>() == null
				|| Fail("Native ambient ward parts remained after exact cleanup.", out Failure);
		}

		private static AmbientStabilization OwnedNative(Zone Zone,
			KingdomAssentingWardAuthority Marker)
		{
			if (Zone?.Parts == null || Marker == null) return null;
			int at = Zone.Parts.IndexOf(Marker);
			if (at < 0 || at + 1 >= Zone.Parts.Count) return null;
			return Zone.Parts[at + 1] as AmbientStabilization;
		}

		private static bool HasOtherAuthority(Zone Zone,
			KingdomAssentingWardAuthority Authority)
		{
			if (Zone?.Parts == null) return false;
			for (int i = 0; i < Zone.Parts.Count; i++)
				if (Zone.Parts[i] is KingdomAssentingWardAuthority marker
					&& !ReferenceEquals(marker, Authority)) return true;
			return false;
		}

		private static bool HasOtherNative(Zone Zone, AmbientStabilization Native)
		{
			if (Zone?.Parts == null) return false;
			for (int i = 0; i < Zone.Parts.Count; i++)
				if (Zone.Parts[i] is AmbientStabilization part
					&& !ReferenceEquals(part, Native)) return true;
			return false;
		}

		private static bool PlayerHasConflictingStabilization(Zone Zone, GameObject Building)
		{
			GameObject player = The.Player;
			if (player == null || !ReferenceEquals(player.CurrentZone, Zone)) return false;
			AmbientRealityStabilized ambient = player.GetEffect<AmbientRealityStabilized>();
			if (ambient != null && !OwnerIs(ambient.Owner, Building.IDIfAssigned)) return true;
			RealityStabilized reality = player.GetEffect<RealityStabilized>();
			return reality != null && !OwnerIs(reality.Owner, Building.IDIfAssigned);
		}

		private static bool EffectsClaimable(Zone Zone, GameObject Building, int Strength)
		{
			GameObject player = The.Player;
			if (player == null || !ReferenceEquals(player.CurrentZone, Zone)) return true;
			AmbientRealityStabilized ambient = player.GetEffect<AmbientRealityStabilized>();
			RealityStabilized reality = player.GetEffect<RealityStabilized>();
			bool ambientOwned = ambient == null || OwnerIs(ambient.Owner, Building.IDIfAssigned);
			if (!ambientOwned && (ambient.Owner != null || ambient.Strength != Strength)) return false;
			if (reality == null || OwnerIs(reality.Owner, Building.IDIfAssigned)) return true;
			return reality.Owner == null && ambient != null && reality.Strength == Strength;
		}

		private static bool StampEffectOwner(Zone Zone, GameObject Building, int Strength)
		{
			if (!EffectsClaimable(Zone, Building, Strength)) return false;
			GameObject player = The.Player;
			if (player == null || !ReferenceEquals(player.CurrentZone, Zone)) return true;
			AmbientRealityStabilized ambient = player.GetEffect<AmbientRealityStabilized>();
			RealityStabilized reality = player.GetEffect<RealityStabilized>();
			if (ambient != null) { ambient.Owner = Building; ambient.Strength = Strength; }
			if (reality != null) { reality.Owner = Building; reality.Strength = Strength; }
			return true;
		}

		private static void RemoveOwnedEffects(Zone Zone, string BuildingId)
		{
			GameObject player = The.Player;
			if (player == null || !ReferenceEquals(player.CurrentZone, Zone)) return;
			AmbientRealityStabilized ambient = player.GetEffect<AmbientRealityStabilized>();
			if (ambient != null && OwnerIs(ambient.Owner, BuildingId)) player.RemoveEffect(ambient);
			RealityStabilized reality = player.GetEffect<RealityStabilized>();
			if (reality != null && OwnerIs(reality.Owner, BuildingId)) player.RemoveEffect(reality);
		}

		private static bool OwnerIs(GameObject Owner, string Id)
		{
			return Owner != null && !string.IsNullOrEmpty(Id)
				&& string.Equals(Owner.IDIfAssigned, Id, StringComparison.Ordinal);
		}
	}
}
