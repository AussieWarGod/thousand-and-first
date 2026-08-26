using System;

using ThousandAndFirst;

// XRL.World.Parts, for the reason every other part in this folder states: GamePartBlueprint
// resolves a part named in XML as exactly "XRL.World.Parts.<Name>" and tries no other name.
namespace XRL.World.Parts
{
	/// <summary>
	/// The registry office: the annexe's book, read in a city that did not raise the annexe.
	/// <para>
	/// <b>It opens the annexe's own screen and nothing of its own.</b> The reduction is not
	/// performed here and is not performed anywhere: the enrolment gate already asks whether the
	/// building it is standing in IS the annexe (<c>KingdomAnnexe.JudgeFor</c> passes
	/// <c>Building.HasPart("r_KingdomBecomingAnnexe")</c>), and this building is not, so the
	/// ceremony refuses through the gate that was already there. Addendum 22 A2's clause &mdash;
	/// lower-rung outposts may sit anywhere, once-ever ceremonies stay sited &mdash; is enforced by
	/// the shipped code answering a question it was already asking.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomRegistryOffice : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID
				|| ID == GetShortDescriptionEvent.ID;
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append(KingdomSatellite.OfficeDescription());
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Register", "read the realm's rolls", "r_OpenAnnexeRegister", null, 'g', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_OpenAnnexeRegister" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("registry office", delegate
				{
					KingdomAnnexe.OpenRegister(ParentObject, E.Actor);
				});
				return true;
			}
			return base.HandleEvent(E);
		}
	}
}
