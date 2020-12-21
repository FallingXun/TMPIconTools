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
    // Icon尺寸大小
    public const int ICON_SIZE = 32;
    // Icon缩放大小
    public const float DEFAULT_SCALE = 1.5f;
    // Icon坐标y方向偏移量
    public const float OFFSET_Y = 24f;

    // 分离文件路径表示
    private const string SPLIT = "Assets/";

    // 手动生成图文混排图集默认名字
    private const string TMPICON_CUSTOM_NAME = "Default";

    // 一键生成图文混排图集的默认名字
    private const string TMPICON_NAME = "TMPIcon";
    // 一键生成图文混排图集默认保存路径（用户修改设置）
    private static string m_SavePath = "Assets/TMPIcon/";
    // 一键生成图文混排图集默认原图路径（用户修改设置）
    private static string m_SourceTexPath = "Assets/Sprites/1/";


    // 图文混排图片和索引缓存
    private static Dictionary<int, string> m_IconDic = new Dictionary<int, string>();



    [MenuItem("TMPIconTools/一键生成通用图文混排图集TMPIcon")]
    private static void GenerateTMPIcon()
    {
        string sourceDirPath = Path.Combine(Directory.GetCurrentDirectory(), m_SourceTexPath);
        string texPath = GetTMPTexPath();

        // 将散图合并成整图
        GenerateTexture(sourceDirPath, texPath);

        // 编辑整图，切割成多个Sprite
        GenerateSprites(texPath);

        string assetPath = GetTMPAssetPath();
        var selectTex = AssetDatabase.LoadAssetAtPath<UnityTexture2D>(GetTMPTexPath());
        // 生成SpriteAsset字体资源
        GenerateSpriteAsset(assetPath, selectTex);

        var tmpAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(GetTMPAssetPath());
        // 设置图文混排资源为Emoji的fallback，方便直接使用（带来的问题是会打包带进去Resources）
        GenerateFallbackSpriteAssets(tmpAsset);
        Debug.Log("一键生成通用图文混排图集TMPIcon：" + GetTMPAssetPath());
    }

    #region 合并sprites
    [MenuItem("TMPIconTools/图文混排工具/合并文件夹下的所有图为整图")]
    public static void GenerateTextureEditor()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        string sourceDirPath = Path.Combine(Directory.GetCurrentDirectory(), path);
        int index = path.Replace("\\", "/").LastIndexOf('/');
        string texPath = path.Substring(0, index + 1) + TMPICON_CUSTOM_NAME + ".png";
        GenerateTexture(sourceDirPath, texPath);
        Debug.Log("合并结束，整图路径为：" + texPath);
    }

    public static void GenerateTexture(string sourceDirPath, string texPath)
    {
        string[] filePaths = Directory.GetFiles(sourceDirPath, "*.png", SearchOption.AllDirectories);
        // 设置图片为可读写，修改图片最大尺寸
        SetImporter(filePaths, false);
        CombineTexture(filePaths, texPath);
        // 关闭图片可读写，恢复图片默认尺寸
        SetImporter(filePaths, true);
    }

    /// <summary>
    /// 设置图片格式
    /// </summary>
    /// <param name="filePaths"></param>
    /// <param name="reset"></param>
    private static void SetImporter(string[] filePaths, bool reset)
    {
        if (filePaths == null || filePaths.Length <= 0)
        {
            return;
        }
        for (int i = 0; i < filePaths.Length; i++)
        {
            int start = filePaths[i].IndexOf(SPLIT);
            if (start < 0)
            {
                continue;
            }
            UnityTexture2D tex = AssetDatabase.LoadAssetAtPath<UnityTexture2D>(filePaths[i].Substring(start, filePaths[i].Length - start));
            if (tex == null)
            {
                continue;
            }
            var path = AssetDatabase.GetAssetPath(tex);
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

    /// <summary>
    /// 合并图片
    /// </summary>
    /// <param name="filePaths"></param>
    /// <param name="texPath"></param>
    private static void CombineTexture(string[] filePaths, string texPath)
    {
        if (filePaths == null || filePaths.Length <= 0)
        {
            return;
        }

        m_IconDic.Clear();
        // 自适应图集大小
        int sqrt = (int)(Mathf.Sqrt(filePaths.Length));
        if (filePaths.Length - sqrt * sqrt > 0)
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

        // 将散图写入整图中，从上往下，从左往右
        int widthOffset = 0;
        int heightOffset = size - ICON_SIZE;    //左下角为0，0
        for (int i = 0; i < filePaths.Length; i++)
        {
            int start = filePaths[i].IndexOf("Assets/");
            if (start < 0)
            {
                continue;
            }
            UnityTexture2D texture = AssetDatabase.LoadAssetAtPath<UnityTexture2D>(filePaths[i].Substring(start, filePaths[i].Length - start));
            if (texture == null)
            {
                continue;
            }
            m_IconDic[i] = texture.name;
            //var temp = CreateTemporaryDuplicate(texs[i], ICON_SIZE, ICON_SIZE, RenderTextureFormat.ARGB32);
            if (texture.isReadable == false)
            {
                Debug.LogError("isReadable == false", texture);
                continue;
            }

            var temp = texture;
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

        int index = texPath.Replace("\\", "/").LastIndexOf('/') + 1;
        string savePath = texPath.Substring(index, texPath.Length - index);
        var dirPath = Path.Combine(Directory.GetCurrentDirectory(), savePath);
        if (Directory.Exists(dirPath) == false)
        {
            Directory.CreateDirectory(dirPath);
        }
        File.WriteAllBytes(texPath, tex.EncodeToPNG());

        AssetDatabase.Refresh();

        // 修改Android和iOS的图片格式
        TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
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
        // 修改模式为Multiple，才能使用裁剪
        importer.spriteImportMode = SpriteImportMode.Multiple;
        AssetDatabase.ImportAsset(texPath);
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
    [MenuItem("TMPIconTools/图文混排工具/裁剪整图生成Sprites")]
    public static void GenerateSpritesEditor()
    {
        var path = AssetDatabase.GetAssetPath(Selection.activeObject);
        GenerateSprites(path);
        Debug.Log("裁剪完成：" + path);
    }

    private static void GenerateSprites(string path)
    {
        // 参照Editor源码编写，裁剪Sprite
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
            // 使用缓存的原图片名字，不使用索引
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
    [MenuItem("TMPIconTools/图文混排工具/生成字体SpriteAsset")]
    private static void GenerateSpriteAssetEditor()
    {
        UnityTexture2D tex = Selection.activeObject as UnityTexture2D;
        if (tex != null)
        {
            GenerateSpriteAsset(GetAssetPathWithSourceTex(tex), tex);
        }
        Debug.Log("生成字体结束：" + GetAssetPathWithSourceTex(tex));
    }

    private static void GenerateSpriteAsset(string assetPath, UnityTexture2D selectTex)
    {
        if (string.IsNullOrEmpty(assetPath) == true)
        {
            return;
        }
        if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), assetPath)))
        {
            AssetDatabase.DeleteAsset(assetPath);
        }

        if (selectTex == null)
        {
            return;
        }
        // 缓存当前选中的对象
        var temp = Selection.activeObject;

        // 强制设置选中对象，TMP工具里是用Selection.activeObject处理的
        Selection.activeObject = selectTex;
        TMPro.EditorUtilities.TMP_SpriteAssetMenu.CreateSpriteAsset();

        TMP_SpriteAsset spriteAsset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(TMP_SpriteAsset)) as TMP_SpriteAsset;
        if (spriteAsset != null)
        {
            var glyph = spriteAsset.spriteGlyphTable;
            for (int i = 0; i < glyph.Count; i++)
            {
                // 修改默认缩放大小
                glyph[i].scale = DEFAULT_SCALE;
                var metrics = glyph[i].metrics;
                // 修改y偏移量
                metrics.horizontalBearingY = OFFSET_Y;
                glyph[i].metrics = metrics;
            }
        }

        // 恢复选中对象
        Selection.activeObject = temp;
    }
    #endregion

    #region 设置fallback
    [MenuItem("TMPIconTools/图文混排工具/设置默认表情fallback")]
    private static void GenerateFallbackSpriteAssetsEditor()
    {
        TMP_SpriteAsset asset = Selection.activeObject as TMP_SpriteAsset;
        // 设置为默认的SpriteAssets资源的fallback，方便使用
        GenerateFallbackSpriteAssets(asset);
        Debug.Log(string.Format("设置 {0} 为 {1} 的fallback字体", asset.name, TMP_Settings.defaultSpriteAsset.name));
    }

    private static void GenerateFallbackSpriteAssets(TMP_SpriteAsset tmpAsset)
    {
        if (TMP_Settings.defaultSpriteAsset == null)
        {
            return;
        }
        if (tmpAsset != null)
        {
            var table = TMP_Settings.defaultSpriteAsset.fallbackSpriteAssets;
            if (table != null)
            {
                List<TMP_SpriteAsset> spriteAssets = new List<TMP_SpriteAsset>();
                for (int i = 0; i < table.Count; i++)
                {
                    if (table[i] != null)
                    {
                        spriteAssets.Add(table[i]);
                    }
                }
                spriteAssets.Add(tmpAsset);
                TMP_Settings.defaultSpriteAsset.fallbackSpriteAssets = spriteAssets;
            }
        }
        EditorUtility.SetDirty(TMP_Settings.defaultSpriteAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    #endregion

    #region 通用方法
    public static string GetTMPTexPath()
    {
        return string.Format("{0}{1}.png", m_SavePath, TMPICON_NAME);
    }

    public static string GetTMPAssetPath()
    {
        string assetPath = string.Empty;
        UnityTexture2D sourceTex = AssetDatabase.LoadAssetAtPath<UnityTexture2D>(GetTMPTexPath());
        if (sourceTex == null)
        {
            return assetPath;
        }
        assetPath = GetAssetPathWithSourceTex(sourceTex);
        return assetPath;
    }

    private static string GetAssetPathWithSourceTex(UnityTexture2D sourceTex)
    {
        if (sourceTex == null)
        {
            return string.Empty;
        }
        string filePathWithName = AssetDatabase.GetAssetPath(sourceTex);
        string fileNameWithExtension = Path.GetFileName(filePathWithName);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePathWithName);
        string filePath = filePathWithName.Replace(fileNameWithExtension, "");
        string assetPath = filePath + fileNameWithoutExtension + ".asset";
        return assetPath;
    }
    #endregion
}
