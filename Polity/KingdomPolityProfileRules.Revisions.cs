using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityProfileRules
	{
		public static bool TryRevise(KingdomPolityLedger Ledger, long ExpectedLedgerRevision,
			KingdomPolityProfileFactSet Facts, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!ValidRevisionFacts(Facts, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityRecord polity = KingdomPolityAuthority.Polity(Ledger, Facts.PolityId);
			if (polity == null || polity.Lifecycle != KingdomPolityLifecycle.Active ||
				polity.ProfileId != Facts.ProfileId)
				return KingdomPolityAuthority.Refuse(Result,
					"profile fact offer has no exact active polity lineage", out Failure);
			KingdomPolityProfileRevision prior = KingdomPolityAuthority.Profile(Ledger,
				polity.ProfileId, polity.ProfileRevision);
			if (prior == null) return KingdomPolityAuthority.Refuse(Result,
				"active polity profile is missing", out Failure);
			string digest = FactDigest(Facts);
			if (ExactFactRevision(prior, Facts, digest))
			{
				bool exactRetry = Facts.PreviousRevision == prior.Revision - 1 &&
					Facts.EffectiveTick == prior.EffectiveTick;
				bool stableObservation = Facts.PreviousRevision == prior.Revision &&
					Facts.EffectiveTick >= prior.EffectiveTick;
				if (!exactRetry && !stableObservation)
					return KingdomPolityAuthority.Refuse(Result,
						"profile fact retry does not match its immutable publication", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (Facts.PreviousRevision != prior.Revision || Facts.EffectiveTick < prior.EffectiveTick)
				return KingdomPolityAuthority.Refuse(Result,
					"profile fact offer does not follow the current immutable revision", out Failure);
			if (prior.Revision == int.MaxValue || Ledger.Profiles.Count >= KingdomPolityRules.MaxProfiles)
				return KingdomPolityAuthority.Refuse(Result,
					"profile revision capacity is exhausted", out Failure);
			if (Ledger.Revision != ExpectedLedgerRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityProfileRevision next = BuildRevision(prior, Facts, digest);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			InsertRevision(candidate.Profiles, next);
			KingdomPolityRecord changed = KingdomPolityAuthority.Polity(candidate, polity.PolityId);
			changed.ProfileRevision = next.Revision;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		internal static bool ValidRevisionFacts(KingdomPolityProfileFactSet F,
			out string Failure)
		{
			Failure = null;
			if (F == null || !KingdomPolityRules.SemanticId(F.PolityId) ||
				!KingdomPolityRules.TypedId(F.ProfileId, "taf:polity-profile:") ||
				F.PreviousRevision < 1 || F.EffectiveTick < 0L || F.TechnologyBand < 0 ||
				F.TechnologyBand > 10 || F.Facts == null || F.Facts.Count < 1 ||
				F.Facts.Count > KingdomPolityRules.MaxRefs)
				return KingdomPolityRules.Fail("profile revision facts are invalid or unbounded",
					out Failure);
			string previous = null; bool technology = false;
			for (int i = 0; i < F.Facts.Count; i++)
			{
				KingdomPolityProfileFact fact = F.Facts[i];
				if (fact == null || !KingdomPolityRules.TypedId(fact.FactId, "taf:fact:profile:") ||
					(previous != null && string.CompareOrdinal(previous, fact.FactId) >= 0) ||
					fact.Kind == KingdomPolityProfileFactKind.None || (byte)fact.Kind > 7 ||
					!KingdomPolityRules.Text(fact.ValueKey, true) ||
					!KingdomPolityRules.SemanticId(fact.SourceRef))
					return KingdomPolityRules.Fail("profile revision fact is noncanonical", out Failure);
				technology |= fact.Kind == KingdomPolityProfileFactKind.Technology;
				previous = fact.FactId;
			}
			return technology || KingdomPolityRules.Fail(
				"profile revision has no concrete technology fact", out Failure);
		}

		private static string FactDigest(KingdomPolityProfileFactSet F)
		{
			List<string> values = new List<string> { F.PolityId, F.ProfileId,
				F.TechnologyBand.ToString(CultureInfo.InvariantCulture) };
			for (int i = 0; i < F.Facts.Count; i++)
			{
				KingdomPolityProfileFact fact = F.Facts[i];
				values.Add(((byte)fact.Kind).ToString(CultureInfo.InvariantCulture));
				values.Add(fact.FactId); values.Add(fact.ValueKey); values.Add(fact.SourceRef);
			}
			return KingdomPolityRules.ActivationDigest("polity-profile-facts-v2", values);
		}

		private static KingdomPolityProfileRevision BuildRevision(
			KingdomPolityProfileRevision Prior, KingdomPolityProfileFactSet F, string Digest)
		{
			List<string> factIds = new List<string>(); List<string> practices = new List<string>();
			for (int i = 0; i < F.Facts.Count; i++)
			{
				KingdomPolityProfileFact fact = F.Facts[i]; factIds.Add(fact.FactId);
				if (fact.Kind == KingdomPolityProfileFactKind.Decision ||
					fact.Kind == KingdomPolityProfileFactKind.Creed ||
					fact.Kind == KingdomPolityProfileFactKind.Style)
					AddPractice(practices, fact.Kind.ToString().ToLowerInvariant() + "-" +
						Token(fact.ValueKey));
			}
			List<string> gear = GearKeys(F.TechnologyBand);
			KingdomPolityLoadoutPolicy loadout = new KingdomPolityLoadoutPolicy
			{
				Kind = KingdomPolityLoadoutPolicyKind.OwnedReplace,
				ExpectedValueBudget = Math.Min(KingdomPolityRules.MaxValueBudget,
					50 + F.TechnologyBand * 125),
				ExcludedKeys = new List<string>(Prior.Loadout.ExcludedKeys),
				SelectedKeys = new List<string>(gear)
			};
			return new KingdomPolityProfileRevision
			{
				ProfileId = Prior.ProfileId, Revision = Prior.Revision + 1,
				PolityId = Prior.PolityId, EffectiveTick = F.EffectiveTick,
				RulesVersion = RulesVersion, DerivedFromFactIds = factIds, FactsDigest = Digest,
				TechnologyBand = F.TechnologyBand, PracticeTags = practices,
				BodyKeys = new List<string>(Prior.BodyKeys),
				RoleKeys = new List<string>(Prior.RoleKeys), GearKeys = gear, Loadout = loadout
			};
		}

		private static bool ExactFactRevision(KingdomPolityProfileRevision P,
			KingdomPolityProfileFactSet F, string Digest)
		{
			if (P.FactsDigest != Digest || P.TechnologyBand != F.TechnologyBand ||
				P.DerivedFromFactIds.Count != F.Facts.Count) return false;
			for (int i = 0; i < F.Facts.Count; i++)
				if (P.DerivedFromFactIds[i] != F.Facts[i].FactId) return false;
			return true;
		}

		private static void AddPractice(List<string> Values, string Value)
		{
			if (Values.Count >= 8 || Values.Contains(Value)) return;
			Values.Add(Value); Values.Sort(StringComparer.Ordinal);
		}

		private static void InsertRevision(List<KingdomPolityProfileRevision> Values,
			KingdomPolityProfileRevision Revision)
		{
			int at = 0;
			while (at < Values.Count && (string.CompareOrdinal(Values[at].ProfileId,
				Revision.ProfileId) < 0 || (Values[at].ProfileId == Revision.ProfileId &&
				 Values[at].Revision < Revision.Revision))) at++;
			Values.Insert(at, Revision);
		}
	}
}
