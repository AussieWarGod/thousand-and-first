using System;
using System.Collections.Generic;
using System.Globalization;

using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>
	/// Projects construction labour onto real named settlers while a zone is attended. The
	/// simulation remains authoritative: the selected root carries the exact hands and derived
	/// effectiveness that its labour clock consumes, and no body is created, moved between cells,
	/// or cloned here. Stations change anchors; vanilla pathing does the walking.
	/// </summary>
	public static partial class KingdomConstructionPresence
	{
		/// <summary>Named properties only. r_KingdomScaffold and r_KingdomPlotWorks are shipped
		/// positional parts and may never grow fields for this tranche.</summary>
		public const string SchemaProperty = "r_TAF_ConstructionCrewSchema";
		public const string ActiveProperty = "r_TAF_ConstructionCrewActive";
		public const string SelectedProperty = "r_TAF_ConstructionCrewSelected";
		public const string HandsProperty = "r_TAF_ConstructionCrewHands";
		public const string EffectivenessProperty = "r_TAF_ConstructionCrewEffectiveness";
		public const string QueueSaidProperty = "r_TAF_ConstructionCrewQueueSaid";
		public const string LegacyStartProperty = "r_TAF_ConstructionCrewLegacyStart";

		private sealed class Candidate
		{
			internal GameObject Root;
			internal KingdomRaisingCandidate Reading;
			internal string DisplayName;
		}

		/// <summary>
		/// Assigns one oldest raising from the real unposted prefix left after water duty and
		/// finished works. Safe to call more than once in one tick: old construction posts are
		/// cleared first, then the same facts derive the same gang.
		/// </summary>
		public static int Assign(KingdomSystem System, KingdomSurvey Survey)
		{
			if (System == null || !System.Founded || Survey == null)
			{
				return 0;
			}
			Zone zone = Survey.Ground ?? GroundOf(Survey);
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				return 0;
			}

			int prefix = System.Population - System.WaterCrew;
			if (prefix < 0) prefix = 0;
			if (prefix > Survey.Settlers.Count) prefix = Survey.Settlers.Count;

			// A prior pass's construction post is not a second claim on the same body. Ordinary
			// work posts remain: those hands are already spent before construction is asked.
			List<GameObject> previousConstruction = new List<GameObject>();
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				if (GameObject.Validate(settler)
					&& !KingdomPhysicalHappenings.IsStaged(settler)
					&& settler.GetIntProperty(KingdomStations.PostKindProperty)
						== (int)KingdomWorkKind.Construction)
				{
					previousConstruction.Add(settler);
					KingdomStations.Post(settler, 0, KingdomWorkKind.Other);
				}
			}

			List<GameObject> free = new List<GameObject>();
			int occupied = 0;
			for (int i = 0; i < prefix; i++)
			{
				GameObject settler = Survey.Settlers[i];
				if (!GameObject.Validate(settler)
					|| KingdomPhysicalHappenings.IsStaged(settler)) continue;
				if (KingdomStations.PostOf(settler) == 0) free.Add(settler);
				else occupied++;
			}

			List<Candidate> candidates = Candidates(Survey);
			List<KingdomRaisingCandidate> readings = new List<KingdomRaisingCandidate>(
				candidates.Count);
			for (int i = 0; i < candidates.Count; i++) readings.Add(candidates[i].Reading);
			KingdomRaisingPlan plan = KingdomConstructionPresenceRules.Plan(readings, free.Count,
				KingdomRules.RaisingHandsWanted);
			int assigned = 0;
			string selectedName = plan.SelectedIndex >= 0 ? candidates[plan.SelectedIndex].DisplayName
				: null;

			if (plan.SelectedIndex >= 0)
			{
				Candidate selected = candidates[plan.SelectedIndex];
				KingdomCrewRules.CrewOutcome outcome = KingdomCrews.AssignRaising(selected.Root,
					free, plan.AssignedHands);
				assigned = outcome.Assigned;
				int headcount = KingdomRules.RaisingEffectiveness(assigned);
				int capability = KingdomCrewRules.CapabilityEffectiveness(outcome.BestCapability,
					outcome.CapabilityThreshold);
				int effectiveness = KingdomCrewRules.CombinedEffectiveness(headcount, capability);
				effectiveness = KingdomIdentityAffinityRules.Apply(effectiveness,
					outcome.IdentityAffinity);
				if (effectiveness > 100) effectiveness = 100;

				selected.Root.SetIntProperty(SelectedProperty, 1);
				selected.Root.SetIntProperty(HandsProperty, assigned);
				selected.Root.SetIntProperty(EffectivenessProperty, effectiveness);
				selected.Root.RemoveIntProperty(QueueSaidProperty);
				selected.Root.SetIntProperty(KingdomCrews.IdentityAffinityProperty,
					outcome.IdentityAffinity);

				int workId = KingdomCityRules.StableId(selected.Root.ID);
				r_KingdomStation station = selected.Root.RequirePart<r_KingdomStation>();
				station.WorkId = workId;
				station.Kind = (int)KingdomWorkKind.Construction;
				for (int i = 0; outcome.SettlerIndices != null
					&& i < outcome.SettlerIndices.Length; i++)
				{
					int at = outcome.SettlerIndices[i];
					if (at < 0 || at >= free.Count) continue;
					KingdomStations.Post(free[at], workId, KingdomWorkKind.Construction);
				}

				if (outcome.CapabilityThreshold > 0 && capability < 100)
				{
					KingdomCrews.AnnounceShortfall(selected.Root, selected.DisplayName,
						outcome.CapabilityKind, outcome.BestCapability,
						outcome.CapabilityThreshold);
				}
				else KingdomCrews.ClearShortfall(selected.Root);
			}

			for (int i = 0; i < candidates.Count; i++)
			{
				if (i == plan.SelectedIndex) continue;
				Candidate waiting = candidates[i];
				if (waiting.Root.GetIntProperty(QueueSaidProperty) != 1)
				{
					waiting.Root.SetIntProperty(QueueSaidProperty, 1);
					System.Ledger.Note("{{y|" + KingdomConstructionPresenceRules.QueueLine(
						waiting.DisplayName, selectedName) + "}}");
				}
			}
			for (int i = 0; i < previousConstruction.Count; i++)
			{
				GameObject settler = previousConstruction[i];
				if (GameObject.Validate(settler)
					&& !KingdomPhysicalHappenings.IsStaged(settler)
					&& settler.GetIntProperty(KingdomStations.PostKindProperty)
						!= (int)KingdomWorkKind.Construction)
				{
					KingdomStations.Release(zone, settler);
				}
			}

			// Construction hands are now unavailable to later clearing, striking, and mending.
			// Count real normal posts, real assigned builders, and the already-reserved water detail.
			long spent = (long)occupied + assigned + System.WaterCrew;
			System.AssignedCrew = spent >= int.MaxValue ? int.MaxValue : (int)spent;
			return assigned;
		}

		/// <summary>Exact per-root labour pace. Missing schema is compatibility only; new runtime
		/// roots are stamped by <see cref="Assign"/> before construction dispatch.</summary>
		public static int EffectivenessOf(GameObject Root, KingdomSystem System, out int Hands,
			out bool Selected)
		{
			Hands = 0;
			Selected = false;
			if (!GameObject.Validate(Root)) return 0;
			int schema = Root.GetIntProperty(SchemaProperty);
			if (schema == KingdomConstructionPresenceRules.Schema)
			{
				if (Root.GetIntProperty(ActiveProperty) != 1) return 0;
				Selected = Root.GetIntProperty(SelectedProperty) == 1;
				if (!Selected) return 0;
				Hands = Root.GetIntProperty(HandsProperty);
				if (Hands < 0) Hands = 0;
				int effectiveness = Root.GetIntProperty(EffectivenessProperty);
				return effectiveness < 0 ? 0 : (effectiveness > 100 ? 100 : effectiveness);
			}
			if (schema != 0) return 0; // Unknown coordination receipt: freeze, never guess.
			Selected = true;
			if (System == null || !System.Founded)
			{
				Hands = KingdomRules.RaisingHandsWanted;
				return 100;
			}
			Hands = KingdomMaterialRules.FreeHands(System.Population, System.AssignedCrew);
			return KingdomRules.RaisingEffectiveness(Hands);
		}

		/// <summary>Releases construction-only posts whose exact root finished or vanished. Home is
		/// another anchor and MoveTo goal, never a teleport.</summary>
		public static void ReleaseFinished(Zone Z, KingdomSurvey Survey)
		{
			if (Z == null || Survey == null) return;
			HashSet<int> active = new HashSet<int>();
			for (int i = 0; i < Survey.ConstructionRoots.Count; i++)
			{
				GameObject item = Survey.ConstructionRoots[i];
				if (GameObject.Validate(item) && item.GetIntProperty(ActiveProperty) == 1
					&& item.GetIntProperty(SelectedProperty) == 1 && NeedsLabour(item))
				{
					active.Add(KingdomCityRules.StableId(item.ID));
				}
			}
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				if (!GameObject.Validate(settler)
					|| KingdomPhysicalHappenings.IsStaged(settler)
					|| settler.GetIntProperty(KingdomStations.PostKindProperty)
						!= (int)KingdomWorkKind.Construction) continue;
				if (!active.Contains(KingdomStations.PostOf(settler)))
					KingdomStations.Release(Z, settler);
			}
		}

		private static List<Candidate> Candidates(KingdomSurvey Survey)
		{
			List<Candidate> result = new List<Candidate>();
			if (Survey == null) return result;
			for (int i = 0; i < Survey.ConstructionRoots.Count; i++)
			{
				GameObject item = Survey.ConstructionRoots[i];
				r_KingdomPlotWorks plot = item.GetPart<r_KingdomPlotWorks>();
				r_KingdomScaffold scaffold = item.GetPart<r_KingdomScaffold>();
				bool constructionRoot = plot != null || scaffold != null;
				if (!constructionRoot) continue;
				Reset(item);
				if (!NeedsLabour(item)) continue;
				item.SetIntProperty(SchemaProperty, KingdomConstructionPresenceRules.Schema);
				item.SetIntProperty(ActiveProperty, 1);
				item.RequirePart<r_KingdomVisualState>();
				Cell at = item.CurrentCell;
				long started = Started(item, plot, scaffold);
				string display = plot != null ? plot.DisplayName : scaffold.TargetDisplayName;
				if (string.IsNullOrEmpty(display)) display = item.ShortDisplayName;
				result.Add(new Candidate
				{
					Root = item,
					Reading = new KingdomRaisingCandidate(item.ID, started,
						at == null ? int.MaxValue : at.X, at == null ? int.MaxValue : at.Y),
					DisplayName = display
				});
			}
			return result;
		}

	}
}
