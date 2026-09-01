using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The settlement's answer to a building the founder raised without it: recognises a
	/// structure the founder already built as serving a civic role, or says plainly what it is
	/// missing. Follows the <see cref="KingdomSalvage"/>/<c>DedicateVessel</c> idiom &mdash; a
	/// single call that does its own eligibility check and its own success messaging, surfacing
	/// only a decline through <c>Failure</c>. A refusal changes nothing: the structure stays
	/// exactly where the founder put it, untouched. Adoption is a MARK, never a transfer, and it
	/// is always reversible through <see cref="Release"/> &mdash; nothing the founder built is
	/// ever consumed, moved, or destroyed by adopting or releasing it.
	/// </summary>
	/// <remarks>
	/// This checks the requirements of the SPACE, never who made it. A Hearthpyre house, a
	/// cleared vanilla ruin, and four walls and a door laid up by hand are read exactly alike;
	/// nothing here so much as knows whether Hearthpyre is installed. See
	/// <see cref="KingdomAdoptRules"/> for the pure eligibility math this reads real objects and
	/// cells into.
	/// </remarks>
	public static partial class KingdomAdopt
	{
		/// <summary>Shared with every other kind of raised building &mdash; a commissioned work,
		/// an adopted one, and a previously-adopted one all read as "built" alike. Set by this
		/// file only on a structure that did not already carry it (see
		/// <see cref="KingdomAdoptRules.AdoptionVerdict.RefusedAlreadyServing"/>).</summary>
		public const string BuiltProperty = "KingdomBuilt";

		/// <summary>Set on a structure only by this file, and cleared only by
		/// <see cref="Release"/>. Distinguishes an adopted structure from a commissioned one so
		/// releasing an adoption can never touch a building the settlement actually raised
		/// itself.</summary>
		public const string AdoptedProperty = "KingdomAdopted";

		/// <summary>The <c>BuildEntry.Key</c> a structure was adopted as, so
		/// <see cref="Release"/> can name what is being given up and this file never has to
		/// re-derive it from anything that might have changed since.</summary>
		public const string AdoptedKeyProperty = "KingdomAdoptedKey";

		/// <summary>
		/// Which single property (<see cref="StoresProperty"/> or <see cref="LarderProperty"/>)
		/// this file itself switched on for a <see cref="KingdomAdoptRules.RoleKind.Storage"/>
		/// adoption, or empty for every other role. A founder can dedicate a vessel through the
		/// Charter's own dedication action either before or after adopting it as storage;
		/// releasing the adoption must undo only what the adoption itself set, never a
		/// dedication the founder made independently and would expect to survive.
		/// </summary>
		public const string AdoptedMarkProperty = "KingdomAdoptedMark";

		public const string StoresProperty = "KingdomStores";

		public const string LarderProperty = "KingdomLarder";

		// Shared work-state literals retained here for existing lab/research/visual consumers.
		// Adoption never writes either property; an inert designation marker grants no work state.
		public const string StaffNeededProperty = "KingdomStaffNeeded";

		public const string HandCrankedProperty = "KingdomHandCranked";

		/// <summary>
		/// The marker a <see cref="KingdomAdoptRules.RoleKind.Work"/> adoption sets down. A work
		/// role has no single existing object to mark &mdash; a shrine, a workshop, a charging
		/// post are rooms, not things &mdash; so adoption places one small, inert object inside
		/// the room to carry the settlement's mark, the literal "marker down in a building" the
		/// design calls for. It exists only to carry the mark: <see cref="Release"/> destroys it
		/// outright rather than leaving an empty plaque behind, because it was never something
		/// the founder made.
		/// </summary>
		public const string WorkMarkerBlueprint = "r_KingdomAdoptionMarker";

		/// <summary>
		/// Adopts one exact vessel or larder into a
		/// <see cref="KingdomAdoptRules.RoleKind.Storage"/> role. Housing and work are spatial
		/// designations, independent of any one furnishing, and therefore use
		/// <see cref="AdoptWork"/> instead.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone the candidate stands in; must be the kingdom's own claimed
		/// ground.</param>
		/// <param name="Candidate">The object to adopt.</param>
		/// <param name="Key">A <c>KingdomBuildings</c> registry key naming what type of building
		/// this counts as; its <c>Category</c> decides which test applies.</param>
		/// <param name="Failure">Set to a player-facing reason when this returns false. Nothing
		/// is spent or marked when it does.</param>
		/// <returns>True once the candidate has actually been marked.</returns>
		public static bool AdoptExisting(KingdomSystem System, Zone Z, GameObject Candidate, string Key, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A building is adopted on the kingdom's own ground, not in other people's houses.";
				return false;
			}
			if (Candidate == null || !GameObject.Validate(Candidate) || Candidate.IsCreature
				|| Candidate.IsPlayer() || Candidate.CurrentZone == null
				|| Candidate.CurrentCell == null || Candidate.CurrentZone.ZoneID != Z.ZoneID)
			{
				Failure = "There is nothing here to adopt.";
				return false;
			}
			if (!KingdomData.TryGetBuilding(Key, out KingdomRules.BuildEntry entry))
			{
				Failure = "No such design.";
				return false;
			}
			KingdomAdoptRules.RoleKind role = KingdomAdoptRules.ClassifyRole(entry.Category);
			if (!entry.Adoptable)
			{
				Failure = "A " + entry.Name + " needs its authored construction and cannot be assigned to a player-built object.";
				return false;
			}
			if (!KingdomPlots.TryGetSpec(entry.Key, out KingdomPlotRules.PlotSpec spec)
				|| !KingdomAdoptabilityRules.TryClassify(entry.Key, entry.Category,
					spec.Size, spec.Open, out KingdomAdoptionTargetKind target, out Failure))
			{
				if (Failure == null) Failure = "That design has no exact adoption geometry.";
				return false;
			}
			if (!KingdomZoning.Permits(System, Z.ZoneID, entry, out Failure)) return false;
			if (target != KingdomAdoptionTargetKind.Larder
				|| role != KingdomAdoptRules.RoleKind.Storage)
			{
				Failure = "A " + entry.Name + " is adopted as the exact room around you, not as one piece of furniture.";
				return false;
			}
			RecoverPending(Candidate);
			if (!GameObject.Validate(Candidate))
			{
				Failure = "The candidate disappeared during adoption recovery."; return false;
			}
			bool alreadyServing = Candidate.GetIntProperty(BuiltProperty) == 1;
			bool belowStage = System.Stage < entry.MinStage;
			LiquidVolume liquidVolume = Candidate.GetPart<LiquidVolume>();
			bool isLarder = liquidVolume == null && Candidate.Inventory != null;
			if (!KingdomAdoptabilityRules.CandidateMatches(target, liquidVolume != null,
				Candidate.Inventory != null))
			{
				Failure = "A larder designation needs one exact dry container."; return false;
			}
			KingdomAdoptRules.AdoptionVerdict verdict = KingdomAdoptRules.Assess(role,
				alreadyServing, belowStage, isLarder, default);
			if (verdict != KingdomAdoptRules.AdoptionVerdict.Adopted)
			{
				Failure = RefusalMessage(verdict, entry, Candidate, System);
				return false;
			}
			if (!ContainerIsUnclaimed(Z, Candidate.CurrentCell, out Failure)) return false;
			if (!BeginPending(Candidate, Key, false, out Failure)) return false;
			KingdomAdoptionDesignationReceipt receipt;
			ArchitecturePoint[] cell = { new ArchitecturePoint(Candidate.CurrentCell.X,
				Candidate.CurrentCell.Y) };
			if (!KingdomAdoptionDesignation.TryStampContainer(Candidate, Z, Key,
				out receipt, out Failure)
				|| !AdvancePending(Candidate, Key, 2, out Failure)
				|| !KingdomPlots.StampAdoptedExact(Candidate, entry, cell)
				|| !AdvancePending(Candidate, Key, 3, out Failure)
				|| !KingdomZoning.Permits(System, Z.ZoneID, entry, out Failure)
				|| !ContainerIsUnclaimed(Z, Candidate.CurrentCell, out Failure)
				|| !AdvancePending(Candidate, Key, 4, out Failure))
			{
				RollbackPending(Candidate);
				if (Failure == null) Failure = "The container's exact designation could not be recorded.";
				return false;
			}
			string mark = "";
			if (role == KingdomAdoptRules.RoleKind.Storage)
			{
				string storageProperty = LarderProperty;
				if (Candidate.GetIntProperty(storageProperty) != 1)
				{
					// Only track it as ours to undo if it was not already set - a vessel the
					// founder dedicated through the Charter before adopting it keeps that
					// dedication when the adoption is later released.
					mark = storageProperty;
				}
			}
			if (!PrepareStorageMark(Candidate, Key, mark, out Failure)
				|| !FinalizePending(Candidate, Key, mark, out Failure))
			{
				RollbackPending(Candidate); return false;
			}
			AnnounceAdoption(System, entry, Candidate);
			return true;
		}

		private static bool ContainerIsUnclaimed(Zone Z, Cell Cell, out string Failure)
		{
			Failure = null;
			if (Z == null || Cell == null || Cell.ParentZone != Z)
			{
				Failure = "The storage candidate has no exact ground."; return false;
			}
			if (!KingdomDesignationIndex.TryActiveZone(Z, out KingdomDesignationIndex index,
				out Failure)) return false;
			if (index.Containing(Cell.X, Cell.Y, KingdomBenefitScope.Plot).Count > 0)
			{
				Failure = "This storage facility overlaps another exact building designation.";
				return false;
			}
			return true;
		}
	}
}
