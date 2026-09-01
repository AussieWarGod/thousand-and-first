using System;
using System.Collections.Generic;
namespace ThousandAndFirst
{
	/// <summary>Freezes a finite endpoint party from one immutable polity profile revision.</summary>
	public static partial class KingdomPolityCohortRules
	{
		public const int MaximumVisibleMembers = KingdomPolityRules.MaxCohortMembers;
		public static bool TryLevelBand(KingdomPolityLedger Ledger, string PolityId,
			KingdomPolityCohortPurpose Purpose, out int Minimum, out int Maximum,
			out string Failure)
		{
			return TryResolverContract(Ledger, PolityId, Purpose, out _, out Minimum,
				out Maximum, out Failure);
		}

		public static bool TryResolverContract(KingdomPolityLedger Ledger, string PolityId,
			KingdomPolityCohortPurpose Purpose, out int ResolverRulesVersion,
			out int Minimum, out int Maximum, out string Failure)
		{
			ResolverRulesVersion = 0; Minimum = Maximum = 0; Failure = null;
			KingdomPolityRecord polity = KingdomPolityAuthority.Polity(Ledger, PolityId);
			KingdomPolityProfileRevision profile = polity == null ? null :
				KingdomPolityAuthority.Profile(Ledger, polity.ProfileId, polity.ProfileRevision);
			if (polity == null || profile == null || polity.Lifecycle !=
				KingdomPolityLifecycle.Active || Purpose == KingdomPolityCohortPurpose.None ||
				(byte)Purpose > 7)
				return KingdomPolityRules.Fail(
					"level band lacks an exact active polity profile and purpose", out Failure);
			ResolverRulesVersion = profile.RulesVersion == KingdomPolityProfileRules.RulesVersion
				? KingdomPolityNpcRules.RulesVersion : profile.RulesVersion ==
					KingdomPolityProfileRules.LegacyRulesVersion || profile.RulesVersion ==
					KingdomPolityProfileRules.PriorExpressionRulesVersion ? 1 : 0;
			if (ResolverRulesVersion == 0) return KingdomPolityRules.Fail(
				"active polity profile pins an unknown NPC resolver contract", out Failure);
			return TryLevelBandForProfile(profile, Purpose, out Minimum, out Maximum);
		}

		public static bool TryPlan(KingdomPolityLedger Ledger, long ExpectedRevision,
			KingdomPolityCohortPlanRequest Request, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!ValidRequest(Request, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityCohortPlan existing = KingdomPolityAuthority.Cohort(
				Ledger, Request.CohortId);
			if (existing != null)
			{
				if (!ExactRequest(existing, Request)) return KingdomPolityAuthority.Refuse(Result,
					"cohort id already carries different pinned members", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			KingdomPolityRecord polity = KingdomPolityAuthority.Polity(Ledger, Request.PolityId);
			KingdomPolityProfileRevision profile = polity == null ? null :
				KingdomPolityAuthority.Profile(Ledger, polity.ProfileId, polity.ProfileRevision);
			if (polity == null || profile == null || polity.Lifecycle != KingdomPolityLifecycle.Active ||
				!ValidSource(Ledger, Request))
				return KingdomPolityAuthority.Refuse(Result,
					"cohort lacks an active polity, frozen profile, or exact source", out Failure);
			KingdomPolityNamedFigureRecord figure = null;
			if (!string.IsNullOrEmpty(Request.NamedFigureId))
			{
				figure = KingdomPolityAuthority.Figure(Ledger, Request.NamedFigureId);
				if (figure == null || figure.PolityId != Request.PolityId ||
					figure.Phase != KingdomPolityFigurePhase.Active ||
					figure.Origin == KingdomPolityFigureOrigin.Officeholder ||
					figure.ResidentId != 0 ||
					!RoleAllowed(Request.Purpose, figure.RoleKey))
					return KingdomPolityAuthority.Refuse(Result,
						"named representative is not an eligible active face", out Failure);
			}
			KingdomPolityCohortPlan expected;
			if (!TryBuild(Request, profile, figure, out expected, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			if (Ledger.Cohorts.Count >= KingdomPolityRules.MaxCohorts)
				return KingdomPolityAuthority.Refuse(Result, "cohort capacity is exhausted", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			candidate.Cohorts.Add(expected);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		internal static bool TryParseSignature(string Signature, out string RoleKey,
			out string ResolverDigest, out string FigureId)
		{
			RoleKey = null; ResolverDigest = null; FigureId = null;
			if (string.IsNullOrEmpty(Signature)) return false;
			string[] parts = Signature.Split('|');
			if (parts.Length != 4 || parts[0] != "v1" ||
				!KingdomPolityRules.Text(parts[1], true) ||
				!KingdomPolityRules.Digest(parts[2]) ||
				(!string.IsNullOrEmpty(parts[3]) &&
				 !KingdomPolityRules.TypedId(parts[3], "taf:figure:"))) return false;
			RoleKey = parts[1]; ResolverDigest = parts[2];
			FigureId = string.IsNullOrEmpty(parts[3]) ? null : parts[3]; return true;
		}

		private static bool ValidRequest(KingdomPolityCohortPlanRequest R, out string Failure)
		{
			Failure = null;
			if (R == null || !KingdomPolityRules.TypedId(R.CohortId, "taf:cohort:") ||
				R.Purpose == KingdomPolityCohortPurpose.None || (byte)R.Purpose > 7 ||
				!KingdomPolityRules.SemanticId(R.SourceRef) ||
				R.SourceRef.StartsWith("taf:standing:", StringComparison.Ordinal) ||
				!KingdomPolityRules.SemanticId(R.PolityId) ||
				!KingdomPolityRules.SemanticId(R.SurfaceRef) || R.MemberCount < 1 ||
				R.MemberCount > MaximumVisibleMembers || R.MinimumLevel < 1 ||
				R.MaximumLevel < R.MinimumLevel ||
				R.MaximumLevel > KingdomPolityRules.MaxLevel ||
				(!string.IsNullOrEmpty(R.NamedFigureId) &&
				 !KingdomPolityRules.TypedId(R.NamedFigureId, "taf:figure:")) ||
				!KingdomPolityRules.TypedId(R.EventStreamId, "taf:stream:") ||
				(R.RulesVersion != 1 && R.RulesVersion !=
					KingdomPolityLoadoutCatalogue.PriorResolverVersion &&
				 R.RulesVersion != KingdomPolityNpcRules.RulesVersion) ||
				!ValidPresentationAuthority(R.Purpose, R.PresentationAuthority) ||
				(IsWeeklyAmbient(R) && !KingdomPolityAmbientTransactionRules.Valid(
					R.AmbientTransaction, R.CohortId, out _)) ||
				(IsWeeklyAmbient(R) && (R.AmbientTransaction.Purpose != R.Purpose ||
					R.AmbientTransaction.SourcePolityId != R.PolityId ||
					R.AmbientTransaction.DestinationSettlementId != R.SurfaceRef)) ||
				(!IsWeeklyAmbient(R) && R.AmbientTransaction != null))
			{
				Failure = "cohort plan request is invalid, unbounded, or standing-only"; return false;
			}
			return true;
		}

		private static bool ValidSource(KingdomPolityLedger L, KingdomPolityCohortPlanRequest R)
		{
			if (R.SourceRef.StartsWith("taf:route:", StringComparison.Ordinal))
			{
				KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(L, R.SourceRef);
				return route != null && (route.OriginId == R.SurfaceRef ||
					route.DestinationId == R.SurfaceRef) && RoutePurpose(route.Purpose) == R.Purpose;
			}
			if (R.SourceRef.StartsWith("taf:front:", StringComparison.Ordinal))
			{
				for (int i = 0; i < L.Fronts.Count; i++) if (L.Fronts[i].FrontId == R.SourceRef)
					return R.Purpose == KingdomPolityCohortPurpose.Warband ||
						R.Purpose == KingdomPolityCohortPurpose.Patrol;
				return false;
			}
			return R.SourceRef.StartsWith("taf:event:", StringComparison.Ordinal) ||
				R.SourceRef.StartsWith("taf:incident:", StringComparison.Ordinal);
		}

		private static bool TryBuild(KingdomPolityCohortPlanRequest R,
			KingdomPolityProfileRevision Profile, KingdomPolityNamedFigureRecord Figure,
			out KingdomPolityCohortPlan Plan, out string Failure)
		{
			Plan = null; Failure = null;
			if (!TryLevelBandForProfile(Profile, R.Purpose, out int bandMinimum,
				out int bandMaximum) || R.MinimumLevel != bandMinimum ||
				R.MaximumLevel != bandMaximum || R.RulesVersion !=
				(Profile.RulesVersion == KingdomPolityProfileRules.RulesVersion
					? KingdomPolityNpcRules.RulesVersion : 1))
			{
				Failure = "cohort request does not carry the canonical pinned level band";
				return false;
			}
			Plan = new KingdomPolityCohortPlan
			{
				CohortId = R.CohortId, Purpose = R.Purpose, SourceRef = R.SourceRef,
				PolityId = R.PolityId, ProfileId = Profile.ProfileId,
				ProfileRevision = Profile.Revision, MinimumLevel = R.MinimumLevel,
				MaximumLevel = R.MaximumLevel, SurfaceRef = R.SurfaceRef,
				ScaleBudget = R.MemberCount, NamedRepresentativeAllowance = Figure == null ? 0 : 1,
				EventStreamId = R.EventStreamId, RulesVersion = R.RulesVersion,
				EventOrdinal = R.EventOrdinal,
				PresentationOptionKind = R.PresentationAuthority.OptionKind,
				PresentationEnableEpoch = R.PresentationAuthority.EnableEpoch,
				PresentationReservedTick = R.PresentationAuthority.ReservedTick,
				Phase = KingdomPolityCohortPhase.Planned,
				AmbientTransaction = KingdomPolityAmbientTransactionRules.Copy(R.AmbientTransaction)
			};
			for (int i = 0; i < R.MemberCount; i++)
			{
				string role = i == 0 && Figure != null ? Figure.RoleKey : Role(R.Purpose, i);
				if (role.IndexOf('|') >= 0 || !KingdomPolityNpcRules.TryResolvePinned(Profile,
					role, i, R.RulesVersion, R.MinimumLevel, R.MaximumLevel,
					out KingdomPolityNpcSpec spec, out Failure)) return false;
				KingdomPolityAuthority.AddSortedUnique(Plan.RoleSlots, role);
				string figureId = i == 0 && Figure != null ? Figure.FigureId : null;
				Plan.ResolvedMembers.Add(new KingdomPolityCohortMember
				{
					Ordinal = i, MemberKey = KingdomPolityRules.ActivationId(
						"taf:cohort-member:v1:", "polity-cohort-member-v1", R.CohortId,
						i.ToString(System.Globalization.CultureInfo.InvariantCulture), spec.ResolverDigest),
					BlueprintKey = spec.BodyBlueprint, LoadoutKey = spec.ResolverDigest,
					SignatureKey = "v1|" + role + "|" + spec.ResolverDigest + "|" + (figureId ?? "")
				});
			}
			return true;
		}

		private static bool TryLevelBandForProfile(KingdomPolityProfileRevision Profile,
			KingdomPolityCohortPurpose Purpose, out int Minimum, out int Maximum)
		{
			Minimum = Maximum = 0;
			if (Profile == null || Profile.TechnologyBand < 0 || Profile.TechnologyBand > 10 ||
				Purpose == KingdomPolityCohortPurpose.None || (byte)Purpose > 7) return false;
			int purpose = Purpose == KingdomPolityCohortPurpose.Warband ? 4 :
				Purpose == KingdomPolityCohortPurpose.Guard ||
				Purpose == KingdomPolityCohortPurpose.Patrol ? 3 :
				Purpose == KingdomPolityCohortPurpose.Envoy ||
				Purpose == KingdomPolityCohortPurpose.Trader ||
				Purpose == KingdomPolityCohortPurpose.Courier ? 1 : 0;
			Minimum = Math.Min(KingdomPolityRules.MaxLevel,
				1 + Profile.TechnologyBand * 3 + purpose);
			Maximum = Math.Min(KingdomPolityRules.MaxLevel, Minimum + 3);
			return true;
		}

		private static string Role(KingdomPolityCohortPurpose Purpose, int Ordinal)
		{
			if (Ordinal > 0 && (Purpose == KingdomPolityCohortPurpose.Patrol ||
				Purpose == KingdomPolityCohortPurpose.Trader || Purpose == KingdomPolityCohortPurpose.Envoy ||
				Purpose == KingdomPolityCohortPurpose.Courier)) return "guard";
			switch (Purpose)
			{
			case KingdomPolityCohortPurpose.Guard: return "guard";
			case KingdomPolityCohortPurpose.Patrol: return "patrol";
			case KingdomPolityCohortPurpose.Trader: return "trader";
			case KingdomPolityCohortPurpose.Envoy: return "envoy";
			case KingdomPolityCohortPurpose.Courier: return "courier";
			case KingdomPolityCohortPurpose.Warband: return "warband";
			default: return "migrant";
			}
		}

		private static bool RoleAllowed(KingdomPolityCohortPurpose Purpose, string CandidateRole)
		{
			if (Purpose == KingdomPolityCohortPurpose.Warband && CandidateRole == "claimant") return true;
			if (Purpose == KingdomPolityCohortPurpose.Envoy &&
				(CandidateRole == "namesake" || CandidateRole == "successor")) return true;
			return Role(Purpose, 0) == CandidateRole;
		}

		private static KingdomPolityCohortPurpose RoutePurpose(KingdomPolityRoutePurpose P)
		{
			switch (P)
			{
			case KingdomPolityRoutePurpose.Trade: return KingdomPolityCohortPurpose.Trader;
			case KingdomPolityRoutePurpose.Delegation: return KingdomPolityCohortPurpose.Envoy;
			case KingdomPolityRoutePurpose.Patrol: return KingdomPolityCohortPurpose.Patrol;
			case KingdomPolityRoutePurpose.Migration: return KingdomPolityCohortPurpose.Migrant;
			default: return KingdomPolityCohortPurpose.Courier;
			}
		}

		private static bool ValidPresentationAuthority(KingdomPolityCohortPurpose Purpose,
			KingdomPolityPresentationAuthorityProof Proof)
		{
			if (Proof == null || Proof.EnableEpoch < 1L || Proof.ReservedTick < 0L) return false;
			KingdomExperienceOptionKind expected =
				Purpose == KingdomPolityCohortPurpose.Envoy ||
				Purpose == KingdomPolityCohortPurpose.Warband
					? KingdomExperienceOptionKind.CivicStory
					: KingdomExperienceOptionKind.AmbientUse;
			return Proof.OptionKind == expected;
		}

		private static bool IsWeeklyAmbient(KingdomPolityCohortPlanRequest R)
		{
			return R != null && R.EventStreamId != null && R.EventStreamId.StartsWith(
				"taf:stream:polity-due:v1:", StringComparison.Ordinal);
		}

		private static bool ExactPlan(KingdomPolityCohortPlan A, KingdomPolityCohortPlan E)
		{
			if (A.Purpose != E.Purpose || A.SourceRef != E.SourceRef || A.PolityId != E.PolityId ||
				A.ProfileId != E.ProfileId || A.ProfileRevision != E.ProfileRevision ||
				A.SurfaceRef != E.SurfaceRef || A.ScaleBudget != E.ScaleBudget ||
				A.NamedRepresentativeAllowance != E.NamedRepresentativeAllowance ||
				A.EventStreamId != E.EventStreamId || A.RulesVersion != E.RulesVersion ||
				A.EventOrdinal != E.EventOrdinal ||
				A.PresentationOptionKind != E.PresentationOptionKind ||
				A.PresentationEnableEpoch != E.PresentationEnableEpoch ||
				A.PresentationReservedTick != E.PresentationReservedTick ||
				!KingdomPolityAmbientTransactionRules.Same(
					A.AmbientTransaction, E.AmbientTransaction) ||
				A.ResolvedMembers.Count != E.ResolvedMembers.Count)
				return false;
			for (int i = 0; i < A.ResolvedMembers.Count; i++)
			{
				KingdomPolityCohortMember a = A.ResolvedMembers[i], e = E.ResolvedMembers[i];
				if (a.Ordinal != e.Ordinal || a.MemberKey != e.MemberKey ||
					a.BlueprintKey != e.BlueprintKey || a.LoadoutKey != e.LoadoutKey ||
					a.SignatureKey != e.SignatureKey) return false;
			}
			return true;
		}
	}
}
