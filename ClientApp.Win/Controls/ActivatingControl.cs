using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Streamster.ClientApp.Win.Controls
{
    public class ActivatingControl : ContentControl
    {
        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(ActivatingControl), new FrameworkPropertyMetadata(false));
        private bool _initialActivation;

        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public ActivatingControl()
        {
            Focusable = false;
            _initialActivation = true;
            IsActive = true;
            _ = StopInitialLoading();
        }

        private async Task StopInitialLoading()
        {
            await Task.Delay(1000);
            _initialActivation = false;
            Refresh();
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            Refresh();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            Refresh();
            base.OnMouseLeave(e);
        }

        private void Refresh()
        {
            IsActive = _initialActivation || IsMouseOver;
        }
    }
}
