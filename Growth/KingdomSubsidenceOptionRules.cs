using System;

namespace ThousandAndFirst
{
	/// <summary>Five-table admission for the existing elapsed-option wire. Invalid evidence
	/// stays invalid; only demonstrated absence permits an unobserved option.</summary>
	internal static class KingdomSubsidenceOptionRules
	{
		/// <summary>Transient publication expectation, not durable cancellation authority.
		/// Captures values rather than retaining the caller's mutable observation.</summary>
		internal sealed class Snapshot
		{
			internal readonly bool Present;
			internal readonly string PriorWire;
			internal readonly string NextWire;
			internal readonly KingdomElapsedOptionDecision Decision;

			internal Snapshot(bool present, string priorWire, KingdomElapsedOptionDecision decision)
			{
				Present = present; PriorWire = priorWire; Decision = decision;
				NextWire = KingdomElapsedOptionRules.Encode(decision.Record);
			}
		}

		internal static bool TryRead(KingdomDurableKeyObservation observed,
			out KingdomElapsedOptionRecord prior, out bool present)
		{
			prior = KingdomElapsedOptionRecord.Unobserved;
			if (!KingdomScenarioStateShape.TryAuthorityText(observed, out string wire,
				out present, out string _)) return false;
			if (!present) return true;
			return KingdomElapsedOptionRules.TryDecode(wire, out prior)
				&& prior.State != KingdomElapsedOptionState.Unobserved;
		}

		internal static KingdomElapsedOptionDecision Observe(KingdomDurableKeyObservation observed,
			bool enabled, long masterToken, long now, out Snapshot snapshot)
		{
			snapshot = null;
			if (!TryRead(observed, out KingdomElapsedOptionRecord prior, out bool present))
				return new KingdomElapsedOptionDecision(false, prior,
					KingdomElapsedOptionTransition.None, KingdomElapsedOptionAction.Invalid);
			KingdomElapsedOptionDecision decision = KingdomElapsedOptionRules.Observe(
				prior, enabled, masterToken, now);
			if (decision.Valid) snapshot = new Snapshot(present, present ? observed.String : null, decision);
			return decision;
		}

		/// <summary>Before a runtime write, require precisely the original table shape and bytes.
		/// An already-published target is recognized separately; it never licenses replacement.</summary>
		internal static bool CanPublish(Snapshot snapshot, KingdomDurableKeyObservation current,
			out string wire)
		{
			wire = null;
			if (snapshot == null || !snapshot.Decision.Valid || string.IsNullOrEmpty(snapshot.NextWire)
				|| !KingdomScenarioStateShape.TryAuthorityText(current, out string currentWire,
					out bool present, out string _) || present != snapshot.Present
				|| !string.Equals(currentWire, snapshot.PriorWire, StringComparison.Ordinal)) return false;
			wire = snapshot.NextWire;
			return true;
		}

		internal static bool ProvesPublished(Snapshot snapshot, KingdomDurableKeyObservation current)
		{
			return snapshot != null && snapshot.Decision.Valid && !string.IsNullOrEmpty(snapshot.NextWire)
				&& KingdomScenarioStateShape.TryAuthorityText(current, out string wire,
					out bool present, out string _) && present
				&& string.Equals(wire, snapshot.NextWire, StringComparison.Ordinal);
		}
	}
}
