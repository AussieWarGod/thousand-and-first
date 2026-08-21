using System;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently.
namespace XRL.World.Parts
{
	/// <summary>
	/// A commissioned building part-way up: the frame that stands on the ground between the
	/// order and the raising.
	/// <para>
	/// <b>It rises on labour, not on the calendar</b> (BUILDING-CATALOGUE-BRIEF.md Addendum 8
	/// clause 2, and the author's ruling that a scaffold nobody works on does not rise). The
	/// duration a design authors in <c>BuildTicks</c> is what a properly-crewed settlement takes
	/// &mdash; <see cref="KingdomRules.RaisingHandsWanted"/> free pairs of hands &mdash; and it
	/// is banked once into <see cref="RemainingTicks"/> the first time this frame is looked at.
	/// After that, every stretch of elapsed time buys labour ticks at the pace the settlement's
	/// spare hands can actually manage (<see cref="KingdomRules.RaisingEffectiveness"/>), and a
	/// settlement with nobody free raises nothing at all, however long the founder is away.
	/// </para>
	/// <para>
	/// Idle time is SPENT, never banked: <see cref="LastWorkedTick"/> advances whether or not
	/// anyone stood here, exactly as an unstaffed yard's day budget does. A settlement that
	/// emptied out and refilled does not get the empty months back as a burst of building. And
	/// because a shortfall is a thing the founder can act on, it says so once and unsays itself
	/// the moment the crew is whole again (STANDARDS 7b).
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomScaffold : IPart
	{
		public string TargetBlueprint;

		public string TargetDisplayName;

		/// <summary>
		/// Stamped by every commissioning path as the tick this would be finished at if it were
		/// fully crewed from the moment it was ordered, and read once at that value to bank the
		/// authored labour into <see cref="RemainingTicks"/>. Restamped, when the work actually
		/// runs out, to the tick it ACTUALLY ran out at &mdash; which is what the raising
		/// ceremony needs to know whether the founder was standing there for it
		/// (<c>KingdomCeremonyRules.IsAttended</c>). A frame that finished halfway through an
		/// absence is told in the homecoming, exactly as before; one that finishes under the
		/// founder's eye gathers the crew.
		/// </summary>
		public long CompleteTick;

		/// <summary>Labour ticks left to raise this. Zero before the frame has been looked at
		/// once; derived then from <see cref="CompleteTick"/>, so no commissioning path has to
		/// know that raising takes hands.</summary>
		public long RemainingTicks;

		/// <summary>Tick labour was last charged against this frame, or 0 before the first
		/// look.</summary>
		public long LastWorkedTick;

		/// <summary>Whether the founder has already been told this raising is short-handed, so
		/// the reason is given once per stall rather than every turn (STANDARDS 7b).</summary>
		public bool ShortfallSaid;

		public int StaffNeeded;

		public bool ThresholdManning;

		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			if (TargetBlueprint != null)
			{
				Raise(TimeTick);
			}
			base.TurnTick(TimeTick, Amount);
		}

		/// <summary>
		/// Charges the labour the settlement's spare hands did on this frame since it was last
		/// looked at, and finishes it when the work runs out.
		/// <para>
		/// A turn tick only fires in an active zone, so an absence arrives here as one long
		/// stretch resolved at the moment of awareness &mdash; the lazy catch-up the whole mod
		/// keeps. The crew is sampled once, now, because now is the only honest reading there
		/// is: nobody recorded who was standing in an unwatched city.
		/// </para>
		/// </summary>
		private void Raise(long TimeTick)
		{
			if (RemainingTicks <= 0 && LastWorkedTick <= 0)
			{
				long authored = CompleteTick - TimeTick;
				RemainingTicks = (authored > 0) ? authored : 1L;
				LastWorkedTick = TimeTick;
				return;
			}
			long previous = LastWorkedTick;
			long elapsed = TimeTick - previous;
			if (elapsed <= 0)
			{
				return;
			}
			LastWorkedTick = TimeTick;
			int effectiveness = EffectivenessOf(out var freeHands, out var system);
			Say(system, freeHands);
			long worked = KingdomRules.LabouredTicks(elapsed, effectiveness);
			if (worked <= 0 || RemainingTicks <= 0)
			{
				return;
			}
			if (worked < RemainingTicks)
			{
				RemainingTicks -= worked;
				return;
			}
			// The work ran out somewhere inside this stretch, and WHERE matters: it decides
			// whether this was a raising the founder attended or one the homecoming reports.
			// Ticks needed at the pace just measured, rounded up, laid back down from the last
			// stamp.
			long spent = (effectiveness >= 100) ? RemainingTicks : (RemainingTicks * 100L + effectiveness - 1L) / effectiveness;
			long finished = previous + spent;
			CompleteTick = (finished > TimeTick || finished < previous) ? TimeTick : finished;
			RemainingTicks = 0;
			Complete();
		}

		/// <summary>
		/// How fast this frame is rising right now, 0 to 100.
		/// <para>
		/// Founding is the one exemption: a frame raised before there is a settlement is raised
		/// by the founder's own hands, and there is no roster to read. Everything after that is
		/// read off the settlement &mdash; whoever the water detail and the works left over.
		/// </para>
		/// </summary>
		private int EffectivenessOf(out int FreeHands, out KingdomSystem System)
		{
			FreeHands = 0;
			System = The.Game.RequireSystem<KingdomSystem>();
			if (System == null || !System.Founded)
			{
				return 100;
			}
			FreeHands = KingdomMaterialRules.FreeHands(System.Population, System.AssignedCrew);
			return KingdomRules.RaisingEffectiveness(FreeHands);
		}

		/// <summary>Names a short-handed raising once, and unsays it the moment the crew is
		/// whole (STANDARDS 7b).</summary>
		private void Say(KingdomSystem System, int FreeHands)
		{
			if (System == null || !System.Founded)
			{
				return;
			}
			string line = KingdomRules.RaisingShortfallLine(TargetDisplayName ?? "structure", FreeHands);
			if (line == null)
			{
				ShortfallSaid = false;
				return;
			}
			if (ShortfallSaid)
			{
				return;
			}
			ShortfallSaid = true;
			System.Ledger.Note("{{r|" + line + "}}");
		}

		/// <summary>
		/// The one blueprint the settlement dedicates to its own food stores on completion.
		/// Named here rather than inferred, so a future container-bearing building does not
		/// quietly become a pantry.
		/// </summary>
		public const string LarderBlueprint = "r_KingdomLarder";

		public void Complete()
		{
			Cell cell = ParentObject.CurrentCell;
			string blueprint = TargetBlueprint;
			string displayName = TargetDisplayName ?? "structure";
			int defence = ParentObject.GetIntProperty("KingdomDefencePending");
			// Read before the scaffold is taken down, not after: what the founder chose when they
			// commissioned this rides on the scaffold object, and the scaffold is about to stop
			// being a thing to read from.
			string skinColorString = ParentObject.GetStringProperty(KingdomDesign.StagedColorStringProperty);
			string skinDetailColor = ParentObject.GetStringProperty(KingdomDesign.StagedDetailColorProperty);
			string skinRenderString = ParentObject.GetStringProperty(KingdomDesign.StagedRenderStringProperty);
			string skinTile = ParentObject.GetStringProperty(KingdomDesign.StagedTileProperty);
			// Which registry entry ordered this, so a work never has to be recognised by reading
			// its blueprint back against a catalog two designs may share.
			string buildKey = ParentObject.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			string planQuote = ParentObject.GetStringProperty(KingdomCeremony.SurveyorsPlanProperty);
			TargetBlueprint = null;
			if (cell == null)
			{
				return;
			}
			GameObject gameObject = GameObject.Create(blueprint);
			if (gameObject == null)
			{
				return;
			}
			ParentObject.Destroy(null, Silent: true);
			cell.AddObject(gameObject);
			KingdomDesign.ApplyRenderOverrides(gameObject, skinColorString, skinDetailColor, skinRenderString, skinTile);
			if (gameObject.GetPart<XRL.World.Parts.LiquidVolume>() != null)
			{
				gameObject.SetIntProperty("KingdomStores", 1);
			}
			else if (blueprint == LarderBlueprint)
			{
				// A civic larder the settlement paid for is the settlement's, the same way a
				// commissioned cask rack is. Keyed on the blueprint rather than "has an
				// Inventory and no LiquidVolume", because the charging post carries a
				// Container/Inventory pair too and is not a pantry.
				gameObject.SetIntProperty("KingdomLarder", 1);
			}
			gameObject.SetIntProperty("KingdomBuilt", 1);
			if (!string.IsNullOrEmpty(buildKey))
			{
				gameObject.SetStringProperty(KingdomUpgrade.BuildKeyProperty, buildKey);
			}
			if (defence > 0)
			{
				gameObject.SetIntProperty("KingdomDefence", defence);
			}
			if (StaffNeeded > 0)
			{
				gameObject.SetIntProperty("KingdomStaffNeeded", StaffNeeded);
				if (ThresholdManning)
				{
					gameObject.SetIntProperty("KingdomThresholdManning", 1);
				}
				if (gameObject.GetPart<XRL.World.Parts.Capacitor>() != null)
				{
					gameObject.SetIntProperty("KingdomHandCranked", 1);
				}
			}
			gameObject.MakeActive();
			KingdomLog.Log("scaffold complete: " + displayName + " (" + blueprint + ") at " + cell.X + "," + cell.Y);
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (system.Founded)
			{
				KingdomCeremony.OnBuildingRaised(system, cell, displayName, CompleteTick, planQuote);
			}
			else
			{
				MessageQueue.AddPlayerMessage("{{G|The " + displayName + " is complete.}}");
			}
		}
	}
}
