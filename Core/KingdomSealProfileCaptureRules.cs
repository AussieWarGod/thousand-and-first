using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Projects one validated current polity profile into bounded cross-game seal evidence.
	/// No actor, faction, inventory, mutable object identity, or hashed identity correlator crosses
	/// this boundary.
	/// </summary>
	internal static class KingdomSealProfileCaptureRules
	{
		internal static bool TryCapture(KingdomPolityLedger Ledger, string RealmId,
			KingdomSealRecord Record, out long SourceRevision, out string Failure)
		{
			SourceRevision = -1L; Failure = null;
			if (Record == null)
			{
				Failure = "seal profile target is absent"; return false;
			}
			if (!TryProject(Ledger, RealmId, out KingdomPolityLegacySnapshot profile,
				out SourceRevision, out Failure)) return false;
			Apply(Record, profile); return true;
		}

		internal static bool StillMatches(KingdomPolityLedger Ledger, string RealmId,
			KingdomSealRecord Record, long SourceRevision, out string Failure)
		{
			Failure = null;
			if (Record == null || !TryProject(Ledger, RealmId,
				out KingdomPolityLegacySnapshot profile, out long revision, out Failure) ||
				revision != SourceRevision || !Exact(Record, profile))
			{
				Failure = Failure ?? "the committed polity profile changed during seal capture";
				return false;
			}
			return true;
		}

		private static bool TryProject(KingdomPolityLedger Ledger, string RealmId,
			out KingdomPolityLegacySnapshot Profile, out long Revision, out string Failure)
		{
			Profile = Unresolved(); Revision = -1L; Failure = null;
			if (!KingdomPolityRules.TypedId(RealmId, "taf:realm:v1:"))
			{
				Failure = "seal profile realm identity is invalid"; return false;
			}
			// Pre-polity saves remain institutionally readable. Once semantic rows exist, exact
			// current authority is mandatory; no stage/species inference fills a torn ledger.
			if (Ledger == null) return true;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure)) return false;
			Revision = Ledger.Revision;
			if (Ledger.IdentityBound && Ledger.RealmId != RealmId)
			{
				Failure = "polity authority is bound to a different realm"; return false;
			}
			KingdomPolityRecord current = null;
			for (int i = 0; i < Ledger.Polities.Count; i++)
			{
				if (Ledger.Polities[i].Source != KingdomPolitySource.CurrentRealm) continue;
				if (current != null)
				{
					Failure = "polity authority carries more than one current realm"; return false;
				}
				current = Ledger.Polities[i];
			}
			if (current == null)
			{
				if (!HasSemanticRows(Ledger)) return true;
				Failure = "populated polity authority has no current realm profile"; return false;
			}
			if (!Ledger.IdentityBound || current.PolityId != RealmId ||
				current.Lifecycle != KingdomPolityLifecycle.Active)
			{
				Failure = "current polity profile does not match the living realm"; return false;
			}
			KingdomPolityProfileRevision source = KingdomPolityAuthority.Profile(Ledger,
				current.ProfileId, current.ProfileRevision);
			KingdomPolityLegacySnapshot exact = Unresolved();
			if (!KingdomPolityProfileRules.TryCaptureLegacyProfile(exact, source, out Failure))
				return false;
			Profile = exact; return true;
		}

		private static KingdomPolityLegacySnapshot Unresolved()
		{
			return new KingdomPolityLegacySnapshot
			{
				ProfileSchema = KingdomPolityProfileRules.UnresolvedLegacyProfileSchema,
				TechnologyBand = 0, CanonicalBodyKeys = new List<string>(),
				SourceProfileDigest = "", ProfileProvenanceDigest = ""
			};
		}

		private static bool HasSemanticRows(KingdomPolityLedger L)
		{
			return L.Polities.Count != 0 || L.Relations.Count != 0 || L.Profiles.Count != 0 ||
				L.Routes.Count != 0 || L.Grievances.Count != 0 || L.Fronts.Count != 0 ||
				L.Cohorts.Count != 0 || L.NamedFigures.Count != 0 || L.Incidents.Count != 0 ||
				L.Projections.Count != 0 || L.Compactions.Count != 0 ||
				L.FoldedCompactionCount != 0L;
		}

		private static void Apply(KingdomSealRecord Target, KingdomPolityLegacySnapshot P)
		{
			Target.ProfileSchema = P.ProfileSchema; Target.TechnologyBand = P.TechnologyBand;
			Target.CanonicalBodyKeys = new List<string>(P.CanonicalBodyKeys);
			Target.SourceProfileDigest = P.SourceProfileDigest ?? "";
			Target.ProfileProvenanceDigest = P.ProfileProvenanceDigest ?? "";
		}

		private static bool Exact(KingdomSealRecord A, KingdomPolityLegacySnapshot B)
		{
			if (A.ProfileSchema != B.ProfileSchema || A.TechnologyBand != B.TechnologyBand ||
				A.SourceProfileDigest != (B.SourceProfileDigest ?? "") ||
				A.ProfileProvenanceDigest != (B.ProfileProvenanceDigest ?? "") ||
				A.CanonicalBodyKeys.Count != B.CanonicalBodyKeys.Count) return false;
			for (int i = 0; i < A.CanonicalBodyKeys.Count; i++)
				if (A.CanonicalBodyKeys[i] != B.CanonicalBodyKeys[i]) return false;
			return true;
		}
	}
}
