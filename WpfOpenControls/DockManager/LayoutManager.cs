﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Xml;
using System.Windows.Input;
using System.Windows.Media;
using WpfOpenControls.Controls;

namespace WpfOpenControls.DockManager
{
    public class LayoutManager : System.Windows.Controls.Grid, ILayoutFactory, IDockPaneTree
    {
        public LayoutManager()
        {
            Tag = new Guid("3c81a424-ef66-4de7-a361-9968cd88071c");

            FloatingToolPaneGroups = new List<IFloatingPane>();
            FloatingDocumentPaneGroups = new List<IFloatingPane>();

            RowDefinitions.Add(new RowDefinition() { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Auto) });
            RowDefinitions.Add(new RowDefinition() { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            RowDefinitions.Add(new RowDefinition() { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Auto) });

            ColumnDefinitions.Add(new ColumnDefinition() { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Auto) });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition() { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Auto) });
            CreateToolListBoxes();

            Application.Current.MainWindow.LocationChanged += MainWindow_LocationChanged;
            PreviewMouseDown += LayoutManager_PreviewMouseDown;
            SizeChanged += LayoutManager_SizeChanged;

            _dictUnpinnedToolData = new Dictionary<WindowLocation, List<UnpinnedToolData>>();
            _dictUnpinnedToolData.Add(WindowLocation.LeftSide, new List<UnpinnedToolData>());
            _dictUnpinnedToolData.Add(WindowLocation.TopSide, new List<UnpinnedToolData>());
            _dictUnpinnedToolData.Add(WindowLocation.RightSide, new List<UnpinnedToolData>());
            _dictUnpinnedToolData.Add(WindowLocation.BottomSide, new List<UnpinnedToolData>());

            IDockPaneTreeManager = new DockPaneTreeManager(this, this);
        }

        private void LayoutManager_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Point cursorPoint = Utilities.GetCursorPosition();
            Point topLeftPoint = _root.PointToScreen(new Point(0, 0));
            if (
                    (cursorPoint.X > topLeftPoint.X) &&
                    (cursorPoint.Y > topLeftPoint.Y) &&
                    (cursorPoint.X < (topLeftPoint.X + _root.ActualWidth)) &&
                    (cursorPoint.Y < (topLeftPoint.Y + _root.ActualHeight))
                )
            {
                CloseUnpinnedToolPane();
            }
        }

        ~LayoutManager()
        {
            Shutdown();
        }

        private ILayoutFactory ILayoutFactory
        {
            get
            {
                return this;
            }
        }
        private IDockPaneTree IDockPaneTree 
        { 
            get 
            { 
                return this; 
            } 
        }
        private readonly IDockPaneTreeManager IDockPaneTreeManager;

        public void Shutdown()
        {
            CloseUnpinnedToolPane();

            try
            {
                _windowLocationPane?.Close();
                _windowLocationPane = null;
            }
            catch
            {
                // Ignore
            }
            try
            {

                _sideLocationPane?.Close();
                _sideLocationPane = null;
            }
            catch
            {
                // Ignore
            }
        }

        // Call this from the mainwindow when loaded
        public void Initialise()
        {
            //IntPtr windowHandle = new System.Windows.Interop.WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;
            //if (windowHandle == null)
            //{
            //    throw new Exception("LayoutManager.Initialise(): windowHandle is null");
            //}

            //System.Windows.Interop.HwndSource src = System.Windows.Interop.HwndSource.FromHwnd(windowHandle);
            //if (src == null)
            //{
            //    throw new Exception("LayoutManager.Initialise(): src is null");
            //}

            //src.AddHook(WndProc);
        }

        public DataTemplate ToolPaneTitleTemplate { get; set; }
        public DataTemplate ToolPaneHeaderTemplate { get; set; }
        public DataTemplate DocumentPaneTitleTemplate { get; set; }
        public DataTemplate DocumentPaneHeaderTemplate { get; set; }

        public event EventHandler DocumentClosed;
        public event EventHandler ToolClosed;

        private List<IFloatingPane> FloatingToolPaneGroups;
        private List<IFloatingPane> FloatingDocumentPaneGroups;

        private Dictionary<WindowLocation, Controls.ToolListBox> _dictToolListBoxes;

        private ToolListBoxItem _activeToolListBoxItem;
        private UnpinnedToolPane _activeUnpinnedToolPane;
        private UnpinnedToolData _activeUnpinnedToolData;
        private Controls.ToolListBox _activeToolListBox;

        private Dictionary<WindowLocation, List<UnpinnedToolData>> _dictUnpinnedToolData;

        private Grid _root;

        private SelectablePane SelectedPane;

        /*
         * Remove tool views not in ToolsSource
         */
        private void ValidateDockPanes(Grid grid, List<IViewModel> viewModels, List<DockPane> emptyDockPanes, Type type)
        {
            if (grid == null)
            {
                return;
            }

            int numberOfChildren = grid.Children.Count;

            for (int iChild = numberOfChildren - 1; iChild > -1; --iChild)
            {
                UIElement child = grid.Children[iChild];
                if (child.GetType() == type)
                {
                    DockPane dockPane = child as DockPane;
                    int count = dockPane.IViewContainer.GetUserControlCount();
                    for (int index = count - 1; index > -1; --index)
                    {
                        IViewModel iViewModel = dockPane.IViewContainer.GetIViewModel(index);
                        if (!viewModels.Contains(iViewModel))
                        {
                            dockPane.IViewContainer.ExtractUserControl(index);
                        }
                        else
                        {
                            viewModels.Remove(iViewModel);
                        }
                    }

                    if (dockPane.IViewContainer.GetUserControlCount() == 0)
                    {
                        emptyDockPanes.Add(dockPane);
                    }
                }

                if (child is Grid)
                {
                    ValidateDockPanes(child as Grid, viewModels, emptyDockPanes, type);
                }
            }
        }

        private void ValidateFloatingPanes(List<IViewModel> viewModels, List<IFloatingPane> floatingPanes)
        {
            int count = floatingPanes.Count;
            for (int index = count - 1; index > -1; --index)
            {
                IViewContainer iViewContainer = floatingPanes[index].IViewContainer;
                int tabCount = iViewContainer.GetUserControlCount();

                for (int iTab = tabCount - 1; iTab > -1; --iTab)
                {
                    IViewModel iViewModel = iViewContainer.GetIViewModel(iTab);
                    if (!viewModels.Contains(iViewModel))
                    {
                        iViewContainer.ExtractUserControl(iTab);
                    }
                    else
                    {
                        viewModels.Remove(iViewModel);
                    }
                }

                if (iViewContainer.GetUserControlCount() == 0)
                {
                    floatingPanes[index].Close();
                }
            }
        }

        private void ValidatePanes(Type paneType, IEnumerable<IViewModel> enumerable, List<IFloatingPane> floatingPanes)
        {
            List<IViewModel> viewModels = new List<IViewModel>();

            var enumerator = enumerable.GetEnumerator();
            while (enumerator.MoveNext())
            {
                viewModels.Add(enumerator.Current as IViewModel);
            }
            List<DockPane> emptyDockPanes = new List<DockPane>();
            ValidateDockPanes(_root, viewModels, emptyDockPanes, paneType);
            foreach (var dockPane in emptyDockPanes)
            {
                IDockPaneTreeManager.ExtractDockPane(dockPane, out FrameworkElement frameworkElement);
            }

            ValidateFloatingPanes(viewModels, floatingPanes);
        }

        private void ValidateToolPanes()
        {
            ValidatePanes(typeof(ToolPaneGroup), ToolsSource, FloatingToolPaneGroups);
        }

        private void ValidateDocumentPanes()
        {
            ValidatePanes(typeof(DocumentPaneGroup), DocumentsSource, FloatingDocumentPaneGroups);
        }

        #region dependency properties 

        #region DocumentsSource dependency property

        [Bindable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static readonly DependencyProperty DocumentsSourceProperty = DependencyProperty.Register("DocumentsSource", typeof(System.Collections.ObjectModel.ObservableCollection<IViewModel>), typeof(LayoutManager), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnDocumentsSourceChanged)));

        private void LayoutManager_DocumentsSourceCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ValidatePanes(typeof(DocumentPaneGroup), DocumentsSource, FloatingDocumentPaneGroups);
        }

        public System.Collections.ObjectModel.ObservableCollection<IViewModel> DocumentsSource
        {
            get
            {
                return (System.Collections.ObjectModel.ObservableCollection<IViewModel>)GetValue(DocumentsSourceProperty);
            }
            set
            {
                SetValue(DocumentsSourceProperty, value);
                (value as System.Collections.Specialized.INotifyCollectionChanged).CollectionChanged += LayoutManager_DocumentsSourceCollectionChanged;
            }
        }

        private static void OnDocumentsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LayoutManager)d).OnDocumentsSourceChanged(e);
        }

        protected virtual void OnDocumentsSourceChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue != null)
            {
                DocumentsSource = (System.Collections.ObjectModel.ObservableCollection<IViewModel>)e.NewValue;
                Create();
            }
        }

        #endregion

        #region ToolsSource dependency property

        [Bindable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static readonly DependencyProperty ToolsSourceProperty = DependencyProperty.Register("ToolsSource", typeof(System.Collections.ObjectModel.ObservableCollection<IViewModel>), typeof(LayoutManager), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnToolsSourceChanged)));

        private void LayoutManager_ToolsSourceCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ValidateToolPanes();
        }

        public System.Collections.ObjectModel.ObservableCollection<IViewModel> ToolsSource
        {
            get
            {
                return (System.Collections.ObjectModel.ObservableCollection<IViewModel>)GetValue(ToolsSourceProperty);
            }
            set
            {
                SetValue(ToolsSourceProperty, value);
                (value as System.Collections.Specialized.INotifyCollectionChanged).CollectionChanged += LayoutManager_ToolsSourceCollectionChanged;
            }
        }

        private static void OnToolsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LayoutManager)d).OnToolsSourceChanged(e);
        }

        protected virtual void OnToolsSourceChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue != null)
            {
                ToolsSource = e.NewValue as System.Collections.ObjectModel.ObservableCollection<IViewModel>;
                Create();
            }
        }

        #endregion

        #region DocumentTemplates dependency property

        [Bindable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static readonly DependencyProperty DocumentTemplatesProperty = DependencyProperty.Register("DocumentTemplates", typeof(List<DataTemplate>), typeof(LayoutManager), new FrameworkPropertyMetadata(new List<DataTemplate>(), new PropertyChangedCallback(OnDocumentTemplatesChanged)));

        public List<DataTemplate> DocumentTemplates
        {
            get
            {
                return (List<DataTemplate>)GetValue(DocumentTemplatesProperty);
            }
            set
            {
                SetValue(DocumentTemplatesProperty, value);
            }
        }

        private static void OnDocumentTemplatesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LayoutManager)d).OnDocumentTemplatesChanged(e);
        }

        protected virtual void OnDocumentTemplatesChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue != null)
            {
                DocumentTemplates = (List<DataTemplate>)e.NewValue;
                Create();
            }
        }

        #endregion

        #region ToolTemplates dependency property

        [Bindable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static readonly DependencyProperty ToolTemplatesProperty = DependencyProperty.Register("ToolTemplates", typeof(System.Collections.Generic.List<DataTemplate>), typeof(LayoutManager), new FrameworkPropertyMetadata(new List<DataTemplate>(), new PropertyChangedCallback(OnToolTemplatesChanged)));

        public System.Collections.Generic.List<DataTemplate> ToolTemplates
        {
            get
            {
                return (System.Collections.Generic.List<DataTemplate>)GetValue(ToolTemplatesProperty);
            }
            set
            {
                SetValue(ToolTemplatesProperty, value);
            }
        }

        private static void OnToolTemplatesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LayoutManager)d).OnToolTemplatesChanged(e);
        }

        protected virtual void OnToolTemplatesChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue != null)
            {
                ToolTemplates = (System.Collections.Generic.List<DataTemplate>)e.NewValue;
                Create();
            }
        }

        #endregion

        private void UpdateDocumentProperties(IViewContainer iViewContainer)
        {
            iViewContainer.FontSize = DocumentPaneGroupStyle.FontSize;
            iViewContainer.FontFamily = DocumentPaneGroupStyle.FontFamily;
            iViewContainer.TabCornerRadius = DocumentPaneGroupStyle.TabCornerRadius;
            iViewContainer.GapBrush = DocumentPaneGroupStyle.GapBrush;
            iViewContainer.GapHeight = DocumentPaneGroupStyle.GapHeight;
            iViewContainer.ButtonForeground = DocumentPaneGroupStyle.ButtonForeground;
            iViewContainer.SelectedTabStyle = DocumentPaneGroupStyle.SelectedTabStyle;
            iViewContainer.UnselectedTabStyle = DocumentPaneGroupStyle.UnselectedTabStyle;
            iViewContainer.ActiveScrollIndicatorBrush = DocumentPaneGroupStyle.ActiveScrollIndicatorBrush;
            iViewContainer.InactiveScrollIndicatorBrush = DocumentPaneGroupStyle.InactiveScrollIndicatorBrush;
            iViewContainer.TabItemStyle = DocumentTabItemStyle;
            iViewContainer.ListButtonStyle = DocumentPaneGroupStyle.DocumentListButtonStyle;
            (iViewContainer as DocumentContainer).CommandsButtonStyle = DocumentPaneGroupStyle.CommandsButtonStyle;
        }

        private void UpdateProperties(DocumentPaneGroup documentPaneGroup)
        {
            documentPaneGroup.Border.BorderThickness = DocumentPaneGroupStyle.BorderThickness;
            documentPaneGroup.Border.BorderBrush = DocumentPaneGroupStyle.BorderBrush;
            documentPaneGroup.Border.CornerRadius = DocumentPaneGroupStyle.CornerRadius;
            documentPaneGroup.Background = DocumentPaneGroupStyle.Background;
            documentPaneGroup.HighlightBrush = SelectedPaneBrush;
            UpdateDocumentProperties(documentPaneGroup.IViewContainer);
        }

        private void UpdateToolProperties(IViewContainer iViewContainer)
        {
            iViewContainer.FontSize = ToolPaneGroupStyle.FontSize;
            iViewContainer.FontFamily = ToolPaneGroupStyle.FontFamily;
            iViewContainer.TabCornerRadius = ToolPaneGroupStyle.TabCornerRadius;
            iViewContainer.ButtonForeground = ToolPaneGroupStyle.ButtonForeground;
            iViewContainer.Background = ToolPaneGroupStyle.Background;
            iViewContainer.GapBrush = ToolPaneGroupStyle.GapBrush;
            iViewContainer.GapHeight = ToolPaneGroupStyle.GapHeight;
            iViewContainer.SelectedTabStyle = ToolPaneGroupStyle.SelectedTabStyle;
            iViewContainer.UnselectedTabStyle = ToolPaneGroupStyle.UnselectedTabStyle;
            iViewContainer.ActiveScrollIndicatorBrush = ToolPaneGroupStyle.ActiveScrollIndicatorBrush;
            iViewContainer.InactiveScrollIndicatorBrush = ToolPaneGroupStyle.InactiveScrollIndicatorBrush;
            iViewContainer.TabItemStyle = ToolTabItemStyle;
            iViewContainer.ListButtonStyle = ToolPaneGroupStyle.ToolListButtonStyle;
        }

        private void UpdateProperties(ToolPaneGroup toolPaneGroup)
        {
            toolPaneGroup.Border.BorderThickness = ToolPaneGroupStyle.BorderThickness;
            toolPaneGroup.Border.BorderBrush = ToolPaneGroupStyle.BorderBrush;
            toolPaneGroup.Border.CornerRadius = ToolPaneGroupStyle.CornerRadius;
            toolPaneGroup.HeaderBackground = ToolPaneGroupStyle.HeaderStyle.Background;
            toolPaneGroup.HeaderBorder.BorderBrush = ToolPaneGroupStyle.HeaderStyle.BorderBrush;
            toolPaneGroup.HeaderBorder.BorderThickness = ToolPaneGroupStyle.HeaderStyle.BorderThickness;
            toolPaneGroup.HeaderBorder.Background = ToolPaneGroupStyle.HeaderStyle.Background;
            toolPaneGroup.HeaderBorder.CornerRadius = ToolPaneGroupStyle.HeaderStyle.CornerRadius;
            toolPaneGroup.FontSize = ToolPaneGroupStyle.FontSize;
            toolPaneGroup.FontFamily = ToolPaneGroupStyle.FontFamily;
            toolPaneGroup.HighlightBrush = SelectedPaneBrush;
            toolPaneGroup.ButtonForeground = ToolPaneGroupStyle.ButtonForeground;
            toolPaneGroup.CloseButtonStyle = ToolPaneGroupStyle.CloseButtonStyle;
            toolPaneGroup.PinButtonStyle = ToolPaneGroupStyle.PinButtonStyle;
            toolPaneGroup.CommandsButtonStyle = ToolPaneGroupStyle.CommandsButtonStyle;
            UpdateToolProperties(toolPaneGroup.IViewContainer as ToolContainer);
        }

        private void UpdateProperties(FloatingToolPaneGroup floatingToolPaneGroup)
        {
            floatingToolPaneGroup.FontSize = ToolPaneGroupStyle.FontSize;
            floatingToolPaneGroup.FontFamily = ToolPaneGroupStyle.FontFamily;
            floatingToolPaneGroup.Background = ToolPaneGroupStyle.Background;
            floatingToolPaneGroup.TitleBarBackground = FloatingToolTitleBarBackground;
            UpdateToolProperties(floatingToolPaneGroup.IViewContainer);
        }

        private void UpdateProperties(FloatingDocumentPaneGroup floatingDocumentPaneGroup)
        {
            floatingDocumentPaneGroup.FontSize = DocumentPaneGroupStyle.FontSize;
            floatingDocumentPaneGroup.FontFamily = DocumentPaneGroupStyle.FontFamily;
            floatingDocumentPaneGroup.Background = DocumentPaneGroupStyle.Background;
            floatingDocumentPaneGroup.TitleBarBackground = FloatingDocumentTitleBarBackground;
            UpdateDocumentProperties(floatingDocumentPaneGroup.IViewContainer);
        }

        private void UpdateProperties(Controls.ToolListBox toolListBox)
        {
            toolListBox.FontSize = SideToolStyle.FontSize;
            toolListBox.FontFamily = SideToolStyle.FontFamily;
            toolListBox.BarBrush = SideToolStyle.BarBrush;
            toolListBox.BarBrushMouseOver = SideToolStyle.MouseOverBarBrush;
            toolListBox.Foreground = SideToolStyle.Foreground;
            if (SideToolItemContainerStyle != null)
            {
                toolListBox.ItemContainerStyle = SideToolItemContainerStyle;
            }
        }

        private void UpdateSideToolProperties()
        {
            foreach (var keyValuePair in _dictToolListBoxes)
            {
                UpdateProperties(keyValuePair.Value);
            }
        }

        private void UpdateFloatingToolPaneGroupProperties()
        {
            foreach (var floatingToolPaneGroup in FloatingToolPaneGroups)
            {
                UpdateProperties(floatingToolPaneGroup as FloatingToolPaneGroup);
            }
        }

        private void UpdateFloatingDocumentPaneGroupProperties()
        {
            foreach (var floatingDocumentPaneGroup in FloatingDocumentPaneGroups)
            {
                UpdateProperties(floatingDocumentPaneGroup as FloatingDocumentPaneGroup);
            }
        }

        private void UpdateProperties(Grid grid)
        {
            if (grid == null)
            {
                return;
            }

            if (grid is SplitterPane)
            {
                (grid as SplitterPane).SplitterWidth = SplitterWidth;
                (grid as SplitterPane).SplitterBrush = SplitterBrush;
            }

            foreach (var child in grid.Children)
            {
                if (child is DocumentPanel)
                {
                    (child as DocumentPanel).HighlightBrush = SelectedPaneBrush;
                }
                else if (child is ToolPaneGroup)
                {
                    UpdateProperties(child as ToolPaneGroup);
                }
                else if (child is DocumentPaneGroup)
                {
                    UpdateProperties(child as DocumentPaneGroup);
                }
                else if (child is FloatingToolPaneGroup)
                {
                    UpdateProperties((child as FloatingToolPaneGroup));
                }
                else if (child is FloatingDocumentPaneGroup)
                {
                    UpdateProperties(child as FloatingDocumentPaneGroup);
                }

                if (child is Grid)
                {
                    UpdateProperties(child as Grid);
                }
            }
        }

        private void UpdateProperties()
        {
            UpdateProperties(_root);
            UpdateFloatingToolPaneGroupProperties();
            UpdateFloatingDocumentPaneGroupProperties();
        }

        #region SplitterWidth dependency property

        [Bindable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static readonly DependencyProperty SplitterWidthProperty = DependencyProperty.Register("SplitterWidth", typeof(double), typeof(TabHeaderControl), new FrameworkPropertyMetadata(4.0, new PropertyChangedCallback(OnSplitterWidthChanged)));

        public double SplitterWidth
        {
            get
            {
                return (double)GetValue(SplitterWidthProperty);
            }
            set
            {
                if (value != SplitterWidth)
                {
                    SetValue(SplitterWidthProperty, value);
                    UpdateProperties();
                }
            }
        }

        private static void OnSplitterWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LayoutManager)d).OnSplitterWidthChanged(e);
        }

        protected virtual void OnSplitterWidthChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((double)e.NewValue != SplitterWidth)
            {
                UpdateProperties();
            }
        }

        #endregion

        #region SplitterBrush dependency property

        [Bindable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static readonly DependencyProperty SplitterBrushProperty = DependencyProperty.Register("SplitterBrush", typeof(Brush), typeof(TabHeaderControl), new FrameworkPropertyMetadata(Brushes.Gainsboro, new PropertyChangedCallback(OnSplitterBrushChanged)));

        public Brush SplitterBrush
        {
            get
            {
                return (Brush)GetValue(SplitterBrushProperty);
            }
            set
            {
                if (value != SplitterBrush)
                {
                    SetValue(SplitterBrushProperty, value);
                    UpdateProperties();
                }
            }
        }

        private static void OnSplitterBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LayoutManager)d).OnSplitterBrushChanged(e);
        }

        protected virtual void OnSplitterBrushChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((Brush)e.NewValue != SplitterBrush)
            {
                UpdateProperties();
            }
        }

        #endregion

        #region SelectedPaneBrush dependency property

        [Bindable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static readonly DependencyProperty SelectedPaneBrushProperty = DependencyProperty.Register("SelectedPaneBrush", typeof(Brush), typeof(TabHeaderControl), new FrameworkPropertyMetadata(Brushes.Crimson, new PropertyChangedCallback(OnSelectedPaneBrushChanged)));

        public Brush SelectedPaneBrush
        {
            get
            {
                return (Brush)GetValue(SelectedPaneBrushProperty);
            }
            set
            {
                if (value != SelectedPaneBrush)
                {
                    SetValue(SelectedPaneBrushProperty, value);
                    UpdateProperties();
                }
            }
        }

        private static void OnSelectedPaneBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LayoutManager)d).OnSelectedPaneBrushChanged(e);
        }

        protected virtual void OnSelectedPaneBrushChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((Brush)e.NewValue != SelectedPaneBrush)
            {
                UpdateProperties();
            }
        }

        #endregion

        #region ToolTabItemStyle dependency property

        [Bindable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static readonly DependencyProperty ToolTabItemStyleProperty = DependencyProperty.Register("ToolTabItemStyle", typeof(Style), typeof(TabHeaderControl), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnToolTabItemStyleChanged)));

        public Style ToolTabItemStyle
        {
            get
            {
                return (Style)GetValue(ToolTabItemStyleProperty);
            }
            set
            {
                if (value != ToolTabItemStyle)
                {
                    SetValue(ToolTabItemStyleProperty, value);
                    UpdateSideToolProperties();
                }
            }
        }

        private static void OnToolTabItemStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LayoutManager)d).OnToolTabItemStyleChanged(e);
        }

        protected virtual void OnToolTabItemStyleChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((Style)e.NewValue != ToolTabItemStyle)
            {
                UpdateProperties();
            }
        }

        #endregion

        #region ToolPaneGroupStyle dependency property

        [Bindable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static readonly DependencyProperty ToolPaneGroupStyleProperty = DependencyProperty.Register("ToolPaneGroupStyle", typeof(ToolPaneGroupStyle), typeof(TabHeaderControl), new FrameworkPropertyMetadata(new ToolPaneGroupStyle(), new PropertyChangedCallback(OnToolPaneGroupStyleChanged)));

        public ToolPaneGroupStyle ToolPaneGroupStyle
        {
            get
            {
                return (ToolPaneGroupStyle)GetValue(ToolPaneGroupStyleProperty);
            }
            set
            {
                if (value != ToolPaneGroupStyle)
                {
                    if (value.SelectedTabStyle == null)
                    {
                        value.SelectedTabStyle = (new ToolPaneGroupStyle()).SelectedTabStyle;
                    }
                    if (value.UnselectedTabStyle == null)
                    {
                        value.UnselectedTabStyle = (new ToolPaneGroupStyle()).UnselectedTabStyle;
                    }
                    SetValue(ToolPaneGroupStyleProperty, value);
                    UpdateProperties();
                }
            }
        }

        private static void OnToolPaneGroupStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LayoutManager)d).OnToolPaneGroupStyleChanged(e);
        }

        protected virtual void OnToolPaneGroupStyleChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((ToolPaneGroupStyle)e.NewValue != ToolPaneGroupStyle)
            {
                UpdateProperties();
            }
        }

        #endregion

        #region FloatingToolTitleBarBackground dependency property

        [Bindable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static readonly DependencyProperty FloatingToolTitleBarBackgroundProperty = DependencyProperty.Register("FloatingToolTitleBarBackground", typeof(Brush), typeof(TabHeaderControl), new FrameworkPropertyMetadata(Brushes.Gainsboro, new PropertyChangedCallback(OnFloatingToolTitleBarBackgroundChanged)));

        public Brush FloatingToolTitleBarBackground
        {
            get
            {
                return (Brush)GetValue(FloatingToolTitleBarBackgroundProperty);
            }
            set
            {
                if (value != FloatingToolTitleBarBackground)
                {
                    SetValue(FloatingToolTitleBarBackgroundProperty, value);
                    UpdateProperties();
                }
            }
        }

        private static void OnFloatingToolTitleBarBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LayoutManager)d).OnFloatingToolTitleBarBackgroundChanged(e);
        }

        protected virtual void OnFloatingToolTitleBarBackgroundChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((Brush)e.NewValue != FloatingToolTitleBarBackground)
            {
                UpdateProperties();
            }
        }

        #endregion

        #region DocumentTabItemStyle dependency property

        [Bindable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static readonly DependencyProperty DocumentTabItemStyleProperty = DependencyProperty.Register("DocumentTabItemStyle", typeof(Style), typeof(TabHeaderControl), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnDocumentTabItemStyleChanged)));

        public Style DocumentTabItemStyle
        {
            get
            {
                return (Style)GetValue(DocumentTabItemStyleProperty);
            }
            set
            {
                if (value != DocumentTabItemStyle)
                {
                    SetValue(DocumentTabItemStyleProperty, value);
                    UpdateSideToolProperties();
                }
            }
        }

        private static void OnDocumentTabItemStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LayoutManager)d).OnDocumentTabItemStyleChanged(e);
        }

        protected virtual void OnDocumentTabItemStyleChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((Style)e.NewValue != DocumentTabItemStyle)
            {
                UpdateProperties();
            }
        }

        #endregion

        #region DocumentPaneGroupStyle dependency property

        [Bindable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static readonly DependencyProperty DocumentPaneGroupStyleProperty = DependencyProperty.Register("DocumentPaneGroupStyle", typeof(DocumentPaneGroupStyle), typeof(TabHeaderControl), new FrameworkPropertyMetadata(new DocumentPaneGroupStyle(), new PropertyChangedCallback(OnDocumentPaneGroupStyleChanged)));

        public DocumentPaneGroupStyle DocumentPaneGroupStyle
        {
            get
            {
                return (DocumentPaneGroupStyle)GetValue(DocumentPaneGroupStyleProperty);
            }
            set
            {
                if (value != DocumentPaneGroupStyle)
                {
                    if (value.SelectedTabStyle == null)
                    {
                        value.SelectedTabStyle = (new DocumentPaneGroupStyle()).SelectedTabStyle;
                    }
                    if (value.UnselectedTabStyle == null)
                    {
                        value.UnselectedTabStyle = (new DocumentPaneGroupStyle()).UnselectedTabStyle;
                    }
                    SetValue(DocumentPaneGroupStyleProperty, value);
                    UpdateProperties();
                }
            }
        }

        private static void OnDocumentPaneGroupStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LayoutManager)d).OnDocumentPaneGroupStyleChanged(e);
        }

        protected virtual void OnDocumentPaneGroupStyleChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((DocumentPaneGroupStyle)e.NewValue != DocumentPaneGroupStyle)
            {
                UpdateProperties();
            }
        }

        #endregion

        #region FloatingDocumentTitleBarBackground dependency property

        [Bindable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static readonly DependencyProperty FloatingDocumentTitleBarBackgroundProperty = DependencyProperty.Register("FloatingDocumentTitleBarBackground", typeof(Brush), typeof(TabHeaderControl), new FrameworkPropertyMetadata(Brushes.Gainsboro, new PropertyChangedCallback(OnFloatingDocumentTitleBarBackgroundChanged)));

        public Brush FloatingDocumentTitleBarBackground
        {
            get
            {
                return (Brush)GetValue(FloatingDocumentTitleBarBackgroundProperty);
            }
            set
            {
                if (value != FloatingDocumentTitleBarBackground)
                {
                    SetValue(FloatingDocumentTitleBarBackgroundProperty, value);
                }
            }
        }

        private static void OnFloatingDocumentTitleBarBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LayoutManager)d).OnFloatingDocumentTitleBarBackgroundChanged(e);
        }

        protected virtual void OnFloatingDocumentTitleBarBackgroundChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((Brush)e.NewValue != FloatingDocumentTitleBarBackground)
            {
                UpdateProperties();
            }
        }

        #endregion

        #region SideToolStyle dependency property

        private static SideToolStyle DefaultSideToolStyle
        {
            get
            {
                return new SideToolStyle() 
                { 
                    FontSize = 12, 
                    FontFamily = new FontFamily("Arial"), 
                    Foreground = Brushes.White, 
                    BarBrush = Brushes.Navy, 
                    MouseOverBarBrush = Brushes.AliceBlue };
            }
        }

        [Bindable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static readonly DependencyProperty SideToolStyleProperty = DependencyProperty.Register("SideToolStyle", typeof(SideToolStyle), typeof(TabHeaderControl), new FrameworkPropertyMetadata(DefaultSideToolStyle, new PropertyChangedCallback(OnSideToolStyleChanged)));

        public SideToolStyle SideToolStyle
        {
            get
            {
                return (SideToolStyle)GetValue(SideToolStyleProperty);
            }
            set
            {
                if (value != SideToolStyle)
                {
                    SetValue(SideToolStyleProperty, value);
                    UpdateSideToolProperties();
                }
            }
        }

        private static void OnSideToolStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LayoutManager)d).OnSideToolStyleChanged(e);
        }

        protected virtual void OnSideToolStyleChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((SideToolStyle)e.NewValue != SideToolStyle)
            {
                UpdateSideToolProperties();
            }
        }

        #endregion

        #region SideToolItemContainerStyle dependency property

        [Bindable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public static readonly DependencyProperty SideToolItemContainerStyleProperty = DependencyProperty.Register("SideToolItemContainerStyle", typeof(Style), typeof(TabHeaderControl), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnSideToolItemContainerStyleChanged)));

        public Style SideToolItemContainerStyle
        {
            get
            {
                return (Style)GetValue(SideToolItemContainerStyleProperty);
            }
            set
            {
                if (value != SideToolItemContainerStyle)
                {
                    SetValue(SideToolItemContainerStyleProperty, value);
                    UpdateSideToolProperties();
                }
            }
        }

        private static void OnSideToolItemContainerStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LayoutManager)d).OnSideToolItemContainerStyleChanged(e);
        }

        protected virtual void OnSideToolItemContainerStyleChanged(DependencyPropertyChangedEventArgs e)
        {
            if ((Style)e.NewValue != SideToolItemContainerStyle)
            {
                UpdateSideToolProperties();
            }
        }

        #endregion

        #endregion

        private void ProcessMoveResize()
        {
            if (_activeUnpinnedToolPane != null)
            {
                Point topLeftPoint = _root.PointToScreen(new Point(0, 0));
                double left = topLeftPoint.X;
                double top = topLeftPoint.Y;
                switch (_activeToolListBox.WindowLocation)
                {
                    case WindowLocation.TopSide:
                        _activeUnpinnedToolPane.Width = _root.ActualWidth;
                        break;
                    case WindowLocation.BottomSide:
                        top += _root.ActualHeight - _activeUnpinnedToolPane.ActualHeight;
                        _activeUnpinnedToolPane.Width = _root.ActualWidth;
                        break;
                    case WindowLocation.LeftSide:
                        _activeUnpinnedToolPane.Height = _root.ActualHeight;
                        break;
                    case WindowLocation.RightSide:
                        left += _root.ActualWidth - _activeUnpinnedToolPane.ActualWidth;
                        _activeUnpinnedToolPane.Height = _root.ActualHeight;
                        break;
                }
                _activeUnpinnedToolPane.Left = left;
                _activeUnpinnedToolPane.Top = top;
            }
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            ProcessMoveResize();
        }

        private void LayoutManager_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ProcessMoveResize();
        }

        private void CloseUnpinnedToolPane()
        {
            if (_activeUnpinnedToolPane != null)
            {
                UserControl userControl = _activeUnpinnedToolPane.ToolPane.IViewContainer.ExtractUserControl(0);
                _activeToolListBoxItem.IViewContainer.InsertUserControl(_activeToolListBoxItem.Index, userControl);
                _activeUnpinnedToolPane.Close();
                _activeUnpinnedToolPane = null;
                _activeToolListBoxItem = null;
            }
        }

        private UnpinnedToolPane CreateUnpinnedToolPane(ToolListBoxItem toolListBoxItem, WindowLocation windowLocation)
        {
            UnpinnedToolPane unpinnedToolPane = new UnpinnedToolPane();
            UpdateProperties(unpinnedToolPane.ToolPane);

            UserControl userControl = toolListBoxItem.IViewContainer.ExtractUserControl(toolListBoxItem.Index);
            unpinnedToolPane.ToolPane.IViewContainer.AddUserControl(userControl);
            Point topLeftPoint = _root.PointToScreen(new Point(0, 0));
            unpinnedToolPane.Left = topLeftPoint.X;
            unpinnedToolPane.Top = topLeftPoint.Y;
            if ((windowLocation == WindowLocation.TopSide) || (windowLocation == WindowLocation.BottomSide))
            {
                unpinnedToolPane.Width = _root.ActualWidth;
                double height = toolListBoxItem.Height;
                if (height == 0.0)
                {
                    height = _root.ActualHeight / 3;
                }
                unpinnedToolPane.Height = height;
                if (windowLocation == WindowLocation.BottomSide)
                {
                    unpinnedToolPane.Top += _root.ActualHeight - height;
                }
            }
            else
            {
                unpinnedToolPane.Height = _root.ActualHeight;
                double width = toolListBoxItem.Width;
                if (width == 0.0)
                {
                    width = _root.ActualWidth / 3;
                }
                unpinnedToolPane.Width = width;
                if (windowLocation == WindowLocation.RightSide)
                {
                    unpinnedToolPane.Left += _root.ActualWidth - width;
                }
            }
            unpinnedToolPane.CloseRequest += UnpinnedToolPane_CloseRequest;
            unpinnedToolPane.Closed += UnpinnedToolPane_Closed;
            unpinnedToolPane.PinClick += UnpinnedToolPane_PinClick;
            unpinnedToolPane.WindowLocation = windowLocation;
            unpinnedToolPane.Owner = Application.Current.MainWindow;

            unpinnedToolPane.Show();

            return unpinnedToolPane;
        }

        private void ToolListBox_ItemClick(object sender, Events.ItemClickEventArgs e)
        {
            System.Diagnostics.Trace.Assert(sender is ToolListBoxItem);
            System.Diagnostics.Trace.Assert((e != null) && (e.ToolListBox != null));

            ToolListBoxItem toolListBoxItem = _activeToolListBoxItem;
            CloseUnpinnedToolPane();

            if (toolListBoxItem == (sender as ToolListBoxItem))
            {
                return;
            }

            _activeToolListBox = e.ToolListBox;
            _activeToolListBoxItem = sender as ToolListBoxItem;
            _activeUnpinnedToolData = _dictUnpinnedToolData[e.ToolListBox.WindowLocation].Where(n => n.Items.Contains(_activeToolListBoxItem)).First();
            _activeUnpinnedToolPane = CreateUnpinnedToolPane(sender as ToolListBoxItem, e.ToolListBox.WindowLocation);
        }

        private void UnpinnedToolPane_PinClick(object sender, EventArgs e)
        {
            System.Diagnostics.Trace.Assert(_activeUnpinnedToolPane == sender);
            System.Diagnostics.Trace.Assert(_activeUnpinnedToolData != null);

            /*
             * Put the view back into its ToolPane  
             */

            UserControl userControl = _activeUnpinnedToolPane.ToolPane.IViewContainer.ExtractUserControl(0);
            _activeToolListBoxItem.IViewContainer.InsertUserControl(_activeToolListBoxItem.Index, userControl);

            /*
             * Restore the pane in the view tree
             */

           IDockPaneTreeManager.PinToolPane(_activeUnpinnedToolData);

            /*
             * Remove the tool list items from the side bar
             */

            List<ToolListBoxItem> toolListBoxItems = _activeUnpinnedToolData.Items;
            IEnumerable<Controls.IToolListBoxItem> iEnumerable = _activeToolListBox.ItemsSource.Except(toolListBoxItems);
            _activeToolListBox.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<Controls.IToolListBoxItem>(iEnumerable);

            _dictUnpinnedToolData[_activeToolListBox.WindowLocation].Remove(_activeUnpinnedToolData);
            _activeUnpinnedToolPane.Close();
            _activeUnpinnedToolPane = null;
            _activeToolListBoxItem = null;
            _activeToolListBox = null;
            _activeUnpinnedToolData = null;
        }

        private void UnpinnedToolPane_CloseRequest(object sender, EventArgs e)
        {
            System.Diagnostics.Trace.Assert(sender is UnpinnedToolPane);
            System.Diagnostics.Trace.Assert(sender == _activeUnpinnedToolPane);

            UserControl userControl = _activeUnpinnedToolPane.ToolPane.IViewContainer.ExtractUserControl(0);
            IViewModel iViewModel = userControl.DataContext as IViewModel;

            System.Diagnostics.Trace.Assert(iViewModel != null);

            /*
             * Remove the tool list items from the side bar
             */

            _activeToolListBox.ItemsSource.Remove(_activeToolListBoxItem);
            if (_activeToolListBox.ItemsSource.Count == 0)
            {
                _dictUnpinnedToolData[_activeToolListBox.WindowLocation].Remove(_activeUnpinnedToolData);
            }
            _activeUnpinnedToolPane.Close();
            _activeUnpinnedToolPane = null;
            _activeToolListBoxItem = null;
            _activeToolListBox = null;
            _activeUnpinnedToolData = null;

            System.Diagnostics.Trace.Assert(ToolsSource.Contains(iViewModel));
            ToolsSource.Remove(iViewModel);
        }

        private void UnpinnedToolPane_Closed(object sender, EventArgs e)
        {
            System.Diagnostics.Trace.Assert(_activeUnpinnedToolPane == sender);

            _activeToolListBoxItem.Width = (sender as UnpinnedToolPane).ActualWidth;
            _activeToolListBoxItem.Height = (sender as UnpinnedToolPane).ActualHeight;
        }

        private void AddUnpinnedToolData(UnpinnedToolData unpinnedToolData, WindowLocation windowLocation, Controls.ToolListBox ToolListBox)
        {
            _dictUnpinnedToolData[windowLocation].Add(unpinnedToolData);

            int count = unpinnedToolData.ToolPaneGroup.IViewContainer.GetUserControlCount();
            for (int i = 0; i < count; ++i)
            {
                ToolListBoxItem toolListBoxItem = new ToolListBoxItem()
                {
                    IViewContainer = unpinnedToolData.ToolPaneGroup.IViewContainer,
                    Index = i,
                };
                (ToolListBox.ItemsSource as System.Collections.ObjectModel.ObservableCollection<Controls.IToolListBoxItem>).Add(toolListBoxItem);
                unpinnedToolData.Items.Add(toolListBoxItem);
            }
        }

        private void ToolPane_UnPinClick(object sender, EventArgs e)
        {
            System.Diagnostics.Trace.Assert(sender is ToolPaneGroup);

            ToolPaneGroup toolPaneGroup = sender as ToolPaneGroup;
            IDockPaneTreeManager.UnpinToolPane(toolPaneGroup, out UnpinnedToolData unpinnedToolData);

            Controls.ToolListBox toolListBox = null;
            WindowLocation windowLocation = WindowLocation.None;
            if (unpinnedToolData.IsHorizontal)
            {
                if (unpinnedToolData.IsFirst)
                {
                    windowLocation = WindowLocation.TopSide;
                }
                else
                {
                    windowLocation = WindowLocation.BottomSide;
                }
            }
            else
            {
                if (unpinnedToolData.IsFirst)
                {
                    windowLocation = WindowLocation.LeftSide;
                }
                else
                {
                    windowLocation = WindowLocation.RightSide;
                }
            }
            toolListBox = _dictToolListBoxes[windowLocation];

            AddUnpinnedToolData(unpinnedToolData, windowLocation, toolListBox);
        }

        private void CreateToolListBox(int row, int column, bool isHorizontal, WindowLocation windowLocation)
        {
            System.Collections.ObjectModel.ObservableCollection<Controls.IToolListBoxItem> items = new System.Collections.ObjectModel.ObservableCollection<Controls.IToolListBoxItem>();
            Controls.ToolListBox toolListBox = new Controls.ToolListBox();
            toolListBox.WindowLocation = windowLocation;
            UpdateProperties(toolListBox);
            toolListBox.Background = System.Windows.Media.Brushes.Transparent;
            toolListBox.ItemsSource = items;
            toolListBox.IsHorizontal = isHorizontal;
            toolListBox.DisplayMemberPath = "Title";
            toolListBox.ItemClick += ToolListBox_ItemClick;
            Children.Add(toolListBox);
            Grid.SetRow(toolListBox, row);
            Grid.SetColumn(toolListBox, column);

            _dictToolListBoxes.Add(windowLocation, toolListBox);
        }

        private void CreateToolListBoxes()
        {
            _dictToolListBoxes = new Dictionary<WindowLocation, Controls.ToolListBox>();
            CreateToolListBox(1, 0, false, WindowLocation.LeftSide);
            CreateToolListBox(1, 2, false, WindowLocation.RightSide);
            CreateToolListBox(0, 1, true, WindowLocation.TopSide);
            CreateToolListBox(2, 1, true, WindowLocation.BottomSide);
        }

        public void Clear()
        {
            Children.Clear();
            CreateToolListBoxes();
            while (FloatingToolPaneGroups.Count > 0)
            {
                FloatingToolPaneGroups[0].Close();
            }
            FloatingToolPaneGroups.Clear();
            while (FloatingDocumentPaneGroups.Count > 0)
            {
                FloatingDocumentPaneGroups[0].Close();
            }
            FloatingDocumentPaneGroups.Clear();
        }

        public List<UserControl> LoadViewsFromTemplates(List<DataTemplate> dataTemplates, System.Collections.IEnumerable viewModels)
        {
            List<UserControl> views = new List<UserControl>();

            if ((dataTemplates == null) || (dataTemplates.Count == 0) || (viewModels == null))
            {
                return views;
            }

            // First load the views and view models

            foreach (var viewModel in viewModels)
            {
                foreach (var item in dataTemplates)
                {
                    if (item is DataTemplate)
                    {
                        DataTemplate dataTemplate = item as DataTemplate;

                        if (viewModel.GetType() == (Type)dataTemplate.DataType)
                        {
                            UserControl view = (dataTemplate.LoadContent() as UserControl);
                            if (view != null)
                            {
                                IView iToolView = (view as IView);
                                System.Diagnostics.Trace.Assert(iToolView != null, "The UserControl must implement interface IView");
                                iToolView.IViewModel = viewModel as IViewModel;
                                view.DataContext = viewModel;
                                view.HorizontalAlignment = HorizontalAlignment.Stretch;
                                view.VerticalAlignment = VerticalAlignment.Stretch;

                                views.Add(view);
                            }
                        }
                    }
                }
            }

            return views;
        }

        #region ILayoutFactory

        DocumentPanel ILayoutFactory.MakeDocumentPanel()
        {
            DocumentPanel documentPanel = new DocumentPanel();
            documentPanel.HighlightBrush = SelectedPaneBrush;
            return documentPanel;
        }

        SplitterPane ILayoutFactory.MakeSplitterPane(bool isHorizontal)
        {
            return new SplitterPane(isHorizontal, SplitterWidth, SplitterBrush);
        }

        private void RegisterDockPane(DockPane dockPane)
        {
            System.Diagnostics.Trace.Assert(dockPane != null);

            dockPane.CloseRequest += DockPane_CloseRequest;
            dockPane.Float += DockPane_Float;
            dockPane.FloatTabRequest += DockPane_FloatTabRequest;
            dockPane.UngroupCurrent += DockPane_UngroupCurrent;
            dockPane.Ungroup += DockPane_Ungroup;
            dockPane.TabClosed += DockPane_TabClosed;
        }

        private void DockPane_TabClosed(object sender, Events.TabClosedEventArgs e)
        {
            System.Diagnostics.Trace.Assert(e.UserControl.DataContext is IViewModel);
            if (sender is DocumentPaneGroup)
            {
                System.Diagnostics.Trace.Assert(DocumentsSource.Contains(e.UserControl.DataContext as IViewModel));
                DocumentsSource.Remove(e.UserControl.DataContext as IViewModel);
            }
            else if (sender is ToolPaneGroup)
            {
                System.Diagnostics.Trace.Assert(ToolsSource.Contains(e.UserControl.DataContext as IViewModel));
                ToolsSource.Remove(e.UserControl.DataContext as IViewModel);
            }
        }

        DocumentPaneGroup ILayoutFactory.MakeDocumentPaneGroup()
        {
            DocumentPaneGroup documentPaneGroup = new DocumentPaneGroup();
            UpdateProperties(documentPaneGroup);
            RegisterDockPane(documentPaneGroup);
            return documentPaneGroup;
        }

        ToolPaneGroup ILayoutFactory.MakeToolPaneGroup()
        {
            ToolPaneGroup toolPaneGroup = new ToolPaneGroup();
            UpdateProperties(toolPaneGroup);
            RegisterDockPane(toolPaneGroup);
            toolPaneGroup.UnPinClick += ToolPane_UnPinClick;
            return toolPaneGroup;
        }

        private void RegisterFloatingPane(FloatingPane floatingPane)
        {
            floatingPane.LocationChanged += FloatingWindow_LocationChanged;
            floatingPane.Closed += FloatingPane_Closed;
            floatingPane.Ungroup += FloatingPane_Ungroup;
            floatingPane.UngroupCurrent += FloatingPane_UngroupCurrent;
            floatingPane.DataContext = new FloatingViewModel();
            (floatingPane.DataContext as FloatingViewModel).Title = floatingPane.Title;
            floatingPane.EndDrag += FloatingPane_EndDrag;
            // Ensure the window remains on top of the main window
            floatingPane.Owner = Application.Current.MainWindow;
            floatingPane.Show();
        }

        FloatingDocumentPaneGroup ILayoutFactory.MakeFloatingDocumentPaneGroup()
        {
            FloatingDocumentPaneGroup floatingDocumentPaneGroup = new FloatingDocumentPaneGroup();
            UpdateProperties(floatingDocumentPaneGroup);
            RegisterFloatingPane(floatingDocumentPaneGroup);
            FloatingDocumentPaneGroups.Add(floatingDocumentPaneGroup);
            return floatingDocumentPaneGroup;
        }

        FloatingToolPaneGroup ILayoutFactory.MakeFloatingToolPaneGroup()
        {
            FloatingToolPaneGroup floatingToolPaneGroup = new FloatingToolPaneGroup();
            UpdateProperties(floatingToolPaneGroup);
            RegisterFloatingPane(floatingToolPaneGroup);
            FloatingToolPaneGroups.Add(floatingToolPaneGroup);
            return floatingToolPaneGroup;
        }

        void ILayoutFactory.MakeUnpinnedToolPaneGroup(WindowLocation windowLocation, ToolPaneGroup toolPaneGroup, string siblingGuid, bool isHorizontal, bool isFirst)
        {
            System.Diagnostics.Trace.Assert(_dictToolListBoxes.ContainsKey(windowLocation));

            Controls.ToolListBox toolListBox = _dictToolListBoxes[windowLocation];

            UnpinnedToolData unpinnedToolData = new UnpinnedToolData();
            unpinnedToolData.ToolPaneGroup = toolPaneGroup;
            unpinnedToolData.SiblingGuid = new Guid(siblingGuid);
            unpinnedToolData.IsFirst = isFirst;
            unpinnedToolData.IsHorizontal = isHorizontal;

            AddUnpinnedToolData(unpinnedToolData, windowLocation, toolListBox);
        }

        string ILayoutFactory.MakeDocumentKey(string contentId, string Url)
        {
            return contentId + "," + Url;
        }

        void ILayoutFactory.SetRootPane(Grid grid, out int row, out int column)
        {
            IDockPaneTree.RootPane = grid;
            row = Grid.GetRow(grid);
            column = Grid.GetColumn(grid);
        }

        #endregion ILayoutFactory

        #region IDockPaneTree

        void IDockPaneTree.FrameworkElementRemoved(FrameworkElement frameworkElement)
        {
            foreach (var keyValuePair in _dictUnpinnedToolData)
            {
                foreach (var item in keyValuePair.Value)
                {
                    if (item.SiblingGuid == (Guid)frameworkElement.Tag)
                    {
                        item.SiblingGuid = (Guid)(frameworkElement.Parent as FrameworkElement).Tag;
                    }
                }
            }
        }

        Grid IDockPaneTree.RootPane
        {
            get
            {
                return _root;
            }
            set
            {

                if ((_root != null) && Children.Contains(_root))
                {
                    Children.Remove(_root);
                }
                _root = value;
                Children.Add(_root);
                Grid.SetRow(_root, 1);
                Grid.SetColumn(_root, 1);
            }
        }
        
        Grid IDockPaneTree.ParentGrid 
        { 
            get
            {
                return this;
            }
        }

        #endregion IDockPaneTree

        private void Create()
        {
            Clear();

            /*
             * We descend the tree level by level, adding a new level when the current one is full. 
             * We continue adding nodes until we have exhausted the items in views (created above). 
             * 
                    Parent              Level Index
                       G                    1   
                 /           \              
                 G           G              2 
              /     \     /     \           
              D     D     D     D           3
              
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
            (_root as SplitterPane).AddChild(documentPanel, true);

            List<UserControl> documentViews = LoadViewsFromTemplates(DocumentTemplates, DocumentsSource);

            if ((documentViews != null) && (documentViews.Count > 0))
            {
                List<FrameworkElement> list_N = new List<FrameworkElement>();

                DockManager.DockPane documentPane = ILayoutFactory.MakeDocumentPaneGroup();
                documentPane.IViewContainer.AddUserControl(documentViews[0]);

                documentPanel.Children.Add(documentPane);
                list_N.Add(documentPane);
                IDockPaneTreeManager.AddViews(documentViews, list_N, delegate { return ILayoutFactory.MakeDocumentPaneGroup(); });
            }

            List<UserControl> toolViews = LoadViewsFromTemplates(ToolTemplates, ToolsSource);
            if ((toolViews != null) && (toolViews.Count > 0))
            {
                List<FrameworkElement> list_N = new List<FrameworkElement>();

                DockManager.DockPane toolPaneGroup = ILayoutFactory.MakeToolPaneGroup();
                toolPaneGroup.IViewContainer.AddUserControl(toolViews[0]);

                (_root as SplitterPane).AddChild(toolPaneGroup, false);

                list_N.Add(toolPaneGroup);
                IDockPaneTreeManager.AddViews(toolViews, list_N, delegate { return ILayoutFactory.MakeToolPaneGroup(); });
            }

            UpdateLayout();
        }

        public bool SaveLayout(out XmlDocument xmlDocument, string fileNameAndPath)
        {
            xmlDocument = new XmlDocument();

            if (Children.Count == 0)
            {
                return false;
            }

            Serialisation.LayoutWriter.SaveLayout(
                xmlDocument, 
                _root, 
                FloatingToolPaneGroups, 
                FloatingDocumentPaneGroups, 
                _dictUnpinnedToolData);

            xmlDocument.Save(fileNameAndPath);

            return true;
        }

        public bool LoadLayout(out XmlDocument xmlDocument, string fileNameAndPath)
        {
            Clear();

            xmlDocument = new XmlDocument();
            xmlDocument.Load(fileNameAndPath);

            if (xmlDocument.ChildNodes.Count == 0)
            {
                return false;
            }

            List<UserControl> views = new List<UserControl>();
            Dictionary<string, UserControl> viewsMap = new Dictionary<string, UserControl>();

            List<UserControl> documentViews = LoadViewsFromTemplates(DocumentTemplates, DocumentsSource);
            foreach (var item in documentViews)
            {
                viewsMap.Add(ILayoutFactory.MakeDocumentKey(item.Name, ((item.DataContext) as IViewModel).URL), item);
            }

            List<UserControl> toolViews = LoadViewsFromTemplates(ToolTemplates, ToolsSource);
            foreach (var item in toolViews)
            {
                viewsMap.Add(item.Name, item);
            }

            // Now load the views into the dock manager => one or more views might not be visible!

            Serialisation.LayoutReader.LoadNode(this, viewsMap, this, this, xmlDocument.DocumentElement, true);

            // Remove any view without a view model 

            ValidateToolPanes();
            ValidateDocumentPanes();

            return true;
        }

        private void DockPane_Ungroup(object sender, EventArgs e)
        {
            DockPane dockPane = sender as DockPane;
            var parentGrid = dockPane.Parent as Grid;

            double paneWidth = dockPane.ActualWidth / dockPane.IViewContainer.GetUserControlCount();
            while (IDockPaneTreeManager.UngroupDockPane(dockPane, 1, paneWidth))
            {
                // Nothing here
            }
        }

        private void DockPane_UngroupCurrent(object sender, EventArgs e)
        {
            System.Diagnostics.Trace.Assert(sender is DockPane);

            DockPane dockPane = sender as DockPane;

            double paneWidth = dockPane.ActualWidth / 2;
            int index = dockPane.IViewContainer.GetCurrentTabIndex();
            if (index > -1)
            {
                IDockPaneTreeManager.UngroupDockPane(dockPane, index, paneWidth);
            }
        }

        private void DockPane_Float(object sender, Events.FloatEventArgs e)
        {
            System.Diagnostics.Trace.Assert(sender is DockPane);

            IDockPaneTreeManager.Float(sender as DockPane, e.Drag, false);
        }

        private void DockPane_FloatTabRequest(object sender, EventArgs e)
        {
            System.Diagnostics.Trace.Assert(sender is DockPane);

            IDockPaneTreeManager.Float(sender as DockPane, true, true);
        }

        private void DockPane_CloseRequest(object sender, EventArgs e)
        {
            DockPane dockPane = sender as DockPane;

            if (dockPane == null)
            {
                return;
            }

            IDockPaneTreeManager.ExtractDockPane(dockPane, out FrameworkElement frameworkElement);

            IViewContainer iViewContainer = dockPane.IViewContainer;
            while (true)
            {
                UserControl userControl = iViewContainer.ExtractUserControl(0);
                if (userControl == null)
                {
                    break;
                }
                IViewModel iViewModel = (userControl.DataContext as IViewModel);
                if (dockPane is DocumentPaneGroup)
                {
                    System.Diagnostics.Trace.Assert(DocumentsSource.Contains(iViewModel));
                    DocumentsSource.Remove(iViewModel);
                    DocumentClosed?.Invoke(iViewModel, null);
                }
                else if (dockPane is ToolPaneGroup)
                {
                    System.Diagnostics.Trace.Assert(ToolsSource.Contains(iViewModel));
                    ToolsSource.Remove(iViewModel);
                    ToolClosed?.Invoke(iViewModel, null);
                }
            }
        }

        private void FloatingPane_Closed(object sender, EventArgs e)
        {
            if ((sender is FloatingToolPaneGroup) && (FloatingToolPaneGroups.Contains(sender as FloatingToolPaneGroup)))
            {
                FloatingToolPaneGroups.Remove(sender as FloatingToolPaneGroup);
            }

            if ((sender is FloatingDocumentPaneGroup) && (FloatingDocumentPaneGroups.Contains(sender as FloatingDocumentPaneGroup)))
            {
                FloatingDocumentPaneGroups.Remove(sender as FloatingDocumentPaneGroup);
            }
        }

        private FloatingPane UnGroupFloatingPane(FloatingPane floatingPane, int index, double left, double top)
        {
            if (floatingPane == null)
            {
                return null;
            }

            UserControl userControl = floatingPane.IViewContainer.ExtractUserControl(index);
            if (userControl == null)
            {
                return null;
            }

            FloatingPane newFloatingPane = null;
            if (floatingPane is FloatingToolPaneGroup)
            {
                newFloatingPane = ILayoutFactory.MakeFloatingToolPaneGroup();
            }
            else
            {
                newFloatingPane = ILayoutFactory.MakeFloatingDocumentPaneGroup();
            }

            newFloatingPane.Left = left;
            newFloatingPane.Top = top;
            newFloatingPane.IViewContainer.AddUserControl(userControl);

            return floatingPane;
        }

        private void FloatingPane_Ungroup(object sender, EventArgs e)
        {
            System.Diagnostics.Trace.Assert(sender is FloatingPane);

            FloatingPane floatingPane = sender as FloatingPane;

            int viewCount = floatingPane.IViewContainer.GetUserControlCount();

            double left = floatingPane.Left;
            double top = floatingPane.Top;

            // Warning warning => off screen?
            for (int index = 1; index < viewCount; ++index)
            {
                left += 10;
                top += 10;
                if (UnGroupFloatingPane(floatingPane, 1, left, top) == null)
                {
                    return;
                }
            }
        }

        private void FloatingPane_UngroupCurrent(object sender, EventArgs e)
        {
            System.Diagnostics.Trace.Assert(sender is FloatingPane);

            FloatingPane floatingPane = sender as FloatingPane;

            int index = floatingPane.IViewContainer.GetCurrentTabIndex();
            if (index > -1)
            {
                UnGroupFloatingPane(floatingPane, index, floatingPane.Left + 10, floatingPane.Top + 10);
            }
        }

        private void CancelSelection()
        {
            if (SelectedPane != null)
            {
                SelectedPane.IsHighlighted = false;
                SelectedPane = null;
            }
            _insertionIndicatorManager?.HideInsertionIndicator();
            _windowLocationPane?.Close();
            _windowLocationPane = null;
            _sideLocationPane?.Close();
            _sideLocationPane = null;
        }

        private void FloatingPane_EndDrag(object sender, EventArgs e)
        {
            System.Diagnostics.Trace.Assert(sender is FloatingPane, System.Reflection.MethodBase.GetCurrentMethod().Name + ": sender not a FloatingPane");
            Application.Current.Dispatcher.Invoke(delegate
            {
                FloatingPane floatingPane = sender as FloatingPane;

                if (
                        (floatingPane == null) ||
                        ((SelectedPane != null) && !(SelectedPane.Parent is SplitterPane) && !(SelectedPane.Parent is DocumentPanel) && (SelectedPane.Parent != this)) ||
                        (_insertionIndicatorManager == null) ||
                        (_insertionIndicatorManager.WindowLocation == WindowLocation.None)
                   )
                {
                    return;
                }

                SelectablePane selectedPane = SelectedPane;
                WindowLocation windowLocation = _insertionIndicatorManager.WindowLocation;
                CancelSelection();

                IDockPaneTreeManager.Unfloat(floatingPane, selectedPane, windowLocation);

                Application.Current.MainWindow.Activate();
            });
        }

        private WindowLocationPane _windowLocationPane;
        private SideLocationPane _sideLocationPane;
        private InsertionIndicatorManager _insertionIndicatorManager;

        private void FloatingWindow_LocationChanged(object sender, EventArgs e)
        {
            System.Diagnostics.Trace.Assert(sender is Window);

            Window floatingWindow = sender as Window;
            Point cursorPositionOnScreen = WpfOpenControls.Controls.Utilities.GetCursorPosition();

            bool foundSelectedPane = false;
            Point cursorPositionInMainWindow = PointFromScreen(cursorPositionOnScreen);
            if (
                    (cursorPositionInMainWindow.X >= 0) &&
                    (cursorPositionInMainWindow.X <= this.ActualWidth) && 
                    (cursorPositionInMainWindow.Y >= 0) &&
                    (cursorPositionInMainWindow.Y <= this.ActualHeight) 
                )
            {
                Type paneType = (sender is FloatingDocumentPaneGroup) ? typeof(DocumentPaneGroup) : typeof(ToolPaneGroup);
                var pane = IDockPaneTreeManager.FindSelectablePane(this, cursorPositionOnScreen);
                foundSelectedPane = pane != null;
                if ((pane != null) && (SelectedPane != pane))
                {
                    if (SelectedPane != null)
                    {
                        SelectedPane.IsHighlighted = false;
                    }

                    pane.IsHighlighted = true;
                    SelectedPane = pane;
                    if (_windowLocationPane != null)
                    {
                        _windowLocationPane.Close();
                        _windowLocationPane = null;
                    }

                    if ((paneType == pane.GetType()) || ((pane is DocumentPanel) && (sender is FloatingDocumentPaneGroup)))
                    {
                        _windowLocationPane = new WindowLocationPane();
                        _windowLocationPane.AllowsTransparency = true;
                        if (SelectedPane is DocumentPanel)
                        {
                            _windowLocationPane.ShowIcons(WindowLocation.Middle);
                        }
                        Point topLeftPanePoint = pane.PointToScreen(new Point(0, 0));
                        _windowLocationPane.Left = topLeftPanePoint.X;
                        _windowLocationPane.Top = topLeftPanePoint.Y;
                        _windowLocationPane.Width = SelectedPane.ActualWidth;
                        _windowLocationPane.Height = SelectedPane.ActualHeight;
                        _windowLocationPane.Show();
                    }
                }

                if (sender is FloatingToolPaneGroup)
                {
                    if (_sideLocationPane == null)
                    {
                        _sideLocationPane = new SideLocationPane();
                        _sideLocationPane.AllowsTransparency = true;
                    }

                    Point topLeftRootPoint = _root.PointToScreen(new Point(0, 0));
                    _sideLocationPane.Left = topLeftRootPoint.X;
                    _sideLocationPane.Top = topLeftRootPoint.Y;
                    _sideLocationPane.Width = _root.ActualWidth;
                    _sideLocationPane.Height = _root.ActualHeight;
                    _sideLocationPane.Show();
                }
            }
            else
            {
                if (_sideLocationPane != null)
                {
                    _sideLocationPane.Close();
                    _sideLocationPane = null;
                }
            }

            SelectablePane previousPane = SelectedPane;

            if (!foundSelectedPane)
            {
                SelectedPane = null;
            }

            if (!foundSelectedPane)
            {
                if (_windowLocationPane != null)
                {
                    _windowLocationPane.Close();
                    _windowLocationPane = null;
                }
           }

            if ((previousPane != null) && (SelectedPane != previousPane))
            {
                previousPane.IsHighlighted = false;
            }

            WindowLocation windowLocation = WindowLocation.None;

            if (_sideLocationPane != null)
            {
                windowLocation = _sideLocationPane.TrySelectIndicator(cursorPositionOnScreen);
                switch (windowLocation)
                {
                    case WindowLocation.LeftSide:
                    case WindowLocation.RightSide:
                    case WindowLocation.TopSide:
                    case WindowLocation.BottomSide:
                        if ((_insertionIndicatorManager != null) && (_insertionIndicatorManager.ParentGrid != this))
                        {
                            _insertionIndicatorManager.HideInsertionIndicator();
                            _insertionIndicatorManager = null;
                        }
                        if (_insertionIndicatorManager == null)
                        {
                            _insertionIndicatorManager = new InsertionIndicatorManager(this);
                        }
                        _insertionIndicatorManager.ShowInsertionIndicator(windowLocation);
                        return;
                }
            }

            if ((_windowLocationPane != null) && (SelectedPane != null))
            {
                windowLocation = _windowLocationPane.TrySelectIndicator(cursorPositionOnScreen);
                if ((_insertionIndicatorManager != null) && (_insertionIndicatorManager.ParentGrid != SelectedPane))
                {
                    _insertionIndicatorManager.HideInsertionIndicator();
                    _insertionIndicatorManager = null;
                }
                if (_insertionIndicatorManager == null)
                {
                    _insertionIndicatorManager = new InsertionIndicatorManager(SelectedPane);
                }

                _insertionIndicatorManager.ShowInsertionIndicator(windowLocation);
            }
        }
    }
}
