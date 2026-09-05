using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>Callback-time PHYSICAL snapshot of one actor an EARLIER probe callback returned.
	/// Placement is READ off the body itself at that instant - never inferred from the projection's
	/// own Proved bookkeeping, which is the model's claim about the world rather than the world.
	/// <para>The body is retained by reference and never released, so the checks can revalidate it
	/// after the run. Reads only: IDIfAssigned (GameObject.cs:424, never forces an identity),
	/// CurrentZone (:532), CurrentCell (:517) and PartsList (:151). No zone survey runs here - a
	/// per-callback census inside factory dispatch would be both costly and a second chance to
	/// perturb the very state being observed.</para></summary>
	public sealed class r_TAF_RaidMintPriorActor
	{
		public readonly int Sequence;
		public readonly GameObject Body;
		public readonly string ObjectId;
		public readonly string ZoneId;
		public readonly int? CellX;
		public readonly int? CellY;
		public readonly int MarkerParts;
		public readonly string MarkerOperationId;

		internal r_TAF_RaidMintPriorActor(int Sequence, GameObject Body)
		{
			this.Sequence = Sequence;
			this.Body = Body;
			if (Body == null) return;
			ObjectId = Body.IDIfAssigned;
			Zone zone = Body.CurrentZone;
			ZoneId = zone == null ? null : zone.ZoneID;
			Cell cell = Body.CurrentCell;
			if (cell != null) { CellX = cell.X; CellY = cell.Y; }
			string operation;
			MarkerParts = r_TAF_RaidMintSnapshots.MarkerParts(Body, out operation);
			MarkerOperationId = operation;
		}
	}

	/// <summary>The RAW entry snapshot of one creation callback. Every authority fact the run will
	/// ever report about this mint is read HERE, at handler entry, BEFORE any substitution can move
	/// the operation or its projections; the substitute and fault are attached to this same retained
	/// record afterwards. Held by the probe's static list, so the bodies stay strongly reachable
	/// even if building the immutable observation from it refuses.</summary>
	public sealed class r_TAF_RaidMintEntry
	{
		private static readonly r_TAF_RaidMintPriorActor[] NoPriors = new r_TAF_RaidMintPriorActor[0];
		public readonly int Sequence;
		public readonly string RequestedBlueprint;
		public readonly string Context;
		public readonly KingdomLifecycleOperation Raid;
		public readonly string OperationId;
		public readonly KingdomLifecyclePhase Phase;
		public readonly int ProjectionCount;
		public readonly string ProjectionStates;
		public readonly int MintIndex;
		public readonly int ProvedBefore;
		public readonly KingdomLifecyclePhysicalState MintState;
		public readonly string MintBlueprint;
		public readonly string MintObjectId;
		/// <summary>One snapshot per body an earlier callback returned, in creation order. Built
		/// once at entry and never written again.</summary>
		public readonly r_TAF_RaidMintPriorActor[] Priors;
		public readonly GameObject Original;
		public readonly long Tick;
		public readonly DateTime Utc;
		/// <summary>Attached after this record is already retained, never before.</summary>
		public GameObject Substitute;
		public string SubstituteBlueprint;
		public string Fault;

		/// <summary>Fault path: raw references only, with no frozen authority scan, so a record
		/// that keeps the bodies reachable exists even when the scan below refuses.</summary>
		internal r_TAF_RaidMintEntry(int Sequence, GameObject Original)
		{
			this.Sequence = Sequence;
			this.Original = Original;
			RequestedBlueprint = Original == null ? null : Original.Blueprint;
			Phase = KingdomLifecyclePhase.Invalid;
			MintState = KingdomLifecyclePhysicalState.None;
			MintIndex = -1;
			Priors = NoPriors;
			Tick = -1L;
			Utc = DateTime.UtcNow;
		}

		internal r_TAF_RaidMintEntry(int Sequence, GameObject Original, string Context,
			KingdomLifecycleOperation Raid, r_TAF_RaidMintPriorActor[] Priors)
			: this(Sequence, Original)
		{
			this.Context = Context;
			this.Raid = Raid;
			this.Priors = Priors ?? NoPriors;
			if (The.Game != null) Tick = The.Game.TimeTicks;
			OperationId = Raid == null ? null : Raid.Id;
			Phase = Raid == null ? KingdomLifecyclePhase.Invalid : Raid.Phase;
			List<KingdomLifecycleProjection> projections = Raid == null ? null : Raid.Projections;
			ProjectionCount = projections == null ? 0 : projections.Count;
			StringBuilder states = new StringBuilder();
			for (int i = 0; i < ProjectionCount; i++)
			{
				KingdomLifecycleProjection projection = projections[i];
				if (states.Length > 0) states.Append(',');
				states.Append(projection == null ? "null" : projection.State.ToString());
				if (projection == null) continue;
				if (projection.State == KingdomLifecyclePhysicalState.Proved) { ProvedBefore++; continue; }
				if (MintIndex >= 0) continue;
				MintIndex = i;
				MintState = projection.State;
				MintBlueprint = projection.Blueprint;
				MintObjectId = projection.ObjectId;
			}
			ProjectionStates = states.ToString();
		}
	}

	/// <summary>Capture helpers shared by the probe (inside dispatch) and the checks (outside it).
	/// Nothing here throws into the factory: every caller in the probe is already inside its own
	/// try/catch, and these helpers only read.</summary>
	internal static class r_TAF_RaidMintSnapshots
	{
		/// <summary>One immutable prior-actor snapshot per EARLIER callback, in creation order. The
		/// body a callback returned is its substitute when it substituted, otherwise the body the
		/// factory handed it.</summary>
		internal static r_TAF_RaidMintPriorActor[] Priors(List<r_TAF_RaidMintEntry> Entries)
		{
			int count = Entries == null ? 0 : Entries.Count;
			r_TAF_RaidMintPriorActor[] priors = new r_TAF_RaidMintPriorActor[count];
			for (int j = 0; j < count; j++)
			{
				r_TAF_RaidMintEntry entry = Entries[j];
				priors[j] = new r_TAF_RaidMintPriorActor(entry == null ? 0 : entry.Sequence,
					entry == null ? null : (entry.Substitute ?? entry.Original));
			}
			return priors;
		}

		/// <summary>Counts objective parts on one body and reports the first one's operation.
		/// PartsList precedent: Growth/KingdomProcedures.03.Stamps.cs:34-40; the Raids slice only
		/// reads GetPart&lt;T&gt;, which cannot see a duplicate.</summary>
		internal static int MarkerParts(GameObject Body, out string OperationId)
		{
			OperationId = null;
			if (Body == null || Body.PartsList == null) return 0;
			int found = 0;
			for (int i = 0; i < Body.PartsList.Count; i++)
			{
				r_KingdomRaiderObjective marker = Body.PartsList[i] as r_KingdomRaiderObjective;
				if (marker == null) continue;
				if (found == 0) OperationId = marker.OperationId;
				found++;
			}
			return found;
		}

		internal static int MarkerParts(GameObject Body)
		{
			string operation;
			return MarkerParts(Body, out operation);
		}

		/// <summary>Zone-wide identity/marker census, mirroring CountProjection
		/// (Raids/KingdomRaids.08.DemandProjection.cs:66-76). CHECKS ONLY: a whole-zone survey must
		/// never run inside a creation callback, which is why the probe handler snapshots the prior
		/// bodies it already holds instead, and why a contract test forbids KingdomSurvey there.
		/// </summary>
		internal static GameObject Census(Zone Zone, KingdomLifecycleProjection Projection,
			out int Ids, out int Markers)
		{
			Ids = 0;
			Markers = 0;
			GameObject exact = null;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Zone))
			{
				if (item.GetStringProperty(KingdomRaids.ProjectionMarkerProperty) == Projection.Marker)
					Markers++;
				if (item.IDIfAssigned == Projection.ObjectId) { Ids++; exact = item; }
			}
			return exact;
		}

		/// <summary>The callback-time placement row for one mint's prior actors, rendered next to
		/// the snapshot type it reads so the record and the capture cannot drift apart.</summary>
		internal static string Describe(r_TAF_RaidMintPriorActor[] Priors)
		{
			if (Priors == null || Priors.Length == 0) return "-";
			StringBuilder row = new StringBuilder();
			for (int j = 0; j < Priors.Length; j++)
			{
				r_TAF_RaidMintPriorActor prior = Priors[j];
				if (row.Length > 0) row.Append('/');
				row.Append(prior.ObjectId ?? "(null)").Append('@')
					.Append(prior.ZoneId ?? "-").Append(':')
					.Append(prior.CellX.HasValue ? prior.CellX.Value + "," + prior.CellY.Value : "-")
					.Append('x').Append(prior.MarkerParts)
					.Append('=').Append(prior.MarkerOperationId ?? "-");
			}
			return row.ToString();
		}
	}
}
