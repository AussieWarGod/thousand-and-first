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
		private static bool RemoveCreatedWorks(GameObject Works, Zone ExpectedZone = null)
		{
			Zone zone = GameObject.Validate(Works) ? Works.CurrentZone : ExpectedZone;
			if (!GameObject.Validate(Works))
			{
				KingdomSurvey.ObserveRemovedFromActive(zone, Works);
				return true;
			}
			try
			{
				return Works.Obliterate(null, Silent: true) && !GameObject.Validate(Works);
			}
			catch { return false; }
			finally
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(zone, Works);
			}
		}

		private static bool ProjectPlot(KingdomSystem System, Zone Z,
			KingdomPlotRules.PlotRect Rect, KingdomRules.BuildEntry Entry,
			KingdomPlotRules.PlotSpec Spec, GroundGrid Grid, string SkinKey, bool Carved,
			KingdomConstructionJob Job, out GameObject Works,
			out KingdomConstructionJob Updated, out string Failure)
		{
			KingdomConstructionJob current = Job;
			Updated = current;
			Failure = null;
			if (Job == null || !TryDecodePlotPayload(Job.Payload,
				out KingdomPlotRules.PlotRect paidRect, out string paidSkin,
				out KingdomArchitectureIntent architecture, out bool legacyArchitecture,
				out Failure) || !SameRect(paidRect, Rect) || !SamePlotSkin(paidSkin, SkinKey)
				|| Job.TargetKey != Entry.Key
				|| (!legacyArchitecture && (architecture == null
					|| architecture.BuildKey != Entry.Key || Job.X != architecture.MainWorldX
					|| Job.Y != architecture.MainWorldY))
				|| (legacyArchitecture && (Job.X != Rect.CenterX || Job.Y != Rect.CenterY)))
			{
				if (Failure == null) Failure =
					"The paid plot payload does not match its exact authored projection.";
				if (Updated != null) KingdomConstruction.Quarantine(ref Updated, Failure);
				Works = null;
				return false;
			}
			KingdomPhysicalLookupState worksState = FindConstructionResult(
				Z, Job, false, out Works);
			Cell cell = legacyArchitecture ? Z?.GetCell(Rect.CenterX, Rect.CenterY)
				: Z?.GetCell(architecture.MainWorldX, architecture.MainWorldY);
			if (worksState == KingdomPhysicalLookupState.Ambiguous)
			{
				Failure = "The frozen plot-works ID is duplicated or malformed.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (ExpectedWorks(Works, cell, Entry.Key, architecture, legacyArchitecture, Job)
				&& Works.IDIfAssigned == (Job.OutputId ?? Job.SubjectId)
				&& KingdomConstruction.HasReceipt(Works, Job))
			{
				KingdomConstruction.FinishProjection(ref Updated, true, true);
				return true;
			}
			if (GameObject.Validate(Works)
				&& (Works.IDIfAssigned != Job.OutputId && Works.IDIfAssigned != Job.SubjectId
					|| !ExpectedWorks(Works, cell, Entry.Key, architecture, legacyArchitecture, Job)
					|| !KingdomConstruction.HasReceipt(Works, Job)))
			{
				Failure = "The frozen plot receipt is attached to an unexpected projection.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			GameObject unexpected;
			KingdomPhysicalLookupState receiptState = KingdomConstruction.FindReceipt(
				Z, Job, out unexpected);
			if (receiptState == KingdomPhysicalLookupState.Ambiguous
				|| (receiptState == KingdomPhysicalLookupState.Exact && unexpected != Works))
			{
				Failure = "The plot receipt is attached to a foreign or replacement projection.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (!string.IsNullOrEmpty(Job.OutputId))
			{
				Failure = "The exact frozen plot-works output is absent in its loaded owner zone.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (!KingdomConstructionRules.TryReadBuildTruth(Job, out _, out _, out _))
			{
				Failure = "The unprojected legacy plot predates frozen build effects.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (cell == null || !KingdomConstruction.BeginProjection(ref Updated, out Failure))
			{
				return false;
			}
			Works = Stake(System, Z, Rect, Entry, Spec, Grid, SkinKey, Carved,
				architecture, legacyArchitecture, ref Updated);
			if (!ExpectedWorks(Works, cell, Entry.Key, architecture, legacyArchitecture, Updated)
				|| Works.IDIfAssigned != Updated.OutputId || !KingdomConstruction.HasReceipt(Works, Updated))
			{
				Failure = "The plot works could not be verified in the staked cell.";
				if (Updated.Phase != KingdomConstructionPhase.InspectionRequired)
					KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if ((Updated.Route == KingdomConstructionRoute.PlotCommission
					|| Updated.Route == KingdomConstructionRoute.SocketBuild)
				&& !KingdomConstruction.UpdateSubject(ref Updated, Works.IDIfAssigned))
			{
				Failure = "The plot-works identity could not be published after placement.";
				return false;
			}
			KingdomConstruction.FinishProjection(ref Updated, true, true);
			return true;
		}

		internal static bool ProjectOnRect(KingdomSystem System, Zone Z,
			KingdomPlotRules.PlotRect Rect, KingdomRules.BuildEntry Entry,
			KingdomPlotRules.PlotSpec Spec, string SkinKey, KingdomConstructionJob Job,
			out GameObject Works, out KingdomConstructionJob Updated, out string Failure)
		{
			return ProjectPlot(System, Z, Rect, Entry, Spec, new GroundGrid(Z), SkinKey,
				KingdomPlotRules.IsUnderground(Z.Z), Job, out Works, out Updated, out Failure);
		}

		private static bool ExpectedWorks(GameObject Works, Cell Cell, string Key,
			KingdomArchitectureIntent Intent, bool Legacy, KingdomConstructionJob Job = null)
		{
			r_KingdomPlotWorks part = GameObject.Validate(Works)
				? Works.GetPart<r_KingdomPlotWorks>() : null;
			if (part == null || part.DesignKey != Key) return false;
			if (Job != null)
			{
				if (KingdomConstructionRules.TryReadBuildTruth(Job,
					out bool hasPlot, out bool frontier, out int defence))
				{
					if (!hasPlot || frontier || part.DefencePending != defence) return false;
				}
				else if (Job.BuildTruthSchema != 0 || part.DefencePending < 0) return false;
			}
			return ExpectedArchitectureReceipt(Works, Cell, Key, Intent, Legacy);
		}

		internal static bool ExpectedArchitectureReceipt(GameObject Object, Cell Cell, string Key,
			KingdomArchitectureIntent Intent, bool Legacy)
		{
			if (!GameObject.Validate(Object) || Cell == null || Object.CurrentCell != Cell
				|| Object.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Key) return false;
			if (Legacy)
				return Intent == null && !HasArchitectureReceiptEvidence(Object);
			return Intent != null && Intent.BuildKey == Key
				&& Object.CurrentCell.X == Intent.MainWorldX
				&& Object.CurrentCell.Y == Intent.MainWorldY
				&& KingdomArchitectureRuntime.TryRead(Object,
					out KingdomArchitectureIntent frozen, out _)
				&& SameIntent(frozen, Intent)
				&& (!KingdomArchitectureRules.IsManagedSnapshotEncoding(Intent.EncodedSnapshot)
					|| (KingdomArchitectureStamper.TryReadOwner(Object, out _, out _,
						out string lotId, out _)
						&& lotId == Object.GetStringProperty(PlotIdProperty)));
		}

		private static bool HasArchitectureReceiptEvidence(GameObject Object)
		{
			return HasArchitectureProperty(Object, KingdomArchitectureRuntime.SchemaProperty)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.BuildKeyProperty)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.PlanKeyProperty)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.BindingKeyProperty)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.TierKeyProperty)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.VariantKeyProperty)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.PaletteKeyProperty)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.LotTypeProperty)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.LotSizeProperty)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.FacingProperty)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.SnapshotProperty)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.HashProperty)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.RectX1Property)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.RectY1Property)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.RectX2Property)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.RectY2Property)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.MainXProperty)
				|| HasArchitectureProperty(Object, KingdomArchitectureRuntime.MainYProperty);
		}

		private static bool HasArchitectureProperty(GameObject Object, string Property)
		{
			return Object != null
				&& (Object.HasIntProperty(Property) || Object.HasStringProperty(Property));
		}

		/// <summary>
		/// Resolves and freezes all authored authority needed by a future plot job. Call before any
		/// water/material reservation or world mutation. The resulting payload is the only authority
		/// projection and retry consume.
		/// </summary>
	}
}
