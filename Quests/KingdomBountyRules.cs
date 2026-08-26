using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{

	/// <summary>
	/// The posted price: engine-free arithmetic and every hand-written line behind the notice a
	/// founder stakes at the heart (<see cref="KingdomBounty"/> is the engine-coupled shell).
	/// <para>
	/// Two things are load-bearing here. The first is that <b>nothing is escrowed</b>: this file
	/// never asks what the stores hold at posting time, because the price does not leave them
	/// until the work is done. The second is that <b>who attempts what is drawn, not chosen</b>
	/// &mdash; every draw goes through <see cref="CounterRandom"/> on a key built from the
	/// settlement, the notice's posted tick, and the pass ordinal, so the same notice on the same
	/// pass always finds the same reader on any reload.
	/// </para>
	/// <para>
	/// The weighting reads a settler's tastes and traits out of <see cref="KingdomCeremonyRules"/>
	/// rather than inventing a second vocabulary for the same people. See
	/// <see cref="PersonOrdinal"/> for why a person-keyed ceremony draw can never collide with a
	/// tick-keyed one.
	/// </para>
	/// </summary>
	public static partial class KingdomBountyRules
	{
		public const int MaxSavedTextChars = 4096;
		public const int MaxPaymentRows = 256;
		public const int MaxPaymentRowsChars = 8192;
		public const int MaxObjectIdChars = 256;
		public const int MaxCanonicalIntegerChars = 10;

		public static bool SinkSettled(BountySinkDisposition State)
		{
			return State == BountySinkDisposition.Delivered
				|| State == BountySinkDisposition.Skipped
				|| State == BountySinkDisposition.Lost;
		}

		/// <summary>An interrupted uninspectable call is explicit loss, never assumed delivery.</summary>
		public static BountySinkDisposition RecoverUninspectable(BountySinkDisposition State)
		{
			return State == BountySinkDisposition.Attempting
				? BountySinkDisposition.Lost : State;
		}

		/// <summary>Strict bounded canonical non-negative integer rows.</summary>
		public static bool TryCanonicalIntRows(string Text, out int[] Values)
		{
			Values = null;
			if (string.IsNullOrEmpty(Text) || Text.Length > MaxPaymentRowsChars) return false;
			int separators = 0;
			for (int i = 0; i < Text.Length; i++)
			{
				if (Text[i] == '|') separators++;
				if (separators >= MaxPaymentRows) return false;
			}
			string[] rows = Text.Split('|');
			if (rows.Length == 0 || rows.Length > MaxPaymentRows) return false;
			Values = new int[rows.Length];
			for (int i = 0; i < rows.Length; i++)
			{
				if (rows[i].Length == 0 || rows[i].Length > MaxCanonicalIntegerChars
					|| !int.TryParse(rows[i], global::System.Globalization.NumberStyles.None,
						global::System.Globalization.CultureInfo.InvariantCulture, out Values[i])
					|| Values[i] < 0 || Values[i].ToString(
						global::System.Globalization.CultureInfo.InvariantCulture) != rows[i]) return false;
			}
			return true;
		}

		/// <summary>Strict bounded object-id rows; separators are never valid inside an id.</summary>
		public static bool TryObjectIdRows(string Text, out string[] Values)
		{
			Values = null;
			if (string.IsNullOrEmpty(Text) || Text.Length > MaxPaymentRowsChars) return false;
			int separators = 0;
			for (int i = 0; i < Text.Length; i++)
			{
				if (Text[i] == '|') separators++;
				if (separators >= MaxPaymentRows) return false;
			}
			string[] rows = Text.Split('|');
			if (rows.Length == 0 || rows.Length > MaxPaymentRows) return false;
			for (int i = 0; i < rows.Length; i++)
			{
				if (string.IsNullOrEmpty(rows[i]) || rows[i].Length > MaxObjectIdChars) return false;
				for (int j = 0; j < i; j++)
				{
					if (string.Equals(rows[j], rows[i], global::System.StringComparison.Ordinal)) return false;
				}
			}
			Values = rows;
			return true;
		}
		private const int BountyRulesVersion = 1;

		private const int ScheduledBountyRulesVersion = 2;

		/// <summary>Fixed, all-zero seed, exactly as <c>KingdomChronicle</c>,
		/// <c>KingdomVoiceRules</c>, and <c>KingdomCeremonyRules</c> use it: domain separation is
		/// carried entirely by the settlement id, stream, kind, and ordinal folded into each
		/// key.</summary>
		private static readonly KernelSeed128 BountySeed = default(KernelSeed128);

		/// <summary>Ordinal lane for notice draws &mdash; one per settlement, shared with no other
		/// kernel-backed draw in the mod.</summary>
		private const string NoticeEventStreamId = "taf:bounty:notice:v1";

		private const string ScheduledNoticeStreamPrefix = "taf:bounty:notice:v2:";

		/// <summary>Ordinal lane for the frontier pick. A lane of its own rather than another draw
		/// index on the notice lane: the notice's indices are <c>pass * 3 + k</c> and already
		/// cover every non-negative index, so any frontier index on that lane would be some other
		/// pass's read or take draw.</summary>
		private const string FrontierEventStreamId = "taf:bounty:frontier:v1";

		private const uint NoticeEventKind = 1u;

		/// <summary>Draws one pass of one notice spends: whether anybody reads it, which settler
		/// does, and whether they take it. Fixed, because a semantic draw index must name a
		/// purpose forever.</summary>
		public const uint DrawsPerPass = 3u;

		/// <summary>Passes a single notice can be resolved for before the draw index would run
		/// past <c>uint</c>. Ten million passes is roughly ten million attended visits to one
		/// notice; the cap exists so the arithmetic is total, not because it can be reached.</summary>
		public const int MaxPasses = 10000000;

		/// <summary>One opportunity per Qud day, independent of zone activation cadence.</summary>
		public const long AttemptIntervalTicks = 1200L;

		/// <summary>
		/// Compatibility cap for callers which inspect an absolute schedule as a prefix. Runtime
		/// notices deliberately resolve only the latest due opportunity, because an unattended
		/// historical draw has no historically captured roster to resolve against.
		/// </summary>
		public const int MaxAttemptsPerSettlementPass = 4096;

		/// <summary>Compatibility presentation cap for schedule-inspection clients. Runtime's
		/// latest-only policy can produce at most one refusal in an attended pass.</summary>
		public const int MaxAttemptPresentations = 3;

	}
}
