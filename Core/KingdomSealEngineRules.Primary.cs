using System;
using System.Collections.Generic;
using System.IO;

namespace ThousandAndFirst
{
	internal static partial class KingdomSealEngineRules
	{
		/// <summary>
		/// Proves one exact Primary across every canonical engine save root. Roots are normalized
		/// and de-duplicated before inspection. Any ambiguous root wins over presence; presence
		/// wins over absence; absence is returned only when every distinct root proves absence.
		/// </summary>
		internal static KingdomSealPrimaryState ExactPrimaryAcrossRoots(string GameId,
			IList<string> SavesRoots, int MaxRootEntries, int MaxOriginEntries,
			out string Failure)
		{
			Failure = "";
			if (!KingdomSealReceipt.ValidId(GameId) || SavesRoots == null
				|| SavesRoots.Count == 0 || SavesRoots.Count > 8
				|| MaxRootEntries <= 0 || MaxOriginEntries <= 0)
			{
				Failure = "the game id, save roots, or scan bounds are invalid";
				return KingdomSealPrimaryState.Unknown;
			}
			try
			{
				StringComparison comparison = Path.DirectorySeparatorChar == '\\'
					? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
				List<string> roots = new List<string>();
				for (int i = 0; i < SavesRoots.Count; i++)
				{
					if (string.IsNullOrWhiteSpace(SavesRoots[i]))
					{
						Failure = "a canonical Saves root is missing";
						return KingdomSealPrimaryState.Unknown;
					}
					string root = Path.GetFullPath(SavesRoots[i]).TrimEnd(
						Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
					if (root.Length == 0 || !string.Equals(Path.GetFileName(root), "Saves",
						StringComparison.Ordinal))
					{
						Failure = "a canonical save root is not an exact Saves directory";
						return KingdomSealPrimaryState.Unknown;
					}
					bool duplicate = false;
					for (int j = 0; j < roots.Count; j++)
					{
						if (string.Equals(roots[j], root, comparison))
						{
							duplicate = true;
							break;
						}
					}
					if (!duplicate)
					{
						roots.Add(root);
					}
				}

				bool present = false;
				for (int i = 0; i < roots.Count; i++)
				{
					string rootFailure;
					KingdomSealPrimaryState state = ExactPrimaryInRoot(GameId, roots[i],
						MaxRootEntries, MaxOriginEntries, comparison, out rootFailure);
					if (state == KingdomSealPrimaryState.Unknown)
					{
						Failure = "save root " + i + " is ambiguous: " + rootFailure;
						return KingdomSealPrimaryState.Unknown;
					}
					present |= state == KingdomSealPrimaryState.Present;
				}
				return present ? KingdomSealPrimaryState.Present : KingdomSealPrimaryState.Absent;
			}
			catch (Exception ex)
			{
				Failure = "the canonical save roots could not be inspected: " + ex.Message;
				return KingdomSealPrimaryState.Unknown;
			}
		}

		private static KingdomSealPrimaryState ExactPrimaryInRoot(string GameId,
			string SavesRoot, int MaxRootEntries, int MaxOriginEntries,
			StringComparison Comparison, out string Failure)
		{
			Failure = "";
			if (!Directory.Exists(SavesRoot))
			{
				string parent = Path.GetDirectoryName(SavesRoot);
				if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent)
					|| !IsDirectDirectory(File.GetAttributes(parent)))
				{
					Failure = "the missing Saves root has no direct inspectable parent";
					return KingdomSealPrimaryState.Unknown;
				}
				int parentEntries = 0;
				foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
				{
					if (++parentEntries > MaxRootEntries)
					{
						Failure = "the Saves parent exceeds the bounded entry scan";
						return KingdomSealPrimaryState.Unknown;
					}
					if (string.Equals(Path.GetFullPath(entry), SavesRoot, Comparison)
						|| string.Equals(Path.GetFileName(entry), "Saves",
							StringComparison.OrdinalIgnoreCase))
					{
						Failure = "the nominally missing Saves root is an ambiguous filesystem entry";
						return KingdomSealPrimaryState.Unknown;
					}
				}
				return KingdomSealPrimaryState.Absent;
			}
			if (!IsDirectDirectory(File.GetAttributes(SavesRoot)))
			{
				Failure = "the Saves root is redirected or not a directory";
				return KingdomSealPrimaryState.Unknown;
			}

			string expectedOrigin = Path.GetFullPath(Path.Combine(SavesRoot, GameId));
			string origin = null;
			int rootEntries = 0;
			foreach (string entry in Directory.EnumerateFileSystemEntries(SavesRoot))
			{
				if (++rootEntries > MaxRootEntries)
				{
					Failure = "the Saves root exceeds the bounded entry scan";
					return KingdomSealPrimaryState.Unknown;
				}
				string entryName = Path.GetFileName(entry);
				if (!string.Equals(entryName, GameId, StringComparison.Ordinal))
				{
					if (string.Equals(entryName, GameId, StringComparison.OrdinalIgnoreCase))
					{
						Failure = "a case-variant origin entry may alias the exact game id";
						return KingdomSealPrimaryState.Unknown;
					}
					continue;
				}
				string fullEntry = Path.GetFullPath(entry);
				if (origin != null || !string.Equals(fullEntry, expectedOrigin, Comparison)
					|| !IsDirectDirectory(File.GetAttributes(fullEntry)))
				{
					Failure = "the exact origin entry is duplicate, redirected, or not a directory";
					return KingdomSealPrimaryState.Unknown;
				}
				origin = fullEntry;
			}
			if (!IsDirectDirectory(File.GetAttributes(SavesRoot)))
			{
				Failure = "the Saves root changed during inspection";
				return KingdomSealPrimaryState.Unknown;
			}
			if (origin == null)
			{
				return KingdomSealPrimaryState.Absent;
			}

			string expectedGzip = Path.GetFullPath(Path.Combine(origin, "Primary.sav.gz"));
			string expectedLegacy = Path.GetFullPath(Path.Combine(origin, "Primary.sav"));
			string gzipPath = null;
			string legacyPath = null;
			int originEntries = 0;
			foreach (string entry in Directory.EnumerateFileSystemEntries(origin))
			{
				if (++originEntries > MaxOriginEntries)
				{
					Failure = "the origin folder exceeds the bounded entry scan";
					return KingdomSealPrimaryState.Unknown;
				}
				string name = Path.GetFileName(entry);
				bool gzip = string.Equals(name, "Primary.sav.gz", StringComparison.Ordinal);
				bool legacy = string.Equals(name, "Primary.sav", StringComparison.Ordinal);
				if (!gzip && !legacy)
				{
					if (string.Equals(name, "Primary.sav.gz", StringComparison.OrdinalIgnoreCase)
						|| string.Equals(name, "Primary.sav", StringComparison.OrdinalIgnoreCase))
					{
						Failure = "a case-variant Primary entry may alias a loadable save";
						return KingdomSealPrimaryState.Unknown;
					}
					continue;
				}
				string fullEntry = Path.GetFullPath(entry);
				if ((gzip && (gzipPath != null || !string.Equals(fullEntry, expectedGzip,
					Comparison))) || (legacy && (legacyPath != null
					|| !string.Equals(fullEntry, expectedLegacy, Comparison))))
				{
					Failure = "the exact Primary entry is duplicate or escapes its origin";
					return KingdomSealPrimaryState.Unknown;
				}
				FileInfo primary = new FileInfo(fullEntry);
				if (!IsRegularPrimary(primary.Attributes, primary.Length))
				{
					Failure = "the exact Primary entry is not a nonempty regular file";
					return KingdomSealPrimaryState.Unknown;
				}
				if (gzip) gzipPath = fullEntry;
				else legacyPath = fullEntry;
			}
			if (!IsDirectDirectory(File.GetAttributes(origin)))
			{
				Failure = "the origin folder changed during inspection";
				return KingdomSealPrimaryState.Unknown;
			}
			if (gzipPath == null && legacyPath == null)
			{
				return KingdomSealPrimaryState.Absent;
			}
			if (!ReproveRegularPrimary(gzipPath) || !ReproveRegularPrimary(legacyPath))
			{
				Failure = "the exact Primary entry changed during inspection";
				return KingdomSealPrimaryState.Unknown;
			}
			return KingdomSealPrimaryState.Present;
		}

		private static bool ReproveRegularPrimary(string Pathname)
		{
			if (Pathname == null) return true;
			FileInfo proof = new FileInfo(Pathname);
			return IsRegularPrimary(proof.Attributes, proof.Length);
		}
	}
}
