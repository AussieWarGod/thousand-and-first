using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomResidentDeathRuntime
	{
		private static bool Locate(KingdomSystem system, GameObject body, out KingdomCityBook city)
		{
			city = null;
			if (system == null || !GameObject.Validate(body) || string.IsNullOrEmpty(body.IDIfAssigned)) return false;
			int id = KingdomResidents.IdOf(body); if (id <= 0) return false;
			foreach (var book in system.OwnedCityBooks())
			{
				if (book == null || !book.TryReadExact(out var state, out _)) return false;
				if (!state.TryResidentIndex(id, out _)) continue;
				if (city != null) return false; city = book;
			}
			return city != null;
		}
		private static bool Capture(Frame f, GameObject body, KingdomStandingCause cause, out KingdomResidentDeathReceipt r)
		{
			r = null;
			if (!Exact(f) || !GameObject.Validate(body) || !KingdomCitizenship.BelongsTo(f.System, body)
				|| !f.City.TryReadExact(out var state, out _) || !state.TryResidentIndex(KingdomResidents.IdOf(body), out int index)
				|| !state.TryResident(index, out var before) || !KingdomResidentRules.Bindable(before.Standing)
				|| !f.System.Bindings.TryReadExact(out var table, out _) || !table.TryGet(before.ResidentId, KingdomBindingKind.Resident, out var binding)
				|| binding.ObjectId != body.IDIfAssigned || binding.ZoneId != body.CurrentZone?.ZoneID || before.BoundZoneId != binding.ZoneId
				|| before.Name != body.GetStringProperty("KingdomName") || before.Origin != (body.GetStringProperty("KingdomOrigin") ?? "")) return false;
			if (!f.System.TryFindSettlement(f.City, out bool seated, out var other)) return false;
			var value = new KingdomResidentDeathReceipt { Realm = f.Realm, Settlement = f.Settlement,
				SettlementName = seated ? f.System.SeatName : other.SettlementName, Body = binding.ObjectId, Zone = binding.ZoneId,
				Tick = f.Game.TimeTicks, MintedTick = binding.MintedTick, Before = before, Cause = cause, Memory = KingdomOffices.Enabled,
				Culture = body.GetStringProperty(KingdomResidentIdentity.CultureProperty) ?? "",
				Species = body.GetStringProperty(KingdomResidentIdentity.SpeciesProperty) ?? "",
				IdentityKeys = body.GetStringProperty(KingdomResidentIdentity.IdentityKeysProperty) ?? "",
				Creed = body.GetStringProperty(KingdomCreed.CreedProperty) ?? "", PastCreeds = body.GetStringProperty(KingdomCreed.CreedPastProperty) ?? "" };
			value.Telling = value.Memory ? KingdomResidentDeathTelling.Pending : KingdomResidentDeathTelling.Disabled;
			if (!KingdomSubsidenceStepCodec.TryDecode(f.City.SubsidenceModel, out var step)) return false;
			value.StepWire = f.City.SubsidenceModel;
			if (step.Active != null && step.Active.RungModel != KingdomSubsidenceStepRules.NoRungs
				&& step.Active.RungModel != KingdomSubsidenceStepRules.UnplannedRungs)
			{
				if (!KingdomSubsidenceStepRules.TryReadRungPlan(step, out var plan)) return false;
				foreach (var work in plan.Works)
					for (int i = 0; i < work.Roofs.Count; i++)
						if (work.Roofs[i].ResidentId == before.ResidentId)
						{ value.RoofWork = work.ObjectId; value.RoofIndex = i; value.RoofBlocked = work.Roofs[i].Phase != KingdomSubsidenceEffectPhase.Proved; }
			}
			if (!CaptureRoles(f, value)) value.RoleFault = "The witnessed role authority was unreadable; no role absence was inferred.";
			if (!KingdomExpeditions.TryCaptureWitnessedDeath(f.System, before.ResidentId, value.Tick, value.Zone,
				out value.ExpeditionBefore, out value.ExpeditionPrepared))
				value.RoleFault = "The witnessed expedition authority was unreadable; no terminal job was guessed.";
			if (!KingdomResidentDeathRules.Valid(value) || !Exact(f) || f.Game.TimeTicks != value.Tick
				|| !GameObject.Validate(body) || body.IDIfAssigned != value.Body || body.CurrentZone?.ZoneID != value.Zone
				|| !f.City.TryReadExact(out var recheck, out _) || !recheck.TryResident(index, out var row)
				|| KingdomResidentDeathCodec.Row(row) != KingdomResidentDeathCodec.Row(before)
				|| !f.System.Bindings.TryReadExact(out var again, out _) || !again.TryGet(before.ResidentId, KingdomBindingKind.Resident, out var held)
				|| !KingdomResidentDeathRules.BindingExact(value, held)) return false;
			r = value; return true;
		}
		private static bool DeadExact(Frame f, KingdomResidentDeathReceipt r)
		{
			return Exact(f) && f.City.HasValidSubsidenceStorage() && f.City.TryReadExact(out var state, out _)
				&& state.TryResidentIndex(r.Before.ResidentId, out int at) && state.TryResident(at, out var row)
				&& KingdomResidentDeathRules.RowCut(r, row) == 1;
		}
		private static bool PrepareExpedition(Frame f, KingdomResidentDeathReceipt r, GameObject body)
		{
			Func<bool> exact = () => Exact(f) && r.RoleFault == "" && f.City.TryReadExact(out var state, out _)
				&& state.TryResidentIndex(r.Before.ResidentId, out int at) && state.TryResident(at, out var row)
				&& KingdomResidentDeathRules.RowCut(r, row) >= 0 && f.System.Bindings.TryReadExact(out var bindings, out _)
				&& bindings.TryGet(r.Before.ResidentId, KingdomBindingKind.Resident, out var bound)
				&& KingdomResidentDeathRules.BindingExact(r, bound);
			return exact() && KingdomExpeditions.TryPrepareWitnessedDeath(f.System, r.Before.ResidentId,
				r.Tick, r.Zone, r.ExpeditionBefore, r.ExpeditionPrepared, body, exact) && exact();
		}
		private static bool RetireBinding(Frame f, KingdomResidentDeathReceipt r)
		{
			var registry = f.System.Bindings;
			if (!DeadExact(f, r) || registry == null || !registry.TryReadExact(out var table, out _)) return false;
			if (!table.TryGet(r.Before.ResidentId, KingdomBindingKind.Resident, out var held)) return true;
			if (!KingdomResidentDeathRules.BindingExact(r, held)
				|| !table.TryUnbind(r.Before.ResidentId, KingdomBindingKind.Resident, KingdomUnbindCause.Death,
					out var next, out _, out _)) return false;
			var replacement = new KingdomBindingRegistry();
			if (!replacement.TryPublish(next, out _)) return false;
			var keys = registry.Keys; var kinds = registry.Kinds; var zones = registry.ZoneIds;
			var objects = registry.ObjectIds; var ticks = registry.MintedTicks;
			if (!DeadExact(f, r) || !ReferenceEquals(registry, f.System.Bindings) || !registry.TryReadExact(out var current, out _)
				|| !SameBindings(table, current) || !ReferenceEquals(keys, registry.Keys) || !ReferenceEquals(kinds, registry.Kinds)
				|| !ReferenceEquals(zones, registry.ZoneIds) || !ReferenceEquals(objects, registry.ObjectIds) || !ReferenceEquals(ticks, registry.MintedTicks)) return false;
			// Allocation and rule publication completed on the candidate. The exact carrier swap has no callbacks.
			registry.Keys = replacement.Keys; registry.Kinds = replacement.Kinds; registry.ZoneIds = replacement.ZoneIds;
			registry.ObjectIds = replacement.ObjectIds; registry.MintedTicks = replacement.MintedTicks;
			return DeadExact(f, r) && ReferenceEquals(registry, f.System.Bindings) && registry.TryReadExact(out var after, out _)
				&& SameBindings(next, after);
		}
		private static bool SameBindings(KingdomBindingTable a, KingdomBindingTable b)
		{
			if (a.Count != b.Count) return false;
			for (int i = 0; i < a.Count; i++)
			{
				if (!a.TryAt(i, out var x) || !b.TryAt(i, out var y) || x.BindingKey != y.BindingKey || x.Kind != y.Kind
					|| x.ObjectId != y.ObjectId || x.ZoneId != y.ZoneId || x.MintedTick != y.MintedTick) return false;
			}
			return true;
		}
	}
}
