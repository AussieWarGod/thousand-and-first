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
		private static bool SinkClean(KingdomTradeSinkState State)
		{
			return State == KingdomTradeSinkState.Delivered
				|| State == KingdomTradeSinkState.Skipped;
		}

		private static bool ValidMaterialMarker(string OperationId, string Marker)
		{
			if (!ValidId(OperationId) || !ValidId(Marker)) return false;
			for (int i = 0; i < MaxMaterialOutputs; i++)
				if (string.Equals(Marker, MaterialMarker(OperationId, i), StringComparison.Ordinal)) return true;
			return false;
		}

		public static bool ValidId(string Value)
		{
			return !string.IsNullOrWhiteSpace(Value) && Value.Length <= MaxIdChars;
		}

		public static bool ValidName(string Value)
		{
			return !string.IsNullOrWhiteSpace(Value) && Value.Length <= MaxNameChars;
		}

		private static long PositiveCounter(long Value)
		{
			return Value <= 0L ? 1L : Value;
		}

		private static int Nonnegative(int Value)
		{
			return Value < 0 ? 0 : Value;
		}

		private static long Nonnegative(long Value)
		{
			return Value < 0L ? 0L : Value;
		}

		private static string Bound(string Value, int Maximum)
		{
			if (Value == null) return null;
			return Value.Length <= Maximum ? Value : Value.Substring(0, Maximum);
		}

		private static bool TooLong(string Value, int Maximum)
		{
			return Value != null && Value.Length > Maximum;
		}

		private static string AppendFault(string Existing, string Added)
		{
			if (!string.IsNullOrEmpty(Existing))
			{
				if (Existing.Length > MaxTextChars || string.IsNullOrEmpty(Added)
					|| Added.Length > MaxTextChars - Existing.Length - 2) return Existing;
				return Existing + "; " + Added;
			}
			return Bound(Added, MaxTextChars);
		}
	}
}
