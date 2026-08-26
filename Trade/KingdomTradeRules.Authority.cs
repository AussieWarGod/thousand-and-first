using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		public static bool IdentityContainsSettlement(KingdomTradeBook Book, string SettlementId)
		{
			return BookUsable(Book) && Book.SettlementIds.Contains(SettlementId);
		}

		/// <summary>Captures immutable authority and topology immediately before a hostile callback.</summary>
		public static KingdomTradeAuthoritySeal CaptureAuthoritySeal(KingdomTradeBook Book,
			IList<string> ClaimedZones, IList<string> CityZones)
		{
			if (Book == null || ClaimedZones == null || CityZones == null) return null;
			try
			{
				return new KingdomTradeAuthoritySeal
				{
					BookBytes = KingdomTradeCodec.EncodePayload(Book),
					ClaimedZones = ClaimedZones,
					ClaimedRows = CopyStrings(ClaimedZones),
					CityZones = CityZones,
					CityRows = CopyStrings(CityZones)
				};
			}
			catch { return null; }
		}

		/// <summary>No callback may alter authority or topology beyond its one declared mutation.</summary>
		public static bool ExactAuthoritySeal(KingdomTradeBook Book,
			IList<string> ClaimedZones, IList<string> CityZones, KingdomTradeAuthoritySeal Seal)
		{
			if (Book == null || Seal == null || Seal.BookBytes == null
				|| !ReferenceEquals(ClaimedZones, Seal.ClaimedZones)
				|| !ReferenceEquals(CityZones, Seal.CityZones)
				|| !ExactStrings(ClaimedZones, Seal.ClaimedRows)
				|| !ExactStrings(CityZones, Seal.CityRows)) return false;
			byte[] current;
			try { current = KingdomTradeCodec.EncodePayload(Book); }
			catch { return false; }
			if (current.Length != Seal.BookBytes.Length) return false;
			for (int i = 0; i < current.Length; i++)
				if (current[i] != Seal.BookBytes[i]) return false;
			return true;
		}

		/// <summary>
		/// Captures every bounded mutable reference reachable through concrete lists, maps, arrays,
		/// and public persisted TAF fields. Values are proved separately by canonical graph bytes.
		/// </summary>
		public static bool TryCaptureExactReferenceSeal(IList<object> Roots,
			out KingdomTradeReferenceSeal Seal)
		{
			Seal = null;
			if (Roots == null || Roots.Count > 256) return false;
			try
			{
				List<object> rows = new List<object>();
				HashSet<object> expanded = new HashSet<object>(new ExactReferenceComparer());
				for (int i = 0; i < Roots.Count; i++)
					if (!CollectExactReferences(Roots[i], 0, rows, expanded)) return false;
				Seal = new KingdomTradeReferenceSeal { Rows = rows.ToArray() };
				return true;
			}
			catch { Seal = null; return false; }
		}

		public static bool ExactReferenceSeal(IList<object> Roots,
			KingdomTradeReferenceSeal Seal)
		{
			if (Seal?.Rows == null || !TryCaptureExactReferenceSeal(Roots,
				out KingdomTradeReferenceSeal current) || current.Rows.Length != Seal.Rows.Length)
				return false;
			for (int i = 0; i < Seal.Rows.Length; i++)
				if (!ReferenceEquals(Seal.Rows[i], current.Rows[i])) return false;
			return true;
		}

		private static bool CollectExactReferences(object Value, int Depth,
			List<object> Rows, HashSet<object> Expanded)
		{
			if (Rows == null || Expanded == null || Depth > MaxReferenceSealDepth
				|| Rows.Count >= MaxReferenceSealRows) return false;
			Rows.Add(Value);
			if (Value == null) return true;
			Type type = Value.GetType();
			if (type.IsValueType || Value is string) return false;
			if (!Expanded.Add(Value)) return true;

			if (Value is Array array)
			{
				if (array.Length > 1024) return false;
				Type element = type.GetElementType();
				if (element == null || element.IsValueType || element == typeof(string)) return true;
				for (int i = 0; i < array.Length; i++)
					if (!CollectExactReferences(array.GetValue(i), Depth + 1, Rows, Expanded))
						return false;
				return true;
			}
			if (Value is IDictionary dictionary)
			{
				if (dictionary.Count > 1024) return false;
				foreach (DictionaryEntry row in dictionary)
				{
					if (row.Key != null && !(row.Key is string) && !row.Key.GetType().IsValueType
						&& !CollectExactReferences(row.Key, Depth + 1, Rows, Expanded)) return false;
					if (row.Value != null && !(row.Value is string) && !row.Value.GetType().IsValueType
						&& !CollectExactReferences(row.Value, Depth + 1, Rows, Expanded)) return false;
				}
				return true;
			}
			if (Value is IList list)
			{
				if (list.Count > 1024) return false;
				for (int i = 0; i < list.Count; i++)
				{
					object row = list[i];
					if (row != null && !(row is string) && !row.GetType().IsValueType
						&& !CollectExactReferences(row, Depth + 1, Rows, Expanded)) return false;
				}
				return true;
			}
			if (type.Namespace == null
				|| !type.Namespace.StartsWith("ThousandAndFirst", StringComparison.Ordinal))
				return true;

			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
			Array.Sort(fields, (left, right) => string.CompareOrdinal(left.Name, right.Name));
			for (int i = 0; i < fields.Length; i++)
			{
				FieldInfo field = fields[i];
				if (field.IsStatic || field.FieldType.IsValueType || field.FieldType == typeof(string)
					|| field.GetCustomAttribute<NonSerializedAttribute>() != null) continue;
				if (!CollectExactReferences(field.GetValue(Value), Depth + 1, Rows, Expanded))
					return false;
			}
			return true;
		}

		/// <summary>Callers never receive a mutable alias to persisted manifest authority.</summary>
		public static KingdomTradeManifestState SnapshotManifest(KingdomTradeManifestState Manifest)
		{
			if (Manifest == null) return null;
			return new KingdomTradeManifestState
			{
				OperationSequence = Manifest.OperationSequence,
				OperationId = Manifest.OperationId,
				Id = Manifest.Id,
				OriginId = Manifest.OriginId,
				OriginName = Manifest.OriginName,
				DestinationId = Manifest.DestinationId,
				DestinationName = Manifest.DestinationName,
				OriginalDrams = Manifest.OriginalDrams,
				EscrowDrams = Manifest.EscrowDrams,
				LoadedTick = Manifest.LoadedTick,
				DeadlineTick = Manifest.DeadlineTick,
				TurnedBack = Manifest.TurnedBack,
				Status = Manifest.Status,
				Fault = Manifest.Fault
			};
		}

		private static string[] CopyStrings(IList<string> Values)
		{
			string[] copy = new string[Values.Count];
			for (int i = 0; i < copy.Length; i++) copy[i] = Values[i];
			return copy;
		}

		private static bool ExactStrings(IList<string> Current, string[] Expected)
		{
			if (Current == null || Expected == null || Current.Count != Expected.Length) return false;
			for (int i = 0; i < Expected.Length; i++)
				if (!string.Equals(Current[i], Expected[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ValidSettlementSet(List<string> Values)
		{
			if (Values == null || Values.Count < 1 || Values.Count > MaxSettlementIds) return false;
			for (int i = 0; i < Values.Count; i++)
				if (!ValidId(Values[i]) || (i > 0 && string.CompareOrdinal(Values[i - 1], Values[i]) >= 0))
					return false;
			return true;
		}

		private static bool TryExactSettlementSet(IEnumerable<string> Values,
			out List<string> Exact)
		{
			Exact = new List<string>();
			if (Values == null) return false;
			try
			{
				foreach (string id in Values)
				{
					if (!ValidId(id) || Exact.Contains(id) || Exact.Count >= MaxSettlementIds)
						return false;
					Exact.Add(id);
				}
				Exact.Sort(StringComparer.Ordinal);
				return ValidSettlementSet(Exact);
			}
			catch
			{
				Exact = null;
				return false;
			}
		}

		private static bool TryExactSettlementSet(List<string> Values,
			out List<string> Exact)
		{
			Exact = new List<string>();
			if (Values == null || Values.Count < 1 || Values.Count > MaxSettlementIds)
				return false;
			for (int i = 0; i < Values.Count; i++)
			{
				string id = Values[i];
				if (!ValidId(id) || Exact.Contains(id)) return false;
				Exact.Add(id);
			}
			Exact.Sort(StringComparer.Ordinal);
			return ValidSettlementSet(Exact);
		}

		private static bool TryExactSettlementSet(string[] Values,
			out List<string> Exact)
		{
			Exact = new List<string>();
			if (Values == null || Values.Length < 1 || Values.Length > MaxSettlementIds)
				return false;
			for (int i = 0; i < Values.Length; i++)
			{
				string id = Values[i];
				if (!ValidId(id) || Exact.Contains(id)) return false;
				Exact.Add(id);
			}
			Exact.Sort(StringComparer.Ordinal);
			return ValidSettlementSet(Exact);
		}

		private static bool ExactStringSet(List<string> Left, List<string> Right)
		{
			if (Left == null || Right == null || Left.Count != Right.Count) return false;
			for (int i = 0; i < Left.Count; i++)
				if (!string.Equals(Left[i], Right[i], StringComparison.Ordinal)) return false;
			return true;
		}

	}
}
