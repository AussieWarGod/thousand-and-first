using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// Which durable type table a key was observed under. Presence, never value.
	/// </summary>
	public enum KingdomDurableKeyShape : byte
	{
		/// <summary>No key under any durable type table. The only fresh state.</summary>
		Absent = 0,

		/// <summary>Present under the int table and no other. A stored zero is still present.</summary>
		ExactInt = 1,

		/// <summary>Present under the string table and no other. A stored empty is still present.</summary>
		ExactString = 2,

		/// <summary>Present under a wrong table, or under two tables at once. Never readable.</summary>
		Torn = 3
	}

	/// <summary>
	/// One key's raw presence across every durable game-state table, plus the values of the two
	/// tables a harness authority key is allowed to live in.
	/// <para>
	/// This is a plain record so the shapes can be enumerated without a live game. A runtime reader
	/// fills it from the engine's <c>Has*GameState</c> family; nothing else may decide presence,
	/// because <c>Get*GameState(name, 0)</c> and <c>Get*GameState(name, "")</c> return the same
	/// answer for an absent key and for an explicitly stored zero or empty string.
	/// </para>
	/// </summary>
	public sealed class KingdomDurableKeyObservation
	{
		public bool HasString;
		public string String;
		public bool HasInt;
		public int Int;
		public bool HasInt64;
		public bool HasObject;
		public bool HasBoolean;
	}

	/// <summary>Durable state of the one production transaction a scenario may run.</summary>
	public enum KingdomScenarioTransactionShape : byte
	{
		/// <summary>Neither key exists anywhere. The only state a run may begin from.</summary>
		None = 0,

		/// <summary>Exactly one int key holding 1, with no committed key.</summary>
		Attempted = 1,

		/// <summary>Exactly the two int keys, holding 2 and 1.</summary>
		Committed = 2,

		/// <summary>Every other shape. Refuses permanently.</summary>
		Torn = 3
	}

	/// <summary>Whether this game carries a scenario stamp, and whether it could be read.</summary>
	public enum KingdomScenarioStampShape : byte
	{
		/// <summary>No provenance key and no marker key anywhere. The only ordinary-play case.</summary>
		Absent = 0,

		/// <summary>Exact string provenance plus the exact int marker 1.</summary>
		Readable = 1,

		/// <summary>
		/// Any other present shape. Never overwritten, and never founds an ordinary anchor: a torn
		/// stamp that fell through to "ordinary" would launder a scenario-built state.
		/// </summary>
		PresentUnreadable = 2
	}

	/// <summary>
	/// Pure classifier for every durable key the scenario harness owns.
	/// <para>
	/// Engine-free on purpose. The shapes that matter are the corrupt ones, and a corrupt shape
	/// cannot be reached by playing; it has to be constructed. Keeping the verdict here lets the
	/// zero, empty, wrong-type, dual-type, and cross-key matrix execute in the pure test assembly,
	/// and lets the runtime reader be pinned to a decision it does not itself make.
	/// </para>
	/// </summary>
	public static class KingdomScenarioStateShape
	{
		public const int AttemptedValue = 1;
		public const int CommittedValue = 2;
		public const int MarkerValue = 1;

		/// <summary>Raw table presence for one key. Values are judged by the callers below.</summary>
		public static KingdomDurableKeyShape Classify(KingdomDurableKeyObservation Observed,
			out string Detail)
		{
			Detail = null;
			if (Observed == null)
			{
				Detail = "the key was never observed";
				return KingdomDurableKeyShape.Torn;
			}
			int tables = (Observed.HasString ? 1 : 0) + (Observed.HasInt ? 1 : 0)
				+ (Observed.HasInt64 ? 1 : 0) + (Observed.HasObject ? 1 : 0)
				+ (Observed.HasBoolean ? 1 : 0);
			if (tables == 0) return KingdomDurableKeyShape.Absent;
			if (tables > 1)
			{
				Detail = "the key is present under " + tables.ToString() + " durable type tables";
				return KingdomDurableKeyShape.Torn;
			}
			if (Observed.HasInt) return KingdomDurableKeyShape.ExactInt;
			if (Observed.HasString) return KingdomDurableKeyShape.ExactString;
			Detail = "the key is present under a durable type table it may never use";
			return KingdomDurableKeyShape.Torn;
		}

		/// <summary>
		/// The pre-mutation poison cut. Attempted means the ground may already have been altered by
		/// an unjournalled staging call, so every shape except None refuses the profile forever.
		/// </summary>
		public static KingdomScenarioTransactionShape Transaction(
			KingdomDurableKeyObservation Attempt, KingdomDurableKeyObservation Committed,
			out string Detail)
		{
			string attemptDetail;
			string committedDetail;
			KingdomDurableKeyShape attempt = Classify(Attempt, out attemptDetail);
			KingdomDurableKeyShape commit = Classify(Committed, out committedDetail);
			Detail = null;
			if (attempt == KingdomDurableKeyShape.Absent && commit == KingdomDurableKeyShape.Absent)
				return KingdomScenarioTransactionShape.None;
			if (attempt == KingdomDurableKeyShape.ExactInt
				&& Attempt.Int == AttemptedValue && commit == KingdomDurableKeyShape.Absent)
				return KingdomScenarioTransactionShape.Attempted;
			if (attempt == KingdomDurableKeyShape.ExactInt && Attempt.Int == CommittedValue
				&& commit == KingdomDurableKeyShape.ExactInt && Committed.Int == MarkerValue)
				return KingdomScenarioTransactionShape.Committed;
			Detail = Torn("transaction", attempt, attemptDetail)
				?? Torn("committed cross-check", commit, committedDetail)
				?? "the transaction keys hold a shape no run may ever have produced";
			return KingdomScenarioTransactionShape.Torn;
		}

		/// <summary>
		/// Scenario provenance. Absent is the ONLY case a caller may treat as ordinary play; every
		/// present shape that is not the exact pair is unreadable rather than a fall-through.
		/// </summary>
		public static KingdomScenarioStampShape Stamp(KingdomDurableKeyObservation Provenance,
			KingdomDurableKeyObservation Marker, out string Detail)
		{
			string provenanceDetail;
			string markerDetail;
			KingdomDurableKeyShape provenance = Classify(Provenance, out provenanceDetail);
			KingdomDurableKeyShape marker = Classify(Marker, out markerDetail);
			Detail = null;
			if (provenance == KingdomDurableKeyShape.Absent
				&& marker == KingdomDurableKeyShape.Absent) return KingdomScenarioStampShape.Absent;
			if (provenance == KingdomDurableKeyShape.ExactString
				&& !string.IsNullOrEmpty(Provenance.String)
				&& marker == KingdomDurableKeyShape.ExactInt && Marker.Int == MarkerValue)
				return KingdomScenarioStampShape.Readable;
			Detail = Torn("scenario provenance", provenance, provenanceDetail)
				?? Torn("scenario presence marker", marker, markerDetail)
				?? Pair(provenance, Provenance, marker, Marker);
			return KingdomScenarioStampShape.PresentUnreadable;
		}

		/// <summary>
		/// A harness-owned authority key that carries text. Absent is ordinary; an exact non-empty
		/// string is readable; a stored empty string, a wrong table, or two tables refuse.
		/// </summary>
		public static bool TryAuthorityText(KingdomDurableKeyObservation Observed, out string Value,
			out bool Present, out string Detail)
		{
			Value = null;
			Present = false;
			KingdomDurableKeyShape shape = Classify(Observed, out Detail);
			if (shape == KingdomDurableKeyShape.Absent)
			{
				Detail = null;
				return true;
			}
			Present = true;
			if (shape == KingdomDurableKeyShape.ExactString && !string.IsNullOrEmpty(Observed.String))
			{
				Detail = null;
				Value = Observed.String;
				return true;
			}
			if (Detail == null)
				Detail = shape == KingdomDurableKeyShape.ExactString
					? "the key holds an explicitly stored empty string"
					: "the key is present under the int table where text was required";
			return false;
		}

		/// <summary>
		/// Whether this game may found ORDINARY-PLAY anchor evidence.
		/// <para>
		/// Absence of a stamp is not innocence. A scenario profile whose stamp was deleted, torn, or
		/// never published still carries its transaction marker and its request key, and either one
		/// means the world was arranged rather than played. Eligibility therefore needs all three
		/// authorities absent across every durable type table, judged in one place so the capture
		/// command and the operator-facing status can never disagree about it.
		/// </para>
		/// </summary>
		public static bool OrdinaryAnchorEligible(KingdomDurableKeyObservation Provenance,
			KingdomDurableKeyObservation Marker, KingdomDurableKeyObservation Attempt,
			KingdomDurableKeyObservation Committed, KingdomDurableKeyObservation Request,
			out string Refusal)
		{
			string detail;
			KingdomScenarioStampShape stamp = Stamp(Provenance, Marker, out detail);
			if (stamp != KingdomScenarioStampShape.Absent)
			{
				Refusal = stamp == KingdomScenarioStampShape.Readable
					? "this game carries a scenario stamp"
					: "this game carries scenario provenance in an unreadable shape ("
						+ (detail ?? "unknown fault") + ")";
				return false;
			}
			KingdomScenarioTransactionShape transaction = Transaction(Attempt, Committed,
				out detail);
			if (transaction != KingdomScenarioTransactionShape.None)
			{
				Refusal = "this game carries a scenario transaction marker ("
					+ transaction.ToString().ToLowerInvariant()
					+ (detail == null ? "" : ": " + detail) + ")";
				return false;
			}
			if (Classify(Request, out detail) != KingdomDurableKeyShape.Absent)
			{
				Refusal = "this game carries a scenario request key ("
					+ (detail ?? "a request key exists under a durable type table") + ")";
				return false;
			}
			Refusal = null;
			return true;
		}

		private static string Torn(string Name, KingdomDurableKeyShape Shape, string Detail)
		{
			return Shape == KingdomDurableKeyShape.Torn
				? "the " + Name + " key is torn (" + (Detail ?? "unknown fault") + ")"
				: null;
		}

		private static string Pair(KingdomDurableKeyShape Provenance,
			KingdomDurableKeyObservation ProvenanceRow, KingdomDurableKeyShape Marker,
			KingdomDurableKeyObservation MarkerRow)
		{
			if (Provenance == KingdomDurableKeyShape.ExactString
				&& string.IsNullOrEmpty(ProvenanceRow.String))
				return "the scenario provenance key holds an explicitly stored empty string";
			if (Provenance == KingdomDurableKeyShape.ExactInt)
				return "the scenario provenance key is present under the int table";
			if (Marker == KingdomDurableKeyShape.ExactString)
				return "the scenario presence marker is present under the string table";
			if (Marker == KingdomDurableKeyShape.ExactInt && MarkerRow.Int != MarkerValue)
				return "the scenario presence marker holds an unknown value";
			if (Provenance == KingdomDurableKeyShape.Absent)
				return "the scenario presence marker is set but no stamp exists";
			if (Marker == KingdomDurableKeyShape.Absent)
				return "a scenario stamp is present without its presence marker";
			return "the scenario presence marker holds a value no gate ever writes";
		}
	}
}
