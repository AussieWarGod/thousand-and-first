using System;
using System.Collections.Generic;
using XRL;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		internal bool TryDescribeCurrentSuccession(KingdomSystem System,
			out string Description, out string Failure)
		{
			Description = null;
			KingdomSuccessionConfiguration config;
			if (!TryGetCurrentConfiguration(System, out config, out Failure)) return false;
			if (config.Choice == HeirChoice.Law)
			{
				Description = "Current custom: seniority. The longest-serving eligible resident inherits.";
				return true;
			}
			if (config.Choice == HeirChoice.Chosen)
			{
				string name;
				bool exact = TryCanonicalResidentName(System, config.ChosenResidentId, out name);
				string identity = exact ? KingdomPresentation.Rich(name) + " (resident "
					+ config.ChosenResidentId + ")" : "exact resident " + config.ChosenResidentId;
				Description = "Current custom: " + identity + " carries the next life; "
					+ (config.SeatCostEnabled ? "the senior heir keeps the Charter."
						: "the chosen life inherits the Charter.")
					+ (exact ? "" : " Exact-roll failure will fall back to seniority without a seat cost.");
				return true;
			}
			KingdomGroomingRecord grooming;
			bool present;
			if (!TryRefreshGrooming(System, false, out grooming, out present, out Failure)
				|| !present) return false;
			List<HeirRuntime> heirs;
			if (!TryReadHeirs(System, out heirs))
			{
				Failure = "The complete resident roll could not be read.";
				return false;
			}
			HeirRuntime nominee;
			bool exactNominee = TryUniqueHeir(heirs, grooming.ResidentId, true, out nominee);
			string shown = KingdomPresentation.Rich(exactNominee
				? nominee.Rule.Name : grooming.NomineeName);
			string progress = KingdomGroomingRules.Progress(grooming.ServiceMarks,
				grooming.StudyMarks);
			Description = "Current custom: groom " + shown + " (resident "
				+ grooming.ResidentId + ") as lawful successor. Progress: " + progress + ". "
				+ (!exactNominee ? "The exact identity is no longer uniquely eligible; seniority will answer."
					: (grooming.Ready ? "Preparation is complete; this resident inherits the next life and Charter."
						: "Preparation is incomplete; seniority answers if the founder dies now. "
							+ "Service needs a month on the roll or office; schooling needs this city "
							+ "to hold schooling and this resident to hold a knowledge post."));
			return true;
		}

		private bool TryRefreshGrooming(KingdomSystem System, bool CommitProgress,
			out KingdomGroomingRecord Record, out bool Present, out string Failure)
		{
			Record = default(KingdomGroomingRecord);
			Present = false;
			Failure = null;
			KingdomSuccessionConfiguration config;
			if (!TryGetCurrentConfiguration(System, out config, out Failure)) return false;
			if (config.Choice != HeirChoice.Groomed) return true;
			if (!TryReadRealmGrooming(System, out Record, out Present, out Failure)) return false;
			if (!Present || Record.ResidentId != config.ChosenResidentId)
			{
				Failure = "The groomed succession record does not match the Charter.";
				return false;
			}
			List<HeirRuntime> heirs;
			if (!TryReadHeirs(System, out heirs))
			{
				Failure = "The complete resident roll could not be read.";
				return false;
			}
			HeirRuntime nominee;
			if (!TryUniqueHeir(heirs, Record.ResidentId, true, out nominee)) return true;
			KingdomGroomingRecord advanced;
			if (!KingdomGroomingRecord.TryAdvance(Record, nominee.ServiceMarks,
				nominee.StudyMarks, out advanced))
			{
				if (nominee.ServiceMarks > Record.ServiceMarks
					|| nominee.StudyMarks > Record.StudyMarks)
				{
					Failure = "The grooming progress revision is full.";
					return false;
				}
				return true;
			}
			string wire = KingdomGroomingRecord.Encode(advanced);
			if (string.IsNullOrEmpty(wire))
			{
				Failure = "The grooming progress could not fit its bounded record.";
				return false;
			}
			if (CommitProgress) GroomingRecordWire = wire;
			Record = advanced;
			return true;
		}

		private bool TryReadRealmGrooming(KingdomSystem System,
			out KingdomGroomingRecord Record, out bool Present, out string Failure)
		{
			Record = default(KingdomGroomingRecord);
			Present = false;
			Failure = null;
			if (string.IsNullOrEmpty(GroomingRecordWire)) return true;
			if (!KingdomGroomingRecord.TryDecode(GroomingRecordWire, out Record))
			{
				Failure = "The saved grooming record is malformed.";
				return false;
			}
			if (System == null || !string.Equals(Record.RealmId, System.RealmId,
				StringComparison.Ordinal)) return true;
			Present = true;
			return true;
		}

		private static bool TryBuildGroomingRecord(KingdomSystem System,
			List<HeirRuntime> Heirs, int ResidentId, out KingdomGroomingRecord Record,
			out string Failure)
		{
			Record = default(KingdomGroomingRecord);
			Failure = null;
			HeirRuntime nominee;
			if (!TryUniqueHeir(Heirs, ResidentId, true, out nominee))
			{
				Failure = "That exact resident is no longer uniquely eligible.";
				return false;
			}
			long now = The.Game == null || The.Game.TimeTicks < 0L ? 0L : The.Game.TimeTicks;
			if (!KingdomGroomingRecord.TryCreate(System.RealmId, ResidentId,
				nominee.Rule.Name, now, nominee.ServiceMarks, nominee.StudyMarks, 0,
				out Record))
			{
				Failure = "That nomination cannot fit the bounded grooming record.";
				return false;
			}
			return true;
		}

		private static bool TryUniqueHeir(List<HeirRuntime> Heirs, int ResidentId,
			bool RequireEligible, out HeirRuntime Result)
		{
			Result = null;
			int count = 0;
			for (int i = 0; Heirs != null && i < Heirs.Count; i++)
				if (Heirs[i].Rule.ResidentId == ResidentId)
				{
					Result = Heirs[i];
					count++;
				}
			return count == 1 && (!RequireEligible
				|| KingdomSuccessionRules.Eligible(Result.Rule));
		}
	}
}
