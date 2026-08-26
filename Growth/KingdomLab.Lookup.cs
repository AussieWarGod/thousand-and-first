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

		/// <summary>Counts the actual engine effect, independent of the lab record. Callers compare
		/// before and after so a callback fault after mutation cannot turn a completed graft free.</summary>
		private static int ProcedurePresence(GameObject Actor, LabProcedure Procedure)
		{
			try
			{
				if (Actor == null || Procedure == null)
				{
					return -1;
				}
				if (Procedure.Source == LabSource.Mutation)
				{
					XRL.World.Parts.Mutations mutations = Actor.GetPart<XRL.World.Parts.Mutations>();
					return KingdomLabRules.MutationPresence(
						mutations != null && mutations.HasMutation(Procedure.Grants),
						Actor.GetPart(Procedure.Grants) is XRL.World.Parts.Mutation.BaseMutation);
				}
				List<XRL.World.Anatomy.BodyPart> parts = Actor.Body?.GetParts();
				if (Procedure.Source == LabSource.Limb)
				{
					int limbs = 0;
					string manager = KingdomProcedures.ManagerFor(Procedure.Key);
					for (int i = 0; parts != null && i < parts.Count; i++)
					{
						if (parts[i] != null && string.Equals(parts[i].Manager, manager,
							StringComparison.OrdinalIgnoreCase))
						{
							limbs++;
						}
					}
					return limbs;
				}
				int held = (Actor.GetPart(Procedure.Grants) == null) ? 0 : 1;
				List<GameObject> seen = new List<GameObject>();
				for (int i = 0; parts != null && i < parts.Count; i++)
				{
					GameObject bearer = parts[i]?.DefaultBehavior;
					if (!GameObject.Validate(bearer) || seen.Contains(bearer))
					{
						continue;
					}
					seen.Add(bearer);
					if (bearer.GetPart(Procedure.Grants) != null)
					{
						held++;
					}
				}
				return held;
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: procedure presence read threw (" + ex.Message + ")");
				return -1;
			}
		}

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
