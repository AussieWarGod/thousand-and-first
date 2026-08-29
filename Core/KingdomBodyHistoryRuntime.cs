using System;
using System.Collections.Generic;
using System.Text;
using XRL.World;
using XRL.World.Anatomy;

namespace ThousandAndFirst
{
	/// <summary>
	/// Exact Qud 2.0.211.51 evidence: GameObject.cs:139 exposes Body and :424-434
	/// exposes non-minting IDIfAssigned; Parts/Body.cs:919-923 returns live parts
	/// in anatomy order; Anatomy/BodyPart.cs:56,108,203,359,5728 exposes Type,
	/// stored _ID, ParentPart/Parts, Abstract, Cybernetics and GetOrdinalName().
	/// </summary>
	public static partial class KingdomBodyHistoryRuntime
	{
		/// <summary>
		/// Reads only one caller-provided, already loaded body. No ID is minted, no
		/// zone is resolved, and no anatomy or equipment state is changed.
		/// </summary>
		public static bool TryReadLoaded(GameObject ExactBody, string ResidentIdentity,
			string ExpectedBodyObjectId, long Tick,
			out KingdomLiveAnatomySnapshot Snapshot, out string Failure)
		{
			Snapshot = null;
			Failure = null;
			string objectId = ExactBody?.IDIfAssigned;
			if (!GameObject.Validate(ExactBody) || ExactBody.CurrentZone == null
				|| ExactBody.CurrentCell == null || ExactBody.Body == null
				|| string.IsNullOrEmpty(objectId)
				|| !string.Equals(ExpectedBodyObjectId, "taf:object:" + objectId,
					StringComparison.Ordinal)
				|| string.IsNullOrEmpty(ResidentIdentity) || Tick < 0)
			{
				Failure = "The exact loaded resident body is unavailable or was replaced.";
				return false;
			}

			List<BodyPart> native = ExactBody.Body.GetParts();
			List<KingdomLiveAnatomyPart> parts = new List<KingdomLiveAnatomyPart>();
			for (int i = 0; native != null && i < native.Count; i++)
			{
				BodyPart part = native[i];
				if (part == null || part.Abstract) continue;
				if (string.IsNullOrEmpty(part.Type)
					|| string.IsNullOrEmpty(part.Name))
				{
					Failure = "The loaded anatomy contains an incomplete part.";
					return false;
				}
				GameObject cybernetics = part.Cybernetics;
				parts.Add(new KingdomLiveAnatomyPart
				{
					NativeOrderIndex = parts.Count,
					NativePath = ReadNativePath(part),
					BodyPartId = part._ID,
					Type = part.Type,
					OrdinalName = part.GetOrdinalName(),
					Category = part.Category,
					Extrinsic = part.Extrinsic,
					CyberneticsBlueprint = GameObject.Validate(cybernetics)
						? cybernetics.Blueprint ?? "" : ""
				});
				if (parts.Count > KingdomBodyHistoryRules.MaxAnatomyParts)
				{
					Failure = "The loaded anatomy exceeds the bounded civic view.";
					return false;
				}
			}

			Snapshot = new KingdomLiveAnatomySnapshot
			{
				ResidentIdentity = ResidentIdentity,
				BodyObjectId = ExpectedBodyObjectId,
				ObservedTick = Tick,
				OrderedParts = parts
			};
			Snapshot.BodyIdentityDigest = KingdomBodyHistoryRules.AnatomyDigest(
				Snapshot.ResidentIdentity, Snapshot.BodyObjectId, Snapshot.OrderedParts);
			if (KingdomBodyHistoryRules.TryView(Snapshot, out _, out Failure)) return true;
			Snapshot = null;
			return false;
		}

		private static string ReadNativePath(BodyPart Part)
		{
			List<int> reverse = new List<int>();
			BodyPart current = Part;
			while (current?.ParentPart != null)
			{
				BodyPart parent = current.ParentPart;
				int child = parent.Parts == null ? -1 : parent.Parts.IndexOf(current);
				if (child < 0) return "";
				reverse.Add(child);
				current = parent;
			}
			StringBuilder path = new StringBuilder("0");
			for (int i = reverse.Count - 1; i >= 0; i--)
				path.Append('/').Append(reverse[i]);
			return path.ToString();
		}
	}
}
