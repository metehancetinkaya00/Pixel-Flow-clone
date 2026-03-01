using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelLayout))]
public class LevelLayoutEditor : Editor
{
    private BlockColor paintColor = BlockColor.Pink;
    private bool isPainting;
    private int paintButton;

    private const float CellSize = 22f;
    private const float CellPadding = 2f;

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

        paintColor = (BlockColor)EditorGUILayout.EnumPopup("Paint Color", paintColor);

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
                Color drawColor = ToUnityColor(cellColor);

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

                if (cellColor == BlockColor.None)
                {
                    EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), new Color(0f, 0f, 0f, 0.15f));
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

    private Color ToUnityColor(BlockColor c)
    {
        if (c == BlockColor.Pink)
        {
            return new Color(1f, 0.2f, 0.8f, 1f);
        }

        if (c == BlockColor.Blue)
        {
            return new Color(0.2f, 0.6f, 1f, 1f);
        }

        if (c == BlockColor.Green)
        {
            return new Color(0.2f, 1f, 0.4f, 1f);
        }

        if (c == BlockColor.Orange)
        {
            return new Color(1f, 0.55f, 0.1f, 1f);
        }

        if (c == BlockColor.White)
        {
            return new Color(0.95f, 0.95f, 0.95f, 1f);
        }

        if (c == BlockColor.Black)
        {
            return new Color(0.15f, 0.15f, 0.15f, 1f);
        }

        return new Color(0.1f, 0.1f, 0.1f, 0.1f);
    }
}