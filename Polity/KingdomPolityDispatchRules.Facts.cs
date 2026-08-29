using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityDispatchRules
	{
		private sealed class DueFactParts
		{
			internal bool IsSeat;
			internal int Population;
			internal int Stage;
			internal int ShopTier;
			internal int StorageSpace;
			internal int EndpointCount;
			internal string Topology;
			internal string SourceDigest;
			internal string Cause;
			internal string Event;
		}

		private static string DueSourceDigest(KingdomPolityEndpointFacts E, int Count,
			KingdomPolityCohortPurpose Purpose, string Cause)
		{
			return DueSourceDigest(E.SettlementId, E.IsSeat, E.Population, E.Stage,
				E.ShopTier, E.KnownStorageSpace, Count, Purpose, Cause);
		}

		private static string DueSourceDigest(string SettlementId, bool IsSeat, int Population,
			int Stage, int ShopTier, int StorageSpace, int Count,
			KingdomPolityCohortPurpose Purpose, string Cause)
		{
			return KingdomPolityRules.ActivationDigest("polity-dispatch-due-source-v1",
				SettlementId, IsSeat ? "1" : "0",
				Population.ToString(CultureInfo.InvariantCulture),
				Stage.ToString(CultureInfo.InvariantCulture),
				ShopTier.ToString(CultureInfo.InvariantCulture),
				StorageSpace.ToString(CultureInfo.InvariantCulture),
				Count.ToString(CultureInfo.InvariantCulture),
				((byte)Purpose).ToString(CultureInfo.InvariantCulture), Cause);
		}

		private static string DueFacts(KingdomPolityEndpointFacts E, int Count,
			string Topology, string SourceDigest, string Cause, string Source)
		{
			return "due facts: seat=" + (E.IsSeat ? "yes" : "no") + ", population="
				+ E.Population.ToString(CultureInfo.InvariantCulture) + ", stage="
				+ E.Stage.ToString(CultureInfo.InvariantCulture) + ", shop-tier="
				+ E.ShopTier.ToString(CultureInfo.InvariantCulture) + ", storage-space="
				+ E.KnownStorageSpace.ToString(CultureInfo.InvariantCulture) + ", endpoints="
				+ Count.ToString(CultureInfo.InvariantCulture) + "; topology=" + Topology
				+ "; source-digest=" + SourceDigest + "; cause=" + Cause + "; event=" + Source;
		}

		private static bool TryReadDueFacts(string Value, out DueFactParts Facts)
		{
			Facts = null;
			if (!KingdomPolityRules.Text(Value, true)) return false;
			string[] marks = { "due facts: seat=", ", population=", ", stage=",
				", shop-tier=", ", storage-space=", ", endpoints=", "; topology=",
				"; source-digest=", "; cause=", "; event=" };
			string[] parts = Value.Split(marks, System.StringSplitOptions.None);
			if (parts.Length != 11 || parts[0].Length != 0
				|| parts[1] != "yes" && parts[1] != "no"
				|| !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture,
					out int population) || population < 0 || population > 10000
				|| !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture,
					out int stage) || stage < 0 || stage > 4
				|| !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture,
					out int shop) || shop < 0 || shop > 8
				|| !int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture,
					out int space) || space < 0
				|| !int.TryParse(parts[6], NumberStyles.None, CultureInfo.InvariantCulture,
					out int endpoints) || endpoints < 1 || endpoints > MaximumEndpoints
				|| !KingdomPolityRules.Digest(parts[7]) || !KingdomPolityRules.Digest(parts[8])
				|| !KingdomPolityRules.SemanticId(parts[9])
				|| !KingdomPolityRules.SemanticId(parts[10])) return false;
			Facts = new DueFactParts { IsSeat = parts[1] == "yes", Population = population,
				Stage = stage, ShopTier = shop, StorageSpace = space, EndpointCount = endpoints,
				Topology = parts[7], SourceDigest = parts[8], Cause = parts[9], Event = parts[10] };
			return true;
		}

		private static bool ExactEndpointRow(KingdomPolityDispatchState State,
			KingdomPolityDirectRecord Record, out DueFactParts Facts)
		{
			Facts = null;
			if (State == null || Record == null || !KingdomPolityRules.TypedId(
				Record.SettlementId, "taf:settlement:v1:") || !AmbientPurpose(Record.Purpose)
				|| !TryReadDueFacts(Record.EndpointVerb, out Facts)
				|| !EligibleFacts(Facts, Record.Purpose)
				|| Record.WindowOrdinal > (ulong)(long.MaxValue / PeriodTicks)
				|| Record.CauseTick != (long)Record.WindowOrdinal * PeriodTicks) return false;
			string digest = DueSourceDigest(Record.SettlementId, Facts.IsSeat, Facts.Population,
				Facts.Stage, Facts.ShopTier, Facts.StorageSpace, Facts.EndpointCount,
				Record.Purpose, Facts.Cause);
			string ordinal = Record.WindowOrdinal.ToString(CultureInfo.InvariantCulture);
			string token = ((byte)Record.Purpose).ToString(CultureInfo.InvariantCulture);
			string expectedEvent = Id("taf:event:polity-due:v1:", "event", State.RealmId,
				Record.SettlementId, ordinal, token, Facts.Cause, digest);
			string expectedCohort = Id("taf:cohort:polity-due:v1:", "cohort", State.RealmId,
				Record.SettlementId, ordinal, token, Facts.Cause, digest);
			return Facts.SourceDigest == digest && Facts.Event == expectedEvent
				&& Record.SourceRef == expectedCohort;
		}

		private static bool EligibleFacts(DueFactParts F, KingdomPolityCohortPurpose P)
		{
			switch (P) { case KingdomPolityCohortPurpose.Guard: return F.Population > 0;
			case KingdomPolityCohortPurpose.Patrol: return F.EndpointCount > 1 && F.Population > 1;
			case KingdomPolityCohortPurpose.Courier: return F.EndpointCount > 1;
			case KingdomPolityCohortPurpose.Trader: return F.ShopTier > 0;
			case KingdomPolityCohortPurpose.Migrant: return F.StorageSpace > 0;
			default: return false; }
		}
	}
}
