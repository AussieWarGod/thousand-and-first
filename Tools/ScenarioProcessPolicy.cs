using System;

namespace ThousandAndFirst.Tools
{
	/// <summary>Observed process facts. Arguments include the executable at index zero.</summary>
	public sealed class ScenarioProcessIdentity
	{
		public int Pid;
		public long StartTicks;
		public string Executable;
		public string[] Arguments;
	}

	/// <summary>
	/// Pure ownership classifier shared by portable tests and the Windows process adapter.
	/// No names, defaults, path normalization, or missing observations can establish ownership.
	/// </summary>
	public static class ScenarioProcessPolicy
	{
		public const string Schema = "taf-scenario-process-v1";
		private const string RootName = "taf-scenario.";

		public static bool ValidRoot(string Root)
		{
			if (Root == null || Root.Length <= 3 + RootName.Length
				|| !AsciiLetter(Root[0]) || Root[1] != ':' || Root[2] != '\\'
				|| !Root.Substring(3).StartsWith(RootName, StringComparison.OrdinalIgnoreCase))
				return false;
			for (int i = 3 + RootName.Length; i < Root.Length; i++)
				if (!AsciiLetter(Root[i]) && (Root[i] < '0' || Root[i] > '9')) return false;
			return true;
		}

		/// <summary>Returns null on invalid configuration; never repairs an ambiguous path.</summary>
		public static string[] ExpectedArguments(string Root, string Executable)
		{
			if (!ValidRoot(Root) || !CanonicalExecutable(Executable)) return null;
			return new[]
			{
				Executable, "-savepath", Root + "\\Save", "-sharedpath", Root + "\\Local",
				"-syncedpath", Root + "\\Synced", "-logFile", Root + "\\Player.log",
				"NOMETRICS", "STEAM:NO", "GALAXY:NO"
			};
		}

		public static bool ValidIdentity(string Root, string Executable, ScenarioProcessIdentity Identity)
		{
			string[] expected = ExpectedArguments(Root, Executable);
			if (expected == null || Identity == null || Identity.Pid <= 0
				|| Identity.StartTicks <= 0 || Identity.StartTicks > DateTime.MaxValue.Ticks
				|| !CanonicalExecutable(Identity.Executable)
				|| !string.Equals(Identity.Executable, Executable, StringComparison.OrdinalIgnoreCase)
				|| Identity.Arguments == null || Identity.Arguments.Length != expected.Length)
				return false;
			for (int i = 0; i < expected.Length; i++)
			{
				bool path = i == 0 || i == 2 || i == 4 || i == 6 || i == 8;
				if (!string.Equals(Identity.Arguments[i], expected[i],
					path ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return false;
			}
			return true;
		}

		public static string Decide(string Root, string Executable,
			ScenarioProcessIdentity Recorded, ScenarioProcessIdentity Live)
		{
			if (!ValidIdentity(Root, Executable, Recorded)) return "REFUSE";
			if (Live == null) return "ALREADY_EXITED";
			// Both identities must independently bind to the caller's exact executable and argv.
			if (!ValidIdentity(Root, Executable, Live)
				|| Live.Pid != Recorded.Pid || Live.StartTicks != Recorded.StartTicks) return "REFUSE";
			return "STOP_EXACT";
		}

		private static bool CanonicalExecutable(string Path)
		{
			if (string.IsNullOrEmpty(Path)) return false;
			int start, minimum;
			if (Path.Length >= 3 && AsciiLetter(Path[0]) && Path[1] == ':' && Path[2] == '\\')
			{
				start = 3;
				minimum = 1;
			}
			else if (Path.StartsWith("\\\\", StringComparison.Ordinal))
			{
				start = 2;
				minimum = 3; // Server, share, and a file component are all required.
			}
			else return false;
			int count = 0;
			for (int i = start; i <= Path.Length; i++)
			{
				if (i < Path.Length && Path[i] != '\\') continue;
				if (!CanonicalSegment(Path.Substring(start, i - start))) return false;
				count++;
				start = i + 1;
			}
			return count >= minimum;
		}

		private static bool CanonicalSegment(string Segment)
		{
			if (Segment.Length == 0 || Segment == "." || Segment == ".."
				|| Segment[Segment.Length - 1] == '.' || Segment[Segment.Length - 1] == ' ')
				return false;
			for (int i = 0; i < Segment.Length; i++)
				if (char.IsControl(Segment[i]) || "\"<>|?*:/".IndexOf(Segment[i]) >= 0) return false;
			int dot = Segment.IndexOf('.');
			string stem = (dot < 0 ? Segment : Segment.Substring(0, dot)).TrimEnd(' ');
			if (EqualDevice(stem, "CON") || EqualDevice(stem, "PRN") || EqualDevice(stem, "AUX")
				|| EqualDevice(stem, "NUL") || EqualDevice(stem, "CONIN$") || EqualDevice(stem, "CONOUT$"))
				return false;
			if (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
				|| stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)))
			{
				char digit = stem[3];
				if ((digit >= '1' && digit <= '9') || digit == '\u00b9' || digit == '\u00b2' || digit == '\u00b3')
					return false;
			}
			return true;
		}

		private static bool EqualDevice(string Left, string Right)
		{
			return string.Equals(Left, Right, StringComparison.OrdinalIgnoreCase);
		}

		private static bool AsciiLetter(char Value)
		{
			return (Value >= 'A' && Value <= 'Z') || (Value >= 'a' && Value <= 'z');
		}
	}
}
