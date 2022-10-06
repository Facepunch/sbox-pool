using Sandbox;
using System.Linq;

namespace Facepunch.Pool
{
	public partial class PowerupEntity : ModelEntity
	{
		[Net] public Powerup Powerup { get; private set; }

		private TimeUntil NextMovePowerup { get; set; }

		public override void Spawn()
		{
			SetModel( "models/pool/pool_ball.vmdl" );
			SetupPhysicsFromModel( PhysicsMotionType.Dynamic, true );

			Tags.Add( "powerup" );

			Transmit = TransmitType.Always;

			var powerups = TypeLibrary.GetDescriptions<Powerup>().ToList();
			Powerup = Rand.FromList( powerups ).Create<Powerup>();
			Powerup.OnSpawn( this );

			EnableTouch = true;

			base.Spawn();
		}

		public override void StartTouch( Entity other )
		{
			if ( other is PoolBall ball && ( ball.Type == PoolBallType.White || ball.Type == Game.Instance.CurrentPlayer.BallType ) )
			{
				Log.Info( "Hit powerup: " + Powerup.Name );
			}

			base.StartTouch( other );
		}

		[Event.Tick.Server]
		private void ServerTick()
		{
			if ( !Powerup.IsStatic )
			{
				if ( NextMovePowerup )
				{
					NextMovePowerup = Rand.Float( 4f, 6f );
					ApplyAbsoluteImpulse( Vector3.Random.WithZ( 0f ) * 100f * 3f );
				}
			}
		}
	}
}
