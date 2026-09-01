using System;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomResidentDepartureRuntime
	{
		internal static bool TryBegin(KingdomSystem System, GameObject Body, string Cause,
			bool Chronicled, string Note, KingdomResidentDestructionAuthorization Authorization,
			out KingdomResidentRow FormerRow, out string Failure)
		{
			FormerRow = default(KingdomResidentRow); Failure = null;
			if (System == null || !GameObject.Validate(Body)) return false;
			System.ResidentDeparture = KingdomResidentDepartureRules.NormalizeOldDefault(
				System.ResidentDeparture);
			if (!KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture))
			{
				TryRecoverPending(System, Body.CurrentZone, out string _);
				if (!KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture))
				{
					Failure = "another exact resident departure is still recovering"; return false;
				}
			}
			int residentId = KingdomResidents.IdOf(Body);
			if (!KingdomResidentTransitionAuthority.CanPrepareResidentBodyDestruction(
				System, Body, residentId, Authorization)
				|| Body.GetPart<r_KingdomResidentDeparture>() != null
				|| !TryCapture(System, Body, residentId, Cause, Chronicled, Note,
					Authorization, out KingdomResidentDepartureOperation operation,
					out FormerRow, out Failure)) return false;

			System.ResidentDeparture = operation;
			r_KingdomResidentDeparture marker = new r_KingdomResidentDeparture
			{
				OperationId = operation.OperationId, RealmId = operation.RealmId,
				ResidentId = residentId, BodyObjectId = operation.BodyObjectId
			};
			try { Body.AddPart(marker); }
			catch (Exception ex)
			{
				// AddPart may throw after attaching. Retain write-ahead authority whenever any
				// marker exists; recovery may remove an exact marker and refuses a foreign one.
				if (Body.GetPart<r_KingdomResidentDeparture>() == null)
					System.ResidentDeparture = KingdomResidentDepartureRules.Empty();
				Failure = "departure marker attachment threw " + ex.GetType().Name; return false;
			}
			if (Body.GetPart<r_KingdomResidentDeparture>() != marker)
			{
				if (Body.GetPart<r_KingdomResidentDeparture>() == null)
					System.ResidentDeparture = KingdomResidentDepartureRules.Empty();
				Failure = "departure marker did not attach exactly"; return false;
			}

			if (!KingdomResidentDeparturePreparation.TryPrepare(System, Body, Authorization,
				operation,
				out KingdomResidentDeparturePreparation prepared, out Failure)
				|| !SamePreparation(operation, prepared))
			{
				if (string.IsNullOrEmpty(Failure))
					Failure = "role preparation diverged from its write-ahead snapshots";
				TryRollbackPrepared(System, Body, operation, out string _); return false;
			}
			if (!KingdomResidentDepartureRules.Advance(operation,
				KingdomResidentDeparturePhase.Prepared,
				KingdomResidentDeparturePhase.RolesPrepared))
			{
				Failure = "departure role-prepared phase could not advance";
				TryRollbackPrepared(System, Body, operation, out string _); return false;
			}
			if (!KingdomCitizenship.CanRemove(System, Body, out Failure)
				|| !KingdomResidentTransitionAuthority.CanPrepareJournaledRoles(
					System, Body, operation, Authorization, RolesPrepared: true))
			{
				Failure = Failure ?? "resident authority changed before citizenship removal";
				TryRollbackPrepared(System, Body, operation, out string _); return false;
			}
			if (!KingdomCitizenship.TryRemove(System, Body,
				KingdomCitizenshipRemovalReason.Emigration, out Failure))
			{
				if (!ExactRemovedCitizenship(System, Body, operation))
					TryRollbackPrepared(System, Body, operation, out string _);
				return false;
			}
			if (!KingdomResidentDepartureRules.Advance(operation,
				KingdomResidentDeparturePhase.RolesPrepared,
				KingdomResidentDeparturePhase.CitizenshipRemoved))
			{
				Failure = "departure citizenship phase could not advance"; return false;
			}
			return TryContinue(System, Body, out FormerRow, out Failure);
		}

		private static bool TryCapture(KingdomSystem System, GameObject Body, int ResidentId,
			string Cause, bool Chronicled, string Note,
			KingdomResidentDestructionAuthorization Authorization,
			out KingdomResidentDepartureOperation Operation, out KingdomResidentRow Row,
			out string Failure)
		{
			Operation = null; Row = default(KingdomResidentRow); Failure = null;
			if (!KingdomResidents.TryLocate(System, Body, out KingdomCityBook book,
				out int foundId) || book == null || foundId != ResidentId
				|| !book.TryRead(out KingdomCityState state, out KingdomCityFault _)
				|| !state.TryResidentIndex(ResidentId, out int at)
				|| !state.TryResident(at, out Row)) return false;
			string settlement = System.SettlementIdForOwnedZone(Body.CurrentZone?.ZoneID);
			if (Row.ResidentId != ResidentId
				|| Row.Standing != KingdomResidentStanding.Resident
				|| Row.BoundZoneId != Body.CurrentZone?.ZoneID
				|| settlement != book.SettlementId
				|| Row.Name != Body.GetStringProperty("KingdomName")
				|| !TryCaptureRoles(System, Body, ResidentId, settlement,
					out KingdomNamedCookReceipt cook, out KingdomCivicOfficeReceipt office,
					out KingdomPolityNamedFigureRecord polity, out string polityConclusion,
					out Failure)) return false;
			long tick = The.Game == null ? 0L : Math.Max(0L, The.Game.TimeTicks);
			string cause = string.IsNullOrEmpty(Cause)
				? "for wetter country, the cisterns having run dry" : Cause;
			string note = string.IsNullOrEmpty(Note)
				? (string.IsNullOrEmpty(Cause) ? "for wetter country" : Cause) : Note;
			string chronicle = "", ledger = "";
			if (Chronicled)
			{
				string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
				string named = KingdomPresentation.Rich(XRL.Language.Grammar.A(Row.Name));
				string namedStart = KingdomPresentation.Rich(
					XRL.Language.Grammar.A(Row.Name, Capitalize: true));
				chronicle = named + " left " + realm + " " + cause;
				ledger = KingdomVoices.Say(System, VoiceOccasion.CitizenLost,
					"{{R|" + namedStart + " left " + realm + " " + note + ".}}");
			}
			Operation = new KingdomResidentDepartureOperation
			{
				Version = KingdomResidentDepartureOperation.CurrentVersion,
				Phase = (int)KingdomResidentDeparturePhase.Prepared, Revision = 1L,
				RealmId = System.CurrentRealmId, SettlementId = settlement,
				ResidentId = ResidentId, BodyObjectId = Body.IDIfAssigned,
				ZoneId = Body.CurrentZone.ZoneID, ResidentName = Row.Name,
				Origin = Row.Origin ?? "", PreparedTick = tick,
				DeparturesBefore = System.Ledger?.Departures ?? -1,
				Chronicled = Chronicled, ChronicleLine = chronicle,
				LedgerLine = ledger, Cause = cause, PriorCook = cook,
				PriorOffice = office, PriorPolity = polity,
				PolityConclusionRef = polityConclusion,
				AuthorizationKind = (int)Authorization.Kind,
				AuthorizationEventId = Authorization.EventId ?? "",
				AuthorizationOwnerObjectId = Authorization.OwnerObjectId ?? "",
				AuthorizationCauseDigest = Authorization.CauseDigest ?? ""
			};
			Operation.OperationId = KingdomResidentDepartureRules.Id(Operation.RealmId,
				settlement, ResidentId, Operation.BodyObjectId, tick);
			if (KingdomResidentDepartureRules.Valid(Operation)) return true;
			Failure = "departure write-ahead record is invalid"; Operation = null; return false;
		}
	}
}
