using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace UtiliExtract.Models
{
    public class BillData
    {
        public UsageType UsageType { get; set; }
        public UsageUnit UsageUnit { get; set; }

        [RequiredWithContext(ErrorMessage = "Account number is required")]
        public string? AccountNumber { get; set; }
        public string? Name { get; set; }
        public string? ServiceAddress { get; set; }
        public DateTime? BillingDate { get; set; }
        [RequiredWithContext(ErrorMessage = "Period start is required")]
        public DateTime? DurationStart { get; set; }
        [RequiredWithContext(ErrorMessage = "Period end is required")]
        public DateTime? DurationEnd { get; set; }
        public decimal? Charges { get; set; }
        [RequiredWithContext(ErrorMessage = "Consumption is required")]
        public double? Consumption { get; set; }
        public bool IsMetered { get; set; } = true;
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RequiredWithContextAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext context)
        {
            // 1) Get the bill instance & prefix
            if (context.ObjectInstance is not BillData bill)
            {
                // if somehow applied elsewhere, just do a normal required check
                return value != null
                    ? ValidationResult.Success
                    : new ValidationResult(ErrorMessage ?? "Required", new[] { context.MemberName! });
            }

            //var prefix = bill.Name ?? "(unknown bill)";
            var prefix = $"<a href='invoicepdf#{bill.AccountNumber}'>{bill.Name}</a>" ?? "(unknown bill)";

            // 2) Find the property so we know its declared type
            var prop = context.ObjectType.GetProperty(context.MemberName!,
                                                      BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
            {
                // fallback: require non-null
                if (value != null) return ValidationResult.Success;
                return new ValidationResult($"{prefix} – {ErrorMessage}", new[] { context.MemberName! });
            }

            var propType = prop.PropertyType;
            bool isValid = false;

            // 3) string: non-null, non-whitespace
            if (propType == typeof(string))
            {
                isValid = value is string s && !string.IsNullOrWhiteSpace(s);
            }
            // 4) reference types (other than string): non-null
            else if (!propType.IsValueType)
            {
                isValid = value != null;
            }
            else
            {
                // 5) value or nullable value types: non-default
                //    e.g. null => invalid; 0 or DateTime.MinValue => invalid
                var actualType = Nullable.GetUnderlyingType(propType) ?? propType;
                var defaultValue = Activator.CreateInstance(actualType);
                isValid = value != null && !value.Equals(defaultValue);
            }

            if (isValid)
                return ValidationResult.Success;

            // 6) if we get here, it failed
            return new ValidationResult($"{prefix} – {ErrorMessage}",
                                        new[] { context.MemberName! });
        }
    }
}
