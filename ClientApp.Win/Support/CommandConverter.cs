using MaterialDesignThemes.Wpf;
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace Streamster.ClientApp.Win.Support
{
    public class CommandConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Action a)
                return new TransientCommand(a);
            else if (value is Action<object> b)
                return new TransientCommand(b);

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class CloseDialogCommandConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new TransientCommand(() =>
            {
                ((ICommand)DialogHost.CloseDialogCommand).Execute(parameter);
                ((Action)value)();
            });
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class DelayedCloseDialogCommandConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new TransientCommand(async() =>
            {
                await Task.Delay(300);
                ((ICommand)DialogHost.CloseDialogCommand).Execute(parameter);
                ((Action)value)();
            });
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    class TransientCommand : ICommand
    {
        private readonly Action _action;
        private readonly Action<object> _action2;

        public TransientCommand(Action action)
        {
            _action = action;
        }

        public TransientCommand(Action<object> action)
        {
            _action2 = action;
        }

        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            if (_action == null)
                _action2(parameter);
            else _action();
        }
    }
}
