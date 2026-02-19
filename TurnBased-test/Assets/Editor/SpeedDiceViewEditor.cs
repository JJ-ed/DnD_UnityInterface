using UnityEditor;
using UnityEngine;

// Custom inspector:
// Shows Min/Max speed as a single "[Min] - [Max]".
[CustomEditor(typeof(SpeedDiceView))]
public sealed class SpeedDiceViewEditor : Editor
{
    private SerializedProperty? _minSpeed;
    private SerializedProperty? _maxSpeed;

    private void OnEnable()
    {
        _minSpeed = serializedObject.FindProperty("minSpeed");
        _maxSpeed = serializedObject.FindProperty("maxSpeed");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw everything except the raw min/max fields; we'll render them together.
        DrawPropertiesExcluding(serializedObject, "m_Script", "minSpeed", "maxSpeed");

        if (_minSpeed != null && _maxSpeed != null)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Speed Range", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                // Display as: [Min] - [Max]
                EditorGUILayout.PropertyField(_minSpeed, GUIContent.none, GUILayout.MinWidth(40));
                GUILayout.Label("-", GUILayout.Width(12));
                EditorGUILayout.PropertyField(_maxSpeed, GUIContent.none, GUILayout.MinWidth(40));
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}

