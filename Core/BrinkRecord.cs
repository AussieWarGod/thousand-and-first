using XRL;
using XRL.World;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>
	/// One brink as it stands right now: whether there is one at all, when it was reached, when
	/// the founder was warned of it, and what they would have to act on. Immutable, because a
	/// half-read brink two callers can disagree about is a settler who leaves twice.
	/// </summary>
	public readonly struct BrinkRecord
	{
		/// <summary>Whether a brink is recorded at all. Everything else is meaningless when this
		/// is false.</summary>
		public readonly bool Stands;

		/// <summary>The tick the irreversible line was actually crossed &mdash; not the pass the
		/// founder noticed it. Zero for a brink recorded before it could be dated.</summary>
		public readonly long ReachedTick;

		/// <summary>
		/// The tick the word went out. <see cref="KingdomBrinkRules.Unwarned"/> until it has, and
		/// the anchor of the whole window once it has: the founder's time runs from being told,
		/// never from the crossing, so a brink reached deep inside an absence still hands them the
		/// entire window on the day they hear about it.
		/// </summary>
		public readonly long WarnedTick;

		/// <summary>What the founder would act on: the creed pulling at them, the other city.
		/// Null when the kind carries no cause of its own.</summary>
		public readonly string Cause;

		/// <summary>The <see cref="ConversionChannel"/> a creed brink was reached through, so the
		/// conversion that fires at the end of the window picks the same words it would have
		/// picked on the day. Zero for the kinds that have no channel.</summary>
		public readonly int Channel;

		public BrinkRecord(bool Stands, long ReachedTick, long WarnedTick, string Cause, int Channel)
		{
			this.Stands = Stands;
			this.ReachedTick = Stands ? ReachedTick : 0L;
			this.WarnedTick = Stands ? WarnedTick : 0L;
			this.Cause = Stands ? (string.IsNullOrEmpty(Cause) ? null : Cause) : null;
			this.Channel = Stands ? Channel : 0;
		}

		/// <summary>Whether the founder has been told. A brink nobody has been told about can
		/// never fire, however old it is.</summary>
		public bool Warned
		{
			get { return Stands && KingdomBrinkRules.Warned(WarnedTick); }
		}

		/// <summary>No brink. What every settler and every realm carries nearly always.</summary>
		public static BrinkRecord None
		{
			get { return new BrinkRecord(Stands: false, 0L, 0L, null, 0); }
		}
	}
}
