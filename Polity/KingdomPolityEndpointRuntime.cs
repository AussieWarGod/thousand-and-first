using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Loaded-owned-endpoint adapter for finite parties; travel remains body-free.</summary>
	public static partial class KingdomPolityEndpointRuntime
	{
		internal const string CohortOwnerProperty = "r_TAF_PolityCohortOwner_v1";
		internal const string CohortProperty = "r_TAF_PolityCohort_v1";
		internal const string ProjectionProperty = "r_TAF_PolityCohortProjection_v1";
		internal const string MemberOrdinalProperty = "r_TAF_PolityCohortMember_v1";
		internal const string CohortXProperty = "r_TAF_PolityCohortX_v1";
		internal const string CohortYProperty = "r_TAF_PolityCohortY_v1";

		public static bool TryManifestCurrentEndpoint(KingdomSystem System, string CohortId,
			long Tick, out string Failure)
		{
			Failure = null; Zone zone; KingdomPolityLedger ledger; KingdomPolityCohortPlan cohort;
			if (!TryAdmit(System, CohortId, out zone, out ledger, out cohort, out Failure)) return false;
			bool ambient = cohort.PresentationOptionKind ==
				KingdomExperienceOptionKind.AmbientUse;
			if (!ambient && cohort.PresentationOptionKind !=
				KingdomExperienceOptionKind.CivicStory)
			{
				Failure = "cohort projection has no exact persisted presentation mode"; return false;
			}
			bool reserved = ambient
				? KingdomPolityExperienceRuntime.TryReserveAmbientProjection(System, cohort, Tick,
					out KingdomExperienceCapacityFault _, out Failure)
				: KingdomPolityExperienceRuntime.TryReserveDirectedProjection(System, cohort, Tick,
					out KingdomExperienceCapacityFault _, out Failure);
			if (!reserved) return false;
			KingdomPolityProjectionReceipt receipt = string.IsNullOrEmpty(
				cohort.ManifestationReceiptId) ? null : KingdomPolityAuthority.Projection(
				ledger, cohort.ManifestationReceiptId);
			if (receipt == null)
			{
				if (!KingdomPolityCohortRules.TryPrepareEndpointManifestation(ledger,
					ledger.Revision, cohort.CohortId, zone.ZoneID, Tick,
					out KingdomPolityPublicationResult _, out Failure)) return false;
				cohort = KingdomPolityAuthority.Cohort(ledger, CohortId);
				receipt = KingdomPolityAuthority.Projection(ledger, cohort.ManifestationReceiptId);
			}
			if (!ExactReceipt(cohort, receipt, zone, out Failure)) return false;
			long manifestationRevision = ledger.Revision;
			if (cohort.Purpose == KingdomPolityCohortPurpose.Warband &&
				!CausedConfrontation(ledger, cohort))
			{
				Failure = "warband has no caused witnessed confrontation at this endpoint"; return false;
			}
			GameObject[] observed;
			if (!TryObserve(zone, ledger.RealmId, cohort, receipt, out observed, out Failure)) return false;
			if (receipt.Phase == KingdomPolityProjectionPhase.Committed)
			{
				for (int i = 0; i < observed.Length; i++)
					if (!GameObject.Validate(observed[i]))
					{
						Failure = "committed endpoint body is missing; resurrection is forbidden";
						return false;
					}
				if ((cohort.Phase == KingdomPolityCohortPhase.Materialized ||
					cohort.Phase == KingdomPolityCohortPhase.Concluded) &&
					TryProveFrozenCohort(ledger, zone, ledger.RealmId, cohort, receipt,
						observed, AllowRemovedGear: false, out FrozenCustodyPlan[] _, out Failure))
					return true;
				if (Failure != null) return false;
				Failure = "committed endpoint projection has an incoherent cohort phase"; return false;
			}
			if (receipt.Phase != KingdomPolityProjectionPhase.Prepared ||
				cohort.Phase != KingdomPolityCohortPhase.Planned)
			{
				Failure = "endpoint manifestation is not recoverable from this phase"; return false;
			}
			KingdomPolityRecord polity = KingdomPolityAuthority.Polity(ledger, cohort.PolityId);
			KingdomPolityProfileRevision profile = KingdomPolityAuthority.Profile(ledger,
				cohort.ProfileId, cohort.ProfileRevision);
			if (polity == null || profile == null)
			{
				Failure = "endpoint cohort lost its pinned polity profile"; return false;
			}
			Cell[] placements;
			if (!TryPlanDistinctReachableCells(zone, observed, out placements, out Failure)) return false;
			for (int i = 0; i < observed.Length; i++)
			{
				if (GameObject.Validate(observed[i])) continue;
				if (!TryCreatePreparedMember(ledger, zone, placements[i], cohort, receipt, profile, polity,
					System.KingdomFactionName,
					i, out observed[i], out Failure)) return false;
			}
			if (!TryObserve(zone, ledger.RealmId, cohort, receipt, out observed, out Failure)) return false;
			for (int i = 0; i < observed.Length; i++)
				if (!GameObject.Validate(observed[i]))
				{
					Failure = "prepared endpoint cohort remains physically incomplete"; return false;
				}
			if (!TryProveFrozenCohort(ledger, zone, ledger.RealmId, cohort, receipt, observed,
				AllowRemovedGear: false, out FrozenCustodyPlan[] _, out Failure)) return false;
			return KingdomPolityCohortRules.TryCommitEndpointManifestation(ledger,
				manifestationRevision, cohort.CohortId, receipt.ProjectionId, receipt.ObjectIds, Tick,
				out KingdomPolityPublicationResult _, out Failure);
		}

		public static bool TryCleanupCurrentEndpoint(KingdomSystem System, string CohortId,
			out string Failure)
		{
			Failure = null; Zone zone; KingdomPolityLedger ledger; KingdomPolityCohortPlan cohort;
			if (!TryAdmit(System, CohortId, out zone, out ledger, out cohort, out Failure)) return false;
			if ((cohort.Phase == KingdomPolityCohortPhase.Concluded ||
				cohort.Phase == KingdomPolityCohortPhase.Abandoned) &&
				!TryReplayDeathIntents(System, CohortId, out Failure)) return false;
			cohort = KingdomPolityAuthority.Cohort(ledger, CohortId);
			KingdomPolityProjectionReceipt receipt = KingdomPolityAuthority.Projection(ledger,
				cohort.ManifestationReceiptId);
			if (!ExactReceipt(cohort, receipt, zone, out Failure)) return false;
			bool alreadyCleaned = (cohort.Phase == KingdomPolityCohortPhase.Cleaned ||
				cohort.Phase == KingdomPolityCohortPhase.Abandoned) &&
				receipt.Phase == KingdomPolityProjectionPhase.Cleaned;
			if (alreadyCleaned)
			{
				if (!TryClearCohortRemovalWitnesses(zone, ledger, cohort, receipt, out Failure))
					return false;
				if (!TryClearCohortDeathIntents(zone, ledger, cohort, receipt, out Failure))
					return false;
			}
			else
			{
				if ((cohort.Phase != KingdomPolityCohortPhase.Concluded &&
					cohort.Phase != KingdomPolityCohortPhase.Abandoned) ||
					receipt.Phase != KingdomPolityProjectionPhase.Committed)
				{
					Failure = "endpoint cohort has not concluded or abandoned for exact cleanup";
					return false;
				}
				long cleanupRevision = ledger.Revision;
				GameObject[] observed;
				if (!TryPromotePreparedCleanupIntents(zone, ledger.RealmId, cohort, receipt,
					out Failure) || !TryObserveForCleanup(zone, ledger, cohort, receipt, out observed, out Failure) ||
					!TryProveFrozenCohort(ledger, zone, ledger.RealmId, cohort, receipt, observed,
						AllowRemovedGear: true, out FrozenCustodyPlan[] plans, out Failure)) return false;
				for (int i = 0; i < observed.Length; i++)
				{
					GameObject body = observed[i];
					if (!GameObject.Validate(body)) continue;
					if (!TryBuildCustodyPlan(ledger, zone, ledger.RealmId, cohort, receipt, body, i,
						AllowRemovedGear: true, out plans[i], out Failure) ||
						!TryReleaseFrozenCustody(ledger, ledger.RealmId, cohort, receipt, plans[i],
							out Failure) || !TryRemoveExactBody(body, plans[i].Cell, ledger.RealmId,
							cohort, receipt, i, out Failure)) return false;
				}
				if (!TryObserveForCleanup(zone, ledger, cohort, receipt, out observed, out Failure))
					return false;
				if (!KingdomPolityCohortRules.TryCommitEndpointCleanup(ledger, cleanupRevision,
					cohort.CohortId, receipt.ProjectionId, receipt.ObjectIds,
					out KingdomPolityPublicationResult _, out Failure)) return false;
				if (!TryClearCohortRemovalWitnesses(zone, ledger, cohort, receipt, out Failure))
					return false;
				if (!TryClearCohortDeathIntents(zone, ledger, cohort, receipt, out Failure))
					return false;
			}
			cohort = KingdomPolityAuthority.Cohort(ledger, CohortId);
			return KingdomPolityExperienceRuntime.TryReleaseForCohort(System, cohort, out Failure);
		}

		private static bool TryCreatePreparedMember(KingdomPolityLedger Ledger, Zone Zone, Cell cell,
			KingdomPolityCohortPlan Cohort, KingdomPolityProjectionReceipt Receipt,
			KingdomPolityProfileRevision Profile, KingdomPolityRecord Polity,
			string CurrentFactionId, int Ordinal,
			out GameObject Body, out string Failure)
		{
			Body = null; Failure = null; KingdomPolityCohortMember member = Cohort.ResolvedMembers[Ordinal];
			if (!KingdomPolityCohortRules.TryParseSignature(member.SignatureKey,
				out string role, out string resolver, out string figureId) ||
				!KingdomPolityNpcRules.TryResolve(Profile, role, Ordinal,
					out KingdomPolityNpcSpec spec, out Failure) || spec.ResolverDigest != resolver ||
				spec.ResolverDigest != member.LoadoutKey || spec.BodyBlueprint != member.BlueprintKey)
			{
				Failure = Failure ?? "prepared member no longer matches its frozen resolver"; return false;
			}
			KingdomPolityNamedFigureRecord figure = string.IsNullOrEmpty(figureId) ? null :
				KingdomPolityAuthority.Figure(Ledger, figureId);
			if (figure != null && (figure.ResidentId != 0 || figure.Phase !=
				KingdomPolityFigurePhase.Active || figure.PolityId != Cohort.PolityId))
			{
				Failure = "prepared face is resident, inactive, or foreign"; return false;
			}
			if (cell == null)
			{
				Failure = "no proved local route can host the endpoint party"; return false;
			}
			string objectId = KingdomPolityCohortRules.PreparedObjectId(Cohort, Ordinal);
			bool made = KingdomPolityNpcRuntime.TryCreate(Profile, role, Ordinal,
				Polity.ProjectedFactionId, figureId, figure == null ? null : figure.DisplayName,
				Ledger.RealmId, Cohort.CohortId, Receipt.ProjectionId, objectId,
				created =>
				{
					created.ID = objectId;
					created.SetStringProperty(CohortOwnerProperty, Cohort.PolityId);
					created.SetStringProperty(CohortProperty, Cohort.CohortId);
					created.SetStringProperty(ProjectionProperty, Receipt.ProjectionId);
					created.SetIntProperty(MemberOrdinalProperty, Ordinal);
					created.SetIntProperty(CohortXProperty, cell.X);
					created.SetIntProperty(CohortYProperty, cell.Y);
					created.AddPart(new XRL.World.Parts.r_KingdomPolityCohortBody(Ledger.RealmId,
						Cohort.CohortId, Cohort.Purpose, Ordinal == 0));
				}, out GameObject created, out Failure);
			if (!made)
			{
				if (!TryMarkContestedPreparedBody(cell, created, Ledger.RealmId, Cohort, Receipt,
					Ordinal, out string markFailure)) Failure = (Failure ??
					"prepared body creation failed") + "; " + markFailure;
				TryPlaceContestedPreparedBody(cell, created); return false;
			}
			GameObject accepted = null;
			try { accepted = cell.AddObject(created); }
			catch (Exception ex)
			{
				Failure = "prepared body placement callback failed: " + ex.Message;
				if (!TryMarkContestedPreparedBody(cell, created, Ledger.RealmId, Cohort, Receipt,
					Ordinal, out string markFailure)) Failure += "; " + markFailure;
				TryPlaceContestedPreparedBody(cell, created); return false;
			}
			if (!KingdomPolityPhysicalCustodyRules.ExactPlacementAftermath(
				ReferenceEquals(accepted, created), GameObject.Validate(created),
				ReferenceEquals(created.CurrentCell, cell), created.IDIfAssigned == objectId,
				created.InInventory == null && created.Equipped == null))
			{
				Failure = "prepared body placement callback did not leave exact custody";
				if (!TryMarkContestedPreparedBody(cell, created, Ledger.RealmId, Cohort, Receipt,
					Ordinal, out string markFailure)) Failure += "; " + markFailure;
				TryPlaceContestedPreparedBody(cell, created);
				return false;
			}
			try
			{
				created.Brain.Wanders = false; created.Brain.WandersRandomly = false;
				created.Brain.Stay(cell);
				if (Cohort.Purpose == KingdomPolityCohortPurpose.Warband)
				{
					created.Brain.Allegiance["Player"] = -100;
					if (!string.IsNullOrEmpty(CurrentFactionId))
						created.Brain.Allegiance[CurrentFactionId] = -100;
				}
				created.MakeActive();
			}
			catch (Exception ex)
			{
				Failure = "prepared body activation callback failed: " + ex.Message;
				if (!TryMarkContestedPreparedBody(cell, created, Ledger.RealmId, Cohort, Receipt,
					Ordinal, out string markFailure)) Failure += "; " + markFailure;
				return false;
			}
			if (!TryBuildCustodyPlan(Ledger, Zone, Ledger.RealmId, Cohort, Receipt,
				created, Ordinal, AllowRemovedGear: false, out FrozenCustodyPlan _, out Failure))
			{
				if (!TryMarkContestedPreparedBody(cell, created, Ledger.RealmId, Cohort, Receipt,
					Ordinal, out string markFailure)) Failure = (Failure ??
					"prepared body custody proof failed") + "; " + markFailure;
				return false;
			}
			Body = created; return true;
		}
	}
}
