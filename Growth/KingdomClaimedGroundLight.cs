using System;
using XRL.World;

namespace XRL.World.ZoneParts
{
	/// <summary>
	/// The founder's own ground, read at a glance. While the founder stands in a claimed zone this
	/// raises the whole zone to <see cref="LightLevel.Light"/> once per rendered frame &mdash; the
	/// same tier a torch answers with, so walls still stop sight, interiors behind them stay dark,
	/// and nothing hidden is revealed. Deliberately not the omniscient tier: the engine treats that
	/// one as psychic sight (D/XRL/World/Parts/Mutation/Invisibility.cs:65), and a lit city is
	/// lamplight, not second sight. Mod-owned rather than a vanilla part, so a save loaded without
	/// this mod is dark ground again instead of ground somebody else's part lights forever.
	/// </summary>
	[Serializable]
	public sealed class KingdomClaimedGroundLight : IZonePart
	{
		public int Version = 1;
		public string SettlementId = "";

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == BeforeRenderEvent.ID;
		}

		public override bool HandleEvent(BeforeRenderEvent E)
		{
			// Presentation only, and only where the founder actually is: a claimed zone the founder
			// is not standing in pays nothing at all. The option is read here as well as at the
			// activation that attaches the part, so switching it off goes dark on the next frame
			// instead of waiting for the visit that removes the part. The founder is checked for
			// existence first because this runs on the render dispatch: the engine sends
			// BeforeRenderEvent before it touches the player itself (D/XRL/Core/XRLCore.cs:2507),
			// and Zone.HasObject dereferences what it is handed (D/XRL/World/Zone.cs:3365-3368),
			// so a frame drawn with no player must be a frame this part does nothing on rather
			// than a null reference thrown out of the renderer.
			if (ParentZone != null
				&& ThousandAndFirst.KingdomClaimedGround.Enabled
				&& The.Player != null
				&& ParentZone.HasObject(The.Player))
				ParentZone.AddLight(LightLevel.Light);
			return base.HandleEvent(E);
		}

		public override void Write(Zone Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomClaimedGroundLight));
		}

		public override void Read(Zone Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomClaimedGroundLight));
			SettlementId = SettlementId ?? "";
		}
	}
}
