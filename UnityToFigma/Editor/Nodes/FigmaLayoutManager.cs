using System;
using UnityEngine;
using UnityEngine.UI;
using UnityToFigma.Editor.FigmaApi;
using UnityToFigma.Editor.Utils;

namespace UnityToFigma.Editor.Nodes
{
    /// <summary>
    /// Manages layout functionality for Figma nodes
    /// </summary>
    public static class FigmaLayoutManager
    {
        /// <summary>
        /// Applies layout properties for a given node to a gameObject, using Vertical/Horizontal layout groups.
        /// When the node implements scrolling, builds a standard uGUI ScrollView tree:
        ///   nodeGameObject (ScrollRect) -> Viewport (RectMask2D) -> Content (LayoutGroup + ContentSizeFitter)
        /// </summary>
        /// <param name="nodeGameObject"></param>
        /// <param name="node"></param>
        /// <param name="figmaImportProcessData"></param>
        /// <param name="scrollContentGameObject">The generated Content GameObject (when scrolling is implemented). Child nodes should be parented here.</param>
        public static void ApplyLayoutPropertiesForNode( GameObject nodeGameObject,Node node,
            FigmaImportProcessData figmaImportProcessData,out GameObject scrollContentGameObject)
        {

            // Depending on whether scrolling is applied, we may want to add layout to this object or to the content
            // holder

            var targetLayoutObject = nodeGameObject;
            scrollContentGameObject = null;

            // Check scrolling requirements
            var implementScrolling = node.type == NodeType.FRAME && node.overflowDirection != Node.OverflowDirection.NONE;
            if (implementScrolling)
            {
                // Build standard uGUI ScrollView tree:
                //   nodeGameObject (ScrollRect)
                //     └─ Viewport (RectMask2D) – stretches to fill ScrollRect
                //          └─ Content (LayoutGroup + ContentSizeFitter when AutoLayout is enabled)

                // Create Viewport as a child of nodeGameObject. Stretches to fill, top-left pivot.
                var viewportGameObject = new GameObject($"{node.name}_Viewport", typeof(RectTransform));
                var viewportRectTransform = (RectTransform)viewportGameObject.transform;
                viewportRectTransform.SetParent(nodeGameObject.transform, false);
                viewportRectTransform.anchorMin = new Vector2(0, 0);
                viewportRectTransform.anchorMax = new Vector2(1, 1);
                viewportRectTransform.pivot = new Vector2(0, 1);
                viewportRectTransform.offsetMin = Vector2.zero;
                viewportRectTransform.offsetMax = Vector2.zero;

                // Viewport gets the RectMask2D so clipping happens here, not on the ScrollRect host
                if (node.clipsContent) UnityUiUtils.GetOrAddComponent<RectMask2D>(viewportGameObject);

                // Create Content as a child of Viewport. Top-left pivot/anchor so size grows down-right naturally.
                scrollContentGameObject = new GameObject($"{node.name}_Content", typeof(RectTransform));
                var scrollContentRectTransform = (RectTransform)scrollContentGameObject.transform;
                scrollContentRectTransform.SetParent(viewportGameObject.transform, false);
                scrollContentRectTransform.pivot = new Vector2(0, 1);
                scrollContentRectTransform.anchorMin = scrollContentRectTransform.anchorMax = new Vector2(0, 1);
                scrollContentRectTransform.anchoredPosition = Vector2.zero;

                // Wire up the ScrollRect on the host node
                var scrollRectComponent = UnityUiUtils.GetOrAddComponent<ScrollRect>(nodeGameObject);
                scrollRectComponent.viewport = viewportRectTransform;
                scrollRectComponent.content = scrollContentRectTransform;
                scrollRectComponent.horizontal =
                    node.overflowDirection is Node.OverflowDirection.HORIZONTAL_SCROLLING
                        or Node.OverflowDirection.HORIZONTAL_AND_VERTICAL_SCROLLING;
                scrollRectComponent.vertical =
                    node.overflowDirection is Node.OverflowDirection.VERTICAL_SCROLLING
                        or Node.OverflowDirection.HORIZONTAL_AND_VERTICAL_SCROLLING;

                // ContentSizeFitter is only added when AutoLayout will actually attach a LayoutGroup to Content –
                // i.e. EnableAutoLayout is on AND the Figma node uses auto layout. Without a LayoutGroup the CSF
                // has no preferred size source and would collapse the Content rect.
                if (figmaImportProcessData.Settings.EnableAutoLayout && node.layoutMode != Node.LayoutMode.NONE)
                {
                    var contentSizeFitter = UnityUiUtils.GetOrAddComponent<ContentSizeFitter>(scrollContentGameObject);
                    contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }

                // LayoutGroup (if any) is applied to Content
                targetLayoutObject = scrollContentGameObject;
            }


            // Ignore if layout mode is NONE or layout disabled
            if (node.layoutMode == Node.LayoutMode.NONE || !figmaImportProcessData.Settings.EnableAutoLayout) return;
            
            // Remove an existing layout group if it exists
            var existingLayoutGroup = targetLayoutObject.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (existingLayoutGroup!=null) UnityEngine.Object.DestroyImmediate(existingLayoutGroup);
            
            HorizontalOrVerticalLayoutGroup layoutGroup = null;
            
            switch (node.layoutMode)
            {
                case Node.LayoutMode.VERTICAL:
                    layoutGroup= UnityUiUtils.GetOrAddComponent<VerticalLayoutGroup>(targetLayoutObject);
                    layoutGroup.childForceExpandWidth= layoutGroup.childForceExpandHeight = false;
                    // Setup alignment according to Figma layout. Primary is Vertical
                    switch (node.primaryAxisAlignItems)
                    {
                        // Upper Alignment
                        case Node.PrimaryAxisAlignItems.MIN:
                            layoutGroup.childAlignment = node.counterAxisAlignItems switch
                            {
                                Node.CounterAxisAlignItems.MIN => TextAnchor.UpperLeft,
                                Node.CounterAxisAlignItems.CENTER => TextAnchor.UpperCenter,
                                Node.CounterAxisAlignItems.MAX => TextAnchor.UpperRight,
                                _ => layoutGroup.childAlignment
                            };
                            break;
                        // Center alignment
                        case Node.PrimaryAxisAlignItems.CENTER:
                            layoutGroup.childAlignment = node.counterAxisAlignItems switch
                            {
                                Node.CounterAxisAlignItems.MIN => TextAnchor.MiddleLeft,
                                Node.CounterAxisAlignItems.CENTER => TextAnchor.MiddleCenter,
                                Node.CounterAxisAlignItems.MAX => TextAnchor.MiddleRight,
                                _ => layoutGroup.childAlignment
                            };
                            break;
                        // Lower alignment
                        case Node.PrimaryAxisAlignItems.MAX:
                            layoutGroup.childAlignment = node.counterAxisAlignItems switch
                            {
                                Node.CounterAxisAlignItems.MIN => TextAnchor.LowerLeft,
                                Node.CounterAxisAlignItems.CENTER => TextAnchor.LowerCenter,
                                Node.CounterAxisAlignItems.MAX => TextAnchor.LowerRight,
                                _ => layoutGroup.childAlignment
                            };
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    break;
                case Node.LayoutMode.HORIZONTAL:
                    layoutGroup= UnityUiUtils.GetOrAddComponent<HorizontalLayoutGroup>(targetLayoutObject);
                    layoutGroup.childForceExpandWidth= layoutGroup.childForceExpandHeight = false;
                    // Setup alignment according to Figma layout. Primary is Horizontal
                    layoutGroup.childAlignment = node.primaryAxisAlignItems switch
                    {
                        // Left Alignment
                        Node.PrimaryAxisAlignItems.MIN => node.counterAxisAlignItems switch
                        {
                            Node.CounterAxisAlignItems.MIN => TextAnchor.UpperLeft,
                            Node.CounterAxisAlignItems.CENTER => TextAnchor.MiddleLeft,
                            Node.CounterAxisAlignItems.MAX => TextAnchor.LowerLeft,
                            _ => layoutGroup.childAlignment
                        },
                        // Center alignment
                        Node.PrimaryAxisAlignItems.CENTER => node.counterAxisAlignItems switch
                        {
                            Node.CounterAxisAlignItems.MIN => TextAnchor.UpperCenter,
                            Node.CounterAxisAlignItems.CENTER => TextAnchor.MiddleCenter,
                            Node.CounterAxisAlignItems.MAX => TextAnchor.LowerCenter,
                            _ => layoutGroup.childAlignment
                        },
                        // Right alignment
                        Node.PrimaryAxisAlignItems.MAX => node.counterAxisAlignItems switch
                        {
                            Node.CounterAxisAlignItems.MIN => TextAnchor.UpperRight,
                            Node.CounterAxisAlignItems.CENTER => TextAnchor.MiddleRight,
                            Node.CounterAxisAlignItems.MAX => TextAnchor.LowerRight,
                            _ => layoutGroup.childAlignment
                        },
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    break;
            }

            layoutGroup.childControlHeight = true;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;

            layoutGroup.padding = new RectOffset(Mathf.RoundToInt(node.paddingLeft), Mathf.RoundToInt(node.paddingRight),
                Mathf.RoundToInt(node.paddingTop), Mathf.RoundToInt(node.paddingBottom));
            layoutGroup.spacing = node.itemSpacing;
        }
    }
}