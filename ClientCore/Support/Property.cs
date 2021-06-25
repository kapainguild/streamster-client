using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Streamster.ClientCore
{
    public class Property<T> : INotifyPropertyChanged
    {
        private T _value;
        private readonly string _propertyName;

        public event PropertyChangedEventHandler PropertyChanged;

        public Property(T val = default, [CallerMemberName] string propertyName = null)
        {
            _value = val;
            _propertyName = propertyName;
        }

        public Action<T, T> OnChange { get; set; }

        public T Value
        {
            get => _value;
            set
            {
                var old = _value;
                _value = value;
                OnChange?.Invoke(old, _value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Value)));
            }
        }

        public T SilentValue
        {
            get => _value;
            set
            {
                var old = _value;
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Value)));
            }
        }

        protected void RaisePropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void SetValueAsMethod(T value)
        {
            Value = value;
        }

        public override string ToString() => $"{_propertyName} = {_value}";
    }
}
