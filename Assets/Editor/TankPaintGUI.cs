using UnityEngine;
using UnityEditor;

public class TankPaintGUI : ShaderGUI
{
    static bool paintExtras = false;
    static bool rustExtras  = false;

    public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
    {
        MaterialProperty baseMap       = FindProperty("_BaseMap",          props);
        MaterialProperty baseColor     = FindProperty("_BaseColor",        props);
        MaterialProperty bumpMap       = FindProperty("_BumpMap",          props);
        MaterialProperty bumpScale     = FindProperty("_BumpScale",        props);
        MaterialProperty metallicGloss = FindProperty("_MetallicGlossMap", props);
        MaterialProperty metallic      = FindProperty("_Metallic",         props);
        MaterialProperty smoothness    = FindProperty("_Smoothness",       props);
        MaterialProperty parallaxMap   = FindProperty("_ParallaxMap",      props);
        MaterialProperty parallax      = FindProperty("_Parallax",         props);

        MaterialProperty rustTex         = FindProperty("_RustTex",           props);
        MaterialProperty rustMask        = FindProperty("_RustMask",          props);
        MaterialProperty rustBlend       = FindProperty("_RustBlendStrength", props);
        MaterialProperty rustNormal      = FindProperty("_RustNormalMap",     props);
        MaterialProperty rustNormalScale = FindProperty("_RustNormalScale",   props);
        MaterialProperty rustMetallicMap = FindProperty("_RustMetallicMap",   props);
        MaterialProperty rustMetallic    = FindProperty("_RustMetallic",      props);
        MaterialProperty rustSmoothness  = FindProperty("_RustSmoothness",    props);
        MaterialProperty rustHeight      = FindProperty("_RustHeightMap",     props);
        MaterialProperty rustParallax    = FindProperty("_RustParallax",      props);

        // ── PAINT ─────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Paint Layer", EditorStyles.boldLabel);
        editor.TexturePropertySingleLine(new GUIContent("Paint Albedo"), baseMap, baseColor);

        paintExtras = EditorGUILayout.Foldout(paintExtras, "Extra Maps (optional)", true);
        if (paintExtras)
        {
            EditorGUI.indentLevel++;
            editor.TexturePropertySingleLine(new GUIContent("Normal Map"), bumpMap, bumpScale);
            editor.TexturePropertySingleLine(new GUIContent("Metallic (R)  Smooth (A)"), metallicGloss);
            EditorGUI.indentLevel++;
            editor.ShaderProperty(metallic,   new GUIContent("Metallic"));
            editor.ShaderProperty(smoothness, new GUIContent("Smoothness"));
            EditorGUI.indentLevel--;
            editor.TexturePropertySingleLine(new GUIContent("Height Map"), parallaxMap, parallax);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);

        // ── RUST ──────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Rust Layer", EditorStyles.boldLabel);
        editor.TexturePropertySingleLine(
            new GUIContent("Rust Texture (RGBA)",
                "Your rust photo. RGB = color, A = rust flake shape.\n" +
                "Import settings: Alpha Source = Input Texture Alpha, Alpha Is Transparency = ON."),
            rustTex);
        editor.TexturePropertySingleLine(
            new GUIContent("Rust Mask",
                "B/W mask painted in Blender. White = show rust, black = show paint.\n" +
                "Keep material Tiling at 1,1."),
            rustMask);
        editor.ShaderProperty(rustBlend,
            new GUIContent("Rust Blend Strength",
                "Raise above 1 to amplify faint painted strokes."));

        rustExtras = EditorGUILayout.Foldout(rustExtras, "Extra Maps (optional)", true);
        if (rustExtras)
        {
            EditorGUI.indentLevel++;
            editor.TexturePropertySingleLine(new GUIContent("Normal Map"), rustNormal, rustNormalScale);
            editor.TexturePropertySingleLine(new GUIContent("Metallic (R)  Smooth (A)"), rustMetallicMap);
            EditorGUI.indentLevel++;
            editor.ShaderProperty(rustMetallic,   new GUIContent("Metallic"));
            editor.ShaderProperty(rustSmoothness, new GUIContent("Smoothness"));
            EditorGUI.indentLevel--;
            editor.TexturePropertySingleLine(new GUIContent("Height Map"), rustHeight, rustParallax);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(6);
        editor.RenderQueueField();
    }
}
