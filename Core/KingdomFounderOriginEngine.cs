using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// Counts exactly one founder into one settlement's per-profile origin tally, exactly once,
	/// ever.
	/// <para>
	/// The tally is a shared aggregate: ordinary arrivals raise the same dictionary inside their
	/// own before/after protocol, and a founding cohort can be seeded long after some of them have
	/// arrived. So this may never assume the tally is zero, may never assume it belongs to this
	/// cohort, and may never RESET it. It only ever ADDS one, and only when it can prove it has
	/// not already added that one.
	/// </para>
	/// <para>
	/// The proof is a durable, identity-bound obligation written BEFORE anything is mutated, and a
	/// completion written after both mutations are measured. Between the two writes nothing
	/// eventful may run: both are direct assignments the seam guarantees (see
	/// <see cref="IKingdomFounderOriginHost"/>), which is what makes "write the label, raise the
	/// tally, measure both" a single live attempt rather than a sequence a callback can walk into.
	/// </para>
	/// <para>
	/// What it deliberately CANNOT do is infer completion from the counter. An unrelated arrival
	/// can move the tally to any value, including exactly the value this obligation intended to
	/// leave behind, so counter equality never authorizes marking an obligation completed. Only an
	/// independently sufficient before image &#8212; the origin label being absent, which proves
	/// the increment that strictly follows it never happened &#8212; authorizes replay. Anything
	/// else is mixed state, and mixed state is quarantined in the open rather than guessed at.
	/// </para>
	/// <para>
	/// That quarantine is a SAFETY POLICY, not a claim of fully automatic forward recovery. A
	/// two-write pair across a body property and a shared aggregate cannot be recovered
	/// automatically in every case, and this does not pretend otherwise: where it cannot prove
	/// which side of the increment an interruption fell on, it says so, stops, and leaves the
	/// settlement playable with one honest gap in a tally rather than a silent double count or a
	/// silent undercount nobody is told about. The announcement is bounded to at most once per
	/// obligation, because the terminal state is written on the body it belongs to.
	/// </para>
	/// </summary>
	internal static class KingdomFounderOriginEngine
	{
		internal static KingdomFounderOriginOutcome Account(IKingdomFounderOriginHost Host,
			string Profile, out string Reason)
		{
			Reason = "";
			if (Host == null)
			{
				Reason = "the founder accounting had no host";
				return KingdomFounderOriginOutcome.Quarantined;
			}
			string body = Host.BodyId;
			string city = Host.CityId;
			KingdomFounderOriginReceipt shape = new KingdomFounderOriginReceipt(body, Profile,
				city, 0, KingdomFounderOriginState.Prepared);
			if (!KingdomFounderOriginCodec.Valid(shape))
			{
				Reason = "the founder accounting identities were not exact";
				return KingdomFounderOriginOutcome.Quarantined;
			}

			KingdomFounderOriginReceipt held = null;
			if (Host.HasReceipt())
			{
				if (!KingdomFounderOriginCodec.TryDecode(Host.RawReceipt(), out held))
					return Quarantine(Host, shape, out Reason,
						"a founder accounting receipt was malformed or of the wrong shape");
				if (!held.Binds(body, Profile, city))
				{
					// It decodes, so it is somebody's obligation - just not this one's. Refuse
					// without overwriting it: destroying another body's, profile's or city's
					// accounting to make this attempt tidy is exactly the kind of guess this
					// transaction exists to refuse.
					Reason = "a founder accounting receipt named another body, profile or city";
					return KingdomFounderOriginOutcome.Quarantined;
				}
				if (held.State == KingdomFounderOriginState.Completed)
					return KingdomFounderOriginOutcome.AlreadySettled;
				if (held.State == KingdomFounderOriginState.Quarantined)
					return KingdomFounderOriginOutcome.AlreadyQuarantined;
			}

			// The origin label is the only witness independent of the shared counter, and it is
			// sufficient in exactly one direction: it is written immediately before the increment,
			// so its ABSENCE proves the increment did not happen. Its presence proves nothing
			// about the counter at all.
			if (Host.HasOrigin())
			{
				string origin = Host.RawOrigin();
				if (string.Equals(origin, Profile, StringComparison.Ordinal))
					return Quarantine(Host, shape, out Reason,
						"a founder carried this origin with no completed accounting, so whether "
							+ "the tally already holds them cannot be told from the tally");
				return Quarantine(Host, shape, out Reason,
					"a founder carried a conflicting or empty origin label");
			}

			// Freeze the before image against the tally as it stands NOW. A prepared receipt from
			// an earlier attempt is deliberately re-frozen rather than trusted: an unrelated
			// arrival may have moved the tally since, and the label's absence says this obligation
			// contributed none of that movement.
			int recorded;
			if (!TryFrozenTally(Host, Profile, out recorded))
				return Quarantine(Host, shape, out Reason,
					"the settlement's origin tally was negative or at its ceiling");
			if (held == null || held.Before != recorded)
			{
				KingdomFounderOriginReceipt prepared = new KingdomFounderOriginReceipt(body,
					Profile, city, recorded, KingdomFounderOriginState.Prepared);
				if (!Publish(Host, prepared))
					return Quarantine(Host, shape, out Reason,
						"the founder accounting obligation would not persist before its writes");
				held = prepared;
			}

			// Re-prove the exact before image immediately before the mutation, so the last thing
			// read before the write is the thing the write depends on.
			int now;
			if (!TryFrozenTally(Host, Profile, out now) || now != held.Before)
				return Quarantine(Host, shape, out Reason,
					"the settlement's origin tally moved between preparation and its increment");

			Host.WriteOrigin(Profile);
			Host.WriteTally(Profile, now + 1);

			int after;
			if (!Host.HasOrigin()
				|| !string.Equals(Host.RawOrigin(), Profile, StringComparison.Ordinal)
				|| !Host.TryTally(Profile, out after) || after != now + 1)
				return Quarantine(Host, shape, out Reason,
					"a founder's origin and tally did not both take exactly");
			if (!Publish(Host, held.With(KingdomFounderOriginState.Completed)))
				return Quarantine(Host, shape, out Reason,
					"a founder was counted but its completion would not persist");
			return KingdomFounderOriginOutcome.Applied;
		}

		/// <summary>
		/// The tally as a number this accounting may add to: absent counts as zero, a negative
		/// reading is corruption rather than a small number, and the ceiling is refused before the
		/// addition rather than wrapped through it.
		/// </summary>
		private static bool TryFrozenTally(IKingdomFounderOriginHost Host, string Profile,
			out int Recorded)
		{
			int recorded;
			Recorded = Host.TryTally(Profile, out recorded) ? recorded : 0;
			return Recorded >= 0 && Recorded < int.MaxValue;
		}

		private static bool Publish(IKingdomFounderOriginHost Host,
			KingdomFounderOriginReceipt Receipt)
		{
			string wire = KingdomFounderOriginCodec.Encode(Receipt);
			if (wire == null) return false;
			Host.WriteReceipt(wire);
			KingdomFounderOriginReceipt read;
			return Host.HasReceipt()
				&& string.Equals(Host.RawReceipt(), wire, StringComparison.Ordinal)
				&& KingdomFounderOriginCodec.TryDecode(Host.RawReceipt(), out read)
				&& read.State == Receipt.State && read.Before == Receipt.Before
				&& read.Binds(Receipt.BodyId, Receipt.Profile, Receipt.CityId);
		}

		/// <summary>
		/// Writes the terminal state so this founder is never attempted again, and reports it once.
		/// A quarantine that cannot itself be written is still a quarantine: the caller is told,
		/// and the next attempt reaches the same refusal rather than a different one.
		/// </summary>
		private static KingdomFounderOriginOutcome Quarantine(IKingdomFounderOriginHost Host,
			KingdomFounderOriginReceipt Shape, out string Reason, string Because)
		{
			Reason = Because;
			try { Publish(Host, Shape.With(KingdomFounderOriginState.Quarantined)); }
			catch { /* The refusal stands whether or not the record of it does. */ }
			return KingdomFounderOriginOutcome.Quarantined;
		}
	}
}
