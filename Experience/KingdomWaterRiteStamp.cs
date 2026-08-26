using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// What one refusal recorded about the night it happened, so a second asking can be told apart
	/// from the same asking twice. Written onto the settler themselves and read back by
	/// <see cref="KingdomWaterRiteRules.SomethingChanged"/>; it holds no countdown and is shown to
	/// the founder as words rather than as numbers.
	/// </summary>
	public readonly struct WaterRiteStamp
	{
		/// <summary>The answer given, for the line that repeats it when the founder asks why the
		/// door is shut.</summary>
		public readonly WaterRiteAnswer Answer;

		/// <summary>Hostility as it stood that night. A fall re-opens the question.</summary>
		public readonly int Hostility;

		/// <summary>Whether a rival shrine stood in their quarter that night. Its going re-opens
		/// the question.</summary>
		public readonly bool RivalShrine;

		/// <summary>Whether nothing but a change of the realm's own creed can re-open this. Set
		/// for <see cref="WaterRiteAnswer.Steadfast"/> and for nothing else.</summary>
		public readonly bool Absolute;

		/// <summary>Shared passes at which their reach would have covered the distance, or zero
		/// when no shared life could. From <see cref="KingdomWaterRiteRules.NeededDays"/>.</summary>
		public readonly int NeededDays;

		/// <summary>The realm's creed as it stood that night. A different creed is a different
		/// question, and is always allowed to be asked.</summary>
		public readonly string RealmCreed;

		public WaterRiteStamp(WaterRiteAnswer Answer, int Hostility, bool RivalShrine, bool Absolute, int NeededDays, string RealmCreed)
		{
			this.Answer = Answer;
			this.Hostility = Hostility;
			this.RivalShrine = RivalShrine;
			this.Absolute = Absolute;
			this.NeededDays = NeededDays;
			this.RealmCreed = RealmCreed;
		}
	}
}
