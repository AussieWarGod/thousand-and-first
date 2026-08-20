using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	[Serializable]
	public class KingdomSystem : IGameSystem
	{
		private const int SerializationMagic = 1413563987;

		private const int CurrentSerializationVersion = 2;

		private const int FirstNamedSerializationVersion = 2;

		private const int LegacyReflectedSerializationVersion = 1;

		public int SerializationVersion = CurrentSerializationVersion;

		/// <summary>
		/// Set when <see cref="Read"/> could not interpret the saved state. Not serialized: it
		/// describes this load, not the kingdom. Cleared once the founder has been told.
		/// </summary>
		[NonSerialized]
		public bool LoadFailed;

		/// <summary>
		/// Days accounted for by the homecoming report now waiting in the Charter. Not
		/// serialized: a homecoming is news about this visit, and a saved one would be told
		/// twice on the next load.
		/// </summary>
		[NonSerialized]
		public int HomecomingDays;

		public string KingdomFactionName;

		public string KingdomDisplayName;

		public string Style = "common";

		/// <summary>
		/// The terrain blueprint read at the founding site, or null when the lookup was
		/// unavailable. Kept because <see cref="Style"/> is a conclusion and this is the evidence
		/// for it: a tester who disagrees with the style needs to see what the ground actually
		/// said. Serialization is by named fields, so a save written before this field existed
		/// simply arrives without it.
		/// </summary>
		public string FoundingTerrainBlueprint;

		/// <summary>Canonical terrain region of the founding site, or null. Evidence, as above.</summary>
		public string FoundingRegionName;

		/// <summary>Depth of the founding zone. Surface and strata read differently.</summary>
		public int FoundingZLevel;

		public long FoundedTick;

		public GrowthStage Stage = GrowthStage.Camp;

		public int Population;

		public int DryStreak;

		public bool Withered;

		public bool HasShopkeeper;

		public bool NoRoomAnnounced;

		public long LastHeartbeatTick;

		public int IdleWorks;

		public int ShorthandedWorks;

		public bool IdleWorksAnnounced;

		public int ShopTier;

		public long LastVisitTick;

		public string LastDeed;

		public long LastDeedTick;

		public KingdomRules.GatePolicy Gate = KingdomRules.GatePolicy.Open;

		public KingdomRules.StoresPolicy Stores = KingdomRules.StoresPolicy.Plenty;

		public int RaidTimesDeferred;

		public List<string> RosterNames = new List<string>();

		public List<string> RosterOrigins = new List<string>();

		public List<string> RosterArrived = new List<string>();

		public KingdomRules.PetitionKind PetitionKind = KingdomRules.PetitionKind.None;

		public string PetitionPetitioner;

		public string PetitionFaction;

		public int PetitionTarget;

		public long PetitionIssuedTick;

		public long LastPetitionTick;

		public int PetitionsMet;

		public int Dead;

		public KingdomLedger Ledger = new KingdomLedger();

		/// <summary>
		/// Records the kingdom's most recent notable act, which is what draws settlers and
		/// what arrival messages name. Deeds are forgotten after a while; reputation is not.
		/// </summary>
		/// <param name="Deed">Lower-case noun phrase, e.g. "the cistern you raised".</param>
		public void RecordDeed(string Deed)
		{
			LastDeed = Deed;
			LastDeedTick = The.Game.TimeTicks;
		}

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

		public override bool WantFieldReflection => false;

		public override void Write(SerializationWriter Writer)
		{
			SerializationVersion = CurrentSerializationVersion;
			Writer.Write(SerializationMagic);
			Writer.Write(CurrentSerializationVersion);
			Writer.WriteNamedFields(this, typeof(KingdomSystem));
		}

		/// <summary>
		/// Reads kingdom state, tolerating every layout this mod has ever written.
		/// <para>
		/// Two regimes meet here. Saves written before named fields arrived were emitted by the
		/// engine's positional reflection, so the engine has already filled every field by the
		/// time we are called &mdash; including <see cref="SerializationVersion"/>, which is how we
		/// recognise them. Nothing remains in the block to read, so we return.
		/// </para>
		/// <para>
		/// Named-field saves are self-describing: a reader may meet a field it does not know, and
		/// may miss one it expects, without either being an error. Any named-field version from
		/// the first through ours is therefore readable. Older positional versions and saves from
		/// a <i>newer</i> build are genuinely beyond this path.
		/// </para>
		/// <para>
		/// Throwing is the only way to reach the engine's block-skip recovery, so an unreadable
		/// save must throw &mdash; but it flags <see cref="LoadFailed"/> first, because the engine
		/// swallows the exception and hands back a blank system. Without the flag the founder's
		/// settlement would simply be gone, unremarked. See <see cref="ReportLoadFailure"/>.
		/// </para>
		/// </summary>
		public override void Read(SerializationReader Reader)
		{
			try
			{
				if (SerializationVersion == LegacyReflectedSerializationVersion)
				{
					SerializationVersion = CurrentSerializationVersion;
					NormalizeState();
					return;
				}
				int magic = Reader.ReadInt32();
				if (magic != SerializationMagic)
				{
					throw new InvalidOperationException("Invalid ThousandAndFirst kingdom save marker.");
				}
				int version = Reader.ReadInt32();
				if (version < FirstNamedSerializationVersion || version > CurrentSerializationVersion)
				{
					throw new InvalidOperationException("Unsupported ThousandAndFirst kingdom save version " + version + "; this build reads named versions " + FirstNamedSerializationVersion + " through " + CurrentSerializationVersion + ".");
				}
				Reader.ReadNamedFields(this, typeof(KingdomSystem));
				SerializationVersion = CurrentSerializationVersion;
				NormalizeState();
			}
			catch
			{
				LoadFailed = true;
				throw;
			}
		}

		/// <summary>
		/// Tells the founder, once, that the records could not be read. The engine catches
		/// deserialization failures and carries on with a blank system, so without this the loss
		/// would be visible only in the metrics log &mdash; the player would find the settlement
		/// unfounded and no reason given.
		/// </summary>
		private void ReportLoadFailure()
		{
			LoadFailed = false;
			MetricsManager.LogError("ThousandAndFirst: kingdom state could not be read; the settlement has been reset.");
			Popup.Show("The founding records cannot be read. Whatever kingdom you held is not recorded in this save, and the founding must begin again.\n\nYour game is otherwise unharmed.");
		}

		public override void AfterLoad(XRLGame Game)
		{
			base.AfterLoad(Game);
			NormalizeState();
		}

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
				// The district-aware overload: a garrison district trains the whole watch, so the
				// bonus has to be on the shared survey Raids later reads defence from.
				survey = KingdomSurvey.Take(E.Zone, this);
			});
			if (survey == null)
			{
				return base.HandleEvent(E);
			}
			Ledger.Reset();
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
			Guard("digest", delegate
			{
				long elapsed = The.Game.TimeTicks - LastVisitTick;
				LastVisitTick = The.Game.TimeTicks;
				HomecomingDays = (int)(elapsed / KingdomRules.TicksPerDay);
				if (Ledger.Any && elapsed >= KingdomRules.TicksPerDay)
				{
					// Nonmodal on purpose. You come home to a report, not an inspection: the
					// settlement says it has news and waits to be asked, in the Charter.
					XRL.Messages.MessageQueue.AddPlayerMessage("{{C|" + KingdomDisplayName + "}} has news of the "
						+ ((HomecomingDays == 1) ? "day" : HomecomingDays + " days") + " you were away. {{K|(Charter: what happened while you were away)}}");
				}
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
			if (LoadFailed)
			{
				Guard("load failure report", ReportLoadFailure);
			}
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

		private void NormalizeState()
		{
			if (RosterNames == null)
			{
				RosterNames = new List<string>();
			}
			if (RosterOrigins == null)
			{
				RosterOrigins = new List<string>();
			}
			if (RosterArrived == null)
			{
				RosterArrived = new List<string>();
			}
			if (Ledger == null)
			{
				Ledger = new KingdomLedger();
			}
			Ledger.Normalize();
			if (ClaimedZones == null)
			{
				ClaimedZones = new List<string>();
			}
			if (ZoneDistricts == null)
			{
				ZoneDistricts = new Dictionary<string, string>();
			}
			if (ActiveDealKeys == null)
			{
				ActiveDealKeys = new List<string>();
			}
			if (ActiveDealFactions == null)
			{
				ActiveDealFactions = new List<string>();
			}
			if (DealNextTicks == null)
			{
				DealNextTicks = new List<long>();
			}
			int dealCount = Math.Min(ActiveDealKeys.Count, Math.Min(ActiveDealFactions.Count, DealNextTicks.Count));
			if (ActiveDealKeys.Count > dealCount)
			{
				ActiveDealKeys.RemoveRange(dealCount, ActiveDealKeys.Count - dealCount);
			}
			if (ActiveDealFactions.Count > dealCount)
			{
				ActiveDealFactions.RemoveRange(dealCount, ActiveDealFactions.Count - dealCount);
			}
			if (DealNextTicks.Count > dealCount)
			{
				DealNextTicks.RemoveRange(dealCount, DealNextTicks.Count - dealCount);
			}
			if (ChronicleEntries == null)
			{
				ChronicleEntries = new List<string>();
			}
			if (OutsiderEntries == null)
			{
				OutsiderEntries = new List<string>();
			}
			if (OriginCounts == null)
			{
				OriginCounts = new Dictionary<string, int>();
			}
			if (Standings == null)
			{
				Standings = new Dictionary<string, int>();
			}
		}
	}
}
