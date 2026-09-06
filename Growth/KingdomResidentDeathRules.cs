using System;
using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static class KingdomResidentDeathRules
	{
		internal const int MaxEntries = 4096;
		internal static bool Valid(KingdomResidentDeathJournal journal)
		{
			if (journal == null || !KingdomIdentityRules.IsRealmId(journal.Realm)
				|| !KingdomIdentityRules.IsSettlementId(journal.Settlement) || journal.Entries == null
				|| journal.Entries.Count > MaxEntries) return false;
			var ids = new HashSet<int>(); var bodies = new HashSet<string>(StringComparer.Ordinal);
			int accounts = 0;
			foreach (var r in journal.Entries)
			{
				if (!Valid(r) || r.Realm != journal.Realm || r.Settlement != journal.Settlement
					|| !ids.Add(r.Before.ResidentId) || !bodies.Add(r.Body)) return false;
				if (r.Phase == KingdomResidentDeathPhase.AccountingPrepared || r.Phase == KingdomResidentDeathPhase.Accounted) accounts++;
			}
			return accounts <= 1;
		}
		internal static bool Valid(KingdomResidentDeathReceipt r)
		{
			if (r == null || !KingdomIdentityRules.IsRealmId(r.Realm) || !KingdomIdentityRules.IsSettlementId(r.Settlement)
				|| !Text(r.SettlementName, 1024, false) || !Text(r.Body, 1024, false) || !Text(r.Zone, 1024, false)
				|| r.Tick < 0 || r.MintedTick < 0 || r.MintedTick > r.Tick || r.Before.ArrivedTick > r.Tick
				|| !BeforeValid(r.Before) || !KingdomResidentRules.CauseFits(KingdomResidentStanding.Dead, r.Cause)
				|| (int)r.Phase < 0 || r.Phase > KingdomResidentDeathPhase.Settled
				|| (int)r.Telling < 0 || r.Telling > KingdomResidentDeathTelling.Disabled
				|| !Text(r.Culture, 1024) || !Text(r.Species, 1024) || !Text(r.IdentityKeys, 16384)
				|| !Text(r.Creed, 1024) || !Text(r.PastCreeds, 16384)
				|| r.OfficeGeneration < 0 || r.CookGeneration < 0 || !Text(r.OfficeBefore, 16384)
				|| !Text(r.CookBefore, 16384) || !Text(r.FigureId, 1024) || !Text(r.RoleFault, 512)
				|| !Text(r.ExpeditionBefore, 65536) || !Text(r.ExpeditionPrepared, 65536)
				|| (r.ExpeditionBefore == "") != (r.ExpeditionPrepared == "")
				|| (r.OfficeGeneration == 0) != (r.OfficeBefore == "") || (r.CookGeneration == 0) != (r.CookBefore == "")
				|| r.BeforeAccounts == null || r.AfterAccounts == null || r.BeforeAccounts.Length != r.AfterAccounts.Length) return false;
			if (!r.Memory && r.Telling != KingdomResidentDeathTelling.Disabled
				|| !r.Memory && (r.Remembrance || r.RemembranceUnavailable) || r.Remembrance && r.RemembranceUnavailable
				|| r.Memory && r.Telling == KingdomResidentDeathTelling.Disabled
				|| r.Phase < KingdomResidentDeathPhase.Accounted && r.Memory && r.Telling != KingdomResidentDeathTelling.Pending
				|| r.Phase == KingdomResidentDeathPhase.Settled && (r.Telling == KingdomResidentDeathTelling.Pending
					|| r.Telling == KingdomResidentDeathTelling.Attempting)) return false;
			bool accounts = r.Phase == KingdomResidentDeathPhase.AccountingPrepared || r.Phase == KingdomResidentDeathPhase.Accounted;
			if (r.BeforeAccounts.Length != (accounts ? 6 : 0)
				|| r.Phase < KingdomResidentDeathPhase.AccountingPrepared && r.AccountProof != ""
				|| r.Phase >= KingdomResidentDeathPhase.AccountingPrepared && !KingdomChronicleReceiptRules.IsSha256(r.AccountProof)) return false;
			for (int i = 0; i < r.BeforeAccounts.Length; i++)
				if (!Text(r.BeforeAccounts[i], 2097152) || !Text(r.AfterAccounts[i], 2097152)) return false;
			if (accounts && !ValidAccounts(r)) return false;
			if (r.StepWire == "") return r.RoofWork == "" && r.RoofIndex == -1 && !r.RoofBlocked;
			if (!KingdomSubsidenceStepCodec.TryDecode(r.StepWire, out var step)
				|| step.Admission == KingdomSubsidenceAdmission.Admitted && (step.RealmId != r.Realm || step.SettlementId != r.Settlement)) return false;
			if (step.Active == null || step.Active.RungModel == KingdomSubsidenceStepRules.NoRungs
				|| step.Active.RungModel == KingdomSubsidenceStepRules.UnplannedRungs) return r.RoofWork == "" && r.RoofIndex == -1 && !r.RoofBlocked;
			if (!KingdomSubsidenceStepRules.TryReadRungPlan(step, out var plan)) return false;
			int selected = 0;
			foreach (var work in plan.Works)
				for (int i = 0; i < work.Roofs.Count; i++)
				{
					var roof = work.Roofs[i]; if (roof.ResidentId != r.Before.ResidentId) continue;
					selected++;
					if (roof.BodyObjectId != r.Body || work.ObjectId != r.RoofWork || i != r.RoofIndex
						|| r.RoofBlocked != (roof.Phase != KingdomSubsidenceEffectPhase.Proved)) return false;
				}
			return selected == 1 || selected == 0 && r.RoofWork == "" && r.RoofIndex == -1 && !r.RoofBlocked;
		}
		internal static bool TryAppend(KingdomResidentDeathJournal journal, KingdomResidentDeathReceipt witness,
			out KingdomResidentDeathJournal next, out int index)
		{
			next = null; index = -1;
			if (!Valid(journal) || !Valid(witness) || witness.Realm != journal.Realm || witness.Settlement != journal.Settlement
				|| witness.Phase != KingdomResidentDeathPhase.Witnessed) return false;
			for (int i = 0; i < journal.Entries.Count; i++)
			{
				var r = journal.Entries[i];
				if (r.Before.ResidentId != witness.Before.ResidentId && r.Body != witness.Body) continue;
				if (!SameWitness(r, witness)) return false;
				next = journal; index = i; return true;
			}
			if (journal.Entries.Count == MaxEntries) return false;
			index = journal.Entries.Count; next = journal.With(index, witness); return Valid(next);
		}
		internal static bool SameWitness(KingdomResidentDeathReceipt a, KingdomResidentDeathReceipt b)
		{
			return a != null && b != null && a.Realm == b.Realm && a.Settlement == b.Settlement && a.Body == b.Body
				&& KingdomResidentDeathCodec.Row(a.Before) == KingdomResidentDeathCodec.Row(b.Before)
				&& a.Cause == b.Cause && a.Tick == b.Tick && a.Zone == b.Zone && a.MintedTick == b.MintedTick
				&& a.SettlementName == b.SettlementName && a.Memory == b.Memory && a.StepWire == b.StepWire
				&& a.Remembrance == b.Remembrance && a.RemembranceUnavailable == b.RemembranceUnavailable
				&& a.RoofWork == b.RoofWork && a.RoofIndex == b.RoofIndex && a.RoofBlocked == b.RoofBlocked
				&& a.Culture == b.Culture && a.Species == b.Species && a.IdentityKeys == b.IdentityKeys
				&& a.Creed == b.Creed && a.PastCreeds == b.PastCreeds && a.OfficeGeneration == b.OfficeGeneration
				&& a.CookGeneration == b.CookGeneration && a.OfficeBefore == b.OfficeBefore && a.CookBefore == b.CookBefore
				&& a.FigureId == b.FigureId && a.RoleFault == b.RoleFault && a.ExpeditionBefore == b.ExpeditionBefore
				&& a.ExpeditionPrepared == b.ExpeditionPrepared;
		}
		internal static bool RoleClaimAgrees(KingdomResidentDeathReceipt r, int residentId, string body)
		{ return r != null && (residentId == r.Before.ResidentId) == (body == r.Body); }
		internal static int RowCut(KingdomResidentDeathReceipt r, KingdomResidentRow row)
		{
			if (!Valid(r)) return -1;
			if (KingdomResidentDeathCodec.Row(row) == KingdomResidentDeathCodec.Row(r.Before)) return 0;
			return KingdomResidentDeathCodec.Row(row) == KingdomResidentDeathCodec.Row(
				r.Before.WithStanding(KingdomResidentStanding.Dead, r.Cause)) ? 1 : -1;
		}
		internal static bool BindingExact(KingdomResidentDeathReceipt r, KingdomBinding b)
		{ return b.BindingKey == r.Before.ResidentId && b.Kind == KingdomBindingKind.Resident && b.ObjectId == r.Body && b.ZoneId == r.Zone && b.MintedTick == r.MintedTick; }
		internal static int Prefix(IReadOnlyList<string> before, IReadOnlyList<string> after, IReadOnlyList<string> observed)
		{
			if (before == null || after == null || observed == null || before.Count != after.Count || before.Count != observed.Count) return -1;
			int cut = 0; bool pending = false;
			for (int i = 0; i < before.Count; i++)
			{
				if (observed[i] == after[i] && (!pending || before[i] == after[i])) { if (!pending) cut = i + 1; continue; }
				if (observed[i] != before[i]) return -1;
				pending = true;
			}
			return cut;
		}
		internal static bool TryPrepareAccounts(KingdomResidentDeathReceipt r, string[] before, out KingdomResidentDeathReceipt next)
		{
			next = null;
			if (!Valid(r) || r.Phase != KingdomResidentDeathPhase.RolesSettled || before == null || before.Length != 6) return false;
			try
			{
				var value = r.Copy(); value.BeforeAccounts = (string[])before.Clone(); value.AfterAccounts = AfterAccounts(r, before);
				if (!KingdomChronicleReceiptRules.TryCanonicalHash("taf-witnessed-death-accounts-v1",
					new List<string>(value.BeforeAccounts) { value.AfterAccounts[0], value.AfterAccounts[1], value.AfterAccounts[2],
						value.AfterAccounts[3], value.AfterAccounts[4], value.AfterAccounts[5] }, out value.AccountProof)) return false;
				value.Phase = KingdomResidentDeathPhase.AccountingPrepared;
				if (!Valid(value)) return false; next = value; return true;
			}
			catch { return false; }
		}
		private static bool ValidAccounts(KingdomResidentDeathReceipt r)
		{
			try
			{
				string[] after = AfterAccounts(r, r.BeforeAccounts);
				for (int i = 0; i < after.Length; i++) if (after[i] != r.AfterAccounts[i]) return false;
				var fields = new List<string>(r.BeforeAccounts); fields.AddRange(after);
				return KingdomChronicleReceiptRules.TryCanonicalHash("taf-witnessed-death-accounts-v1", fields, out string proof) && proof == r.AccountProof;
			}
			catch { return false; }
		}
		private static string[] AfterAccounts(KingdomResidentDeathReceipt r, string[] before)
		{
			var result = new string[6];
			for (int i = 0; i < 5; i++)
			{
				var map = KingdomResidentDeathCodec.ReadMap(before[i]);
				if (i == 0) KingdomResidentIdentityRules.Transition(map, KingdomZoningRules.KindCulture, r.Culture, null);
				if (i == 1) KingdomResidentIdentityRules.Transition(map, KingdomZoningRules.KindSpecies, r.Species, null);
				if (i == 2) KingdomResidentIdentityRules.TransitionIdentityKeys(map, KingdomResidentIdentityRules.DecodeIdentityKeys(r.IdentityKeys), null);
				if (i == 3) Drop(map, r.Creed);
				if (i == 4) foreach (string creed in KingdomCreedRules.DecodeKept(r.PastCreeds)) Drop(map, creed);
				result[i] = KingdomResidentDeathCodec.Map(map);
			}
			string[] history = KingdomResidentDeathCodec.ReadFields(before[5]);
			if (history.Length < 1 || (history.Length - 1) % 4 != 0 || history.Length > 16385
				|| !int.TryParse(history[0], out int count) || count < 0 || count.ToString(System.Globalization.CultureInfo.InvariantCulture) != history[0]) throw new FormatException();
			for (int i = 1; i < history.Length; i++) if (!Text(history[i], 4096)) throw new FormatException();
			var fields = new List<string>(history);
			if (r.Memory)
			{
				fields[0] = checked(count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
				fields.Add(r.Before.Name); fields.Add(r.Before.Origin); fields.Add(r.Before.Arrived);
				fields.Add(KingdomOfficeRules.CauseClause((KingdomOfficeRules.DeathCause)((int)r.Cause - (int)KingdomStandingCause.Unwitnessed)));
			}
			result[5] = KingdomResidentDeathCodec.Fields(fields.ToArray()); return result;
		}
		private static void Drop(Dictionary<string, int> map, string key)
		{
			if (string.IsNullOrEmpty(key) || !map.TryGetValue(key, out int count)) return;
			if (count <= 1) map.Remove(key); else map[key] = count - 1;
		}
		internal static bool Text(string text, int maximum, bool empty = true)
		{
			if (text == null || text.Length > maximum || !empty && text.Length == 0) return false;
			foreach (char c in text) if (char.IsControl(c)) return false;
			try { new UTF8Encoding(false, true).GetByteCount(text); return true; } catch { return false; }
		}
		private static bool BeforeValid(KingdomResidentRow r)
		{
			return r.ResidentId > 0 && Text(r.Name, 1024, false) && Text(r.Origin, 1024) && Text(r.Arrived, 1024)
				&& r.ArrivedTick >= 0 && KingdomResidentRules.CauseFits(r.Standing, r.Cause)
				&& r.Standing != KingdomResidentStanding.Dead && Enum.IsDefined(typeof(KingdomDayShape), r.DayShape)
				&& Text(r.BoundZoneId, 1024, false) && (r.CreedToward == null || Text(r.CreedToward, 1024))
				&& (r.KeptCreeds == null || Text(r.KeptCreeds, 16384));
		}
	}
}
