using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace Streamster.ClientCore.Support
{
    public class ValidatedProperty<T> : INotifyPropertyChanged
    { 
        private bool _isValid;

        private T _value;
        private readonly string _propertyName;

        public event PropertyChangedEventHandler PropertyChanged;

        public ValidatedProperty(T val = default, [CallerMemberName] string propertyName = null)
        {
            _value = val;
            _propertyName = propertyName;
        }

        public Action<T, T> OnChange { get; set; }

        public Func<T, bool> Validate { get; set; }

        public T Value
        {
            get => _value;
            set
            {
                var old = _value;
                _value = value;

                if (Validate != null)
                    IsValid = Validate(value);

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

        public bool IsValid
        {
            get => _isValid;
            set
            {
                _isValid = value;
                RaisePropertyChanged(nameof(IsValid));
            }
        }
    }
}
