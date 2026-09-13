using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Save-scoped wake for an interrupted quickstart. One unchanged receipt is attempted once;
	/// measured progress publishes a new receipt and therefore authorizes the next attempt.
	/// <para>
	/// What counts as finished is <see cref="KingdomQuickstartRules.IsTerminal"/>, not a phase
	/// comparison: a Complete receipt that still owes or is part-way through its founding cohort
	/// is not done, and a Complete receipt whose founders were omitted or faulted is.
	/// </para>
	/// </summary>
	[Serializable]
	public sealed class KingdomQuickstartLifecycle : IPlayerSystem
	{
		[NonSerialized]
		private string AttemptedReceipt = "";

		public override void Register(XRLGame Game, IEventRegistrar Registrar)
		{
			Registrar.Register(AfterGameLoadedEvent.ID);
			Registrar.Register(ZoneActivatedEvent.ID);
			Registrar.Register(EndTurnEvent.ID);
		}

		public override void AfterLoad(XRLGame Game)
		{
			base.AfterLoad(Game);
			AttemptedReceipt = "";
		}

		public override bool HandleEvent(AfterGameLoadedEvent E)
		{
			AttemptedReceipt = "";
			TryResume();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			AttemptedReceipt = "";
			TryResume();
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			TryResume();
			return base.HandleEvent(E);
		}

		private void TryResume()
		{
			XRLGame game = The.Game;
			if (game == null || !KingdomQuickstartRules.IsMode(game.gameMode)) return;
			string raw = game.GetStringGameState(KingdomQuickstartRules.ReceiptState, null);
			if (string.IsNullOrEmpty(raw) || string.Equals(raw, AttemptedReceipt,
				StringComparison.Ordinal)
				|| !KingdomQuickstartRules.TryDecode(raw,
					out KingdomQuickstartReceipt receipt)
				|| KingdomQuickstartRules.IsTerminal(receipt)) return;
			AttemptedReceipt = raw;
			if (!KingdomQuickstartBootstrap.Run(game, out string failure))
				MetricsManager.LogError("ThousandAndFirst quickstart recovery: "
					+ (string.IsNullOrEmpty(failure) ? "unspecified refusal" : failure));
		}
	}
}
