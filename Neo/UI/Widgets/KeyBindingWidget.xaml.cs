﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using Neo.Editing;
using Neo.Scene;
using Neo.Settings;

namespace Neo.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for KeyBindingWidget.xaml
    /// </summary>
    public partial class KeyBindingWidget
    {
        private static readonly KeysConverter Converter = new KeysConverter();
        private bool mInitialized;
        private KeyBindingControl mCurrentBinding;
        private readonly List<Key> mCurrentKeys = new List<Key>();
        private readonly List<Key> mCurrentPressedKeys = new List<Key>();

        public KeyBindingWidget()
        {
            InitializeComponent();
        }

        private void ElementClicked(object sender, EventArgs args)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button == null)
            {
	            return;
            }

	        var binding = button.Tag as KeyBindingControl;
            System.Windows.Controls.Border border;

            if (binding != null && Equals(binding, this.mCurrentBinding))
            {
                border = this.mCurrentBinding.Label.Parent as System.Windows.Controls.Border;
                if(border != null)
                {
	                border.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                }
	            this.mCurrentBinding = null;
                return;
            }

            if (binding == null)
            {
	            return;
            }

	        if (this.mCurrentBinding != null)
            {
                border = this.mCurrentBinding.Label.Parent as System.Windows.Controls.Border;
                if (border != null)
                {
	                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                }
            }

            border = binding.Label.Parent as System.Windows.Controls.Border;
            if (border != null)
            {
	            border.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            }

	        this.mCurrentBinding = binding;
	        this.mCurrentKeys.Clear();
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs args)
        {
            if (this.mCurrentPressedKeys.Contains(args.Key))
            {
	            return;
            }

	        if (this.mCurrentBinding == null)
	        {
		        return;
	        }

	        this.mCurrentKeys.Add(args.Key);
	        this.mCurrentPressedKeys.Add(args.Key);
	        this.mCurrentBinding.Label.Text = string.Join(" + ", this.mCurrentKeys.Select(k => Converter.ConvertToString(k)));
        }

        private void OnKeyUp(object sender, System.Windows.Input.KeyEventArgs args)
        {
            if (this.mCurrentBinding == null)
            {
	            return;
            }

	        this.mCurrentPressedKeys.Remove(args.Key);
            if (this.mCurrentPressedKeys.Count != 0)
            {
	            return;
            }

	        var border = this.mCurrentBinding.Label.Parent as System.Windows.Controls.Border;
            if (border != null)
            {
	            border.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            }
	        var bindField = this.mCurrentBinding.Tag as Tuple<FieldInfo, object>;
            if (bindField != null && this.mCurrentKeys.Count > 0)
            {
                bindField.Item1.SetValue(bindField.Item2, this.mCurrentKeys.Select(k => (Keys)KeyInterop.VirtualKeyFromKey(k)).ToArray());
                KeyBindings.Save();
            }

	        this.mCurrentBinding = null;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
	        this.SensitivitySliderIndicator.Text = (this.SensitivitySlider.Value / 5.0f).ToString("F2");

            if (DesignerProperties.GetIsInDesignMode(this))
            {
	            return;
            }

	        if (this.mInitialized)
	        {
		        return;
	        }

	        if (DesignerProperties.GetIsInDesignMode(this))
	        {
		        return;
	        }

	        this.mInitialized = true;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            var baseType = typeof(KeyBindings);
            foreach (var category in baseType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var catName = category.Name;
                var instance = category.GetValue(KeyBindings.Instance);

                foreach (var binding in instance.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (binding.FieldType.IsArray == false)
                    {
	                    continue;
                    }

	                var arrType = binding.FieldType.GetElementType();
                    if (arrType != typeof(Keys))
                    {
	                    continue;
                    }

	                var elemName = binding.Name;
                    var keys = (Keys[])binding.GetValue(instance);
                    var control = new KeyBindingControl
                    {
                        Tag = new Tuple<FieldInfo, object>(binding, instance),
                        Button = { Content = string.Format("{0}.{1}", catName, elemName) },
                        Label = { Text = string.Join(" + ", keys.Select(k => Converter.ConvertToString(k))) }
                    };

                    control.Button.Tag = control;

                    control.Button.Click += ElementClicked;
                    InputElementWrapper.Children.Add(control);
                }
            }
        }

        private void InvertMouseBox_Clicked(object sender, RoutedEventArgs e)
        {
           var cb = sender as System.Windows.Controls.CheckBox;
            if (cb == null)
            {
	            return;
            }

	        WorldFrame.Instance.CamControl.InvertY = !WorldFrame.Instance.CamControl.InvertY;
        }

        private void InvertMouseBoxX_Clicked(object sender, RoutedEventArgs e)
        {
            var cb = sender as System.Windows.Controls.CheckBox;
            if (cb == null)
            {
	            return;
            }

	        WorldFrame.Instance.CamControl.InvertX = !WorldFrame.Instance.CamControl.InvertX;
        }

        private void SensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var slider = sender as System.Windows.Controls.Slider;
            if (slider == null)
            {
	            return;
            }


	        var newValue = slider.Value / 5.0f;
            if (this.SensitivitySliderIndicator != null)
            {
	            this.SensitivitySliderIndicator.Text = newValue.ToString("F2");
            }

	        if (WorldFrame.Instance == null || WorldFrame.Instance.CamControl == null)
	        {
		        return;
	        }

	        WorldFrame.Instance.CamControl.TurnFactor = (float)newValue * 0.2f;
        }

        private void reconnect_Click(object sender, RoutedEventArgs e)
        {
            TabletManager.Instance.TryConnect();
            // Check if the tablet is connected
            if (TabletManager.Instance.IsConnected)
            {
                // if the tablet is connected hide reconnect button
	            this.reconnect.Visibility = Visibility.Hidden;
            }
        }

        private void UserControl_GotFocus(object sender, RoutedEventArgs e)
        {
            // Check if the tablet is connected
            if (TabletManager.Instance.IsConnected)
            {
                // if the tablet is connected hide reconnect button
	            this.reconnect.Visibility = Visibility.Hidden;
            }
            else {
	            this.reconnect.Visibility = Visibility.Visible;
            }
        }
    }
}