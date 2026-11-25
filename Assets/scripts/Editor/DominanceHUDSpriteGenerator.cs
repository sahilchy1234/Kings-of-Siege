using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Procedurally generates crisp, AAA-grade UI sprites for the top Battle Dominance HUD:
/// - Beveled dark metallic background frame (9-sliced)
/// - Glossy Royal Blue player fill
/// - Glossy Crimson Red enemy fill
/// - Glossy Golden Amber damage ghost lag fill
/// - Center Timer Pill badge (9-sliced)
/// </summary>
public static class DominanceHUDSpriteGenerator
{
    private const string Folder = "Assets/Sprites/DominanceHUD";

    [MenuItem("Tools/Battlefield/Generate Dominance HUD Sprites")]
    public static void GenerateSprites()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Sprites"))
            AssetDatabase.CreateFolder("Assets", "Sprites");

        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Sprites", "DominanceHUD");

        // 1. Dominance Frame Background (128 x 36)
        Texture2D frame = CreateFrameTexture(128, 36, 8,
            HexColor("#2B374A"), HexColor("#4E617E"), HexColor("#141B24"),
            HexColor("#0D1118"), HexColor("#151D28"), HexColor("#07090D"));
        SaveTexture(frame, $"{Folder}/Dominance_Frame_Bg.png");

        // 2. Player Fill - Royal Blue Gloss (128 x 28)
        Texture2D playerFill = CreateGlossyFillTexture(128, 28, 4,
            HexColor("#60A5FA"), HexColor("#2563EB"), HexColor("#1D4ED8"), HexColor("#1E3A8A"), HexColor("#93C5FD"), HexColor("#172554"));
        SaveTexture(playerFill, $"{Folder}/Dominance_Fill_Player.png");

        // 3. Enemy Fill - Crimson / Ruby Red Gloss (128 x 28)
        Texture2D enemyFill = CreateGlossyFillTexture(128, 28, 4,
            HexColor("#F87171"), HexColor("#DC2626"), HexColor("#B91C1C"), HexColor("#7F1D1D"), HexColor("#FCA5A5"), HexColor("#450A0A"));
        SaveTexture(enemyFill, $"{Folder}/Dominance_Fill_Enemy.png");

        // 4. Ghost Fill - Warm Amber / Gold Gloss (128 x 28)
        Texture2D ghostFill = CreateGlossyFillTexture(128, 28, 4,
            HexColor("#FDE047"), HexColor("#EAB308"), HexColor("#CA8A04"), HexColor("#854D0E"), HexColor("#FEF08A"), HexColor("#422006"));
        SaveTexture(ghostFill, $"{Folder}/Dominance_Fill_Ghost.png");

        // 5. Center Timer Pill Badge (128 x 42)
        Texture2D timerPill = CreateFrameTexture(128, 42, 12,
            HexColor("#D97706"), HexColor("#FDE68A"), HexColor("#78350F"),
            HexColor("#0F172A"), HexColor("#1E293B"), HexColor("#020617"));
        SaveTexture(timerPill, $"{Folder}/Dominance_Timer_Pill.png");

        AssetDatabase.Refresh();

        // Configure 9-slice borders on all textures
        ConfigureImporter($"{Folder}/Dominance_Frame_Bg.png", new Vector4(12, 12, 12, 12));
        ConfigureImporter($"{Folder}/Dominance_Fill_Player.png", new Vector4(6, 6, 6, 6));
        ConfigureImporter($"{Folder}/Dominance_Fill_Enemy.png", new Vector4(6, 6, 6, 6));
        ConfigureImporter($"{Folder}/Dominance_Fill_Ghost.png", new Vector4(6, 6, 6, 6));
        ConfigureImporter($"{Folder}/Dominance_Timer_Pill.png", new Vector4(16, 16, 16, 16));

        Debug.Log("<color=green>[DominanceHUD] Generated all 5 HUD sprites successfully in " + Folder + "</color>");
    }

    private static Texture2D CreateFrameTexture(int width, int height, int cornerRadius,
        Color borderColor, Color topHighlight, Color botShadow,
        Color bgTop, Color bgBot, Color innerShadow)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            float v = (float)y / (height - 1);
            for (int x = 0; x < width; x++)
            {
                float dx = 0;
                if (x < cornerRadius) dx = cornerRadius - x;
                else if (x >= width - cornerRadius) dx = x - (width - 1 - cornerRadius);

                float dy = 0;
                if (y < cornerRadius) dy = cornerRadius - y;
                else if (y >= height - cornerRadius) dy = y - (height - 1 - cornerRadius);

                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                Color col = Color.clear;
                if (dist <= cornerRadius + 0.5f)
                {
                    float alpha = Mathf.Clamp01(cornerRadius + 0.5f - dist);

                    // 2.5px border
                    if (dist > cornerRadius - 2.5f || x < 2 || x >= width - 2 || y < 2 || y >= height - 2)
                    {
                        Color bCol = borderColor;
                        if (y >= height - 3) bCol = Color.Lerp(borderColor, topHighlight, 0.75f);
                        else if (y <= 2) bCol = Color.Lerp(borderColor, botShadow, 0.75f);
                        col = bCol;
                    }
                    else if (y >= height - 5) // Inner top shadow
                    {
                        col = Color.Lerp(innerShadow, bgTop, (height - 1 - y) / 4f);
                    }
                    else
                    {
                        col = Color.Lerp(bgBot, bgTop, v);
                    }
                    col.a *= alpha;
                }
                pixels[y * width + x] = col;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateGlossyFillTexture(int width, int height, int cornerRadius,
        Color topSheen, Color midLight, Color midBase, Color botDark, Color edgeGlow, Color bottomEdge)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            float v = (float)y / (height - 1);
            for (int x = 0; x < width; x++)
            {
                float dx = 0;
                if (x < cornerRadius) dx = cornerRadius - x;
                else if (x >= width - cornerRadius) dx = x - (width - 1 - cornerRadius);

                float dy = 0;
                if (y < cornerRadius) dy = cornerRadius - y;
                else if (y >= height - cornerRadius) dy = y - (height - 1 - cornerRadius);

                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                Color col = Color.clear;
                if (dist <= cornerRadius + 0.5f)
                {
                    float alpha = Mathf.Clamp01(cornerRadius + 0.5f - dist);

                    if (y == height - 1)
                    {
                        col = edgeGlow; // 1px bright top specular highlight
                    }
                    else if (y == 0)
                    {
                        col = bottomEdge; // 1px dark bottom edge
                    }
                    else if (v >= 0.52f)
                    {
                        float t = (v - 0.52f) / 0.48f;
                        col = Color.Lerp(midLight, topSheen, Mathf.Pow(t, 0.75f));
                    }
                    else if (v >= 0.15f)
                    {
                        float t = (v - 0.15f) / 0.37f;
                        col = Color.Lerp(botDark, midBase, t);
                    }
                    else
                    {
                        float t = v / 0.15f;
                        col = Color.Lerp(bottomEdge, botDark, t);
                    }
                    col.a *= alpha;
                }
                pixels[y * width + x] = col;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static void SaveTexture(Texture2D tex, string path)
    {
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        Object.DestroyImmediate(tex);
    }

    private static void ConfigureImporter(string path, Vector4 border)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder = border;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.filterMode = FilterMode.Bilinear;
            imp.SaveAndReimport();
        }
    }

    private static Color HexColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color c)) return c;
        return Color.white;
    }
}
