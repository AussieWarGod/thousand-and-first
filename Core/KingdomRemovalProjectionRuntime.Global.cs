using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomRemovalProjectionRuntime
	{
		internal static bool TryInspectGlobalStates(KingdomSystem System,
			KingdomRealmRemovalFinalPlan Plan,
			out List<string> Rows, out string Failure)
		{
			Rows = new List<string>(); Failure = null;
			if (The.Game == null || System == null || Plan == null
				|| string.IsNullOrEmpty(System.RealmId))
				return Fail("global retirement state is unavailable", out Failure);
			Plan.RealmId = System.RealmId;
			if (!CollectStringKeys(System, Plan, Rows, out Failure)) return false;
			foreach (KeyValuePair<string, object> row in The.Game.ObjectGameState)
			{
				KingdomRemovalGlobalDisposition disposition =
					KingdomRemovalCoverage.GlobalDisposition(row.Key);
				if (disposition == KingdomRemovalGlobalDisposition.Unknown
					|| disposition == KingdomRemovalGlobalDisposition.Preserve) continue;
				if (disposition == KingdomRemovalGlobalDisposition.EmptyOnly)
				{
					if (row.Value != null
						&& row.Value.GetType().Name != "KingdomInheritanceState")
						return Fail("inheritance singleton key belongs to another value", out Failure);
					if (row.Value is KingdomInheritanceState inheritance
						&& inheritance.Phase != KingdomInheritancePhase.Empty)
						return Fail("value-bearing inheritance registry must reach empty state or be preserved before removal",
							out Failure);
					Plan.EmptyObjectStates.Add(row.Key);
					Rows.Add("empty-object\u001f" + row.Key); continue;
				}
				return Fail("object-state disposition cannot be executed safely: " + row.Key,
					out Failure);
			}
			Rows.Sort(StringComparer.Ordinal);
			return true;
		}

		internal static bool TryRemoveGlobalStates(KingdomSystem System,
			KingdomRealmRemovalFinalPlan Plan,
			out string Failure)
		{
			Failure = null;
			KingdomRealmRemovalFinalPlan current = new KingdomRealmRemovalFinalPlan();
			if (!TryInspectGlobalStates(System, current, out List<string> _, out Failure)
				|| !SameKeys(Plan.StringStates, current.StringStates)
				|| !SameKeys(Plan.EmptyObjectStates, current.EmptyObjectStates)
				|| !SameMap(Plan.HostedAuthorityStates, current.HostedAuthorityStates)
				|| !SameMap(Plan.HostedDepartureStates, current.HostedDepartureStates))
				return Fail(Failure ?? "owned global keys changed after preview", out Failure);
			foreach (KeyValuePair<string, string> row in Plan.HostedAuthorityStates)
				if (The.Game.GetStringGameState(row.Key, null) != row.Value)
					return Fail("hosted authority changed after exact preview", out Failure);
			foreach (KeyValuePair<string, string> row in Plan.HostedDepartureStates)
				if (The.Game.GetStringGameState(row.Key, null) != row.Value)
					return Fail("hosted departure changed after exact preview", out Failure);
			RemoveKeys(The.Game.StringGameState, Plan.StringStates);
			RemoveKeys(The.Game.ObjectGameState, Plan.EmptyObjectStates);
			foreach (KeyValuePair<string, string> row in Plan.HostedDepartureStates)
				The.Game.RemoveStringGameState(row.Key);
			foreach (KeyValuePair<string, string> row in Plan.HostedAuthorityStates)
				The.Game.RemoveStringGameState(row.Key);
			KingdomRealmRemovalFinalPlan empty = new KingdomRealmRemovalFinalPlan();
			return TryInspectGlobalStates(System, empty, out List<string> rows, out Failure)
				&& (rows.Count == 0 || Fail("TAF-owned global state remains", out Failure));
		}

		private static bool CollectStringKeys(KingdomSystem System,
			KingdomRealmRemovalFinalPlan Plan, List<string> Rows, out string Failure)
		{
			Failure = null;
			foreach (KeyValuePair<string, string> row in The.Game.StringGameState)
			{
				KingdomRemovalGlobalDisposition disposition =
					KingdomRemovalCoverage.GlobalDisposition(row.Key);
				if (disposition == KingdomRemovalGlobalDisposition.Unknown
					|| disposition == KingdomRemovalGlobalDisposition.Preserve
					|| disposition == KingdomRemovalGlobalDisposition.TerminalMarkerCut) continue;
				if (disposition != KingdomRemovalGlobalDisposition.ExactCurrentRealmClear)
					return Fail("string-state disposition cannot be executed safely: " + row.Key,
						out Failure);
				if (string.IsNullOrEmpty(row.Value))
				{
					Plan.StringStates.Add(row.Key); Rows.Add("empty-string\u001f" + row.Key); continue;
				}
				if (Array.IndexOf(KingdomRemovalCoverage.HostedArcologyAuthorityStates,
					row.Key) >= 0)
				{
					if (!KingdomHostedArcologyReceiptCodec.TryDecodeAuthority(row.Value,
						out KingdomHostedArcologyAuthority authority))
						return Fail("hosted authority slot is malformed and cannot be classified",
							out Failure);
					if (authority.RealmId != System.RealmId) continue;
					Plan.HostedAuthorityStates[row.Key] = row.Value;
					Rows.Add("hosted-authority\u001f" + row.Key + "\u001f" + authority.ZoneId
						+ "\u001f" + KingdomRetirementDigestRules.Evidence(
							"hosted-authority-wire-v1", new List<string> { row.Value }));
					continue;
				}
				if (!KingdomHostedDepartureCodec.TryDecode(row.Value,
					out KingdomHostedDepartureState departure)
					|| !KingdomHostedDepartureRules.SlotKeyMatches(row.Key, departure))
					return Fail("hosted departure slot is malformed and cannot be classified",
						out Failure);
				if (departure.RealmId != System.RealmId) continue;
				Plan.HostedDepartureStates[row.Key] = row.Value;
				Rows.Add("hosted-departure\u001f" + row.Key + "\u001f"
					+ departure.ExteriorZoneId + "\u001f"
					+ KingdomRetirementDigestRules.Evidence("hosted-departure-wire-v1",
						new List<string> { row.Value }));
			}
			Plan.StringStates.Sort(StringComparer.Ordinal); return true;
		}

		internal static bool TryInspectSystems(KingdomSystem System,
			out List<IGameSystem> Systems, out List<string> Rows, out string Failure)
		{
			Systems = new List<IGameSystem>(); Rows = new List<string>(); Failure = null;
			if (The.Game?.Systems == null || System == null)
				return Fail("native game-system registry is unavailable", out Failure);
			int carrier = 0;
			HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < The.Game.Systems.Count; i++)
			{
				IGameSystem candidate = The.Game.Systems[i];
				string name = candidate?.GetType().Name;
				if (!KingdomRemovalCoverage.IsCustomSystem(name)) continue;
				if (name == "KingdomSystem")
				{
					if (!ReferenceEquals(candidate, System))
						return Fail("a second realm-system carrier is registered", out Failure);
					carrier++; continue;
				}
				if (!identities.Add(name))
					return Fail("custom auxiliary system identity is duplicated: " + name,
						out Failure);
				Systems.Add(candidate); Rows.Add(name);
			}
			if (carrier != 1) return Fail("the exact realm-system carrier is absent", out Failure);
			Rows.Sort(StringComparer.Ordinal);
			return true;
		}

		internal static bool TryRemoveAuxiliarySystems(KingdomSystem System,
			KingdomRealmRemovalFinalPlan Plan,
			out int Removed, out string Failure)
		{
			Removed = 0;
			if (!TryInspectSystems(System, out List<IGameSystem> systems,
				out List<string> currentRows, out Failure)) return false;
			if (Plan == null || !SameSystemReferences(Plan.Systems, systems))
				return Fail("auxiliary system subset changed after its exact preview", out Failure);
			for (int i = 0; i < systems.Count; i++)
			{
				IGameSystem candidate = systems[i];
				try
				{
					The.Game.RemoveSystem(candidate);
					if (The.Game.Systems.Contains(candidate))
						return Fail("auxiliary system remained registered after callback", out Failure);
					Removed++;
				}
				catch (Exception ex)
				{
					if (The.Game.Systems.Contains(candidate))
						return Fail("auxiliary callback failed before native registry removal: "
							+ ex.Message, out Failure);
					Removed++;
				}
			}
			return TryInspectSystems(System, out systems, out List<string> remaining, out Failure)
				&& (remaining.Count == 0 || Fail("custom auxiliary systems remain", out Failure));
		}

		private static bool SameSystemReferences(IList<IGameSystem> Expected,
			IList<IGameSystem> Actual)
		{
			if (Expected == null || Actual == null || Expected.Count != Actual.Count) return false;
			HashSet<IGameSystem> seen = new HashSet<IGameSystem>(Expected);
			return seen.Count == Expected.Count && seen.SetEquals(Actual);
		}

		private static void RemoveKeys<T>(Dictionary<string, T> Source, IList<string> Keys)
		{
			for (int i = 0; i < (Keys?.Count ?? 0); i++) Source.Remove(Keys[i]);
		}

		private static bool SameKeys(List<string> A, List<string> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++) if (A[i] != B[i]) return false;
			return true;
		}

		private static bool SameMap(Dictionary<string, string> A,
			Dictionary<string, string> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			foreach (KeyValuePair<string, string> row in A)
				if (!B.TryGetValue(row.Key, out string value) || value != row.Value) return false;
			return true;
		}
	}
}
