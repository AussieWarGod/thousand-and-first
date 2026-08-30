using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using XRL.Core;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The sealed scenario script: <c>&lt;profileRoot&gt;/Local/scenario-script.txt</c>, one verb
	/// per line, in the exact strings <c>kingdom:scenario</c> already accepts.
	/// <para>
	/// SEALED INPUT. <c>Tools/prepare-scenario.sh</c> writes this file BEFORE the profile is sealed,
	/// so it sits inside the one closed inventory <c>Tools/run-scenario.ps1</c> proves in both
	/// directions at launch. The script an unattended run executes is therefore exactly the script
	/// the operator sealed - a file dropped in afterwards fails the seal and the launcher refuses.
	/// That is also why the script lives under <c>Local</c> and the journal does not: one is a
	/// launcher input, the other is a run's output.
	/// </para>
	/// <para>
	/// INERT WHEN ABSENT. No script file means no scripted execution, and no journal row about it.
	/// A prepared profile without a script is an ordinary attended profile.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioScript
	{
		internal const string FileName = "scenario-script.txt";

		/// <summary>Comment marker, so a sealed script can explain itself to the next reader.</summary>
		internal const char Comment = '#';

		/// <summary>
		/// Bounds. A script is a short list of harness verbs, not a program: refusing an oversized
		/// or over-long file outright is cheaper than discovering mid-run that a profile was sealed
		/// around something nobody meant to run.
		/// </summary>
		internal const int MaxVerbs = 32;

		internal const int MaxFileBytes = 65536;

		internal const int MaxVerbChars = KingdomScenarioRules.MaxTextChars;

		/// <summary>
		/// The script's full path, or null when the engine exposes no shared path.
		/// <para>
		/// Anchored on <see cref="XRLCore.LocalPath"/>, which is exactly the directory
		/// <c>Tools/run-scenario.ps1</c> passes as <c>-sharedpath</c> and exactly the directory
		/// <c>Tools/prepare-scenario.sh</c> seals - so under the launcher this is
		/// <c>&lt;profileRoot&gt;/Local/scenario-script.txt</c> and it is sealed content by
		/// construction. Asking the engine beats joining "Local" onto a root of our own: a profile
		/// launched some other way then simply finds no script and stays inert, rather than reading
		/// one out of a tree no seal covered.
		/// </para>
		/// </summary>
		internal static string Locate()
		{
			try
			{
				string local = XRLCore.LocalPath;
				return string.IsNullOrEmpty(local) ? null : Path.Combine(local, FileName);
			}
			catch (Exception)
			{
				return null;
			}
		}

		/// <summary>True only when a readable script file is actually there.</summary>
		internal static bool Present()
		{
			string path = Locate();
			if (path == null) return false;
			try
			{
				return File.Exists(path);
			}
			catch (Exception)
			{
				return false;
			}
		}

		/// <summary>
		/// Reads the sealed script. Fail-closed: a file that is present but oversized, unreadable,
		/// empty of verbs, or carrying an over-long line refuses by name rather than running a
		/// partial script.
		/// </summary>
		internal static bool TryRead(out IList<string> Verbs, out string Failure)
		{
			Verbs = null;
			Failure = null;
			string path = Locate();
			if (path == null)
				return Refuse("the engine exposes no shared path, so no sealed script directory "
					+ "could be located", out Failure);
			string[] lines;
			try
			{
				FileInfo info = new FileInfo(path);
				if (!info.Exists) return Refuse("no script file at " + path, out Failure);
				if (info.Length > MaxFileBytes)
					return Refuse("the script file is " + info.Length + " bytes, over the "
						+ MaxFileBytes + "-byte bound", out Failure);
				lines = File.ReadAllLines(path, new UTF8Encoding(false, true));
			}
			catch (Exception exception)
			{
				return Refuse("the script file could not be read: "
					+ KingdomScenarioRules.Bounded(exception.Message), out Failure);
			}
			return TryParse(lines, out Verbs, out Failure);
		}

		/// <summary>
		/// Pure line-to-verb parsing. Blank lines and comments are dropped; everything else is a
		/// verb, passed through verbatim so the script and the wish always mean the same thing.
		/// This never decides whether a verb EXISTS - the shared entry owns the closed verb set, and
		/// duplicating it here would give a script two places to disagree with the wish.
		/// </summary>
		internal static bool TryParse(IList<string> Lines, out IList<string> Verbs,
			out string Failure)
		{
			Verbs = null;
			Failure = null;
			List<string> found = new List<string>();
			for (int i = 0; Lines != null && i < Lines.Count; i++)
			{
				string line = (Lines[i] ?? "").Trim();
				if (line.Length == 0 || line[0] == Comment) continue;
				if (line.Length > MaxVerbChars)
					return Refuse("script line " + (i + 1) + " is " + line.Length
						+ " characters, over the " + MaxVerbChars + "-character bound",
						out Failure);
				if (found.Count == MaxVerbs)
					return Refuse("the script declares more than " + MaxVerbs + " verbs",
						out Failure);
				found.Add(line);
			}
			if (found.Count == 0)
				return Refuse("the script file declares no verbs", out Failure);
			Verbs = found;
			return true;
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
