﻿/**
 * Copyright 2018, Adrien Tétar. All Rights Reserved.
 */

namespace Fonte.App.Controls
{
    using Fonte.App.Delegates;
    using Fonte.App.Interfaces;

    using System;
    using System.Collections.ObjectModel;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;

    public partial class Toolbar : UserControl
    {
        private int _selectedIndex;

        public event EventHandler CurrentItemChanged;

        public ObservableCollection<IToolbarItem> Items
        { get; }

        public int SelectedIndex
        {
            get
            {
                return _selectedIndex;
            }
            set
            {
                if (value < 0 || value >= Items.Count)
                {
                    throw new ArgumentException(string.Format(
                        "Invalid index {0} given items count {1}", value, Items.Count));
                }
                _selectedIndex = value;
                CheckActiveButton();
                CurrentItemChanged?.Invoke(this, new EventArgs());
            }
        }

        public IToolbarItem SelectedItem => Items[SelectedIndex];

        public Toolbar()
        {
            InitializeComponent();

            Items = new ObservableCollection<IToolbarItem>()
            {
                new SelectionTool(),
                new PenTool(),
                new ShapesTool(),
#if DEBUG
                new DrawingTool(),
#endif
            };
        }

        void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            SelectedIndex = 0;
        }

        void OnButtonChecked(object sender, RoutedEventArgs e)
        {
            var parent = VisualTreeHelper.GetParent((UIElement)sender);
            SelectedIndex = ItemsControl.IndexFromContainer(parent);
        }

        /**/

        void CheckActiveButton()
        {
            for (int index = 0; index < Items.Count; ++index)
            {
                var presenter = ItemsControl.ContainerFromIndex(index);
                var button = (AppBarToggleButton)VisualTreeHelper.GetChild(presenter, 0);
                button.IsChecked = index == _selectedIndex;
            }
        }
    }
}
