using Sandbox;
using System.Linq;

namespace Facepunch.Pool
{
	[ClassName( "rules_power_pool")]
	public class PowerPoolRules : BaseGameRules
	{
		private TimeUntil NextSpawnPowerup { get; set; }

		public override PoolBall CreatePoolBall() => new PowerPoolBall();

		protected override void OnStart()
		{
			NextSpawnPowerup = Rand.Float( 10f, 30f );
		}

		[Event.Tick.Server]
		private void ServerTick()
		{
			if ( !IsPlaying ) return;

			if ( Entity.All.OfType<PowerupEntity>().Count() > 2 )
			{
				NextSpawnPowerup = Rand.Float( 20f, 40f );
				return;
			}

			if ( NextSpawnPowerup )
			{
				var powerup = new PowerupEntity();
				var spawn = Rand.FromList( Entity.All.OfType<PoolBallSpawn>().ToList() );

				powerup.Transform = spawn.Transform;

				NextSpawnPowerup = Rand.Float( 20f, 40f );
			}
		}
	}
}
