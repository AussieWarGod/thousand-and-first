using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomBountyRules
	{
		/// <summary>
		/// Persistent semantic lane for one notice. Qud object ids are decimal game-object ids, but
		/// folding is total for imported or hand-edited values too.
		/// </summary>
		public static string NoticeEventStream(string NoticeId)
		{
			StringBuilder builder = new StringBuilder(ScheduledNoticeStreamPrefix);
			if (string.IsNullOrEmpty(NoticeId))
			{
				builder.Append("unknown");
			}
			else
			{
				for (int i = 0; i < NoticeId.Length && builder.Length < 128; i++)
				{
					char c = NoticeId[i];
					if (c >= 'A' && c <= 'Z')
					{
						c = (char)(c + 32);
					}
					bool allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
						|| c == '.' || c == '_' || c == ':' || c == '-';
					builder.Append(allowed ? c : '-');
				}
			}
			return builder.ToString();
		}

		/// <summary>Stable caller key for keyed chronicle and durable output receipts.</summary>
		public static string NoticeEventId(string NoticeId)
		{
			const string prefix = "taf:bounty:event:v1:";
			StringBuilder builder = new StringBuilder(prefix);
			string source = string.IsNullOrEmpty(NoticeId) ? "unknown" : NoticeId;
			for (int i = 0; i < source.Length && builder.Length < 180; i++)
			{
				char c = source[i];
				if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
				bool allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
					|| c == '.' || c == '_' || c == ':' || c == '-';
				builder.Append(allowed ? c : '-');
			}
			return builder.ToString();
		}

		public static bool IsNoticeEventStream(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= 128
				&& Value.StartsWith(ScheduledNoticeStreamPrefix, System.StringComparison.Ordinal);
		}

		public static bool IsNoticeEventId(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= 180
				&& Value.StartsWith("taf:bounty:event:v1:", System.StringComparison.Ordinal);
		}

		/// <summary>Pure recovery law for one exact item move.</summary>
		public static BountyTransferAction TransferAction(BountyTransferPhase Phase,
			BountyTransferLocation Location)
		{
			if (Phase == BountyTransferPhase.Quarantined)
			{
				return BountyTransferAction.Wait;
			}
			if (Phase == BountyTransferPhase.None)
			{
				return BountyTransferAction.Bind;
			}
			if (Phase == BountyTransferPhase.Bound
				&& Location == BountyTransferLocation.SourceOnly)
			{
				return BountyTransferAction.Remove;
			}
			if (Phase == BountyTransferPhase.Arrived
				&& Location == BountyTransferLocation.DestinationOnly)
			{
				return BountyTransferAction.Confirm;
			}
			return BountyTransferAction.Quarantine;
		}

		/// <summary>
		/// Pure holder law for one exact fetch move witness.
		///
		/// The engine's Inventory.AddObject assigns the destination as the moved object's holder
		/// before it returns, so a witness taken after the add can only ever read the
		/// destination. A detached holder is the removal step's proof and only the removal
		/// step's: demanding it again after the add is unsatisfiable, and quarantined every
		/// carry that had in fact arrived, before it could be credited.
		/// </summary>
		public static bool TransferOwnerExact(BountyTransferPhase Phase,
			BountyTransferLocation Holder)
		{
			if (Phase == BountyTransferPhase.RemoveIntent)
			{
				return Holder == BountyTransferLocation.Detached;
			}
			if (Phase == BountyTransferPhase.AddIntent)
			{
				return Holder == BountyTransferLocation.DestinationOnly;
			}
			return false;
		}

		/// <summary>
		/// Pure row law for one exact subtraction: the observed rows are the captured rows with
		/// exactly one occurrence of the moved identity dropped, and every surviving row keeps
		/// its order, identity and count. A repeated or absent moved identity is refused, so an
		/// ambiguous list can never be read as a subtraction.
		/// </summary>
		public static bool TransferRowsMinus(string[] CapturedIds, int[] CapturedCounts,
			string[] ObservedIds, int[] ObservedCounts, string MovedId, int MovedUnits,
			out int RemovedIndex)
		{
			RemovedIndex = -1;
			if (CapturedIds == null || CapturedCounts == null || ObservedIds == null
				|| ObservedCounts == null || string.IsNullOrEmpty(MovedId) || MovedUnits <= 0
				|| CapturedIds.Length != CapturedCounts.Length
				|| ObservedIds.Length != ObservedCounts.Length
				|| ObservedIds.Length != CapturedIds.Length - 1) return false;
			int found = -1;
			for (int i = 0; i < CapturedIds.Length; i++)
			{
				if (CapturedIds[i] != MovedId) continue;
				if (found >= 0) return false;
				found = i;
			}
			if (found < 0 || CapturedCounts[found] != MovedUnits) return false;
			int current = 0;
			for (int i = 0; i < CapturedIds.Length; i++)
			{
				if (i == found) continue;
				if (ObservedIds[current] != CapturedIds[i]
					|| ObservedCounts[current] != CapturedCounts[i]) return false;
				current++;
			}
			RemovedIndex = found;
			return true;
		}

	}
}
