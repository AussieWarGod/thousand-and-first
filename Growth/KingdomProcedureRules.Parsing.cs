using System.Collections.Generic;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomProcedureRules
	{
		// --- The one sanctioned draw (DIVERSITY §3.4 hard rule 4's own exception; §3.7) --------

		private const int ConfessionRulesVersion = 1;

		private static readonly KernelSeed128 ConfessionSeed = default(KernelSeed128);

		private const string ConfessionEventStreamId = "taf:lab:confession:v1";

		private const uint ConfessionEventKind = 1u;

		private const uint ConfessionDrawIndex = 0u;

		/// <summary>
		/// Which limb the Chimeric Confession comes back with.
		/// <para>
		/// <b>The only die this system rolls, and it is disclosed before it is thrown.</b> Every
		/// other procedure is what the slate said, because a thing that cost a season and a fortune
		/// may not roll dice (DIVERSITY &sect;3.1's rejection of golem randomness). This one is the
		/// exception the doctrine names by hand: the confession is confessedly a gamble and is
		/// priced as one, and the slate says so in the founder's own language before they commit.
		/// </para>
		/// <para>
		/// Drawn through the settlement kernel rather than <c>Stat.Random</c>, so the same
		/// confession on the same save is the same limb after a reload &mdash; a gamble the founder
		/// takes once, not a gamble the save file re-takes every time it is opened.
		/// </para>
		/// </summary>
		/// <param name="SettlementId">The settlement's kernel id
		/// (<c>KingdomChronicle.SettlementId</c>).</param>
		/// <param name="Ordinal">The tick the confession was commissioned at.</param>
		/// <param name="CandidateCount">How many limbs the game's own chimera weighting offered.</param>
		/// <returns>An index into the candidates, or -1 when there was nothing to choose from.
		/// Falls back to the first candidate if the kernel refuses, which is a limb rather than a
		/// crash.</returns>
		public static int ChooseChimericSlot(string SettlementId, ulong Ordinal, int CandidateCount)
		{
			if (CandidateCount <= 0)
			{
				return -1;
			}
			if (CandidateCount == 1)
			{
				return 0;
			}
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(ConfessionRulesVersion, SettlementId, ConfessionEventStreamId,
				ConfessionEventKind, Ordinal, out key, out fault))
			{
				return 0;
			}
			ulong value;
			if (!CounterRandom.TryDrawBelow(ConfessionSeed, key, ConfessionDrawIndex, (ulong)CandidateCount, out value, out fault))
			{
				return 0;
			}
			return (int)value;
		}

		// --- Parsing (STANDARDS §6) ------------------------------------------------------------

		/// <summary>
		/// Reads one <c>&lt;procedure&gt;</c> element into a record.
		/// <para>
		/// A procedure is REFUSED whole on a fault, the way a research node is and unlike a building
		/// gate, and for the same reason: a gate restricts a design that exists either way, while a
		/// procedure whose slots or class cannot be read is a thing that would open a founder's body
		/// on a guess.
		/// </para>
		/// <para>
		/// The one fault that is a rule rather than a typo is the blocklist (Addendum 22 D1): a
		/// record granting a self-replication part, <c>Invisibility</c>, <c>WallWalker</c>,
		/// <c>Metamorphosis</c> or <c>OldElectricalGeneration</c> is refused by file and key rather
		/// than left as a convention somebody can forget. Boundary powers arrive as named
		/// procedures, one ruling each, or they do not arrive.
		/// </para>
		/// </summary>
		/// <param name="Error">Null on success; one sentence naming the key and the fault otherwise.</param>
		public static bool TryParseProcedureAttributes(string Key, string DisplayName, string Class, string Grants,
			string Slots, string SlotCategories, string Source, string Attach, string MinRung,
			string Cost, string Bits, string StaffDays, string Preserved, string Creeds, string Knowledge,
			string Magnitude, out LabProcedure Procedure, out string Error)
		{
			Procedure = null;
			string key = Fold(Key);
			if (key == null)
			{
				Error = "a <procedure> element carries no Key.";
				return false;
			}
			string grants = Trimmed(Grants);
			if (grants == null)
			{
				Error = "procedure " + key + ": Grants names no part class. A procedure grants a CLASS, never a creature.";
				return false;
			}
			if (Blocked(grants))
			{
				Error = "procedure " + key + ": Grants \"" + grants
					+ "\", which the blocklist holds (Addendum 22 D1). Boundary powers arrive as named procedures, one ruling each, or not at all.";
				return false;
			}
			LabClass cls;
			if (!TryParseClass(Class, out cls))
			{
				Error = "procedure " + key + ": Class \"" + Class + "\" is not I, II, III or IV.";
				return false;
			}
			LabSource source = LabSource.Part;
			if (!string.IsNullOrEmpty(Source) && !TryParseSource(Source, out source))
			{
				Error = "procedure " + key + ": Source \"" + Source + "\" is not part, limb or mutation.";
				return false;
			}
			LabAttach attach;
			if (!TryParseAttach(Attach, out attach))
			{
				Error = "procedure " + key + ": Attach \"" + Attach
					+ "\" is not body or weapon. A rider that only ever fires on a weapon is inert on a torso, so every record must say which it is.";
				return false;
			}
			string slots = Trimmed(Slots);
			if (slots == null)
			{
				Error = "procedure " + key + ": Slots names nowhere on a body to put it.";
				return false;
			}
			int rung = RungForClass(cls);
			if (!string.IsNullOrEmpty(MinRung) && (!int.TryParse(MinRung.Trim(), out rung) || rung < RungSlab || rung > RungTheatre))
			{
				Error = "procedure " + key + ": MinRung \"" + MinRung + "\" is not one of " + RungSlab + " to " + RungTheatre + ".";
				return false;
			}
			int cost = 0;
			if (!string.IsNullOrEmpty(Cost) && (!int.TryParse(Cost.Trim(), out cost) || cost < 0))
			{
				Error = "procedure " + key + ": Cost \"" + Cost + "\" is not a count of drams.";
				return false;
			}
			int staffDays = 1;
			if (!string.IsNullOrEmpty(StaffDays) && (!int.TryParse(StaffDays.Trim(), out staffDays) || staffDays < 1))
			{
				Error = "procedure " + key + ": StaffDays \"" + StaffDays + "\" is not a count of days of work.";
				return false;
			}
			int preserved = 1;
			if (!string.IsNullOrEmpty(Preserved) && (!int.TryParse(Preserved.Trim(), out preserved) || preserved < 0))
			{
				Error = "procedure " + key + ": Preserved \"" + Preserved + "\" is not a count of kept parts.";
				return false;
			}
			string magnitudeField;
			int low;
			int high;
			string bandError;
			if (!TryParseMagnitude(Magnitude, out magnitudeField, out low, out high, out bandError))
			{
				Error = "procedure " + key + ": Magnitude " + bandError;
				return false;
			}
			Procedure = new LabProcedure
			{
				Key = key,
				DisplayName = Trimmed(DisplayName),
				Class = cls,
				Grants = grants,
				Slots = slots,
				SlotCategories = Trimmed(SlotCategories),
				Source = source,
				Attach = attach,
				MinRung = rung,
				Cost = cost,
				Bits = Trimmed(Bits),
				StaffDays = staffDays,
				Preserved = preserved,
				Creeds = Trimmed(Creeds),
				Knowledge = Trimmed(Knowledge),
				Magnitude = Trimmed(Magnitude)
			};
			Error = null;
			return true;
		}

		/// <summary>The class ladder in the vocabulary the design doc writes it in, and in the
		/// numbers a hand-written file might use instead.</summary>
		public static bool TryParseClass(string Source, out LabClass Class)
		{
			Class = LabClass.Rider;
			switch (Fold(Source))
			{
			case "i":
			case "1":
			case "rider":
				Class = LabClass.Rider;
				return true;
			case "ii":
			case "2":
			case "defence":
			case "defense":
				Class = LabClass.Defence;
				return true;
			case "iii":
			case "3":
			case "limb":
				Class = LabClass.Limb;
				return true;
			case "iv":
			case "4":
			case "named":
				Class = LabClass.Named;
				return true;
			default:
				return false;
			}
		}

		public static bool TryParseSource(string Source, out LabSource Kind)
		{
			Kind = LabSource.Part;
			switch (Fold(Source))
			{
			case "part":
				Kind = LabSource.Part;
				return true;
			case "limb":
				Kind = LabSource.Limb;
				return true;
			case "mutation":
				Kind = LabSource.Mutation;
				return true;
			default:
				return false;
			}
		}

		/// <summary>
		/// The attach bit. Absent reads as <see cref="LabAttach.Body"/>, which is the safe default
		/// only because every record this mod ships states it outright and the validator says so
		/// about every record that does not.
		/// </summary>
		public static bool TryParseAttach(string Source, out LabAttach Attach)
		{
			Attach = LabAttach.Body;
			if (string.IsNullOrEmpty(Source) || Source.Trim().Length == 0)
			{
				return true;
			}
			switch (Fold(Source))
			{
			case "body":
			case "bearer":
				Attach = LabAttach.Body;
				return true;
			case "weapon":
			case "natural":
				Attach = LabAttach.Weapon;
				return true;
			default:
				return false;
			}
		}

	}
}
