using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityEndpointRuntime
	{
		private static bool TryAdmit(KingdomSystem System, string CohortId, out Zone Zone,
			out KingdomPolityLedger Ledger, out KingdomPolityCohortPlan Cohort,
			out string Failure)
		{
			Zone = null;
			Ledger = System == null ? null : System.PolityLedger;
			Cohort = Ledger == null ? null : KingdomPolityAuthority.Cohort(Ledger, CohortId);
			Failure = null;
			if (System == null || !System.Founded || Ledger == null || Cohort == null ||
				Cohort.ScaleBudget < 1 ||
				Cohort.ScaleBudget > KingdomPolityCohortRules.MaximumVisibleMembers ||
				!KingdomPolityRules.TryValidate(Ledger, out Failure))
			{
				Failure = Failure ?? "player is not at the exact active claimed cohort endpoint";
				return false;
			}
			if (!KingdomPolityLoadedEndpointRuntime.TryObserve(System, out Zone,
				out string loadedSettlementId, out bool available, out Failure) || !available ||
				!string.Equals(loadedSettlementId, Cohort.SurfaceRef, StringComparison.Ordinal))
			{
				Failure = Failure ?? "player is not at the exact active claimed cohort endpoint";
				return false;
			}
			return TryBindExactLegacyOwners(Zone, Ledger, Cohort, out Failure);
		}

		private static bool ExactReceipt(KingdomPolityCohortPlan Cohort,
			KingdomPolityProjectionReceipt Receipt, Zone Zone, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityCohortRules.ExactEndpointReceipt(Cohort, Receipt, Zone.ZoneID) ||
				Receipt.Kind != KingdomPolityProjectionKind.CohortManifestation ||
				Receipt.SourceRef != Cohort.CohortId ||
				Receipt.ObjectIds.Count != Cohort.ResolvedMembers.Count)
			{
				Failure = "cohort projection is missing, foreign, or in another zone"; return false;
			}
			for (int i = 0; i < Cohort.ResolvedMembers.Count; i++)
				if (!KingdomPolityAuthority.Contains(Receipt.ObjectIds,
					KingdomPolityCohortRules.PreparedObjectId(Cohort, i)))
				{
					Failure = "cohort projection does not own its exact prepared object ids"; return false;
				}
			return true;
		}

		private static bool TryObserve(Zone Zone, string RealmId, KingdomPolityCohortPlan Cohort,
			KingdomPolityProjectionReceipt Receipt, out GameObject[] Observed,
			out string Failure)
		{
			Observed = new GameObject[Cohort.ResolvedMembers.Count]; Failure = null;
			List<GameObject> objects = Zone.GetObjects();
			for (int i = 0; objects != null && i < objects.Count; i++)
			{
				GameObject body = objects[i]; if (!GameObject.Validate(body)) continue;
				bool marked = body.GetStringProperty(ProjectionProperty) == Receipt.ProjectionId;
				string bodyId = body.IDIfAssigned;
				bool expectedId = KingdomPolityAuthority.Contains(Receipt.ObjectIds, bodyId);
				if (!expectedId && !marked) continue;
				int ordinal = body.GetIntProperty(MemberOrdinalProperty, -1);
				XRL.World.Parts.r_KingdomPolityCohortBody part =
					body.GetPart<XRL.World.Parts.r_KingdomPolityCohortBody>();
				if (!expectedId || !marked || ordinal < 0 || ordinal >= Observed.Length ||
					bodyId != KingdomPolityCohortRules.PreparedObjectId(Cohort, ordinal) ||
					body.GetStringProperty(CohortOwnerProperty) != Cohort.PolityId ||
					body.GetStringProperty(CohortProperty) != Cohort.CohortId ||
					part == null || part.Inert || part.RealmId != RealmId ||
					part.CohortId != Cohort.CohortId || part.Purpose != Cohort.Purpose ||
					part.Representative != (ordinal == 0) ||
					body.CurrentCell == null || !ReferenceEquals(body.CurrentZone, Zone) ||
					body.GetIntProperty(CohortXProperty, -1) != body.CurrentCell.X ||
					body.GetIntProperty(CohortYProperty, -1) != body.CurrentCell.Y ||
					GameObject.Validate(Observed[ordinal]))
				{
					Failure = "prepared cohort object id or ownership marker is ambiguous"; return false;
				}
				Observed[ordinal] = body;
			}
			for (int i = 0; i < Observed.Length; i++)
			{
				string expected = KingdomPolityCohortRules.PreparedObjectId(Cohort, i);
				if (!TryFindResidentObject(expected, out GameObject indexed, out Failure)) return false;
				if (GameObject.Validate(indexed))
				{
					if (!ReferenceEquals(indexed, Observed[i]) || indexed.IDIfAssigned != expected ||
						indexed.CurrentCell == null || !ReferenceEquals(indexed.CurrentZone, Zone))
					{
						Failure = "prepared cohort body is moved or its global id is ambiguous";
						return false;
					}
				}
				else if (GameObject.Validate(Observed[i]))
				{
					Failure = "prepared cohort body is absent from the exact global id index";
					return false;
				}
			}
			return true;
		}

		private static bool TryBindExactLegacyOwners(Zone Zone, KingdomPolityLedger Ledger,
			KingdomPolityCohortPlan Cohort, out string Failure)
		{
			Failure = null;
			if (Zone == null || Ledger == null || Cohort == null ||
				!KingdomPolityRules.TypedId(Ledger.RealmId, "taf:realm:"))
			{
				Failure = "cohort owner binding lacks exact realm authority"; return false;
			}
			KingdomPolityProjectionReceipt receipt = string.IsNullOrEmpty(
				Cohort.ManifestationReceiptId) ? null : KingdomPolityAuthority.Projection(
					Ledger, Cohort.ManifestationReceiptId);
			if (receipt == null) return true;
			if (!KingdomPolityCohortRules.ExactEndpointReceipt(Cohort, receipt, Zone.ZoneID) ||
				receipt.Kind != KingdomPolityProjectionKind.CohortManifestation ||
				receipt.SourceRef != Cohort.CohortId)
			{
				Failure = "legacy cohort owner binding lacks an exact endpoint receipt"; return false;
			}
			List<GameObject> objects = Zone.GetObjects();
			for (int i = 0; objects != null && i < objects.Count; i++)
			{
				GameObject body = objects[i];
				int ordinal = body == null ? -1 : body.GetIntProperty(MemberOrdinalProperty, -1);
				if (!GameObject.Validate(body) || ordinal < 0 || ordinal >=
					Cohort.ResolvedMembers.Count || body.IDIfAssigned !=
					KingdomPolityCohortRules.PreparedObjectId(Cohort, ordinal) ||
					!KingdomPolityAuthority.Contains(receipt.ObjectIds, body.IDIfAssigned) ||
					body.GetStringProperty(CohortProperty) != Cohort.CohortId ||
					body.GetStringProperty(CohortOwnerProperty) != Cohort.PolityId ||
					body.GetStringProperty(ProjectionProperty) != receipt.ProjectionId) continue;
				XRL.World.Parts.r_KingdomPolityCohortBody part =
					body.GetPart<XRL.World.Parts.r_KingdomPolityCohortBody>();
				if (part == null || part.Inert || part.CohortId != Cohort.CohortId ||
					part.Purpose != Cohort.Purpose || part.Representative != (ordinal == 0)) continue;
				if (part.RealmId != Ledger.RealmId)
				{
					Failure = "cohort body lacks exact frozen realm ownership"; return false;
				}
			}
			return true;
		}

		private static bool CausedConfrontation(KingdomPolityLedger L,
			KingdomPolityCohortPlan Cohort)
		{
			for (int i = 0; i < L.Incidents.Count; i++)
			{
				KingdomPolityIncidentRecord incident = L.Incidents[i];
				if (incident.Purpose != KingdomPolityCohortPurpose.Warband ||
					incident.Conclusion != null || !KingdomPolityAuthority.Contains(
						incident.ParticipantCohortRefs, Cohort.CohortId)) continue;
				for (int j = 0; j < L.Fronts.Count; j++)
				{
					KingdomPolityFrontRecord front = L.Fronts[j];
					if (front.Phase != KingdomPolityFrontPhase.ConfrontationAvailable) continue;
					if (front.TargetKind == KingdomPolityFrontTarget.Cohort &&
						front.TargetRef == Cohort.CohortId) return true;
					if (KingdomPolityAuthority.Contains(incident.DisclosedStakeRefs,
						front.TargetRef)) return true;
				}
			}
			return false;
		}
	}
}
