using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using UnityTexture2D = UnityEngine.Texture2D;
using UnityEditor.Experimental.AssetImporters;
using UnityEditorInternal;
using System.IO;
using TMPro;

public class TMPIconTools : Editor
{
    public const int ICON_SIZE = 32;
    public const float DEFAULT_SCALE = 1.5f;
    private const string TMPICON_NAME = "TMPIcon";
    private static string m_SavePath = "Assets/TMPIcon/";
    private static Dictionary<int, string> m_IconDic = new Dictionary<int, string>();

    [MenuItem("TMPIconTools/生成TMPIcon")]
    private static void GenerateTMPIcon()
    {
        SetImporter(false);
        CombineTexture();
        SetImporter(true);
        var path = string.Format("{0}{1}.png", m_SavePath, TMPICON_NAME);
        var spriteRects = DoGridSlicing(path, new Vector2(ICON_SIZE, ICON_SIZE), Vector2.zero, Vector2.zero, (int)SpriteAlignment.TopLeft, new Vector2(0f, 1f));
        Apply(path, spriteRects);
    }

    #region 合并sprites
    [MenuItem("TMPIconTools/合并为整图")]
    public static void GenerateTexture()
    {
        SetImporter(false);
        CombineTexture();
        SetImporter(true);
    }

    private static void SetImporter(bool reset)
    {
        UnityTexture2D[] texs = Selection.GetFiltered<UnityTexture2D>(SelectionMode.DeepAssets);
        if (texs == null || texs.Length < 1)
        {
            return;
        }
        for (int i = 0; i < texs.Length; i++)
        {
            var path = AssetDatabase.GetAssetPath(texs[i]);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (reset)
            {
                importer.isReadable = false;
                importer.maxTextureSize = 2048;
            }
            else
            {
                importer.isReadable = true;
                importer.maxTextureSize = ICON_SIZE;
            }
            AssetDatabase.ImportAsset(path);
        }
    }

    private static void CombineTexture()
    {
        UnityTexture2D[] texs = Selection.GetFiltered<UnityTexture2D>(SelectionMode.DeepAssets);
        if (texs == null || texs.Length < 1)
        {
            return;
        }
        m_IconDic.Clear();
        int sqrt = (int)(Mathf.Sqrt(texs.Length));
        if (texs.Length - sqrt * sqrt > 0)
        {
            ++sqrt;
        }
        int count = 1;
        while (count < sqrt)
        {
            count *= 2;
        }
        int size = count * ICON_SIZE;
        var tex = CreateTexture2D(size, size);
        int widthOffset = 0;
        int heightOffset = size - ICON_SIZE;    //左下角为0，0
        for (int i = 0; i < texs.Length; i++)
        {
            m_IconDic[i] = texs[i].name;
            //var temp = CreateTemporaryDuplicate(texs[i], ICON_SIZE, ICON_SIZE, RenderTextureFormat.ARGB32);
            if (texs[i].isReadable == false)
            {
                Debug.LogError("isReadable == false", texs[i]);
                continue;
            }

            var temp = texs[i];
            if (widthOffset + ICON_SIZE > size)
            {
                heightOffset -= ICON_SIZE;
                widthOffset = 0;
            }
            for (int w = 0; w < temp.width; w++)
            {
                for (int h = 0; h < temp.height; h++)
                {
                    Color color = temp.GetPixel(w, h);
                    tex.SetPixel(w + widthOffset, h + heightOffset, color);
                }
            }
            widthOffset += ICON_SIZE;
        }
        tex.Apply();

        var dirPath = Path.Combine(Directory.GetCurrentDirectory(), m_SavePath);
        if (Directory.Exists(dirPath) == false)
        {
            Directory.CreateDirectory(dirPath);
        }
        string path = string.Format("{0}{1}.png", m_SavePath, TMPICON_NAME);
        File.WriteAllBytes(path, tex.EncodeToPNG());

        AssetDatabase.Refresh();

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        TextureImporterPlatformSettings iphone = new TextureImporterPlatformSettings()
        {
            name = "iPhone",
            overridden = true,
            maxTextureSize = 2048,
            format = importer.DoesSourceTextureHaveAlpha() ? TextureImporterFormat.ASTC_RGBA_5x5 : TextureImporterFormat.ASTC_RGB_5x5,
        };
        importer.SetPlatformTextureSettings(iphone);
        TextureImporterPlatformSettings android = new TextureImporterPlatformSettings()
        {
            name = "Android",
            overridden = true,
            maxTextureSize = 2048,
            format = importer.DoesSourceTextureHaveAlpha() ? TextureImporterFormat.ETC2_RGBA8 : TextureImporterFormat.ETC_RGB4,
        };
        importer.SetPlatformTextureSettings(android);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        AssetDatabase.ImportAsset(path);
    }

    private static UnityTexture2D CreateTexture2D(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }
        Color color = new Color(0, 0, 0, 0);
        UnityTexture2D tex = new UnityTexture2D(width, height);
        for (int w = 0; w < width; w++)
        {
            for (int h = 0; h < height; h++)
            {
                tex.SetPixel(w, h, color);
            }
        }
        return tex;
    }
    #endregion

    #region 裁切sprite
    [MenuItem("TMPIconTools/生成Sprites")]
    public static void Slice()
    {
        var path = AssetDatabase.GetAssetPath(Selection.activeObject);
        var spriteRects = DoGridSlicing(path, new Vector2(ICON_SIZE, ICON_SIZE), Vector2.zero, Vector2.zero, (int)SpriteAlignment.TopLeft, new Vector2(0f, 1f));
        Apply(path, spriteRects);
    }

    public static List<SpriteRect> DoGridSlicing(string path, Vector2 size, Vector2 offset, Vector2 padding, int alignment, Vector2 pivot)
    {
        var textureToUse = GetTextureToSlice(path);
        Rect[] frames = InternalSpriteUtility.GenerateGridSpriteRectangles((UnityTexture2D)textureToUse, offset, size, padding);

        int index = 0;

        List<SpriteRect> spriteRects = new List<SpriteRect>();
        foreach (Rect rect in frames)
        {
            SpriteRect spriteRect = new SpriteRect();

            spriteRect.rect = rect;
            spriteRect.alignment = (SpriteAlignment)alignment;
            spriteRect.pivot = pivot;

            if (m_IconDic.ContainsKey(index))
            {
                spriteRect.name = m_IconDic[index];
            }
            else
            {
                spriteRect.name = "Error" + index.ToString();
                Debug.LogError("找不到对应索引图片的名称:" + index.ToString());
            }

            //spriteRect.originalName = spriteRect.name;
            spriteRect.border = Vector4.zero;

            spriteRects.Add(spriteRect);
            ++index;
        }

        return spriteRects;

    }

    private static UnityTexture2D GetTextureToSlice(string path)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        MethodInfo method = ti.GetType().GetMethod("GetSourceTextureInformation", BindingFlags.Instance | BindingFlags.NonPublic);
        var val = method.Invoke(ti, null);
        SourceTextureInformation info = val as SourceTextureInformation;
        int width = info.width;
        int height = info.height;

        UnityTexture2D tex = AssetDatabase.LoadAssetAtPath<UnityTexture2D>(path);
        // we want to slice based on the original texture slice. Upscale the imported texture
        var texture = CreateTemporaryDuplicate(tex, width, height);
        if (texture != null)
            texture.filterMode = tex.filterMode;
        return texture;
    }

    public static UnityTexture2D CreateTemporaryDuplicate(UnityTexture2D original, int width, int height)
    {
        if (!ShaderUtil.hardwareSupportsRectRenderTexture || !original)
            return null;

        RenderTexture save = RenderTexture.active;

        RenderTexture tmp = RenderTexture.GetTemporary(
            width,
            height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.sRGB);

        Graphics.Blit(original, tmp);

        RenderTexture.active = tmp;

        // If the user system doesn't support this texture size, force it to use mipmap
        bool forceUseMipMap = width >= SystemInfo.maxTextureSize || height >= SystemInfo.maxTextureSize;

        UnityTexture2D copy = new UnityTexture2D(width, height, TextureFormat.RGBA32, original.mipmapCount > 1 || forceUseMipMap);
        copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        copy.Apply();
        RenderTexture.ReleaseTemporary(tmp);

        copy.alphaIsTransparency = original.alphaIsTransparency;
        return copy;
    }

    public static void Apply(string path, List<SpriteRect> spriteRects)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        var so = new SerializedObject(ti);
        var spriteSheetSO = so.FindProperty("m_SpriteSheet.m_Sprites");
        spriteSheetSO.ClearArray();
        for (int i = 0; i < spriteRects.Count; i++)
        {
            spriteSheetSO.InsertArrayElementAtIndex(i);
            var sp = spriteSheetSO.GetArrayElementAtIndex(i);
            sp.FindPropertyRelative("m_Rect").rectValue = spriteRects[i].rect;
            sp.FindPropertyRelative("m_Name").stringValue = spriteRects[i].name;
            sp.FindPropertyRelative("m_Border").vector4Value = spriteRects[i].border;
            sp.FindPropertyRelative("m_Alignment").intValue = (int)spriteRects[i].alignment;
            sp.FindPropertyRelative("m_Pivot").vector2Value = spriteRects[i].pivot;
            sp.FindPropertyRelative("m_TessellationDetail").floatValue = 0;
            sp.FindPropertyRelative("m_SpriteID").stringValue = spriteRects[i].spriteID.ToString();
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        var originalValue = EditorPrefs.GetBool("VerifySavingAssets", false);
        EditorPrefs.SetBool("VerifySavingAssets", false);
        AssetDatabase.ForceReserializeAssets(new[] { path }, ForceReserializeAssetsOptions.ReserializeMetadata);
        EditorPrefs.SetBool("VerifySavingAssets", originalValue);

        try
        {
            AssetDatabase.StartAssetEditing();
            AssetDatabase.ImportAsset(path);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }

    #endregion

    #region 生成字体
    [MenuItem("TMPIconTools/生成字体SpriteAsset")]
    private static void CreateFont()
    {
        Object target = Selection.activeObject;

        if (target == null || target.GetType() != typeof(UnityTexture2D))
        {
            return;
        }

        CreateSpriteAsset(target as UnityTexture2D);
    }

    private static void CreateSpriteAsset(UnityTexture2D sourceTex)
    {
        if(sourceTex == null)
        {
            return;
        }
        string filePathWithName = AssetDatabase.GetAssetPath(sourceTex);
        string fileNameWithExtension = Path.GetFileName(filePathWithName);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePathWithName);
        string filePath = filePathWithName.Replace(fileNameWithExtension, "");
        string assetPath = filePath + fileNameWithoutExtension + ".asset";
        AssetDatabase.DeleteAsset(assetPath);

        TMPro.EditorUtilities.TMP_SpriteAssetMenu.CreateSpriteAsset();

        TMP_SpriteAsset spriteAsset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(TMP_SpriteAsset)) as TMP_SpriteAsset;
        if (spriteAsset != null)
        {
            var glyph = spriteAsset.spriteGlyphTable;
            for (int i = 0; i < glyph.Count; i++)
            {
                glyph[i].scale = DEFAULT_SCALE;
                var metrics = glyph[i].metrics;
                metrics.horizontalBearingY = 1.0f * ICON_SIZE / 4 * 3;
                glyph[i].metrics = metrics;
            }
        }
    }
    #endregion
}
