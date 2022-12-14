using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.UI;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Facepunch.Pool
{
	public partial class PoolBall : ModelEntity
	{
		[Net] public PoolBallNumber Number { get; private set; }
		[Net] public PoolBallType Type { get; private set; }
		public Player LastStriker { get; private set; }
		public bool IsAnimating { get; private set; }
		public TriggerBallPocket LastPocket { get; set; }

		public void ResetLastStriker()
		{
			LastStriker = null;
		}

		public void StartPlacing()
		{
			EnableAllCollisions = false;
			PhysicsEnabled = false;
		}

		public string GetIconClass()
		{
			if ( Type == PoolBallType.Black )
				return "black";
			else if ( Type == PoolBallType.White )
				return "white";

			return $"{ Type.ToString().ToLower() }_{ (int)Number }";
		}

		public bool CanPlayerHit( Player player )
		{
			if ( player.BallType == PoolBallType.White )
			{
				if ( Type != PoolBallType.Black )
					return true;
				else
					return false;
			}

			if ( PoolGame.Entity.GetBallPlayer( this ) == player )
				return true;

			if ( Type == PoolBallType.Black && player.BallsLeft == 0 )
				return true;

			return false;
		}

		public async Task AnimateIntoPocket()
		{
			Assert.True( !IsAnimating );

			PhysicsEnabled = false;
			IsAnimating = true;

			while ( true )
			{
				await Task.Delay( 30 );

				RenderColor = RenderColor.WithAlpha( RenderColor.a.LerpTo( 0f, Time.Delta * 5f ) );

				if ( LastPocket != null && LastPocket.IsValid() )
					Position = Position.LerpTo( LastPocket.Position + LastPocket.CollisionBounds.Center, Time.Delta * 16f );

				if ( RenderColor.a.AlmostEqual( 0f ) )
					break;
			}

			PhysicsEnabled = true;
			IsAnimating = false;
		}

		public void StopPlacing()
		{
			EnableAllCollisions = true;
			PhysicsEnabled = true;
			ResetInterpolation();
		}

		public void SetType( PoolBallType type, PoolBallNumber number )
		{
			if ( type == PoolBallType.Black )
				SetMaterialGroup( 8 );
			else if ( type == PoolBallType.Spots )
				SetMaterialGroup( (int)number );
			else if ( type == PoolBallType.Stripes )
				SetMaterialGroup( (int)number + 8 );

			Number = number;
			Type = type;
		}

		public void TryMoveTo( Vector3 worldPos, BBox within )
		{
			if ( !IsAuthority ) return;

			var worldOBB = CollisionBounds + worldPos;

			foreach ( var ball in All.OfType<PoolBall>() )
			{
				if ( ball != this )
				{
					var ballOBB = ball.CollisionBounds + ball.Position;

					// We can't place on other balls.
					if ( ballOBB.Overlaps( worldOBB ) )
						return;
				}
			}

			if ( within.ContainsXY( worldOBB ) )
			{
				Position = worldPos.WithZ( Position.z );
				ResetInterpolation();
			}
		}

		public override void Spawn()
		{
			base.Spawn();

			SetModel( "models/pool/pool_ball.vmdl" );
			SetupPhysicsFromModel( PhysicsMotionType.Dynamic, true );

			Tags.Add( "ball" );

			Transmit = TransmitType.Always;
		}

		public virtual void OnEnterPocket( TriggerBallPocket pocket )
		{
			// We may already be animating into a pocket... if we are, don't continue.
			if ( !IsAnimating )
			{
				LastPocket = pocket;
				PoolGame.Entity.Round?.OnBallEnterPocket( this, pocket );
			}
		}

		protected override void OnPhysicsCollision( CollisionEventData eventData )
		{
			// Our last striker is the one responsible for this collision.
			if ( eventData.Other.Entity is PoolBall other )
			{
				LastStriker = PoolGame.Entity.CurrentPlayer;
				PoolGame.Entity.Round?.OnBallHitOtherBall( this, other );

				var sound = PlaySound( "ball-collide" );
				sound.SetPitch( Game.Random.Float( 0.9f, 1f ) );
				sound.SetVolume( (1f / 100f) * eventData.Speed );

				Velocity = eventData.This.PostVelocity.WithZ( 0f );
			}
			else
			{
				if ( Math.Abs( eventData.Normal.x ) >= 0.5f || Math.Abs( eventData.Normal.y ) >= 0.5f )
				{
					var sound = PlaySound( "ball-hit-side" );
					sound.SetPitch( Game.Random.Float( 0.9f, 1f ) );
					sound.SetVolume( (1f / 100f) * eventData.Speed );

					var velocity = eventData.This.PreVelocity.WithZ( 0f );
					var speed = velocity.Length;
					var direction = Vector3.Reflect( velocity.Normal, eventData.Normal.Normal ).Normal;
					Velocity = direction * speed * 0.8f;
				}
				else
				{
					Velocity = eventData.This.PostVelocity.WithZ( 0f );
				}
			}

			base.OnPhysicsCollision( eventData );
		}
	}
}
