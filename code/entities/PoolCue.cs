using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Facepunch.Pool
{
	[Library( "pool_cue" )]
	public partial class PoolCue : ModelEntity
	{
		[Net, Predicted] public Vector3 AimDir { get; set; }
		[Net, Predicted] public float ShotPower { get; set; }
		public bool IsMakingShot { get; set; }
		public float CuePitch { get; set; }
		public float CueYaw { get; set; }
		public ShotPowerLine ShotPowerLine { get; set; }
		public ModelEntity GhostBall { get; private set; }

		private float CuePullBackOffset;
		private float LastPowerDistance;
		private float MaxCuePitch = 17f;
		private float MinCuePitch = 5f;

		public void Reset()
		{
			IsMakingShot = false;
		}

		public Vector3 DirectionTo( PoolBall ball )
		{
			return (ball.Position - Position.WithZ( ball.Position.z )).Normal;
		}

		public override void Spawn()
		{
			base.Spawn();

			SetModel( "models/pool/pool_cue_b.vmdl" );

			EnableDrawing = false;
			Predictable = true;
			Transmit = TransmitType.Always;
		}

		public override void Simulate( Client client )
		{
			var whiteBall = Game.Instance.WhiteBall;
			
			EnableDrawing = false;

			if ( !IsOwnerInPlay( whiteBall, out var controller ) )
				return;

			if ( controller.IsPlacingWhiteBall )
			{
				HandleWhiteBallPlacement( controller, whiteBall );
				return;
			}

			if ( !Input.Down( InputButton.PrimaryAttack ) )
			{
				UpdateAimDir( controller, whiteBall.Position );

				if ( !IsMakingShot )
				{
					RotateCue( whiteBall );
				}
				else
				{
					if ( Host.IsServer )
						TakeShot( controller, whiteBall );

					CuePullBackOffset = 0f;
					IsMakingShot = false;
					ShotPower = 0f;
				}
			}
			else
			{
				HandlePowerSelection( controller );
			}

			EnableDrawing = true;
			Position = whiteBall.Position - Rotation.Forward * (1f + CuePullBackOffset + (CuePitch * 0.04f));

			// Never interpolate just update its position immediately for everybody.
			ResetInterpolation();

			base.Simulate( client );
		}

		[Event( EventType.ClientTick )]
		private void Tick()
		{
			var whiteBall = Game.Instance.WhiteBall;

			if ( Host.IsClient )
			{
				if ( ShotPowerLine != null )
					ShotPowerLine.IsEnabled = false;

				if ( GhostBall != null )
					GhostBall.EnableDrawing = false;
			}

			if ( !IsOwnerInPlay( whiteBall, out var controller ) )
				return;

			if ( controller.IsPlacingWhiteBall )
			{
				ShowWhiteArea( true );
				return;
			}	
			else
			{
				ShowWhiteArea( false );
			}

			if ( ShotPowerLine == null )
				ShotPowerLine = new ShotPowerLine();

			var trace = Trace.Ray( whiteBall.Position, whiteBall.Position + AimDir * 1000f )
				.WithoutTags( "powerup" )
				.Ignore( whiteBall )
				.Ignore( this )
				.Run();

			ShotPowerLine.IsEnabled = true;
			ShotPowerLine.Position = trace.StartPosition;
			ShotPowerLine.ShotPower = ShotPower;
			ShotPowerLine.EndPos = trace.EndPosition;
			ShotPowerLine.Color = Color.Green;
			ShotPowerLine.Width = 0.1f + ((0.15f / 100f) * ShotPower);

			var fromTransform = whiteBall.PhysicsBody.Transform;
			var toTransform = whiteBall.PhysicsBody.Transform;
			toTransform.Position = trace.EndPosition;

			var sweep = Trace.Sweep( whiteBall.PhysicsBody, fromTransform, toTransform )
				.Ignore( whiteBall )
				.Run();

			if ( sweep.Hit )
			{
				if ( GhostBall == null )
				{
					GhostBall = new ModelEntity();
					GhostBall.SetModel( "models/pool/pool_ball.vmdl" );
					GhostBall.RenderColor = Color.White.WithAlpha( 0.4f );
				}

				if ( sweep.Entity is PoolBall other && !other.CanPlayerHit( controller ) )
				{
					ShotPowerLine.Color = Color.Red;
					GhostBall.RenderColor = Color.Red;
				}
				else
				{
					GhostBall.RenderColor = Color.White;
				}

				GhostBall.EnableDrawing = true;
				GhostBall.Position = sweep.EndPosition;
			}
		}

		private bool IsOwnerInPlay( PoolBall whiteBall, out Player controller )
		{
			controller = Owner as Player;

			if ( controller == null )
				return false;

			if ( !controller.IsTurn || controller.HasStruckWhiteBall )
				return false;

			if ( whiteBall == null || !whiteBall.IsValid() )
				return false;

			return true;
		}

		private void ShowWhiteArea( bool shouldShow )
		{
			if ( Host.IsServer ) return;

			var whiteArea = Game.Instance.WhiteArea;

			if ( whiteArea != null && whiteArea.IsValid() )
				whiteArea.Quad.IsEnabled = shouldShow;
		}

		private void TakeShot( Player controller, PoolBall whiteBall )
		{
			Host.AssertServer();

			if ( ShotPower >= 5f )
			{
				using ( Prediction.Off() )
				{
					controller.StrikeWhiteBall( this, whiteBall, ShotPower * 6f );

					var soundFileId = Convert.ToInt32( MathF.Round( (2f / 100f) * ShotPower ) );
					whiteBall.PlaySound( $"shot-power-{soundFileId}" );
				}
			}
		}

		private void HandleWhiteBallPlacement( Player controller, PoolBall whiteBall )
		{
			var cursorTrace = Trace.Ray( controller.EyePosition, controller.EyePosition + controller.CursorDirection * 1000f )
				.WorldOnly()
				.Run();

			var whiteArea = Game.Instance.WhiteArea;
			var whiteAreaWorldOBB = whiteArea.CollisionBounds.ToWorldSpace( whiteArea );

			whiteBall.TryMoveTo( cursorTrace.EndPosition, whiteAreaWorldOBB );

			if ( Input.Released( InputButton.PrimaryAttack ) )
				controller.StopPlacingWhiteBall();
		}

		private void HandlePowerSelection( Player controller )
		{
			var cursorPlaneEndPos = controller.EyePosition + controller.CursorDirection * 350f;
			var distanceToCue = cursorPlaneEndPos.Distance( Position - Rotation.Forward * 100f );
			var cuePullBackDelta = (LastPowerDistance - distanceToCue) * Time.Delta * 20f;

			if ( !IsMakingShot )
			{
				LastPowerDistance = 0f;
				cuePullBackDelta = 0f;
			}

			CuePullBackOffset = Math.Clamp( CuePullBackOffset + cuePullBackDelta, 0f, 8f );
			LastPowerDistance = distanceToCue;

			IsMakingShot = true;
			ShotPower = CuePullBackOffset.AsPercentMinMax( 0f, 8f );
		}

		private bool UpdateAimDir( Player controller, Vector3 ballCenter )
		{
			if ( IsMakingShot ) return true;

			var tablePlane = new Plane( ballCenter, Vector3.Up );
			var hitPos = tablePlane.Trace( new Ray( controller.EyePosition, controller.CursorDirection ), true );

			if ( !hitPos.HasValue ) return false;

			AimDir = (hitPos.Value - ballCenter).WithZ( 0 ).Normal;

			return true;
		}

		private void RotateCue( PoolBall whiteBall )
		{
			var rollTrace = Trace.Ray( whiteBall.Position, whiteBall.Position - AimDir * 100f )
				.Ignore( this )
				.Ignore( whiteBall )
				.Run();

			var aimRotation = Rotation.LookAt( AimDir, Vector3.Forward );

			CuePullBackOffset = CuePullBackOffset.LerpTo( 0f, Time.Delta * 10f );

			CuePitch = CuePitch.LerpTo( MinCuePitch + ((MaxCuePitch - MinCuePitch) * (1f - rollTrace.Fraction)), Time.Delta * 10f );
			CueYaw = aimRotation.Yaw().Normalize( 0f, 360f );

			if ( IsAuthority )
			{
				Rotation = Rotation.From(
					Rotation.Angles()
						.WithYaw( CueYaw )
						.WithPitch( CuePitch )
				);
			}
		}
	}
}
