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

	}
}
