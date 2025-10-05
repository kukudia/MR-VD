// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Archanor VFX/Real Fog/UnlitFog"
{
	Properties
	{
		_Texture("Texture", 2D) = "white" {}
		_FogColor("Fog Color", Color) = (1,1,1,0)
		_FogGlow("Fog Glow", Range( 0 , 5)) = 0
		_SoftParticlesFactor("Soft Particles Factor", Range( 0 , 1)) = 0.51
		_CameraFadeDistance("Camera Fade Distance", Range( 0 , 20)) = 10
		_CameraFadeSharpness("Camera Fade Sharpness", Range( 1 , 15)) = 1
		[Toggle(_SOFTPARTICLES_ON)] _SoftParticles("Soft Particles", Float) = 1
		[Toggle(_CAMERAFADE_ON)] _CameraFade("Camera Fade", Float) = 1
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Off
		CGPROGRAM
		#include "UnityCG.cginc"
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma shader_feature_local _CAMERAFADE_ON
		#pragma shader_feature_local _SOFTPARTICLES_ON
		#pragma surface surf Standard alpha:fade keepalpha noshadow exclude_path:deferred 
		struct Input
		{
			float2 uv_texcoord;
			float4 vertexColor : COLOR;
			float4 screenPos;
			float3 worldPos;
		};

		uniform float _FogGlow;
		uniform float4 _FogColor;
		uniform sampler2D _Texture;
		uniform float4 _Texture_ST;
		UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
		uniform float4 _CameraDepthTexture_TexelSize;
		uniform float _SoftParticlesFactor;
		uniform float _CameraFadeDistance;
		uniform float _CameraFadeSharpness;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_Texture = i.uv_texcoord * _Texture_ST.xy + _Texture_ST.zw;
			float4 tex2DNode3 = tex2D( _Texture, uv_Texture );
			o.Emission = ( ( _FogGlow + _FogColor ) * ( tex2DNode3 * i.vertexColor ) ).rgb;
			float temp_output_49_0 = ( tex2DNode3.a * i.vertexColor.a * _FogColor.a );
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			float screenDepth40 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_screenPosNorm.xy ));
			float distanceDepth40 = abs( ( screenDepth40 - LinearEyeDepth( ase_screenPosNorm.z ) ) / ( 1.0 ) );
			float2 _Vector0 = float2(1,0);
			float temp_output_51_0 = ( temp_output_49_0 * saturate( ( distanceDepth40 * (_Vector0.x + (_SoftParticlesFactor - _Vector0.y) * (_Vector0.y - _Vector0.x) / (_Vector0.x - _Vector0.y)) ) ) );
			#ifdef _SOFTPARTICLES_ON
				float staticSwitch77 = temp_output_51_0;
			#else
				float staticSwitch77 = temp_output_49_0;
			#endif
			float3 ase_worldPos = i.worldPos;
			float clampResult76 = clamp( pow( ( distance( ase_worldPos , _WorldSpaceCameraPos ) / _CameraFadeDistance ) , _CameraFadeSharpness ) , 0.0 , 1.0 );
			#ifdef _CAMERAFADE_ON
				float staticSwitch78 = ( temp_output_51_0 * clampResult76 );
			#else
				float staticSwitch78 = staticSwitch77;
			#endif
			o.Alpha = staticSwitch78;
		}

		ENDCG
	}
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18909
87;110;1536;1001;1975.168;25.83231;1.094748;True;False
Node;AmplifyShaderEditor.WorldSpaceCameraPos;68;-1535.222,767.9733;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldPosInputsNode;69;-1532.223,532.8253;Inherit;True;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.Vector2Node;81;-1177.973,491.2291;Inherit;False;Constant;_Vector0;Vector 0;8;0;Create;True;0;0;0;False;0;False;1,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;54;-1308.12,401.0468;Inherit;False;Property;_SoftParticlesFactor;Soft Particles Factor;3;0;Create;True;0;0;0;False;0;False;0.51;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;79;-997.4961,420.4991;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;40;-1124.33,283.525;Inherit;False;True;False;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;73;-1213.259,811.2847;Inherit;False;Property;_CameraFadeDistance;Camera Fade Distance;4;0;Create;True;0;0;0;False;0;False;10;0;0;20;0;1;FLOAT;0
Node;AmplifyShaderEditor.DistanceOpNode;70;-1149.763,647.8361;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;3;-1052.327,-114.0568;Inherit;True;Property;_Texture;Texture;0;0;Create;True;0;0;0;False;0;False;-1;None;e38eadc65b184624386851d849ef5164;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;55;-775.563,399.9464;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;63;-899.7346,-302.3693;Inherit;False;Property;_FogColor;Fog Color;1;0;Create;True;0;0;0;False;0;False;1,1,1,0;1,1,1,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;75;-904.9777,774.8889;Inherit;False;Property;_CameraFadeSharpness;Camera Fade Sharpness;5;0;Create;True;0;0;0;False;0;False;1;0;1;15;0;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;8;-922.3686,110.7819;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleDivideOpNode;72;-912.3898,649.5117;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;74;-618.431,647.2009;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;56;-613.5858,400.89;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;49;-621.3314,149.8613;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;76;-428.7731,538.8506;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;51;-442.4584,376.6159;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;65;-907.9571,-391.8967;Inherit;False;Property;_FogGlow;Fog Glow;2;0;Create;True;0;0;0;False;0;False;0;0;0;5;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;38;-629.2167,-80.99727;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;71;-260.398,376.3885;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;67;-609.5992,-322.5733;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.StaticSwitch;77;-386.4276,157.2063;Inherit;False;Property;_SoftParticles;Soft Particles;6;0;Create;True;0;0;0;False;0;False;0;1;1;True;;Toggle;2;Key0;Key1;Create;True;True;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;78;-129.6233,157.7454;Inherit;False;Property;_CameraFade;Camera Fade;7;0;Create;True;0;0;0;False;0;False;0;1;1;True;;Toggle;2;Key0;Key1;Create;True;True;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;64;-447.2647,-176.4904;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;114.545,-51.88639;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;Archanor VFX/Real Fog/UnlitFog;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Off;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Transparent;0.5;True;False;0;False;Transparent;;Transparent;ForwardOnly;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;False;2;5;False;-1;10;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;79;0;54;0
WireConnection;79;1;81;2
WireConnection;79;2;81;1
WireConnection;79;3;81;1
WireConnection;79;4;81;2
WireConnection;70;0;69;0
WireConnection;70;1;68;0
WireConnection;55;0;40;0
WireConnection;55;1;79;0
WireConnection;72;0;70;0
WireConnection;72;1;73;0
WireConnection;74;0;72;0
WireConnection;74;1;75;0
WireConnection;56;0;55;0
WireConnection;49;0;3;4
WireConnection;49;1;8;4
WireConnection;49;2;63;4
WireConnection;76;0;74;0
WireConnection;51;0;49;0
WireConnection;51;1;56;0
WireConnection;38;0;3;0
WireConnection;38;1;8;0
WireConnection;71;0;51;0
WireConnection;71;1;76;0
WireConnection;67;0;65;0
WireConnection;67;1;63;0
WireConnection;77;1;49;0
WireConnection;77;0;51;0
WireConnection;78;1;77;0
WireConnection;78;0;71;0
WireConnection;64;0;67;0
WireConnection;64;1;38;0
WireConnection;0;2;64;0
WireConnection;0;9;78;0
ASEEND*/
//CHKSM=94C17C7057C371B98B19E9D14F0BF6A8DD2C22FF