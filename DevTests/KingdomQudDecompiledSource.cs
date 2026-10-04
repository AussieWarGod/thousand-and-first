#if TAF_TESTS
using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Selects the ILSpy decompile that answers the "InstalledQud" source checks. The engine is
	/// the release target in Tools/workshop_metadata.py (GAME_CORE_BUILD): every selected root
	/// must declare exactly that AssemblyVersion in Properties/AssemblyInfo.cs, and an installed
	/// engine named by TAF_QUD_BASE must be that core too, so a replaced engine's decompile can
	/// never answer for the target. Without TAF_QUD_DECOMPILED the only default is the
	/// version-keyed archive (home)/coq/qud_helper/game_base/decompiled/(core)-ilspy9.1; when it
	/// is absent the caller skips, which every zero-skip licensed run refuses.
	/// </summary>
	internal static class KingdomQudDecompiledSource
	{
		internal const string Variable = "TAF_QUD_DECOMPILED";
		internal const string Decompiler = "ilspy9.1";

		internal static string Locate(string requiredRelative)
		{
			string pinned = PinnedCoreBuild();
			string root = Select(Environment.GetEnvironmentVariable(Variable),
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), pinned,
				requiredRelative);
			if (root == null) return null;
			string installed = InstalledCoreBuild();
			if (installed != null && !string.Equals(installed, pinned, StringComparison.Ordinal))
				throw new InvalidOperationException("Installed Qud core " + installed
					+ " is not the pinned core " + pinned + "; its decompile cannot answer for it.");
			return root;
		}

		internal static string Select(string supplied, string home, string pinned,
			string requiredRelative)
		{
			string root;
			if (supplied != null)
			{
				if (string.IsNullOrWhiteSpace(supplied) || !Directory.Exists(supplied))
					throw new InvalidOperationException(Variable
						+ " is set but is not a source directory.");
				root = supplied;
			}
			else
			{
				if (string.IsNullOrWhiteSpace(home)) return null;
				root = DefaultRoot(home, pinned);
				if (!Directory.Exists(root)) return null;
			}
			if (requiredRelative != null && !File.Exists(Path.Combine(root, requiredRelative)))
				throw new InvalidOperationException("Qud decompile root lacks " + requiredRelative
					+ ": " + root);
			string declared = DeclaredAssemblyVersion(root);
			if (!string.Equals(declared, pinned, StringComparison.Ordinal))
				throw new InvalidOperationException("Qud decompile root declares AssemblyVersion "
					+ declared + ", not the pinned core " + pinned + ": " + root);
			return root;
		}

		internal static string DefaultRoot(string home, string pinned)
		{
			return Path.Combine(home, "coq", "qud_helper", "game_base", "decompiled",
				pinned + "-" + Decompiler);
		}

		internal static string PinnedCoreBuild()
		{
			MatchCollection found = Regex.Matches(
				TestMain.ReadRepositoryText("Tools/workshop_metadata.py"),
				@"^GAME_CORE_BUILD\s*=\s*""(\d+\.\d+\.\d+\.\d+)""\s*$",
				RegexOptions.Multiline | RegexOptions.CultureInvariant);
			if (found.Count != 1)
				throw new InvalidOperationException(
					"Tools/workshop_metadata.py must own exactly one GAME_CORE_BUILD.");
			return found[0].Groups[1].Value;
		}

		internal static string DeclaredAssemblyVersion(string root)
		{
			string path = Path.Combine(root, "Properties", "AssemblyInfo.cs");
			if (!File.Exists(path))
				throw new InvalidOperationException(
					"Qud decompile root lacks Properties/AssemblyInfo.cs: " + root);
			MatchCollection found = Regex.Matches(File.ReadAllText(path),
				@"^\[assembly: AssemblyVersion\(""(\d+\.\d+\.\d+\.\d+)""\)\]\s*$",
				RegexOptions.Multiline | RegexOptions.CultureInvariant);
			if (found.Count != 1)
				throw new InvalidOperationException(
					"Qud decompile root must declare exactly one AssemblyVersion: " + path);
			return found[0].Groups[1].Value;
		}

		internal static string InstalledCoreBuild()
		{
			string supplied = Environment.GetEnvironmentVariable("TAF_QUD_BASE");
			if (supplied == null) return null;
			if (string.IsNullOrWhiteSpace(supplied))
				throw new InvalidOperationException("TAF_QUD_BASE is set but empty.");
			string path = Path.GetFullPath(Path.Combine(supplied, "..", "..", "Managed",
				"Assembly-CSharp.dll"));
			if (!File.Exists(path))
				throw new InvalidOperationException(
					"TAF_QUD_BASE does not resolve Assembly-CSharp.dll: " + supplied);
			return AssemblyName.GetAssemblyName(path).Version.ToString();
		}
	}
}
#endif
