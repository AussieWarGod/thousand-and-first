using System;
using System.Collections.Generic;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomResidents
	{
		// ==================================================================================
		// The registry
		// ==================================================================================

		/// <summary>
		/// Check-before-mint (&sect;3.8), asked at the edge and answered by the rules.
		/// <para>
		/// The presence is resolved here because only the engine knows whether an object id still
		/// names something live and where; the VERDICT is <c>KingdomBindingRules</c>'s, because a
		/// second opinion about duplication is how a settler ends up in two places.
		/// </para>
		/// <para>
		/// <b>W2 ships the verdict. W3 obeys it.</b> Nothing in this wave mints or moves a body, so
		/// this has exactly one production caller today — the roster read below, which only ever
		/// asks about bodies that are already standing in front of it.
		/// </para>
		/// </summary>
		public static KingdomBindingVerdict Judge(KingdomSystem System, int bindingKey, KingdomBindingKind kind, string zoneId)
		{
			KingdomBindingTable table;
			if (!TryTable(System, out table))
			{
				// No registry is not a miss. A miss is a licence to mint, and a realm whose registry
				// could not be read has no way of knowing what it would be minting a second copy of.
				return KingdomBindingVerdict.Refuse;
			}
			KingdomBinding binding;
			if (!table.TryGet(bindingKey, kind, out binding))
			{
				if (kind == KingdomBindingKind.Transient
					&& !KingdomExperienceRuntime.FoundationOwnsCarrierClaim(System, bindingKey)
					&& !KingdomExperienceRuntime.TryAdmitFoundationTransientClaim(System,
						bindingKey, out KingdomExperienceCapacityFault _, out string _))
					return KingdomBindingVerdict.Refuse;
				return KingdomBindingRules.Judge(kind, KingdomBodyPresence.None);
			}
			return KingdomBindingRules.Judge(kind, PresenceOf(binding, zoneId));
		}

		/// <summary>
		/// What the ground says about one bound key, unnarrowed by a verdict.
		/// <para>
		/// <see cref="Judge"/> collapses "live in another resident zone" and "on disk" to the same
		/// refusal, which is exactly right when the question is <i>may I mint</i> and exactly wrong
		/// when it is <i>has the model outlived this body</i>. LIVING-CITY-ARCHITECTURE &sect;3.8's
		/// t2 closes a job whose carrier is frozen; it must never close one whose carrier is still
		/// walking somewhere the founder could go and watch.
		/// </para>
		/// </summary>
		public static KingdomBodyPresence PresenceOfKey(KingdomSystem System, int bindingKey, KingdomBindingKind kind, string zoneId)
		{
			KingdomBindingTable table;
			KingdomBinding binding;
			if (!TryTable(System, out table) || !table.TryGet(bindingKey, kind, out binding))
			{
				return KingdomBodyPresence.None;
			}
			return PresenceOf(binding, zoneId);
		}

		/// <summary>The ground a binding names, or null when there is no binding for the key. What a
		/// caller needs to tell "the body is on disk" from "the body was destroyed": both fail to
		/// resolve, and only one of them can be swept later.</summary>
		public static bool TryBoundZone(KingdomSystem System, int bindingKey, KingdomBindingKind kind, out string zoneId)
		{
			zoneId = null;
			KingdomBindingTable table;
			KingdomBinding binding;
			if (!TryTable(System, out table) || !table.TryGet(bindingKey, kind, out binding))
			{
				return false;
			}
			zoneId = binding.ZoneId;
			return !string.IsNullOrEmpty(zoneId);
		}

		/// <summary>
		/// Resolves the exact body named by one resident binding, optionally thawing its recorded
		/// zone. Never mints or substitutes. Resolution follows the engine object-id index and then
		/// re-proves binding kind/key, ground, citizenship, and resident-row authority; it never walks
		/// every object in a cached remote zone.
		/// This is the preflight for accession: a caller may move player identity only after this
		/// method has proved the row, binding, object id, zone and body are the same fact.
		/// </summary>
		internal static bool TryResolveBoundBody(KingdomSystem System, int residentId, bool LoadZone,
			out GameObject Body, out string ZoneId)
		{
			Body = null;
			ZoneId = null;
			if (System == null || residentId == 0 || The.ZoneManager == null)
			{
				return false;
			}
			KingdomBindingTable table;
			KingdomBinding binding;
			if (!TryTable(System, out table)
				|| !table.TryGet(residentId, KingdomBindingKind.Resident, out binding)
				|| string.IsNullOrEmpty(binding.ZoneId) || string.IsNullOrEmpty(binding.ObjectId))
			{
				return false;
			}
			Zone zone = null;
			if (The.ZoneManager.CachedZones != null)
			{
				The.ZoneManager.CachedZones.TryGetValue(binding.ZoneId, out zone);
			}
			if (zone == null && LoadZone)
			{
				try
				{
					zone = The.ZoneManager.GetZone(binding.ZoneId);
				}
				catch (Exception ex)
				{
					KingdomLog.Log("binding: resident resolution could not thaw " + binding.ZoneId + " ("
						+ ex.GetType().Name + ")");
					return false;
				}
			}
			if (zone == null)
			{
				return false;
			}

			GameObject exact = null;
			KingdomSurvey active = KingdomSurvey.ActiveFor(zone);
			if (active != null)
			{
				exact = active.FindBoundBody(binding.ObjectId, KingdomBindingKind.Resident);
				KingdomBodyWitness witness;
				if (!active.TryWitnessResident(residentId, out witness)
					|| witness != KingdomBodyWitness.Present) return false;
			}
			else
			{
				exact = FindExactBindingObject(binding);
			}
			if (!GameObject.Validate(exact) || !exact.IsAlive
				|| exact.IsPlayer() || exact.IsPlayerLed()
				|| !string.Equals(exact.IDIfAssigned, binding.ObjectId, StringComparison.Ordinal)
				|| exact.GetIntProperty(ResidentIdProperty) != residentId
				|| !KingdomCitizenship.BelongsTo(System, exact)
				|| exact.GetIntProperty("KingdomBorn") != 1
				|| exact.CurrentCell == null || !ReferenceEquals(exact.CurrentZone, zone)
				|| !string.Equals(exact.CurrentZone?.ZoneID, binding.ZoneId, StringComparison.Ordinal))
			{
				return false;
			}

			KingdomCityBook book;
			int locatedId;
			KingdomCityState state;
			KingdomResidentRow row;
			int rowIndex;
			KingdomCityFault fault;
			if (!TryLocate(System, exact, out book, out locatedId) || locatedId != residentId
				|| book == null || !book.TryRead(out state, out fault)
				|| !state.TryResidentIndex(residentId, out rowIndex)
				|| !state.TryResident(rowIndex, out row)
				|| (row.Standing != KingdomResidentStanding.Resident
					&& row.Standing != KingdomResidentStanding.Expedition)
				|| (!string.IsNullOrEmpty(row.BoundZoneId)
					&& !string.Equals(row.BoundZoneId, binding.ZoneId, StringComparison.Ordinal)))
			{
				return false;
			}
			Body = exact;
			ZoneId = binding.ZoneId;
			return true;
		}

		/// <summary>Exact engine-index lookup for one binding. This deliberately proves only object
		/// identity and binding kind/key. Transaction owners may additionally accept a narrowly
		/// specified in-flight zone before their copy-on-write binding publish catches up.</summary>
		internal static GameObject FindExactBindingObject(KingdomBinding Binding)
		{
			if (string.IsNullOrEmpty(Binding.ObjectId) || Binding.BindingKey <= 0) return null;
			GameObject exact = GameObject.FindByID(Binding.ObjectId);
			if (!GameObject.Validate(exact)
				|| !string.Equals(exact.IDIfAssigned, Binding.ObjectId, StringComparison.Ordinal))
				return null;
			if (Binding.Kind == KingdomBindingKind.Resident)
				return IdOf(exact) == Binding.BindingKey ? exact : null;
			if (Binding.Kind == KingdomBindingKind.Transient)
				return exact.GetIntProperty(JobIdProperty) == Binding.BindingKey ? exact : null;
			return null;
		}

		/// <summary>
		/// Binds this body to this ground, or moves an existing binding onto it. One call, because
		/// the caller's question is always "this key is here now" and splitting it would make
		/// "bind" and "rebind" two chances to get the same fact wrong.
		/// </summary>
		/// <returns>True when the registry was written.</returns>
	}
}
