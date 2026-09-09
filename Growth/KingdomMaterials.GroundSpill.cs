using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterials
	{
		// --- The ground, as a destination like any other --------------------------------------
		//
		// Overflow used to be its own little loop: stamp a count, call AddObject, and count the
		// batch whether or not anything arrived. Cell.AddObject returns the object it was handed
		// even when Physics.EnterCell REFUSED it, so the call proves nothing; and the stamp there
		// runs StackCountChangedEvent exactly as it does in a chest. So the ground runs the same
		// law, through the same seam, and is paid the same way: on proof.

		/// <summary>
		/// One cell, one material. Ground has no declared size and no designation to lose, so the
		/// only bound on a spill is what is left to deliver; everything else is the ordinary
		/// deposit law.
		/// </summary>
		internal sealed class GroundSpillHost : IKingdomDepositHost
		{
			private readonly Zone Z;

			private readonly Cell Ground;

			private readonly string Blueprint;

			/// <summary>What is left to deliver. The ground's "room" is exactly this: it cannot
			/// fill up, and a batch may never exceed the delivery it belongs to.</summary>
			private readonly int Bound;

			/// <summary>The saying. A cell carries no property the settlement reads back, so the
			/// ground remembers its uncertainty for the length of one spill and no longer.
			/// </summary>
			private bool Spoken;

			internal GroundSpillHost(Zone Z, Cell Ground, string Blueprint, int Bound)
			{
				this.Z = Z;
				this.Ground = Ground;
				this.Blueprint = Blueprint;
				this.Bound = (Bound > 0) ? Bound : 0;
			}

			public bool CustodyAnnounced
			{
				get { return Spoken; }
				set { Spoken = value; }
			}

			/// <summary>Open ground takes whatever is still owed and nothing more. A cell that is
			/// gone, or that belongs to another zone than the one this delivery is running in, has
			/// stopped being a destination and takes nothing.</summary>
			public int RoomNow()
			{
				return (Ground != null && Ground.ParentZone != null
					&& (Z == null || ReferenceEquals(Ground.ParentZone, Z))) ? Bound : 0;
			}

			public int MaterialHeldNow()
			{
				return GroundMaterialHeldNow(Ground, Blueprint);
			}

			public object Create()
			{
				return GameObject.Create(Blueprint);
			}

			public bool Stacks(object Bundle)
			{
				GameObject item = Bundle as GameObject;
				return item != null && item.HasPart("Stacker");
			}

			public void Stamp(object Bundle, int Count)
			{
				GameObject item = Bundle as GameObject;
				if (item != null)
				{
					item.Count = Count;
				}
			}

			public int CountOf(object Bundle)
			{
				GameObject item = Bundle as GameObject;
				return (item != null) ? item.Count : 0;
			}

			public bool Alive(object Bundle)
			{
				return GameObject.Validate(Bundle as GameObject);
			}

			/// <summary>Nobody is holding it: no cell, and no holder of any of the three kinds the
			/// engine keeps separately, because equipping and implanting both clear the inventory
			/// and the cell.</summary>
			public bool HeldByNobody(object Bundle)
			{
				GameObject item = Bundle as GameObject;
				return GameObject.Validate(item) && item.Holder == null
					&& item.CurrentCell == null;
			}

			/// <summary>Destruction is vetoable; only a bundle provably gone counts as withdrawn.
			/// </summary>
			public bool Discard(object Bundle)
			{
				GameObject item = Bundle as GameObject;
				if (!GameObject.Validate(item))
				{
					return false;
				}
				bool gone = item.Obliterate(null, Silent: true);
				return gone && !GameObject.Validate(item);
			}

			/// <summary>Laying a bundle down runs the cell's handlers, and a stack already lying
			/// there absorbs it. That is a real landing and is paid for out of what the ground
			/// gained, not out of this call, which hands the object back either way.</summary>
			public object Insert(object Bundle)
			{
				GameObject item = Bundle as GameObject;
				GameObject accepted = null;
				try { accepted = Ground.AddObject(item); }
				catch
				{
					KingdomSurvey.ObserveAddResultInActive(Z, item, null);
					throw;
				}
				KingdomSurvey.ObserveAddResultInActive(Z, item, accepted);
				return accepted;
			}

			/// <summary>Proof that the exact bundle is lying in the exact cell with the count it
			/// was stamped with. Cell.AddObject returns what it was handed even when the cell
			/// refused entry, and it appends to the REQUESTED cell's list even when the entry
			/// callbacks moved the body somewhere else first, so neither the returned reference
			/// nor list membership proves anything on its own: the body's own cell is read.
			/// </summary>
			public bool Landed(object Bundle, object Accepted, int Batch)
			{
				GameObject item = Bundle as GameObject;
				return ReferenceEquals(Accepted, item) && GameObject.Validate(item)
					&& item.Blueprint == Blueprint && item.Count == Batch
					&& Ground != null && ReferenceEquals(item.CurrentCell, Ground)
					&& item.Holder == null && Ground.Objects.Contains(item);
			}

			/// <summary>The log line is written FIRST and off raw strings only: the founder's
			/// message and the hold reading both reach handlers, and a diagnostic that throws
			/// must not be the reason a delivery loses its accounting.</summary>
			public void AnnounceUncertainCustody()
			{
				KingdomLog.Log("materials: spill custody unproved, blueprint=" + Blueprint);
				MessageQueue.AddPlayerMessage("{{K|A bundle set down for want of a stockpile"
					+ " ended up somewhere the keepers cannot account for; the rest of the load"
					+ " is held rather than made a second time.}}");
			}
		}
	}
}
