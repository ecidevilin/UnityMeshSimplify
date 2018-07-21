// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Shader created with Shader Forge v1.04 
// Shader Forge (c) Neat Corporation / Joachim Holmer - http://www.acegikmo.com/shaderforge/
// Note: Manually altering this data may prevent you from opening it in Shader Forge
/*SF_DATA;ver:1.04;sub:START;pass:START;ps:flbk:Bumped Specular,lico:1,lgpr:1,nrmq:1,limd:1,uamb:True,mssp:True,lmpd:False,lprd:False,rprd:False,enco:False,frtr:True,vitr:True,dbil:False,rmgx:True,rpth:0,hqsc:True,hqlp:False,tesm:0,blpr:0,bsrc:0,bdst:1,culm:0,dpts:2,wrdp:True,dith:2,ufog:True,aust:True,igpj:False,qofs:0,qpre:1,rntp:1,fgom:False,fgoc:False,fgod:False,fgor:False,fgmd:0,fgcr:0.1911765,fgcg:0.1911765,fgcb:0.1911765,fgca:1,fgde:0.3,fgrn:0,fgrf:300,ofsf:0,ofsu:0,f2p0:False;n:type:ShaderForge.SFN_Final,id:4233,x:32828,y:32612,varname:node_4233,prsc:2|diff-5517-RGB,spec-1177-RGB,gloss-5809-OUT,normal-8655-RGB,emission-1008-OUT,lwrap-1728-RGB;n:type:ShaderForge.SFN_Tex2d,id:5517,x:32501,y:32674,ptovrint:False,ptlb:diffusemap,ptin:_diffusemap,varname:node_5517,prsc:2,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Tex2d,id:8655,x:32459,y:32891,ptovrint:False,ptlb:normalmap,ptin:_normalmap,varname:node_8655,prsc:2,ntxv:3,isnm:True;n:type:ShaderForge.SFN_Tex2d,id:1177,x:32417,y:33129,ptovrint:False,ptlb:specmap,ptin:_specmap,varname:node_1177,prsc:2,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Tex2d,id:7662,x:32704,y:33150,ptovrint:False,ptlb:illumMask,ptin:_illumMask,varname:node_7662,prsc:2,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Color,id:1728,x:32917,y:33150,ptovrint:False,ptlb:subcolor,ptin:_subcolor,varname:node_1728,prsc:2,glob:False,c1:0.5,c2:0.5,c3:0.5,c4:1;n:type:ShaderForge.SFN_Slider,id:5809,x:33032,y:32925,ptovrint:False,ptlb:gloss,ptin:_gloss,varname:node_5809,prsc:2,min:0,cur:0,max:1;n:type:ShaderForge.SFN_Color,id:1317,x:33204,y:33200,ptovrint:False,ptlb:illumColor,ptin:_illumColor,varname:node_1317,prsc:2,glob:False,c1:0.5,c2:0.5,c3:0.5,c4:1;n:type:ShaderForge.SFN_Multiply,id:1008,x:33279,y:33059,varname:node_1008,prsc:2|A-1317-RGB,B-7662-RGB;proporder:5517-8655-1177-7662-1728-5809-1317;pass:END;sub:END;*/

Shader "custom/monsterShader" {
    Properties {
        _diffusemap ("diffusemap", 2D) = "white" {}
        _normalmap ("normalmap", 2D) = "bump" {}
        _specmap ("specmap", 2D) = "white" {}
        _illumMask ("illumMask", 2D) = "white" {}
        _subcolor ("subcolor", Color) = (0.5,0.5,0.5,1)
        _gloss ("gloss", Range(0, 1)) = 0
        _illumColor ("illumColor", Color) = (0.5,0.5,0.5,1)
    }
    SubShader {
        Tags {
            "RenderType"="Opaque"
        }
        Pass {
            Name "ForwardBase"
            Tags {
                "LightMode"="ForwardBase"
            }
            
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define UNITY_PASS_FORWARDBASE
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #pragma multi_compile_fwdbase_fullshadows
            #pragma exclude_renderers xbox360 ps3 flash d3d11_9x 
            #pragma target 3.0
            uniform float4 _LightColor0;
            uniform sampler2D _diffusemap; uniform float4 _diffusemap_ST;
            uniform sampler2D _normalmap; uniform float4 _normalmap_ST;
            uniform sampler2D _specmap; uniform float4 _specmap_ST;
            uniform sampler2D _illumMask; uniform float4 _illumMask_ST;
            uniform float4 _subcolor;
            uniform float _gloss;
            uniform float4 _illumColor;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float4 posWorld : TEXCOORD1;
                float3 normalDir : TEXCOORD2;
                float3 tangentDir : TEXCOORD3;
                float3 binormalDir : TEXCOORD4;
                LIGHTING_COORDS(5,6)
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.normalDir = mul(unity_ObjectToWorld, float4(v.normal,0)).xyz;
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.binormalDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos(v.vertex);
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            fixed4 frag(VertexOutput i) : COLOR {
                i.normalDir = normalize(i.normalDir);
                float3x3 tangentTransform = float3x3( i.tangentDir, i.binormalDir, i.normalDir);
/////// Vectors:
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 _normalmap_var = UnpackNormal(tex2D(_normalmap,TRANSFORM_TEX(i.uv0, _normalmap)));
                float3 normalLocal = _normalmap_var.rgb;
                float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 lightColor = _LightColor0.rgb;
                float3 halfDirection = normalize(viewDirection+lightDirection);
////// Lighting:
                float attenuation = LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.xyz;
///////// Gloss:
                float gloss = _gloss;
                float specPow = exp2( gloss * 10.0+1.0);
////// Specular:
                float NdotL = max(0, dot( normalDirection, lightDirection ));
                float4 _specmap_var = tex2D(_specmap,TRANSFORM_TEX(i.uv0, _specmap));
                float3 specularColor = _specmap_var.rgb;
                float3 directSpecular = (floor(attenuation) * _LightColor0.xyz) * pow(max(0,dot(halfDirection,normalDirection)),specPow);
                float3 specular = directSpecular * specularColor;
/////// Diffuse:
                NdotL = dot( normalDirection, lightDirection );
                float3 w = _subcolor.rgb*0.5; // Light wrapping
                float3 NdotLWrap = NdotL * ( 1.0 - w );
                float3 forwardLight = max(float3(0.0,0.0,0.0), NdotLWrap + w );
                float3 indirectDiffuse = float3(0,0,0);
                float3 directDiffuse = forwardLight * attenColor;
                indirectDiffuse += UNITY_LIGHTMODEL_AMBIENT.rgb; // Ambient Light
                float4 _diffusemap_var = tex2D(_diffusemap,TRANSFORM_TEX(i.uv0, _diffusemap));
                float3 diffuse = (directDiffuse + indirectDiffuse) * _diffusemap_var.rgb;
////// Emissive:
                float4 _illumMask_var = tex2D(_illumMask,TRANSFORM_TEX(i.uv0, _illumMask));
                float3 emissive = (_illumColor.rgb*_illumMask_var.rgb);
/// Final Color:
                float3 finalColor = diffuse + specular + emissive;
                return fixed4(finalColor,1);
            }
            ENDCG
        }
        Pass {
            Name "ForwardAdd"
            Tags {
                "LightMode"="ForwardAdd"
            }
            Blend One One
            
            
            Fog { Color (0,0,0,0) }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define UNITY_PASS_FORWARDADD
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #pragma multi_compile_fwdadd_fullshadows
            #pragma exclude_renderers xbox360 ps3 flash d3d11_9x 
            #pragma target 3.0
            uniform float4 _LightColor0;
            uniform sampler2D _diffusemap; uniform float4 _diffusemap_ST;
            uniform sampler2D _normalmap; uniform float4 _normalmap_ST;
            uniform sampler2D _specmap; uniform float4 _specmap_ST;
            uniform sampler2D _illumMask; uniform float4 _illumMask_ST;
            uniform float4 _subcolor;
            uniform float _gloss;
            uniform float4 _illumColor;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float4 posWorld : TEXCOORD1;
                float3 normalDir : TEXCOORD2;
                float3 tangentDir : TEXCOORD3;
                float3 binormalDir : TEXCOORD4;
                LIGHTING_COORDS(5,6)
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.normalDir = mul(unity_ObjectToWorld, float4(v.normal,0)).xyz;
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.binormalDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos(v.vertex);
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            fixed4 frag(VertexOutput i) : COLOR {
                i.normalDir = normalize(i.normalDir);
                float3x3 tangentTransform = float3x3( i.tangentDir, i.binormalDir, i.normalDir);
/////// Vectors:
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 _normalmap_var = UnpackNormal(tex2D(_normalmap,TRANSFORM_TEX(i.uv0, _normalmap)));
                float3 normalLocal = _normalmap_var.rgb;
                float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals
                float3 lightDirection = normalize(lerp(_WorldSpaceLightPos0.xyz, _WorldSpaceLightPos0.xyz - i.posWorld.xyz,_WorldSpaceLightPos0.w));
                float3 lightColor = _LightColor0.rgb;
                float3 halfDirection = normalize(viewDirection+lightDirection);
////// Lighting:
                float attenuation = LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.xyz;
///////// Gloss:
                float gloss = _gloss;
                float specPow = exp2( gloss * 10.0+1.0);
////// Specular:
                float NdotL = max(0, dot( normalDirection, lightDirection ));
                float4 _specmap_var = tex2D(_specmap,TRANSFORM_TEX(i.uv0, _specmap));
                float3 specularColor = _specmap_var.rgb;
                float3 directSpecular = attenColor * pow(max(0,dot(halfDirection,normalDirection)),specPow);
                float3 specular = directSpecular * specularColor;
/////// Diffuse:
                NdotL = dot( normalDirection, lightDirection );
                float3 w = _subcolor.rgb*0.5; // Light wrapping
                float3 NdotLWrap = NdotL * ( 1.0 - w );
                float3 forwardLight = max(float3(0.0,0.0,0.0), NdotLWrap + w );
                float3 directDiffuse = forwardLight * attenColor;
                float4 _diffusemap_var = tex2D(_diffusemap,TRANSFORM_TEX(i.uv0, _diffusemap));
                float3 diffuse = directDiffuse * _diffusemap_var.rgb;
/// Final Color:
                float3 finalColor = diffuse + specular;
                return fixed4(finalColor * 1,0);
            }
            ENDCG
        }
    }
    FallBack "Bumped Specular"
    CustomEditor "ShaderForgeMaterialInspector"
}
