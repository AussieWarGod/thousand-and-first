using System;

using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst;

// Named in code only, but kept in the engine's part namespace so it remains safe to expose in a
// third-party blueprint later. It is stateless: every frame derives from gameplay state already
// held by the work, never from a cosmetic save latch.
namespace XRL.World.Parts
{
	/// <summary>Vanilla-style alternating map indicator for exact settlement work state.</summary>
	[Serializable]
	public class r_KingdomVisualState : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID;
		}

		public override bool FinalRender(RenderEvent E)
		{
			KingdomVisualStateKind state = KingdomVisualState.StateOf(ParentObject);
			if (state != KingdomVisualStateKind.Sound)
			{
				KingdomVisualCue cue = KingdomVisualStateRules.Cue(state);
				// Vanilla's own status idiom: show the authored object for half the cycle and a
				// silhouette/glyph cue for half. UI renders keep the real object icon. Glyph and
				// silhouette, not color, distinguish the states.
				E.RenderEffectIndicator(cue.Glyph, cue.Tile, cue.ColorString, cue.DetailColor,
					30, 30);
			}
			return base.FinalRender(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			KingdomVisualStateKind state = KingdomVisualState.StateOf(ParentObject);
			if (state != KingdomVisualStateKind.Sound)
			{
				KingdomVisualCue cue = KingdomVisualStateRules.Cue(state);
				E.Postfix.Append("\n{{rules|Map sign ").Append(cue.Glyph).Append(": ")
					.Append(cue.Label).Append(".}}");
			}
			return base.HandleEvent(E);
		}
	}

	/// <summary>
	/// Active-only motion for a hand-cranked civic charger. Vanilla's
	/// <c>AnimatedMaterialElectric</c> flashes even when a machine is empty and unstaffed, so the
	/// charging post cannot honestly reuse it. This stateless reader alternates a visible glyph
	/// only while the exact work is sound, fully staffed, and actually holds charge.
	/// </summary>
	[Serializable]
	public class r_KingdomHandCrankedVisual : IPart
	{
		private const string ActiveGlyph = "~";

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID;
		}

		public override bool FinalRender(RenderEvent E)
		{
			if (IsActive())
			{
				// Same vanilla effect-indicator channel as ordinary Qud status motion. The glyph,
				// not its color, distinguishes the active half-frame for color-independent reading.
				E.RenderEffectIndicator(ActiveGlyph, null, "&Y", "W", 30, 30);
			}
			return base.FinalRender(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			if (IsActive())
			{
				E.Postfix.Append("\n{{rules|Map sign ").Append(ActiveGlyph)
					.Append(": hand-cranked, fully staffed, and holding charge.}}");
			}
			return base.HandleEvent(E);
		}

		private bool IsActive()
		{
			if (!GameObject.Validate(ParentObject)
				|| ParentObject.GetIntProperty(KingdomAdopt.HandCrankedProperty) != 1
				|| KingdomVisualState.StateOf(ParentObject) != KingdomVisualStateKind.Sound)
			{
				return false;
			}
			Capacitor store = ParentObject.GetPart<Capacitor>();
			return store != null && store.Charge > 0;
		}
	}
}

namespace ThousandAndFirst
{
	/// <summary>Engine edge for the pure visual-state resolver.</summary>
	public static class KingdomVisualState
	{
		public const string WitheredProperty = "r_TAF_VisualWithered";
		public const string FamishedProperty = "r_TAF_VisualFamished";

		/// <summary>Attaches one stateless reader to finished works and the first basin, and stamps
		/// deprivation only on the city heart. Called every semantic pass, so recovery removes the
		/// cue in the same pass and old saves need no migration.</summary>
		public static void Refresh(KingdomSystem System, Zone Z, KingdomSurvey Survey = null)
		{
			if (System == null || !System.Founded || Z == null) return;
			KingdomSurvey survey = Survey ?? KingdomSurvey.Take(Z, System);
			if (!ReferenceEquals(survey.Ground, Z)) return;
			for (int i = 0; i < survey.VisualRoots.Count; i++)
			{
				GameObject item = survey.VisualRoots[i];
				if (!GameObject.Validate(item)) continue;
				bool heart = item.GetIntProperty(KingdomPlots.HeartPlotProperty) == 1
					|| item.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1;
				bool eligible = item.GetIntProperty("KingdomBuilt") == 1 || heart
					|| item.GetIntProperty(KingdomConstructionPresence.ActiveProperty) == 1;
				if (!eligible) continue;
				item.RequirePart<r_KingdomVisualState>();
				if (item.GetIntProperty(KingdomAdopt.HandCrankedProperty) == 1)
				{
					item.RequirePart<r_KingdomHandCrankedVisual>();
				}
				if (heart && System.Withered) item.SetIntProperty(WitheredProperty, 1);
				else item.RemoveIntProperty(WitheredProperty);
				if (heart && System.Famished) item.SetIntProperty(FamishedProperty, 1);
				else item.RemoveIntProperty(FamishedProperty);
			}
		}

		public static KingdomVisualFacts FactsOf(GameObject Work)
		{
			if (!GameObject.Validate(Work)) return default(KingdomVisualFacts);
			r_KingdomWear wear = Work.GetPart<r_KingdomWear>();
			bool heart = Work.GetIntProperty(KingdomPlots.HeartPlotProperty) == 1
				|| Work.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1;
			return new KingdomVisualFacts(
				Work.GetIntProperty(KingdomConstructionPresence.ActiveProperty) == 1,
				Work.GetIntProperty(KingdomConstructionPresence.SelectedProperty) == 1,
				Work.GetIntProperty(KingdomConstructionPresence.HandsProperty),
				Work.GetIntProperty(KingdomMaterials.StrikeEffortProperty) > 0,
				wear != null && wear.RepairEffortLeft > 0,
				wear == null ? 0 : wear.Wear,
				heart,
				Work.GetIntProperty(WitheredProperty) == 1,
				Work.GetIntProperty(FamishedProperty) == 1,
				Work.GetIntProperty("KingdomBrownout") == 1,
				Work.GetIntProperty("KingdomStaffNeeded"),
				Work.GetIntProperty("KingdomEffectiveness"));
		}

		public static KingdomVisualStateKind StateOf(GameObject Work)
		{
			return KingdomVisualStateRules.Resolve(FactsOf(Work));
		}
	}
}
