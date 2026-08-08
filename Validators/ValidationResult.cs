namespace ModdingGUI
{
    public enum ValidationSeverity { Info, Warning, Error }

    public record ValidationIssue(string Message, ValidationSeverity Severity);

    public class ValidationResult
    {
        public bool IsValid { get; }
        public IReadOnlyList<ValidationIssue> Issues { get; }
        public int StepCount { get; }

        public ValidationResult(bool isValid, IReadOnlyList<ValidationIssue> issues, int stepCount)
        {
            IsValid = isValid;
            Issues = issues ?? new List<ValidationIssue>();
            StepCount = stepCount;
        }
    }
}
