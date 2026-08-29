using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		private const int MaxPendingConfigurationChronicleChars = 1024;
		private string SuccessionConfigurationWire;
		private string GroomingRecordWire;
		private string PendingConfigurationChronicle;
		private string PendingSelectionReceipt;
		private string CompletedSeatConsequenceToken;
		private string ActiveSeatClimbRealmId;
		private string ActiveSeatClimbToken;
		private int ActiveSeatKeeperResidentId;
		private string ActiveSeatKeeperName;
		private bool LegacySelectionReceiptUnavailable;
		internal bool TryGetCurrentConfiguration(KingdomSystem System,
			out KingdomSuccessionConfiguration Configuration, out string Failure)
		{
			Configuration = default(KingdomSuccessionConfiguration);
			Failure = null;
			if (System == null || !System.Founded || string.IsNullOrEmpty(System.RealmId))
			{
				Failure = "No founded realm can declare a succession custom.";
				return false;
			}
			if (!string.IsNullOrEmpty(SuccessionConfigurationWire))
			{
				KingdomSuccessionConfiguration stored;
				if (!KingdomSuccessionConfiguration.TryDecode(SuccessionConfigurationWire,
					out stored))
				{
					Failure = "The saved succession custom is malformed.";
					return false;
				}
				if (string.Equals(stored.RealmId, System.RealmId, StringComparison.Ordinal))
				{
					Configuration = stored;
					return true;
				}
			}
			if (!KingdomSuccessionConfiguration.TryDefault(System.RealmId, out Configuration))
			{
				Failure = "The realm identity is outside the succession bound.";
				return false;
			}
			return true;
		}
		internal bool TryGetSuccessionResidents(KingdomSystem System,
			out SuccessionResidentView[] Residents, out string Failure)
		{
			Residents = Array.Empty<SuccessionResidentView>();
			Failure = null;
			List<HeirRuntime> heirs;
			if (!TryReadHeirs(System, out heirs))
			{
				Failure = "The complete resident roll could not be read.";
				return false;
			}
			heirs.Sort((a, b) => KingdomSuccessionRules.Senior(a.Rule, b.Rule) ? -1
				: (KingdomSuccessionRules.Senior(b.Rule, a.Rule) ? 1 : 0));
			List<SuccessionResidentView> result = new List<SuccessionResidentView>();
			for (int i = 0; i < heirs.Count; i++)
				if (KingdomSuccessionRules.Eligible(heirs[i].Rule))
					result.Add(new SuccessionResidentView(heirs[i].Rule.ResidentId,
						heirs[i].Rule.Name, heirs[i].CityName, heirs[i].HomeName,
						heirs[i].ArrivedLabel, heirs[i].ServiceMarks, heirs[i].StudyMarks));
			Residents = result.ToArray();
			return true;
		}
		internal bool TryDescribeSuccessionCustom(KingdomSystem System, HeirChoice Choice,
			int ResidentId, bool SeatCostEnabled, out string Description, out string Failure)
		{
			Description = null;
			Failure = null;
			KingdomSuccessionConfiguration current;
			List<HeirRuntime> heirs;
			if (!TryGetCurrentConfiguration(System, out current, out Failure)
				|| !TryReadHeirs(System, out heirs))
			{
				Failure = Failure ?? "The complete resident roll could not be read.";
				return false;
			}
			if (current.Revision == int.MaxValue)
			{
				Failure = "The succession custom revision is full.";
				return false;
			}
			KingdomSuccessionConfiguration proposed;
			int revision = current.Revision + 1;
			if (!KingdomSuccessionConfiguration.TryCreate(System.RealmId, Choice, ResidentId,
				SeatCostEnabled, revision, out proposed))
			{
				Failure = "That succession custom is invalid.";
				return false;
			}
			KingdomHeir[] candidates = RulesOf(heirs);
			KingdomGroomingRecord grooming = default(KingdomGroomingRecord);
			bool hasGrooming = false;
			if (Choice == HeirChoice.Groomed)
			{
				if (!TryBuildGroomingRecord(System, heirs, ResidentId, out grooming,
					out Failure)) return false;
				hasGrooming = true;
			}
			KingdomSuccessionSelection selection;
			if (!KingdomSuccessionRules.TryResolveConfiguredHeir(candidates, proposed, grooming,
				hasGrooming, out selection))
			{
				Failure = "No eligible heir is presently on the roll.";
				return false;
			}
			if (Choice == HeirChoice.Chosen && !UniqueEligible(candidates, ResidentId))
			{
				Failure = "That exact resident is no longer uniquely eligible.";
				return false;
			}
			string heir = heirs[selection.HeirIndex].Rule.Name;
			string law = heirs[selection.LawHeirIndex].Rule.Name;
			string shownHeir = KingdomPresentation.Rich(heir);
			string shownLaw = KingdomPresentation.Rich(law);
			if (Choice == HeirChoice.Groomed)
			{
				Description = "Nominate {{C|" + KingdomPresentation.Rich(grooming.NomineeName)
					+ "}} (resident " + grooming.ResidentId + ") for lawful succession. Progress: "
					+ KingdomGroomingRules.Progress(grooming.ServiceMarks, grooming.StudyMarks)
					+ ". " + (grooming.Ready ? "If the founder died now, this resident would carry the next life and Charter."
						: "If the founder died now, seniority would raise " + shownLaw
							+ "; no chosen-life seat cost applies.");
				return true;
			}
			Description = "If the founder died now, {{C|" + shownHeir
				+ "}} would carry the next life."
				+ (selection.Choice == HeirChoice.Chosen
					? " Seniority names {{C|" + shownLaw + "}}. " + (selection.CostsTheSeat
						? shownLaw + " would keep the Charter; " + shownHeir
							+ " must earn trusted regard before claiming it."
						: shownHeir + " would inherit the Charter as well.")
					: " This is the realm's seniority law; no seat cost applies.");
			return true;
		}
		internal bool TryChangeSuccessionCustom(KingdomSystem System, HeirChoice Choice,
			int ResidentId, bool SeatCostEnabled, out string Failure)
		{
			Failure = null;
			if (!KingdomMaster.NewWorkAllowed(System))
			{
				Failure = "Settlement simulation is paused by the master option.";
				return false;
			}
			if (!TryPublishPendingConfiguration("before another custom"))
			{
				Failure = "The previous succession custom still awaits its Chronicle receipt.";
				return false;
			}
			string preview;
			if (!TryDescribeSuccessionCustom(System, Choice, ResidentId, SeatCostEnabled,
				out preview, out Failure)) return false;
			KingdomSuccessionConfiguration current, next;
			if (!TryGetCurrentConfiguration(System, out current, out Failure)
				|| !KingdomSuccessionConfiguration.TryRevise(current, Choice, ResidentId,
					SeatCostEnabled, out next))
			{
				Failure = "The Charter already keeps that exact custom, or its revision is full.";
				return false;
			}
			string wire = KingdomSuccessionConfiguration.Encode(next);
			string groomingWire = "";
			string canonicalName = "seniority";
			if ((Choice == HeirChoice.Chosen || Choice == HeirChoice.Groomed)
				&& !TryCanonicalResidentName(System, ResidentId, out canonicalName))
			{
				Failure = "That exact resident changed before the Charter could be written.";
				return false;
			}
			if (Choice == HeirChoice.Groomed)
			{
				List<HeirRuntime> heirs;
				KingdomGroomingRecord grooming;
				if (!TryReadHeirs(System, out heirs)
					|| !TryBuildGroomingRecord(System, heirs, ResidentId, out grooming,
						out Failure)) return false;
				groomingWire = KingdomGroomingRecord.Encode(grooming);
			}
			string line = KingdomPresentation.Rich(
				KingdomSuccessionRules.ConfigurationChronicle(Choice, canonicalName,
					ResidentId, SeatCostEnabled));
			if (string.IsNullOrEmpty(wire) || string.IsNullOrEmpty(line)
				|| (Choice == HeirChoice.Groomed && string.IsNullOrEmpty(groomingWire))
				|| line.Length > MaxPendingConfigurationChronicleChars)
			{
				Failure = "The custom could not fit its bounded record.";
				return false;
			}
			GroomingRecordWire = groomingWire;
			SuccessionConfigurationWire = wire;
			PendingConfigurationChronicle = line;
			if (!TryPublishPendingConfiguration("Charter confirmation"))
				Failure = "The custom is saved; its Chronicle line is queued for retry.";
			return true;
		}
		private bool TryPublishPendingConfiguration(string Context)
		{
			if (string.IsNullOrEmpty(PendingConfigurationChronicle)) return true;
			KingdomSuccessionConfiguration config;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (system == null || !system.Founded
				|| !KingdomSuccessionConfiguration.TryDecode(SuccessionConfigurationWire,
					out config)
				|| !string.Equals(config.RealmId, system.RealmId, StringComparison.Ordinal))
				return false;
			string eventId = KingdomSuccessionRules.ConfigurationEventId(config.RealmId,
				config.Revision);
			try
			{
				if (string.IsNullOrEmpty(eventId) || !KingdomChronicle.RecordOnce(system,
					eventId, PendingConfigurationChronicle)) return false;
				PendingConfigurationChronicle = "";
				return true;
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: succession custom Chronicle failed", ex);
				KingdomLog.Log("succession: custom Chronicle remains after " + Context);
				return false;
			}
		}
		private void TryRecoverLegacySelectionReceipt(KingdomSystem System)
		{
			if (!LegacySelectionReceiptUnavailable || string.IsNullOrEmpty(PendingDeathToken)
				|| !string.IsNullOrEmpty(PendingSelectionReceipt) || PendingHeirResidentId <= 0
				|| string.IsNullOrEmpty(PendingHeirName) || System == null
				|| string.IsNullOrEmpty(System.RealmId)) return;
			KingdomSuccessionSelectionReceipt receipt;
			if (!KingdomSuccessionSelectionReceipt.TryCreate(System.RealmId, PendingDeathToken,
				0, PendingHeirResidentId, PendingHeirName, PendingHeirResidentId,
				PendingHeirName, HeirChoice.Law, false, SuccessionSelectionReason.Seniority,
				out receipt)) return;
			PendingSelectionReceipt = KingdomSuccessionSelectionReceipt.Encode(receipt);
			LegacySelectionReceiptUnavailable = string.IsNullOrEmpty(PendingSelectionReceipt);
		}
		private void PrepareConfigurationRecovery(KingdomSystem System)
		{
			ReconcileAbandonedSeatClimb(System);
			TryRecoverLegacySelectionReceipt(System);
			if (System != null && System.Founded)
			{
				KingdomGroomingRecord ignored;
				bool present;
				string failure;
				if (!TryRefreshGrooming(System, true, out ignored, out present, out failure))
					KingdomLog.Log("succession: grooming progress waits (" + failure + ")");
			}
		}
		private void FinishConfigurationRecovery(KingdomSystem System, string Context)
		{
			TryPublishPendingConfiguration(Context);
			TrySettleSelectionConsequence(System, Context);
		}
		private static KingdomHeir[] RulesOf(List<HeirRuntime> Heirs)
		{
			KingdomHeir[] result = new KingdomHeir[Heirs.Count];
			for (int i = 0; i < result.Length; i++) result[i] = Heirs[i].Rule;
			return result;
		}
		private static bool UniqueEligible(KingdomHeir[] Heirs, int ResidentId)
		{
			int found = -1;
			for (int i = 0; i < Heirs.Length; i++)
				if (Heirs[i].ResidentId == ResidentId)
				{
					if (found >= 0) return false;
					found = i;
				}
			return found >= 0 && KingdomSuccessionRules.Eligible(Heirs[found]);
		}
		private static bool TryCanonicalResidentName(KingdomSystem System, int ResidentId,
			out string Name)
		{
			Name = null;
			List<HeirRuntime> heirs;
			if (!TryReadHeirs(System, out heirs)) return false;
			int count = 0;
			for (int i = 0; i < heirs.Count; i++)
				if (heirs[i].Rule.ResidentId == ResidentId
					&& KingdomSuccessionRules.Eligible(heirs[i].Rule))
				{
					Name = heirs[i].Rule.Name;
					count++;
				}
			return count == 1;
		}
	}
}
