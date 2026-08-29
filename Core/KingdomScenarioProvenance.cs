using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// One developer scenario's stamp on a save. Written only by the excluded harness tree; read
	/// here, in production, so a scenario-built save can never be mistaken for a played one even
	/// when the harness is absent.
	/// </summary>
	public sealed class KingdomScenarioProvenance
	{
		public string ScenarioKey;
		public string AuthorityClass;

		/// <summary>Exact production verb sequence, in order, joined by '+'. Never a digest.</summary>
		public string Verbs;

		/// <summary>Ordinary-play differential anchor this state leans on; null when none.</summary>
		public string AnchorId;

		/// <summary>Digest of the compared semantic key set; null when nothing was compared.</summary>
		public string KeySetDigest;

		public string Seed;
		public string ModVersion;
		public string QudCoreVersion;
		public string DefinitionDigest;

		/// <summary>
		/// Canonical digest over the exact resolved plan this state was built from: bindings and
		/// every resolved step argument. An attended run proves it is executing the stamped plan.
		/// </summary>
		public string PlanDigest;

		/// <summary>Recovery-diagnostic state. Never proves ordinary reachability or acceptance.</summary>
		public bool Synthetic;
	}

	/// <summary>
	/// Pure grammar and eligibility rules for the scenario stamp. Engine-free so the verdict is
	/// testable without a game, and dependency-free of <c>Debug</c> so Core never inverts that edge.
	/// </summary>
	public static class KingdomScenarioProvenanceRules
	{
		/// <summary>Serialized string game state carrying the stamp. One per game.</summary>
		public const string ProvenanceState = "r_TAF_ScenarioProvenance_v1";

		public const string Tag = "sc1";
		public const int Fields = 12;
		public const int MaxVerbs = 16;
		public const int MaxWire = 1024;
		private const string Absent = "-";

		public static string Encode(KingdomScenarioProvenance Record)
		{
			// Every field the decoder requires is validated here, or a record could encode to text
			// its own decoder refuses. PlanDigest was the field that proved that: it was written
			// unchecked and read as a 64-hex digest, so an empty one produced a wire only a failing
			// decode could discover.
			if (Record == null || !SafeToken(Record.ScenarioKey)
				|| !SafeToken(Record.AuthorityClass) || !ValidVerbs(Record.Verbs)
				|| !ValidSeed(Record.Seed) || !SafeToken(Record.ModVersion)
				|| !SafeToken(Record.QudCoreVersion) || !ValidDigest(Record.DefinitionDigest)
				|| !ValidDigest(Record.PlanDigest)
				|| !ValidOptional(Record.AnchorId, false)
				|| !ValidOptional(Record.KeySetDigest, true)) return null;
			string wire = Tag + "|" + Record.ScenarioKey + "|" + Record.AuthorityClass + "|"
				+ Record.Verbs + "|" + (Record.AnchorId ?? Absent) + "|"
				+ (Record.KeySetDigest ?? Absent) + "|" + Record.Seed + "|" + Record.ModVersion
				+ "|" + Record.QudCoreVersion + "|" + Record.DefinitionDigest + "|"
				+ Record.PlanDigest + "|" + (Record.Synthetic ? "1" : "0");
			return wire.Length > MaxWire ? null : wire;
		}

		public static bool TryDecode(string Raw, out KingdomScenarioProvenance Record,
			out string Failure)
		{
			Record = null;
			Failure = null;
			if (string.IsNullOrEmpty(Raw)) return Refuse("No scenario stamp is present.", out Failure);
			if (Raw.Length > MaxWire)
				return Refuse("The scenario stamp exceeds its bounded wire size.", out Failure);
			string[] f = Raw.Split('|');
			if (f.Length != Fields || f[0] != Tag)
				return Refuse("The scenario stamp is not this grammar.", out Failure);
			if (!SafeToken(f[1]) || !SafeToken(f[2]) || !ValidVerbs(f[3]) || !ValidSeed(f[6])
				|| !SafeToken(f[7]) || !SafeToken(f[8]) || !ValidDigest(f[9])
				|| !ValidDigest(f[10]) || (f[11] != "0" && f[11] != "1"))
				return Refuse("The scenario stamp has a malformed field.", out Failure);
			if (f[4] != Absent && !SafeToken(f[4]))
				return Refuse("The scenario stamp has a malformed anchor id.", out Failure);
			if (f[5] != Absent && !ValidDigest(f[5]))
				return Refuse("The scenario stamp has a malformed key-set digest.", out Failure);
			Record = new KingdomScenarioProvenance
			{
				ScenarioKey = f[1],
				AuthorityClass = f[2],
				Verbs = f[3],
				AnchorId = f[4] == Absent ? null : f[4],
				KeySetDigest = f[5] == Absent ? null : f[5],
				Seed = f[6],
				ModVersion = f[7],
				QudCoreVersion = f[8],
				DefinitionDigest = f[9],
				PlanDigest = f[10],
				Synthetic = f[11] == "1"
			};
			return true;
		}

		/// <summary>The ordered verb sequence a scenario claims to have run.</summary>
		public static IList<string> VerbSequence(string Verbs)
		{
			List<string> result = new List<string>();
			if (!ValidVerbs(Verbs)) return result;
			result.AddRange(Verbs.Split('+'));
			return result;
		}

		/// <summary>
		/// Whether this stamp is well formed and still describes the current build.
		/// <para>
		/// This is NOT an acceptance decision and must never be used as one. A stamp names the
		/// anchor it claims; it cannot prove that anchor exists, was reached by ordinary play, or
		/// measured the same authority class, verb sequence, and key set. Only an independently
		/// supplied frozen anchor-evidence record can prove that, and it lives outside production
		/// so that neither a save nor the scenario registry can manufacture it.
		/// </para>
		/// </summary>
		public static bool TryValidateStampShape(KingdomScenarioProvenance Record,
			string CurrentDefinitionDigest, string CurrentModVersion, string CurrentQudCoreVersion,
			out string Failure)
		{
			Failure = null;
			if (Record == null) return Refuse("No scenario stamp to judge.", out Failure);
			// Re-prove the grammar so a directly constructed record cannot bypass decode checks.
			string wire = Encode(Record);
			if (string.IsNullOrEmpty(wire))
				return Refuse("The scenario stamp is malformed.", out Failure);
			KingdomScenarioProvenance reparsed;
			string decodeFailure;
			if (!TryDecode(wire, out reparsed, out decodeFailure))
				return Refuse("The scenario stamp is malformed: " + decodeFailure, out Failure);
			// Exact round trip, not merely a successful decode: a directly constructed record whose
			// field serializes as a different value would otherwise be judged as the value it is not.
			if (!string.Equals(Encode(reparsed), wire, StringComparison.Ordinal)
				|| !Same(Record, reparsed))
				return Refuse("The scenario stamp does not round-trip to itself.", out Failure);
			if (!ValidDigest(CurrentDefinitionDigest)
				|| !string.Equals(Record.DefinitionDigest, CurrentDefinitionDigest,
					StringComparison.Ordinal))
				return Refuse("The scenario definition changed since this state was built; "
					+ "the stamp is stale.", out Failure);
			if (!string.Equals(Record.ModVersion, CurrentModVersion, StringComparison.Ordinal)
				|| !string.Equals(Record.QudCoreVersion, CurrentQudCoreVersion,
					StringComparison.Ordinal))
				return Refuse("The mod or core version changed since this state was built; "
					+ "the stamp is stale.", out Failure);
			return true;
		}

		/// <summary>
		/// Why a stamp alone can never be green. Kept beside the shape check so a caller reaching
		/// for acceptance finds the refusal rather than inventing one.
		/// </summary>
		public static string AcceptanceRequiresIndependentAnchorEvidence(
			KingdomScenarioProvenance Record)
		{
			if (Record != null && Record.Synthetic)
				return "Synthetic scenario states are recovery diagnostics only; they never sign "
					+ "native acceptance.";
			if (Record == null || Record.AnchorId == null || Record.KeySetDigest == null)
				return "This state names no ordinary-play differential anchor; verdicts under it "
					+ "are ineligible, not green.";
			return "Acceptance needs the independently held anchor-evidence record for '"
				+ Record.AnchorId + "'; a stamp names its anchor but cannot prove it.";
		}

		/// <summary>One operator-readable block. Never claims eligibility it has not proved.</summary>
		public static string Describe(KingdomScenarioProvenance Record)
		{
			if (Record == null) return "Scenario stamp: none (ordinary game).";
			StringBuilder sb = new StringBuilder();
			sb.Append("Scenario stamp: ").Append(Record.ScenarioKey)
				.Append("  authority=").Append(Record.AuthorityClass)
				.Append(Record.Synthetic ? "  SYNTHETIC (recovery diagnostic only)" : "")
				.Append("\n  verbs: ").Append(Record.Verbs)
				.Append("\n  anchor: ").Append(Record.AnchorId ?? "none (verdicts ineligible)")
				.Append("  keyset: ").Append(Record.KeySetDigest ?? "none")
				.Append("\n  seed: ").Append(Record.Seed)
				.Append("  mod ").Append(Record.ModVersion)
				.Append("  qud ").Append(Record.QudCoreVersion)
				.Append("\n  definition: ").Append(Record.DefinitionDigest)
				.Append("\n  plan: ").Append(Record.PlanDigest);
			return sb.ToString();
		}

		/// <summary>What a report says when the stamp is present but unreadable.</summary>
		public static string DescribeUnreadable(string Failure)
		{
			return "Scenario stamp: present but unreadable ("
				+ (string.IsNullOrEmpty(Failure) ? "unknown fault" : Failure)
				+ "). This save was not produced by ordinary play.";
		}

		/// <summary>Field-for-field equality, so "it decoded" is never mistaken for "it is the same".</summary>
		private static bool Same(KingdomScenarioProvenance A, KingdomScenarioProvenance B)
		{
			return A != null && B != null
				&& string.Equals(A.ScenarioKey, B.ScenarioKey, StringComparison.Ordinal)
				&& string.Equals(A.AuthorityClass, B.AuthorityClass, StringComparison.Ordinal)
				&& string.Equals(A.Verbs, B.Verbs, StringComparison.Ordinal)
				&& string.Equals(A.AnchorId, B.AnchorId, StringComparison.Ordinal)
				&& string.Equals(A.KeySetDigest, B.KeySetDigest, StringComparison.Ordinal)
				&& string.Equals(A.Seed, B.Seed, StringComparison.Ordinal)
				&& string.Equals(A.ModVersion, B.ModVersion, StringComparison.Ordinal)
				&& string.Equals(A.QudCoreVersion, B.QudCoreVersion, StringComparison.Ordinal)
				&& string.Equals(A.DefinitionDigest, B.DefinitionDigest, StringComparison.Ordinal)
				&& string.Equals(A.PlanDigest, B.PlanDigest, StringComparison.Ordinal)
				&& A.Synthetic == B.Synthetic;
		}

		/// <summary>
		/// An optional field that must never collide with its own absent sentinel.
		/// <para>
		/// <c>AnchorId="-"</c> used to pass the token rule, encode byte-identically to null, and
		/// reparse as null: one in-memory value serialized as another, which is exactly the
		/// non-injectivity the grammar elsewhere refuses. The sentinel is now reserved from present
		/// values, so a record round-trips to itself or does not encode at all.
		/// </para>
		/// </summary>
		private static bool ValidOptional(string Value, bool Digest)
		{
			if (Value == null) return true;
			if (string.Equals(Value, Absent, StringComparison.Ordinal)) return false;
			return Digest ? ValidDigest(Value) : SafeToken(Value);
		}

		private static bool ValidVerbs(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return false;
			string[] parts = Value.Split('+');
			if (parts.Length < 1 || parts.Length > MaxVerbs) return false;
			for (int i = 0; i < parts.Length; i++) if (!SafeToken(parts[i])) return false;
			return true;
		}

		/// <summary>
		/// The literal seed string handed to the engine. A leading '#' is Qud's own exact-world-seed
		/// prefix and is recorded verbatim rather than normalized away.
		/// </summary>
		private static bool ValidSeed(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return false;
			return Value[0] == '#'
				? Value.Length > 1 && SafeToken(Value.Substring(1))
				: SafeToken(Value);
		}

		private static bool ValidDigest(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| (Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}

		private static bool SafeToken(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > 96) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= 'a' && Value[i] <= 'z')
					|| (Value[i] >= '0' && Value[i] <= '9')
					|| Value[i] == '-' || Value[i] == '.')) return false;
			return true;
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
