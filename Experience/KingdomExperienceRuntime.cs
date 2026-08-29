using System;
using System.IO;
using System.Text;
using XRL;
using XRL.UI;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>Narrow engine seam for W0. It reads options, the existing binding registry and one
	/// local export path. It never resolves a body, loads a zone, rewards value, or chooses content.</summary>
	public static partial class KingdomExperienceRuntime
	{
		public const string StoryOptionId = KingdomExperienceOptions.StoryOptionId;
		public const string KnowledgeOptionId = KingdomExperienceOptions.KnowledgeOptionId;
		public const string AmbientOptionId = KingdomExperienceOptions.AmbientOptionId;
		public const string TelemetryOptionId = KingdomExperienceOptions.TelemetryOptionId;
		private const string ExportFolder = "ThousandAndFirst";
		private const string ExportFile = "experience-session.tsv";
		private static KingdomExperienceTelemetryBuffer Session;

		public static bool TryObserveConfiguredOptions(KingdomSystem System, long Tick,
			out string Failure)
		{
			Failure = null;
			if (System == null) { Failure = "experience system is absent"; return false; }
			// The master-off steady path ends before any experience option or collection is read.
			if (!KingdomMaster.NewWorkAllowed(System)) return true;
			if (!System.Founded) return true;
			if (System.Experience == null) System.Experience = new KingdomExperienceLedger();
			KingdomExperienceRules.Normalize(System.Experience);
			if (!KingdomExperienceRules.TryRebindEmptyIdentity(System.Experience, System.RealmId,
				out Failure)) return false;
			bool story = Options.GetOption(StoryOptionId, "Yes") != "No";
			bool knowledge = Options.GetOption(KnowledgeOptionId, "Yes") != "No";
			bool ambient = Options.GetOption(AmbientOptionId, "Yes") != "No";
			if (!KingdomExperienceRules.TryObserveOptions(System.Experience,
				System.Experience.Revision, story, knowledge, ambient, Tick, out Failure)) return false;
			if (Options.GetOption(TelemetryOptionId, "No") == "No") Session = null;
			return true;
		}

		public static bool TryReserveAudience(KingdomSystem System,
			KingdomExperienceAudienceReceipt Request, out KingdomExperienceCapacityFault Fault,
			out string Failure)
		{
			if (!PrepareReservation(System, Request?.RealmId, Request?.SettlementId,
				Request == null ? -1L : Request.ReservedTick, out Fault, out Failure)) return false;
			return KingdomExperienceRules.TryReserveAudience(System.Experience,
				System.Experience.Revision, Request, out Fault, out Failure);
		}

		public static bool TryReserveBodies(KingdomSystem System,
			KingdomExperienceBodyReservation Request, out KingdomExperienceCapacityFault Fault,
			out string Failure)
		{
			if (!PrepareReservation(System, Request?.RealmId, Request?.SettlementId,
				Request == null ? -1L : Request.ReservedTick, out Fault, out Failure)) return false;
			if (!TryCountProtectedFoundationBodies(System, "body", out int live,
				out Fault, out Failure)) return false;
			return KingdomExperienceRules.TryReserveBodies(System.Experience,
				System.Experience.Revision, Request, live, out Fault, out Failure);
		}

		public static bool TryReservePresentation(KingdomSystem System,
			KingdomExperienceAudienceReceipt Audience, KingdomExperienceBodyReservation Bodies,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			if (!PrepareReservation(System, Audience?.RealmId, Audience?.SettlementId,
				Audience == null ? -1L : Audience.ReservedTick, out Fault, out Failure)) return false;
			if (!TryCountProtectedFoundationBodies(System, "presentation", out int live,
				out Fault, out Failure)) return false;
			return KingdomExperienceRules.TryReservePresentation(System.Experience,
				System.Experience.Revision, Audience, Bodies, live, out Fault, out Failure);
		}

		public static bool TryRecoverRetirementBodies(KingdomSystem System,
			KingdomExperienceBodyReservation Request, long ObservedTick,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			if (!PrepareRecovery(System, Request?.RealmId, ObservedTick,
				out Fault, out Failure)) return false;
			if (!TryCountProtectedFoundationBodies(System, "body retirement", out int live,
				out Fault, out Failure)) return false;
			return KingdomExperienceRules.TryRecoverRetirementBodies(System.Experience,
				System.Experience.Revision, Request, live, out Fault, out Failure);
		}

		public static bool TryRecoverRetirementPresentation(KingdomSystem System,
			KingdomExperienceAudienceReceipt Audience, KingdomExperienceBodyReservation Bodies,
			long ObservedTick, out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			if (!PrepareRecovery(System, Audience?.RealmId, ObservedTick,
				out Fault, out Failure)) return false;
			if (!TryCountProtectedFoundationBodies(System, "presentation retirement", out int live,
				out Fault, out Failure)) return false;
			return KingdomExperienceRules.TryRecoverRetirementPresentation(System.Experience,
				System.Experience.Revision, Audience, Bodies, live, out Fault, out Failure);
		}

		public static bool TryRecoverDurableBodies(KingdomSystem System,
			KingdomExperienceBodyReservation Request, long ObservedTick,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			if (!PrepareRecovery(System, Request?.RealmId, ObservedTick,
				out Fault, out Failure)) return false;
			if (!TryCountProtectedFoundationBodies(System, "durable body recovery", out int live,
				out Fault, out Failure)) return false;
			return KingdomExperienceRules.TryRecoverDurableBodies(System.Experience,
				System.Experience.Revision, Request, live, out Fault, out Failure);
		}

		public static bool TryRecoverDurablePresentation(KingdomSystem System,
			KingdomExperienceAudienceReceipt Audience, KingdomExperienceBodyReservation Bodies,
			long ObservedTick, out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			if (!PrepareRecovery(System, Audience?.RealmId, ObservedTick,
				out Fault, out Failure)) return false;
			if (!TryCountProtectedFoundationBodies(System, "durable presentation recovery",
				out int live, out Fault, out Failure)) return false;
			return KingdomExperienceRules.TryRecoverDurablePresentation(System.Experience,
				System.Experience.Revision, Audience, Bodies, live, out Fault, out Failure);
		}

		private static bool PrepareRecovery(KingdomSystem System, string RealmId, long Tick,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.InvalidLedger; Failure = null;
			if (System == null || System.Experience == null || Tick < 0L)
			{
				Failure = "experience recovery context is invalid"; return false;
			}
			if (!string.Equals(RealmId, System.RealmId, StringComparison.Ordinal))
			{
				Fault = KingdomExperienceCapacityFault.WrongRealm;
				Failure = "experience recovery belongs to another realm"; return false;
			}
			if (!KingdomExperienceRules.TryValidate(System.Experience, out Failure)) return false;
			Fault = KingdomExperienceCapacityFault.None; return true;
		}

		private static bool PrepareReservation(KingdomSystem System, string RealmId,
			string SettlementId, long Tick, out KingdomExperienceCapacityFault Fault,
			out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.InvalidRequest; Failure = null;
			if (System == null || Tick < 0L)
			{
				Failure = "experience reservation context is invalid"; return false;
			}
			// Master steady-off ends before experience state, topology, or options are scanned.
			if (!KingdomMaster.NewWorkAllowed(System))
			{
				Fault = KingdomExperienceCapacityFault.OptionDisabled;
				Failure = "experience master option is disabled"; return false;
			}
			if (!string.Equals(RealmId, System.RealmId, StringComparison.Ordinal))
			{
				Fault = KingdomExperienceCapacityFault.WrongRealm;
				Failure = "experience reservation belongs to another realm"; return false;
			}
			if (!System.TryFindSettlement(SettlementId, out bool _, out KingdomSettlement _))
			{
				Fault = KingdomExperienceCapacityFault.WrongRealm;
				Failure = "experience reservation names no owned settlement"; return false;
			}
			if (!TryObserveConfiguredOptions(System, Tick, out Failure))
			{
				Fault = KingdomExperienceCapacityFault.InvalidLedger; return false;
			}
			Fault = KingdomExperienceCapacityFault.None; return true;
		}

		public static bool TryReleaseAudience(KingdomSystem System, string ReservationId,
			string SourceId, out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.InvalidLedger;
			if (System == null || System.Experience == null)
			{
				Failure = "experience system is absent"; return false;
			}
			return KingdomExperienceRules.TryReleaseAudience(System.Experience,
				System.Experience.Revision, ReservationId, SourceId, out Fault, out Failure);
		}

		public static bool TryReleaseBodies(KingdomSystem System, string ReservationId,
			string SourceId, out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.InvalidLedger;
			if (System == null || System.Experience == null)
			{
				Failure = "experience system is absent"; return false;
			}
			return KingdomExperienceRules.TryReleaseBodies(System.Experience,
				System.Experience.Revision, ReservationId, SourceId, out Fault, out Failure);
		}

		public static bool TryReleasePresentation(KingdomSystem System,
			string AudienceReservationId, string BodyReservationId, string SourceId,
			out KingdomExperienceCapacityFault Fault, out string Failure)
		{
			Fault = KingdomExperienceCapacityFault.InvalidLedger;
			if (System == null || System.Experience == null)
			{
				Failure = "experience system is absent"; return false;
			}
			return KingdomExperienceRules.TryReleasePresentation(System.Experience,
				System.Experience.Revision, AudienceReservationId, BodyReservationId, SourceId,
				out Fault, out Failure);
		}

		public static bool TryRecord(KingdomSystem System, KingdomExperienceExperiment Experiment,
			KingdomExperienceTrialArm Arm, KingdomExperienceObservationKind Observation, int Measure)
		{
			if (!KingdomMaster.NewWorkAllowed(System)) return false;
			// Check the default-off option before allocating the session ring.
			if (Options.GetOption(TelemetryOptionId, "No") == "No")
			{
				Session = null; return false;
			}
			if (Session == null) Session = new KingdomExperienceTelemetryBuffer();
			return Session.TryRecord(Experiment, Arm,
				KingdomExperienceTelemetryRules.FixtureFor(Experiment), Observation, Measure);
		}

		public static bool TryExport(out string PathWritten, out string Failure)
		{
			PathWritten = null; Failure = null;
			if (Options.GetOption(TelemetryOptionId, "No") == "No")
			{
				Failure = "local experience telemetry is disabled"; return false;
			}
			if (!KingdomExperienceTelemetryExport.TryCompose(Session, out string text))
			{
				Failure = "no bounded experience session is available"; return false;
			}
			try
			{
				string folder = DataManager.SavePath(ExportFolder);
				Directory.CreateDirectory(folder);
				string target = Path.Combine(folder, ExportFile);
				File.WriteAllText(target, text, new UTF8Encoding(false, true));
				PathWritten = target; return true;
			}
			catch (Exception e)
			{
				Failure = "experience export failed: " + e.GetType().Name; return false;
			}
		}
	}
}
