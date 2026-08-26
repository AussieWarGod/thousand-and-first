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

		// ==================================================================================
		// Reading the world
		// ==================================================================================

		/// <summary>
		/// Which records this hall could perform at this place, for this founder, today.
		/// <para>
		/// The visibility law is enforced HERE and by the accessor rather than by discipline: a
		/// named procedure the founder has not found is dropped before any row, count or refusal is
		/// computed, so "cannot have it" and "have never heard of it" are the same absence of a row.
		/// </para>
		/// </summary>
		private static List<LabProcedure> Candidates(List<LabSlot> Anatomy, int At, int Rung,
			List<GameObject> Kept, r_KingdomLabRecord Record, List<string> Roster)
		{
			List<LabProcedure> offers = new List<LabProcedure>();
			List<LabProcedure> all = KingdomProcedures.All;
			for (int i = 0; i < all.Count; i++)
			{
				LabProcedure procedure = all[i];
				if (!KingdomProcedures.Discovered(procedure) || Record.Refuses(procedure.Key))
				{
					continue;
				}
				if (procedure.IsNamed && Record.AlreadyHad(procedure.Key))
				{
					continue;
				}
				// The record's own Knowledge gate, read through the SHIPPED roster grammar and
				// nothing of ours: a procedure gates on a research node, a rite, a taught disk or a
				// certified machine with one attribute, exactly as a building does, and a third
				// party's procedure gates on a third party's research with no code at all.
				if (!KingdomProcedureRules.KnowledgeMet(Roster, procedure.Knowledge))
				{
					continue;
				}
				if (Rung < procedure.MinRung || CountFor(Kept, procedure) < procedure.Preserved)
				{
					continue;
				}
				if (KingdomProcedureRules.JudgeSlot(procedure, Anatomy[At], KingdomProcedures.Categories(procedure)) == LabVerdict.Allowed)
				{
					offers.Add(procedure);
				}
			}
			return offers;
		}

		private static List<GameObject> KeptParts(GameObject Actor)
		{
			List<GameObject> kept = new List<GameObject>();
			foreach (GameObject item in Actor.GetInventoryAndEquipment())
			{
				if (item != null && item.GetIntProperty(KeptProperty) == 1)
				{
					kept.Add(item);
				}
			}
			return kept;
		}

		private static int TotalKept(List<GameObject> Kept)
		{
			int total = 0;
			for (int i = 0; i < Kept.Count; i++)
			{
				total += Kept[i].Count;
			}
			return total;
		}

		/// <summary>How many kept parts would answer this record: stamped with the class it grants,
		/// and inside its band if it names one.</summary>
		private static int CountFor(List<GameObject> Kept, LabProcedure Procedure)
		{
			int total = 0;
			for (int i = 0; i < Kept.Count; i++)
			{
				string stamp = Kept[i].GetStringProperty(KingdomProcedures.StampProperty);
				if (KingdomProcedureRules.StampCarries(stamp, Procedure.Grants)
					&& KingdomProcedureRules.MagnitudeAdmits(Procedure, stamp))
				{
					total += Kept[i].Count;
				}
			}
			return total;
		}

		private static GameObject FirstSourceFor(List<GameObject> Kept, LabProcedure Procedure)
		{
			for (int i = 0; i < Kept.Count; i++)
			{
				string stamp = Kept[i].GetStringProperty(KingdomProcedures.StampProperty);
				if (KingdomProcedureRules.StampCarries(stamp, Procedure.Grants)
					&& KingdomProcedureRules.MagnitudeAdmits(Procedure, stamp))
				{
					return Kept[i];
				}
			}
			return null;
		}

	}
}
