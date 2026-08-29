using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The scenario request line, as the launcher freezes it into the profile's embark module:
	/// <c>&lt;key&gt;[;param=value][;seed=#N]</c>.
	/// <para>
	/// The seed is a request field rather than a scenario field. Caves of Qud exposes no
	/// launcher-reachable pre-generation seed injection, so authored data declares no seed and the
	/// launcher freezes one here; the new-game gate proves the engine actually generated under it.
	/// </para>
	/// <para>
	/// The parser is TOTAL and BOUNDED. It is fed a durable game-state string, so it is an untrusted
	/// boundary: every malformed shape returns a named refusal, nothing is silently discarded or
	/// overwritten, and no allocation or traversal happens past the caps below. Splitting first and
	/// asking questions afterwards is what let <c>";"</c> reach <c>parts[0]</c> on an empty array.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioRequest
	{
		/// <summary>Whole-request cap, proved BEFORE any split allocates.</summary>
		internal const int MaxRequestChars = 512;

		/// <summary>Segment cap, proved by a bounded scan BEFORE any split allocates.</summary>
		internal const int MaxSegments = 16;

		/// <summary>Reserved: frozen by the launcher, never an authored scenario parameter.</summary>
		internal const string SeedName = "seed";

		/// <summary>
		/// Splits and proves one raw request. Pure: no registry, no game, no allocation past cap.
		/// </summary>
		internal static bool TryParse(string Request, out string Key,
			out IDictionary<string, string> Selection, out string Seed, out string Failure)
		{
			Key = null;
			Selection = null;
			Seed = null;
			Failure = null;
			if (string.IsNullOrEmpty(Request))
				return Refuse("no scenario was requested", out Failure);
			if (Request.Length > MaxRequestChars)
				return Refuse("the scenario request exceeds " + MaxRequestChars + " characters",
					out Failure);
			int segments = 1;
			for (int i = 0; i < Request.Length; i++)
			{
				if (Request[i] != ';') continue;
				segments++;
				// Counted, not collected: the cap bounds the scan itself, not just its verdict.
				if (segments > MaxSegments)
					return Refuse("the scenario request declares more than " + MaxSegments
						+ " segments", out Failure);
			}
			// Empty entries are KEPT. Discarding them would turn ";" into a keyless request and
			// "a;;b=1" into a lawful one, which is laundering rather than parsing.
			string[] parts = Request.Split(';');
			// NOT trimmed. Trim-before-SafeToken silently repaired malformed durable text - the same
		// laundering this docket removed from the XML adapter, one boundary over.
		string key = parts[0];
			if (key.Length == 0) return Refuse("the scenario request names no key", out Failure);
			if (!KingdomScenarioRules.SafeToken(key))
				return Refuse("the scenario request key '" + KingdomScenarioRules.Bounded(key)
					+ "' is malformed", out Failure);
			Dictionary<string, string> selection =
				new Dictionary<string, string>(StringComparer.Ordinal);
			string seed = null;
			for (int i = 1; i < parts.Length; i++)
			{
				if (!TrySegment(parts[i], selection, ref seed, out Failure)) return false;
			}
			Key = key;
			Selection = selection;
			Seed = seed;
			return true;
		}

		private static bool TrySegment(string Segment, IDictionary<string, string> Selection,
			ref string Seed, out string Failure)
		{
			Failure = null;
			if (Segment.Length == 0)
				return Refuse("the scenario request carries an empty segment", out Failure);
			int equals = Segment.IndexOf('=');
			if (equals < 1)
				return Refuse("malformed parameter '" + KingdomScenarioRules.Bounded(Segment) + "'",
					out Failure);
			string name = Segment.Substring(0, equals);
			string value = Segment.Substring(equals + 1);
			if (!KingdomScenarioRules.SafeToken(name))
				return Refuse("malformed parameter name '" + KingdomScenarioRules.Bounded(name)
					+ "'", out Failure);
			if (string.Equals(name, SeedName, StringComparison.Ordinal))
			{
				// At most one seed, and it must be a seed. A second occurrence used to overwrite the
				// first, so a request could name two worlds and the gate would prove only the last.
				if (Seed != null)
					return Refuse("the scenario request declares more than one seed", out Failure);
				if (!KingdomScenarioRules.ValidSeed(value))
					return Refuse("the frozen seed '" + KingdomScenarioRules.Bounded(value)
						+ "' is not a recordable seed token", out Failure);
				Seed = value;
				return true;
			}
			// The VALUE is shape-validated too. It was not, so " north" and "north " parsed clean
			// and then failed to match any domain member for a reason the operator never saw - and
			// two of this file's own padding controls were passing against a mirror that checked
			// what this code did not.
			if (value.Length == 0)
				return Refuse("parameter '" + name + "' selects an empty value", out Failure);
			if (!KingdomScenarioRules.SafeToken(value))
				return Refuse("parameter '" + name + "' selects malformed value '"
					+ KingdomScenarioRules.Bounded(value) + "'", out Failure);
			if (Selection.ContainsKey(name))
				return Refuse("parameter '" + name + "' is selected more than once; an ambiguous "
					+ "request is refused rather than resolved", out Failure);
			Selection[name] = value;
			return true;
		}

		/// <summary>Resolves a request line into a fully preflighted plan. Mutates nothing.</summary>
		internal static bool TryPlan(string Request, out KingdomScenarioPlan Plan,
			out string Failure)
		{
			Plan = null;
			string key;
			IDictionary<string, string> selection;
			string seed;
			if (!TryParse(Request, out key, out selection, out seed, out Failure)) return false;
			if (!KingdomScenarioRegistry.Healthy)
				return Refuse("the scenario roster is not healthy: "
					+ string.Join("; ", Findings()), out Failure);
			KingdomScenarioDefinition definition = KingdomScenarioRegistry.Find(key);
			if (definition == null)
				return Refuse("no scenario is keyed '" + KingdomScenarioRules.Bounded(key) + "'",
					out Failure);
			if (!KingdomScenarioAnchorRules.IsKnownAuthorityClass(definition.AuthorityClass))
				return Refuse("authority class '"
					+ KingdomScenarioRules.Bounded(definition.AuthorityClass)
					+ "' declares no semantic key set", out Failure);
			return KingdomScenarioRules.TryPlan(definition, selection,
				KingdomScenarioRegistry.Digest, seed, out Plan, out Failure);
		}

		private static IList<string> Findings()
		{
			IList<string> findings = KingdomScenarioRegistry.Findings;
			return findings == null || findings.Count == 0
				? new List<string> { "unknown fault" } : findings;
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
