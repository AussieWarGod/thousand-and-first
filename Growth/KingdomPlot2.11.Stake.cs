using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		private static GameObject Stake(KingdomSystem System, Zone Z,
			KingdomPlotRules.PlotRect Rect, KingdomRules.BuildEntry Entry,
			KingdomPlotRules.PlotSpec Spec, GroundGrid Grid, string SkinKey, bool Carved,
			KingdomArchitectureIntent Architecture, bool LegacyArchitecture,
			ref KingdomConstructionJob Job, FoundingHeartPlacement Heart = null)
		{
			string intentFailure;
			if (System == null || Z == null || Entry == null || Spec == null || Grid == null
				|| (LegacyArchitecture && (Architecture != null || Job == null))
				|| (Heart != null && (Job != null || LegacyArchitecture
					|| Heart.Zone != Z || Heart.Context?.Architecture != Architecture))
				|| (!LegacyArchitecture && (!KingdomArchitectureRuntime.TryValidate(
					Architecture, out intentFailure) || Architecture.BuildKey != Entry.Key
					|| !SameRect(Architecture.Rect, Rect))))
			{
				if (Job != null) KingdomConstruction.Quarantine(ref Job,
					"Plot staking lacks a valid frozen authored intent.");
				return Heart != null ? HeartRefusedNull("stake: intent, rect, or placement authority") : null;
			}
			KingdomFoundingHeartStakeTruth heartTruth = Heart?.Context?.Stake;
			if (Heart != null && !KingdomFoundingHeartStakeRules.Valid(heartTruth)) return HeartRefusedNull("stake: stake truth invalid");
			KingdomPlotRules.PlotRect frozenFootprint = default(KingdomPlotRules.PlotRect);
			KingdomPlotRules.RoofState frozenRoof = KingdomPlotRules.RoofState.Open;
			if (Heart != null)
			{
				frozenFootprint = new KingdomPlotRules.PlotRect(
					heartTruth.FootprintX1, heartTruth.FootprintY1,
					heartTruth.FootprintX2, heartTruth.FootprintY2);
				frozenRoof = (KingdomPlotRules.RoofState)heartTruth.Roof;
				if (KingdomArchitectureRules.IsLatestSnapshotEncoding(
					Architecture.EncodedSnapshot)
					&& (!KingdomArchitectureRuntime.TryWorldFootprint(Architecture,
						out KingdomPlotRules.PlotRect authoredFootprint, out _)
						|| !SameRect(authoredFootprint, frozenFootprint)
						|| !KingdomArchitectureRuntime.TryRoofOnGround(Architecture, Carved,
							out KingdomPlotRules.RoofState authoredRoof, out _)
						|| authoredRoof != frozenRoof)) return HeartRefusedNull("stake: authored footprint or roof differs from the frozen stake truth");
			}
			else if (!LegacyArchitecture
				&& (!KingdomArchitectureRuntime.TryWorldFootprint(Architecture,
						out frozenFootprint, out string footprintFailure)
					|| !KingdomArchitectureRuntime.TryRoofOnGround(Architecture, Carved,
						out frozenRoof, out footprintFailure)))
			{
				if (Job != null) KingdomConstruction.Quarantine(ref Job,
					"The paid plot has no exact frozen building footprint: "
					+ footprintFailure);
				return null;
			}
			Cell cell = LegacyArchitecture ? Z.GetCell(Rect.CenterX, Rect.CenterY)
				: Z.GetCell(Architecture.MainWorldX, Architecture.MainWorldY);
			if (cell == null)
			{
				return Heart != null ? HeartRefusedNull("stake: main world cell "
					+ Architecture.MainWorldX + "," + Architecture.MainWorldY + " is outside the zone") : null;
			}
			if (Job != null && !string.IsNullOrEmpty(Job.OutputId))
			{
				// A generated ID already crossed the durable callback boundary. It can only be
				// inspected; the engine cannot recreate that exact ID safely.
				KingdomConstruction.Quarantine(ref Job,
					"Frozen plot-works identity is absent; replacement creation is forbidden.");
				return null;
			}
			GameObject works;
			try { works = GameObject.Create(WorksBlueprint); }
			catch (System.Exception ex)
			{
				if (Job != null) KingdomConstruction.Quarantine(ref Job,
					"Plot-works creation threw: " + ex.Message);
				return Heart != null ? HeartRefusedNull("stake: works creation threw " + ex.Message) : null;
			}
			if (works == null)
			{
				return Heart != null ? HeartRefusedNull("stake: no works object was created") : null;
			}
			if (Job != null && (!KingdomConstruction.Owns(System, Z, Job)
				|| !KingdomConstruction.IsCurrent(Job)))
			{
				RemoveCreatedWorks(works, Z);
				KingdomConstruction.Quarantine(ref Job,
					"Plot authority changed during works creation.");
				return null;
			}
			r_KingdomPlotWorks part = works.GetPart<r_KingdomPlotWorks>();
			if (part == null)
			{
				bool cleaned = RemoveCreatedWorks(works, Z);
				if (Job != null && !cleaned) KingdomConstruction.Quarantine(ref Job,
					"Partless plot works could not be removed exactly.");
				return Heart != null ? HeartRefusedNull("stake: works carry no plot part") : null;
			}
			part.DesignKey = Entry.Key;
			part.DisplayName = Heart == null ? Entry.Name : heartTruth.DisplayName;
			part.X1 = Rect.X1;
			part.Y1 = Rect.Y1;
			part.X2 = Rect.X2;
			part.Y2 = Rect.Y2;
			int heartX;
			int heartY;
			if (Heart == null) HeartFor(Z, Rect, out heartX, out heartY);
			else { heartX = Heart.Context.Plan.RiteX; heartY = Heart.Context.Plan.RiteY; }
			KingdomPlotRules.RoofState roof = Heart == null
				? LegacyArchitecture
					? KingdomPlotRules.RoofOnGround(Spec.Roof, Carved) : frozenRoof
				: frozenRoof;
			bool heartRung = KingdomPlotRules.HeartRungOf(Entry.Key) > 0;
			KingdomPlotRules.PlotRect footprint = Heart != null || !LegacyArchitecture
				? frozenFootprint
				: heartRung
				? HeartFootprintFor(Z, Rect, Spec)
				: FootprintFor(Rect, Spec, heartX, heartY);
			part.StartTick = Heart != null ? Heart.Context.Plan.StartedTick
				: Job == null ? The.Game.TimeTicks : Job.StartedTick;
			// The whole PLOT is cleared and the FOOTPRINT is walled: staking wide is paid for in
			// clearing, earned back in material and yard, and never in a longer wall than the
			// building actually has.
			long measuredTicks = Heart != null ? Heart.Context.Plan.TotalTicks
				: KingdomPlotRules.RaiseTicks(
					KingdomCommission.CraftBuildTicks(Entry.BuildTicks, System.ZoneDistricts.Values),
					Grid.CellsOf(Rect), footprint, roof, Carved);
			part.TotalTicks = Job != null && Job.DueTick > Job.StartedTick
					? Job.DueTick - Job.StartedTick : measuredTicks;
			if (part.TotalTicks < 1L) part.TotalTicks = 1L;
			part.StageApplied = (int)KingdomPlotRules.PlotStage.Staked;
			part.Open = Heart == null ? Spec.Open : heartTruth.Open;
			part.Carved = Heart == null ? Carved : heartTruth.Carved;
			part.WallBlueprint = Heart == null
				? KingdomPlotRules.RaisesWalls(roof)
					? KingdomPlotRules.WallBlueprintFor(System.Style, System.FoundingRegionName) : null
				: heartTruth.WallBlueprint;
			part.ContentsTable = Heart == null ? Spec.Contents : heartTruth.Contents;
			part.StaffNeeded = Heart == null ? Entry.Staff : heartTruth.Staff;
			part.ThresholdManning = Heart == null
				? KingdomRules.IsThresholdManning(Entry.Manning) : heartTruth.ThresholdManning;
			if (Job != null)
			{
				if (!KingdomConstructionRules.TryReadBuildTruth(Job,
					out bool hasPlot, out bool frontier, out int defence)
					|| !hasPlot || frontier)
				{
					RemoveCreatedWorks(works, Z);
					KingdomConstruction.Quarantine(ref Job,
						"The paid plot has no exact plotted build effects.");
					return null;
				}
				part.DefencePending = defence;
			}
			else if (Heart != null) part.DefencePending = heartTruth.Defence;
			else if (Entry.Defence > 0)
			{
				bool hasTinkering = The.Player != null && The.Player.HasSkill("Tinkering");
				bool hasAdvancedTinkering = The.Player != null && The.Player.HasSkill("Tinkering_Tinker1");
				part.DefencePending = KingdomRules.BuiltDefence(Entry.Defence, true,
					System.FoundingTerrainBlueprint, System.FoundingRegionName,
					hasTinkering, hasAdvancedTinkering);
			}
			bool foundDoor = KingdomPlotRules.TryDoor(footprint, heartX, heartY,
				out var doorX, out var doorY);
			part.HasDoor = Heart == null
				? foundDoor && KingdomPlotRules.Encloses(roof) : heartTruth.HasDoor;
			part.DoorX = Heart == null ? doorX : heartTruth.DoorX;
			part.DoorY = Heart == null ? doorY : heartTruth.DoorY;
			works.DisplayName = "plot: " + (Heart == null ? Entry.Name : heartTruth.DisplayName);
			// Consent before cost, at the moment the ground is spoken for: a plot the founder puts
			// down inside the ground the heart was surveyed for is marked yielding here, says so in
			// its own description from this moment on, and says so out loud in the sentence the
			// commission or the plan prints. The heart's own rungs are never marked -- the ground
			// is theirs.
			if (KingdomPlotRules.HeartRungOf(Entry.Key) == 0
				&& TrySurveyedHeart(Z, out var survey)
				&& KingdomPlotRules.OverlapArea(Rect, survey) > 0)
			{
				works.SetIntProperty(YieldingProperty, 1);
				works.RequirePart<r_KingdomYielding>();
			}
			string plotId = Heart == null
				? Entry.Key + "@" + Rect.X1 + "." + Rect.Y1 + "." + The.Game.TimeTicks
				: Heart.Context.Plan.PlotId;
			works.SetStringProperty(PlotIdProperty, plotId);
			if (heartRung) works.SetIntProperty(HeartPlotProperty, 1);
			// Only an exact paid job enters attended schema two. Receiptless/direct stakes keep the
			// shipped schema-zero calendar; named receipt fields must never silently upgrade them.
			if (Job != null)
			{
				works.SetIntProperty(PlotWorkSchemaProperty, PlotWorkSchema);
				SetPlotWorkLong(works, PlotWorkRequiredProperty, part.TotalTicks);
				SetPlotWorkLong(works, PlotWorkRemainingProperty, part.TotalTicks);
				SetPlotWorkLong(works, PlotWorkLastTickProperty, The.Game.TimeTicks);
				if (works.GetIntProperty(PlotWorkSchemaProperty) != PlotWorkSchema
					|| !TryGetPlotWorkLong(works, PlotWorkRequiredProperty, out long frozenRequired)
					|| !TryGetPlotWorkLong(works, PlotWorkRemainingProperty, out long frozenRemaining)
					|| !TryGetPlotWorkLong(works, PlotWorkLastTickProperty, out _)
					|| frozenRequired != part.TotalTicks || frozenRemaining != part.TotalTicks)
				{
					bool cleaned = RemoveCreatedWorks(works, Z);
					KingdomConstruction.Quarantine(ref Job, cleaned
						? "Plot labour receipt could not be frozen before projection."
						: "Plot labour receipt failed and exact cleanup was not possible.");
					return null;
				}
			}
			StampRect(works, Rect);
			StampFootprint(works, footprint, roof);
			works.SetStringProperty(KingdomUpgrade.BuildKeyProperty, Entry.Key);
			KingdomDesign.StageSkin(works, Entry, SkinKey);
			if (!(Heart != null
				? KingdomPurpose.FreezeFoundingHeartOnWork(works, heartTruth.PurposeLegacy)
				: KingdomPurpose.FreezeOnWork(works, Entry.Key,
					Job == null ? null : Job.PhysicalReceipt)))
			{
				bool cleaned = RemoveCreatedWorks(works, Z);
				if (Job != null) KingdomConstruction.Quarantine(ref Job, cleaned
					? "The plot could not freeze its exact city-purpose commitment."
					: "The purpose commitment failed and exact cleanup was not possible.");
				return Heart != null ? HeartRefusedNull("stake: purpose commitment") : null;
			}
			if (!LegacyArchitecture && !KingdomArchitectureRuntime.TryFreeze(
				works, Architecture, out string freezeFailure))
			{
				bool cleaned = RemoveCreatedWorks(works, Z);
				if (Job != null) KingdomConstruction.Quarantine(ref Job, cleaned
					? "Authored plot receipt could not be frozen before identity publication: "
						+ freezeFailure
					: "Authored plot receipt failed and exact cleanup was not possible: "
						+ freezeFailure);
				return Heart != null ? HeartRefusedNull("stake: architecture freeze: " + freezeFailure) : null;
			}
			if (!LegacyArchitecture && !KingdomArchitectureStamper.TryInitializeOwner(
				works, Architecture, plotId, out string layoutFailure))
			{
				bool cleaned = RemoveCreatedWorks(works, Z);
				if (Job != null) KingdomConstruction.Quarantine(ref Job, cleaned
					? "Authored layout receipt could not be frozen before identity publication: "
						+ layoutFailure
					: "Authored layout receipt failed and exact cleanup was not possible: "
						+ layoutFailure);
				return Heart != null ? HeartRefusedNull("stake: layout owner: " + layoutFailure) : null;
			}
			if (Job != null)
			{
				if (!KingdomConstruction.UpdateOutput(ref Job, works.ID))
				{
					bool cleaned = RemoveCreatedWorks(works, Z);
					KingdomConstruction.Quarantine(ref Job, cleaned
						? "Plot-works identity publication failed; exact replacement is forbidden."
						: "Plot-works identity publication failed and cleanup was not exact.");
					return null;
				}
				KingdomConstruction.Bind(works, Job);
			}
				return CompleteStakeAdd(System, Z, cell, works, part, Entry, Rect,
					footprint, roof, Architecture, LegacyArchitecture, ref Job, Heart);
			}

	}
}
