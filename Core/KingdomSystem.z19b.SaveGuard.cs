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
		/// <summary>
		/// The last gate before a buffered save is allowed to reach the primary file, and the
		/// only one this class has. <see cref="Write"/> has no way to refuse; by the time it runs
		/// the engine has already decided this instance is going to disk. The refusal has to
		/// happen one step earlier, here.
		/// <para>
		/// <c>XRLGame.SaveGame</c> serializes the player into a shared in-memory writer, then
		/// <c>XRLGame.SaveSystems</c> calls <c>system?.BeforeSave()</c> immediately before
		/// <c>Writer.Write(system)</c> for every registered system, in the same loop
		/// (<c>XRL/XRLGame.cs:1580-1589</c>, <c>:2300-2306</c>). The primary save is not backed up
		/// or written until after all systems, zones, quests, factions, and Journal state finish
		/// serializing and <c>FinalizeWrite</c> returns (<c>:2307-2357</c>). An exception here reaches
		/// the outer handler at <c>:2383-2387</c>; its <c>SaveGameError</c> call has
		/// <c>RestoreBackup=false</c> and therefore only logs/reports (<c>:2459-2473</c>).
		/// Throwing is therefore not a side effect of this override &mdash; it is the entire
		/// mechanism. There is nothing else in the engine that lets a mod veto a save.
		/// </para>
		/// <para>
		/// Two latches are checked. This override only reads them; it never sets, clears, or
		/// repairs either one. <see cref="LoadFailed"/> means the engine's positional reader or
		/// <see cref="Read"/> could not make sense of what was on disk.
		/// <see cref="ReportLoadFailure"/> may tell the founder once,
		/// but presentation never retires this authority latch.
		/// <see cref="RealmIdentityFenceFault"/> means the live realm and the base game's own
		/// identity fence have diverged (<see cref="KingdomIdentityFenceRuntime"/>). Either one
		/// standing means this instance is not trustworthy enough to become the founder's save, so
		/// saving is refused outright rather than quietly writing over whatever is already there.
		/// </para>
		/// </summary>
		public override void BeforeSave()
		{
			if (LoadFailed)
			{
				throw new InvalidOperationException(
					"ThousandAndFirst: refusing to save. The last load of this kingdom's " +
					"records could not be read, and saving now would write that failure over " +
					"the good save you already have. Quit without saving; the save on disk is " +
					"untouched. This session cannot safely be saved after the failed load, " +
					"even after its warning has been dismissed.");
			}
			if (!string.IsNullOrEmpty(RealmIdentityFenceFault))
			{
				throw new InvalidOperationException(
					"ThousandAndFirst: refusing to save. This realm's identity no longer " +
					"agrees with the base game's own record of it (" + RealmIdentityFenceFault +
					"), and saving now would make that mismatch permanent. Quit without saving " +
					"and report this fault; the save on disk is untouched.");
			}
		}
	}
}
