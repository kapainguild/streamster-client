using System.Globalization;
using System.Windows.Controls;

namespace Streamster.ClientApp.Win.Support
{
    public class MustBeANumberValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var val = (value ?? "").ToString();
            if (string.IsNullOrWhiteSpace(val))
                return new ValidationResult(false, "Field is required");

            if (!int.TryParse(val, out var num))
                return new ValidationResult(false, "Must be a number");

            if (num <= 0)
                return new ValidationResult(false, "Must be > 0");

            return ValidationResult.ValidResult;
        }
    }
}
