using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		public static bool TrySetEmptyImportPolicy(KingdomPolityLedger Ledger, long ExpectedRevision,
			KingdomPolityImportPolicy Desired, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = BeginResult(Ledger); Failure = null;
			if (!TryValidate(Ledger, out Failure) || (Desired != KingdomPolityImportPolicy.Off &&
				Desired != KingdomPolityImportPolicy.LatestEligible)) return Refuse(Result, Failure, out Failure);
			if (Ledger.Options.ImportPolicy == Desired)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (Ledger.Revision != ExpectedRevision) return Conflict(Result, out Failure);
			if (HasSemanticRows(Ledger) || Ledger.Compactions.Count != 0 ||
				Ledger.FoldedCompactionCount != 0L)
				return Refuse(Result, "import policy is frozen behind semantic evidence", out Failure);
			KingdomPolityLedger candidate = Clone(Ledger);
			candidate.Options.ImportPolicy = Desired; candidate.Options.ImportPolicyFrozen = true;
			if (!Increment(candidate, out Failure) || !TryValidate(candidate, out Failure))
				return Refuse(Result, Failure, out Failure);
			Commit(Ledger, candidate, Result); return true;
		}

		/// <summary>
		/// Publishes current-realm authority and zero or one committed-seal counterparty as one CAS.
		/// Runtime recovery observes this frozen authority; it never rebuilds it from mutable live facts.
		/// </summary>
		public static bool TryPublishFoundation(KingdomPolityLedger Ledger, long ExpectedRevision,
			KingdomPolityFoundationFacts Facts, KingdomPolityLegacySnapshot Legacy,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = BeginResult(Ledger); Failure = null;
			if (!TryValidate(Ledger, out Failure) || !KingdomPolityProfileRules.ValidFoundation(
				Facts, out Failure)) return Refuse(Result, Failure, out Failure);
			if (!Ledger.IdentityBound || Ledger.RealmId != Facts.RealmId)
				return Refuse(Result, "foundation facts do not bind this polity ledger", out Failure);
			if (Legacy != null && (Ledger.Options.ImportPolicy !=
				KingdomPolityImportPolicy.LatestEligible ||
				!KingdomPolityProfileRules.ValidLegacy(Legacy, out Failure)))
				return Refuse(Result, Failure ?? "legacy import is not enabled", out Failure);
			FoundationPlan plan;
			if (!TryPlanFoundation(Facts, Legacy, out plan, out Failure))
				return Refuse(Result, Failure, out Failure);
			Result.CurrentPolityId = Facts.RealmId;
			Result.ImportedPolityId = plan.Legacy == null ? null : plan.Legacy.PolityId;
			bool currentExact = CurrentFoundationExact(Ledger, plan);
			bool legacyExact = plan.Legacy == null || LegacyFoundationExact(Ledger, plan);
			if (currentExact && legacyExact)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (Ledger.Revision != ExpectedRevision) return Conflict(Result, out Failure);
			if ((!currentExact && HasSemanticRows(Ledger)) ||
				(currentExact && (HasExternalPolity(Ledger) || Legacy == null)))
				return Refuse(Result, "foundation would reinterpret populated polity authority", out Failure);
			KingdomPolityLedger candidate = Clone(Ledger);
			if (!currentExact) AddCurrentFoundation(candidate, plan);
			if (plan.Legacy != null) AddLegacyFoundation(candidate, plan);
			CanonicalSort(candidate);
			if (!Increment(candidate, out Failure) || !TryValidate(candidate, out Failure))
				return Refuse(Result, Failure, out Failure);
			Commit(Ledger, candidate, Result); return true;
		}

		private sealed class FoundationPlan
		{
			internal KingdomPolityRecord Current;
			internal KingdomPolityProfileRevision CurrentProfile;
			internal KingdomPolityProjectionReceipt CurrentProjection;
			internal KingdomPolityRecord Legacy;
			internal KingdomPolityProfileRevision LegacyProfile;
			internal KingdomPolityRelation Forward;
			internal KingdomPolityRelation Reverse;
			internal KingdomPolityNamedFigureRecord LegacyFigure;
		}

		private static bool TryPlanFoundation(KingdomPolityFoundationFacts Facts,
			KingdomPolityLegacySnapshot Legacy, out FoundationPlan Plan, out string Failure)
		{
			Plan = new FoundationPlan(); Failure = null;
			if (!KingdomPolityProfileRules.TryCreateCurrent(Facts,
				out Plan.CurrentProfile, out Failure)) return false;
			Plan.Current = new KingdomPolityRecord
			{
				PolityId = Facts.RealmId, DisplayName = Facts.DisplayName, NameRevision = 1,
				Source = KingdomPolitySource.CurrentRealm, Lifecycle = KingdomPolityLifecycle.Active,
				ProfileId = Plan.CurrentProfile.ProfileId, ProfileRevision = 1,
				ProjectedFactionId = Facts.FactionId
			};
			Plan.CurrentProjection = FoundationProjection(Facts.RealmId, Facts.FactionId,
				ProfileExpressionDigest(Plan.CurrentProfile), Facts.FoundedTick, true);
			if (Legacy == null) return true;
			string polityId = ActivationId("taf:polity:legacy:v1:", "legacy-polity-id-v1",
				Facts.RealmId, Legacy.LegacyToken, Legacy.LineageToken);
			string factionId = ActivationId("taf:polity-faction:v1:", "legacy-faction-id-v1",
				Facts.RealmId, polityId, Legacy.LegacyToken);
			if (!KingdomPolityProfileRules.TryCreateLegacy(polityId, Legacy, Facts.FoundedTick,
				out Plan.LegacyProfile, out Failure)) return false;
			Plan.Legacy = new KingdomPolityRecord
			{
				PolityId = polityId, DisplayName = Legacy.RealmName, NameRevision = 1,
				Source = KingdomPolitySource.ImportedLegacy,
				Lifecycle = KingdomPolityLifecycle.Latent,
				ProfileId = Plan.LegacyProfile.ProfileId, ProfileRevision = 1,
				ProjectedFactionId = factionId
			};
			KingdomPolityRelationBand band = RelationshipFor(Facts, Legacy);
			string cause = ActivationId("taf:fact:legacy-relation:v1:",
				"legacy-relation-fact-v1", Facts.RealmId, polityId, band.ToString());
			Plan.Forward = Relation(Facts.RealmId, polityId, band, cause, Facts.FoundedTick);
			Plan.Reverse = Relation(polityId, Facts.RealmId, band, cause, Facts.FoundedTick);
			Plan.LegacyFigure = LegacyFigure(polityId, Facts, Legacy, band, cause);
			return true;
		}

		private static void AddCurrentFoundation(KingdomPolityLedger L, FoundationPlan P)
		{
			L.Polities.Add(P.Current); L.Profiles.Add(P.CurrentProfile);
			L.Projections.Add(P.CurrentProjection);
		}

		private static void AddLegacyFoundation(KingdomPolityLedger L, FoundationPlan P)
		{
			L.Polities.Add(P.Legacy); L.Profiles.Add(P.LegacyProfile);
			L.Relations.Add(P.Forward); L.Relations.Add(P.Reverse);
			L.NamedFigures.Add(P.LegacyFigure);
		}

		private static bool CurrentFoundationExact(KingdomPolityLedger L, FoundationPlan P)
		{
			return ExactPolity(L, P.Current) && ExactProfile(L, P.CurrentProfile) &&
				ExactProjection(L, P.CurrentProjection);
		}

		private static bool LegacyFoundationExact(KingdomPolityLedger L, FoundationPlan P)
		{
			return ExactLegacyPolity(L, P.Legacy) && ExactProfile(L, P.LegacyProfile) &&
				ExactRelation(L, P.Forward) && ExactRelation(L, P.Reverse) &&
				ExactFigure(L, P.LegacyFigure);
		}
	}
}
