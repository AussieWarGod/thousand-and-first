#if !TAF_TESTS
using System;
using XRL;

namespace ThousandAndFirst
{
	internal static partial class KingdomSaveSystemRosterRuntime
	{
		/// <summary>
		/// Counts exact runtime types in one detached pass. This deliberately does not use
		/// RequireSystem: GetSystem/RequireSystem return only the first exact type and RequireSystem
		/// manufactures a new instance when none survived (XRL/XRLGame.cs:286-332). The count must
		/// therefore be frozen before any recovery shell can conceal the absence.
		/// </summary>
		internal static KingdomSaveSystemRosterCounts Snapshot(XRLGame Game)
		{
			KingdomSaveSystemRosterCounts counts = new KingdomSaveSystemRosterCounts();
			if (Game?.Systems == null)
			{
				counts.Realm = -1; counts.Seal = -1; counts.CivicMemory = -1;
				counts.Succession = -1; counts.Inheritance = -1;
				return counts;
			}
			for (int i = 0; i < Game.Systems.Count; i++)
			{
				IGameSystem system = Game.Systems[i];
				if (system == null) continue;
				Type type = system.GetType();
				if (type == typeof(KingdomSystem)) counts.Realm++;
				else if (type == typeof(KingdomSeal)) counts.Seal++;
				else if (type == typeof(KingdomCivicMemorySystem)) counts.CivicMemory++;
				else if (type == typeof(KingdomSuccession)) counts.Succession++;
				else if (type == typeof(KingdomInheritanceLifecycle)) counts.Inheritance++;
			}
			return counts;
		}

		/// <summary>Creates only types named by a frozen plan. LoadSystems silently omits a
		/// composite that never became IGameSystem (XRL/XRLGame.cs:1592-1603); the reader can return
		/// null after type resolution or construction failed (XRL/World/SerializationReader.cs:180-224,
		/// :1320-1339, :2120-2133). These are inert witnesses after the recovery callback latches.</summary>
		internal static void Ensure(XRLGame Game, int Mask, bool LegacyCivicAbsence)
		{
			if (Game == null) throw new ArgumentNullException("Game");
			if ((Mask & KingdomSaveSystemRosterRules.RealmBit) != 0
				&& Game.GetSystem<KingdomSystem>() == null)
				Game.RequireSystem<KingdomSystem>();
			if ((Mask & KingdomSaveSystemRosterRules.SealBit) != 0
				&& Game.GetSystem<KingdomSeal>() == null)
				Game.RequireSystem<KingdomSeal>();
			if ((Mask & KingdomSaveSystemRosterRules.CivicMemoryBit) != 0
				&& Game.GetSystem<KingdomCivicMemorySystem>() == null)
			{
				KingdomCivicMemorySystem memory =
					Game.RequireSystem<KingdomCivicMemorySystem>();
				if (LegacyCivicAbsence) memory.AdoptRosterLegacyAbsence();
			}
			if ((Mask & KingdomSaveSystemRosterRules.SuccessionBit) != 0
				&& Game.GetSystem<KingdomSuccession>() == null)
				Game.RequireSystem<KingdomSuccession>();
			if ((Mask & KingdomSaveSystemRosterRules.InheritanceBit) != 0
				&& Game.GetSystem<KingdomInheritanceLifecycle>() == null)
				Game.RequireSystem<KingdomInheritanceLifecycle>();
		}

		internal static bool IsPreparedRemovalSave(XRLGame Game, bool MarkerPresent,
			KingdomSaveSystemRosterCounts Counts)
		{
			if (Game == null || MarkerPresent
				|| !KingdomSaveSystemRosterRuntimePlan.Empty(Counts)) return false;
			string raw = Game.GetStringGameState(KingdomIdentityFenceRules.StateKey, null);
			return !string.IsNullOrEmpty(raw)
				&& KingdomRealmRetirementCodec.TryDecodeFence(raw,
					out KingdomIdentityFence fence, out string _)
				&& fence.GameId == Game.GameID
				&& fence.Disposition == KingdomIdentityFenceDisposition.PreparedForRemoval;
		}

		internal static bool Marker(XRLGame Game, out int Raw)
		{
			Raw = 0;
			if (Game == null || !Game.HasIntGameState(KingdomSaveSystemRosterRules.StateKey))
				return false;
			Raw = Game.GetIntGameState(KingdomSaveSystemRosterRules.StateKey);
			return true;
		}
	}
}
#endif
