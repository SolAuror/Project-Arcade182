using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sol.Minigames.EditorTools
{
    /// <summary>
    /// Regenerates the two in-repo bitmap fonts used by the Labyrinth Crawler
    /// HUD from hand-drawn glyph strings:
    ///
    ///  - F_LabUI_Pixel   : 5x7 caps-only body face (lowercase reuses the caps
    ///                      glyphs), design size 8 -> 1x/2x/3x at fontSize 8/16/24.
    ///  - F_LabUI_Display : heavier mixed-case display face with real lowercase,
    ///                      for banners and titles, design size 12.
    ///
    /// Each font is authored as a point-filtered atlas PNG + UI/Default material
    /// + .fontsettings written to fixed paths, so re-running preserves the asset
    /// GUIDs (and therefore every HUD reference that points at them). This script
    /// is the ONLY source of truth for the glyph shapes: edit a string below and
    /// re-run to reshape a letter.
    ///
    /// Menu: Sol/Author Labyrinth Fonts. Or run closed-editor:
    ///   Unity.exe -batchmode -quit -projectPath [project] -executeMethod
    ///   Sol.Minigames.EditorTools.LabyrinthFontBuilder.Build
    /// </summary>
    public static class LabyrinthFontBuilder
    {
        private const string UiDir = "Assets/0_Jd/Minigames/LabyrinthCrawler/UI";

        private const string BodyTexturePath = UiDir + "/T_LabUI_Font.png";
        private const string BodyMaterialPath = UiDir + "/M_LabUI_Font.mat";
        private const string BodyFontPath = UiDir + "/F_LabUI_Pixel.fontsettings";

        private const string DisplayTexturePath = UiDir + "/T_LabUI_FontDisplay.png";
        private const string DisplayMaterialPath = UiDir + "/M_LabUI_FontDisplay.mat";
        private const string DisplayFontPath = UiDir + "/F_LabUI_Display.fontsettings";

        private const int AtlasCols = 16;

        [MenuItem("Sol/Author Labyrinth Fonts")]
        public static void Build()
        {
            EnsureFolder();
            BuildBodyFont();
            BuildDisplayFont();
            AssetDatabase.SaveAssets();
            Debug.Log("LabyrinthFontBuilder: regenerated F_LabUI_Pixel + F_LabUI_Display.");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(UiDir))
            {
                AssetDatabase.CreateFolder("Assets/0_Jd/Minigames/LabyrinthCrawler", "UI");
            }
        }

        // ------------------------------------------------------------------
        // Body font: 5x7 glyphs plus one descent row, 8x10 padded atlas
        // cells. Caps-only; a-z share the A-Z glyph rects. Design size 8, so
        // Text.fontSize 8/16/24 renders at 1x/2x/3x on the 240-line grid.
        // ------------------------------------------------------------------

        private const int BodyGlyphW = 5;
        private const int BodyGlyphRows = 8;
        private const int BodyCellW = 8;
        private const int BodyCellH = 10;

        private static readonly Dictionary<char, string> BodyGlyphs = new Dictionary<char, string>
        {
            ['A'] = ".###./#...#/#...#/#####/#...#/#...#/#...#/.....",
            ['B'] = "####./#...#/#...#/####./#...#/#...#/####./.....",
            ['C'] = ".####/#..../#..../#..../#..../#..../.####/.....",
            ['D'] = "####./#...#/#...#/#...#/#...#/#...#/####./.....",
            ['E'] = "#####/#..../####./#..../#..../#..../#####/.....",
            ['F'] = "#####/#..../####./#..../#..../#..../#..../.....",
            ['G'] = ".####/#..../#..../#.###/#...#/#...#/.###./.....",
            ['H'] = "#...#/#...#/#####/#...#/#...#/#...#/#...#/.....",
            ['I'] = "#####/..#../..#../..#../..#../..#../#####/.....",
            ['J'] = "..###/...#./...#./...#./...#./#..#./.##../.....",
            ['K'] = "#...#/#..#./#.#../##.../#.#../#..#./#...#/.....",
            ['L'] = "#..../#..../#..../#..../#..../#..../#####/.....",
            ['M'] = "#...#/##.##/#.#.#/#.#.#/#...#/#...#/#...#/.....",
            ['N'] = "#...#/##..#/#.#.#/#..##/#...#/#...#/#...#/.....",
            ['O'] = ".###./#...#/#...#/#...#/#...#/#...#/.###./.....",
            ['P'] = "####./#...#/#...#/####./#..../#..../#..../.....",
            ['Q'] = ".###./#...#/#...#/#...#/#.#.#/#..#./.##.#/.....",
            ['R'] = "####./#...#/#...#/####./#.#../#..#./#...#/.....",
            ['S'] = ".####/#..../#..../.###./....#/....#/####./.....",
            ['T'] = "#####/..#../..#../..#../..#../..#../..#../.....",
            ['U'] = "#...#/#...#/#...#/#...#/#...#/#...#/.###./.....",
            ['V'] = "#...#/#...#/#...#/#...#/#...#/.#.#./..#../.....",
            ['W'] = "#...#/#...#/#...#/#.#.#/#.#.#/##.##/#...#/.....",
            ['X'] = "#...#/#...#/.#.#./..#../.#.#./#...#/#...#/.....",
            ['Y'] = "#...#/#...#/.#.#./..#../..#../..#../..#../.....",
            ['Z'] = "#####/....#/...#./..#../.#.../#..../#####/.....",
            ['0'] = ".###./#...#/#..##/#.#.#/##..#/#...#/.###./.....",
            ['1'] = "..#../.##../..#../..#../..#../..#../#####/.....",
            ['2'] = ".###./#...#/....#/...#./..#../.#.../#####/.....",
            ['3'] = ".###./#...#/....#/..##./....#/#...#/.###./.....",
            ['4'] = "...#./..##./.#.#./#..#./#####/...#./...#./.....",
            ['5'] = "#####/#..../####./....#/....#/#...#/.###./.....",
            ['6'] = ".###./#..../#..../####./#...#/#...#/.###./.....",
            ['7'] = "#####/....#/...#./..#../.#.../.#.../.#.../.....",
            ['8'] = ".###./#...#/#...#/.###./#...#/#...#/.###./.....",
            ['9'] = ".###./#...#/#...#/.####/....#/#...#/.###./.....",
            [' '] = "...../...../...../...../...../...../...../.....",
            ['.'] = "...../...../...../...../...../.##../.##../.....",
            [','] = "...../...../...../...../...../.##../.##../..#..",
            [':'] = "...../.##../.##../...../.##../.##../...../.....",
            [';'] = "...../.##../.##../...../.##../.##../..#../.....",
            ['!'] = "..#../..#../..#../..#../..#../...../..#../.....",
            ['?'] = ".###./#...#/....#/...#./..#../...../..#../.....",
            ['\''] = "..#../..#../...../...../...../...../...../.....",
            ['"'] = ".#.#./.#.#./...../...../...../...../...../.....",
            ['-'] = "...../...../...../.###./...../...../...../.....",
            ['+'] = "...../..#../..#../#####/..#../..#../...../.....",
            ['/'] = "....#/...#./...#./..#../.#.../.#.../#..../.....",
            ['%'] = "##..#/##..#/...#./..#../.#.../#..##/#..##/.....",
            ['('] = "...#./..#../..#../..#../..#../..#../...#./.....",
            [')'] = ".#.../..#../..#../..#../..#../..#../.#.../.....",
            ['['] = ".###./.#.../.#.../.#.../.#.../.#.../.###./.....",
            [']'] = ".###./...#./...#./...#./...#./...#./.###./.....",
            ['='] = "...../...../.###./...../.###./...../...../.....",
            ['<'] = "...#./..#../.#.../#..../.#.../..#../...#./.....",
            ['>'] = ".#.../..#../...#./....#/...#./..#../.#.../.....",
            ['*'] = "...../.#.#./..#../.###./..#../.#.#./...../.....",
            ['&'] = ".##../#..#./#.#../.#.../#.#.#/#..#./.##.#/.....",
            ['_'] = "...../...../...../...../...../...../#####/.....",
            ['#'] = ".#.#./#####/.#.#./.#.#./#####/.#.#./...../....."
        };

        private static void BuildBodyFont()
        {
            List<char> order = BodyGlyphs.Keys.OrderBy(c => c).ToList();
            int cellRows = Mathf.CeilToInt(order.Count / (float)AtlasCols);
            int texW = AtlasCols * BodyCellW;
            int texH = Mathf.NextPowerOfTwo(cellRows * BodyCellH);

            Texture2D tex = NewTex(texW, texH);
            var infos = new List<CharacterInfo>();
            Color32 white = new Color32(255, 255, 255, 255);

            for (int i = 0; i < order.Count; i++)
            {
                char ch = order[i];
                int ox = (i % AtlasCols) * BodyCellW + 1;
                int oyTop = (i / AtlasCols) * BodyCellH + 1;

                string[] rows = BodyGlyphs[ch].Split('/');
                if (rows.Length != BodyGlyphRows || rows.Any(r => r.Length != BodyGlyphW))
                {
                    throw new System.InvalidOperationException($"Body glyph '{ch}' is not {BodyGlyphW}x{BodyGlyphRows}.");
                }

                for (int r = 0; r < BodyGlyphRows; r++)
                {
                    for (int x = 0; x < BodyGlyphW; x++)
                    {
                        if (rows[r][x] == '#')
                        {
                            PxT(tex, ox + x, oyTop + r, white);
                        }
                    }
                }

                infos.Add(MakeBodyInfo(ch, ox, oyTop, texW, texH));
                if (ch >= 'A' && ch <= 'Z')
                {
                    infos.Add(MakeBodyInfo(char.ToLowerInvariant(ch), ox, oyTop, texW, texH));
                }
            }

            Texture2D atlas = SaveAtlas(tex, BodyTexturePath);
            Material material = LoadOrCreateFontMaterial(BodyMaterialPath, atlas);
            Font font = LoadOrCreateFont(BodyFontPath, "LabUI Pixel", material, infos.ToArray());
            ApplyMetrics(font, size: 8f, lineSpacing: 10f, ascent: 7f);
        }

        private static CharacterInfo MakeBodyInfo(char c, int ox, int oyTop, int texW, int texH)
        {
            float uMin = ox / (float)texW;
            float uMax = (ox + BodyGlyphW) / (float)texW;
            float vTop = (texH - oyTop) / (float)texH;
            float vBottom = (texH - oyTop - BodyGlyphRows) / (float)texH;

            return new CharacterInfo
            {
                index = c,
                minX = 0,
                maxX = BodyGlyphW,
                minY = -1,
                maxY = BodyGlyphRows - 1,
                advance = BodyGlyphW + 1,
                uvBottomLeft = new Vector2(uMin, vBottom),
                uvBottomRight = new Vector2(uMax, vBottom),
                uvTopLeft = new Vector2(uMin, vTop),
                uvTopRight = new Vector2(uMax, vTop)
            };
        }

        // ------------------------------------------------------------------
        // Display font: heavy blackletter-flavoured face for banners, card
        // titles and the death screen. Proportional widths, real lowercase,
        // cap height 11 + 3 descent rows in a 14-row box, design size 12
        // (use fontSize 12/24/36 for 1x/2x/3x).
        // ------------------------------------------------------------------

        private const int DisplayRows = 14;
        private const int DisplayCellW = 12;
        private const int DisplayCellH = 16;

        private static readonly Dictionary<char, string> DisplayGlyphs = new Dictionary<char, string>
        {
            ['A'] = "...##.../..####../.##..##./.##..##./##....##/##....##/########/##....##/##....##/##....##/###..###/......../......../........",
            ['B'] = "######../.##..##./.##..##./.##..##./.#####../.##..##./.##...##/.##...##/.##...##/.##..##./######../......../......../........",
            ['C'] = "..#####./.##...##/##....../##....../##....../##....../##....../##....../##....##/.##..##./..####../......../......../........",
            ['D'] = "######../.##..##./.##...##/.##...##/.##...##/.##...##/.##...##/.##...##/.##...##/.##..##./######../......../......../........",
            ['E'] = "########/.##....#/.##...../.##..#../.######./.##..#../.##...../.##...../.##....#/.##....#/########/......../......../........",
            ['F'] = "########/.##....#/.##...../.##..#../.######./.##..#../.##...../.##...../.##...../.##...../####..../......../......../........",
            ['G'] = "..#####./.##...##/##....../##....../##....../##..####/##....##/##....##/##....##/.##..###/..###.##/......../......../........",
            ['H'] = "###..###/.##..##./.##..##./.##..##./.######./.##..##./.##..##./.##..##./.##..##./.##..##./###..###/......../......../........",
            ['I'] = "####/.##./.##./.##./.##./.##./.##./.##./.##./.##./####/..../..../....",
            ['J'] = "..#####/....##./....##./....##./....##./....##./....##./....##./##..##./##..##./.####../......./......./.......",
            ['K'] = "###..###/.##..##./.##.##../.####.../.###..../.####.../.##.##../.##..##./.##..##./.##...##/###...##/......../......../........",
            ['L'] = "####..../.##...../.##...../.##...../.##...../.##...../.##...../.##...../.##....#/.##....#/########/......../......../........",
            ['M'] = "###....###/.##....##./.###..###./.########./.##.##.##./.##.##.##./.##....##./.##....##./.##....##./.##....##./###....###/........../........../..........",
            ['N'] = "###...###/.##...##./.###..##./.####.##./.##.####./.##..###./.##...##./.##...##./.##...##./.##...##./###...###/........./........./.........",
            ['O'] = "..####../.##..##./##....##/##....##/##....##/##....##/##....##/##....##/##....##/.##..##./..####../......../......../........",
            ['P'] = "######../.##..##./.##...##/.##...##/.##..##./.#####../.##...../.##...../.##...../.##...../####..../......../......../........",
            ['Q'] = "..####../.##..##./##....##/##....##/##....##/##....##/##....##/##....##/##.##.##/.##..##./..######/......##/......../........",
            ['R'] = "######../.##..##./.##...##/.##...##/.##..##./.#####../.####.../.##.##../.##..##./.##..###/###...##/......../......../........",
            ['S'] = "..######/.##....#/##....../##....../.####.../..#####./.....###/......##/#.....##/##...##./.#####../......../......../........",
            ['T'] = "########/#..##..#/...##.../...##.../...##.../...##.../...##.../...##.../...##.../...##.../..####../......../......../........",
            ['U'] = "###..###/.##..##./.##..##./.##..##./.##..##./.##..##./.##..##./.##..##./.##..##./.##..##./..####../......../......../........",
            ['V'] = "###..###/.##..##./.##..##./.##..##./.##..##./.##..##./..####../..####../...##.../...##.../...##.../......../......../........",
            ['W'] = "###....###/.##....##./.##....##./.##....##./.##....##./.##.##.##./.##.##.##./.##.##.##./.########./.###..###./.##....##./........../........../..........",
            ['X'] = "###..###/.##..##./.##..##./..####../...##.../...##.../...##.../..####../.##..##./.##..##./###..###/......../......../........",
            ['Y'] = "###..###/.##..##./.##..##./.##..##./..####../...##.../...##.../...##.../...##.../...##.../..####../......../......../........",
            ['Z'] = "########/##....##/.....##./....##../...##.../..##..../..##..../.##...../##....##/##....##/########/......../......../........",
            ['0'] = ".#####./##...##/##...##/##..###/##.#.##/###..##/##...##/##...##/##...##/##...##/.#####./......./......./.......",
            ['1'] = "..##../.###../####../..##../..##../..##../..##../..##../..##../..##../######/....../....../......",
            ['2'] = ".#####./##...##/##...##/.....##/....##./...##../..##.../.##..../##...../##...##/#######/......./......./.......",
            ['3'] = ".#####./##...##/.....##/.....##/..####./.....##/.....##/.....##/##...##/##...##/.#####./......./......./.......",
            ['4'] = "....###./...####./..##.##./.##..##./##...##./########/.....##./.....##./.....##./.....##./....####/......../......../........",
            ['5'] = "#######/##...../##...../##...../######./.....##/.....##/.....##/##...##/##...##/.#####./......./......./.......",
            ['6'] = "..####./.##..##/##...../##...../######./##...##/##...##/##...##/##...##/.##..##/..####./......./......./.......",
            ['7'] = "#######/##...##/....##./....##./...##../...##../..##.../..##.../..##.../..##.../..##.../......./......./.......",
            ['8'] = ".#####./##...##/##...##/##...##/.#####./##...##/##...##/##...##/##...##/##...##/.#####./......./......./.......",
            ['9'] = ".####../##..##./##...##/##...##/##...##/.######/.....##/.....##/.....##/.##.##./.####../......./......./.......",
            ['a'] = "......./......./......./......./.#####./##...##/.....##/.######/##...##/##...##/.######/......./......./.......",
            ['b'] = "##...../##...../##...../##...../######./##...##/##...##/##...##/##...##/##...##/######./......./......./.......",
            ['c'] = "....../....../....../....../.#####/##...#/##..../##..../##..../##...#/.#####/....../....../......",
            ['d'] = ".....##/.....##/.....##/.....##/.######/##...##/##...##/##...##/##...##/##...##/.######/......./......./.......",
            ['e'] = "......./......./......./......./.#####./##...##/##...##/#######/##...../##...##/.#####./......./......./.......",
            ['f'] = "..###./.##.../.##.../#####./.##.../.##.../.##.../.##.../.##.../.##.../.##.../....../....../......",
            ['g'] = "......./......./......./......./.######/##...##/##...##/##...##/.######/.....##/.....##/.....##/##...##/.#####.",
            ['h'] = "##...../##...../##...../##...../######./##...##/##...##/##...##/##...##/##...##/##...##/......./......./.......",
            ['i'] = "..../.##./.##./..../.##./.##./.##./.##./.##./.##./####/..../..../....",
            ['j'] = "...../..##./..##./...../..##./..##./..##./..##./..##./..##./..##./..##./..##./###..",
            ['k'] = "##...../##...../##...../##...../##..##./##.##../####.../###..../##.##../##..##./##...##/......./......./.......",
            ['l'] = ".##./.##./.##./.##./.##./.##./.##./.##./.##./.##./####/..../..../....",
            ['m'] = "........../........../........../........../#########./##..##..##/##..##..##/##..##..##/##..##..##/##..##..##/##..##..##/........../........../..........",
            ['n'] = "......./......./......./......./######./##...##/##...##/##...##/##...##/##...##/##...##/......./......./.......",
            ['o'] = "......./......./......./......./.#####./##...##/##...##/##...##/##...##/##...##/.#####./......./......./.......",
            ['p'] = "......./......./......./......./######./##...##/##...##/##...##/##...##/######./##...../##...../##...../####...",
            ['q'] = "......./......./......./......./.######/##...##/##...##/##...##/##...##/.######/.....##/.....##/.....##/...####",
            ['r'] = "....../....../....../....../##.###/###.../##..../##..../##..../##..../####../....../....../......",
            ['s'] = "....../....../....../....../.####./##...#/###.../..###./...###/#...##/.####./....../....../......",
            ['t'] = "....../....../.##.../.##.../#####./.##.../.##.../.##.../.##.../.##..#/..###./....../....../......",
            ['u'] = "......./......./......./......./##...##/##...##/##...##/##...##/##...##/##...##/.######/......./......./.......",
            ['v'] = "......./......./......./......./##...##/##...##/##...##/.##.##./.##.##./..###../..##.../......./......./.......",
            ['w'] = "........../........../........../........../##..##..##/##..##..##/##..##..##/##..##..##/##..##..##/.########./.###..###./........../........../..........",
            ['x'] = "......./......./......./......./##...##/.##.##./..###../..###../.##.##./##...##/##...##/......./......./.......",
            ['y'] = "......./......./......./......./##...##/##...##/##...##/##...##/##...##/##...##/.######/.....##/##..##./.####..",
            ['z'] = "....../....../....../....../######/#...##/...##./..##../.##.../##...#/######/....../....../......",
            [' '] = "..../..../..../..../..../..../..../..../..../..../..../..../..../....",
            ['.'] = "../../../../../../../../../##/##/../../..",
            [','] = ".../.../.../.../.../.../.../.../.../.##/.##/.##/##./...",
            [':'] = "../../../../##/##/../../../##/##/../../..",
            ['!'] = "##/##/##/##/##/##/##/../../##/##/../../..",
            ['?'] = ".#####./##...##/.....##/....##./...##../...##../...##../......./......./...##../...##../......./......./.......",
            ['\''] = "##/##/##/../../../../../../../../../../..",
            ['-'] = "....../....../....../....../....../....../######/######/....../....../....../....../....../......",
            ['+'] = "......../......../......../...##.../...##.../########/########/...##.../...##.../......../......../......../......../........",
            ['/'] = "....##/....##/...##./...##./..##../..##../.##.../.##.../##..../##..../##..../....../....../......",
            ['['] = "####/##../##../##../##../##../##../##../##../##../####/..../..../....",
            [']'] = "####/..##/..##/..##/..##/..##/..##/..##/..##/..##/####/..../..../....",
            ['('] = "..##/.##./##../##../##../##../##../##../##../.##./..##/..../..../....",
            [')'] = "##../.##./..##/..##/..##/..##/..##/..##/..##/.##./##../..../..../....",
        };

        private static void BuildDisplayFont()
        {
            List<char> order = DisplayGlyphs.Keys.OrderBy(c => c).ToList();
            int cellRows = Mathf.CeilToInt(order.Count / (float)AtlasCols);
            int texW = AtlasCols * DisplayCellW;
            int texH = cellRows * DisplayCellH;

            Texture2D tex = NewTex(texW, texH);
            var infos = new List<CharacterInfo>();
            Color32 whitePx = new Color32(255, 255, 255, 255);

            for (int i = 0; i < order.Count; i++)
            {
                char ch = order[i];
                int ox = (i % AtlasCols) * DisplayCellW + 1;
                int oyTop = (i / AtlasCols) * DisplayCellH + 1;

                string[] rows = DisplayGlyphs[ch].Split('/');
                if (rows.Length != DisplayRows || rows.Any(r => r.Length != rows[0].Length))
                {
                    throw new System.InvalidOperationException(
                        $"Display glyph '{ch}' must be {DisplayRows} uniform rows (got {rows.Length}).");
                }

                int width = rows[0].Length;
                if (width < 2 || width > DisplayCellW - 2)
                {
                    throw new System.InvalidOperationException($"Display glyph '{ch}' width {width} out of range.");
                }

                for (int r = 0; r < DisplayRows; r++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (rows[r][x] == '#')
                        {
                            PxT(tex, ox + x, oyTop + r, whitePx);
                        }
                    }
                }

                float uMin = ox / (float)texW;
                float uMax = (ox + width) / (float)texW;
                float vTop = (texH - oyTop) / (float)texH;
                float vBottom = (texH - oyTop - DisplayRows) / (float)texH;

                infos.Add(new CharacterInfo
                {
                    index = ch,
                    minX = 0,
                    maxX = width,
                    minY = -3,
                    maxY = DisplayRows - 3,
                    advance = width + 1,
                    uvBottomLeft = new Vector2(uMin, vBottom),
                    uvBottomRight = new Vector2(uMax, vBottom),
                    uvTopLeft = new Vector2(uMin, vTop),
                    uvTopRight = new Vector2(uMax, vTop)
                });
            }

            Texture2D atlas = SaveAtlas(tex, DisplayTexturePath);
            Material material = LoadOrCreateFontMaterial(DisplayMaterialPath, atlas);
            Font font = LoadOrCreateFont(DisplayFontPath, "LabUI Display", material, infos.ToArray());
            ApplyMetrics(font, size: 12f, lineSpacing: 16f, ascent: 11f);
        }

        // ------------------------------------------------------------------
        // Shared asset plumbing
        // ------------------------------------------------------------------

        private static Material LoadOrCreateFontMaterial(string path, Texture atlas)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("UI/Default"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.mainTexture = atlas;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Font LoadOrCreateFont(string path, string fontName, Material material, CharacterInfo[] infos)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font == null)
            {
                font = new Font(fontName);
                AssetDatabase.CreateAsset(font, path);
            }

            font.material = material;
            font.characterInfo = infos;
            return font;
        }

        // Font metrics live on hidden serialized properties, so poke them
        // through SerializedObject rather than the (read-only) Font API.
        private static void ApplyMetrics(Font font, float size, float lineSpacing, float ascent)
        {
            SerializedObject serializedFont = new SerializedObject(font);
            SetFloatIfPresent(serializedFont, "m_FontSize", size);
            SetFloatIfPresent(serializedFont, "m_LineSpacing", lineSpacing);
            SetFloatIfPresent(serializedFont, "m_Ascent", ascent);
            serializedFont.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(font);
        }

        private static void SetFloatIfPresent(SerializedObject target, string property, float value)
        {
            SerializedProperty prop = target.FindProperty(property);
            if (prop != null)
            {
                prop.floatValue = value;
            }
        }

        // Writes the atlas PNG to a fixed path (GUID preserved on re-run) and
        // forces the point-filtered, uncompressed import a bitmap font needs.
        private static Texture2D SaveAtlas(Texture2D tex, string path)
        {
            tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D NewTex(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(new Color32[width * height]);
            return tex;
        }

        // Glyph strings read top-down; textures are bottom-up. Flip Y here.
        private static void PxT(Texture2D tex, int x, int yFromTop, Color32 color)
        {
            tex.SetPixel(x, tex.height - 1 - yFromTop, color);
        }
    }
}
