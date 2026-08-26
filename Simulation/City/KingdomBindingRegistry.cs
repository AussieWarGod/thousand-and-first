using System;
using System.Collections.Generic;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What a binding key names. LIVING-CITY-ARCHITECTURE &sect;3.8: one registry answers for both,
	/// because both are things this mod <b>mints</b>, and anything we mint we can duplicate.
	/// </summary>
	public enum KingdomBindingKind : byte
	{
		/// <summary>The key is a <c>ResidentId</c>. A person, and &sect;8.3's law applies to them:
		/// materialisation may mint or move a body and may never remove one.</summary>
		Resident = 0,

		/// <summary>The key is a <c>JobId</c> &mdash; a delivery, a mend, a message. A transient is
		/// a RENDERING of a job, and jobs close.</summary>
		Transient = 1
	}

	/// <summary>
	/// Why a binding was evicted. LIVING-CITY-ARCHITECTURE &sect;3.8 keeps no second "closed" list,
	/// so absence from the registry is proof of closure &mdash; which makes the cause something the
	/// caller must state at the moment it evicts, and a reason that arrives later is a reason
	/// nobody wrote down.
	/// </summary>
	public enum KingdomUnbindCause : byte
	{
		/// <summary>Not a cause. Refused: an unbinding without a reason is how a settler
		/// disappears and nothing in the game says why.</summary>
		None = 0,

		/// <summary>The body was killed. The row reads <c>Dead</c> and the city holds a
		/// funeral.</summary>
		Death = 1,

		/// <summary>The person left the roll &mdash; emigration, exile, secession.</summary>
		Departure = 2,

		/// <summary>The body is elsewhere with the founder. The row reads <c>Abroad</c>: still on
		/// the roll, contributing no labour, and honestly reported as such.</summary>
		Abroad = 3,

		/// <summary>The job reached its completion tick and the model closed it. Transients
		/// only.</summary>
		JobClosed = 4,

		/// <summary>The realm let the whole city go. Every binding it held is evicted at
		/// once.</summary>
		Dissolved = 5,

		/// <summary>The resident took the charter and became the player. Their real body remains,
		/// but it is no longer a city-model view and must never be re-minted from the old row.</summary>
		Accession = 6,

		/// <summary>A transient reached an exact zone boundary or shaft endpoint. Its one live
		/// rendering is removed while the still-open job remains authority for the next zone to
		/// render. Never valid for residents and never closes the job.</summary>
		ZoneHandoff = 7
	}

	/// <summary>
	/// What the engine edge found when it resolved an existing binding's object. The one thing the
	/// pure rule cannot know for itself, so it is handed in.
	/// </summary>
	public enum KingdomBodyPresence : byte
	{
		/// <summary>There is no binding for this key at all.</summary>
		None = 0,

		/// <summary>The bound object resolves live in the zone being asked about.</summary>
		Here = 1,

		/// <summary>The bound object resolves live in another zone that is currently
		/// resident &mdash; in RAM, reachable, movable.</summary>
		Elsewhere = 2,

		/// <summary>The bound object does not resolve: its zone is on disk. <b>The frozen body is
		/// invisible; its binding is not.</b></summary>
		Frozen = 3
	}

	/// <summary>What check-before-mint answers. LIVING-CITY-ARCHITECTURE &sect;3.8.</summary>
	public enum KingdomBindingVerdict : byte
	{
		/// <summary>Nothing is bound to this key. Mint a body, and write the binding in the SAME
		/// copy-on-write publish as the debt decrement.</summary>
		Mint = 0,

		/// <summary>A body is already here. Move it. Do not mint.</summary>
		Move = 1,

		/// <summary>A body is live in another resident zone. A resident moves across; a transient
		/// does not, because a porter is a rendering of one job and one job has one road.</summary>
		MoveAcross = 2,

		/// <summary>Refuse. The debt stays owed. <b>An unresolvable binding is a refusal to mint,
		/// never a licence to mint</b> &mdash; that single line is the whole anti-duplication
		/// argument, and it holds across suspend, freeze, save, reload and crash.</summary>
		Refuse = 3
	}

	/// <summary>What the stale-transient sweep says about one object found in a thawed zone.</summary>
	public enum KingdomSweepVerdict : byte
	{
		/// <summary>The object carries no job id. Not ours to judge, and never touched.</summary>
		NotTransient = 0,

		/// <summary>The object carries a job id whose binding is still open. It is the rendering
		/// of a job that has not finished; leave it alone.</summary>
		Bound = 1,

		/// <summary>The object carries a job id with no open binding. The model closed the job
		/// while the ground was on disk, and the goods it is holding were already credited at the
		/// dated tick. This is the one instant they could exist twice.</summary>
		Stale = 2
	}

	/// <summary>
	/// One binding: which key, of which kind, in which ground, on which object, from when.
	/// <para>
	/// Twenty-nine declared bytes against the thirty-two LIVING-CITY-ARCHITECTURE &sect;0.0(c)
	/// budgets. The object reference is the engine's own persistent object <c>ID</c> string and not
	/// a live reference, which is not a compromise but the point: a live reference to a body in a
	/// frozen zone is exactly the thing that cannot survive the case &sect;3.8 was written for.
	/// </para>
	/// </summary>
	internal readonly struct KingdomBinding
	{
		internal readonly int BindingKey;

		internal readonly KingdomBindingKind Kind;

		internal readonly string ZoneId;

		internal readonly string ObjectId;

		internal readonly long MintedTick;

		internal KingdomBinding(int bindingKey, KingdomBindingKind kind, string zoneId, string objectId, long mintedTick)
		{
			BindingKey = bindingKey;
			Kind = kind;
			ZoneId = zoneId;
			ObjectId = objectId;
			MintedTick = mintedTick;
		}

		/// <summary>This binding in other ground, on whatever object stands there now. The minted
		/// tick does not move: a body that walked across a zone line is the same body, and
		/// redating it would lose the one fact the registry is for.</summary>
		internal KingdomBinding WithPlace(string zoneId, string objectId)
		{
			return new KingdomBinding(BindingKey, Kind, zoneId, objectId, MintedTick);
		}
	}

	/// <summary>
	/// The pure half of LIVING-CITY-ARCHITECTURE &sect;3.8: what to do about a key, given what the
	/// registry holds for it and what the ground says about the body it names.
	/// <para>
	/// Total over every representable input, engine-free, and the only place the four outcomes are
	/// decided. The engine edge supplies the presence and obeys the verdict; nothing else is
	/// allowed to reason about duplication.
	/// </para>
	/// </summary>
	internal static class KingdomBindingRules
	{
		/// <summary>
		/// Check-before-mint, exactly as &sect;3.8 tabulates it.
		/// <list type="bullet">
		/// <item><description>hit, resolves live in THIS zone &rarr; MOVE it, do not mint;</description></item>
		/// <item><description>hit, resolves live in another RESIDENT zone &rarr; a resident moves
		/// across, a transient is refused;</description></item>
		/// <item><description>hit, does not resolve (its zone is on disk) &rarr; REFUSE THE MINT,
		/// the debt stays owed;</description></item>
		/// <item><description>miss &rarr; mint, and write the binding in the same publish.</description></item>
		/// </list>
		/// </summary>
		internal static KingdomBindingVerdict Judge(KingdomBindingKind kind, KingdomBodyPresence presence)
		{
			switch (presence)
			{
			case KingdomBodyPresence.Here:
				return KingdomBindingVerdict.Move;
			case KingdomBodyPresence.Elsewhere:
				return (kind == KingdomBindingKind.Resident)
					? KingdomBindingVerdict.MoveAcross
					: KingdomBindingVerdict.Refuse;
			case KingdomBodyPresence.Frozen:
				return KingdomBindingVerdict.Refuse;
			case KingdomBodyPresence.None:
				return KingdomBindingVerdict.Mint;
			default:
				// A presence this build has no word for is not a licence to mint. The default of a
				// duplication rule is always the side that cannot duplicate.
				return KingdomBindingVerdict.Refuse;
			}
		}

		/// <summary>Whether a verdict is one that puts a NEW body on the ground. The one question
		/// the reify budget asks, and the reason the four outcomes are not a bool.</summary>
		internal static bool Mints(KingdomBindingVerdict verdict)
		{
			return verdict == KingdomBindingVerdict.Mint;
		}

		/// <summary>
		/// The stale-transient sweep's verdict on one object found in a thawed zone.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.8 t3: any object carrying a <c>KingdomJobId</c> with no
		/// open binding is stale, because the model closed the job and evicted the binding while
		/// the ground was on disk, and what the body is carrying was already credited to the stores
		/// at the dated tick. <b>W2 ships the verdict; the despawn is W3.</b>
		/// </para>
		/// <para>
		/// A resident is never swept, and there is no argument about it here because there is no
		/// input for it: the sweep is keyed on a job id, and a person does not have one.
		/// </para>
		/// </summary>
		internal static KingdomSweepVerdict JudgeStale(int jobId, bool hasOpenBinding)
		{
			if (jobId == 0)
			{
				return KingdomSweepVerdict.NotTransient;
			}
			return hasOpenBinding ? KingdomSweepVerdict.Bound : KingdomSweepVerdict.Stale;
		}
	}

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

	/// <summary>
	/// The binding registry as the save file holds it: realm-scope, written as columns.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.8 puts this on <c>KingdomSystem</c> beside the realm seed
	/// &mdash; <b>not</b> on a settlement, because a bound body can be in another city's zone or
	/// walked off the map entirely. It is therefore realm state and must never appear among
	/// <c>KingdomSettlement</c>'s carried fields; <c>SettlementSeatTests</c> asserts that directly,
	/// and a seat swap consequently leaves it exactly as it found it.
	/// </para>
	/// <para>
	/// Columns rather than a list of row composites, for the reason the city book gives: &sect;0.0(c)
	/// budgets the model with no per-row object header.
	/// </para>
	/// </summary>
	[Serializable]
	public class KingdomBindingRegistry
#if !TAF_TESTS
		: IComposite
#endif
	{
		public List<int> Keys = new List<int>();

		public List<int> Kinds = new List<int>();

		public List<string> ZoneIds = new List<string>();

		/// <summary>The engine's own persistent object <c>ID</c>, as a string. Never a live
		/// reference: the case &sect;3.8 exists for is a body whose zone is on disk.</summary>
		public List<string> ObjectIds = new List<string>();

		public List<long> MintedTicks = new List<long>();

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomBindingRegistry));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomBindingRegistry));
			Normalize();
		}
#endif

		public int Count => Keys.Count;

		/// <summary>
		/// Repairs a registry read from a save written by an older build. Null columns become
		/// empty; <b>ragged columns are truncated to the shortest</b>, because a binding half of
		/// whose fields are missing is not a binding and a reader that trusted the longest column
		/// would invent one out of a default key.
		/// <para>
		/// A duplicate key is dropped rather than carried: a save that came back holding one key
		/// twice is a save that can put a settler in two places, and the first row wins because it
		/// is the one every earlier session was already answering with.
		/// </para>
		/// </summary>
		public void Normalize()
		{
			Keys = Repair(Keys);
			Kinds = Repair(Kinds);
			ZoneIds = Repair(ZoneIds);
			ObjectIds = Repair(ObjectIds);
			MintedTicks = Repair(MintedTicks);
			int count = Shortest(Keys.Count, Kinds.Count, ZoneIds.Count, ObjectIds.Count, MintedTicks.Count);
			Trim(Keys, count);
			Trim(Kinds, count);
			Trim(ZoneIds, count);
			Trim(ObjectIds, count);
			Trim(MintedTicks, count);
			for (int i = Keys.Count - 1; i >= 0; i--)
			{
				if (ZoneIds[i] == null)
				{
					ZoneIds[i] = "";
				}
				if (ObjectIds[i] == null)
				{
					ObjectIds[i] = "";
				}
				if (MintedTicks[i] < 0L)
				{
					MintedTicks[i] = 0L;
				}
				if (Keys[i] == 0 || Duplicated(i))
				{
					RemoveAt(i);
				}
			}
			DropOverCap(KingdomBindingKind.Resident, KingdomBindingTable.MaxResidentBindings);
			DropOverCap(KingdomBindingKind.Transient, KingdomBindingTable.MaxTransientBindings);
		}

		/// <summary>The registry as the frozen table the rules layer works on. Refuses and
		/// publishes nothing rather than handing back a half-built one.</summary>
		internal bool TryRead(out KingdomBindingTable table, out KingdomCityFault fault)
		{
			Normalize();
			KingdomBinding[] rows = new KingdomBinding[Keys.Count];
			for (int i = 0; i < rows.Length; i++)
			{
				rows[i] = new KingdomBinding(Keys[i], KindOf(Kinds[i]), ZoneIds[i], ObjectIds[i], MintedTicks[i]);
			}
			return KingdomBindingTable.TryCreate(rows, out table, out fault);
		}

		/// <summary>Writes one frozen table into the columns, in one call and after the rules have
		/// succeeded. The single publisher &sect;1.3 requires, applied to the registry &mdash; which
		/// is what makes "the mint and the binding are published together or not at all" a fact
		/// about the code and not an intention.</summary>
		internal bool TryPublish(KingdomBindingTable table, out KingdomCityFault fault)
		{
			if (table == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			Keys.Clear();
			Kinds.Clear();
			ZoneIds.Clear();
			ObjectIds.Clear();
			MintedTicks.Clear();
			for (int i = 0; i < table.Count; i++)
			{
				KingdomBinding binding;
				if (!table.TryAt(i, out binding))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				Keys.Add(binding.BindingKey);
				Kinds.Add((int)binding.Kind);
				ZoneIds.Add(binding.ZoneId ?? "");
				ObjectIds.Add(binding.ObjectId ?? "");
				MintedTicks.Add(binding.MintedTick);
			}
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>Anything this build has no word for reads as a transient, which is the side
		/// that can be swept and refused rather than the side that is a person.</summary>
		private static KingdomBindingKind KindOf(int stored)
		{
			return (stored == (int)KingdomBindingKind.Resident)
				? KingdomBindingKind.Resident
				: KingdomBindingKind.Transient;
		}

		private bool Duplicated(int index)
		{
			for (int i = 0; i < index; i++)
			{
				if (Keys[i] == Keys[index] && KindOf(Kinds[i]) == KindOf(Kinds[index]))
				{
					return true;
				}
			}
			return false;
		}

		private void DropOverCap(KingdomBindingKind kind, int cap)
		{
			int seen = 0;
			for (int i = 0; i < Keys.Count; i++)
			{
				if (KindOf(Kinds[i]) != kind)
				{
					continue;
				}
				seen++;
				if (seen > cap)
				{
					RemoveAt(i);
					i--;
				}
			}
		}

		private void RemoveAt(int index)
		{
			Keys.RemoveAt(index);
			Kinds.RemoveAt(index);
			ZoneIds.RemoveAt(index);
			ObjectIds.RemoveAt(index);
			MintedTicks.RemoveAt(index);
		}

		private static List<T> Repair<T>(List<T> column)
		{
			return column ?? new List<T>();
		}

		private static int Shortest(int a, int b, int c, int d, int e)
		{
			int shortest = a;
			if (b < shortest) { shortest = b; }
			if (c < shortest) { shortest = c; }
			if (d < shortest) { shortest = d; }
			if (e < shortest) { shortest = e; }
			return shortest;
		}

		private static void Trim<T>(List<T> column, int count)
		{
			if (column.Count > count)
			{
				column.RemoveRange(count, column.Count - count);
			}
		}
	}
}
