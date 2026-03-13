using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;

[CustomEditor(typeof(LevelLayout))]
public class LevelLayoutEditor : Editor
{
    private BlockColor paintColor = BlockColor.Pink;
    private bool isPainting;
    private int paintButton;

    private const float CellSize = 22f;
    private const float CellPadding = 2f;

    private static readonly string[] SearchFolders = new string[] { "Assets" };

    private readonly Dictionary<BlockColor, Material> materialCache = new Dictionary<BlockColor, Material>();
    private readonly Dictionary<BlockColor, Color> colorCache = new Dictionary<BlockColor, Color>();

    public override void OnInspectorGUI()
    {
        LevelLayout layout = (LevelLayout)target;

        EditorGUI.BeginChangeCheck();

        int newWidth = EditorGUILayout.IntField("Width", layout.width);
        int newHeight = EditorGUILayout.IntField("Height", layout.height);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(layout, "Resize LevelLayout");
            layout.Resize(newWidth, newHeight);
            EditorUtility.SetDirty(layout);
        }

        GUILayout.Space(8);

        DrawPalette();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Clear"))
        {
            Undo.RecordObject(layout, "Clear LevelLayout");
            layout.ClearAll();
            EditorUtility.SetDirty(layout);
        }

        if (GUILayout.Button("Fill"))
        {
            Undo.RecordObject(layout, "Fill LevelLayout");
            layout.FillAll(paintColor);
            EditorUtility.SetDirty(layout);
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        layout.EnsureCellsSize();

        DrawGrid(layout);

        if (GUI.changed)
        {
            EditorUtility.SetDirty(layout);
        }
    }

    private void DrawPalette()
    {
        Array values = Enum.GetValues(typeof(BlockColor));

        GUILayout.BeginVertical();

        int perRow = 8;
        int countInRow = 0;

        GUILayout.BeginHorizontal();

        for (int i = 0; i < values.Length; i++)
        {
            BlockColor c = (BlockColor)values.GetValue(i);

            if (c == BlockColor.None)
            {
                continue;
            }

            if (countInRow >= perRow)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                countInRow = 0;
            }

            Color col = GetDisplayColor(c);

            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = col;

            if (GUILayout.Button(c.ToString(), GUILayout.Height(22f)))
            {
                paintColor = c;
            }

            GUI.backgroundColor = prev;

            countInRow++;
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        paintColor = (BlockColor)EditorGUILayout.EnumPopup("Paint Color", paintColor);

        GUILayout.EndVertical();
    }

    private void DrawGrid(LevelLayout layout)
    {
        Event ev = Event.current;

        if (ev.type == EventType.MouseUp)
        {
            isPainting = false;
        }

        for (int y = layout.height - 1; y >= 0; y--)
        {
            GUILayout.BeginHorizontal();

            for (int x = 0; x < layout.width; x++)
            {
                Rect rect = GUILayoutUtility.GetRect(CellSize, CellSize, GUILayout.Width(CellSize), GUILayout.Height(CellSize));
                rect.x += CellPadding;
                rect.y += CellPadding;
                rect.width -= CellPadding * 2f;
                rect.height -= CellPadding * 2f;

                BlockColor cellColor = layout.Get(x, y);
                Color drawColor = GetDisplayColor(cellColor);

                EditorGUI.DrawRect(rect, drawColor);

                bool hovered = rect.Contains(ev.mousePosition);

                if (hovered && ev.type == EventType.MouseDown)
                {
                    isPainting = true;
                    paintButton = ev.button;

                    PaintCell(layout, x, y, paintButton);
                    ev.Use();
                }

                if (hovered && isPainting && ev.type == EventType.MouseDrag)
                {
                    PaintCell(layout, x, y, paintButton);
                    ev.Use();
                }

                Handles.color = new Color(0f, 0f, 0f, 0.25f);
                Handles.DrawAAPolyLine(
                    1f,
                    new Vector3(rect.x, rect.y),
                    new Vector3(rect.xMax, rect.y),
                    new Vector3(rect.xMax, rect.yMax),
                    new Vector3(rect.x, rect.yMax),
                    new Vector3(rect.x, rect.y)
                );
            }

            GUILayout.EndHorizontal();
        }
    }

    private void PaintCell(LevelLayout layout, int x, int y, int mouseButton)
    {
        Undo.RecordObject(layout, "Paint Cell");

        if (mouseButton == 1)
        {
            layout.Set(x, y, BlockColor.None);
            return;
        }

        layout.Set(x, y, paintColor);
    }

    private Color GetDisplayColor(BlockColor c)
    {
        if (colorCache.ContainsKey(c))
        {
            return colorCache[c];
        }

        Color result = GetColorFromMaterial(c);

        colorCache[c] = result;
        return result;
    }

    private Color GetColorFromMaterial(BlockColor c)
    {
        if (c == BlockColor.None)
        {
            return new Color(0f, 0f, 0f, 0.08f);
        }

        Material mat = GetMaterialFor(c);

        if (mat == null)
        {
            return GenerateFallbackColor(c);
        }

        if (mat.HasProperty("_BaseColor"))
        {
            return mat.GetColor("_BaseColor");
        }

        if (mat.HasProperty("_Color"))
        {
            return mat.GetColor("_Color");
        }

        return mat.color;
    }

    private Material GetMaterialFor(BlockColor c)
    {
        if (materialCache.ContainsKey(c))
        {
            return materialCache[c];
        }

        string enumName = c.ToString();

        Material found = FindMaterialByName("Mat_" + enumName);

        if (found == null)
        {
            found = FindMaterialByName(enumName);
        }

        materialCache[c] = found;
        return found;
    }

    private Material FindMaterialByName(string materialName)
    {
        string[] guids = AssetDatabase.FindAssets(materialName + " t:Material", SearchFolders);

        if (guids == null || guids.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                continue;
            }

            if (string.Equals(mat.name, materialName, StringComparison.Ordinal))
            {
                return mat;
            }
        }

        string firstPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<Material>(firstPath);
    }

    private Color GenerateFallbackColor(BlockColor c)
    {
        int i = (int)c;
        float h = Mathf.Repeat(i * 0.147f, 1f);
        return Color.HSVToRGB(h, 0.65f, 1f);
    }
}