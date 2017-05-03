﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TabControl.cs" company="WildGums">
//   Copyright (c) 2008 - 2017 WildGums. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------


namespace Orc.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using Catel.Windows.Data;

    /// <summary>
    /// Load behavior of the tabs in the <see cref="TabControl"/>.
    /// </summary>
    public enum LoadTabItemsBehavior
    {
        /// <summary>
        /// Load all tabs using lazy loading, but keeps the tabs in memory afterwards.
        /// </summary>
        LazyLoading,

        /// <summary>
        /// Load all tabs using lazy loading. As soon as a tab is loaded, all other loaded tabs will be unloaded.
        /// </summary>
        LazyLoadingUnloadOthers,

        /// <summary>
        /// Load all tabs as soon as the tab control is loaded.
        /// </summary>
        EagerLoading,

        /// <summary>
        /// Load all tabs when any of the tabs is used for the first time.
        /// </summary>
        EagerLoadingOnFirstUse,
    }

    /// <summary>
    /// Item data for a tab control item.
    /// </summary>
    public class TabControlItemData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TabControlItemData" /> class.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="content">The content.</param>
        /// <param name="contentTemplate">The content template.</param>
        /// <param name="item">The item.</param>
        public TabControlItemData(object container, object content, DataTemplate contentTemplate, object item)
        {
            Container = container;
            TabItem = container as TabItem;

            if (TabItem != null)
            {
#pragma warning disable WPF0041 // Set mutable dependency properties using SetCurrentValue.
                TabItem.Content = null;
                TabItem.ContentTemplate = null;
#pragma warning restore WPF0041 // Set mutable dependency properties using SetCurrentValue.
            }

            Content = content;
            ContentTemplate = contentTemplate;
            Item = item;
        }

        /// <summary>
        /// Gets the container.
        /// </summary>
        /// <value>The container.</value>
        public object Container { get; private set; }

        /// <summary>
        /// Gets the tab item.
        /// </summary>
        /// <value>The tab item.</value>
        public TabItem TabItem { get; private set; }

        /// <summary>
        /// Gets the content.
        /// </summary>
        /// <value>The content.</value>
        public object Content { get; private set; }

        /// <summary>
        /// Gets the content template.
        /// </summary>
        /// <value>The content.</value>
        public DataTemplate ContentTemplate { get; private set; }

        /// <summary>
        /// The item from which it was generated.
        /// </summary>
        /// <value>The item.</value>
        public object Item { get; private set; }
    }

    /// <summary>
    /// TabControl that will not remove the tab items from the visual tree. This way, views can be re-used.
    /// </summary>
    [TemplatePart(Name = "PART_ItemsHolder", Type = typeof(Panel))]
    public class TabControl : System.Windows.Controls.TabControl
    {
        /// <summary>
        /// Dependency property registration for the <see cref="LoadTabItems"/> property.
        /// </summary>
        public static readonly DependencyProperty LoadTabItemsProperty = DependencyProperty.Register("LoadTabItems",
            typeof(LoadTabItemsBehavior), typeof(TabControl), new PropertyMetadata(LoadTabItemsBehavior.LazyLoading,
                (sender, e) => ((TabControl) sender).OnLoadTabItemsChanged()));

        private readonly ConditionalWeakTable<object, object> _wrappedContainers = new ConditionalWeakTable<object, object>();
        private Panel _itemsHolder;

#if NET

        /// <summary>
        /// Initializes a new instance of the <see cref="TabControl"/>.class.
        /// </summary>
        static TabControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TabControl), new FrameworkPropertyMetadata(typeof(TabControl)));
        }

#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Windows.Controls.TabControl"/>.class.
        /// </summary>
        /// <remarks></remarks>
        public TabControl()
        {
            // this is necessary so that we get the initial databound selected item
            ItemContainerGenerator.StatusChanged += OnItemContainerGeneratorStatusChanged;
            Loaded += OnTabControlLoaded;

            this.SubscribeToDependencyProperty("SelectedItem", OnSelectedItemChanged);
        }

        /// <summary>
        /// Gets or sets the load tab items.
        /// <para />
        /// The default value is <see cref="LoadTabItemsBehavior.LazyLoading"/>.
        /// </summary>
        /// <value>
        /// The load tab items.
        /// </value>
        public LoadTabItemsBehavior LoadTabItems
        {
            get { return (LoadTabItemsBehavior) GetValue(LoadTabItemsProperty); }
            set { SetValue(LoadTabItemsProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this tab control uses any of the lazy loading options.
        /// </summary>
        /// <value><c>true</c> if this instance is lazy loading; otherwise, <c>false</c>.</value>
        public bool IsLazyLoading
        {
            get
            {
                var loadTabItems = LoadTabItems;
                return loadTabItems == LoadTabItemsBehavior.LazyLoading || loadTabItems == LoadTabItemsBehavior.LazyLoadingUnloadOthers;
            }
        }

        /// <summary>
        /// Called when the tab control is loaded.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs"/> instance containing the event data.</param>
        private void OnTabControlLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnTabControlLoaded;

            InitializeItems();
        }

        /// <summary>
        /// If containers are done, generate the selected item.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void OnItemContainerGeneratorStatusChanged(object sender, EventArgs e)
        {
            if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                ItemContainerGenerator.StatusChanged -= OnItemContainerGeneratorStatusChanged;

                InitializeItems();
            }
        }

        /// <summary>
        /// Get the ItemsHolder and generate any children.
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _itemsHolder = GetTemplateChild("PART_ItemsHolder") as Panel;

            InitializeItems();
        }

        private void OnLoadTabItemsChanged()
        {
            InitializeItems();
        }

        /// <summary>
        /// When the items change we remove any generated panel children and add any new ones as necessary
        /// </summary>
        /// <param name="e">The event data for the <see cref="E:System.Windows.Controls.ItemContainerGenerator.ItemsChanged"/> event.</param>
        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);

            if (_itemsHolder == null)
            {
                return;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    _itemsHolder.Children.Clear();
                    break;

                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    if (e.OldItems != null)
                    {
                        foreach (var item in e.OldItems)
                        {
                            var cp = FindChildContentPresenter(item);
                            if (cp != null)
                            {
                                _itemsHolder.Children.Remove(cp);
                            }
                        }
                    }

                    // don't do anything with new items because we don't want to
                    // create visuals that aren't being shown
                    break;
            }

            InitializeItems();
        }

        private void OnSelectedItemChanged(object sender, DependencyPropertyValueChangedEventArgs e)
        {
            UpdateItems();
        }

        private void InitializeItems()
        {
            if (_itemsHolder == null)
            {
                return;
            }

            var items = Items;
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                if (item != null)
                {
                    CreateChildContentPresenter(item);
                }
            }

            var loadAllItems = (LoadTabItems == LoadTabItemsBehavior.EagerLoading) || (IsLoaded && !IsLazyLoading);
            if (loadAllItems)
            {
                foreach (ContentPresenter child in _itemsHolder.Children)
                {
                    var tabControlItemData = child.Tag as TabControlItemData;
                    if (tabControlItemData != null)
                    {
                        var tabItem = tabControlItemData.TabItem;
                        if (tabItem != null)
                        {
                            ShowChildContent(child, tabControlItemData);

                            // Collapsed is hidden + not loaded
#pragma warning disable WPF0041 // Set mutable dependency properties using SetCurrentValue.
                            child.Visibility = Visibility.Collapsed;
#pragma warning restore WPF0041 // Set mutable dependency properties using SetCurrentValue.

                            if (LoadTabItems == LoadTabItemsBehavior.EagerLoading)
                            {
                                EagerLoadAllTabs();
                            }
                        }
                    }
                }
            }

            if (SelectedItem != null)
            {
                UpdateItems();
            }
        }

        private void EagerLoadAllTabs()
        {
            if (_itemsHolder == null)
            {
                return;
            }

            foreach (ContentPresenter child in _itemsHolder.Children)
            {
                var tabControlItemData = child.Tag as TabControlItemData;
                if (tabControlItemData != null)
                {
                    var tabItem = tabControlItemData.TabItem;
                    if (tabItem != null)
                    {
                        ShowChildContent(child, tabControlItemData);
                    }
                }

                // Always start invisible, the selection will take care of visibility
#pragma warning disable WPF0041 // Set mutable dependency properties using SetCurrentValue.
                child.Visibility = Visibility.Hidden;
#pragma warning restore WPF0041 // Set mutable dependency properties using SetCurrentValue.
            }
        }

        private void UpdateItems()
        {
            if (_itemsHolder == null)
            {
                return;
            }

            var items = Items;
            if (items == null)
            {
                return;
            }

            if (SelectedItem != null)
            {
                if (!IsLazyLoading)
                {
                    EagerLoadAllTabs();
                }
            }

            // Show the right child first (to prevent flickering)
            var itemsToHide = new Dictionary<ContentPresenter, TabControlItemData>();

            foreach (ContentPresenter child in _itemsHolder.Children)
            {
                var tabControlItemData = child.Tag as TabControlItemData;
                if (tabControlItemData != null)
                {
                    var tabItem = tabControlItemData.TabItem;
                    if (tabItem != null && tabItem.IsSelected)
                    {
                        if (child.Content == null)
                        {
                            ShowChildContent(child, tabControlItemData);
                        }

#pragma warning disable WPF0041 // Set mutable dependency properties using SetCurrentValue.
                        child.Visibility = Visibility.Visible;
#pragma warning restore WPF0041 // Set mutable dependency properties using SetCurrentValue.
                    }
                    else
                    {
                        itemsToHide.Add(child, tabControlItemData);
                    }
                }
            }

            // Now hide so we have prevented flickering
            foreach (var itemToHide in itemsToHide)
            {
                var child = itemToHide;

                // Note: hidden, not collapsed otherwise items will not be loaded
#pragma warning disable WPF0041 // Set mutable dependency properties using SetCurrentValue.
                child.Key.Visibility = Visibility.Hidden;
#pragma warning restore WPF0041 // Set mutable dependency properties using SetCurrentValue.

                if (LoadTabItems == LoadTabItemsBehavior.LazyLoadingUnloadOthers)
                {
                    HideChildContent(child.Key, child.Value);
                }
            }
        }

        /// <summary>
        /// Create the child ContentPresenter for the given item (could be data or a TabItem)
        /// </summary>
        /// <param name="item">The item.</param>
        private void CreateChildContentPresenter(object item)
        {
            if (item == null)
            {
                return;
            }

            object dummyObject = null;
            if (_wrappedContainers.TryGetValue(item, out dummyObject))
            {
                return;
            }

            _wrappedContainers.Add(item, new object());

            var cp = FindChildContentPresenter(item);
            if (cp != null)
            {
                return;
            }

            // the actual child to be added.  cp.Tag is a reference to the TabItem
            cp = new ContentPresenter();

            var container = GetContentContainer(item);
            var content = GetContent(item);

            var tabItemData = new TabControlItemData(container, content, ContentTemplate, item);

#pragma warning disable WPF0041 // Set mutable dependency properties using SetCurrentValue.
            cp.Tag = tabItemData;
#pragma warning restore WPF0041 // Set mutable dependency properties using SetCurrentValue.

            if (!IsLazyLoading)
            {
                ShowChildContent(cp, tabItemData);
            }

#pragma warning disable WPF0041 // Set mutable dependency properties using SetCurrentValue.
            cp.ContentTemplateSelector = ContentTemplateSelector;
            cp.ContentStringFormat = SelectedContentStringFormat;
#pragma warning restore WPF0041 // Set mutable dependency properties using SetCurrentValue.

            _itemsHolder.Children.Add(cp);
        }

        private object GetContent(object item)
        {
            var itemAsTabItem = item as TabItem;
            if (itemAsTabItem != null)
            {
                return itemAsTabItem.Content;
            }

            return item;
        }

        private object GetContentContainer(object item)
        {
            var itemAsTabItem = item as TabItem;
            if (itemAsTabItem != null)
            {
                return itemAsTabItem;
            }

            return ItemContainerGenerator.ContainerFromItem(item);
        }

        private void ShowChildContent(ContentPresenter child, TabControlItemData tabControlItemData)
        {
            if (child.Content == null)
            {
#pragma warning disable WPF0041 // Set mutable dependency properties using SetCurrentValue.
                child.Content = tabControlItemData.Content;
#pragma warning restore WPF0041 // Set mutable dependency properties using SetCurrentValue.
            }

            if (child.ContentTemplate == null)
            {
#pragma warning disable WPF0041 // Set mutable dependency properties using SetCurrentValue.
                child.ContentTemplate = tabControlItemData.ContentTemplate;
#pragma warning restore WPF0041 // Set mutable dependency properties using SetCurrentValue.
            }

#pragma warning disable WPF0041 // Set mutable dependency properties using SetCurrentValue.
            tabControlItemData.TabItem.Content = child;
#pragma warning restore WPF0041 // Set mutable dependency properties using SetCurrentValue.
        }

        private void HideChildContent(ContentPresenter child, TabControlItemData tabControlItemData)
        {
#pragma warning disable WPF0041 // Set mutable dependency properties using SetCurrentValue.
            child.Content = null;
            child.ContentTemplate = null;

            tabControlItemData.TabItem.Content = null;
#pragma warning restore WPF0041 // Set mutable dependency properties using SetCurrentValue.
        }

        /// <summary>
        /// Find the CP for the given object.  data could be a TabItem or a piece of data.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        private ContentPresenter FindChildContentPresenter(object data)
        {
            if (data == null)
            {
                return null;
            }

            var dataAsTabItem = data as TabItem;
            if (dataAsTabItem != null)
            {
                data = dataAsTabItem.Content;
            }

            if (_itemsHolder == null)
            {
                return null;
            }

            var existingCp = _itemsHolder.Children.Cast<ContentPresenter>().FirstOrDefault(cp => ReferenceEquals(((TabControlItemData) cp.Tag).Item, data));
            if (existingCp != null)
            {
                return existingCp;
            }

            return null;
        }

        /// <summary>
        /// Copied from TabControl; wish it were protected in that class instead of private.
        /// </summary>
        /// <returns></returns>
        protected TabItem GetSelectedTabItem()
        {
            object selectedItem = SelectedItem;
            if (selectedItem == null)
            {
                return null;
            }

            var item = selectedItem as TabItem;
            if (item == null)
            {
                item = ItemContainerGenerator.ContainerFromIndex(SelectedIndex) as TabItem;
            }

            return item;
        }
    }
}