using UnityEditor;
using UnityEngine;

/// <summary>
/// ★<see cref="ChargePhantomSet.PhantomAnim"/> 배열 원소를 'Element N' 대신 그 <c>name</c>으로 표시한다.
/// (어느 팬텀 애니인지 인스펙터에서 바로 구분 — 랜덤이 아니라 이름으로 찾아 하나하나 튜닝하기 위함.)
/// 같은 타입에 PropertyField를 다시 호출하면 무한재귀라, 자식 프로퍼티를 직접 순회해 그린다.
/// </summary>
[CustomPropertyDrawer(typeof(ChargePhantomSet.PhantomAnim))]
public class ChargePhantomAnimDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var nameProp = property.FindPropertyRelative("name");
        if (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
            label = new GUIContent(nameProp.stringValue);

        EditorGUI.BeginProperty(position, label, property);

        var foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var child = property.Copy();
            var end = property.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                float h = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), child, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
            }
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = EditorGUIUtility.singleLineHeight;
        if (property.isExpanded)
        {
            var child = property.Copy();
            var end = property.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                h += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
            }
        }
        return h;
    }
}
