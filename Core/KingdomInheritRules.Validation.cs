using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritRules
	{
		internal static string FailureLine(KingdomInheritFault Fault)
		{
			switch (Fault)
			{
				case KingdomInheritFault.None:
					return "";
				case KingdomInheritFault.NullInput:
					return "the inherited street plan is missing";
				case KingdomInheritFault.RowCountMismatch:
					return "the inherited street plan has torn rows";
				case KingdomInheritFault.TooManyWorks:
					return "the inherited street plan carries too many works";
				case KingdomInheritFault.InvalidKey:
					return "the inherited street plan carries a malformed semantic key";
				case KingdomInheritFault.ConditionOutOfRange:
					return "the inherited street plan carries an impossible condition";
				case KingdomInheritFault.CoordinateOutOfRange:
					return "the inherited street plan carries an impossible old coordinate";
				case KingdomInheritFault.RelativeRange:
					return "the inherited street plan is too wide to normalize safely";
				case KingdomInheritFault.InvalidState:
					return "the inherited settlement state is unknown";
				case KingdomInheritFault.InterregnumRollOutOfRange:
					return "the inherited settlement carries an impossible interregnum draw";
				case KingdomInheritFault.ImpossibleFootprint:
					return "the inherited footprint cannot fit this eighty-by-twenty-five zone";
				case KingdomInheritFault.Overlap:
					return "two inherited works claim the same ground";
				case KingdomInheritFault.NoEntry:
					return "the inherited plan leaves no safe entry and cairn pair";
				default:
					return "the inherited street plan is malformed";
			}
		}

		private static Definition Find(string Key)
		{
			if (Key == null)
			{
				return null;
			}
			for (int i = 0; i < Definitions.Length; i++)
			{
				if (string.Equals(Definitions[i].Key, Key, StringComparison.Ordinal))
				{
					return Definitions[i];
				}
			}
			return null;
		}

		private static bool IsTafBlueprint(string Blueprint)
		{
			return Blueprint != null && Blueprint.StartsWith("r_Kingdom", StringComparison.Ordinal);
		}

		private static bool SourceCoordinate(int Coordinate)
		{
			return Coordinate >= -MaxSourceCoordinateMagnitude && Coordinate <= MaxSourceCoordinateMagnitude;
		}

		private static int Deduplicate(Candidate[] Candidates)
		{
			int write = 0;
			for (int i = 0; i < Candidates.Length; i++)
			{
				if (write > 0 && Candidates[write - 1].X == Candidates[i].X && Candidates[write - 1].Y == Candidates[i].Y)
				{
					continue;
				}
				Candidates[write++] = Candidates[i];
			}
			return write;
		}

	}
}
