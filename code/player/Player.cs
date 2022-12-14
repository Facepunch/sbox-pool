using Sandbox;
using System;
using System.Linq;

namespace Facepunch.Pool
{
	public partial class Player : Entity
	{
		[Net] public TimeSince TimeSinceWhiteStruck { get; private set; }
		[Net] public bool HasStruckWhiteBall { get; set; }
		[Net] public PoolBallType BallType { get; set; }
		[Net] public bool IsSpectator { get; private set;  }
		[Net] public FoulReason FoulReason { get; private set; }
		[Net] public bool IsPlacingWhiteBall { get; private set; }
		[Net] public bool HasSecondShot { get; set; }
		[Net] public bool IsTurn { get; private set; }
		[Net] public int Score { get; set; }
		[Net] public EloScore Elo { get; private set; }
		public bool DidHitOwnBall { get; set; }
		public bool DidPotBall { get; set; }

		[ClientInput] public Vector3 CursorDirection { get; set; }
		[ClientInput] public Vector3 CameraPosition { get; set; }

		public int BallsLeft
		{
			get
			{
				var balls = All.OfType<PoolBall>().Where( ( e ) =>
				{
					return e.Type == BallType;
				} );

				return balls.Count();
			}
		}

		public Player()
		{
			Elo = new();
			Transmit = TransmitType.Always;
		}

		public void MakeSpectator( bool isSpectator )
		{
			HasStruckWhiteBall = false;
			IsSpectator = isSpectator;
			IsTurn = false;
			Score = 0;
		}

		[ClientRpc]
		public void SendSound( string soundName )
		{
			PlaySound( soundName );
		}

		public void StartPlacingWhiteBall()
		{
			var whiteBall = PoolGame.Entity.WhiteBall;

			if ( whiteBall != null && whiteBall.IsValid() )
			{
				whiteBall.StartPlacing();
				whiteBall.Owner = this;
			}

			_ = PoolGame.Entity.RespawnBallAsync( whiteBall );

			IsPlacingWhiteBall = true;
		}

		public void StopPlacingWhiteBall()
		{
			var whiteBall = PoolGame.Entity.WhiteBall;

			if ( whiteBall != null && whiteBall.IsValid() )
			{
				whiteBall.StopPlacing();
				whiteBall.Owner = null;
			}

			IsPlacingWhiteBall = false;
		}

		public void Foul( FoulReason reason )
		{
			if ( FoulReason == FoulReason.None )
			{
				Log.Info( Client.Name + " has fouled (reason: " + reason.ToString() + ")" );

				PoolGame.Entity.AddToast( To.Everyone, this, reason.ToMessage( this ), "foul" );

				PlaySound( "foul" );

				HasSecondShot = false;
				FoulReason = reason;
			}
		}

		public void StartPlaying()
		{
			_ = Elo.Update( Client );
			MakeSpectator( false );
			BallType = PoolBallType.White;
			Score = 0;
		}

		public void StartTurn(bool hasSecondShot = false, bool showMessage = true)
		{
			if ( showMessage )
				PoolGame.Entity.AddToast( To.Everyone, this, $"{ Client.Name } has started their turn" );

			SendSound( To.Single( this ), "ding" );

			// This player will be predicting the pool cue now.
			PoolGame.Entity.CurrentPlayer = this;
			PoolGame.Entity.Cue.Owner = this;

			HasStruckWhiteBall = false;
			HasSecondShot = hasSecondShot;
			FoulReason = FoulReason.None;
			DidHitOwnBall = false;
			DidPotBall = false;
			IsTurn = true;

			if ( hasSecondShot )
				StartPlacingWhiteBall();
		}

		public void FinishTurn()
		{
			HasStruckWhiteBall = false;
			IsTurn = false;
		}

		public void StrikeWhiteBall( PoolCue cue, PoolBall whiteBall, float force )
		{
			var direction = cue.DirectionTo( whiteBall );

			whiteBall.PhysicsBody.ApplyImpulse( direction * force * whiteBall.PhysicsBody.Mass );

			TimeSinceWhiteStruck = 0;
			HasStruckWhiteBall = true;
		}

		public override void BuildInput()
		{
			CursorDirection = Mouse.Visible ? Screen.GetDirection( Mouse.Position ) : Camera.Rotation.Forward;
			CameraPosition = Camera.Position;

			base.BuildInput();
		}

		public override void Simulate( IClient client )
		{
			PoolGame.Entity.Round?.UpdatePlayerPosition( this );
			base.Simulate( client );
		}
	}
}
