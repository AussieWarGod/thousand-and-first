using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Engine-free validation and resolution for semantic fixture pose families.</summary>
	public static partial class KingdomArchitectureRules
	{
		private static readonly HashSet<string> CardinalPoseIdentityAllowlist =
			new HashSet<string>(StringComparer.Ordinal);

		/// <summary>
		/// Cardinal siblings have different concrete blueprint names. TAF fixtures may have exact-name
		/// gameplay consumers, so each one requires an explicit source-reviewed admission. Vanilla
		/// stair identity is likewise runtime authority rather than interchangeable visual identity.
		/// </summary>
		internal static bool CardinalPoseIdentityAllowed(string SemanticBlueprint)
		{
			return ValidBlueprint(SemanticBlueprint)
				&& SemanticBlueprint != "StairsDown" && SemanticBlueprint != "StairsUp"
				&& (!SemanticBlueprint.StartsWith("r_Kingdom", StringComparison.Ordinal)
					|| CardinalPoseIdentityAllowlist.Contains(SemanticBlueprint));
		}

		public static bool TryParsePoseMode(string Text, out ArchitecturePoseMode Mode)
		{
			Mode = ArchitecturePoseMode.Invariant;
			string folded = FoldType(Text);
			if (folded == "invariant") Mode = ArchitecturePoseMode.Invariant;
			else if (folded == "connected") Mode = ArchitecturePoseMode.Connected;
			else if (folded == "cardinal") Mode = ArchitecturePoseMode.Cardinal;
			else return false;
			return true;
		}

		public static bool TryResolvePose(IList<ArchitecturePoseDraft> Poses,
			string SemanticBlueprint, bool HasLocalOrientation,
			ArchitectureFacing LocalOrientation, ArchitectureFacing LotFacing,
			out string ConcreteBlueprint, out string Failure)
		{
			ConcreteBlueprint = null;
			if (!TryPoseCatalogue(Poses,
				out Dictionary<string, ArchitecturePoseDraft> poses, out Failure)) return false;
			return TryResolvePose(poses, SemanticBlueprint, HasLocalOrientation,
				LocalOrientation, LotFacing, out ConcreteBlueprint, out Failure);
		}

		internal static bool TryCreatePoseRegistry(IList<ArchitecturePoseDraft> Poses,
			IEnumerable<string> Poisoned, out ArchitecturePoseRegistry Registry,
			out string Failure)
		{
			Registry = null;
			if (!TryPoseCatalogue(Poses,
				out Dictionary<string, ArchitecturePoseDraft> poses, out Failure)) return false;
			HashSet<string> poisoned = new HashSet<string>(StringComparer.Ordinal);
			if (Poisoned != null)
			{
				foreach (string key in Poisoned)
				{
					if (poisoned.Count >= MaxPoseRecords || !ValidBlueprint(key)
						|| key[0] == '$' || poses.ContainsKey(key) || !poisoned.Add(key))
						return Fail("fixture pose poison catalogue is malformed, overlapping, or over the bound",
							out Failure);
				}
			}
			Registry = poses.Count == 0 && poisoned.Count == 0
				? ArchitecturePoseRegistry.Empty : new ArchitecturePoseRegistry(poses, poisoned);
			return true;
		}

		private static bool TryPoseCatalogue(IList<ArchitecturePoseDraft> Poses,
			out Dictionary<string, ArchitecturePoseDraft> Catalogue, out string Failure)
		{
			Catalogue = null;
			Failure = null;
			if (Poses != null && Poses.Count > MaxPoseRecords)
				return Fail("fixture pose catalogue is over the bound", out Failure);
			Dictionary<string, ArchitecturePoseDraft> result =
				new Dictionary<string, ArchitecturePoseDraft>(StringComparer.Ordinal);
			if (Poses == null) { Catalogue = result; return true; }
			for (int i = 0; i < Poses.Count; i++)
			{
				ArchitecturePoseDraft pose = Poses[i];
				if (pose == null || !ValidBlueprint(pose.Blueprint)
					|| pose.Blueprint[0] == '$' || !KnownPoseMode(pose.Mode)
					|| result.ContainsKey(pose.Blueprint))
					return Fail("fixture pose catalogue has a malformed or duplicate semantic blueprint",
						out Failure);
				if (!ValidPoseShape(pose))
					return Fail("fixture pose " + pose.Blueprint
						+ " has malformed or incoherent directional siblings", out Failure);
				result.Add(pose.Blueprint, pose);
			}
			Catalogue = result;
			return true;
		}

		private static bool TryResolvePose(Dictionary<string, ArchitecturePoseDraft> Poses,
			string SemanticBlueprint, bool HasLocalOrientation,
			ArchitectureFacing LocalOrientation, ArchitectureFacing LotFacing,
			out string ConcreteBlueprint, out string Failure)
		{
			ConcreteBlueprint = null;
			Failure = null;
			if (!ValidBlueprint(SemanticBlueprint))
				return Fail("scenery blueprint is malformed", out Failure);
			if (!KnownFacing(LotFacing))
				return Fail("fixture pose lot facing is malformed", out Failure);
			if (!Poses.TryGetValue(SemanticBlueprint, out ArchitecturePoseDraft pose))
			{
				if (HasLocalOrientation)
					return Fail("local orientation requires an exact cardinal fixture pose declaration",
						out Failure);
				ConcreteBlueprint = SemanticBlueprint;
				return true;
			}
			if (pose.Mode != ArchitecturePoseMode.Cardinal)
			{
				if (HasLocalOrientation)
					return Fail("connected or invariant scenery rejects local orientation", out Failure);
				ConcreteBlueprint = pose.Blueprint;
				return true;
			}
			if (!HasLocalOrientation || !KnownFacing(LocalOrientation) || !KnownFacing(LotFacing))
				return Fail("cardinal scenery requires one valid layer-local orientation", out Failure);
			ArchitectureFacing world = (ArchitectureFacing)
				(((int)LocalOrientation + (int)LotFacing) & 3);
			ConcreteBlueprint = world == ArchitectureFacing.North ? pose.North
				: world == ArchitectureFacing.East ? pose.East
				: world == ArchitectureFacing.South ? pose.South : pose.West;
			return true;
		}

		internal static bool TryResolvePose(ArchitecturePoseRegistry Registry,
			string SemanticBlueprint, bool HasLocalOrientation,
			ArchitectureFacing LocalOrientation, ArchitectureFacing LotFacing,
			out string ConcreteBlueprint, out string Failure)
		{
			ConcreteBlueprint = null;
			Failure = null;
			Registry = Registry ?? ArchitecturePoseRegistry.Empty;
			if (!ValidBlueprint(SemanticBlueprint))
				return Fail("scenery blueprint is malformed", out Failure);
			if (!KnownFacing(LotFacing))
				return Fail("fixture pose lot facing is malformed", out Failure);
			if (Registry.IsPoisoned(SemanticBlueprint))
				return Fail("selected scenery references a malformed fixture pose declaration",
					out Failure);
			if (!Registry.TryGet(SemanticBlueprint, out ArchitecturePoseDraft pose))
			{
				if (HasLocalOrientation)
					return Fail("local orientation requires an exact cardinal fixture pose declaration",
						out Failure);
				ConcreteBlueprint = SemanticBlueprint;
				return true;
			}
			if (pose.Mode != ArchitecturePoseMode.Cardinal)
			{
				if (HasLocalOrientation)
					return Fail("connected or invariant scenery rejects local orientation", out Failure);
				ConcreteBlueprint = pose.Blueprint;
				return true;
			}
			if (!HasLocalOrientation || !KnownFacing(LocalOrientation))
				return Fail("cardinal scenery requires one valid layer-local orientation", out Failure);
			ArchitectureFacing world = (ArchitectureFacing)
				(((int)LocalOrientation + (int)LotFacing) & 3);
			ConcreteBlueprint = world == ArchitectureFacing.North ? pose.North
				: world == ArchitectureFacing.East ? pose.East
				: world == ArchitectureFacing.South ? pose.South : pose.West;
			return true;
		}

		private static bool ValidPoseShape(ArchitecturePoseDraft Pose)
		{
			if (Pose.Mode != ArchitecturePoseMode.Cardinal)
				return Pose.North == null && Pose.East == null
					&& Pose.South == null && Pose.West == null;
			return ValidBlueprint(Pose.North) && ValidBlueprint(Pose.East)
				&& ValidBlueprint(Pose.South) && ValidBlueprint(Pose.West);
		}

		private static bool KnownPoseMode(ArchitecturePoseMode Mode)
		{
			return Mode == ArchitecturePoseMode.Invariant || Mode == ArchitecturePoseMode.Connected
				|| Mode == ArchitecturePoseMode.Cardinal;
		}
	}
}
