using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Mutation-free result naming the exact lower landing a frozen delve will use.</summary>
	public sealed class KingdomDelveLinkIntent
	{
		public string HeadZoneId { get; internal set; }
		public string FootZoneId { get; internal set; }
		public int X { get; internal set; }
		public int Y { get; internal set; }
		public string SnapshotHash { get; internal set; }
		public string DownSlot { get; internal set; }
	}

	/// <summary>
	/// Engine-coupled paired-shaft transaction. Architecture owns one Down in the head map. This
	/// class proves an already-built claimed foot before debit, then creates exactly one reciprocal
	/// Up using Qud's native connection idiom. Named receipts make every callback boundary retryable.
	/// </summary>
	public static partial class KingdomDelveLink
	{
	}
}
