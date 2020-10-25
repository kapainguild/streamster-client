using Streamster.ClientCore.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Controls;

namespace Streamster.ClientApp.Win.Support
{
    public class PathExistsValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var val = (value ?? "").ToString();
            if (!MainSettingsModel.IsValidRecordingPath(val))
                return new ValidationResult(false, "Path is not valid or access denied");
            else
                return ValidationResult.ValidResult;
        }
    }
}
