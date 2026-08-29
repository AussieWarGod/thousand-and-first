using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomPetitionLifecycle
	{
		private static void Project(KingdomSystem system, KingdomLifecycleOperation op)
		{
			if (system == null || !KingdomPetitionRules.FrozenSnapshotValid(op)) return;
			system.PetitionState = KingdomPetitionRules.LifecycleOf(op);
			system.PetitionKind = (KingdomRules.PetitionKind)op.Kind;
			system.PetitionEventId = op.ObjectMarker;
			system.PetitionOriginSettlementId = op.Origin;
			system.PetitionCauseSnapshot = op.Detail;
			system.PetitionPetitioner = op.ObjectName;
			system.PetitionFaction = op.Faction;
			system.PetitionTarget = op.Target;
			if (KingdomPetitionRules.TryIssuedTick(op, out long issued))
			{
				system.PetitionIssuedTick = issued;
				system.LastPetitionMonthOrdinal = KingdomPetitionRules.CanonicalMonthOrdinal(issued);
				system.LastPetitionTick = issued;
			}
		}

		private static KingdomLifecycleBook Authority(KingdomSystem system)
		{
			if (system == null || !system.Founded || system.LifecycleBook == null
				|| system.City == null || string.IsNullOrEmpty(system.CurrentSettlementId)
				|| !string.Equals(system.LifecycleBook.SettlementId,
					system.CurrentSettlementId, StringComparison.Ordinal)
				|| !string.Equals(system.City.SettlementId,
					system.CurrentSettlementId, StringComparison.Ordinal)) return null;
			KingdomLifecycleRules.Normalize(system.LifecycleBook);
			if (system.LifecycleBook.Petition != null
				&& !KingdomPetitionRules.FrozenSnapshotValid(system.LifecycleBook.Petition))
			{
				if (system.LifecycleBook.Petition.Phase != KingdomLifecyclePhase.Quarantined)
					KingdomLifecycleRules.Quarantine(system.LifecycleBook.Petition,
						"malformed petition snapshot was retained without reinterpretation");
				system.LifecycleBook.Quarantined = true;
				if (string.IsNullOrEmpty(system.LifecycleBook.Fault))
					system.LifecycleBook.Fault =
						"malformed petition authority was quarantined without clearing evidence";
				return null;
			}
			return KingdomLifecycleRules.CanOwnAuthority(system.LifecycleBook)
				? system.LifecycleBook : null;
		}

		private static bool SeatGround(KingdomSystem system, Zone zone, KingdomSurvey survey)
		{
			return system != null && zone != null && survey != null
				&& ReferenceEquals(survey.Ground, zone) && system.ClaimedZones != null
				&& system.ClaimedZones.Contains(zone.ZoneID)
				&& string.Equals(system.LifecycleBook?.SettlementId,
					system.CurrentSettlementId, StringComparison.Ordinal);
		}

		private static bool HasShrine(KingdomSurvey survey)
		{
			return survey != null && survey.Shrines.Count > 0;
		}

		private static string DisplayFaction(string faction)
		{
			if (string.IsNullOrEmpty(faction)) return null;
			try
			{
				return ConsoleLib.Console.ColorUtility.StripFormatting(
					XRL.World.Faction.GetFormattedName(faction));
			}
			catch { return faction; }
		}

		private static bool QuarantineAfterRetirement(KingdomLifecycleBook book,
			KingdomLifecycleOperation evidence, string reason)
		{
			book.Quarantined = true;
			book.Fault = reason;
			return false;
		}

		private static KingdomLifecycleSinkState SinkState(KingdomLifecycleOutbox box,
			KingdomLifecycleSinkMask sink)
		{
			if (box == null) return KingdomLifecycleSinkState.None;
			switch (sink)
			{
			case KingdomLifecycleSinkMask.Chronicle: return box.ChronicleState;
			case KingdomLifecycleSinkMask.Ledger: return box.LedgerState;
			case KingdomLifecycleSinkMask.Message: return box.MessageState;
			case KingdomLifecycleSinkMask.Deed: return box.DeedState;
			default: return box.GuestbookState;
			}
		}

		private static bool Settled(KingdomLifecycleOutbox box)
		{
			return box != null
				&& KingdomLifecycleRules.SinkSettled(box.ChronicleState)
				&& KingdomLifecycleRules.SinkSettled(box.LedgerState)
				&& KingdomLifecycleRules.SinkSettled(box.MessageState)
				&& KingdomLifecycleRules.SinkSettled(box.DeedState)
				&& KingdomLifecycleRules.SinkSettled(box.GuestbookState);
		}	}
}
