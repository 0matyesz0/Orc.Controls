﻿namespace Orc.Controls
{
    using System.Windows;
    using Catel.MVVM.Views;

    /// <summary>
    /// Interaction logic for LogFilterEditorControl.xaml
    /// </summary>
    public partial class LogFilterEditorControl
    {
        public LogFilterEditorControl()
        {
            InitializeComponent();
        }

        [ViewToViewModel(MappingType = ViewToViewModelMappingType.ViewModelToView)]
        public LogFilter LogFilter
        {
            get => (LogFilter)GetValue(LogFilterProperty);
            set => SetValue(LogFilterProperty, value);
        }

        public static readonly DependencyProperty LogFilterProperty = DependencyProperty.Register(nameof(LogFilter), 
            typeof(LogFilter), typeof(LogFilterEditorControl), new PropertyMetadata(default(LogFilter)));
    }
}
