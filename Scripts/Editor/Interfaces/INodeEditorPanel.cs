using XNode;

namespace XNodeEditor
{
    public interface INodeEditorPanel
    {
        void Set(NodeGraphEditor editor, NodeGraph graph);
        void OnGUI();

        float CalculateWidth();
        bool IsVisible();
    }
}