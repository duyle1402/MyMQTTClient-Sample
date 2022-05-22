using HMIControl;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace AS_MQTTClient.Controls
{
    public class HMISwitch : ButtonBase
    {
        static HMISwitch()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(HMISwitch), new FrameworkPropertyMetadata(typeof(HMISwitch)));
        } 
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (IsPulse)
            {
                if (_funcWrites.Count > 0)
                    _funcWrites.ForEach(x => x(false));
            }
            e.Handled = false;
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (IsPulse)
            {
                if (_funcWrites.Count > 0)
                    _funcWrites.ForEach(x => x(true));
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!IsPulse)
            {
                if (base.IsMouseCaptured)
                {
                    e.Handled = true;
                    double num = base.ActualWidth / 2.0;
                    Point position = Mouse.GetPosition(this);
                    if (position.X < num)
                    {
                        IsChecked = false;
                    }
                    else if (position.X > (  num))
                    {
                        IsChecked = true;
                    }
                    
                    foreach (var item in _funcints)
                    {
                        _funcints.ForEach(x => x());
                    }                  
                }
            }
        }
        protected override void OnCheckedChanged(bool? oldstat, bool? newstat)
        {
            if (newstat.HasValue)
            {
                VisualStateManager.GoToState(this, newstat.Value == true ? "ON" : "OFF", true);
            }           
        }     
    }
}
