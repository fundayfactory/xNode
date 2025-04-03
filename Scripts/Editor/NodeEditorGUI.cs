using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XNode;
using XNodeEditor.Internal;
#if UNITY_2019_1_OR_NEWER && USE_ADVANCED_GENERIC_MENU
using GenericMenu = XNodeEditor.AdvancedGenericMenu;
#endif

namespace XNodeEditor {
    /// <summary> Contains GUI methods </summary>
    public partial class NodeEditorWindow {
        public NodeGraphEditor graphEditor;
        private List<UnityEngine.Object> selectionCache;
        private List<XNode.Node> culledNodes;
        /// <summary> 19 if docked, 22 if not </summary>
        private int topPadding { get { return isDocked() ? 19 : 22; } }
        private Rect prevToolbarRect;

        /// <summary> Executed after all other window GUI. Useful if Zoom is ruining your day. Automatically resets after being run.</summary>
        public event Action onLateGUI;

        protected virtual void OnGUI() {
            if (graph == null)
                return;

            Matrix4x4 m = GUI.matrix;
            ValidateGraphEditor();
            Controls();

            DrawGrid(position, zoom, panOffset);
            graphEditor.OnPreGUI();
            DrawConnections();
            DrawPostConnections();
            DrawDraggedConnection();
            DrawNodes();
            DrawSelectionBox();
            graphEditor.OnGUI();
            DrawTooltip();

            // Run and reset onLateGUI
            if (onLateGUI != null) {
                onLateGUI();
                onLateGUI = null;
            }

            PostControls();
            GUI.matrix = m;

            DrawToolbar();
            DrawPanels();
        }

        public static void BeginZoomed(Rect rect, float zoom, float topPadding) {
            GUI.EndClip();

            GUIUtility.ScaleAroundPivot(Vector2.one / zoom, rect.size * 0.5f);
            GUI.BeginClip(new Rect(-((rect.width * zoom) - rect.width) * 0.5f, -(((rect.height * zoom) - rect.height) * 0.5f) + (topPadding * zoom),
                rect.width * zoom,
                rect.height * zoom));
        }

        public static void EndZoomed(Rect rect, float zoom, float topPadding) {
            GUIUtility.ScaleAroundPivot(Vector2.one * zoom, rect.size * 0.5f);
            Vector3 offset = new Vector3(
                (((rect.width * zoom) - rect.width) * 0.5f),
                (((rect.height * zoom) - rect.height) * 0.5f) + (-topPadding * zoom) + topPadding + 4,
                0);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one);
        }

        public void DrawGrid(Rect rect, float zoom, Vector2 panOffset) {

            rect.position = Vector2.zero;

            Vector2 center = rect.size / 2f;
            Texture2D gridTex = graphEditor.GetGridTexture();
            Texture2D crossTex = graphEditor.GetSecondaryGridTexture();

            // Offset from origin in tile units
            float xOffset = -(center.x * zoom + panOffset.x) / gridTex.width;
            float yOffset = ((center.y - rect.size.y) * zoom + panOffset.y) / gridTex.height;

            Vector2 tileOffset = new Vector2(xOffset, yOffset);

            // Amount of tiles
            float tileAmountX = Mathf.Round(rect.size.x * zoom) / gridTex.width;
            float tileAmountY = Mathf.Round(rect.size.y * zoom) / gridTex.height;

            Vector2 tileAmount = new Vector2(tileAmountX, tileAmountY);

            // Draw tiled background
            GUI.DrawTextureWithTexCoords(rect, gridTex, new Rect(tileOffset, tileAmount));
            GUI.DrawTextureWithTexCoords(rect, crossTex, new Rect(tileOffset + new Vector2(0.5f, 0.5f), tileAmount));
        }

        public void DrawSelectionBox() {
            if (currentActivity == NodeActivity.DragGrid) {
                Vector2 curPos = WindowToGridPosition(Event.current.mousePosition);
                Vector2 size = curPos - dragBoxStart;
                Rect r = new Rect(dragBoxStart, size);
                r.position = GridToWindowPosition(r.position);
                r.size /= zoom;
                Handles.DrawSolidRectangleWithOutline(r, new Color(0, 0, 0, 0.1f), new Color(1, 1, 1, 0.6f));
            }
        }

        public static bool DropdownButton(string name, float width) {
            return GUILayout.Button(name, EditorStyles.toolbarDropDown, GUILayout.Width(width));
        }

        /// <summary> Show right-click context menu for hovered reroute </summary>
        void ShowRerouteContextMenu(RerouteReference reroute) {
            GenericMenu contextMenu = new GenericMenu();
            contextMenu.AddItem(new GUIContent("Remove"), false, () => reroute.RemovePoint());
            contextMenu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
            if (NodeEditorPreferences.GetSettings().autoSave) AssetDatabase.SaveAssets();
        }

        /// <summary> Show right-click context menu for hovered port </summary>
        void ShowPortContextMenu(XNode.NodePort hoveredPort) {
            GenericMenu contextMenu = new GenericMenu();
            foreach (var port in hoveredPort.GetConnections()) {
                var name = port.node.name;
                var index = hoveredPort.GetConnectionIndex(port);
                contextMenu.AddItem(new GUIContent(string.Format("Disconnect({0})", name)), false, () => hoveredPort.Disconnect(index));
            }
            contextMenu.AddItem(new GUIContent("Clear Connections"), false, () => hoveredPort.ClearConnections());
            //Get compatible nodes with this port
            if (NodeEditorPreferences.GetSettings().createFilter) {
                contextMenu.AddSeparator("");

                if (hoveredPort.direction == XNode.NodePort.IO.Input)
                    graphEditor.AddContextMenuItems(contextMenu, hoveredPort.ValueType, XNode.NodePort.IO.Output);
                else
                    graphEditor.AddContextMenuItems(contextMenu, hoveredPort.ValueType, XNode.NodePort.IO.Input);
            }
            contextMenu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
            if (NodeEditorPreferences.GetSettings().autoSave) AssetDatabase.SaveAssets();
        }

        /// <summary> Draw a bezier from output to input in grid coordinates </summary>
        public void DrawNoodle(NodePort output, NodePort input, Gradient gradient, INoodleDrawer drawer, NoodleStroke stroke, float thickness, List<Vector2> gridPoints) {
            // convert grid points to window points
            for (var i = 0; i < gridPoints.Count; ++i)
            {
                gridPoints[i] = GridToWindowPosition(gridPoints[i]);
            }

            Color originalHandlesColor = Handles.color;
            Handles.color = gradient.Evaluate(0f);
            drawer.DrawNoodle(graph, output, input, zoom, gradient, stroke, thickness, gridPoints);
            Handles.color = originalHandlesColor;
        }

        /// <summary> Draws all connections </summary>
        public void DrawConnections() {
            Vector2 mousePos = Event.current.mousePosition;
            List<RerouteReference> selection = preBoxSelectionReroute != null ? new List<RerouteReference>(preBoxSelectionReroute) : new List<RerouteReference>();
            hoveredReroute = new RerouteReference();

            List<Vector2> gridPoints = new List<Vector2>(2);

            Color col = GUI.color;
            foreach (XNode.Node node in graph.nodes) {
                //If a null node is found, return. This can happen if the nodes associated script is deleted. It is currently not possible in Unity to delete a null asset.
                if (node == null) continue;

                // Draw full connections and output > reroute
                foreach (XNode.NodePort output in node.Outputs) {
                    //Needs cleanup. Null checks are ugly
                    Rect fromRect;
                    if (!_portConnectionPoints.TryGetValue(output, out fromRect)) continue;

                    Color portColor = graphEditor.GetPortColor(output);
                    GUIStyle portStyle = graphEditor.GetPortStyle(output);

                    for (int k = 0; k < output.ConnectionCount; k++) {
                        XNode.NodePort input = output.GetConnection(k);

                        Gradient noodleGradient = graphEditor.GetNoodleGradient(output, input);
                        float noodleThickness = graphEditor.GetNoodleThickness(output, input);
                        INoodleDrawer noodleDrawer = graphEditor.GetNoodleDrawer(output, input);
                        NoodleStroke noodleStroke = graphEditor.GetNoodleStroke(output, input);

                        // Error handling
                        if (input == null) continue; //If a script has been updated and the port doesn't exist, it is removed and null is returned. If this happens, return.
                        if (!input.IsConnectedTo(output)) input.Connect(output);
                        Rect toRect;
                        if (!_portConnectionPoints.TryGetValue(input, out toRect)) continue;

                        List<Vector2> reroutePoints = output.GetReroutePoints(k);

                        gridPoints.Clear();
                        gridPoints.Add(fromRect.center);
                        gridPoints.AddRange(reroutePoints);
                        gridPoints.Add(toRect.center);
                        DrawNoodle(output, input, noodleGradient, noodleDrawer, noodleStroke, noodleThickness, gridPoints);

                        // Loop through reroute points again and draw the points
                        for (int i = 0; i < reroutePoints.Count; i++) {
                            RerouteReference rerouteRef = new RerouteReference(output, k, i);
                            // Draw reroute point at position
                            Rect rect = new Rect(reroutePoints[i], new Vector2(12, 12));
                            rect.position = new Vector2(rect.position.x - 6, rect.position.y - 6);
                            rect = GridToWindowRect(rect);

                            // Draw selected reroute points with an outline
                            if (selectedReroutes.Contains(rerouteRef)) {
                                GUI.color = NodeEditorPreferences.GetSettings().highlightColor;
                                GUI.DrawTexture(rect, portStyle.normal.background);
                            }

                            GUI.color = portColor;
                            GUI.DrawTexture(rect, portStyle.active.background);
                            if (rect.Overlaps(selectionBox)) selection.Add(rerouteRef);
                            if (rect.Contains(mousePos)) hoveredReroute = rerouteRef;

                        }
                    }
                }
            }
            GUI.color = col;
            if (Event.current.type != EventType.Layout && currentActivity == NodeActivity.DragGrid) selectedReroutes = selection;
        }

        private void DrawPostConnections()
        {
            Matrix4x4 m = GUI.matrix;
            GUI.EndClip();

            var scale = Vector2.one / zoom;
            var pivotPoint = position.size * 0.5f;

            var rectPos = new Vector2(
                -(position.width * zoom - position.width) * 0.5f,
                -((position.height * zoom - position.height) * 0.5f) + topPadding * zoom);
            var rectSize = new Vector2(
                position.width * zoom,
                position.height * zoom);

            GUIUtility.ScaleAroundPivot(scale, pivotPoint);
            GUI.BeginClip(new Rect(rectPos, rectSize));
            graphEditor.OnPostConnectionsGUI();
            GUI.EndClip();

            rectPos = new Vector2(0f, topPadding + 4);
            rectSize = new Vector2(position.width, position.height);

            GUI.matrix = m;
            GUI.BeginClip(new Rect(rectPos, rectSize));
        }

        private void DrawNodes() {
            Event e = Event.current;
            if (e.type == EventType.Layout) {
                selectionCache = new List<UnityEngine.Object>(Selection.objects);
            }

            System.Reflection.MethodInfo onValidate = null;
            if (Selection.activeObject != null && Selection.activeObject is XNode.Node) {
                onValidate = Selection.activeObject.GetType().GetMethod("OnValidate");
                if (onValidate != null) EditorGUI.BeginChangeCheck();
            }

            BeginZoomed(position, zoom, topPadding);

            Vector2 mousePos = Event.current.mousePosition;

            if (e.type != EventType.Layout) {
                hoveredNode = null;
                hoveredPort = null;
            }

            List<UnityEngine.Object> preSelection = preBoxSelection != null ? new List<UnityEngine.Object>(preBoxSelection) : new List<UnityEngine.Object>();

            // Selection box stuff
            Vector2 boxStartPos = GridToWindowPositionNoClipped(dragBoxStart);
            Vector2 boxSize = mousePos - boxStartPos;
            if (boxSize.x < 0) { boxStartPos.x += boxSize.x; boxSize.x = Mathf.Abs(boxSize.x); }
            if (boxSize.y < 0) { boxStartPos.y += boxSize.y; boxSize.y = Mathf.Abs(boxSize.y); }
            Rect selectionBox = new Rect(boxStartPos, boxSize);

            //Save guiColor so we can revert it
            Color guiColor = GUI.color;

            List<XNode.NodePort> removeEntries = new List<XNode.NodePort>();

            if (e.type == EventType.Layout) culledNodes = new List<XNode.Node>();
            for (int n = 0; n < graph.nodes.Count; n++) {
                // Skip null nodes. The user could be in the process of renaming scripts, so removing them at this point is not advisable.
                if (graph.nodes[n] == null) continue;
                if (n >= graph.nodes.Count) return;
                XNode.Node node = graph.nodes[n];

                // Culling
                if (e.type == EventType.Layout) {
                    // Cull unselected nodes outside view
                    if (!Selection.Contains(node) && ShouldBeCulled(node)) {
                        culledNodes.Add(node);
                        continue;
                    }
                } else if (culledNodes.Contains(node)) continue;

                if (e.type == EventType.Repaint) {
                    removeEntries.Clear();
                    foreach (var kvp in _portConnectionPoints)
                        if (kvp.Key.node == node) removeEntries.Add(kvp.Key);
                    foreach (var k in removeEntries) _portConnectionPoints.Remove(k);
                }

                NodeEditor nodeEditor = NodeEditor.GetEditor(node, this);

                NodeEditor.portPositions.Clear();

                // Set default label width. This is potentially overridden in OnBodyGUI
                EditorGUIUtility.labelWidth = 84;

                //Get node position
                Vector2 nodePos = GridToWindowPositionNoClipped(node.position);
                nodePos.y += 8f;

                GUILayout.BeginArea(new Rect(nodePos, new Vector2(nodeEditor.GetWidth(), 4000)));
                GUILayout.Space(8f);

                bool selected = selectionCache.Contains(graph.nodes[n]);

                if (selected) {
                    GUIStyle style = new GUIStyle(nodeEditor.GetBodyStyle());
                    GUIStyle highlightStyle = new GUIStyle(nodeEditor.GetBodyHighlightStyle());
                    highlightStyle.padding = style.padding;
                    style.padding = new RectOffset();
                    GUI.color = nodeEditor.GetTint();
                    GUILayout.BeginVertical(style);
                    GUI.color = NodeEditorPreferences.GetSettings().highlightColor;
                    GUILayout.BeginVertical(new GUIStyle(highlightStyle), GUILayout.MinHeight(64));
                } else {
                    GUIStyle style = new GUIStyle(nodeEditor.GetBodyStyle());
                    GUI.color = nodeEditor.GetTint();
                    GUILayout.BeginVertical(style, GUILayout.MinHeight(64));
                }

                GUI.color = guiColor;
                EditorGUI.BeginChangeCheck();

                //Draw node contents
                nodeEditor.OnHeaderGUI();
                nodeEditor.OnBodyGUI();

                GUILayout.EndVertical();
                nodeEditor.OnVerticalPortsGUI(GetNodeSize(node));

                //If user changed a value, notify other scripts through onUpdateNode
                if (EditorGUI.EndChangeCheck()) {
                    if (NodeEditor.onUpdateNode != null) NodeEditor.onUpdateNode(node);
                    EditorUtility.SetDirty(node);
                    nodeEditor.serializedObject.ApplyModifiedProperties();
                }

                //Cache data about the node for next frame
                if (e.type == EventType.Repaint) {
                    Vector2 size = GUILayoutUtility.GetLastRect().size;
                    SetNodeSize(node, size);

                    foreach (var kvp in NodeEditor.portPositions) {
                        Vector2 portHandlePos = kvp.Value;
                        portHandlePos += node.position;
                        Rect rect = new Rect(portHandlePos.x - 8, portHandlePos.y, 16, 16);
                        portConnectionPoints[kvp.Key] = rect;
                    }
                }

                if (selected) GUILayout.EndVertical();

                if (e.type != EventType.Layout) {
                    //Check if we are hovering this node
                    Vector2 nodeSize = GUILayoutUtility.GetLastRect().size;
                    Rect windowRect = new Rect(nodePos, nodeSize);
                    if (windowRect.Contains(mousePos)) hoveredNode = node;

                    //If dragging a selection box, add nodes inside to selection
                    if (currentActivity == NodeActivity.DragGrid) {
                        if (windowRect.Overlaps(selectionBox)) preSelection.Add(node);
                    }

                    //Check if we are hovering any of this nodes ports
                    //Check input ports
                    foreach (XNode.NodePort input in node.Inputs) {
                        //Check if port rect is available
                        if (!portConnectionPoints.ContainsKey(input)) continue;
                        Rect r = GridToWindowRectNoClipped(portConnectionPoints[input]);
                        if (r.Contains(mousePos)) hoveredPort = input;
                    }
                    //Check all output ports
                    foreach (XNode.NodePort output in node.Outputs) {
                        //Check if port rect is available
                        if (!portConnectionPoints.ContainsKey(output)) continue;
                        Rect r = GridToWindowRectNoClipped(portConnectionPoints[output]);
                        if (r.Contains(mousePos)) hoveredPort = output;
                    }
                }

                GUILayout.EndArea();
            }

            if (e.type != EventType.Layout && currentActivity == NodeActivity.DragGrid) Selection.objects = preSelection.ToArray();
            EndZoomed(position, zoom, topPadding);

            //If a change in is detected in the selected node, call OnValidate method.
            //This is done through reflection because OnValidate is only relevant in editor,
            //and thus, the code should not be included in build.
            if (onValidate != null && EditorGUI.EndChangeCheck()) onValidate.Invoke(Selection.activeObject, null);
        }

        private bool ShouldBeCulled(XNode.Node node) {

            Vector2 nodePos = GridToWindowPositionNoClipped(node.position);
            if (nodePos.x / _zoom > position.width) return true; // Right
            else if (nodePos.y / _zoom > position.height) return true; // Bottom
            else if (TryGetNodeSize(node, out Vector2 size)) {
                if (nodePos.x + size.x < 0) return true; // Left
                else if (nodePos.y + size.y < 0) return true; // Top
            }
            return false;
        }

        private void DrawTooltip() {
            if (!NodeEditorPreferences.GetSettings().portTooltips || graphEditor == null)
                return;
            string tooltip = null;
            if (hoveredPort != null) {
                tooltip = graphEditor.GetPortTooltip(hoveredPort);
            } else if (hoveredNode != null && IsHoveringNode && IsHoveringTitle(hoveredNode)) {
                tooltip = NodeEditor.GetEditor(hoveredNode, this).GetHeaderTooltip();
            }
            if (string.IsNullOrEmpty(tooltip)) return;
            GUIContent content = new GUIContent(tooltip);
            Vector2 size = NodeEditorResources.styles.tooltip.CalcSize(content);
            size.x += 8;
            Rect rect = new Rect(Event.current.mousePosition - (size), size);
            EditorGUI.LabelField(rect, content, NodeEditorResources.styles.tooltip);
            Repaint();
        }

        private void DrawToolbar() {
            if (graphEditor == null)
                return;
            if (graphEditor.toolbarOptionsLeft.Count == 0 && graphEditor.toolbarOptionsRight.Count == 0)
                return;

            GUI.EndClip();
            GUI.BeginClip(new Rect(0f, topPadding, position.width, position.height));
            NodeEditorGUILayout.BeginToolbar(GetToolbarRect());

            for (var i = 0; i < graphEditor.toolbarOptionsLeft.Count; i++) {
                DrawToolbarOption(graphEditor.toolbarOptionsLeft[i]);
            }

            GUILayout.FlexibleSpace();

            for (var i = 0; i < graphEditor.toolbarOptionsRight.Count; i++) {
                DrawToolbarOption(graphEditor.toolbarOptionsRight[i]);
            }

            NodeEditorGUILayout.EndToolbar();
        }

        private Rect GetToolbarRect()
        {
            if (graphEditor.toolbarOptionsLeft.Count == 0 && graphEditor.toolbarOptionsRight.Count == 0)
                return Rect.zero;

            return new Rect(0f, 0f, position.width, 22f);
        }

        private void DrawToolbarOption(NodeGraphEditor.ToolbarOption option)
        {
            bool prevEnabled = GUI.enabled;
            GUI.enabled = prevEnabled && (option.active == null || option.active.Invoke());

            if (option.drawer != null && option.drawer.Invoke()) {
                try {
                    option.action?.Invoke();
                }
                catch (Exception e) {
                    Debug.LogException(e);
                }
            }

            GUI.enabled = prevEnabled;
        }

        private void DrawPanels() {
            if (graphEditor == null)
                return;

            float padding = topPadding + GetToolbarRect().height;

            GUI.EndClip();
            GUI.BeginClip(new Rect(0f, padding, position.width, position.height));

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            for (var i = 0; i < graphEditor.panelsLeft.Count; i++) {
                NodeGraphEditor.Panel panel = graphEditor.panelsLeft[i];
                panel.drawer.Set(graphEditor, graphEditor.target);

                if (!panel.drawer.IsVisible())
                    continue;

                GUILayout.BeginVertical(GUILayout.MaxWidth(panel.drawer.CalculateWidth()));
                panel.drawer.OnGUI();
                GUILayout.EndVertical();

                if (Event.current.type == EventType.Repaint)
                    panel.rect = GUILayoutUtility.GetLastRect();
            }

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();

            for (var i = 0; i < graphEditor.panelsRight.Count; i++) {
                NodeGraphEditor.Panel panel = graphEditor.panelsRight[i];
                panel.drawer.Set(graphEditor, graphEditor.target);

                if (!panel.drawer.IsVisible())
                    continue;

                GUILayout.BeginVertical(GUILayout.MaxWidth(panel.drawer.CalculateWidth()));
                panel.drawer.OnGUI();
                GUILayout.EndVertical();

                if (Event.current.type == EventType.Repaint)
                    panel.rect = GUILayoutUtility.GetLastRect();
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private Vector2 GetNodeSize(Node node) {
            return nodeSizes.TryGetValue(node, out Vector2 size) ? size : Vector2.zero;
        }

        private bool TryGetNodeSize(Node node, out Vector2 size) {
            return nodeSizes.TryGetValue(node, out size);
        }

        private void SetNodeSize(Node node, Vector2 size)
        {
            if (nodeSizes.ContainsKey(node)) nodeSizes[node] = size;
            else nodeSizes.Add(node, size);
        }

        public bool TryGetConnectionLabelPoint(NodePort outPort, NodePort inPort, out Vector2 point)
        {
            if (portConnectionLabelPoints.TryGetValue(outPort, out var connections))
                return connections.TryGetValue(inPort, out point);
            point = Vector2.zero;
            return false;
        }

        public void SetConnectionLabelPoint(NodePort outPort, NodePort inPort, Vector2 point)
        {
            if (!portConnectionLabelPoints.TryGetValue(outPort, out var connections))
            {
                connections = new Dictionary<NodePort, Vector2>();
                portConnectionLabelPoints.Add(outPort, connections);
            }

            connections[inPort] = point;
        }
    }
}
