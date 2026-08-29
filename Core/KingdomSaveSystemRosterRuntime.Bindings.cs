#if !TAF_TESTS
using XRL;

namespace ThousandAndFirst
{
	/// <summary>Single fail-closed boundary used after an exact pre-require observation. Recovery
	/// may create shells, but only this callback is allowed to turn them into inert witnesses.</summary>
	internal delegate void KingdomSaveSystemRosterRecoveryCallback(XRLGame Game, string Cause);

	internal static class KingdomSaveSystemRosterRecoveryBindings
	{
		internal static void Refuse(XRLGame Game, string Cause)
		{
			if (Game?.Systems == null) return;
			string cause = string.IsNullOrEmpty(Cause)
				? "the saved game-system roster could not be proved" : Cause;
			for (int i = 0; i < Game.Systems.Count; i++)
			{
				IGameSystem candidate = Game.Systems[i];
				if (candidate == null) continue;
				System.Type type = candidate.GetType();
				if (type == typeof(KingdomSystem))
					((KingdomSystem)candidate).RefuseSaveRosterLoss(cause);
				else if (type == typeof(KingdomSeal))
					((KingdomSeal)candidate).RefuseSaveRosterLoss(cause);
				else if (type == typeof(KingdomCivicMemorySystem))
					((KingdomCivicMemorySystem)candidate).RefuseRosterLoss(cause);
				else if (type == typeof(KingdomSuccession))
					((KingdomSuccession)candidate).RefuseSaveRosterLoss(cause);
				else if (type == typeof(KingdomInheritanceLifecycle))
					KingdomSaveSystemRosterInheritanceGuard.Refuse(
						(KingdomInheritanceLifecycle)candidate, cause);
			}
		}
	}

	public partial class KingdomSystem
	{
		internal bool SaveRosterHasDecodedRealm
		{
			get { return CustomReadCompleted && !LoadFailed; }
		}

		internal void RefuseSaveRosterLoss(string Cause)
		{
			// Nonserialized, false-to-true only on this boundary. BeforeSave is the durable veto.
			LoadFailed = true;
		}
	}

	public sealed partial class KingdomSeal
	{
		internal void RefuseSaveRosterLoss(string Cause)
		{
			// Do not neutralize fields here: the old bytes remain evidence until the session ends.
			LoadFailed = true;
			SealDisabled = true;
		}
	}

	public sealed partial class KingdomSuccession
	{
		internal void RefuseSaveRosterLoss(string Cause)
		{
			LoadFailed = true;
			SuccessionDisabled = true;
		}
	}
}
#endif
