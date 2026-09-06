using System;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Fail-closed single-step accounting. The runtime must prove the exact departure
	/// and persist each returned book before moving either physical carriers or the clock.</summary>
	internal static partial class KingdomSubsidenceStepRules
	{
		internal const long StepTicks = (long)KingdomSubsidenceRules.StepDays * KingdomRules.TicksPerDay;
		internal const string NoRungs = "sr1:none";
		internal const string UnplannedRungs = "sr1:pending";
		internal const string NoOption = "so1:none";
		private const string StepPrefix = "taf:subsidence-step:v1:";
		private const string DeparturePrefix = "taf:resident-departure:v1:";
		private static readonly string CreditLength = (DeparturePrefix.Length + 64)
			.ToString(CultureInfo.InvariantCulture) + ":";

		internal static bool Valid(KingdomSubsidenceStepBook book)
		{
			return ValidShape(book) && KingdomSubsidenceReportArchive.Valid(book)
				&& KingdomSubsidenceAnnouncementRules.MatchesShape(book)
				&& ValidRungReport(book) && (book.BatchModel == KingdomSubsidenceBatchRules.None
				|| KingdomSubsidenceBatchCodec.TryDecode(book.BatchModel, out KingdomSubsidenceBatch batch)
					&& KingdomSubsidenceBatchRules.MatchesShape(batch, book)) && (book.OptionModel == NoOption
				|| book.Admission == KingdomSubsidenceAdmission.Admitted
					&& KingdomSubsidenceOptionIntentRules.TryDecode(book.OptionModel,
						out KingdomSubsidenceOptionIntent intent)
					&& KingdomSubsidenceOptionIntentRules.MatchesShape(intent, book));
		}

		private static bool ValidShape(KingdomSubsidenceStepBook book)
		{
			if (book == null || book.Sequence < 0 || book.LastRetiredTick < 0
				|| book.Sequence == 0 && book.LastRetiredTick != 0) return false;
			if (book.Admission == KingdomSubsidenceAdmission.Fresh
				|| book.Admission == KingdomSubsidenceAdmission.Legacy)
				return book.RealmId == "" && book.SettlementId == ""
					&& book.Sequence == 0 && book.Active == null;
			if (book.Admission != KingdomSubsidenceAdmission.Admitted
				|| !KingdomIdentityRules.IsRealmId(book.RealmId)
				|| !KingdomIdentityRules.IsSettlementId(book.SettlementId)) return false;
			KingdomSubsidenceStepOperation op = book.Active;
			if (op == null) return book.Sequence == 0 || book.LastRetiredTick > 0;
			if (book.Sequence == 0 || op.AnchorTick < 0 || op.AnchorTick > long.MaxValue - StepTicks
				|| op.AnchorTick < book.LastRetiredTick || (book.Sequence == 1) != (book.LastRetiredTick == 0)
				|| op.DueTick != op.AnchorTick + StepTicks || !Stage(op.FromStage)
				|| op.LastActivityTick < op.DueTick
				|| !Stage(op.ReachedStage) || op.ReachedStage > op.FromStage
				|| op.ReachedStage < op.FromStage - 1
				|| op.Quota < 1 || op.Quota > KingdomSubsidenceRules.SettlersPerStep(op.FromStage)
				|| op.StorageCapacity < 0 || op.BindingSupport != "water" && op.BindingSupport != "roof"
				|| op.Completed < 0 || op.Completed > op.Quota
				|| op.Completed == 0 && op.ReachedStage != op.FromStage
				|| op.PendingDepartureId == null
				|| op.PendingDepartureId != "" && !Hashed(op.PendingDepartureId, DeparturePrefix)
				|| op.PendingCredited && (op.PendingDepartureId == "" || op.Completed == 0)
				|| !op.PendingCredited && op.PendingDepartureId != "" && op.Completed == op.Quota
				|| op.CancelRequested && (op.CancelTick < op.DueTick || op.CancelToken < 0)
				|| op.CancelRequested && op.CancelTick < op.LastActivityTick
				|| !op.CancelRequested && (op.CancelTick != 0 || op.CancelToken != 0)
				|| !Text(op.Fault, 512) || !Credits(op) || !PendingIdentity(book, op)) return false;
			bool settling = op.Completed == op.Quota || op.CancelRequested
				&& (op.PendingDepartureId == "" || op.PendingCredited);
			if (op.Phase == KingdomSubsidenceStepPhase.Quarantined)
			{
				if (op.Fault.Length == 0) return false;
			}
			else if (op.Fault.Length != 0 || op.Phase != (settling
				? KingdomSubsidenceStepPhase.Settling : KingdomSubsidenceStepPhase.Departing)) return false;
			if (op.ReachedStage == op.FromStage)
			{
				if (op.RungModel != NoRungs) return false;
			}
			else if (op.RungModel != UnplannedRungs
				&& (!KingdomSubsidenceRungCodec.TryDecode(op.RungModel, out KingdomSubsidenceRungPlan plan)
					|| !KingdomSubsidenceRungRules.Matches(plan, book))) return false;
			try { return op.Id == Id(book, book.Sequence, op.AnchorTick, op.FromStage, op.Quota,
				op.StorageCapacity, op.BindingSupport); }
			catch { return false; }
		}

		internal static bool TryAdmit(KingdomSubsidenceStepBook prior, string realm,
			string settlement, out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!Valid(prior) || !KingdomIdentityRules.IsRealmId(realm)
				|| !KingdomIdentityRules.IsSettlementId(settlement)) return false;
			if (prior.Admission == KingdomSubsidenceAdmission.Admitted)
			{
				if (prior.RealmId != realm || prior.SettlementId != settlement) return false;
				next = prior; return true;
			}
			next = new KingdomSubsidenceStepBook(KingdomSubsidenceAdmission.Admitted,
				realm, settlement, 0, null);
			return true;
		}

		internal static bool TryBegin(KingdomSubsidenceStepBook prior, long anchor, long now,
			GrowthStage stage, int quota, out KingdomSubsidenceStepBook next,
			int storageCapacity, string bindingSupport)
		{
			next = null;
			if (!Valid(prior) || prior.Admission != KingdomSubsidenceAdmission.Admitted
				|| prior.OptionModel != NoOption
				|| prior.Active != null || prior.Sequence == long.MaxValue || anchor < 0
				|| anchor < prior.LastRetiredTick
				|| anchor > long.MaxValue - StepTicks || now < anchor + StepTicks
				|| storageCapacity < 0 || bindingSupport != "water" && bindingSupport != "roof"
				|| !Stage(stage) || quota < 1 || quota > KingdomSubsidenceRules.SettlersPerStep(stage)) return false;
			long sequence = prior.Sequence + 1;
			try
			{
				KingdomSubsidenceStepOperation op = new KingdomSubsidenceStepOperation(
					Id(prior, sequence, anchor, stage, quota, storageCapacity, bindingSupport),
					anchor, anchor + StepTicks, stage, stage, quota, 0, KingdomSubsidenceStepPhase.Departing,
					"", false, false, 0, 0, NoRungs, "", storageCapacity, bindingSupport, now);
				next = prior.With(op, sequence);
				if (Valid(next)) return true;
				next = null; return false;
			}
			catch { return false; }
		}

		private static string Id(KingdomSubsidenceStepBook book, long sequence,
			long anchor, GrowthStage stage, int quota, int storageCapacity, string bindingSupport)
		{
			return KingdomPolityRules.ActivationId(StepPrefix, "subsidence-step-v1",
				book.RealmId, book.SettlementId, sequence.ToString(CultureInfo.InvariantCulture),
				anchor.ToString(CultureInfo.InvariantCulture), ((int)stage).ToString(CultureInfo.InvariantCulture),
				quota.ToString(CultureInfo.InvariantCulture), storageCapacity.ToString(CultureInfo.InvariantCulture),
				bindingSupport);
		}

		private static bool PendingIdentity(KingdomSubsidenceStepBook book, KingdomSubsidenceStepOperation op)
		{
			KingdomSubsidenceDepartureIdentity held = op.PendingIdentity;
			if (op.PendingDepartureId == "") return held == null;
			if (held == null || held.ResidentId <= 0 || held.PreparedTick != op.LastActivityTick
				|| !Text(held.BodyObjectId, 512) || held.BodyObjectId.Length == 0
				|| !Text(held.ZoneId, 512) || held.ZoneId.Length == 0) return false;
			try { return op.PendingDepartureId == KingdomResidentDepartureRules.Id(book.RealmId,
				book.SettlementId, held.ResidentId, held.BodyObjectId, held.PreparedTick); }
			catch { return false; }
		}

		private static bool Stage(GrowthStage value) => value >= GrowthStage.Camp && value <= GrowthStage.City;

		private static bool Credits(KingdomSubsidenceStepOperation op)
		{
			int width = CreditLength.Length + DeparturePrefix.Length + 64;
			if (op.CreditedDepartureIds == null || op.CreditedDepartureIds.Length != op.Completed * width)
				return false;
			if (op.Completed == 0) return true;
			string[] ids = new string[op.Completed];
			for (int i = 0; i < ids.Length; i++)
			{
				if (string.CompareOrdinal(op.CreditedDepartureIds, i * width, CreditLength, 0, CreditLength.Length) != 0)
					return false;
				ids[i] = op.CreditedDepartureIds.Substring(i * width + CreditLength.Length, DeparturePrefix.Length + 64);
				if (!Hashed(ids[i], DeparturePrefix)) return false;
				for (int j = 0; j < i; j++) if (ids[i] == ids[j]) return false;
				if (ids[i] == op.PendingDepartureId && (!op.PendingCredited || i != ids.Length - 1)) return false;
			}
			return !op.PendingCredited || op.PendingDepartureId == ids[ids.Length - 1];
		}

		private static bool AlreadyCredited(KingdomSubsidenceStepOperation op, string id)
		{
			int width = CreditLength.Length + DeparturePrefix.Length + 64;
			for (int i = 0; i < op.Completed; i++)
				if (string.CompareOrdinal(op.CreditedDepartureIds, i * width + CreditLength.Length,
					id, 0, id.Length) == 0) return true;
			return false;
		}

		internal static bool IsStepId(string value) => Hashed(value, StepPrefix);

		private static bool Hashed(string value, string prefix)
		{
			if (value == null || value.Length != prefix.Length + 64
				|| !value.StartsWith(prefix, StringComparison.Ordinal)) return false;
			for (int i = prefix.Length; i < value.Length; i++)
				if (!(value[i] >= '0' && value[i] <= '9') && !(value[i] >= 'a' && value[i] <= 'f')) return false;
			return true;
		}

		private static bool Text(string value, int max)
		{
			if (value == null || value.Length > max) return false;
			for (int i = 0; i < value.Length; i++)
				if (char.IsControl(value[i]) || char.IsSurrogate(value[i])) return false;
			return true;
		}
	}
}
