using System;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Persisted value last observed for one elapsed-time module option.</summary>
	public enum KingdomElapsedOptionState : byte
	{
		Unobserved = 0,
		Disabled = 1,
		Enabled = 2
	}

	/// <summary>Why an elapsed-time module may need to reanchor its owned clocks.</summary>
	public enum KingdomElapsedOptionTransition : byte
	{
		None = 0,
		InitializedDisabled = 1,
		InitializedEnabled = 2,
		Disabled = 3,
		Enabled = 4,
		MasterRelatchedDisabled = 5,
		MasterRelatchedEnabled = 6
	}

	/// <summary>What an elapsed-time module may do on this wake.</summary>
	public enum KingdomElapsedOptionAction : byte
	{
		Disabled = 0,
		AnchorDisabled = 1,
		AnchorEnabled = 2,
		Wait = 3,
		Run = 4,
		Invalid = 5
	}

	/// <summary>
	/// One bounded persisted observation. <see cref="MasterResumeToken"/> makes a module notice a
	/// master-off span even though the master hot path correctly never calls module code while off.
	/// </summary>
	public readonly struct KingdomElapsedOptionRecord
	{
		public readonly KingdomElapsedOptionState State;
		public readonly long ObservedTick;
		public readonly long MasterResumeToken;

		public KingdomElapsedOptionRecord(KingdomElapsedOptionState State, long ObservedTick,
			long MasterResumeToken)
		{
			this.State = State;
			this.ObservedTick = ObservedTick;
			this.MasterResumeToken = MasterResumeToken;
		}

		public static KingdomElapsedOptionRecord Unobserved
		{
			get { return new KingdomElapsedOptionRecord(KingdomElapsedOptionState.Unobserved, 0L, 0L); }
		}
	}

	/// <summary>Pure result. Invalid never grants a module permission to run.</summary>
	public readonly struct KingdomElapsedOptionDecision
	{
		public readonly bool Valid;
		public readonly KingdomElapsedOptionRecord Record;
		public readonly KingdomElapsedOptionTransition Transition;
		public readonly KingdomElapsedOptionAction Action;

		public KingdomElapsedOptionDecision(bool Valid, KingdomElapsedOptionRecord Record,
			KingdomElapsedOptionTransition Transition, KingdomElapsedOptionAction Action)
		{
			this.Valid = Valid;
			this.Record = Record;
			this.Transition = Transition;
			this.Action = Action;
		}
	}

	/// <summary>
	/// Shared pure law for elapsed-time options. Transition observation always precedes due work;
	/// every initialization/disable/resume/master relatch consumes its wake, and an unchanged
	/// enabled latch still refuses a second call at the transition tick. Module code owns whether
	/// disabling cancels or preserves its unpaid semantic progress.
	/// </summary>
	public static class KingdomElapsedOptionRules
	{
		public const int MaxEncodedChars = 64;

		public static KingdomElapsedOptionDecision Observe(KingdomElapsedOptionRecord Prior,
			bool ConfiguredEnabled, long CurrentMasterResumeToken, long Now)
		{
			if (!WellFormed(Prior) || CurrentMasterResumeToken < 0L || Now < 0L
				|| CurrentMasterResumeToken < Prior.MasterResumeToken || Now < Prior.ObservedTick)
			{
				return new KingdomElapsedOptionDecision(false, Prior,
					KingdomElapsedOptionTransition.None, KingdomElapsedOptionAction.Invalid);
			}

			KingdomElapsedOptionState observed = ConfiguredEnabled
				? KingdomElapsedOptionState.Enabled : KingdomElapsedOptionState.Disabled;
			KingdomElapsedOptionTransition transition;
			if (Prior.State == KingdomElapsedOptionState.Unobserved)
			{
				transition = ConfiguredEnabled
					? KingdomElapsedOptionTransition.InitializedEnabled
					: KingdomElapsedOptionTransition.InitializedDisabled;
			}
			else if (Prior.State != observed)
			{
				// A module change made while master was off owns policy ahead of the master
				// relatch: disabling must still cancel what that module says is uncommitted.
				transition = ConfiguredEnabled
					? KingdomElapsedOptionTransition.Enabled
					: KingdomElapsedOptionTransition.Disabled;
			}
			else if (Prior.MasterResumeToken != CurrentMasterResumeToken)
			{
				transition = ConfiguredEnabled
					? KingdomElapsedOptionTransition.MasterRelatchedEnabled
					: KingdomElapsedOptionTransition.MasterRelatchedDisabled;
			}
			else
			{
				KingdomElapsedOptionAction unchanged = ConfiguredEnabled
					? ((Prior.ObservedTick == Now) ? KingdomElapsedOptionAction.Wait
						: KingdomElapsedOptionAction.Run)
					: KingdomElapsedOptionAction.Disabled;
				return new KingdomElapsedOptionDecision(true, Prior,
					KingdomElapsedOptionTransition.None, unchanged);
			}

			KingdomElapsedOptionRecord next = new KingdomElapsedOptionRecord(observed, Now,
				CurrentMasterResumeToken);
			return new KingdomElapsedOptionDecision(true, next, transition,
				ConfiguredEnabled ? KingdomElapsedOptionAction.AnchorEnabled
					: KingdomElapsedOptionAction.AnchorDisabled);
		}

		public static bool WellFormed(KingdomElapsedOptionRecord Record)
		{
			if (Record.ObservedTick < 0L || Record.MasterResumeToken < 0L) return false;
			switch (Record.State)
			{
			case KingdomElapsedOptionState.Unobserved:
				return Record.ObservedTick == 0L && Record.MasterResumeToken == 0L;
			case KingdomElapsedOptionState.Disabled:
			case KingdomElapsedOptionState.Enabled:
				return true;
			default:
				return false;
			}
		}

		/// <summary>
		/// Classifies the transition a late-loaded local owner missed after the realm epoch was
		/// published. Local module state changes outrank a simultaneous master relatch. An empty
		/// owner is additive-save initialization; a foreign nonempty owner is cancellation policy.
		/// </summary>
		public static KingdomElapsedOptionTransition LocalTransition(bool ConfiguredEnabled,
			bool ForeignOwner, bool PriorDecoded, KingdomElapsedOptionRecord Prior,
			KingdomElapsedOptionRecord Current)
		{
			KingdomElapsedOptionTransition module = ConfiguredEnabled
				? KingdomElapsedOptionTransition.Enabled
				: KingdomElapsedOptionTransition.Disabled;
			KingdomElapsedOptionTransition initialized = ConfiguredEnabled
				? KingdomElapsedOptionTransition.InitializedEnabled
				: KingdomElapsedOptionTransition.InitializedDisabled;
			if (!WellFormed(Current)
				|| Current.State == KingdomElapsedOptionState.Unobserved) return module;
			if (ForeignOwner) return module;
			if (!PriorDecoded || !WellFormed(Prior)
				|| Prior.State == KingdomElapsedOptionState.Unobserved) return initialized;
			if (Prior.State != Current.State) return module;
			if (Prior.MasterResumeToken != Current.MasterResumeToken)
				return ConfiguredEnabled
					? KingdomElapsedOptionTransition.MasterRelatchedEnabled
					: KingdomElapsedOptionTransition.MasterRelatchedDisabled;
			// A changed epoch with the same state/token means a complete module off/on cycle was
			// first observed elsewhere. Exact equality needs no transition.
			return Prior.ObservedTick == Current.ObservedTick
				? KingdomElapsedOptionTransition.None : module;
		}

		/// <summary>Canonical bounded wire: empty is additive-save unobserved; otherwise v1.</summary>
		public static string Encode(KingdomElapsedOptionRecord Record)
		{
			if (!WellFormed(Record)) return null;
			if (Record.State == KingdomElapsedOptionState.Unobserved) return "";
			return "v1|" + (Record.State == KingdomElapsedOptionState.Enabled ? "E" : "D")
				+ "|" + Record.ObservedTick.ToString(CultureInfo.InvariantCulture)
				+ "|" + Record.MasterResumeToken.ToString(CultureInfo.InvariantCulture);
		}

		public static bool TryDecode(string Encoded, out KingdomElapsedOptionRecord Record)
		{
			Record = KingdomElapsedOptionRecord.Unobserved;
			if (string.IsNullOrEmpty(Encoded)) return true;
			if (Encoded.Length > MaxEncodedChars) return false;
			string[] fields = Encoded.Split('|');
			if (fields.Length != 4 || fields[0] != "v1"
				|| (fields[1] != "D" && fields[1] != "E")) return false;
			long tick;
			long token;
			if (!TryCanonicalNonNegativeLong(fields[2], out tick)
				|| !TryCanonicalNonNegativeLong(fields[3], out token)) return false;
			Record = new KingdomElapsedOptionRecord(fields[1] == "E"
				? KingdomElapsedOptionState.Enabled : KingdomElapsedOptionState.Disabled,
				tick, token);
			return true;
		}

		private static bool TryCanonicalNonNegativeLong(string Text, out long Value)
		{
			Value = 0L;
			return !string.IsNullOrEmpty(Text) && Text.Length <= 19
				&& long.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
				&& Value >= 0L && Value.ToString(CultureInfo.InvariantCulture) == Text;
		}
	}
}
