using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	internal static partial class KingdomLab
	{

		/// <summary>
		/// The rung this building performs at. Read off the building's own part rather than off a
		/// survey, because the founder is standing in front of it and the thing they are standing in
		/// front of is the authority on what it can do.
		/// </summary>
		private static int RungAt(GameObject Building)
		{
			if (Building == null)
			{
				return -1;
			}
			if (Building.HasPart("r_KingdomChimericTheatre"))
			{
				return KingdomProcedureRules.RungTheatre;
			}
			if (Building.HasPart("r_KingdomGraftingHall"))
			{
				return KingdomProcedureRules.RungHall;
			}
			return Building.HasPart("r_KingdomVatHouse") ? KingdomProcedureRules.RungVat : KingdomProcedureRules.RungSlab;
		}

		/// <summary>The lodged savant's name, or null when the hall has nobody who knows the work.
		/// Derived from the crew the lodging machinery already placed &mdash; the hall assigns
		/// nobody, exactly as Addendum 6 says a great work never does.</summary>
		private static string SavantAt(KingdomSystem System)
		{
			return Simulation.City.KingdomResidents.HeadName(System);
		}
	}
}
