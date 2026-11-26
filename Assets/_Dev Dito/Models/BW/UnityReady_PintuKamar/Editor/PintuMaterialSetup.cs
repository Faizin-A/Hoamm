using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Menu: Tools > Pintu > Setup Materials
// Membuat material Pintu_Kayu, Pintu_Logam, Pintu_LubangKunci lalu memasangnya ke semua FBX di folder Models.
public static class PintuMaterialSetup
{
    [MenuItem("Tools/Pintu/Setup Materials")]
    public static void Setup()
    {
        string root = FindRoot();
        if (root == null) { Debug.LogError("Folder root tidak ditemukan."); return; }

        string texDir = root + "/Textures";
        string matDir = root + "/Materials";
        string modelDir = root + "/Models";
        if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(root, "Materials");

        // 1) Pengaturan import texture
        SetTexture(texDir + "/Pintu_BaseColor.png", srgb: true, normal: false);
        SetTexture(texDir + "/Pintu_Normal.png", srgb: false, normal: true);
        SetTexture(texDir + "/Pintu_MetallicSmoothness.png", srgb: false, normal: false);

        var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/Pintu_BaseColor.png");
        var nrmTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/Pintu_Normal.png");
        var msTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "/Pintu_MetallicSmoothness.png");

        bool urp = GraphicsSettings.currentRenderPipeline != null &&
                   GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal");
        Shader shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
        if (shader == null) { Debug.LogError("Shader tidak ditemukan. Pipeline HDRP belum didukung script ini."); return; }

        // 2) Material
        var kayu = GetOrCreate(matDir + "/Pintu_Kayu.mat", shader);
        SetTex(kayu, urp ? "_BaseMap" : "_MainTex", baseTex);
        kayu.SetColor(urp ? "_BaseColor" : "_Color", Color.white);
        SetTex(kayu, "_BumpMap", nrmTex);
        kayu.SetFloat("_BumpScale", 1f);
        kayu.EnableKeyword("_NORMALMAP");
        SetTex(kayu, "_MetallicGlossMap", msTex);
        kayu.EnableKeyword("_METALLICSPECGLOSSMAP");
        kayu.SetFloat(urp ? "_Smoothness" : "_GlossMapScale", 1f);
        kayu.SetFloat("_Metallic", 1f);
        kayu.SetFloat("_SmoothnessTextureChannel", 0f); // smoothness dari alpha texture metallic
        EditorUtility.SetDirty(kayu);

        var logam = GetOrCreate(matDir + "/Pintu_Logam.mat", shader);
        SetSolid(logam, urp, new Color(0.36f, 0.25f, 0.09f).gamma, 1f, 0.55f);

        var kunci = GetOrCreate(matDir + "/Pintu_LubangKunci.mat", shader);
        SetSolid(kunci, urp, new Color(0.005f, 0.004f, 0.004f).gamma, 0f, 0.10f);

        AssetDatabase.SaveAssets();

        // 3) Pasang ke semua FBX
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { modelDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) continue;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Local);
            Remap(imp, "Pintu_Kayu", kayu);
            Remap(imp, "Pintu_Logam", logam);
            Remap(imp, "Pintu_LubangKunci", kunci);
            imp.SaveAndReimport();
            count++;
        }
        Debug.Log("Pintu material setup selesai. Model diproses: " + count + (urp ? " (URP)" : " (Built-in)"));
    }

    static string FindRoot()
    {
        foreach (string g in AssetDatabase.FindAssets("PintuMaterialSetup t:MonoScript"))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (!p.EndsWith("PintuMaterialSetup.cs")) continue;
            string editorDir = Path.GetDirectoryName(p).Replace("\\", "/");
            return Path.GetDirectoryName(editorDir).Replace("\\", "/");
        }
        return null;
    }

    static Material GetOrCreate(string path, Shader shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else m.shader = shader;
        return m;
    }

    static void SetTex(Material m, string prop, Texture t) { if (m.HasProperty(prop)) m.SetTexture(prop, t); }

    static void SetSolid(Material m, bool urp, Color c, float metallic, float smooth)
    {
        m.SetColor(urp ? "_BaseColor" : "_Color", c);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat(urp ? "_Smoothness" : "_Glossiness", smooth);
        EditorUtility.SetDirty(m);
    }

    static void SetTexture(string path, bool srgb, bool normal)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning("Texture tidak ada: " + path); return; }
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = srgb;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = false;
        ti.SaveAndReimport();
    }

    static void Remap(ModelImporter imp, string name, Material mat)
    {
        imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
    }
}
