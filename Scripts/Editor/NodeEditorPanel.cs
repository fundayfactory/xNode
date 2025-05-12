using UnityEditor;
using UnityEngine;
using XNode;

namespace XNodeEditor
{
    public abstract class NodeEditorPanel : INodeEditorPanel
    {
        public Rect Rect { get; set; }

        protected NodeGraphEditor editor;
        protected NodeGraph graph;
        protected bool defaultCollapsed;

        public virtual void Set(NodeGraphEditor editor, NodeGraph graph)
        {
            this.editor = editor;
            this.graph = graph;
        }

        public void OnGUI()
        {
            if (editor == null || graph == null)
                return;
            if (!IsVisible())
                return;

            GUI.color = new Color(0.2f, 0.2f, 0.2f, 1.0f);
            EditorGUILayout.BeginVertical(NodeEditorResources.styles.nodeBody, GUILayout.MaxWidth(Rect.width));
            GUI.color = Color.white;

            EditorGUILayout.BeginVertical();
            OnDrawHeader();
            EditorGUILayout.EndVertical();

            if (Event.current.type != EventType.Layout) {
                Rect rect = GUILayoutUtility.GetLastRect();

                if (GUI.Button(rect, string.Empty, GUIStyle.none))
                    SetCollapsed(!IsCollapsed());
            }

            if (!IsCollapsed())
            {
                if (IsValidToDrawBody())
                    OnDraw();
                else
                    OnDrawFailed();
            }
            else
            {
                OnDrawCollapsed();
            }

            EditorGUILayout.EndVertical();
        }

        public virtual float CalculateWidth() { return 300f; }
        public virtual bool IsVisible() { return true; }
        protected virtual bool IsValidToDrawBody() { return true; }

        protected abstract void OnDrawHeader();
        protected abstract void OnDraw();
        protected abstract void OnDrawFailed();

        protected virtual void OnDrawCollapsed() { }

        protected bool IsCollapsed() {
            return EditorPrefs.GetBool(GetType().Name, defaultCollapsed);
        }

        protected void SetCollapsed(bool isCollapsed) {
            EditorPrefs.SetBool(GetType().Name, isCollapsed);
        }
    }
}