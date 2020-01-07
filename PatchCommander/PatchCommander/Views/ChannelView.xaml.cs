using System.ComponentModel;
using System;
using MHApi.GUI;
using PatchCommander.ViewModels;
using System.Windows;

namespace PatchCommander.Views
{
    /// <summary>
    /// Interaction logic for ChannelView.xaml
    /// </summary>
    public partial class ChannelView : WindowAwareView
    {
        private ChannelViewModel _viewModel;

        public ChannelView()
        {
            InitializeComponent();
            _viewModel = ViewModel.Source as ChannelViewModel;
        }

        protected override void WindowClosing(object sender, CancelEventArgs e)
        {
            //Clean up when the window closes
            _viewModel.Dispose();
            base.WindowClosing(sender, e);
        }

        public static DependencyProperty ChannelIndexProperty = DependencyProperty.Register("ChannelIndex", typeof(int), typeof(ChannelView), new PropertyMetadata(-1, OnChannelIndexChanged), OnValidateChannelIndexProperty);

        /// <summary>
        /// The index of the electrode channel
        /// </summary>
        public int ChannelIndex
        {
            get
            {
                return (int)GetValue(ChannelIndexProperty);
            }
            set
            {
                if (value < -1 || value > 1)
                    throw new ArgumentOutOfRangeException("ChannelIndex", "ChannelIndex can be either -1, 0 or 1");
                SetValue(ChannelIndexProperty, value);
            }
        }

        private static bool OnValidateChannelIndexProperty(object data)
        {
            if (!(data is int))
                return false;
            int i = (int)data;
            if (i < -1 || i > 1)
                return false;
            return true;
        }

        private static void OnChannelIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ChannelView ch = (ChannelView)d;
            int v = (int)e.NewValue;
            ch._viewModel.ChannelIndex = v;
            if (v > -1)
                ch.groupBox.Header = string.Format("Channel {0}", v + 1);
            else
                ch.groupBox.Header = string.Format("Channel not assigned");
        }
    }

}
