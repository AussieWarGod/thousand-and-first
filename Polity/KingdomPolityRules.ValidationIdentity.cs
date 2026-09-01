using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		private static bool ValidateIdentityState(KingdomPolityLedger L, out string Failure)
		{
			Failure = null;
			int current = 0, liveExternal = 0, vanilla = 0;
			string previous = null;
			for (int i = 0; i < L.Polities.Count; i++)
			{
				KingdomPolityRecord p = L.Polities[i];
				if (p == null || !SemanticId(p.PolityId) || !After(previous, p.PolityId) ||
					!Text(p.DisplayName, true) || p.NameRevision < 1 ||
					!Defined((byte)p.Source, 4) || p.Source == KingdomPolitySource.None ||
					!Defined((byte)p.Lifecycle, 3))
					return Fail("polity record is invalid or noncanonical", out Failure);
				previous = p.PolityId;
				if (p.Source == KingdomPolitySource.CurrentRealm)
				{
					current++;
					if (!string.Equals(p.PolityId, L.RealmId, StringComparison.Ordinal))
						return Fail("current polity does not equal realm identity", out Failure);
				}
				else if (p.Source == KingdomPolitySource.VanillaCounterparty) vanilla++;
				else if (p.Lifecycle != KingdomPolityLifecycle.Ended) liveExternal++;
				if (p.Source == KingdomPolitySource.VanillaCounterparty)
				{
					if (!Text(p.ExternalCounterpartyKey, true))
						return Fail("vanilla counterparty lacks bounded key", out Failure);
				}
				else if (!string.IsNullOrEmpty(p.ExternalCounterpartyKey))
					return Fail("owned polity carries vanilla counterparty key", out Failure);
				if (!string.IsNullOrEmpty(p.ProjectedFactionId) &&
					!SemanticId(p.ProjectedFactionId))
					return Fail("projected faction id is not semantic", out Failure);
				bool hasProfile = !string.IsNullOrEmpty(p.ProfileId) || p.ProfileRevision != 0;
				if (hasProfile && (!TypedId(p.ProfileId, "taf:polity-profile:") ||
					p.ProfileRevision < 1))
					return Fail("polity profile pointer is incomplete", out Failure);
				if (!hasProfile && p.Lifecycle != KingdomPolityLifecycle.Latent)
					return Fail("non-latent polity lacks a profile", out Failure);
				if ((p.Lifecycle == KingdomPolityLifecycle.Ended) != (p.EndedTick > 0L))
					return Fail("polity end evidence is incoherent", out Failure);
			}
			if (current > 1 || liveExternal > 1 || vanilla > 2)
				return Fail("polity source capacity is exceeded", out Failure);
			if (L.IdentityBound && L.Polities.Count > 0 && current != 1)
				return Fail("bound populated ledger lacks one current polity", out Failure);
			if (!ValidateRelations(L.Relations, out Failure)) return false;
			return ValidateProfiles(L.Profiles, out Failure);
		}

		private static bool ValidateRelations(IList<KingdomPolityRelation> Values,
			out string Failure)
		{
			Failure = null;
			string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolityRelation r = Values[i];
				if (r == null || !TypedId(r.RelationId, "taf:relation:") ||
					!After(previous, r.RelationId) || !SemanticId(r.FromPolityId) ||
					!SemanticId(r.ToPolityId) || r.FromPolityId == r.ToPolityId ||
					!Defined((byte)r.Band, 6) || r.ChangedTick < 0L ||
					!SortedSemanticRefs(r.SourceRefs, MaxRefs, r.Band !=
						KingdomPolityRelationBand.Unspecified) || !ValidRelationProvenance(r))
					return Fail("relation record is invalid or noncanonical", out Failure);
				previous = r.RelationId;
				for (int j = 0; j < i; j++)
					if (Values[j].FromPolityId == r.FromPolityId &&
						Values[j].ToPolityId == r.ToPolityId)
						return Fail("directional relation pair is duplicated", out Failure);
			}
			return true;
		}

		private static bool ValidateProfiles(IList<KingdomPolityProfileRevision> Values,
			out string Failure)
		{
			Failure = null;
			string previousId = null; int previousRevision = 0;
			for (int i = 0; i < Values.Count; i++)
			{
				KingdomPolityProfileRevision p = Values[i];
				if (p == null || !TypedId(p.ProfileId, "taf:polity-profile:") ||
					p.Revision < 1 || !ProfileAfter(previousId, previousRevision,
					p.ProfileId, p.Revision) || !SemanticId(p.PolityId) ||
					p.EffectiveTick < 0L || (p.RulesVersion !=
						KingdomPolityProfileRules.LegacyRulesVersion && p.RulesVersion !=
						KingdomPolityProfileRules.PriorExpressionRulesVersion &&
						p.RulesVersion != KingdomPolityProfileRules.RulesVersion) ||
					!SortedSemanticRefs(p.DerivedFromFactIds, MaxRefs, true) ||
					!Digest(p.FactsDigest) || p.TechnologyBand < 0 || p.TechnologyBand > 10 ||
					!SortedText(p.PracticeTags, 8, false) ||
					!SortedText(p.BodyKeys, MaxRefs, true) ||
					!SortedText(p.RoleKeys, MaxRefs, true) ||
					!SortedText(p.GearKeys, MaxRefs, false) || !ValidLoadout(p.Loadout) ||
					!ValidExpressionCues(p))
					return Fail("profile revision is invalid or noncanonical", out Failure);
				previousId = p.ProfileId; previousRevision = p.Revision;
			}
			return true;
		}

		private static bool ValidExpressionCues(KingdomPolityProfileRevision Profile)
		{
			IList<KingdomPolityExpressionCue> values = Profile.ExpressionCues;
			if (values == null || values.Count > KingdomPolityProfileExpressionCatalogue.MaxCues)
				return false;
			if (Profile.RulesVersion == KingdomPolityProfileRules.LegacyRulesVersion)
				return values.Count == 0;
			KingdomPolityExpressionKind first = KingdomPolityExpressionKind.None;
			bool independent = false;
			for (int i = 0; i < values.Count; i++)
			{
				if (!KingdomPolityProfileExpressionCatalogue.ValidCue(values[i]) ||
					(Profile.RulesVersion == KingdomPolityProfileRules.RulesVersion &&
					 !KingdomPolityProfileExpressionCatalogue.CausallyAdmitted(values[i])) ||
					(i > 0 && KingdomPolityProfileExpressionCatalogue.Compare(
						values[i - 1], values[i]) >= 0)) return false;
				if (first == KingdomPolityExpressionKind.None) first = values[i].Kind;
				else if (values[i].Kind != first) independent = true;
			}
			return independent;
		}

		private static bool ValidLoadout(KingdomPolityLoadoutPolicy P)
		{
			if (P == null || !Defined((byte)P.Kind, 3) ||
				P.Kind == KingdomPolityLoadoutPolicyKind.None || P.ExpectedValueBudget < 0 ||
				P.ExpectedValueBudget > MaxValueBudget ||
				!SortedText(P.ExcludedKeys, MaxRefs, false) ||
				!SortedText(P.SelectedKeys, MaxRefs, false)) return false;
			return P.Kind != KingdomPolityLoadoutPolicyKind.StockPreserve ||
				P.SelectedKeys.Count == 0;
		}

		private static bool ProfileAfter(string PreviousId, int PreviousRevision,
			string Id, int Revision)
		{
			if (PreviousId == null) return true;
			int compare = string.CompareOrdinal(PreviousId, Id);
			return compare < 0 || (compare == 0 && PreviousRevision < Revision);
		}

		internal static bool After(string Previous, string Current)
		{
			return Previous == null || string.CompareOrdinal(Previous, Current) < 0;
		}

		internal static bool SortedSemanticRefs(IList<string> Values, int Maximum, bool Required)
		{
			if (!Count(Values, Maximum) || (Required && Values.Count == 0)) return false;
			string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				if (!SemanticId(Values[i]) || !After(previous, Values[i])) return false;
				previous = Values[i];
			}
			return true;
		}

		internal static bool SortedText(IList<string> Values, int Maximum, bool Required)
		{
			if (!Count(Values, Maximum) || (Required && Values.Count == 0)) return false;
			string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				if (!Text(Values[i], true) || !After(previous, Values[i])) return false;
				previous = Values[i];
			}
			return true;
		}
	}
}
