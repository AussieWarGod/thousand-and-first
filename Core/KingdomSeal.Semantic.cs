using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using XRL;
using XRL.Core;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSeal : IPlayerSystem
	{
		/// <summary>Marks a semantic action dirty. Call <see cref="TryStageSemanticSnapshot"/> at
		/// the coherent end of that action.</summary>
		public static void MarkSemanticDirty(string Reason)
		{
			try
			{
				KingdomSystem kingdom = The.Game?.GetSystem<KingdomSystem>();
				if (!KingdomMaster.NewWorkAllowed(kingdom) || !SealEnabled())
				{
					return;
				}
				KingdomSeal seal = The.Game?.GetSystem<KingdomSeal>();
				if (seal != null)
				{
					string authorityFailure;
					if (!seal.TryRequireAuthority(out authorityFailure))
					{
						seal.ReportFailure("mark dirty", authorityFailure);
						return;
					}
					seal.MarkDirty(Reason);
				}
			}
			catch (Exception ex)
			{
				LogStaticFailure("mark dirty", ex);
			}
		}

		/// <summary>Stages the next coherent living snapshot after a semantic action. Safe to call
		/// when no fact changed; canonical comparison suppresses a redundant revision.</summary>
		public static bool TryStageSemanticSnapshot(string Reason, out string Failure)
		{
			Failure = "";
			try
			{
				KingdomSystem kingdom = The.Game?.GetSystem<KingdomSystem>();
				if (!KingdomMaster.NewWorkAllowed(kingdom) || !SealEnabled())
				{
					return true;
				}
				KingdomSeal seal = The.Game?.GetSystem<KingdomSeal>();
				if (seal == null)
				{
					Failure = "the seal coordinator is not loaded";
					return false;
				}
				if (!seal.TryRequireAuthority(out Failure))
				{
					return false;
				}
				seal.MarkDirty(Reason);
				return seal.TryFlushLiving(Reason, ProbeEvenIfClean: true, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				LogStaticFailure("semantic stage", ex);
				return false;
			}
		}

		/// <summary>Founding's immediate flush. Loader/founding integration calls this after the
		/// founding action is wholly published.</summary>
		public static bool TryFoundingCompleted(out string Failure)
		{
			Failure = "";
			try
			{
				KingdomSystem kingdom = The.Game?.GetSystem<KingdomSystem>();
				if (!KingdomMaster.NewWorkAllowed(kingdom) || !SealEnabled())
				{
					return true;
				}
				KingdomSeal seal = The.Game?.RequireSystem<KingdomSeal>();
				if (seal == null)
				{
					Failure = "the seal coordinator could not be loaded";
					return false;
				}
				if (!seal.TryRequireAuthority(out Failure))
				{
					return false;
				}
				seal.MarkDirty("founding");
				bool result = seal.TryFlushLiving("founding", ProbeEvenIfClean: true, out Failure);
				if (!result)
				{
					seal.ReportFailure("founding stage", Failure);
				}
				return result;
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				LogStaticFailure("founding stage", ex);
				return false;
			}
		}

		/// <summary>
		/// Kingdom-mode terminal route. Succession calls this only after exact-heir resolution has
		/// ruled the line ended. Calling it for a successful accession is refused.
		/// </summary>
		public static bool TryTerminalFromSuccession(AfterDieEvent Death, bool LineEnded,
			out string Failure)
		{
			Failure = "";
			try
			{
				if (!SealEnabled())
				{
					return true;
				}
				XRLGame game = The.Game;
				KingdomSeal seal = game?.GetSystem<KingdomSeal>();
				KingdomSystem kingdom = game?.GetSystem<KingdomSystem>();
				if (seal == null || game == null)
				{
					Failure = "the seal coordinator is not loaded";
					return false;
				}
				if (!KingdomMaster.AutomaticWorkAllowed(kingdom))
				{
					Failure = "realm simulation is paused by the master option";
					return false;
				}
				if (!seal.TryRequireAuthority(out Failure))
				{
					return false;
				}
				if (!KingdomSealEngineRules.AcceptSuccessionTerminal(IsKingdomMode(game),
					kingdom != null && kingdom.Founded, seal.IsGenerationSealed, LineEnded))
				{
					Failure = "succession has not ruled an unsealed Kingdom-mode line ended";
					return false;
				}
				bool result = seal.TryWriteTerminal(DeathReason(Death), DeathCategory(Death),
					game.TimeTicks, out Failure);
				if (!result)
				{
					seal.ReportFailure("succession terminal attempt", Failure);
				}
				return result;
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				LogStaticFailure("succession terminal attempt", ex);
				return false;
			}
		}

		/// <summary>
		/// Successful-accession route. Must be called after body/row/ledger/chronicle publication.
		/// Token is succession's exact founder-death token and makes a retry idempotent.
		/// </summary>
		public static bool TryStartSuccessorGeneration(string AccessionToken, out string Failure)
		{
			Failure = "";
			try
			{
				XRLGame game = The.Game;
				KingdomSystem kingdom = game?.GetSystem<KingdomSystem>();
				if (!KingdomMaster.AutomaticWorkAllowed(kingdom))
				{
					Failure = "realm simulation is paused by the master option";
					return false;
				}
				if (!SealEnabled())
				{
					return true;
				}
				KingdomSeal seal = game?.GetSystem<KingdomSeal>();
				if (seal == null)
				{
					Failure = "the seal coordinator is not loaded";
					return false;
				}
				if (!seal.TryRequireAuthority(out Failure))
				{
					return false;
				}
				bool result = seal.TryAdvanceGeneration(AccessionToken, out Failure);
				if (!result)
				{
					seal.ReportFailure("successor generation", Failure);
				}
				return result;
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				LogStaticFailure("successor generation", ex);
				return false;
			}
		}

		/// <summary>Immediately stages and promotes explicit retirement. The save remains alive;
		/// <see cref="RetiredLegacyId"/> records the exact immutable generation in it.</summary>
		public static bool TryRetireGeneration(out string Failure)
		{
			Failure = "";
			try
			{
				KingdomSystem kingdom = The.Game?.GetSystem<KingdomSystem>();
				if (!KingdomMaster.NewWorkAllowed(kingdom))
				{
					Failure = "realm simulation is paused by the master option";
					return false;
				}
				if (!SealEnabled())
				{
					Failure = "realm sealing is disabled in the options";
					return false;
				}
				KingdomSeal seal = The.Game?.GetSystem<KingdomSeal>();
				if (seal == null)
				{
					Failure = "the seal coordinator is not loaded";
					return false;
				}
				if (!seal.TryRequireAuthority(out Failure))
				{
					return false;
				}
				bool result = seal.TryRetire(out Failure);
				if (!result)
				{
					seal.ReportFailure("retirement", Failure);
				}
				return result;
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				LogStaticFailure("retirement", ex);
				return false;
			}
		}

		/// <summary>
		/// Import coordinator entrypoint. Returns true with null outputs when policy has nothing to
		/// offer. An existing exact-target reservation is returned on retry.
		/// </summary>
	}
}
