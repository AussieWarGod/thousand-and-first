using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomResidentDeathRuntime
	{
		private static bool Recovering;
		internal static void Record(KingdomSystem system, GameObject body, KingdomStandingCause cause)
		{
			try
			{
				if (system == null || !system.Founded || body == null) return;
				if (!Locate(system, body, out var city) || !Open(system, city, out Frame f))
				{ Notice(system, null, "The engine reported a death, but its exact resident journal could not be opened."); return; }
				int id = KingdomResidents.IdOf(body), index = -1;
				for (int i = 0; i < f.Journal.Entries.Count; i++)
				{
					var old = f.Journal.Entries[i];
					if (old.Before.ResidentId != id && old.Body != body.IDIfAssigned) continue;
					if (old.Before.ResidentId != id || old.Body != body.IDIfAssigned || old.Cause != cause || old.Tick != f.Game.TimeTicks)
					{ Notice(system, old, "A second death witness conflicts with retained identity or cause; no accounting was repeated."); return; }
					index = i; break;
				}
				if (index < 0)
				{
					if (!Capture(f, body, cause, out var r) || !KingdomResidentDeathRules.TryAppend(f.Journal, r, out var next, out index)
						|| !Save(f, next))
					{ Notice(system, null, "The engine death could not acquire bounded exact accounting. Existing evidence was retained; death was not prevented."); return; }
				}
				if (Recovering) return;
				Recovering = true;
				try { if (!Resume(f, index, body)) Notice(system, f.Journal.Entries[index], "Witnessed death accounting remains pending; its evidence was retained."); }
				finally { Recovering = false; }
			}
			catch (Exception ex) { Notice(system, null, "Witnessed death accounting stopped with " + ex.GetType().Name + "; the engine still owns death."); }
		}
		internal static bool TryRecoverPending(KingdomSystem system, out string failure)
		{
			failure = "A witnessed death awaits exact body-independent recovery.";
			if (Recovering) return false;
			Recovering = true;
			try
			{
				if (system == null || !system.Founded) return false;
				foreach (var city in system.OwnedCityBooks())
				{
					if (!Open(system, city, out Frame f)) return false;
					// Finish the unique prepared shared-account cut before any earlier witness can prepare another.
					for (int i = 0; i < f.Journal.Entries.Count; i++)
						if ((f.Journal.Entries[i].Phase == KingdomResidentDeathPhase.AccountingPrepared
							|| f.Journal.Entries[i].Phase == KingdomResidentDeathPhase.Accounted) && !RecoverOne(f, i)) return false;
					for (int i = 0; i < f.Journal.Entries.Count; i++)
					{
						var r = f.Journal.Entries[i];
						if (r.Phase == KingdomResidentDeathPhase.Settled) { WarnOutcome(system, r); continue; }
						if (!RecoverOne(f, i)) { Notice(system, r, failure); return false; }
					}
				}
				return CanProceed(system, out failure);
			}
			catch (Exception ex) { failure = "Witnessed death recovery stopped with " + ex.GetType().Name + "; evidence is retained."; return false; }
			finally { Recovering = false; }
		}
		private static bool RecoverOne(Frame f, int index)
		{
			var r = f.Journal.Entries[index]; GameObject body = GameObject.FindByID(r.Body);
			if (!GameObject.Validate(body)) body = null;
			else if (body.IDIfAssigned != r.Body || KingdomResidents.IdOf(body) != r.Before.ResidentId
				|| body.CurrentZone?.ZoneID != r.Zone) return false;
			return Resume(f, index, body);
		}
		private static bool Resume(Frame f, int index, GameObject body)
		{
			var r = f.Journal.Entries[index];
			if (r.Phase == KingdomResidentDeathPhase.Settled) { WarnOutcome(f.System, r); return Exact(f); }
			if (r.Phase == KingdomResidentDeathPhase.Witnessed)
			{
				if (!PrepareExpedition(f, r, body) || !Exact(f)
					|| !f.City.TryPublishWitnessedDeath(r, f.City.SubsidenceModel, () => Exact(f))) return false;
				var next = r.Copy(); next.Phase = KingdomResidentDeathPhase.StandingWritten;
				if (!Save(f, index, next)) return false; r = next;
			}
			if (r.Phase == KingdomResidentDeathPhase.StandingWritten)
			{
				if (!RetireBinding(f, r)) return false;
				var next = r.Copy(); next.Phase = KingdomResidentDeathPhase.BindingRetired;
				if (!Save(f, index, next)) return false; r = next;
			}
			if (r.Phase == KingdomResidentDeathPhase.BindingRetired)
			{
				if (!Roles(f, r, body) || !DeadExact(f, r)) return false;
				if (body != null && (!KingdomCitizenship.TryRemove(f.System, body, KingdomCitizenshipRemovalReason.Death, out _)
					|| !DeadExact(f, r))) return false;
				var next = r.Copy(); next.Phase = KingdomResidentDeathPhase.RolesSettled;
				if (!Save(f, index, next)) return false; r = next;
			}
			if (!Accounts(f, index, ref r) || !DeadExact(f, r)) return false;
			if (body != null && !ClearCountedProperties(f, r, body)) return false;
			return Tell(f, index, r, body?.CurrentZone);
		}
		private static bool ClearCountedProperties(Frame f, KingdomResidentDeathReceipt r, GameObject body)
		{
			if (!DeadExact(f, r) || body.IDIfAssigned != r.Body || KingdomResidents.IdOf(body) != r.Before.ResidentId) return false;
			string[] keys = { KingdomResidentIdentity.CultureProperty, KingdomResidentIdentity.SpeciesProperty,
				KingdomResidentIdentity.IdentityKeysProperty, KingdomCreed.CreedProperty, KingdomCreed.CreedPastProperty };
			string[] prior = { r.Culture, r.Species, r.IdentityKeys, r.Creed, r.PastCreeds };
			for (int i = 0; i < keys.Length; i++)
			{
				string value = body.GetStringProperty(keys[i]) ?? "";
				if (value != "" && value != prior[i] || !DeadExact(f, r)) return false;
				body.SetStringProperty(keys[i], null, RemoveIfNull: true);
				if (!DeadExact(f, r) || body.GetStringProperty(keys[i]) != null) return false;
			}
			return true;
		}
	}
}
