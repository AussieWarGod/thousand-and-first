using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	[Serializable]
	public class KingdomSystem : IGameSystem
	{
		public int SerializationVersion = 1;

		public string KingdomFactionName;

		public string KingdomDisplayName;

		public string Style = "common";

		public long FoundedTick;

		public GrowthStage Stage = GrowthStage.Camp;

		public int Population;

		public int DryStreak;

		public bool Withered;

		public bool HasShopkeeper;

		public bool NoRoomAnnounced;

		public long LastHeartbeatTick;

		public int IdleWorks;

		public bool IdleWorksAnnounced;

		public int ShopTier;

		public long NextArrivalTick;

		public int RaidState;

		public string RaidFactionName;

		public long RaidDueTick;

		public long LastRaidTick;

		public List<string> ClaimedZones = new List<string>();

		public Dictionary<string, string> ZoneDistricts = new Dictionary<string, string>();

		public List<string> ActiveDealKeys = new List<string>();

		public List<string> ActiveDealFactions = new List<string>();

		public List<long> DealNextTicks = new List<long>();

		public List<string> ChronicleEntries = new List<string>();

		public List<string> OutsiderEntries = new List<string>();

		public Dictionary<string, int> OriginCounts = new Dictionary<string, int>();

		public Dictionary<string, int> Standings = new Dictionary<string, int>();

		public bool Founded => !string.IsNullOrEmpty(KingdomFactionName);

		public override void Register(XRLGame Game, IEventRegistrar Registrar)
		{
			Registrar.Register(AfterReputationChangeEvent.ID);
			Registrar.Register(AfterGameLoadedEvent.ID);
			Registrar.Register(ZoneActivatedEvent.ID);
		}

		public override bool HandleEvent(ZoneActivatedEvent E)
		{
			if (!Founded || E.Zone == null || !ClaimedZones.Contains(E.Zone.ZoneID))
			{
				return base.HandleEvent(E);
			}
			KingdomSurvey survey = null;
			Guard("survey", delegate
			{
				survey = KingdomSurvey.Take(E.Zone);
			});
			if (survey == null)
			{
				return base.HandleEvent(E);
			}
			Guard("growth", delegate
			{
				KingdomGrowth.OnZoneActivated(this, E.Zone, survey);
			});
			Guard("trade", delegate
			{
				KingdomTrade.OnZoneActivated(this, E.Zone, survey);
			});
			Guard("raids", delegate
			{
				KingdomRaids.OnZoneActivated(this, E.Zone, survey);
			});
			return base.HandleEvent(E);
		}

		/// <summary>
		/// Runs an action inside the engine's event dispatch without letting it escape.
		/// A failure is logged and the step is skipped; the host game and other systems
		/// are never affected. All engine-invoked entry points must route through this.
		/// </summary>
		/// <param name="Step">Short label identifying the step, used in the error log.</param>
		/// <param name="Action">The work to perform.</param>
		public static void Guard(string Step, System.Action Action)
		{
			try
			{
				Action();
			}
			catch (System.Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: " + Step + " failed and was skipped", ex);
				KingdomLog.Log("GUARD caught in " + Step + ": " + ex.Message);
			}
		}

		public override bool HandleEvent(AfterReputationChangeEvent E)
		{
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
			if (!Founded || FactionName == KingdomFactionName || FactionName == "Player")
			{
				return;
			}
			Faction faction = Factions.Get(FactionName);
			if (faction != null)
			{
				faction.SetFactionFeeling(KingdomFactionName, Reputation.GetFeeling((float)GetStanding(FactionName)));
			}
		}

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
			Factions.Get(KingdomFactionName)?.SetFactionFeeling("Player", 100);
		}
	}
}
