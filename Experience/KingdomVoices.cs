using System;
using XRL;

namespace ThousandAndFirst
{
	/// <summary>
	/// The settlement's named voices, at the engine edge.
	/// <para>
	/// Everything here wraps a line the settlement was already printing. It adds no messages, no
	/// popups and no interruptions of its own &mdash; the only change a player sees is that the
	/// news now comes from somebody who lives there.
	/// </para>
	/// <para>
	/// <see cref="KingdomVoiceRules"/> owns the whole decision; this file supplies the two things
	/// a pure rule cannot reach for itself, the settlement's kernel id and the current tick, and
	/// guarantees that a failure in either costs a voice rather than a settlement pass.
	/// </para>
	/// </summary>
	public static class KingdomVoices
	{
		/// <summary>
		/// Who speaks for this settlement about this occasion, right now.
		/// </summary>
		/// <param name="System">The kingdom. Null, unfounded, or an empty roll all yield
		/// <see cref="KingdomVoiceRules.Speaker.None"/>, which is the settlement's own voice and
		/// not an error.</param>
		/// <param name="Occasion">The moment being spoken.</param>
		/// <returns>A settler on the roll, or <see cref="KingdomVoiceRules.Speaker.None"/>. Never
		/// throws: a fault here is logged and degrades to the settlement's own voice, because a
		/// line of flavour must never be able to abort the growth pass that was printing it.</returns>
		public static KingdomVoiceRules.Speaker Draw(KingdomSystem System, VoiceOccasion Occasion)
		{
			if (System == null || !System.Founded)
			{
				return KingdomVoiceRules.Speaker.None;
			}
			try
			{
				string settlementId = KingdomChronicle.SettlementId(System);
				if (!KingdomIdentityRules.IsSettlementId(settlementId))
					return KingdomVoiceRules.Speaker.None;
				Simulation.City.KingdomCityState state;
				Simulation.City.KingdomResidentRollProjection roll;
				if (!Simulation.City.KingdomResidents.TryRoll(System, out state, out roll))
					return KingdomVoiceRules.Speaker.None;
				return KingdomVoiceRules.ChooseSpeaker(
					roll.Names,
					roll.Origins,
					settlementId,
					Occasion,
					CurrentOrdinal());
			}
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst: choosing a voice for " + Occasion, error);
				return KingdomVoiceRules.Speaker.None;
			}
		}

		/// <summary>
		/// Draws a speaker and puts the settlement's announcement in their mouth. The ordinary
		/// call site: one line, at the point that was already printing.
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Occasion">The moment being spoken.</param>
		/// <param name="Announcement">The line the settlement was already going to print.</param>
		/// <param name="Words">An authored clause to speak ahead of the settler's own, or null.</param>
		/// <returns>The announcement with a named voice after it, or the announcement unchanged
		/// when nobody is on the roll to say anything.</returns>
		public static string Say(KingdomSystem System, VoiceOccasion Occasion, string Announcement, string Words = null)
		{
			return KingdomVoiceRules.Compose(Draw(System, Occasion), Occasion, Announcement, Words);
		}

		/// <summary>
		/// The same, for a call site that has already drawn its speaker because it also needs the
		/// name for the chronicle. Re-drawing would return the identical speaker &mdash; the draw
		/// is a pure function of settlement, occasion and tick &mdash; so this exists to keep the
		/// name and the words provably the same person, not to save the work.
		/// </summary>
		/// <param name="Speaker">The speaker already drawn for this occasion.</param>
		/// <param name="Occasion">The moment being spoken.</param>
		/// <param name="Announcement">The line the settlement was already going to print.</param>
		/// <param name="Words">An authored clause to speak ahead of the settler's own, or null.</param>
		public static string Say(KingdomVoiceRules.Speaker Speaker, VoiceOccasion Occasion, string Announcement, string Words = null)
		{
			return KingdomVoiceRules.Compose(Speaker, Occasion, Announcement, Words);
		}

		/// <summary>
		/// The tick the moment happened on, as the draw's ordinal. Clamped at zero because the
		/// ordinal is unsigned and a game that has not started its clock must still be able to
		/// speak.
		/// </summary>
		private static ulong CurrentOrdinal()
		{
			if (The.Game == null)
			{
				return 0uL;
			}
			long ticks = The.Game.TimeTicks;
			return (ticks > 0L) ? (ulong)ticks : 0uL;
		}
	}
}
