using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Derives bounded ambient causes only from persisted city readings. These are
	/// historical observations, not claims that an unloaded zone still has the same state.</summary>
	public static class KingdomPolityEndpointObservationRules
	{
		public static bool TryGuard(string RealmId, string SettlementId,
			IList<string> ZoneIds, IList<long> LastReadTicks, IList<int> Defences,
			out KingdomPolityEndpointObservation Observation, out string Failure)
		{
			Observation = null; Failure = null;
			if (!Identity(RealmId, SettlementId) || !SameCount(ZoneIds, LastReadTicks, Defences))
				return Fail("guard observation columns are invalid", out Failure);
			int selected = -1;
			for (int i = 0; i < ZoneIds.Count; i++)
			{
				if (!Row(ZoneIds[i], LastReadTicks[i]) || Defences[i] < 0)
					return Fail("guard observation row is invalid", out Failure);
				if (LastReadTicks[i] == 0L || Defences[i] == 0) continue;
				if (selected < 0 || string.CompareOrdinal(ZoneIds[i], ZoneIds[selected]) < 0)
					selected = i;
			}
			if (selected < 0) return true;
			string tick = LastReadTicks[selected].ToString(CultureInfo.InvariantCulture);
			string defence = Defences[selected].ToString(CultureInfo.InvariantCulture);
			Observation = new KingdomPolityEndpointObservation
			{
				LocusRef = Id("taf:locus:witnessed-watch:v1:", "watch-locus",
					RealmId, SettlementId, ZoneIds[selected]),
				CauseRef = Id("taf:fact:witnessed:", "watch-reading",
					RealmId, SettlementId, ZoneIds[selected], tick, defence),
				Detail = "The last witnessed watch mustered " + defence + " defense."
			};
			return Valid(Observation) || Fail("guard observation is noncanonical", out Failure);
		}

		public static bool TryCondition(string RealmId, string SettlementId,
			IList<string> ZoneIds, IList<long> LastReadTicks, IList<int> WorkIds,
			IList<string> WorkZoneIds, IList<string> DesignKeys, IList<int> Conditions,
			IList<long> RanThroughTicks, out KingdomPolityEndpointObservation Observation,
			out string Failure)
		{
			Observation = null; Failure = null;
			if (!Identity(RealmId, SettlementId) || !SameCount(ZoneIds, LastReadTicks) ||
				!SameCount(WorkIds, WorkZoneIds, DesignKeys, Conditions, RanThroughTicks))
				return Fail("condition observation columns are invalid", out Failure);
			Dictionary<string, long> witnessed = new Dictionary<string, long>(StringComparer.Ordinal);
			for (int i = 0; i < ZoneIds.Count; i++)
			{
				if (!Row(ZoneIds[i], LastReadTicks[i]) || witnessed.ContainsKey(ZoneIds[i]))
					return Fail("condition zone observation is invalid or duplicated", out Failure);
				witnessed.Add(ZoneIds[i], LastReadTicks[i]);
			}
			int selected = -1;
			for (int i = 0; i < WorkIds.Count; i++)
			{
				if (WorkIds[i] <= 0 || !KingdomPolityAmbientTransactionRules.SafeText(
					WorkZoneIds[i], true) || !KingdomPolityAmbientTransactionRules.SafeText(
					DesignKeys[i], true) || Conditions[i] < 0 || Conditions[i] > 100 ||
					RanThroughTicks[i] < 0L || !witnessed.TryGetValue(WorkZoneIds[i], out long read))
					return Fail("condition work observation is invalid", out Failure);
				if (read == 0L) continue;
				if (selected < 0 || CompareWork(i, selected, WorkZoneIds, WorkIds, DesignKeys) < 0)
					selected = i;
			}
			if (selected < 0) return true;
			long witnessedTick = witnessed[WorkZoneIds[selected]];
			string work = WorkIds[selected].ToString(CultureInfo.InvariantCulture);
			string condition = Conditions[selected].ToString(CultureInfo.InvariantCulture);
			string readTick = witnessedTick.ToString(CultureInfo.InvariantCulture);
			string ranTick = RanThroughTicks[selected].ToString(CultureInfo.InvariantCulture);
			Observation = new KingdomPolityEndpointObservation
			{
				LocusRef = Id("taf:locus:site-condition:v1:", "work-locus", RealmId,
					SettlementId, WorkZoneIds[selected], work),
				CauseRef = Id("taf:fact:route-condition:", "site-reading", RealmId,
					SettlementId, WorkZoneIds[selected], work, DesignKeys[selected], condition,
					readTick, ranTick),
				Detail = Conditions[selected] == 100
					? "A patrol last found one local work sound at 100 parts in a hundred."
					: "A patrol last found one local work worn to " + condition +
						" parts in a hundred."
			};
			return Valid(Observation) || Fail("condition observation is noncanonical", out Failure);
		}

		private static bool Identity(string RealmId, string SettlementId)
		{
			return KingdomPolityRules.TypedId(RealmId, "taf:realm:") &&
				KingdomPolityRules.TypedId(SettlementId, "taf:settlement:v1:");
		}

		private static bool Row(string ZoneId, long Tick)
		{
			return KingdomPolityAmbientTransactionRules.SafeText(ZoneId, true) && Tick >= 0L;
		}

		private static bool Valid(KingdomPolityEndpointObservation Value)
		{
			return Value != null && KingdomPolityRules.SemanticId(Value.CauseRef) &&
				KingdomPolityRules.SemanticId(Value.LocusRef) &&
				KingdomPolityAmbientTransactionRules.SafeText(Value.Detail, true);
		}

		private static int CompareWork(int A, int B, IList<string> Zones, IList<int> WorkIds,
			IList<string> Designs)
		{
			int c = string.CompareOrdinal(Zones[A], Zones[B]);
			if (c != 0) return c;
			c = WorkIds[A].CompareTo(WorkIds[B]);
			return c != 0 ? c : string.CompareOrdinal(Designs[A], Designs[B]);
		}

		private static bool SameCount<TA, TB>(IList<TA> A, IList<TB> B)
		{
			return A != null && B != null && A.Count == B.Count;
		}

		private static bool SameCount<TA, TB, TC>(IList<TA> A, IList<TB> B, IList<TC> C)
		{
			return SameCount(A, B) && C != null && C.Count == A.Count;
		}

		private static bool SameCount<TA, TB, TC, TD, TE>(IList<TA> A, IList<TB> B,
			IList<TC> C, IList<TD> D, IList<TE> E)
		{
			return A != null && B != null && C != null && D != null && E != null &&
				A.Count == B.Count && A.Count == C.Count && A.Count == D.Count &&
				A.Count == E.Count;
		}

		private static string Id(string Prefix, string Kind, params string[] Values)
		{
			string[] all = new string[Values.Length + 1]; all[0] = Kind;
			for (int i = 0; i < Values.Length; i++) all[i + 1] = Values[i] ?? "";
			return KingdomPolityRules.ActivationId(Prefix,
				"polity-endpoint-observation-v1", all);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}

	public sealed class KingdomPolityEndpointObservation
	{
		public string CauseRef;
		public string LocusRef;
		public string Detail;
	}
}
