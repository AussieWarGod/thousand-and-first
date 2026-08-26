using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		public override bool HandleEvent(AfterReputationChangeEvent E)
		{
			if (!KingdomMaster.AutomaticWorkAllowed(this)) return base.HandleEvent(E);
			// The realm's own faction is excluded from the mirror below — a polity does not hold a
			// standing with itself — but it is the one faction whose reputation cell says what the
			// realm thinks of its founder, so it is read here instead of ignored.
			Guard("realm regard", delegate
			{
				if (Founded && !E.Transient && E.Faction != null && E.Faction.Name == KingdomFactionName)
				{
					OnRealmRegardChanged(E.Type);
				}
			});
			Guard("reputation mirror", delegate
			{
				if (Founded && !E.Transient && E.Faction != null && E.Faction.Name != KingdomFactionName && E.Faction.Name != "Player")
				{
					int delta = KingdomRules.SpilloverDelta(E.To - E.From, Stage);
					AdjustStanding(E.Faction.Name, delta);
					KingdomLog.Log("mirror: " + E.Faction.Name + " rep " + E.From + "->" + E.To + " spillover=" + delta + " standing=" + GetStanding(E.Faction.Name));
				}
			});
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(AfterGameLoadedEvent E)
		{
			if (LoadFailed)
			{
				Guard("load failure report", ReportLoadFailure);
			}
			XRLGame game = The.Game;
			if (game == null || !KingdomMaster.ObserveAutomaticWake(this, game.TimeTicks))
				return base.HandleEvent(E);
			Guard("feeling re-assert", ReassertFeelings);
			return base.HandleEvent(E);
		}

		/// <summary>
		/// The kingdom's standing with a faction. This is the kingdom's own ledger, separate
		/// from the founder's personal reputation: a faction may love the founder and resent
		/// the polity, or the reverse.
		/// </summary>
		/// <param name="FactionName">Faction name (not display name).</param>
		/// <returns>Standing on the vanilla reputation scale; 0 if never recorded.</returns>
		public int GetStanding(string FactionName)
		{
			if (FactionName == null || !Standings.TryGetValue(FactionName, out var value))
			{
				return 0;
			}
			return value;
		}

		/// <summary>
		/// Sets the kingdom's standing with a faction and mirrors the result into that
		/// faction's feeling toward the kingdom, so NPC attitudes follow.
		/// </summary>
		/// <param name="FactionName">Faction name (not display name). Ignored if null.</param>
		/// <param name="Value">New standing on the vanilla reputation scale.</param>
		/// <param name="Mirror">False to defer the feeling write (bulk edits); the mirror is
		/// re-asserted on game load regardless.</param>
		public void SetStanding(string FactionName, int Value, bool Mirror = true)
		{
			if (FactionName == null)
			{
				return;
			}
			Standings[FactionName] = Value;
			if (Mirror)
			{
				MirrorFeeling(FactionName);
			}
		}

		/// <summary>
		/// Adjusts the kingdom's standing with a faction by a delta. Use this rather than
		/// writing <see cref="Standings"/> directly so the feeling mirror stays consistent.
		/// </summary>
		/// <param name="FactionName">Faction name (not display name). Ignored if null.</param>
		/// <param name="Delta">Signed change; zero is a no-op.</param>
		/// <param name="Mirror">False to defer the feeling write.</param>
		public void AdjustStanding(string FactionName, int Delta, bool Mirror = true)
		{
			if (Delta != 0)
			{
				SetStanding(FactionName, GetStanding(FactionName) + Delta, Mirror);
			}
		}

		/// <summary>
		/// Writes one faction's feeling toward the kingdom from its recorded standing.
		/// Safe to call when unfounded or for unknown factions; does nothing in those cases.
		/// </summary>
		/// <param name="FactionName">Faction name (not display name).</param>
		public void MirrorFeeling(string FactionName)
		{
			if (!Founded || string.IsNullOrEmpty(FactionName)
				|| FactionName == KingdomFactionName || FactionName == "Player")
			{
				return;
			}
			// One projection is never allowed to abort the rest of a load-time reassertion. The
			// standings dictionary is durable truth; a missing or hostile faction implementation
			// merely leaves its derived feeling stale until the next retry.
			Guard("feeling projection " + (FactionName ?? "?"), delegate
			{
				// GetIfExists, never Get: a standings key can outlive the faction it names when a
				// save moves between builds.
				Faction faction = Factions.GetIfExists(FactionName);
				if (faction != null)
				{
					faction.SetFactionFeeling(KingdomFactionName,
						Reputation.GetFeeling((float)GetStanding(FactionName)));
				}
			});
		}

		/// <summary>
		/// Rewrites, from recorded state, every faction feeling the kingdom depends on. Called
		/// after load because the engine rebuilds feelings from its own reputation table and knows
		/// nothing about the kingdom's separate standings ledger.
		/// </summary>
		public void ReassertFeelings()
		{
			if (!Founded)
			{
				return;
			}
			foreach (KeyValuePair<string, int> standing in Standings)
			{
				MirrorFeeling(standing.Key);
			}
			// Derived from the founder's actual reputation, never hardcoded to 100. A realm holds
			// whatever opinion of its founder their deeds earned it: stamping love here on every
			// load would silently undo a fall in regard the moment the save was reloaded, and the
			// expulsion ladder reads no other surface. The context-free overload is deliberate —
			// the engine's own rebuild uses the holy-place-sensitive one, which can materialise a
			// neutral value as -50 depending on where the founder happens to be standing.
			Faction realm = Factions.GetIfExists(KingdomFactionName);
			if (realm != null)
			{
				realm.SetFactionFeeling("Player", Reputation.GetFeeling((float)FounderRegard()));
			}
		}

	}
}
