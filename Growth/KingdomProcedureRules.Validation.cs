using System.Collections.Generic;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomProcedureRules
	{
		// --- The blocklist (Addendum 22 D1) ----------------------------------------------------

		/// <summary>
		/// Part and mutation classes no derived record may ever grant, whatever any file says.
		/// <para>
		/// The golem quest's own list (<c>D/XRL/World/Quests/GolemQuest/GolemAtzmusSelection.cs:21</c>)
		/// plus the precedent whitelist's <c>[Spicy]</c> block, whose own header calls it
		/// experimental and save-breaking, plus every self-replication class the census turned up.
		/// It is enforced at LOAD rather than at commission, so a third party's file that names one
		/// fails loudly on the day it ships rather than quietly on the day somebody clicks.
		/// </para>
		/// </summary>
		public static readonly string[] Blocklist = new string[19]
		{
			"Invisibility", "WallWalker", "Metamorphosis", "OldElectricalGeneration",
			"Reconstitution", "SplitOnDeath", "Cloneling", "Mimic", "MimicProperties",
			"Engulfing", "EngulfingDamage", "FugueOnStep", "StunningForceOnJump", "Twinner", "Triner",
			"Spawner", "Breeder", "CloneOnHit", "FabricateFromSelf"
		};

		/// <summary>Whether a class is on the blocklist. Case-insensitive, because a file that
		/// spells it in lower case is naming the same class.</summary>
		public static bool Blocked(string ClassName)
		{
			if (string.IsNullOrEmpty(ClassName))
			{
				return false;
			}
			string wanted = Fold(ClassName);
			for (int i = 0; i < Blocklist.Length; i++)
			{
				if (Fold(Blocklist[i]) == wanted)
				{
					return true;
				}
			}
			return false;
		}

		// --- Registry validation (STANDARDS §6, §9) --------------------------------------------

		/// <summary>
		/// What is wrong with a merged registry, said once at load. Nothing is unregistered: a
		/// record that is wrong about itself stays in the registry and is offered, which is the only
		/// shape a check on third-party content can honestly take. The checks are the ones no single
		/// record can see.
		/// </summary>
		/// <returns>One sentence per finding, in registry order; never null.</returns>
		public static List<string> Validate(IList<LabProcedure> Procedures)
		{
			List<string> findings = new List<string>();
			if (Procedures == null)
			{
				return findings;
			}
			for (int i = 0; i < Procedures.Count; i++)
			{
				LabProcedure procedure = Procedures[i];
				if (procedure == null || procedure.Key == null)
				{
					continue;
				}
				// Class IV is deliberately exempt. A named procedure's gate is AUTHORED, one ruling
				// each (DIVERSITY §3.7), and the four do not all sit at the same height: the Lantern
				// Rib is hall work at rung 2 because it does not change what a founder IS, only what
				// they are carrying and where. Flagging that would be flagging the design.
				if (procedure.Class != LabClass.Named && procedure.MinRung < RungForClass(procedure.Class))
				{
					findings.Add("procedure " + procedure.Key + " is Class " + Roman(procedure.Class) + " and sits at rung "
						+ procedure.MinRung + ", below the rung that class of work is done at.");
				}
				if (procedure.Source == LabSource.Limb && procedure.Class != LabClass.Limb && !procedure.IsNamed)
				{
					findings.Add("procedure " + procedure.Key + " takes a severed limb and is not Class III. A limb is grafted at the theatre or not at all.");
				}
				if (procedure.Attach == LabAttach.Weapon && procedure.Source != LabSource.Part)
				{
					findings.Add("procedure " + procedure.Key + " attaches to a natural weapon and does not grant a part. Only a part rides a weapon.");
				}
				if (procedure.Magnitude != null && procedure.Source != LabSource.Part)
				{
					findings.Add("procedure " + procedure.Key + " names a Magnitude band and does not grant a part. There is no field on a limb to band.");
				}
				for (int j = i + 1; j < Procedures.Count; j++)
				{
					if (Procedures[j] == null || Procedures[j].Key == null || Procedures[j].Grants != procedure.Grants)
					{
						continue;
					}
					// Two records over one class is the QB-10 shape and is lawful, but only when
					// something tells them apart: without bands on both, the cheaper one is simply
					// the better buy and the dearer one is a record nobody will ever pick.
					if (procedure.Magnitude == null || Procedures[j].Magnitude == null)
					{
						findings.Add("procedures " + procedure.Key + " and " + Procedures[j].Key + " both grant " + procedure.Grants
							+ " and at least one names no Magnitude band, so nothing tells the two apart at the slate.");
					}
				}
			}
			return findings;
		}

		/// <summary>The class as the design doc and the slate both write it.</summary>
		public static string Roman(LabClass Class)
		{
			switch (Class)
			{
			case LabClass.Rider:
				return "I";
			case LabClass.Defence:
				return "II";
			case LabClass.Limb:
				return "III";
			default:
				return "IV";
			}
		}

		// --- The words (STANDARDS 7b: every refusal names the fix) -----------------------------

		/// <summary>
		/// Why the hall will not do a thing, in the founder's own language.
		/// <para>
		/// Empty for <see cref="LabVerdict.Allowed"/>, and empty for
		/// <see cref="LabVerdict.RefusedUndiscovered"/> &mdash; the second deliberately, because
		/// telling a founder that something they have never heard of is refused would be telling
		/// them it exists, which is the one thing the visibility law forbids.
		/// </para>
		/// </summary>
		/// <param name="Verdict">What <see cref="Judge"/> answered.</param>
		/// <param name="Procedure">The record, for the things a refusal may name.</param>
		public static string RefusalLine(LabVerdict Verdict, LabProcedure Procedure)
		{
			string named = (Procedure == null) ? "it" : Procedure.Named;
			switch (Verdict)
			{
			case LabVerdict.RefusedNoSlot:
				return "There is nowhere on you to put " + named + ". A body is a finite thing, and yours has no "
					+ FirstSlot(Procedure) + ".";
			case LabVerdict.RefusedSlotTaken:
				return "Every place " + named + " could go is already spoken for. Have something taken off, and the hall can put this on.";
			case LabVerdict.RefusedCategory:
				return "You are not made of the kind of thing " + named + " is grafted to. The hall can open a body; it cannot change what a body is.";
			case LabVerdict.RefusedRung:
				return "The hall here is not built high enough for " + named + ". That is "
					+ RungName(Procedure == null ? RungTheatre : Procedure.MinRung) + " work.";
			case LabVerdict.RefusedNoWeapon:
				return "There is nothing on you there for " + named
					+ " to ride. It lives in a claw or a sting, not in the flesh behind one, so it wants a limb that already bites.";
			case LabVerdict.RefusedUnkept:
				return "The hall will not open a body for a thing that was not kept. The vat-house has no "
					+ ((Procedure == null) ? "source" : SourceWord(Procedure)) + " for " + named + ".";
			case LabVerdict.RefusedOnceEver:
				return "That was done to you once. It is not the kind of thing that is done twice.";
			case LabVerdict.RefusedMagnitude:
				return "What the vat-house is keeping is of the right kind and the wrong measure for " + named + ".";
			default:
				return "";
			}
		}

		/// <summary>The rung a founder would name it by.</summary>
		public static string RungName(int Rung)
		{
			switch (Rung)
			{
			case RungSlab:
				return "the slab's";
			case RungVat:
				return "the vat-house's";
			case RungHall:
				return "the grafting hall's";
			default:
				return "the chimeric theatre's";
			}
		}

		/// <summary>What the vat-house would be keeping, said as a founder would say it.</summary>
		public static string SourceWord(LabProcedure Procedure)
		{
			if (Procedure == null)
			{
				return "source";
			}
			switch (Procedure.Source)
			{
			case LabSource.Limb:
				return "kept limb";
			case LabSource.Mutation:
				return "kept gland";
			default:
				return "kept part";
			}
		}

		private static string FirstSlot(LabProcedure Procedure)
		{
			List<string> slots = SlotTypes(Procedure);
			return (slots.Count == 0) ? "such place" : slots[0];
		}

		// --- Small shared helpers ----------------------------------------------------------------

		/// <summary>A comma list, folded and trimmed, empties dropped. Never null.</summary>
		public static List<string> Split(string Source)
		{
			List<string> parts = new List<string>();
			if (string.IsNullOrEmpty(Source))
			{
				return parts;
			}
			string[] raw = Source.Split(',');
			for (int i = 0; i < raw.Length; i++)
			{
				string one = Fold(raw[i]);
				if (one != null && !parts.Contains(one))
				{
					parts.Add(one);
				}
			}
			return parts;
		}

		/// <summary>A comma list, trimmed only, empties dropped. Never null. See
		/// <see cref="SlotCategoryNames"/> for why the un-folded variant exists.</summary>
		public static List<string> SplitTrimmed(string Source)
		{
			List<string> parts = new List<string>();
			if (string.IsNullOrEmpty(Source))
			{
				return parts;
			}
			string[] raw = Source.Split(',');
			for (int i = 0; i < raw.Length; i++)
			{
				string one = Trimmed(raw[i]);
				if (one != null && !parts.Contains(one))
				{
					parts.Add(one);
				}
			}
			return parts;
		}

		private static int Clamp(int Value, int Low, int High)
		{
			if (Value < Low)
			{
				return Low;
			}
			return (Value > High) ? High : Value;
		}

		private static string Fold(string Value)
		{
			if (Value == null)
			{
				return null;
			}
			string folded = Value.Trim().ToLowerInvariant();
			return (folded.Length == 0) ? null : folded;
		}

		private static string Trimmed(string Value)
		{
			if (Value == null)
			{
				return null;
			}
			string trimmed = Value.Trim();
			return (trimmed.Length == 0) ? null : trimmed;
		}
	}
}
