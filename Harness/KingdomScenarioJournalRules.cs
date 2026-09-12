namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Pure bounds for one scenario journal row. Engine-free so the cap and its marker are
	/// testable in both public test projects. Native run 49: the rung-3 reads were cut at the
	/// cap with no sign, and three rows of exactly the cap read as complete. A cut row now ends
	/// in a marker naming how much was lost, and the row is never over the cap.
	/// </summary>
	internal static class KingdomScenarioJournalRules
	{
		/// <summary>
		/// Bounds one row so a runaway report cannot fill the profile drive. Deliberately NOT the
		/// registry's 300-char row bound: this column carries the whole report the operator would
		/// otherwise have read in the popup, and truncating it to a roster field width would throw
		/// away the answer the journal exists to deliver.
		/// </summary>
		internal const int MaxMessageChars = 8192;

		/// <summary>The camp-heart blocked-path message dump, sized so a check row's own reads
		/// fit under the cap at every field's worst width (KingdomScenarioJournalRulesTests).</summary>
		internal const int BlockedMessagesKept = 8;
		internal const int BlockedMessageChars = 192;

		internal const string TruncatedOpen = "[truncated ";
		internal const string TruncatedClose = " chars]";

		/// <summary>Message unchanged when it fits; otherwise cut so that the kept text plus the
		/// marker is exactly MaxChars, the marker naming the characters dropped.</summary>
		internal static string Bound(string Message, int MaxChars)
		{
			if (string.IsNullOrEmpty(Message)) return "";
			if (Message.Length <= MaxChars) return Message;
			// The count is unknown until the cut is chosen; widen once for the count's own digits.
			int dropped = Message.Length - MaxChars;
			string marker = TruncatedOpen + dropped + TruncatedClose;
			int keep = MaxChars - marker.Length;
			if (keep < 0) keep = 0;
			marker = TruncatedOpen + (Message.Length - keep) + TruncatedClose;
			keep = MaxChars - marker.Length;
			if (keep < 0) keep = 0;
			marker = TruncatedOpen + (Message.Length - keep) + TruncatedClose;
			return Message.Substring(0, keep) + marker;
		}
	}
}
