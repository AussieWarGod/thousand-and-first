using System;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_KingdomScaffold
	{
		public static bool TellCompletion(KingdomSystem System, GameObject Successor,
			KingdomConstructionJob Job)
		{
			Zone zone = GameObject.Validate(Successor) ? Successor.CurrentZone : null;
			Cell cell = Successor?.CurrentCell;
			KingdomRules.BuildEntry entry;
			if (!GameObject.Validate(Successor) || Job == null || zone == null || cell == null
				|| Job.Phase != KingdomConstructionPhase.Complete
				|| !KingdomConstruction.Owns(System, zone, Job)
				|| cell != zone.GetCell(Job.X, Job.Y)
				|| Successor.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1
				|| Successor.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Job.TargetKey
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out entry)
				|| Successor.Blueprint != entry.Blueprint
				|| !KingdomConstruction.HasReceipt(Successor, Job)
				|| !HasRemovalProof(Successor, Job.SubjectId)
				|| !KingdomConstruction.IsCurrent(Job)) return false;
			int told = Successor.GetIntProperty(TellingProperty);
			if (told != 0 && told != 1)
			{
				KingdomConstructionJob corrupt = Job;
				KingdomConstruction.Quarantine(ref corrupt,
					"The construction telling flag is not an exact boolean.");
				return false;
			}
			if (told == 1
				&& Job.Outbox != null && KingdomConstructionRules.OutboxSettled(Job.Outbox))
			{
				KingdomConstructionJob closed = Job;
				return closed.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled
					|| KingdomConstruction.UpdatePhysical(ref closed,
						KingdomPhysicalPhase.EffectsSettled, closed.PhysicalIndex,
						closed.PhysicalAmount, closed.PhysicalSpilled, closed.PhysicalItemId,
						closed.PhysicalDestinationId, closed.PhysicalReceipt);
			}
			string displayName = Successor.GetStringProperty(CompletionNameProperty)
				?? Successor.ShortDisplayName ?? "structure";
			long tick;
			if (!long.TryParse(Successor.GetStringProperty(CompletionTickProperty),
				NumberStyles.Integer, CultureInfo.InvariantCulture, out tick)) tick = Job.DueTick;
			KingdomConstructionJob telling = Job;
			if (telling.PhysicalPhase != KingdomPhysicalPhase.EffectsPending
				&& !KingdomConstruction.UpdatePhysical(ref telling,
					KingdomPhysicalPhase.EffectsPending, telling.PhysicalIndex,
					telling.PhysicalAmount, telling.PhysicalSpilled, telling.PhysicalItemId,
					telling.PhysicalDestinationId, telling.PhysicalReceipt)) return false;
			if (!KingdomCeremony.EnsureBuildingRaised(System, cell, displayName, tick,
				Successor.GetStringProperty(CompletionPlanProperty), ref telling)) return false;
			GameObject exactSuccessor;
			if (!KingdomConstruction.Owns(System, zone, telling)
				|| !KingdomConstruction.IsCurrent(telling)
				|| KingdomConstruction.FindExactId(zone, telling.OutputId, out exactSuccessor)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactSuccessor, Successor)
				|| !IsExactSuccessor(Successor, zone, cell, telling, entry.Blueprint)
				|| !HasRemovalProof(Successor, telling.SubjectId)) return false;
			Successor.SetIntProperty(TellingProperty, 1);
			if (Successor.GetIntProperty(TellingProperty) != 1) return false;
			if (!KingdomConstruction.UpdatePhysical(ref telling,
				KingdomPhysicalPhase.EffectsSettled, telling.PhysicalIndex,
				telling.PhysicalAmount, telling.PhysicalSpilled, telling.PhysicalItemId,
				telling.PhysicalDestinationId, telling.PhysicalReceipt)) return false;
			KingdomLog.Log("scaffold complete: " + displayName + " (" + Successor.Blueprint + ")");
			return true;
		}

		/// <summary>
		/// How fast this frame is rising right now, 0 to 100.
		/// <para>
		/// Founding is the one exemption: a frame raised before there is a settlement is raised
		/// by the founder's own hands, and there is no roster to read. Everything after that is
		/// read off the settlement &mdash; whoever the water detail and the works left over.
		/// </para>
		/// </summary>
		private int EffectivenessOf(out int FreeHands, out KingdomSystem System,
			out bool Selected)
		{
			FreeHands = 0;
			Selected = false;
			System = The.Game.RequireSystem<KingdomSystem>();
			return KingdomConstructionPresence.EffectivenessOf(ParentObject, System,
				out FreeHands, out Selected);
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

		private void CompleteLegacy()
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
			if (cell == null)
			{
				return;
			}
			GameObject gameObject = null;
			string legacySuccessorId = ParentObject.GetStringProperty(LegacySuccessorIdProperty);
			if (!string.IsNullOrEmpty(legacySuccessorId))
			{
				foreach (GameObject item in cell.GetObjects())
				{
					if (GameObject.Validate(item) && item.ID == legacySuccessorId
						&& item.Blueprint == blueprint)
					{
						gameObject = item;
						break;
					}
				}
				// A known successor moved or changed. Freeze; never guess by creating a duplicate.
				if (gameObject == null) return;
			}
			if (gameObject == null)
			{
				gameObject = GameObject.Create(blueprint);
				if (gameObject == null) return;
				ParentObject.SetStringProperty(LegacySuccessorIdProperty, gameObject.ID);
				GameObject accepted = null;
				try { accepted = cell.AddObject(gameObject); }
				finally
				{
					KingdomSurvey.ObserveAddResultInActive(cell.ParentZone, gameObject, accepted);
				}
				if (!ReferenceEquals(accepted, gameObject)) return;
			}
			if (gameObject.CurrentCell != cell || gameObject.Blueprint != blueprint)
			{
				return;
			}
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
			if (ParentObject.GetIntProperty(KingdomPlots.FrontierWorkProperty) == 1)
				gameObject.SetIntProperty(KingdomPlots.FrontierWorkProperty, 1);
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
			if (gameObject.CurrentCell != cell || gameObject.Blueprint != blueprint
				|| gameObject.GetIntProperty("KingdomBuilt") != 1) return;
			KingdomSurvey.ObserveChangedInActive(cell.ParentZone, gameObject);
			bool removed;
			try { removed = ParentObject.Destroy(null, Silent: true); }
			finally { KingdomSurvey.ObserveCurrentTopologyInActive(cell.ParentZone, ParentObject); }
			if (!removed || GameObject.Validate(ParentObject)) return;
			TargetBlueprint = null;
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
		}	}
}
