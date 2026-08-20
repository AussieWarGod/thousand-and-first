using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The settlement's answer to a machine hauled home from a ruin: certifies it fit for the
	/// grid, or says plainly why not. Follows the <see cref="KingdomLarder"/> idiom &mdash; a
	/// single call that does its own eligibility check, its own success messaging, and its own
	/// chronicle entry, surfacing only a decline through <c>Failure</c>. A refusal changes
	/// nothing: the machine stays exactly where the founder put it, untouched.
	/// </summary>
	public static class KingdomSalvage
	{
		/// <summary>
		/// Property flag set on a machine once the settlement has certified it fit for its
		/// grid. Nothing but <see cref="Certify"/> sets or clears it. A future power system
		/// reads this before treating a hauled-in machine as part of the grid &mdash; the
		/// interlock this system feeds.
		/// </summary>
		public const string CertifiedProperty = "KingdomCertified";

		/// <summary>
		/// What <see cref="Assess"/> found, ready for a confirmation popup before anything is
		/// spent or marked.
		/// </summary>
		public struct SalvageAssessment
		{
			/// <summary>False only when the call had nothing to assess (no system, no zone, no
			/// machine, or an unfounded kingdom). Every other field is meaningless when this is
			/// false.</summary>
			public bool Valid;

			/// <summary>True when the machine already carries <see cref="CertifiedProperty"/>.
			/// <see cref="Certify"/> decommissions rather than certifies when this is set, for
			/// no cost and no eligibility check &mdash; taking a machine off the grid is always
			/// the founder's to do.</summary>
			public bool AlreadyCertified;

			public KingdomSalvageRules.SalvageVerdict Verdict;

			public int WaterCost;

			public int HandsRequired;
		}

		/// <summary>
		/// Reads a machine's state &mdash; Examiner readings, damage effects, the engine's own
		/// explosiveness check, the settlement's stores and population &mdash; and reports the
		/// verdict without changing anything. Safe to call for a confirmation popup; call
		/// <see cref="Certify"/> to actually act on it.
		/// </summary>
		/// <param name="System">The kingdom; must be founded for a meaningful assessment.</param>
		/// <param name="Z">Zone the machine stands in; must be the kingdom's own claimed ground.</param>
		/// <param name="Machine">The machine to inspect.</param>
		public static SalvageAssessment Assess(KingdomSystem System, Zone Z, GameObject Machine)
		{
			SalvageAssessment assessment = default;
			if (System == null || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return assessment;
			}
			if (Machine == null || !GameObject.Validate(Machine) || Machine.CurrentZone == null || Machine.CurrentZone.ZoneID != Z.ZoneID)
			{
				return assessment;
			}
			assessment.Valid = true;
			if (Machine.GetIntProperty(CertifiedProperty) == 1)
			{
				assessment.AlreadyCertified = true;
				assessment.Verdict = KingdomSalvageRules.SalvageVerdict.Certified;
				return assessment;
			}
			Inspect(Machine, out bool hazardous, out bool broken, out bool rusted, out bool understood, out int complexity, out int difficulty);
			int storedWater = KingdomSurvey.Take(Z).StoredWater;
			assessment.Verdict = KingdomSalvageRules.Assess(hazardous, broken, rusted, understood, complexity, difficulty, storedWater, System.Population, out int waterCost, out int handsRequired);
			assessment.WaterCost = waterCost;
			assessment.HandsRequired = handsRequired;
			return assessment;
		}

		/// <summary>
		/// Certifies a machine fit for the settlement's grid, or decommissions one already
		/// certified. The service does its own eligibility check and success messaging; this
		/// only surfaces a decline, matching every other action <see cref="KingdomLarder"/> and
		/// the Charter's vessel dedication already establish.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone the machine stands in; must be the kingdom's own claimed ground.</param>
		/// <param name="Machine">The machine to certify or decommission.</param>
		/// <param name="Failure">Set to a player-facing reason when this returns false. Nothing
		/// is spent or marked when it does.</param>
		/// <returns>True once the machine's grid standing has actually changed.</returns>
		public static bool Certify(KingdomSystem System, Zone Z, GameObject Machine, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A machine is certified on the kingdom's own ground, not in other people's houses.";
				return false;
			}
			if (Machine == null || !GameObject.Validate(Machine) || Machine.CurrentZone == null || Machine.CurrentZone.ZoneID != Z.ZoneID)
			{
				Failure = "There is nothing here to certify.";
				return false;
			}
			if (Machine.IsCreature)
			{
				Failure = "The keepers certify machines, not people.";
				return false;
			}
			if (Machine.GetIntProperty(CertifiedProperty) == 1)
			{
				Machine.SetIntProperty(CertifiedProperty, 0);
				MessageQueue.AddPlayerMessage("{{K|" + Machine.ShortDisplayName + " is taken off the grid of " + System.SeatName + ".}} It stands exactly where it stood.");
				KingdomLog.Log("salvage: decommissioned " + Machine.ShortDisplayName + " at " + System.SeatName);
				return true;
			}
			Inspect(Machine, out bool hazardous, out bool broken, out bool rusted, out bool understood, out int complexity, out int difficulty);
			KingdomSurvey survey = KingdomSurvey.Take(Z);
			KingdomSalvageRules.SalvageVerdict verdict = KingdomSalvageRules.Assess(hazardous, broken, rusted, understood, complexity, difficulty, survey.StoredWater, System.Population, out int waterCost, out int handsRequired);
			if (verdict != KingdomSalvageRules.SalvageVerdict.Certified)
			{
				Failure = RefusalMessage(verdict, Machine, System, waterCost, handsRequired, survey.StoredWater);
				return false;
			}
			survey.Consume(waterCost);
			Machine.SetIntProperty(CertifiedProperty, 1);
			string settler = (System.RosterNames.Count > 0) ? System.RosterNames.GetRandomElement() : "a settler";
			MessageQueue.AddPlayerMessage("{{G|" + Machine.ShortDisplayName + " is certified fit for the grid of " + System.SeatName + ".}} " + settler + " signs off on it.");
			KingdomChronicle.Record(System, Machine.ShortDisplayName + " was certified fit for the grid at " + System.KingdomDisplayName);
			System.RecordDeed(Machine.ShortDisplayName + " certified fit for the grid at " + System.KingdomDisplayName);
			KingdomLog.Log("salvage: certified " + Machine.ShortDisplayName + " cost=" + waterCost + " hands=" + handsRequired + " at " + System.SeatName);
			return true;
		}

		/// <summary>
		/// Reads the vanilla state a certification inspection cares about. Understanding
		/// requires both that the founder has actually identified the machine
		/// (<see cref="GameObject.Understood"/>) and that some tinker's schema for it exists at
		/// all: a <c>TinkerItem</c> that can be neither built nor disassembled is a piece of
		/// machinery nobody, anywhere, has ever taken apart, and the founder's own knowledge of
		/// what it does is not the settlement's knowledge of how it is built. Hazard is read
		/// from the engine's own <see cref="IsExplosiveEvent"/> query, plus a bare
		/// <c>FusionReactor</c> part directly &mdash; that part never answers
		/// <c>IsExplosiveEvent</c> itself, despite carrying an explode chance and force of its
		/// own.
		/// </summary>
		private static void Inspect(GameObject Machine, out bool Hazardous, out bool Broken, out bool Rusted, out bool Understood, out int Complexity, out int Difficulty)
		{
			Examiner examiner = Machine.GetPart<Examiner>();
			Complexity = (examiner != null) ? examiner.Complexity : 0;
			Difficulty = (examiner != null) ? examiner.Difficulty : 0;
			TinkerItem tinkerItem = Machine.GetPart<TinkerItem>();
			bool hasSchema = tinkerItem == null || tinkerItem.CanBuild || tinkerItem.CanDisassemble;
			Understood = Machine.Understood() && hasSchema;
			Broken = Machine.IsBroken();
			Rusted = Machine.IsRusted();
			Hazardous = IsExplosiveEvent.Check(Machine) || Machine.GetPart<FusionReactor>() != null;
		}

		/// <summary>Composes the player-facing sentence for a refusal, in the settlement's own
		/// voice. Never called for <see cref="KingdomSalvageRules.SalvageVerdict.Certified"/>.</summary>
		private static string RefusalMessage(KingdomSalvageRules.SalvageVerdict Verdict, GameObject Machine, KingdomSystem System, int WaterCost, int HandsRequired, int StoredWater)
		{
			string name = Machine.ShortDisplayName;
			switch (Verdict)
			{
			case KingdomSalvageRules.SalvageVerdict.RefusedHazardous:
				return "The keepers won't put " + name + " anywhere near the grid. {{r|Something in it wants to go off.}}";
			case KingdomSalvageRules.SalvageVerdict.RefusedBroken:
				return name + " is broken, and broken machinery joins nobody's grid. Mend it, then bring it back to the keepers.";
			case KingdomSalvageRules.SalvageVerdict.RefusedRusted:
				return name + " has rusted through and won't answer power the way it is. Scour it clean first.";
			case KingdomSalvageRules.SalvageVerdict.RefusedNotUnderstood:
				return "Nobody at " + System.SeatName + " knows what " + name + " does yet. Have it identified before the keepers will vouch for it.";
			case KingdomSalvageRules.SalvageVerdict.RefusedCannotAfford:
				return "Certifying " + name + " would cost {{C|" + WaterCost + "}} drams, and the stores hold only " + StoredWater + ". Bring more water, or dedicate what you have, and ask again.";
			case KingdomSalvageRules.SalvageVerdict.RefusedNoHands:
				return "Certifying " + name + " wants " + HandsRequired + " hands free to test it properly, and " + System.SeatName + " doesn't have them to spare yet.";
			default:
				return name + " cannot be certified right now.";
			}
		}
	}
}
