#if !TAF_TESTS
using XRL;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCivicMemorySystem
	{
		/// <summary>
		/// Refuses a constructed shell whose custom reader never completed. Qud may return a
		/// half-built composite after a read fault; constructor defaults are not saved evidence.
		/// A genuinely absent legacy system is required only after <c>AfterLoad</c>, so it never
		/// enters this path.
		/// </summary>
		public override void AfterLoad(XRLGame Game)
		{
			base.AfterLoad(Game);
			if (!CustomReadCompleted)
				RefuseRosterLoss("the civic-memory system was constructed from a save but its "
					+ "custom read did not complete");
		}

		/// <summary>One-way recovery entry used when the saved roster proves this system vanished
		/// before construction. It keeps the session read-only; it never synthesizes saved rows.</summary>
		internal void RefuseRosterLoss(string Cause)
		{
			string cause = string.IsNullOrEmpty(Cause)
				? "the saved civic-memory system is absent" : Cause;
			if (CustomReadCompleted)
			{
				Records.Latch.Trip(cause);
				return;
			}
			Records.AdoptUnreadableFraming(new byte[0], cause);
		}

		/// <summary>Establishes the one lawful empty after the roster has independently proved an
		/// explicit new game or a decoded pre-C18 legacy save.</summary>
		internal void AdoptRosterLegacyAbsence()
		{
			Records.AdoptAbsent();
			CustomReadCompleted = true;
		}
	}
}
#endif
