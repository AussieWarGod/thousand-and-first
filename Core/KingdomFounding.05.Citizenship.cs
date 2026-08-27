using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.World;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	public static partial class KingdomFounding
	{
		/// <summary>
		/// Enrols a creature as a citizen by owning exactly one namespaced slot in its base
		/// allegiance. Every other slot, temporary layer, flag and Brain field remains untouched.
		/// </summary>
		/// <param name="Citizen">The creature. The player is rejected; so is anything brainless.</param>
		/// <returns>True if enrolled, false if unfounded or the target is ineligible.</returns>
		/// <remarks>Enrolled creatures are protected: kingdom systems never destroy a citizen
		/// they did not themselves create (see the protection law in STANDARDS 7). Settlers
		/// spawned by the growth engine additionally carry KingdomBorn and may emigrate.</remarks>
		public static bool EnrollCitizen(GameObject Citizen)
		{
			return EnrollCitizen(Citizen, KingdomCitizenshipEnrollmentReason.Arrival);
		}

		public static bool EnrollCitizen(GameObject Citizen,
			KingdomCitizenshipEnrollmentReason Reason)
		{
			return EnrollCitizen(Citizen, Reason,
				The.Game == null ? 0L : The.Game.TimeTicks);
		}

		public static bool EnrollCitizen(GameObject Citizen,
			KingdomCitizenshipEnrollmentReason Reason, long FrozenAppliedTick)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			string failure;
			bool enrolled = KingdomCitizenship.TryEnroll(system, Citizen, Reason,
				FrozenAppliedTick, out failure);
			if (!enrolled && !string.IsNullOrEmpty(failure))
				KingdomLog.Log("citizenship: enrolment refused (" + failure + ")");
			return enrolled;
		}

	}
}
