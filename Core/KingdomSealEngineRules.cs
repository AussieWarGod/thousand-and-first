using System;
using System.Collections.Generic;
using System.IO;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-facing state rules for <see cref="KingdomSeal"/>, kept free of engine types so the
	/// ownership and recovery matrix can be tested without running Qud.
	/// </summary>
	internal static partial class KingdomSealEngineRules
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
	}
}
