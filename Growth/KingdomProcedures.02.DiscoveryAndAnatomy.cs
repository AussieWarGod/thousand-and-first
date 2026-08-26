using System;
using System.Collections.Generic;
using System.Reflection;

using Qud.API;
using XRL;
using XRL.World;
using XRL.World.Anatomy;

namespace ThousandAndFirst
{
	public static partial class KingdomProcedures
	{
		/// <summary>
		/// Whether this founder may see a procedure at all.
		/// <para>
		/// <b>Every surface asks this before it draws a row</b>, which is what makes "cannot have
		/// it" and "have never heard of it" the same absence of a row rather than two renderings. An
		/// ordinary record is always visible; a named one is invisible until it is found in the
		/// world (Addendum 14 at full strength, Addendum 20's hidden clause).
		/// </para>
		/// </summary>
		public static bool Discovered(LabProcedure Procedure)
		{
			if (Procedure == null || !Enabled)
			{
				return false;
			}
			if (!Procedure.IsNamed)
			{
				return true;
			}
			FileNotes();
			string id = NoteId(Procedure.Key);
			return id != null && JournalAPI.HasNote(id);
		}

		/// <summary>
		/// Tells the founder a named procedure exists, and where they heard it. Vanilla stamps the
		/// provenance on the entry itself, so the chronicle line writes itself.
		/// </summary>
		/// <returns>True when this call is what revealed it.</returns>
		public static bool Reveal(string Key, string LearnedFrom)
		{
			LabProcedure procedure;
			if (!Enabled || !TryGet(Key, out procedure) || !procedure.IsNamed || Discovered(procedure))
			{
				return false;
			}
			string id = NoteId(procedure.Key);
			if (id == null || !JournalAPI.TryRevealNote(id, LearnedFrom))
			{
				return false;
			}
			KingdomLog.Log("lab: found " + procedure.Key + ((LearnedFrom == null) ? "" : (" (" + LearnedFrom + ")")));
			return true;
		}

		// ==================================================================================
		// Reading a real body
		// ==================================================================================

		/// <summary>
		/// The founder's own anatomy, in the vocabulary the rules judge in.
		/// <para>
		/// <b>This is the rationing mechanism and there is no other one.</b> A procedure's
		/// <c>Slots</c> is checked against what this founder actually has, not against a table, so a
		/// True Kin, a robot player and a slime player each get a different legal set for free
		/// &mdash; derived, with no genotype list anywhere in this codebase (DIVERSITY &sect;3.4
		/// hard rules 2 and 3).
		/// </para>
		/// <para>
		/// Anatomy order is kept, because the founder reads their own body the way the game lists it
		/// and the slate must say it back the same way.
		/// </para>
		/// </summary>
		/// <param name="Who">The founder. Null or bodiless reads as an empty anatomy, which refuses
		/// everything by name rather than throwing.</param>
		/// <param name="Names">Filled with each slot's name as the founder would say it, index for
		/// index with the returned list. May be null.</param>
		public static List<LabSlot> Census(GameObject Who, List<string> Names = null)
		{
			List<LabSlot> anatomy = new List<LabSlot>();
			XRL.World.Parts.Body body = Who?.Body;
			if (body == null)
			{
				return anatomy;
			}
			List<BodyPart> parts = body.GetParts();
			if (parts == null)
			{
				return anatomy;
			}
			for (int i = 0; i < parts.Count; i++)
			{
				BodyPart part = parts[i];
				if (part == null || part.Abstract)
				{
					continue;
				}
				anatomy.Add(new LabSlot(part.Type, part.Category, part.Extrinsic,
					GameObject.Validate(part.DefaultBehavior), part.Manager));
				Names?.Add(part.GetOrdinalName());
			}
			return anatomy;
		}

		/// <summary>
		/// A record's <c>SlotCategories</c> as engine codes.
		/// <para>
		/// Resolved through <c>BodyPartCategory</c>'s own name table rather than a table of ours
		/// (<c>D/XRL/World/Anatomy/BodyPartCategory.cs:104-165</c>), which is why a modded category
		/// would work here the day the engine had one. A name the engine does not know resolves to
		/// zero and is DROPPED with a logged reason rather than silently admitting everything
		/// &mdash; hostile-input discipline, and the difference between a typo and an open door.
		/// </para>
		/// </summary>
		/// <returns>Empty when the record names none, which admits any live category.</returns>
		public static List<int> Categories(LabProcedure Procedure)
		{
			List<int> codes = new List<int>();
			List<string> names = KingdomProcedureRules.SlotCategoryNames(Procedure);
			for (int i = 0; i < names.Count; i++)
			{
				int code = BodyPartCategory.GetCodeIfExists(names[i]);
				if (code <= 0)
				{
					KingdomLog.Log("KingdomProcedures: procedure " + Procedure.Key + " names category \"" + names[i]
						+ "\", which the engine does not know. Dropped.");
					continue;
				}
				if (!codes.Contains(code))
				{
					codes.Add(code);
				}
			}
			return codes;
		}

		/// <summary>Live and detached anatomy are one identity domain.</summary>
		internal static List<BodyPart> AllBodyParts(GameObject Who)
		{
			List<BodyPart> result = new List<BodyPart>();
			List<BodyPart> live = Who?.Body?.GetParts();
			for (int i = 0; live != null && i < live.Count; i++)
			{
				if (live[i] != null && !ContainsReference(result, live[i])) result.Add(live[i]);
			}
			List<XRL.World.Parts.Body.DismemberedPart> detached = Who?.Body?.DismemberedParts;
			for (int i = 0; detached != null && i < detached.Count; i++)
			{
				BodyPart part = detached[i]?.Part;
				if (part != null && !ContainsReference(result, part)) result.Add(part);
			}
			return result;
		}

		internal static BodyPart ExactBodyPart(GameObject Who, int BodyPartId)
		{
			return (BodyPartId > 0) ? Who?.Body?.GetPartByID(BodyPartId, EvenIfDismembered: true) : null;
		}

		/// <summary>Exact identity in the live body tree. Detached anatomy is deliberately excluded.</summary>
		internal static BodyPart ExactLiveBodyPart(GameObject Who, int BodyPartId)
		{
			if (BodyPartId <= 0 || Who?.Body == null) return null;
			BodyPart candidate = Who.Body.GetPartByID(BodyPartId, EvenIfDismembered: false);
			return BodyOwnsLivePart(Who, candidate) ? candidate : null;
		}

		internal static bool BodyOwnsLivePart(GameObject Who, BodyPart Candidate)
		{
			return Who?.Body != null && Candidate != null
				&& ReferenceEquals(Candidate.ParentBody, Who.Body)
				&& ContainsReference(Who.Body.GetParts(), Candidate);
		}

		internal static bool BodyOwnsPart(GameObject Who, BodyPart Candidate)
		{
			if (Who?.Body == null || Candidate == null || !ReferenceEquals(Candidate.ParentBody, Who.Body))
			{
				return false;
			}
			return ContainsReference(AllBodyParts(Who), Candidate);
		}

		private static bool ContainsReference(IList<BodyPart> Parts, BodyPart Candidate)
		{
			for (int i = 0; Parts != null && i < Parts.Count; i++)
			{
				if (ReferenceEquals(Parts[i], Candidate)) return true;
			}
			return false;
		}
	}
}
