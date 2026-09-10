using System;

namespace ThousandAndFirst
{
	/// <summary>Whether a delivery can account for every bundle it made.</summary>
	internal enum KingdomDepositCustody
	{
		/// <summary>Every bundle is proved placed, or proved destroyed while this delivery still
		/// held it, or gone into the destination with the destination's own gain in this
		/// material accounting for the whole of it.</summary>
		Settled = 0,

		/// <summary>A bundle this delivery made is somewhere the delivery cannot prove. Nothing
		/// more is created, nothing more is counted, and nothing is destroyed.</summary>
		Unproved = 1,
	}

	/// <summary>What one destination's fill placed, and whether the delivery may go on at all.
	/// </summary>
	internal sealed class KingdomDepositOutcome
	{
		/// <summary>Units proved into the destination, never more.</summary>
		internal readonly int Placed;

		/// <summary>Whether every bundle this fill made is accounted for.</summary>
		internal readonly KingdomDepositCustody Custody;

		internal KingdomDepositOutcome(int Placed, KingdomDepositCustody Custody)
		{
			this.Placed = (Placed > 0) ? Placed : 0;
			this.Custody = Custody;
		}

		/// <summary>Whether the whole delivery must stop here.</summary>
		internal bool Refused
		{
			get { return Custody != KingdomDepositCustody.Settled; }
		}
	}

	/// <summary>
	/// Fills one destination up to the room it declared, and no further.
	/// <para>
	/// Nothing is remembered across a callback, because there are four of them and every one
	/// belongs to somebody else: creating the bundle, STAMPING ITS COUNT (which is
	/// <c>Stacker.StackCount</c>, and sends <c>StackCountChangedEvent</c>), inserting it, and
	/// destroying it (which is vetoable). So the destination, its room, and this delivery's
	/// custody of the bundle are all proved again after every one of them.
	/// </para>
	/// <para>
	/// Three laws sit above the counting. A delivery only ever MUTATES a bundle it can prove
	/// nobody else is holding, because an insertion takes a body out of whoever has it. It only
	/// ever DESTROYS such a bundle, and only counts the destruction once the body is provably
	/// gone, because destruction can be vetoed by a handler that moved the body first. And it
	/// never MINTS a replacement for a bundle whose fate it could not read, because the units
	/// would then exist twice. Anything unproved stops the delivery outright, and what could not
	/// be proved deposited is never credited.
	/// </para>
	/// <para>
	/// READING is not free either. Asking a bundle for its count reaches <c>Stacker.Number</c>,
	/// which REPAIRS a nonpositive count by assigning one and dispatching
	/// <c>StackCountChangedEvent</c>; and a destination's room and hold are counted by walking
	/// objects and asking each of them that same question. Proving custody after such a read is
	/// not enough, because the read may have changed the count or the room instead of the holder,
	/// and because the read's own write lands on a body somebody else may already have taken.
	/// </para>
	/// <para>
	/// So the order is RAW OBSERVE, DECIDE, MUTATE. An ordinary reading may be taken as ADVICE
	/// &mdash; it decides how much to ask for &mdash; but every FINAL proof, immediately before a
	/// mutation and before any credit, is a raw one: the field itself, no repair, no send. The
	/// last thing a delivery looks at can then never be the thing that moves the bundle.
	/// </para>
	/// </summary>
	internal static class KingdomDepositEngine
	{
		/// <summary>
		/// Fills one destination, up to its room and up to what is left to deliver.
		/// </summary>
		/// <param name="Host">The seam onto the destination and its bundles.</param>
		/// <param name="Room">Room the destination had when this delivery chose it.</param>
		/// <param name="Units">Units still to deliver.</param>
		/// <returns>What was proved placed, and whether the delivery may continue.</returns>
		internal static KingdomDepositOutcome Fill(IKingdomDepositHost Host, int Room, int Units)
		{
			if (Host == null || Units < 1 || Room < 1)
			{
				return new KingdomDepositOutcome(0, KingdomDepositCustody.Settled);
			}
			int placed = 0;
			try
			{
				return Run(Host, Room, Units, ref placed);
			}
			catch (Exception)
			{
				// A handler threw somewhere inside a callback this delivery cannot see into. The
				// bundle it was working on is in an unknown state, and unwinding past the caller
				// would throw away units this fill had already PROVED into the destination. So
				// the throw stops here: what was proved is returned and credited, the rest is
				// uncertain, and the caller reads that off the custody rather than off a stack
				// unwind. The host has already observed the fault at the seam where it happened.
				return Refuse(Host, placed);
			}
		}

		/// <summary>The fill itself. <paramref name="Placed"/> is carried by reference so a throw
		/// out of any callback still reports what was proved before it.</summary>
		private static KingdomDepositOutcome Run(IKingdomDepositHost Host, int Room, int Units,
			ref int Placed)
		{
			int remaining = Units;
			int room = Room;
			while (remaining > 0 && room > 0)
			{
				object bundle = Host.Create();
				if (bundle == null)
				{
					break;
				}
				// Creation has already run its callbacks, and the ordinary room census below runs
				// more of them. That census is ADVICE: it decides how much this delivery asks
				// for, and custody is proved after it, but nothing is destroyed or inserted on
				// the strength of it. The proofs that licence those come further down, raw.
				bool stacks = Host.Stacks(bundle);
				int live = Host.RoomNow();
				if (!Host.HeldByNobody(bundle))
				{
					return Refuse(Host, Placed);
				}
				int batch = KingdomRules.DepositBatch(remaining, room, live, stacks);
				if (batch < 1)
				{
					// Nothing is placed and nothing is counted; the units stay to deliver and go
					// to the next destination with room, or on the ground.
					if (!Host.Discard(bundle))
					{
						return Refuse(Host, Placed);
					}
					break;
				}
				if (batch > 1)
				{
					Host.Stamp(bundle, batch);
				}
				// The final proof of the batch, and every reading in it is RAW. EVERY batch is
				// proved, not only a stamped one: a creation handler can leave a stack of two
				// standing where the delivery only ever wanted one, and inserting it would put
				// two units into a destination paid for one. Nothing in this group can move the
				// bundle, change its count, or fill the destination, so no reading here can
				// invalidate another and the custody proof beside them stays true until the
				// mutation that follows it.
				bool roomKnown = Host.TryRawRoomNow(out int roomNow);
				int carried = Host.RawCountOf(bundle);
				if (!Host.HeldByNobody(bundle))
				{
					return Refuse(Host, Placed);
				}
				if (!roomKnown)
				{
					// The destination's hold is not a whole number, so there is no room to judge
					// this batch against. The parcel is real, stamped, and standing in nobody's
					// hands -- the proof just above says so -- and leaving it there would be
					// material minted and abandoned. It is put back exactly where a refused batch
					// is put back, and the delivery stops either way: destruction is vetoable, and
					// a veto handler may have moved the body before refusing.
					Host.Discard(bundle);
					return Refuse(Host, Placed);
				}
				if (!KingdomRules.DepositStampHolds(batch, carried, roomNow))
				{
					if (!Host.Discard(bundle))
					{
						return Refuse(Host, Placed);
					}
					break;
				}
				// What the destination holds OF THIS MATERIAL going in, so what it gained can be
				// told from what the insertion returned. Raw, because it is half of a CREDIT: a
				// census that repaired a resident's count as it walked could move that resident,
				// or another, and pay this delivery for a landing that happened somewhere else.
				if (!Host.TryRawMaterialHeldNow(out int held))
				{
					// Half of a credit is missing before the insertion has even run. Nothing
					// between the custody proof above and this reading dispatches, so the parcel
					// is still this delivery's to put back; it goes back rather than into a
					// destination whose gain could never afterwards be told.
					Host.Discard(bundle);
					return Refuse(Host, Placed);
				}
				object accepted = Host.Insert(bundle);
				if (Host.Landed(bundle, accepted, batch))
				{
					Placed += batch;
					remaining -= batch;
					room -= batch;
					continue;
				}
				if (Host.Alive(bundle))
				{
					// The bundle survived the insertion. Held by nobody it reached nobody and is
					// withdrawn; held by anyone at all it reached SOMEBODY, and this delivery may
					// neither destroy it nor mint its units a second time.
					if (!Host.HeldByNobody(bundle) || !Host.Discard(bundle))
					{
						return Refuse(Host, Placed);
					}
					break;
				}
				// The bundle went into this destination and stopped existing. Only what the
				// destination itself gained IN THIS MATERIAL may be counted, and only a gain
				// covering the whole batch settles it: a bundle that vanished leaving less behind
				// took the remainder somewhere this delivery cannot read.
				if (!Host.TryRawMaterialHeldNow(out int heldAfter))
				{
					// The parcel is already inside the destination and this delivery no longer
					// owns it, so nothing here is destroyed, withdrawn, or moved. What it cannot
					// read it does not credit: no units are counted, and the delivery stops.
					return Refuse(Host, Placed);
				}
				int landed = KingdomRules.DepositLandedUnits(batch, false, held, heldAfter);
				Placed += landed;
				remaining -= landed;
				room -= landed;
				if (landed < batch)
				{
					return Refuse(Host, Placed);
				}
			}
			return Settle(Host, Placed);
		}

		/// <summary>
		/// Stops the delivery and says so once (STANDARDS 7b).
		/// <para>
		/// SAYING SO MUST NEVER COST THE DELIVERY ITS ACCOUNTING. Naming a store reaches display
		/// handlers, and a handler that throws while the delivery is already reporting a fault
		/// would otherwise unwind past the caller and take the proved units with it &mdash; the
		/// one thing this whole file exists to keep hold of. So the outcome is built either way,
		/// and the once flag is set BEFORE the saying, so a throwing handler cannot turn one
		/// uncertainty into a line said at every delivery afterwards. This is the one place in
		/// the mod where a swallowed exception is correct, and it is swallowed at the narrowest
		/// possible point: the diagnostic, never the decision.
		/// </para>
		/// </summary>
		private static KingdomDepositOutcome Refuse(IKingdomDepositHost Host, int Placed)
		{
			try
			{
				if (!Host.CustodyAnnounced)
				{
					Host.CustodyAnnounced = true;
					Host.AnnounceUncertainCustody();
				}
			}
			catch (Exception)
			{
			}
			return new KingdomDepositOutcome(Placed, KingdomDepositCustody.Unproved);
		}

		/// <summary>Ends a fill that accounted for everything it made, and takes back an earlier
		/// uncertainty about this destination once a delivery has landed in it proved. Taking the
		/// saying back is a diagnostic too, and may not cost the delivery its accounting.
		/// </summary>
		private static KingdomDepositOutcome Settle(IKingdomDepositHost Host, int Placed)
		{
			try
			{
				if (Placed > 0 && Host.CustodyAnnounced)
				{
					Host.CustodyAnnounced = false;
				}
			}
			catch (Exception)
			{
			}
			return new KingdomDepositOutcome(Placed, KingdomDepositCustody.Settled);
		}
	}
}
