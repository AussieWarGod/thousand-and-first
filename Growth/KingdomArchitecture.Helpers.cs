using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitecture
	{
		private static bool TryList(string Text, int Maximum, out List<string> Values)
		{
			Values = new List<string>();
			if (string.IsNullOrWhiteSpace(Text)) return false;
			string[] fields = Text.Split(',');
			if (fields.Length > Maximum) return false;
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < fields.Length; i++)
			{
				string field = fields[i].Trim();
				if (!ValidKey(field) || !seen.Add(field)) return false;
				Values.Add(field);
			}
			return true;
		}

		private static bool DirectBlueprintsExist(ArchitectureGlyphDraft Glyph)
		{
			return DirectBlueprintExists(Glyph.Ground) && DirectBlueprintExists(Glyph.Structure)
				&& DirectBlueprintExists(Glyph.Object);
		}

		private static bool DirectBlueprintExists(string Token)
		{
			return string.IsNullOrEmpty(Token) || Token[0] == '$' || BlueprintExists(Token);
		}

		private static bool BlueprintExists(string Blueprint)
		{
			try { return GameObjectFactory.Factory.HasBlueprint(Blueprint); }
			catch { return false; }
		}

		private static bool ValidKey(string Value)
		{
			return !string.IsNullOrEmpty(Value)
				&& Value.Length <= KingdomArchitectureRules.MaxKeyChars
				&& Value == Value.Trim() && !HasControl(Value);
		}

		private static bool ValidOptionalKey(string Value)
		{
			return string.IsNullOrEmpty(Value) || ValidKey(Value);
		}

		private static bool ValidBlueprint(string Value)
		{
			return !string.IsNullOrEmpty(Value)
				&& Value.Length <= KingdomArchitectureRules.MaxBlueprintChars
				&& Value == Value.Trim() && !HasControl(Value);
		}

		private static bool HasControl(string Value)
		{
			if (Value == null) return false;
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return true;
			return false;
		}

		private static string Fold(string Value)
		{
			return string.IsNullOrWhiteSpace(Value) ? null : Value.Trim().ToLowerInvariant();
		}

		private static List<string> OrderedKeys<T>(Dictionary<string, T> Values)
		{
			List<string> result = new List<string>(Values.Keys);
			result.Sort(StringComparer.Ordinal);
			return result;
		}

		private static List<ResolvedRecord> OrderedRecords(
			Dictionary<string, ResolvedRecord> Values)
		{
			List<ResolvedRecord> result = new List<ResolvedRecord>(Values.Values);
			result.Sort(delegate(ResolvedRecord left, ResolvedRecord right)
			{
				int order = string.CompareOrdinal(left.View.BuildKey, right.View.BuildKey);
				if (order != 0) return order;
				order = string.CompareOrdinal(Fold(left.View.TypeKey), Fold(right.View.TypeKey));
				if (order != 0) return order;
				order = left.View.LotSize.CompareTo(right.View.LotSize);
				if (order != 0) return order;
				order = string.CompareOrdinal(left.View.PlanKey, right.View.PlanKey);
				if (order != 0) return order;
				return string.CompareOrdinal(left.View.BindingKey, right.View.BindingKey);
			});
			return result;
		}

		private static bool ResolveFault(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}

		private static bool Fault(LoadState State, string Name, string Message)
		{
			AddFault(State, Name, Message);
			return false;
		}

		private static void AddFault(LoadState State, string Name, string Message)
		{
			if (State.FaultOverflow) return;
			Name = string.IsNullOrWhiteSpace(Name) ? "catalogue" : Name.Trim();
			Message = string.IsNullOrWhiteSpace(Message) ? "unknown fault" : Message.Trim();
			string identity = Name + "\n" + Message;
			if (!State.FaultKeys.Add(identity)) return;
			if (State.Faults.Count < MaxFaults)
			{
				State.Faults.Add(new KingdomArchitectureFault(Name, Message));
				return;
			}
			if (!State.FaultOverflow)
			{
				State.FaultOverflow = true;
				State.Faults[MaxFaults - 1] = new KingdomArchitectureFault("catalogue",
					"fault report exceeded " + MaxFaults + " entries; later faults were suppressed");
			}
		}
	}
}
