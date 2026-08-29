using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		public const string FoundingHeartTerminalProperty = "r_TAF_FoundingHeartTerminal";
		public const string FoundingHeartFinalRootPrefix = "r_TAF_FoundingHeartFinalRoot:";
		public const string FoundingHeartTerminalFailureProperty = "r_TAF_FoundingHeartTerminalFailure";

		private static string FoundingHeartFinalId(KingdomFoundingHeartPlan Plan)
		{
			return KingdomFoundingHeartRules.Valid(Plan)
				? KingdomFoundingHeartRules.StableId(Plan.TransactionId, Plan.ZoneId, "final") : null;
		}

		private static string FoundingHeartFinalRootKey(KingdomFoundingHeartPlan Plan)
		{
			string id = FoundingHeartFinalId(Plan);
			return id == null ? null : FoundingHeartFinalRootPrefix + id;
		}

		private static bool RootFoundingHeartFinal(KingdomFoundingHeartPlan Plan, GameObject Final)
		{
			string key = FoundingHeartFinalRootKey(Plan);
			if (The.Game == null || key == null || !GameObject.Validate(Final)
				|| Final.IDIfAssigned != FoundingHeartFinalId(Plan)) return false;
			if (The.Game.ObjectGameState.TryGetValue(key, out object prior)
				&& !object.ReferenceEquals(prior, Final)) return false;
			try { The.Game.SetObjectGameState(key, Final); }
			catch { return false; }
			return ExactFoundingHeartFinalObjectGameState(Plan, Final, true)
				&& FindGlobalFoundingHeartId(Final.IDIfAssigned, out GameObject exact,
					out bool graveyard) == KingdomPhysicalLookupState.Exact
				&& !graveyard && object.ReferenceEquals(exact, Final);
		}

		private static bool TryFoundingHeartFinalRoot(KingdomFoundingHeartPlan Plan,
			out GameObject Final)
		{
			Final = null;
			string key = FoundingHeartFinalRootKey(Plan);
			if (The.Game == null || key == null
				|| !The.Game.ObjectGameState.TryGetValue(key, out object value)) return false;
			Final = value as GameObject;
			return GameObject.Validate(Final) && Final.IDIfAssigned == FoundingHeartFinalId(Plan);
		}

		private static bool RetireFoundingHeartFinalRoot(KingdomFoundingHeartPlan Plan,
			GameObject Final)
		{
			string key = FoundingHeartFinalRootKey(Plan);
			if (The.Game == null || key == null) return false;
			if (The.Game.ObjectGameState.TryGetValue(key, out object value))
			{
				if (!object.ReferenceEquals(value, Final)) return false;
				The.Game.ObjectGameState.Remove(key);
			}
			return ExactFoundingHeartFinalObjectGameState(Plan, Final, false);
		}

		private static bool ExactFoundingHeartFinalObjectGameState(
			KingdomFoundingHeartPlan Plan, GameObject Expected, bool Present)
		{
			string id = FoundingHeartFinalId(Plan);
			string key = FoundingHeartFinalRootKey(Plan);
			if (The.Game?.ObjectGameState == null || id == null || key == null) return false;
			int matches = 0;
			int visited = 0;
			try
			{
				foreach (KeyValuePair<string, object> row in The.Game.ObjectGameState)
				{
					GameObject root = row.Value as GameObject;
					if (root == null) continue;
					List<GameObject> pending = new List<GameObject> { root };
					HashSet<GameObject> expanded = new HashSet<GameObject>();
					while (pending.Count > 0)
					{
						GameObject item = pending[pending.Count - 1];
						pending.RemoveAt(pending.Count - 1);
						if (item == null) continue;
						if (++visited > MaximumFoundingHeartCustodyObjects) return false;
						if (item.IDIfAssigned == id)
						{
							matches++;
							if (row.Key != key || !object.ReferenceEquals(root, Expected)
								|| !object.ReferenceEquals(item, Expected)) return false;
						}
						if (!expanded.Add(item)) continue;
						List<GameObject> children = item.GetInventoryDirectAndEquipment();
						if (children != null) for (int i = 0; i < children.Count; i++)
							pending.Add(children[i]);
					}
				}
			}
			catch { return false; }
			return Present
				? matches == 1 && The.Game.ObjectGameState.TryGetValue(key, out object exact)
					&& object.ReferenceEquals(exact, Expected)
				: matches == 0 && !The.Game.ObjectGameState.ContainsKey(key);
		}

		private static bool FoundingHeartTerminalBinding(FoundingHeartContext Context,
			KingdomFoundingHeartTerminalPlan Terminal)
		{
			KingdomFoundingHeartPlan plan = Context?.Plan;
			return KingdomFoundingHeartRules.Complete(plan)
				&& KingdomFoundingHeartTerminalRules.Valid(Terminal)
				&& Terminal.TransactionId == plan.TransactionId
				&& Terminal.CompletionSeal == KingdomFoundingHeartRules.CompletionSeal(plan)
				&& Terminal.ZoneId == plan.ZoneId
				&& Terminal.PredecessorId == KingdomFoundingHeartRules.SlotId(plan,
					KingdomFoundingHeartRules.WorksSlot)
				&& Terminal.FinalId == FoundingHeartFinalId(plan)
				&& Terminal.Blueprint == Context.Stake.Blueprint
				&& Terminal.BuildKey == Context.Stake.BuildKey && Terminal.PlotId == plan.PlotId
				&& Terminal.X == Context.Architecture.MainWorldX
				&& Terminal.Y == Context.Architecture.MainWorldY;
		}

		private static bool PublishFoundingHeartTerminal(Zone Z, GameObject Final,
			FoundingHeartContext Context, KingdomFoundingHeartTerminalPlan Terminal,
			string Expected)
		{
			string encoded = KingdomFoundingHeartTerminalRules.Encode(Terminal);
			string finalExpected = Expected ?? encoded;
			if (Z == null || !GameObject.Validate(Final) || encoded == null
				|| !FoundingHeartTerminalBinding(Context, Terminal)
				|| Z.GetZoneProperty(FoundingHeartTerminalProperty, null) != Expected
				|| Final.GetStringProperty(FoundingHeartTerminalProperty) != finalExpected) return false;
			try { Z.SetZoneProperty(FoundingHeartTerminalProperty, encoded); }
			catch
			{
				if (Z.GetZoneProperty(FoundingHeartTerminalProperty, null) != encoded) return false;
			}
			try { Final.SetStringProperty(FoundingHeartTerminalProperty, encoded); }
			catch { return false; }
			return Z.GetZoneProperty(FoundingHeartTerminalProperty, null) == encoded
				&& Final.GetStringProperty(FoundingHeartTerminalProperty) == encoded;
		}

		private static bool TryReadFoundingHeartTerminal(Zone Z, FoundingHeartContext Context,
			out KingdomFoundingHeartTerminalPlan Terminal, out GameObject Final)
		{
			Terminal = null;
			Final = null;
			KingdomFoundingHeartPlan plan = Context?.Plan;
			string raw = Z?.GetZoneProperty(FoundingHeartTerminalProperty, null);
			if (!string.IsNullOrEmpty(Z?.GetZoneProperty(FoundingHeartTerminalFailureProperty, null)))
				return false;
			if (string.IsNullOrEmpty(raw))
			{
				if (!TryFoundingHeartFinalRoot(plan, out Final)) return false;
				raw = Final.GetStringProperty(FoundingHeartTerminalProperty);
				if (!KingdomFoundingHeartTerminalRules.TryDecode(raw, out Terminal)
					|| !FoundingHeartTerminalBinding(Context, Terminal)
					|| !ExactFoundingHeartFinalObjectGameState(plan, Final, true)) return false;
				try { Z.SetZoneProperty(FoundingHeartTerminalProperty, raw); }
				catch { return false; }
			}
			if (!KingdomFoundingHeartTerminalRules.TryDecode(raw, out Terminal)
				|| !FoundingHeartTerminalBinding(Context, Terminal)
				|| FindGlobalFoundingHeartId(Terminal.FinalId, out Final, out bool graveyard)
					!= KingdomPhysicalLookupState.Exact || graveyard) return false;
			if (!string.IsNullOrEmpty(Final.GetStringProperty(
				FoundingHeartTerminalFailureProperty))) return false;
			string mirror = Final.GetStringProperty(FoundingHeartTerminalProperty);
			if (mirror != raw)
			{
				if (!KingdomFoundingHeartTerminalRules.TryDecode(mirror, out var prior)
					|| !KingdomFoundingHeartTerminalRules.SameBinding(prior, Terminal)) return false;
				try { Final.SetStringProperty(FoundingHeartTerminalProperty, raw); }
				catch { return false; }
			}
			bool rooted = ExactFoundingHeartFinalObjectGameState(plan, Final, true);
			return Final.GetStringProperty(FoundingHeartTerminalProperty) == raw
				&& (Terminal.Phase == KingdomFoundingHeartTerminalPhase.EffectsSettled
						? rooted || ExactFoundingHeartFinalObjectGameState(plan, Final, false) : rooted);
		}

		private static bool QuarantineFoundingHeartTerminal(Zone Z, GameObject Final, string Failure)
		{
			string failure = string.IsNullOrEmpty(Failure) ? "terminal topology is ambiguous"
				: Failure.Length > 1024 ? Failure.Substring(0, 1024) : Failure;
			try { Final?.SetStringProperty(FoundingHeartTerminalFailureProperty, failure); }
			catch { }
			try { Z?.SetZoneProperty(FoundingHeartTerminalFailureProperty, failure); }
			catch { }
			return false;
		}
	}
}
