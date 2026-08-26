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
		private bool DispatchReturnFeelings(KingdomRealmArchive Archive,
			out string Refusal)
		{
			Refusal = "";
			if (!TryFeelingEffect(Archive, Desired: false, out string before) ||
				!TryFeelingEffect(Archive, Desired: true, out string after))
				return QuarantineReturn(Archive, "feeling graph cannot be bounded", out Refusal);
			KingdomRealmCallbackReceipt receipt = Archive.ReturnFeelings;
			if (receipt.Phase != KingdomRealmCallbackPhase.None)
			{
				before = receipt.BeforeEffect; after = receipt.AfterEffect;
			}
			int targetSpoken = (int)KingdomExileRules.ClassifyRegard(Archive.ReturnRegard);
			int beforeSpoken = receipt.Phase == KingdomRealmCallbackPhase.None
				? Archive.RegardSpoken : receipt.BeforeStamp;
			if (!TryFeelingEffect(Archive, Desired: false, out string current))
				return QuarantineReturn(Archive, "feeling graph cannot be inspected", out Refusal);
			if (receipt.Phase == KingdomRealmCallbackPhase.Settled)
				return current == after && RegardSpoken == targetSpoken &&
					Archive.RegardSpoken == targetSpoken &&
					SettledCallbackStillMatches(Archive, receipt, current, out Refusal);
			if (!PrepareReturnCallback(Archive, receipt, KingdomRealmCallbackScope.Feelings,
				before, after,
				out bool invokeAuthorized, out Refusal, BeforeStamp: beforeSpoken,
				AfterStamp: targetSpoken)) return false;
			if (!TryFeelingEffect(Archive, Desired: false, out current))
				return QuarantineReturn(Archive, "feeling graph changed during intent", out Refusal);
			if (current != before && current != after)
				return QuarantineReturn(Archive, "feeling callback reached a third graph", out Refusal);
			bool stampBefore = RegardSpoken == beforeSpoken &&
				Archive.RegardSpoken == beforeSpoken;
			bool stampCut = RegardSpoken == targetSpoken &&
				Archive.RegardSpoken == beforeSpoken;
			bool stampAfter = RegardSpoken == targetSpoken &&
				Archive.RegardSpoken == targetSpoken;
			if (!stampBefore && !stampCut && !stampAfter)
				return QuarantineReturn(Archive,
					"feeling callback reached a third or reverse regard stamp", out Refusal);
			if (current == after)
			{
				if (!TrySettleFeelingStamp(Archive, beforeSpoken, targetSpoken))
					return QuarantineReturn(Archive,
						"feeling callback stamp could not settle exact poststate", out Refusal);
				return SettleReturnCallback(Archive, receipt,
					before == after && beforeSpoken == targetSpoken
						? KingdomRealmCallbackDisposition.Skipped :
					KingdomRealmCallbackDisposition.Delivered, current, out Refusal);
			}
			if (current != before)
				return QuarantineReturn(Archive,
					"feeling callback poststate lacks matching regard stamp", out Refusal);
			if (!stampBefore)
				return QuarantineReturn(Archive,
					"feeling callback stamp advanced without inspectable poststate", out Refusal);
			if (!invokeAuthorized)
				return QuarantineReturn(Archive,
					"feeling callback was interrupted before exact poststate publication", out Refusal);
			if (!TryCaptureFeelingReferences(out List<Faction> factionReferences,
				out List<Dictionary<string, int>> feelingReferences))
				return QuarantineReturn(Archive, "feeling reference graph cannot be bounded",
					out Refusal);
			ReassertFeelings();
			if (!FeelingReferencesStillMatch(factionReferences, feelingReferences) ||
				!TryFeelingEffect(Archive, Desired: false, out current) || current != after)
				return QuarantineReturn(Archive,
					"feeling callback did not publish the complete exact graph", out Refusal);
			if (!TrySettleFeelingStamp(Archive, beforeSpoken, targetSpoken))
				return QuarantineReturn(Archive,
					"feeling callback stamp could not publish exact poststate", out Refusal);
			return SettleReturnCallback(Archive, receipt,
				before == after && beforeSpoken == targetSpoken
					? KingdomRealmCallbackDisposition.Skipped :
				KingdomRealmCallbackDisposition.Delivered, current, out Refusal);
		}

		private bool TrySettleFeelingStamp(KingdomRealmArchive Archive, int Before, int After)
		{
			if (Archive == null || (RegardSpoken != Before && RegardSpoken != After) ||
				(Archive.RegardSpoken != Before && Archive.RegardSpoken != After) ||
				(Archive.RegardSpoken == After && RegardSpoken == Before)) return false;
			if (RegardSpoken == Before) RegardSpoken = After;
			if (Archive.RegardSpoken == Before) Archive.RegardSpoken = After;
			return RegardSpoken == After && Archive.RegardSpoken == After;
		}

		private bool TryFeelingEffect(KingdomRealmArchive Archive, bool Desired,
			out string Effect)
		{
			Effect = null;
			if (Archive?.Standings == null || Archive.Standings.Count > 512) return false;
			try
			{
				IReadOnlyList<Faction> source = Factions.GetList();
				if (source == null || source.Count > 4096) return false;
				List<Faction> factions = new List<Faction>(source.Count);
				for (int i = 0; i < source.Count; i++)
					if (source[i] != null) factions.Add(source[i]);
				factions.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
				using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
				using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream,
					new System.Text.UTF8Encoding(false, true), true))
				{
					writer.Write(0x54464631); // TFF1
					writer.Write(factions.Count);
					for (int i = 0; i < factions.Count; i++)
					{
						Faction faction = factions[i];
						if (i > 0 && faction.Name == factions[i - 1].Name) return false;
						if (faction.FactionFeeling == null || faction.FactionFeeling.Count > 4096)
							return false;
						WriteProofString(writer, faction.Name); writer.Write(faction.ID);
						List<string> keys = new List<string>(faction.FactionFeeling.Keys);
						bool mirrorsStanding = Archive.Standings.ContainsKey(faction.Name);
						if (Desired && mirrorsStanding && !keys.Contains(Archive.FactionName))
							keys.Add(Archive.FactionName);
						bool mirrorsPlayer = faction.Name == Archive.FactionName;
						if (Desired && mirrorsPlayer && !keys.Contains("Player")) keys.Add("Player");
						keys.Sort(StringComparer.Ordinal); writer.Write(keys.Count);
						for (int j = 0; j < keys.Count; j++)
						{
							WriteProofString(writer, keys[j]);
							if (Desired && mirrorsStanding && keys[j] == Archive.FactionName)
								writer.Write(Reputation.GetFeeling(
									(float)Archive.Standings[faction.Name]));
							else if (Desired && mirrorsPlayer && keys[j] == "Player")
								writer.Write(Reputation.GetFeeling((float)Archive.ReturnRegard));
							else writer.Write(faction.FactionFeeling[keys[j]]);
						}
					}
					return FinishProofHash(stream, writer, out Effect);
				}
			}
			catch { return false; }
		}

		private static bool TryCaptureFeelingReferences(out List<Faction> FactionReferences,
			out List<Dictionary<string, int>> FeelingReferences)
		{
			FactionReferences = new List<Faction>();
			FeelingReferences = new List<Dictionary<string, int>>();
			IReadOnlyList<Faction> source = Factions.GetList();
			if (source == null || source.Count > 4096) return false;
			for (int i = 0; i < source.Count; i++)
				if (source[i] != null) FactionReferences.Add(source[i]);
			FactionReferences.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
			for (int i = 0; i < FactionReferences.Count; i++)
			{
				if (FactionReferences[i].FactionFeeling == null ||
					(i > 0 && FactionReferences[i].Name == FactionReferences[i - 1].Name)) return false;
				FeelingReferences.Add(FactionReferences[i].FactionFeeling);
			}
			return true;
		}

		private static bool FeelingReferencesStillMatch(List<Faction> FactionReferences,
			List<Dictionary<string, int>> FeelingReferences)
		{
			if (!TryCaptureFeelingReferences(out List<Faction> currentFactions,
				out List<Dictionary<string, int>> currentFeelings) ||
				FactionReferences == null || FeelingReferences == null ||
				FactionReferences.Count != currentFactions.Count ||
				FeelingReferences.Count != currentFeelings.Count) return false;
			for (int i = 0; i < FactionReferences.Count; i++)
				if (!ReferenceEquals(FactionReferences[i], currentFactions[i]) ||
					!ReferenceEquals(FeelingReferences[i], currentFeelings[i])) return false;
			return true;
		}

		private bool DispatchReturnChronicle(KingdomRealmArchive Archive,
			out string Refusal)
		{
			string eventId = "taf:realm:return:v1:" + Archive.RealmId;
			string telling = KingdomExileRules.ReturnTelling(
				KingdomPresentation.Rich(Archive.DisplayName));
			return DispatchRealmChronicle(Archive, Archive.ReturnChronicle, eventId, telling,
				"return", out Refusal);
		}

		private static bool ChronicleDeclarationMatchesArchive(KingdomRealmArchive Archive,
			KingdomChronicleDeclaration Declaration, out string Failure)
		{
			Failure = null;
			if (Archive == null || Declaration == null ||
				!DeclarationListMatches(Archive.ChronicleEntries, "official",
					Declaration.Official, Declaration.OfficialBefore,
					Declaration.OfficialAfter) ||
				!DeclarationListMatches(Archive.OutsiderEntries, "outsider",
					Declaration.Outsider, Declaration.OutsiderBefore,
					Declaration.OutsiderAfter))
			{
				Failure = "archived Chronicle declaration lists differ from frozen CAS";
				return false;
			}
			return true;
		}

		private static bool DeclarationListMatches(List<string> Values, string Domain,
			string DeclaredValue, string BeforeHash, string AfterHash)
		{
			if (Values == null || string.IsNullOrEmpty(DeclaredValue) ||
				!KingdomChronicleReceiptRules.TryHashList(Domain, Values,
					out string current)) return false;
			if (current == BeforeHash)
				return KingdomChronicleReceiptRules.TryHashAfter(Domain, Values, DeclaredValue,
					out string declaredAfter) && declaredAfter == AfterHash;
			return current == AfterHash && Values.Count > 0 &&
				string.Equals(Values[Values.Count - 1], DeclaredValue,
					StringComparison.Ordinal);
		}

		private bool TryValidateChronicleLists(KingdomChronicleDeclaration Declaration,
			KingdomChronicleReceipt EventReceipt, bool Present, bool Terminal,
			out string OfficialHash, out string OutsiderHash, out bool ListLost)
		{
			OfficialHash = null; OutsiderHash = null; ListLost = false;
			if (Declaration == null ||
				!DeclarationListMatches(ChronicleEntries, "official", Declaration.Official,
					Declaration.OfficialBefore, Declaration.OfficialAfter) ||
				!DeclarationListMatches(OutsiderEntries, "outsider", Declaration.Outsider,
					Declaration.OutsiderBefore, Declaration.OutsiderAfter) ||
				!KingdomChronicleReceiptRules.TryHashList("official", ChronicleEntries,
					out OfficialHash) ||
				!KingdomChronicleReceiptRules.TryHashList("outsider", OutsiderEntries,
					out OutsiderHash)) return false;
			if (!Present)
				return !Terminal && EventReceipt == null &&
					OfficialHash == Declaration.OfficialBefore &&
					OutsiderHash == Declaration.OutsiderBefore;
			if (EventReceipt == null ||
				(!EventReceipt.Compact &&
				 (!string.Equals(EventReceipt.Official, Declaration.Official,
					 StringComparison.Ordinal) ||
				  !string.Equals(EventReceipt.Outsider, Declaration.Outsider,
					 StringComparison.Ordinal) ||
				  EventReceipt.OfficialBefore != Declaration.OfficialBefore ||
				  EventReceipt.OfficialAfter != Declaration.OfficialAfter ||
				  EventReceipt.OutsiderBefore != Declaration.OutsiderBefore ||
				  EventReceipt.OutsiderAfter != Declaration.OutsiderAfter))) return false;
			return KingdomRealmCallbackProofRules.ChronicleListsMatch(
				EventReceipt.OfficialState, OfficialHash, Declaration.OfficialBefore,
				Declaration.OfficialAfter, EventReceipt.OutsiderState, OutsiderHash,
				Declaration.OutsiderBefore, Declaration.OutsiderAfter, Terminal,
				out ListLost);
		}

	}
}
