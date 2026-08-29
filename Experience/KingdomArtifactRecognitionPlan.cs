using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Everything the founder is told before one recognition becomes durable, and nothing the
	/// settlement has yet agreed to.
	/// <para>
	/// A plan is built by running the real transition against a private copy of the held authority.
	/// <see cref="Text"/> is therefore not a preview of the durable text &mdash; it is the durable
	/// text, produced by the same code that will produce the committed row. Disclosing anything
	/// else would be a promise the commit does not keep.
	/// </para>
	/// <para>
	/// Building, reading, and printing a plan spend nothing: no state, no action, no energy. A
	/// founder who reads one and walks away leaves the save byte-identical.
	/// </para>
	/// </summary>
	public sealed class KingdomArtifactRecognitionPlan
	{
		/// <summary>The exact object as it stood when it was read, never the object itself.</summary>
		public readonly KingdomArtifactSnapshot Source;

		public readonly KingdomArtifactRecognitionKind Kind;

		/// <summary>Zero when the city speaks for itself and no resident is named.</summary>
		public readonly int AttributedResidentId;

		public readonly string AttributionName;

		/// <summary>The exact durable sentence this plan will record, disclosed before commit.</summary>
		public readonly string Text;

		/// <summary>Always zero. A recognition is not a good and has no price.</summary>
		public readonly int CommerceValue;

		public readonly long PlannedTick;

		/// <summary>How many rows the authority already holds, and how many it may ever hold.</summary>
		public readonly int RetainedRows;

		public readonly int MaxRows;

		/// <summary>The id this plan would take. A repeat of the same plan finds this row.</summary>
		public readonly string RecognitionId;

		/// <summary>
		/// Whether the city already holds exactly this. Confirming an already-kept plan is lawful
		/// and free, and the founder is told so before they are asked rather than after.
		/// </summary>
		public readonly bool AlreadyKept;

		/// <summary>
		/// The settlement that owns the ground this was read on, by its own name. Never the seat's
		/// name and never the realm's: a realm's second city keeps its own recognitions.
		/// </summary>
		public readonly string SettlementName;

		internal KingdomArtifactRecognitionPlan(KingdomArtifactRecognitionReceipt Prepared,
			int RetainedRows, bool AlreadyKept, string SettlementName)
		{
			this.AlreadyKept = AlreadyKept;
			this.SettlementName = SettlementName;
			Source = Prepared.Source;
			Kind = Prepared.Kind;
			AttributedResidentId = Prepared.AttributedResidentId;
			AttributionName = Prepared.AttributionName;
			Text = Prepared.Text;
			CommerceValue = Prepared.CommerceValue;
			PlannedTick = Prepared.RecognizedTick;
			RecognitionId = Prepared.RecognitionId;
			this.RetainedRows = RetainedRows;
			MaxRows = KingdomArtifactRecognitionRules.MaxRows;
		}

		/// <summary>
		/// The frozen facts, then the exact sentence. Both halves are shown before the founder is
		/// asked, because a recognition that had to be committed to be read would be a purchase.
		/// <para>
		/// Plain text on purpose. These strings carry object display names and settler names this
		/// mod did not author, so the engine-side opener escapes the whole block once instead of
		/// each fragment here guessing what is safe to leave as markup.
		/// </para>
		/// </summary>
		public string Disclosure()
		{
			return "What " + KingdomArtifactRecognitionRegister.Plain(SettlementName)
				+ " would write down\n"
				+ KingdomArtifactRecognitionRegister.Facts(Source)
				+ "\nKept by: " + KingdomArtifactRecognitionRegister.Plain(SettlementName)
				+ "\nAttributed to: " + (AttributedResidentId == 0 ? "the city itself"
					: KingdomArtifactRecognitionRegister.Plain(AttributionName))
				+ "\nForm: " + KingdomArtifactRecognitionRegister.KindName(Kind)
				+ "\nCommerce value: " + CommerceValue
				+ "\nCustody: none taken; the object is not moved, held, locked, or retagged"
				+ "\nAuthority: " + RetainedRows + " of " + MaxRows + " rows already kept"
				+ (AlreadyKept ? "\nThis is already written down. Confirming it changes nothing "
					+ "and spends nothing." : "")
				+ "\n\nThe exact words that would be kept:\n"
				+ KingdomArtifactRecognitionRegister.Plain(Text);
		}
	}

	/// <summary>Pure attribution proof for D6. It reads a roll and never writes one.</summary>
	public static class KingdomArtifactRecognitionAttribution
	{
		/// <summary>
		/// Whether the named resident is still exactly this settlement's, right now.
		/// <para>
		/// Attribution is optional, so no resident at all is a lawful answer. When one is named,
		/// the roll must carry that id exactly once and still carry that name against it. A settler
		/// who left, was renamed, or whose id now appears twice cannot be spoken for; the honest
		/// outcome is a refusal that changes nothing, not a row attributed to a guess.
		/// </para>
		/// </summary>
		public static bool TryProveResident(int ResidentId, string AttributionName,
			IList<int> RollResidentIds, IList<string> RollNames, out string Failure)
		{
			Failure = null;
			if (ResidentId < 0) return Fail("a resident id cannot be negative", out Failure);
			if (ResidentId == 0)
				return string.IsNullOrEmpty(AttributionName)
					|| Fail("an unnamed attribution cannot carry a name", out Failure);
			if (string.IsNullOrEmpty(AttributionName))
				return Fail("a named resident attribution needs that resident's exact name",
					out Failure);
			if (RollResidentIds == null || RollNames == null
				|| RollResidentIds.Count != RollNames.Count)
				return Fail("the settlement roll could not be read exactly", out Failure);
			int matches = 0;
			bool named = false;
			for (int i = 0; i < RollResidentIds.Count; i++)
			{
				if (RollResidentIds[i] != ResidentId) continue;
				matches++;
				named = string.Equals(RollNames[i], AttributionName, StringComparison.Ordinal);
			}
			if (matches == 0)
				return Fail("that settler is no longer on this settlement's roll", out Failure);
			if (matches > 1)
				return Fail("two settlers claim that exact roll identity", out Failure);
			return named
				|| Fail("that settler's name on the roll is no longer the name being recorded",
					out Failure);
		}

		private static bool Fail(string Text, out string Failure)
		{
			Failure = Text;
			return false;
		}
	}
}
