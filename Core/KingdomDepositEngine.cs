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
				// Creation has already run its callbacks. Two things are proved before the bundle
				// is touched again: that this delivery is still the only party that could be
				// holding it, and that the destination has the room the batch is about to claim.
				if (!Host.HeldByNobody(bundle))
				{
					return Refuse(Host, Placed);
				}
				int batch = KingdomRules.DepositBatch(remaining, room, Host.RoomNow(),
					Host.Stacks(bundle));
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
					// The stamp ran the engine's stack-count handlers. Custody is proved again
					// before the insertion, because inserting a body takes it out of whoever is
					// holding it; and a destination filled to its last unit while the stamp ran
					// refuses the bundle rather than being paid for it out of the older number.
					if (!Host.HeldByNobody(bundle))
					{
						return Refuse(Host, Placed);
					}
					if (!KingdomRules.DepositStampHolds(batch, Host.CountOf(bundle),
						Host.RoomNow()))
					{
						if (!Host.Discard(bundle))
						{
							return Refuse(Host, Placed);
						}
						break;
					}
				}
				// What the destination holds OF THIS MATERIAL going in, so what it gained can be
				// told from what the insertion returned.
				int held = Host.MaterialHeldNow();
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
				int landed = KingdomRules.DepositLandedUnits(batch, false, held,
					Host.MaterialHeldNow());
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

		/// <summary>Stops the delivery and says so once (STANDARDS 7b).</summary>
		private static KingdomDepositOutcome Refuse(IKingdomDepositHost Host, int Placed)
		{
			if (!Host.CustodyAnnounced)
			{
				Host.CustodyAnnounced = true;
				Host.AnnounceUncertainCustody();
			}
			return new KingdomDepositOutcome(Placed, KingdomDepositCustody.Unproved);
		}

		/// <summary>Ends a fill that accounted for everything it made, and takes back an earlier
		/// uncertainty about this destination once a delivery has landed in it proved.</summary>
		private static KingdomDepositOutcome Settle(IKingdomDepositHost Host, int Placed)
		{
			if (Placed > 0 && Host.CustodyAnnounced)
			{
				Host.CustodyAnnounced = false;
			}
			return new KingdomDepositOutcome(Placed, KingdomDepositCustody.Settled);
		}
	}
}
