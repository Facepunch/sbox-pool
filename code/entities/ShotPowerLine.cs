using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Facepunch.Pool
{
	[Library( "shot_power_arrow" )]
	public partial class ShotPowerLine : RenderEntity
	{
		public Material PowerCircleMaterial = Material.Load( "materials/pool_power_circle.vmat" );
		public Material LineMaterial = Material.Load( "materials/pool_cue_line.vmat" );
		public bool IsEnabled { get; set; }
		public float ShotPower { get; set; }
		public Color Color { get; set; }
		public Vector3 EndPos { get; set; }
		public float Width { get; set; } = 1f;

		public override void DoRender( SceneObject sceneObject  )
		{
			if ( IsEnabled )
			{
				var vertexBuffer = new VertexBuffer();
				vertexBuffer.Init( true );

				var widthOffset = Vector3.Cross( ( EndPos - Position).Normal, Vector3.Up ) * Width;
				var powerFraction = (ShotPower / 100f);
				var offset = (EndPos - Position);

				var a = new Vertex( -widthOffset, Vector3.Up, Vector3.Right, new Vector4( 0, 1, 0, 0 ) );
				var b = new Vertex( widthOffset, Vector3.Up, Vector3.Right, new Vector4( 1, 1, 0, 0 ) );
				var c = new Vertex( offset + widthOffset, Vector3.Up, Vector3.Right, new Vector4( 1, 0, 0, 0 ) );
				var d = new Vertex( offset - widthOffset, Vector3.Up, Vector3.Right, new Vector4( 0, 0, 0, 0 ) );

				vertexBuffer.AddQuad( a, b, c, d );

				var attributes = new RenderAttributes();
				attributes.Set( "Opacity", 0.5f + ( ( 0.5f / 100f ) * powerFraction ) );
				attributes.Set( "Color", Color );

				vertexBuffer.Draw( LineMaterial, attributes );
				vertexBuffer.Clear();

				var circleSize = 1f + (5f * powerFraction);

				a = new Vertex( new Vector3( -circleSize, -circleSize, 0f ), Vector3.Up, Vector3.Right, new Vector4( 0, 1, 0, 0 ) );
				b = new Vertex( new Vector3( circleSize, -circleSize, 0f ), Vector3.Up, Vector3.Right, new Vector4( 1, 1, 0, 0 ) );
				c = new Vertex( new Vector3( circleSize, circleSize, 0f ), Vector3.Up, Vector3.Right, new Vector4( 1, 0, 0, 0 ) );
				d = new Vertex( new Vector3( -circleSize, circleSize, 0f ), Vector3.Up, Vector3.Right, new Vector4( 0, 0, 0, 0 ) );

				vertexBuffer.AddQuad( a, b, c, d );

				attributes.Set( "Opacity", powerFraction );

				vertexBuffer.Draw( PowerCircleMaterial, attributes );
			}
		}
	}
}
