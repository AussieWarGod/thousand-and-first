using System;

using XRL;
using XRL.UI;
using XRL.World;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomHappenings
	{

		// ==================================================================================
		// Funerals — the one telling a death gets, enriched rather than duplicated
		// ==================================================================================

		/// <summary>
		/// Compatibility prose helper. Physical publication is owned by
		/// <see cref="OwnDeathTelling"/>; this method never publishes by itself.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Name">The settler who died, as the roll carried them.</param>
		/// <param name="Cause">The cause the memory machinery classified.</param>
		/// <param name="Z">Where they were, or null.</param>
		/// <returns>The clause to append to the death's own telling. Empty when happenings are
		/// off, which leaves the existing telling exactly as it was.</returns>
		public static string FuneralClause(KingdomSystem System, string Name, KingdomOfficeRules.DeathCause Cause, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || string.IsNullOrEmpty(Name))
			{
				return "";
			}
			// The office holder as the city knows them, epithet and all (lane 5): the names lane 5
			// mints are the names the happenings use, which is the whole point of minting them.
			return KingdomHappeningRules.FuneralClause(
				KingdomOfficeRules.ChooseTitle(System.SeatName),
				KingdomPresentation.Rich(KingdomNotables.HolderName(System)));
		}

		/// <summary>
		/// Takes ownership of one death's only semantic telling. A witnessed death stages living,
		/// named mourners at a functional shrine. Missing bodies, ground, or shrine produce only a
		/// dated report. The lifecycle sidecar owns chronicle/told/message dispositions in both cases.
		/// </summary>
		internal static bool OwnDeathTelling(KingdomSystem System, string Name, string Origin,
			KingdomOfficeRules.DeathCause Cause, Zone Z, long Tick)
		{
			if (!Enabled || System == null || !System.Founded || string.IsNullOrEmpty(Name))
				return false;
			if (System.City == null || Tick <= 0L) return false;
			KingdomCityState state;
			KingdomCityFault fault;
			if (!System.City.TryRead(out state, out fault))
			{
				KingdomLog.Log("happening: funeral book refused (" + fault + ") for " + Name);
				return false;
			}
			int residentId = ResidentIdOf(state, Name);
			if (residentId <= 0) return false;
			if (KingdomHappeningRules.AlreadyTold(state, KingdomHappeningKind.Funeral,
				residentId, 0)) return true;
			if (KingdomPhysicalHappenings.AlreadyCompleted(System.City,
				KingdomPhysicalHappeningKind.Funeral, residentId, 0, Tick)) return true;
			QueueFuneral(System, System.City, state, System.SeatName, Tick, residentId, Name,
				Origin, Cause, Z, out KingdomPhysicalQueueResult result);
			return result != KingdomPhysicalQueueResult.Refused
				&& result != KingdomPhysicalQueueResult.Busy;
		}

		/// <summary>
		/// The safety net, and the reason <c>KingdomHappeningRules.FuneralDue</c> exists: a row the
		/// model found dead that the memory machinery never heard about.
		/// <para>
		/// <c>r_KingdomCitizenLegacy</c> is attached on a settlement pass, so a settler killed
		/// before this mod ever tagged them dies without <c>RecordDeath</c> running &mdash; and the
		/// row still goes <c>Dead</c> when the roster is next read. Without this the city would
		/// lose somebody in silence, which STANDARDS 7b does not allow.
		/// </para>
		/// <para>
		/// <b>It cannot double-tell.</b> Two independent guards have to both fail before a second
		/// telling is possible: the told-log ring already carries a <c>Funeral</c> line for this
		/// resident (written by the physical/report-only funeral lifecycle that owns the death's
		/// telling), and the dead roll already carries the name. The roll is the stronger of the two
		/// because it is unbounded where the ring is thirty-two lines, and it is
		/// <c>KingdomOffices</c>' own record rather than a copy of it.
		/// </para>
		/// </summary>
		private static KingdomCityState Funerals(KingdomSystem System, KingdomCityBook book,
			KingdomCityState state, string label, bool here, long nowTick, ref int pushed,
			int pushBudget)
		{
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row) || row.ResidentId <= 0 || !KingdomHappeningRules.FuneralDue(row))
				{
					continue;
				}
				if (KingdomHappeningRules.AlreadyTold(state, KingdomHappeningKind.Funeral,
					row.ResidentId, 0)
					|| KingdomPhysicalHappenings.AlreadyCompleted(book,
						KingdomPhysicalHappeningKind.Funeral, row.ResidentId, 0, nowTick)
					|| System.DeadNames.Contains(row.Name))
				{
					continue;
				}
				int ordinal;
				KingdomOfficeRules.DeathCause cause = KingdomResidentRules.TryDeathCauseOrdinal(row.Cause, out ordinal)
					? (KingdomOfficeRules.DeathCause)ordinal
					: KingdomOfficeRules.DeathCause.Unknown;
				Zone zone = here ? The.Player?.CurrentZone : null;
				state = QueueFuneral(System, book, state, label, nowTick, row.ResidentId,
					Named(row.Name), "", cause, zone);
				// One a pass, the same discipline KingdomOffices uses for cairns: a city that lost
				// several people off-screen tells them one visit at a time rather than all at once.
				return state;
			}
			return state;
		}

		private static KingdomCityState QueueFuneral(KingdomSystem System, KingdomCityBook book,
			KingdomCityState state, string label, long tick, int residentId, string name,
			string origin, KingdomOfficeRules.DeathCause cause, Zone zone)
		{
			return QueueFuneral(System, book, state, label, tick, residentId, name, origin,
				cause, zone, out KingdomPhysicalQueueResult ignored);
		}

		private static KingdomCityState QueueFuneral(KingdomSystem System, KingdomCityBook book,
			KingdomCityState state, string label, long tick, int residentId, string name,
			string origin, KingdomOfficeRules.DeathCause cause, Zone zone,
			out KingdomPhysicalQueueResult result)
		{
			result = KingdomPhysicalQueueResult.Refused;
			if (state == null || residentId <= 0 || string.IsNullOrEmpty(name)) return state;
			if (KingdomHappeningRules.AlreadyTold(state, KingdomHappeningKind.Funeral,
				residentId, 0)) return state;
			string place = KingdomPresentation.Rich(KingdomWord.CityName(System, label));
			string mourning = KingdomOfficeRules.MourningChronicle(name,
				KingdomPresentation.Rich(origin), place, cause);
			string rite = KingdomHappeningRules.FuneralClause(
				KingdomOfficeRules.ChooseTitle(System.SeatName),
				KingdomPresentation.Rich(KingdomNotables.HolderName(System)));
			result = KingdomPhysicalHappenings.QueueGeneric(System,
				book, KingdomPhysicalHappeningKind.Funeral, tick, residentId, 0, (int)cause,
				zone, null, mourning + rite, DatedReport(tick, mourning), "", "",
				KingdomVoices.Say(System, VoiceOccasion.CitizenLost,
					"{{r|" + KingdomOfficeRules.MourningMessage(name, cause) + "}}"), "", "",
				"water-speaking shrine", CurrentTick(tick));
			KingdomCityState next = Refresh(book, state);
			if (KingdomHappeningRules.AlreadyTold(next, KingdomHappeningKind.Funeral,
				residentId, 0)) KingdomLog.Log("happening: funeral " + name + " cause=" + cause
				+ " at " + label + " physical="
				+ (result == KingdomPhysicalQueueResult.AttendedReady));
			return next;
		}
	}
}
