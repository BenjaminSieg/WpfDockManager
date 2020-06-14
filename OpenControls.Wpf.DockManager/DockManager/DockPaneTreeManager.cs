﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OpenControls.Wpf.DockManager
{
    internal class DockPaneTreeManager : IDockPaneTreeManager
    {
        public DockPaneTreeManager(IDockPaneTree iDockPaneTree, ILayoutFactory iLayoutFactory)
        {
            ILayoutFactory = iLayoutFactory;
            IDockPaneTree = iDockPaneTree;
        }

        private readonly IDockPaneTree IDockPaneTree;
        private readonly ILayoutFactory ILayoutFactory;

        private Grid FindElement(Guid guid, Grid parentGrid)
        {
            Grid grid = null;

            foreach (var child in parentGrid.Children)
            {
                grid = child as Grid;
                if (grid != null)
                {
                    if ((grid.Tag != null) && ((Guid)grid.Tag == guid))
                    {
                        return grid;
                    }

                    grid = FindElement(guid, grid);
                    if (grid != null)
                    {
                        return grid;
                    }
                }
            }

            return grid;
        }

        private static void ExtractDocuments(FloatingPane floatingPane, DockPane dockPane)
        {
            while (true)
            {
                UserControl userControl = floatingPane.IViewContainer.ExtractUserControl(0);
                if (userControl == null)
                {
                    break;
                }

                dockPane.IViewContainer.AddUserControl(userControl);
            }
            floatingPane.Close();
        }

        private void InsertDockPane(Grid parentSplitterPane, SelectablePane selectablePane, DockPane dockPaneToInsert, bool isHorizontalSplit)
        {
            parentSplitterPane.Children.Remove(selectablePane);

            SplitterPane newGrid = ILayoutFactory.MakeSplitterPane(isHorizontalSplit);
            parentSplitterPane.Children.Add(newGrid);
            Grid.SetRow(newGrid, Grid.GetRow(selectablePane));
            Grid.SetColumn(newGrid, Grid.GetColumn(selectablePane));

            newGrid.AddChild(selectablePane, true);
            newGrid.AddChild(dockPaneToInsert, false);
        }

        #region IDockPaneTreeManager

        public SelectablePane FindElementOfType(Type type, Grid grid)
        {
            System.Diagnostics.Trace.Assert(grid != null);

            if (grid.GetType() == type)
            {
                return grid as SelectablePane;
            }

            foreach (var child in grid.Children)
            {
                if (child is Grid)
                {
                    SelectablePane selectablePane = FindElementOfType(type, child as Grid);
                    if (selectablePane != null)
                    {
                        return selectablePane;
                    }
                }
            }

            return null;
        }

        private void AddViews(List<UserControl> views, List<FrameworkElement> list_N, DelegateCreateDockPane createDockPane)
        {
            List<FrameworkElement> list_N_plus_1 = new List<FrameworkElement>();
            bool isHorizontal = false;
            int viewIndex = 1;

            while (viewIndex < views.Count)
            {
                for (int i = 0; (i < list_N.Count) && (viewIndex < views.Count); ++i)
                {
                    // Warning warning => use insert dock pane method
                    SplitterPane splitterPane = ILayoutFactory.MakeSplitterPane(isHorizontal);

                    var node = list_N[i];
                    System.Windows.Markup.IAddChild parentElement = (System.Windows.Markup.IAddChild)node.Parent;
                    (node.Parent as Grid).Children.Remove(node);

                    parentElement.AddChild(splitterPane);
                    Grid.SetRow(splitterPane, Grid.GetRow(node));
                    Grid.SetColumn(splitterPane, Grid.GetColumn(node));

                    splitterPane.AddChild(node, true);

                    list_N_plus_1.Add(node);

                    node = views[viewIndex];
                    DockPane dockPane = createDockPane();
                    dockPane.IViewContainer.AddUserControl(node as UserControl);

                    list_N_plus_1.Add(dockPane);

                    splitterPane.AddChild(dockPane, false);

                    ++viewIndex;
                }

                isHorizontal = !isHorizontal;
                list_N = list_N_plus_1;
                list_N_plus_1 = new List<FrameworkElement>();
            }
        }

        public DockPane ExtractDockPane(DockPane dockPane, out FrameworkElement frameworkElement)
        {
            frameworkElement = null;

            if (dockPane == null)
            {
                return null;
            }

            Grid parentGrid = dockPane.Parent as Grid;
            System.Diagnostics.Trace.Assert(parentGrid != null, System.Reflection.MethodBase.GetCurrentMethod().Name + ": DockPane parent must be a Grid");

            if (parentGrid == IDockPaneTree)
            {
                IDockPaneTree.Children.Remove(dockPane);
            }
            else
            {
                Grid grandparentGrid = parentGrid.Parent as Grid;
                System.Diagnostics.Trace.Assert(grandparentGrid != null, System.Reflection.MethodBase.GetCurrentMethod().Name + ": Grid parent not a Grid");

                IDockPaneTree.FrameworkElementRemoved(dockPane);
                parentGrid.Children.Remove(dockPane);

                if (!(parentGrid is DocumentPanel))
                {
                    foreach (var item in parentGrid.Children)
                    {
                        if (!(item is GridSplitter))
                        {
                            frameworkElement = item as FrameworkElement;
                            break;
                        }
                    }
                    System.Diagnostics.Trace.Assert(frameworkElement != null);

                    IDockPaneTree.FrameworkElementRemoved(parentGrid);
                    grandparentGrid.Children.Remove(parentGrid);
                    parentGrid.Children.Remove(frameworkElement);
                    int row = Grid.GetRow(parentGrid);
                    int column = Grid.GetColumn(parentGrid);
                    grandparentGrid.Children.Remove(parentGrid);
                    if (grandparentGrid == IDockPaneTree)
                    {
                        IDockPaneTree.RootPane = frameworkElement as Grid;
                    }
                    else
                    {
                        grandparentGrid.Children.Add(frameworkElement);
                        Grid.SetRow(frameworkElement, row);
                        Grid.SetColumn(frameworkElement, column);
                    }
                }
            }

            return dockPane;
        }

        public bool UngroupDockPane(DockPane dockPane, int index, double paneWidth)
        {
            System.Diagnostics.Trace.Assert(dockPane != null, System.Reflection.MethodBase.GetCurrentMethod().Name + ": dockPane is null");

            int viewCount = dockPane.IViewContainer.GetUserControlCount();
            if (viewCount < 2)
            {
                // Cannot ungroup one item!
                return false;
            }

            // The parent must be a SplitterPane or the LayoutManager
            Grid parentSplitterPane = dockPane.Parent as Grid;
            System.Diagnostics.Trace.Assert(parentSplitterPane != null, System.Reflection.MethodBase.GetCurrentMethod().Name + ": dockPane.Parent not a Grid");

            UserControl userControl = dockPane.IViewContainer.ExtractUserControl(index);
            if (userControl == null)
            {
                return false;
            }

            DockPane newDockPane = (dockPane is ToolPaneGroup) ? (DockPane)ILayoutFactory.MakeToolPaneGroup() : ILayoutFactory.MakeDocumentPaneGroup();
            newDockPane.IViewContainer.AddUserControl(userControl);
            InsertDockPane(parentSplitterPane, dockPane, newDockPane, false);

            return true;
        }

        public void Float(DockPane dockPane, bool drag, bool selectedTabOnly)
        {
            if (!selectedTabOnly || (dockPane.IViewContainer.GetUserControlCount() == 1))
            {
                ExtractDockPane(dockPane, out FrameworkElement frameworkElement);
            }

            Point mainWindowLocation = Application.Current.MainWindow.PointToScreen(new Point(0, 0));

            FloatingPane floatingPane = null;
            if (dockPane is ToolPaneGroup)
            {
                floatingPane = ILayoutFactory.MakeFloatingToolPaneGroup();
            }
            else
            {
                floatingPane = ILayoutFactory.MakeFloatingDocumentPaneGroup();
            }

            int index = selectedTabOnly ? dockPane.IViewContainer.GetCurrentTabIndex() : 0;
            while (true)
            {
                UserControl userControl = dockPane.IViewContainer.ExtractUserControl(index);
                if (userControl == null)
                {
                    break;
                }

                floatingPane.IViewContainer.AddUserControl(userControl);

                if (selectedTabOnly)
                {
                    break;
                }
            }

            if (drag)
            {
                IntPtr hWnd = new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow).EnsureHandle();
                OpenControls.Wpf.DockManager.Controls.Utilities.SendLeftMouseButtonUp(hWnd);

                // Ensure the floated window can be dragged by the user
                hWnd = new System.Windows.Interop.WindowInteropHelper(floatingPane).EnsureHandle();
                OpenControls.Wpf.DockManager.Controls.Utilities.SendLeftMouseButtonDown(hWnd);
            }

            Point cursorPositionOnScreen = OpenControls.Wpf.DockManager.Controls.Utilities.GetCursorPosition();
            floatingPane.Left = cursorPositionOnScreen.X - 30;
            floatingPane.Top = cursorPositionOnScreen.Y - 30;
            floatingPane.Width = dockPane.ActualWidth;
            floatingPane.Height = dockPane.ActualHeight;
        }

        public SelectablePane FindSelectablePane(Grid grid, Point pointOnScreen)
        {
            if (grid == null)
            {
                return null;
            }

            foreach (var child in grid.Children)
            {
                if ((child is SelectablePane) || (child is SplitterPane))
                {
                    Grid childGrid = child as Grid;
                    Point pointInToolPane = childGrid.PointFromScreen(pointOnScreen);
                    if (
                            (pointInToolPane.X >= 0) &&
                            (pointInToolPane.X <= childGrid.ActualWidth) &&
                            (pointInToolPane.Y >= 0) &&
                            (pointInToolPane.Y <= childGrid.ActualHeight)
                        )
                    {
                        if (child is DocumentPanel)
                        {
                            if (!(child as DocumentPanel).ContainsDocuments())
                            {
                                return child as DocumentPanel;
                            }
                        }
                        else if (child is DockPane)
                        {
                            return child as DockPane;
                        }

                        return FindSelectablePane(childGrid, pointOnScreen);
                    }
                }
            }

            return null;
        }

        public void Unfloat(FloatingPane floatingPane, SelectablePane selectedPane, WindowLocation windowLocation)
        {
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                if (
                        (floatingPane == null) ||
                        ((selectedPane != null) && !(selectedPane.Parent is SplitterPane) && !(selectedPane.Parent is DocumentPanel) && (selectedPane.Parent != IDockPaneTree))
                   )
                {
                    return;
                }

                SplitterPane parentSplitterPane = null;
                DockPane dockPane = null;

                switch (windowLocation)
                {
                    case WindowLocation.BottomSide:
                    case WindowLocation.TopSide:
                    case WindowLocation.LeftSide:
                    case WindowLocation.RightSide:

                        if (floatingPane is FloatingToolPaneGroup)
                        {
                            dockPane = ILayoutFactory.MakeToolPaneGroup();
                        }
                        else
                        {
                            dockPane = ILayoutFactory.MakeDocumentPaneGroup();
                        }
                        ExtractDocuments(floatingPane, dockPane);

                        parentSplitterPane = ILayoutFactory.MakeSplitterPane((windowLocation == WindowLocation.TopSide) || (windowLocation == WindowLocation.BottomSide));
                        bool isFirst = (windowLocation == WindowLocation.TopSide) || (windowLocation == WindowLocation.LeftSide);
                        parentSplitterPane.AddChild(dockPane, isFirst);

                        if (IDockPaneTree.Children.Count == 0)
                        {
                            IDockPaneTree.Children.Add(parentSplitterPane);
                        }
                        else
                        {
                            Grid rootPane = IDockPaneTree.RootPane;
                            IDockPaneTree.RootPane = parentSplitterPane;
                            parentSplitterPane.AddChild(rootPane, !isFirst);
                        }
                        break;

                    case WindowLocation.Right:
                    case WindowLocation.Left:
                    case WindowLocation.Top:
                    case WindowLocation.Bottom:

                        if (floatingPane is FloatingToolPaneGroup)
                        {
                            dockPane = ILayoutFactory.MakeToolPaneGroup();
                        }
                        else
                        {
                            dockPane = ILayoutFactory.MakeDocumentPaneGroup();
                        }
                        ExtractDocuments(floatingPane, dockPane);

                        SplitterPane newGrid = ILayoutFactory.MakeSplitterPane((windowLocation == WindowLocation.Top) || (windowLocation == WindowLocation.Bottom));

                        if (selectedPane.Parent is DocumentPanel)
                        {
                            DocumentPanel documentPanel = selectedPane.Parent as DocumentPanel;
                            documentPanel.Children.Remove(selectedPane);
                            documentPanel.Children.Add(newGrid);
                        }
                        else
                        {
                            parentSplitterPane = (selectedPane.Parent as SplitterPane);
                            parentSplitterPane.Children.Remove(selectedPane);
                            parentSplitterPane.Children.Add(newGrid);
                            Grid.SetRow(newGrid, Grid.GetRow(selectedPane));
                            Grid.SetColumn(newGrid, Grid.GetColumn(selectedPane));
                        }

                        bool isTargetFirst = (windowLocation == WindowLocation.Right) || (windowLocation == WindowLocation.Bottom);
                        newGrid.AddChild(selectedPane, isTargetFirst);
                        newGrid.AddChild(dockPane, !isTargetFirst);
                        break;

                    case WindowLocation.Middle:

                        if (selectedPane is DockPane)
                        {
                            ExtractDocuments(floatingPane, selectedPane as DockPane);
                        }
                        else if (selectedPane is DocumentPanel)
                        {
                            DocumentPaneGroup documentPaneGroup = ILayoutFactory.MakeDocumentPaneGroup();
                            selectedPane.Children.Add(documentPaneGroup);
                            ExtractDocuments(floatingPane, documentPaneGroup);
                        }
                        break;
                }

                Application.Current.MainWindow.Activate();
            });
        }

        public void PinToolPane(UnpinnedToolData unpinnedToolData, WindowLocation defaultWindowLocation)
        {
            Grid sibling = null;
            if (unpinnedToolData.SiblingGuid == (Guid)IDockPaneTree.RootGrid.Tag)
            {
                sibling = IDockPaneTree.RootGrid;
            }
            else
            {
                sibling = FindElement(unpinnedToolData.SiblingGuid, IDockPaneTree.RootGrid);
            }

            // This can happen when loading a layout
            bool isHorizontal = unpinnedToolData.IsHorizontal;
            bool isFirst = unpinnedToolData.IsFirst;
            if (sibling == null)
            {
                sibling = IDockPaneTree as Grid;
                isHorizontal = (defaultWindowLocation == WindowLocation.TopSide) || (defaultWindowLocation == WindowLocation.BottomSide);
                isFirst = (defaultWindowLocation == WindowLocation.TopSide) || (defaultWindowLocation == WindowLocation.LeftSide);
            }
        
            SplitterPane newSplitterPane = ILayoutFactory.MakeSplitterPane(isHorizontal);

            if (sibling == IDockPaneTree)
            {
                IEnumerable<SplitterPane> enumerableSplitterPanes = IDockPaneTree.Children.OfType<SplitterPane>();
                if (enumerableSplitterPanes.Count() == 1)
                {
                    SplitterPane splitterPane = enumerableSplitterPanes.First();

                    IDockPaneTree.RootPane = newSplitterPane;
                    newSplitterPane.AddChild(splitterPane, !isFirst);
                    newSplitterPane.AddChild(unpinnedToolData.ToolPaneGroup, isFirst);
                }
                else
                {
                    IEnumerable<DocumentPanel> enumerableDocumentPanels = IDockPaneTree.Children.OfType<DocumentPanel>();
                    System.Diagnostics.Trace.Assert(enumerableDocumentPanels.Count() == 1);

                    DocumentPanel documentPanel = enumerableDocumentPanels.First();

                    IDockPaneTree.RootPane = newSplitterPane;
                    newSplitterPane.AddChild(documentPanel, !isFirst);
                    newSplitterPane.AddChild(unpinnedToolData.ToolPaneGroup, isFirst);
                }
            }
            else if (sibling.Parent == IDockPaneTree)
            {
                IDockPaneTree.RootPane = newSplitterPane;
                newSplitterPane.AddChild(sibling, !isFirst);
                newSplitterPane.AddChild(unpinnedToolData.ToolPaneGroup, isFirst);
            }
            else
            {
                SplitterPane parentSplitterPane = sibling.Parent as SplitterPane;
                int row = Grid.GetRow(sibling);
                int column = Grid.GetColumn(sibling);
                isFirst = (parentSplitterPane.IsHorizontal && (row == 0)) || (!parentSplitterPane.IsHorizontal && (column == 0));
                parentSplitterPane.Children.Remove(sibling);

                parentSplitterPane.AddChild(newSplitterPane, isFirst);

                newSplitterPane.AddChild(sibling, !unpinnedToolData.IsFirst);
                newSplitterPane.AddChild(unpinnedToolData.ToolPaneGroup, unpinnedToolData.IsFirst);
            }
        }

        public void UnpinToolPane(ToolPaneGroup toolPaneGroup, out UnpinnedToolData unpinnedToolData, out WindowLocation toolListBoxLocation)
        {
            System.Diagnostics.Trace.Assert(toolPaneGroup != null);

            DocumentPanel documentPanel = FindElementOfType(typeof(DocumentPanel), IDockPaneTree.RootPane) as DocumentPanel;
            System.Diagnostics.Trace.Assert(documentPanel != null);

            List<Grid> documentPanelAncestors = new List<Grid>();
            Grid grid = documentPanel;
            while (grid.Parent != IDockPaneTree)
            {
                grid = grid.Parent as SplitterPane;
                documentPanelAncestors.Add(grid);
            }

            /*
             * Find the first common ancestor for the document panel and the tool pane group
             */

            FrameworkElement frameworkElement = toolPaneGroup;
            while (true)
            {
                if (documentPanelAncestors.Contains(frameworkElement.Parent as Grid))
                {
                    break;
                }

                frameworkElement = frameworkElement.Parent as FrameworkElement;
            }

            toolListBoxLocation = WindowLocation.None;
            bool isFirst = (Grid.GetRow(frameworkElement) == 0) && (Grid.GetColumn(frameworkElement) == 0);
            bool isHorizontal = (frameworkElement.Parent as SplitterPane).IsHorizontal;
            if (isHorizontal)
            {
                if (isFirst)
                {
                    toolListBoxLocation = WindowLocation.TopSide;
                }
                else
                {
                    toolListBoxLocation = WindowLocation.BottomSide;
                }
            }
            else
            {
                if (isFirst)
                {
                    toolListBoxLocation = WindowLocation.LeftSide;
                }
                else
                {
                    toolListBoxLocation = WindowLocation.RightSide;
                }
            }

            unpinnedToolData = new UnpinnedToolData();
            unpinnedToolData.ToolPaneGroup = toolPaneGroup;
            Grid parentGrid = toolPaneGroup.Parent as Grid;
            unpinnedToolData.IsFirst = (Grid.GetRow(toolPaneGroup) == 0) && (Grid.GetColumn(toolPaneGroup) == 0);
            unpinnedToolData.IsHorizontal = (parentGrid as SplitterPane).IsHorizontal;

            ExtractDockPane(toolPaneGroup, out frameworkElement);
            System.Diagnostics.Trace.Assert(frameworkElement != null);

            unpinnedToolData.SiblingGuid = (Guid)((frameworkElement as Grid).Tag);
        }

        public void CreateDefaultLayout(List<UserControl> documentViews, List<UserControl> toolViews)
        {
            IDockPaneTree.Clear();

            /*
             * We descend the tree level by level, adding a new level when the current one is full. 
             * We continue adding nodes until we have exhausted the items in views (created above). 
             * 
                    Parent              Level Index
                       G                    1   
                 /           \              
                 G           DP             2 
              /     \     /     \           
              T     T     D     D           3
              
                Key: 

                    G = Grid
                    T = Tool
                    DP = Document Panel: the root ancestor of all documents. 
                    D = Documents

                Assume we are building level N where there are potentially 2 ^ (N-1) nodes (denoted by a star). 
                Maintain two node lists. One for level N and one for level N + 1. 
                List for level N is denoted list(N). 
                Assume level N nodes are complete. 
                Then for each item in list(N) we add two child nodes, and then add these child nodes to list (N+1). 

                First level: 

                       D1

                Add a node -> replace D1 with a grid containing two documents and a splitter: 

                       G
                 /           \              
                 D1          D2              

                Add a node -> replace D1 with a grid containing two documents and a splitter: 

                       G
                 /           \              
                 G           D2
              /     \     
              D1    D3     

                Add a node -> replace D2 with a grid containing two documents and a splitter: 

                       G
                 /           \              
                 G           G
              /     \     /    \
              D1    D3    D2    D4

                and so on ... 

                Document panes are children of a dock panel. At first this is a child of the top level 
                splitter pane, or the layout manager if there are no tool panes

             */

            IDockPaneTree.RootPane = ILayoutFactory.MakeSplitterPane(true);

            DocumentPanel documentPanel = ILayoutFactory.MakeDocumentPanel();
            (IDockPaneTree.RootPane as SplitterPane).AddChild(documentPanel, true);

            if ((documentViews != null) && (documentViews.Count > 0))
            {
                List<FrameworkElement> list_N = new List<FrameworkElement>();

                DockPane documentPane = ILayoutFactory.MakeDocumentPaneGroup();
                documentPane.IViewContainer.AddUserControl(documentViews[0]);

                documentPanel.Children.Add(documentPane);
                list_N.Add(documentPane);
                AddViews(documentViews, list_N, delegate { return ILayoutFactory.MakeDocumentPaneGroup(); });
            }

            if ((toolViews != null) && (toolViews.Count > 0))
            {
                List<FrameworkElement> list_N = new List<FrameworkElement>();

                DockPane toolPaneGroup = ILayoutFactory.MakeToolPaneGroup();
                toolPaneGroup.IViewContainer.AddUserControl(toolViews[0]);

                (IDockPaneTree.RootPane as SplitterPane).AddChild(toolPaneGroup, false);

                list_N.Add(toolPaneGroup);
                AddViews(toolViews, list_N, delegate { return ILayoutFactory.MakeToolPaneGroup(); });
            }
        }

        private void ValidateDockPanes(Grid grid, Dictionary<IViewModel, List<string>> viewModels, List<DockPane> emptyDockPanes, Type paneType)
        {
            if (grid == null)
            {
                return;
            }

            int numberOfChildren = grid.Children.Count;

            for (int iChild = numberOfChildren - 1; iChild > -1; --iChild)
            {
                UIElement child = grid.Children[iChild];
                if (child.GetType() == paneType)
                {
                    DockPane dockPane = child as DockPane;
                    int count = dockPane.IViewContainer.GetUserControlCount();
                    for (int index = count - 1; index > -1; --index)
                    {
                        IViewModel iViewModel = dockPane.IViewContainer.GetIViewModel(index);
                        if (viewModels.ContainsKey(iViewModel) && (viewModels[iViewModel].Contains(iViewModel.URL)))
                        {
                            viewModels[iViewModel].Remove(iViewModel.URL);
                            if (viewModels[iViewModel].Count == 0)
                            {
                                viewModels.Remove(iViewModel);
                            }
                        }
                        else
                        {
                            dockPane.IViewContainer.ExtractUserControl(index);
                        }
                    }

                    if (dockPane.IViewContainer.GetUserControlCount() == 0)
                    {
                        emptyDockPanes.Add(dockPane);
                    }
                }

                if (child is Grid)
                {
                    ValidateDockPanes(child as Grid, viewModels, emptyDockPanes, paneType);
                }
            }
        }

        /*
         * Remove tool panes with no corresponding IViewModel
         * Ensure there is a tool pane for each IViewModel
         */
        public void ValidateDockPanes(Grid grid, Dictionary<IViewModel, List<string>> viewModelUrlDictionary, Type paneType)
        {
            List<DockPane> emptyDockPanes = new List<DockPane>();
            
            ValidateDockPanes(IDockPaneTree.RootPane, viewModelUrlDictionary, emptyDockPanes, paneType);

            /*
             * Remove dock panes with no matching view model
             */

            foreach (var dockPane in emptyDockPanes)
            {
                ExtractDockPane(dockPane, out FrameworkElement frameworkElement);
            }

            if (viewModelUrlDictionary.Count > 0)
            {
                if (paneType == typeof(DocumentPaneGroup))
                {
                    DockPane siblingDockPane = FindElementOfType(typeof(DocumentPaneGroup), IDockPaneTree.RootPane) as DockPane;
                    if (siblingDockPane == null)
                    {
                        siblingDockPane = ILayoutFactory.MakeDocumentPaneGroup();

                        // There is always a document panel 
                        DocumentPanel documentPanel = FindElementOfType(typeof(DocumentPanel), IDockPaneTree.RootPane) as DocumentPanel;
                        documentPanel.Children.Add(siblingDockPane);
                    }
                    System.Diagnostics.Trace.Assert(siblingDockPane != null);

                    List<UserControl> userControls = IDockPaneTree.LoadDocumentViews(new ObservableCollection<IViewModel>(viewModelUrlDictionary.Keys));
                    foreach (UserControl userControl in userControls)
                    {
                        siblingDockPane.IViewContainer.AddUserControl(userControl);
                    }
                }
                else if (paneType == typeof(ToolPaneGroup))
                {
                    DockPane siblingDockPane = FindElementOfType(typeof(ToolPaneGroup), IDockPaneTree.RootPane) as DockPane;
                    if (siblingDockPane == null)
                    {
                        siblingDockPane = ILayoutFactory.MakeToolPaneGroup();

                        if (IDockPaneTree.RootPane is DocumentPanel)
                        {
                            DocumentPanel documentPanel = IDockPaneTree.RootPane as DocumentPanel;

                            SplitterPane splitterPane = ILayoutFactory.MakeSplitterPane(true);
                            IDockPaneTree.RootPane = splitterPane;
                            splitterPane.AddChild(documentPanel, true);
                            splitterPane.AddChild(siblingDockPane, false);
                        }
                        else
                        {
                            // There is always a document panel 
                            DocumentPanel documentPanel = FindElementOfType(typeof(DocumentPanel), IDockPaneTree.RootPane) as DocumentPanel;

                            InsertDockPane(IDockPaneTree.RootPane, documentPanel, siblingDockPane, false);
                        }
                    }
                    System.Diagnostics.Trace.Assert(siblingDockPane != null);

                    List<UserControl> userControls = IDockPaneTree.LoadToolViews(new ObservableCollection<IViewModel>(viewModelUrlDictionary.Keys));
                    foreach (UserControl userControl in userControls)
                    {
                        siblingDockPane.IViewContainer.AddUserControl(userControl);
                    }
                }
            }
        }

        #endregion IDockPaneTreeManager
    }
}
