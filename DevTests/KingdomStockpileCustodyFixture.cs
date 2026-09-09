#if TAF_TESTS
using System;
using System.Collections.Generic;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>A bundle a fill made. <c>Holder</c> is null while nobody has it; "store" once it
	/// is standing in the destination; anything else is a foreign custody the delivery may not
	/// resolve by force. The engine keeps equipment, inventory and implantation apart, and so does
	/// the real seam; here any non-null holder is somebody else.</summary>
	internal sealed class FakeBundle
	{
		/// <summary>The raw field, which is what a stamp writes and what a raw read returns.
		/// A nonpositive value is what arms the native count repair.</summary>
		internal int RawCount = 1;

		internal int Count
		{
			get { return RawCount; }
			set { RawCount = value; }
		}

		internal bool Alive = true;

		internal string Holder;
	}

	/// <summary>
	/// A destination whose callbacks run exactly where the engine's would: inside creation,
	/// inside the count stamp, inside the insertion, and inside the destruction &mdash; which is
	/// vetoable, and may move the body before it refuses.
	/// <para>
	/// Occupancy and material are held apart on purpose. <c>Held</c> is what the store is full of,
	/// which is what room is measured against; <c>MaterialHeld</c> is what it holds OF THE
	/// MATERIAL BEING DELIVERED, which is the only evidence that may pay for a bundle that
	/// stopped existing.
	/// </para>
	/// </summary>
	internal sealed class FakeStore : IKingdomDepositHost
	{
		internal int Capacity = 8;

		internal int Held;

		internal int MaterialHeld;

		internal bool Stackable = true;

		/// <summary>Whether the destination is still dedicated settlement stock. A handler that
		/// clears the dedication leaves the bundle physically in the container, so membership
		/// alone must not read as a landing.</summary>
		internal bool Dedicated = true;

		internal bool Announced;

		/// <summary>False models a destination that can no longer carry the saying &mdash; a store
		/// a handler destroyed mid-fill. The host must still not say the same thing twice.
		/// </summary>
		internal bool CanRemember = true;

		private bool Spoken;

		internal readonly List<FakeBundle> Created = new List<FakeBundle>();

		internal readonly List<FakeBundle> Discarded = new List<FakeBundle>();

		internal int Sayings;

		/// <summary>Runs inside creation, before anything is proved.</summary>
		internal Action<FakeStore, FakeBundle> OnCreate;

		/// <summary>Stands in for <c>StackCountChangedEvent</c>: it runs inside the stamp.
		/// </summary>
		internal Action<FakeStore, FakeBundle> OnStamp;

		/// <summary>Stands in for the destination's own handlers. Returns whatever the insertion
		/// accepted, which is null when it accepted nothing.</summary>
		internal Func<FakeStore, FakeBundle, object> OnInsert;

		/// <summary>Stands in for a <c>BeforeDestroyObjectEvent</c> handler. Returning false is a
		/// veto, and the handler may have moved the body first.</summary>
		internal Func<FakeStore, FakeBundle, bool> OnDiscard;

		/// <summary>Insertions before the one that throws. Negative never throws.</summary>
		internal int ThrowOnInsertAfter = -1;

		/// <summary>True makes the saying itself throw, as a display handler can: naming a store
		/// reaches <c>GetDisplayNameEvent</c>.</summary>
		internal bool ThrowOnAnnounce;

		/// <summary>
		/// READS are callbacks too, and these model that. Native <c>GameObject.Count</c> reaches
		/// <c>Stacker.Number</c>, which repairs a nonpositive count by assigning one and
		/// dispatching <c>StackCountChangedEvent</c>; a room or material census walks objects and
		/// asks every one of them the same question. Each hook runs inside the corresponding read.
		/// </summary>
		internal Action<FakeStore, FakeBundle> OnCountRead;

		/// <summary>Runs inside a room census. <see cref="RoomReads"/> tells one from the next.
		/// </summary>
		internal Action<FakeStore, FakeBundle> OnRoomRead;

		/// <summary>Runs inside a material census.</summary>
		internal Action<FakeStore, FakeBundle> OnMaterialRead;

		internal int RoomReads;

		internal int MaterialReads;

		internal int CountReads;

		internal int RawRoomReads;

		internal int RawMaterialReads;

		internal int RawCountReads;

		/// <summary>What is already standing in the store, so a census is a walk over rows rather
		/// than a scalar and a row's own reading can be made to move another row.</summary>
		internal readonly List<FakeBundle> Residents = new List<FakeBundle>();

		/// <summary>Runs inside an EVENTFUL census, once per counted row.</summary>
		internal Action<FakeStore, FakeBundle> OnRowRead;

		/// <summary>Insertions actually attempted, so a test can prove a foreign-held body was
		/// never handed to the destination at all.</summary>
		internal int Insertions;

		/// <summary>The bundle the fill is working on, so a read hook can reach it.</summary>
		private FakeBundle Working;

		/// <summary>The ORDINARY reading, which dispatches. The engine may take it as advice, so
		/// its hook is allowed to change the pending bundle's count, fill the store, or carry the
		/// bundle off &mdash; and the raw proofs further down must still catch all three.</summary>
		public int RoomNow()
		{
			RoomReads++;
			// A census totals what it walked, and a handler it fired during the walk changes the
			// store AFTERWARDS. So the number it returns can already be out of date by the time
			// the caller has it, which is the whole reason a raw re-observation follows.
			int room = Capacity - Held;
			if (OnRoomRead != null && Working != null) OnRoomRead(this, Working);
			return (room > 0) ? room : 0;
		}

		/// <summary>The RAW reading. It runs no hook and changes nothing.</summary>
		public int RawRoomNow()
		{
			RawRoomReads++;
			int room = Capacity - Held;
			return (room > 0) ? room : 0;
		}

		/// <summary>
		/// The RAW census: one pass over the residents, each counted off its own raw field, with
		/// no hook anywhere inside it. <see cref="EventfulCensus"/> is the reading this replaced,
		/// and a test asserts the engine never takes it: a census that dispatches can move an
		/// earlier row after its count has already been added to the total.
		/// </summary>
		public int RawMaterialHeldNow()
		{
			RawMaterialReads++;
			int held = 0;
			for (int i = 0; i < Residents.Count; i++)
			{
				FakeBundle resident = Residents[i];
				if (resident.Alive && resident.Holder == "store") held += resident.RawCount;
			}
			return held + MaterialHeld;
		}

		/// <summary>The census as it would be if every row were asked its count the ordinary way:
		/// row N's read may move row N-1 out of the store AFTER its units have been counted. The
		/// engine must never reach this.</summary>
		internal int EventfulCensus()
		{
			MaterialReads++;
			int held = 0;
			for (int i = 0; i < Residents.Count; i++)
			{
				FakeBundle resident = Residents[i];
				if (!resident.Alive || resident.Holder != "store") continue;
				held += resident.RawCount;
				if (OnRowRead != null) OnRowRead(this, resident);
			}
			if (OnMaterialRead != null && Working != null) OnMaterialRead(this, Working);
			return held + MaterialHeld;
		}

		public object Create()
		{
			FakeBundle bundle = new FakeBundle();
			Created.Add(bundle);
			Working = bundle;
			if (OnCreate != null) OnCreate(this, bundle);
			return bundle;
		}

		public bool Stacks(object Bundle)
		{
			return Stackable;
		}

		public void Stamp(object Bundle, int Count)
		{
			FakeBundle bundle = (FakeBundle)Bundle;
			bundle.Count = Count;
			if (OnStamp != null) OnStamp(this, bundle);
		}

		/// <summary>The RAW count: the field, no repair, no hook.</summary>
		public int RawCountOf(object Bundle)
		{
			RawCountReads++;
			FakeBundle bundle = (FakeBundle)Bundle;
			return (bundle.RawCount > 0) ? bundle.RawCount : 1;
		}

		/// <summary>The count as the engine ordinarily reports it: a nonpositive raw count is
		/// REPAIRED to one and the hook runs for it. The engine must never reach this.</summary>
		internal int EventfulCountOf(FakeBundle Bundle)
		{
			CountReads++;
			if (Bundle.RawCount <= 0) Bundle.RawCount = 1;
			if (OnCountRead != null) OnCountRead(this, Bundle);
			return Bundle.RawCount;
		}

		public bool Alive(object Bundle)
		{
			return ((FakeBundle)Bundle).Alive;
		}

		public bool HeldByNobody(object Bundle)
		{
			FakeBundle bundle = (FakeBundle)Bundle;
			return bundle.Alive && bundle.Holder == null;
		}

		public bool Discard(object Bundle)
		{
			FakeBundle bundle = (FakeBundle)Bundle;
			if (OnDiscard != null)
			{
				bool gone = OnDiscard(this, bundle);
				if (gone) Discarded.Add(bundle);
				return gone && !bundle.Alive;
			}
			if (!bundle.Alive) return false;
			bundle.Alive = false;
			Discarded.Add(bundle);
			return true;
		}

		public object Insert(object Bundle)
		{
			FakeBundle bundle = (FakeBundle)Bundle;
			if (ThrowOnInsertAfter >= 0 && Insertions >= ThrowOnInsertAfter)
			{
				Insertions++;
				throw new InvalidOperationException("a handler threw inside the insertion");
			}
			Insertions++;
			if (OnInsert != null) return OnInsert(this, bundle);
			bundle.Holder = "store";
			Held += bundle.Count;
			MaterialHeld += bundle.Count;
			return bundle;
		}

		public bool Landed(object Bundle, object Accepted, int Batch)
		{
			FakeBundle bundle = (FakeBundle)Bundle;
			return ReferenceEquals(Accepted, Bundle) && bundle.Alive
				&& bundle.Count == Batch && bundle.Holder == "store" && Dedicated;
		}

		public bool CustodyAnnounced
		{
			get { return CanRemember ? Announced : Spoken; }
			set
			{
				Spoken = value;
				if (CanRemember) Announced = value;
			}
		}

		public void AnnounceUncertainCustody()
		{
			Sayings++;
			if (ThrowOnAnnounce)
			{
				throw new InvalidOperationException("a display handler threw while naming a store");
			}
		}

		/// <summary>Units physically standing anywhere at all, so a duplication shows up as a
		/// number the delivery never earned.</summary>
		internal int UnitsInTheWorld()
		{
			int units = 0;
			for (int i = 0; i < Created.Count; i++)
			{
				if (Created[i].Alive) units += Created[i].Count;
			}
			return units;
		}

		/// <summary>A handler that carries the bundle into somebody else's hands.</summary>
		internal static void CarryOff(FakeStore Store, FakeBundle Bundle)
		{
			Bundle.Holder = "a passing merchant";
		}
	}
}
#endif
