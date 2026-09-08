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
		internal int Count = 1;

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

		private int Insertions;

		public int RoomNow()
		{
			int room = Capacity - Held;
			return (room > 0) ? room : 0;
		}

		public int MaterialHeldNow()
		{
			return MaterialHeld;
		}

		public object Create()
		{
			FakeBundle bundle = new FakeBundle();
			Created.Add(bundle);
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

		public int CountOf(object Bundle)
		{
			return ((FakeBundle)Bundle).Count;
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
