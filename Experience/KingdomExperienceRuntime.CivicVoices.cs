using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomExperienceRuntime
	{
		/// <summary>Builds optional prose around an owner-authored preview; never owns the choice.</summary>
		public static bool TryPrepareCivicVoice(KingdomSystem System,
			KingdomCivicVoiceFixture Fixture, int SourceVersion, string SourceId,
			string SettlementId, string Facts, long CauseTick,
			out KingdomCivicVoiceReceipt Receipt, out string Rendering)
		{
			Receipt = null; Rendering = Facts ?? "";
			try
			{
				// Master-off returns before experience state, options, residents, or bodies are read.
				if (!KingdomMaster.NewWorkAllowed(System)
					|| !TryVoiceSettlement(System, SettlementId, out KingdomCityBook book)
					|| !TryObserveConfiguredOptions(System, CauseTick, out string _)
					|| !KingdomExperienceRules.TryGetEnableEpoch(System.Experience,
						KingdomExperienceOptionKind.CivicStory, CauseTick, out long epoch,
						out string _)) return false;
				List<KingdomResidentRow> rows = VoiceRows(book);
				List<KingdomCivicVoiceCandidate> candidates =
					new List<KingdomCivicVoiceCandidate>(rows.Count);
				for (int i = 0; i < rows.Count; i++)
					if (rows[i].Standing == KingdomResidentStanding.Resident
						&& KingdomExperienceRules.CivicText(rows[i].Name, true))
						candidates.Add(new KingdomCivicVoiceCandidate(rows[i].ResidentId,
							rows[i].Name));
				KingdomCivicDecisionPreview preview = new KingdomCivicDecisionPreview
				{
					Fixture = Fixture, SourceVersion = SourceVersion, SourceId = SourceId,
					SettlementId = SettlementId, Facts = Facts, CauseTick = CauseTick,
					EnableEpoch = epoch
				};
				if (!KingdomCivicVoiceRules.TryPrepare(System.Experience, preview,
					candidates.ToArray(), out Receipt, out string _)) return false;
				bool first = CivicWitnessAvailable(System, SettlementId,
					Receipt.FirstResidentId);
				bool second = CivicWitnessAvailable(System, SettlementId,
					Receipt.SecondResidentId);
				Rendering = KingdomCivicVoiceRules.Render(Receipt, first, second);
				TryRecord(System, KingdomExperienceExperiment.CivicVoices,
					first && second ? KingdomExperienceTrialArm.SemanticOnly
						: KingdomExperienceTrialArm.FactsOnly,
					KingdomExperienceObservationKind.Exposed, 1);
				return true;
			}
			catch (Exception error)
			{
				KingdomLog.Log("civic voices: preview fell back to facts ("
					+ error.GetType().Name + ")");
				Receipt = null; Rendering = Facts ?? ""; return false;
			}
		}

		/// <summary>Best-effort post-outcome publication. Failure cannot roll back its source.</summary>
		public static bool TryPublishCivicVoice(KingdomSystem System,
			KingdomCivicVoiceReceipt Receipt)
		{
			try
			{
				if (!KingdomMaster.NewWorkAllowed(System) || System?.Experience == null
					|| Receipt == null) return false;
				bool published = KingdomCivicVoiceRules.TryPublish(System.Experience,
					System.Experience.Revision, Receipt, out string _);
				if (published) TryRecord(System, KingdomExperienceExperiment.CivicVoices,
					KingdomExperienceTrialArm.SemanticOnly,
					KingdomExperienceObservationKind.Committed, 1);
				return published;
			}
			catch (Exception error)
			{
				KingdomLog.Log("civic voices: publication refused ("
					+ error.GetType().Name + ")"); return false;
			}
		}

		/// <summary>Consumes at most one explicit later callback, never loading a zone.</summary>
		public static bool TryRecallCivicVoice(KingdomSystem System, long Tick,
			out string Text)
		{
			Text = null;
			try
			{
				if (!KingdomMaster.NewWorkAllowed(System) || System?.Experience?.Voices == null
					|| !KingdomExperienceRules.CanEmit(System.Experience,
						KingdomExperienceOptionKind.CivicStory, Tick)) return false;
				for (int i = 0; i < System.Experience.Voices.Count; i++)
				{
					KingdomCivicVoiceReceipt row = System.Experience.Voices[i];
					if (row.CallbackConsumed) continue;
					int resident = CivicWitnessAvailable(System, row.SettlementId,
						row.FirstResidentId)
						? row.FirstResidentId : CivicWitnessAvailable(System, row.SettlementId,
							row.SecondResidentId)
							? row.SecondResidentId : 0;
					if (resident == 0) continue;
					return KingdomCivicVoiceRules.TryConsumeCallback(System.Experience,
						System.Experience.Revision, row.SourceId, resident, true, Tick,
						out Text, out string _);
				}
				return false;
			}
			catch (Exception error)
			{
				KingdomLog.Log("civic voices: callback refused ("
					+ error.GetType().Name + ")"); return false;
			}
		}

		private static bool CivicWitnessAvailable(KingdomSystem System,
			string SettlementId, int ResidentId)
		{
			if (!TryVoiceSettlement(System, SettlementId, out KingdomCityBook book)) return false;
			List<KingdomResidentRow> rows = VoiceRows(book);
			bool standing = false;
			for (int i = 0; i < rows.Count; i++)
				if (rows[i].ResidentId == ResidentId)
				{
					standing = rows[i].Standing == KingdomResidentStanding.Resident; break;
				}
			return standing && KingdomResidents.TryResolveBoundBody(System, ResidentId, false,
				out GameObject _, out string zoneId)
				&& string.Equals(System.SettlementIdForOwnedZone(zoneId), SettlementId,
					StringComparison.Ordinal);
		}

		private static bool TryVoiceSettlement(KingdomSystem System, string SettlementId,
			out KingdomCityBook Book)
		{
			Book = null;
			if (System == null || !System.TryFindSettlement(SettlementId, out bool seated,
				out KingdomSettlement settlement)) return false;
			Book = seated ? System.City : settlement?.City;
			return Book != null && string.Equals(Book.SettlementId, SettlementId,
				StringComparison.Ordinal);
		}

		private static List<KingdomResidentRow> VoiceRows(KingdomCityBook Book)
		{
			List<KingdomResidentRow> rows = new List<KingdomResidentRow>();
			if (Book == null || !Book.TryRead(out KingdomCityState state,
				out KingdomCityFault _)) return rows;
			for (int i = 0; i < state.ResidentCount; i++)
				if (state.TryResident(i, out KingdomResidentRow row)
					&& KingdomResidentRules.OnTheRoll(row)) rows.Add(row);
			return rows;
		}
	}
}
