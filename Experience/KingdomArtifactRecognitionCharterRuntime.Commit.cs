using System;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The committing half: re-prove the very same object, offer one payload under the one lease
	/// the conversation began with, and then ask the save itself what it kept.
	/// </summary>
	public static partial class KingdomArtifactRecognitionCharterRuntime
	{
		/// <summary>
		/// The one place in D6 where anything durable happens.
		/// <para>
		/// The object is read a second time, at the same tick the plan was built for, and its
		/// digest must match the plan's exactly. A digest covers identity, blueprint, display name,
		/// owner, and the cell it stood in, so an object that moved, changed hands, was renamed, or
		/// stopped existing between the disclosure and the commit refuses here and changes nothing.
		/// </para>
		/// <para>
		/// The payload is then offered under the very lease the register was read from, and no
		/// section is opened here at all. That is what makes the disclosure and the commit one
		/// transaction rather than two: anything that wrote to section one while the founder was
		/// reading has moved the revision, and the authority refuses this offer as stale. Taking a
		/// fresh lease here instead would have quietly accepted that race and written words the
		/// founder was shown against a payload that no longer existed.
		/// </para>
		/// <para>
		/// The governance scope is marked only once civic memory has actually taken a new row, so
		/// an idempotent repeat and every refusal above cost no action and no energy.
		/// </para>
		/// </summary>
		private static void Commit(Ground Ground, KingdomCivicMemorySectionLease Lease,
			GameObject Selected, KingdomArtifactRecognitionPlan Plan)
		{
			if (!KingdomArtifactRecognitionSelectionRuntime.TrySnapshotNearby(Ground.Founder,
				Selected, null, null, Plan.Source.ObservedTick,
				out KingdomArtifactSnapshot reproved, out string failure))
			{
				Popup.Show("Nothing was changed.\n\n" + KingdomPresentation.Rich(failure));
				return;
			}
			if (!string.Equals(reproved.SnapshotDigest, Plan.Source.SnapshotDigest,
					StringComparison.Ordinal)
				|| !ProveResident(Ground, Plan.AttributedResidentId, Plan.AttributionName,
					out failure))
			{
				Popup.Show("Nothing was changed.\n\n" + KingdomPresentation.Rich(failure
					?? "That exact object is no longer what the city was about to write about."));
				return;
			}
			if (!KingdomArtifactRecognitionCommit.TryCommitPlanned(Ground.Memory, Lease,
				Ground.RealmId, reproved, Plan.Kind, Plan.AttributedResidentId,
				Plan.AttributionName, Ground.Tick,
				out KingdomArtifactRecognitionReceipt receipt,
				out KingdomArtifactRecognitionOutcome outcome, out failure))
			{
				Popup.Show("Nothing was changed.\n\n" + KingdomPresentation.Rich(failure));
				return;
			}
			if (outcome == KingdomArtifactRecognitionOutcome.Recorded)
				KingdomGovernanceScope.Commit("recognize artifact");
			Report(Ground, receipt, outcome);
		}

		/// <summary>
		/// Tells the founder what the save holds, by asking the save. The row is read back through
		/// a fresh section read, so what is shown is what survived, not what was offered.
		/// </summary>
		private static void Report(Ground Ground, KingdomArtifactRecognitionReceipt Receipt,
			KingdomArtifactRecognitionOutcome Outcome)
		{
			// The receipt names the settlement that owns this ground, resolved from the realm's
			// topology, so a second city's recognition is never reported under the seat's name.
			string city = KingdomPresentation.Rich(Ground.SettlementName);
			string lead = Outcome == KingdomArtifactRecognitionOutcome.Recorded
				? "{{G|" + city + " has written it down.}}"
				: "{{K|" + city + " had already written exactly this down. Nothing was spent.}}";
			if (!KingdomArtifactRecognitionLease.TryReadBackRow(Ground.Memory, Ground.RealmId,
				Receipt.RecognitionId, out KingdomArtifactRecognitionReceipt kept,
				out string failure))
			{
				Popup.Show(lead + "\n\n{{r|Its readback could not be proved: "
					+ KingdomPresentation.Rich(failure) + "}}");
				return;
			}
			Popup.Show(lead + "\n\n" + KingdomPresentation.Rich(
				KingdomArtifactRecognitionRegister.Plain(kept.Text))
				+ "\n\n{{K|The thing itself has not moved, changed hands, or gained a price.}}");
		}
	}
}
