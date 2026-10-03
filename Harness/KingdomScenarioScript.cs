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

		/// <summary>
		/// The file bound. An oversized file is refused before it is read; what its lines mean, and
		/// how many verbs and characters a line may carry, is <c>KingdomScenarioScriptRules</c>'s
		/// alone, so the offline tools mirror one parser rather than two.
		/// </summary>
		internal const int MaxFileBytes = 65536;

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
		/// empty of verbs, past the verb bound, or carrying an over-long line refuses by name rather
		/// than running a partial script. The line rules are <c>KingdomScenarioScriptRules</c>'s.
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
			return KingdomScenarioScriptRules.TryParse(lines, out Verbs, out Failure);
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
