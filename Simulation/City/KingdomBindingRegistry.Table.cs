using System;
using System.Collections.Generic;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The registry as the rules layer works on it: frozen, total, copy-on-write.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.8, and the &sect;1.3 doctrine applied to it &mdash; the
	/// same pairing <see cref="KingdomCityState"/> has with <see cref="KingdomCityBook"/>, for the
	/// same reason: a named-field reader must assign fields and the rules layer must not.
	/// </para>
	/// <para>
	/// <b>Realm-scope, not per city.</b> A bound body can be in the other city's zone or walked off
	/// the map entirely, so a registry that lived on a settlement would answer for half the
	/// question and lose the other half on every seat swap.
	/// </para>
	/// </summary>
	internal sealed class KingdomBindingTable
	{
		/// <summary>LIVING-CITY-ARCHITECTURE &sect;3.8: sixty residents times two cities.</summary>
		internal const int MaxResidentBindings = KingdomCityState.MaxResidents * KingdomCityMemoryRules.CitiesPerRealm;

		/// <summary>LIVING-CITY-ARCHITECTURE &sect;3.8: open jobs, realm-wide. A closed job is
		/// evicted at once, so this is a cap on what is OPEN and never on what has ever run.</summary>
		internal const int MaxTransientBindings = KingdomCityMemoryRules.MaxOpenJobs;

		private readonly KingdomBinding[] bindings;

		private KingdomBindingTable(KingdomBinding[] bindings)
		{
			this.bindings = bindings;
		}

		/// <summary>An empty registry. What a realm carries before it has minted anything.</summary>
		internal static KingdomBindingTable Empty
		{
			get { return new KingdomBindingTable(new KingdomBinding[0]); }
		}

		/// <summary>
		/// Builds a registry, or refuses and publishes nothing. Refuses two bindings on one key
		/// (invariant I3, at the door rather than at the reader), refuses over either cap, and
		/// refuses a binding with no key.
		/// </summary>
		internal static bool TryCreate(KingdomBinding[] rows, out KingdomBindingTable table, out KingdomCityFault fault)
		{
			table = null;
			int count = (rows == null) ? 0 : rows.Length;
			int residents = 0;
			int transients = 0;
			for (int i = 0; i < count; i++)
			{
				if (rows[i].BindingKey == 0)
				{
					fault = KingdomCityFault.UnknownBinding;
					return false;
				}
				if (rows[i].Kind == KingdomBindingKind.Resident)
				{
					residents++;
				}
				else
				{
					transients++;
				}
				for (int j = i + 1; j < count; j++)
				{
					if (rows[i].BindingKey == rows[j].BindingKey && rows[i].Kind == rows[j].Kind)
					{
						fault = KingdomCityFault.DuplicateBinding;
						return false;
					}
				}
			}
			if (residents > MaxResidentBindings || transients > MaxTransientBindings)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			KingdomBinding[] copy = new KingdomBinding[count];
			if (count > 0)
			{
				Array.Copy(rows, copy, count);
			}
			table = new KingdomBindingTable(copy);
			fault = KingdomCityFault.None;
			return true;
		}

		internal int Count
		{
			get { return bindings.Length; }
		}

		internal bool TryAt(int index, out KingdomBinding binding)
		{
			if (index < 0 || index >= bindings.Length)
			{
				binding = default(KingdomBinding);
				return false;
			}
			binding = bindings[index];
			return true;
		}

		/// <summary>
		/// The binding on this key, or false. <b>Absence IS proof of closure</b> &mdash; there is no
		/// second list to keep in step with this one, which is why nothing here ever returns a
		/// closed binding.
		/// </summary>
		internal bool TryGet(int bindingKey, KingdomBindingKind kind, out KingdomBinding binding)
		{
			for (int i = 0; i < bindings.Length; i++)
			{
				if (bindings[i].BindingKey == bindingKey && bindings[i].Kind == kind)
				{
					binding = bindings[i];
					return true;
				}
			}
			binding = default(KingdomBinding);
			return false;
		}

		/// <summary>Whether this key is bound at all, of any kind. The question the stale sweep
		/// asks.</summary>
		internal bool Holds(int bindingKey, KingdomBindingKind kind)
		{
			KingdomBinding binding;
			return TryGet(bindingKey, kind, out binding);
		}

		internal int CountOf(KingdomBindingKind kind)
		{
			int count = 0;
			for (int i = 0; i < bindings.Length; i++)
			{
				if (bindings[i].Kind == kind)
				{
					count++;
				}
			}
			return count;
		}

		/// <summary>
		/// Writes a binding for a key nothing holds. Refuses a key already bound rather than
		/// overwriting it: an overwrite is precisely the moment the old body stops being accounted
		/// for and starts being a duplicate. To move a bound body, use <see cref="TryRebind"/>.
		/// </summary>
		internal bool TryBind(int bindingKey, KingdomBindingKind kind, string zoneId, string objectId, long mintedTick, out KingdomBindingTable next, out KingdomCityFault fault)
		{
			next = null;
			if (bindingKey == 0)
			{
				fault = KingdomCityFault.UnknownBinding;
				return false;
			}
			if (mintedTick < 0L)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			if (Holds(bindingKey, kind))
			{
				fault = KingdomCityFault.DuplicateBinding;
				return false;
			}
			int cap = (kind == KingdomBindingKind.Resident) ? MaxResidentBindings : MaxTransientBindings;
			if (CountOf(kind) >= cap)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			KingdomBinding[] grown = new KingdomBinding[bindings.Length + 1];
			Array.Copy(bindings, grown, bindings.Length);
			grown[bindings.Length] = new KingdomBinding(bindingKey, kind, zoneId, objectId, mintedTick);
			next = new KingdomBindingTable(grown);
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Moves an existing binding to other ground and, where the body was re-minted there, to
		/// another object. Refuses a key nothing holds: rebinding what was never bound would mint a
		/// binding through the door that exists to stop exactly that.
		/// </summary>
		internal bool TryRebind(int bindingKey, KingdomBindingKind kind, string zoneId, string objectId, out KingdomBindingTable next, out KingdomCityFault fault)
		{
			next = null;
			for (int i = 0; i < bindings.Length; i++)
			{
				if (bindings[i].BindingKey != bindingKey || bindings[i].Kind != kind)
				{
					continue;
				}
				KingdomBinding[] moved = new KingdomBinding[bindings.Length];
				Array.Copy(bindings, moved, bindings.Length);
				moved[i] = bindings[i].WithPlace(zoneId, objectId);
				next = new KingdomBindingTable(moved);
				fault = KingdomCityFault.None;
				return true;
			}
			fault = KingdomCityFault.UnknownBinding;
			return false;
		}

		/// <summary>
		/// Evicts a binding, naming why. The cause is required and
		/// <see cref="KingdomUnbindCause.None"/> is refused: absence from the registry is the only
		/// record that a binding closed, so a caller that will not say why is asking for a body to
		/// vanish unaccounted for.
		/// </summary>
		internal bool TryUnbind(int bindingKey, KingdomBindingKind kind, KingdomUnbindCause cause, out KingdomBindingTable next, out KingdomBinding evicted, out KingdomCityFault fault)
		{
			next = null;
			evicted = default(KingdomBinding);
			if (cause == KingdomUnbindCause.None)
			{
				fault = KingdomCityFault.CauseRequired;
				return false;
			}
			for (int i = 0; i < bindings.Length; i++)
			{
				if (bindings[i].BindingKey != bindingKey || bindings[i].Kind != kind)
				{
					continue;
				}
				evicted = bindings[i];
				KingdomBinding[] shrunk = new KingdomBinding[bindings.Length - 1];
				Array.Copy(bindings, 0, shrunk, 0, i);
				Array.Copy(bindings, i + 1, shrunk, i, bindings.Length - i - 1);
				next = new KingdomBindingTable(shrunk);
				fault = KingdomCityFault.None;
				return true;
			}
			fault = KingdomCityFault.UnknownBinding;
			return false;
		}

		/// <summary>
		/// Invariant I3, asserted rather than assumed: <i>no <c>BindingKey</c> ever resolves to two
		/// living bodies, in any zone, at any time.</i> The registry holds one row per key by
		/// construction, so what this actually checks is that construction held &mdash; and it is
		/// what <c>kingdom:selftest</c> calls.
		/// </summary>
		internal bool TryAudit(out KingdomCityFault fault)
		{
			for (int i = 0; i < bindings.Length; i++)
			{
				if (bindings[i].BindingKey == 0)
				{
					fault = KingdomCityFault.UnknownBinding;
					return false;
				}
				for (int j = i + 1; j < bindings.Length; j++)
				{
					if (bindings[i].BindingKey == bindings[j].BindingKey && bindings[i].Kind == bindings[j].Kind)
					{
						fault = KingdomCityFault.DuplicateBinding;
						return false;
					}
				}
			}
			if (CountOf(KingdomBindingKind.Resident) > MaxResidentBindings
				|| CountOf(KingdomBindingKind.Transient) > MaxTransientBindings)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			fault = KingdomCityFault.None;
			return true;
		}
	}

}
