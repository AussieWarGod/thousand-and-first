namespace ThousandAndFirst
{
	/// <summary>Explicit polity adapters for ambient and directed embodied presentations.</summary>
	internal static partial class KingdomPolityExperienceRuntime
	{
		internal static bool TryReserveAmbientPlan(KingdomSystem System, string CohortId,
			string SettlementId, int BodyCount,
			long CauseTick, long Tick, out KingdomPolityPresentationAuthorityProof Proof,
			out bool Changed,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Proof = null;
			if (!TryReserveAmbient(System, CohortId, SettlementId, BodyCount,
				CauseTick, Tick, out Changed, out Fault, out Failure)) return false;
			return TryAuthorityProof(System, CohortId, KingdomExperienceOptionKind.AmbientUse,
				out Proof, out Failure);
		}

		internal static bool TryReserveDirectedPlan(KingdomSystem System, string CohortId,
			string SettlementId, int BodyCount,
			long CauseTick, long Tick, out KingdomPolityPresentationAuthorityProof Proof,
			out bool Changed,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Proof = null;
			if (!TryReserveDirected(System, CohortId, SettlementId, BodyCount,
				CauseTick, Tick, out Changed, out Fault, out Failure)) return false;
			return TryAuthorityProof(System, CohortId, KingdomExperienceOptionKind.CivicStory,
				out Proof, out Failure);
		}

		internal static bool TryReserveAmbientProjection(KingdomSystem System,
			KingdomPolityCohortPlan Cohort, long Tick,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			if (!TryCause(System?.PolityLedger, Cohort, out long cause, out Failure))
			{
				Fault = KingdomExperienceCapacityFault.InvalidRequest; return false;
			}
			return TryAssertActiveProjectionLease(System, Cohort, cause, Tick,
				KingdomExperienceOptionKind.AmbientUse, out Fault, out Failure);
		}

		internal static bool TryReserveDirectedProjection(KingdomSystem System,
			KingdomPolityCohortPlan Cohort, long Tick,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			if (!TryCause(System?.PolityLedger, Cohort, out long cause, out Failure))
			{
				Fault = KingdomExperienceCapacityFault.InvalidRequest; return false;
			}
			return TryAssertActiveProjectionLease(System, Cohort, cause, Tick,
				KingdomExperienceOptionKind.CivicStory, out Fault, out Failure);
		}

		internal static bool TryReleaseForCohort(KingdomSystem System,
			KingdomPolityCohortPlan Cohort, out string Failure)
		{
			if (Cohort == null)
			{
				Failure = "polity presentation release has no cohort"; return false;
			}
			if (Cohort.PresentationOptionKind == KingdomExperienceOptionKind.AmbientUse)
				return TryReleaseAmbient(System, Cohort.CohortId, out Failure);
			if (Cohort.PresentationOptionKind == KingdomExperienceOptionKind.CivicStory)
				return TryReleaseDirected(System, Cohort.CohortId, out Failure);
			// A v3 unpresented/terminal cohort may carry no triple. Release only the exact
			// persisted shape; projected legacy ambiguity is rejected before this seam.
			return FindAudience(System?.Experience, Cohort.CohortId) != null
				? TryReleaseAmbient(System, Cohort.CohortId, out Failure)
				: TryReleaseDirected(System, Cohort.CohortId, out Failure);
		}

		internal static bool TryReleaseAmbient(KingdomSystem System, string CohortId,
			out string Failure)
		{
			Failure = null;
			if (System?.Experience == null) return true;
			if (!ValidCohortId(CohortId, out Failure)) return false;
			return KingdomExperienceRuntime.TryReleasePresentation(System,
				AudienceReservationId(CohortId), BodyReservationId(CohortId), CohortId,
				out KingdomExperienceCapacityFault _, out Failure);
		}

		internal static bool TryReleaseDirected(KingdomSystem System, string CohortId,
			out string Failure)
		{
			Failure = null;
			if (System?.Experience == null) return true;
			if (!ValidCohortId(CohortId, out Failure)) return false;
			return KingdomExperienceRuntime.TryReleaseBodies(System, BodyReservationId(CohortId),
				CohortId, out KingdomExperienceCapacityFault _, out Failure);
		}

		internal static bool ExpectedCapacityRefusal(KingdomExperienceCapacityFault Fault)
		{
			return Fault == KingdomExperienceCapacityFault.OptionDisabled ||
				Fault == KingdomExperienceCapacityFault.CauseBeforeEnable ||
				Fault == KingdomExperienceCapacityFault.AudienceCapacityFull ||
				Fault == KingdomExperienceCapacityFault.LiveBodyCapacityFull ||
				Fault == KingdomExperienceCapacityFault.ReservationCapacityFull;
		}

		internal static bool CapacityRefusalNeedsDirectRecord(
			KingdomExperienceCapacityFault Fault)
		{
			return Fault == KingdomExperienceCapacityFault.AudienceCapacityFull ||
				Fault == KingdomExperienceCapacityFault.LiveBodyCapacityFull ||
				Fault == KingdomExperienceCapacityFault.ReservationCapacityFull;
		}

		internal static string AudienceReservationId(string CohortId)
		{
			return KingdomPolityRules.ActivationId("taf:experience-audience:polity:v1:",
				"polity-shared-presentation-v1", CohortId);
		}

		internal static string BodyReservationId(string CohortId)
		{
			return KingdomPolityRules.ActivationId("taf:experience-body:polity:v1:",
				"polity-shared-presentation-v1", CohortId);
		}

		private static bool TryReserveAmbient(KingdomSystem System, string CohortId,
			string SettlementId, int BodyCount,
			long CauseTick, long Tick, out bool Changed,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Changed = false;
			if (!TryPrepare(System, CohortId, SettlementId, BodyCount, CauseTick, Tick,
				KingdomExperienceOptionKind.AmbientUse, out long epoch, out Fault, out Failure)) return false;
			KingdomExperienceAudienceReceipt audience = FindAudience(System.Experience, CohortId);
			KingdomExperienceBodyReservation bodies = FindBodies(System.Experience, CohortId);
			if (!ExactAmbient(audience, bodies, System.RealmId, SettlementId, CohortId,
				KingdomExperienceOptionKind.AmbientUse, BodyCount, CauseTick, epoch, Tick,
				out Fault, out Failure)) return false;
			long reserved = audience?.ReservedTick ?? bodies?.ReservedTick ?? Tick;
			KingdomExperienceAudienceReceipt requestedAudience = audience ?? Audience(
				System.RealmId, SettlementId, CohortId, KingdomExperienceOptionKind.AmbientUse,
				CauseTick, reserved, epoch);
			KingdomExperienceBodyReservation requestedBodies = bodies ?? Bodies(System.RealmId,
				SettlementId, CohortId, KingdomExperienceOptionKind.AmbientUse, BodyCount,
				CauseTick, reserved, epoch);
			if (!KingdomExperienceRuntime.TryReservePresentation(System, requestedAudience,
				requestedBodies, out Fault, out Failure)) return false;
			Changed = audience == null || bodies == null; return true;
		}

		private static bool TryReserveDirected(KingdomSystem System, string CohortId,
			string SettlementId, int BodyCount,
			long CauseTick, long Tick, out bool Changed,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Changed = false;
			if (!TryPrepare(System, CohortId, SettlementId, BodyCount, CauseTick, Tick,
				KingdomExperienceOptionKind.CivicStory, out long epoch, out Fault, out Failure)) return false;
			if (FindAudience(System.Experience, CohortId) != null)
			{
				Fault = KingdomExperienceCapacityFault.DuplicateMismatch;
				Failure = "directed polity presentation owns a forbidden audience lease"; return false;
			}
			KingdomExperienceBodyReservation bodies = FindBodies(System.Experience, CohortId);
			if (bodies != null && !Matches(bodies, System.RealmId, SettlementId, CohortId,
				KingdomExperienceOptionKind.CivicStory, BodyCount, CauseTick, epoch, Tick))
			{
				Fault = KingdomExperienceCapacityFault.DuplicateMismatch;
				Failure = "directed polity body lease is mismatched"; return false;
			}
			KingdomExperienceBodyReservation request = bodies ?? Bodies(System.RealmId,
				SettlementId, CohortId, KingdomExperienceOptionKind.CivicStory, BodyCount,
				CauseTick, Tick, epoch);
			if (!KingdomExperienceRuntime.TryReserveBodies(System, request, out Fault,
				out Failure)) return false;
			Changed = bodies == null; return true;
		}

		private static bool TryPrepare(KingdomSystem System, string CohortId,
			string SettlementId, int BodyCount, long CauseTick, long Tick,
			KingdomExperienceOptionKind Option, out long Epoch,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Epoch = 0L; Fault = KingdomExperienceCapacityFault.InvalidRequest; Failure = null;
			if (System == null || Tick < 0L || CauseTick < 0L || CauseTick > Tick ||
				!KingdomPolityRules.TypedId(CohortId, "taf:cohort:") ||
				!KingdomPolityRules.TypedId(SettlementId, "taf:settlement:v1:") ||
				BodyCount < 1 || BodyCount > KingdomPolityRules.MaxCohortMembers)
			{
				Failure = "polity shared-capacity request is invalid"; return false;
			}
			if (!KingdomMaster.NewWorkAllowed(System))
			{
				Fault = KingdomExperienceCapacityFault.OptionDisabled;
				Failure = "experience master option is disabled"; return false;
			}
			if (!KingdomExperienceRuntime.TryObserveConfiguredOptions(System, Tick, out Failure))
			{
				Fault = KingdomExperienceCapacityFault.InvalidLedger; return false;
			}
			if (!KingdomExperienceRules.TryGetEnableEpoch(System.Experience, Option, CauseTick,
				out Epoch, out Failure))
			{
				Fault = KingdomExperienceCapacityFault.CauseBeforeEnable; return false;
			}
			return true;
		}

		private static bool ValidCohortId(string CohortId, out string Failure)
		{
			Failure = null;
			if (KingdomPolityRules.TypedId(CohortId, "taf:cohort:")) return true;
			Failure = "polity presentation release identity is invalid"; return false;
		}

		private static bool TryAuthorityProof(KingdomSystem System, string CohortId,
			KingdomExperienceOptionKind Expected, out KingdomPolityPresentationAuthorityProof Proof,
			out string Failure)
		{
			Proof = null; Failure = null;
			KingdomExperienceBodyReservation row = FindBodies(System?.Experience, CohortId);
			if (row == null || row.OptionKind != Expected || row.SourceId != CohortId ||
				row.Lane != KingdomExperienceLane.PolityCohort || row.EnableEpoch < 1L ||
				row.ReservedTick < row.CauseTick)
			{
				Failure = "polity plan reservation produced no exact typed authority"; return false;
			}
			Proof = new KingdomPolityPresentationAuthorityProof
			{
				OptionKind = row.OptionKind, EnableEpoch = row.EnableEpoch,
				ReservedTick = row.ReservedTick
			};
			return true;
		}
	}
}
