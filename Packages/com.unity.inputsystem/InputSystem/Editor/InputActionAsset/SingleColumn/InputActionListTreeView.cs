#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace UnityEngine.Experimental.Input.Editor
{
    internal class InputActionListTreeView : InputActionTreeBase
    {
        private SerializedObject m_SerializedObject;

        public static InputActionListTreeView CreateFromSerializedObject(Action applyAction, SerializedObject serializedObject, ref TreeViewState treeViewState)
        {
            if (treeViewState == null)
                treeViewState = new TreeViewState();

            var treeView = new InputActionListTreeView(applyAction, treeViewState);
            treeView.m_SerializedObject = serializedObject;
            treeView.Reload();
            treeView.ExpandAll();
            return treeView;
        }

        static bool OnFoldoutDraw(Rect position, bool expandedState, GUIStyle style)
        {
            var indent = (int)(position.x / 15);
            position.x = 6 * indent + 8;
            return EditorGUI.Foldout(position, expandedState, GUIContent.none, style);
        }

        protected InputActionListTreeView(Action applyAction, TreeViewState state)
            : base(applyAction, state)
        {
            ////REVIEW: good enough like this for 2018.2?
            #if UNITY_2018_3_OR_NEWER
            foldoutOverride += OnFoldoutDraw;
            #endif
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem
            {
                id = 0,
                depth = -1
            };
            root.children = new List<TreeViewItem>();
            if (m_SerializedObject != null)
            {
                BuildFromSerializedObject(root);
            }
            return root;
        }

        private void BuildFromSerializedObject(TreeViewItem root)
        {
            m_SerializedObject.Update();
            var actionMapArrayProperty = m_SerializedObject.FindProperty("m_ActionMaps");
            for (var i = 0; i < actionMapArrayProperty.arraySize; i++)
            {
                var actionMapProperty = actionMapArrayProperty.GetArrayElementAtIndex(i);
                var actionMapItem = new ActionMapTreeItem(actionMapProperty, i);
                ParseActionMap(actionMapItem, actionMapProperty, 1);
                root.AddChild(actionMapItem);
            }
        }

        protected void ParseActionMap(TreeViewItem parentTreeItem, SerializedProperty actionMapProperty, int depth)
        {
            var actionsArrayProperty = actionMapProperty.FindPropertyRelative("m_Actions");
            for (var i = 0; i < actionsArrayProperty.arraySize; i++)
            {
                ParseAction(parentTreeItem, actionMapProperty, actionsArrayProperty, i, depth);
            }
        }

        private void ParseAction(TreeViewItem parentTreeItem, SerializedProperty actionMapProperty, SerializedProperty actionsArrayProperty, int index, int depth)
        {
            var bindingsArrayProperty = actionMapProperty.FindPropertyRelative("m_Bindings");
            var actionMapName = actionMapProperty.FindPropertyRelative("m_Name").stringValue;
            var actionProperty = actionsArrayProperty.GetArrayElementAtIndex(index);

            var actionItem = new ActionTreeItem(actionMapProperty, actionProperty, index);
            actionItem.depth = depth;
            var actionName = actionItem.actionName;

            ParseBindings(actionItem, actionMapName, actionName, bindingsArrayProperty, depth + 1);
            parentTreeItem.AddChild(actionItem);
        }

        protected void ParseBindings(TreeViewItem parent, string actionMapName, string actionName, SerializedProperty bindingsArrayProperty, int depth)
        {
            var bindingsCount = InputActionSerializationHelpers.GetBindingCount(bindingsArrayProperty, actionName);
            CompositeGroupTreeItem compositeGroupTreeItem = null;
            for (var j = 0; j < bindingsCount; j++)
            {
                var bindingProperty = InputActionSerializationHelpers.GetBinding(bindingsArrayProperty, actionName, j);
                var bindingsItem = new BindingTreeItem(actionMapName, bindingProperty, j);
                bindingsItem.depth = depth;
                if (bindingsItem.isComposite)
                {
                    compositeGroupTreeItem = new CompositeGroupTreeItem(actionMapName, bindingProperty, j);
                    compositeGroupTreeItem.depth = depth;
                    parent.AddChild(compositeGroupTreeItem);
                    continue;
                }
                if (bindingsItem.isPartOfComposite)
                {
                    var compositeItem = new CompositeTreeItem(actionMapName, bindingProperty, j);
                    compositeItem.depth = depth + 1;
                    if (compositeGroupTreeItem != null)
                        compositeGroupTreeItem.AddChild(compositeItem);
                    continue;
                }
                compositeGroupTreeItem = null;
                parent.AddChild(bindingsItem);
            }
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            if (args.draggedItemIDs.Count > 1)
                return false;
            var item = FindItem(args.draggedItemIDs[0], rootItem) as ActionTreeViewItem;
            return item.isDraggable;
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            var row = FindItem(args.draggedItemIDs.First(), rootItem);
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.paths = args.draggedItemIDs.Select(i => "" + i).ToArray();
            DragAndDrop.StartDrag(row.displayName);
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            if (args.dragAndDropPosition != DragAndDropPosition.BetweenItems)
                return DragAndDropVisualMode.None;

            var id = Int32.Parse(DragAndDrop.paths.First());
            var item = FindItem(id, rootItem);
            var row = (ActionTreeViewItem)item;

            if (!row.isDraggable || args.parentItem != row.parent)
            {
                return DragAndDropVisualMode.None;
            }

            if (args.performDrop)
            {
                var compositeChildrenCount = 0;
                for (var i = 0; i < args.insertAtIndex; i++)
                {
                    item = args.parentItem.children[i];
                    if (item.hasChildren)
                    {
                        compositeChildrenCount += item.children.Count;
                    }
                }
                args.insertAtIndex += compositeChildrenCount;

                var action = (ActionTreeItem)args.parentItem;
                var map = (ActionMapTreeItem)args.parentItem.parent;

                var dstIndex = action.bindingsStartIndex + args.insertAtIndex;
                var srcIndex = action.bindingsStartIndex + row.index;
                if (dstIndex > srcIndex)
                {
                    dstIndex--;
                }

                InputActionSerializationHelpers.MoveBinding(map.elementProperty, srcIndex, dstIndex);

                if (row.hasChildren)
                {
                    for (var i = 0; i < row.children.Count; i++)
                    {
                        if (dstIndex > srcIndex)
                        {
                            // when moving composite down
                            InputActionSerializationHelpers.MoveBinding(map.elementProperty, srcIndex, dstIndex);
                            continue;
                        }

                        // when moving composite up
                        dstIndex++;
                        srcIndex = action.bindingsStartIndex + (row.children[i] as CompositeTreeItem).index;
                        InputActionSerializationHelpers.MoveBinding(map.elementProperty, srcIndex, dstIndex);
                    }
                }

                m_ApplyAction();
                DragAndDrop.AcceptDrag();
                //since there is no easy way to know the id of the element after reordering
                //instead of updating the selected item, we leave the UI without selection
                if (OnSelectionChanged != null)
                    OnSelectionChanged();
            }
            return DragAndDropVisualMode.Move;
        }
    }
}
#endif // UNITY_EDITOR