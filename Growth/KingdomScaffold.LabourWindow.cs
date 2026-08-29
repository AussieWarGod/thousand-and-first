using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_KingdomScaffold
	{
		/// <summary>Canonical prior-loaded crew witness. Named property preserves positional ABI.</summary>
		public const string WorkWindowProperty = "r_TAF_ScaffoldWorkWindow";

		/// <summary>
		/// Charges one elapsed interval. Current receipt-backed work uses only a canonical witness
		/// anchored to the previous labour tick. Crew observed now can price only the next interval.
		/// Schema-zero and receiptless scaffolds retain their shipped current-reading behaviour.
		/// </summary>
		private bool AdvanceLabour(long TimeTick)
		{
			if (RemainingTicks <= 0L && LastWorkedTick <= 0L)
			{
				long authored = CompleteTick - TimeTick;
				RemainingTicks = authored > 0L ? authored : 1L;
				LastWorkedTick = TimeTick;
				return false;
			}
			if (RemainingTicks <= 0L) return true;
			long previous = LastWorkedTick;
			int schema = ReceiptLabourSchema();
			bool windowed = schema == KingdomConstructionRules.BuildTruthSchema;
			if (TimeTick < previous) return false;
			if (TimeTick == previous)
			{
				if (windowed) CaptureCurrentLabourWindow(TimeTick);
				return false;
			}
			KingdomScaffoldLabourWindow prior = null;
			bool witnessed = windowed && KingdomScaffoldLabourWindowRules.TryForInterval(
				ParentObject.GetStringProperty(WorkWindowProperty), previous, out prior);

			int pricedEffectiveness = 0;
			KingdomSystem legacySystem = null;
			if (schema == 0)
			{
				pricedEffectiveness = EffectivenessOf(out int freeHands,
					out legacySystem, out bool selected);
				if (selected) Say(legacySystem, freeHands);
				else ShortfallSaid = false;
			}
			else if (witnessed) pricedEffectiveness = prior.EffectivenessPercent;
			KingdomScaffoldLabourStep progress = KingdomScaffoldLabourRules.Advance(
				previous, TimeTick, RemainingTicks, pricedEffectiveness);
			LastWorkedTick = progress.NextTick;
			RemainingTicks = progress.RemainingTicks;
			if (progress.Complete)
			{
				CompleteTick = progress.CompletionTick;
				ParentObject.RemoveStringProperty(WorkWindowProperty);
				return true;
			}
			if (windowed) CaptureCurrentLabourWindow(TimeTick);
			return false;
		}

		private void CaptureCurrentLabourWindow(long TimeTick)
		{
			int effectiveness = EffectivenessOf(out int freeHands,
				out KingdomSystem system, out bool selected);
			if (ParentObject.GetIntProperty(KingdomConstructionPresence.SchemaProperty)
				!= KingdomConstructionPresenceRules.Schema)
			{
				effectiveness = 0;
				freeHands = 0;
				selected = false;
			}
			KingdomScaffoldLabourWindow current = new KingdomScaffoldLabourWindow
			{
				Tick = TimeTick,
				EffectivenessPercent = effectiveness,
				Hands = freeHands,
				Selected = selected
			};
			if (!KingdomScaffoldLabourWindowRules.TryEncode(current, out string encoded))
			{
				current.EffectivenessPercent = 0;
				freeHands = current.Hands = 0;
				selected = current.Selected = false;
				if (!KingdomScaffoldLabourWindowRules.TryEncode(current, out encoded)) return;
			}
			ParentObject.SetStringProperty(WorkWindowProperty, encoded);
			if (ParentObject.GetStringProperty(WorkWindowProperty) != encoded)
			{
				current.EffectivenessPercent = 0;
				current.Hands = 0;
				current.Selected = false;
				selected = false;
				if (KingdomScaffoldLabourWindowRules.TryEncode(current, out encoded))
				{
					ParentObject.SetStringProperty(WorkWindowProperty, encoded);
					if (ParentObject.GetStringProperty(WorkWindowProperty) == encoded)
					{
						ShortfallSaid = false;
						return;
					}
				}
				ParentObject.RemoveStringProperty(WorkWindowProperty);
			}
			if (selected) Say(system, freeHands);
			else ShortfallSaid = false;
		}

		/// <summary>Zero is exact compatibility. Unknown receipt state fails closed at zero work.</summary>
		private int ReceiptLabourSchema()
		{
			string receipt = ParentObject.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (string.IsNullOrEmpty(receipt)) return 0;
			KingdomConstructionJob job;
			if (!KingdomConstruction.TryFind(receipt, out job)
				|| !KingdomConstruction.IsCurrent(job)
				|| !KingdomConstruction.HasReceipt(ParentObject, job)) return -1;
			if (job.BuildTruthSchema == 0) return 0;
			return job.BuildTruthSchema == KingdomConstructionRules.BuildTruthSchema
				? job.BuildTruthSchema : -1;
		}
	}
}
