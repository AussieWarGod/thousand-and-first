using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitecture
	{
		// --- Raw merge helpers --------------------------------------------------------------

		private static RawPalette GetPalette(LoadState State, string Key, string Origin)
		{
			if (!ValidKey(Key))
			{
				AddFault(State, "palette", "missing or malformed Key at " + Origin);
				return null;
			}
			RawPalette result;
			if (State.RawPalettes.TryGetValue(Key, out result)) return result;
			if (State.RawPalettes.Count >= MaxTopRecords)
			{
				AddFault(State, "palettes", "record bound exceeded");
				return null;
			}
			result = new RawPalette(Key, Origin);
			State.RawPalettes.Add(Key, result);
			return result;
		}

		private static RawMap GetMap(LoadState State, string Key, string Origin)
		{
			if (!ValidKey(Key))
			{
				AddFault(State, "map", "missing or malformed Key at " + Origin);
				return null;
			}
			RawMap result;
			if (State.RawMaps.TryGetValue(Key, out result)) return result;
			if (State.RawMaps.Count >= MaxTopRecords)
			{
				AddFault(State, "maps", "record bound exceeded");
				return null;
			}
			result = new RawMap(Key, Origin);
			State.RawMaps.Add(Key, result);
			return result;
		}

		private static RawPlan GetPlan(LoadState State, string Key, string Origin)
		{
			if (!ValidKey(Key))
			{
				AddFault(State, "plan", "missing or malformed Key at " + Origin);
				return null;
			}
			RawPlan result;
			if (State.RawPlans.TryGetValue(Key, out result)) return result;
			if (State.RawPlans.Count >= MaxTopRecords)
			{
				AddFault(State, "plans", "record bound exceeded");
				return null;
			}
			result = new RawPlan(Key, Origin);
			State.RawPlans.Add(Key, result);
			return result;
		}

		private static RawBinding GetBinding(LoadState State, RawPlan Plan,
			string Key, string Origin)
		{
			if (!ValidKey(Key))
			{
				AddFault(State, "plan " + Plan.Key + " binding",
					"missing or malformed Key at " + Origin);
				return null;
			}
			RawBinding result;
			if (Plan.Bindings.TryGetValue(Key, out result)) return result;
			if (Plan.Bindings.Count >= KingdomArchitectureRules.MaxBindingsPerPlan)
			{
				Plan.Overflow = true;
				AddFault(State, "plan " + Plan.Key, "binding bound exceeded");
				return null;
			}
			result = new RawBinding(Key, Origin);
			Plan.Bindings.Add(Key, result);
			return result;
		}

		private static RawTier GetTier(LoadState State, RawBinding Binding,
			string Key, string Origin)
		{
			if (!ValidKey(Key))
			{
				AddFault(State, "binding " + Binding.Key + " tier",
					"missing or malformed Key at " + Origin);
				return null;
			}
			RawTier result;
			if (Binding.Tiers.TryGetValue(Key, out result)) return result;
			if (Binding.Tiers.Count >= KingdomArchitectureRules.MaxTiersPerBinding)
			{
				Binding.Overflow = true;
				AddFault(State, "binding " + Binding.Key, "tier bound exceeded");
				return null;
			}
			result = new RawTier(Key, Origin);
			Binding.Tiers.Add(Key, result);
			return result;
		}

		private static RawRecord GetRecord(LoadState State,
			Dictionary<string, RawRecord> Records, string Key, int Maximum,
			string Scope, string Origin)
		{
			if (!ValidKey(Key))
			{
				AddFault(State, Scope, "missing or malformed key at " + Origin);
				return null;
			}
			RawRecord result;
			if (Records.TryGetValue(Key, out result)) return result;
			if (Records.Count >= Maximum)
			{
				AddFault(State, Scope, "record bound exceeded");
				return null;
			}
			result = new RawRecord(Key, Origin);
			Records.Add(Key, result);
			return result;
		}

		private static void Set(LoadState State, RawRecord Record, string Name, string Value)
		{
			if (Value == null) return; // omission is inheritance across XML streams.
			if (Value.Length > MaxAttributeChars || HasControl(Value))
			{
				Record.Values.Remove(Name);
				Record.BadAttributes.Add(Name);
				return;
			}
			Record.BadAttributes.Remove(Name);
			Record.Values[Name] = Value;
		}

		private static void SetAlias(LoadState State, RawRecord Record, string Name,
			string Canonical, string Alias, string AliasName)
		{
			if (Canonical != null && Alias != null
				&& !string.Equals(Canonical, Alias, StringComparison.OrdinalIgnoreCase))
			{
				Record.Values.Remove(Name);
				Record.BadAttributes.Add(Name);
				return;
			}
			Set(State, Record, Name, Canonical ?? Alias);
		}

		private static void Unknown(LoadState State, XmlDataHelper Xml)
		{
			AddFault(State, "node " + Xml.Name, "unknown architecture node at " + Source(Xml));
			Skip(Xml);
		}

		private static void Skip(XmlDataHelper Xml)
		{
			Xml.HandleNodes(new Dictionary<string, Action<XmlDataHelper>>(),
				delegate(XmlDataHelper child) { Skip(child); });
		}

		private static string Source(XmlDataHelper Xml)
		{
			try { return Xml.GetSourcePoint(); }
			catch { return "an unknown source"; }
		}

	}
}
