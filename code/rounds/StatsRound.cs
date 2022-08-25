using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Facepunch.Pool
{
	public partial class StatsRound : BaseRound
	{
		public override string RoundName => "STATS";
		public override int RoundDuration => 10;

		protected override void OnStart()
		{

		}

		protected override void OnFinish()
		{

		}

		protected override void OnTimeUp()
		{
			Game.Instance.HideWinSummary( To.Everyone );
			Game.Instance.ChangeRound( new PlayRound() );

			base.OnTimeUp();
		}

		public override void OnPlayerJoin( Player player )
		{
			if ( Players.Contains( player ) ) return;

			AddPlayer( player );

			base.OnPlayerJoin( player );
		}
	}
}
