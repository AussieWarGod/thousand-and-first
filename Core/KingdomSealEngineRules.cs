using System;
using System.Collections.Generic;
using System.IO;

namespace ThousandAndFirst
{
	/// <summary>
	/// What a bounded, synchronous look at one game's primary save proved. Absence is distinct
	/// from an I/O failure: only <see cref="Absent"/> may help prove an ended origin. Presence
	/// proves save durability, never that a lazy inherited zone was actually applied.
	/// </summary>
	internal enum KingdomSealPrimaryState
	{
		Unknown = 0,
		Absent = 1,
		Present = 2
	}

	/// <summary>
	/// Engine-facing state rules for <see cref="KingdomSeal"/>, kept free of engine types so the
	/// ownership and recovery matrix can be tested without running Qud.
	/// </summary>
	internal static class KingdomSealEngineRules
	{
		internal static bool SealAuthorityEnabled(bool CurrentReadFailed,
			bool PersistedDisabled)
		{
			return !CurrentReadFailed && !PersistedDisabled;
		}

		internal static bool PersistSealDisabled(bool CurrentReadFailed,
			bool PersistedDisabled)
		{
			return CurrentReadFailed || PersistedDisabled;
		}

		internal static bool IsCanonicalDisabledSealShape(string LineageId, string LegacyId,
			string OriginGameId, int Generation, int Revision, long LastPollTick,
			string SealedLegacyId, string LastAccessionToken, string PendingAccessionToken)
		{
			return LineageId == "" && LegacyId == "" && OriginGameId == ""
				&& Generation == 0 && Revision == 0 && LastPollTick == 0L
				&& SealedLegacyId == "" && LastAccessionToken == ""
				&& PendingAccessionToken == "";
		}

		/// <summary>Whether the daily missed-dirty poll is due. A backwards clock is due once so
		/// checkpoint/debug restoration re-anchors instead of waiting for a future it undid.</summary>
		internal static bool PollDue(long LastPollTick, long NowTick, long Period)
		{
			if (Period <= 0L)
			{
				return true;
			}
			long now = NowTick < 0L ? 0L : NowTick;
			long last = LastPollTick < 0L ? 0L : LastPollTick;
			if (last == 0L || now < last)
			{
				return true;
			}
			return now - last >= Period;
		}

		/// <summary>Advances a journal revision without wrapping. False means the lineage has
		/// exhausted the record format and must fail closed.</summary>
		internal static bool TryNextRevision(int Current, out int Next)
		{
			Next = Current;
			if (Current < 0 || Current == int.MaxValue)
			{
				return false;
			}
			Next = Current + 1;
			return true;
		}

		/// <summary>Mints the next generation ordinal without crossing the schema's declared
		/// ceiling.</summary>
		internal static bool TryNextGeneration(int Current, out int Next)
		{
			Next = Current;
			if (Current < 0 || Current >= 1024)
			{
				return false;
			}
			Next = Current + 1;
			return true;
		}

		/// <summary>Outside Kingdom Mode the profile coordinator observes player death. Inside
		/// Kingdom Mode succession owns the event and the coordinator must remain silent.</summary>
		internal static bool ObserveDeathDirectly(bool KingdomMode, bool Founded, bool GenerationSealed)
		{
			return !KingdomMode && Founded && !GenerationSealed;
		}

		/// <summary>Succession may ask for a terminal attempt only after it has ruled the line
		/// ended. This explicit call is the only Kingdom-mode death route.</summary>
		internal static bool AcceptSuccessionTerminal(bool KingdomMode, bool Founded,
			bool GenerationSealed, bool LineEnded)
		{
			return KingdomMode && Founded && !GenerationSealed && LineEnded;
		}

		/// <summary>Automatic promotion requires all three independent proofs: Store supplied a
		/// Terminal stage, score lookup found the exact origin id, and bounded save inspection
		/// proved the exact primary absent.</summary>
		internal static bool MayPromote(KingdomSealStatus Status, bool ExactScore,
			KingdomSealPrimaryState Primary)
		{
			return Status == KingdomSealStatus.Terminal && ExactScore
				&& Primary == KingdomSealPrimaryState.Absent;
		}

		/// <summary>Primary proof accepts only a nonempty regular file. Directories, symlinks,
		/// junctions, and empty placeholders remain ambiguous.</summary>
		internal static bool IsRegularPrimary(FileAttributes Attributes, long Length)
		{
			return Length > 0L
				&& (Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0;
		}

		internal static bool IsDirectDirectory(FileAttributes Attributes)
		{
			return (Attributes & FileAttributes.Directory) != 0
				&& (Attributes & FileAttributes.ReparsePoint) == 0;
		}

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

		internal static bool TryValidateAccessionTokens(int Generation, string Last,
			string Pending, out string Failure)
		{
			Failure = "";
			string last = Last ?? "";
			string pending = Pending ?? "";
			if (Generation < 0 || Generation > 1024 || last == pending && last.Length > 0)
			{
				Failure = "the accession token tuple is contradictory";
				return false;
			}
			if (Generation == 0)
			{
				if (last.Length == 0 && pending.Length == 0) return true;
				Failure = "the founder generation cannot carry an accession token";
				return false;
			}
			if (pending.Length == 0)
			{
				if (AccessionTokenIsOrdinal(last, Generation)) return true;
				Failure = "the completed accession token does not name the current generation";
				return false;
			}
			if (!AccessionTokenIsOrdinal(pending, Generation)
				|| (Generation == 1 ? last.Length != 0
					: !AccessionTokenIsOrdinal(last, Generation - 1)))
			{
				Failure = "the pending accession token does not name the adjacent generation";
				return false;
			}
			return true;
		}

		internal static bool AccessionTokenIsOrdinal(string Token, int Ordinal)
		{
			int parsed;
			long tick;
			return Ordinal > 0 && KingdomSuccessionRules.TryReadDeathToken(Token,
				out parsed, out tick) && parsed == Ordinal;
		}

		/// <summary>Whether a successful accession can hand the store from one generation to the
		/// next. A terminal attempt means succession already ruled the line ended and cannot become
		/// a successor. Retirement may advance because continued play does not rewrite its sealed
		/// legacy; it starts a new generation instead.</summary>
		internal static bool MayAdvanceGeneration(KingdomSealRecord Previous,
			KingdomSealRecord Successor)
		{
			if (Previous == null || Successor == null
				|| (Previous.Status != KingdomSealStatus.Living
					&& Previous.Status != KingdomSealStatus.Retired)
				|| Successor.Status != KingdomSealStatus.Living || Successor.IsResolved
				|| Previous.LineageId != Successor.LineageId
				|| Previous.OriginGameId != Successor.OriginGameId
				|| Previous.LegacyId == Successor.LegacyId
				|| !KingdomSealReceipt.ValidId(Successor.LegacyId)
				|| Previous.Generation < 0 || Previous.Generation >= 1024
				|| Successor.Generation != Previous.Generation + 1
				|| Previous.Revision < 0 || Previous.Revision == int.MaxValue
				|| Successor.Revision != Previous.Revision + 1)
			{
				return false;
			}
			return true;
		}

		/// <summary>A loaded primary may replace only its own living/attempt journal, or a
		/// strictly newer abandoned living/attempt generation of the same lineage and origin.
		/// Retirement is an explicit immutable action and is never rolled back here.</summary>
		internal static bool MayRestoreLoadedPrimary(KingdomSealRecord External,
			KingdomSealRecord SavedLiving)
		{
			if (External == null || SavedLiving == null
				|| (External.Status != KingdomSealStatus.Living
					&& External.Status != KingdomSealStatus.Terminal)
				|| SavedLiving.Status != KingdomSealStatus.Living
				|| External.LineageId != SavedLiving.LineageId
				|| External.OriginGameId != SavedLiving.OriginGameId)
			{
				return false;
			}
			if (External.Generation == SavedLiving.Generation)
			{
				return External.LegacyId == SavedLiving.LegacyId;
			}
			return External.Generation > SavedLiving.Generation
				&& External.Revision > SavedLiving.Revision
				&& External.LegacyId != SavedLiving.LegacyId;
		}

		/// <summary>
		/// Compares two living semantic snapshots while ignoring only journal mechanics: revision
		/// and written tick. Engine/writer versions remain facts, so an upgrade writes a new stage.
		/// </summary>
		internal static bool SameLivingSnapshot(KingdomSealRecord A, KingdomSealRecord B)
		{
			if (A == null || B == null || A.Status != KingdomSealStatus.Living
				|| B.Status != KingdomSealStatus.Living)
			{
				return false;
			}
			try
			{
				KingdomSealRecord a = KingdomSealRules.Copy(A);
				KingdomSealRecord b = KingdomSealRules.Copy(B);
				a.Revision = 0;
				b.Revision = 0;
				a.WrittenTick = 0L;
				b.WrittenTick = 0L;
				return string.Equals(a.Compose(), b.Compose(), StringComparison.Ordinal);
			}
			catch (Exception)
			{
				return false;
			}
		}
	}
}
