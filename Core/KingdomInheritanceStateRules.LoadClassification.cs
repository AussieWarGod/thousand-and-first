using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritanceStateRules
	{
		internal static KingdomInheritanceLoadKind ClassifyExactLoadSource(
			string SourceStem, string SavesRoot,
			string TargetGameId, FileAttributes SavesRootAttributes,
			FileAttributes GameDirectoryAttributes, bool GzipExists,
			FileAttributes GzipAttributes, long GzipLength, bool LegacyExists,
			FileAttributes LegacyAttributes, long LegacyLength, out string Failure)
		{
			Failure = "";
			if (string.IsNullOrWhiteSpace(SourceStem) || string.IsNullOrWhiteSpace(SavesRoot)
				|| !KingdomSealReceipt.ValidId(TargetGameId))
			{
				Failure = "the load source or target game identity was missing";
				return KingdomInheritanceLoadKind.Unknown;
			}
			try
			{
				if (!string.IsNullOrEmpty(Path.GetExtension(SourceStem)))
				{
					Failure = "the load source was not an extension-free save stem";
					return KingdomInheritanceLoadKind.Unknown;
				}
				string root = Path.GetFullPath(SavesRoot)
					.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				string source = Path.GetFullPath(SourceStem)
					.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				string leaf = Path.GetFileName(source);
				KingdomInheritanceLoadKind kind;
				if (leaf == "Primary")
				{
					kind = KingdomInheritanceLoadKind.Primary;
				}
				else if (leaf == "Quick" || leaf == "Checkpoint" || leaf == "Precognition")
				{
					kind = KingdomInheritanceLoadKind.SameGameRollback;
				}
				else
				{
					Failure = "the load source was not a supported exact save stem";
					return KingdomInheritanceLoadKind.Unknown;
				}
				string expected = Path.GetFullPath(Path.Combine(root, TargetGameId, leaf))
					.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				StringComparison comparison = Path.DirectorySeparatorChar == '\\'
					? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
				if (!string.Equals(source, expected, comparison)
					|| !string.Equals(Path.GetFileName(source), leaf, StringComparison.Ordinal)
					|| !string.Equals(Path.GetFileName(Path.GetDirectoryName(source)), TargetGameId,
						StringComparison.Ordinal)
					|| !string.Equals(Path.GetFileName(Path.GetDirectoryName(
						Path.GetDirectoryName(source))), "Saves", StringComparison.Ordinal))
				{
					Failure = "the load source was not an exact supported stem for the target game";
					return KingdomInheritanceLoadKind.Unknown;
				}
				if (!KingdomSealEngineRules.IsDirectDirectory(SavesRootAttributes)
					|| !KingdomSealEngineRules.IsDirectDirectory(GameDirectoryAttributes))
				{
					Failure = "the Saves root or target game directory was not direct";
					return KingdomInheritanceLoadKind.Unknown;
				}
				bool regularSelected = GzipExists
					? KingdomSealEngineRules.IsRegularPrimary(GzipAttributes, GzipLength)
					: LegacyExists && KingdomSealEngineRules.IsRegularPrimary(LegacyAttributes,
						LegacyLength);
				if (!regularSelected)
				{
					Failure = GzipExists
						? "the preferred .sav.gz source was not a nonempty regular file"
						: "neither the exact .sav.gz nor fallback .sav source was regular";
					return KingdomInheritanceLoadKind.Unknown;
				}
				return kind;
			}
			catch (Exception ex)
			{
				Failure = "the load path could not be normalized: " + ex.Message;
				return KingdomInheritanceLoadKind.Unknown;
			}
		}

	}
}
