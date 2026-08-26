using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private bool PrepareReturnCallback(KingdomRealmArchive Archive,
			KingdomRealmCallbackReceipt Receipt, KingdomRealmCallbackScope Scope,
			string BeforeEffect, string AfterEffect,
			out bool InvokeAuthorized, out string Refusal,
			int BeforeStamp = int.MinValue, int AfterStamp = int.MinValue)
		{
			InvokeAuthorized = false;
			Refusal = "";
			if (Archive == null || Receipt == null || Scope == KingdomRealmCallbackScope.None ||
				BeforeEffect == null || AfterEffect == null ||
				BeforeEffect.Length > KingdomRealmCallbackReceipt.MaxEffectChars ||
				AfterEffect.Length > KingdomRealmCallbackReceipt.MaxEffectChars)
				return QuarantineReturn(Archive, "callback intent is unbounded", out Refusal);
			if (Receipt.Phase == KingdomRealmCallbackPhase.None)
			{
				if (!Archive.CurrentGraphMatches(this, out string failure) ||
					!ExactExileMirrors(Archive) ||
					!TradeTransitionProofMatches(Archive,
						RequireBound: ReturnCallbackTradeBound(Archive), out failure) ||
					!KingdomRealmArchive.TryCurrentGraphHash(this, out string graph, out failure) ||
					!Archive.TryAuthorityHash(Receipt, Scope, out string archiveGraph, out failure))
					return QuarantineReturn(Archive, failure, out Refusal);
				Receipt.Scope = Scope;
				Receipt.BeforeGraph = graph;
				Receipt.BeforeArchiveGraph = archiveGraph;
				Receipt.BeforeEffect = BeforeEffect;
				Receipt.AfterEffect = AfterEffect;
				Receipt.BeforeStamp = BeforeStamp;
				Receipt.AfterStamp = AfterStamp;
				Receipt.Phase = KingdomRealmCallbackPhase.Intent;
			}
			if (!Receipt.Validate() || Receipt.Scope != Scope || Receipt.BeforeEffect != BeforeEffect ||
				Receipt.AfterEffect != AfterEffect || Receipt.BeforeStamp != BeforeStamp ||
				Receipt.AfterStamp != AfterStamp)
				return QuarantineReturn(Archive, "callback receipt conflicts with frozen intent",
					out Refusal);
			if (Receipt.Phase == KingdomRealmCallbackPhase.Intent)
			{
				if (!Archive.CurrentGraphMatches(this, out string failure) ||
					!ExactExileMirrors(Archive) ||
					!TradeTransitionProofMatches(Archive,
						RequireBound: ReturnCallbackTradeBound(Archive), out failure) ||
					!KingdomRealmArchive.TryCurrentGraphHash(this, out string graph, out failure) ||
					!Archive.TryAuthorityHash(Receipt, Scope, out string archiveGraph, out failure) ||
					!string.Equals(graph, Receipt.BeforeGraph, StringComparison.Ordinal) ||
					!string.Equals(archiveGraph, Receipt.BeforeArchiveGraph,
						StringComparison.Ordinal))
					return QuarantineReturn(Archive,
						failure ?? "callback graph changed before attempt", out Refusal);
				Receipt.Phase = KingdomRealmCallbackPhase.Attempting;
				InvokeAuthorized = true;
			}
			return true;
		}

		private bool SettleReturnCallback(KingdomRealmArchive Archive,
			KingdomRealmCallbackReceipt Receipt, KingdomRealmCallbackDisposition Disposition,
			string ObservedEffect, out string Refusal, bool SeatSwapped = false)
		{
			Refusal = "";
			string failure = null;
			string graph = null;
			string archiveGraph = null;
			if (Receipt == null || Receipt.Phase != KingdomRealmCallbackPhase.Attempting ||
				Disposition == KingdomRealmCallbackDisposition.None ||
				ObservedEffect == null ||
				ObservedEffect.Length > KingdomRealmCallbackReceipt.MaxEffectChars ||
				!(SeatSwapped ? Archive.CurrentGraphMatchesAfterSeat(this, true, out failure) :
					Archive.CurrentGraphMatches(this, out failure)) ||
				!ExactExileMirrors(Archive) ||
				!TradeTransitionProofMatches(Archive,
					RequireBound: ReturnCallbackTradeBound(Archive), out failure) ||
				!KingdomRealmArchive.TryCurrentGraphHash(this, out graph, out failure) ||
				!Archive.TryAuthorityHash(Receipt, Receipt.Scope, out archiveGraph, out failure) ||
				!string.Equals(archiveGraph, Receipt.BeforeArchiveGraph,
					StringComparison.Ordinal) ||
				((Receipt.Scope == KingdomRealmCallbackScope.Ability ||
				  Receipt.Scope == KingdomRealmCallbackScope.Reputation) &&
				 !string.Equals(graph, Receipt.BeforeGraph, StringComparison.Ordinal)))
				return QuarantineReturn(Archive, failure ?? "callback could not settle exact graph",
					out Refusal);
			Receipt.AfterGraph = graph;
			Receipt.AfterArchiveGraph = archiveGraph;
			Receipt.ObservedEffect = ObservedEffect;
			Receipt.Disposition = Disposition;
			Receipt.Phase = KingdomRealmCallbackPhase.Settled;
			return Receipt.Validate();
		}

		private bool SettledCallbackStillMatches(KingdomRealmArchive Archive,
			KingdomRealmCallbackReceipt Receipt, string ObservedEffect, out string Refusal)
		{
			Refusal = "";
			string failure = null;
			if (Receipt == null || !Receipt.Validate() ||
				Receipt.Phase != KingdomRealmCallbackPhase.Settled ||
				!string.Equals(ObservedEffect, Receipt.ObservedEffect, StringComparison.Ordinal) ||
				!Archive.CurrentGraphMatches(this, out failure) ||
				!ExactExileMirrors(Archive) ||
				!TradeTransitionProofMatches(Archive,
					RequireBound: ReturnCallbackTradeBound(Archive), out failure) ||
				!KingdomRealmArchive.TryCurrentGraphHash(this, out string graph, out failure) ||
				!Archive.TryAuthorityHash(Receipt, Receipt.Scope, out string archiveGraph, out failure) ||
				!string.Equals(graph, Receipt.AfterGraph, StringComparison.Ordinal) ||
				!string.Equals(archiveGraph, Receipt.AfterArchiveGraph,
					StringComparison.Ordinal))
				return QuarantineReturn(Archive, failure ??
					"settled callback proof no longer matches exact poststate", out Refusal);
			return true;
		}

		private bool TradeTransitionProofMatches(KingdomRealmArchive Archive,
			bool RequireBound, out string Failure)
		{
			Failure = null;
			if (Archive == null || TradeBook == null ||
				!KingdomTradeRules.TryAuthenticateExactExileClosedTick(TradeBook, Archive.RealmId,
					Archive.SettlementIds, out long closedTick, out Failure) ||
				closedTick != Archive.ClosedTick)
			{
				Failure = Failure ?? "Trade exile receipt differs from archive";
				return false;
			}
			if (!RequireBound)
			{
				if (!TradeBook.IdentityBound) return true;
				Failure = "Trade must remain unbound before returned realm publication";
				return false;
			}
			if (!KingdomTradeRules.BookUsable(TradeBook) ||
				!string.Equals(TradeBook.RealmId, Archive.RealmId, StringComparison.Ordinal) ||
				TradeBook.SettlementIds == null ||
				TradeBook.SettlementIds.Count != Archive.SettlementIds.Count)
			{
				Failure = "Trade is not bound to the returned exact realm topology";
				return false;
			}
			for (int i = 0; i < Archive.SettlementIds.Count; i++)
				if (!string.Equals(TradeBook.SettlementIds[i], Archive.SettlementIds[i],
					StringComparison.Ordinal))
				{
					Failure = "Trade returned settlement topology differs from archive";
					return false;
				}
			return true;
		}

		private static bool ExactStringRows(List<string> Left, List<string> Right)
		{
			if (Left == null || Right == null || Left.Count != Right.Count) return false;
			for (int i = 0; i < Left.Count; i++)
				if (!string.Equals(Left[i], Right[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static bool ReturnCallbackTradeBound(KingdomRealmArchive Archive)
		{
			return Archive != null &&
				(Archive.Phase == KingdomRealmArchivePhase.Restored ||
				 Archive.Phase == KingdomRealmArchivePhase.ReturnCleaning);
		}

		private bool DispatchReturnReputation(KingdomRealmArchive Archive, out string Refusal)
		{
			Refusal = "";
			Faction realm = Factions.GetIfExists(Archive.FactionName);
			if (!TryReputationEffect(realm, Archive, Desired: false, out string before) ||
				!TryReputationEffect(realm, Archive, Desired: true, out string after))
				return QuarantineReturn(Archive, "reputation graph cannot be bounded", out Refusal);
			KingdomRealmCallbackReceipt receipt = Archive.ReturnReputation;
			if (receipt.Phase == KingdomRealmCallbackPhase.Settled)
				return TryReputationEffect(realm, Archive, Desired: false, out string settled) &&
					string.Equals(settled, receipt.AfterEffect,
					StringComparison.Ordinal) && CurrentRealmMatchesArchive(Archive) &&
					SettledCallbackStillMatches(Archive, receipt, settled, out Refusal);
			if (receipt.Phase != KingdomRealmCallbackPhase.None)
			{
				before = receipt.BeforeEffect; after = receipt.AfterEffect;
			}
			if (!PrepareReturnCallback(Archive, receipt, KingdomRealmCallbackScope.Reputation,
				before, after,
				out bool invokeAuthorized, out Refusal)) return false;
			if (!TryReputationEffect(realm, Archive, Desired: false, out string current))
				return QuarantineReturn(Archive, "reputation graph cannot be inspected", out Refusal);
			if (current == after)
				return SettleReturnCallback(Archive, receipt,
					before == after ? KingdomRealmCallbackDisposition.Skipped :
					KingdomRealmCallbackDisposition.Delivered, current, out Refusal);
			if (current != before)
				return QuarantineReturn(Archive, "reputation callback reached a third value",
					out Refusal);
			if (realm == null)
				return SettleReturnCallback(Archive, receipt,
					KingdomRealmCallbackDisposition.Skipped, current, out Refusal);
			if (!invokeAuthorized)
				return QuarantineReturn(Archive,
					"reputation callback was interrupted before exact poststate publication",
					out Refusal);
			XRLGame gameReference = The.Game;
			Reputation reputationReference = gameReference.PlayerReputation;
			Dictionary<string, float> valuesReference = reputationReference.ReputationValues;
			Dictionary<string, string> ranksReference = reputationReference.FactionRanks;
			List<WorshipTracking> worshipReference = reputationReference.WorshipTracking;
			List<WorshipTracking> blasphemyReference = reputationReference.BlasphemyTracking;
			Dictionary<string, int> feelingReference = realm.FactionFeeling;
			The.Game.PlayerReputation.Set(realm, Archive.ReturnRegard);
			if (!ReferenceEquals(The.Game, gameReference) ||
				!ReferenceEquals(The.Game.PlayerReputation, reputationReference) ||
				!ReferenceEquals(reputationReference.ReputationValues, valuesReference) ||
				!ReferenceEquals(reputationReference.FactionRanks, ranksReference) ||
				!ReferenceEquals(reputationReference.WorshipTracking, worshipReference) ||
				!ReferenceEquals(reputationReference.BlasphemyTracking, blasphemyReference) ||
				!ReferenceEquals(Factions.GetIfExists(Archive.FactionName), realm) ||
				!ReferenceEquals(realm.FactionFeeling, feelingReference) ||
				!TryReputationEffect(realm, Archive, Desired: false, out current) || current != after)
				return QuarantineReturn(Archive, "reputation callback did not publish exact target",
					out Refusal);
			return SettleReturnCallback(Archive, receipt,
				KingdomRealmCallbackDisposition.Delivered, current, out Refusal);
		}

		private static bool TryReputationEffect(Faction Realm, KingdomRealmArchive Archive,
			bool Desired, out string Effect)
		{
			Effect = null;
			if (Realm == null) { Effect = "absent"; return true; }
			try
			{
				Reputation reputation = The.Game?.PlayerReputation;
				if (reputation?.ReputationValues == null || reputation.FactionRanks == null ||
					reputation.WorshipTracking == null || reputation.BlasphemyTracking == null ||
					Realm.FactionFeeling == null || reputation.ReputationValues.Count > 4096 ||
					reputation.FactionRanks.Count > 4096 ||
					reputation.WorshipTracking.Count > 4096 ||
					reputation.BlasphemyTracking.Count > 4096 || Realm.FactionFeeling.Count > 4096 ||
					!string.Equals(Realm.Name, Archive.FactionName, StringComparison.Ordinal)) return false;
				using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
				using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream,
					new System.Text.UTF8Encoding(false, true), true))
				{
					writer.Write(0x54525031); // TRP1
					WriteProofString(writer, Realm.Name); writer.Write(Realm.ID);
					List<string> valueKeys = new List<string>(reputation.ReputationValues.Keys);
					if (!valueKeys.Contains(Archive.FactionName)) valueKeys.Add(Archive.FactionName);
					valueKeys.Sort(StringComparer.Ordinal); writer.Write(valueKeys.Count);
					for (int i = 0; i < valueKeys.Count; i++)
					{
						WriteProofString(writer, valueKeys[i]);
						if (Desired && valueKeys[i] == Archive.FactionName)
							writer.Write((float)Archive.ReturnRegard);
						else if (reputation.ReputationValues.TryGetValue(valueKeys[i], out float value))
							writer.Write(value);
						else writer.Write(float.NaN);
					}
					WriteProofStringDictionary(writer, reputation.FactionRanks);
					WriteWorshipProof(writer, reputation.WorshipTracking);
					WriteWorshipProof(writer, reputation.BlasphemyTracking);
					List<string> feelingKeys = new List<string>(Realm.FactionFeeling.Keys);
					if (!feelingKeys.Contains("Player")) feelingKeys.Add("Player");
					feelingKeys.Sort(StringComparer.Ordinal); writer.Write(feelingKeys.Count);
					for (int i = 0; i < feelingKeys.Count; i++)
					{
						WriteProofString(writer, feelingKeys[i]);
						if (Desired && feelingKeys[i] == "Player")
							writer.Write(Reputation.GetFeeling((float)Archive.ReturnRegard));
						else if (Realm.FactionFeeling.TryGetValue(feelingKeys[i], out int value))
							writer.Write(value);
						else writer.Write(int.MinValue);
					}
					return FinishProofHash(stream, writer, out Effect);
				}
			}
			catch { return false; }
		}

	}
}
