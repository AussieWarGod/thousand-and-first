using System;
using System.Globalization;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static class KingdomResidentDepartureRules
	{
		internal const int MaximumLineChars = 4096;

		internal static KingdomResidentDepartureOperation Empty()
		{
			return new KingdomResidentDepartureOperation();
		}

		internal static bool IsEmpty(KingdomResidentDepartureOperation Operation)
		{
			return Operation == null || Operation.Version == 0 && Operation.Phase == 0
				&& Operation.Revision == 0L && Operation.ResidentId == 0
				&& Operation.PreparedTick == 0L && Operation.DeparturesBefore == 0
				&& !Operation.Chronicled && Operation.AuthorizationKind == 0
				&& Operation.PriorCook == null && Operation.PriorOffice == null
				&& Operation.PriorPolity == null
				&& string.IsNullOrEmpty(Operation.OperationId)
				&& string.IsNullOrEmpty(Operation.RealmId)
				&& string.IsNullOrEmpty(Operation.SettlementId)
				&& string.IsNullOrEmpty(Operation.BodyObjectId)
				&& string.IsNullOrEmpty(Operation.ZoneId)
				&& string.IsNullOrEmpty(Operation.ResidentName)
				&& string.IsNullOrEmpty(Operation.Origin)
				&& string.IsNullOrEmpty(Operation.ChronicleLine)
				&& string.IsNullOrEmpty(Operation.LedgerLine)
				&& string.IsNullOrEmpty(Operation.Cause)
				&& string.IsNullOrEmpty(Operation.PolityConclusionRef)
				&& string.IsNullOrEmpty(Operation.AuthorizationEventId)
				&& string.IsNullOrEmpty(Operation.AuthorizationOwnerObjectId)
				&& string.IsNullOrEmpty(Operation.AuthorizationCauseDigest);
		}

		internal static KingdomResidentDepartureOperation NormalizeOldDefault(
			KingdomResidentDepartureOperation Operation)
		{
			return IsEmpty(Operation) ? Empty() : Operation;
		}

		internal static string Id(string RealmId, string SettlementId, int ResidentId,
			string BodyObjectId, long Tick)
		{
			return KingdomPolityRules.ActivationId("taf:resident-departure:v1:",
				"resident-departure-v1", RealmId, SettlementId,
				ResidentId.ToString(CultureInfo.InvariantCulture), BodyObjectId,
				Tick.ToString(CultureInfo.InvariantCulture));
		}

		internal static bool Valid(KingdomResidentDepartureOperation O)
		{
			if (O == null || O.Version != KingdomResidentDepartureOperation.CurrentVersion
				|| O.Phase < (int)KingdomResidentDeparturePhase.Prepared
				|| O.Phase > (int)KingdomResidentDeparturePhase.EffectsPublished
				|| O.Revision < 1L || !KingdomIdentityRules.IsRealmId(O.RealmId)
				|| !KingdomIdentityRules.IsSettlementId(O.SettlementId)
				|| O.ResidentId <= 0 || !Text(O.BodyObjectId, true, 512)
				|| !Text(O.ZoneId, true, 512) || !Text(O.ResidentName, true, 512)
				|| !Text(O.Origin, false, 512) || O.PreparedTick < 0L
				|| O.DeparturesBefore < 0 || !Text(O.Cause, false, 1024)
				|| !Text(O.ChronicleLine, false, MaximumLineChars)
				|| !Text(O.LedgerLine, false, MaximumLineChars)
				|| O.OperationId != Id(O.RealmId, O.SettlementId, O.ResidentId,
					O.BodyObjectId, O.PreparedTick)) return false;
			if (O.Chronicled && (O.ChronicleLine.Length == 0 || O.LedgerLine.Length == 0)
				|| !O.Chronicled && (O.ChronicleLine.Length != 0
					|| O.LedgerLine.Length != 0)) return false;
			if (!ValidCook(O) || !ValidOffice(O) || !ValidPolity(O)) return false;
			bool noAuthorization = O.AuthorizationKind == 0
				&& string.IsNullOrEmpty(O.AuthorizationEventId)
				&& string.IsNullOrEmpty(O.AuthorizationOwnerObjectId)
				&& string.IsNullOrEmpty(O.AuthorizationCauseDigest);
			bool labAuthorization = O.AuthorizationKind
				== (int)KingdomResidentDestructionAuthorizationKind.LabRefusalDeparture
				&& Text(O.AuthorizationEventId, true, 512)
				&& Text(O.AuthorizationOwnerObjectId, true, 512)
				&& Text(O.AuthorizationCauseDigest, true, 512);
			return noAuthorization || labAuthorization;
		}

		internal static bool Advance(KingdomResidentDepartureOperation O,
			KingdomResidentDeparturePhase Expected, KingdomResidentDeparturePhase Next)
		{
			if (!Valid(O) || O.Phase != (int)Expected
				|| (int)Next != (int)Expected + 1
				|| O.Revision == long.MaxValue) return false;
			O.Phase = (int)Next; O.Revision++; return Valid(O);
		}

		private static bool ValidCook(KingdomResidentDepartureOperation O)
		{
			return O.PriorCook == null || KingdomNamedCookRules.Validate(O.PriorCook,
				out string _) && O.PriorCook.Phase == KingdomNamedCookPhase.Applied
				&& O.PriorCook.ResidentId == O.ResidentId
				&& O.PriorCook.BodyObjectId == O.BodyObjectId
				&& O.PriorCook.SettlementId == O.SettlementId;
		}

		private static bool ValidOffice(KingdomResidentDepartureOperation O)
		{
			return O.PriorOffice == null || KingdomExperienceRules.ValidOffice(O.PriorOffice)
				&& (O.PriorOffice.Phase == KingdomCivicOfficePhase.Held
					|| O.PriorOffice.Phase == KingdomCivicOfficePhase.AppointmentPrepared)
				&& O.PriorOffice.HolderResidentId == O.ResidentId
				&& O.PriorOffice.HolderObjectId == O.BodyObjectId
				&& O.PriorOffice.SettlementId == O.SettlementId;
		}

		private static bool ValidPolity(KingdomResidentDepartureOperation O)
		{
			return O.PriorPolity == null && string.IsNullOrEmpty(O.PolityConclusionRef)
				|| O.PriorPolity != null
				&& O.PriorPolity.Phase == KingdomPolityFigurePhase.Active
				&& O.PriorPolity.Origin == KingdomPolityFigureOrigin.PromotedByDeed
				&& O.PriorPolity.ResidentId == O.ResidentId
				&& O.PriorPolity.ResidentSettlementId == O.SettlementId
				&& O.PriorPolity.DisplayName == O.ResidentName
				&& !string.IsNullOrEmpty(O.PolityConclusionRef);
		}

		private static bool Text(string Value, bool Required, int Maximum)
		{
			return Value != null && Value.Length <= Maximum && (!Required || Value.Length > 0);
		}
	}
}
