using System;
using System.Globalization;
using System.IO;
using System.Text;

using XRL;
using XRL.Core;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Machine-readable scenario journal: one tab-separated row per verb, appended to
	/// <c>&lt;profileRoot&gt;/scenario-journal.tsv</c>.
	/// <para>
	/// POST-SEAL OUTPUT, exactly like <c>Player.log</c>. The closed profile seal covers the
	/// launcher's INPUTS - the staged runtime, the harness overlay, the generated manifest and
	/// request, and both option files, all under <c>&lt;profileRoot&gt;/Local</c>. This file is
	/// written beside that tree, never inside it, so a run can never invalidate the seal it was
	/// launched under. <c>Tools/run-scenario.ps1</c> asserts the seal BEFORE launch and never again,
	/// so nothing this file does is visible to that assertion.
	/// </para>
	/// <para>
	/// FAIL-OPEN. A diagnostics write must never break the verb it is describing: every failure is
	/// caught, logged, and returned as a note for the caller to append to its own message. Losing a
	/// journal row must not also lose the run - the same rule
	/// <see cref="KingdomScenarioEvidence"/> already keeps.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioJournal
	{
		internal const string FileName = "scenario-journal.tsv";

		/// <summary>The verb acted, or answered a read. Never a judgement about the answer.</summary>
		internal const string OutcomeOk = "OK";

		/// <summary>The verb refused. The auto-runner stops on this, and only on this.</summary>
		internal const string OutcomeRefused = "REFUSED";

		/// <summary>
		/// Bounds one row so a runaway report cannot fill the profile drive. Deliberately NOT the
		/// registry's 300-char row bound: this column carries the whole report the operator would
		/// otherwise have read in the popup, and truncating it to a roster field width would throw
		/// away the answer the journal exists to deliver.
		/// </summary>
		internal const int MaxMessageChars = 8192;

		/// <summary>
		/// The throwaway profile root: the parent of the engine's save path.
		/// <para>
		/// <c>Tools/run-scenario.ps1</c> launches with <c>-savepath &lt;root&gt;\Save</c>, which
		/// <c>XRLCore.InitializePaths</c> stores verbatim (through <c>Path.GetFullPath</c>, with no
		/// trailing separator) in <see cref="XRLCore.SavePath"/>. The parent of that is the root the
		/// launcher was given, whose <c>Local</c> subtree is the sealed one.
		/// </para>
		/// </summary>
		internal static string ProfileRoot()
		{
			try
			{
				string save = XRLCore.SavePath;
				if (string.IsNullOrEmpty(save)) return null;
				DirectoryInfo parent = Directory.GetParent(save.TrimEnd('\\', '/'));
				return parent == null ? null : parent.FullName;
			}
			catch (Exception)
			{
				return null;
			}
		}

		/// <summary>
		/// Appends one row. Returns null when the row landed, or a bounded note naming the fault.
		/// Never throws.
		/// </summary>
		internal static string Append(string Verb, bool Ok, string Message)
		{
			string root = ProfileRoot();
			if (root == null)
				return Warn("the engine exposes no save path, so no profile root could be derived");
			try
			{
				Directory.CreateDirectory(root);
				File.AppendAllText(Path.Combine(root, FileName),
					Row(DateTime.UtcNow, Verb, Ok, Message) + "\n",
					new UTF8Encoding(false, true));
				return null;
			}
			catch (Exception exception)
			{
				return Warn(KingdomScenarioRules.Bounded(exception.Message));
			}
		}

		/// <summary>
		/// One row: UTC timestamp, verb, outcome, message. Pure, so the grammar can be read without
		/// a game. Every field is escaped, so a tab or a newline inside a report can never forge a
		/// column or a row.
		/// </summary>
		internal static string Row(DateTime Utc, string Verb, bool Ok, string Message)
		{
			return new StringBuilder()
				.Append(Utc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ",
					CultureInfo.InvariantCulture))
				.Append('\t').Append(Escape(KingdomScenarioRules.Bounded(Verb)))
				.Append('\t').Append(Ok ? OutcomeOk : OutcomeRefused)
				.Append('\t').Append(Escape(Bound(Message)))
				.ToString();
		}

		/// <summary>
		/// Reversible escaping. Backslash goes first so the escape character itself round-trips;
		/// every other control character becomes a space rather than a second escape nobody reads.
		/// </summary>
		internal static string Escape(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return "-";
			StringBuilder sb = new StringBuilder(Value.Length);
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (c == '\\') sb.Append("\\\\");
				else if (c == '\n') sb.Append("\\n");
				else if (c == '\r') sb.Append("\\r");
				else if (c == '\t') sb.Append("\\t");
				else if (c < ' ' || c == (char)127) sb.Append(' ');
				else sb.Append(c);
			}
			return sb.ToString();
		}

		private static string Bound(string Message)
		{
			if (string.IsNullOrEmpty(Message)) return "";
			return Message.Length <= MaxMessageChars
				? Message
				: Message.Substring(0, MaxMessageChars);
		}

		private static string Warn(string Detail)
		{
			KingdomLog.Log("[TAF scenario] journal row could not be written: " + Detail);
			return Detail;
		}
	}
}
