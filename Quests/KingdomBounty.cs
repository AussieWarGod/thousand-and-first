using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X"
// (GamePartBlueprint.Namespace, :178; T => ModManager.ResolveType(Namespace, Name), :240), and
// the promised bare-TypeID fallback does not exist in the code. A part named in XML must live in
// this namespace or the object is built without it, silently. Only the part moves; the rest of
// the posted price stays where the mod's code lives.
namespace XRL.World.Parts
{
	/// <summary>
	/// A notice staked at the heart: a task, a price, and whatever the pass has made of it.
	/// <para>
	/// Carries no <c>WantTurnTick</c> and never will. A notice is read, taken, and worked off only
	/// on the settlement's ordinary <c>ZoneActivatedEvent</c> pass, through
	/// <see cref="ThousandAndFirst.KingdomBounty.OnSettlementPass"/>, so nothing here runs on a
	/// clock of its own and nothing accrues against a founder who is elsewhere.
	/// </para>
	/// <para>
	/// Nothing is escrowed on this part. <see cref="Price"/> is a promise; <see cref="Paid"/> is
	/// the only number that ever corresponds to water having left the stores.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomNotice : IPart
	{
		/// <summary>The posted task, as <c>ThousandAndFirst.BountyTask</c>.</summary>
		public int TaskCode;

		/// <summary>Drams promised. Never taken from the stores until the work is done.</summary>
		public int Price;

		/// <summary>Drams actually handed over so far. The remainder is what is owed.</summary>
		public int Paid;

		/// <summary>Tick the notice went up. Its ordinal in every kernel draw it will ever
		/// make.</summary>
		public long PostedTick;

		/// <summary>Attended passes this notice has already been resolved for. Drives the draw
		/// index, so a reload replays the same readers in the same order.</summary>
		public int Passes;

		/// <summary>Whoever took it, or null while it still stands unclaimed.</summary>
		public string WorkerName;

		/// <summary>Tick it was taken. Zero while unclaimed.</summary>
		public long TakenTick;

		/// <summary>Tick the work falls due, or zero for a task whose finish is read off the
		/// world rather than off a clock.</summary>
		public long DueTick;

		/// <summary>Cells, units, or works &mdash; whatever the task counts, kept for the prose
		/// and for the haul's own length.</summary>
		public int Magnitude;

		/// <summary>True once the work itself is finished and only the price remains.</summary>
		public bool Done;

		/// <summary>West edge of the staked rect, for a clearance notice.</summary>
		public int X1;

		/// <summary>North edge of the staked rect.</summary>
		public int Y1;

		/// <summary>East edge of the staked rect.</summary>
		public int X2;

		/// <summary>South edge of the staked rect.</summary>
		public int Y2;

		/// <summary>Object id of the marked pile, for a fetch notice.</summary>
		public string PileId;

		/// <summary>The reason last given for this notice standing still, as
		/// <c>ThousandAndFirst.BountyBlock</c>. Zero when nothing has been said. STANDARDS 7b:
		/// the reason is given once per stall, and again only when it changes.</summary>
		public int AnnouncedBlock;

		/// <summary>Set once the founder has been told the clearing gang would not take the
		/// staked rect. Cleared when it does.</summary>
		public bool StakeFailedAnnounced;

		/// <summary>Set once a refusal has been chronicled for this notice. Later refusals are
		/// remembered in the ledger instead, so a notice nobody wants never becomes a nag.</summary>
		public bool RefusalTold;
	}
}

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// The posted price: a notice staked at the heart offering drams to whoever performs a named
	/// task, and the settlers and notables who read it and decide for themselves.
	/// <para>
	/// Three rules run through everything here.
	/// <b>Nothing is escrowed</b> &mdash; the price is a promise until the work is done, and the
	/// only water that ever leaves the stores is water paid to somebody who finished something.
	/// <b>Nothing nags</b> &mdash; an unclaimed notice just stands there, with no expiry, no
	/// reminder, and no penalty; the founder takes it down for free whenever they like, and the
	/// chronicle remembers that they did. <b>Nothing stalls in silence</b> (STANDARDS 7b) &mdash;
	/// a notice that cannot be moved says why once, and a notice that can never be attempted at
	/// all says so once and then keeps quiet forever.
	/// </para>
	/// <para>
	/// Every founder-facing entry point does its own eligibility check, its own messaging, and its
	/// own chronicle entry, and surfaces only a decline &mdash; the <c>KingdomLarder</c> idiom the
	/// rest of the mod follows. A refusal changes nothing.
	/// </para>
	/// </summary>
	public static class KingdomBounty
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionBounty") != "No";

		/// <summary>The one blueprint a staked notice can be, named here rather than inferred.</summary>
		public const string NoticeBlueprint = "r_KingdomNotice";

		/// <summary>
		/// String property written on a container the founder marks for a fetch notice, carrying
		/// the notice's own object id. The mark <b>is</b> the designation the protection law
		/// requires: nothing is ever carried out of a container that does not name a live notice
		/// of this settlement's.
		/// </summary>
		public const string FetchMarkProperty = "KingdomFetchNotice";

		/// <summary>
		/// Takeover point for the carry-sign, which generalises the fetch task past this
		/// settlement's own ground: distance-scaled days, chronicled porters, and a load that can
		/// be lost to the road.
		/// <para>
		/// Left null, the fetch task resolves the short way below &mdash; a marked pile standing in
		/// the same ground as the notice, carried into the dedicated stockpiles. Set, it is
		/// consulted first and its answer is final: return true once the haul has been taken over
		/// (this file then only pays the price when the hook reports the load home), false to let
		/// the short way run.
		/// </para>
		/// <para>
		/// Arguments, in order: the realm, the marked pile, the porter's name, and the tick the
		/// haul was taken. Not supported API &mdash; a coordination seam between two systems of
		/// this mod, and it moves when they do.
		/// </para>
		/// </summary>
		public static Func<KingdomSystem, GameObject, string, long, bool> HaulHook;

		private static readonly int[] PriceLadder = new int[8] { 1, 2, 3, 5, 8, 12, 20, 40 };

		// ==================================================================================
		// Posting
		// ==================================================================================

		/// <summary>
		/// The Charter's own entry: shows what is standing at this heart, lets the founder post
		/// another, and lets them take one down. Does all its own messaging.
		/// </summary>
		/// <param name="System">The realm. Unfounded is refused with a reason.</param>
		/// <param name="Founder">The object holding the Charter, read for where it is standing.</param>
		public static void OpenNotices(KingdomSystem System, GameObject Founder)
		{
			if (System == null || !System.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			if (!Enabled)
			{
				Popup.Show("The settlement posts no prices. (Options: the posted price)");
				return;
			}
			Zone zone = (Founder != null) ? Founder.CurrentZone : null;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A notice is staked on the kingdom's own ground, where its people will walk past it.");
				return;
			}
			while (true)
			{
				List<GameObject> notices = Notices(zone);
				List<string> options = new List<string>();
				for (int i = 0; i < notices.Count; i++)
				{
					options.Add(StatusLine(System, notices[i]));
				}
				options.Add((notices.Count >= KingdomBountyRules.MaxNotices)
					? ("{{K|" + KingdomBountyRules.MaxNotices + " notices already stand here}}")
					: "{{W|Post a new notice}}");
				int pick = Popup.PickOption(
					Title: "The notice board of " + System.SeatName,
					Intro: "Nothing here is set aside. A price leaves the stores the day the work is done, and not before.",
					Options: options, AllowEscape: true);
				if (pick < 0)
				{
					return;
				}
				if (pick >= notices.Count)
				{
					if (notices.Count >= KingdomBountyRules.MaxNotices)
					{
						Popup.Show("Three notices is as many as one heart can carry without the settlement stopping to read them all.");
						continue;
					}
					PostNotice(System, zone, Founder);
					continue;
				}
				Inspect(System, zone, notices[pick]);
			}
		}

		private static void Inspect(KingdomSystem System, Zone Z, GameObject Notice)
		{
			r_KingdomNotice notice = Notice.GetPart<r_KingdomNotice>();
			if (notice == null)
			{
				return;
			}
			BountyTask task = (BountyTask)notice.TaskCode;
			string body = KingdomBountyRules.NoticeText(task, notice.Price, DetailOf(System, Z, notice))
				+ "\n\n" + Progress(System, notice);
			int pick = Popup.PickOption(
				Title: "A posted notice",
				Intro: body,
				Options: new string[2] { "Leave it standing", "{{W|Take it down}}" },
				AllowEscape: true);
			if (pick != 1)
			{
				return;
			}
			if (Popup.ShowYesNo("Take the notice down?\n\nNothing is owed either way, and nobody who took it is made to give anything back.") != DialogResult.Yes)
			{
				return;
			}
			Withdraw(System, Z, Notice, notice);
		}

		private static void Withdraw(KingdomSystem System, Zone Z, GameObject Notice, r_KingdomNotice Data)
		{
			BountyTask task = (BountyTask)Data.TaskCode;
			bool claimed = !string.IsNullOrEmpty(Data.WorkerName);
			ClearFetchMark(Z, Notice, Data);
			KingdomChronicle.Record(System, KingdomBountyRules.WithdrawnChronicle(System.SeatName, task, claimed, Data.WorkerName));
			MessageQueue.AddPlayerMessage("{{K|The notice comes off the stake.}} " + (claimed
				? ("Word will reach " + Data.WorkerName + " that the settlement has changed its mind. Nothing is asked back.")
				: "Nobody had taken it, and nothing was spent."));
			Notice.Obliterate();
			KingdomLog.Log("bounty: withdrawn task=" + KingdomBountyRules.TaskKey(task) + " claimed=" + claimed);
		}

		private static void PostNotice(KingdomSystem System, Zone Z, GameObject Founder)
		{
			KingdomSurvey survey = KingdomSurvey.Take(Z);
			BountyTask[] tasks = new BountyTask[KingdomBountyRules.TaskCount]
			{
				BountyTask.Clearance, BountyTask.Fetch, BountyTask.Manning, BountyTask.Scouting
			};
			string[] refusals = new string[KingdomBountyRules.TaskCount];
			string[] options = new string[KingdomBountyRules.TaskCount];
			for (int i = 0; i < tasks.Length; i++)
			{
				refusals[i] = WhyNotPostable(System, Z, Founder, survey, tasks[i]);
				options[i] = (refusals[i] == null)
					? KingdomBountyRules.TaskName(tasks[i]).Capitalize()
					: ("{{K|" + KingdomBountyRules.TaskName(tasks[i]).Capitalize() + " -- " + refusals[i] + "}}");
			}
			int pick = Popup.PickOption(
				Title: "What is the price for?",
				Intro: "Name a task and a price. Settlers read the notice on their own time and decide for themselves; nobody is ordered to take it.",
				Options: options, AllowEscape: true);
			if (pick < 0)
			{
				return;
			}
			if (refusals[pick] != null)
			{
				Popup.Show(refusals[pick].Capitalize() + ".");
				return;
			}
			Stake(System, Z, Founder, survey, tasks[pick]);
		}

		/// <summary>Why a task cannot be posted on this ground right now, or null when it can.
		/// Lower-case clause, no trailing period.</summary>
		private static string WhyNotPostable(KingdomSystem System, Zone Z, GameObject Founder, KingdomSurvey Survey, BountyTask Task)
		{
			switch (Task)
			{
			case BountyTask.Clearance:
			{
				if (!KingdomMaterials.Enabled)
				{
					return "the settlement is not clearing ground";
				}
				Cell cell = (Founder != null) ? Founder.CurrentCell : null;
				if (cell == null)
				{
					return "there is nowhere here to stake a cord";
				}
				return null;
			}
			case BountyTask.Fetch:
			{
				if (MarkablePiles(Z, Founder).Count == 0)
				{
					return "there is no pile of materials within reach to mark";
				}
				if (KingdomMaterials.Stock(Z).None)
				{
					return "there is no stockpile dedicated to carry anything into";
				}
				return null;
			}
			case BountyTask.Manning:
				return (Survey.Works.Count == 0) ? "the settlement has no works to stand" : null;
			case BountyTask.Scouting:
				return (Frontier(System).Count == 0) ? "the claim has no unclaimed edge left" : null;
			default:
				return "the notice board does not know that word";
			}
		}

		private static void Stake(KingdomSystem System, Zone Z, GameObject Founder, KingdomSurvey Survey, BountyTask Task)
		{
			int magnitude = 0;
			int x1 = 0;
			int y1 = 0;
			int x2 = 0;
			int y2 = 0;
			GameObject pile = null;
			if (Task == BountyTask.Clearance && !PickRect(System, Z, Founder, out x1, out y1, out x2, out y2, out magnitude))
			{
				return;
			}
			if (Task == BountyTask.Fetch && !PickPile(System, Z, Founder, out pile, out magnitude))
			{
				return;
			}
			if (Task == BountyTask.Manning)
			{
				magnitude = Survey.Works.Count;
			}
			int price = PickPrice(System, Survey, Task, magnitude);
			if (price <= 0)
			{
				return;
			}
			string warning = (Survey.StoredWater < price)
				? ("\n\n{{r|The stores hold " + Survey.StoredWater + " drams, which will not cover it today.}} Nothing is set aside now, so the notice may stand until they do -- but whoever claims it will be owed until then.")
				: "";
			if (Popup.ShowYesNo("Stake the notice?\n\n" + KingdomBountyRules.NoticeText(Task, price, null)
				+ "\n\nNo water leaves the stores until somebody has done it." + warning) != DialogResult.Yes)
			{
				return;
			}
			Cell cell = HeartCell(Z, Founder);
			if (cell == null)
			{
				Popup.Show("There is nowhere at the heart to drive a stake.");
				return;
			}
			GameObject notice = GameObject.Create(NoticeBlueprint);
			r_KingdomNotice data = (notice != null) ? notice.GetPart<r_KingdomNotice>() : null;
			if (data == null)
			{
				notice?.Obliterate();
				Popup.Show("The stake could not be driven.");
				return;
			}
			data.TaskCode = (int)Task;
			data.Price = price;
			data.PostedTick = The.Game.TimeTicks;
			data.Magnitude = magnitude;
			data.X1 = x1;
			data.Y1 = y1;
			data.X2 = x2;
			data.Y2 = y2;
			cell.AddObject(notice);
			notice.MakeActive();
			if (pile != null)
			{
				data.PileId = pile.ID;
				pile.SetStringProperty(FetchMarkProperty, notice.ID);
			}
			Describe(System, Z, notice, data);
			KingdomChronicle.Record(System, KingdomBountyRules.PostedChronicle(System.SeatName, Task, price));
			MessageQueue.AddPlayerMessage("{{G|The notice is up.}} " + KingdomBountyRules.NoticeText(Task, price, null));
			KingdomLog.Log("bounty: posted task=" + KingdomBountyRules.TaskKey(Task) + " price=" + price + " magnitude=" + magnitude);
		}

		private static bool PickRect(KingdomSystem System, Zone Z, GameObject Founder, out int X1, out int Y1, out int X2, out int Y2, out int Cells)
		{
			X1 = 0;
			Y1 = 0;
			X2 = 0;
			Y2 = 0;
			Cells = 0;
			Cell here = (Founder != null) ? Founder.CurrentCell : null;
			if (here == null)
			{
				return false;
			}
			int[] widths = new int[3] { 3, 5, 9 };
			int[] heights = new int[3] { 3, 5, 7 };
			string[] names = new string[3] { "the ground you stand on", "a working yard", "a wide clearing" };
			string[] options = new string[3];
			int[][] rects = new int[3][];
			KingdomMaterials.ClearanceAssessment[] assessed = new KingdomMaterials.ClearanceAssessment[3];
			for (int i = 0; i < 3; i++)
			{
				int left = here.X - widths[i] / 2;
				int top = here.Y - heights[i] / 2;
				rects[i] = new int[4] { left, top, left + widths[i] - 1, top + heights[i] - 1 };
				assessed[i] = KingdomMaterials.Assess(System, Z, rects[i][0], rects[i][1], rects[i][2], rects[i][3]);
				string size = " (" + widths[i] + " by " + heights[i] + ")";
				if (!assessed[i].Valid)
				{
					options[i] = "{{K|" + names[i] + size + " -- runs off the edge of this ground}}";
				}
				else if (assessed[i].Refusal != null)
				{
					options[i] = "{{K|" + names[i] + size + " -- something stands in it}}";
				}
				else if (assessed[i].Standing <= 0)
				{
					options[i] = "{{K|" + names[i] + size + " -- nothing in it has to come down}}";
				}
				else
				{
					options[i] = names[i] + size + " {{K|(" + assessed[i].Standing + " standing, and "
						+ (assessed[i].Yield.Describe() ?? "turned earth") + " out of it)}}";
				}
			}
			int pick = Popup.PickOption(
				Title: "Which ground?",
				Intro: "A clearance notice pays twice: the price you post, and whatever comes out of the ground.",
				Options: options, AllowEscape: true);
			if (pick < 0)
			{
				return false;
			}
			if (!assessed[pick].Valid)
			{
				Popup.Show("That ground runs off the edge of this one.");
				return false;
			}
			if (assessed[pick].Refusal != null)
			{
				Popup.Show(assessed[pick].Refusal);
				return false;
			}
			if (assessed[pick].Standing <= 0)
			{
				Popup.Show("There is nothing in it that has to come down. A notice over clear ground would never be claimed.");
				return false;
			}
			X1 = rects[pick][0];
			Y1 = rects[pick][1];
			X2 = rects[pick][2];
			Y2 = rects[pick][3];
			Cells = assessed[pick].Cells;
			return true;
		}

		private static bool PickPile(KingdomSystem System, Zone Z, GameObject Founder, out GameObject Pile, out int Units)
		{
			Pile = null;
			Units = 0;
			List<GameObject> candidates = MarkablePiles(Z, Founder);
			if (candidates.Count == 0)
			{
				Popup.Show("There is no pile of materials within reach to mark.");
				return false;
			}
			string[] options = new string[candidates.Count];
			int[] counts = new int[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				counts[i] = MaterialUnits(candidates[i]);
				options[i] = candidates[i].ShortDisplayName + " {{K|(" + counts[i]
					+ ((counts[i] == 1) ? " load" : " loads") + " of material)}}";
			}
			int pick = Popup.PickOption(
				Title: "Which pile?",
				Intro: "The mark is the whole of the designation. Nothing is ever carried out of a container you have not marked, and the mark comes off when the notice does.",
				Options: options, AllowEscape: true);
			if (pick < 0)
			{
				return false;
			}
			Pile = candidates[pick];
			Units = counts[pick];
			return true;
		}

		private static int PickPrice(KingdomSystem System, KingdomSurvey Survey, BountyTask Task, int Magnitude)
		{
			int suggested = KingdomBountyRules.SuggestedPrice(Task, Magnitude);
			string[] options = new string[PriceLadder.Length];
			for (int i = 0; i < PriceLadder.Length; i++)
			{
				int price = KingdomBountyRules.ClampPrice(PriceLadder[i]);
				options[i] = price + ((price == 1) ? " dram" : " drams")
					+ ((price == suggested) ? " {{G|[what the work is worth]}}" : "")
					+ ((price > Survey.StoredWater) ? " {{r|(more than the stores hold today)}}" : "");
			}
			int pick = Popup.PickOption(
				Title: "Name the price",
				Intro: "The stores hold " + Survey.StoredWater + " drams. None of it is set aside by posting; the price is drawn the day the work is done.",
				Options: options, AllowEscape: true);
			return (pick < 0) ? 0 : KingdomBountyRules.ClampPrice(PriceLadder[pick]);
		}

		// ==================================================================================
		// The pass
		// ==================================================================================

		/// <summary>
		/// Resolves every notice standing on this ground for one attended pass: who read them, who
		/// took them, what got finished, and what got paid.
		/// <para>
		/// Call from the settlement's <c>ZoneActivatedEvent</c> pass <b>after</b> growth and
		/// improvement, and for the same reason those two are ordered against each other: this
		/// pays out of what the stores have left once the settlement's own upkeep and arrivals are
		/// done with them, and it mans works out of the idleness growth has just finished
		/// measuring.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Does nothing when unfounded.</param>
		/// <param name="Z">The activated ground. Does nothing when it is not the kingdom's.</param>
		/// <param name="Survey">This pass's shared survey, drawn from and written to.</param>
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || Survey == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			List<GameObject> notices = Notices(Z);
			for (int i = 0; i < notices.Count; i++)
			{
				GameObject notice = notices[i];
				r_KingdomNotice data = notice.GetPart<r_KingdomNotice>();
				if (data == null || !GameObject.Validate(notice))
				{
					continue;
				}
				Resolve(System, Z, Survey, notice, data);
			}
		}

		private static void Resolve(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Notice, r_KingdomNotice Data)
		{
			if (Data.Done)
			{
				Settle(System, Z, Survey, Notice, Data);
				return;
			}
			if (!string.IsNullOrEmpty(Data.WorkerName))
			{
				Work(System, Z, Survey, Notice, Data);
				return;
			}
			BountyBlock block = Blocking(System, Z, Survey, Data);
			if (block != BountyBlock.None)
			{
				Announce(System, Data, block);
				return;
			}
			Announce(System, Data, BountyBlock.None);
			BountyTask task = (BountyTask)Data.TaskCode;
			KingdomBountyRules.BountyAttempt attempt = KingdomBountyRules.Resolve(
				KingdomChronicle.SettlementId(System.KingdomFactionName),
				Data.PostedTick, Data.Passes, System.RosterNames, task, Data.Price);
			if (Data.Passes < KingdomBountyRules.MaxPasses)
			{
				Data.Passes++;
			}
			if (attempt.Outcome == BountyOutcome.NobodyTried)
			{
				return;
			}
			if (attempt.Outcome == BountyOutcome.Refused)
			{
				if (!Data.RefusalTold)
				{
					Data.RefusalTold = true;
					KingdomChronicle.Record(System, KingdomBountyRules.RefusedChronicle(attempt.Name, task, attempt.FlawIndex));
				}
				System.Ledger.Note("{{K|" + attempt.Name + " read the notice offering water to " + KingdomBountyRules.TaskName(task) + ", and left it standing.}}");
				KingdomLog.Log("bounty: refused by " + attempt.Name + " task=" + KingdomBountyRules.TaskKey(task));
				return;
			}
			Take(System, Z, Notice, Data, task, attempt);
		}

		private static void Take(KingdomSystem System, Zone Z, GameObject Notice, r_KingdomNotice Data, BountyTask Task, KingdomBountyRules.BountyAttempt Attempt)
		{
			if (Task == BountyTask.Clearance)
			{
				if (!KingdomMaterials.StakeClearance(System, Z, Data.X1, Data.Y1, Data.X2, Data.Y2, out var failure))
				{
					if (!Data.StakeFailedAnnounced)
					{
						Data.StakeFailedAnnounced = true;
						System.Ledger.Note("{{r|" + Attempt.Name + " would have taken the clearance notice, and could not: " + failure + "}}");
					}
					return;
				}
				Data.StakeFailedAnnounced = false;
			}
			Data.WorkerName = Attempt.Name;
			Data.TakenTick = The.Game.TimeTicks;
			int days = KingdomBountyRules.WorkDays(Task, Data.Magnitude);
			Data.DueTick = (days > 0) ? (Data.TakenTick + (days * KingdomRules.TicksPerDay)) : 0L;
			KingdomChronicle.Record(System, KingdomBountyRules.TakenChronicle(Attempt.Name, Task, Attempt.VirtueIndex, Attempt.TasteMatched));
			System.Ledger.Note("{{G|" + Attempt.Name + " took the notice offering water to " + KingdomBountyRules.TaskName(Task) + ".}}");
			MessageQueue.AddPlayerMessage("{{G|" + Attempt.Name + " takes the posted notice.}}");
			Describe(System, Z, Notice, Data);
			KingdomLog.Log("bounty: taken by " + Attempt.Name + " task=" + KingdomBountyRules.TaskKey(Task) + " taste=" + Attempt.TasteMatched + " due=" + Data.DueTick);
		}

		private static void Work(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Notice, r_KingdomNotice Data)
		{
			BountyTask task = (BountyTask)Data.TaskCode;
			long now = The.Game.TimeTicks;
			switch (task)
			{
			case BountyTask.Clearance:
			{
				KingdomMaterials.ClearanceAssessment assessment = KingdomMaterials.Assess(System, Z, Data.X1, Data.Y1, Data.X2, Data.Y2);
				if (!assessment.Valid || assessment.Standing > 0)
				{
					return;
				}
				Finish(System, Z, Survey, Notice, Data, null);
				return;
			}
			case BountyTask.Manning:
			{
				ManOneWork(System, Survey);
				if (now < Data.DueTick)
				{
					return;
				}
				Finish(System, Z, Survey, Notice, Data, null);
				return;
			}
			case BountyTask.Fetch:
			{
				if (now < Data.DueTick)
				{
					return;
				}
				Carry(System, Z, Notice, Data, Survey);
				return;
			}
			case BountyTask.Scouting:
			{
				if (now < Data.DueTick)
				{
					return;
				}
				Scout(System, Z, Survey, Notice, Data);
				return;
			}
			default:
				Finish(System, Z, Survey, Notice, Data, null);
				return;
			}
		}

		private static void Carry(KingdomSystem System, Zone Z, GameObject Notice, r_KingdomNotice Data, KingdomSurvey Survey)
		{
			GameObject pile = FindPile(Z, Notice, Data);
			if (pile == null)
			{
				Announce(System, Data, BountyBlock.PileEmpty);
				return;
			}
			if (HaulHook != null && HaulHook(System, pile, Data.WorkerName, Data.TakenTick))
			{
				return;
			}
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z);
			GameObject container = null;
			for (int i = 0; i < stock.Stockpiles.Count; i++)
			{
				if (stock.Stockpiles[i].Inventory != null && stock.Stockpiles[i] != pile)
				{
					container = stock.Stockpiles[i];
					break;
				}
			}
			if (container == null)
			{
				Announce(System, Data, BountyBlock.NowhereToCarry);
				return;
			}
			if (pile.Inventory == null)
			{
				Announce(System, Data, BountyBlock.PileEmpty);
				return;
			}
			// Snapshot first: moving an item out of this Inventory mutates the very list a
			// foreach would be walking.
			List<GameObject> held = new List<GameObject>(pile.Inventory.Objects);
			int moved = 0;
			for (int i = 0; i < held.Count; i++)
			{
				GameObject item = held[i];
				if (!GameObject.Validate(item) || !KingdomMaterials.TryMaterialOf(item, out _))
				{
					continue;
				}
				// Counted before the move and confirmed after it. A vetoed removal leaves the item
				// where it was, and an item still in the pile was never carried, whatever any
				// engine call reported.
				int units = item.Count;
				pile.Inventory.RemoveObject(item);
				if (pile.Inventory.Objects.Contains(item))
				{
					continue;
				}
				container.Inventory.AddObject(item);
				moved += units;
			}
			if (moved <= 0)
			{
				Announce(System, Data, BountyBlock.PileEmpty);
				return;
			}
			Announce(System, Data, BountyBlock.None);
			Finish(System, Z, Survey, Notice, Data, moved + ((moved == 1) ? " load was carried in" : " loads were carried in"));
		}

		private static void Scout(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Notice, r_KingdomNotice Data)
		{
			List<string> frontier = Frontier(System);
			if (frontier.Count == 0)
			{
				Announce(System, Data, BountyBlock.NoFrontier);
				return;
			}
			int index;
			KingdomBountyRules.TryPickFrontier(KingdomChronicle.SettlementId(System.KingdomFactionName), Data.PostedTick, Data.Passes, frontier.Count, out index);
			if (index < 0 || index >= frontier.Count)
			{
				index = 0;
			}
			string ground = null;
			KingdomSystem.Guard("bounty: name frontier", delegate
			{
				ground = The.ZoneManager.GetZoneDisplayName(frontier[index], WithIndefiniteArticle: true);
			});
			KingdomChronicle.Record(System, KingdomBountyRules.ScoutChronicle(Data.WorkerName, System.SeatName, ground));
			System.RecordDeed(KingdomBountyRules.ScoutDeed(System.SeatName));
			Announce(System, Data, BountyBlock.None);
			Finish(System, Z, Survey, Notice, Data, string.IsNullOrEmpty(ground) ? "the frontier was walked" : ("the frontier was walked, and " + ground + " lies past it"));
		}

		private static void ManOneWork(KingdomSystem System, KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.Works.Count; i++)
			{
				GameObject work = Survey.Works[i];
				if (work.GetIntProperty("KingdomEffectiveness") > 0)
				{
					continue;
				}
				work.SetIntProperty("KingdomStaffed", 1);
				work.SetIntProperty("KingdomEffectiveness", 100);
				if (System.IdleWorks > 0)
				{
					System.IdleWorks--;
				}
				if (System.IdleWorks == 0)
				{
					System.IdleWorksAnnounced = false;
				}
				return;
			}
		}

		/// <summary>Marks the work finished and tries to pay for it in the same breath.</summary>
		private static void Finish(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Notice, r_KingdomNotice Data, string Extra)
		{
			Data.Done = true;
			ClearFetchMark(Z, Notice, Data);
			if (!string.IsNullOrEmpty(Extra))
			{
				System.Ledger.Note("{{G|" + Extra.Capitalize() + ".}}");
			}
			Settle(System, Z, Survey, Notice, Data);
		}

		private static void Settle(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Notice, r_KingdomNotice Data)
		{
			int owed = Data.Price - Data.Paid;
			if (owed > 0)
			{
				// Measured, never assumed: Consume reports what actually left the stores.
				int drawn = Survey.Consume(owed);
				Data.Paid += drawn;
				owed -= drawn;
			}
			if (owed > 0)
			{
				if (Data.AnnouncedBlock != (int)BountyBlock.StoresCannotPay)
				{
					KingdomChronicle.Record(System, KingdomBountyRules.OwedChronicle(Data.WorkerName, System.SeatName, Data.Paid, owed));
				}
				Announce(System, Data, BountyBlock.StoresCannotPay);
				System.Ledger.Note(KingdomBountyRules.OwedLedgerNote(Data.WorkerName, owed));
				Describe(System, Z, Notice, Data);
				return;
			}
			KingdomChronicle.Record(System, KingdomBountyRules.PaidChronicle(Data.WorkerName, System.SeatName, (BountyTask)Data.TaskCode, Data.Paid));
			System.Ledger.Note("{{G|" + (string.IsNullOrEmpty(Data.WorkerName) ? "Somebody" : Data.WorkerName) + " was paid "
				+ Data.Paid + ((Data.Paid == 1) ? " dram" : " drams") + " off the notice board.}}");
			MessageQueue.AddPlayerMessage("{{G|The notice is claimed and paid.}} " + Data.Paid
				+ ((Data.Paid == 1) ? " dram goes" : " drams go") + " to " + (string.IsNullOrEmpty(Data.WorkerName) ? "whoever did it" : Data.WorkerName) + ".");
			KingdomLog.Log("bounty: paid " + Data.Paid + " to " + Data.WorkerName + " task=" + KingdomBountyRules.TaskKey((BountyTask)Data.TaskCode));
			Notice.Obliterate();
		}

		// ==================================================================================
		// Saying why, once
		// ==================================================================================

		private static BountyBlock Blocking(KingdomSystem System, Zone Z, KingdomSurvey Survey, r_KingdomNotice Data)
		{
			if (System.RosterNames.Count == 0)
			{
				return BountyBlock.NobodyToTry;
			}
			switch ((BountyTask)Data.TaskCode)
			{
			case BountyTask.Clearance:
			{
				KingdomMaterials.ClearanceAssessment assessment = KingdomMaterials.Assess(System, Z, Data.X1, Data.Y1, Data.X2, Data.Y2);
				return (assessment.Valid && assessment.Standing <= 0) ? BountyBlock.NothingStanding : BountyBlock.None;
			}
			case BountyTask.Fetch:
			{
				GameObject pile = FindPile(Z, null, Data);
				if (pile == null || MaterialUnits(pile) <= 0)
				{
					return BountyBlock.PileEmpty;
				}
				return KingdomMaterials.Stock(Z).None ? BountyBlock.NowhereToCarry : BountyBlock.None;
			}
			case BountyTask.Manning:
				if (Survey.Works.Count == 0)
				{
					return BountyBlock.NoWorks;
				}
				return (System.IdleWorks <= 0) ? BountyBlock.NoIdleWork : BountyBlock.None;
			case BountyTask.Scouting:
				return (Frontier(System).Count == 0) ? BountyBlock.NoFrontier : BountyBlock.None;
			default:
				return BountyBlock.None;
			}
		}

		/// <summary>
		/// Says why once, and only once, per stall. A permanent reason is never repeated even if
		/// the notice is looked at again; an ordinary block is re-armed the moment it lifts, so a
		/// stall that comes back is spoken about again rather than swallowed. STANDARDS 7b.
		/// </summary>
		private static void Announce(KingdomSystem System, r_KingdomNotice Data, BountyBlock Block)
		{
			if (Data.AnnouncedBlock == (int)Block)
			{
				return;
			}
			if (Block == BountyBlock.None)
			{
				if (!KingdomBountyRules.IsPermanent((BountyBlock)Data.AnnouncedBlock))
				{
					Data.AnnouncedBlock = 0;
				}
				return;
			}
			if (KingdomBountyRules.IsPermanent((BountyBlock)Data.AnnouncedBlock))
			{
				return;
			}
			Data.AnnouncedBlock = (int)Block;
			string reason = KingdomBountyRules.BlockReason(Block, (BountyTask)Data.TaskCode, System.SeatName);
			if (reason != null)
			{
				System.Ledger.Note("{{r|" + reason + "}}");
				MessageQueue.AddPlayerMessage("{{r|" + reason + "}}");
			}
			KingdomLog.Log("bounty: blocked " + Block + " task=" + KingdomBountyRules.TaskKey((BountyTask)Data.TaskCode));
		}

		// ==================================================================================
		// Reading the ground
		// ==================================================================================

		/// <summary>Every notice standing on this ground, in the order the zone yields them.</summary>
		public static List<GameObject> Notices(Zone Z)
		{
			List<GameObject> found = new List<GameObject>();
			if (Z == null)
			{
				return found;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.HasPart(typeof(r_KingdomNotice)))
				{
					found.Add(item);
				}
			}
			return found;
		}

		/// <summary>
		/// Containers within reach of the founder that a fetch notice could be posted over: things
		/// that hold material, are not already dedicated stockpiles, and are not already marked for
		/// another notice.
		/// </summary>
		private static List<GameObject> MarkablePiles(Zone Z, GameObject Founder)
		{
			List<GameObject> found = new List<GameObject>();
			Cell here = (Founder != null) ? Founder.CurrentCell : null;
			if (Z == null || here == null)
			{
				return found;
			}
			List<Cell> reach = new List<Cell>();
			reach.Add(here);
			here.GetAdjacentCells(1, reach);
			for (int i = 0; i < reach.Count; i++)
			{
				Cell cell = reach[i];
				if (cell == null || cell.ParentZone != Z)
				{
					continue;
				}
				foreach (GameObject item in cell.GetObjects())
				{
					if (item.Inventory == null || item.IsCreature || KingdomMaterials.IsStockpile(item))
					{
						continue;
					}
					if (!string.IsNullOrEmpty(item.GetStringProperty(FetchMarkProperty)) || found.Contains(item))
					{
						continue;
					}
					if (MaterialUnits(item) > 0)
					{
						found.Add(item);
					}
				}
			}
			return found;
		}

		private static int MaterialUnits(GameObject Container)
		{
			if (Container == null || Container.Inventory == null)
			{
				return 0;
			}
			int units = 0;
			foreach (GameObject held in Container.Inventory.Objects)
			{
				if (KingdomMaterials.TryMaterialOf(held, out _))
				{
					units += held.Count;
				}
			}
			return units;
		}

		private static GameObject FindPile(Zone Z, GameObject Notice, r_KingdomNotice Data)
		{
			if (Z == null || Data == null || string.IsNullOrEmpty(Data.PileId))
			{
				return null;
			}
			GameObject pile = Z.FindObjectByID(Data.PileId);
			if (pile == null || !GameObject.Validate(pile))
			{
				return null;
			}
			// The mark is the designation, so the mark is what is checked - not the id we happen
			// to have stored. A founder who cleared it has taken their permission back.
			string marked = pile.GetStringProperty(FetchMarkProperty);
			if (string.IsNullOrEmpty(marked))
			{
				return null;
			}
			if (Notice != null && marked != Notice.ID)
			{
				return null;
			}
			return pile;
		}

		private static void ClearFetchMark(Zone Z, GameObject Notice, r_KingdomNotice Data)
		{
			GameObject pile = FindPile(Z, Notice, Data);
			pile?.RemoveStringProperty(FetchMarkProperty);
			Data.PileId = null;
		}

		/// <summary>
		/// Every zone touching the realm's claim that the realm does not hold: the ground a scout
		/// can be sent to look at. Sorted ordinally, so the kernel's pick lands on the same ground
		/// on any reload.
		/// </summary>
		public static List<string> Frontier(KingdomSystem System)
		{
			List<string> found = new List<string>();
			if (System == null || !System.Founded)
			{
				return found;
			}
			for (int i = 0; i < System.ClaimedZones.Count; i++)
			{
				string world;
				int px;
				int py;
				int zx;
				int zy;
				int z;
				if (!ZoneID.Parse(System.ClaimedZones[i], out world, out px, out py, out zx, out zy, out z))
				{
					continue;
				}
				int globalX = px * KingdomBountyRules.ZonesPerParasang + zx;
				int globalY = py * KingdomBountyRules.ZonesPerParasang + zy;
				for (int step = 0; step < KingdomBountyRules.NeighbourCount; step++)
				{
					int nx;
					int ny;
					if (!KingdomBountyRules.TryNeighbour(globalX, globalY, step, out nx, out ny))
					{
						continue;
					}
					int npx;
					int nzx;
					int npy;
					int nzy;
					if (!KingdomBountyRules.TrySplitGlobal(nx, out npx, out nzx) || !KingdomBountyRules.TrySplitGlobal(ny, out npy, out nzy))
					{
						continue;
					}
					string id = ZoneID.Assemble(world, npx, npy, nzx, nzy, z);
					if (System.ClaimedZones.Contains(id) || found.Contains(id))
					{
						continue;
					}
					if (System.Away != null && System.Away.ClaimedZones.Contains(id))
					{
						continue;
					}
					found.Add(id);
				}
			}
			found.Sort(StringComparer.Ordinal);
			return found;
		}

		// ==================================================================================
		// Prose on the object itself
		// ==================================================================================

		private static void Describe(KingdomSystem System, Zone Z, GameObject Notice, r_KingdomNotice Data)
		{
			BountyTask task = (BountyTask)Data.TaskCode;
			string text = KingdomBountyRules.NoticeText(task, Data.Price, DetailOf(System, Z, Data)) + " " + Progress(System, Data);
			Notice.DisplayName = string.IsNullOrEmpty(Data.WorkerName) ? "a posted notice" : "a claimed notice";
			Notice.RequirePart<Description>().Short = text;
		}

		private static string DetailOf(KingdomSystem System, Zone Z, r_KingdomNotice Data)
		{
			switch ((BountyTask)Data.TaskCode)
			{
			case BountyTask.Clearance:
				return "The cord runs round " + Data.Magnitude + " paces of it.";
			case BountyTask.Fetch:
			{
				GameObject pile = FindPile(Z, null, Data);
				return (pile == null) ? null : ("The mark is cut into " + pile.ShortDisplayName + ".");
			}
			case BountyTask.Manning:
				return "A season is " + KingdomBountyRules.ManningSeasonDays + " days, and the settlement counts them.";
			default:
				return null;
			}
		}

		private static string Progress(KingdomSystem System, r_KingdomNotice Data)
		{
			if (Data.Done)
			{
				int owed = Data.Price - Data.Paid;
				return (owed > 0)
					? ("{{r|The work is done and " + owed + ((owed == 1) ? " dram is" : " drams are") + " still owed on it.}}")
					: "{{G|The work is done and the price is paid.}}";
			}
			if (string.IsNullOrEmpty(Data.WorkerName))
			{
				string reason = KingdomBountyRules.BlockReason((BountyBlock)Data.AnnouncedBlock, (BountyTask)Data.TaskCode, System.SeatName);
				return (reason == null) ? "{{K|Nobody has taken it yet.}}" : ("{{r|" + reason + "}}");
			}
			if (Data.DueTick <= 0L)
			{
				return "{{W|" + Data.WorkerName + " has it, and is at it now.}}";
			}
			long left = Data.DueTick - The.Game.TimeTicks;
			int days = (int)((left + KingdomRules.TicksPerDay - 1L) / KingdomRules.TicksPerDay);
			if (days <= 0)
			{
				return "{{W|" + Data.WorkerName + " has it, and is due back.}}";
			}
			return "{{W|" + Data.WorkerName + " has it. " + days + ((days == 1) ? " day" : " days") + " left of it.}}";
		}

		private static string StatusLine(KingdomSystem System, GameObject Notice)
		{
			r_KingdomNotice data = Notice.GetPart<r_KingdomNotice>();
			if (data == null)
			{
				return "{{K|an unreadable notice}}";
			}
			int price = KingdomBountyRules.ClampPrice(data.Price);
			return KingdomBountyRules.TaskName((BountyTask)data.TaskCode).Capitalize()
				+ " {{K|(" + price + ((price == 1) ? " dram" : " drams") + ")}} -- " + Progress(System, data);
		}

		private static Cell HeartCell(Zone Z, GameObject Founder)
		{
			int heartX = -1;
			int heartY = -1;
			KingdomSystem.Guard("bounty: heart", delegate
			{
				List<KingdomLayoutRules.LayoutMark> marks = KingdomLayout.ReadMarks(Z);
				int centreX;
				int centreY;
				if (KingdomLayoutRules.TryHeart(marks, out centreX, out centreY))
				{
					heartX = centreX;
					heartY = centreY;
				}
			});
			Cell here = (Founder != null) ? Founder.CurrentCell : null;
			if (heartX < 0 && here != null)
			{
				heartX = here.X;
				heartY = here.Y;
			}
			if (heartX < 0)
			{
				return null;
			}
			// Empty cells only, per the protection law: a notice never lands on top of anything,
			// and least of all on top of something the founder put there.
			for (int radius = 0; radius <= 6; radius++)
			{
				for (int y = heartY - radius; y <= heartY + radius; y++)
				{
					for (int x = heartX - radius; x <= heartX + radius; x++)
					{
						if (radius > 0 && x != heartX - radius && x != heartX + radius && y != heartY - radius && y != heartY + radius)
						{
							continue;
						}
						Cell candidate = Z.GetCell(x, y);
						if (candidate != null && candidate.IsEmpty() && candidate.IsPassable())
						{
							return candidate;
						}
					}
				}
			}
			return null;
		}
	}
}
