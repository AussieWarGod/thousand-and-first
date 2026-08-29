using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlanMarker
	{
		/// <summary>Every physical plan marker in Z, oldest first, including blocked work.</summary>
		public static List<GameObject> FindPending(Zone Z)
		{
			List<GameObject> found = new List<GameObject>();
			if (Z == null) return found;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				r_KingdomPlanMarker marker = GameObject.Validate(item)
					? item.GetPart<r_KingdomPlanMarker>() : null;
				if (marker != null && item.CurrentZone == Z && item.CurrentCell != null)
					found.Add(item);
			}
			found.Sort(delegate(GameObject a, GameObject b)
			{
				r_KingdomPlanMarker markerA = a.GetPart<r_KingdomPlanMarker>();
				r_KingdomPlanMarker markerB = b.GetPart<r_KingdomPlanMarker>();
				return KingdomPlanRules.CompareOrder(
					new KingdomPendingPlan(markerA.PlacedTick, markerA.PlacedOrder, 0, false),
					new KingdomPendingPlan(markerB.PlacedTick, markerB.PlacedOrder, 0, false));
			});
			return found;
		}

		/// <summary>What Marker is staked to become, for a menu line or prompt.</summary>
		public static string Describe(GameObject Marker)
		{
			if (Marker == null) return "a plan";
			r_KingdomPlanMarker part = Marker.GetPart<r_KingdomPlanMarker>();
			if (part != null && KingdomData.TryGetBuilding(part.DesignKey, out var entry))
				return entry.Name;
			return Marker.ShortDisplayName ?? "a plan";
		}

		private static bool TryPrepareCancellation(KingdomSystem System, GameObject Marker,
			out KingdomPlanMarkerProof Proof, out string Failure)
		{
			Zone zone = GameObject.Validate(Marker) ? Marker.CurrentZone : null;
			if (!TryBuildProof(System, zone, Marker, out Proof, out Failure)) return false;
			if (Proof.ReceiptShape == KingdomPlanReceiptShape.Corrupt)
			{
				Failure = "The plan's construction receipt property is partial or has the wrong type.";
				return false;
			}
			if (!RegistryAllows(Proof, Proof.ReceiptShape == KingdomPlanReceiptShape.Exact,
				Proof.ReceiptId, out Failure)) return false;
			return true;
		}

		/// <summary>Read-only exact ownership, topology, and durable-registry preflight.</summary>
		public static bool CanCancel(KingdomSystem System, GameObject Marker, out string Failure)
		{
			return TryPrepareCancellation(System, Marker, out _, out Failure);
		}

		/// <summary>Compatibility preflight resolves only the current exact kingdom system.</summary>
		public static bool CanCancel(GameObject Marker, out string Failure)
		{
			return CanCancel(The.Game?.GetSystem<KingdomSystem>(), Marker, out Failure);
		}

		/// <summary>Calls off only a marker proved economically untouched.</summary>
		public static bool TryCancel(KingdomSystem System, GameObject Marker, out string Failure)
		{
			if (!TryPrepareCancellation(System, Marker, out KingdomPlanMarkerProof proof,
				out Failure)) return false;
			if (!TryPrepareCancellation(System, Marker, out KingdomPlanMarkerProof immediate,
				out Failure) || immediate.MarkerId != proof.MarkerId
				|| immediate.FrozenBytes != proof.FrozenBytes
				|| immediate.ReceiptShape != proof.ReceiptShape
				|| immediate.ReceiptId != proof.ReceiptId) return false;
			Exception callbackFailure = null;
			try
			{
				Marker.Destroy(null, Silent: true);
			}
			catch (Exception ex)
			{
				callbackFailure = ex;
			}
			finally
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(proof.Zone, Marker);
			}
			KingdomPhysicalLookupState idState = KingdomConstruction.FindExactId(
				proof.Zone, proof.MarkerId, out GameObject exact);
			bool registrySafe = RegistryAllows(proof,
				proof.ReceiptShape == KingdomPlanReceiptShape.Exact, proof.ReceiptId,
				out string registryFailure);
			bool authoritySafe = AuthorityStillExact(System, proof);
			if (KingdomConstructionRules.PlanMarkerCancellationRemovalProved(
				GameObject.Validate(Marker), idState, registrySafe, authoritySafe))
			{
				KingdomSurvey.ObserveRemovedFromActive(proof.Zone, Marker);
				if (callbackFailure != null)
					KingdomLog.Log("construction: plan cancellation callback threw after exact "
						+ "absence and clean registry were proved: " + callbackFailure.Message);
				Failure = null;
				return true;
			}
			bool survivorRegistrySafe = false;
			string survivorFailure = null;
			bool exactSurvivor = idState == KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(exact, Marker)
				&& ReproveSurvivor(System, Marker, proof,
					out survivorRegistrySafe, out survivorFailure);
			if (KingdomConstructionRules.PlanMarkerSurvivorProved(exactSurvivor,
				exactSurvivor, survivorRegistrySafe, authoritySafe))
			{
				Failure = callbackFailure == null
					? "The plan marker resisted removal and remains safely staked."
					: "Plan-marker removal stopped; the exact marker remains safely staked: "
						+ callbackFailure.Message;
				return false;
			}
			if (!GameObject.Validate(Marker) && idState == KingdomPhysicalLookupState.Absent)
			{
				Failure = "The stake is gone, but durable construction state no longer proves a clean cancellation. "
					+ (registryFailure ?? "No second removal will be attempted.");
				return false;
			}
			Failure = "Plan-marker removal left ambiguous identity, custody, authority, or registry topology."
				+ (survivorFailure == null ? "" : " " + survivorFailure);
			return false;
		}

		/// <summary>Compatibility command resolves only the current exact kingdom system.</summary>
		public static bool TryCancel(GameObject Marker, out string Failure)
		{
			return TryCancel(The.Game?.GetSystem<KingdomSystem>(), Marker, out Failure);
		}

		/// <summary>Compatibility entry point; refusal remains a no-op.</summary>
		public static void Cancel(GameObject Marker)
		{
			TryCancel(Marker, out _);
		}
	}
}
