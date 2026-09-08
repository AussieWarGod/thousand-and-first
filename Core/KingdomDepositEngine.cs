namespace ThousandAndFirst
{
	/// <summary>Whether a delivery can account for every bundle it made.</summary>
	internal enum KingdomDepositCustody
	{
		/// <summary>Every bundle is proved placed, or proved withdrawn because it reached
		/// nobody, or gone with the store's own gain accounting for the whole of it.</summary>
		Settled = 0,

		/// <summary>A bundle this delivery made is somewhere the delivery cannot prove it owns.
		/// Nothing more is created, nothing more is counted, and nothing is destroyed.</summary>
		Unproved = 1,
	}

	/// <summary>What one store's fill placed, and whether the delivery may go on at all.</summary>
	internal sealed class KingdomDepositOutcome
	{
		/// <summary>Units proved into this store, never more.</summary>
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
	/// Fills one store up to the room it declared, and no further.
	/// <para>
	/// Nothing is remembered across a callback, because there are three of them and every one
	/// belongs to somebody else: creating the bundle, STAMPING ITS COUNT (which is
	/// <c>Stacker.StackCount</c>, and sends <c>StackCountChangedEvent</c>), and inserting it. So
	/// the destination and its room are proved after the creation and again after the stamp, and
	/// the outcome &mdash; this bundle, standing in this store, carrying the count it was stamped
	/// with &mdash; is proved before a single unit is counted.
	/// </para>
	/// <para>
	/// Two laws sit above the counting, and they are what make a refusal possible at all. A
	/// delivery never DESTROYS a body it cannot prove it owns at that instant: a handler that
	/// carried the bundle into another inventory is holding real material, and obliterating it
	/// would erase somebody else's goods to tidy up an ambiguity. And a delivery never MINTS a
	/// replacement for a bundle whose fate it could not read: the units would then exist twice,
	/// once wherever the handler put them and once in the next store or on the ground. So an
	/// unproved bundle stops the delivery outright, and what could not be proved deposited is
	/// never credited.
	/// </para>
	/// </summary>
	internal static class KingdomDepositEngine
	{
		/// <summary>
		/// Fills one store, up to its room and up to what is left to deliver.
		/// </summary>
		/// <param name="Host">The seam onto the destination and its bundles.</param>
		/// <param name="Room">Room the store had when this delivery chose it.</param>
		/// <param name="Units">Units still to deliver.</param>
		/// <returns>What was proved placed, and whether the delivery may continue.</returns>
		internal static KingdomDepositOutcome Fill(IKingdomDepositHost Host, int Room, int Units)
		{
			if (Host == null || Units < 1 || Room < 1)
			{
				return new KingdomDepositOutcome(0, KingdomDepositCustody.Settled);
			}
			int placed = 0;
			int remaining = Units;
			int room = Room;
			while (remaining > 0 && room > 0)
			{
				object bundle = Host.Create();
				if (bundle == null)
				{
					break;
				}
				// Creation has already run its callbacks, so the destination is proved again here
				// rather than trusted: the batch is bounded by the room this exact store has at
				// this instant as well as by the room it was chosen with.
				int batch = KingdomRules.DepositBatch(remaining, room, Host.RoomNow(),
					Host.Stacks(bundle));
				if (batch < 1)
				{
					// Nothing is placed and nothing is counted; the units stay to deliver and go
					// to the next store with room, or on the ground.
					if (!TryWithdraw(Host, bundle))
					{
						return Refuse(Host, placed);
					}
					break;
				}
				if (batch > 1 && !TryStamp(Host, bundle, batch))
				{
					// A store filled to its last unit while the stamp ran refuses the bundle
					// rather than being paid for it out of a number read before that handler.
					if (!TryWithdraw(Host, bundle))
					{
						return Refuse(Host, placed);
					}
					break;
				}
				// What the store holds going in, so what it gained can be told from what the
				// insertion returned.
				int held = Host.HeldNow();
				object accepted = Host.Insert(bundle);
				if (Host.Landed(bundle, accepted, batch))
				{
					placed += batch;
					remaining -= batch;
					room -= batch;
					continue;
				}
				if (Host.Alive(bundle))
				{
					// The bundle survived the insertion. Standing nowhere at all it reached
					// nobody and is withdrawn; standing anywhere it reached SOMEBODY, and this
					// delivery may neither destroy it nor mint its units a second time.
					if (!Host.Ownerless(bundle))
					{
						return Refuse(Host, placed);
					}
					Host.Discard(bundle);
					break;
				}
				// The bundle is gone. Only what the store itself gained may be counted, and only
				// a gain covering the whole batch settles it: a bundle that vanished leaving less
				// behind took the remainder somewhere this delivery cannot read.
				int landed = KingdomRules.DepositLandedUnits(batch, false, held, Host.HeldNow());
				placed += landed;
				remaining -= landed;
				room -= landed;
				if (landed < batch)
				{
					return Refuse(Host, placed);
				}
			}
			return Settle(Host, placed);
		}

		/// <summary>Stamps a batch onto a bundle and judges whether it may still be inserted. The
		/// stamp itself runs the engine's stack-count handlers, so the bundle and the destination
		/// are both read again afterwards.</summary>
		private static bool TryStamp(IKingdomDepositHost Host, object Bundle, int Batch)
		{
			Host.Stamp(Bundle, Batch);
			return Host.Alive(Bundle)
				&& KingdomRules.DepositStampHolds(Batch, Host.CountOf(Bundle), Host.RoomNow());
		}

		/// <summary>Takes back a bundle the delivery made and never placed. True once nothing is
		/// outstanding: the bundle is already gone, or it reached nobody and has been destroyed.
		/// False means somebody else is holding it, and it is left exactly where it stands.
		/// </summary>
		private static bool TryWithdraw(IKingdomDepositHost Host, object Bundle)
		{
			if (!Host.Alive(Bundle))
			{
				return true;
			}
			if (!Host.Ownerless(Bundle))
			{
				return false;
			}
			Host.Discard(Bundle);
			return true;
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
		/// uncertainty about this store once a delivery has landed in it proved.</summary>
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
