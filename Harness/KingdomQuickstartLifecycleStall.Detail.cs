using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	using XRL.World.Parts;

	/// <summary>The untruncated DetailRow reading and the works-root read it shares with
	/// Describe, split out only to keep the main shard under the house line cap.</summary>
	internal static partial class KingdomQuickstartLifecycleStall
	{
		/// <summary>
		/// The untruncated reading behind Describe's stamped row, plus the settlement's one-gang
		/// allocation state (Growth/KingdomConstructionPresenceRules.cs) at the moment this ran.
		/// Read-only in the sense that matters here: nothing mutates a property or assigns a
		/// gang. TryBindLocalOperation may still Take a fresh survey when none is already active
		/// (Growth/KingdomSurvey.LocalOperation.cs:24) -- scoped and disposed on the way out, but
		/// a real bind, not a free read of one the caller already held. A field this cannot read
		/// honestly names itself absent/unavailable rather than being guessed or omitted. Meant
		/// for <see cref="DetailRow"/>, never for the stamped, length-bounded refusal row itself.
		/// </summary>
		internal static string DetailMessage(XRLGame Game, Zone Zone, KingdomSystem System,
			KingdomConstructionJob Job)
		{
			if (Game == null || Zone == null || System == null || Job == null)
				return "reason=no live game, zone, settlement or job";
			Reading reading = Read(Zone, Job);
			string roots = "unavailable";
			string candidates = "unavailable";
			string selectedId = "absent";
			string free = "unavailable";
			if (KingdomSurvey.TryBindLocalOperation(Zone, System, out var scope, out string failure))
			{
				using (scope)
				{
					KingdomSurvey survey = KingdomSurvey.ActiveFor(Zone);
					if (survey != null)
					{
						int candidateCount = 0;
						for (int i = 0; i < survey.ConstructionRoots.Count; i++)
						{
							GameObject item = survey.ConstructionRoots[i];
							if (!GameObject.Validate(item)) continue;
							if (item.GetIntProperty(KingdomConstructionPresence.ActiveProperty) == 1)
								candidateCount++;
							if (item.GetIntProperty(KingdomConstructionPresence.SelectedProperty) == 1)
								selectedId = string.IsNullOrEmpty(item.IDIfAssigned)
									? "unassigned" : item.IDIfAssigned;
						}
						roots = survey.ConstructionRoots.Count.ToString(CultureInfo.InvariantCulture);
						candidates = candidateCount.ToString(CultureInfo.InvariantCulture);
						List<GameObject> available = KingdomCrews.AvailableSettlers(System, survey);
						int prefix = KingdomCrews.WorkHandCount(System, available);
						int freeCount = 0;
						for (int i = 0; i < prefix; i++)
							if (GameObject.Validate(available[i])
								&& KingdomStations.PostOf(available[i]) == 0) freeCount++;
						free = freeCount.ToString(CultureInfo.InvariantCulture);
					}
				}
			}
			else if (!string.IsNullOrEmpty(failure)) roots = "unavailable(" + Bounded(failure) + ")";
			string occupants = reading.Occupants.Count == 0 ? "none"
				: string.Join(",", reading.Occupants.ToArray());
			return "selectedId=" + selectedId + "; candidates=" + candidates + "; roots=" + roots
				+ "; free=" + free + "; plotRemaining=" + reading.Remaining
				+ "; lastSemanticTick=" + System.LastSemanticTick + "; schema=" + reading.Schema
				+ "; stage-applied=" + reading.StageApplied + "; physical=" + Job.PhysicalPhase
				+ "; occupants=" + occupants;
		}

		/// <summary>The job's works root, read once for both Describe and DetailMessage.</summary>
		private static Reading Read(Zone Zone, KingdomConstructionJob Job)
		{
			long remaining = -1L;
			long lastWorked = -1L;
			long required = 0L;
			string window = "absent";
			string crew = "root-absent";
			string schema = "absent";
			int stageApplied = 0;
			int stageTarget = 0;
			List<string> occupants = new List<string>();
			GameObject root = null;
			if (!string.IsNullOrEmpty(Job.SubjectId)
				&& KingdomConstruction.FindExactId(Zone, Job.SubjectId, out root)
					== KingdomPhysicalLookupState.Exact && GameObject.Validate(root))
			{
				if (Job.Projection == KingdomConstructionProjection.PlotWorks)
				{
					if (root.GetIntProperty(KingdomPlots.PlotWorkSchemaProperty)
						== KingdomPlots.PlotWorkSchema)
					{
						if (TryLong(root, KingdomPlots.PlotWorkRemainingProperty, out long r))
							remaining = r;
						if (TryLong(root, KingdomPlots.PlotWorkLastTickProperty, out long l))
							lastWorked = l;
						if (TryLong(root, KingdomPlots.PlotWorkRequiredProperty, out long q))
							required = q;
					}
					string encoded = root.GetStringProperty(KingdomPlots.PlotWorkWindowProperty);
					if (!string.IsNullOrEmpty(encoded)) window = encoded;
					r_KingdomPlotWorks plot = root.GetPart<r_KingdomPlotWorks>();
					if (plot != null)
					{
						stageApplied = plot.StageApplied;
						stageTarget = required > 0L
							? (int)KingdomPlotRules.StageAt(required - (remaining < 0L ? 0L : remaining),
								required)
							: stageApplied;
						occupants = OccupantsOn(Zone, plot.Rect());
					}
				}
				else
				{
					r_KingdomScaffold scaffold = root.GetPart<r_KingdomScaffold>();
					if (scaffold != null)
					{
						remaining = scaffold.RemainingTicks;
						lastWorked = scaffold.LastWorkedTick;
					}
					string encoded = root.GetStringProperty(r_KingdomScaffold.WorkWindowProperty);
					if (!string.IsNullOrEmpty(encoded)) window = encoded;
				}
				schema = Text(root, KingdomConstructionPresence.SchemaProperty);
				crew = "selected=" + Text(root, KingdomConstructionPresence.SelectedProperty)
					+ " hands=" + Text(root, KingdomConstructionPresence.HandsProperty)
					+ " effectiveness=" + Text(root, KingdomConstructionPresence.EffectivenessProperty)
					+ " schema=" + schema;
			}
			return new Reading(remaining, lastWorked, required, window, crew, schema,
				stageApplied, stageTarget, occupants);
		}

		/// <summary>Living occupants (creature or player) standing anywhere on a plot's own
		/// footprint, by id -- the same test Growth/KingdomArchitectureStamper.Verification.cs's
		/// CanInsert refuses an apply on (IsCreature || IsPlayer()). Read-only: nothing here moves,
		/// posts, or removes anyone. An unassigned id is named rather than dropped.</summary>
		private static List<string> OccupantsOn(Zone Zone, KingdomPlotRules.PlotRect Rect)
		{
			List<string> ids = new List<string>();
			if (Zone == null) return ids;
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null) continue;
					List<GameObject> objects = cell.GetObjects();
					for (int i = 0; i < objects.Count; i++)
					{
						GameObject item = objects[i];
						if (!GameObject.Validate(item) || !(item.IsCreature || item.IsPlayer()))
							continue;
						ids.Add(string.IsNullOrEmpty(item.IDIfAssigned)
							? "unassigned" : item.IDIfAssigned);
					}
				}
			return ids;
		}
	}
}
