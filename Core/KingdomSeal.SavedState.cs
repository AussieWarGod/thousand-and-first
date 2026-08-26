using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using XRL;
using XRL.Core;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSeal : IPlayerSystem
	{
		private void ValidateSavedState()
		{
			if (SealDisabled)
			{
				if (!KingdomSealEngineRules.IsCanonicalDisabledSealShape(LineageId, LegacyId,
					OriginGameId, Generation, Revision, LastPollTick, SealedLegacyId,
					LastAccessionToken, PendingAccessionToken))
				{
					throw new InvalidOperationException(
						"The disabled saved seal coordinator is not canonical.");
				}
				NeutralizeDisabledState();
				return;
			}
			bool none = string.IsNullOrEmpty(LineageId) && string.IsNullOrEmpty(LegacyId)
				&& string.IsNullOrEmpty(OriginGameId);
			if (!none && !HasCompleteIdentity)
			{
				throw new InvalidOperationException("The saved seal lineage identity is incomplete.");
			}
			if (!string.IsNullOrEmpty(SealedLegacyId)
				&& (!KingdomSealReceipt.ValidId(SealedLegacyId) || SealedLegacyId != LegacyId))
			{
				throw new InvalidOperationException("The saved retirement marker does not name the current legacy.");
			}
			LineageId = LineageId ?? "";
			LegacyId = LegacyId ?? "";
			OriginGameId = OriginGameId ?? "";
			SealedLegacyId = SealedLegacyId ?? "";
			LastAccessionToken = LastAccessionToken ?? "";
			PendingAccessionToken = PendingAccessionToken ?? "";
			string accessionFailure;
			if (none)
			{
				if (LastAccessionToken.Length != 0 || PendingAccessionToken.Length != 0)
				{
					throw new InvalidOperationException("An unfounded seal cannot carry accession state.");
				}
			}
			else if (!KingdomSealEngineRules.TryValidateAccessionTokens(Generation,
				LastAccessionToken, PendingAccessionToken, out accessionFailure))
			{
				throw new InvalidOperationException("The saved accession identity is invalid: "
					+ accessionFailure + ".");
			}
			LastPollTick = SafeTick(LastPollTick);
		}

		private static bool HasExactLegacy(KingdomSealStore Store, string Wanted,
			out string Failure)
		{
			Failure = "";
			int refused;
			List<KingdomSealRecord> legacies = Store.ReadLegacies(out refused);
			if (refused > 0)
			{
				Failure = "one or more immutable legacy files could not be validated";
				return false;
			}
			for (int i = 0; i < legacies.Count; i++)
			{
				if (legacies[i].LegacyId == Wanted)
				{
					return true;
				}
			}
			Failure = "the save marks legacy " + Wanted + " retired, but its immutable file is missing";
			return false;
		}

		private static bool TryExactScore(string Origin, out bool ExactScore, out string Failure)
		{
			ExactScore = false;
			Failure = "";
			if (!KingdomSealReceipt.ValidId(Origin))
			{
				Failure = "the terminal stage has no valid origin id";
				return false;
			}
			try
			{
				Scoreboard2 scoreboard = Scores.Scoreboard;
				if (scoreboard == null || scoreboard.Scores == null)
				{
					Failure = "the scoreboard could not be read";
					return false;
				}
				if (scoreboard.Scores.Count > MaxScoresScanned)
				{
					Failure = "the scoreboard exceeds the bounded reconciliation scan";
					return false;
				}
				for (int i = 0; i < scoreboard.Scores.Count; i++)
				{
					ScoreEntry2 entry = scoreboard.Scores[i];
					if (entry != null && string.Equals(entry.GameId, Origin, StringComparison.Ordinal))
					{
						ExactScore = true;
						break;
					}
				}
				return true;
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		private static KingdomSealPrimaryState ExactPrimaryState(string GameId,
			out string Failure)
		{
			try
			{
				return KingdomSealEngineRules.ExactPrimaryAcrossRoots(GameId,
					new[] { DataManager.SyncedPath("Saves"), DataManager.SavePath("Saves") },
					MaxSaveDirectoriesScanned, MaxSaveEntriesScanned, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return KingdomSealPrimaryState.Unknown;
			}
		}

		private static KingdomSealReceipt CopyReceipt(KingdomSealReceipt Source,
			KingdomSealReceiptState State, long WrittenTick)
		{
			return new KingdomSealReceipt
			{
				LineageId = Source.LineageId,
				LegacyId = Source.LegacyId,
				TargetGameId = Source.TargetGameId,
				State = State,
				WrittenTick = Math.Max(Source.WrittenTick, SafeTick(WrittenTick))
			};
		}

	}
}
