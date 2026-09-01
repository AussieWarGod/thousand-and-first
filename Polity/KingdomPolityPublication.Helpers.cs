using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		private const string ZeroDigest =
			"0000000000000000000000000000000000000000000000000000000000000000";

		public static KingdomPolityFigureOrigin FigureOriginFor(KingdomPolityRelationBand Band,
			bool Namesake)
		{
			if (Band == KingdomPolityRelationBand.Rival ||
				Band == KingdomPolityRelationBand.Hostile) return KingdomPolityFigureOrigin.Claimant;
			if (Namesake)
				return KingdomPolityFigureOrigin.Namesake;
			return KingdomPolityFigureOrigin.LegacyEnvoy;
		}

		public static KingdomPolityFigureOrigin LegacyFigureOriginFor(
			KingdomPolityRelationBand Band, bool Namesake, int InheritedState)
		{
			if (Namesake) return KingdomPolityFigureOrigin.Namesake;
			if (InheritedState == 0)
				return KingdomPolityFigureOrigin.Successor;
			return KingdomPolityFigureOrigin.LegacyEnvoy;
		}

		private static KingdomPolityProjectionReceipt FoundationProjection(string PolityId,
			string FactionId, string ProfileCommitment, long Tick, bool Committed)
		{
			string projectionId = ActivationId("taf:projection:faction:v1:",
				"polity-faction-projection-v1", PolityId, FactionId, ProfileCommitment);
			return new KingdomPolityProjectionReceipt
			{
				ProjectionId = projectionId, Kind = KingdomPolityProjectionKind.Faction,
				SourceRef = PolityId, Phase = Committed ? KingdomPolityProjectionPhase.Committed
					: KingdomPolityProjectionPhase.Prepared,
				ObjectIds = new List<string> { FactionId }, PriorDigest = ZeroDigest,
				AppliedDigest = ActivationDigest("polity-faction-applied-v1",
					PolityId, FactionId, ProfileCommitment), PreparedTick = Tick,
				CommittedTick = Committed ? Tick : 0L
			};
		}

		private static KingdomPolityRelation Relation(string From, string To,
			KingdomPolityRelationBand Band, string Cause, long Tick)
		{
			return new KingdomPolityRelation
			{
				RelationId = ActivationId("taf:relation:v1:", "polity-relation-id-v1", From, To),
				FromPolityId = From, ToPolityId = To, Band = Band,
				SourceRefs = new List<string> { Cause }, ChangedTick = Tick,
				FoundationState = KingdomPolityFoundationRelationState.Causal,
				InitialBand = Band, FoundationOriginalCauseRef = Cause
			};
		}

		private static KingdomPolityNamedFigureRecord LegacyFigure(string PolityId,
			KingdomPolityFoundationFacts Current, KingdomPolityLegacySnapshot Legacy,
			KingdomPolityRelationBand Band, string Cause)
		{
			bool namesake = Same(Current.FounderName, Legacy.FounderName) ||
				ActivationContains(Legacy.RollNames, Current.FounderName);
			KingdomPolityFigureOrigin origin = FigureOriginFor(Band, namesake);
			if (origin == KingdomPolityFigureOrigin.LegacyEnvoy)
				origin = LegacyFigureOriginFor(Band, namesake, Legacy.InheritedState);
			string baseName = string.IsNullOrEmpty(Legacy.FounderName)
				? (Legacy.RollNames.Count == 0 ? "the remembered one" : Legacy.RollNames[0])
				: Legacy.FounderName;
			string role; string name;
			switch (origin)
			{
			case KingdomPolityFigureOrigin.Claimant:
				role = "claimant"; name = "the Claimant of " + Legacy.RealmName; break;
			case KingdomPolityFigureOrigin.Namesake:
				role = "namesake"; name = baseName + " the Namesake"; break;
			case KingdomPolityFigureOrigin.Successor:
				role = "successor"; name = baseName + "'s Successor"; break;
			default:
				role = "envoy"; name = "the Envoy of " + Legacy.RealmName; break;
			}
			if (name.Length > 240) name = name.Substring(0, 240);
			return new KingdomPolityNamedFigureRecord
			{
				FigureId = ActivationId("taf:figure:legacy:v1:", "legacy-figure-id-v1",
					PolityId, Legacy.LegacyToken, role), PolityId = PolityId, DisplayName = name,
				RoleKey = role, Origin = origin, Phase = KingdomPolityFigurePhase.Active,
				CauseRef = Cause
			};
		}

		private static KingdomPolityRelationBand RelationshipFor(
			KingdomPolityFoundationFacts Current, KingdomPolityLegacySnapshot Legacy)
		{
			if (Same(Current.Style, Legacy.Style) || Same(Current.Vocation, Legacy.Vocation) ||
				ContainsCross(Current.OriginKeys, Legacy.OriginKeys) ||
				(!string.IsNullOrEmpty(Current.Creed) &&
					ActivationContains(Legacy.CreedKeys, Current.Creed)))
				return KingdomPolityRelationBand.Pact;
			return KingdomPolityRelationBand.Rival;
		}

		private static bool Same(string A, string B)
		{
			return !string.IsNullOrEmpty(A) && !string.IsNullOrEmpty(B) &&
				string.Equals(A.Trim(), B.Trim(), StringComparison.OrdinalIgnoreCase);
		}

		private static bool ContainsCross(IList<string> A, IList<string> B)
		{
			for (int i = 0; A != null && i < A.Count; i++)
				if (ActivationContains(B, A[i])) return true;
			return false;
		}

		private static bool ActivationContains(IList<string> Values, string Value)
		{
			for (int i = 0; Values != null && i < Values.Count; i++)
				if (string.Equals(Values[i], Value, StringComparison.OrdinalIgnoreCase)) return true;
			return false;
		}

		private static void CanonicalSort(KingdomPolityLedger L)
		{
			L.Polities.Sort((a, b) => string.CompareOrdinal(a.PolityId, b.PolityId));
			L.Relations.Sort((a, b) => string.CompareOrdinal(a.RelationId, b.RelationId));
			L.Profiles.Sort((a, b) => { int c = string.CompareOrdinal(a.ProfileId, b.ProfileId);
				return c != 0 ? c : a.Revision.CompareTo(b.Revision); });
			L.NamedFigures.Sort((a, b) => string.CompareOrdinal(a.FigureId, b.FigureId));
			L.Projections.Sort((a, b) => string.CompareOrdinal(a.ProjectionId, b.ProjectionId));
		}

		private static bool HasExternalPolity(KingdomPolityLedger L)
		{
			for (int i = 0; i < L.Polities.Count; i++)
				if (L.Polities[i].Source != KingdomPolitySource.CurrentRealm) return true;
			return false;
		}

		private static KingdomPolityPublicationResult BeginResult(KingdomPolityLedger L)
		{
			long revision = L == null ? -1L : L.Revision;
			return new KingdomPolityPublicationResult { Outcome = KingdomPolityCasOutcome.Refused,
				SourceRevision = revision, CommittedRevision = revision };
		}

		private static bool Increment(KingdomPolityLedger L, out string Failure)
		{
			Failure = null;
			if (L.Revision == long.MaxValue) return Fail("polity revision is exhausted", out Failure);
			L.Revision++; return true;
		}

		private static void Commit(KingdomPolityLedger Target, KingdomPolityLedger Candidate,
			KingdomPolityPublicationResult Result)
		{
			Target.CopyFrom(Candidate); Result.Outcome = KingdomPolityCasOutcome.Applied;
			Result.CommittedRevision = Candidate.Revision;
		}

		private static bool Conflict(KingdomPolityPublicationResult Result, out string Failure)
		{
			Result.Outcome = KingdomPolityCasOutcome.Conflict;
			Failure = "polity compare-and-swap revision conflict"; return false;
		}

		private static bool Refuse(KingdomPolityPublicationResult Result, string Reason,
			out string Failure)
		{
			Result.Outcome = KingdomPolityCasOutcome.Refused;
			Failure = string.IsNullOrEmpty(Reason) ? "polity publication was refused" : Reason;
			return false;
		}
	}
}
