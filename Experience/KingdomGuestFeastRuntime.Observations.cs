#if !TAF_TESTS
using System;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomGuestFeastRuntime
	{
		internal static bool TryJoinedAwaitingPractice(KingdomSystem system,
			string settlementId, out KingdomGuestFeastReceipt receipt, out string failure)
		{
			receipt = null; failure = null;
			if (!TryRead(system, out KingdomGuestFeastBook book, out failure)
				|| !book.IdentityBound || !KingdomGuestFeastRules.TryFind(book,
					settlementId, out receipt)) return false;
			if (receipt == null) return true;
			if (receipt.Phase == KingdomGuestFeastPhase.AwaitingPractice
				&& receipt.GuestResult == KingdomGrowthArrivalDisposition.Joined
				&& KingdomGuestFeastRules.TerminalDigest(receipt) != null) return true;
			receipt = null; return true;
		}

		internal static bool TryObservePractice(KingdomSystem system, Zone zone,
			KingdomFirstFeastReceipt practice, out string failure)
		{
			failure = null;
			try { return TryObservePracticeCore(system, zone, practice, out failure); }
			catch (Exception error)
			{
				return Fail("guest-feast practice adapter failed closed ("
					+ error.GetType().Name + ")", out failure);
			}
		}

		private static bool TryObservePracticeCore(KingdomSystem system, Zone zone,
			KingdomFirstFeastReceipt practice, out string failure)
		{
			failure = null;
			if (practice == null || !KingdomFirstFeastRules.Valid(practice))
				return Fail("First Feast practice observation is invalid", out failure);
			if (!TryRead(system, out KingdomGuestFeastBook book, out failure)) return false;
			if (!book.IdentityBound) return true;
			if (!KingdomGuestFeastRules.TryFind(book, practice.SettlementId,
				out KingdomGuestFeastReceipt row)) return false;
			if (row == null || KingdomGuestFeastRules.IsTerminal(row.Phase)) return true;
			if (!TryWritePractice(system, practice, out bool changed, out failure)) return false;
			if (changed) KingdomExperienceRuntime.TryRecord(system,
				KingdomExperienceExperiment.GuestsFeast,
				KingdomExperienceTrialArm.Integrated,
				KingdomExperienceObservationKind.Closed, 1);
			if (!KingdomFirstFeastRules.IsAffirmative(practice)) return true;
			KingdomSurvey survey = zone == null ? null : KingdomSurvey.ActiveFor(zone);
			return TryRecoverLocusIfExact(system, zone, survey, practice.SettlementId,
				practice.DecidedTick, out failure);
		}

		private static bool TryReconcileOwners(KingdomSystem system, Zone zone,
			KingdomSurvey survey, string settlementId, out string failure)
		{
			failure = null;
			if (!TryReconcileGrowthTerminalBestEffort(system, settlementId, out failure))
				return false;
			if (!KingdomExperienceRules.TryGetFirstFeast(system.Experience, settlementId,
				out KingdomFirstFeastReceipt practice, out failure)) return false;
			if (practice == null || practice.Phase == KingdomFirstFeastPhase.Offered) return true;
			if (!TryWritePractice(system, practice, out bool _, out failure)) return false;
			return !KingdomFirstFeastRules.IsAffirmative(practice)
				|| TryRecoverLocusIfExact(system, zone, survey, settlementId,
					practice.DecidedTick, out failure);
		}

		private static bool TryRecoverLocusIfExact(KingdomSystem system, Zone zone,
			KingdomSurvey survey, string settlementId, long practiceTick, out string failure)
		{
			failure = null;
			if (!TryRead(system, out KingdomGuestFeastBook book, out failure)
				|| !KingdomGuestFeastRules.TryFind(book, settlementId,
					out KingdomGuestFeastReceipt row)) return false;
			if (row == null || row.Phase != KingdomGuestFeastPhase.AwaitingLocus
				&& row.Phase != KingdomGuestFeastPhase.Cycling
				&& row.Phase != KingdomGuestFeastPhase.Exhausted) return true;
			long now = XRL.The.Game?.TimeTicks ?? -1L;
			if (now <= practiceTick) return true;
			if (row.LocusProjectionId != null)
			{
				KingdomGuestFeastLocusReceipt standing = Locus(row);
				if (ExactProjectedLocus(zone, survey, standing)) return true;
				// Only current exact ground can prove destruction. Absence elsewhere is unknown.
				if (zone == null || survey == null || !ReferenceEquals(zone, XRL.The.Player?.CurrentZone)
					|| standing.ZoneId != zone.ZoneID) return true;
				KingdomGuestFeastBook lost = KingdomGuestFeastRules.Clone(book);
				if (!KingdomGuestFeastRules.TryLoseLocus(lost, lost.Revision, settlementId,
					standing, out failure) || !TryPublish(system, lost, out failure)
					|| !TryRead(system, out book, out failure)) return false;
			}
			if (!TryCaptureReadyLocus(system, zone, survey, settlementId, now,
				out KingdomGuestFeastLocusReceipt locus)) return true;
			KingdomGuestFeastBook next = KingdomGuestFeastRules.Clone(book);
			if (!KingdomGuestFeastRules.TryObserveLocus(next, next.Revision, settlementId,
				locus, out failure)) return false;
			return next.Revision == book.Revision || TryPublish(system, next, out failure);
		}

		private static bool TryWritePractice(KingdomSystem system,
			KingdomFirstFeastReceipt practice, out bool changed, out string failure)
		{
			changed = false; failure = null;
			if (!TryRead(system, out KingdomGuestFeastBook book, out failure)
				|| !KingdomGuestFeastRules.TryFind(book, practice.SettlementId,
					out KingdomGuestFeastReceipt row)) return false;
			if (row == null || KingdomGuestFeastRules.IsTerminal(row.Phase)) return true;
			KingdomGuestFeastBook next = KingdomGuestFeastRules.Clone(book);
			if (!KingdomGuestFeastRules.TryObservePractice(next, next.Revision,
				practice.SettlementId, practice, out _, out failure)) return false;
			changed = next.Revision != book.Revision;
			return !changed || TryPublish(system, next, out failure);
		}

		private static bool TryCaptureReadyLocus(KingdomSystem system, Zone zone,
			KingdomSurvey survey, string settlementId, long now,
			out KingdomGuestFeastLocusReceipt locus)
		{
			locus = null;
			if (system == null || zone == null || survey == null
				|| !ReferenceEquals(zone, XRL.The.Player?.CurrentZone)
				|| !ReferenceEquals(survey.Ground, zone)
				|| system.SettlementIdForOwnedZone(zone.ZoneID) != settlementId
				|| !TryCityBook(system, zone, settlementId, out KingdomCityBook city)
				|| !city.TryRead(out KingdomCityState state, out KingdomCityFault _)) return false;
			int workId = KingdomLocusRules.SelectLocusWork(city.WorkIds,
				city.WorkDesignKeys, KingdomLocus.BenchBlueprint);
			GameObject bench = null; int benches = 0;
			for (int i = 0; i < survey.Objects.Count; i++)
			{
				GameObject item = survey.Objects[i];
				if (GameObject.Validate(item) && ReferenceEquals(item.CurrentZone, zone)
					&& item.CurrentCell != null && item.Blueprint == KingdomLocus.BenchBlueprint
					&& item.GetIntProperty("KingdomBuilt") == 1
					&& KingdomCityRules.StableId(item.IDIfAssigned) == workId)
				{ bench = item; benches++; }
			}
			if (workId == 0 || benches != 1 || bench.GetIntProperty("KingdomStaffNeeded") != 1
				|| bench.GetIntProperty("KingdomStaffed") != 1) return false;
			int keepers = 0;
			for (int i = 0; i < survey.Settlers.Count; i++)
			{
				GameObject body = survey.Settlers[i];
				if (GameObject.Validate(body) && body.IsAlive && body.Brain != null
					&& ReferenceEquals(body.CurrentZone, zone) && body.CurrentCell != null
					&& body.GetIntProperty("KingdomKeeper") == 1
					&& KingdomResidents.IdOf(body) > 0
					&& KingdomStations.PostOf(body) == workId
					&& !KingdomPhysicalHappenings.IsStaged(body)
					&& KingdomCitizenship.BelongsTo(system, body)) keepers++;
			}
			return keepers == 1 && KingdomGuestFeastRules.TryBuildLocusReceipt(system.RealmId,
				settlementId, workId, bench.IDIfAssigned, zone.ZoneID, bench.Blueprint, now,
				out locus);
		}

		private static bool TryCityBook(KingdomSystem system, Zone zone, string settlementId,
			out KingdomCityBook city)
		{
			city = null;
			if (system.CurrentSettlementId == settlementId) city = system.City;
			else city = system.FindNonSeatSettlementByZone(zone.ZoneID)?.City;
			return city != null && city.SettlementId == settlementId;
		}

		private static KingdomGuestFeastLocusReceipt Locus(KingdomGuestFeastReceipt row) =>
			new KingdomGuestFeastLocusReceipt { ProjectionId = row.LocusProjectionId,
				RealmId = row.LocusRealmId, SettlementId = row.LocusSettlementId,
				WorkId = row.LocusWorkId, ObjectId = row.LocusObjectId,
				ZoneId = row.LocusZoneId, Blueprint = row.LocusBlueprint,
				ObservedTick = row.LocusObservedTick };

		private static bool ExactProjectedLocus(Zone zone, KingdomSurvey survey,
			KingdomGuestFeastLocusReceipt receipt)
		{
			if (!KingdomGuestFeastRules.ValidLocus(receipt) || zone == null || survey == null
				|| !ReferenceEquals(survey.Ground, zone) || zone.ZoneID != receipt.ZoneId) return false;
			int found = 0;
			for (int i = 0; i < survey.Objects.Count; i++)
			{
				GameObject item = survey.Objects[i];
				if (GameObject.Validate(item) && item.IDIfAssigned == receipt.ObjectId
					&& item.Blueprint == receipt.Blueprint && item.CurrentCell != null
					&& ReferenceEquals(item.CurrentZone, zone)
					&& KingdomCityRules.StableId(item.IDIfAssigned) == receipt.WorkId) found++;
			}
			return found == 1;
		}
	}
}
#endif
