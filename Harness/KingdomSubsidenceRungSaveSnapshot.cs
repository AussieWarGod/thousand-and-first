using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst.Harness
{
	/// <summary>One resident and the exact body that carried them, kept as a single typed identity
	/// so no witness can pair a resident of one row with the body of another.</summary>
	internal sealed class KingdomSubsidenceRungSaveBody
	{
		internal readonly int ResidentId;
		internal readonly string ObjectId, Key;

		internal KingdomSubsidenceRungSaveBody(int residentId, string objectId)
		{
			ResidentId = residentId; ObjectId = objectId; Key = Compose(residentId, objectId);
		}

		/// <summary>Varying token first. Every codec text refuses control characters, so the unit
		/// separator can never occur inside an object id and the pair key stays unambiguous.</summary>
		internal static string Compose(int residentId, string objectId)
		{
			return (objectId ?? "") + "\u001f" + residentId.ToString(CultureInfo.InvariantCulture);
		}
	}

	/// <summary>One planned work frozen at the native second-write release cut: its typed identity,
	/// its exact attachment count, and the ten wear-receipt fields read off the live part.</summary>
	internal sealed class KingdomSubsidenceRungSaveWork
	{
		internal readonly string ObjectId, Blueprint, PlotId, DesignStamp;
		internal readonly int X, Y, PartCopies, BeforeWear, AfterWear;
		internal readonly int IncidentPhase, IncidentCause, IncidentBeforeWear, IncidentAfterWear;
		internal readonly int Wear, LastCause, IncidentMessageState;
		internal readonly string IncidentId, LastCompletedIncidentId, IncidentLine;
		internal readonly bool Quarantined;

		internal KingdomSubsidenceRungSaveWork(string objectId, string blueprint, string plotId,
			string designStamp, int x, int y, int partCopies, int beforeWear, int afterWear,
			int incidentPhase, string incidentId, int incidentCause, int incidentBeforeWear,
			int incidentAfterWear, int wear, int lastCause, string lastCompletedIncidentId,
			string incidentLine, int incidentMessageState, bool quarantined)
		{
			ObjectId = objectId; Blueprint = blueprint; PlotId = plotId; DesignStamp = designStamp;
			X = x; Y = y; PartCopies = partCopies; BeforeWear = beforeWear; AfterWear = afterWear;
			IncidentPhase = incidentPhase; IncidentId = incidentId; IncidentCause = incidentCause;
			IncidentBeforeWear = incidentBeforeWear; IncidentAfterWear = incidentAfterWear;
			Wear = wear; LastCause = lastCause; LastCompletedIncidentId = lastCompletedIncidentId;
			IncidentLine = incidentLine; IncidentMessageState = incidentMessageState;
			Quarantined = quarantined;
		}
	}

	/// <summary>The full raw roof tuple of the one housed survivor, exactly as the city book carries
	/// it. Its carrier is one typed resident/body pair, never two loose fields.</summary>
	internal sealed class KingdomSubsidenceRungSaveRoof
	{
		internal readonly KingdomSubsidenceRungSaveBody Carrier;
		internal readonly int HomeWorkId, Standing;
		internal readonly string ZoneId;
		internal readonly bool RoofStanding;
		internal readonly long Reached, Warned;
		internal int ResidentId { get { return Carrier.ResidentId; } }
		internal string BodyObjectId { get { return Carrier.ObjectId; } }

		internal KingdomSubsidenceRungSaveRoof(int residentId, string bodyObjectId, int homeWorkId,
			int standing, string zoneId, bool roofStanding, long reached, long warned)
		{
			Carrier = new KingdomSubsidenceRungSaveBody(residentId, bodyObjectId);
			HomeWorkId = homeWorkId; Standing = standing; ZoneId = zoneId;
			RoofStanding = roofStanding; Reached = reached; Warned = warned;
		}
	}

	/// <summary>Frozen native test witness of a D5 rung release cut after its second exact receipt
	/// write. StepWire and RungWire are opaque bytes here; the witness proves the live protocol.</summary>
	internal sealed class KingdomSubsidenceRungSaveSnapshot
	{
		internal const int SurvivorCount = 35;
		internal const int AbsentCount = 15;
		internal const int WriteCut = 2;

		internal readonly string GameId, ZoneId, StepWire, RungWire, StepId, TellingDigest;
		internal readonly long Now, AnchorTick, DueTick, Sequence, LastSubsidenceTick;
		internal readonly int LedgerDepartures, Population, Stage, ChronicleCount, OutsiderCount;
		internal readonly KingdomSubsidenceRungSaveWork Work;
		internal readonly KingdomSubsidenceRungSaveRoof Roof;
		internal readonly IReadOnlyList<int> ResidentIds, AbsentResidentIds;
		internal readonly IReadOnlyList<string> ObjectIds, AbsentObjectIds;

		internal KingdomSubsidenceRungSaveSnapshot(string gameId, string zoneId, string stepWire,
			string rungWire, string stepId, string tellingDigest, long now, long anchorTick,
			long dueTick, long sequence, long lastSubsidenceTick, int ledgerDepartures,
			int population, int stage, int chronicleCount, int outsiderCount,
			KingdomSubsidenceRungSaveWork work, KingdomSubsidenceRungSaveRoof roof,
			int[] residentIds, string[] objectIds, int[] absentResidentIds, string[] absentObjectIds)
		{
			GameId = gameId; ZoneId = zoneId; StepWire = stepWire; RungWire = rungWire;
			StepId = stepId; TellingDigest = tellingDigest; Now = now; AnchorTick = anchorTick;
			DueTick = dueTick; Sequence = sequence; LastSubsidenceTick = lastSubsidenceTick;
			LedgerDepartures = ledgerDepartures; Population = population; Stage = stage;
			ChronicleCount = chronicleCount; OutsiderCount = outsiderCount; Work = work; Roof = roof;
			ResidentIds = Freeze(residentIds); ObjectIds = Freeze(objectIds);
			AbsentResidentIds = Freeze(absentResidentIds); AbsentObjectIds = Freeze(absentObjectIds);
		}

		private static IReadOnlyList<int> Freeze(int[] values)
		{
			return values == null ? null : Array.AsReadOnly((int[])values.Clone());
		}

		private static IReadOnlyList<string> Freeze(string[] values)
		{
			return values == null ? null : Array.AsReadOnly((string[])values.Clone());
		}
	}
}
