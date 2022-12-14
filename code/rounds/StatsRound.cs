using Sandbox;

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
			PoolGame.Entity.HideWinSummary( To.Everyone );
			PoolGame.Entity.ChangeRound( new PlayRound() );

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
