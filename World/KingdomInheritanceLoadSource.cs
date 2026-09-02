using System;
using System.IO;
using HarmonyLib;
using XRL;
using XRL.Core;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Prefix-only: LoadGame is async, and its AfterGameLoaded event occurs after awaits.
	/// AsyncLocal keeps overlapping coda or save-menu loads isolated until that event consumes it.</summary>
	[HarmonyPatch(typeof(XRLGame), "LoadGame")]
	internal static class KingdomInheritanceLoadGamePatch
	{
		private static void Prefix(string Path)
		{
			KingdomInheritanceLoadSourceFlow.Record(Path);
		}
	}

	internal static class KingdomInheritancePrimaryLoad
	{
		internal static KingdomInheritanceLoadKind TryConsume(XRLGame Game, out string Failure)
		{
			Failure = "";
			string source;
			if (!KingdomInheritanceLoadSourceFlow.TryConsume(out source))
			{
				Failure = "the successful load had no captured XRLGame.LoadGame source";
				return KingdomInheritanceLoadKind.Unknown;
			}
			if (Game == null || !KingdomSealReceipt.ValidId(Game.GameID))
			{
				Failure = "the successful load had no canonical target game identity";
				return KingdomInheritanceLoadKind.Unknown;
			}
			try
			{
				string stemName = Path.GetFileName(source);
				if (stemName != "Primary" && stemName != "Quick"
					&& stemName != "Checkpoint" && stemName != "Precognition")
				{
					Failure = "the successful load was not an exact supported target-save stem";
					return KingdomInheritanceLoadKind.Unknown;
				}
				string[] roots = new string[2]
				{
					DataManager.SyncedPath("Saves"),
					DataManager.SavePath("Saves")
				};
				string seenRoot = "";
				StringComparison rootComparison = Path.DirectorySeparatorChar == '\\'
					? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
				string lastFailure = "the source was outside both canonical save roots";
				for (int i = 0; i < roots.Length; i++)
				{
					string savesRoot = System.IO.Path.GetFullPath(roots[i])
						.TrimEnd(System.IO.Path.DirectorySeparatorChar,
							System.IO.Path.AltDirectorySeparatorChar);
					if (seenRoot.Length > 0 && string.Equals(seenRoot, savesRoot,
						rootComparison))
					{
						continue;
					}
					seenRoot = savesRoot;
					string gameDirectory = System.IO.Path.Combine(savesRoot, Game.GameID);
					string selectedGzip = System.IO.Path.Combine(gameDirectory,
						stemName + ".sav.gz");
					string selectedLegacy = System.IO.Path.Combine(gameDirectory,
						stemName + ".sav");
					if (!Directory.Exists(savesRoot) || !Directory.Exists(gameDirectory)
						|| (!File.Exists(selectedGzip) && !Directory.Exists(selectedGzip)
							&& !File.Exists(selectedLegacy) && !Directory.Exists(selectedLegacy)))
					{
						lastFailure = "a canonical root lacked the exact captured save source";
						continue;
					}
					DirectoryInfo rootInfo = new DirectoryInfo(savesRoot);
					DirectoryInfo gameInfo = new DirectoryInfo(gameDirectory);
					bool gzipExists = File.Exists(selectedGzip) || Directory.Exists(selectedGzip);
					bool legacyExists = File.Exists(selectedLegacy) || Directory.Exists(selectedLegacy);
					FileAttributes gzipAttributes = gzipExists
						? File.GetAttributes(selectedGzip) : (FileAttributes)0;
					FileAttributes legacyAttributes = legacyExists
						? File.GetAttributes(selectedLegacy) : (FileAttributes)0;
					long gzipLength = File.Exists(selectedGzip)
						? new FileInfo(selectedGzip).Length : 0L;
					long legacyLength = File.Exists(selectedLegacy)
						? new FileInfo(selectedLegacy).Length : 0L;
					KingdomInheritanceLoadKind kind =
						KingdomInheritanceStateRules.ClassifyExactLoadSource(source, savesRoot,
						Game.GameID, rootInfo.Attributes, gameInfo.Attributes, gzipExists,
						gzipAttributes, gzipLength, legacyExists, legacyAttributes, legacyLength,
						out lastFailure);
					if (kind != KingdomInheritanceLoadKind.Unknown)
					{
						return kind;
					}
				}
				Failure = lastFailure;
				return KingdomInheritanceLoadKind.Unknown;
			}
			catch (Exception ex)
			{
				Failure = "the successful load source could not prove a direct target-save file: "
					+ ex.Message;
				return KingdomInheritanceLoadKind.Unknown;
			}
		}
	}
}
