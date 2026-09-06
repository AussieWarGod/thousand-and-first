using System;
using System.Globalization;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomResidentDeathRuntime
	{
		private static string N(long value) { return value.ToString(CultureInfo.InvariantCulture); }
		private static bool CaptureRoles(Frame f, KingdomResidentDeathReceipt r)
		{
			var experience = f.System.Experience;
			if (experience == null || !KingdomExperienceRules.TryValidate(experience, out _)
				|| experience.IdentityBound && experience.RealmId != r.Realm) return false;
			if (!KingdomExperienceRules.TryGetOffice(experience, r.Settlement, out var office, out _)) return false;
			foreach (var held in experience.Offices)
				if ((held.HolderResidentId == r.Before.ResidentId || held.HolderObjectId == r.Body)
					&& held.SettlementId != r.Settlement) return false;
			foreach (var book in f.System.OwnedCityBooks())
			{
				var held = book?.NamedCook;
				if (!KingdomNamedCookRules.Validate(held, out _)) return false;
				if ((held.ResidentId == r.Before.ResidentId || held.BodyObjectId == r.Body)
					&& !KingdomNamedCookRules.IsVacant(held.Phase) && !ReferenceEquals(book, f.City)) return false;
			}
			if (!KingdomExperienceRules.TryGetRemembrance(experience, r.Settlement, out var remembrance, out _)) return false;
			r.Remembrance = r.Memory && experience.IdentityBound && remembrance == null;
			r.RemembranceUnavailable = r.Memory && !experience.IdentityBound;
			if (office != null && !KingdomResidentDeathRules.RoleClaimAgrees(r, office.HolderResidentId, office.HolderObjectId)) return false;
			if (office != null && office.HolderResidentId == r.Before.ResidentId)
			{
				if (office.HolderObjectId != r.Body || office.HolderName != r.Before.Name
					|| office.Phase != KingdomCivicOfficePhase.Held && office.Phase != KingdomCivicOfficePhase.AppointmentPrepared) return false;
				r.OfficeGeneration = office.Generation; r.OfficeBefore = OfficeWire(office);
			}
			var cook = f.City.NamedCook;
			if (!KingdomNamedCookRules.Validate(cook, out _)) return false;
			if (!KingdomNamedCookRules.IsVacant(cook.Phase)
				&& !KingdomResidentDeathRules.RoleClaimAgrees(r, cook.ResidentId, cook.BodyObjectId)) return false;
			if (cook.ResidentId == r.Before.ResidentId && !KingdomNamedCookRules.IsVacant(cook.Phase))
			{
				if (cook.BodyObjectId != r.Body || cook.RealmId != r.Realm || cook.SettlementId != r.Settlement
					|| cook.Phase != KingdomNamedCookPhase.Applied && cook.Phase != KingdomNamedCookPhase.Prepared) return false;
				r.CookGeneration = cook.Generation; r.CookBefore = CookWire(cook);
			}
			if (!KingdomPolityRules.TryCaptureDeedResident(f.System.PolityLedger, r.Realm, r.Settlement,
				r.Before.ResidentId, r.Before.Name, out var figure, out _)) return false;
			r.FigureId = figure?.FigureId ?? "";
			return true;
		}
		private static bool Roles(Frame f, KingdomResidentDeathReceipt r, GameObject body)
		{
			if (r.RoleFault != "" || !DeadExact(f, r) || !Office(f, r, body) || !DeadExact(f, r)) return false;
			if (r.CookGeneration != 0)
			{
				var current = f.City.NamedCook; var prior = ReadCook(r.CookBefore);
				var prepared = KingdomNamedCookRules.BeginVacancy(prior, KingdomNamedCookVacancyCause.Death);
				var after = KingdomNamedCookRules.CompleteVacancy(prepared, r.Tick);
				if (current == null || after == null || CookWire(current) != r.CookBefore
					&& CookWire(current) != CookWire(prepared) && CookWire(current) != CookWire(after)) return false;
				if (!KingdomNamedCook.TryConcludeWitnessedDeath(f.System, f.City, r.Before.ResidentId,
					r.Body, r.Tick, body, out _) || !DeadExact(f, r) || CookWire(f.City.NamedCook) != CookWire(after)) return false;
			}
			if (r.FigureId != "")
			{
				var ledger = f.System.PolityLedger;
				if (ledger == null || ledger.RealmId != r.Realm || !KingdomPolityRules.TryValidate(ledger, out _)) return false;
				var figure = KingdomPolityAuthority.Figure(ledger, r.FigureId);
				if (figure == null || figure.DisplayName != r.Before.Name || figure.Origin != KingdomPolityFigureOrigin.PromotedByDeed) return false;
				if (!KingdomPolityRules.TryConcludeDeedResident(ledger, ledger.Revision, r.Settlement, r.Before.ResidentId,
					r.Before.Name, KingdomPolityFigurePhase.Dead, out _, out string conclusion, out _) || !DeadExact(f, r)) return false;
				figure = KingdomPolityAuthority.Figure(ledger, r.FigureId);
				if (!ReferenceEquals(ledger, f.System.PolityLedger) || figure.Phase != KingdomPolityFigurePhase.Dead
					|| figure.ConclusionRef != conclusion || figure.ResidentId != 0 || !string.IsNullOrEmpty(figure.ResidentSettlementId)) return false;
			}
			if (r.Remembrance)
			{
				var ledger = f.System.Experience;
				if (ledger == null || !KingdomExperienceRules.TryGetRemembrance(ledger, r.Settlement, out var prior, out _)) return false;
				if (prior == null && !KingdomExperienceRules.TryCreateRemembranceEligibility(ledger, ledger.Revision,
					r.Settlement, r.SettlementName, r.Before.ResidentId, r.Before.Name, r.Tick, out _)) return false;
				if (!ReferenceEquals(ledger, f.System.Experience) || !DeadExact(f, r)) return false;
			}
			return true;
		}
		private static bool Office(Frame f, KingdomResidentDeathReceipt r, GameObject body)
		{
			if (r.OfficeGeneration == 0) return true;
			var ledger = f.System.Experience;
			if (ledger == null || !KingdomExperienceRules.TryGetOffice(ledger, r.Settlement, out var current, out _)) return false;
			var prior = ReadOffice(r.OfficeBefore);
			var prepared = KingdomExperienceRules.CopyOffice(prior);
			prepared.Phase = KingdomCivicOfficePhase.VacancyPrepared; prepared.VacancyCause = KingdomCivicOfficeVacancyCause.Death;
			prepared.PredecessorResidentId = prior.HolderResidentId; prepared.PredecessorName = prior.HolderName; prepared.ChangedTick = r.Tick;
			var after = KingdomExperienceRules.CopyOffice(prepared); after.Phase = KingdomCivicOfficePhase.Vacant;
			after.HolderResidentId = 0; after.HolderName = after.HolderObjectId = null; after.OwnsRole = false;
			if (current == null || OfficeWire(current) != r.OfficeBefore && OfficeWire(current) != OfficeWire(prepared)
				&& OfficeWire(current) != OfficeWire(after)) return false;
			if (OfficeWire(current) == OfficeWire(after)) return true;
			if (!KingdomExperienceRules.TryPrepareOfficeVacancy(ledger, ledger.Revision, r.Settlement, r.Before.ResidentId,
				KingdomCivicOfficeVacancyCause.Death, r.Tick, out _) || !DeadExact(f, r)) return false;
			if (!KingdomOfficeRuntime.TryConcludeWitnessedDeath(f.System, f.City, r.Before.ResidentId, r.Body, body)
				|| !DeadExact(f, r)) return false;
			return ReferenceEquals(ledger, f.System.Experience) && DeadExact(f, r)
				&& KingdomExperienceRules.TryGetOffice(ledger, r.Settlement, out current, out _) && OfficeWire(current) == OfficeWire(after);
		}
		internal static bool TryWitness(KingdomSystem system, KingdomCityBook city, int residentId, string body,
			out KingdomResidentDeathReceipt witness)
		{
			witness = null;
			try
			{
				if (!Open(system, city, out Frame f)) return false;
				foreach (var r in f.Journal.Entries)
					if (r.Before.ResidentId == residentId && r.Body == body && r.Phase >= KingdomResidentDeathPhase.BindingRetired
						&& DeadExact(f, r) && system.Bindings.TryReadExact(out var table, out _)
						&& !table.TryGet(residentId, KingdomBindingKind.Resident, out _)) { witness = r.Copy(); return true; }
				return false;
			}
			catch { return false; }
		}
		internal static string OfficeWire(KingdomCivicOfficeReceipt r)
		{
			return KingdomResidentDeathCodec.Fields(N(r.Version), N((int)r.Phase), N((int)r.VacancyCause), N(r.Generation), r.SettlementId,
				r.SettlementName, N(r.WorkId), N(r.HolderResidentId), r.HolderName, r.HolderObjectId, r.OwnsRole ? "1" : "0",
				N(r.PredecessorResidentId), r.PredecessorName, N(r.ChangedTick), r.Fault);
		}
		private static KingdomCivicOfficeReceipt ReadOffice(string wire)
		{
			string[] v = KingdomResidentDeathCodec.ReadFields(wire, 15); if (v.Length != 15) throw new FormatException();
			var r = new KingdomCivicOfficeReceipt { Version = I(v[0]), Phase = (KingdomCivicOfficePhase)I(v[1]), VacancyCause = (KingdomCivicOfficeVacancyCause)I(v[2]),
				Generation = I(v[3]), SettlementId = v[4], SettlementName = v[5], WorkId = I(v[6]), HolderResidentId = I(v[7]), HolderName = v[8], HolderObjectId = v[9],
				OwnsRole = v[10] == "1", PredecessorResidentId = I(v[11]), PredecessorName = v[12], ChangedTick = L(v[13]), Fault = v[14] };
			if (OfficeWire(r) != wire) throw new FormatException(); return r;
		}
		internal static string CookWire(KingdomNamedCookReceipt r)
		{
			return KingdomResidentDeathCodec.Fields(N(r.Version), N((int)r.Phase), N(r.Generation), r.RealmId, r.SettlementId, r.SettlementName,
				N(r.ResidentId), r.ResidentName, r.BodyObjectId, r.RecipeId, r.RecipeDisplayName, r.EffectId, r.GraphFingerprint, N(r.DesignatedTick), N(r.ReleasedTick), r.Fault);
		}
		internal static KingdomNamedCookReceipt ReadCook(string wire)
		{
			string[] v = KingdomResidentDeathCodec.ReadFields(wire, 16); if (v.Length != 16) throw new FormatException();
			var r = new KingdomNamedCookReceipt { Version = I(v[0]), Phase = (KingdomNamedCookPhase)I(v[1]), Generation = I(v[2]), RealmId = v[3], SettlementId = v[4], SettlementName = v[5],
				ResidentId = I(v[6]), ResidentName = v[7], BodyObjectId = v[8], RecipeId = v[9], RecipeDisplayName = v[10], EffectId = v[11], GraphFingerprint = v[12],
				DesignatedTick = L(v[13]), ReleasedTick = L(v[14]), Fault = v[15] };
			if (CookWire(r) != wire || !KingdomNamedCookRules.Validate(r, out _)) throw new FormatException(); return r;
		}
		private static int I(string value) { return int.Parse(value, CultureInfo.InvariantCulture); }
		private static long L(string value) { return long.Parse(value, CultureInfo.InvariantCulture); }
	}

	public static partial class KingdomOfficeRuntime
	{
		internal static bool TryConcludeWitnessedDeath(KingdomSystem system, KingdomCityBook city,
			int residentId, string bodyId, GameObject body)
		{
			if (!KingdomResidentDeathRuntime.TryWitness(system, city, residentId, bodyId, out var death)) return false;
			var ledger = system.Experience;
			if (!KingdomExperienceRules.TryGetOffice(ledger, city.SettlementId, out var prepared, out _)
				|| prepared == null || prepared.Generation != death.OfficeGeneration
				|| prepared.Phase != KingdomCivicOfficePhase.VacancyPrepared || prepared.VacancyCause != KingdomCivicOfficeVacancyCause.Death
				|| prepared.HolderResidentId != residentId || prepared.HolderObjectId != bodyId) return false;
			string held = KingdomResidentDeathRuntime.OfficeWire(prepared);
			Func<bool> exact = () => ReferenceEquals(system.Experience, ledger)
				&& KingdomResidentDeathRuntime.TryWitness(system, city, residentId, bodyId, out var witness)
				&& witness.Tick == death.Tick && witness.OfficeBefore == death.OfficeBefore
				&& KingdomExperienceRules.TryGetOffice(ledger, city.SettlementId, out var current, out _)
				&& current != null && KingdomResidentDeathRuntime.OfficeWire(current) == held;
			if (!exact()) return false;
			if (body != null)
			{
				if (body.IDIfAssigned != bodyId || KingdomResidents.IdOf(body) != residentId) return false;
				bool cleaned = TryCleanupDeathProjection(system, prepared, body, out _);
				if (!exact()) return false;
				if (!cleaned) MarkDeathResidue(system, prepared, body);
				if (!exact()) return false;
			}
			if (!city.TryReadExact(out var state, out _) || !exact()
				|| !KingdomExperienceRules.TryCompleteOfficeDeathVacancy(ledger, ledger.Revision,
					city.SettlementId, death.OfficeGeneration, state, out _)
				|| !KingdomResidentDeathRuntime.TryWitness(system, city, residentId, bodyId, out _)
				|| !ReferenceEquals(system.Experience, ledger)) return false;
			if (!KingdomExperienceRules.TryGetOffice(ledger, city.SettlementId, out var vacant, out _)) return false;
			ProjectCompatibility(system, vacant); TellVacant(system, vacant);
			return KingdomResidentDeathRuntime.TryWitness(system, city, residentId, bodyId, out _)
				&& ReferenceEquals(system.Experience, ledger);
		}
	}
}
