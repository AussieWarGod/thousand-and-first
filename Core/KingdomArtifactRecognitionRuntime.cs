using System;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Explicit-object adapter only. It never enumerates inventory or changes custody.</summary>
	/// <remarks>
	/// <para>Qud 2.0.211.51 API evidence: GameObject.cs lines 424-463 (ID), 749-760
	/// (short stripped display), 4942-4988 (string properties), and 8945-8954 (takeability).</para>
	/// <para>
	/// <b>Reading an identity must not create one.</b> <c>GameObject.ID</c> is not an accessor: its
	/// getter (GameObject.cs 436-448) writes the <c>id</c> string property when none is set, and
	/// the <c>BaseID</c> it falls back on (GameObject.cs 400-417) advances the save's
	/// <c>GameObjectIDSequence</c>. Merely looking at an unidentified object through that property
	/// therefore changes both the object and the save &mdash; which would mean a founder who opened
	/// this flow and cancelled had already paid for it. Only <c>IDIfAssigned</c>
	/// (GameObject.cs 424-434) is a pure read, so it is the only identity this family will use, and
	/// an object the world has not already identified is refused rather than minted. The native
	/// sequence is never wound back: a counter that went forward is not this mod's to rewrite.
	/// </para>
	/// </remarks>
	public static class KingdomArtifactRecognitionRuntime
	{
		public static bool TrySnapshotExplicit(GameObject Selected, string DeedId,
			string DeedText, long Tick, out KingdomArtifactSnapshot Snapshot,
			out string Failure)
		{
			Snapshot = null; Failure = null;
			if (!GameObject.Validate(Selected) || string.IsNullOrEmpty(Selected.Blueprint) ||
				Selected.CurrentCell == null || Selected.CurrentZone == null || Tick < 0L)
			{
				Failure = "The exact selected object is not presently observable."; return false;
			}
			string assigned = Selected.IDIfAssigned;
			if (string.IsNullOrEmpty(assigned))
			{
				Failure = "The world has never given that thing an exact identity of its own, so "
					+ "there is nothing the city could name it by. It cannot be recognized.";
				return false;
			}
			Snapshot = new KingdomArtifactSnapshot
			{
				ObjectId = "taf:object:" + assigned,
				Blueprint = Selected.Blueprint,
				DisplayName = Selected.ShortDisplayNameStripped,
				OwnerId = string.IsNullOrEmpty(Selected.Physics?.Owner) ? null :
					"taf:owner:" + Selected.Physics.Owner,
				LocationId = "taf:zone:" + Selected.CurrentZone.ZoneID + ":" +
					Selected.CurrentCell.X + ":" + Selected.CurrentCell.Y,
				DeedId = DeedId, DeedText = DeedText, ObservedTick = Tick
			};
			Snapshot.SnapshotDigest = KingdomArtifactRecognitionRules.SnapshotDigest(Snapshot);
			KingdomArtifactRecognitionBook probe = new KingdomArtifactRecognitionBook();
			if (!KingdomArtifactRecognitionRules.TryRecognize(probe, 0L, Snapshot,
				KingdomArtifactRecognitionKind.Remark, 0, null, Tick, out _, out Failure))
			{
				Snapshot = null; return false;
			}
			return true;
		}
	}
}
